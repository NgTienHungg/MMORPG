using MMORPG.DBServer.Net;
using MMORPG.DBServer.Repositories;
using MMORPG.ServerCore;
using MMORPG.Shared.Db;
using MMORPG.Shared.Dto.Db;

namespace MMORPG.DBServer.Handlers
{
    public static class AccountDbHandler
    {
        /// <summary>Gán một lần trong <c>Program.cs</c>.</summary>
        public static AccountRepository Repository { get; set; }

        [DbHandler(DbCmd.AccountGetByName)]
        public static async Task<DbResult> OnGetByName(DbRequest req)
        {
            var request = req.GetData<AccountGetRequest>();
            AccountGetResponse account = await Repository.GetByUsernameAsync(request.Username);

            Log.Debug($"AccountGetByName {request.Username.Cyan()} → {(account.Found ? "thấy".Green() : "không thấy".Yellow())}");
            return DbResult.Ok(account);
        }

        [DbHandler(DbCmd.AccountCreate)]
        public static async Task<DbResult> OnCreate(DbRequest req)
        {
            var request = req.GetData<AccountCreateRequest>();
            AccountCreateResponse result = await Repository.CreateAsync(request);

            if (result.Created)
                Log.Info($"Tạo tài khoản {request.Username.Cyan()} → id {result.AccountId.ToString().Green()}");
            else
                Log.Warn($"Không tạo được {request.Username.Cyan()}: tên đã tồn tại");

            return DbResult.Ok(result);
        }

        [DbHandler(DbCmd.AccountTouchLogin)]
        public static async Task<DbResult> OnTouchLogin(DbRequest req)
        {
            var request = req.GetData<AccountTouchLoginRequest>();
            await Repository.TouchLoginAsync(request.AccountId);

            Log.Debug($"Ghi last_login cho account {request.AccountId.ToString().Magenta()}");
            return DbResult.Ok(new DbOkResponse { Success = true });
        }
    }
}
