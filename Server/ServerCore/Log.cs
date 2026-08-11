using System.Runtime.CompilerServices;

namespace MMORPG.ServerCore
{
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3,
        Off = 4,
    }

    /// <summary>
    /// Logger dùng chung cho GameServer, DBServer và mọi project server sau này —
    /// đối ứng của <c>DebugEx</c> bên client.
    ///
    /// <code>
    /// Log.Info("Lắng nghe trên 0.0.0.0:7777");
    /// Log.Warn($"Không có handler cho {cmd}");
    /// Log.Error(ex, "Vòng đọc chết");
    /// </code>
    ///
    /// Dòng ra: <c>12:03:41.882 INFO  [TcpDispatcher] Đăng ký 2 handler.</c>
    ///
    /// Tên class trong <c>[Tag]</c> do compiler điền qua <see cref="CallerFilePathAttribute"/>,
    /// nên KHÔNG lặp lại tên class trong nội dung log — y hệt quy ước của <c>DebugEx</c>.
    /// Dùng được cả trong class static (server handler đều là static, nên không thể học theo
    /// kiểu extension <c>this.Log(...)</c> của client).
    /// </summary>
    public static class Log
    {
        /// <summary>Dưới mức này thì không in. Đặt <see cref="LogLevel.Info"/> khi chạy thật để bớt nhiễu.</summary>
        public static LogLevel MinLevel { get; set; } = LogLevel.Debug;

        /// <summary>
        /// Tắt khi output bị chuyển hướng ra file hoặc khi có biến môi trường NO_COLOR —
        /// mã màu lọt vào file log chỉ tổ rác.
        /// </summary>
        public static bool UseColor { get; set; } = DetectColorSupport();

        public static void Debug(string message, [CallerFilePath] string filePath = "")
            => Write(LogLevel.Debug, message, filePath);

        public static void Info(string message, [CallerFilePath] string filePath = "")
            => Write(LogLevel.Info, message, filePath);

        public static void Warn(string message, [CallerFilePath] string filePath = "")
            => Write(LogLevel.Warn, message, filePath);

        public static void Error(string message, [CallerFilePath] string filePath = "")
            => Write(LogLevel.Error, message, filePath);

        /// <summary>Log lỗi kèm nguyên stack trace — đừng chỉ in <c>ex.Message</c>, mất chỗ ném là mất tất.</summary>
        public static void Error(Exception ex, string message, [CallerFilePath] string filePath = "")
            => Write(LogLevel.Error, $"{message}{Environment.NewLine}{ex}", filePath);

        private static void Write(LogLevel level, string message, string filePath)
        {
            if (level < MinLevel)
                return;

            string time = DateTime.Now.ToString("HH:mm:ss.fff");
            string tag = Path.GetFileNameWithoutExtension(filePath);

            // Một lần WriteLine duy nhất: Console.Out đã được đồng bộ hoá, nên nhiều session
            // log cùng lúc vẫn ra từng dòng trọn vẹn thay vì cắt xen vào nhau.
            Console.Out.WriteLine($"{LevelLabel(level)} [{tag.Bold()}] {message}"); // {time.Dim()}
        }

        /// <summary>
        /// Chỉ LEVEL và [Tag] được tô màu; phần nội dung để nguyên để màu do
        /// <see cref="AnsiExtensions"/> đặt bên trong câu không bị mã reset cắt ngang.
        /// </summary>
        private static string LevelLabel(LogLevel level) => level switch
        {
            LogLevel.Debug => "DEBUG".Gray(),
            LogLevel.Info => "INFO ".Cyan(),
            LogLevel.Warn => "WARN ".Yellow().Bold(),
            LogLevel.Error => "ERROR".Red().Bold(),
            _ => "     ",
        };

        private static bool DetectColorSupport()
        {
            if (Console.IsOutputRedirected)
                return false;

            // Quy ước chung của giới CLI: https://no-color.org
            return Environment.GetEnvironmentVariable("NO_COLOR") is null;
        }
    }
}
