using MemoryPack;
using MMORPG.Shared.World.Movement;

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
    public sealed partial class WorldConfig
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
        /// <summary>Bản tick của coyote / jump buffer / drop-through — tính lại mỗi lần đọc, đủ rẻ vì mỗi tick chỉ đọc vài lần.</summary>
        /// <summary>Bản tick của ba giá trị trên — tính lại mỗi lần đọc, đủ rẻ vì mỗi tick chỉ đọc vài lần.</summary>
        public int CoyoteTicks => MovementRules.ToTicks(CoyoteSeconds);
        public int JumpBufferTicks => MovementRules.ToTicks(JumpBufferSeconds);
        public int DropThroughTicks => MovementRules.ToTicks(DropThroughSeconds);
    }
}
