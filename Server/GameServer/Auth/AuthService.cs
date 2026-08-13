using MMORPG.GameServer.Db;

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

        !
    }
}
