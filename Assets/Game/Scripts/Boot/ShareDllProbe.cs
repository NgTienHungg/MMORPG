using HungNT;
using MemoryPack;
using MMORPG.Shared;
using UnityEngine;

namespace MMORPG.Client.Boot
{
    /// <summary>
    /// Script tạm để xác nhận Unity nạp được MMORPG.Shared.dll + MemoryPack.
    /// Hết việc từ Phase 2 — NetworkProbe đã chứng minh cả chuỗi. Xoá GameObject rồi xoá file này.
    /// </summary>
    public class ShareDllProbe : MonoBehaviour
    {
        private void Start()
        {
            var dto = new HandshakeDto { ProtocolVersion = 1, ServerName = "local" };

            byte[] bytes = MemoryPackSerializer.Serialize(dto);
            var back = MemoryPackSerializer.Deserialize<HandshakeDto>(bytes);

            this.Log($"serialize {bytes.Length} bytes -> deserialize {back.ProtocolVersion}, {back.ServerName}");
        }
    }
}