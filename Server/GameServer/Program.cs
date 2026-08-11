using System.Net;
using System.Net.Sockets;
using MMORPG.GameServer;
using MMORPG.GameServer.Net;

const int PORT = 7778;

var listener = new TcpListener(IPAddress.Any, PORT);
listener.Start();
Console.WriteLine($"[GameServer] Lắng nghe trên 0.0.0.0:{PORT}");

TcpDispatcher.RegisterAll();

// Ctrl+C để dừng sạch thay vì kill process
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // chặn hành vi kill mặc định
    cts.Cancel();
};

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
    Console.WriteLine("[GameServer] Đã dừng.");
}
