using MMORPG.Shared.Dto;

namespace MMORPG.Shared.Net
{
    /// <summary>
    /// Mã lệnh trên dây. Client và server dùng chung enum này (qua MMORPG.Shared.dll)
    /// nên không tồn tại khả năng lệch số.
    ///
    /// Quy hoạch dải — xem ROADMAP.md §2:
    ///   1–99     hệ thống
    ///   100–199  auth
    ///   200–299  character
    ///   300–399  world / movement
    ///   400–499  inventory
    ///   500–599  combat
    ///   600–699  chat
    ///   1000+    nội bộ GameServer ↔ DBServer (client không bao giờ thấy)
    ///
    /// Thêm lệnh mới: luôn thêm vào CUỐI dải của feature. Không chèn giữa, không tái dùng số đã xoá.
    /// </summary>
    public enum NetCmd
    {
        /// <summary>Giá trị vô hiệu. Dùng làm "không có response".</summary>
        None = 0,

        #region Hệ thống (1–99)

        /// <summary>
        /// Đo độ trễ. Request: <see cref="Dto.PingRequest"/> · Response: <see cref="Dto.PingResponse"/>
        /// Client chủ động gửi.
        /// </summary>
        Ping = 1,

        /// <summary>
        /// Server báo lỗi cho một request. Chỉ server gửi.
        /// Payload: <see cref="Dto.ErrorResponse"/>
        /// </summary>
        Error = 2,

        /// <summary>
        /// Lệnh thử để kiểm đường ống request/response. Không còn dùng — xoá được (kèm handler hai bên).
        /// Request/Response: <see cref="Dto.EchoRequest"/> / <see cref="Dto.EchoResponse"/>
        /// </summary>
        Echo = 3,

        ServerInfo = 4,

        /// <summary>
        /// Server chủ động đá client ra. Chỉ server gửi, client không bao giờ gửi lệnh này.
        /// Payload: <see cref="KickedNotice"/>
        /// </summary>
        Kicked = 5,

        #endregion

        #region Auth (100–199)

        /// <summary>
        /// Tạo tài khoản mới.
        /// Request: <see cref="RegisterRequest"/> · Response: <see cref="AuthResponse"/>
        /// Client chủ động gửi.
        /// </summary>
        Register = 100,

        /// <summary>
        /// Đăng nhập.
        /// Request: <see cref="LoginRequest"/> · Response: <see cref="AuthResponse"/>
        /// Client chủ động gửi.
        /// </summary>
        Login = 101,

        /// <summary>
        /// Đăng xuất chủ động (về màn hình login mà không cắt TCP).
        /// Request: <see cref="Dto.EmptyRequest"/> · Response: <see cref="AuthResponse"/>
        /// </summary>
        Logout = 102,

        #endregion

        #region Character (200–299)

        /// <summary>
        /// Vào thế giới. Nhân vật tự tạo trong lần gọi đầu tiên của tài khoản.
        /// Request: <see cref="Dto.EmptyRequest"/> · Response: <see cref="Dto.EnterWorldResponse"/>
        /// Client chủ động gửi ngay sau khi đăng nhập thành công.
        /// </summary>
        EnterWorld = 200,

        #endregion
    }
}
