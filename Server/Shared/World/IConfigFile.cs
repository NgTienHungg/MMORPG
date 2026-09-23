namespace MMORPG.Shared.World
{
    /// <summary>
    /// Hình dạng chung của mọi file config, đủ để một hàm nạp duy nhất làm việc được với tất cả:
    /// bảng nhân vật, bảng item, và bộ tham số vận hành của server.
    ///
    /// Interface dừng ở hai thành viên này — thêm nữa là ép mọi file tương lai mang thứ mà chỉ một
    /// file dùng. Dấu vân tay không nằm ở đây vì nó TÍNH RA từ file: xem <see cref="ConfigFingerprint"/>.
    /// </summary>
    public interface IConfigFile
    {
        /// <summary>Phiên bản schema của file. Có từ ngày đầu — thêm sau khi đã có người chơi là một cuộc di cư.</summary>
        int Version { get; }

        /// <summary>
        /// Số mục dữ liệu trong file: hàm nạp dùng nó để phát hiện file parse được nhưng rỗng, và để
        /// in log. File không phải bảng thì trả hằng 1 — nó không rỗng được.
        /// </summary>
        int RowCount { get; }
    }
}
