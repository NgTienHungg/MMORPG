using System.Reflection;
using MMORPG.Shared.Dto;
using MMORPG.Shared.Net;

namespace MMORPG.GameServer.Net
{
    /// <summary>
    /// Bảng tra lệnh → handler. Thay cho switch khổng lồ.
    /// Quét reflection một lần lúc khởi động, sau đó chỉ còn tra Dictionary — O(1), không phản chiếu lúc chạy.
    /// </summary>
    public static class TcpDispatcher
    {
        private static readonly Dictionary<NetCmd, Func<NetRequest, NetResult>> _handlers = new();

        /// <summary>
        /// Quét mọi assembly đã nạp, tìm static method có <see cref="TcpHandlerAttribute"/> và đăng ký.
        /// Gọi đúng MỘT lần lúc server khởi động.
        /// </summary>
        public static void RegisterAll()
        {
            _handlers.Clear();

            IEnumerable<MethodInfo> methods = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && (a.FullName?.StartsWith("MMORPG.") ?? false))
                .SelectMany(a => a.GetTypes())
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                .Where(m => m.GetCustomAttribute<TcpHandlerAttribute>() != null);

            foreach (MethodInfo method in methods)
            {
                TcpHandlerAttribute attr = method.GetCustomAttribute<TcpHandlerAttribute>()!;

                if (method.ReturnType != typeof(NetResult) || method.GetParameters().Length != 1 || method.GetParameters()[0].ParameterType != typeof(NetRequest))
                {
                    Console.WriteLine($"[Dispatcher] BỎ QUA {method.DeclaringType?.Name}.{method.Name} — " + "sai chữ ký, phải là: static NetResult Ten(NetRequest req)");
                    continue;
                }

                var del = (Func<NetRequest, NetResult>)Delegate.CreateDelegate(
                    typeof(Func<NetRequest, NetResult>), method
                );

                if (!_handlers.TryAdd(attr.Command, del))
                {
                    Console.WriteLine($"[Dispatcher] TRÙNG {attr.Command} — " + $"đã có handler, bỏ qua {method.DeclaringType?.Name}.{method.Name}");
                    continue;
                }

                Console.WriteLine($"[Dispatcher] {attr.Command} -> {method.DeclaringType?.Name}.{method.Name}");
            }

            Console.WriteLine($"[Dispatcher] Đăng ký {_handlers.Count} handler.");
        }

        /// <summary>
        /// Tìm handler, chạy, và gửi phản hồi (nếu có). Mọi lỗi đều biến thành gói Error gửi về client.
        /// </summary>
        public static void Dispatch(ClientSession session, NetCmd cmd, byte[] payload)
        {
            if (!_handlers.TryGetValue(cmd, out Func<NetRequest, NetResult>? handler))
            {
                SendError(session, cmd, ErrorCode.UnknownCommand, $"Không có handler cho {cmd}");
                return;
            }

            NetResult result;
            try
            {
                result = handler(new NetRequest(session, cmd, payload));
            }
            catch (InvalidDataException ex)
            {
                SendError(session, cmd, ErrorCode.MalformedPayload, ex.Message);
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Dispatcher] Handler {cmd} ném lỗi: {ex}");
                SendError(session, cmd, ErrorCode.InternalError, ex.Message);
                return;
            }

            if (result.Payload == null)
                return; // handler chủ động không trả gì

            NetCmd responseCmd = result.Cmd == NetCmd.None ? cmd : result.Cmd;
            session.SendRaw(responseCmd, result.Payload);
        }

        private static void SendError(ClientSession session, NetCmd failedCmd, ErrorCode code, string detail)
        {
            Console.WriteLine($"[Dispatcher] Lỗi {failedCmd}: {code} — {detail}");

            var dto = new ErrorResponse { FailedCmd = (int)failedCmd, Code = code, Detail = detail };
            session.SendRaw(NetCmd.Error, NetPayload.Serialize(dto));
        }
    }
}
