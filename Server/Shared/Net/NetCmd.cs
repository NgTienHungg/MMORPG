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
        /// Lệnh thử của Phase 2. Request/Response: <see cref="Dto.EchoRequest"/> / <see cref="Dto.EchoResponse"/>
        /// Xoá khi Phase 4 xong.
        /// </summary>
        Echo = 3,
        #endregion
    }
}
