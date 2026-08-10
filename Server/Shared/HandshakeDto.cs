using MemoryPack;

namespace MMORPG.Shared;

/// <summary>
/// DTO thử để kiểm chứng đường ống Shared → Unity đã thông.
/// Phase 1 sẽ thay bằng contract thật.
/// </summary>
[MemoryPackable]
public partial class HandshakeDto
{
    public int ProtocolVersion { get; set; }
    public string ServerName { get; set; } = string.Empty;
}