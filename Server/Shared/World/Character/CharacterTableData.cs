using System;
using MemoryPack;
using MMORPG.Shared.World.Movement;
using Newtonsoft.Json;

namespace MMORPG.Shared.World.Character
{
    /// <summary>
    /// Các con số của MỘT hành động, đọc từ file và đi được trên dây.
    ///
    /// Là struct có chủ đích: <c>default</c> của nó là "0 tick, không khoá thân", nên
    /// <see cref="CharacterConfig.GetAction"/> trả về được một giá trị hợp lệ cho hành động không có
    /// trong bảng mà chỗ gọi không phải kiểm null.
    ///
    /// Toàn kiểu unmanaged nên MemoryPack chép nguyên khối: MỌI trường đều đi trên dây và vào vân
    /// tay, kể cả hai trường tick do <see cref="Prepare"/> điền.
    /// </summary>
    [MemoryPackable]
    public partial struct ActionData
    {
        /// <summary>Ghi bằng TÊN enum trong file ("Attack"), không phải số — người sửa file không phải tra bảng.</summary>
        public ActionState Action;

        /// <summary>Thời lượng hành động, bằng đơn vị người thiết kế dùng.</summary>
        public float DurationSeconds;

        /// <summary>Thời gian hồi trước khi dùng lại được.</summary>
        public float CooldownSeconds;

        /// <summary>Trong lúc hành động diễn ra thì thân thể có mất quyền điều khiển không.</summary>
        public bool LocksMovement;

        /// <summary>Bản tick của <see cref="DurationSeconds"/> — thứ mô phỏng thật sự đọc.</summary>
        public int DurationTicks;

        /// <summary>Bản tick của <see cref="CooldownSeconds"/>.</summary>
        public int CooldownTicks;

        /// <summary>Quy giây ra tick. Gọi một lần lúc nạp bảng, không gọi trong vòng tick.</summary>
        public void Prepare()
        {
            DurationTicks = MovementRules.ToTicks(DurationSeconds);
            CooldownTicks = MovementRules.ToTicks(CooldownSeconds);
        }
    }

    /// <summary>
    /// Cả bảng nhân vật, bản đối chiếu 1-1 với <c>characters.json</c>. Không đi trên dây: mỗi bên
    /// đọc file của chính nó, và vân tay chỉ để in ra log (<see cref="ConfigFingerprint"/>).
    /// </summary>
    [MemoryPackable]
    public sealed partial class CharacterTableData : IConfigFile
    {
        /// <summary>Phiên bản schema, nằm trong file.</summary>
        public int Version { get; set; } = 1;

        /// <summary>Mỗi phần tử là một lớp nhân vật.</summary>
        public CharacterConfig[] Classes { get; set; } = Array.Empty<CharacterConfig>();

        /// <summary>Đếm ra từ <see cref="Classes"/>, nên không tuần tự hoá ở cả hai bộ.</summary>
        [MemoryPackIgnore] [JsonIgnore] public int RowCount => Classes.Length;
    }
}
