using System.Collections.Generic;

namespace MMORPG.Shared.World
{
    /// <summary>
    /// Bản đối chiếu 1-1 với file map JSON. KHÔNG dùng để chạy game — chỉ để đọc/ghi file; kiểu chạy
    /// trong game là <see cref="MapGrid"/>.
    ///
    /// Tách hai kiểu vì hai vai khác nhau: trường nào có trong file là chuyện của ĐỊNH DẠNG, còn tra
    /// một ô nhanh cỡ nào là chuyện của MÔ PHỎNG. Gộp lại thì mỗi lần đổi định dạng là đụng vào thứ
    /// chạy 20 lần mỗi giây. Cùng mẫu với CharacterRow (DB) ≠ PlayerEntity (world) ở Phase 5.
    ///
    /// Tên trường trong file LẤY THẲNG tên property, không có [JsonProperty] nào. Đổi lại sự gọn gàng
    /// ấy: tên property ở đây LÀ định dạng file, nên đổi tên một property là đổi định dạng — phải tăng
    /// <see cref="MapFile.FORMAT_VERSION"/> và export lại mọi map, chứ không phải một thao tác Rename
    /// bình thường trong IDE.
    /// </summary>
    public sealed class MapFileData
    {
        public int Version { get; set; }

        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Khoá tài nguyên của prefab chứa HÌNH của map — thứ Resources.Load nhận. Là khoá chứ không
        /// phải đường dẫn hệ thống tệp: server đọc cùng file này và nó không biết gì về bố cục thư mục
        /// của project Unity. Thiếu trường thì về chuỗi rỗng — map không có hình riêng, và server thì
        /// không đọc trường này bao giờ.
        /// </summary>
        public string PrefabKey { get; set; } = string.Empty;

        // Cho phép null có chủ đích: file thiếu trường thì Newtonsoft để null, và MapFile.Parse phải
        // nói ra bằng một thông điệp đọc được — thay vì để NullReferenceException nổ ở đâu đó xa hơn.
        public CellPoint? Origin { get; set; }

        public List<SpawnPoint>? Spawns { get; set; }

        /// <summary>
        /// Cổng sang map khác. Trường TUỲ CHỌN: map chưa nối đi đâu thì file thiếu hẳn trường này, và
        /// đó là lý do FORMAT_VERSION không phải tăng khi thêm nó.
        /// </summary>
        public List<Portal>? Portals { get; set; }

        /// <summary>Lưới ô, mỗi phần tử là MỘT HÀNG. Hàng đầu là mép TRÊN map — đọc file như nhìn bản vẽ.</summary>
        public List<string>? Cells { get; set; }
    }

    /// <summary>Một điểm theo toạ độ Ô (số nguyên). Dùng cho origin.</summary>
    public sealed class CellPoint
    {
        public int X { get; set; }

        public int Y { get; set; }
    }

    /// <summary>
    /// Một chỗ người chơi có thể xuất hiện, toạ độ WORLD. Có <see cref="Id"/> vì map cần trỏ tới nhau
    /// được bằng tên: một cổng ở map khác phải nói rõ nó dẫn tới điểm nào của map này.
    /// </summary>
    public sealed class SpawnPoint
    {
        public string Id { get; set; } = string.Empty;

        public float X { get; set; }

        public float Y { get; set; }
    }

    /// <summary>
    /// Một vùng chữ nhật world mà BƯỚC VÀO là sang map khác. (X, Y) là TÂM của vùng.
    ///
    /// Điểm đến ghi bằng TÊN chứ không phải toạ độ: file map này không được phép biết toạ độ bên trong
    /// map kia — vẽ lại map kia là mọi cổng trỏ tới nó phải sửa theo, mà không có gì nhắc.
    /// </summary>
    public sealed class Portal
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }

        public int ToMapId { get; set; }

        /// <summary>Id điểm spawn ở map đích. Không có điểm nào mang tên này thì về điểm mặc định.</summary>
        public string ToSpawnId { get; set; } = string.Empty;
    }
}
