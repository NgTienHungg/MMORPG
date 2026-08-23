using System.Net;
using System.Net.Sockets;
using System.Text;
using MMORPG.GameServer;
using MMORPG.GameServer.Auth;
using MMORPG.GameServer.Db;
using MMORPG.GameServer.Handlers;
using MMORPG.GameServer.Net;
using MMORPG.GameServer.World;
using MMORPG.ServerCore;
using MMORPG.Shared.World;

Console.OutputEncoding = Encoding.UTF8;

// 7777 nằm trong dải cổng Windows đã dành riêng cho Hyper-V/WSL trên máy này
// (`netsh int ipv4 show excludedportrange protocol=tcp`) — bind vào đó là SocketException 10013.
const int PORT = 7778;
const int DB_PORT = 7779;

await using var dbClient = new DbClient("127.0.0.1", DB_PORT);
dbClient.Start();

SystemHandler.DbClient = dbClient;
AuthHandler.AuthService = new AuthService(dbClient, new LoginRateLimiter());

var worldService = new WorldService();
CharacterHandler.CharacterService = new CharacterService(dbClient, worldService);

TcpDispatcher.RegisterAll();

var listener = new TcpListener(IPAddress.Any, PORT);
listener.Start();
Log.Info($"Lắng nghe trên {$"0.0.0.0:{PORT}".Green()}");

// Ctrl+C để dừng sạch thay vì kill process
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // chặn hành vi kill mặc định
    cts.Cancel();
};

// Console điều khiển — nguồn phát TẠM cho Hurt/Die tới khi có hệ thống sát thương ở Phase 14.
// Console.ReadKey CHẶN luồng gọi nó, nên phải có luồng riêng: đặt trong vòng accept là treo cả server.
_ = Task.Run(() =>
{
    while (!cts.IsCancellationRequested)
    {
        switch (Console.ReadKey(intercept: true).Key)
        {
            case ConsoleKey.H:
                // Không truyền thời lượng: mỗi entity tra bảng của lớp mình.
                worldService.EnqueueForceAll(ActionState.Hurt);
                Log.Info($"[thử] Toàn map {"HURT".Yellow()}");
                break;

            case ConsoleKey.K:
                worldService.EnqueueForceAll(ActionState.Die);
                Log.Info($"[thử] Toàn map {"DIE".Red()}");
                break;

            case ConsoleKey.J:
                worldService.EnqueueReviveAll();
                Log.Info($"[thử] Toàn map {"hồi sinh".Green()}");
                break;
        }
    }
});

// run game loop
var gameLoop = new GameLoop(worldService);
_ = gameLoop.RunAsync(cts.Token);

try
{
    while (!cts.IsCancellationRequested)
    {
        TcpClient tcpClient = await listener.AcceptTcpClientAsync(cts.Token);

        // Mỗi kết nối chạy độc lập. KHÔNG await ở đây — await là chỉ phục vụ được 1 client.
        var session = new ClientSession(tcpClient);
        _ = session.RunAsync(cts.Token);
    }
}
catch (OperationCanceledException)
{
    // dừng theo yêu cầu, không phải lỗi
}
finally
{
    listener.Stop();
    Log.Info("Đã dừng.");
}