using System.Collections.Concurrent;
using System.Net.Sockets;
using MemoryPack;
using MMORPG.GameServer.Net;
using MMORPG.ServerCore;
using MMORPG.Shared.Dto;
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

        /// <summary>
        /// Nhãn phiên để chèn đầu câu log. Có màu nên nhìn console là bám được ngay
        /// một kết nối cụ thể giữa hàng chục phiên đang chạy song song.
        /// </summary>
        public string Tag { get; }

        /// <summary>Trạng thái hiện tại. Chỉ AuthService và WorldService được đổi.</summary>
        public SessionState State { get; private set; } = SessionState.Connected;

        /// <summary>0 khi chưa đăng nhập. Đây là NGUỒN DUY NHẤT cho biết session này là ai.</summary>
        public long AccountId { get; private set; }

        public string Username { get; private set; } = string.Empty;

        public ClientSession(TcpClient tcpClient)
        {
            _tcpClient = tcpClient;
            _tcpClient.NoDelay = true; // tắt Nagle: game cần độ trễ thấp hơn là gộp gói cho hiệu quả
            _stream = tcpClient.GetStream();
            Id = Interlocked.Increment(ref _nextId);
            Tag = $"#{Id.ToString().Magenta()}";
        }

        public async Task RunAsync(CancellationToken ct)
        {
            Log.Info($"{Tag} Kết nối từ {$"{_tcpClient.Client.RemoteEndPoint}".Green()}");
            SessionRegistry.Add(this);

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);

            Task sendLoop = SendLoopAsync(linked.Token);

            try
            {
                await ReadLoopAsync(linked.Token);
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
                Log.Info($"{Tag} Mất kết nối: {ex.GetType().Name}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"{Tag} Vòng đọc chết");
            }
            finally
            {
                // Cancel là đủ để đánh thức vòng gửi: WaitAsync(ct) huỷ ngay khi token bật.
                linked.Cancel();
                await Task.WhenAny(sendLoop, Task.Delay(1000, CancellationToken.None));

                SessionRegistry.Remove(this);
                _tcpClient.Dispose();
                Log.Info($"{Tag} Đóng.");
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
                    await TcpDispatcher.DispatchAsync(this, (NetCmd)cmd, payload);
            }
        }

        /// <summary>Gửi payload đã đóng gói sẵn. Dispatcher dùng hàm này.</summary>
        public void SendRaw(NetCmd cmd, byte[] payload)
        {
            Send((int)cmd, payload);
        }

        /// <summary>Gửi DTO. Dùng khi server CHỦ ĐỘNG đẩy tin (không phải trả lời request).</summary>
        public void SendData<T>(NetCmd cmd, T dto) where T : IMemoryPackable<T>
        {
            Send((int)cmd, NetPayload.Serialize(dto));
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

                    while (_sendQueue.TryDequeue(out byte[] frame))
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

        public void MarkAuthenticated(long accountId, string username)
        {
            AccountId = accountId;
            Username = username;
            State = SessionState.Authenticated;
        }

        public void MarkLoggedOut()
        {
            AccountId = 0;
            Username = string.Empty;
            State = SessionState.Connected;
        }

        public void Kick(string reason)
        {
            Log.Warn($"{Tag} Kick ra: {reason.Yellow()}");
            SendData(NetCmd.Kicked, new KickedNotice { Reason = reason });

            // Cho vòng gửi kịp đẩy gói Kicked đi rồi mới cắt. Cắt ngay thì client
            // chỉ thấy mất kết nối trần và không biết vì sao.
            _ = CloseAfterFlushAsync();
        }

        private async Task CloseAfterFlushAsync()
        {
            await Task.Delay(100);
            _tcpClient.Close();
        }
    }
}
