using HungNT;
using MMORPG.Client.Network;
using MMORPG.Shared.Dto;
using MMORPG.Shared.Dto.World;
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
        
        public void Move(int seq, float dirX, float dirY)
        {
            // Không log ở đây — 20 lần/giây, log là dìm chết console.
            _netService.Send(NetCmd.MoveInput, new MoveInputRequest { Seq = seq, DirX = dirX, DirY = dirY });
        }
    }
}
