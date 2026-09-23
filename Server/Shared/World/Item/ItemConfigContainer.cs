using System;
using System.Collections.Generic;

namespace MMORPG.Shared.World.Item
{
    /// <summary>
    /// Bảng tra template item theo id. Cùng khuôn với <see cref="Character.CharacterConfigContainer"/>:
    /// bảng tĩnh NẠP ĐƯỢC, và cả hai bên đều nạp từ file của chính mình.
    /// </summary>
    public static class ItemConfigContainer
    {
        /// <summary>Bảng đang chạy. Chỉ <see cref="Load"/> được gán vào nó.</summary>
        private static Dictionary<int, ItemConfig> _byId = new();

        /// <summary>Số loại đồ trong bảng đang chạy.</summary>
        public static int Count => _byId.Count;

        /// <summary>
        /// Template của một id, hoặc null. Trả null chứ không ném vì nguồn của id khác
        /// <c>CharacterConfigContainer.Get</c>: templateId đến từ DB và có thể là đồ của một bảng cũ
        /// hoặc item đã bị gỡ. Cách xử lý đúng cho một dòng như vậy là bỏ qua món đồ đó, không phải
        /// chặn người chơi vào game.
        /// </summary>
        public static ItemConfig Find(int templateId)
        {
            return _byId.TryGetValue(templateId, out ItemConfig config) ? config : null;
        }

        /// <summary>Thay cả bảng. Dựng nguyên bảng mới rồi mới gán — cùng lý do như bảng nhân vật.</summary>
        public static void Load(ItemTableData table)
        {
            if (table == null)
                throw new ArgumentNullException(nameof(table));

            var built = new Dictionary<int, ItemConfig>();

            foreach (ItemConfig config in table.Items)
            {
                // Trùng id thì không có giá trị mặc định nào để lùi về: bảng đã tự mâu thuẫn, và cái
                // nào thắng là chuyện của thứ tự dòng trong file.
                if (built.ContainsKey(config.TemplateId))
                    throw new InvalidOperationException($"Hai item cùng TemplateId {config.TemplateId}: \"{built[config.TemplateId].Name}\" và \"{config.Name}\".");

                built[config.TemplateId] = config;
            }

            _byId = built;
        }
    }
}
