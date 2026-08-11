using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using K4os.Compression.LZ4;
using MemoryPack;

namespace MMORPG.Shared.Net
{
    /// <summary>
    /// Chuyển DTO ↔ mảng byte của payload, kèm nén tuỳ chọn.
    ///
    /// <code>
    /// ┌──────────┬─────────────────────────┬───────────────────┐
    /// │ flag 1B  │ rawLen 4B (chỉ khi nén) │ MemoryPack bytes  │
    /// └──────────┴─────────────────────────┴───────────────────┘
    ///   0x00 = nguyên bản · 0x01 = LZ4
    /// </code>
    /// </summary>
    public static class NetPayload
    {
        /// <summary>Dưới ngưỡng này thì không nén — nén gói bé lỗ vốn CPU mà chẳng bớt được byte nào.</summary>
        public const int COMPRESS_THRESHOLD = 4 * 1024;

        private const byte FLAG_RAW = 0x00;
        private const byte FLAG_LZ4 = 0x01;

        private const int FLAG_SIZE = 1;
        private const int RAW_LENGTH_SIZE = 4;
        private const int COMPRESSED_HEADER_SIZE = FLAG_SIZE + RAW_LENGTH_SIZE;

        /// <exception cref="ArgumentNullException">
        /// Gửi null qua dây luôn là bug: bên nhận không có cách nào phân biệt "cố tình gửi rỗng"
        /// với "quên gán DTO". Chặn ngay tại đây thay vì để nó nổ ở handler bên kia.
        /// </exception>
        public static byte[] Serialize<T>(T value) where T : IMemoryPackable<T>
        {
            if (value is null)
                throw new ArgumentNullException(nameof(value));

            byte[] raw = MemoryPackSerializer.Serialize(value);
            return Pack(raw);
        }

        /// <exception cref="InvalidDataException">Payload hỏng, hoặc giải ra null (xem <see cref="Serialize{T}"/>).</exception>
        public static T Deserialize<T>(byte[] payload) where T : IMemoryPackable<T>
        {
            ReadOnlySpan<byte> raw = Unpack(payload, out byte[]? rented);
            try
            {
                return MemoryPackSerializer.Deserialize<T>(raw)
                       ?? throw new InvalidDataException($"Payload giải ra null cho {typeof(T).Name}.");
            }
            finally
            {
                if (rented != null)
                    ArrayPool<byte>.Shared.Return(rented);
            }
        }

        private static byte[] Pack(ReadOnlySpan<byte> raw)
        {
            if (raw.Length <= COMPRESS_THRESHOLD)
            {
                byte[] plain = new byte[FLAG_SIZE + raw.Length];
                plain[0] = FLAG_RAW;
                raw.CopyTo(plain.AsSpan(FLAG_SIZE));
                return plain;
            }

            int maxSize = LZ4Codec.MaximumOutputSize(raw.Length);
            byte[] temp = ArrayPool<byte>.Shared.Rent(maxSize);

            try
            {
                int written = LZ4Codec.Encode(raw, temp.AsSpan(0, maxSize), LZ4Level.L00_FAST);

                int compressedSize = COMPRESSED_HEADER_SIZE + written;
                int plainSize = FLAG_SIZE + raw.Length;

                // Nén xong mà không nhỏ hơn thì dùng bản gốc — dữ liệu ngẫu nhiên/đã nén sẵn
                // hoàn toàn có thể phình ra sau khi nén.
                if (compressedSize >= plainSize)
                {
                    byte[] plain = new byte[plainSize];
                    plain[0] = FLAG_RAW;
                    raw.CopyTo(plain.AsSpan(FLAG_SIZE));
                    return plain;
                }

                byte[] result = new byte[compressedSize];
                result[0] = FLAG_LZ4;
                BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(FLAG_SIZE, RAW_LENGTH_SIZE), raw.Length);
                temp.AsSpan(0, written).CopyTo(result.AsSpan(COMPRESSED_HEADER_SIZE));
                return result;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(temp);
            }
        }

        /// <param name="payload">Byte thô lấy từ khung gói tin, tính cả byte flag đứng đầu.</param>
        /// <param name="rented">Khác null nghĩa là buffer đi mượn — bắt buộc trả lại ArrayPool sau khi dùng.</param>
        private static ReadOnlySpan<byte> Unpack(byte[] payload, out byte[]? rented)
        {
            rented = null;

            if (payload == null || payload.Length < FLAG_SIZE)
                throw new InvalidDataException("Payload rỗng, không có cả flag.");

            byte flag = payload[0];

            if (flag == FLAG_RAW)
                return payload.AsSpan(FLAG_SIZE);

            if (flag != FLAG_LZ4)
                throw new InvalidDataException($"Flag payload lạ: 0x{flag:X2}");

            if (payload.Length < COMPRESSED_HEADER_SIZE)
                throw new InvalidDataException("Payload báo có nén nhưng thiếu trường độ dài gốc.");

            int rawLength = BinaryPrimitives.ReadInt32LittleEndian(
                payload.AsSpan(FLAG_SIZE, RAW_LENGTH_SIZE));

            if (rawLength < 0 || rawLength > PacketFrame.MAX_PACKET_SIZE)
                throw new InvalidDataException($"Độ dài sau giải nén vô lý: {rawLength}");

            rented = ArrayPool<byte>.Shared.Rent(rawLength);
            int decoded = LZ4Codec.Decode(payload.AsSpan(COMPRESSED_HEADER_SIZE), rented.AsSpan());

            if (decoded != rawLength)
                throw new InvalidDataException($"Giải nén ra {decoded} byte, khai báo {rawLength}.");

            return rented.AsSpan(0, rawLength);
        }
    }
}
