using MMORPG.Shared.World;

namespace MMORPG.GameServer
{
    /// <summary>
    /// Bản đối chiếu 1-1 với Config/game.json. Nằm ở GameServer chứ không ở Shared vì khối Server chỉ
    /// server được biết — đưa nó sang Shared là đưa cả bán kính AOI vào DLL của client.
    /// </summary>
    public sealed class GameConfigData
    {
        public int Version { get; set; } = 1;

        public WorldConfig World { get; set; } = new();

        public ServerConfigData Server { get; set; } = new();
    }

    public sealed class ServerConfigData
    {
        public int StartingMapId { get; set; } = 1;

        public int DefaultClassId { get; set; } = 1;

        /// <summary>Bán kính tầm nhìn theo trục X. Xem Phase 11 để biết vì sao nó phải lớn hơn nửa bề RỘNG màn hình.</summary>
        public float AoiRadiusX { get; set; } = 24f;
    }
}
