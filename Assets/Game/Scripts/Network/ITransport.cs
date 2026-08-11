using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace MMORPG.Client.Network
{
    public enum TransportState
    {
        Disconnected,
        Connecting,
        Connected,
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
