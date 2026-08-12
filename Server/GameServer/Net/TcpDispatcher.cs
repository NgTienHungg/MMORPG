using System.Reflection;
using MMORPG.ServerCore;
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
        private static readonly Dictionary<NetCmd, Func<NetRequest, Task<NetResult>>> _handlers = new();

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

                string origin = $"{method.DeclaringType?.Name}.{method.Name}";

                if (method.ReturnType != typeof(Task<NetResult>) ||
                    method.GetParameters().Length != 1 ||
                    method.GetParameters()[0].ParameterType != typeof(NetRequest))
                {
                    Log.Warn($"BỎ QUA {origin.Yellow()} — sai chữ ký, phải là: static Task<NetResult> Ten(NetRequest req)");
                    continue;
                }

                _handlers[attr.Command] = (Func<NetRequest, Task<NetResult>>)Delegate.CreateDelegate(
                    typeof(Func<NetRequest, Task<NetResult>>), method);
            }

            Log.Info($"Đăng ký {_handlers.Count.ToString().Green()} handler.");
        }

        /// <summary>
        /// Tìm handler, chạy, và gửi phản hồi (nếu có). Mọi lỗi đều biến thành gói Error gửi về client.
        /// </summary>
        public static async Task DispatchAsync(ClientSession session, NetCmd cmd, byte[] payload)
        {
            if (!_handlers.TryGetValue(cmd, out Func<NetRequest, Task<NetResult>>? handler))
            {
                SendError(session, cmd, ErrorCode.UnknownCommand, $"Không có handler cho {cmd}");
                return;
            }

            NetResult result;
            try
            {
                result = await handler(new NetRequest(session, cmd, payload));
            }
            catch (InvalidDataException ex)
            {
                SendError(session, cmd, ErrorCode.MalformedPayload, ex.Message);
                return;
            }
            catch (Db.DbUnavailableException ex)
            {
                // DB chết là lỗi hệ thống, nhưng client vẫn phải nhận được câu trả lời tử tế
                // thay vì ngồi chờ vô hạn.
                Log.Warn($"{cmd} không gọi được DB: {ex.Message.Red()}");
                SendError(session, cmd, ErrorCode.ServiceUnavailable, "Máy chủ dữ liệu tạm thời không phản hồi.");
                return;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Handler {cmd} ném lỗi");
                SendError(session, cmd, ErrorCode.InternalError, ex.Message);
                return;
            }

            // NetResult.None (fire-and-forget) có Payload null — gửi đi là ném NullReference ở PacketFrame.
            if (result.Payload == null)
                return;

            NetCmd responseCmd = result.Cmd == NetCmd.None ? cmd : result.Cmd;
            session.SendRaw(responseCmd, result.Payload);
        }

        private static void SendError(ClientSession session, NetCmd failedCmd, ErrorCode code, string detail)
        {
            Log.Warn($"Lỗi {failedCmd}: {code.ToString().Red()} — {detail}");

            var dto = new ErrorResponse { FailedCmd = (int)failedCmd, Code = code, Detail = detail };
            session.SendRaw(NetCmd.Error, NetPayload.Serialize(dto));
        }
    }
}
