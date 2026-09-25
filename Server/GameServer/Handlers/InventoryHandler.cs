using MMORPG.GameServer.Boot;
using MMORPG.GameServer.Net;
using MMORPG.GameServer.World;
using MMORPG.Shared.Dto.Inventory;
using MMORPG.Shared.Net;

namespace MMORPG.GameServer.Handlers
{
    /// <summary>
    /// Ba lệnh, và cả ba đều KHÔNG trả response: kết quả đi bằng <c>InventoryDelta</c> mà service
    /// tự gửi. Đó là cố ý — nếu có cả response lẫn delta thì client có hai nguồn cho cùng một sự
    /// thật, và hai nguồn thì sớm muộn lệch nhau.
    /// </summary>
    public static class InventoryHandler
    {
        private static InventoryService InventoryService => ServerServices.Get<InventoryService>();

        [TcpHandler(NetCmd.ItemUse, MinState = SessionState.InWorld)]
        public static Task<NetResult> OnUse(NetRequest req)
        {
            PlayerEntity entity = req.Session.Entity;

            // MinState đã chặn phần lớn, nhưng LeaveWorld có thể xảy ra giữa lúc gói đang bay.
            if (entity == null)
                return Task.FromResult(NetResult.None);

            InventoryService.Use(entity, req.GetData<ItemUseRequest>().Slot);

            return Task.FromResult(NetResult.None);
        }

        [TcpHandler(NetCmd.ItemDrop, MinState = SessionState.InWorld)]
        public static Task<NetResult> OnDrop(NetRequest req)
        {
            PlayerEntity entity = req.Session.Entity;

            if (entity == null)
                return Task.FromResult(NetResult.None);

            var request = req.GetData<ItemDropRequest>();
            InventoryService.Drop(entity, request.Slot, request.Quantity);

            return Task.FromResult(NetResult.None);
        }

        [TcpHandler(NetCmd.ItemMove, MinState = SessionState.InWorld)]
        public static Task<NetResult> OnMove(NetRequest req)
        {
            PlayerEntity entity = req.Session.Entity;

            if (entity == null)
                return Task.FromResult(NetResult.None);

            var request = req.GetData<ItemMoveRequest>();
            InventoryService.Move(entity, request.FromSlot, request.ToSlot);

            return Task.FromResult(NetResult.None);
        }
    }
}
