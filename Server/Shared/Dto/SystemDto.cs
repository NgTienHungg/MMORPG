using MemoryPack;

namespace MMORPG.Shared.Dto
{
    [MemoryPackable]
    public partial class EmptyRequest
    {
    }

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

    /// <summary>DTO thử của lệnh Echo. Không còn dùng — xoá được.</summary>
    [MemoryPackable]
    public partial class EchoRequest
    {
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>DTO thử của lệnh Echo. Không còn dùng — xoá được.</summary>
    [MemoryPackable]
    public partial class EchoResponse
    {
        public string Message { get; set; } = string.Empty;
        public long ServerTimeMs { get; set; }
    }

    [MemoryPackable]
    public partial class ServerInfoResponse
    {
        public string ServerName { get; set; } = string.Empty;
        public int OnlineCount { get; set; }
    }

    [MemoryPackable]
    public partial class VersionCheckRequest
    {
        public uint ContractHash { get; set; }
    }

    [MemoryPackable]
    public partial class VersionCheckResponse
    {
        public bool Ok { get; set; }

        /// <summary>
        /// Gửi cả số của server dù client không cần để quyết định gì: nó là thứ người ta dán vào
        /// báo lỗi. "Phiên bản không khớp" không giúp ai; "client A1B2C3D4, server E5F6A7B8" thì có.
        /// </summary>
        public uint ServerHash { get; set; }
    }
}
