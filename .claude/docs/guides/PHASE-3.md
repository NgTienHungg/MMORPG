# PHASE 3 — DBServer & tầng DAL: dữ liệu sống lâu hơn process

> **Kết quả cuối Phase 3:** ba process chạy cùng lúc. Bấm nút trong Unity → GameServer hỏi DBServer qua TCP nội bộ
> → DBServer đọc SQLite → giá trị đi ngược về tới UI. Tắt cả ba, mở lại, giá trị vẫn còn.
>
> **Điều kiện:** xong [`PHASE-2.md`](PHASE-2.md) tới CHECKPOINT C.
>
> **Khái niệm mới quan trọng nhất:** một kết nối duy nhất phải phục vụ hàng nghìn request chạy song song,
> và response về **không theo thứ tự**. Đây là lần đầu bạn cần *request correlation* — và là lý do
> đường ống Phase 2 không bê nguyên sang dùng được.

---

## Vì sao DB phải là process riêng

Câu hỏi hợp lý: GameServer mở thẳng file SQLite có phải gọn hơn không? Ngắn hạn thì có. Đây là những gì bạn mua bằng
sự phức tạp đó:

| Lý do | Cụ thể |
|-------|--------|
| **Game loop không được chờ đĩa** | Từ Phase 6, GameServer chạy tick cố định 20–30 lần/giây. Một query SQLite chậm 50ms là **mất 1–2 tick** cho *toàn bộ* người chơi. Tách process biến "chờ đĩa" thành "chờ mạng bất đồng bộ" — vẫn chờ, nhưng chờ mà không giữ luồng |
| **Nhiều GameServer, một DB** | Map 1 và map 2 chạy 2 process khác nhau (Phase 8+) nhưng cùng một túi đồ của người chơi. Hai process cùng mở một file SQLite = hỏng dữ liệu |
| **Chuỗi tin cậy** | GameServer là thứ duy nhất phơi ra Internet. Nó không giữ mật khẩu DB, không biết schema, chỉ biết gửi `DbCmd`. Chiếm được GameServer vẫn không chiếm được DB |
| **Ranh giới ép bạn thiết kế đúng** | Không thể "tiện tay" gọi `SELECT` giữa vòng lặp chiến đấu, vì cú gọi đó là `await` qua mạng. Ranh giới vật lý giữ cho tầng logic sạch |

**Cái giá phải trả, nói thẳng:** mỗi lần đọc DB giờ tốn thêm ~0.2–1ms round-trip nội bộ, thêm một process phải khởi động
đúng thứ tự, thêm một chỗ có thể chết. Với dự án 1 người thì đây là chi phí thật. Ta chấp nhận vì Phase 6 trở đi
sẽ chứng minh nó đáng — và vì bài học "tách tầng" chính là thứ cần học.

`vo-lam-genz` cũng đúng chuỗi này (`GameServer` ↔ `GameDBServer`). Ta chép **quyết định kiến trúc**, không chép code.

---

## Luồng sẽ dựng

```
Unity Client
 └─► NetCmd.ServerInfo
      │ TCP 7778
      ▼
GameServer                                   ┌── DbClient ────────────────┐
 └─► [TcpHandler(ServerInfo)] async           │ _pending: id → TCS<byte[]> │
      └─► await _db.CallAsync(               │ 1 kết nối, N request bay   │
              DbCmd.ServerMetaGet, req)  ────┤ song song, về không thứ tự │
                                             └──────────┬─────────────────┘
                                                        │ TCP 7779 (chỉ 127.0.0.1)
                                                        ▼
                                              DBServer
                                               └─► [DbHandler(ServerMetaGet)] async
                                                    └─► ServerMetaRepository
                                                         └─► SQLite (WAL)
```

---

## Vấn đề mới: response về không theo thứ tự

Ở Phase 2, mỗi client có **một** kết nối riêng và gần như luôn chỉ có 1 request đang bay. Response về, cứ nhìn `cmd`
là biết nó trả lời cho cái gì.

Đường GameServer ↔ DBServer khác hẳn: **một** kết nối duy nhất gánh request của **mọi** session. 100 người cùng đăng
nhập → 100 `DbCmd.AccountGetByName` bay đi trên cùng một socket. Response thứ nhất về — nó là của ai?

```
GameServer gửi:   [AccountGetByName "an"]  [AccountGetByName "binh"]  [AccountGetByName "cuong"]
DBServer trả về:  [Account binh]  [Account cuong]  [Account an]
                        ▲ nhanh nhất về trước — DB không hứa giữ thứ tự
```

Chỉ có `cmd` thì không phân biệt được. Cần **request id**: mỗi request mang một số duy nhất, response mang lại đúng
số đó, bên gửi tra bảng để biết trả cho ai.

> **Vì sao đường client không cần?** Vì hiện tại mỗi client chỉ có 1 request cùng loại đang bay tại một thời điểm.
> Ngày nào client cần bắn 2 request cùng `cmd` song song và phân biệt được response, ngày đó đường client cũng phải
> có request id. Đừng thêm trước — thêm khi có lý do thật (đây chính là lý do thật, ở đúng chỗ cần).

---

## Bước 1 — Shared: `DbCmd` và khung gói nội bộ

**File mới:** `Server/Shared/Db/DbCmd.cs`

```csharp
namespace MMORPG.Shared.Db
{
    /// <summary>
    /// Mã lệnh của protocol nội bộ GameServer ↔ DBServer.
    ///
    /// Dải 1000+ theo ROADMAP.md §2. Client KHÔNG BAO GIỜ thấy enum này — nó nằm trong
    /// MMORPG.Shared.dll mà Unity cũng nạp, nhưng không có đường nào để client gửi một DbCmd
    /// tới GameServer và được xử lý: hai dispatcher là hai bảng tra hoàn toàn tách biệt.
    /// </summary>
    public enum DbCmd
    {
        None = 0,

        /// <summary>
        /// Kiểm tra DBServer còn sống và query được không.
        /// Request: <see cref="Dto.Db.DbPingRequest"/> · Response: <see cref="Dto.Db.DbPingResponse"/>
        /// GameServer chủ động gửi.
        /// </summary>
        Ping = 1000,

        /// <summary>
        /// Đọc một dòng trong bảng <c>server_meta</c>.
        /// Request: <see cref="Dto.Db.ServerMetaGetRequest"/> · Response: <see cref="Dto.Db.ServerMetaGetResponse"/>
        /// </summary>
        ServerMetaGet = 1001,

        /// <summary>
        /// Ghi (thêm mới hoặc đè) một dòng trong bảng <c>server_meta</c>.
        /// Request: <see cref="Dto.Db.ServerMetaSetRequest"/> · Response: <see cref="Dto.Db.DbOkResponse"/>
        /// </summary>
        ServerMetaSet = 1002,
    }
}
```

**File mới:** `Server/Shared/Db/DbFrame.cs`

