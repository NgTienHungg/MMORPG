using System;
using System.Collections.Generic;
using System.Reflection;
using MemoryPack;
using MMORPG.Shared.World;

namespace MMORPG.Shared.Net
{
    /// <summary>
    /// Dấu vân tay của HÌNH DẠNG contract: mọi giá trị NetCmd + mọi DTO [MemoryPackable] và danh sách
    /// property của chúng. Hai bên tính từ cùng một assembly (MMORPG.Shared) nên hai số khác nhau chỉ
    /// có một nghĩa: hai bên đang chạy hai bản DLL khác nhau.
    ///
    /// Tính bằng reflection chứ không phải một const gõ tay, vì thứ cần chống chính là QUÊN tăng số.
    /// Một hằng số mà người ta quên tăng còn tệ hơn không có — nó tạo cảm giác đã được bảo vệ.
    ///
    /// GIỚI HẠN, phải biết: nó bắt đổi HÌNH DẠNG, không bắt đổi HÀNH VI. Sửa công thức trong
    /// MovementRules.Step thì số này y nguyên.
    /// </summary>
    public static class Contract
    {
        /// <summary>Tính một lần cho cả đời process — reflection không rẻ, mà kết quả thì không đổi.</summary>
        public static uint Hash { get; } = Compute();

        private static uint Compute()
        {
            uint hash = Fnv1a.START;

            // OrderBy theo tên, KHÔNG theo thứ tự reflection trả về: thứ tự ấy không được bảo đảm và
            // trên thực tế khác nhau giữa CoreCLR và Mono. Bỏ phép sắp xếp là hai bên ra hai số khác
            // nhau dù cùng một DLL — và bạn sẽ đi tìm lỗi ở chỗ không có lỗi.
            var cmdNames = new List<string>(Enum.GetNames(typeof(NetCmd)));
            cmdNames.Sort(StringComparer.Ordinal);

            foreach (string name in cmdNames)
            {
                hash = Fnv1a.Mix(hash, name);
                hash = Fnv1a.Mix(hash, (int)Enum.Parse(typeof(NetCmd), name));
            }

            var dtoTypes = new List<Type>();

            foreach (Type type in typeof(Contract).Assembly.GetTypes())
            {
                if (type.GetCustomAttribute<MemoryPackableAttribute>() != null)
                    dtoTypes.Add(type);
            }

            dtoTypes.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));

            foreach (Type type in dtoTypes)
            {
                hash = Fnv1a.Mix(hash, type.FullName);

                var properties = new List<PropertyInfo>(
                    type.GetProperties(BindingFlags.Public | BindingFlags.Instance));

                properties.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

                foreach (PropertyInfo property in properties)
                {
                    hash = Fnv1a.Mix(hash, property.Name);

                    // Băm cả KIỂU: đổi int thành long mà giữ nguyên tên là đổi số byte trên dây, và
                    // đó đúng là loại lệch im lặng nhất — gói vẫn giải mã được, chỉ là ra số khác.
                    hash = Fnv1a.Mix(hash, property.PropertyType.FullName);
                }
            }

            return hash;
        }
    }
}
