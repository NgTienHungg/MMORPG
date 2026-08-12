using MemoryPack;

namespace MMORPG.Shared.Dto
{
    /// <summary>Client gửi mốc thời gian của chính nó để tự tính RTT khi nhận lại.</summary>
    [MemoryPackable]
    public partial class PingRequest
    {
        public long ClientTimeMs { get; set; }
    }

    /// <summary>Server echo lại mốc của client, kèm thời gian server để sau này dùng đồng bộ đồng hồ.</summary>
    [MemoryPackable]
    public partial class PingResponse
    {
        public long ClientTimeMs { get; set; }
        public long ServerTimeMs { get; set; }
    }

    /// <summary>Server báo một request bị lỗi.</summary>
    [MemoryPackable]
    public partial class ErrorResponse
    {
        /// <summary>Lệnh nào gây lỗi.</summary>
        public int FailedCmd { get; set; }

        public Net.ErrorCode Code { get; set; }

        /// <summary>Mô tả cho dev. KHÔNG hiển thị thẳng cho người chơi.</summary>
        public string Detail { get; set; } = string.Empty;
    }

    /// <summary>DTO thử của Phase 2. Xoá khi Phase 4 xong.</summary>
    [MemoryPackable]
    public partial class EchoRequest
    {
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>DTO thử của Phase 2. Xoá khi Phase 4 xong.</summary>
    [MemoryPackable]
    public partial class EchoResponse
    {
        public string Message { get; set; } = string.Empty;
        public long ServerTimeMs { get; set; }
    }

    [MemoryPackable]
    public partial class ServerInfoRequest
    {
    }

    [MemoryPackable]
    public partial class ServerInfoResponse
    {
        public string ServerName { get; set; } = string.Empty;
        public int OnlineCount { get; set; }
    }
}
