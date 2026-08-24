using System.Collections.Generic;
using Newtonsoft.Json;

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
    /// Mọi trường khai báo tên tường minh bằng [JsonProperty]: đổi tên property C# là chuyện gõ code,
    /// đổi tên trường trong file là ĐỔI ĐỊNH DẠNG — không được để một thao tác Rename trong IDE làm cả
    /// hai cùng lúc.
    /// </summary>
    public sealed class MapDefinition
    {
        /// <summary>Dành cho người mở file ra đọc. JSON chuẩn không có cú pháp chú thích nên nó là một trường thật.</summary>
        [JsonProperty("_comment")]
        public string Comment { get; set; } = string.Empty;

        [JsonProperty("version")]
        public int Version { get; set; }

        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        // Cho phép null có chủ đích: file thiếu trường thì Newtonsoft để null, và MapFile.Parse phải
        // nói ra bằng một thông điệp đọc được — thay vì để NullReferenceException nổ ở đâu đó xa hơn.
        [JsonProperty("origin")]
        public CellPoint? Origin { get; set; }

        [JsonProperty("spawns")]
        public List<SpawnPoint>? Spawns { get; set; }

        /// <summary>Lưới ô, mỗi phần tử là MỘT HÀNG. Hàng đầu là mép TRÊN map — đọc file như nhìn bản vẽ.</summary>
        [JsonProperty("cells")]
        public List<string>? Cells { get; set; }
    }

    /// <summary>Một điểm theo toạ độ Ô (số nguyên). Dùng cho origin.</summary>
    public sealed class CellPoint
    {
        [JsonProperty("x")]
        public int X { get; set; }

        [JsonProperty("y")]
        public int Y { get; set; }
    }

    /// <summary>
    /// Một chỗ người chơi có thể xuất hiện, toạ độ WORLD. Có <see cref="Id"/> vì map cần trỏ tới nhau
    /// được bằng tên: một cổng ở map khác phải nói rõ nó dẫn tới điểm nào của map này.
    /// </summary>
    public sealed class SpawnPoint
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("x")]
        public float X { get; set; }

        [JsonProperty("y")]
        public float Y { get; set; }
    }
}