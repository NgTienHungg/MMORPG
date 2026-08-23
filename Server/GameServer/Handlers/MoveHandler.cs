using MMORPG.GameServer.Net;
using MMORPG.GameServer.World;
using MMORPG.Shared.Dto.World;
using MMORPG.Shared.Net;
using MMORPG.Shared.World;

namespace MMORPG.GameServer.Handlers
{
    public static class MoveHandler
    {
        [TcpHandler(NetCmd.MoveInput, MinState = SessionState.InWorld)]
        public static Task<NetResult> OnMoveInput(NetRequest req)
        {
            var input = req.GetData<MoveInputRequest>();
            PlayerEntity entity = req.Session.Entity;

            // MinState đã chặn phần lớn, nhưng LeaveWorld có thể xảy ra giữa lúc gói đang bay.
            if (entity == null)
                return Task.FromResult(NetResult.None);

            // NaN lây qua MỌI phép toán: lọt một lần là X/Y thành NaN vĩnh viễn và theo SavePosition
            // vào tận DB. Chặn ngay cửa. Lưu ý NaN < -1 và NaN > 1 đều FALSE nên Clamp dưới đây
            // không bắt được nó — thứ tự hai phép này không đảo được.
            if (!float.IsFinite(input.Intent.DirX))
                return Task.FromResult(NetResult.None);

            // Dựng lại intent đã làm sạch thay vì dùng thẳng cái client gửi: gói tin là dữ liệu của
            // người lạ, chỉ những trường đã qua kiểm mới được đi tiếp vào mô phỏng.
            var intent = new MoveIntent
            {
                // Chống hack tốc độ: DirX = 10 là chạy nhanh gấp 10.
                DirX = Math.Clamp(input.Intent.DirX, -1f, 1f),

                // Jump và Crouch không cần kiểm: bool chỉ có hai giá trị. Gửi Jump = true mỗi tick
                // cũng vô ích — điều kiện coyote/buffer nằm trong MovementRules.Step, và Step chạy
                // ở đây chứ không ở máy họ.
                Jump = input.Intent.Jump,
                Crouch = input.Intent.Crouch,

                // Enum trên dây chỉ là một byte do MÁY KHÁC gửi: (ActionRequest)77 hợp lệ hoàn toàn
                // với C#, không khớp nhánh nào và tuỳ chỗ dùng mà im lặng hoặc nổ. Kiểu dữ liệu bảo
                // vệ code khỏi chính mình; kiểm miền giá trị mới là thứ bảo vệ server khỏi người khác.
                Action = Enum.IsDefined(input.Intent.Action) ? input.Intent.Action : ActionRequest.None,
            };

            entity.SetInput(input.Seq, intent);

            // Fire-and-forget: không trả lời từng input. Câu trả lời gộp là MoveState mỗi tick.
            return Task.FromResult(NetResult.None);
        }
    }
}
