using System;
using MMORPG.Shared.Dto.Character;
using MMORPG.Shared.Net;

namespace MMORPG.Client.Network.Handlers
{
    public class WorldNetHandler : INetHandlerGroup
    {
        public event Action<EnterWorldResponse> OnEnteredWorld;

        [NetHandler(NetCmd.EnterWorld)]
        private void OnEnterWorld(NetPacket packet)
        {
            OnEnteredWorld?.Invoke(packet.GetData<EnterWorldResponse>());
        }
    }
}
