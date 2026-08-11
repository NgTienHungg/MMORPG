# PHASE 1 — Transport: làm cho byte đi được 2 chiều

> **Kết quả cuối Phase 1:** chạy `dotnet run` ở `Server/GameServer`, bấm Play trong Unity, bấm nút **Ping** →
> server log `nhận cmd 1, 8 byte` → client hiện `RTT: 1 ms`. Đóng server → client tự biết mất kết nối.
>
> **Điều kiện:** đã xong [`PHASE-0.md`](PHASE-0.md) (CHECKPOINT F phải pass — Unity đọc được `MMORPG.Shared.dll`).
>
> **Phase này CHƯA có:** enum lệnh, DTO, dispatch table. Đó là Phase 2. Ở đây `cmd` chỉ là một `int` trần,
> payload chỉ là mảng byte trần. Cố tình như vậy để bạn tập trung vào đúng một bài học: **đóng khung gói tin**.

> **Ghi chú về log:** các đoạn code bên dưới dùng `Console.WriteLine` vì lúc viết phase này
> `MMORPG.ServerCore` chưa tồn tại. Code hiện tại trong repo đã chuyển hết sang `Log.Info(...)` —
> xem [`CONVENTIONS.md` §7](../CONVENTIONS.md#7-log). Khi làm theo tài liệu này, cứ dùng `Log.*`
> thay cho mọi `Console.WriteLine` bạn thấy, và **bỏ phần `[TênClass]`** trong nội dung
> (logger tự chèn).

---

## Luồng sẽ dựng

```
[Nút Ping]  (main thread Unity)
 └─► NetService.Send(cmd: 1, payload: 8 byte timestamp)
      └─► TcpTransport: đóng khung [len][cmd][payload] → đẩy vào hàng đợi gửi
           └─► SendLoop (background): NetworkStream.WriteAsync
                │  TCP
                ▼
           GameServer: AcceptTcpClientAsync → ClientSession.ReadLoop
                └─► FrameReader.Feed(byte vừa đọc) → while TryRead(...) → xử lý
                     └─► cmd 1 (Ping) → gửi lại cmd 2 (Pong) + nguyên payload
                │  TCP
                ▼
      TcpTransport.ReadLoop (background) → FrameReader → event OnPacket
           └─► NetService: await UniTask.SwitchToMainThread()
                └─► NetworkProbe: RTT = now - timestamp → hiện lên UI
```

**Hai điều cốt lõi của phase này:**

1. **TCP không có "gói tin".** Nó là một dòng byte. `Send` 3 lần bên này có thể thành 1 lần `Receive` bên kia,
   hoặc 1 lần `Send` thành 5 lần `Receive`. Ranh giới gói là do **ta tự định nghĩa**, và phải tự gom lại.
2. **Callback socket không ở main thread.** Đụng `Transform`, `Text`, `GameObject` từ đó là crash hoặc
   `UnityException: can only be called from the main thread`. Phải chuyển luồng trước.

---

## Bước 1 — Shared: khung gói tin

**File mới:** `Server/Shared/Net/PacketFrame.cs`

```csharp
using System;
using System.Buffers.Binary;

namespace MMORPG.Shared.Net
{
    /// <summary>
    /// Định nghĩa khung gói tin trên dây. Client và server dùng chung file này,
    /// nên không có cách nào để hai bên hiểu khác nhau về định dạng.
    ///
    /// <code>
    /// ┌─────────────┬─────────────┬────────────────────────┐
    /// │ int32 len   │ int32 cmd   │ payload (len - 4 byte) │
    /// └─────────────┴─────────────┴────────────────────────┘
    ///   len = 4 + payload.Length          little-endian
    /// </code>
    /// </summary>
    public static class PacketFrame
    {
        /// <summary>Số byte của trường độ dài đứng đầu khung.</summary>
        public const int LENGTH_FIELD_SIZE = 4;

        /// <summary>Số byte của mã lệnh.</summary>
        public const int CMD_FIELD_SIZE = 4;

        /// <summary>Tổng phần header: độ dài + mã lệnh.</summary>
        public const int HEADER_SIZE = LENGTH_FIELD_SIZE + CMD_FIELD_SIZE;

        /// <summary>
        /// Chặn trên cho 1 gói. Gói lớn hơn mức này coi như dữ liệu hỏng hoặc bị tấn công —
        /// không cấp phát buffer theo số đọc được từ mạng mà chưa kiểm tra.
        /// </summary>
        public const int MAX_PACKET_SIZE = 1024 * 1024;

        /// <summary>
        /// Đóng khung một gói hoàn chỉnh, sẵn sàng ghi thẳng lên socket.
        /// </summary>
        public static byte[] Encode(int cmd, ReadOnlySpan<byte> payload)
        {
            if (payload.Length + CMD_FIELD_SIZE > MAX_PACKET_SIZE)
                throw new ArgumentException($"Payload {payload.Length} byte vượt giới hạn {MAX_PACKET_SIZE}");

            byte[] frame = new byte[HEADER_SIZE + payload.Length];

            BinaryPrimitives.WriteInt32LittleEndian(
                frame.AsSpan(0, LENGTH_FIELD_SIZE), CMD_FIELD_SIZE + payload.Length);
            BinaryPrimitives.WriteInt32LittleEndian(
                frame.AsSpan(LENGTH_FIELD_SIZE, CMD_FIELD_SIZE), cmd);

            payload.CopyTo(frame.AsSpan(HEADER_SIZE));
            return frame;
        }
    }
}
```

**Vì sao `len` không tính chính nó:** đọc 4 byte đầu ra được `len`, rồi biết còn phải chờ đúng `len` byte nữa.
Nếu `len` tính cả chính nó thì mỗi lần đọc lại phải trừ 4 — thừa một phép tính dễ sai. Cách nào cũng được,
miễn **hai bên thống nhất**; đây là lý do file này nằm ở `Shared`.

**Vì sao little-endian tường minh:** `BitConverter` phụ thuộc endianness của máy. Trên x86/ARM hiện nay đều
little-endian nên có vẻ vô hại — nhưng "có vẻ vô hại" là cách bug ẩn 2 năm rồi nổ khi đổi nền tảng.
`BinaryPrimitives.WriteInt32LittleEndian` nói rõ ý định, không phụ thuộc máy.

---

## Bước 2 — Shared: bộ gom byte thành gói (phần khó nhất)

**File mới:** `Server/Shared/Net/FrameReader.cs`

```csharp
using System;
using System.Buffers.Binary;
using System.IO;

namespace MMORPG.Shared.Net
{
    /// <summary>
    /// Gom dòng byte đọc từ socket thành từng gói hoàn chỉnh.
    ///
    /// Mỗi lần đọc được byte từ mạng thì <see cref="Feed"/> vào đây, rồi gọi <see cref="TryRead"/>
    /// trong vòng lặp cho tới khi trả về false — một lần đọc mạng có thể chứa 0, 1 hoặc nhiều gói,
    /// và gói cuối thường bị cắt dở.
    ///
    /// KHÔNG thread-safe: mỗi kết nối một instance, chỉ dùng trong đúng luồng đọc của kết nối đó.
    /// </summary>
    public sealed class FrameReader
    {
        private readonly int _maxPacketSize;
        private byte[] _buffer;

        /// <summary>Số byte hợp lệ đang giữ, tính từ đầu <see cref="_buffer"/>.</summary>
        private int _count;

        public FrameReader(int initialCapacity = 4096, int maxPacketSize = PacketFrame.MAX_PACKET_SIZE)
        {
            _buffer = new byte[initialCapacity];
            _maxPacketSize = maxPacketSize;
        }

        /// <summary>Nạp thêm byte vừa đọc được từ socket.</summary>
        public void Feed(byte[] data, int offset, int length)
        {
            EnsureCapacity(_count + length);
            Buffer.BlockCopy(data, offset, _buffer, _count, length);
            _count += length;
        }

        /// <summary>
        /// Lấy ra một gói hoàn chỉnh nếu có đủ byte.
        /// </summary>
        /// <returns>false nghĩa là "chưa đủ, chờ thêm dữ liệu" — không phải lỗi.</returns>
        /// <exception cref="InvalidDataException">Độ dài đọc được vô lý → dòng byte đã hỏng, phải ngắt kết nối.</exception>
        public bool TryRead(out int cmd, out byte[] payload)
        {
            cmd = 0;
            payload = null;

            // chưa đọc nổi trường độ dài
            if (_count < PacketFrame.LENGTH_FIELD_SIZE)
                return false;

            int bodyLength = BinaryPrimitives.ReadInt32LittleEndian(
                _buffer.AsSpan(0, PacketFrame.LENGTH_FIELD_SIZE));

            // Kiểm tra TRƯỚC khi cấp phát. Không có bước này thì chỉ cần gửi 4 byte 0x7FFFFFFF
            // là ép server cấp phát 2GB — một kiểu tấn công rẻ tiền mà hiệu quả.
            if (bodyLength < PacketFrame.CMD_FIELD_SIZE || bodyLength > _maxPacketSize)
                throw new InvalidDataException($"Độ dài gói không hợp lệ: {bodyLength}");

            int totalLength = PacketFrame.LENGTH_FIELD_SIZE + bodyLength;

            // gói còn dở — chờ lần Feed sau
            if (_count < totalLength)
                return false;

            cmd = BinaryPrimitives.ReadInt32LittleEndian(
                _buffer.AsSpan(PacketFrame.LENGTH_FIELD_SIZE, PacketFrame.CMD_FIELD_SIZE));

            int payloadLength = bodyLength - PacketFrame.CMD_FIELD_SIZE;
            payload = new byte[payloadLength];
            Buffer.BlockCopy(_buffer, PacketFrame.HEADER_SIZE, payload, 0, payloadLength);

            // dồn phần byte của (các) gói sau về đầu buffer
            int remain = _count - totalLength;
            if (remain > 0)
                Buffer.BlockCopy(_buffer, totalLength, _buffer, 0, remain);
            _count = remain;

            return true;
        }

        private void EnsureCapacity(int needed)
        {
            if (_buffer.Length >= needed)
                return;

            int newSize = _buffer.Length;
            while (newSize < needed)
                newSize *= 2;

            Array.Resize(ref _buffer, newSize);
        }
    }
}
```

### Vì sao phải copy payload ra mảng mới?
`_buffer` là buffer **dùng lại** — lần `Feed` sau sẽ ghi đè lên nó. Nếu trả về `ArraySegment` trỏ vào đó,
handler giữ lại tham chiếu rồi đọc sau là đọc phải rác. `vo-lam-genz` cũng gặp đúng bài này và giải đúng cách:
`RpcData` copy byte ra ngay trong constructor, kèm comment *"rpc data khi chạy qua logic này đều phải copy data
trong nó ra chứ không được giữ reference cũ"*.

Cái giá là mỗi gói tốn 1 lần cấp phát. Với vài chục gói/giây thì không đáng bận tâm. Khi nào đo được rằng nó
thành vấn đề (Phase 13) thì mới tối ưu bằng `ArrayPool`. **Đừng tối ưu trước khi đo.**

### ✅ CHECKPOINT A — viết test cho `FrameReader` trước khi đụng socket

Đây là logic dễ sai nhất trong cả phase, và nó là code thuần — test được mà không cần mạng.

```bash
cd /Users/ngtienhungg/Documents/UnityProjects/MMORPG/Server
dotnet new xunit -o Shared.Tests -n MMORPG.Shared.Tests -f net8.0
dotnet sln add Shared.Tests/MMORPG.Shared.Tests.csproj
dotnet add Shared.Tests/MMORPG.Shared.Tests.csproj reference Shared/MMORPG.Shared.csproj
rm Shared.Tests/UnitTest1.cs
```

`Server/Shared.Tests/FrameReaderTests.cs`:
```csharp
using System.Text;
using MMORPG.Shared.Net;
using Xunit;

namespace MMORPG.Shared.Tests;

public class FrameReaderTests
{
    private static byte[] Frame(int cmd, string text) =>
        PacketFrame.Encode(cmd, Encoding.UTF8.GetBytes(text));

    [Fact]
    public void DocDuocGoiNguyenVen()
    {
        var reader = new FrameReader();
        byte[] frame = Frame(7, "hello");

        reader.Feed(frame, 0, frame.Length);

        Assert.True(reader.TryRead(out int cmd, out byte[] payload));
        Assert.Equal(7, cmd);
        Assert.Equal("hello", Encoding.UTF8.GetString(payload));
        Assert.False(reader.TryRead(out _, out _));
    }

    [Fact]
    public void GoiBiCatLamNhieuLan_VanGhepDuoc()
    {
        var reader = new FrameReader();
        byte[] frame = Frame(7, "hello");

        // mô phỏng TCP cắt gói: đưa vào từng byte một
        for (int i = 0; i < frame.Length - 1; i++)
        {
            reader.Feed(frame, i, 1);
            Assert.False(reader.TryRead(out _, out _));   // chưa đủ thì không được trả gói
        }

        reader.Feed(frame, frame.Length - 1, 1);
        Assert.True(reader.TryRead(out int cmd, out byte[] payload));
        Assert.Equal("hello", Encoding.UTF8.GetString(payload));
    }

    [Fact]
    public void NhieuGoiDinhLien_TachDuocHet()
    {
        var reader = new FrameReader();
        byte[] a = Frame(1, "one");
        byte[] b = Frame(2, "two");
        byte[] merged = new byte[a.Length + b.Length];
        a.CopyTo(merged, 0);
        b.CopyTo(merged, a.Length);

        reader.Feed(merged, 0, merged.Length);

        Assert.True(reader.TryRead(out int cmd1, out byte[] p1));
        Assert.True(reader.TryRead(out int cmd2, out byte[] p2));
        Assert.False(reader.TryRead(out _, out _));

        Assert.Equal(1, cmd1);
        Assert.Equal("one", Encoding.UTF8.GetString(p1));
        Assert.Equal(2, cmd2);
        Assert.Equal("two", Encoding.UTF8.GetString(p2));
    }

    [Fact]
    public void PayloadRong_VanHopLe()
    {
        var reader = new FrameReader();
        byte[] frame = PacketFrame.Encode(99, System.ReadOnlySpan<byte>.Empty);

        reader.Feed(frame, 0, frame.Length);

        Assert.True(reader.TryRead(out int cmd, out byte[] payload));
        Assert.Equal(99, cmd);
        Assert.Empty(payload);
    }

    [Fact]
    public void DoDaiVoLy_ThiNem()
    {
        var reader = new FrameReader();
        byte[] rac = { 0xFF, 0xFF, 0xFF, 0x7F };   // ~2GB

        reader.Feed(rac, 0, rac.Length);

        Assert.Throws<System.IO.InvalidDataException>(() => reader.TryRead(out _, out _));
    }
}
```

```bash
dotnet test
```
**Phải pass 5/5.** Nếu test "cắt từng byte" fail → bạn đang trả gói khi chưa đủ byte, và đó chính là con bug
sẽ khiến bạn mất cả buổi ở bước sau.

---

## Bước 3 — Server: lắng nghe và đọc

**File:** `Server/GameServer/Program.cs` — thay toàn bộ

```csharp
using System.Net;
using System.Net.Sockets;
using MMORPG.GameServer;

const int PORT = 7777;

var listener = new TcpListener(IPAddress.Any, PORT);
listener.Start();
Console.WriteLine($"[GameServer] Lắng nghe trên 0.0.0.0:{PORT}");

// Ctrl+C để dừng sạch thay vì kill process
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;      // chặn hành vi kill mặc định
    cts.Cancel();
};

try
{
    while (!cts.IsCancellationRequested)
    {
        TcpClient tcpClient = await listener.AcceptTcpClientAsync(cts.Token);

        // Mỗi kết nối chạy độc lập. KHÔNG await ở đây — await là chỉ phục vụ được 1 client.
        var session = new ClientSession(tcpClient);
        _ = session.RunAsync(cts.Token);
    }
}
catch (OperationCanceledException)
{
    // dừng theo yêu cầu, không phải lỗi
}
finally
{
    listener.Stop();
    Console.WriteLine("[GameServer] Đã dừng.");
}
```

**File mới:** `Server/GameServer/ClientSession.cs`

```csharp
using System.Collections.Concurrent;
using System.Net.Sockets;
using MMORPG.Shared.Net;

namespace MMORPG.GameServer;

/// <summary>
/// Một kết nối client: vòng đọc, vòng gửi, và vòng đời.
/// Phase 1 chưa gắn với người chơi nào — chỉ echo. Phase 4 sẽ gắn account, Phase 5 gắn nhân vật.
/// </summary>
public sealed class ClientSession
{
    private static int _nextId;

    private readonly TcpClient _tcpClient;
    private readonly NetworkStream _stream;
    private readonly FrameReader _frameReader = new();

    private readonly ConcurrentQueue<byte[]> _sendQueue = new();
    private readonly SemaphoreSlim _sendSignal = new(0);

    public int Id { get; }

    public ClientSession(TcpClient tcpClient)
    {
        _tcpClient = tcpClient;
        _tcpClient.NoDelay = true;   // tắt Nagle: game cần độ trễ thấp hơn là gộp gói cho hiệu quả
        _stream = tcpClient.GetStream();
        Id = Interlocked.Increment(ref _nextId);
    }

    public async Task RunAsync(CancellationToken ct)
    {
        Console.WriteLine($"[Session {Id}] Kết nối từ {_tcpClient.Client.RemoteEndPoint}");

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);

        Task sendLoop = SendLoopAsync(linked.Token);

        try
        {
            await ReadLoopAsync(linked.Token);
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
        {
            Console.WriteLine($"[Session {Id}] Mất kết nối: {ex.GetType().Name}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Session {Id}] Lỗi: {ex}");
        }
        finally
        {
            linked.Cancel();                    // dừng vòng gửi
            _sendSignal.Release();              // đánh thức nó để nó thấy token đã huỷ
            await Task.WhenAny(sendLoop, Task.Delay(1000, CancellationToken.None));

            _tcpClient.Dispose();
            Console.WriteLine($"[Session {Id}] Đóng.");
        }
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        byte[] buffer = new byte[8192];

        while (!ct.IsCancellationRequested)
        {
            int read = await _stream.ReadAsync(buffer, 0, buffer.Length, ct);

            // 0 byte = phía kia đã đóng kết nối một cách bình thường
            if (read == 0)
                break;

            _frameReader.Feed(buffer, 0, read);

            // MỘT lần đọc có thể chứa nhiều gói → phải vắt cạn bằng vòng while
            while (_frameReader.TryRead(out int cmd, out byte[] payload))
                HandlePacket(cmd, payload);
        }
    }

    private void HandlePacket(int cmd, byte[] payload)
    {
        Console.WriteLine($"[Session {Id}] nhận cmd {cmd}, {payload.Length} byte");

        // Phase 1: chỉ có Ping(1) → Pong(2), echo nguyên payload để client tính RTT.
        // Phase 2 sẽ thay chỗ này bằng dispatch table.
        const int CMD_PING = 1;
        const int CMD_PONG = 2;

        if (cmd == CMD_PING)
            Send(CMD_PONG, payload);
    }

    /// <summary>
    /// Gửi gói tin. Gọi được từ bất kỳ luồng nào — gói được xếp hàng, một vòng gửi riêng lo ghi socket.
    /// </summary>
    public void Send(int cmd, ReadOnlySpan<byte> payload)
    {
        _sendQueue.Enqueue(PacketFrame.Encode(cmd, payload));
        _sendSignal.Release();
    }

    private async Task SendLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await _sendSignal.WaitAsync(ct);

                while (_sendQueue.TryDequeue(out byte[]? frame))
                    await _stream.WriteAsync(frame, 0, frame.Length, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
        {
            // kết nối đã chết, vòng đọc sẽ xử lý phần dọn dẹp
        }
    }
}
```

### Vì sao phải có hàng đợi gửi riêng?
`NetworkStream.WriteAsync` **không** an toàn khi hai luồng gọi cùng lúc — hai gói sẽ đan xen byte vào nhau
và bên kia nhận được rác không thể giải mã. Mà trong game thì việc nhiều luồng cùng muốn gửi cho một client
là chuyện thường (worker AOI, worker chat, handler…). Hàng đợi + một vòng gửi duy nhất là cách rẻ nhất để
đảm bảo tại một thời điểm chỉ có đúng một chỗ ghi vào socket.

`vo-lam-genz` cũng có hàng đợi (`_packetQueue` + `BatchSend`), nhưng của họ còn gộp nhiều gói nhỏ thành lô
250ms để giảm số lần syscall. Ta chưa cần — độ trễ quan trọng hơn ở giai đoạn học.

### ✅ CHECKPOINT B

```bash
cd /Users/ngtienhungg/Documents/UnityProjects/MMORPG/Server
dotnet build
dotnet run --project GameServer
```
Thấy: `[GameServer] Lắng nghe trên 0.0.0.0:7777`

Mở terminal thứ 2, thử bằng netcat (chưa cần Unity):
```bash
nc localhost 7777
```
Terminal server phải in `[Session 1] Kết nối từ 127.0.0.1:xxxxx`. Nhấn Ctrl+C ở netcat → server in `[Session 1] Đóng.`

---

## Bước 4 — Client: `ITransport` + `TcpTransport`

**File mới:** `Assets/Game/Scripts/Network/ITransport.cs`

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace MMORPG.Client.Network
{
    public enum TransportState
    {
        Disconnected,
        Connecting,
        Connected
    }

    /// <summary>
    /// Tầng vận chuyển byte thuần. KHÔNG biết gì về game, về lệnh, về DTO.
    ///
    /// Có interface để: (1) test được bằng transport giả, (2) sau này đổi sang WebSocket/KCP
    /// mà không phải sửa một dòng nào ở tầng trên.
    /// </summary>
    public interface ITransport : IDisposable
    {
        TransportState State { get; }

        /// <summary>Đổi trạng thái kết nối. CHẠY Ở BACKGROUND THREAD.</summary>
        event Action<TransportState> OnStateChanged;

        /// <summary>Nhận được một gói hoàn chỉnh. CHẠY Ở BACKGROUND THREAD — không đụng Unity API ở đây.</summary>
        event Action<int, byte[]> OnPacket;

        UniTask<bool> ConnectAsync(string host, int port, CancellationToken ct);

        /// <summary>Gửi gói. Gọi được từ mọi luồng.</summary>
        void Send(int cmd, ReadOnlySpan<byte> payload);

        void Disconnect();
    }
}
```

**File mới:** `Assets/Game/Scripts/Network/TcpTransport.cs`

```csharp
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MMORPG.Shared.Net;
using UnityEngine;

namespace MMORPG.Client.Network
{
    /// <summary>
    /// Transport TCP cho client. Đọc và gửi đều chạy ở luồng nền;
    /// việc chuyển về main thread là trách nhiệm của tầng trên (<see cref="NetService"/>).
    /// </summary>
    public sealed class TcpTransport : ITransport
    {
        private readonly ConcurrentQueue<byte[]> _sendQueue = new();
        private readonly SemaphoreSlim _sendSignal = new(0);
        private readonly FrameReader _frameReader = new();

        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private CancellationTokenSource _cts;

        private int _state = (int)TransportState.Disconnected;

        public TransportState State => (TransportState)Volatile.Read(ref _state);

        public event Action<TransportState> OnStateChanged;
        public event Action<int, byte[]> OnPacket;

        public async UniTask<bool> ConnectAsync(string host, int port, CancellationToken ct)
        {
            if (State != TransportState.Disconnected)
            {
                Debug.LogWarning($"[TcpTransport] Đang ở trạng thái {State}, bỏ qua yêu cầu connect.");
                return false;
            }

            SetState(TransportState.Connecting);

            try
            {
                _tcpClient = new TcpClient { NoDelay = true };
                await _tcpClient.ConnectAsync(host, port);

                _stream = _tcpClient.GetStream();
                _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

                SetState(TransportState.Connected);

                // Không await: hai vòng lặp này sống suốt phiên kết nối.
                _ = Task.Run(() => ReadLoopAsync(_cts.Token));
                _ = Task.Run(() => SendLoopAsync(_cts.Token));

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TcpTransport] Kết nối {host}:{port} thất bại — {ex.Message}");
                Cleanup();
                return false;
            }
        }

        public void Send(int cmd, ReadOnlySpan<byte> payload)
        {
            if (State != TransportState.Connected)
            {
                Debug.LogWarning($"[TcpTransport] Chưa kết nối, bỏ gói cmd {cmd}.");
                return;
            }

            _sendQueue.Enqueue(PacketFrame.Encode(cmd, payload));
            _sendSignal.Release();
        }

        public void Disconnect()
        {
            if (State == TransportState.Disconnected)
                return;

            Cleanup();
        }

        public void Dispose() => Disconnect();

        private async Task ReadLoopAsync(CancellationToken ct)
        {
            byte[] buffer = new byte[8192];

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    int read = await _stream.ReadAsync(buffer, 0, buffer.Length, ct);

                    if (read == 0)
                        break;   // server đóng kết nối

                    _frameReader.Feed(buffer, 0, read);

                    while (_frameReader.TryRead(out int cmd, out byte[] payload))
                        OnPacket?.Invoke(cmd, payload);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
                // mất kết nối — coi là bình thường, xử lý ở finally
            }
            catch (InvalidDataException ex)
            {
                Debug.LogError($"[TcpTransport] Dòng byte hỏng, buộc ngắt kết nối — {ex.Message}");
            }
            finally
            {
                Cleanup();
            }
        }

        private async Task SendLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await _sendSignal.WaitAsync(ct);

                    while (_sendQueue.TryDequeue(out byte[] frame))
                        await _stream.WriteAsync(frame, 0, frame.Length, ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
                // vòng đọc lo phần dọn dẹp
            }
        }

        private void Cleanup()
        {
            if (Interlocked.Exchange(ref _state, (int)TransportState.Disconnected)
                == (int)TransportState.Disconnected)
                return;   // đã dọn rồi, tránh dọn 2 lần từ 2 luồng

            try { _cts?.Cancel(); } catch { /* đã dispose */ }
            _sendSignal.Release();

            _stream?.Dispose();
            _tcpClient?.Dispose();

            _stream = null;
            _tcpClient = null;

            while (_sendQueue.TryDequeue(out _)) { }

            OnStateChanged?.Invoke(TransportState.Disconnected);
        }

        private void SetState(TransportState next)
        {
            Volatile.Write(ref _state, (int)next);
            OnStateChanged?.Invoke(next);
        }
    }
}
```

### Ba chỗ dễ sai, để ý kỹ
1. **`Volatile.Read` / `Interlocked.Exchange` cho `_state`.** Trường này bị đọc/ghi từ nhiều luồng
   (main thread gọi `Send`, luồng đọc gọi `Cleanup`). Đọc thường có thể thấy giá trị cũ do cache CPU.
2. **`Cleanup` phải idempotent.** Vòng đọc và vòng gửi cùng chết một lúc là chuyện bình thường —
   `Interlocked.Exchange` đảm bảo chỉ luồng đầu tiên thực sự dọn.
3. **`OnPacket` chạy ở luồng nền.** Đừng đụng Unity API ở đây. Chuyển luồng là việc của `NetService` ngay dưới.

---

## Bước 5 — Client: `NetService` (nơi chuyển về main thread)

**File mới:** `Assets/Game/Scripts/Network/NetService.cs`

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

namespace MMORPG.Client.Network
{
    /// <summary>
    /// Điểm vào mạng của client. Sở hữu transport, và đảm bảo mọi thứ đi lên tầng trên
    /// đều đã ở main thread — nhờ vậy tầng game phía trên không bao giờ phải nghĩ về luồng.
    /// </summary>
    public sealed class NetService : IDisposable
    {
        private readonly ITransport _transport;
        private readonly CancellationTokenSource _cts = new();

        /// <summary>Nhận gói tin. Đã ở MAIN THREAD — đụng Unity API thoải mái.</summary>
        public event Action<int, byte[]> OnPacket;

        /// <summary>Đổi trạng thái kết nối. Đã ở MAIN THREAD.</summary>
        public event Action<TransportState> OnStateChanged;

        public TransportState State => _transport.State;

        public NetService(ITransport transport)
        {
            _transport = transport;
            _transport.OnPacket += HandlePacketFromBackground;
            _transport.OnStateChanged += HandleStateFromBackground;
        }

        public UniTask<bool> ConnectAsync(string host, int port) =>
            _transport.ConnectAsync(host, port, _cts.Token);

        public void Send(int cmd, ReadOnlySpan<byte> payload) => _transport.Send(cmd, payload);

        public void Disconnect() => _transport.Disconnect();

        public void Dispose()
        {
            _transport.OnPacket -= HandlePacketFromBackground;
            _transport.OnStateChanged -= HandleStateFromBackground;

            _cts.Cancel();
            _cts.Dispose();
            _transport.Dispose();
        }

        private void HandlePacketFromBackground(int cmd, byte[] payload) =>
            RaiseOnMainThread(cmd, payload).Forget();

        private async UniTaskVoid RaiseOnMainThread(int cmd, byte[] payload)
        {
            await UniTask.SwitchToMainThread();
            OnPacket?.Invoke(cmd, payload);
        }

        private void HandleStateFromBackground(TransportState state) =>
            RaiseStateOnMainThread(state).Forget();

        private async UniTaskVoid RaiseStateOnMainThread(TransportState state)
        {
            await UniTask.SwitchToMainThread();
            OnStateChanged?.Invoke(state);
        }
    }
}
```

