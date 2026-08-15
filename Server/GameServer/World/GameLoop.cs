using System.Diagnostics;
using MMORPG.ServerCore;
using MMORPG.Shared.World;

namespace MMORPG.GameServer.World
{
    /// <summary>
    /// Nhịp tim của server: gọi <see cref="WorldService.Tick"/> với bước thời gian cố định,
    /// đều đặn bất kể máy nhanh hay chậm.
    /// </summary>
    public sealed class GameLoop
    {
        /// <summary>
        /// Trần số tick bù trong một lượt. Không có trần: một cú khựng (GC, breakpoint) làm nợ
        /// thời gian phình ra, vòng sau phải bù nhiều tick hơn, lại càng lâu — spiral of death.
        /// Có trần: thế giới chậm lại một nhịp rồi chạy tiếp, xấu nhưng sống.
        /// </summary>
        private const int MAX_CATCH_UP = 5;

        private readonly WorldService _worldService;

        public GameLoop(WorldService worldService)
        {
            _worldService = worldService;
        }

        public async Task RunAsync(CancellationToken ct)
        {
            // Stopwatch chứ không phải DateTime.Now: độ phân giải cao và không bị
            // đổi giờ hệ thống / NTP kéo lùi thời gian.
            var stopwatch = Stopwatch.StartNew();
            double last = 0;
            double accumulator = 0;

            Log.Info($"Game loop {MovementRules.TICK_RATE.ToString().Green()} tick/s " +
                     $"({MovementRules.TICK_DT * 1000:0}ms/tick)");

            while (!ct.IsCancellationRequested)
            {
                // Cộng dồn thời gian thật đã trôi từ vòng trước. Vòng lặp dậy trễ bao nhiêu
                // không quan trọng — nợ được ghi lại đủ ở đây.
                double now = stopwatch.Elapsed.TotalSeconds;
                accumulator += now - last;
                last = now;

                if (accumulator > MovementRules.TICK_DT * MAX_CATCH_UP)
                    accumulator = MovementRules.TICK_DT * MAX_CATCH_UP;

                // Trả nợ: mỗi TICK_DT nợ là một tick, có thể 0 hoặc nhiều tick trong một lượt dậy.
                while (accumulator >= MovementRules.TICK_DT)
                {
                    accumulator -= MovementRules.TICK_DT;

                    try
                    {
                        _worldService.Tick(MovementRules.TICK_DT);
                    }
                    catch (Exception ex)
                    {
                        // Một tick hỏng không được giết nhịp tim của cả server.
                        Log.Error(ex, "Tick ném lỗi");
                    }
                }

                // Nhường CPU. Trên Windows lượt "ngủ 1ms" này thật ra ~15ms do độ phân giải timer —
                // chính vì thế mới cần accumulator thay vì tin vào Delay.
                await Task.Delay(1, ct);
            }
        }
    }
}
