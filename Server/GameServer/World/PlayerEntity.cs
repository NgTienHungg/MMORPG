using MMORPG.Shared.Dto.Db;
using MMORPG.Shared.World;

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

        /// <summary>Input cuối đã nhận. Echo lại cho client trong MoveState để nó biết replay từ đâu.</summary>
        public int LastInputSeq { get; private set; }

        // Hướng đang bấm, do handler ghi / tick đọc. Hai luồng khác nhau nhưng không cần lock:
        // mỗi field là một phép ghi nguyên tử, tệ nhất tick này dùng input trễ một nhịp.
        private float _inputDirX;
        private float _inputDirY;

        // Số tick đã trôi từ input cuối. Client treo/rớt giữa lúc giữ phím mà không có bộ đếm này
        // thì entity chạy theo hướng cũ mãi mãi.
        private int _ticksSinceInput;


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

        public void SetInput(int seq, float dirX, float dirY)
        {
            LastInputSeq = seq;
            _inputDirX = dirX;
            _inputDirY = dirY;
            _ticksSinceInput = 0;
        }

        public void Integrate(float dt)
        {
            // Quá 1 giây không có input mới → coi như đã thả phím. Trạng thái cũ không được sống mãi.
            if (++_ticksSinceInput > MovementRules.TICK_RATE)
            {
                _inputDirX = 0;
                _inputDirY = 0;
            }

            (X, Y) = MovementRules.Step(X, Y, _inputDirX, _inputDirY, dt);
        }
    }
}