> `await UniTask.SwitchToMainThread()` là bản gọn của `MainGame.QueueOnMainThread` trong `vo-lam-genz`.
> Cùng ý tưởng: xếp việc vào hàng đợi để main thread rút ra chạy. Khác ở chỗ ta không phải tự viết
> và tự nhớ gọi ở từng chỗ — nó nằm đúng **một** nơi, mọi gói đi qua đây đều được xử lý.

### Đăng ký vào container

Sửa `Assets/Game/Scripts/Boot/GameLifetimeScope.cs`:

```csharp
using HungNT.Core;
using MMORPG.Client.Network;
using VContainer;
using VContainer.Unity;

namespace MMORPG.Client.Boot
{
    public class GameLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.InstallCore();

            builder.Register<ITransport, TcpTransport>(Lifetime.Singleton);
            builder.Register<NetService>(Lifetime.Singleton);
        }
    }
}
```

---

## Bước 6 — UI thử: nút Ping + hiện RTT

**File mới:** `Assets/Game/Scripts/Network/NetworkProbe.cs`

```csharp
using System;
using System.Buffers.Binary;
using System.Diagnostics;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using Debug = UnityEngine.Debug;

namespace MMORPG.Client.Network
{
    /// <summary>
    /// UI tạm của Phase 1: kết nối, ping, đo RTT. Sẽ bị thay ở Phase 4 bằng UI login thật.
    /// </summary>
    public class NetworkProbe : MonoBehaviour
    {
        private const int CMD_PING = 1;
        private const int CMD_PONG = 2;

        [SerializeField] private string _host = "127.0.0.1";
        [SerializeField] private int _port = 7777;

        [SerializeField] private Button _connectButton;
        [SerializeField] private Button _pingButton;
        [SerializeField] private TextMeshProUGUI _statusText;

        private NetService _net;
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        [Inject]
        public void Construct(NetService net) => _net = net;

        private void Awake()
        {
            _connectButton.onClick.AddListener(() => ConnectAsync().Forget());
            _pingButton.onClick.AddListener(SendPing);

            _net.OnPacket += OnPacket;
            _net.OnStateChanged += OnStateChanged;

            SetStatus("Chưa kết nối");
        }

        private void OnDestroy()
        {
            if (_net == null)
                return;

            _net.OnPacket -= OnPacket;
            _net.OnStateChanged -= OnStateChanged;
        }

        private async UniTaskVoid ConnectAsync()
        {
            SetStatus($"Đang kết nối {_host}:{_port}...");
            bool ok = await _net.ConnectAsync(_host, _port);
            if (!ok)
                SetStatus("Kết nối thất bại");
        }

        private void SendPing()
        {
            Span<byte> payload = stackalloc byte[8];
            BinaryPrimitives.WriteInt64LittleEndian(payload, _clock.ElapsedMilliseconds);

            _net.Send(CMD_PING, payload);
            Debug.Log("[Probe] Gửi Ping");
        }

        private void OnPacket(int cmd, byte[] payload)
        {
            if (cmd != CMD_PONG || payload.Length < 8)
                return;

            long sentAt = BinaryPrimitives.ReadInt64LittleEndian(payload);
            long rtt = _clock.ElapsedMilliseconds - sentAt;

            SetStatus($"Đã kết nối · RTT: {rtt} ms");
        }

        private void OnStateChanged(TransportState state) => SetStatus(state.ToString());

        private void SetStatus(string text)
        {
            _statusText.text = text;
            Debug.Log($"[Probe] {text}");
        }
    }
}
```

