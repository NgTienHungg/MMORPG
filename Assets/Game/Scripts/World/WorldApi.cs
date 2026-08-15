using HungNT;
using MMORPG.Client.Network;
using MMORPG.Shared.Dto;
using MMORPG.Shared.Net;

namespace MMORPG.Client.World
{
    /// <summary>
    /// Gom mọi lệnh world mà client GỬI ĐI. Đối xứng với <see cref="Network.Handlers.WorldNetHandler"/> ở chiều nhận.
    /// </summary>
    public sealed class WorldApi
    {
        private readonly NetService _netService;

        public WorldApi(NetService netService)
        {
            _netService = netService;
        }

        public void EnterWorld()
        {
            this.Log("Enter World");
            _netService.Send(NetCmd.EnterWorld, new EmptyRequest());
        }
    }
}
