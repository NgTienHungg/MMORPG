using MMORPG.Shared.Net;

namespace MMORPG.Client.Auth
{
    /// <summary>
    /// Đổi <see cref="ErrorCode"/> thành câu tiếng Việt cho người chơi.
    /// Việc dịch nằm ở CLIENT, không phải server. Server trả về mã; client quyết định hiển thị thế nào.
    /// Nhờ vậy đổi câu chữ không phải build lại server, và thêm ngôn ngữ khác chỉ là thêm một bảng tra.
    /// </summary>
    public static class AuthErrorText
    {
        public static string Of(ErrorCode code)
        {
            return code switch
            {
                ErrorCode.InvalidInput => "Tên đăng nhập 3–16 ký tự (chữ thường, số, gạch dưới). Mật khẩu tối thiểu 6 ký tự.",
                ErrorCode.AccountExists => "Tên đăng nhập này đã có người dùng.",
                ErrorCode.InvalidCredentials => "Sai tài khoản hoặc mật khẩu.",
                ErrorCode.TooManyAttempts => "Bạn thử sai quá nhiều lần. Chờ một phút rồi thử lại.",
                ErrorCode.ServiceUnavailable => "Máy chủ đang bận. Thử lại sau giây lát.",
                _ => "Có lỗi xảy ra. Thử lại sau.",
            };
        }
    }
}