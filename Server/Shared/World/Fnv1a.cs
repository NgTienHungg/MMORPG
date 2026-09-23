using System;

namespace MMORPG.Shared.World
{
    /// <summary>
    /// Băm FNV-1a 32 bit — dấu vân tay cho mọi bảng dữ liệu của dự án.
    ///
    /// Không phải hàm băm mật mã, và không cần là: câu hỏi nó trả lời là "hai bên có đang cầm cùng một
    /// bảng không", không phải "có ai cố tình làm giả bảng không". Đổi lại nó ngắn, không cấp phát, và
    /// cho cùng kết quả trên CoreCLR lẫn Mono/IL2CPP.
    /// </summary>
    public static class Fnv1a
    {
        public const uint START = 2166136261u;

        private const uint PRIME = 16777619u;

        /// <summary>
        /// Nuốt cả một dãy byte. Đây là lối vào mà <see cref="ConfigFingerprint"/> dùng — băm thẳng
        /// byte đã tuần tự hoá thay vì liệt kê từng trường bằng tay.
        /// </summary>
        public static uint Mix(uint hash, ReadOnlySpan<byte> bytes)
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash *= PRIME;
            }

            return hash;
        }

        /// <summary>Nuốt từng byte một. Cộng thẳng cả int vào thì hai bảng hoán vị vài ô vẫn ra cùng số.</summary>
        public static uint Mix(uint hash, int value)
        {
            for (int shift = 0; shift < 32; shift += 8)
            {
                hash ^= (uint)((value >> shift) & 0xFF);
                hash *= PRIME;
            }

            return hash;
        }

        public static uint Mix(uint hash, string value)
        {
            if (value == null)
                return Mix(hash, -1);

            // Băm cả độ dài: nếu không thì {"ab","c"} và {"a","bc"} nối lại giống hệt nhau.
            hash = Mix(hash, value.Length);

            for (int i = 0; i < value.Length; i++)
                hash = Mix(hash, value[i]);

            return hash;
        }
    }
}
