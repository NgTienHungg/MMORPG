using MemoryPack;
using MMORPG.Shared.Net;
using MMORPG.Shared.World;

namespace MMORPG.Shared.Dto.Character
{
    /// <summary>
    /// Gói mở màn của một phiên chơi: chỉ mang những gì RIÊNG của phiên vừa mở.
    ///
    /// Nó không chở dữ liệu tĩnh nào — bảng nhân vật, bảng item, lưới map đều ship cùng bản build
    /// của client, và server đọc bản copy của mình. Ngoại lệ duy nhất là <see cref="World"/>: người
    /// vận hành chỉnh trọng lực giữa hai lần restart server mà không patch client, nên client không
    /// thể có sẵn mấy con số ấy.
    /// </summary>
    [MemoryPackable]
    public partial class EnterWorldResponse
    {
        /// <summary>Sai thì mọi trường dưới đây vô nghĩa, chỉ đọc <see cref="Error"/>.</summary>
        public bool Success { get; set; }

        /// <summary>Lý do từ chối. <c>None</c> khi thành công.</summary>
        public ErrorCode Error { get; set; }

        /// <summary>Id runtime trong world. Chỉ có nghĩa tới khi rời world.</summary>
        public int EntityId { get; set; }

        /// <summary>Id trong DB — sống qua mọi lần vào ra world.</summary>
        public long CharacterId { get; set; }

        /// <summary>Tên nhân vật.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Lớp nhân vật, khoá tra vào bảng nhân vật.</summary>
        public int ClassId { get; set; }

        /// <summary>Cấp hiện tại.</summary>
        public int Level { get; set; }

        /// <summary>Map đang đứng, khoá tra vào sổ map.</summary>
        public int MapId { get; set; }

        /// <summary>Toạ độ world lúc vào, trục ngang.</summary>
        public float X { get; set; }

        /// <summary>Toạ độ world lúc vào, trục dọc.</summary>
        public float Y { get; set; }

        /// <summary>Mốc thời gian server (Unix ms) tại thời điểm vào world.</summary>
        public long ServerTimeMs { get; set; }

        /// <summary>Luật thế giới đang chạy trên server. Null khi <see cref="Success"/> sai.</summary>
        public WorldConfig World { get; set; }
    }
}
