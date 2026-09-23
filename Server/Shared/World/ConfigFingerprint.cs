using MemoryPack;

namespace MMORPG.Shared.World
{
    /// <summary>
    /// Dấu vân tay của một file config, băm từ byte đã tuần tự hoá. Một hàm cho mọi file.
    ///
    /// Mỗi bên in vân tay của file mình vừa nạp ra log; đặt hai dòng cạnh nhau là biết hai bên có
    /// đang cầm cùng một bộ dữ liệu không. Không có phép so tự động nào trên dây — mỗi bên tự quyết
    /// định nạp file nào, nên hai danh sách không bao giờ bằng nhau.
    /// </summary>
    public static class ConfigFingerprint
    {
        /// <summary>
        /// Băm byte chứ không băm text thô của file: <c>core.autocrlf</c> làm cùng một nội dung ra
        /// CRLF trên Windows và LF trên Linux, còn byte tuần tự hoá thì không có ký tự xuống dòng.
        /// Hai bản build của Shared sinh cùng dãy byte — cả giao thức đã dựa vào điều đó từ khung
        /// gói tin đầu tiên.
        ///
        /// Gọi SAU khi nạp xong (sau <c>Prepare()</c>, sau khi server kẹp miền giá trị), và cả hai
        /// bên gọi ở cùng chỗ ấy; lệch thời điểm là hai số lệch nhau vì một lý do không liên quan
        /// tới nội dung file.
        /// </summary>
        /// <remarks>
        /// Ràng buộc <c>IMemoryPackable</c> giống <see cref="Net.NetPayload.Serialize{T}"/>: nó bắt
        /// lúc biên dịch việc quên <c>[MemoryPackable]</c> trên một bảng mới. File chỉ đi qua
        /// Newtonsoft — file map — thì không in vân tay, và cũng không có hàm băm riêng.
        /// </remarks>
        public static uint Of<TFile>(TFile file)
            where TFile : class, IConfigFile, IMemoryPackable<TFile>
        {
            byte[] bytes = MemoryPackSerializer.Serialize(file);

            return Fnv1a.Mix(Fnv1a.START, bytes);
        }
    }
}