### Dựng UI trong scene
Trong `Bootstrap.unity`, dưới `UIRoot`:
1. Chuột phải `UIRoot` → UI → **Button - TextMeshPro**, đổi tên `ConnectButton`, sửa chữ thành `Connect`.
2. Tương tự tạo `PingButton`, chữ `Ping`. Kéo nó xuống dưới nút Connect.
3. Chuột phải `UIRoot` → UI → **Text - TextMeshPro**, đổi tên `StatusText`, kéo rộng ra, cỡ chữ ~36.
4. Tạo GameObject rỗng tên `NetworkProbe`, gắn script `NetworkProbe`, kéo 3 object trên vào 3 ô tương ứng.
5. Chọn `GameLifetimeScope` → mục **Auto Inject Game Objects** → `+` → kéo `NetworkProbe` vào.

> Bước 5 là chỗ hay quên: VContainer chỉ inject vào MonoBehaviour nếu bạn nói cho nó biết object nào cần inject.
> Quên bước này → `_net` là `null` → `NullReferenceException` ở `Awake`.

---

## Bước 7 — Chạy thử end-to-end

Terminal:
```bash
cd /Users/ngtienhungg/Documents/UnityProjects/MMORPG/Server
dotnet run --project GameServer
```

Unity: bấm Play → bấm **Connect** → bấm **Ping**.

