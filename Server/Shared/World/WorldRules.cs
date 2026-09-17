using MemoryPack;

namespace MMORPG.Shared.World
{
    /// <summary>
    /// Luật thế giới ở ĐƠN VỊ CỦA NGƯỜI VIẾT SỐ: giây, không phải tick. Vừa là bản đối chiếu 1-1 với
    /// khối "World" trong game.json, vừa là thứ đi trên dây xuống client — hai vai mà cùng một hình
    /// dạng, nên một kiểu là đủ.
    ///
    /// Mọi property có giá trị mặc định HỢP LỆ: file thiếu trường nào thì trường đó về mặc định thay
    /// vì làm cả server đứng.
    /// </summary>
    [MemoryPackable]
    public sealed partial class WorldRulesData
    {
        /// <summary>Gia tốc rơi, unit/giây². Lớn hơn 9.81 rất nhiều — trọng lực "đúng vật lý" cho cảm giác lơ lửng.</summary>
        public float Gravity { get; set; } = 30f;

        /// <summary>Trần tốc độ rơi. Trần này là ĐIỀU KIỆN ĐÚNG của phép quét va chạm, không phải sở thích.</summary>
        public float MaxFallSpeed { get; set; } = 20f;

        /// <summary>Còn được nhảy bao lâu sau khi đã rời mép sàn (coyote time).</summary>
        public float CoyoteSeconds { get; set; } = 0.15f;

        /// <summary>Một cú bấm nhảy còn được giữ lại chờ tiếp đất bao lâu (jump buffer).</summary>
        public float JumpBufferSeconds { get; set; } = 0.15f;

        /// <summary>Bỏ qua va chạm với bệ một chiều bao lâu sau khi bấm ngồi + nhảy.</summary>
        public float DropThroughSeconds { get; set; } = 0.3f;
    }

    /// <summary>
    /// Luật thế giới ở ĐƠN VỊ CỦA MÔ PHỎNG: tick. Dựng một lần từ <see cref="WorldRulesData"/>, và từ
    /// đó về sau vòng mô phỏng chỉ còn làm việc với số nguyên — hai đầu dây không có cửa nào lệch nhau
    /// ở chữ số cuối.
    ///
    /// Phép dựng nằm ở Shared có chủ đích: server dựng từ FILE, client dựng từ GÓI TIN, và cả hai chạy
    /// đúng hàm này. Quy đổi giây→tick mà có hai bản là có hai thế giới.
    /// </summary>
    public sealed class WorldRules
    {
        public float Gravity { get; }
        public float MaxFallSpeed { get; }
        public int CoyoteTicks { get; }
        public int JumpBufferTicks { get; }
        public int DropThroughTicks { get; }

        public WorldRules(WorldRulesData data)
        {
            Gravity = data.Gravity;
            MaxFallSpeed = data.MaxFallSpeed;
            CoyoteTicks = MovementRules.ToTicks(data.CoyoteSeconds);
            JumpBufferTicks = MovementRules.ToTicks(data.JumpBufferSeconds);
            DropThroughTicks = MovementRules.ToTicks(data.DropThroughSeconds);
        }
    }
}
