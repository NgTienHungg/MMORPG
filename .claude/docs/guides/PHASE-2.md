# PHASE 2 — Contract & Dispatch: hết `int` trần, hết `if/else`

> **Kết quả cuối Phase 2:** client gửi `EchoRequest { Message = "xin chào" }`, server trả
> `EchoResponse { Message = "xin chào", ServerTimeMs = ... }`, UI hiện nội dung.
> Không có một `if (cmd == ...)` nào trong đường đi của gói tin. Thêm một lệnh mới = thêm 1 enum + 1 DTO + 1 method,
> **không sửa file dùng chung nào**.
>
> **Điều kiện:** xong [`PHASE-1.md`](PHASE-1.md) tới CHECKPOINT C.
>
> **Đây là phase quan trọng nhất của cả dự án.** Mọi feature từ Phase 4 trở đi đều đi đúng đường ống dựng ở đây.
> Làm ẩu chỗ này thì 12 phase sau phải chịu hậu quả.

---

## Vấn đề đang có sau Phase 1

```csharp
// ClientSession.HandlePacket — Phase 1
if (cmd == CMD_PING)
    Send(CMD_PONG, payload);
```

Nhìn thì vô hại. Nhưng thêm 200 lệnh nữa thì nó chính là `PlayZone_Network_Switch.cs` của `vo-lam-genz`:
một file, ~1000 `case`, ai thêm feature cũng phải sửa, ai cũng conflict với nhau.

Và `byte[] payload` trần nghĩa là **mỗi handler tự đoán** trong đó có gì. Đọc sai vài byte thì không có
lỗi compile nào cả — chỉ có dữ liệu sai âm thầm.

Phase này giải hai vấn đề đó bằng **contract có kiểu** + **dispatch table**.

---

## Luồng sẽ dựng

```
Client
 └─► EchoApi.Send("xin chào")
      └─► NetService.Send(NetCmd.Echo, new EchoRequest{...})
           └─► NetPayload.Serialize → [flag][MemoryPack bytes] → PacketFrame.Encode
                │ TCP
                ▼
Server
 └─► ClientSession.HandlePacket(cmd, payload)
      └─► TcpDispatcher.Dispatch(cmd, request)      ← Dictionary<NetCmd, handler>, KHÔNG if/else
           └─► [TcpHandler(NetCmd.Echo)] EchoHandler.Handle(req)
                └─► req.GetData<EchoRequest>() → xử lý → NetResult.Ok(new EchoResponse{...})
                     └─► dispatcher tự gửi trả về ĐÚNG cmd vừa nhận
                │ TCP
                ▼
Client
 └─► NetService (đã SwitchToMainThread) → NetDispatcher.Dispatch
      └─► [NetHandler(NetCmd.Echo)] EchoNetHandler.OnEcho(packet)
           └─► packet.GetData<EchoResponse>() → bắn event → UI
```

**Quy ước quan trọng:** response đi về **đúng cmd của request**. `NetCmd.Echo` dùng cho cả 2 chiều.
Nhờ vậy enum không phình gấp đôi và cặp request/response luôn hiển nhiên.
(Đây là quy ước của `vo-lam-genz` — `TcpResult` echo về đúng cmd vừa nhận. Phần này họ làm đúng.)

---

## Bước 1 — Shared: `NetCmd` và `ErrorCode`

**File mới:** `Server/Shared/Net/NetCmd.cs`

```csharp
namespace MMORPG.Shared.Net
{
    /// <summary>
    /// Mã lệnh trên dây. Client và server dùng chung enum này (qua MMORPG.Shared.dll)
    /// nên không tồn tại khả năng lệch số.
    ///
    /// Quy hoạch dải — xem ROADMAP.md §2:
    ///   1–99     hệ thống
    ///   100–199  auth
    ///   200–299  character
    ///   300–399  world / movement
    ///   400–499  inventory
    ///   500–599  combat
    ///   600–699  chat
    ///   1000+    nội bộ GameServer ↔ DBServer (client không bao giờ thấy)
    ///
    /// Thêm lệnh mới: luôn thêm vào CUỐI dải của feature. Không chèn giữa, không tái dùng số đã xoá.
    /// </summary>
    public enum NetCmd
    {
        /// <summary>Giá trị vô hiệu. Dùng làm "không có response".</summary>
        None = 0,

        #region Hệ thống (1–99)

        /// <summary>
        /// Đo độ trễ. Request: <see cref="Dto.PingRequest"/> · Response: <see cref="Dto.PingResponse"/>
        /// Client chủ động gửi.
        /// </summary>
        Ping = 1,

        /// <summary>
        /// Server báo lỗi cho một request. Chỉ server gửi.
        /// Payload: <see cref="Dto.ErrorResponse"/>
        /// </summary>
        Error = 2,

        /// <summary>
        /// Lệnh thử của Phase 2. Request/Response: <see cref="Dto.EchoRequest"/> / <see cref="Dto.EchoResponse"/>
        /// Xoá khi Phase 4 xong.
        /// </summary>
        Echo = 3,

        #endregion
    }
}
```

**File mới:** `Server/Shared/Net/ErrorCode.cs`

```csharp
namespace MMORPG.Shared.Net
{
    /// <summary>
    /// Mã lỗi nghiệp vụ. Lỗi nghiệp vụ KHÔNG ném exception — trả mã này về cho client.
    /// Exception chỉ dành cho lỗi hệ thống (packet hỏng, mất DB).
    /// </summary>
    public enum ErrorCode
    {
        None = 0,

        /// <summary>Server không có handler cho lệnh này.</summary>
        UnknownCommand = 1,

        /// <summary>Payload không giải mã được — sai kiểu DTO hoặc contract lệch.</summary>
        MalformedPayload = 2,

        /// <summary>Handler ném exception ngoài dự kiến.</summary>
        InternalError = 3,

        /// <summary>Chưa đăng nhập mà gọi lệnh cần đăng nhập.</summary>
        NotAuthenticated = 4,
    }
}
```

