using System.Diagnostics.CodeAnalysis;
using MMORPG.Shared.World;

namespace MMORPG.Shared.Tests
{
    public class MapFileTests
    {
        /// <summary>
        /// Lưới 4×3 có đủ ba loại ô và KHÔNG đối xứng theo trục Y — cố ý, để phép lật trục sai thì bài
        /// test đỏ. Origin âm cũng là cố ý: map thật có origin âm.
        /// </summary>
        private static MapGrid BuildSample()
        {
            var cells = new[]
            {
                // cy = OriginY (hàng dưới cùng)
                CellType.Solid, CellType.Solid, CellType.Solid, CellType.Solid,
                // cy = OriginY + 1
                CellType.Empty, CellType.OneWay, CellType.OneWay, CellType.Empty,
                // cy = OriginY + 2
                CellType.Empty, CellType.Empty, CellType.Empty, CellType.Solid,
            };

            var spawns = new List<SpawnPoint>
            {
                new SpawnPoint { Id = MapGrid.DEFAULT_SPAWN_ID, X = 0.5f, Y = 1f },
            };

            return new MapGrid(7, "Test Map", "Maps/TestMap", originX: -3, originY: -2,
                width: 4, height: 3, spawns, null, cells);
        }

        [Fact]
        public void Write_then_parse_gives_back_the_same_grid()
        {
            MapGrid original = BuildSample();
            MapGrid parsed = MapFile.Parse(MapFile.Write(original));

            Assert.Equal(original.MapId, parsed.MapId);
            Assert.Equal(original.OriginX, parsed.OriginX);
            Assert.Equal(original.OriginY, parsed.OriginY);
            Assert.Equal(original.Width, parsed.Width);
            Assert.Equal(original.Height, parsed.Height);
            Assert.Equal(original.PrefabKey, parsed.PrefabKey);
            Assert.Equal(original.DefaultSpawn.X, parsed.DefaultSpawn.X);

            for (int cy = original.OriginY; cy < original.OriginY + original.Height; cy++)
            {
                for (int cx = original.OriginX; cx < original.OriginX + original.Width; cx++)
                    Assert.Equal(original.At(cx, cy), parsed.At(cx, cy));
            }

            // Và phép so rẻ nhất — chính là phép hai đầu dây sẽ dùng để tự kiểm lúc chạy.
            Assert.Equal(original.Checksum(), parsed.Checksum());
        }

        /// <summary>
        /// Số version dựng từ chính hằng số chứ không gõ tay: tăng FORMAT_VERSION thì bài test đi
        /// theo, thay vì âm thầm thành một phép Replace không khớp gì cả.
        /// </summary>
        private static string VersionField => $"\"{nameof(MapFileData.Version)}\": {MapFile.FORMAT_VERSION}";

        [Fact]
        public void Parse_rejects_unknown_format_version()
        {
            string json = MapFile.Write(BuildSample()).Replace(VersionField, "\"Version\": 99");

            Assert.Throws<FormatException>(() => MapFile.Parse(json));
        }

        [Fact]
        public void Parse_rejects_row_with_wrong_cell_count()
        {
            // Hàng đáy của lưới mẫu là bốn ô Solid. Cắt đi một ô để hàng lệch số ô so với hàng đầu.
            string json = MapFile.Write(BuildSample()).Replace("\"1 1 1 1\"", "\"1 1 1\"");

            Assert.Throws<FormatException>(() => MapFile.Parse(json));
        }

        /// <summary>
        /// Số nào cũng "đúng hình thức", nên đây là chỗ duy nhất chặn một id không có trong CellType.
        /// Đúng cái giá phải trả khi đổi từ ký tự sang id — và bài test này là thứ trả giá đó.
        /// </summary>
        [Fact]
        public void Parse_rejects_unknown_cell_id()
        {
            string json = MapFile.Write(BuildSample()).Replace("\"0 2 2 0\"", "\"0 9 2 0\"");

            Assert.Throws<FormatException>(() => MapFile.Parse(json));
        }

        /// <summary>
        /// Bài này kiểm QUYẾT ĐỊNH THIẾT KẾ, không kiểm code: file map do tool sinh, nên code hôm nay
        /// phải đọc được file mà bản mai này thêm trường vào. Đó là toàn bộ lý do chọn JSON — và đây là
        /// thứ duy nhất canh giữ nó.
        /// </summary>
        [Fact]
        public void Parse_ignores_fields_it_does_not_know()
        {
            string json = MapFile.Write(BuildSample())
                .Replace(VersionField, $"\"portals\": [ {{ \"x\": 3, \"toMapId\": 2 }} ],\n  {VersionField}");

            // Chốt rằng phép Replace ĐÃ chèn được: không có dòng này thì một lần đổi tên trường biến
            // bài test thành "parse một file y hệt bản gốc" và nó xanh mà chẳng kiểm gì.
            Assert.Contains("portals", json);

            MapGrid parsed = MapFile.Parse(json);

            Assert.Equal(BuildSample().Checksum(), parsed.Checksum());
        }
    }
}
