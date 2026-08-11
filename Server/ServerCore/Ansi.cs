namespace MMORPG.ServerCore
{
    /// <summary>Bảng màu chữ của terminal. Giá trị là mã SGR của ANSI.</summary>
    public enum AnsiColor
    {
        Gray = 90,
        Red = 91,
        Green = 92,
        Yellow = 93,
        Blue = 94,
        Magenta = 95,
        Cyan = 96,
        White = 97,
    }

    /// <summary>
    /// Tô màu một MẨU chữ bên trong câu log — song song với <c>StringExtensions</c> của
    /// <c>com.hungnt.core</c> ở client (client dùng rich text của Unity, server dùng ANSI).
    ///
    /// <code>
    /// Log.Info($"Session {id.ToString().Cyan()} đăng nhập thành công");
    /// Log.Info($"HP còn {hp.ToString().Red().Bold()}");
    /// </code>
    ///
    /// <see cref="Log"/> chỉ tô màu phần LEVEL và [Tag], cố tình để phần nội dung nguyên bản —
    /// nhờ vậy màu bạn đặt ở đây không bao giờ bị mã reset của tầng ngoài ăn mất.
    /// </summary>
    public static class AnsiExtensions
    {
        internal const string ESC = "\u001b[";
        internal const string RESET = ESC + "0m";

        public static string Color(this string content, AnsiColor color)
            => Log.UseColor ? $"{ESC}{(int)color}m{content}{RESET}" : content;

        public static string Bold(this string content)
            => Log.UseColor ? $"{ESC}1m{content}{RESET}" : content;

        public static string Dim(this string content)
            => Log.UseColor ? $"{ESC}2m{content}{RESET}" : content;

        public static string Gray(this string content) => content.Color(AnsiColor.Gray);
        public static string Red(this string content) => content.Color(AnsiColor.Red);
        public static string Green(this string content) => content.Color(AnsiColor.Green);
        public static string Yellow(this string content) => content.Color(AnsiColor.Yellow);
        public static string Blue(this string content) => content.Color(AnsiColor.Blue);
        public static string Magenta(this string content) => content.Color(AnsiColor.Magenta);
        public static string Cyan(this string content) => content.Color(AnsiColor.Cyan);
    }
}
