using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace MMORPG.Shared.World
{
    /// <summary>
    /// Đọc và ghi file map JSON. Hai chiều nằm cùng một chỗ có chủ đích: tool export gọi Write, hai
    /// đầu dây gọi Parse — nên định dạng chỉ có một bản mô tả duy nhất, và bài test round-trip ở
    /// Shared.Tests kiểm được nó bằng máy thay vì bằng mắt.
    /// </summary>
    public static class MapFile
    {
        /// <summary>
        /// Version của ĐỊNH DẠNG, không phải của map. Luật: thêm một trường tuỳ chọn thì GIỮ NGUYÊN số
        /// này (JSON tự lo — trường thiếu về mặc định, trường lạ bị bỏ qua); chỉ tăng khi đổi ý nghĩa,
        /// đổi tên hoặc xoá một trường đã có. Đọc phải version lạ thì ném ngay chứ không cố đoán: một
        /// file map đọc sai một nửa còn tệ hơn một file map không đọc được.
        /// </summary>
        public const int FORMAT_VERSION = 1;

        public const char CHAR_EMPTY = '.';
        public const char CHAR_SOLID = '#';
        public const char CHAR_ONE_WAY = '=';

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,

            // Trường lạ thì BỎ QUA. Có chủ đích, và ngược với file config gõ tay ở Phase 12 (nơi sẽ
            // dùng Error): file này do TOOL sinh nên không có lỗi chính tả để bắt, còn cái ta cần là
            // code hôm nay đọc được file mà phiên bản mai này thêm trường vào.
            MissingMemberHandling = MissingMemberHandling.Ignore,
        };

        public static MapGrid Parse(string json)
        {
            MapDefinition? definition = JsonConvert.DeserializeObject<MapDefinition>(json, Settings);

            if (definition == null)
                throw new FormatException("File map rỗng hoặc không phải JSON hợp lệ.");

            if (definition.Version != FORMAT_VERSION)
                throw new FormatException($"File map ghi version {definition.Version}, code chỉ đọc được version {FORMAT_VERSION}. Export lại map.");

            // Rút ra biến cục bộ rồi mới kiểm null. Kiểm thẳng trên property cũng chạy đúng, nhưng
            // phân tích nullable của C# chỉ nhớ chắc chắn trạng thái null của BIẾN CỤC BỘ — kiểm trên
            // property rồi dùng nó sau vài dòng là cách rẻ nhất để lãnh một đống cảnh báo CS8604.
            CellPoint? origin = definition.Origin;
            List<string>? rows = definition.Cells;
            List<SpawnPoint>? spawns = definition.Spawns;

            // Ba phép kiểm này là chỗ trả tiền cho việc để DTO cho phép null: thiếu trường thì người
            // đọc log biết THIẾU CÁI GÌ, thay vì một NullReferenceException ở dòng nào đó xa hơn.
            if (origin == null)
                throw new FormatException("Thiếu trường \"origin\".");

            if (rows == null || rows.Count == 0)
                throw new FormatException("Thiếu lưới ô — trường \"cells\" rỗng.");

            if (spawns == null || spawns.Count == 0)
                throw new FormatException("Map phải có ít nhất một điểm trong \"spawns\".");

            // Kích thước SUY RA từ mảng, không đọc từ một trường riêng: không có trường thì không có
            // cách nào để file tự mâu thuẫn với chính nó.
            int height = rows.Count;
            int width = rows[0].Length;

            var cells = new CellType[width * height];

            for (int row = 0; row < height; row++)
            {
                string line = rows[row];

                // Chuỗi rỗng bắt luôn cả trường hợp JSON ghi null trong mảng — không có hàng nào hợp
                // lệ mà rỗng, vì width đã lấy từ hàng đầu và width = 0 thì MapGrid từ chối.
                if (string.IsNullOrEmpty(line))
                    throw new FormatException($"Hàng {row} rỗng.");

                if (line.Length != width)
                    throw new FormatException($"Hàng {row} dài {line.Length} ký tự, hàng đầu dài {width}.");

                // Hàng ĐẦU trong file là mép TRÊN map — để đọc file như nhìn bản vẽ. Nên khi nạp vào
                // lưới (gốc ở dưới) phải lật trục Y. Quyết định một lần, ghi ngay tại đây, vì viết sai
                // chỉ một trong hai chiều thì map lộn ngược mà không có lỗi nào.
                int cy = height - 1 - row;

                for (int cx = 0; cx < width; cx++)
                    cells[cy * width + cx] = ToCell(line[cx], row, cx);
            }

            return new MapGrid(definition.Id, definition.Name, origin.X, origin.Y,
                width, height, spawns, cells);
        }

        public static string Write(MapGrid map)
        {
            var rows = new List<string>(map.Height);
            var line = new StringBuilder(map.Width);

            for (int row = 0; row < map.Height; row++)
            {
                line.Clear();

                // Ghi từ mép TRÊN xuống — đối xứng với phép lật trong Parse.
                int cy = map.OriginY + map.Height - 1 - row;

                for (int cx = map.OriginX; cx < map.OriginX + map.Width; cx++)
                    line.Append(ToChar(map.At(cx, cy)));

                rows.Add(line.ToString());
            }

            var definition = new MapDefinition
            {
                Comment = "Sinh bởi Tools/MMORPG/Export Map — KHÔNG sửa tay. " +
                          "Sửa va chạm = vẽ lại lớp Tilemap \"Collision\" trong Unity rồi export lại.",
                Version = FORMAT_VERSION,
                Id = map.MapId,
                Name = map.Name,
                Origin = new CellPoint { X = map.OriginX, Y = map.OriginY },
                Spawns = new List<SpawnPoint>(map.Spawns),
                Cells = rows,
            };

            return JsonConvert.SerializeObject(definition, Settings);
        }

        private static CellType ToCell(char symbol, int row, int column)
        {
            switch (symbol)
            {
                case CHAR_EMPTY: return CellType.Empty;
                case CHAR_SOLID: return CellType.Solid;
                case CHAR_ONE_WAY: return CellType.OneWay;

                // Không có nhánh "coi như rỗng": một ký tự lạ nghĩa là file đã hỏng ở đâu đó, và đoán
                // bừa chỉ dời thời điểm phát hiện tới lúc có người đi xuyên tường.
                default:
                    throw new FormatException($"Ký tự lạ '{symbol}' ở hàng {row}, cột {column}.");
            }
        }

        private static char ToChar(CellType cell)
        {
            switch (cell)
            {
                case CellType.Solid: return CHAR_SOLID;
                case CellType.OneWay: return CHAR_ONE_WAY;
                default: return CHAR_EMPTY;
            }
        }
    }
}