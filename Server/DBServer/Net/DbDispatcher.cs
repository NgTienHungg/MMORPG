using System.Reflection;
using MMORPG.ServerCore;
using MMORPG.Shared.Db;
using MMORPG.Shared.Dto.Db;

namespace MMORPG.DBServer.Net
{
    /// <summary>
    /// Bảng tra <see cref="DbCmd"/> → handler. Quét reflection một lần lúc khởi động.
    /// </summary>
    public static class DbDispatcher
    {
        private static readonly Dictionary<DbCmd, Func<DbRequest, Task<DbResult>>> _handlers = new();

        public static void RegisterAll()
        {
            _handlers.Clear();

            IEnumerable<MethodInfo> methods = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && (a.FullName?.StartsWith("MMORPG.") ?? false))
                .SelectMany(a => a.GetTypes())
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                .Where(m => m.GetCustomAttribute<DbHandlerAttribute>() != null);

            foreach (MethodInfo method in methods)
            {
                DbHandlerAttribute attr = method.GetCustomAttribute<DbHandlerAttribute>()!;

                if (method.ReturnType != typeof(Task<DbResult>) ||
                    method.GetParameters().Length != 1 ||
                    method.GetParameters()[0].ParameterType != typeof(DbRequest))
                {
                    Log.Warn($"BỎ QUA {method.DeclaringType?.Name}.{method.Name} — " +
                             "sai chữ ký, phải là: static Task<DbResult> Ten(DbRequest req)");
                    continue;
                }

                var del = (Func<DbRequest, Task<DbResult>>)Delegate.CreateDelegate(
                    typeof(Func<DbRequest, Task<DbResult>>), method);

                if (!_handlers.TryAdd(attr.Command, del))
                {
                    Log.Warn($"TRÙNG {attr.Command}, bỏ qua {method.Name}");
                    continue;
                }

                // Log.Debug($"{attr.Command} -> {method.DeclaringType?.Name}.{method.Name}");
            }

            Log.Info($"Đăng ký {_handlers.Count} handler.");
        }

        /// <summary>
        /// Chạy handler. Mọi lỗi biến thành <see cref="DbOkResponse"/> có <c>Success = false</c>
        /// — GameServer luôn nhận được MỘT response, kể cả khi DB hỏng.
        /// </summary>
        public static async Task<DbResult> DispatchAsync(DbCmd cmd, byte[] payload)
        {
            if (!_handlers.TryGetValue(cmd, out Func<DbRequest, Task<DbResult>>? handler))
            {
                Log.Warn($"Không có handler cho {cmd}");
                return DbResult.Ok(new DbOkResponse { Success = false, Detail = $"Không có handler cho {cmd}" });
            }

            try
            {
                return await handler(new DbRequest(cmd, payload));
            }
            catch (Exception ex)
            {
                Log.Error($"Handler {cmd} ném lỗi: {ex}");
                return DbResult.Ok(new DbOkResponse { Success = false, Detail = ex.Message });
            }
        }
    }
}
