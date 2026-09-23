using System;
using System.Collections.Generic;

namespace MMORPG.Shared.World.Character
{
    /// <summary>
    /// Bảng tra config theo lớp nhân vật: tĩnh nhưng NẠP ĐƯỢC, và mỗi bên nạp từ file của chính mình
    /// — server đọc <c>Data/Config/characters.json</c>, client đọc
    /// <c>Resources/Config/characters.json</c>, hai file sinh từ một nguồn trong repo.
    ///
    /// Chỉ được thay bảng GIỮA HAI PHIÊN chơi. Entity đang sống giữ reference tới CharacterConfig cũ
    /// (<c>PlayerEntity._config</c>), nên thay bảng giữa chừng không kéo người đang online sang bộ
    /// mới — và đó là hành vi đúng, không phải thiếu sót.
    /// </summary>
    public static class CharacterConfigContainer
    {
        /// <summary>Lớp mặc định, chỗ lùi về khi một ClassId lạ lọt vào từ DB.</summary>
        public const int DRAGON_WARRIOR = 1;

        /// <summary>Bảng đang chạy. Chỉ <see cref="Load"/> được gán vào nó.</summary>
        private static Dictionary<int, CharacterConfig> _byClassId = new();

        /// <summary>Số lớp trong bảng đang chạy.</summary>
        public static int Count => _byClassId.Count;

        /// <summary>Config của một lớp. ClassId lạ thì lùi về <see cref="DRAGON_WARRIOR"/>.</summary>
        public static CharacterConfig Get(int classId)
        {
            if (_byClassId.TryGetValue(classId, out var config))
                return config;

            // Bảng rỗng (chưa nạp) thì đây là chỗ duy nhất phát hiện ra, và nó phải ném chứ không trả
            // null: null đi tiếp vài tầng rồi mới nổ ở MovementRules.Step, xa chỗ gây ra nó.
            if (_byClassId.Count == 0)
                throw new InvalidOperationException("CharacterConfigContainer chưa được Load. Cả hai bên nạp từ file của mình lúc khởi động.");

            return _byClassId[DRAGON_WARRIOR];
        }

        /// <summary>
        /// Thay cả bảng bằng dữ liệu vừa đọc từ file.
        ///
        /// Dựng NGUYÊN bảng mới rồi mới gán: nửa chừng mà ném thì bảng cũ còn nguyên, và luồng khác
        /// đọc song song hoặc thấy trọn bảng cũ hoặc trọn bảng mới. Đây cũng là chỗ duy nhất gọi
        /// <c>Prepare()</c>, nên không có đường nào để một config lọt vào bảng mà chưa quy ra tick.
        /// </summary>
        public static void Load(CharacterTableData table)
        {
            if (table == null)
                throw new ArgumentNullException(nameof(table));

            var built = new Dictionary<int, CharacterConfig>();

            foreach (CharacterConfig config in table.Classes)
            {
                // Trùng id thì không có giá trị mặc định nào để lùi về: bảng đã tự mâu thuẫn, và cái
                // nào thắng là chuyện của thứ tự dòng trong file.
                if (built.ContainsKey(config.ClassId))
                    throw new InvalidOperationException($"Hai lớp cùng ClassId {config.ClassId}: \"{built[config.ClassId].Name}\" và \"{config.Name}\".");

                config.Prepare();
                built[config.ClassId] = config;
            }

            _byClassId = built;
        }
    }
}
