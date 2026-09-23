using MMORPG.Shared.World;
using Newtonsoft.Json;

namespace MMORPG.GameServer.Config
{
    /// <summary>
    /// Bản đối chiếu 1-1 với <c>Config/game.json</c> — tham số vận hành, chỉ server đọc.
    ///
    /// Nằm ở GameServer chứ không ở Shared vì khối <see cref="Server"/> chỉ server được biết: đưa
    /// nó sang Shared là đưa cả bán kính AOI vào DLL của client. Client chỉ nhận phần
    /// <see cref="World"/>, qua <c>EnterWorldResponse</c>.
    /// </summary>
    public sealed class GameConfigData : IConfigFile
    {
        /// <summary>Phiên bản schema, nằm trong file.</summary>
        public int Version { get; set; } = 1;

        /// <summary>Luật thế giới — phần duy nhất của file này client được biết.</summary>
        public WorldConfig World { get; set; } = new();

        /// <summary>Những con số chỉ server dùng.</summary>
        public ServerConfigData Server { get; set; } = new();

        /// <summary>Luôn 1: file này không có mảng nào, và mọi trường đều có mặc định hợp lệ.</summary>
        [JsonIgnore] public int RowCount => 1;
    }

    public sealed class ServerConfigData
    {
        /// <summary>Map của nhân vật mới tạo.</summary>
        public int StartingMapId { get; set; } = 1;

        /// <summary>Lớp nhân vật của nhân vật mới tạo.</summary>
        public int DefaultClassId { get; set; } = 1;

        /// <summary>
        /// Bán kính tầm nhìn theo trục X. Phải lớn hơn nửa bề RỘNG màn hình, nếu không người chơi
        /// thấy entity hiện ra giữa khung hình thay vì ở mép.
        /// </summary>
        public float AoiRadiusX { get; set; } = 24f;
    }
}
