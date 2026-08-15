using MMORPG.GameServer.Net;
using MMORPG.GameServer.World;
using MMORPG.Shared.Dto.World;
using MMORPG.Shared.Net;

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

            float dirX = input.DirX;
            float dirY = input.DirY;

            // NaN lây qua MỌI phép toán: lọt một lần là X/Y thành NaN vĩnh viễn và theo
            // SavePosition vào tận DB. Chặn ngay cửa.
            if (!float.IsFinite(dirX) || !float.IsFinite(dirY))
                return Task.FromResult(NetResult.None);

            // Vector dài hơn 1 là gian lận tốc độ (dir=(10,0) = chạy nhanh gấp 10).
            // Chuẩn hoá lại — client tử tế gửi ≤ 1 nên không bị ảnh hưởng.
            float length = MathF.Sqrt(dirX * dirX + dirY * dirY);
            if (length > 1f)
            {
                dirX /= length;
                dirY /= length;
            }

            entity.SetInput(input.Seq, dirX, dirY);

            // Fire-and-forget: không trả lời từng input. Câu trả lời gộp là MoveState mỗi tick.
            return Task.FromResult(NetResult.None);
        }
    }
}
