namespace MMORPG.Shared.World
{
    /// <summary>
    /// Tư thế thân thể — SUY RA từ trạng thái vật lý, không đi trên dây và không ai "quyết" nó cả.
    /// </summary>
    public enum LocomotionState : byte
    {
        Idle = 0,
        Walk = 1,
        Jump = 2,
        Fall = 3,
        Crouch = 4,
    }

    /// <summary>
    /// Hành động mà CLIENT được phép xin. Cố tình không có Hurt/Die: hai trạng thái đó sinh ra từ
    /// sự kiện chỉ server thấy, nên chúng phải nằm ngoài tầm diễn đạt của một gói tin gửi lên.
    /// Chặn bằng kiểu dữ liệu chứ không bằng câu lệnh — câu lệnh chỉ được kiểm bởi trí nhớ của
    /// người sửa code tiếp theo, còn kiểu thì được kiểm mỗi lần build.
    /// </summary>
    public enum ActionRequest : byte
    {
        None = 0,
        Attack = 1,
    }

    /// <summary>
    /// Hành động một entity CÓ THỂ đang ở. Tập này rộng hơn <see cref="ActionRequest"/> đúng ở phần
    /// mà chỉ server có quyền đặt.
    ///
    /// Giá trị số cũng chính là ĐỘ ƯU TIÊN: lớn hơn thì cắt ngang được cái nhỏ hơn.
    /// </summary>
    public enum ActionState : byte
    {
        None = 0,
        Attack = 1,
        Hurt = 2,
        Die = 3,
    }

    /// <summary>Luật của tầng trạng thái: suy tư thế, và ai được cắt ngang ai.</summary>
    public static class CharacterStates
    {
        /// <summary>
        /// Tư thế thân thể tại một trạng thái vật lý. Hàm THUẦN, và thứ tự kiểm là một phần của
        /// định nghĩa: trên không thắng ngồi, ngồi thắng chạy. Đang bay mà bấm ngồi thì vẫn là Jump.
        ///
        /// Server không gọi hàm này lần nào — server không vẽ gì cả. Nó nằm ở đây vì đây là nơi
        /// duy nhất định nghĩa "MoveState này nghĩa là tư thế gì", và định nghĩa phải ở cạnh dữ
        /// liệu nó đọc: thêm một field vào MoveState là thấy ngay có phải sửa chỗ này không.
        /// </summary>
        public static LocomotionState Derive(in MoveState state)
        {
            if (!state.Grounded)
            {
                // Mốc 0 chứ không phải chia theo dấu vận tốc lúc bấm nhảy: đúng đỉnh parabol VelY
                // đi qua 0, và ở đó gọi là Fall hợp lý hơn Jump — thân đã ngừng bốc lên.
                return state.VelY > 0f ? LocomotionState.Jump : LocomotionState.Fall;
            }

            if (state.Crouching)
                return LocomotionState.Crouch;

            return state.VelX != 0f ? LocomotionState.Walk : LocomotionState.Idle;
        }

        /// <summary>
        /// Có được chuyển sang <paramref name="next"/> không. Toàn bộ ma trận 4×4 gói trong một
        /// dòng luật: ưu tiên cao hơn thì cắt ngang được, bằng hoặc thấp hơn thì phải chờ hết
        /// thời lượng. Viết 16 nhánh cho 16 ô là cách chắc chắn để sai một ô mà không ai biết.
        /// </summary>
        public static bool CanEnter(ActionState current, int ticksLeft, ActionState next)
        {
            // Chết là hết. Không có đường ra khỏi Die bằng luật; hồi sinh là lệnh riêng của server
            // đặt thẳng trạng thái, không đi qua hàm này.
            if (current == ActionState.Die)
                return false;

            if (next > current)
                return true;

            return ticksLeft <= 0;
        }
    }
}