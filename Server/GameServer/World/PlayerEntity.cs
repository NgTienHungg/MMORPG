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

        // Trục giữ: handler ghi, tick đọc. Ghi đè là đúng — chỉ giá trị mới nhất có nghĩa.
        private float _intentDirX;
        private bool _intentCrouch;

        // Nút dạng CẠNH: phải chốt lại bằng OR chứ không ghi đè. Client gửi 20 gói/s và server tick
        // 20 lần/s, nhưng hai nhịp không khớp: hai gói tới giữa hai tick là chuyện thường. Ghi đè thì
        // gói { Jump: false } ngay sau gói { Jump: true } sẽ nuốt mất cú nhảy — không lỗi, không log,
        // chỉ là "thỉnh thoảng bấm Space mà không nhảy".
        private bool _pendingJump;
        private ActionRequest _pendingAction;

        // Số tick đã trôi từ input cuối. Client treo/rớt giữa lúc giữ phím mà không có bộ đếm này
        // thì entity chạy theo hướng cũ mãi mãi.
        private int _ticksSinceInput;

        /// <summary>
        /// Bộ số của lớp nhân vật này: tốc độ chạy, độ cao nhảy, thời lượng và hồi chiêu từng hành động.
        /// Tra đúng một lần lúc dựng entity — nó không đổi trong suốt đời entity.
        /// </summary>
        private readonly CharacterProfile _profile;

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

            // Thiếu dòng này thì Step nhận profile null và ném NRE ở MỌI tick. GameLoop nuốt lỗi để
            // một tick hỏng không giết nhịp tim server, nên triệu chứng không phải là crash mà là:
            // không ai được tích phân, không gói MoveState/WorldSnapshot nào được gửi đi.
            _profile = CharacterProfiles.Get(row.ClassId);
        }

        /// <summary>Nhận ý định đã được handler làm sạch. Chạy ở luồng IO, không phải luồng tick.</summary>
        public void SetInput(int seq, MoveIntent intent)
        {
            LastInputSeq = seq;

            _intentDirX = intent.DirX;
            _intentCrouch = intent.Crouch;

            // |= chứ không =. Xem comment ở khai báo _pendingJump.
            _pendingJump |= intent.Jump;

            // Cùng ý với |= ở trên: chỉ ghi khi có gì để ghi, đừng để None xoá mất Attack vừa tới.
            if (intent.Action != ActionRequest.None)
                _pendingAction = intent.Action;

            _ticksSinceInput = 0;
        }

        /// <summary>
        /// Cửa DUY NHẤT để phía server đặt trạng thái hành động (trúng đòn, gục). Vẫn phải hỏi luật:
        /// server có quyền hơn client nhưng không có quyền phá bảng chuyển tiếp — gây choáng cho một
        /// xác chết là vô nghĩa dù ai ra lệnh. Một chỗ duy nhất sửa được tầng 2 thì sau này truy
        /// "ai bật Die" là đọc đúng một hàm.
        ///
        /// CHỈ GỌI TỪ LUỒNG TICK. Lệnh sinh ra ở luồng khác phải đi qua hàng đợi của WorldService.
        /// </summary>
        public bool ForceAction(ActionState action)
        {
            // State là property trả về struct nên "State.Action = ..." không biên dịch được: nó sẽ là
            // phép sửa vào một bản copy tạm rồi vứt đi. Copy ra biến, sửa, gán lại — mặt trái của
            // đúng cái tính chất "gán là copy" đã cứu vòng replay ở Phase 8.
            MoveState state = State;

            if (!CharacterStates.CanEnter(state.Action, state.ActionTicksLeft, action))
                return false;

            state.Action = action;

            // Thời lượng tra từ bảng của CHÍNH nhân vật này, không nhận từ người gọi: cùng một đòn thì
            // hai lớp nhân vật choáng khác nhau, và chỗ ra lệnh không cần biết điều đó.
            state.ActionTicksLeft = _profile.GetAction(action).DurationTicks;
            State = state;

            return true;
        }

        /// <summary>
        /// Đặt lại tầng action về None, BỎ QUA bảng chuyển tiếp. Phải là đường riêng chứ không mượn
        /// ForceAction, vì CanEnter chặn mọi lối ra khỏi Die — mà đó là chủ ý: hồi sinh là quyết định
        /// hành chính của server, không phải một bước chuyển trạng thái trong luật chơi.
        /// </summary>
        public void Revive()
        {
            MoveState state = State;

            state.Action = ActionState.None;
            state.ActionTicksLeft = 0;
            State = state;
        }

        public void Integrate(float dt)
        {
            // Quá 1 giây không có input mới → coi như đã thả phím. Chỉ xoá thứ dạng GIỮ; cú bấm dạng
            // cạnh đã chốt vẫn phải được tiêu thụ — mất mạng không phải cớ để nuốt input đã bấm thật.
            if (++_ticksSinceInput > MovementRules.TICK_RATE)
            {
                _intentDirX = 0f;
                _intentCrouch = false;

                // Kẹp lại luôn để bộ đếm không leo tới tràn int khi một client treo hàng năm trời.
                _ticksSinceInput = MovementRules.TICK_RATE + 1;
            }

            var intent = new MoveIntent
            {
                DirX = _intentDirX,
                Jump = _pendingJump,
                Crouch = _intentCrouch,
                Action = _pendingAction,
            };

            // Đọc-rồi-xoá: một lần bấm chỉ được dùng đúng một tick.
            _pendingJump = false;
            _pendingAction = ActionRequest.None;

            State = MovementRules.Step(State, intent, dt, _profile);
        }
    }
}