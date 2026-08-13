using System.Collections.Concurrent;

namespace MMORPG.GameServer.Auth
{
    /// <summary>
    /// Giới hạn số lần đăng nhập sai. Không có nó thì một script thử vài trăm nghìn mật khẩu mỗi phút.
    ///
    /// Đếm theo session id là mức tối thiểu — kẻ tấn công chỉ cần nối lại là có bộ đếm mới.
    /// Bản thật phải đếm theo địa chỉ IP; ghi vào Phase 16 cùng với TLS và structured logging.
    /// </summary>
    public sealed class LoginRateLimiter
    {
        private const int MAX_ATTEMPTS = 5;
        private static readonly TimeSpan _window = TimeSpan.FromMinutes(1);

        private readonly ConcurrentDictionary<int, (int Count, DateTime WindowStart)> _attempts = new();

        /// <returns>false nghĩa là đã vượt hạn mức, phải từ chối.</returns>
        public bool TryConsume(int sessionId)
        {
            DateTime now = DateTime.UtcNow;

            (int count, DateTime start) = _attempts.AddOrUpdate(
                sessionId,
                addValueFactory: _ => (1, now),
                updateValueFactory: (_, old) =>
                    now - old.WindowStart > _window
                        ? (1, now)
                        : (old.Count + 1, old.WindowStart)
            );

            return count <= MAX_ATTEMPTS;
        }

        public void Reset(int sessionId) => _attempts.TryRemove(sessionId, out _);
    }
}
