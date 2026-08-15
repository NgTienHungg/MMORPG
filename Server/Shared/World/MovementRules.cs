using System;

namespace MMORPG.Shared.World
{
    /// <summary>
    /// Hằng số và công thức di chuyển dùng CHUNG: server mô phỏng thật, client dự đoán trước.
    /// Hai bên phải ra cùng kết quả từ cùng input — vì vậy công thức chỉ tồn tại ở đây, một nơi.
    /// </summary>
    public static class MovementRules
    {
        public const int TICK_RATE = 20;
        public const float TICK_DT = 1f / TICK_RATE;

        /// <summary>Tốc độ chạy, đơn vị world/giây.</summary>
        public const float MOVE_SPEED = 5f;

        /// <summary>Nửa cạnh vùng đi lại: map tạm là hình vuông [-E, +E] quanh gốc, chưa có va chạm.</summary>
        public const float WORLD_HALF_EXTENT = 20f;

        /// <summary>
        /// Một bước mô phỏng: dịch theo hướng rồi kẹp trong biên map.
        /// dir phải đã chuẩn hoá (độ dài ≤ 1) — người gọi chịu trách nhiệm, hàm này không kiểm lại.
        /// </summary>
        public static (float X, float Y) Step(float x, float y, float dirX, float dirY, float dt)
        {
            x += dirX * MOVE_SPEED * dt;
            y += dirY * MOVE_SPEED * dt;

            x = Math.Clamp(x, -WORLD_HALF_EXTENT, WORLD_HALF_EXTENT);
            y = Math.Clamp(y, -WORLD_HALF_EXTENT, WORLD_HALF_EXTENT);

            return (x, y);
        }
    }
}
