using MemoryPack;

namespace MMORPG.Shared.Dto.Db
{
    /// <summary>Dùng cho mọi DbCmd không có tham số.</summary>
    [MemoryPackable]
    public partial class DbEmptyRequest
    {
    }

    /// <summary>Dùng cho mọi DbCmd chỉ cần biết thành công hay không.</summary>
    [MemoryPackable]
    public partial class DbOkResponse
    {
        public bool Success { get; set; }

        /// <summary>Chỉ để dev đọc log. Không bao giờ đẩy chuỗi này tới client.</summary>
        public string Detail { get; set; } = string.Empty;
    }

    [MemoryPackable]
    public partial class DbPingRequest
    {
        public long SentAtMs { get; set; }
    }

    [MemoryPackable]
    public partial class DbPingResponse
    {
        public long SentAtMs { get; set; }

        /// <summary>Số bản ghi trong <c>schema_version</c> — chứng minh DB thật sự query được, không chỉ socket sống.</summary>
        public int SchemaVersion { get; set; }
    }

    [MemoryPackable]
    public partial class ServerMetaGetRequest
    {
        public string Key { get; set; } = string.Empty;
    }

    [MemoryPackable]
    public partial class ServerMetaGetResponse
    {
        public bool Found { get; set; }
        public string Value { get; set; } = string.Empty;
    }

    [MemoryPackable]
    public partial class ServerMetaSetRequest
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}
