using MMORPG.Shared.World.Map;
using MMORPG.Shared.World.Movement;

namespace MMORPG.GameServer.Config
{
    /// <summary>
    /// Trần và sàn của các con số trong file config — <b>không</b> phải giá trị mặc định, mà là ranh
    /// giới ngoài đó thì thuật toán va chạm không còn đúng.
    ///
    /// Mỗi hằng ở đây dẫn xuất từ một tính chất của <see cref="MovementRules"/> hoặc
    /// <see cref="MapGrid"/>, nên nó là hằng số của THUẬT TOÁN và ở lại trong code: người cân bằng
    /// game không có lý do nào để nới trần này, và nới nó thì nhân vật xuyên sàn.
    /// </summary>
    public static class ConfigLimits
    {
        /// <summary>
        /// Trần tuyệt đối cho mọi vận tốc. Đi quá một ô trong một tick là vượt qua giả định của phép
        /// kiểm va chạm: ngang và lên chỉ kiểm ĐIỂM CUỐI, xuống thì quét từng hàng ô nhưng chỉ bảo
        /// đảm trong phạm vi một ô mỗi tick.
        /// </summary>
        public const float SPEED_CAP = MapGrid.CELL_SIZE / MovementRules.TICK_DT;

        /// <summary>
        /// Nửa bề ngang thân phải HẸP HƠN nửa ô, không bằng: rộng đúng nửa ô là vừa khít khe 1 ô, và
        /// "vừa khít" trong số thực dấu phẩy động nghĩa là lúc lọt lúc không.
        /// </summary>
        public const float BODY_HALF_WIDTH_CAP = MapGrid.CELL_SIZE * 0.5f - 0.001f;

        /// <summary>
        /// Trần chiều cao thân khi đứng. Đến từ <c>OverlapsSolid</c>: nó quét ba mức cao, và ba mức
        /// chỉ phủ kín khi khoảng cách giữa hai mức nhỏ hơn cạnh ô. Cao hơn là có ô lọt qua khe kiểm.
        /// </summary>
        public const float BODY_HEIGHT_CAP = MapGrid.CELL_SIZE * 2f;

        /// <summary>Chiều cao khi ngồi không được vượt một ô — nếu không thì ngồi cũng không chui được vào khe cao 1 ô.</summary>
        public const float CROUCH_HEIGHT_CAP = MapGrid.CELL_SIZE;
    }
}
