using MemoryPack;

namespace MMORPG.Shared;

/// <summary>
/// DTO thử để kiểm chứng đường ống Shared → Unity đã thông. Không còn luồng nào dùng — xoá được.
/// </summary>
[MemoryPackable]
public partial class HandshakeDto
{
    public int ProtocolVersion { get; set; }
    public string ServerName { get; set; } = string.Empty;
}