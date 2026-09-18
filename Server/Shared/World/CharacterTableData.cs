using System;
using MemoryPack;
using Newtonsoft.Json;

namespace MMORPG.Shared.World
{
    /// <summary>
    /// Các con số của MỘT hành động. Thay cho ActionDefinition của Phase 9 — cùng nội dung, khác ở chỗ
    /// nó đọc được từ file và đi được trên dây.
    ///
    /// Vẫn là STRUCT, và đó không phải chuyện phong cách: <c>default</c> của nó là "0 tick, không khoá
    /// thân", nên <see cref="CharacterConfig.GetAction"/> trả về được một giá trị hợp lệ cho hành động
    /// không có trong bảng mà chỗ gọi không phải kiểm null. Đổi sang class là mọi chỗ gọi mọc thêm một
    /// phép kiểm, và một trong số đó sẽ bị quên.
    /// </summary>
    [MemoryPackable]
    public partial struct ActionData
    {
        /// <summary>Ghi bằng TÊN enum trong file ("Attack"), không phải số — người sửa file không phải tra bảng.</summary>
        public ActionState Action;

        public float DurationSeconds;

        public float CooldownSeconds;

        /// <summary>
        /// Trong lúc hành động này diễn ra thì thân thể có mất quyền điều khiển không. Là dữ liệu chứ
        /// không phải một nhánh switch: thêm chiêu "đứng yên đọc chú" chỉ là thêm một ô true.
        /// </summary>
        public bool LocksMovement;

        // Dẫn xuất — xem ghi chú ở WorldRules, cùng lý do và cùng cặp thuộc tính bỏ qua.
        [MemoryPackIgnore] [JsonIgnore] public int DurationTicks;

        [MemoryPackIgnore] [JsonIgnore] public int CooldownTicks;

        public void Prepare()
        {
            DurationTicks = MovementRules.ToTicks(DurationSeconds);
            CooldownTicks = MovementRules.ToTicks(CooldownSeconds);
        }
    }

    /// <summary>
    /// Cả bảng. Vừa là bản đối chiếu với characters.json, vừa là thứ đi trong EnterWorldResponse —
    /// bảng này nhỏ nên chọn chế độ "gửi cả dữ liệu", và ở chế độ đó thì lệch là chuyện không xảy ra
    /// được. Checksum vẫn có: nó là một dòng log để so hai server, và là chỗ nối cho Phase 18.
    /// </summary>
    [MemoryPackable]
    public sealed partial class CharacterTableData
    {
        public int Version { get; set; } = 1;

        public CharacterConfig[] Classes { get; set; } = Array.Empty<CharacterConfig>();

        /// <summary>
        /// Dấu vân tay của NỘI DUNG bảng. Không băm Name: nó chỉ để người đọc file dễ chịu, đổi nó
        /// không đổi một hành vi nào — mà dấu vân tay phải trả lời "hai bên có chạy cùng luật không".
        /// </summary>
        public uint Checksum()
        {
            uint hash = Fnv1a.START;

            hash = Fnv1a.Mix(hash, Version);

            foreach (CharacterConfig profile in Classes)
            {
                hash = Fnv1a.Mix(hash, profile.ClassId);
                hash = Fnv1a.Mix(hash, profile.MoveSpeed);
                hash = Fnv1a.Mix(hash, profile.JumpSpeed);
                hash = Fnv1a.Mix(hash, profile.BodyHalfWidth);
                hash = Fnv1a.Mix(hash, profile.BodyHeight);
                hash = Fnv1a.Mix(hash, profile.BodyHeightCrouch);

                foreach (ActionData action in profile.Actions)
                {
                    hash = Fnv1a.Mix(hash, (int)action.Action);
                    hash = Fnv1a.Mix(hash, action.DurationSeconds);
                    hash = Fnv1a.Mix(hash, action.CooldownSeconds);
                    hash = Fnv1a.Mix(hash, action.LocksMovement ? 1 : 0);
                }
            }

            return hash;
        }
    }
}
