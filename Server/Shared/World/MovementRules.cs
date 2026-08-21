using System;

namespace MMORPG.Shared.World
{
    /// <summary>
    /// Luật di chuyển dùng CHUNG: server mô phỏng thật, client dự đoán trước rồi replay.
    /// Hai bên phải ra cùng một kết quả từ cùng input — vì vậy luật chỉ tồn tại ở đây, một nơi.
    ///
    /// Mọi phép tính trong file này chỉ được dùng + - * / và so sánh trên float. Bốn phép đó được
    /// IEEE-754 quy định tới từng bit nên CoreCLR (server) và Mono/IL2CPP (client) buộc phải cho
    /// cùng kết quả. Gọi MathF.Sin/Pow/Exp là mở cửa cho sai số nền tảng, và sai số ấy cộng dồn
    /// suốt cú nhảy chứ không tự triệt tiêu.
    /// </summary>
    public static class MovementRules
    {
        public const int TICK_RATE = 20;
        public const float TICK_DT = 1f / TICK_RATE;

        /// <summary>Tốc độ chạy ngang, world unit/giây.</summary>
        public const float MOVE_SPEED = 5f;

        /// <summary>
        /// Gia tốc rơi, unit/giây². Lớn hơn 9.81 của đời thật rất nhiều — trọng lực "đúng vật lý"
        /// cho cảm giác lơ lửng như trên mặt trăng, không game platformer nào dùng.
        /// </summary>
        public const float GRAVITY = 30f;

        /// <summary>
        /// Vận tốc bật lên tức thời khi nhảy. Đỉnh nhảy = JUMP_SPEED² / (2·GRAVITY) ≈ 2 unit,
        /// thời gian bay ≈ 2·JUMP_SPEED/GRAVITY ≈ 0.73s ≈ 15 tick — đủ dài để nội suy phía
        /// người xem có mẫu mà vẽ đường cong.
        /// </summary>
        public const float JUMP_SPEED = 11f;

        /// <summary>
        /// Trần tốc độ rơi. Không có nó, rơi từ trên cao đủ lâu sẽ đi hơn một ô mỗi tick và
        /// XUYÊN QUA sàn giữa hai lần kiểm va chạm.
        /// </summary>
        public const float MAX_FALL_SPEED = 20f;

        /// <summary>Cao độ mặt sàn tạm — cả thế giới là một mặt phẳng. Map có hình dạng thật là Phase 10.</summary>
        public const float GROUND_Y = 0f;

        /// <summary>Nửa cạnh vùng đi lại theo trục ngang. Trục dọc không còn bị kẹp: đã có sàn và trọng lực.</summary>
        public const float WORLD_HALF_EXTENT = 20f;

        /// <summary>
        /// Số tick còn được nhảy sau khi đã rời mép sàn (coyote time). 3 tick = 150ms: đủ để tha thứ
        /// cho phản xạ người, chưa đủ để thành "nhảy giữa không trung".
        /// </summary>
        public const int COYOTE_TICKS = 3;

        /// <summary>Số tick một cú bấm nhảy còn được giữ lại chờ tiếp đất (jump buffer).</summary>
        public const int JUMP_BUFFER_TICKS = 3;

        /// <summary>
        /// Giá trị "hết hạn" cho hai bộ đếm trên — lớn hơn mọi ngưỡng nên điều kiện nhảy luôn sai.
        /// Cũng là trần kẹp để bộ đếm không tăng tới tràn int khi người chơi đứng yên lâu.
        /// </summary>
        public const int EXPIRED = 999;

        /// <summary>
        /// Một bước mô phỏng. Hàm THUẦN: không đọc thời gian, không random, không đọc biến ngoài —
        /// cùng (state, intent, dt) luôn cho cùng kết quả, ở cả hai đầu dây.
        ///
        /// THỨ TỰ các phép dưới đây là một phần của contract. Đổi thứ tự là đổi kết quả, và vì
        /// hai bên chạy cùng file nên nó sẽ không lệch ngay — nó lệch vào ngày ai đó sửa một bên.
        /// </summary>
        public static MoveState Step(MoveState state, MoveIntent intent, float dt)
        {
            // 1. Vận tốc ngang.
            state.VelX = intent.DirX * MOVE_SPEED;

            // 2. Trọng lực.
            state.VelY -= GRAVITY * dt;
            if (state.VelY < -MAX_FALL_SPEED)
                state.VelY = -MAX_FALL_SPEED;

            // 3a. Hai bộ đếm tha thứ. Kẹp ở EXPIRED để không tăng tới tràn int.
            if (state.TicksSinceGrounded < EXPIRED)
                state.TicksSinceGrounded++;

            if (intent.Jump)
                state.TicksSinceJumpRequest = 0;
            else if (state.TicksSinceJumpRequest < EXPIRED)
                state.TicksSinceJumpRequest++;

            // 3b. Điều kiện nhảy. Lưu ý KHÔNG còn kiểm state.Grounded: đang đứng đất nghĩa là
            //     TicksSinceGrounded == 0, đã nằm trong ngưỡng coyote rồi. Kiểm thêm Grounded
            //     là vô hiệu hoá đúng cái tính năng vừa thêm.
            if (state.TicksSinceJumpRequest <= JUMP_BUFFER_TICKS &&
                state.TicksSinceGrounded <= COYOTE_TICKS)
            {
                // 3c. Bật lên, và tiêu huỷ CẢ HAI tư cách. Chỉ xoá buffer thì coyote còn hiệu lực ở
                //     tick sau → nhảy đôi miễn phí. Chỉ xoá coyote thì buffer còn → vừa chạm đất là
                //     tự nhảy lại, mãi mãi.
                state.VelY = JUMP_SPEED;
                state.TicksSinceJumpRequest = EXPIRED;
                state.TicksSinceGrounded = EXPIRED;
            }

            // 4. Tích phân.
            state.X += state.VelX * dt;
            state.Y += state.VelY * dt;

            // 5. Va chạm sàn.
            if (state.Y <= GROUND_Y)
            {
                state.Y = GROUND_Y;
                state.VelY = 0f;
                state.Grounded = true;
                state.TicksSinceGrounded = 0;
            }
            else
            {
                state.Grounded = false;
            }

            // 6. Biên ngang tạm, chờ map thật ở Phase 10.
            state.X = Math.Clamp(state.X, -WORLD_HALF_EXTENT, WORLD_HALF_EXTENT);

            return state;
        }
    }
}
