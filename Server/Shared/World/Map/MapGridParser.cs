using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace MMORPG.Shared.World.Map
{
    /// <summary>
    /// Đọc và ghi file map JSON. Hai chiều nằm cùng một chỗ có chủ đích: tool export gọi Write, hai
    /// đầu dây gọi Parse — nên định dạng chỉ có một bản mô tả duy nhất, và bài test round-trip ở
    /// Shared.Tests kiểm được nó bằng máy thay vì bằng mắt.
    /// </summary>
    public static class MapGridParser
    {
        /// <summary>
        /// Version của ĐỊNH DẠNG, không phải của map. Luật: thêm một trường tuỳ chọn thì GIỮ NGUYÊN số
        /// này (JSON tự lo — trường thiếu về mặc định, trường lạ bị bỏ qua); chỉ tăng khi đổi ý nghĩa,
        /// đổi tên hoặc xoá một trường đã có. Đọc phải version lạ thì ném ngay chứ không cố đoán: một
        /// file map đọc sai một nửa còn tệ hơn một file map không đọc được.
        ///
        /// Version 2 đổi tên MỌI trường sang đúng tên property (bỏ [JsonProperty]) và bỏ trường
        /// "_comment". File version 1 mà đọc bằng code này thì "prefab" không khớp "PrefabKey" nữa và
        /// map mất hình trong im lặng — đúng loại hỏng mà con số này sinh ra để chặn.
        /// </summary>
        public const int FORMAT_VERSION = 2;

        // Write đệm dấu cách để canh cột; Parse thì tách theo khoảng trắng và bỏ ô rỗng, nên số dấu
        // cách giữa hai id không mang thông tin gì. Tab lọt vào (do ai đó sửa tay) cũng vẫn đọc được.
        private static readonly char[] SEPARATORS = { ' ', '\t' };

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,

            // Trường lạ thì BỎ QUA. Có chủ đích, và ngược với file config gõ tay ở Phase 12 (nơi sẽ
            // dùng Error): file này do TOOL sinh nên không có lỗi chính tả để bắt, còn cái ta cần là
            // code hôm nay đọc được file mà phiên bản mai này thêm trường vào.
            MissingMemberHandling = MissingMemberHandling.Ignore,

            // Trường null thì KHÔNG ghi ra. Nhờ dòng này mà map chưa nối đi đâu cho ra file không có
            // trường "Portals" thay vì một dòng `"Portals": null` — người mở file ra đọc không phải
            // đoán xem null ở đây nghĩa là "chưa có cổng" hay "tool ghi hỏng".
            NullValueHandling = NullValueHandling.Ignore,
        };

        public static MapGrid Parse(string json)
        {
            MapConfig? definition = JsonConvert.DeserializeObject<MapConfig>(json, Settings);

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

            // Tách hàng đầu ngay để lấy width, rồi dùng lại chính kết quả đó trong vòng lặp — tách hai
            // lần thì không sai, chỉ là thừa.
            string[] firstRow = SplitRow(rows[0], 0);
            int width = firstRow.Length;

            var cells = new CellType[width * height];

            for (int row = 0; row < height; row++)
            {
                string[] tokens = row == 0 ? firstRow : SplitRow(rows[row], row);

                if (tokens.Length != width)
                    throw new FormatException($"Hàng {row} có {tokens.Length} ô, hàng đầu có {width}.");

                // Hàng ĐẦU trong file là mép TRÊN map — để đọc file như nhìn bản vẽ. Nên khi nạp vào
                // lưới (gốc ở dưới) phải lật trục Y. Quyết định một lần, ghi ngay tại đây, vì viết sai
                // chỉ một trong hai chiều thì map lộn ngược mà không có lỗi nào.
                int cy = height - 1 - row;

                for (int cx = 0; cx < width; cx++)
                    cells[cy * width + cx] = ToCell(tokens[cx], row, cx);
            }

            return new MapGrid(definition.Id, definition.Name, definition.PrefabKey,
                origin.X, origin.Y, width, height, spawns, definition.Portals, cells);
        }

        public static string Write(MapGrid map)
        {
            var rows = new List<string>(map.Height);

            // Bề rộng cột = số chữ số của id LỚN NHẤT đang có mặt trong map. Canh phải theo nó thì mọi
            // cột thẳng hàng và mắt vẫn nhìn ra hình. Parse bỏ qua hoàn toàn chuyện này — đây thuần
            // tuý là việc làm đẹp cho người đọc.
            int columnWidth = MaxIdWidth(map);
            var line = new StringBuilder(map.Width * (columnWidth + 1));

            for (int row = 0; row < map.Height; row++)
            {
                line.Clear();

                // Ghi từ mép TRÊN xuống — đối xứng với phép lật trong Parse.
                int cy = map.OriginY + map.Height - 1 - row;

                for (int cx = map.OriginX; cx < map.OriginX + map.Width; cx++)
                {
                    if (cx > map.OriginX)
                        line.Append(' ');

                    line.Append(((int)map.At(cx, cy)).ToString().PadLeft(columnWidth));
                }

                rows.Add(line.ToString());
            }

            var definition = new MapConfig
            {
                Version = FORMAT_VERSION,
                Id = map.MapId,
                Name = map.Name,
                PrefabKey = map.PrefabKey,
                Origin = new CellPoint { X = map.OriginX, Y = map.OriginY },
                Spawns = new List<SpawnPoint>(map.Spawns),

                // Map không có cổng thì file KHÔNG có trường Portals, chứ không phải có mà rỗng:
                // NullValueHandling.Ignore ở Settings lo phần đó. Một mảng rỗng nằm trong file là một
                // câu hỏi thừa cho người mở file ra đọc.
                Portals = map.Portals.Count > 0 ? new List<Portal>(map.Portals) : null,

                Cells = rows,
            };

            return JsonConvert.SerializeObject(definition, Settings);
        }

        /// <summary>
        /// Tách một hàng thành các id. Chuỗi rỗng bắt luôn cả trường hợp JSON ghi null trong mảng —
        /// không có hàng hợp lệ nào rỗng, vì width lấy từ hàng đầu và width = 0 thì MapGrid từ chối.
        /// </summary>
        private static string[] SplitRow(string line, int row)
        {
            if (string.IsNullOrWhiteSpace(line))
                throw new FormatException($"Hàng {row} rỗng.");

            return line.Split(SEPARATORS, StringSplitOptions.RemoveEmptyEntries);
        }

        private static CellType ToCell(string token, int row, int column)
        {
            if (!int.TryParse(token, out int id))
                throw new FormatException($"Ô ở hàng {row}, cột {column} không phải số: \"{token}\".");

            // Hỏi thẳng enum thay vì viết một switch liệt kê lại ba loại ô: id trong file CHÍNH LÀ giá
            // trị enum, nên enum là chỗ duy nhất giữ danh sách — thêm loại ô mới không phải nhớ sửa
            // thêm chỗ nào ở đây. Enum.IsDefined dùng reflection và có boxing, nhưng nó chạy đúng một
            // lần cho mỗi ô lúc nạp map, không nằm trong vòng chạy 20 lần mỗi giây.
            //
            // Phép kẹp byte đứng trước là bắt buộc: CellType là enum byte, ép một số ngoài 0..255 sang
            // byte thì C# cắt cụt trong im lặng (256 thành 0 = Empty) — một lỗ trên sàn không ai thấy.
            if (id < byte.MinValue || id > byte.MaxValue || !Enum.IsDefined(typeof(CellType), (byte)id))
                throw new FormatException($"Id ô lạ {id} ở hàng {row}, cột {column}. " +
                                          "Nhiều khả năng file map do một bản tool mới hơn code này sinh ra.");

            // Không có nhánh "coi như rỗng": một id lạ nghĩa là file hỏng hoặc code cũ, và đoán bừa chỉ
            // dời thời điểm phát hiện tới lúc có người đi xuyên tường.
            return (CellType)id;
        }

        /// <summary>Số chữ số của id lớn nhất trong lưới — bề rộng cột để canh phải lúc ghi.</summary>
        private static int MaxIdWidth(MapGrid map)
        {
            int max = 0;

            for (int cy = map.OriginY; cy < map.OriginY + map.Height; cy++)
            {
                for (int cx = map.OriginX; cx < map.OriginX + map.Width; cx++)
                {
                    int id = (int)map.At(cx, cy);

                    if (id > max)
                        max = id;
                }
            }

            return max.ToString().Length;
        }
    }
}
