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
        /// Giá trị "hết hạn" cho hai bộ đếm trên — lớn hơn mọi ngưỡng nên điều kiện nhảy luôn sai.
        /// Cũng là trần kẹp để bộ đếm không tăng tới tràn int khi người chơi đứng yên lâu.
        /// </summary>
        public const int EXPIRED = 999;

        /// <summary>
        /// Lùi vào trong một chút khi quét mép thân. Cần vì đứng trên sàn thì chân nằm ĐÚNG đường
        /// biên hai ô, mà Floor đưa đường biên về ô PHÍA TRÊN — tức ô trống. Quét đúng ở cao độ
        /// chân thì tick nào cũng kết luận "không có gì dưới chân" và Grounded nhấp nháy 20 lần/giây.
        /// </summary>
        private const float EDGE = 0.01f;

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
        public static MoveState Step(MoveState state, MoveIntent intent, float dt,
            WorldConfig world, CharacterConfig config, MapGrid map)
        {
            // 0. Nhịp của tầng action, thêm bộ đếm rơi xuyên.
            if (state.ActionTicksLeft > 0)
                state.ActionTicksLeft--;

            if (state.TicksSinceAttack < EXPIRED)
                state.TicksSinceAttack++;

            if (state.DropThroughTicks > 0)
                state.DropThroughTicks--;

            if (state.ActionTicksLeft <= 0 && state.Action != ActionState.Die)
                state.Action = ActionState.None;

            bool locked = config.GetAction(state.Action).LocksMovement;

            // 1. Tư thế. Muốn ngồi thì ngồi được ngay; muốn ĐỨNG DẬY thì còn phải hỏi thế giới —
            //    trần thấp thì không đứng lên được, và giữ nguyên tư thế ngồi là câu trả lời đúng.
            //    Bỏ phép hỏi này thì thân nở ra bên trong trần và tick sau bị đẩy đi đâu không biết.
            bool wantCrouch = intent.Crouch && state.Grounded && !locked;

            if (wantCrouch)
            {
                state.Crouching = true;
            }
            else if (state.Crouching)
            {
                state.Crouching = !CanStandUp(map, config, state);
            }

            // 2. Vận tốc ngang + hướng mặt (như Phase 9).
            if (locked || state.Crouching)
            {
                state.VelX = 0f;
            }
            else
            {
                state.VelX = intent.DirX * config.MoveSpeed;
            }

            // Hướng mặt: chỉ đổi khi đang thật sự dịch chuyển VÀ không vướng hành động nào.
            // Đứng yên thì giữ hướng cũ (đó là lý do FacingLeft phải là trạng thái, không phải suy ra).
            // Khoá hướng trong lúc hành động: vung tay mà xoay được người thì đòn đánh quét cả hai bên.
            if (state.VelX != 0f && state.Action == ActionState.None)
                state.FacingLeft = state.VelX < 0f;

            // 3. Trọng lực — luật của thế giới, không theo nhân vật.
            state.VelY -= world.Gravity * dt;
            if (state.VelY < -world.MaxFallSpeed)
                state.VelY = -world.MaxFallSpeed;

            // 4a. Hai bộ đếm tha thứ (như Phase 9).
            if (state.TicksSinceGrounded < EXPIRED)
                state.TicksSinceGrounded++;

            if (intent.Jump)
                state.TicksSinceJumpRequest = 0;
            else if (state.TicksSinceJumpRequest < EXPIRED)
                state.TicksSinceJumpRequest++;

            // 4b. Rơi xuyên bệ — xử lý TRƯỚC cú nhảy và tiêu thụ luôn yêu cầu nhảy, nếu không thì
            //     người chơi vừa tụt xuống vừa bật lên trong cùng một tick. Đặt cả TicksSinceGrounded
            //     về EXPIRED để coyote time không cho một cú nhảy giữa không trung ngay tick sau.
            if (!locked && intent.Crouch && intent.Jump && state.Grounded &&
                StandingOnOneWay(map, config, state))
            {
                state.DropThroughTicks = world.DropThroughTicks;
                state.TicksSinceJumpRequest = EXPIRED;
                state.TicksSinceGrounded = EXPIRED;
                state.Grounded = false;
            }
            // 4c. Nhảy (như Phase 9).
            else if (!locked &&
                     state.TicksSinceJumpRequest <= world.JumpBufferTicks &&
                     state.TicksSinceGrounded <= world.CoyoteTicks)
            {
                state.VelY = config.JumpSpeed;
                state.TicksSinceJumpRequest = EXPIRED;
                state.TicksSinceGrounded = EXPIRED;
            }

            // 5. Xin hành động (như Phase 9).
            ActionData attack = config.GetAction(ActionState.Attack);

            if (intent.Action == ActionRequest.Attack &&
                state.TicksSinceAttack >= attack.CooldownTicks &&
                CharacterStates.CanEnter(state.Action, state.ActionTicksLeft, ActionState.Attack))
            {
                state.Action = ActionState.Attack;
                state.ActionTicksLeft = attack.DurationTicks;
                state.TicksSinceAttack = 0;
            }

            // 6 & 7. Tích phân có va chạm. Thứ tự X trước Y là một phần của contract.
            state = ResolveHorizontal(map, config, state, dt);
            state = ResolveVertical(map, config, state, dt);

            // 8. Biên ngang — hai mép map, đọc từ file chứ không phải hằng số trong code.
            state.X = ClampX(map, config, state.X);

            return state;
        }

        private static float BodyHeight(CharacterConfig config, bool crouching)
        {
            return crouching ? config.BodyHeightCrouch : config.BodyHeight;
        }

        private static bool IsSolid(MapGrid map, float x, float y)
        {
            return map.AtWorld(x, y) == CellType.Solid;
        }

        /// <summary>
        /// Thân (đặt tại x, y, cao height) có đè lên ô đặc nào không. Bệ một chiều KHÔNG tính: nó chỉ
        /// chặn theo chiều rơi, còn đứng lọt trong nó là chuyện bình thường.
        ///
        /// Quét 6 điểm = 2 mép ngang × 3 mức cao. Ba mức vì thân cao 1.6 mà ô cao 1.0: hai điểm ở hai
        /// đầu thì ô ở giữa lọt qua khe kiểm. LUẬT: khoảng cách giữa hai mức phải NHỎ HƠN cạnh ô —
        /// với 1.6 thì ba mức cách nhau 0.75, an toàn. Vì chiều cao thân giờ là DỮ LIỆU trong profile,
        /// luật này thành ràng buộc lên dữ liệu: lớp nhân vật nào cao quá 2.0 là phải thêm mức quét.
        /// </summary>
        private static bool OverlapsSolid(MapGrid map, CharacterConfig config, float x, float y, float height)
        {
            float left = x - config.BodyHalfWidth;
            float right = x + config.BodyHalfWidth;

            float footY = y + EDGE;
            float midY = y + height * 0.5f;
            float headY = y + height - EDGE;

            return IsSolid(map, left, footY) || IsSolid(map, right, footY) ||
                   IsSolid(map, left, midY) || IsSolid(map, right, midY) ||
                   IsSolid(map, left, headY) || IsSolid(map, right, headY);
        }

        /// <summary>Có đủ chỗ trống để đứng thẳng dậy tại chỗ đang đứng không.</summary>
        public static bool CanStandUp(MapGrid map, CharacterConfig config, in MoveState state)
        {
            return !OverlapsSolid(map, config, state.X, state.Y, config.BodyHeight);
        }

        /// <summary>Ô ngay dưới chân có phải bệ một chiều không — điều kiện để được chủ động tụt xuống.</summary>
        private static bool StandingOnOneWay(MapGrid map, CharacterConfig config, in MoveState state)
        {
            float probeY = state.Y - EDGE;

            return map.AtWorld(state.X - config.BodyHalfWidth, probeY) == CellType.OneWay
                   || map.AtWorld(state.X + config.BodyHalfWidth, probeY) == CellType.OneWay;
        }

        /// <summary>
        /// Dịch theo trục X rồi dán lại nếu đâm tường. Chỉ kiểm điểm cuối: 5 unit/giây là 0.25 unit
        /// mỗi tick, không cách nào vượt qua một ô rộng 1.0.
        /// </summary>
        private static MoveState ResolveHorizontal(MapGrid map, CharacterConfig config, MoveState state, float dt)
        {
            state.X += state.VelX * dt;

            if (state.VelX == 0f)
                return state;

            float height = BodyHeight(config, state.Crouching);
            float footY = state.Y + EDGE;
            float midY = state.Y + height * 0.5f;
            float headY = state.Y + height - EDGE;

            if (state.VelX > 0f)
            {
                float edgeX = state.X + config.BodyHalfWidth;

                if (IsSolid(map, edgeX, footY) || IsSolid(map, edgeX, midY) || IsSolid(map, edgeX, headY))
                {
                    state.X = MapGrid.ColumnLeft(MapGrid.CellX(edgeX)) - config.BodyHalfWidth;
                    state.VelX = 0f;
                }
            }
            else
            {
                float edgeX = state.X - config.BodyHalfWidth;

                if (IsSolid(map, edgeX, footY) || IsSolid(map, edgeX, midY) || IsSolid(map, edgeX, headY))
                {
                    state.X = MapGrid.ColumnRight(MapGrid.CellX(edgeX)) + config.BodyHalfWidth;
                    state.VelX = 0f;
                }
            }

            return state;
        }

        /// <summary>
        /// Dịch theo trục Y rồi dán lại nếu chạm trần hoặc chạm sàn.
        ///
        /// Chiều xuống là chiều DUY NHẤT phải quét cả quãng đường: rơi kịch trần là 20 unit/giây, tức
        /// đúng 1.00 unit mỗi tick — vừa đủ để lọt qua một tấm bệ dày 1 ô giữa hai lần kiểm. Chiều lên
        /// (0.55 unit/tick) và chiều ngang (0.25) thì kiểm điểm cuối là đủ.
        /// </summary>
        private static MoveState ResolveVertical(MapGrid map, CharacterConfig config, MoveState state, float dt)
        {
            float prevFeetY = state.Y;
            state.Y += state.VelY * dt;

            float height = BodyHeight(config, state.Crouching);

            if (state.VelY > 0f)
            {
                float headY = state.Y + height;

                // Bệ một chiều KHÔNG chặn chiều lên — đó là toàn bộ ý nghĩa của nó.
                if (IsSolid(map, state.X - config.BodyHalfWidth, headY) ||
                    IsSolid(map, state.X + config.BodyHalfWidth, headY))
                {
                    state.Y = MapGrid.RowBottom(MapGrid.CellY(headY)) - height;
                    state.VelY = 0f;
                }

                state.Grounded = false;
                return state;
            }

            // Quét từ hàng ô dưới chân lúc ĐẦU tick xuống tới hàng ô dưới chân lúc CUỐI tick.
            // Quét ở mức "dưới chân một chút" (xem comment của EDGE), không đúng bằng chân.
            int fromRow = MapGrid.CellY(prevFeetY - EDGE);
            int toRow = MapGrid.CellY(state.Y - EDGE);

            for (int row = fromRow; row >= toRow; row--)
            {
                if (!BlocksFall(map, config, state, row, prevFeetY))
                    continue;

                state.Y = MapGrid.RowTop(row);
                state.VelY = 0f;
                state.Grounded = true;
                state.TicksSinceGrounded = 0;

                return state;
            }

            state.Grounded = false;

            return state;
        }

        /// <summary>
        /// Hàng ô <paramref name="row"/> có chặn cú rơi này không.
        /// Ô đặc thì luôn chặn. Bệ một chiều chỉ chặn khi ĐỦ CẢ HAI: chân đã ở trên mặt bệ từ đầu tick
        /// (thiếu điều kiện này thì đi ngang vào cạnh bệ là bị bắn lên mặt bệ), và người chơi không
        /// đang chủ động tụt xuống.
        /// </summary>
        private static bool BlocksFall(MapGrid map, CharacterConfig config, in MoveState state, int row, float prevFeetY)
        {
            int leftCell = MapGrid.CellX(state.X - config.BodyHalfWidth);
            int rightCell = MapGrid.CellX(state.X + config.BodyHalfWidth);

            for (int cx = leftCell; cx <= rightCell; cx++)
            {
                CellType cell = map.At(cx, row);

                if (cell == CellType.Solid)
                    return true;

                if (cell == CellType.OneWay &&
                    state.DropThroughTicks <= 0 &&
                    prevFeetY >= MapGrid.RowTop(row))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Kẹp vào biên ngang của map. Biên là DỮ LIỆU đọc từ file, không còn là hằng số.</summary>
        public static float ClampX(MapGrid map, CharacterConfig config, float x)
        {
            return Math.Clamp(x, map.MinX + config.BodyHalfWidth, map.MaxX - config.BodyHalfWidth);
        }

        /// <summary>
        /// Đẩy một điểm spawn lên chỗ đứng được gần nhất. Cần vì vị trí người chơi đã LƯU trong DB còn
        /// hình dạng map thì sửa được bất cứ lúc nào: mỗi lần bạn vẽ thêm một bức tường là một lần có
        /// ai đó đang offline ở đúng chỗ ấy.
        /// </summary>
        public static float ResolveSpawnY(MapGrid map, CharacterConfig config, float x, float y)
        {
            // Trần lặp = chiều cao map: tường dày mấy hàng cũng thoát ra được, mà không có đường nào
            // để vòng lặp này chạy mãi nếu một ngày nào đó At() đổi cách trả lời.
            for (int guard = 0; guard < map.Height + 1; guard++)
            {
                if (!OverlapsSolid(map, config, x, y, config.BodyHeight))
                    return y;

                // Nhảy lên mặt trên của hàng ô đang kẹt rồi thử lại.
                y = MapGrid.RowTop(MapGrid.CellY(y));
            }

            return y;
        }
    }
}
