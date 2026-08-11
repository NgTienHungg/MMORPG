using System;
using MemoryPack;
using MMORPG.Shared.Net;

namespace MMORPG.Client.Network
{
    /// <summary>Gói tin đã về tới main thread, chờ giải mã.</summary>
    public readonly struct NetPacket
    {
        public NetCmd Cmd { get; }
        private readonly byte[] _payload;

        public NetPacket(NetCmd cmd, byte[] payload)
        {
            Cmd = cmd;
            _payload = payload;
        }

        public T GetData<T>() where T : IMemoryPackable<T> => NetPayload.Deserialize<T>(_payload);
    }

    /// <summary>
    /// Đánh dấu một method là handler cho một lệnh.
    /// Chữ ký bắt buộc: <c>void Ten(NetPacket packet)</c> — method thường (không static),
    /// nằm trong một class cài <see cref="INetHandlerGroup"/> đã đăng ký vào container.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class NetHandlerAttribute : Attribute
    {
        public NetCmd Command { get; }
        public NetHandlerAttribute(NetCmd command) => Command = command;
    }

    /// <summary>Marker để container gom mọi nhóm handler lại cho dispatcher.</summary>
    public interface INetHandlerGroup { }
}