> **Vì sao lỗi nghiệp vụ không ném exception:** "sai mật khẩu" là kết quả **bình thường** của việc đăng nhập,
> không phải sự cố. Ném exception cho việc bình thường thì (a) đắt, (b) làm mờ ranh giới giữa "chuyện đời thường"
> và "server đang hỏng", (c) rất dễ dẫn tới `catch (Exception) {}` nuốt tất — đúng cái bẫy `vo-lam-genz` mắc.

---

## Bước 2 — Shared: đóng gói payload (serialize + nén)

**File mới:** `Server/Shared/Net/NetPayload.cs`

```csharp
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using K4os.Compression.LZ4;
using MemoryPack;

namespace MMORPG.Shared.Net
{
    /// <summary>
    /// Chuyển DTO ↔ mảng byte của payload, kèm nén tuỳ chọn.
    ///
    /// <code>
    /// ┌──────────┬─────────────────────────┬───────────────────┐
    /// │ flag 1B  │ rawLen 4B (chỉ khi nén) │ MemoryPack bytes  │
    /// └──────────┴─────────────────────────┴───────────────────┘
    ///   0x00 = nguyên bản · 0x01 = LZ4
    /// </code>
    /// </summary>
    public static class NetPayload
    {
        /// <summary>Dưới ngưỡng này thì không nén — nén gói bé lỗ vốn CPU mà chẳng bớt được byte nào.</summary>
        public const int COMPRESS_THRESHOLD = 4 * 1024;

        private const byte FLAG_RAW = 0x00;
        private const byte FLAG_LZ4 = 0x01;

        private const int FLAG_SIZE = 1;
        private const int RAW_LENGTH_SIZE = 4;
        private const int COMPRESSED_HEADER_SIZE = FLAG_SIZE + RAW_LENGTH_SIZE;

        public static byte[] Serialize<T>(T value) where T : IMemoryPackable<T>
        {
            if (value is null)
                return new[] { FLAG_RAW };

            byte[] raw = MemoryPackSerializer.Serialize(value);
            return Pack(raw);
        }

        public static T Deserialize<T>(byte[] payload) where T : IMemoryPackable<T>
        {
            ReadOnlySpan<byte> raw = Unpack(payload, out byte[] rented);
            try
            {
                return MemoryPackSerializer.Deserialize<T>(raw);
            }
            finally
            {
                if (rented != null)
                    ArrayPool<byte>.Shared.Return(rented);
            }
        }

        private static byte[] Pack(ReadOnlySpan<byte> raw)
        {
            if (raw.Length <= COMPRESS_THRESHOLD)
            {
                byte[] plain = new byte[FLAG_SIZE + raw.Length];
                plain[0] = FLAG_RAW;
                raw.CopyTo(plain.AsSpan(FLAG_SIZE));
                return plain;
            }

            int maxSize = LZ4Codec.MaximumOutputSize(raw.Length);
            byte[] temp = ArrayPool<byte>.Shared.Rent(maxSize);

            try
            {
                int written = LZ4Codec.Encode(raw, temp.AsSpan(0, maxSize), LZ4Level.L00_FAST);

                int compressedSize = COMPRESSED_HEADER_SIZE + written;
                int plainSize = FLAG_SIZE + raw.Length;

                // Nén xong mà không nhỏ hơn thì dùng bản gốc — dữ liệu ngẫu nhiên/đã nén sẵn
                // hoàn toàn có thể phình ra sau khi nén.
                if (compressedSize >= plainSize)
                {
                    byte[] plain = new byte[plainSize];
                    plain[0] = FLAG_RAW;
                    raw.CopyTo(plain.AsSpan(FLAG_SIZE));
                    return plain;
                }

                byte[] result = new byte[compressedSize];
                result[0] = FLAG_LZ4;
                BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(FLAG_SIZE, RAW_LENGTH_SIZE), raw.Length);
                temp.AsSpan(0, written).CopyTo(result.AsSpan(COMPRESSED_HEADER_SIZE));
                return result;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(temp);
            }
        }

        /// <param name="rented">Khác null nghĩa là buffer đi mượn — bắt buộc trả lại ArrayPool sau khi dùng.</param>
        private static ReadOnlySpan<byte> Unpack(byte[] payload, out byte[] rented)
        {
            rented = null;

            if (payload == null || payload.Length < FLAG_SIZE)
                throw new InvalidDataException("Payload rỗng, không có cả flag.");

            byte flag = payload[0];

            if (flag == FLAG_RAW)
                return payload.AsSpan(FLAG_SIZE);

            if (flag != FLAG_LZ4)
                throw new InvalidDataException($"Flag payload lạ: 0x{flag:X2}");

            if (payload.Length < COMPRESSED_HEADER_SIZE)
                throw new InvalidDataException("Payload báo có nén nhưng thiếu trường độ dài gốc.");

            int rawLength = BinaryPrimitives.ReadInt32LittleEndian(
                payload.AsSpan(FLAG_SIZE, RAW_LENGTH_SIZE));

            if (rawLength < 0 || rawLength > PacketFrame.MAX_PACKET_SIZE)
                throw new InvalidDataException($"Độ dài sau giải nén vô lý: {rawLength}");

            rented = ArrayPool<byte>.Shared.Rent(rawLength);
            int decoded = LZ4Codec.Decode(payload.AsSpan(COMPRESSED_HEADER_SIZE), rented.AsSpan());

            if (decoded != rawLength)
                throw new InvalidDataException($"Giải nén ra {decoded} byte, khai báo {rawLength}.");

            return rented.AsSpan(0, rawLength);
        }
    }
}
```

> Đây là bản rút gọn của `MemoryPackUtility.cs` trong `vo-lam-genz` — cùng scheme flag/rawLen, cùng ngưỡng 4KB,
> cùng nguyên tắc "chỉ dùng bản nén nếu thực sự nhỏ hơn". Bỏ bớt các overload cho `List<T>`/`T[]` và
> `ArrayBufferWriterPool` — thêm khi nào đo được là cần.

