using MMORPG.Shared.Dto.Db;

namespace MMORPG.GameServer.World
{
    /// <summary>
    /// Một nhân vật đang sống trong world: định danh runtime + bản sao RAM của dữ liệu character
    /// đang được chơi. Tồn tại từ EnterWorld tới lúc rời world, không lâu hơn.
    /// </summary>
    public sealed class PlayerEntity
    {
        public int EntityId { get; private set; }

        /// <summary>Khoá để lưu về DB. Không bao giờ gửi cho client khác.</summary>
        public long CharacterId { get; private set; }

        public long AccountId { get; private set; }
        public string Name { get; private set; }
        public int ClassId { get; private set; }
        public int Level { get; private set; }

        public int MapId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }

        /// <summary>Session đang điều khiển entity này.</summary>
        public ClientSession Owner { get; private set; }

        public PlayerEntity(int entityId, CharacterRow row, ClientSession owner)
        {
            EntityId = entityId;
            CharacterId = row.CharacterId;
            AccountId = row.AccountId;
            Name = row.Name;
            ClassId = row.ClassId;
            Level = row.Level;
            MapId = row.MapId;
            X = row.X;
            Y = row.Y;
            Owner = owner;
        }
    }
}
