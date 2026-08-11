using System;
using System.Buffers.Binary;
using System.IO;

namespace MMORPG.Shared.Net
{
    /// <summary>
    /// Gom dòng byte đọc từ socket thành từng gói hoàn chỉnh.
    ///
    /// Mỗi lần đọc được byte từ mạng thì <see cref="Feed"/> vào đây, rồi gọi <see cref="TryRead"/>
    /// trong vòng lặp cho tới khi trả về false — một lần đọc mạng có thể chứa 0, 1 hoặc nhiều gói,
    /// và gói cuối thường bị cắt dở.
    ///
    /// KHÔNG thread-safe: mỗi kết nối một instance, chỉ dùng trong đúng luồng đọc của kết nối đó.
    /// </summary>
    public sealed class FrameReader
    {
        private readonly int _maxPacketSize;
        private byte[] _buffer;

        /// <summary>Số byte hợp lệ đang giữ, tính từ đầu <see cref="_buffer"/>.</summary>
        private int _count;

        public FrameReader(int initialCapacity = 4096, int maxPacketSize = PacketFrame.MAX_PACKET_SIZE)
        {
            _buffer = new byte[initialCapacity];
            _maxPacketSize = maxPacketSize;
        }

        /// <summary>Nạp thêm byte vừa đọc được từ socket.</summary>
        public void Feed(byte[] data, int offset, int length)
        {
            EnsureCapacity(_count + length);
            Buffer.BlockCopy(data, offset, _buffer, _count, length);
            _count += length;
        }

        /// <summary>
        /// Lấy ra một gói hoàn chỉnh nếu có đủ byte.
        /// </summary>
        /// <returns>false nghĩa là "chưa đủ, chờ thêm dữ liệu" — không phải lỗi.</returns>
        /// <exception cref="InvalidDataException">Độ dài đọc được vô lý → dòng byte đã hỏng, phải ngắt kết nối.</exception>
        public bool TryRead(out int cmd, out byte[] payload)
        {
            cmd = 0;
            payload = null;

            // chưa đọc nổi trường độ dài
            if (_count < PacketFrame.LENGTH_FIELD_SIZE)
                return false;

            int bodyLength = BinaryPrimitives.ReadInt32LittleEndian(
                _buffer.AsSpan(0, PacketFrame.LENGTH_FIELD_SIZE));

            // Kiểm tra TRƯỚC khi cấp phát. Không có bước này thì chỉ cần gửi 4 byte 0x7FFFFFFF
            // là ép server cấp phát 2GB — một kiểu tấn công rẻ tiền mà hiệu quả.
            if (bodyLength < PacketFrame.CMD_FIELD_SIZE || bodyLength > _maxPacketSize)
                throw new InvalidDataException($"Độ dài gói không hợp lệ: {bodyLength}");

            int totalLength = PacketFrame.LENGTH_FIELD_SIZE + bodyLength;

            // gói còn dở — chờ lần Feed sau
            if (_count < totalLength)
                return false;

            cmd = BinaryPrimitives.ReadInt32LittleEndian(
                _buffer.AsSpan(PacketFrame.LENGTH_FIELD_SIZE, PacketFrame.CMD_FIELD_SIZE));

            int payloadLength = bodyLength - PacketFrame.CMD_FIELD_SIZE;
            payload = new byte[payloadLength];
            Buffer.BlockCopy(_buffer, PacketFrame.HEADER_SIZE, payload, 0, payloadLength);

            // dồn phần byte của (các) gói sau về đầu buffer
            int remain = _count - totalLength;
            if (remain > 0)
                Buffer.BlockCopy(_buffer, totalLength, _buffer, 0, remain);
            _count = remain;

            return true;
        }

        private void EnsureCapacity(int needed)
        {
            if (_buffer.Length >= needed)
                return;

            int newSize = _buffer.Length;
            while (newSize < needed)
                newSize *= 2;

            Array.Resize(ref _buffer, newSize);
        }
    }
}
