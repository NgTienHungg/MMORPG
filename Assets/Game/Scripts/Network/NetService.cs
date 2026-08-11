using System;
using System.Threading;
using Cysharp.Threading.Tasks;

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

        public UniTask<bool> ConnectAsync(string host, int port) => _transport.ConnectAsync(host, port, _cts.Token);

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