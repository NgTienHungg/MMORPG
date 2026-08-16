using System;
using MMORPG.Shared.Dto.Character;
using MMORPG.Shared.Dto.World;
using MMORPG.Shared.Net;

namespace MMORPG.Client.Network.Handlers
{
    public class WorldNetHandler : INetHandlerGroup
    {
        public event Action<EnterWorldResponse> OnEnteredWorld;
        public event Action<MoveStateResponse> OnMoveState;

        [NetHandler(NetCmd.EnterWorld)]
        private void HandleEnterWorld(NetPacket packet)
        {
            OnEnteredWorld?.Invoke(packet.GetData<EnterWorldResponse>());
        }
        
        [NetHandler(NetCmd.MoveState)]
        private void HandleMoveState(NetPacket packet)
        {
            OnMoveState?.Invoke(packet.GetData<MoveStateResponse>());
        }
    }
}
