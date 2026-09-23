using MMORPG.GameServer.Boot;
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
        // Xem ghi chú ở AuthHandler: property tra lúc DÙNG, không phải field khởi tạo lúc nạp class.
        private static DbClient DbClient => ServerServices.Get<DbClient>();

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

        [TcpHandler(NetCmd.Echo, MinState = SessionState.Verified)]
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

        [TcpHandler(NetCmd.ServerInfo, MinState = SessionState.Verified)]
        public static async Task<NetResult> OnServerInfo(NetRequest req)
        {
            var serverMeta = await DbClient.CallAsync<ServerMetaGetRequest, ServerMetaGetResponse>(
                DbCmd.ServerMetaGet, new ServerMetaGetRequest { Key = "server_name" });

            return NetResult.Ok(new ServerInfoResponse
            {
                ServerName = serverMeta.Found ? serverMeta.Value : "(chưa đặt tên)",
                OnlineCount = SessionRegistry.Count,
            });
        }

        [TcpHandler(NetCmd.VersionCheck)]
        public static Task<NetResult> OnVersionCheck(NetRequest req)
        {
            var request = req.GetData<VersionCheckRequest>();
            bool ok = request.ContractHash == Contract.Hash;

            if (ok)
            {
                req.Session.MarkVerified();
            }
            else
            {
                Log.Warn($"{req.Session.Tag} Contract lệch: client {request.ContractHash:X8} " +
                         $"≠ server {Contract.Hash.ToString("X8").Red()}");
            }

            return Task.FromResult(NetResult.Ok(new VersionCheckResponse
            {
                Ok = ok,
                ServerHash = Contract.Hash,
            }));
        }
    }
}
