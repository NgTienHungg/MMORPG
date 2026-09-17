using System;

namespace MMORPG.Shared.World
{
    /// <summary>
    /// Nạp bản đồ từ nội dung dạng chữ và tính hash phiên bản của nó.
    /// Bản đồ không còn là hằng số trong code: nó là DỮ LIỆU, có một bản gốc duy nhất ở file,
    /// và client nhận đúng bản mà server đang chạy.
    /// </summary>
    public static class Maps
    {
        /// <summary>Tách nội dung file thành các hàng, bỏ dòng trống và ký tự xuống dòng của Windows.</summary>
        public static string[] SplitRows(string content)
        {
            return content.Replace("\r", string.Empty)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// Hash phiên bản của một bảng dữ liệu. FNV-1a 32-bit: không phải hàm băm mật mã, chỉ cần
        /// hai tính chất — cùng nội dung cho cùng số, và đổi một ký tự thì số đổi. Đặt ở Shared để
        /// hai bên tính ra cùng kết quả; mỗi bên tự viết một hàm băm là chép tay contract.
        /// </summary>
        public static int Version(string[] rows)
        {
            unchecked
            {
                const int OFFSET = (int)2166136261;
                const int PRIME = 16777619;

                int hash = OFFSET;

                foreach (string row in rows)
                {
                    foreach (char c in row)
                    {
                        hash = (hash ^ c) * PRIME;
                    }

                    // Băm cả ranh giới hàng: nếu không, hai map cắt hàng khác nhau mà nối lại
                    // ra cùng một chuỗi sẽ cho cùng hash.
                    hash = (hash ^ '\n') * PRIME;
                }

                return hash;
            }
        }
    }
}
