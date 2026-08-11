using System;
using System.Collections.Generic;
using System.Reflection;
using HungNT;
using MMORPG.Client.Network;
using MMORPG.Shared.Net;

namespace MMORPG.Scripts.Network
{
    public class NetDispatcher
    {
        private Dictionary<NetCmd, Action<NetPacket>> _handlers = new();

        public NetDispatcher(IReadOnlyList<INetHandlerGroup> groups)
        {
            foreach (INetHandlerGroup group in groups)
                RegisterGroup(group);

            this.Log($"[NetDispatcher] Đăng ký {_handlers.Count} handler từ {groups.Count} nhóm.");
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
                this.LogError($"[NetDispatcher] Handler {cmd} ném lỗi: {ex}");
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
                    if (method.ReturnType != typeof(void) || method.GetParameters().Length != 1 || method.GetParameters()[0].ParameterType != typeof(NetPacket))
                    {
                        this.LogWarning($"[NetDispatcher] BỎ QUA {group.GetType().Name}.{method.Name} — " + "sai chữ ký, phải là: void Ten(NetPacket packet)");
                        continue;
                    }

                    var del = (Action<NetPacket>)Delegate.CreateDelegate(
                        typeof(Action<NetPacket>), group, method
                    );

                    if (_handlers.ContainsKey(attr.Command))
                    {
                        this.LogWarning($"[NetDispatcher] TRÙNG {attr.Command}, bỏ qua " + $"{group.GetType().Name}.{method.Name}");
                        continue;
                    }

                    _handlers[attr.Command] = del;
                }
            }
        }
    }
}