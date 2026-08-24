using System;
using System.Collections.Generic;

namespace MMORPG.Shared.World
{
    /// <summary>Loại ô. Ba loại, và loại thứ ba là thứ làm nên thể loại platformer.</summary>
    public enum CellType : byte
    {
        Empty = 0,
        Solid = 1,

        /// <summary>
        /// Bệ xuyên-một-chiều: đứng được ở trên, nhảy từ dưới lên thì lọt qua.
        /// Ô này chặn hay không KHÔNG chỉ phụ thuộc vị trí mà còn phụ thuộc vận tốc và vị trí ở
        /// tick trước — đó là lý do vận tốc phải là một phần của trạng thái chứ không suy ra được.
        /// </summary>
        OneWay = 2,
    }

    /// <summary>
    /// Hình dạng một map dạng lưới ô 1×1, kèm vài con số riêng của nó (id, tên, các điểm spawn).
    /// Nguồn DUY NHẤT về việc đi được chỗ nào: server va chạm thật và client va chạm dự đoán đều đọc
    /// từ đây, và cả hai dựng nó từ cùng một file.
    ///
    /// Cố tình KHÔNG mô tả hình thức (cỏ, cây, nền trời) — đó là việc của các lớp tilemap khác. Một
    /// lưới ba trạng thái không đủ để vẽ đẹp, và một hình đẹp thì thừa thãi với va chạm.
    ///
    /// Toạ độ ô ở đây LÀ toạ độ ô của Tilemap trong Unity: ô (cx, cy) chiếm vùng world
    /// [cx, cx+1] × [cy, cy+1]. Vùng đã vẽ bắt đầu từ (OriginX, OriginY) — có thể âm — và phép dịch
    /// về chỉ số mảng nằm gọn trong hàm At, không ai bên ngoài phải biết tới nó.
    /// </summary>
    public sealed class MapGrid
    {
        public const float CELL_SIZE = 1f;

        /// <summary>Id của điểm spawn mặc định — chỗ người chơi xuất hiện khi không có nguồn nào khác chỉ định.</summary>
        public const string DEFAULT_SPAWN_ID = "default";

        public int MapId { get; }
        public string Name { get; }

        /// <summary>Ô góc dưới-trái của vùng đã vẽ. Âm là chuyện bình thường.</summary>
        public int OriginX { get; }

        public int OriginY { get; }

        public int Width { get; }
        public int Height { get; }

        public IReadOnlyList<SpawnPoint> Spawns => _spawns;

        /// <summary>Điểm spawn mặc định, chốt một lần lúc dựng để chỗ gọi không phải tìm lại mỗi lần.</summary>
        public SpawnPoint DefaultSpawn { get; }

        private readonly SpawnPoint[] _spawns;

        // Mảng một chiều chứ không phải [,]: cùng số ô, ít một tầng gián tiếp, và tiện cho vòng băm ở
        // Checksum. Chỉ số = (cy - OriginY) * Width + (cx - OriginX).
        private readonly CellType[] _cells;

        public MapGrid(int mapId, string name, int originX, int originY, int width, int height,
            IReadOnlyList<SpawnPoint> spawns, CellType[] cells)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentException($"Kích thước map không hợp lệ: {width}×{height}.");

            if (cells.Length != width * height)
                throw new ArgumentException($"Lưới {width}×{height} cần {width * height} ô, nhận {cells.Length}.");

            if (spawns.Count == 0)
                throw new ArgumentException("Map phải có ít nhất một điểm spawn.");

            MapId = mapId;
            Name = name;
            OriginX = originX;
            OriginY = originY;
            Width = width;
            Height = height;
            _cells = cells;

            // Copy ra mảng riêng: người gọi giữ tiếp danh sách của họ và sửa nó cũng không đụng được
            // vào map đang chạy. Bất biến không phải là chuyện phong cách — map bị sửa giữa chừng thì
            // client và server lệch nhau mà không ai biết từ lúc nào.
            _spawns = new SpawnPoint[spawns.Count];
            for (int i = 0; i < spawns.Count; i++)
                _spawns[i] = spawns[i];

            DefaultSpawn = FindDefaultSpawn(_spawns);
        }

