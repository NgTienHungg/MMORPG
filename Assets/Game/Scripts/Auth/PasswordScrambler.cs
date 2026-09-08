using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace MMORPG.Client.Auth
{
    /// <summary>
    /// Xáo mật khẩu trước khi ghi ra file save, và xáo ngược lại lúc đọc.
    ///
    /// <para><b>Đây KHÔNG phải mã hoá.</b> Khoá sinh từ id máy nên bất cứ đoạn code nào chạy trên chính
    /// máy đó đều mở lại được — kể cả code của người khác. Mục đích duy nhất là để mật khẩu không nằm
    /// trần trong file JSON: người ngồi cạnh mở file bằng Notepad sẽ không đọc ra gì, và chép file sang
    /// máy khác cũng vô dụng vì id máy đã khác.</para>
    ///
    /// <para>Cách duy nhất để không phải giữ mật khẩu trên máy người chơi là server cấp token đăng nhập
    /// lại dùng được nhiều phiên; hiện server chỉ nhận username + password nên mới cần lớp này.</para>
    /// </summary>
    public static class PasswordScrambler
    {
        /// <summary>Trộn thêm vào id máy để keystream không trùng với thứ khác cũng băm từ id máy.</summary>
        private const string SALT = "MMORPG.RememberLogin";

        public static string Scramble(string plain)
        {
            if (string.IsNullOrEmpty(plain))
                return string.Empty;

            byte[] bytes = Encoding.UTF8.GetBytes(plain);
            ApplyKeystream(bytes);
            return Convert.ToBase64String(bytes);
        }

        public static string Unscramble(string scrambled)
        {
            if (string.IsNullOrEmpty(scrambled))
                return string.Empty;

            try
            {
                byte[] bytes = Convert.FromBase64String(scrambled);
                ApplyKeystream(bytes);
                return Encoding.UTF8.GetString(bytes);
            }
            catch (FormatException)
            {
                // File bị sửa tay hoặc ghi dở dang. Không có gì để cứu, cũng chẳng đáng ném lỗi:
                // trả về rỗng thì ô mật khẩu trống và người chơi gõ lại là xong.
                return string.Empty;
            }
        }

        /// <summary>
        /// XOR tại chỗ với keystream sinh từ id máy. XOR là phép đối xứng — chạy lần nữa trên kết quả
        /// thì ra đúng chuỗi ban đầu — nên cả hai chiều xáo/giải xáo dùng chung một hàm này.
        /// </summary>
        private static void ApplyKeystream(byte[] bytes)
        {
            byte[] key = BuildKey();
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] ^= key[i % key.Length];
        }

        /// <summary>
        /// Băm id máy thành 32 byte khoá. Sinh khoá từ máy thay vì hằng số trong source vì hằng số nằm
        /// trong file build ra, ai mở cũng thấy — mà repo này cấm hard-code khoá trong code.
        /// </summary>
        private static byte[] BuildKey()
        {
            using SHA256 sha256 = SHA256.Create();
            return sha256.ComputeHash(Encoding.UTF8.GetBytes(SystemInfo.deviceUniqueIdentifier + SALT));
        }
    }
}
