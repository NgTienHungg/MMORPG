using System.Net;
using System.Net.Sockets;
using System.Text;
using MMORPG.GameServer;
using MMORPG.GameServer.Boot;
using MMORPG.GameServer.Config;
using MMORPG.GameServer.World;
using MMORPG.ServerCore;
using MMORPG.Shared.World.Character;

Console.OutputEncoding = Encoding.UTF8;

// 7777 nằm trong dải cổng Windows đã dành riêng cho Hyper-V/WSL trên máy này
// (`netsh int ipv4 show excludedportrange protocol=tcp`) — bind vào đó là SocketException 10013.
const int port = 7778;
const int dbPort = 7779;

// Toàn bộ danh sách service nằm trong ServerBootstrap. File này chỉ còn lo VÒNG ĐỜI CỦA PROCESS:
// mở cổng, nhận kết nối, nghe phím, tắt sạch.
try
{
    ServerBootstrap.Build("127.0.0.1", dbPort);
}
catch (Exception ex)
{
    // Biên của process là chỗ duy nhất được bắt Exception trần: ở đây không còn ai phía trên để xử lý
    // tiếp. Và nguyên nhân hay gặp nhất — một file config hoặc file map gõ sai — cần hiện ở dòng đầu
    // kèm tên file, chứ không nằm sau mười dòng stack của Newtonsoft.
    Log.Error(ex, "Không boot được, server dừng.");

    // Dọn những service đã kịp dựng trước khi hỏng: DbClient đang giữ một kết nối mở tới DBServer.
    await ServerServices.ShutdownAsync();

    return 1;
}

var config = ServerServices.Get<ConfigService>();
var worldService = ServerServices.Get<WorldService>();
var inventoryService = ServerServices.Get<InventoryService>();
var gameLoop = ServerServices.Get<GameLoop>();

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

// Id của "Bình máu nhỏ" trong items.json. Hằng số CỦA PHÍM THỬ, không phải của game — ngày có quái
// rơi đồ thì phím này biến mất cùng nó.
const int POTION_TEMPLATE_ID = 1;

// ĐẶT KHỐI NÀY TRƯỚC vòng `while (!cts.IsCancellationRequested) { ... AcceptTcpClientAsync ... }`.
//
// Đây là chỗ dễ sai nhất của cả bước, và sai thì KHÔNG có lỗi biên dịch: đặt nó sau vòng accept thì
// luồng phím chỉ khởi động lúc server đang tắt, tức là phím R không bao giờ có tác dụng — mà triệu
// chứng lại giống hệt "hot reload chưa chạy".
//
// Vòng đọc phím nằm ở LUỒNG RIÊNG, không trộn vào vòng accept. Console.ReadKey chặn cả luồng, nên
// đặt chung là không ai vào được game cho tới khi bạn gõ một phím.
//
// Thread chứ không Task.Run: đây là blocking I/O, không phải việc CPU. Nhét nó vào thread pool là
// chiếm một worker suốt đời process. IsBackground = true để nó không giữ process sống lúc thoát.
var console = new Thread(() =>
{
    // Không có bàn phím thì không có gì để đọc: chạy qua dịch vụ Windows, qua Docker, hay đơn giản
    // là `MMORPG.GameServer.exe < nul`. ReadKey trong hoàn cảnh đó ném InvalidOperationException, và
    // vì nó ở luồng riêng nên exception ấy KHÔNG ai bắt — .NET kết luận process phải chết. Server
    // tắt ngóm vì một phím tiện lợi lúc dev là cái giá không đáng trả.
    if (Console.IsInputRedirected)
    {
        Log.Info("stdin bị chuyển hướng — tắt phím điều khiển (R/H/K/J).");
        return;
    }

    while (!cts.IsCancellationRequested)
    {
        switch (Console.ReadKey(intercept: true).Key)
        {
            case ConsoleKey.R:
                config.Load();
                break;

            // Ba phím thử, chạy cùng vòng lặp này.
            case ConsoleKey.H:
                worldService.EnqueueForceAll(ActionState.Hurt);
                break;

            case ConsoleKey.K:
                worldService.EnqueueForceAll(ActionState.Die);
                break;

            case ConsoleKey.J:
                worldService.EnqueueReviveAll();
                break;

            // Nguồn item DUY NHẤT của Phase 13. Quái và đồ rơi dưới đất là Phase 15.
            case ConsoleKey.G:
                inventoryService.EnqueueGrantAll(POTION_TEMPLATE_ID, 3);
                break;
        }
    }
})
{
    IsBackground = true,
};

console.Start();

#endregion

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

    // Đóng mọi service theo chiều ngược thứ tự đăng ký. Thay cho `await using var dbClient` trước
    // đây: nay sổ giữ service thì sổ cũng giữ trách nhiệm đóng chúng.
    await ServerServices.ShutdownAsync();

    Log.Info("Đã dừng.");
}

// 0 = dừng bình thường. Mã thoát phải có ở MỌI đường ra vì nhánh boot hỏng trả 1.
return 0;