        /// <summary>Mép trái/phải của map theo world. Thay cho hằng WORLD_HALF_EXTENT của Phase 6.</summary>
        public float MinX => OriginX * CELL_SIZE;

        public float MaxX => (OriginX + Width) * CELL_SIZE;

        /// <summary>
        /// Ô tại toạ độ ô. Ba phía ngoài lưới trả về ba thứ khác nhau, mỗi thứ một lý do — xem bảng
        /// trong tài liệu phase. Đáy lưới là Solid: đó là CẦU CHÌ chứ không phải thiết kế game. Vẽ
        /// thiếu một ô sàn thì hệ quả tệ nhất là rơi xuống đáy map rồi đứng đó, chứ không phải Y trôi
        /// ra vô cực và mọi phép tính sau đó thành vô nghĩa.
        /// </summary>
        public CellType At(int cx, int cy)
        {
            if (cy < OriginY)
                return CellType.Solid;

            if (cx < OriginX || cx >= OriginX + Width || cy >= OriginY + Height)
                return CellType.Empty;

            return _cells[(cy - OriginY) * Width + (cx - OriginX)];
        }

        public CellType AtWorld(float x, float y)
        {
            return At(CellX(x), CellY(y));
        }

        // Floor chứ không phải ép kiểu (int): cast cắt VỀ PHÍA 0 nên -0.5 thành 0, trong khi
        // Floor(-0.5) = -1. Map này có toạ độ âm ở nửa trái nên đây không phải chuyện lý thuyết.
        public static int CellX(float worldX)
        {
            return (int)MathF.Floor(worldX / CELL_SIZE);
        }

        public static int CellY(float worldY)
        {
            return (int)MathF.Floor(worldY / CELL_SIZE);
        }

        public static float ColumnLeft(int cx)
        {
            return cx * CELL_SIZE;
        }

        public static float ColumnRight(int cx)
        {
            return (cx + 1) * CELL_SIZE;
        }

        public static float RowBottom(int cy)
        {
            return cy * CELL_SIZE;
        }

        public static float RowTop(int cy)
        {
            return (cy + 1) * CELL_SIZE;
        }

        /// <summary>
        /// Dấu vân tay của LƯỚI (FNV-1a). Hai bên in ra cùng một số nghĩa là đang chạy đúng một map —
        /// bằng chứng rẻ nhất có thể có, và là hạt giống cho phép kiểm version ở Phase 12.
        ///
        /// Cố ý không băm danh sách spawn: câu hỏi con số này trả lời là "hai bên có cùng hình dạng va
        /// chạm không", còn điểm spawn thì chỉ server dùng.
        /// </summary>
        public uint Checksum()
        {
            uint hash = 2166136261u;

            hash = Mix(hash, MapId);
            hash = Mix(hash, OriginX);
            hash = Mix(hash, OriginY);
            hash = Mix(hash, Width);
            hash = Mix(hash, Height);

            for (int i = 0; i < _cells.Length; i++)
                hash = Mix(hash, (int)_cells[i]);

            return hash;
        }

        /// <summary>
        /// Điểm mang id "default"; không có thì lấy điểm đầu tiên.
        ///
        /// Lùi về điểm đầu chứ không ném: map thiếu điểm mặc định vẫn là map chơi được, và chết lúc
        /// khởi động vì một cái tên là cái giá quá đắt. Còn map KHÔNG có điểm spawn nào thì đã bị hàm
        /// dựng chặn từ trước — đó mới là thứ không chơi được.
        /// </summary>
        private static SpawnPoint FindDefaultSpawn(SpawnPoint[] spawns)
        {
            for (int i = 0; i < spawns.Length; i++)
            {
                if (spawns[i].Id == DEFAULT_SPAWN_ID)
                    return spawns[i];
            }

            return spawns[0];
        }

        // FNV-1a nuốt từng byte một. Cộng thẳng cả int vào thì hai lưới hoán vị vài ô vẫn có thể ra
        // cùng một số; xor theo byte rồi nhân số nguyên tố ở mỗi bước thì không.
        private static uint Mix(uint hash, int value)
        {
            for (int shift = 0; shift < 32; shift += 8)
            {
                hash ^= (uint)((value >> shift) & 0xFF);
                hash *= 16777619u;
            }

            return hash;
        }
    }
}