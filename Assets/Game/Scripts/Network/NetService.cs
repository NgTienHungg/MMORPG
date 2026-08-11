using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using HungNT;
using MMORPG.Shared.Net;

namespace MMORPG.Client.Network
{
    /// <summary>
    /// Điểm vào mạng của client. Sở hữu transport, và đảm bảo mọi thứ đi lên tầng trên
    /// đều đã ở main thread — nhờ vậy tầng game phía trên không bao giờ phải nghĩ về luồng.
    /// </summary>
    public sealed class NetService : IDisposable
    {
        private readonly ITransport _transport;
        private readonly NetDispatcher _dispatcher;
        private readonly CancellationTokenSource _cts = new();

        /// <summary>Đổi trạng thái kết nối. Đã ở MAIN THREAD.</summary>
        public event Action<TransportState> OnStateChanged;

        public TransportState State => _transport.State;

        public NetService(ITransport transport, NetDispatcher dispatcher)
        {
            _transport = transport;
            _dispatcher = dispatcher;
            _transport.OnPacket += HandlePacketFromBackground;
            _transport.OnStateChanged += HandleStateFromBackground;
        }

        public UniTask<bool> ConnectAsync(string host, int port) => _transport.ConnectAsync(host, port, _cts.Token);

        /// <summary>Gửi DTO. Đây là API duy nhất tầng game nên dùng để gửi.</summary>
        public void Send<T>(NetCmd cmd, T dto) where T : MemoryPack.IMemoryPackable<T> => _transport.Send((int)cmd, NetPayload.Serialize(dto));

        public void Disconnect() => _transport.Disconnect();

        public void Dispose()
        {
            _transport.OnPacket -= HandlePacketFromBackground;
            _transport.OnStateChanged -= HandleStateFromBackground;

            // Ngắt kết nối chứ KHÔNG dispose transport: transport là singleton do container tạo,
            // nên container mới là chỗ dispose nó. Dispose hộ ở đây là dispose hai lần.
            _transport.Disconnect();

            _cts.Cancel();
            _cts.Dispose();
        }

        private void HandlePacketFromBackground(int cmd, byte[] payload) => RaiseOnMainThread(cmd, payload).Forget();

        private async UniTaskVoid RaiseOnMainThread(int cmd, byte[] payload)
        {
            await UniTask.SwitchToMainThread();

            var netCmd = (NetCmd)cmd;
            if (!_dispatcher.Dispatch(netCmd, payload))
                this.LogWarning($"Không có handler cho {netCmd} — quên đăng ký nhóm handler trong GameLifetimeScope?");
        }

        private void HandleStateFromBackground(TransportState state) => RaiseStateOnMainThread(state).Forget();

        private async UniTaskVoid RaiseStateOnMainThread(TransportState state)
        {
            await UniTask.SwitchToMainThread();
            OnStateChanged?.Invoke(state);
        }
    }
}
