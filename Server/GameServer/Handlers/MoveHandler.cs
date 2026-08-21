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
                // Chống hack tốc độ: DirX = 10 là chạy nhanh gấp 10. Giờ DirX là số vô hướng nên chỉ
                // cần kẹp, không phải chuẩn hoá vector như hồi còn hai trục.
                DirX = Math.Clamp(input.Intent.DirX, -1f, 1f),

                // Jump không cần kiểm: bool chỉ có hai giá trị. Client gian lận gửi Jump = true mỗi
                // tick cũng không bay được — điều kiện coyote/buffer nằm trong MovementRules.Step và
                // Step chạy ở đây, không ở máy họ. Thứ duy nhất họ được là tự nhảy lại mỗi lần tiếp
                // đất, đúng bằng thứ một người bấm Space liên tục cũng có.
                Jump = input.Intent.Jump,
            };

            entity.SetInput(input.Seq, intent);

            // Fire-and-forget: không trả lời từng input. Câu trả lời gộp là MoveState mỗi tick.
            return Task.FromResult(NetResult.None);
        }
    }
}