### ✅ CHECKPOINT C — mục tiêu cuối Phase 1

| Nơi | Phải thấy |
|-----|-----------|
| Console server | `[Session 1] Kết nối từ 127.0.0.1:...` rồi `[Session 1] nhận cmd 1, 8 byte` |
| Console Unity | `[Probe] Gửi Ping` |
| UI | `Đã kết nối · RTT: 0 ms` (hoặc 1–2 ms) |

### Thử 3 tình huống hỏng (quan trọng không kém lúc chạy đúng)

1. **Tắt server khi client đang kết nối** (Ctrl+C ở terminal)
   → UI phải chuyển sang `Disconnected` trong vòng ~1 giây, Unity **không** treo, **không** spam exception.
2. **Bấm Ping khi chưa Connect**
   → Console warning `Chưa kết nối, bỏ gói cmd 1`, không exception.
3. **Bấm Ping 20 lần thật nhanh**
   → server log đủ 20 dòng, UI cập nhật RTT bình thường. Nếu thiếu dòng hoặc server ném `InvalidDataException`
   thì `FrameReader` hoặc hàng đợi gửi đang sai — quay lại CHECKPOINT A.

---

## Troubleshooting

| Triệu chứng | Nguyên nhân thường gặp | Xử lý |
|-------------|------------------------|-------|
| `SocketException: Connection refused` | Server chưa chạy, hoặc sai port | Kiểm tra terminal server; `lsof -i :7777` |
| Client nối được nhưng Ping không thấy gì | Quên vòng `while (TryRead(...))`, chỉ đọc 1 gói rồi thôi | Xem lại `ReadLoopAsync` |
| `InvalidDataException: Độ dài gói không hợp lệ` ngay gói đầu | Hai bên lệch quy ước `len` (tính cả header hay không), hoặc lệch endianness | Cả hai bên phải dùng chung `PacketFrame` từ `Shared` — nếu bạn viết lại tay ở client là sai ngay chỗ này |
| `UnityException: ... can only be called from the main thread` | Đụng Unity API trong `OnPacket` của transport thay vì của `NetService` | Chỉ subscribe `NetService.OnPacket`, đừng subscribe thẳng `ITransport.OnPacket` |
| `NullReferenceException` ở `NetworkProbe.Awake` | Quên thêm `NetworkProbe` vào *Auto Inject Game Objects* của LifetimeScope | Làm lại Bước 6.5 |
| Unity treo khi thoát Play mode | Vòng lặp nền không dừng theo CancellationToken | `Dispose` của `NetService` phải được gọi — VContainer tự gọi khi scope huỷ; kiểm tra `NetService` đăng ký là `Lifetime.Singleton` trong scope, không phải `new` tay |
| Đổi code server mà không thấy tác dụng | `dotnet run` vẫn đang chạy bản cũ | Ctrl+C rồi `dotnet run` lại |
| RTT âm hoặc rất lớn | Dùng `Time.time` thay `Stopwatch`, hoặc so mốc thời gian giữa 2 máy khác nhau | Mốc thời gian phải do **cùng một máy** sinh ra — ở đây client sinh, server chỉ echo lại |

