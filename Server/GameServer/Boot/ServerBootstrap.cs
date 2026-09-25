using MMORPG.GameServer.Auth;
using MMORPG.GameServer.Config;
using MMORPG.GameServer.Db;
using MMORPG.GameServer.Net;
using MMORPG.GameServer.World;

namespace MMORPG.GameServer.Boot
{
    /// <summary>
    /// Chỗ duy nhất biết server có những gì và ai cần ai — đối ứng của
    /// <c>GameLifetimeScope.Configure</c> bên client. Tách khỏi Program.cs vì Program.cs lo vòng đời
    /// của process (mở cổng, bắt Ctrl+C, vòng accept), còn danh sách service là chuyện khác hẳn.
    ///
    /// <b>Thứ tự các dòng ở đây là thứ tự phụ thuộc, không phải sở thích.</b> Đặt sai thì một service
    /// nhận vào null và chết ở lần dùng đầu tiên: không có container nào tự giải phụ thuộc hộ. Đổi
    /// lại, đọc từ trên xuống là biết hết.
    /// </summary>
    public static class ServerBootstrap
    {
        /// <summary>
        /// Dựng và đăng ký mọi service, rồi đóng sổ. Gọi đúng một lần, trước khi mở cổng lắng nghe.
        /// </summary>
        public static void Build(string dbHost, int dbPort)
        {
            // Hạ tầng DB trước: mọi thứ nghiệp vụ đều hỏi nó.
            var dbClient = ServerServices.Register(new DbClient(dbHost, dbPort));
            dbClient.Start();

            // Config đọc TRƯỚC mọi thứ thuộc về world: MapRegistry và WorldService đều cần số từ nó.
            var config = ServerServices.Register(new ConfigService());

            var maps = ServerServices.Register(new MapRegistry(config));
            var inventoryService = ServerServices.Register(new InventoryService(dbClient));
            var worldService = ServerServices.Register(new WorldService(maps, config, inventoryService));

            ServerServices.Register(new AuthService(dbClient, new LoginRateLimiter()));
            ServerServices.Register(new CharacterService(dbClient, worldService, maps, config, inventoryService));

            // GameLoop không phải service ai đó gọi tới, nhưng vẫn đăng ký: Program.cs cần nó, và
            // "mọi thứ sống lâu bằng process đều nằm trong một sổ" là luật dễ theo hơn "trừ cái này".
            ServerServices.Register(new GameLoop(worldService));

            ServerServices.Seal();

            // Quét assembly tìm [TcpHandler] — sau Seal() vì handler chạm tới ServerServices ngay
            // khi gói tin đầu tiên tới, và tới lúc đó sổ phải đã đóng.
            TcpDispatcher.RegisterAll();
        }
    }
}
