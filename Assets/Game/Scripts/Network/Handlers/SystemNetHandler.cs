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

        [NetHandler(NetCmd.Ping)]
        private void HandlePing(NetPacket packet) => OnPong?.Invoke(packet.GetData<PingResponse>());

        [NetHandler(NetCmd.Echo)]
        private void HandleEcho(NetPacket packet) => OnEcho?.Invoke(packet.GetData<EchoResponse>());

        [NetHandler(NetCmd.Error)]
        private void HandleError(NetPacket packet)
        {
            var error = packet.GetData<ErrorResponse>();
            this.LogError($"Server báo lỗi cmd {(NetCmd)error.FailedCmd}: {error.Code} — {error.Detail}");
        }
    }
}
