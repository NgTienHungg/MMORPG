namespace MMORPG.Shared.Net
{
    /// <summary>
    /// Mã lỗi nghiệp vụ. Lỗi nghiệp vụ KHÔNG ném exception — trả mã này về cho client.
    /// Exception chỉ dành cho lỗi hệ thống (packet hỏng, mất DB).
    /// </summary>
    public enum ErrorCode
    {
        None = 0,

        /// <summary>Server không có handler cho lệnh này.</summary>
        UnknownCommand = 1,

        /// <summary>Payload không giải mã được — sai kiểu DTO hoặc contract lệch.</summary>
        MalformedPayload = 2,

        /// <summary>Handler ném exception ngoài dự kiến.</summary>
        InternalError = 3,

        /// <summary>Chưa đăng nhập mà gọi lệnh cần đăng nhập.</summary>
        NotAuthenticated = 4,

        /// <summary>Một dịch vụ phía sau (DBServer) không phản hồi. Client nên cho người chơi thử lại.</summary>
        ServiceUnavailable = 5,

        /// <summary>Dữ liệu client gửi lên sai định dạng (tên quá ngắn, ký tự lạ...).</summary>
        InvalidInput = 6,

        /// <summary>Tên đăng nhập đã có người dùng.</summary>
        AccountExists = 7,

        /// <summary>Sai tài khoản HOẶC sai mật khẩu — cố tình không phân biệt.</summary>
        InvalidCredentials = 8,

        /// <summary>Thử sai quá nhiều lần, phải chờ.</summary>
        TooManyAttempts = 9,

        /// <summary>Gọi lệnh cần đăng nhập nhưng session đã đăng nhập rồi (vd: Login hai lần).</summary>
        AlreadyAuthenticated = 10,
    }
}
