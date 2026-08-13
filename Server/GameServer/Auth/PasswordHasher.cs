using System.Security.Cryptography;
using System.Text;

namespace MMORPG.GameServer.Auth
{
    /// <summary>
    /// Băm và kiểm mật khẩu bằng PBKDF2-HMAC-SHA256.
    ///
    /// PBKDF2 được thiết kế để CHẬM một cách có kiểm soát. Đó không phải nhược điểm mà là toàn bộ mục đích:
    /// SHA256 trần băm được hàng tỉ mật khẩu mỗi giây trên một GPU tầm trung, nên nếu file DB bị lộ thì mọi
    /// mật khẩu ngắn đều đoán ra trong vài phút. Với 100.000 vòng lặp, cùng con GPU đó chỉ thử được
    /// vài chục nghìn lần mỗi giây.
    /// </summary>
    public static class PasswordHasher
    {
        /// <summary>
        /// Khuyến nghị của OWASP cho PBKDF2-SHA256 hiện là 600.000 vòng. Ta dùng 100.000 cho dự án học
        /// để đăng nhập lúc dev không phải chờ — con số này lưu theo từng tài khoản trong DB nên nâng lên
        /// lúc nào cũng được mà không làm hỏng tài khoản cũ.
        /// </summary>
        public const int DEFAULT_ITERATIONS = 100_000;

        private const int SALT_SIZE = 16;
        private const int HASH_SIZE = 32;

        public static (byte[] Hash, byte[] Salt, int Iterations) Hash(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SALT_SIZE);
            byte[] hash = Derive(password, salt, DEFAULT_ITERATIONS);

            return (hash, salt, DEFAULT_ITERATIONS);
        }

        public static bool Verify(string password, byte[] expectedHash, byte[] salt, int iterations)
        {
            if (salt.Length == 0 || iterations <= 0)
                return false;

            byte[] actual = Derive(password, salt, iterations);

            // So sánh thời gian cố định. `actual.SequenceEqual(expected)` dừng ngay khi gặp byte đầu tiên
            // khác nhau, nên thời gian chạy tiết lộ ta đã đoán đúng bao nhiêu byte đầu. Đo đủ nhiều lần
            // là dựng lại được cả hash.
            return CryptographicOperations.FixedTimeEquals(actual, expectedHash);
        }

        /// <summary>
        /// Băm giả để tiêu tốn đúng lượng thời gian như một lần kiểm thật.
        /// Dùng khi tài khoản KHÔNG tồn tại — xem <c>AuthService.LoginAsync</c>.
        /// </summary>
        public static void BurnEquivalentTime() =>
            Derive("dummy", new byte[SALT_SIZE], DEFAULT_ITERATIONS);

        private static byte[] Derive(string password, byte[] salt, int iterations) =>
            Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, HASH_SIZE);
    }
}
