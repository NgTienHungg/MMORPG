using HungNT;
using MMORPG.Client.Network;
using MMORPG.Shared.Dto;
using MMORPG.Shared.Dto.World;
using MMORPG.Shared.Net;
using MMORPG.Shared.World;

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

        /// <summary>Gửi đúng cái intent vừa dùng để dự đoán — không dàn nó ra thành từng tham số rồi ráp lại.</summary>
        public void Move(int seq, MoveIntent intent)
        {
            // Không log ở đây — 20 lần/giây, log là dìm chết console.
            _netService.Send(NetCmd.MoveInput, new MoveInputRequest { Seq = seq, Intent = intent });
        }
    }
}
