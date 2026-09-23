using MemoryPack;

namespace MMORPG.Shared.World.Item
{
    /// <summary>
    /// Định nghĩa một LOẠI đồ. Bất biến trong suốt phiên chạy: nó là dữ liệu game design, không phải
    /// trạng thái người chơi.
    /// </summary>
    [MemoryPackable]
    public sealed partial class ItemConfig
    {
        /// <summary>Khoá của bảng. Một món đồ trong túi trỏ về đây bằng số này.</summary>
        public int TemplateId { get; set; }

        /// <summary>Tên hiển thị cho người chơi.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Mô tả hiển thị cho người chơi.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Khoá tài nguyên của icon — THUẦN client, server không đọc dòng này bao giờ. Vẫn để chung
        /// bảng vì tách ra là có hai bảng phải khớp nhau theo <see cref="TemplateId"/>.
        /// </summary>
        public string IconKey { get; set; } = string.Empty;

        /// <summary>Quyết định món đồ dùng được hay chỉ để bán, và nằm ở nhóm nào trong túi.</summary>
        public ItemKind Kind { get; set; }

        /// <summary>Tối đa bao nhiêu cái trong một ô. 1 nghĩa là không xếp chồng.</summary>
        public int MaxStack { get; set; } = 1;
    }
}
