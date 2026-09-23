using System.Net;
using System.Net.Sockets;
using System.Text;
using MMORPG.GameServer;
using MMORPG.GameServer.Boot;
using MMORPG.GameServer.Config;
using MMORPG.GameServer.World;
using MMORPG.ServerCore;

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
