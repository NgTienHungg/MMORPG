using System.Collections.Concurrent;
using System.Net.Sockets;
using MemoryPack;
using MMORPG.ServerCore;
using MMORPG.Shared.Db;
using MMORPG.Shared.Net;

namespace MMORPG.GameServer.Db
{
    /// <summary>
    /// Đầu GameServer của đường nội bộ. Biến "gửi gói rồi chờ" thành một <c>await</c> bình thường.
    ///
    /// Một kết nối duy nhất phục vụ mọi session; <see cref="_pending"/> là thứ ghép response về đúng
    /// chỗ đang chờ.
    /// </summary>
    public sealed class DbClient : IAsyncDisposable
    {
        /// <summary>Chờ lâu hơn mức này thì coi như DBServer đã chết. Query bình thường tính bằng mili giây.</summary>
        private const int TIMEOUT_MS = 5000;

        /// <summary>
        /// Nghỉ giữa hai lần thử nối lại. Chu kỳ thật dài hơn con số này: trên Windows, connect vào
        /// cổng không ai nghe phải ~2 giây mới bị từ chối, nên nhịp quan sát được là ~3 giây.
        /// </summary>
        private const int RECONNECT_DELAY_MS = 1000;

        private readonly string _host;
        private readonly int _port;

        private readonly ConcurrentDictionary<int, TaskCompletionSource<byte[]>> _pending = new();
        private readonly ConcurrentQueue<byte[]> _sendQueue = new();
        private readonly SemaphoreSlim _sendSignal = new(0);

        private readonly CancellationTokenSource _cts = new();

        private int _nextRequestId;
        private volatile TcpClient? _tcpClient;
        private Task? _connectionLoop;

        public bool IsConnected => _tcpClient?.Connected == true;

        public DbClient(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public void Start() => _connectionLoop = ConnectionLoopAsync(_cts.Token);

        /// <summary>
        /// Gửi một request và chờ response tương ứng.
        /// </summary>
        /// <exception cref="DbUnavailableException">DBServer không nối được, hoặc quá <see cref="TIMEOUT_MS"/>.</exception>
        public async Task<TResponse> CallAsync<TRequest, TResponse>(DbCmd cmd, TRequest request)
            where TRequest : IMemoryPackable<TRequest>
            where TResponse : IMemoryPackable<TResponse>
        {
            // Nội dung exception để TRẦN, không tô màu. Nơi bắt nó (TcpDispatcher) bọc cả
            // ex.Message trong một màu khác; mã reset của màu bên trong sẽ cắt ngang màu bên
            // ngoài và phần đuôi câu mất màu. Quy tắc: tô màu ở chỗ LOG, không ở chỗ THROW.
            if (!IsConnected)
                throw new DbUnavailableException($"Chưa nối được DBServer khi gọi {cmd}.");

            int requestId = Interlocked.Increment(ref _nextRequestId);

            // RunContinuationsAsynchronously: KHÔNG có cờ này thì phần code phía sau `await CallAsync`
            // sẽ chạy ngay trên luồng đang đọc socket, và vòng đọc đứng chờ nó xong mới đọc tiếp.
            // Một handler chậm sẽ làm tắc TOÀN BỘ đường DB. Đây là cái bẫy kinh điển của TaskCompletionSource.
            var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[requestId] = tcs;

            try
            {
                _sendQueue.Enqueue(DbFrame.Encode(cmd, requestId, NetPayload.Serialize(request)));
                _sendSignal.Release();

                using var timeout = new CancellationTokenSource(TIMEOUT_MS);
                await using (timeout.Token.Register(() => tcs.TrySetCanceled()))
                {
                    byte[] payload = await tcs.Task;
                    return NetPayload.Deserialize<TResponse>(payload);
                }
            }
            catch (TaskCanceledException)
            {
                throw new DbUnavailableException($"{cmd} không có phản hồi sau {TIMEOUT_MS}ms.");
            }
            finally
            {
                // Bắt buộc, kể cả đường thành công: thiếu dòng này thì _pending phình mãi
                // và bạn có một rò rỉ bộ nhớ chỉ lộ ra sau vài giờ chạy.
                _pending.TryRemove(requestId, out _);
            }
        }

        private async Task ConnectionLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                TcpClient? client = null;

                try
                {
                    client = new TcpClient { NoDelay = true };
                    await client.ConnectAsync(_host, _port, ct);
                    _tcpClient = client;
                    Log.Info($"Đã nối DBServer {$"{_host}:{_port}".Green()}");

                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    Task sendLoop = SendLoopAsync(client, linked.Token);

                    await ReadLoopAsync(client, linked.Token);

                    // Cancel là đủ để đánh thức vòng gửi — WaitAsync(ct) huỷ ngay khi token bật.
                    // Đừng Release() thêm ở đây: tín hiệu thừa không ai tiêu thụ sẽ sống sang
                    // lần kết nối sau và làm vòng gửi mới tỉnh dậy một lượt vô ích.
                    linked.Cancel();
                    await Task.WhenAny(sendLoop, Task.Delay(1000, CancellationToken.None));
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Log.Warn($"Mất kết nối DBServer: {ex.GetType().Name.Red()} — {ex.Message}");
                }
                finally
                {
                    // Dispose biến cục bộ chứ KHÔNG phải _tcpClient: khi chính ConnectAsync ném thì
                    // _tcpClient còn null, và cái socket vừa mở sẽ rò cho tới lúc GC chạy finalizer.
                    client?.Dispose();
                    _tcpClient = null;

                    FailAllPending();
                    DrainSendQueue();
                }

                if (!ct.IsCancellationRequested)
                    await Task.Delay(RECONNECT_DELAY_MS, CancellationToken.None);
            }
        }

