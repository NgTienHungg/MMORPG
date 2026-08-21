using MemoryPack;
using MMORPG.Shared.World;

namespace MMORPG.Shared.Dto.World
{
    /// <summary>
    /// Ý định của client tại một bước dự đoán. Cố tình KHÔNG có trường thời gian:
    /// server tích phân bằng TICK_DT của chính nó — dt mà đi trên gói tin thì dt là chỗ hack tốc độ.
    /// </summary>
    [MemoryPackable]
    public partial class MoveInputRequest
    {
        /// <summary>Số thứ tự client tự đánh, tăng dần. Server echo lại để client biết mình đã được xử tới đâu.</summary>
        public int Seq { get; set; }

        /// <summary>
        /// Đúng cái struct mà <see cref="MovementRules.Step"/> nhận, không phải bản sao dàn phẳng của nó.
        /// Client dự đoán bằng struct này rồi gửi chính nó đi; thêm một nút bấm mới sau này là sửa
        /// một chỗ, không phải bốn.
        /// </summary>
        public MoveIntent Intent { get; set; }
    }

    /// <summary>
    /// Trạng thái authoritative của chính người nhận, gửi mỗi tick. Mang nguyên
    /// <see cref="MoveState"/> chứ không dàn phẳng ra từng trường: client replay các input còn treo
    /// từ đây, mà replay thì cần ĐỦ trạng thái — thiếu một trường là replay ra một quỹ đạo khác.
    /// Dàn phẳng thì mỗi lần <see cref="MoveState"/> mọc thêm trường là một lần có thể quên chép.
    /// </summary>
    [MemoryPackable]
    public partial class MoveStateResponse
    {
        /// <summary>Input cuối cùng server đã nhận trước tick này. Client xoá pending ≤ số này rồi replay phần còn lại.</summary>
        public int LastInputSeq { get; set; }

        /// <summary>Trạng thái vật lý sau tick này, y hệt cái server đang giữ.</summary>
        public MoveState State { get; set; }
    }
}
