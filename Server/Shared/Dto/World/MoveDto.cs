using MemoryPack;

namespace MMORPG.Shared.Dto.World
{
    /// <summary>
    /// Trạng thái phím đang bấm tại một bước dự đoán của client. Cố tình KHÔNG có trường thời gian:
    /// server tích phân bằng TICK_DT của chính nó — dt mà đi trên gói tin thì dt là chỗ hack tốc độ.
    /// </summary>
    [MemoryPackable]
    public partial class MoveInputRequest
    {
        /// <summary>Số thứ tự client tự đánh, tăng dần. Server echo lại để client biết mình đã được xử tới đâu.</summary>
        public int Seq { get; set; }

        public float DirX { get; set; }
        public float DirY { get; set; }
    }

    [MemoryPackable]
    public partial class MoveStateResponse
    {
        /// <summary>Input cuối cùng server đã nhận trước tick này. Client xoá pending ≤ số này rồi replay phần còn lại.</summary>
        public int LastInputSeq { get; set; }

        public float X { get; set; }
        public float Y { get; set; }
    }
}
