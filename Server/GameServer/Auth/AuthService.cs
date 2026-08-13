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
        private readonly DbClient _dbClient;
        private readonly LoginRateLimiter _rateLimiter;

        public AuthService(DbClient dbClient, LoginRateLimiter rateLimiter)
        {
            _dbClient = dbClient;
            _rateLimiter = rateLimiter;
        }

        public async Task<AuthResponse> RegisterAsync(ClientSession session, RegisterRequest request)
        {
            string username = AccountNameRules.Normalize(request.Username);
            Log.Info($"{session.Tag} Đăng ký: {username.Cyan()} (mật khẩu {request.Password?.Length ?? 0} ký tự)");

            if (!AccountNameRules.IsValidUsername(username))
            {
                Log.Warn($"{session.Tag} Từ chối đăng ký {username.Cyan()}: tên không hợp lệ");
                return Fail(ErrorCode.InvalidInput);
            }

            if (!AccountNameRules.IsValidPassword(request.Password))
            {
                Log.Warn($"{session.Tag} Từ chối đăng ký {username.Cyan()}: mật khẩu không hợp lệ");
                return Fail(ErrorCode.InvalidInput);
            }

            (byte[] hash, byte[] salt, int iterations) = PasswordHasher.Hash(request.Password);

            var result = await _dbClient.CallAsync<AccountCreateRequest, AccountCreateResponse>(
                DbCmd.AccountCreate,
                new AccountCreateRequest
                {
                    Username = username,
                    PasswordHash = hash,
                    Salt = salt,
                    Iterations = iterations
                });

            if (!result.Created)
            {
                Log.Warn($"{session.Tag} Từ chối đăng ký {username.Cyan()}: tên đã có người dùng");
                return Fail(ErrorCode.AccountExists);
            }

            Log.Info($"{session.Tag} Tạo tài khoản {username.Cyan()} (id {result.AccountId.ToString().Green()})");

            // Đăng ký xong đăng nhập luôn — người chơi không phải gõ lại.
            return Authenticate(session, result.AccountId, username);
        }

        public async Task<AuthResponse> LoginAsync(ClientSession session, LoginRequest request)
        {
            if (!_rateLimiter.TryConsume(session.Id))
            {
                Log.Warn($"{session.Tag} Chặn đăng nhập: vượt hạn mức thử trong một phút");
                return Fail(ErrorCode.TooManyAttempts);
            }

            string username = AccountNameRules.Normalize(request.Username);
            Log.Info($"{session.Tag} Đăng nhập: {username.Cyan()} (mật khẩu {request.Password?.Length ?? 0} ký tự)");

            // Định dạng sai thì chắc chắn không có trong DB — nhưng vẫn trả InvalidCredentials
            // chứ không phải InvalidInput. Nói "tên này sai định dạng" là đã tiết lộ một mẩu thông tin.
            if (!AccountNameRules.IsValidUsername(username) || !AccountNameRules.IsValidPassword(request.Password))
            {
                // Lý do thật chỉ được nói trong log server — client luôn nhận InvalidCredentials chung.
                Log.Warn($"{session.Tag} Từ chối {username.Cyan()}: sai định dạng tên/mật khẩu");
                PasswordHasher.BurnEquivalentTime();
                return Fail(ErrorCode.InvalidCredentials);
            }

            var account = await _dbClient.CallAsync<AccountGetRequest, AccountGetResponse>(
                DbCmd.AccountGetByName, new AccountGetRequest { Username = username });

            if (!account.Found)
            {
                // Không có tài khoản thì trả về ngay sẽ NHANH hơn hẳn trường hợp có tài khoản
                // (vì bỏ qua 100.000 vòng PBKDF2). Kẻ tấn công chỉ cần bấm giờ là dò được
                // tài khoản nào tồn tại. Đốt đúng lượng thời gian đó để hai đường bằng nhau.
                Log.Warn($"{session.Tag} Từ chối {username.Cyan()}: tài khoản không tồn tại");
                PasswordHasher.BurnEquivalentTime();
                return Fail(ErrorCode.InvalidCredentials);
            }

            if (!PasswordHasher.Verify(request.Password, account.PasswordHash, account.Salt, account.Iterations))
            {
                Log.Warn($"{session.Tag} Từ chối {username.Cyan()}: sai mật khẩu");
                return Fail(ErrorCode.InvalidCredentials);
            }

            if (account.IsBanned)
            {
                Log.Warn($"{session.Tag} Từ chối {username.Cyan()}: tài khoản bị khoá");
                return Fail(ErrorCode.InvalidCredentials);
            }

            _rateLimiter.Reset(session.Id);
            KickPreviousSession(session, account.AccountId);

            _ = _dbClient.CallAsync<AccountTouchLoginRequest, DbOkResponse>(
                DbCmd.AccountTouchLogin, new AccountTouchLoginRequest { AccountId = account.AccountId });

            Log.Info($"{session.Tag} {username.Cyan()} đăng nhập thành công");
            return Authenticate(session, account.AccountId, username);
        }

        public AuthResponse Logout(ClientSession session)
        {
            Log.Info($"{session.Tag} {session.Username.Cyan()} đăng xuất");
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

            // Chỉ log ĐỘ DÀI — token là thứ thay cho mật khẩu, lộ ra log là lộ phiên.
            string sessionToken = SessionTokens.Issue(accountId);
            Log.Debug($"{session.Tag} Cấp token phiên cho {username.Cyan()} ({sessionToken.Length} ký tự)");

            return new AuthResponse
            {
                Success = true,
                Username = username,
                SessionToken = sessionToken,
            };
        }

        private static AuthResponse Fail(ErrorCode error)
        {
            return new AuthResponse { Success = false, Error = error };
        }
    }
}
