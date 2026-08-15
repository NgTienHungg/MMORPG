namespace MMORPG.GameServer
{
    /// <summary>
    /// Session đi một chiều qua các trạng thái này, không quay lui (trừ Logout về Connected).
    /// Giá trị tăng dần để so sánh được bằng <c>&gt;=</c> — dispatcher dựa vào đó để chặn lệnh gọi sai lúc.
    /// </summary>
    public enum SessionState
    {
        /// <summary>TCP đã nối, chưa biết là ai.</summary>
        Connected = 0,

        /// <summary>Đã đăng nhập, chưa chọn nhân vật.</summary>
        Authenticated = 1,

        /// <summary>Đã vào thế giới, đang điều khiển một entity.</summary>
        InWorld = 2,
    }
}
