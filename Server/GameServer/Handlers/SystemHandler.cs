using MMORPG.GameServer.Net;
using MMORPG.ServerCore;
using MMORPG.Shared.Dto;
using MMORPG.Shared.Net;

namespace MMORPG.GameServer.Handlers
{
    /// <summary>
    /// Handler cho nhóm lệnh hệ thống (1–99).
    /// Handler chỉ làm 3 việc: giải mã → gọi logic → đóng gói kết quả. Không chứa nghiệp vụ.
    /// </summary>
    public static class SystemHandler
    {
        [TcpHandler(NetCmd.Ping)]
        public static NetResult OnPing(NetRequest req)
        {
            var request = req.GetData<PingRequest>();

            return NetResult.Ok(new PingResponse
            {
                ClientTimeMs = request.ClientTimeMs,
                ServerTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            });
        }

        [TcpHandler(NetCmd.Echo)]
        public static NetResult OnEcho(NetRequest req)
        {
            var request = req.GetData<EchoRequest>();
            Log.Debug($"{req.Session.Tag} echo: \"{request.Message}\"");

            return NetResult.Ok(new EchoResponse
            {
                Message = request.Message,
                ServerTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            });
        }
    }
}