---

## Bước 3 — Shared: DTO đầu tiên

**File mới:** `Server/Shared/Dto/SystemDto.cs`

```csharp
using MemoryPack;

namespace MMORPG.Shared.Dto
{
    /// <summary>Client gửi mốc thời gian của chính nó để tự tính RTT khi nhận lại.</summary>
    [MemoryPackable]
    public partial class PingRequest
    {
        public long ClientTimeMs { get; set; }
    }

    /// <summary>Server echo lại mốc của client, kèm thời gian server để sau này dùng đồng bộ đồng hồ.</summary>
    [MemoryPackable]
    public partial class PingResponse
    {
        public long ClientTimeMs { get; set; }
        public long ServerTimeMs { get; set; }
    }

    /// <summary>Server báo một request bị lỗi.</summary>
    [MemoryPackable]
    public partial class ErrorResponse
    {
        /// <summary>Lệnh nào gây lỗi.</summary>
        public int FailedCmd { get; set; }

        public Net.ErrorCode Code { get; set; }

        /// <summary>Mô tả cho dev. KHÔNG hiển thị thẳng cho người chơi.</summary>
        public string Detail { get; set; } = string.Empty;
    }

    /// <summary>DTO thử của Phase 2. Xoá khi Phase 4 xong.</summary>
    [MemoryPackable]
    public partial class EchoRequest
    {
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>DTO thử của Phase 2. Xoá khi Phase 4 xong.</summary>
    [MemoryPackable]
    public partial class EchoResponse
    {
        public string Message { get; set; } = string.Empty;
        public long ServerTimeMs { get; set; }
    }
}
```

**Luật DTO** (nhắc lại từ `CONVENTIONS.md` §6):
- `[MemoryPackable] public partial class` — thiếu `partial` là lỗi compile ngay, vì source generator cần ghi thêm vào class.
- **Chỉ property auto**, không method, không logic. DTO là dữ liệu trên dây, không phải model nghiệp vụ.
- `string` luôn khởi tạo `= string.Empty` — tránh `null` bay qua mạng rồi `NullReferenceException` ở đầu kia.
- **Thêm field mới luôn thêm vào CUỐI class.** Lý do ở ngay dưới đây.

### MemoryPack ghi ra byte như thế nào — và vì sao có mấy luật trên

Lấy đúng DTO thử của Phase 0, `HandshakeDto { ProtocolVersion = 1, ServerName = "local" }` → 18 byte:

```
02   01 00 00 00   FA FF FF FF   05 00 00 00   6C 6F 63 61 6C
```

| Byte | Là gì |
|------|-------|
| `02` | **object header** = số member của class (2). Object `null` thì ở đây là `FF` và cả gói chỉ dài **1 byte** |
| `01 00 00 00` | `int ProtocolVersion = 1` — 4 byte little-endian |
| `FA FF FF FF` | int32 = **−6** = `~5` → chuỗi mã hoá **UTF8, dài 5 byte**. Số âm chính là cờ báo UTF8; số dương nghĩa là UTF16 |
| `05 00 00 00` | độ dài UTF16 = 5 ký tự — để bên đọc cấp phát `string` đúng cỡ ngay, khỏi đếm lại |
| `6C 6F 63 61 6C` | `"local"` |

Vài phép thử cho thấy quy luật:

| Giá trị | Byte | Ghi chú |
|---------|------|---------|
| `ServerName = ""` | 9 | chuỗi rỗng chỉ tốn `00 00 00 00`, không có phần độ dài UTF16 |
| `ServerName = "l"` | 14 | `FE FF FF FF` = −2 = `~1` |
| `ServerName = "ánh"` | 17 | 3 ký tự nhưng **4 byte UTF8** (`C3 A1 6E 68`) — đúng lý do phải có cả hai trường độ dài |
| cả object = `null` | 1 | chỉ `FF` |

**Hai hệ quả trực tiếp:**

1. **Trong byte không có tên field.** So với JSON `{"ProtocolVersion":1,"ServerName":"local"}` = 42 byte thì 18 byte là rất gọn — nhưng cái giá là **thứ tự khai báo property chính là contract**. Đổi chỗ 2 property, hoặc chèn một property vào giữa, thì gói cũ vẫn giải mã "thành công" mà ra giá trị sai, **không có lỗi nào cả**. Byte header `02` cho phép bên đọc biết gói cũ chỉ có 2 member và gán default cho member thứ 3 — nên **thêm field vào cuối là tương thích ngược, chèn vào giữa là hỏng câm**.

2. **Chuỗi đắt.** `"local"` chiếm 13/18 byte, trong đó 8 byte chỉ là header độ dài. Với gói gửi 20 lần/giây (di chuyển, chiến đấu) thì đừng dùng `string` — dùng `int` id rồi tra bảng config. Đó là lý do DTO ở Phase 6–8 gần như toàn số.

> Muốn tự xem byte của một DTO bất kỳ: tạo console project, `dotnet add package MemoryPack`, rồi
> `Console.WriteLine(string.Join(" ", MemoryPackSerializer.Serialize(dto).Select(b => b.ToString("X2"))));`
> Đọc byte thật luôn nhanh hơn đoán.

### ✅ CHECKPOINT A — test `NetPayload`

