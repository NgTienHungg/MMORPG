using MMORPG.DBServer.Net;
using MMORPG.DBServer.Repositories;
using MMORPG.Shared.Db;
using MMORPG.Shared.Dto.Db;

namespace MMORPG.DBServer.Handlers
{
    public static class AccountDbHandler
    {
        public static AccountRepository Repository { get; set; } = null;

        [DbHandler(DbCmd.AccountGetByName)]
        public static async Task<DbResult> OnGetByName(DbRequest req)
        {
            var request = req.GetData<AccountGetRequest>();
            return DbResult.Ok(await Repository.GetByUsernameAsync(request.Username));
        }

        [DbHandler(DbCmd.AccountCreate)]
        public static async Task<DbResult> OnCreate(DbRequest req)
        {
            var request = req.GetData<AccountCreateRequest>();
            return DbResult.Ok(await Repository.CreateAsync(request));
        }

        [DbHandler(DbCmd.AccountTouchLogin)]
        public static async Task<DbResult> OnTouchLogin(DbRequest req)
        {
            var request = req.GetData<AccountTouchLoginRequest>();
            await Repository.TouchLoginAycn(request.AccountId);
            return DbResult.Ok(new DbOkResponse { Success = true });
        }
    }
}
