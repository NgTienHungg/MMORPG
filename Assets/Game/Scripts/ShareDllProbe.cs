using MemoryPack;
using MMORPG.Shared;
using UnityEngine;

namespace Game.Scripts
{
    public class ShareDllProbe : MonoBehaviour
    {
        private void Start()
        {
            var dto = new HandshakeDto { ProtocolVersion = 1, ServerName = "local" };
            
            byte[] bytes = MemoryPackSerializer.Serialize(dto);
            var back = MemoryPackSerializer.Deserialize<HandshakeDto>(bytes);
            
            Debug.Log($"[Probe] serialize {bytes.Length} bytes -> deserialize {back.ProtocolVersion}, {back.ServerName}");
        }
    }
}