```csharp
using System;
using System.Buffers.Binary;
using System.IO;
using MMORPG.Shared.Net;

namespace MMORPG.Shared.Db
{
    /// <summary>
    /// Đường nội bộ dùng lại nguyên <see cref="PacketFrame"/> và <see cref="FrameReader"/> của đường client —
    /// chỉ quy ước thêm rằng payload bắt đầu bằng một request id.
    ///
    /// <code>
    /// ┌───────────┬───────────┬──────────────┬─────────────────────────┐
    /// │ int32 len │ int32 cmd │ int32 reqId  │ payload NetPayload      │
    /// └───────────┴───────────┴──────────────┴─────────────────────────┘
    ///  └── PacketFrame lo phần này ──┘└── DbFrame lo phần này ──┘
    /// </code>
    ///
    /// Đây là ví dụ của việc xếp tầng protocol: tầng khung (đếm byte) không cần biết
    /// tầng trên đang nói chuyện kiểu gì, nên hai protocol khác nhau vẫn dùng chung một bộ đọc khung.
    /// </summary>
    public static class DbFrame
    {
        public const int REQUEST_ID_SIZE = 4;

        /// <summary>Đóng khung hoàn chỉnh, ghi thẳng lên socket được.</summary>
        public static byte[] Encode(DbCmd cmd, int requestId, ReadOnlySpan<byte> payload)
        {
            byte[] body = new byte[REQUEST_ID_SIZE + payload.Length];
            BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(0, REQUEST_ID_SIZE), requestId);
            payload.CopyTo(body.AsSpan(REQUEST_ID_SIZE));

            return PacketFrame.Encode((int)cmd, body);
        }

        /// <summary>Tách request id ra khỏi payload mà <see cref="FrameReader.TryRead"/> vừa trả về.</summary>
        public static byte[] Decode(byte[] framePayload, out int requestId)
        {
            if (framePayload == null || framePayload.Length < REQUEST_ID_SIZE)
                throw new InvalidDataException("Gói nội bộ thiếu request id.");

            requestId = BinaryPrimitives.ReadInt32LittleEndian(framePayload.AsSpan(0, REQUEST_ID_SIZE));

            byte[] payload = new byte[framePayload.Length - REQUEST_ID_SIZE];
            Buffer.BlockCopy(framePayload, REQUEST_ID_SIZE, payload, 0, payload.Length);
            return payload;
        }
    }
}
```

> `Decode` cấp phát một mảng mới mỗi gói. Với đường nội bộ chạy trong RAM cùng máy thì không đáng lo ở giai đoạn này;
> nếu Phase 16 đo thấy GC nhức thì đổi sang trả `ReadOnlyMemory<byte>` trỏ vào buffer gốc. **Đừng tối ưu trước khi đo.**

---

## Bước 2 — Shared: DTO của protocol DB

**File mới:** `Server/Shared/Dto/Db/DbSystemDto.cs`

```csharp
using MemoryPack;

namespace MMORPG.Shared.Dto.Db
{
    /// <summary>Dùng cho mọi DbCmd không có tham số.</summary>
    [MemoryPackable]
    public partial class DbEmptyRequest
    {
    }

    /// <summary>Dùng cho mọi DbCmd chỉ cần biết thành công hay không.</summary>
    [MemoryPackable]
    public partial class DbOkResponse
    {
        public bool Success { get; set; }

        /// <summary>Chỉ để dev đọc log. Không bao giờ đẩy chuỗi này tới client.</summary>
        public string Detail { get; set; } = string.Empty;
    }

    [MemoryPackable]
    public partial class DbPingRequest
    {
        public long SentAtMs { get; set; }
    }

    [MemoryPackable]
    public partial class DbPingResponse
    {
        public long SentAtMs { get; set; }

        /// <summary>Số bản ghi trong <c>schema_version</c> — chứng minh DB thật sự query được, không chỉ socket sống.</summary>
        public int SchemaVersion { get; set; }
    }

    [MemoryPackable]
    public partial class ServerMetaGetRequest
    {
        public string Key { get; set; } = string.Empty;
    }

    [MemoryPackable]
    public partial class ServerMetaGetResponse
    {
        public bool Found { get; set; }
        public string Value { get; set; } = string.Empty;
    }

    [MemoryPackable]
    public partial class ServerMetaSetRequest
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}
```

> **Vì sao DTO của DB nằm trong `Shared` chứ không phải trong `DBServer`:** vì cả hai đầu dây đều phải hiểu nó.
> Đúng lý lẽ của golden rule #4 — contract có 1 nguồn. Việc Unity cũng nạp mấy class này chỉ tốn vài KB DLL
> và không mở ra đường tấn công nào: client không có cách nào khiến `TcpDispatcher` gọi một `[DbHandler]`.

---

## Bước 3 — DBServer: dispatcher bất đồng bộ

Cấu trúc giống `TcpDispatcher` của Phase 2, khác một điểm: handler ở đây **`async`**. Truy cập đĩa là I/O — bắt luồng
đứng chờ nó là lãng phí đúng thứ đang khan hiếm.

**File mới:** `Server/DBServer/Net/DbRequest.cs`

```csharp
using MemoryPack;
using MMORPG.Shared.Db;
using MMORPG.Shared.Net;

namespace MMORPG.DBServer.Net
{
    /// <summary>Một request đến từ GameServer.</summary>
    public readonly struct DbRequest
    {
        public DbCmd Cmd { get; }
        private readonly byte[] _payload;

        public DbRequest(DbCmd cmd, byte[] payload)
        {
            Cmd = cmd;
            _payload = payload;
        }

        public T GetData<T>() where T : IMemoryPackable<T> => NetPayload.Deserialize<T>(_payload);
    }

    /// <summary>Kết quả xử lý. Dispatcher lo việc gắn lại request id và gửi đi.</summary>
    public readonly struct DbResult
    {
        public byte[] Payload { get; }

        private DbResult(byte[] payload) => Payload = payload;

        public static DbResult Ok<T>(T dto) where T : IMemoryPackable<T> =>
            new(NetPayload.Serialize(dto));
    }

    /// <summary>Đánh dấu một static async method là handler cho một <see cref="DbCmd"/>.</summary>
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class DbHandlerAttribute : System.Attribute
    {
        public DbCmd Command { get; }
        public DbHandlerAttribute(DbCmd command) => Command = command;
    }
}
```

> **Không có `DbResult.None`.** Đường nội bộ là request/response nghiêm ngặt: GameServer đang `await` một cái gì đó.
> Handler không trả gì = bên kia treo tới lúc timeout. Bỏ luôn khả năng đó khỏi API là cách rẻ nhất để không mắc lỗi ấy.

**File mới:** `Server/DBServer/Net/DbDispatcher.cs`

