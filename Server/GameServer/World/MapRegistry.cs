using MMORPG.ServerCore;
using MMORPG.Shared.Dto.Db;
using MMORPG.Shared.World;

namespace MMORPG.GameServer.World
{
    /// <summary>
    /// Mọi map server biết, tra theo id. Nạp HẾT một lần lúc khởi động chứ không nạp lười: file map
    /// là dữ liệu tĩnh, và nạp lười thì một file hỏng chỉ lộ ra vào lần đầu có người bước sang map
    /// đó — giữa lúc đang chơi, thay vì lúc bạn đang nhìn console.
    /// </summary>
    public sealed class MapRegistry
    {
        /// <summary>
        /// Map của nhân vật mới toanh. Là LUẬT CHƠI ("người mới bắt đầu ở đâu"), không phải thuộc
        /// tính của map nào — nên nó nằm ở đây chứ không nằm trong file map.
        /// </summary>
        private readonly int _startingMapId;

        private readonly Dictionary<int, MapGrid> _byId = new();

        /// <summary>Map khởi đầu, chốt một lần lúc dựng để chỗ gọi không phải tra lại.</summary>
        public MapGrid Starting { get; }

        public int Count => _byId.Count;

        public MapRegistry(ConfigService config)
        {
            _startingMapId = config.Current.Server.StartingMapId;

            // AppContext.BaseDirectory chứ không phải thư mục hiện hành: chỗ gõ lệnh không phải chỗ
            // file exe nằm, và `dotnet run` từ thư mục khác là hỏng.
            string folder = Path.Combine(AppContext.BaseDirectory, "Data", "Maps");

            if (!Directory.Exists(folder))
                throw new DirectoryNotFoundException($"Không thấy {folder}. Chạy Tools/MMORPG/Export Map rồi build lại GameServer.");

            foreach (string path in Directory.EnumerateFiles(folder, "*.json"))
            {
                MapGrid map;

                try
                {
                    map = MapGridParser.Parse(File.ReadAllText(path));
                }
                catch (FormatException ex)
                {
                    // Thông điệp gốc nói "hàng 3 lệch ô" mà không nói của file nào — vô dụng khi có hai chục map.
                    throw new InvalidOperationException($"File map hỏng: {path} — {ex.Message}", ex);
                }

                // Hai file cùng nhận một id thì file nào thắng là chuyện của thứ tự liệt kê thư mục —
                // tức là không xác định. Chết ngay còn hơn chạy với một nửa map ngẫu nhiên.
                if (_byId.TryGetValue(map.MapId, out MapGrid existing))
                    throw new InvalidOperationException($"Hai map cùng id {map.MapId}: \"{existing.Name}\" và \"{map.Name}\" ({path}).");

                _byId[map.MapId] = map;

                Log.Info($"Map {map.Name.Cyan()} #{map.MapId} — {map.Width}×{map.Height} ô, " +
                         $"origin ({map.OriginX}, {map.OriginY}), checksum {map.Checksum():X8}");
            }

            if (!_byId.TryGetValue(_startingMapId, out MapGrid starting))
                throw new InvalidOperationException($"Không có map khởi đầu #{_startingMapId} trong {_byId.Count} map đã nạp.");

            Starting = starting;
            Log.Info($"Đã nạp {_byId.Count.ToString().Green()} map, khởi đầu ở #{_startingMapId}");
        }

        public bool TryGet(int mapId, out MapGrid map)
        {
            return _byId.TryGetValue(mapId, out map);
        }

        /// <summary>
        /// Map cho một nhân vật đang vào world.
        ///
        /// Map cũ không còn (bị xoá, bị đổi id) thì đưa về map khởi đầu — VÀ SỬA LUÔN row. Bỏ bước
        /// sửa row thì entity mang id map A trong khi đứng bằng toạ độ của map B, và LeaveWorld lưu
        /// nguyên cặp lệch đó xuống DB: lần sau người chơi vào world ở trong lòng đất.
        /// </summary>
        public MapGrid ResolveFor(CharacterRow row)
        {
            if (TryGet(row.MapId, out MapGrid map))
                return map;

            Log.Warn($"{row.Name.Cyan()} đang ở map {row.MapId} — không có trong {Count} map đã nạp. " +
                     $"Đưa về map khởi đầu #{_startingMapId}.");

            map = Starting;
            row.MapId = map.MapId;
            row.X = map.DefaultSpawn.X;
            row.Y = map.DefaultSpawn.Y;

            return map;
        }
    }
}
