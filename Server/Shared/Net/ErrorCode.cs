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
    }
}
