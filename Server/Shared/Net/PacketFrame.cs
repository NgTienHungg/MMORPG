using System;
using System.Buffers.Binary;

namespace MMORPG.Shared.Net
{
    /// <summary>
    /// Định nghĩa khung gói tin trên dây. Client và server dùng chung file này,
    /// nên không có cách nào để hai bên hiểu khác nhau về định dạng.
    ///
    /// <code>
    /// ┌─────────────┬─────────────┬────────────────────────┐
    /// │ int32 len   │ int32 cmd   │ payload (len - 4 byte) │
    /// └─────────────┴─────────────┴────────────────────────┘
    ///   len = 4 + payload.Length          little-endian
    /// </code>
    /// </summary>
    public static class PacketFrame
    {
        /// <summary>Số byte của trường độ dài đứng đầu khung.</summary>
        public const int LENGTH_FIELD_SIZE = 4;

        /// <summary>Số byte của mã lệnh.</summary>
        public const int CMD_FIELD_SIZE = 4;

        /// <summary>Tổng phần header: độ dài + mã lệnh.</summary>
        public const int HEADER_SIZE = LENGTH_FIELD_SIZE + CMD_FIELD_SIZE;

        /// <summary>
        /// Chặn trên cho 1 gói. Gói lớn hơn mức này coi như dữ liệu hỏng hoặc bị tấn công —
        /// không cấp phát buffer theo số đọc được từ mạng mà chưa kiểm tra.
        /// </summary>
        public const int MAX_PACKET_SIZE = 1024 * 1024;

        /// <summary>
        /// Đóng khung một gói hoàn chỉnh, sẵn sàng ghi thẳng lên socket.
        /// </summary>
        public static byte[] Encode(int cmd, ReadOnlySpan<byte> payload)
        {
            if (payload.Length + CMD_FIELD_SIZE > MAX_PACKET_SIZE)
                throw new ArgumentException($"Payload {payload.Length} byte vượt giới hạn {MAX_PACKET_SIZE}");

            byte[] frame = new byte[HEADER_SIZE + payload.Length];

            BinaryPrimitives.WriteInt32LittleEndian(
                frame.AsSpan(0, LENGTH_FIELD_SIZE), CMD_FIELD_SIZE + payload.Length);
            BinaryPrimitives.WriteInt32LittleEndian(
                frame.AsSpan(LENGTH_FIELD_SIZE, CMD_FIELD_SIZE), cmd);

            payload.CopyTo(frame.AsSpan(HEADER_SIZE));
            return frame;
        }
    }
}
