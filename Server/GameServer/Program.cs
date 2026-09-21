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
using MMORPG.Shared.World.Character;

Console.OutputEncoding = Encoding.UTF8;

// 7777 nằm trong dải cổng Windows đã dành riêng cho Hyper-V/WSL trên máy này
// (`netsh int ipv4 show excludedportrange protocol=tcp`) — bind vào đó là SocketException 10013.
const int port = 7778;
const int dbPort = 7779;

await using var dbClient = new DbClient("127.0.0.1", dbPort);
dbClient.Start();

SystemHandler.DbClient = dbClient;
AuthHandler.AuthService = new AuthService(dbClient, new LoginRateLimiter());

// Config đọc TRƯỚC mọi thứ khác: MapRegistry và WorldService đều cần số từ nó.
var config = new ConfigService();
var maps = new MapRegistry(config);
var worldService = new WorldService(maps, config);
CharacterHandler.CharacterService = new CharacterService(dbClient, worldService, maps, config);

TcpDispatcher.RegisterAll();

var listener = new TcpListener(IPAddress.Any, port);
listener.Start();
Log.Info($"Lắng nghe trên {$"0.0.0.0:{port}".Green()}");

// Ctrl+C để dừng sạch thay vì kill process
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // chặn hành vi kill mặc định
    cts.Cancel();
};

//----------------------------------------------------------------------------------------------------

#region === Console Key ===

// ĐẶT KHỐI NÀY TRƯỚC vòng `while (!cts.IsCancellationRequested) { ... AcceptTcpClientAsync ... }`.
//
// Đây là chỗ dễ sai nhất của cả bước, và sai thì KHÔNG có lỗi biên dịch: đặt nó sau vòng accept thì
// luồng phím chỉ khởi động lúc server đang tắt, tức là phím R không bao giờ có tác dụng — mà triệu
// chứng lại giống hệt "hot reload chưa chạy".
//
// Vòng đọc phím nằm ở LUỒNG RIÊNG, không trộn vào vòng accept. Console.ReadKey chặn cả luồng, nên
// đặt chung là không ai vào được game cho tới khi bạn gõ một phím — đó là lý do đoạn code cũ ở đây
// từng bị comment lại.
//
// Thread chứ không Task.Run: đây là blocking I/O, không phải việc CPU. Nhét nó vào thread pool là
// chiếm một worker suốt đời process. IsBackground = true để nó không giữ process sống lúc thoát.
var console = new Thread(() =>
{
    while (!cts.IsCancellationRequested)
    {
        switch (Console.ReadKey(intercept: true).Key)
        {
            case ConsoleKey.R:
                config.Load();
                break;

            // Ba phím thử của Phase 9, sống lại cùng vòng lặp này.
            case ConsoleKey.H:
                worldService.EnqueueForceAll(ActionState.Hurt);
                break;

            case ConsoleKey.K:
                worldService.EnqueueForceAll(ActionState.Die);
                break;

            case ConsoleKey.J:
                worldService.EnqueueReviveAll();
                break;
        }
    }
})
{
    IsBackground = true,
};

console.Start();

#endregion

//----------------------------------------------------------------------------------------------------

#region === Game Loop ===

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

#endregion
