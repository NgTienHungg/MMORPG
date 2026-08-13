using System;
using MMORPG.Shared.Dto;
using MMORPG.Shared.Net;

namespace MMORPG.Client.Network.Handlers
{
    public sealed class AuthNetHandler : INetHandlerGroup
    {
        public event Action<AuthResponse> OnRegisterResult;
        public event Action<AuthResponse> OnLoginResult;
        public event Action<AuthResponse> OnLogoutResult;
        public event Action<KickedNotice> OnKicked;

        [NetHandler(NetCmd.Register)]
        private void HandleRegister(NetPacket packet)
        {
            OnRegisterResult?.Invoke(packet.GetData<AuthResponse>());
        }

        [NetHandler(NetCmd.Login)]
        private void HandleLogin(NetPacket packet)
        {
            OnLoginResult?.Invoke(packet.GetData<AuthResponse>());
        }

        [NetHandler(NetCmd.Logout)]
        private void HandleLogout(NetPacket packet)
        {
            OnLogoutResult?.Invoke(packet.GetData<AuthResponse>());
        }

        [NetHandler(NetCmd.Kicked)]
        private void HandleKicked(NetPacket packet)
        {
            OnKicked?.Invoke(packet.GetData<KickedNotice>());
        }
    }
}