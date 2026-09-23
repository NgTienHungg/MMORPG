namespace MMORPG.Shared.World
{
    /// <summary>
    /// Tên những file config mà cả hai bên cùng đọc — client từ <c>Assets/Game/Resources/Config/</c>,
    /// server từ bản copy trong <c>Data/Config/</c>. Chung một hằng để hai bên không lệch chuỗi sau
    /// lần đổi tên file đầu tiên.
    ///
    /// File chỉ một bên đọc thì hằng tên của nó nằm private ở bên đó — <c>game.json</c> là ví dụ.
    /// Không có đuôi <c>.json</c>: server tự thêm, còn <c>Resources.Load</c> luôn bỏ phần đuôi.
    /// </summary>
    public static class ConfigFiles
    {
        public const string CHARACTERS = "characters";

        public const string ITEMS = "items";
    }
}
