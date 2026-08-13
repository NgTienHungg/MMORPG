using System;
using MemoryPack;

namespace MMORPG.Shared.Dto.Db
{
    [MemoryPackable]
    public partial class AccountGetRequest
    {
        public string Username { get; set; } = string.Empty;
    }

    [MemoryPackable]
    public partial class AccountGetResponse
    {
        public bool Found { get; set; }

        public long AccountId { get; set; }
        public string Username { get; set; } = string.Empty;

        public byte[] PasswordHash { get; set; } = Array.Empty<byte>();
        public byte[] Salt { get; set; } = Array.Empty<byte>();
        public int Iterations { get; set; }

        public bool IsBanned { get; set; }
    }

    [MemoryPackable]
    public partial class AccountCreateRequest
    {
        public string Username { get; set; } = string.Empty;
        public byte[] PasswordHash { get; set; } = Array.Empty<byte>();
        public byte[] Salt { get; set; } = Array.Empty<byte>();
        public int Iterations { get; set; }
    }

    [MemoryPackable]
    public partial class AccountCreateResponse
    {
        /// <summary>false nghĩa là tên đã tồn tại — đây là kết quả bình thường, không phải lỗi.</summary>
        public bool Created { get; set; }

        public long AccountId { get; set; }
    }

    [MemoryPackable]
    public partial class AccountTouchLoginRequest
    {
        public long AccountId { get; set; }
    }
}
