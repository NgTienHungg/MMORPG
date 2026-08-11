using System.Collections.Concurrent;
using System.Net.Sockets;
using MMORPG.Shared.Net;

namespace MMORPG.GameServer
{
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
            _tcpClient.NoDelay = true; // tắt Nagle: game cần độ trễ thấp hơn là gộp gói cho hiệu quả
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
                linked.Cancel(); // dừng vòng gửi
                _sendSignal.Release(); // đánh thức nó để nó thấy token đã huỷ
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
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
                // kết nối đã chết, vòng đọc sẽ xử lý phần dọn dẹp
            }
        }
    }
}
