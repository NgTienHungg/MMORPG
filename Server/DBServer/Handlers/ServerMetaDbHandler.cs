using MMORPG.DBServer.Net;
using MMORPG.DBServer.Repositories;
using MMORPG.Shared.Db;
using MMORPG.Shared.Dto.Db;

namespace MMORPG.DBServer.Handlers
{
    public static class ServerMetaDbHandler
    {
        /// <summary>
        /// Handler là static (để dispatcher quét được) nhưng repository thì không —
        /// gán một lần lúc khởi động là đủ cho một process chỉ có một database.
        /// Khi nào cần nhiều nguồn dữ liệu thì đổi sang DI thật.
        /// </summary>
        public static ServerMetaRepository Repository { get; set; } = null!;

        [DbHandler(DbCmd.Ping)]
        public static async Task<DbResult> OnPing(DbRequest req)
        {
            var request = req.GetData<DbPingRequest>();

            return DbResult.Ok(new DbPingResponse
            {
                SentAtMs = request.SentAtMs,
                SchemaVersion = await Repository.GetSchemaVersionAsync(),
            });
        }

        [DbHandler(DbCmd.ServerMetaGet)]
        public static async Task<DbResult> OnGet(DbRequest req)
        {
            var request = req.GetData<ServerMetaGetRequest>();
            string? value = await Repository.GetAsync(request.Key);

            return DbResult.Ok(new ServerMetaGetResponse
            {
                Found = value != null,
                Value = value ?? string.Empty,
            });
        }

        [DbHandler(DbCmd.ServerMetaSet)]
        public static async Task<DbResult> OnSet(DbRequest req)
        {
            var request = req.GetData<ServerMetaSetRequest>();
            await Repository.SetAsync(request.Key, request.Value);

            return DbResult.Ok(new DbOkResponse { Success = true });
        }
    }
}