```csharp
using System.Reflection;
using MMORPG.ServerCore;
using MMORPG.Shared.Db;
using MMORPG.Shared.Dto.Db;

namespace MMORPG.DBServer.Net
{
    /// <summary>
    /// Bảng tra <see cref="DbCmd"/> → handler. Quét reflection một lần lúc khởi động.
    /// </summary>
    public static class DbDispatcher
    {
        private static readonly Dictionary<DbCmd, Func<DbRequest, Task<DbResult>>> _handlers = new();

        public static void RegisterAll()
        {
            _handlers.Clear();

            IEnumerable<MethodInfo> methods = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && (a.FullName?.StartsWith("MMORPG.") ?? false))
                .SelectMany(a => a.GetTypes())
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                .Where(m => m.GetCustomAttribute<DbHandlerAttribute>() != null);

            foreach (MethodInfo method in methods)
            {
                DbHandlerAttribute attr = method.GetCustomAttribute<DbHandlerAttribute>()!;
                string origin = $"{method.DeclaringType?.Name}.{method.Name}";

                if (method.ReturnType != typeof(Task<DbResult>) ||
                    method.GetParameters().Length != 1 ||
                    method.GetParameters()[0].ParameterType != typeof(DbRequest))
                {
                    Log.Warn($"BỎ QUA {origin.Yellow()} — sai chữ ký, phải là: static Task<DbResult> Ten(DbRequest req)");
                    continue;
                }

                var del = (Func<DbRequest, Task<DbResult>>)Delegate.CreateDelegate(
                    typeof(Func<DbRequest, Task<DbResult>>), method);

                if (!_handlers.TryAdd(attr.Command, del))
                {
                    Log.Warn($"TRÙNG {attr.Command.ToString().Yellow()} — đã có handler, bỏ qua {origin}");
                    continue;
                }

                Log.Debug($"{attr.Command.ToString().Cyan()} -> {origin}");
            }

            Log.Info($"Đăng ký {_handlers.Count.ToString().Green()} handler.");
        }

        /// <summary>
        /// Chạy handler. Mọi lỗi biến thành <see cref="DbOkResponse"/> có <c>Success = false</c>
        /// — GameServer luôn nhận được MỘT response, kể cả khi DB hỏng.
        /// </summary>
        public static async Task<DbResult> DispatchAsync(DbCmd cmd, byte[] payload)
        {
            if (!_handlers.TryGetValue(cmd, out Func<DbRequest, Task<DbResult>>? handler))
            {
                Log.Warn($"Không có handler cho {cmd.ToString().Red()}");
                return DbResult.Ok(new DbOkResponse { Success = false, Detail = $"Không có handler cho {cmd}" });
            }

            try
            {
                return await handler(new DbRequest(cmd, payload));
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Handler {cmd} ném lỗi");
                return DbResult.Ok(new DbOkResponse { Success = false, Detail = ex.Message });
            }
        }
    }
}
```

> **Điểm sống còn:** khối `catch` này **không** được phép để lọt một exception nào. Nếu handler ném mà dispatcher
> im lặng bỏ qua, GameServer sẽ `await` một `Task` không bao giờ hoàn thành — và người chơi thấy màn hình đứng
> chứ không thấy thông báo lỗi. "Luôn trả về đúng một response cho mỗi request" là hợp đồng của tầng này.
>
> Chú ý kiểu lỗi ở đây khác `TcpDispatcher`: nó **không** phân biệt lỗi nghiệp vụ với lỗi hệ thống, vì đường nội bộ
> không có "nghiệp vụ" — nghiệp vụ nằm ở GameServer. DBServer chỉ đọc/ghi.

---

## Bước 4 — DBServer: SQLite, migration, repository

Thêm package:

```bash
dotnet add Server/DBServer package Microsoft.Data.Sqlite --version 8.0.10
```

**File mới:** `Server/DBServer/Data/Database.cs`

```csharp
using Microsoft.Data.Sqlite;

namespace MMORPG.DBServer.Data
{
    /// <summary>
    /// Sở hữu chuỗi kết nối và việc mở kết nối. Mọi repository đi qua đây.
    ///
    /// Không giữ một <see cref="SqliteConnection"/> dùng chung: ADO.NET đã có connection pool,
    /// mở/đóng quanh mỗi thao tác là cách dùng đúng và tránh được việc hai request async
    /// cùng đạp lên một connection.
    /// </summary>
    public sealed class Database
    {
        private readonly string _connectionString;

        public Database(string filePath)
        {
            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = filePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared,
            }.ToString();
        }

        public async Task<SqliteConnection> OpenAsync(CancellationToken ct = default)
        {
            var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct);
            return connection;
        }

        /// <summary>
        /// Bật các PRAGMA cần thiết. Gọi một lần lúc khởi động.
        /// </summary>
        public async Task InitAsync(CancellationToken ct = default)
        {
            await using SqliteConnection connection = await OpenAsync(ct);
            await using SqliteCommand cmd = connection.CreateCommand();

            // WAL: cho phép đọc song song với một lượt ghi. Mặc định (journal DELETE) thì
            // một lượt ghi khoá toàn bộ file, mọi lượt đọc phải xếp hàng.
            // foreign_keys: SQLite mặc định TẮT ràng buộc khoá ngoại — không bật thì
            // "ON DELETE CASCADE" ở Phase 5 sẽ im lặng không chạy.
            cmd.CommandText = """
                PRAGMA journal_mode = WAL;
                PRAGMA synchronous  = NORMAL;
                PRAGMA foreign_keys = ON;
                """;
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
```

**File mới:** `Server/DBServer/Data/Migrator.cs`

