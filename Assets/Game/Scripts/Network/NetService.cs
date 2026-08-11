using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using HungNT;
using MMORPG.Scripts.Network;
using MMORPG.Shared.Net;

namespace MMORPG.Game.Scripts.Network
{
    /// <summary>
    /// Điểm vào mạng của client. Sở hữu transport, và đảm bảo mọi thứ đi lên tầng trên
    /// đều đã ở main thread — nhờ vậy tầng game phía trên không bao giờ phải nghĩ về luồng.
    /// </summary>
    public sealed class NetService : IDisposable
    {
        private readonly ITransport _transport;
        private readonly CancellationTokenSource _cts = new();

        /// <summary>Đổi trạng thái kết nối. Đã ở MAIN THREAD.</summary>
        public event Action<TransportState> OnStateChanged;

        public TransportState State => _transport.State;

        private readonly NetDispatcher _dispatcher;

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

            _cts.Cancel();
            _cts.Dispose();
            _transport.Dispose();
        }

        private void HandlePacketFromBackground(int cmd, byte[] payload) => RaiseOnMainThread(cmd, payload).Forget();

        private async UniTaskVoid RaiseOnMainThread(int cmd, byte[] payload)
        {
            await UniTask.SwitchToMainThread();

            var netCmd = (NetCmd)cmd;
            if (!_dispatcher.Dispatch(netCmd, payload))
                this.LogWarning($"[NetService] Không có handler cho {netCmd} — quên đăng ký nhóm handler?");
        }

        private void HandleStateFromBackground(TransportState state) => RaiseStateOnMainThread(state).Forget();

        private async UniTaskVoid RaiseStateOnMainThread(TransportState state)
        {
            await UniTask.SwitchToMainThread();
            OnStateChanged?.Invoke(state);
        }
    }
}