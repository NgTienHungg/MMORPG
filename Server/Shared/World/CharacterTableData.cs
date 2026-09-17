using System;
using MemoryPack;

namespace MMORPG.Shared.World
{
    /// <summary>Một hành động trong file, ở đơn vị của người viết số.</summary>
    [MemoryPackable]
    public sealed partial class ActionData
    {
        /// <summary>Ghi bằng TÊN enum trong file ("Attack"), không phải số — người sửa file không phải tra bảng.</summary>
        public ActionState Action { get; set; }

        public float DurationSeconds { get; set; }

        public float CooldownSeconds { get; set; }

        public bool LocksMovement { get; set; }
    }

    /// <summary>Một lớp nhân vật trong file. Cùng kiểu này đi trên dây xuống client.</summary>
    [MemoryPackable]
    public sealed partial class CharacterProfileData
    {
        public int ClassId { get; set; }

        /// <summary>Tên để đọc log và sửa file cho dễ. Mô phỏng không dùng.</summary>
        public string Name { get; set; } = string.Empty;

        public float MoveSpeed { get; set; } = 5f;
        public float JumpSpeed { get; set; } = 16f;
        public float BodyHalfWidth { get; set; } = 0.35f;
        public float BodyHeight { get; set; } = 1.6f;
        public float BodyHeightCrouch { get; set; } = 0.9f;

        /// <summary>Mảng chứ không phải object có khoá cố định: thêm một hành động mới là thêm phần tử.</summary>
        public ActionData[] Actions { get; set; } = Array.Empty<ActionData>();
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

        public CharacterProfileData[] Classes { get; set; } = Array.Empty<CharacterProfileData>();

        /// <summary>
        /// Dấu vân tay của NỘI DUNG bảng. Không băm Name: nó chỉ để người đọc file dễ chịu, đổi nó
        /// không đổi một hành vi nào — mà dấu vân tay phải trả lời "hai bên có chạy cùng luật không".
        /// </summary>
        public uint Checksum()
        {
            uint hash = Fnv1a.START;

            hash = Fnv1a.Mix(hash, Version);

            foreach (CharacterProfileData profile in Classes)
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
