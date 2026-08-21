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

        /// <summary>
        /// Trạng thái vật lý authoritative — nơi duy nhất trong toàn hệ thống mà vị trí một người
        /// chơi là SỰ THẬT. Setter private: chỉ <see cref="Integrate"/> đổi được nó. Đọc ra là một
        /// bản copy (struct), nên bên ngoài cũng không sửa lén vào bên trong được.
        /// </summary>
        public MoveState State { get; private set; }

        /// <summary>Vị trí đọc-ra-ngoài (log, snapshot, lưu DB). Không có setter: vị trí chỉ đổi qua Integrate.</summary>
        public float X => State.X;
        public float Y => State.Y;

        /// <summary>Session đang điều khiển entity này.</summary>
        public ClientSession Owner { get; private set; }

        /// <summary>Input cuối đã nhận. Echo lại cho client trong MoveState để nó biết replay từ đâu.</summary>
        public int LastInputSeq { get; private set; }

        // Trục giữ: handler ghi, tick đọc. Hai luồng khác nhau nhưng không cần lock —
        // mỗi field là một phép ghi nguyên tử, tệ nhất tick này dùng input trễ một nhịp.
        private float _intentDirX;

        // Nút dạng CẠNH: phải chốt lại bằng OR chứ không ghi đè. Client gửi 20 gói/s và server tick
        // 20 lần/s, nhưng hai nhịp không khớp: hai gói tới giữa hai tick là chuyện thường. Ghi đè thì
        // gói { Jump: false } ngay sau gói { Jump: true } sẽ nuốt mất cú nhảy — không lỗi, không log,
        // chỉ là "thỉnh thoảng bấm Space mà không nhảy".
        private bool _pendingJump;

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
            State = MoveState.AtRest(row.X, row.Y);
            Owner = owner;
        }

        /// <summary>Nhận ý định đã được handler làm sạch. Chạy ở luồng IO, không phải luồng tick.</summary>
        public void SetInput(int seq, MoveIntent intent)
        {
            LastInputSeq = seq;
            _intentDirX = intent.DirX;

            // |= chứ không =. Xem comment ở khai báo _pendingJump.
            _pendingJump |= intent.Jump;

            _ticksSinceInput = 0;
        }

        public void Integrate(float dt)
        {
            // Quá 1 giây không có input mới → coi như đã thả phím. Trạng thái cũ không được sống mãi.
            // Chỉ xoá hướng chạy: cú nhảy đã chốt vẫn phải được tiêu thụ, mất mạng không phải là lý do
            // để nuốt một input người chơi đã bấm thật.
            if (++_ticksSinceInput > MovementRules.TICK_RATE)
            {
                _intentDirX = 0f;

                // Kẹp lại luôn để bộ đếm không leo tới tràn int khi một client treo hàng năm trời.
                _ticksSinceInput = MovementRules.TICK_RATE + 1;
            }

            // Đọc-rồi-xoá: một lần bấm nhảy chỉ được dùng đúng một tick. Không xoá thì nhân vật
            // nhảy lại mỗi lần chạm đất, mãi mãi, cho tới gói input kế tiếp.
            var intent = new MoveIntent { DirX = _intentDirX, Jump = _pendingJump };
            _pendingJump = false;

            State = MovementRules.Step(State, intent, dt);
        }
    }
}
