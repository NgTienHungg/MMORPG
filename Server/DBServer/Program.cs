using System.Net;
using System.Net.Sockets;
using System.Text;
using MMORPG.DBServer;
using MMORPG.DBServer.Data;
using MMORPG.DBServer.Handlers;
using MMORPG.DBServer.Net;
using MMORPG.DBServer.Repositories;
using MMORPG.ServerCore;

Console.OutputEncoding = Encoding.UTF8;

const int PORT = 7779;
const string DB_FILE = "mmorpg.db";

var database = new Database(DB_FILE);
await database.InitAsync();
await Migrator.MigrateAsync(database);

ServerMetaDbHandler.Repository = new ServerMetaRepository(database);
DbDispatcher.RegisterAll();

// CHỈ loopback. DBServer không bao giờ được phơi ra ngoài máy — nó không có
// xác thực, ai nối được là đọc/ghi được toàn bộ dữ liệu người chơi.
var listener = new TcpListener(IPAddress.Loopback, PORT);
listener.Start();
Log.Info($"Lắng nghe trên {$"127.0.0.1:{PORT}".Green()}");
Log.Info($"DB File {Path.GetFullPath(DB_FILE).Green()}");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    while (!cts.IsCancellationRequested)
    {
        TcpClient tcpClient = await listener.AcceptTcpClientAsync(cts.Token);
        var session = new DbSession(tcpClient);
        _ = session.RunAsync(cts.Token);
    }
}
catch (OperationCanceledException)
{
}
finally
{
    listener.Stop();
    Log.Info("Đã dừng.");
}
