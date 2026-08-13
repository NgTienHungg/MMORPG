namespace MMORPG.Shared.Db
{
    /// <summary>
    /// Mã lệnh của protocol nội bộ GameServer ↔ DBServer.
    ///
    /// Dải 1000+ theo ROADMAP.md §2. Client KHÔNG BAO GIỜ thấy enum này — nó nằm trong
    /// MMORPG.Shared.dll mà Unity cũng nạp, nhưng không có đường nào để client gửi một DbCmd
    /// tới GameServer và được xử lý: hai dispatcher là hai bảng tra hoàn toàn tách biệt.
    /// </summary>
    public enum DbCmd
    {
        None = 0,

        #region Hệ thống (1000–1099)

        Ping = 1000,
        ServerMetaGet = 1001,
        ServerMetaSet = 1002,

        #endregion

        #region Account (1100–1199)

        /// <summary>
        /// Tìm tài khoản theo tên đăng nhập.
        /// Request: <see cref="Dto.Db.AccountGetRequest"/> · Response: <see cref="Dto.Db.AccountGetResponse"/>
        /// </summary>
        AccountGetByName = 1100,

        /// <summary>
        /// Tạo tài khoản mới. Trùng tên thì trả <c>Created = false</c>, KHÔNG ném lỗi.
        /// Request: <see cref="Dto.Db.AccountCreateRequest"/> · Response: <see cref="Dto.Db.AccountCreateResponse"/>
        /// </summary>
        AccountCreate = 1101,

        /// <summary>
        /// Ghi mốc đăng nhập gần nhất. Fire-and-forget về mặt nghiệp vụ nhưng vẫn có response.
        /// Request: <see cref="Dto.Db.AccountTouchLoginRequest"/> · Response: <see cref="Dto.Db.DbOkResponse"/>
        /// </summary>
        AccountTouchLogin = 1102,

        #endregion
    }
}
