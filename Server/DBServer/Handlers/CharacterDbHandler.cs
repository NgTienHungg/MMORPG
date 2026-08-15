using MMORPG.DBServer.Net;
using MMORPG.DBServer.Repositories;
using MMORPG.ServerCore;
using MMORPG.Shared.Db;
using MMORPG.Shared.Dto.Db;

namespace MMORPG.DBServer.Handlers
{
    public static class CharacterDbHandler
    {
        public static CharacterRepository Repository { get; set; }

        [DbHandler(DbCmd.CharacterGetOrCreate)]
        public static async Task<DbResult> OnGetOrCreate(DbRequest req)
        {
            var request = req.GetData<CharacterGetOrCreateRequest>();
            var response = await Repository.GetOrCreateAsync(request);

            if (response.Created)
                Log.Info($"account:{request.AccountId} tạo nhân vật '{request.Name}' id:{response.Character.CharacterId}");

            return DbResult.Ok(response);
        }

        [DbHandler(DbCmd.CharacterSavePosition)]
        public static async Task<DbResult> OnSavePosition(DbRequest req)
        {
            var request = req.GetData<CharacterSavePositionRequest>();
            await Repository.SavePositionAsync(request);

            return DbResult.Ok(new DbOkResponse { Success = true });
        }
    }
}
