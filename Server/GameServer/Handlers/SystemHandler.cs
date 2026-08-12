using MMORPG.GameServer.Db;
using MMORPG.GameServer.Net;
using MMORPG.ServerCore;
using MMORPG.Shared.Db;
using MMORPG.Shared.Dto;
using MMORPG.Shared.Dto.Db;
using MMORPG.Shared.Net;

namespace MMORPG.GameServer.Handlers
{
    public static class SystemHandler
    {
        /// <summary>Gán một lần trong <c>Program.cs</c>, giống <c>ServerMetaDbHandler.Repository</c>.</summary>
        public static DbClient Db { get; set; } = null!;

        [TcpHandler(NetCmd.Ping)]
        public static Task<NetResult> OnPing(NetRequest req)
        {
            var request = req.GetData<PingRequest>();

            // Không có gì để chờ — trả Task đã hoàn thành, không tốn một lần chuyển ngữ cảnh nào.
            return Task.FromResult(NetResult.Ok(new PingResponse
            {
                ClientTimeMs = request.ClientTimeMs,
                ServerTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            }));
        }

        [TcpHandler(NetCmd.Echo)]
        public static Task<NetResult> OnEcho(NetRequest req)
        {
            var request = req.GetData<EchoRequest>();
            Log.Debug($"{req.Session.Tag} echo: \"{request.Message}\"");

            return Task.FromResult(NetResult.Ok(new EchoResponse
            {
                Message = request.Message,
                ServerTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            }));
        }

        [TcpHandler(NetCmd.ServerInfo)]
        public static async Task<NetResult> OnServerInfo(NetRequest req)
        {
            var db = await Db.CallAsync<ServerMetaGetRequest, ServerMetaGetResponse>(
                DbCmd.ServerMetaGet, new ServerMetaGetRequest { Key = "server_name" });

            return NetResult.Ok(new ServerInfoResponse
            {
                ServerName = db.Found ? db.Value : "(chưa đặt tên)",
                OnlineCount = SessionRegistry.Count,
            });
        }
    }
}
