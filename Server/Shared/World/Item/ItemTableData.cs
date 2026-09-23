using System;
using MemoryPack;
using Newtonsoft.Json;

namespace MMORPG.Shared.World.Item
{
    /// <summary>
    /// Cả bảng item, bản đối chiếu 1-1 với <c>items.json</c>. Không đi trên dây: mỗi bên đọc file của chính nó
    /// </summary>
    [MemoryPackable]
    public sealed partial class ItemTableData : IConfigFile
    {
        /// <summary>Phiên bản schema, nằm trong file.</summary>
        public int Version { get; set; } = 1;

        /// <summary>Mỗi phần tử là một loại đồ.</summary>
        public ItemConfig[] Items { get; set; } = Array.Empty<ItemConfig>();

        /// <summary>Đếm ra từ <see cref="Items"/>, nên không tuần tự hoá ở cả hai bộ.</summary>
        [MemoryPackIgnore] [JsonIgnore] public int RowCount => Items.Length;
    }
}