`Server/Shared.Tests/NetPayloadTests.cs`:
```csharp
using System.Linq;
using MMORPG.Shared.Dto;
using MMORPG.Shared.Net;
using Xunit;

namespace MMORPG.Shared.Tests;

public class NetPayloadTests
{
    [Fact]
    public void GoiNho_KhongNen_VanDungNguyenVen()
    {
        var dto = new EchoRequest { Message = "xin chào" };

        byte[] packed = NetPayload.Serialize(dto);
        var back = NetPayload.Deserialize<EchoRequest>(packed);

        Assert.Equal(0x00, packed[0]);              // flag = không nén
        Assert.Equal("xin chào", back.Message);
    }

    [Fact]
    public void GoiLon_LapLai_ThiNenVaVanGiaiDuoc()
    {
        // chuỗi lặp → nén rất tốt, chắc chắn vượt ngưỡng 4KB
        var dto = new EchoRequest { Message = string.Concat(Enumerable.Repeat("abcdefgh", 2000)) };

        byte[] packed = NetPayload.Serialize(dto);
        var back = NetPayload.Deserialize<EchoRequest>(packed);

        Assert.Equal(0x01, packed[0]);              // flag = LZ4
        Assert.True(packed.Length < dto.Message.Length / 2, "nén phải ăn thua rõ rệt");
        Assert.Equal(dto.Message, back.Message);
    }

    [Fact]
    public void FlagLa_ThiNem()
    {
        byte[] rac = { 0x7F, 1, 2, 3 };
        Assert.Throws<System.IO.InvalidDataException>(() => NetPayload.Deserialize<EchoRequest>(rac));
    }
}
```

```bash
cd /Users/ngtienhungg/Documents/UnityProjects/MMORPG/Server
dotnet test
```
Phải pass hết (5 test của Phase 1 + 3 test mới).

> Nếu build báo `EchoRequest does not implement IMemoryPackable` → thiếu `partial`, hoặc source generator
> chưa chạy. Thử `dotnet clean && dotnet build`.

---

## Bước 4 — Server: dispatch table

**File mới:** `Server/GameServer/Net/NetRequest.cs`

```csharp
using MemoryPack;
using MMORPG.Shared.Net;

namespace MMORPG.GameServer.Net;

/// <summary>
/// Bối cảnh của một gói tin đến: ai gửi, nội dung gì.
/// </summary>
public readonly struct NetRequest
{
    public ClientSession Session { get; }
    public NetCmd Cmd { get; }
    private readonly byte[] _payload;

    public NetRequest(ClientSession session, NetCmd cmd, byte[] payload)
    {
        Session = session;
        Cmd = cmd;
        _payload = payload;
    }

    /// <summary>Giải mã payload thành DTO. Ném <see cref="System.IO.InvalidDataException"/> nếu sai kiểu.</summary>
    public T GetData<T>() where T : IMemoryPackable<T> => NetPayload.Deserialize<T>(_payload);
}

/// <summary>
/// Kết quả xử lý. Handler trả struct này, dispatcher lo việc gửi đi.
/// Handler KHÔNG tự gọi Send cho phần response — để một chỗ duy nhất chịu trách nhiệm.
/// </summary>
public readonly struct NetResult
{
    /// <summary><see cref="NetCmd.None"/> nghĩa là trả về đúng cmd của request.</summary>
    public NetCmd Cmd { get; }

    /// <summary>null nghĩa là không trả gì.</summary>
    public byte[] Payload { get; }

    private NetResult(NetCmd cmd, byte[] payload)
    {
        Cmd = cmd;
        Payload = payload;
    }

    /// <summary>Không phản hồi (fire-and-forget, ví dụ gói di chuyển).</summary>
    public static NetResult None => default;

    /// <summary>Trả DTO về đúng cmd vừa nhận.</summary>
    public static NetResult Ok<T>(T dto) where T : IMemoryPackable<T> =>
        new(NetCmd.None, NetPayload.Serialize(dto));

    /// <summary>Trả DTO về một cmd khác (dùng khi response không cùng cặp với request).</summary>
    public static NetResult On<T>(NetCmd cmd, T dto) where T : IMemoryPackable<T> =>
        new(cmd, NetPayload.Serialize(dto));
}

/// <summary>Đánh dấu một static method là handler cho một lệnh.</summary>
[System.AttributeUsage(System.AttributeTargets.Method)]
public sealed class TcpHandlerAttribute : System.Attribute
{
    public NetCmd Command { get; }
    public TcpHandlerAttribute(NetCmd command) => Command = command;
}
```

**File mới:** `Server/GameServer/Net/TcpDispatcher.cs`

