namespace MMORPG.Shared.World.Item
{
    /// <summary>
    /// Loại đồ quyết định nó LÀM ĐƯỢC GÌ, nên nó là một nhánh xử lý chứ không phải một con số —
    /// khác hẳn MaxStack, thứ chỉ khác nhau về giá trị giữa các dòng.
    /// </summary>
    public enum ItemKind : byte
    {
        /// <summary>Đồ linh tinh: chỉ nằm trong túi, vứt được, không dùng được.</summary>
        Misc = 0,

        /// <summary>Dùng một lần, giảm số lượng.</summary>
        Consumable = 1,

        /// <summary>Mặc được. Phase 13 chưa mặc được gì — đó là việc của Phase 14.</summary>
        Equipment = 2,
    }
}
