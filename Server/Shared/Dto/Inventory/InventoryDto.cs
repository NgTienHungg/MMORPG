using System;
using MemoryPack;

namespace MMORPG.Shared.Dto.Inventory
{
    /// <summary>
    /// Một ô túi trên dây. Struct vì nó nhỏ và đi thành mảng — và vì <c>default</c> của nó
    /// (<c>TemplateId = 0</c>) đã đúng nghĩa "ô trống", nên không cần một giá trị canh riêng.
    ///
    /// KHÔNG mang ItemId: client không có việc gì với id của một vật cụ thể — nó thao tác bằng
    /// SỐ Ô. Gửi thêm một trường mà người nhận không dùng là mời họ dùng nó sai.
    /// </summary>
    [MemoryPackable]
    public partial struct InventorySlotDto
    {
        public int Slot;

        /// <summary>0 = ô này TRỐNG. Không có gói "xoá ô" riêng — xem Bước 3.</summary>
        public int TemplateId;

        public int Quantity;
    }

    /// <summary>
    /// Toàn bộ túi. Gửi ĐÚNG MỘT LẦN, ngay sau EnterWorld, khi client chưa có gì để mà "đổi từ".
    /// Mọi hệ đồng bộ đều có hình dạng này: một trạng thái đầy đủ ban đầu, rồi một dòng thay đổi.
    /// </summary>
    [MemoryPackable]
    public partial class InventorySnapshotNotice
    {
        /// <summary>Chỉ các ô CÓ ĐỒ. Ô trống không cần một dòng để nói rằng nó trống.</summary>
        public InventorySlotDto[] Slots { get; set; } = Array.Empty<InventorySlotDto>();
    }

    /// <summary>Chỉ những ô VỪA ĐỔI. Đây là thứ UI cần — xem "snapshot ≠ delta" ở Bước 3.</summary>
    [MemoryPackable]
    public partial class InventoryDeltaNotice
    {
        public InventorySlotDto[] Slots { get; set; } = Array.Empty<InventorySlotDto>();
    }

    [MemoryPackable]
    public partial class ItemUseRequest
    {
        public int Slot { get; set; }
    }

    [MemoryPackable]
    public partial class ItemDropRequest
    {
        public int Slot { get; set; }

        public int Quantity { get; set; }
    }

    [MemoryPackable]
    public partial class ItemMoveRequest
    {
        public int FromSlot { get; set; }

        public int ToSlot { get; set; }
    }
}