```csharp
using System.Reflection;
using MMORPG.Shared.Dto;
using MMORPG.Shared.Net;

namespace MMORPG.GameServer.Net;

/// <summary>
/// Bảng tra lệnh → handler. Thay cho switch khổng lồ.
/// Quét reflection một lần lúc khởi động, sau đó chỉ còn tra Dictionary — O(1), không phản chiếu lúc chạy.
/// </summary>
public static class TcpDispatcher
{
    private static readonly Dictionary<NetCmd, Func<NetRequest, NetResult>> _handlers = new();

    /// <summary>
    /// Quét mọi assembly đã nạp, tìm static method có <see cref="TcpHandlerAttribute"/> và đăng ký.
    /// Gọi đúng MỘT lần lúc server khởi động.
    /// </summary>
    public static void RegisterAll()
    {
        _handlers.Clear();

        IEnumerable<MethodInfo> methods = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && (a.FullName?.StartsWith("MMORPG.") ?? false))
            .SelectMany(a => a.GetTypes())
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .Where(m => m.GetCustomAttribute<TcpHandlerAttribute>() != null);

        foreach (MethodInfo method in methods)
        {
            TcpHandlerAttribute attr = method.GetCustomAttribute<TcpHandlerAttribute>()!;

            if (method.ReturnType != typeof(NetResult) ||
                method.GetParameters().Length != 1 ||
                method.GetParameters()[0].ParameterType != typeof(NetRequest))
            {
                Console.WriteLine($"[Dispatcher] BỎ QUA {method.DeclaringType?.Name}.{method.Name} — " +
                                  "sai chữ ký, phải là: static NetResult Ten(NetRequest req)");
                continue;
            }

            var del = (Func<NetRequest, NetResult>)Delegate.CreateDelegate(
                typeof(Func<NetRequest, NetResult>), method);

            if (!_handlers.TryAdd(attr.Command, del))
            {
                Console.WriteLine($"[Dispatcher] TRÙNG {attr.Command} — " +
                                  $"đã có handler, bỏ qua {method.DeclaringType?.Name}.{method.Name}");
                continue;
            }

            Console.WriteLine($"[Dispatcher] {attr.Command} -> {method.DeclaringType?.Name}.{method.Name}");
        }

        Console.WriteLine($"[Dispatcher] Đăng ký {_handlers.Count} handler.");
    }

    /// <summary>
    /// Tìm handler, chạy, và gửi phản hồi (nếu có). Mọi lỗi đều biến thành gói Error gửi về client.
    /// </summary>
    public static void Dispatch(ClientSession session, NetCmd cmd, byte[] payload)
    {
        if (!_handlers.TryGetValue(cmd, out Func<NetRequest, NetResult>? handler))
        {
            SendError(session, cmd, ErrorCode.UnknownCommand, $"Không có handler cho {cmd}");
            return;
        }

        NetResult result;
        try
        {
            result = handler(new NetRequest(session, cmd, payload));
        }
        catch (System.IO.InvalidDataException ex)
        {
            SendError(session, cmd, ErrorCode.MalformedPayload, ex.Message);
            return;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Dispatcher] Handler {cmd} ném lỗi: {ex}");
            SendError(session, cmd, ErrorCode.InternalError, ex.Message);
            return;
        }

        if (result.Payload == null)
            return;   // handler chủ động không trả gì

        NetCmd responseCmd = result.Cmd == NetCmd.None ? cmd : result.Cmd;
        session.SendRaw(responseCmd, result.Payload);
    }

    private static void SendError(ClientSession session, NetCmd failedCmd, ErrorCode code, string detail)
    {
        Console.WriteLine($"[Dispatcher] Lỗi {failedCmd}: {code} — {detail}");

        var dto = new ErrorResponse { FailedCmd = (int)failedCmd, Code = code, Detail = detail };
        session.SendRaw(NetCmd.Error, NetPayload.Serialize(dto));
    }
}
```

> **Vì sao lọc `a.FullName.StartsWith("MMORPG.")`:** quét toàn bộ assembly kể cả `System.*` vừa chậm
> (hàng chục nghìn type) vừa hay ném `ReflectionTypeLoadException` ở những assembly lạ.
> `vo-lam-genz` phải viết cả một hàm `IsSystemAssembly` để loại trừ từng cái một — lọc theo prefix của mình
> gọn hơn và không bao giờ sót.

---

## Bước 5 — Server: nối vào `ClientSession`, xoá if/else

Sửa `Server/GameServer/ClientSession.cs`:

```csharp
// dùng thêm
using MMORPG.GameServer.Net;
using MMORPG.Shared.Net;
```

Thay `HandlePacket` cũ và bổ sung `SendRaw`:

```csharp
    private void HandlePacket(int cmd, byte[] payload)
    {
        TcpDispatcher.Dispatch(this, (NetCmd)cmd, payload);
    }

    /// <summary>Gửi payload đã đóng gói sẵn. Dispatcher dùng hàm này.</summary>
    public void SendRaw(NetCmd cmd, byte[] payload) => Send((int)cmd, payload);

    /// <summary>Gửi DTO. Dùng khi server CHỦ ĐỘNG đẩy tin (không phải trả lời request).</summary>
    public void SendData<T>(NetCmd cmd, T dto) where T : MemoryPack.IMemoryPackable<T> =>
        Send((int)cmd, NetPayload.Serialize(dto));
```

Gọi `RegisterAll()` lúc khởi động, trong `Program.cs` — ngay trước `listener.Start()`:

```csharp
MMORPG.GameServer.Net.TcpDispatcher.RegisterAll();
```

**File mới:** `Server/GameServer/Handlers/SystemHandler.cs`

```csharp
using MMORPG.GameServer.Net;
using MMORPG.Shared.Dto;
using MMORPG.Shared.Net;

namespace MMORPG.GameServer.Handlers;

/// <summary>
/// Handler cho nhóm lệnh hệ thống (1–99).
/// Handler chỉ làm 3 việc: giải mã → gọi logic → đóng gói kết quả. Không chứa nghiệp vụ.
/// </summary>
public static class SystemHandler
{
    [TcpHandler(NetCmd.Ping)]
    public static NetResult OnPing(NetRequest req)
    {
        var request = req.GetData<PingRequest>();

        return NetResult.Ok(new PingResponse
        {
            ClientTimeMs = request.ClientTimeMs,
            ServerTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        });
    }

    [TcpHandler(NetCmd.Echo)]
    public static NetResult OnEcho(NetRequest req)
    {
        var request = req.GetData<EchoRequest>();
        Console.WriteLine($"[Session {req.Session.Id}] echo: \"{request.Message}\"");

        return NetResult.Ok(new EchoResponse
        {
            Message = request.Message,
            ServerTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        });
    }
}
```

### ✅ CHECKPOINT B

```bash
cd /Users/ngtienhungg/Documents/UnityProjects/MMORPG/Server
dotnet build && dotnet run --project GameServer
```
Log khởi động phải có:
```
[Dispatcher] Ping -> SystemHandler.OnPing
[Dispatcher] Echo -> SystemHandler.OnEcho
[Dispatcher] Đăng ký 2 handler.
[GameServer] Lắng nghe trên 0.0.0.0:7777
```

Nếu ra `Đăng ký 0 handler` → xem mục Troubleshooting, dòng "0 handler".

---

## Bước 6 — Client: dispatcher phía Unity

**File mới:** `Assets/Game/Scripts/Network/NetPacket.cs`