        private async Task ReadLoopAsync(TcpClient client, CancellationToken ct)
        {
            var frameReader = new FrameReader();
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[8192];

            while (!ct.IsCancellationRequested)
            {
                int read = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
                if (read == 0)
                    throw new IOException("DBServer đóng kết nối.");

                frameReader.Feed(buffer, 0, read);

                while (frameReader.TryRead(out int _, out byte[]? framePayload))
                {
                    byte[] payload = DbFrame.Decode(framePayload, out int requestId);

                    if (_pending.TryRemove(requestId, out TaskCompletionSource<byte[]>? tcs))
                        tcs.TrySetResult(payload);
                    else
                        // Response về sau khi bên gửi đã bỏ cuộc vì timeout. Không phải lỗi,
                        // nhưng thấy nhiều dòng này nghĩa là DB đang chậm hơn TIMEOUT_MS.
                        Log.Warn($"Response lạc: reqId {requestId.ToString().Magenta()} không còn ai chờ.");
                }
            }
        }

        private async Task SendLoopAsync(TcpClient client, CancellationToken ct)
        {
            NetworkStream stream = client.GetStream();

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await _sendSignal.WaitAsync(ct);

                    while (_sendQueue.TryDequeue(out byte[]? frame))
                        await stream.WriteAsync(frame, 0, frame.Length, ct);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
            }
        }

        /// <summary>
        /// Đánh trượt mọi request đang chờ khi kết nối chết. Không làm việc này thì
        /// chúng treo tới lúc timeout — 5 giây người chơi nhìn màn hình đứng mà chẳng vì lý do gì.
        /// </summary>
        private void FailAllPending()
        {
            foreach (int key in _pending.Keys)
            {
                if (_pending.TryRemove(key, out TaskCompletionSource<byte[]>? tcs))
                    tcs.TrySetException(new DbUnavailableException("Mất kết nối DBServer."));
            }
        }

        /// <summary>
        /// Vét hàng đợi gửi và tín hiệu đi kèm. Frame còn sót lại là của những request vừa bị
        /// <see cref="FailAllPending"/> đánh trượt — gửi chúng ở lần kết nối sau chỉ đẻ ra một loạt
        /// "Response lạc" vì không còn ai chờ nữa.
        /// </summary>
        private void DrainSendQueue()
        {
            while (_sendQueue.TryDequeue(out _))
            {
            }

            // Wait(0) không bao giờ chặn: nó chỉ lấy bớt count khi count đang > 0.
            while (_sendSignal.CurrentCount > 0)
                _sendSignal.Wait(0);
        }

        public async ValueTask DisposeAsync()
        {
            // Cancel đủ để dừng cả vòng nối lẫn vòng gửi; Release() ở đây chỉ để lại
            // một tín hiệu thừa trên semaphore sắp bị vứt đi.
            _cts.Cancel();

            if (_connectionLoop != null)
                await Task.WhenAny(_connectionLoop, Task.Delay(2000, CancellationToken.None));

            _tcpClient?.Dispose();
            _cts.Dispose();
        }
    }

    /// <summary>DBServer không dùng được. Đây là lỗi HỆ THỐNG, không phải lỗi nghiệp vụ.</summary>
    public sealed class DbUnavailableException : Exception
    {
        public DbUnavailableException(string message) : base(message)
        {
        }
    }
}
