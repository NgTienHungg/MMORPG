using System;
using MemoryPack;

namespace MMORPG.Shared.Dto.Db
{
    /// <summary>
    /// Một dòng nguyên vẹn của bảng <c>inventory_item</c>. Chỉ đi trên đường nội bộ GameServer ↔ DBServer.
    ///
    /// Hình dạng của CHỖ DỮ LIỆU NẰM, không phải của thứ chạy trong game — thứ chạy là
    /// <c>ItemStack</c> trong <c>Inventory</c>, và nó không có <c>CharacterId</c> vì cả cái túi đã
    /// thuộc về một nhân vật rồi. Cùng mẫu với CharacterRow ≠ PlayerEntity ở Phase 5.
    /// </summary>
    [MemoryPackable]
    public partial class InventoryRow
    {
        /// <summary>Id của MỘT VẬT cụ thể. DB cấp bằng AUTOINCREMENT — xem bảng itemId ≠ templateId.</summary>
        public long ItemId { get; set; }

        public int TemplateId { get; set; }

        public int Quantity { get; set; }

        public int Slot { get; set; }
    }

    [MemoryPackable]
    public partial class InventoryLoadRequest
    {
        public long CharacterId { get; set; }
    }

    [MemoryPackable]
    public partial class InventoryLoadResponse
    {
        public InventoryRow[] Items { get; set; } = Array.Empty<InventoryRow>();
    }

    /// <summary>
    /// Ghi TOÀN BỘ túi. Không có "lưu một ô": xem bảng so sánh ở Bước 2 — xoá sạch rồi ghi lại là
    /// lựa chọn có ý thức, và nó chỉ đúng khi cả cái túi đi cùng nhau trong một transaction.
    /// </summary>
    [MemoryPackable]
    public partial class InventorySaveRequest
    {
        public long CharacterId { get; set; }

        public InventoryRow[] Items { get; set; } = Array.Empty<InventoryRow>();
    }
}
