using System.IO;
using HungNT;
using MMORPG.Shared.World;
using UnityEngine;

namespace MMORPG.Client.World
{
    /// <summary>
    /// Nạp hình dạng map từ file đã export. Client đọc ĐÚNG file server đọc — đó là toàn bộ lý do nó
    /// tồn tại, và cũng là lý do ở đây không có tí logic nào về hình dạng map.
    ///
    /// Resources là cách rẻ nhất cho hôm nay. Phase 18 chuyển nó sang Addressables/CDN cùng các bảng dữ
    /// liệu khác; lúc đó chỉ hàm Load này đổi, chỗ gọi giữ nguyên.
    /// </summary>
    public sealed class MapService
    {
        private const string RESOURCE_FOLDER = "Maps";

        /// <summary>
        /// Khuôn tên file map, {0} là id. Ở đây chứ không ở tool export: tool GHI theo khuôn này còn
        /// client ĐỌC theo nó, nên hai bên phải nhìn vào cùng một chuỗi — hằng số nằm bên nào cũng
        /// được, miễn là chỉ có một.
        /// </summary>
        public const string FILE_MAP_FORMAT = "map_{0}";

        /// <summary>Map đang đứng. Null cho tới lần Load đầu tiên.</summary>
        public MapGrid Current { get; private set; }

        public MapGrid Load(int mapId)
        {
            // Đã nạp đúng map này rồi thì thôi: Load được gọi mỗi lần vào world, mà vào world lại sau
            // khi logout là chuyện thường.
            if (Current != null && Current.MapId == mapId)
                return Current;

            string fileName = string.Format(FILE_MAP_FORMAT, mapId);

            // Không có đuôi .json trong đường dẫn: Resources.Load luôn bỏ phần đuôi file.
            var asset = Resources.Load<TextAsset>($"{RESOURCE_FOLDER}/{fileName}");

            if (asset == null)
                throw new FileNotFoundException($"Không thấy Resources/{RESOURCE_FOLDER}/{fileName}.json. Chạy Tools/MMORPG/Export Map.");

            Current = MapFile.Parse(asset.text);

            // In checksum ra để đối chiếu với dòng server in lúc khởi động. Hai số khác nhau nghĩa là
            // hai bên đang chạy hai map khác nhau — biết ngay ở đây, thay vì đoán qua triệu chứng.
            this.Log($"Map {Current.Name} #{Current.MapId} — {Current.Width}×{Current.Height} ô, " +
                     $"checksum {Current.Checksum():X8}");

            return Current;
        }
    }
}
