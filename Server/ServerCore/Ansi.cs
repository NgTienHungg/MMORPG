namespace MMORPG.ServerCore
{
    /// <summary>Bảng màu chữ của terminal. Giá trị là mã SGR của ANSI.</summary>
    public enum Color
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
    /// Dùng lối tắt theo tên màu; <c>Color(...)</c> chỉ cần đến khi màu là biến. Nhận <c>string</c>,
    /// nên số phải <c>.ToString()</c> trước:
    /// <code>
    /// Log.Info($"Session {id.ToString().Cyan()} đăng nhập thành công");
    /// Log.Info($"HP còn {hp.ToString().Red()}");
    /// </code>
    ///
    /// <see cref="Log"/> chỉ tô màu phần LEVEL và [Tag], cố tình để phần nội dung nguyên bản —
    /// nhờ vậy màu bạn đặt ở đây không bao giờ bị mã reset của tầng ngoài ăn mất.
    ///
    /// <b>Đừng tô một chuỗi đã có màu ở giữa.</b> Mỗi hàm ở đây đóng bằng một mã RESET, nên nếu
    /// đoạn màu bên trong nằm giữa câu thì màu bên ngoài đứt ngay tại đó và phần đuôi mất màu.
    /// (Bọc trọn vẹn thì không sao — <c>x.Red().Bold()</c> vẫn đúng vì không còn chữ nào phía sau.)
    /// Hệ quả thực tế: <b>nội dung exception phải để trần</b>, vì nơi bắt nó thường bọc cả
    /// <c>ex.Message</c> trong một màu. Tô ở chỗ log, không ở chỗ throw.
    ///
    /// Quy ước màu đang dùng trong repo (giữ nhất quán để đọc console theo phản xạ):
    /// <list type="table">
    ///   <item><term>Green</term><description>địa chỉ, đường dẫn, số đếm thành công</description></item>
    ///   <item><term>Magenta</term><description>định danh: id phiên (<c>Tag</c>), request id</description></item>
    ///   <item><term>Cyan</term><description>tên lệnh: <c>NetCmd</c> / <c>DbCmd</c></description></item>
    ///   <item><term>Yellow</term><description>thứ bị bỏ qua khi khởi động (handler sai chữ ký, trùng)</description></item>
    ///   <item><term>Red</term><description>mã lỗi, tên exception</description></item>
    /// </list>
    /// </summary>
    public static class AnsiExtensions
    {
        internal const string ESC = "\u001b[";
        internal const string RESET = ESC + "0m";

        public static string Color(this string content, Color color)
        {
            return Log.UseColor ? $"{ESC}{(int)color}m{content}{RESET}" : content;
        }

        public static string Bold(this string content)
        {
            return Log.UseColor ? $"{ESC}1m{content}{RESET}" : content;
        }

        public static string Dim(this string content)
        {
            return Log.UseColor ? $"{ESC}2m{content}{RESET}" : content;
        }

        // Lối tắt cho 7 màu hay dùng. Chỉ là vỏ bọc của Color(...) ở trên — thêm màu mới thì
        // thêm vào enum, còn thêm lối tắt ở đây chỉ khi màu đó thực sự được dùng nhiều.
        public static string Gray(this string content)
        {
            return content.Color(ServerCore.Color.Gray);
        }

        public static string Red(this string content)
        {
            return content.Color(ServerCore.Color.Red);
        }

        public static string Green(this string content)
        {
            return content.Color(ServerCore.Color.Green);
        }

        public static string Yellow(this string content)
        {
            return content.Color(ServerCore.Color.Yellow);
        }

        public static string Blue(this string content)
        {
            return content.Color(ServerCore.Color.Blue);
        }

        public static string Magenta(this string content)
        {
            return content.Color(ServerCore.Color.Magenta);
        }

        public static string Cyan(this string content)
        {
            return content.Color(ServerCore.Color.Cyan);
        }
    }
}