```csharp
using System;
using MemoryPack;
using MMORPG.Shared.Net;

namespace MMORPG.Client.Network
{
    /// <summary>Gói tin đã về tới main thread, chờ giải mã.</summary>
    public readonly struct NetPacket
    {
        public NetCmd Cmd { get; }
        private readonly byte[] _payload;

        public NetPacket(NetCmd cmd, byte[] payload)
        {
            Cmd = cmd;
            _payload = payload;
        }

        public T GetData<T>() where T : IMemoryPackable<T> => NetPayload.Deserialize<T>(_payload);
    }

    /// <summary>
    /// Đánh dấu một method là handler cho một lệnh.
    /// Chữ ký bắt buộc: <c>void Ten(NetPacket packet)</c> — method thường (không static),
    /// nằm trong một class cài <see cref="INetHandlerGroup"/> đã đăng ký vào container.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class NetHandlerAttribute : Attribute
    {
        public NetCmd Command { get; }
        public NetHandlerAttribute(NetCmd command) => Command = command;
    }

    /// <summary>Marker để container gom mọi nhóm handler lại cho dispatcher.</summary>
    public interface INetHandlerGroup { }
}
```

**File mới:** `Assets/Game/Scripts/Network/NetDispatcher.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Reflection;
using MMORPG.Shared.Net;
using UnityEngine;

namespace MMORPG.Client.Network
{
    /// <summary>
    /// Bảng tra lệnh → handler phía client. Nhận mọi <see cref="INetHandlerGroup"/> từ container
    /// rồi quét các method có <see cref="NetHandlerAttribute"/>.
    ///
    /// Handler là method THƯỜNG (không static) nên nhóm handler có thể nhận inject
    /// (event bus, service...) — khác với bản static của vo-lam-genz vốn buộc phải dùng static event.
    /// </summary>
    public sealed class NetDispatcher
    {
        private readonly Dictionary<NetCmd, Action<NetPacket>> _handlers = new();

        public NetDispatcher(IReadOnlyList<INetHandlerGroup> groups)
        {
            foreach (INetHandlerGroup group in groups)
                RegisterGroup(group);

            Debug.Log($"[NetDispatcher] Đăng ký {_handlers.Count} handler từ {groups.Count} nhóm.");
        }

        /// <returns>false nếu không có handler — để tầng trên quyết định log hay bỏ qua.</returns>
        public bool Dispatch(NetCmd cmd, byte[] payload)
        {
            if (!_handlers.TryGetValue(cmd, out Action<NetPacket> handler))
                return false;

            try
            {
                handler(new NetPacket(cmd, payload));
            }
            catch (Exception ex)
            {
                // Một handler hỏng không được làm sập vòng nhận gói.
                Debug.LogError($"[NetDispatcher] Handler {cmd} ném lỗi: {ex}");
            }

            return true;
        }

        private void RegisterGroup(INetHandlerGroup group)
        {
            MethodInfo[] methods = group.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (MethodInfo method in methods)
            {
                foreach (NetHandlerAttribute attr in method.GetCustomAttributes<NetHandlerAttribute>())
                {
                    if (method.ReturnType != typeof(void) ||
                        method.GetParameters().Length != 1 ||
                        method.GetParameters()[0].ParameterType != typeof(NetPacket))
                    {
                        Debug.LogWarning($"[NetDispatcher] BỎ QUA {group.GetType().Name}.{method.Name} — " +
                                         "sai chữ ký, phải là: void Ten(NetPacket packet)");
                        continue;
                    }

                    var del = (Action<NetPacket>)Delegate.CreateDelegate(
                        typeof(Action<NetPacket>), group, method);

                    if (_handlers.ContainsKey(attr.Command))
                    {
                        Debug.LogWarning($"[NetDispatcher] TRÙNG {attr.Command}, bỏ qua " +
                                         $"{group.GetType().Name}.{method.Name}");
                        continue;
                    }

                    _handlers[attr.Command] = del;
                }
            }
        }
    }
}
```

### Sửa `NetService` để dùng dispatcher

Thay phần liên quan tới packet trong `Assets/Game/Scripts/Network/NetService.cs`:

```csharp
        private readonly NetDispatcher _dispatcher;

        public NetService(ITransport transport, NetDispatcher dispatcher)
        {
            _transport = transport;
            _dispatcher = dispatcher;
            _transport.OnPacket += HandlePacketFromBackground;
            _transport.OnStateChanged += HandleStateFromBackground;
        }

        /// <summary>Gửi DTO. Đây là API duy nhất tầng game nên dùng để gửi.</summary>
        public void Send<T>(NetCmd cmd, T dto) where T : MemoryPack.IMemoryPackable<T> =>
            _transport.Send((int)cmd, NetPayload.Serialize(dto));

        private async UniTaskVoid RaiseOnMainThread(int cmd, byte[] payload)
        {
            await UniTask.SwitchToMainThread();

            var netCmd = (NetCmd)cmd;
            if (!_dispatcher.Dispatch(netCmd, payload))
                Debug.LogWarning($"[NetService] Không có handler cho {netCmd} — quên đăng ký nhóm handler?");
        }
```

(Bỏ `event Action<int, byte[]> OnPacket` và hàm `Send(int, ReadOnlySpan<byte>)` cũ — giờ không ai dùng nữa.
Giữ lại `OnStateChanged` vì UI vẫn cần biết trạng thái kết nối.)

**File mới:** `Assets/Game/Scripts/Network/Handlers/SystemNetHandler.cs`

```csharp
using System;
using MMORPG.Shared.Dto;
using MMORPG.Shared.Net;
using UnityEngine;

namespace MMORPG.Client.Network.Handlers
{
    /// <summary>
    /// Nhận nhóm lệnh hệ thống. Handler chỉ giải mã rồi bắn event — không đụng UI trực tiếp.
    /// </summary>
    public sealed class SystemNetHandler : INetHandlerGroup
    {
        public event Action<PingResponse> OnPong;
        public event Action<EchoResponse> OnEcho;

        [NetHandler(NetCmd.Ping)]
        private void HandlePing(NetPacket packet) => OnPong?.Invoke(packet.GetData<PingResponse>());

        [NetHandler(NetCmd.Echo)]
        private void HandleEcho(NetPacket packet) => OnEcho?.Invoke(packet.GetData<EchoResponse>());

        [NetHandler(NetCmd.Error)]
        private void HandleError(NetPacket packet)
        {
            var error = packet.GetData<ErrorResponse>();
            Debug.LogError($"[Net] Server báo lỗi cmd {(NetCmd)error.FailedCmd}: {error.Code} — {error.Detail}");
        }
    }
}
```

