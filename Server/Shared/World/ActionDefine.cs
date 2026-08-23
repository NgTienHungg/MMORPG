using System.Collections.Generic;

namespace MMORPG.Shared.World
{
    /// <summary>
    /// Các con số của MỘT hành động. Người thiết kế viết bằng giây — đơn vị của họ; hàm dựng quy ra
    /// tick ngay tại đây để từ đó về sau mô phỏng chỉ còn làm việc với số nguyên.
    /// </summary>
    public readonly struct ActionDefinition
    {
        /// <summary>Hành động kéo dài bao nhiêu tick.</summary>
        public readonly int DurationTicks;

        /// <summary>Nhịp tối thiểu giữa hai lần dùng, đếm từ lúc BẮT ĐẦU lần trước.</summary>
        public readonly int CooldownTicks;

        /// <summary>
        /// Trong lúc hành động này diễn ra thì thân thể có mất quyền điều khiển không.
        /// Là dữ liệu chứ không phải một nhánh switch: thêm một chiêu "đứng yên đọc chú" chỉ là
        /// thêm một ô true, không phải sửa luật.
        /// </summary>
        public readonly bool LocksMovement;

        public ActionDefinition(float durationSeconds, float cooldownSeconds, bool locksMovement)
        {
            DurationTicks = MovementRules.ToTicks(durationSeconds);
            CooldownTicks = MovementRules.ToTicks(cooldownSeconds);
            LocksMovement = locksMovement;
        }
    }

    /// <summary>
    /// Bộ số của một lớp nhân vật: chạy nhanh bao nhiêu, nhảy cao bao nhiêu, mỗi hành động dài bao lâu.
    ///
    /// Cả server lẫn client đều đọc — server để mô phỏng, client để dự đoán và để co hoạt ảnh cho vừa
    /// thời lượng. Vì vậy nó phải ở Shared: hai bên đọc hai bảng khác nhau thì mọi thứ vẫn chạy, chỉ
    /// là lệch dần, và không có lỗi nào để lần theo.
    /// </summary>
    public sealed class CharacterProfile
    {
        public int ClassId { get; }

        /// <summary>Tốc độ chạy ngang, world unit/giây.</summary>
        public float MoveSpeed { get; }

        /// <summary>Vận tốc bật lên tức thời khi nhảy.</summary>
        public float JumpSpeed { get; }

        private readonly Dictionary<ActionState, ActionDefinition> _actions;

        public CharacterProfile(int classId, float moveSpeed, float jumpSpeed,
            Dictionary<ActionState, ActionDefinition> actions)
        {
            ClassId = classId;
            MoveSpeed = moveSpeed;
            JumpSpeed = jumpSpeed;
            _actions = actions;
        }

        /// <summary>
        /// Số liệu của một hành động. Hành động không có trong bảng (kể cả None) trả về bản rỗng:
        /// 0 tick, không khoá thân — nhờ vậy chỗ gọi không phải kiểm null hay kiểm None.
        /// </summary>
        public ActionDefinition GetAction(ActionState action)
        {
            if (!_actions.TryGetValue(action, out ActionDefinition definition))
                return default;

            return definition;
        }
    }

    /// <summary>
    /// Bảng tra profile theo lớp nhân vật. Hiện dựng bằng C# ngay trong Shared; khi bảng chuyển sang
    /// đọc từ file thì chỉ hàm Build đổi, mọi chỗ gọi Get giữ nguyên.
    /// </summary>
    public static class CharacterProfiles
    {
        public const int DRAGON_WARRIOR = 1;

        private static readonly Dictionary<int, CharacterProfile> _byClassId = Build();

        public static CharacterProfile Get(int classId)
        {
            if (!_byClassId.TryGetValue(classId, out CharacterProfile profile))
                return _byClassId[DRAGON_WARRIOR];

            return profile;
        }

        private static Dictionary<int, CharacterProfile> Build()
        {
            var dragonWarrior = new CharacterProfile(
                DRAGON_WARRIOR,
                moveSpeed: 5f,
                jumpSpeed: 11f,
                new Dictionary<ActionState, ActionDefinition>
                {
                    // Mọi con số dưới đây viết bằng GIÂY. Đòn đánh 0.25s cho clip 3 frame là 12fps,
                    // vừa mắt; hồi chiêu 0.4s là nhịp bấm liên tục mà không thành máy khoan.
                    [ActionState.Attack] = new ActionDefinition(0.25f, 0.4f, locksMovement: false),

                    // Choáng thì khoá thân: mất quyền điều khiển là toàn bộ ý nghĩa của trúng đòn.
                    [ActionState.Hurt] = new ActionDefinition(0.2f, 0f, locksMovement: true),

                    // Hết 1 giây là hết HOẠT ẢNH gục; trạng thái Die thì ở lại cho tới khi hồi sinh.
                    [ActionState.Die] = new ActionDefinition(1f, 0f, locksMovement: true),
                });

            return new Dictionary<int, CharacterProfile>
            {
                [dragonWarrior.ClassId] = dragonWarrior,
            };
        }
    }
}