using MMORPG.GameServer.Db;
using MMORPG.ServerCore;
using MMORPG.Shared.Db;
using MMORPG.Shared.Dto;
using MMORPG.Shared.Dto.Db;
using MMORPG.Shared.Net;

namespace MMORPG.GameServer.Auth
{
    /// <summary>
    /// Toàn bộ nghiệp vụ đăng ký / đăng nhập. Handler không chứa gì ngoài lời gọi vào đây.
    /// </summary>
    public sealed class AuthService
    {
        private readonly DbClient _db;
        private readonly LoginRateLimiter _rateLimiter;

        public AuthService(DbClient db, LoginRateLimiter rateLimiter)
        {
            _db = db;
            _rateLimiter = rateLimiter;
        }

        public async Task<AuthResponse> RegisterAsync(ClientSession session, RegisterRequest request)
        {
            string username = AccountNameRules.Normalize(request.Username);

            if (!AccountNameRules.IsValidUsername(username))
                return Fail(ErrorCode.InvalidInput);

            if (!AccountNameRules.IsValidPassword(request.Password))
                return Fail(ErrorCode.InvalidInput);

            (byte[] hash, byte[] salt, int iterations) = PasswordHasher.Hash(request.Password);

            var result = await _db.CallAsync<AccountCreateRequest, AccountCreateResponse>(
                DbCmd.AccountCreate,
                new AccountCreateRequest
                {
                    Username = username,
                    PasswordHash = hash,
                    Salt = salt,
                    Iterations = iterations
                });

            if (!result.Created)
                return Fail(ErrorCode.AccountExists);

            Log.Info($"{session.Tag} Tạo tài khoản {username.Cyan()} (id {result.AccountId.ToString().Green()})");

            // Đăng ký xong đăng nhập luôn — người chơi không phải gõ lại.
            return Authenticate(session, result.AccountId, username);
        }

        public async Task<AuthResponse> LoginAsync(ClientSession session, LoginRequest request)
        {
            if (!_rateLimiter.TryConsume(session.Id))
                return Fail(ErrorCode.TooManyAttempts);

            string username = AccountNameRules.Normalize(request.Username);

            // Định dạng sai thì chắc chắn không có trong DB — nhưng vẫn trả InvalidCredentials
            // chứ không phải InvalidInput. Nói "tên này sai định dạng" là đã tiết lộ một mẩu thông tin.
            if (!AccountNameRules.IsValidUsername(username) || !AccountNameRules.IsValidPassword(request.Password))
            {
                PasswordHasher.BurnEquivalentTime();
                return Fail(ErrorCode.InvalidCredentials);
            }

            var account = await _db.CallAsync<AccountGetRequest, AccountGetResponse>(
                DbCmd.AccountGetByName, new AccountGetRequest { Username = username });

            if (!account.Found)
            {
                // Không có tài khoản thì trả về ngay sẽ NHANH hơn hẳn trường hợp có tài khoản
                // (vì bỏ qua 100.000 vòng PBKDF2). Kẻ tấn công chỉ cần bấm giờ là dò được
                // tài khoản nào tồn tại. Đốt đúng lượng thời gian đó để hai đường bằng nhau.
                PasswordHasher.BurnEquivalentTime();
                return Fail(ErrorCode.InvalidCredentials);
            }

            if (!PasswordHasher.Verify(request.Password, account.PasswordHash, account.Salt, account.Iterations))
                return Fail(ErrorCode.InvalidCredentials);

            if (account.IsBanned)
                return Fail(ErrorCode.InvalidCredentials);

            _rateLimiter.Reset(session.Id);
            KickPreviousSession(session, account.AccountId);

            _ = _db.CallAsync<AccountTouchLoginRequest, DbOkResponse>(
                DbCmd.AccountTouchLogin, new AccountTouchLoginRequest { AccountId = account.AccountId });

            Log.Info($"{session.Tag} {username.Cyan()} đăng nhập thành công");
            return Authenticate(session, account.AccountId, username);
        }

        public AuthResponse Logout(ClientSession session)
        {
            SessionTokens.Revoke(session.AccountId);
            session.MarkLoggedOut();

            return new AuthResponse { Success = true };
        }

        /// <summary>
        /// Đá session cũ của cùng tài khoản. Chọn "người mới thắng" vì tình huống thật hay gặp nhất
        /// là người chơi rớt mạng: session cũ còn treo trên server nhưng đã chết ở phía họ.
        /// Nếu chọn "người cũ thắng" thì họ phải ngồi chờ hết timeout mới vào lại được.
        /// </summary>
        private static void KickPreviousSession(ClientSession current, long accountId)
        {
            foreach (ClientSession other in SessionRegistry.All)
            {
                if (other.Id == current.Id || other.AccountId != accountId)
                    continue;

                other.Kick("Tài khoản của bạn vừa đăng nhập ở nơi khác.");
            }
        }

        private static AuthResponse Authenticate(ClientSession session, long accountId, string username)
        {
            session.MarkAuthenticated(accountId, username);

            return new AuthResponse
            {
                Success = true,
                Username = username,
                SessionToken = SessionTokens.Issue(accountId),
            };
        }

        private static AuthResponse Fail(ErrorCode error) =>
            new() { Success = false, Error = error };
    }
}