---

## Tự kiểm tra hiểu bài

1. Server gọi `stream.WriteAsync` một lần với 500 byte. Client có chắc chắn nhận được đúng một lần `ReadAsync` trả về 500 byte không? Vì sao?
2. Vì sao `TryRead` trả `false` **không phải** là lỗi, nhưng `InvalidDataException` thì buộc phải ngắt kết nối?
3. Nếu bỏ kiểm tra `bodyLength > _maxPacketSize`, kẻ tấn công làm được gì chỉ với 4 byte?
4. Vì sao `FrameReader` phải copy payload ra mảng mới thay vì trả về `ArraySegment` trỏ vào buffer nội bộ?
5. Hai worker cùng gọi `session.Send(...)` một lúc. Nếu bỏ hàng đợi mà ghi thẳng `WriteAsync` thì hỏng thế nào?
6. `NetService` giải quyết đúng một vấn đề mà `TcpTransport` cố tình không giải quyết. Vấn đề gì, và vì sao nên tách?
7. Nếu mai bạn muốn đổi từ TCP sang WebSocket, những file nào phải sửa? (Đáp án đúng chỉ có **một** file.)

---

**Xong Phase 1 → [`PHASE-2.md`](PHASE-2.md): biến `int cmd` + `byte[]` trần thành contract có kiểu và dispatch table.**
