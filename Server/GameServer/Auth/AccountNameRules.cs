using System.Text.RegularExpressions;

namespace MMORPG.GameServer.Auth
{
    /// <summary>
    /// Luật định dạng tài khoản. Client cũng kiểm mấy luật này, nhưng chỉ để báo lỗi sớm cho đẹp —
    /// bản kiểm ở đây mới là bản có hiệu lực. Client là thứ người chơi sửa được.
    /// </summary>
    public static class AccountNameRules
    {
        public const int USERNAME_MIN = 3;
        public const int USERNAME_MAX = 16;
        public const int PASSWORD_MIN = 6;
        public const int PASSWORD_MAX = 64;

        private static readonly Regex _usernamePattern = new("^[a-z0-9_]+$", RegexOptions.Compiled);

        /// <summary>Chuẩn hoá về dạng lưu trong DB. Gọi TRƯỚC mọi so sánh và mọi query.</summary>
        public static string Normalize(string username)
        {
            return (username ?? string.Empty).Trim().ToLowerInvariant();
        }

        public static bool IsValidUsername(string normalized)
        {
            return normalized.Length >= USERNAME_MIN &&
                   normalized.Length <= USERNAME_MAX &&
                   _usernamePattern.IsMatch(normalized);
        }

        public static bool IsValidPassword(string password)
        {
            return !string.IsNullOrEmpty(password) &&
                   password.Length >= PASSWORD_MIN &&
                   password.Length <= PASSWORD_MAX;
        }
    }
}