```csharp
using Microsoft.Data.Sqlite;
using MMORPG.ServerCore;

namespace MMORPG.DBServer.Data
{
    /// <summary>
    /// Nâng schema từ phiên bản hiện tại lên mới nhất, chạy lúc khởi động.
    ///
    /// Quy tắc bất di bất dịch: <b>migration đã chạy thì không bao giờ sửa nữa.</b> Cần đổi gì thì
    /// thêm migration mới. Sửa migration cũ nghĩa là máy bạn (chưa chạy nó) và máy đã chạy nó
    /// cho ra hai schema khác nhau mà không có gì báo.
    /// </summary>
    public static class Migrator
    {
        private static readonly (int Version, string Sql)[] _migrations =
        {
            (1, """
                CREATE TABLE server_meta (
                    key   TEXT PRIMARY KEY NOT NULL,
                    value TEXT NOT NULL
                );

                INSERT INTO server_meta (key, value) VALUES
                    ('server_name', 'local-dev'),
                    ('created_at',  datetime('now'));
                """),
        };

        public static async Task MigrateAsync(Database database, CancellationToken ct = default)
        {
            await using SqliteConnection connection = await database.OpenAsync(ct);

            await using (SqliteCommand create = connection.CreateCommand())
            {
                create.CommandText = """
                    CREATE TABLE IF NOT EXISTS schema_version (
                        version    INTEGER PRIMARY KEY NOT NULL,
                        applied_at TEXT NOT NULL
                    );
                    """;
                await create.ExecuteNonQueryAsync(ct);
            }

            int current;
            await using (SqliteCommand read = connection.CreateCommand())
            {
                read.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_version;";
                current = Convert.ToInt32(await read.ExecuteScalarAsync(ct));
            }

            foreach ((int version, string sql) in _migrations)
            {
                if (version <= current)
                    continue;

                // Mỗi migration nằm trọn trong một transaction: hoặc lên hẳn phiên bản mới,
                // hoặc không đổi gì cả. Không có trạng thái "lên được nửa chừng".
                await using SqliteTransaction tx = (SqliteTransaction)await connection.BeginTransactionAsync(ct);

                await using (SqliteCommand apply = connection.CreateCommand())
                {
                    apply.Transaction = tx;
                    apply.CommandText = sql;
                    await apply.ExecuteNonQueryAsync(ct);
                }

                await using (SqliteCommand stamp = connection.CreateCommand())
                {
                    stamp.Transaction = tx;
                    stamp.CommandText =
                        "INSERT INTO schema_version (version, applied_at) VALUES ($v, datetime('now'));";
                    stamp.Parameters.AddWithValue("$v", version);
                    await stamp.ExecuteNonQueryAsync(ct);
                }

                await tx.CommitAsync(ct);
                Log.Info($"Áp dụng migration {version.ToString().Green()}.");
            }

            Log.Info($"Schema đang ở phiên bản {Math.Max(current, LatestVersion).ToString().Green()}.");
        }

        public static int LatestVersion => _migrations.Length == 0 ? 0 : _migrations[^1].Version;
    }
}
```

**File mới:** `Server/DBServer/Repositories/ServerMetaRepository.cs`

```csharp
using Microsoft.Data.Sqlite;
using MMORPG.DBServer.Data;

namespace MMORPG.DBServer.Repositories
{
    /// <summary>
    /// Truy cập bảng <c>server_meta</c>. Repository là nơi DUY NHẤT có chuỗi SQL —
    /// đó chính là thứ khiến Phase 15 (đổi sang MySQL) chỉ phải sửa tầng này.
    /// </summary>
    public sealed class ServerMetaRepository
    {
        private readonly Database _database;

        public ServerMetaRepository(Database database) => _database = database;

        public async Task<string?> GetAsync(string key, CancellationToken ct = default)
        {
            await using SqliteConnection connection = await _database.OpenAsync(ct);
            await using SqliteCommand cmd = connection.CreateCommand();

            // Tham số hoá, không nội suy chuỗi. Ở đây `key` do GameServer đưa nên tưởng như an toàn,
            // nhưng thói quen phải đúng từ query đầu tiên — Phase 4 sẽ có chuỗi do NGƯỜI CHƠI gõ.
            cmd.CommandText = "SELECT value FROM server_meta WHERE key = $key;";
            cmd.Parameters.AddWithValue("$key", key);

            object? value = await cmd.ExecuteScalarAsync(ct);
            return value as string;
        }

        public async Task SetAsync(string key, string value, CancellationToken ct = default)
        {
            await using SqliteConnection connection = await _database.OpenAsync(ct);
            await using SqliteCommand cmd = connection.CreateCommand();

            cmd.CommandText = """
                INSERT INTO server_meta (key, value) VALUES ($key, $value)
                ON CONFLICT(key) DO UPDATE SET value = excluded.value;
                """;
            cmd.Parameters.AddWithValue("$key", key);
            cmd.Parameters.AddWithValue("$value", value);

            await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task<int> GetSchemaVersionAsync(CancellationToken ct = default)
        {
            await using SqliteConnection connection = await _database.OpenAsync(ct);
            await using SqliteCommand cmd = connection.CreateCommand();

            cmd.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_version;";
            return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        }
    }
}
```

**File mới:** `Server/DBServer/Handlers/ServerMetaDbHandler.cs`

```csharp
using MMORPG.DBServer.Net;
using MMORPG.DBServer.Repositories;
using MMORPG.Shared.Db;
using MMORPG.Shared.Dto.Db;

namespace MMORPG.DBServer.Handlers
{
    public static class ServerMetaDbHandler
    {
        /// <summary>
        /// Handler là static (để dispatcher quét được) nhưng repository thì không —
        /// gán một lần lúc khởi động là đủ cho một process chỉ có một database.
        /// Khi nào cần nhiều nguồn dữ liệu thì đổi sang DI thật.
        /// </summary>
        public static ServerMetaRepository Repository { get; set; } = null!;

        [DbHandler(DbCmd.Ping)]
        public static async Task<DbResult> OnPing(DbRequest req)
        {
            var request = req.GetData<DbPingRequest>();

            return DbResult.Ok(new DbPingResponse
            {
                SentAtMs = request.SentAtMs,
                SchemaVersion = await Repository.GetSchemaVersionAsync(),
            });
        }

        [DbHandler(DbCmd.ServerMetaGet)]
        public static async Task<DbResult> OnGet(DbRequest req)
        {
            var request = req.GetData<ServerMetaGetRequest>();
            string? value = await Repository.GetAsync(request.Key);

            return DbResult.Ok(new ServerMetaGetResponse
            {
                Found = value != null,
                Value = value ?? string.Empty,
            });
        }

        [DbHandler(DbCmd.ServerMetaSet)]
        public static async Task<DbResult> OnSet(DbRequest req)
        {
            var request = req.GetData<ServerMetaSetRequest>();
            await Repository.SetAsync(request.Key, request.Value);

            return DbResult.Ok(new DbOkResponse { Success = true });
        }
    }
}
```

**File mới:** `Server/DBServer/DbSession.cs`

Vòng đọc/gửi giống `ClientSession` của Phase 1, khác ở chỗ mỗi gói được xử lý **song song** thay vì tuần tự:

```csharp
using System.Collections.Concurrent;
using System.Net.Sockets;
using MMORPG.DBServer.Net;
using MMORPG.ServerCore;
using MMORPG.Shared.Db;
using MMORPG.Shared.Net;

namespace MMORPG.DBServer
{
    /// <summary>Một kết nối từ GameServer.</summary>
    public sealed class DbSession
    {
        private static int _nextId;

        private readonly TcpClient _tcpClient;
        private readonly NetworkStream _stream;
        private readonly FrameReader _frameReader = new();

        private readonly ConcurrentQueue<byte[]> _sendQueue = new();
        private readonly SemaphoreSlim _sendSignal = new(0);

        public int Id { get; }

        public DbSession(TcpClient tcpClient)
        {
            _tcpClient = tcpClient;
            _tcpClient.NoDelay = true;
            _stream = tcpClient.GetStream();
            Id = Interlocked.Increment(ref _nextId);
        }

        public async Task RunAsync(CancellationToken ct)
        {
            Log.Info($"#{Id} GameServer kết nối từ {$"{_tcpClient.Client.RemoteEndPoint}".Green()}");

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            Task sendLoop = SendLoopAsync(linked.Token);

            try
            {
                byte[] buffer = new byte[8192];

                while (!linked.IsCancellationRequested)
                {
                    int read = await _stream.ReadAsync(buffer, 0, buffer.Length, linked.Token);
                    if (read == 0)
                        break;

                    _frameReader.Feed(buffer, 0, read);

                    while (_frameReader.TryRead(out int cmd, out byte[] framePayload))
                    {
                        // KHÔNG await ở đây. Query chậm của session A không được chặn
                        // việc đọc request của session B — đó là toàn bộ lý do có request id.
                        _ = ProcessAsync((DbCmd)cmd, framePayload);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
                Log.Info($"#{Id} Mất kết nối: {ex.GetType().Name}");
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                linked.Cancel();
                _sendSignal.Release();
                await Task.WhenAny(sendLoop, Task.Delay(1000, CancellationToken.None));

                _tcpClient.Dispose();
                Log.Info($"#{Id} Đóng.");
            }
        }

        private async Task ProcessAsync(DbCmd cmd, byte[] framePayload)
        {
            try
            {
                byte[] payload = DbFrame.Decode(framePayload, out int requestId);
                DbResult result = await DbDispatcher.DispatchAsync(cmd, payload);

                _sendQueue.Enqueue(DbFrame.Encode(cmd, requestId, result.Payload));
                _sendSignal.Release();
            }
            catch (Exception ex)
            {
                // Task này không ai await — exception lọt ra ngoài đây là biến mất không dấu vết.
                Log.Error(ex, $"#{Id} Lỗi xử lý {cmd}");
            }
        }

        private async Task SendLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await _sendSignal.WaitAsync(ct);

                    while (_sendQueue.TryDequeue(out byte[]? frame))
                        await _stream.WriteAsync(frame, 0, frame.Length, ct);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
            }
        }
    }
}
```

> **Vì sao `_ = ProcessAsync(...)` mà không `await`:** đây chính là chỗ request id trả công. Không có nó thì phải
> xử lý tuần tự để biết response nào của request nào — và cả DBServer tụt xuống tốc độ của query chậm nhất.
> Cái giá: exception trong một task không được await sẽ **biến mất im lặng**, nên `try/catch` bao trọn `ProcessAsync`
> không phải để cho đẹp, nó là bắt buộc.

**File:** `Server/DBServer/Program.cs` — viết lại

```csharp
using System.Net;
using System.Net.Sockets;
using System.Text;
using MMORPG.DBServer;
using MMORPG.DBServer.Data;
using MMORPG.DBServer.Handlers;
using MMORPG.DBServer.Net;
using MMORPG.DBServer.Repositories;
using MMORPG.ServerCore;

// Console Windows mặc định CP437/1252 — không có ký tự tiếng Việt, log sẽ ra "L?ng nghe".
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
Log.Info($"DB file {Path.GetFullPath(DB_FILE).Green()}");

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
```

### ✅ CHECKPOINT A — DBServer đứng một mình

```bash
dotnet run --project Server/DBServer
```

Phải thấy:
```
INFO  [Migrator] Áp dụng migration 1.
INFO  [Migrator] Schema đang ở phiên bản 1.
DEBUG [DbDispatcher] Ping -> ServerMetaDbHandler.OnPing
DEBUG [DbDispatcher] ServerMetaGet -> ServerMetaDbHandler.OnGet
DEBUG [DbDispatcher] ServerMetaSet -> ServerMetaDbHandler.OnSet
INFO  [DbDispatcher] Đăng ký 3 handler.
INFO  [Program] Lắng nghe trên 127.0.0.1:7779
INFO  [Program] DB file ...\Server\DBServer\bin\Debug\net8.0\mmorpg.db
```

`[Tag]` do `Log` tự điền từ **tên file** qua `[CallerFilePath]` — nên `Program.cs` (top-level statement)
ra `[Program]`, không phải `[DBServer]`. Ba dòng `DEBUG` chỉ hiện khi `Log.MinLevel` còn ở `Debug` (mặc định).

**Chạy lại lần thứ hai** — dòng `Áp dụng migration 1` phải **biến mất**, chỉ còn `Schema đang ở phiên bản 1`.
Nếu nó chạy lại migration mỗi lần khởi động thì `schema_version` không được ghi, xem lại `Migrator`.

