using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using HungNT;
using MMORPG.Shared.Net;

namespace MMORPG.Client.Network
{
    /// <summary>
    /// Transport TCP cho client. Đọc và gửi đều chạy ở luồng nền;
    /// việc chuyển về main thread là trách nhiệm của tầng trên (<see cref="NetService"/>).
    /// </summary>
    public class TcpTransport : ITransport
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

        public async UniTask<bool> ConnectAsync(string host, int port, CancellationToken token)
        {
            if (State != TransportState.Disconnected)
            {
                this.LogWarning($"Đang ở trạng thái {State}, bỏ qua yêu cầu connect.");
                return false;
            }

            SetState(TransportState.Connecting);

            try
            {
                _tcpClient = new TcpClient { NoDelay = true };
                await _tcpClient.ConnectAsync(host, port);

                _stream = _tcpClient.GetStream();
                _cts = CancellationTokenSource.CreateLinkedTokenSource(token);

                SetState(TransportState.Connected);

                // Không await: hai vòng lặp này sống suốt phiên kết nối.
                _ = Task.Run(() => ReadLoopAsync(_cts.Token));
                _ = Task.Run(() => SendLoopAsync(_cts.Token));

                return true;
            }
            catch (Exception ex)
            {
                this.LogError($"Kết nối {host}:{port} thất bại - {ex.Message}");
                Cleanup();
                return false;
            }
        }

        public void Send(int cmd, ReadOnlySpan<byte> payload)
        {
            if (State != TransportState.Connected)
            {
                this.LogWarning($"Chưa kết nối, bỏ gói cmd {cmd}");
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

        public void Dispose()
        {
            Disconnect();
        }

        private async Task ReadLoopAsync(CancellationToken ct)
        {
            byte[] buffer = new byte[8192];

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    int read = await _stream.ReadAsync(buffer, 0, buffer.Length, ct);
                    if (read == 0) break; // server đóng kết nối

                    _frameReader.Feed(buffer, 0, read);

                    while (_frameReader.TryRead(out int cmd, out byte[] payload))
                        OnPacket?.Invoke(cmd, payload);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
                // mất kết nối — coi là bình thường, xử lý ở finally
            }
            catch (InvalidDataException ex)
            {
                // InvalidDataException KHÔNG kế thừa IOException nên không bị filter phía trên nuốt mất.
                this.LogError($"Dòng byte hỏng, buộc ngắt kết nối — {ex.Message}");
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
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
                // vòng đọc lo phần dọn dẹp
            }
        }

        private void Cleanup()
        {
            // Vòng đọc, vòng gửi và luồng UI đều có thể gọi vào đây cùng lúc — chỉ một lượt được dọn.
            if (Interlocked.Exchange(ref _state, (int)TransportState.Disconnected) == (int)TransportState.Disconnected)
                return;

            // Giành lấy _cts rồi mới đụng: lần connect sau có thể đã kịp gán cái mới.
            CancellationTokenSource cts = Interlocked.Exchange(ref _cts, null);
            cts?.Cancel(); // đủ để đánh thức vòng gửi — WaitAsync(ct) huỷ ngay khi token bật
            cts?.Dispose();

            _stream?.Dispose();
            _tcpClient?.Dispose();

            _stream = null;
            _tcpClient = null;

            // Dọn cả hàng đợi lẫn tín hiệu. Bỏ tín hiệu lại thì lần kết nối sau vòng gửi
            // sẽ tỉnh dậy đúng bằng số gói còn dang dở rồi quay vòng vô ích.
            while (_sendQueue.TryDequeue(out _))
            {
            }

            while (_sendSignal.CurrentCount > 0)
                _sendSignal.Wait(0);

            OnStateChanged?.Invoke(TransportState.Disconnected);
        }

        private void SetState(TransportState next)
        {
            Volatile.Write(ref _state, (int)next);
            OnStateChanged?.Invoke(next);
        }
    }
}
