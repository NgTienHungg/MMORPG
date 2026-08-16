using System;
using MemoryPack;

namespace MMORPG.Shared.Dto.World
{
    /// <summary>
    /// Phần BẤT BIẾN của một entity — gửi đúng một lần lúc nó xuất hiện.
    /// Thứ đổi theo tick (vị trí) đi trong snapshot, không lặp lại ở đây mỗi tick.
    /// </summary>
    [MemoryPackable]
    public partial class EntitySpawnNotice
    {
        public int EntityId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int ClassId { get; set; }

        /// <summary>Vị trí lúc xuất hiện — mồi đầu tiên cho buffer nội suy phía client.</summary>
        public float X { get; set; }
        public float Y { get; set; }
    }

    [MemoryPackable]
    public partial class EntityDespawnNotice
    {
        public int EntityId { get; set; }
    }

    /// <summary>Trạng thái một entity tại một tick. Cố tình chỉ có thứ thay đổi theo tick.</summary>
    [MemoryPackable]
    public partial class EntityState
    {
        public int EntityId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
    }

    [MemoryPackable]
    public partial class WorldSnapshotNotice
    {
        public EntityState[] States { get; set; } = Array.Empty<EntityState>();
    }
}