> **Vì sao handler chỉ bắn event, không đụng UI:** để UI có thể mở/đóng tự do mà handler vẫn sống.
> Nếu handler gọi thẳng `_statusText.text = ...` thì lúc UI chưa mở là `NullReferenceException`,
> và một handler chỉ phục vụ được đúng một UI. `vo-lam-genz` cũng làm đúng vậy
> (`BaguaTcpHandler` bắn `OnBaguaAllDataReceived`, presenter mới là chỗ mở UI).

### Đăng ký vào container

`Assets/Game/Scripts/Boot/GameLifetimeScope.cs`:

```csharp
using HungNT.Core;
using MMORPG.Client.Network;
using MMORPG.Client.Network.Handlers;
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
            builder.Register<NetDispatcher>(Lifetime.Singleton);
            builder.Register<NetService>(Lifetime.Singleton);

            // Mỗi nhóm handler mới thêm một dòng ở đây.
            builder.Register<SystemNetHandler>(Lifetime.Singleton)
                   .AsSelf()
                   .As<INetHandlerGroup>();
        }
    }
}
```

> **`.AsSelf().As<INetHandlerGroup>()`** — `As<INetHandlerGroup>` để `NetDispatcher` gom được qua
> `IReadOnlyList<INetHandlerGroup>`; `AsSelf` để UI inject được đúng kiểu cụ thể mà subscribe event.
> Thiếu `AsSelf` → UI không resolve được `SystemNetHandler`.

> **Vì sao client đăng ký tay còn server quét tự động:** server handler là hàm static thuần, quét cả assembly
> là an toàn và tiện. Client handler cần nhận inject nên phải là instance do container tạo — mà container
> thì phải được **bảo** là tạo cái gì. Đổi lại, nhìn `GameLifetimeScope` là biết chính xác client đang lắng nghe
> những nhóm lệnh nào; bản quét tự động không cho bạn cái nhìn đó.

---

## Bước 7 — Sửa `NetworkProbe` sang dùng DTO

```csharp
using System;
using Cysharp.Threading.Tasks;
using MMORPG.Client.Network.Handlers;
using MMORPG.Shared.Dto;
using MMORPG.Shared.Net;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace MMORPG.Client.Network
{
    public class NetworkProbe : MonoBehaviour
    {
        [SerializeField] private string _host = "127.0.0.1";
        [SerializeField] private int _port = 7777;

        [SerializeField] private Button _connectButton;
        [SerializeField] private Button _pingButton;
        [SerializeField] private Button _echoButton;
        [SerializeField] private TMP_InputField _echoInput;
        [SerializeField] private TextMeshProUGUI _statusText;

        private NetService _net;
        private SystemNetHandler _systemHandler;

        [Inject]
        public void Construct(NetService net, SystemNetHandler systemHandler)
        {
            _net = net;
            _systemHandler = systemHandler;
        }

        private void Awake()
        {
            _connectButton.onClick.AddListener(() => ConnectAsync().Forget());
            _pingButton.onClick.AddListener(SendPing);
            _echoButton.onClick.AddListener(SendEcho);

            _systemHandler.OnPong += OnPong;
            _systemHandler.OnEcho += OnEcho;
            _net.OnStateChanged += state => SetStatus(state.ToString());

            SetStatus("Chưa kết nối");
        }

        private void OnDestroy()
        {
            if (_systemHandler == null)
                return;

            _systemHandler.OnPong -= OnPong;
            _systemHandler.OnEcho -= OnEcho;
        }

        private async UniTaskVoid ConnectAsync()
        {
            SetStatus($"Đang kết nối {_host}:{_port}...");
            if (!await _net.ConnectAsync(_host, _port))
                SetStatus("Kết nối thất bại");
        }

        private void SendPing() =>
            _net.Send(NetCmd.Ping, new PingRequest
            {
                ClientTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            });

        private void SendEcho() =>
            _net.Send(NetCmd.Echo, new EchoRequest { Message = _echoInput.text });

        private void OnPong(PingResponse res)
        {
            long rtt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - res.ClientTimeMs;
            SetStatus($"RTT: {rtt} ms · lệch giờ server: {res.ServerTimeMs - res.ClientTimeMs} ms");
        }

        private void OnEcho(EchoResponse res) => SetStatus($"Server vọng lại: \"{res.Message}\"");

        private void SetStatus(string text)
        {
            _statusText.text = text;
            Debug.Log($"[Probe] {text}");
        }
    }
}
```

Trong scene: thêm `EchoButton` (chữ `Echo`) và một `TMP_InputField` tên `EchoInput`, kéo vào 2 ô mới.

### ✅ CHECKPOINT C — mục tiêu cuối Phase 2

1. Chạy server, Play Unity, bấm **Connect**.
2. Bấm **Ping** → UI hiện `RTT: 0 ms · lệch giờ server: ...`
3. Gõ `xin chào` vào ô input, bấm **Echo**
   → server log `[Session 1] echo: "xin chào"`
   → UI hiện `Server vọng lại: "xin chào"`
4. **Test đường lỗi:** tạm sửa `SendEcho` thành `_net.Send((NetCmd)77, new EchoRequest { ... })`
   → Console Unity phải hiện:
   ```
   [Net] Server báo lỗi cmd 77: UnknownCommand — Không có handler cho 77
   ```
   Sửa lại như cũ sau khi thử xong.

**Bước 4 quan trọng ngang bước 3.** Nó chứng minh đường xử lý lỗi cũng thông, không phải chỉ đường thành công.

