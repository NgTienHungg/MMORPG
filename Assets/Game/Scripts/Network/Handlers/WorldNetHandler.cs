using System;
using MMORPG.Shared.Dto.Character;
using MMORPG.Shared.Dto.World;
using MMORPG.Shared.Net;

namespace MMORPG.Client.Network.Handlers
{
    public class WorldNetHandler : INetHandlerGroup
    {
        public event Action<EnterWorldResponse> OnEnterWorldResult;
        public event Action<MoveStateResponse> OnMoveStateResult;
        public event Action<EntitySpawnNotice> OnEntitySpawn;
        public event Action<EntityDespawnNotice> OnEntityDespawn;
        public event Action<WorldSnapshotNotice> OnSnapshot;

        [NetHandler(NetCmd.EnterWorld)]
        private void HandleEnterWorld(NetPacket packet)
        {
            OnEnterWorldResult?.Invoke(packet.GetData<EnterWorldResponse>());
        }

        [NetHandler(NetCmd.MoveState)]
        private void HandleMoveState(NetPacket packet)
        {
            OnMoveStateResult?.Invoke(packet.GetData<MoveStateResponse>());
        }

        [NetHandler(NetCmd.EntitySpawn)]
        private void HandleEntitySpawn(NetPacket packet)
        {
            OnEntitySpawn?.Invoke(packet.GetData<EntitySpawnNotice>());
        }

        [NetHandler(NetCmd.EntityDespawn)]
        private void HandleEntityDespawn(NetPacket packet)
        {
            OnEntityDespawn?.Invoke(packet.GetData<EntityDespawnNotice>());
        }

        [NetHandler(NetCmd.WorldSnapshot)]
        private void HandleSnapshot(NetPacket packet)
        {
            OnSnapshot?.Invoke(packet.GetData<WorldSnapshotNotice>());
        }
    }
}
