using MemoryPack;
using MMORPG.Shared.Db;
using MMORPG.Shared.Net;

namespace MMORPG.DBServer.Net
{
    /// <summary>Một request đến từ GameServer.</summary>
    public readonly struct DbRequest
    {
        public DbCmd Cmd { get; }
        private readonly byte[] _payload;

        public DbRequest(DbCmd cmd, byte[] payload)
        {
            Cmd = cmd;
            _payload = payload;
        }

        public T GetData<T>() where T : IMemoryPackable<T>
        {
            return NetPayload.Deserialize<T>(_payload);
        }
    }

    /// <summary>Kết quả xử lý. Dispatcher lo việc gắn lại request id và gửi đi.</summary>
    public readonly struct DbResult
    {
        public byte[] Payload { get; }

        private DbResult(byte[] payload)
        {
            Payload = payload;
        }

        public static DbResult Ok<T>(T dto) where T : IMemoryPackable<T>
        {
            return new DbResult(NetPayload.Serialize(dto));
        }
    }

    /// <summary>Đánh dấu một static async method là handler cho một <see cref="DbCmd"/>.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class DbHandlerAttribute : Attribute
    {
        public DbCmd Command { get; }

        public DbHandlerAttribute(DbCmd command)
        {
            Command = command;
        }
    }
}
