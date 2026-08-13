using System.Collections.Concurrent;
using System.Net.Sockets;
using MMORPG.DBServer.Net;
using MMORPG.ServerCore;
using MMORPG.Shared.Db;
using MMORPG.Shared.Net;

namespace MMORPG.DBServer
{
    /// <summary>Một kết nối từ GameServer.</summary>
    public sealed class DbSession
    {
        private static int _nextId;

        private readonly TcpClient _tcpClient;
        private readonly NetworkStream _stream;
        private readonly FrameReader _frameReader = new();

        private readonly ConcurrentQueue<byte[]> _sendQueue = new();
        private readonly SemaphoreSlim _sendSignal = new(0);

        public int Id { get; }

        /// <summary>
        /// Nhãn phiên chèn đầu câu log, đối ứng của <c>ClientSession.Tag</c> bên GameServer.
        /// Tính một lần trong constructor để mọi dòng log của phiên này có cùng một nhãn có màu.
        /// </summary>
        public string Tag { get; }

        public DbSession(TcpClient tcpClient)
        {
            _tcpClient = tcpClient;
            _tcpClient.NoDelay = true;
            _stream = tcpClient.GetStream();
            Id = Interlocked.Increment(ref _nextId);
            Tag = $"#{Id.ToString().Magenta()}";
        }

        public async Task RunAsync(CancellationToken ct)
        {
            Log.Info($"{Tag} GameServer kết nối từ {$"{_tcpClient.Client.RemoteEndPoint}".Green()}");

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            Task sendLoop = SendLoopAsync(linked.Token);

            try
            {
                byte[] buffer = new byte[8192];

                while (!linked.IsCancellationRequested)
                {
                    int read = await _stream.ReadAsync(buffer, 0, buffer.Length, linked.Token);
                    if (read == 0)
                        break;

                    _frameReader.Feed(buffer, 0, read);

                    while (_frameReader.TryRead(out int cmd, out byte[]? framePayload))
                    {
                        // KHÔNG await ở đây. Query chậm của session A không được chặn
                        // việc đọc request của session B — đó là toàn bộ lý do có request id.
                        _ = ProcessAsync((DbCmd)cmd, framePayload);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
                // Info chứ không Error: GameServer tắt bằng Ctrl+C cũng rơi vào đây, và một dòng
                // ERROR đỏ cho một lần tắt máy bình thường chỉ làm loãng những lỗi thật.
                // Cùng mức với ClientSession bên GameServer.
                Log.Info($"{Tag} Mất kết nối: {ex.GetType().Name.Red()}");
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                linked.Cancel();
                _sendSignal.Release();
                await Task.WhenAny(sendLoop, Task.Delay(1000, CancellationToken.None));

                _tcpClient.Dispose();
                Log.Info($"{Tag} Đóng.");
            }
        }

        private async Task ProcessAsync(DbCmd cmd, byte[] framePayload)
        {
            try
            {
                byte[] payload = DbFrame.Decode(framePayload, out int requestId);
                DbResult result = await DbDispatcher.DispatchAsync(cmd, payload);

                _sendQueue.Enqueue(DbFrame.Encode(cmd, requestId, result.Payload));
                _sendSignal.Release();
            }
            catch (Exception ex)
            {
                // Task này không ai await — exception lọt ra ngoài đây là biến mất không dấu vết.
                Log.Error(ex, $"{Tag} Lỗi xử lý {cmd.ToString().Cyan()}");
            }
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
            }
        }
    }
}
