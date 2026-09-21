using System;
using MemoryPack;
using MMORPG.Shared.World;
using MMORPG.Shared.World.Character;
using MMORPG.Shared.World.Movement;

namespace MMORPG.Shared.Dto.World
{
    /// <summary>
    /// Phần BẤT BIẾN của một entity — gửi đúng một lần lúc nó xuất hiện.
    /// Thứ đổi theo tick đi trong snapshot, không lặp lại ở đây mỗi tick.
    /// </summary>
    [MemoryPackable]
    public partial class EntitySpawnNotice
    {
        public int EntityId { get; set; }
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Lớp nhân vật. Người nhận tra bảng bằng số này để biết đối phương chạy nhanh bao nhiêu và
        /// mỗi hành động của họ dài bao lâu — nhờ vậy các con số ấy không phải đi trên dây.
        /// </summary>
        public int ClassId { get; set; }

        /// <summary>Vị trí lúc xuất hiện — mồi đầu tiên cho buffer nội suy phía client.</summary>
        public float X { get; set; }
        public float Y { get; set; }

        /// <summary>Tư thế lúc xuất hiện — để nhân vật hiện ra đã đúng hướng, không quay đầu một nhịp sau.</summary>
        public bool FacingLeft { get; set; }
        public bool Crouching { get; set; }
        public ActionState Action { get; set; }
    }

    [MemoryPackable]
    public partial class EntityDespawnNotice
    {
        public int EntityId { get; set; }
    }

    /// <summary>
    /// Trạng thái một entity tại một tick. Cố tình chỉ có thứ NGƯỜI XEM cần để vẽ — không phải trọn
    /// MoveState như kênh của chính mình. Người xem không replay ai cả, nên vận tốc và các bộ đếm
    /// là byte thừa, 20 lần mỗi giây, nhân với số người quanh họ.
    /// </summary>
    [MemoryPackable]
    public partial class EntityState
    {
        public int EntityId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }

        /// <summary>Hai thứ KHÔNG suy được từ chuỗi vị trí: hướng mặt (có trí nhớ) và tư thế ngồi.</summary>
        public bool FacingLeft { get; set; }
        public bool Crouching { get; set; }

        /// <summary>Tầng action — do server quyết, người xem không có cách nào tự biết.</summary>
        public ActionState Action { get; set; }
    }

    [MemoryPackable]
    public partial class WorldSnapshotNotice
    {
        public EntityState[] States { get; set; } = Array.Empty<EntityState>();
    }

    [MemoryPackable]
    public partial class MapChangedNotice
    {
        public int MapId { get; set; }

        /// <summary>
        /// Trạng thái ĐẦY ĐỦ ở map mới, không chỉ toạ độ. Gửi mỗi (x, y) thì vận tốc và hai bộ đếm
        /// coyote/jump-buffer của map cũ còn nguyên, và tick đầu ở map mới diễn ra với một cú nhảy
        /// đang dở.
        /// </summary>
        public MoveState State { get; set; }
    }
}