---

## Bước 8 — Bài tập: thêm một lệnh mới trong 5 phút

Đây là bài kiểm tra xem kiến trúc có thật sự đúng không. Thêm lệnh `ServerInfo` trả về tên server và số người đang online.

1. `NetCmd.cs`: thêm `ServerInfo = 4,` vào cuối dải hệ thống.
2. `SystemDto.cs`: thêm
   ```csharp
   [MemoryPackable]
   public partial class ServerInfoResponse
   {
       public string ServerName { get; set; } = string.Empty;
       public int OnlineCount { get; set; }
   }
   ```
   (Request rỗng thì vẫn cần một DTO rỗng `ServerInfoRequest` — hoặc dùng lại `EchoRequest`. Cách gọn:
   tạo `[MemoryPackable] public partial class EmptyRequest { }` trong `SystemDto.cs` và dùng lại cho mọi lệnh không tham số.)
3. `dotnet build` (DLL tự sang Unity).
4. Server, thêm vào `SystemHandler`:
   ```csharp
   [TcpHandler(NetCmd.ServerInfo)]
   public static NetResult OnServerInfo(NetRequest req) =>
       NetResult.Ok(new ServerInfoResponse { ServerName = "local-dev", OnlineCount = 1 });
   ```
5. Client, thêm vào `SystemNetHandler`:
   ```csharp
   public event Action<ServerInfoResponse> OnServerInfo;

   [NetHandler(NetCmd.ServerInfo)]
   private void HandleServerInfo(NetPacket packet) => OnServerInfo?.Invoke(packet.GetData<ServerInfoResponse>());
   ```

**Đếm xem bạn phải sửa bao nhiêu file dùng chung: 0.** Không đụng `TcpDispatcher`, không đụng `NetDispatcher`,
không đụng `ClientSession`, không đụng `NetService`. Đó là toàn bộ điểm của Phase 2.

Đối chiếu: cùng việc này ở `vo-lam-genz` **kiểu cũ** là sửa `TCPCmdHandler.cs` (2.056 dòng) — file mà 5 người khác
cũng đang sửa cùng lúc.

---

## Troubleshooting

| Triệu chứng | Nguyên nhân | Xử lý |
|-------------|-------------|-------|
| `[Dispatcher] Đăng ký 0 handler` | Class handler chưa bao giờ được nạp — .NET nạp assembly lười (lazy) | Handler nằm cùng assembly `MMORPG.GameServer` nên vẫn quét được. Nếu tách assembly riêng thì phải chạm vào nó một lần, vd `_ = typeof(SystemHandler);` trước `RegisterAll()` |
| `[Dispatcher] BỎ QUA ... sai chữ ký` | Method không `static`, hoặc trả `void`, hoặc tham số sai kiểu | Đúng chữ ký: `static NetResult Ten(NetRequest req)` |
| `[NetDispatcher] Đăng ký 0 handler từ 0 nhóm` | Quên `.As<INetHandlerGroup>()` trong `GameLifetimeScope` | Thêm lại, chú ý cả `.AsSelf()` |
| `VContainerException: ... SystemNetHandler is not registered` | Chỉ đăng ký `.As<INetHandlerGroup>()` mà thiếu `.AsSelf()` | Thêm `.AsSelf()` |
| `InvalidDataException: Flag payload lạ` | Một bên gửi bằng `PacketFrame` trần (không qua `NetPayload`), bên kia giải bằng `NetPayload` | Mọi thứ đi qua mạng đều phải qua `NetService.Send<T>` / `NetResult.Ok`, đừng gọi `transport.Send` trực tiếp |
| Server nhận đúng nhưng `GetData<T>()` ném lỗi | Client gửi DTO kiểu A, server đọc kiểu B | Cặp request/response phải khớp; xem lại `SystemHandler` |
| Unity báo `MMORPG.Shared.Dto` không tồn tại sau khi thêm DTO | Quên `dotnet build` | Build lại `Server/`, chờ Unity import DLL |
| Đổi DTO xong client vẫn nhận data cũ | Unity giữ DLL cũ trong bộ nhớ | Thoát Play mode, chờ Unity recompile, Play lại |
| `MemoryPackSerializationException: ... is not registered` | Type dùng `[MemoryPackable]` nhưng thiếu `partial`, hoặc DTO lồng type chưa `[MemoryPackable]` | Mọi type trong cây DTO đều phải `[MemoryPackable] partial` |

---

## Tự kiểm tra hiểu bài

1. Vì sao response đi về **đúng cmd của request** lại tiện hơn tạo một enum riêng cho mỗi response?
2. `NetResult.None` và `NetResult.Ok(dto)` khác nhau ở điểm nào trong `Dispatch`? Khi nào dùng `None`?
3. Vì sao lỗi "sai mật khẩu" trả `ErrorCode` chứ không ném exception, còn "payload hỏng" thì ném?
4. Nếu bỏ ngưỡng 4KB và nén **mọi** gói, chuyện gì xảy ra với gói di chuyển 20 byte gửi 20 lần/giây?
5. `NetDispatcher` bắt exception của handler rồi log thay vì để nó ném lên. Vì sao? Cái giá của lựa chọn đó là gì?
6. Vì sao nhóm handler client phải `.AsSelf().As<INetHandlerGroup>()` — bỏ một trong hai thì hỏng thế nào?
7. Handler client chỉ bắn event chứ không đụng UI. Nếu cho nó đụng thẳng UI thì hỏng ở tình huống nào?
8. Bạn thêm `NetCmd.Foo = 4` ở client nhưng quên `dotnet build`. Server và client lệch nhau thế nào, và bạn phát hiện lúc nào?

---

**Xong Phase 2 → PHASE-3: dựng `DBServer` như một process riêng và tầng DAL.**
(Tài liệu Phase 3 sẽ được viết khi bạn báo đã xong Phase 2.)
