namespace MMORPG.GameServer
{
    /// <summary>
    /// Session đi một chiều qua các trạng thái này, không quay lui (trừ Logout về Connected).
    /// Giá trị tăng dần để so sánh được bằng <c>&gt;=</c> — dispatcher dựa vào đó để chặn lệnh gọi sai lúc.
    /// </summary>
    public enum SessionState
    {
        /// <summary>TCP đã nối, chưa biết là ai, và chưa biết có cùng contract không.</summary>
        Connected = 0,

        /// <summary>
        /// Đã qua kiểm phiên bản. Bậc này nằm GIỮA Connected và Authenticated chứ không thêm vào cuối:
        /// dispatcher so bằng >=, nên thứ tự các bậc LÀ luật. Thêm vào cuối thì "đã vào world" không
        /// còn hàm ý "đã kiểm phiên bản".
        /// </summary>
        Verified = 1,

        /// <summary>Đã đăng nhập, chưa vào thế giới.</summary>
        Authenticated = 2,

        /// <summary>Đã vào thế giới, đang điều khiển một entity.</summary>
        InWorld = 3,
    }
}
