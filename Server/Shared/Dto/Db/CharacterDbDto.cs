using MemoryPack;

namespace MMORPG.Shared.Dto.Db
{
    /// <summary>Một dòng nguyên vẹn của bảng <c>character</c>. Chỉ đi trên đường nội bộ.</summary>
    [MemoryPackable]
    public partial class CharacterRow
    {
        public long CharacterId { get; set; }
        public long AccountId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int ClassId { get; set; }
        public int Level { get; set; }
        public long Exp { get; set; }
        public int MapId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
    }

    /// <summary>
    /// Giá trị mặc định cho lần tạo đầu do GAMESERVER quyết — spawn ở đâu, nghề gì là luật chơi,
    /// không phải việc của tầng lưu trữ.
    /// </summary>
    [MemoryPackable]
    public partial class CharacterGetOrCreateRequest
    {
        public long AccountId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int ClassId { get; set; }
        public int MapId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
    }

    [MemoryPackable]
    public partial class CharacterGetOrCreateResponse
    {
        /// <summary>true = lần này vừa tạo mới (lần vào world đầu tiên của tài khoản).</summary>
        public bool Created { get; set; }

        public CharacterRow Character { get; set; }
    }

    [MemoryPackable]
    public partial class CharacterSavePositionRequest
    {
        public long CharacterId { get; set; }
        public int MapId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
    }
}
