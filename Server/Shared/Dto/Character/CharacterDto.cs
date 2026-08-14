using System;
using MemoryPack;
using MMORPG.Shared.Net;

namespace MMORPG.Shared.Dto.Character
{
    [MemoryPackable]
    public partial class EnterWorldResponsive
    {
        public bool Success { get; set; }

        public ErrorCode Error { get; set; }

        /// <summary>Id runtime trong world. Chỉ có nghĩa tới khi rời world.</summary>
        public int EntityId { get; set; }

        public long CharacterId { get; set; }

        public string Name { get; set; } = string.Empty;

        public int ClassId { get; set; }

        public int Level { get; set; }

        public int MapId { get; set; }

        public float X { get; set; }

        public float Y { get; set; }

        /// <summary>Mốc thời gian server lúc vào. Phase 6 dùng làm gốc cho đồng bộ tick.</summary>
        public long ServerTimeMs { get; set; }
    }
}