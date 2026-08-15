using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace MMORPG.GameServer.Auth
{
    /// <summary>
    /// Token phiên, giữ trong RAM — cấp khi đăng nhập, thu hồi khi đăng xuất.
    ///
    /// Server restart là mất hết token — đúng như mong muốn ở giai đoạn này: người chơi đăng nhập lại,
    /// không có gì hỏng. Muốn token sống qua restart thì phải lưu DB, và lúc đó phải nghĩ tới hạn dùng
    /// và thu hồi — chưa cần.
    /// </summary>
    public static class SessionTokens
    {
        private static readonly ConcurrentDictionary<long, string> _byAccount = new();

        public static string Issue(long accountId)
        {
            // 32 byte ngẫu nhiên MẬT MÃ. Không dùng Guid, không dùng Random —
            // cả hai đều đoán được nếu biết đủ mẫu.
            string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            _byAccount[accountId] = token;

            return token;
        }

        public static bool Validate(long accountId, string token)
        {
            return _byAccount.TryGetValue(accountId, out string known) &&
                   !string.IsNullOrEmpty(token) &&
                   CryptographicOperations.FixedTimeEquals(
                       System.Text.Encoding.ASCII.GetBytes(known),
                       System.Text.Encoding.ASCII.GetBytes(token));
        }

        public static void Revoke(long accountId)
        {
            _byAccount.TryRemove(accountId, out _);
        }
    }
}
