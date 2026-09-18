using System;
using System.Collections.Generic;

namespace MMORPG.Shared.World
{
    /// <summary>
    /// Bảng tra profile theo lớp nhân vật. Bảng TĨNH nhưng NẠP ĐƯỢC: server nạp từ file lúc boot,
    /// client nạp từ gói EnterWorld. Một bảng, hai đường vào, một hàm dựng — nên hai bên không có cửa
    /// nào cầm hai bộ số khác nhau.
    ///
    /// Chỉ được thay bảng GIỮA HAI PHIÊN chơi. Entity đang sống giữ reference tới CharacterProfile cũ
    /// (PlayerEntity._profile), nên thay bảng giữa chừng không kéo được người đang online sang bộ mới —
    /// và đó là hành vi đúng, không phải thiếu sót.
    /// </summary>
    public static class CharacterConfigContainer
    {
        public const int DRAGON_WARRIOR = 1;

        private static Dictionary<int, CharacterConfig> _byClassId = new();

        /// <summary>Checksum của bảng đang nạp — để in log và để so hai đầu dây.</summary>
        public static uint Checksum { get; private set; }

        public static CharacterConfig Get(int classId)
        {
            if (_byClassId.TryGetValue(classId, out CharacterConfig profile))
                return profile;

            // Bảng rỗng (chưa nạp) thì đây là chỗ duy nhất phát hiện ra, và nó phải ném chứ không trả
            // null: null đi tiếp vài tầng rồi mới nổ ở MovementRules.Step, xa chỗ gây ra nó.
            if (_byClassId.Count == 0)
                throw new InvalidOperationException("CharacterProfiles chưa được Load. Server nạp lúc boot, client nạp khi vào world.");

            return _byClassId[DRAGON_WARRIOR];
        }

        /// <summary>
        /// Thay cả bảng bằng dữ liệu vừa đọc (server) hoặc vừa nhận (client). Dựng NGUYÊN bảng mới rồi
        /// mới gán — xem câu 4 phần tự kiểm tra.
        ///
        /// Đây cũng là chỗ DUY NHẤT gọi Prepare(), nên không có đường nào để một profile lọt vào bảng
        /// mà chưa quy ra tick.
        /// </summary>
        public static void Load(CharacterTableData table)
        {
            var built = new Dictionary<int, CharacterConfig>();

            foreach (CharacterConfig profile in table.Classes)
            {
                profile.Prepare();
                built[profile.ClassId] = profile;
            }

            _byClassId = built;
            Checksum = table.Checksum();
        }
    }
}
