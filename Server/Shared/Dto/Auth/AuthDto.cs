using MemoryPack;

namespace MMORPG.Shared.Dto
{
    [MemoryPackable]
    public partial class RegisterRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    [MemoryPackable]
    public partial class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>
    /// Dùng chung cho Register / Login / Logout.
    ///
    /// KHÔNG chứa AccountId. Client không cần biết id của mình để làm gì cả —
    /// mọi lệnh sau đó server đều tự tra từ session. Không gửi đi thứ không ai cần
    /// là cách rẻ nhất để không ai lạm dụng nó.
    /// </summary>
    [MemoryPackable]
    public partial class AuthResponse
    {
        public bool Success { get; set; }

        /// <summary><see cref="Net.ErrorCode.None"/> khi thành công.</summary>
        public Net.ErrorCode Error { get; set; }

        /// <summary>Tên hiển thị, server trả về đúng như đã lưu (đã chuẩn hoá chữ thường).</summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Token phiên do server cấp khi đăng nhập thành công. Rỗng khi thất bại.
        /// </summary>
        public string SessionToken { get; set; } = string.Empty;
    }

    /// <summary>Server báo lý do đá client ra.</summary>
    [MemoryPackable]
    public partial class KickedNotice
    {
        /// <summary>Chuỗi tiếng Việt hiển thị thẳng cho người chơi.</summary>
        public string Reason { get; set; } = string.Empty;
    }
}
