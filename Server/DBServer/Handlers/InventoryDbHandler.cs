using MMORPG.DBServer.Net;
using MMORPG.DBServer.Repositories;
using MMORPG.Shared.Db;
using MMORPG.Shared.Dto.Db;

namespace MMORPG.DBServer.Handlers
{
    public static class InventoryDbHandler
    {
        /// <summary>Gán một lần trong <c>Program.cs</c> của DBServer.</summary>
        public static InventoryRepository Repository { get; set; }

        [DbHandler(DbCmd.InventoryLoad)]
        public static async Task<DbResult> OnLoad(DbRequest req)
        {
            return DbResult.Ok(await Repository.LoadAsync(req.GetData<InventoryLoadRequest>()));
        }

        [DbHandler(DbCmd.InventorySave)]
        public static async Task<DbResult> OnSave(DbRequest req)
        {
            await Repository.SaveAsync(req.GetData<InventorySaveRequest>());
            return DbResult.Ok(new DbOkResponse { Success = true });
        }
    }
}
