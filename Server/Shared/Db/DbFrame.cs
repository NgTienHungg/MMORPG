using System;
using System.Buffers.Binary;
using System.IO;
using MMORPG.Shared.Net;

namespace MMORPG.Shared.Db
{
    /// <summary>
    /// Đường nội bộ dùng lại nguyên <see cref="PacketFrame"/> và <see cref="FrameReader"/> của đường client —
    /// chỉ quy ước thêm rằng payload bắt đầu bằng một request id.
    ///
    /// <code>
    /// ┌───────────┬───────────┬──────────────┬─────────────────────────┐
    /// │ int32 len │ int32 cmd │ int32 reqId  │ payload NetPayload      │
    /// └───────────┴───────────┴──────────────┴─────────────────────────┘
    ///  └── PacketFrame lo phần này ──┘└── DbFrame lo phần này ──┘
    /// </code>
    ///
    /// Đây là ví dụ của việc xếp tầng protocol: tầng khung (đếm byte) không cần biết
    /// tầng trên đang nói chuyện kiểu gì, nên hai protocol khác nhau vẫn dùng chung một bộ đọc khung.
    /// </summary>
    public static class DbFrame
    {
        public const int REQUEST_ID_SIZE = 4;

        /// <summary>Đóng khung hoàn chỉnh, ghi thẳng lên socket được.</summary>
        public static byte[] Encode(DbCmd cmd, int requestId, ReadOnlySpan<byte> payload)
        {
            byte[] body = new byte[REQUEST_ID_SIZE + payload.Length];
            BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(0, REQUEST_ID_SIZE), requestId);
            payload.CopyTo(body.AsSpan(REQUEST_ID_SIZE));

            return PacketFrame.Encode((int)cmd, body);
        }

        /// <summary>Tách request id ra khỏi payload mà <see cref="FrameReader.TryRead"/> vừa trả về.</summary>
        public static byte[] Decode(byte[] framePayload, out int requestId)
        {
            if (framePayload == null || framePayload.Length < REQUEST_ID_SIZE)
                throw new InvalidDataException("Gói nội bộ thiếu request id.");

            requestId = BinaryPrimitives.ReadInt32LittleEndian(framePayload.AsSpan(0, REQUEST_ID_SIZE));

            byte[] payload = new byte[framePayload.Length - REQUEST_ID_SIZE];
            Buffer.BlockCopy(framePayload, REQUEST_ID_SIZE, payload, 0, payload.Length);
            return payload;
        }
    }
}
