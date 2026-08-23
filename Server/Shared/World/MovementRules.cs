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

        /// <summary>
        /// Gia tốc rơi, unit/giây². Lớn hơn 9.81 của đời thật rất nhiều — trọng lực "đúng vật lý"
        /// cho cảm giác lơ lửng như trên mặt trăng, không game platformer nào dùng.
        /// </summary>
        public const float GRAVITY = 30f;

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
        /// Quy giây ra tick. Chạy MỘT LẦN lúc dựng bảng, không nằm trong Step — nhờ vậy vòng mô phỏng
        /// chỉ còn làm việc với số nguyên, và hai đầu dây không có cửa nào để lệch nhau ở chữ số cuối.
        ///
        /// Làm tròn LÊN và có sàn 1: một hành động 10ms mà quy ra 0 tick thì nó không tồn tại — phép 0
        /// của Step sẽ xoá nó ngay tick sau. Hệ quả phải nói trước với người viết số: thời lượng bị
        /// lượng tử hoá theo 50ms, viết 0.23s hay 0.25s đều ra 5 tick.
        /// </summary>
        public static int ToTicks(float seconds)
        {
            if (seconds <= 0f)
                return 0;

            int ticks = (int)MathF.Ceiling(seconds * TICK_RATE);

            return ticks < 1 ? 1 : ticks;
        }

        /// <summary>
        /// Một bước mô phỏng. Hàm THUẦN: không đọc thời gian, không random, không đọc biến ngoài —
        /// cùng (state, intent, dt, profile) luôn cho cùng kết quả, ở cả hai đầu dây.
        ///
        /// profile là bộ số của CHÍNH nhân vật đang được mô phỏng. Nó vào bằng tham số chứ không nằm
        /// trong MoveState: MoveState đi trên dây mỗi tick, còn profile thì hai bên tra được từ ClassId
        /// — gửi kèm là trả tiền băng thông 20 lần mỗi giây cho một thứ không bao giờ đổi.
        ///
        /// THỨ TỰ các phép dưới đây là một phần của contract. Đổi thứ tự là đổi kết quả, và vì
        /// hai bên chạy cùng file nên nó sẽ không lệch ngay — nó lệch vào ngày ai đó sửa một bên.
        /// </summary>
        public static MoveState Step(MoveState state, MoveIntent intent, float dt, CharacterProfile profile)
        {
            // 0. Nhịp của tầng action. PHẢI chạy trước phép 5: chạy sau thì đòn vừa bắt đầu ở tick
            //    này bị trừ mất một tick ngay khi chưa kịp diễn.
            if (state.ActionTicksLeft > 0)
                state.ActionTicksLeft--;

            if (state.TicksSinceAttack < EXPIRED)
                state.TicksSinceAttack++;

            // Hết thời lượng thì về None — TRỪ Die. Chết rồi thì hết ticks là hết hoạt ảnh, không
            // phải hết trạng thái; bỏ nhánh loại trừ này là xác chết đứng dậy đi tiếp sau một giây.
            if (state.ActionTicksLeft <= 0 && state.Action != ActionState.Die)
                state.Action = ActionState.None;

            // Tra bảng SAU phép 0, vì phép 0 vừa có thể đưa Action về None. Tra trước thì cả tick này
            // thân thể còn bị khoá theo một hành động đã hết hạn — trễ một nhịp, đủ để thấy "đơ".
            bool locked = profile.GetAction(state.Action).LocksMovement;

            // 1. Tư thế. Ngồi chỉ có nghĩa khi chân chạm đất và thân thể còn nghe lời.
            state.Crouching = intent.Crouch && state.Grounded && !locked;

            // 2. Vận tốc ngang. Ăn đòn / gục thì mất quyền điều khiển; ngồi thì đứng yên tại chỗ.
            if (locked || state.Crouching)
            {
                state.VelX = 0f;
            }
            else
            {
                state.VelX = intent.DirX * profile.MoveSpeed;
            }

            // Hướng mặt: chỉ đổi khi đang thật sự dịch chuyển VÀ không vướng hành động nào.
            // Đứng yên thì giữ hướng cũ (đó là lý do FacingLeft phải là trạng thái, không phải suy ra).
            // Khoá hướng trong lúc hành động: vung tay mà xoay được người thì đòn đánh quét cả hai bên.
            if (state.VelX != 0f && state.Action == ActionState.None)
                state.FacingLeft = state.VelX < 0f;

            // 3. Trọng lực — luật của thế giới, không theo nhân vật.
            state.VelY -= GRAVITY * dt;
            if (state.VelY < -MAX_FALL_SPEED)
                state.VelY = -MAX_FALL_SPEED;

            // 4a. Hai bộ đếm tha thứ.
            if (state.TicksSinceGrounded < EXPIRED)
                state.TicksSinceGrounded++;

            if (intent.Jump)
                state.TicksSinceJumpRequest = 0;
            else if (state.TicksSinceJumpRequest < EXPIRED)
                state.TicksSinceJumpRequest++;

            // 4b/4c. Nhảy — thêm điều kiện thân thể còn nghe lời.
            if (!locked &&
                state.TicksSinceJumpRequest <= JUMP_BUFFER_TICKS &&
                state.TicksSinceGrounded <= COYOTE_TICKS)
            {
                state.VelY = profile.JumpSpeed;
                state.TicksSinceJumpRequest = EXPIRED;
                state.TicksSinceGrounded = EXPIRED;
            }

            // 5. Xin hành động. HAI điều kiện chặn khác nhau, cố tình không gộp:
            //    CanEnter  = "trạng thái hiện tại có cho phép không" (đang choáng thì không)
            //    cooldown  = "nhịp đánh đã tới chưa"
            //    Gộp vào một số là mất khả năng diễn đạt "hết cooldown rồi nhưng đang choáng nên vẫn cấm".
            ActionDefinition attack = profile.GetAction(ActionState.Attack);

            if (intent.Action == ActionRequest.Attack &&
                state.TicksSinceAttack >= attack.CooldownTicks &&
                CharacterStates.CanEnter(state.Action, state.ActionTicksLeft, ActionState.Attack))
            {
                state.Action = ActionState.Attack;
                state.ActionTicksLeft = attack.DurationTicks;
                state.TicksSinceAttack = 0;
            }

            // 6. Tích phân.
            state.X += state.VelX * dt;
            state.Y += state.VelY * dt;

            // 7. Va chạm với sàn phẳng.
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

            // 8. Biên ngang tạm (hoặc hai hằng WORLD_MIN_X / WORLD_MAX_X nếu bạn chọn cách thứ
            //    hai ở Bước 0). Cả hai đều biến mất ở Phase 10 khi map có tường thật.
            state.X = Math.Clamp(state.X, -WORLD_HALF_EXTENT, WORLD_HALF_EXTENT);

            return state;
        }
    }
}