Xem thẳng file DB (cài `dotnet tool install -g dotnet-script` hoặc bất kỳ trình xem SQLite nào,
[DB Browser for SQLite](https://sqlitebrowser.org/) là gọn nhất):

```sql
SELECT * FROM schema_version;
SELECT * FROM server_meta;
```

Phải có 1 dòng version và 2 dòng meta.

---

## Bước 5 — GameServer: `DbClient`

Đây là phần đáng gõ tay nhất Phase 3.

**File mới:** `Server/GameServer/Db/DbClient.cs`

```csharp
using System.Collections.Concurrent;
using System.Net.Sockets;
using MemoryPack;
using MMORPG.ServerCore;
using MMORPG.Shared.Db;
using MMORPG.Shared.Net;

namespace MMORPG.GameServer.Db
{
    /// <summary>
    /// Đầu GameServer của đường nội bộ. Biến "gửi gói rồi chờ" thành một <c>await</c> bình thường.
    ///
    /// Một kết nối duy nhất phục vụ mọi session; <see cref="_pending"/> là thứ ghép response về đúng
    /// chỗ đang chờ.
    /// </summary>
    public sealed class DbClient : IAsyncDisposable
    {
        /// <summary>Chờ lâu hơn mức này thì coi như DBServer đã chết. Query bình thường tính bằng mili giây.</summary>
        private const int TIMEOUT_MS = 5000;

        private const int RECONNECT_DELAY_MS = 1000;

        private readonly string _host;
        private readonly int _port;

        private readonly ConcurrentDictionary<int, TaskCompletionSource<byte[]>> _pending = new();
        private readonly ConcurrentQueue<byte[]> _sendQueue = new();
        private readonly SemaphoreSlim _sendSignal = new(0);

        private readonly CancellationTokenSource _cts = new();

        private int _nextRequestId;
        private volatile TcpClient? _tcpClient;
        private Task? _connectionLoop;

        public bool IsConnected => _tcpClient?.Connected == true;

        public DbClient(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public void Start() => _connectionLoop = ConnectionLoopAsync(_cts.Token);

        /// <summary>
        /// Gửi một request và chờ response tương ứng.
        /// </summary>
        /// <exception cref="DbUnavailableException">DBServer không nối được, hoặc quá <see cref="TIMEOUT_MS"/>.</exception>
        public async Task<TResponse> CallAsync<TRequest, TResponse>(DbCmd cmd, TRequest request)
            where TRequest : IMemoryPackable<TRequest>
            where TResponse : IMemoryPackable<TResponse>
        {
            if (!IsConnected)
                throw new DbUnavailableException($"Chưa nối được DBServer khi gọi {cmd}.");

            int requestId = Interlocked.Increment(ref _nextRequestId);

            // RunContinuationsAsynchronously: KHÔNG có cờ này thì phần code phía sau `await CallAsync`
            // sẽ chạy ngay trên luồng đang đọc socket, và vòng đọc đứng chờ nó xong mới đọc tiếp.
            // Một handler chậm sẽ làm tắc TOÀN BỘ đường DB. Đây là cái bẫy kinh điển của TaskCompletionSource.
            var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[requestId] = tcs;

            try
            {
                _sendQueue.Enqueue(DbFrame.Encode(cmd, requestId, NetPayload.Serialize(request)));
                _sendSignal.Release();

                using var timeout = new CancellationTokenSource(TIMEOUT_MS);
                await using (timeout.Token.Register(() => tcs.TrySetCanceled()))
                {
                    byte[] payload = await tcs.Task;
                    return NetPayload.Deserialize<TResponse>(payload);
                }
            }
            catch (TaskCanceledException)
            {
                throw new DbUnavailableException($"{cmd} không có phản hồi sau {TIMEOUT_MS}ms.");
            }
            finally
            {
                // Bắt buộc, kể cả đường thành công: thiếu dòng này thì _pending phình mãi
                // và bạn có một rò rỉ bộ nhớ chỉ lộ ra sau vài giờ chạy.
                _pending.TryRemove(requestId, out _);
            }
        }

        private async Task ConnectionLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var client = new TcpClient { NoDelay = true };
                    await client.ConnectAsync(_host, _port, ct);
                    _tcpClient = client;
                    Log.Info($"Đã nối DBServer {$"{_host}:{_port}".Green()}");

                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    Task sendLoop = SendLoopAsync(client, linked.Token);

                    await ReadLoopAsync(client, linked.Token);

                    linked.Cancel();
                    _sendSignal.Release();
                    await Task.WhenAny(sendLoop, Task.Delay(1000, CancellationToken.None));
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Log.Warn($"Mất kết nối DBServer: {ex.GetType().Name.Red()} — {ex.Message}");
                }
                finally
                {
                    _tcpClient?.Dispose();
                    _tcpClient = null;
                    FailAllPending();
                }

                if (!ct.IsCancellationRequested)
                    await Task.Delay(RECONNECT_DELAY_MS, CancellationToken.None);
            }
        }

        private async Task ReadLoopAsync(TcpClient client, CancellationToken ct)
        {
            var frameReader = new FrameReader();
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[8192];

            while (!ct.IsCancellationRequested)
            {
                int read = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
                if (read == 0)
                    throw new IOException("DBServer đóng kết nối.");

                frameReader.Feed(buffer, 0, read);

                while (frameReader.TryRead(out int _, out byte[] framePayload))
                {
                    byte[] payload = DbFrame.Decode(framePayload, out int requestId);

                    if (_pending.TryRemove(requestId, out TaskCompletionSource<byte[]>? tcs))
                        tcs.TrySetResult(payload);
                    else
                        // Response về sau khi bên gửi đã bỏ cuộc vì timeout. Không phải lỗi,
                        // nhưng thấy nhiều dòng này nghĩa là DB đang chậm hơn TIMEOUT_MS.
                        Log.Warn($"Response lạc: reqId {requestId.ToString().Yellow()} không còn ai chờ.");
                }
            }
        }

        private async Task SendLoopAsync(TcpClient client, CancellationToken ct)
        {
            NetworkStream stream = client.GetStream();

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await _sendSignal.WaitAsync(ct);

                    while (_sendQueue.TryDequeue(out byte[]? frame))
                        await stream.WriteAsync(frame, 0, frame.Length, ct);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
            }
        }

        /// <summary>
        /// Đánh trượt mọi request đang chờ khi kết nối chết. Không làm việc này thì
        /// chúng treo tới lúc timeout — 5 giây người chơi nhìn màn hình đứng mà chẳng vì lý do gì.
        /// </summary>
        private void FailAllPending()
        {
            foreach (int key in _pending.Keys)
            {
                if (_pending.TryRemove(key, out TaskCompletionSource<byte[]>? tcs))
                    tcs.TrySetException(new DbUnavailableException("Mất kết nối DBServer."));
            }
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            _sendSignal.Release();

            if (_connectionLoop != null)
                await Task.WhenAny(_connectionLoop, Task.Delay(2000, CancellationToken.None));

            _tcpClient?.Dispose();
            _cts.Dispose();
        }
    }

    /// <summary>DBServer không dùng được. Đây là lỗi HỆ THỐNG, không phải lỗi nghiệp vụ.</summary>
    public sealed class DbUnavailableException : Exception
    {
        public DbUnavailableException(string message) : base(message)
        {
        }
    }
}
```

---

## Bước 6 — GameServer: handler chuyển sang bất đồng bộ

`SystemHandler.OnPing` của Phase 2 trả `NetResult` ngay lập tức. Nhưng `OnServerInfo` giờ phải `await` DBServer,
và từ Phase 4 thì **gần như mọi handler** đều phải chờ DB. Chữ ký handler buộc phải đổi.

**Sửa** `Server/GameServer/Net/TcpDispatcher.cs`:

```csharp
        private static readonly Dictionary<NetCmd, Func<NetRequest, Task<NetResult>>> _handlers = new();
```

Trong `RegisterAll`, đổi phần kiểm tra chữ ký và tạo delegate:

```csharp
                if (method.ReturnType != typeof(Task<NetResult>) ||
                    method.GetParameters().Length != 1 ||
                    method.GetParameters()[0].ParameterType != typeof(NetRequest))
                {
                    Log.Warn($"BỎ QUA {origin.Yellow()} — sai chữ ký, phải là: static Task<NetResult> Ten(NetRequest req)");
                    continue;
                }

                var del = (Func<NetRequest, Task<NetResult>>)Delegate.CreateDelegate(
                    typeof(Func<NetRequest, Task<NetResult>>), method);
```

Và `Dispatch` thành `DispatchAsync`:

```csharp
        public static async Task DispatchAsync(ClientSession session, NetCmd cmd, byte[] payload)
        {
            if (!_handlers.TryGetValue(cmd, out Func<NetRequest, Task<NetResult>>? handler))
            {
                SendError(session, cmd, ErrorCode.UnknownCommand, $"Không có handler cho {cmd}");
                return;
            }

            NetResult result;
            try
            {
                result = await handler(new NetRequest(session, cmd, payload));
            }
            catch (System.IO.InvalidDataException ex)
            {
                SendError(session, cmd, ErrorCode.MalformedPayload, ex.Message);
                return;
            }
            catch (Db.DbUnavailableException ex)
            {
                // DB chết là lỗi hệ thống, nhưng client vẫn phải nhận được câu trả lời tử tế
                // thay vì ngồi chờ vô hạn.
                Log.Warn($"{cmd} không gọi được DB: {ex.Message.Red()}");
                SendError(session, cmd, ErrorCode.ServiceUnavailable, "Máy chủ dữ liệu tạm thời không phản hồi.");
                return;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Handler {cmd} ném lỗi");
                SendError(session, cmd, ErrorCode.InternalError, ex.Message);
                return;
            }

            if (result.Payload == null)
                return;

            NetCmd responseCmd = result.Cmd == NetCmd.None ? cmd : result.Cmd;
            session.SendRaw(responseCmd, result.Payload);
        }
```

**Thêm** vào `Server/Shared/Net/ErrorCode.cs`:

```csharp
        /// <summary>Một dịch vụ phía sau (DBServer) không phản hồi. Client nên cho người chơi thử lại.</summary>
        ServiceUnavailable = 5,
```

**Sửa** `ClientSession.ReadLoopAsync` — `await` phần dispatch:

```csharp
                while (_frameReader.TryRead(out int cmd, out byte[] payload))
                    await TcpDispatcher.DispatchAsync(this, (NetCmd)cmd, payload);
```

và bỏ hàm `HandlePacket` cũ.

> **Vì sao ở đây `await` tuần tự, còn `DbSession` thì không?** Vì hai bên có ràng buộc khác nhau.
> Gói của **một người chơi** phải xử lý đúng thứ tự họ gửi — "dùng thuốc" rồi "vứt thuốc" mà chạy song song thì
> tuỳ cái nào xong trước, có thể vứt được cả thuốc vừa dùng. `await` tuần tự cho mỗi session vừa giữ đúng thứ tự,
> vừa tạo backpressure tự nhiên: client spam thì chính client đó chậm, không ảnh hưởng ai khác.
> Đường DB thì ngược lại — request đến từ nhiều người chơi khác nhau, chẳng có thứ tự nào cần giữ.

**Sửa** `SystemHandler` — mọi handler nhận thêm `async Task<NetResult>`:

```csharp
using MMORPG.GameServer.Db;
using MMORPG.GameServer.Net;
using MMORPG.ServerCore;
using MMORPG.Shared.Db;
using MMORPG.Shared.Dto;
using MMORPG.Shared.Dto.Db;
using MMORPG.Shared.Net;

namespace MMORPG.GameServer.Handlers
{
    public static class SystemHandler
    {
        /// <summary>Gán một lần trong <c>Program.cs</c>, giống <c>ServerMetaDbHandler.Repository</c>.</summary>
        public static DbClient Db { get; set; } = null!;

        [TcpHandler(NetCmd.Ping)]
        public static Task<NetResult> OnPing(NetRequest req)
        {
            var request = req.GetData<PingRequest>();

            // Không có gì để chờ — trả Task đã hoàn thành, không tốn một lần chuyển ngữ cảnh nào.
            return Task.FromResult(NetResult.Ok(new PingResponse
            {
                ClientTimeMs = request.ClientTimeMs,
                ServerTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            }));
        }

        [TcpHandler(NetCmd.Echo)]
        public static Task<NetResult> OnEcho(NetRequest req)
        {
            var request = req.GetData<EchoRequest>();
            Log.Debug($"{req.Session.Tag} echo: \"{request.Message}\"");

            return Task.FromResult(NetResult.Ok(new EchoResponse
            {
                Message = request.Message,
                ServerTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            }));
        }

        [TcpHandler(NetCmd.ServerInfo)]
        public static async Task<NetResult> OnServerInfo(NetRequest req)
        {
            var db = await Db.CallAsync<ServerMetaGetRequest, ServerMetaGetResponse>(
                DbCmd.ServerMetaGet, new ServerMetaGetRequest { Key = "server_name" });

            return NetResult.Ok(new ServerInfoResponse
            {
                ServerName = db.Found ? db.Value : "(chưa đặt tên)",
                OnlineCount = SessionRegistry.Count,
            });
        }
    }
}
```

Nếu Phase 2 bạn chưa làm bài tập `ServerInfo`, thêm luôn bây giờ: `NetCmd.ServerInfo = 4` và

```csharp
    [MemoryPackable]
    public partial class ServerInfoResponse
    {
        public string ServerName { get; set; } = string.Empty;
        public int OnlineCount { get; set; }
    }
```

**File mới:** `Server/GameServer/SessionRegistry.cs` — đếm số session đang mở. Phase 4 sẽ dùng lại để chống login trùng.

```csharp
using System.Collections.Concurrent;

namespace MMORPG.GameServer
{
    /// <summary>Danh bạ session đang mở. Truy cập từ nhiều luồng nên phải là collection an toàn.</summary>
    public static class SessionRegistry
    {
        private static readonly ConcurrentDictionary<int, ClientSession> _sessions = new();

        public static int Count => _sessions.Count;

        public static void Add(ClientSession session) => _sessions[session.Id] = session;

        public static void Remove(ClientSession session) => _sessions.TryRemove(session.Id, out _);

        public static IReadOnlyCollection<ClientSession> All => _sessions.Values.ToList();
    }
}
```

Gọi `SessionRegistry.Add(this)` ở đầu `ClientSession.RunAsync` và `SessionRegistry.Remove(this)` trong khối `finally`.

**Sửa** `Server/GameServer/Program.cs`:

```csharp
using System.Net;
using System.Net.Sockets;
using System.Text;
using MMORPG.GameServer;
using MMORPG.GameServer.Db;
using MMORPG.GameServer.Handlers;
using MMORPG.GameServer.Net;
using MMORPG.ServerCore;

Console.OutputEncoding = Encoding.UTF8;

const int PORT = 7778;
const int DB_PORT = 7779;

await using var db = new DbClient("127.0.0.1", DB_PORT);
db.Start();

SystemHandler.Db = db;
TcpDispatcher.RegisterAll();

var listener = new TcpListener(IPAddress.Any, PORT);
listener.Start();
Log.Info($"Lắng nghe trên {$"0.0.0.0:{PORT}".Green()}");

// ... phần còn lại giữ nguyên
```

> **GameServer KHÔNG chờ nối được DBServer mới khởi động.** Nó bật listener ngay, `DbClient` tự thử lại nền.
> Nhờ vậy thứ tự bật hai process không quan trọng, và DBServer restart giữa chừng không kéo theo GameServer.
> Cái giá: request DB trong khoảng chưa nối được sẽ ném `DbUnavailableException` — đúng như thiết kế,
> và bạn sẽ thử đúng tình huống đó ở Bước 7.

---

### ✅ CHECKPOINT B — ba tầng thông nhau

Cần **hai** terminal (Rider: `Alt+F12` rồi bấm `+` để mở tab thứ hai).

Terminal 1:
```bash
dotnet run --project Server/DBServer
```

Terminal 2:
```bash
dotnet run --project Server/GameServer
```

GameServer phải in `INFO  [DbClient] Đã nối DBServer 127.0.0.1:7779`.

Trong Unity, thêm nút **ServerInfo** vào `NetworkProbe` (theo mẫu nút Echo), gửi
`_net.Send(NetCmd.ServerInfo, new EmptyRequest())` và hiện `res.ServerName`.

Bấm nút → UI phải hiện `local-dev`.

**Đổi giá trị trong DB rồi thử lại** — mở `mmorpg.db` bằng DB Browser:
```sql
UPDATE server_meta SET value = 'may-cua-hung' WHERE key = 'server_name';
```
Bấm ServerInfo lại → UI hiện `may-cua-hung`, **không cần build lại gì cả**. Đó là bằng chứng dữ liệu thật sự
đi từ đĩa lên tới màn hình.

---

## Bước 7 — Ba tình huống hỏng phải tự thử

Chạy đúng chỉ chứng minh được một nửa. Ba thử nghiệm dưới đây quan trọng ngang CHECKPOINT B:

**1. DBServer chưa bật.** Tắt terminal 1, khởi động lại GameServer, bấm ServerInfo.
- GameServer log `WARN  [DbClient] Mất kết nối DBServer: SocketException` mỗi giây.
- Unity Console hiện `ServiceUnavailable — Máy chủ dữ liệu tạm thời không phản hồi.`
- GameServer **không** chết, Ping và Echo vẫn chạy bình thường.

**2. DBServer chết giữa chừng.** Bật lại DBServer, bấm ServerInfo thấy chạy, rồi `Ctrl+C` DBServer và bấm tiếp.
- Lần bấm ngay sau đó phải trả `ServiceUnavailable` **trong vòng dưới 1 giây** — nhờ `FailAllPending`.
  Nếu bạn phải chờ đủ 5 giây thì `FailAllPending` chưa được gọi, xem lại khối `finally` của `ConnectionLoopAsync`.
- Bật DBServer lại → GameServer tự nối lại, bấm ServerInfo lại chạy. Không phải restart GameServer.

**3. Query chậm.** Thêm tạm `await Task.Delay(6000);` vào đầu `ServerMetaDbHandler.OnGet`.
- GameServer trả `ServiceUnavailable` sau đúng 5 giây (timeout).
- Vài giây sau, DBServer trả lời muộn → GameServer in `WARN  [DbClient] Response lạc: reqId N không còn ai chờ.`
- **Trong lúc chờ 6 giây đó, Ping và Echo vẫn phải trả lời tức thì.** Nếu chúng cũng đứng thì bạn đã `await`
  nhầm chỗ nào đó — nhiều khả năng là quên `RunContinuationsAsynchronously`, hoặc `DbSession` đang `await ProcessAsync`.
- Xoá dòng `Task.Delay` sau khi thử xong.

Thử nghiệm 3 là thứ đáng giá nhất Phase này. Nó là bản thu nhỏ của điều sẽ xảy ra lúc 200 người online và ổ đĩa
bị nghẽn — chỉ khác là bây giờ bạn tự gây ra được và quan sát được.

---

## Troubleshooting

| Triệu chứng | Nguyên nhân | Xử lý |
|-------------|-------------|-------|
| `[TcpDispatcher] Đăng ký 0 handler` sau khi đổi sang async | Chữ ký cũ trả `NetResult`, dispatcher mới tìm `Task<NetResult>` | Đổi hết handler sang `Task<NetResult>`; loại không cần chờ thì `Task.FromResult(...)` |
| `SQLite Error 14: unable to open database file` | Đường dẫn tương đối tính từ thư mục chạy, không phải thư mục project | Log `Path.GetFullPath(DB_FILE)` để biết nó đang mở file nào; hoặc dùng đường dẫn tuyệt đối |
| `database is locked` | Hai process cùng ghi, hoặc quên `PRAGMA journal_mode = WAL` | Kiểm tra `InitAsync` có chạy; chỉ DBServer được mở file này |
| GameServer treo vĩnh viễn khi bấm ServerInfo | Handler DBServer ném nhưng không trả response | `DbDispatcher.DispatchAsync` phải bọc `try/catch` trả về `DbOkResponse{Success=false}`, không được để lọt exception |
| Response về sai người (dữ liệu của request khác) | Request id không được gắn/đọc đúng | Cả 2 bên phải dùng `DbFrame.Encode`/`Decode`; không tự ghép byte tay |
| Ping/Echo đứng khi có 1 query chậm | Thiếu `RunContinuationsAsynchronously`, hoặc `DbSession` đang `await ProcessAsync` | Xem lại Bước 5 và Bước 4 |
| Bộ nhớ GameServer tăng dần | `_pending` không được dọn | `finally { _pending.TryRemove(...) }` phải chạy ở **mọi** đường ra, kể cả thành công |
| `Migrator` chạy lại migration mỗi lần khởi động | Bảng `schema_version` không được ghi, hoặc mỗi lần chạy lại tạo file DB mới | Kiểm tra file `.db` có được tạo lại không (xem timestamp) |
| Đổi `_migrations` cũ mà DB không đổi theo | Đúng như thiết kế — migration đã chạy không chạy lại | Thêm migration mới. Lúc dev muốn làm lại từ đầu: xoá file `.db` (và cả `.db-wal`, `.db-shm`) |
| Unity không thấy `MMORPG.Shared.Db` | Quên build lại `Shared` | `dotnet build Server/Shared` |

---

## Tự kiểm tra hiểu bài

1. Vì sao đường GameServer ↔ DBServer cần request id mà đường client ↔ GameServer thì chưa?
   Điều gì phải xảy ra để đường client cũng cần?
2. `DbSession` không `await ProcessAsync` còn `ClientSession` thì `await DispatchAsync`. Nếu đảo ngược
   hai lựa chọn đó thì mỗi bên hỏng thế nào?
3. `TaskCreationOptions.RunContinuationsAsynchronously` bảo vệ ta khỏi điều gì? Mô tả kịch bản hỏng nếu bỏ nó đi.
4. Vì sao `_pending.TryRemove` phải nằm trong `finally` chứ không phải sau `await tcs.Task`?
5. `FailAllPending` giải quyết vấn đề gì mà timeout 5 giây không giải quyết được?
6. Vì sao `DbDispatcher` biến mọi exception thành `DbOkResponse{Success=false}` thay vì để nó ném lên?
   Điều gì xảy ra ở GameServer nếu nó không làm vậy?
7. Vì sao DBServer chỉ nghe trên `IPAddress.Loopback` chứ không phải `IPAddress.Any`?
8. Quy tắc "migration đã chạy thì không sửa" bảo vệ điều gì? Cho một ví dụ hỏng cụ thể nếu vi phạm.
9. Nếu bỏ hẳn DBServer và cho GameServer mở thẳng SQLite, đoạn nào của Phase 6 (tick loop) sẽ gặp vấn đề đầu tiên?

---

**Xong Phase 3 → [PHASE-4](PHASE-4.md): đăng ký, đăng nhập, và bài học "không bao giờ tin client".**
