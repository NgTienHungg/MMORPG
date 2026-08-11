using System;
using System.Collections.Generic;
using System.Reflection;
using HungNT;
using MMORPG.Shared.Net;

namespace MMORPG.Client.Network
{
    /// <summary>
    /// Bảng tra lệnh → handler của client. Thay cho switch khổng lồ.
    ///
    /// Khác server ở chỗ handler là method INSTANCE: nhóm handler cần nhận inject nên phải do
    /// container tạo. Vì vậy mỗi nhóm mới đều phải được đăng ký tay trong
    /// <c>GameLifetimeScope</c> với <c>.As&lt;INetHandlerGroup&gt;()</c> — không có bản quét tự động.
    /// </summary>
    public class NetDispatcher
    {
        private readonly Dictionary<NetCmd, Action<NetPacket>> _handlers = new();

        public NetDispatcher(IReadOnlyList<INetHandlerGroup> groups)
        {
            foreach (INetHandlerGroup group in groups)
                RegisterGroup(group);

            this.Log($"Đăng ký {_handlers.Count} handler từ {groups.Count} nhóm.");
        }

        /// <returns>false nếu không có handler — để tầng trên quyết định log hay bỏ qua.</returns>
        public bool Dispatch(NetCmd cmd, byte[] payload)
        {
            if (!_handlers.TryGetValue(cmd, out Action<NetPacket> handler))
                return false;

            try
            {
                handler(new NetPacket(cmd, payload));
            }
            catch (Exception ex)
            {
                // Một handler hỏng không được làm sập vòng nhận gói.
                this.LogError($"Handler {cmd} ném lỗi: {ex}");
            }

            return true;
        }

        private void RegisterGroup(INetHandlerGroup group)
        {
            MethodInfo[] methods = group.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (MethodInfo method in methods)
            {
                foreach (NetHandlerAttribute attr in method.GetCustomAttributes<NetHandlerAttribute>())
                {
                    string origin = $"{group.GetType().Name}.{method.Name}";

                    if (method.ReturnType != typeof(void) || method.GetParameters().Length != 1 || method.GetParameters()[0].ParameterType != typeof(NetPacket))
                    {
                        this.LogWarning($"BỎ QUA {origin} — sai chữ ký, phải là: void Ten(NetPacket packet)");
                        continue;
                    }

                    if (_handlers.ContainsKey(attr.Command))
                    {
                        this.LogWarning($"TRÙNG {attr.Command}, bỏ qua {origin}");
                        continue;
                    }

                    _handlers[attr.Command] = (Action<NetPacket>)Delegate.CreateDelegate(
                        typeof(Action<NetPacket>), group, method
                    );
                }
            }
        }
    }
}
