using System;
using HungNT;
using MMORPG.Shared.Dto;
using MMORPG.Shared.Net;

namespace MMORPG.Client.Network.Handlers
{
    /// <summary>
    /// Nhận nhóm lệnh hệ thống. Handler chỉ giải mã rồi bắn event — không đụng UI trực tiếp.
    /// </summary>
    public sealed class SystemNetHandler : INetHandlerGroup
    {
        public event Action<PingResponse> OnPong;
        public event Action<EchoResponse> OnEcho;
        public event Action<ServerInfoResponse> OnServerInfo;

        /// <summary>
        /// Kết quả kiểm phiên bản contract. Lệnh ĐẦU TIÊN của mọi phiên: tới khi nó về thì session
        /// còn ở bậc Connected và server từ chối gần như mọi lệnh khác.
        /// </summary>
        public event Action<VersionCheckResponse> OnVersionCheck;

        [NetHandler(NetCmd.Ping)]
        private void HandlePing(NetPacket packet)
        {
            OnPong?.Invoke(packet.GetData<PingResponse>());
        }

        [NetHandler(NetCmd.VersionCheck)]
        private void HandleVersionCheck(NetPacket packet)
        {
            var response = packet.GetData<VersionCheckResponse>();

            // Log ngay tại đây chứ không để chỗ nghe event lo: hai con số này là thứ người ta dán vào
            // báo lỗi, và nó phải có mặt kể cả khi chưa ai đăng ký event.
            if (response.Ok)
                this.Log($"Contract khớp: {Contract.Hash:X8}");
            else
                this.LogError($"Contract LỆCH — client {Contract.Hash:X8} ≠ server {response.ServerHash:X8}. " +
                              "Build lại Server/Shared để DLL trong Assets/Plugins/Shared/ khớp server.");

            OnVersionCheck?.Invoke(response);
        }

        [NetHandler(NetCmd.Echo)]
        private void HandleEcho(NetPacket packet)
        {
            OnEcho?.Invoke(packet.GetData<EchoResponse>());
        }

        [NetHandler(NetCmd.Error)]
        private void HandleError(NetPacket packet)
        {
            var error = packet.GetData<ErrorResponse>();
            this.LogError($"Server báo lỗi cmd {(NetCmd)error.FailedCmd}: {error.Code} — {error.Detail}");
        }

        [NetHandler(NetCmd.ServerInfo)]
        private void HandleServerInfo(NetPacket packet)
        {
            OnServerInfo?.Invoke(packet.GetData<ServerInfoResponse>());
        }
    }
}