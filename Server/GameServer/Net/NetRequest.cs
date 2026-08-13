using MemoryPack;
using MMORPG.Shared.Net;

namespace MMORPG.GameServer.Net
{
    /// <summary>
    /// Bối cảnh của một gói tin đến: ai gửi, nội dung gì.
    /// </summary>
    public readonly struct NetRequest
    {
        public ClientSession Session { get; }
        public NetCmd Cmd { get; }
        private readonly byte[] _payload;

        public NetRequest(ClientSession session, NetCmd cmd, byte[] payload)
        {
            Session = session;
            Cmd = cmd;
            _payload = payload;
        }

        /// <summary>Giải mã payload thành DTO. Ném <see cref="System.IO.InvalidDataException"/> nếu sai kiểu.</summary>
        public T GetData<T>() where T : IMemoryPackable<T> => NetPayload.Deserialize<T>(_payload);
    }

    /// <summary>
    /// Kết quả xử lý. Handler trả struct này, dispatcher lo việc gửi đi.
    /// Handler KHÔNG tự gọi Send cho phần response — để một chỗ duy nhất chịu trách nhiệm.
    /// </summary>
    public readonly struct NetResult
    {
        /// <summary><see cref="NetCmd.None"/> nghĩa là trả về đúng cmd của request.</summary>
        public NetCmd Cmd { get; }

        /// <summary>null nghĩa là không trả gì.</summary>
        public byte[] Payload { get; }

        private NetResult(NetCmd cmd, byte[] payload)
        {
            Cmd = cmd;
            Payload = payload;
        }

        /// <summary>Không phản hồi (fire-and-forget, ví dụ gói di chuyển).</summary>
        public static NetResult None => default;

        /// <summary>Trả DTO về đúng cmd vừa nhận.</summary>
        public static NetResult Ok<T>(T dto) where T : IMemoryPackable<T> => new(NetCmd.None, NetPayload.Serialize(dto));

        /// <summary>Trả DTO về một cmd khác (dùng khi response không cùng cặp với request).</summary>
        public static NetResult On<T>(NetCmd cmd, T dto) where T : IMemoryPackable<T> => new(cmd, NetPayload.Serialize(dto));
    }

    /// <summary>Đánh dấu một static method là handler cho một lệnh.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class TcpHandlerAttribute : Attribute
    {
        public NetCmd Command { get; }

        /// <summary>Trạng thái tối thiểu để được gọi lệnh này. Mặc định: không cần đăng nhập.</summary>
        public SessionState MinState { get; set; } = SessionState.Connected;

        public TcpHandlerAttribute(NetCmd command) => Command = command;
    }
}
