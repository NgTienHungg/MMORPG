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

        /// <summary>
        /// Kiểm tra DBServer còn sống và query được không.
        /// Request: <see cref="Dto.Db.DbPingRequest"/> · Response: <see cref="Dto.Db.DbPingResponse"/>
        /// GameServer chủ động gửi.
        /// </summary>
        Ping = 1000,

        /// <summary>
        /// Đọc một dòng trong bảng <c>server_meta</c>.
        /// Request: <see cref="Dto.Db.ServerMetaGetRequest"/> · Response: <see cref="Dto.Db.ServerMetaGetResponse"/>
        /// </summary>
        ServerMetaGet = 1001,

        /// <summary>
        /// Ghi (thêm mới hoặc đè) một dòng trong bảng <c>server_meta</c>.
        /// Request: <see cref="Dto.Db.ServerMetaSetRequest"/> · Response: <see cref="Dto.Db.DbOkResponse"/>
        /// </summary>
        ServerMetaSet = 1002,
    }
}
