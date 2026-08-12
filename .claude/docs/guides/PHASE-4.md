# PHASE 4 — Đăng ký / Đăng nhập: lần đầu server phải nghi ngờ client

> **Kết quả cuối Phase 4:** UI đăng nhập trong Unity. Đăng ký tài khoản → lưu SQLite → thoát game →
> mở lại → đăng nhập được. Sai mật khẩu báo đúng lỗi. Đăng nhập lần hai ở client khác thì client cũ bị đá ra.
>
> **Điều kiện:** xong [`PHASE-3.md`](PHASE-3.md) tới CHECKPOINT B và cả 3 thử nghiệm hỏng ở Bước 7.
>
> **Bài học chính:** từ phase này trở đi, mọi thứ client gửi lên đều là **lời khai**, không phải sự thật.
> Client nói "tôi là admin" thì đó chỉ là một chuỗi byte — server phải tự chứng minh.

---

## Ba thứ tuyệt đối không được làm

Ghi ra trước khi viết dòng code nào, vì cả ba đều rất dễ "tiện tay" phạm phải:

1. **Không lưu mật khẩu.** DB không bao giờ chứa mật khẩu, kể cả mã hoá hai chiều. Chỉ chứa **hash một chiều**
   không đảo ngược được. Bạn — chủ server — cũng không được có khả năng đọc mật khẩu của người chơi.
2. **Không để client quyết định mình là ai.** Không có DTO nào mang `AccountId` từ client lên. Danh tính do
   server gán vào `ClientSession` sau khi xác thực, và chỉ đọc từ đó.
3. **Không nói cho kẻ tấn công biết chúng đoán đúng phần nào.** "Tài khoản không tồn tại" và "sai mật khẩu"
   phải trả về **cùng một thông điệp**, và tốn **cùng một khoảng thời gian**.

### Một điều thành thật về Phase này

Mật khẩu đi từ Unity tới GameServer bằng TCP **trần** — ai bắt được gói tin trên đường là đọc được. Băm ở server
không cứu được điều đó; băm chỉ bảo vệ khi *file DB* bị lộ.

Ta chấp nhận vì đang chạy localhost và mục tiêu là học kiến trúc. Thứ đúng đắn là bọc TLS quanh transport —
ghi vào Phase 16 (vận hành), và lúc đó `ITransport` của Phase 1 sẽ trả công: chỉ thêm một implementation mới,
không phần nào khác của game phải biết.

> **Đừng băm mật khẩu ở client rồi gửi hash lên.** Nghe có vẻ an toàn hơn nhưng thật ra tệ hơn: cái hash đó
> *trở thành* mật khẩu. Ai bắt được nó thì đăng nhập được mà chẳng cần biết mật khẩu gốc, và bạn mất luôn khả năng
> nâng cấp thuật toán băm ở server. Băm là việc của server.

---

## Luồng sẽ dựng

```
LoginUi  ──►  LoginPresenter  ──►  AuthApi.LoginAsync("hung", "123456")
                    ▲                     │ NetCmd.Login + LoginRequest
                    │                     ▼
              AuthNetHandler  ◄──── GameServer [TcpHandler(Login)]
              (OnLoginResult)           └─► AuthService.LoginAsync()
                                             ├─ validate định dạng
                                             ├─ RateLimiter kiểm tra
                                             ├─ await Db(AccountGetByName)  ──► DBServer ──► SQLite
                                             ├─ PasswordHasher.Verify()
                                             ├─ đá session trùng nếu có
                                             └─ session.MarkAuthenticated(accountId)
```

**`AuthService` chứa nghiệp vụ, `AuthHandler` chỉ điều phối.** Handler = giải mã → gọi service → đóng gói.
Ranh giới này là thứ giữ cho handler không phình thành god class ở Phase 10+.

---

## Bước 1 — Shared: contract của auth

**Sửa** `Server/Shared/Db/DbCmd.cs` — chia dải cho gọn trước khi nó đông:

```csharp
        #region Hệ thống (1000–1099)

        Ping = 1000,
        ServerMetaGet = 1001,
        ServerMetaSet = 1002,

        #endregion

        #region Account (1100–1199)

        /// <summary>
        /// Tìm tài khoản theo tên đăng nhập.
        /// Request: <see cref="Dto.Db.AccountGetRequest"/> · Response: <see cref="Dto.Db.AccountGetResponse"/>
        /// </summary>
        AccountGetByName = 1100,

        /// <summary>
        /// Tạo tài khoản mới. Trùng tên thì trả <c>Created = false</c>, KHÔNG ném lỗi.
        /// Request: <see cref="Dto.Db.AccountCreateRequest"/> · Response: <see cref="Dto.Db.AccountCreateResponse"/>
        /// </summary>
        AccountCreate = 1101,

        /// <summary>
        /// Ghi mốc đăng nhập gần nhất. Fire-and-forget về mặt nghiệp vụ nhưng vẫn có response.
        /// Request: <see cref="Dto.Db.AccountTouchLoginRequest"/> · Response: <see cref="Dto.Db.DbOkResponse"/>
        /// </summary>
        AccountTouchLogin = 1102,

        #endregion
```

> Cập nhật luôn `ROADMAP.md` §2: dải `DbCmd` chia `1000–1099` hệ thống · `1100–1199` account ·
> `1200–1299` character (Phase 5) · `1300+` phần sau.

**Sửa** `Server/Shared/Net/NetCmd.cs` — thêm dải auth và một lệnh hệ thống:

```csharp
        /// <summary>
        /// Server chủ động đá client ra. Chỉ server gửi, client không bao giờ gửi lệnh này.
        /// Payload: <see cref="Dto.KickedNotice"/>
        /// </summary>
        Kicked = 5,

        #endregion

        #region Auth (100–199)

        /// <summary>
        /// Tạo tài khoản mới.
        /// Request: <see cref="Dto.RegisterRequest"/> · Response: <see cref="Dto.AuthResponse"/>
        /// Client chủ động gửi.
        /// </summary>
        Register = 100,

        /// <summary>
        /// Đăng nhập.
        /// Request: <see cref="Dto.LoginRequest"/> · Response: <see cref="Dto.AuthResponse"/>
        /// Client chủ động gửi.
        /// </summary>
        Login = 101,

        /// <summary>
        /// Đăng xuất chủ động (về màn hình login mà không cắt TCP).
        /// Request: <see cref="Dto.EmptyRequest"/> · Response: <see cref="Dto.AuthResponse"/>
        /// </summary>
        Logout = 102,

        #endregion
```

**Sửa** `Server/Shared/Net/ErrorCode.cs`:

```csharp
        /// <summary>Dữ liệu client gửi lên sai định dạng (tên quá ngắn, ký tự lạ...).</summary>
        InvalidInput = 6,

        /// <summary>Tên đăng nhập đã có người dùng.</summary>
        AccountExists = 7,

        /// <summary>Sai tài khoản HOẶC sai mật khẩu — cố tình không phân biệt.</summary>
        InvalidCredentials = 8,

        /// <summary>Thử sai quá nhiều lần, phải chờ.</summary>
        TooManyAttempts = 9,

        /// <summary>Gọi lệnh cần đăng nhập nhưng session đã đăng nhập rồi (vd: Login hai lần).</summary>
        AlreadyAuthenticated = 10,
```

**File mới:** `Server/Shared/Dto/Auth/AuthDto.cs`

```csharp
using MemoryPack;

namespace MMORPG.Shared.Dto
{
    [MemoryPackable]
    public partial class RegisterRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    [MemoryPackable]
    public partial class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>
    /// Dùng chung cho Register / Login / Logout.
    ///
    /// KHÔNG chứa AccountId. Client không cần biết id của mình để làm gì cả —
    /// mọi lệnh sau đó server đều tự tra từ session. Không gửi đi thứ không ai cần
    /// là cách rẻ nhất để không ai lạm dụng nó.
    /// </summary>
    [MemoryPackable]
    public partial class AuthResponse
    {
        public bool Success { get; set; }

        /// <summary><see cref="Net.ErrorCode.None"/> khi thành công.</summary>
        public Net.ErrorCode Error { get; set; }

        /// <summary>Tên hiển thị, server trả về đúng như đã lưu (đã chuẩn hoá chữ thường).</summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Token phiên, dùng cho nối lại sau khi rớt mạng (Phase 7 sẽ dùng thật).
        /// Rỗng khi đăng nhập thất bại.
        /// </summary>
        public string SessionToken { get; set; } = string.Empty;
    }

    /// <summary>Server báo lý do đá client ra.</summary>
    [MemoryPackable]
    public partial class KickedNotice
    {
        /// <summary>Chuỗi tiếng Việt hiển thị thẳng cho người chơi.</summary>
        public string Reason { get; set; } = string.Empty;
    }
}
```

**File mới:** `Server/Shared/Dto/Db/AccountDto.cs`

```csharp
using System;
using MemoryPack;

namespace MMORPG.Shared.Dto.Db
{
    [MemoryPackable]
    public partial class AccountGetRequest
    {
        public string Username { get; set; } = string.Empty;
    }

    [MemoryPackable]
    public partial class AccountGetResponse
    {
        public bool Found { get; set; }

        public long AccountId { get; set; }
        public string Username { get; set; } = string.Empty;

        public byte[] PasswordHash { get; set; } = Array.Empty<byte>();
        public byte[] Salt { get; set; } = Array.Empty<byte>();
        public int Iterations { get; set; }

        public bool IsBanned { get; set; }
    }

    [MemoryPackable]
    public partial class AccountCreateRequest
    {
        public string Username { get; set; } = string.Empty;
        public byte[] PasswordHash { get; set; } = Array.Empty<byte>();
        public byte[] Salt { get; set; } = Array.Empty<byte>();
        public int Iterations { get; set; }
    }

    [MemoryPackable]
    public partial class AccountCreateResponse
    {
        /// <summary>false nghĩa là tên đã tồn tại — đây là kết quả bình thường, không phải lỗi.</summary>
        public bool Created { get; set; }
        public long AccountId { get; set; }
    }

    [MemoryPackable]
    public partial class AccountTouchLoginRequest
    {
        public long AccountId { get; set; }
    }
}
```

> Chú ý `AccountGetResponse` mang cả hash và salt **về GameServer**. Nghe có vẻ ngược — sao không để DBServer
> tự so sánh? Vì so sánh mật khẩu là **nghiệp vụ**, mà DBServer thì cố tình ngu: nó chỉ đọc/ghi. Giữ được ranh giới
> đó thì Phase 15 đổi sang MySQL không phải viết lại một dòng logic nào. Hash đi trên đường nội bộ loopback,
> và bản thân hash không đảo ngược được.

---

## Bước 2 — DBServer: bảng `account`

**Sửa** `Server/DBServer/Data/Migrator.cs` — **thêm** vào cuối mảng `_migrations`, không đụng migration 1:

```csharp
            (2, """
                CREATE TABLE account (
                    id            INTEGER PRIMARY KEY AUTOINCREMENT,
                    username      TEXT    NOT NULL,
                    password_hash BLOB    NOT NULL,
                    salt          BLOB    NOT NULL,
                    iterations    INTEGER NOT NULL,
                    is_banned     INTEGER NOT NULL DEFAULT 0,
                    created_at    TEXT    NOT NULL,
                    last_login_at TEXT
                );

                -- Ràng buộc UNIQUE ở DB, không phải ở code. Xem giải thích ở AccountRepository.CreateAsync.
                CREATE UNIQUE INDEX idx_account_username ON account (username);
                """),
```

Vài quyết định trong schema đáng nói:

| Cột | Vì sao vậy |
|-----|------------|
| `username` lưu **chữ thường** | Chuẩn hoá lúc ghi, không dùng `COLLATE NOCASE`. "Hung" và "hung" phải là một tài khoản, và quy tắc đó nằm ở một chỗ duy nhất (`AuthService.Normalize`) chứ không rải rác trong từng query |
| `iterations` lưu theo từng tài khoản | Vài năm nữa nâng lên 600k thì tài khoản cũ vẫn đăng nhập được bằng số vòng của chính nó, và ta có thể băm lại âm thầm lúc họ đăng nhập đúng |
| `salt` riêng mỗi tài khoản | Không có salt thì hai người cùng mật khẩu ra cùng hash — lộ một cái là lộ cả hai, và rainbow table dùng được |
| Không có cột `password` | Cố ý. Đừng thêm, kể cả để "debug" |

**File mới:** `Server/DBServer/Repositories/AccountRepository.cs`

```csharp
using Microsoft.Data.Sqlite;
using MMORPG.DBServer.Data;
using MMORPG.Shared.Dto.Db;

namespace MMORPG.DBServer.Repositories
{
    public sealed class AccountRepository
    {
        /// <summary>Mã lỗi SQLite khi vi phạm ràng buộc UNIQUE.</summary>
        private const int SQLITE_CONSTRAINT_UNIQUE = 2067;

        private readonly Database _database;

        public AccountRepository(Database database) => _database = database;

        public async Task<AccountGetResponse> GetByUsernameAsync(string username, CancellationToken ct = default)
        {
            await using SqliteConnection connection = await _database.OpenAsync(ct);
            await using SqliteCommand cmd = connection.CreateCommand();

            cmd.CommandText = """
                SELECT id, username, password_hash, salt, iterations, is_banned
                FROM account WHERE username = $username;
                """;
            cmd.Parameters.AddWithValue("$username", username);

            await using SqliteDataReader reader = await cmd.ExecuteReaderAsync(ct);

            if (!await reader.ReadAsync(ct))
                return new AccountGetResponse { Found = false };

            return new AccountGetResponse
            {
                Found = true,
                AccountId = reader.GetInt64(0),
                Username = reader.GetString(1),
                PasswordHash = (byte[])reader[2],
                Salt = (byte[])reader[3],
                Iterations = reader.GetInt32(4),
                IsBanned = reader.GetInt32(5) != 0,
            };
        }

        public async Task<AccountCreateResponse> CreateAsync(AccountCreateRequest request,
                                                             CancellationToken ct = default)
        {
            await using SqliteConnection connection = await _database.OpenAsync(ct);
            await using SqliteCommand cmd = connection.CreateCommand();

            cmd.CommandText = """
                INSERT INTO account (username, password_hash, salt, iterations, created_at)
                VALUES ($username, $hash, $salt, $iterations, datetime('now'))
                RETURNING id;
                """;
            cmd.Parameters.AddWithValue("$username", request.Username);
            cmd.Parameters.AddWithValue("$hash", request.PasswordHash);
            cmd.Parameters.AddWithValue("$salt", request.Salt);
            cmd.Parameters.AddWithValue("$iterations", request.Iterations);

            try
            {
                long id = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
                return new AccountCreateResponse { Created = true, AccountId = id };
            }
            catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == SQLITE_CONSTRAINT_UNIQUE)
            {
                // Để DB phát hiện trùng, KHÔNG "SELECT xem có chưa rồi mới INSERT".
                // Giữa hai câu lệnh đó có một khe thời gian; hai người đăng ký cùng tên
                // trong cùng một phần nghìn giây sẽ cùng thấy "chưa có" và cùng INSERT.
                // Ràng buộc UNIQUE là thứ duy nhất không có khe đó.
                return new AccountCreateResponse { Created = false };
            }
        }

        public async Task TouchLoginAsync(long accountId, CancellationToken ct = default)
        {
            await using SqliteConnection connection = await _database.OpenAsync(ct);
            await using SqliteCommand cmd = connection.CreateCommand();

            cmd.CommandText = "UPDATE account SET last_login_at = datetime('now') WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", accountId);

            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
```

> **Đây là bài học đắt nhất Bước 2.** Kiểu "kiểm tra rồi mới hành động" (check-then-act) là nguồn bug đồng thời
> phổ biến nhất trong game server, và nó **không bao giờ lộ ra khi bạn tự test một mình**. Nó chỉ xuất hiện lúc có
> người thật, và lúc đó bạn có 2 tài khoản trùng tên trong DB mà không hiểu vì sao. Quy tắc: khi DB có sẵn ràng buộc,
> hãy để DB thi hành và bắt lỗi của nó.

**File mới:** `Server/DBServer/Handlers/AccountDbHandler.cs`

```csharp
using MMORPG.DBServer.Net;
using MMORPG.DBServer.Repositories;
using MMORPG.Shared.Db;
using MMORPG.Shared.Dto.Db;

namespace MMORPG.DBServer.Handlers
{
    public static class AccountDbHandler
    {
        public static AccountRepository Repository { get; set; } = null!;

        [DbHandler(DbCmd.AccountGetByName)]
        public static async Task<DbResult> OnGetByName(DbRequest req)
        {
            var request = req.GetData<AccountGetRequest>();
            return DbResult.Ok(await Repository.GetByUsernameAsync(request.Username));
        }

        [DbHandler(DbCmd.AccountCreate)]
        public static async Task<DbResult> OnCreate(DbRequest req)
        {
            var request = req.GetData<AccountCreateRequest>();
            return DbResult.Ok(await Repository.CreateAsync(request));
        }

        [DbHandler(DbCmd.AccountTouchLogin)]
        public static async Task<DbResult> OnTouchLogin(DbRequest req)
        {
            var request = req.GetData<AccountTouchLoginRequest>();
            await Repository.TouchLoginAsync(request.AccountId);

            return DbResult.Ok(new DbOkResponse { Success = true });
        }
    }
}
```

Trong `Server/DBServer/Program.cs`, thêm trước `DbDispatcher.RegisterAll()`:

```csharp
AccountDbHandler.Repository = new AccountRepository(database);
```

---

## Bước 3 — GameServer: băm mật khẩu

**File mới:** `Server/GameServer/Auth/PasswordHasher.cs`

```csharp
using System.Security.Cryptography;
using System.Text;

namespace MMORPG.GameServer.Auth
{
    /// <summary>
    /// Băm và kiểm mật khẩu bằng PBKDF2-HMAC-SHA256.
    ///
    /// PBKDF2 được thiết kế để CHẬM một cách có kiểm soát. Đó không phải nhược điểm mà là toàn bộ mục đích:
    /// SHA256 trần băm được hàng tỉ mật khẩu mỗi giây trên một GPU tầm trung, nên nếu file DB bị lộ thì mọi
    /// mật khẩu ngắn đều đoán ra trong vài phút. Với 100.000 vòng lặp, cùng con GPU đó chỉ thử được
    /// vài chục nghìn lần mỗi giây.
    /// </summary>
    public static class PasswordHasher
    {
        /// <summary>
        /// Khuyến nghị của OWASP cho PBKDF2-SHA256 hiện là 600.000 vòng. Ta dùng 100.000 cho dự án học
        /// để đăng nhập lúc dev không phải chờ — con số này lưu theo từng tài khoản trong DB nên nâng lên
        /// lúc nào cũng được mà không làm hỏng tài khoản cũ.
        /// </summary>
        public const int DEFAULT_ITERATIONS = 100_000;

        private const int SALT_SIZE = 16;
        private const int HASH_SIZE = 32;

        public static (byte[] Hash, byte[] Salt, int Iterations) Hash(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SALT_SIZE);
            byte[] hash = Derive(password, salt, DEFAULT_ITERATIONS);

            return (hash, salt, DEFAULT_ITERATIONS);
        }

        public static bool Verify(string password, byte[] expectedHash, byte[] salt, int iterations)
        {
            if (salt.Length == 0 || iterations <= 0)
                return false;

            byte[] actual = Derive(password, salt, iterations);

            // So sánh thời gian cố định. `actual.SequenceEqual(expected)` dừng ngay khi gặp byte đầu tiên
            // khác nhau, nên thời gian chạy tiết lộ ta đã đoán đúng bao nhiêu byte đầu. Đo đủ nhiều lần
            // là dựng lại được cả hash.
            return CryptographicOperations.FixedTimeEquals(actual, expectedHash);
        }

        /// <summary>
        /// Băm giả để tiêu tốn đúng lượng thời gian như một lần kiểm thật.
        /// Dùng khi tài khoản KHÔNG tồn tại — xem <c>AuthService.LoginAsync</c>.
        /// </summary>
        public static void BurnEquivalentTime() =>
            Derive("dummy", new byte[SALT_SIZE], DEFAULT_ITERATIONS);

        private static byte[] Derive(string password, byte[] salt, int iterations) =>
            Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, HASH_SIZE);
    }
}
```

**File mới:** `Server/GameServer/Auth/AccountNameRules.cs`

```csharp
using System.Text.RegularExpressions;

namespace MMORPG.GameServer.Auth
{
    /// <summary>
    /// Luật định dạng tài khoản. Client cũng kiểm mấy luật này, nhưng chỉ để báo lỗi sớm cho đẹp —
    /// bản kiểm ở đây mới là bản có hiệu lực. Client là thứ người chơi sửa được.
    /// </summary>
    public static class AccountNameRules
    {
        public const int USERNAME_MIN = 3;
        public const int USERNAME_MAX = 16;
        public const int PASSWORD_MIN = 6;

        /// <summary>Chặn trên cho mật khẩu: PBKDF2 với chuỗi 10MB là một kiểu DoS rẻ tiền.</summary>
        public const int PASSWORD_MAX = 64;

        private static readonly Regex _usernamePattern = new("^[a-z0-9_]+$", RegexOptions.Compiled);

        /// <summary>Chuẩn hoá về dạng lưu trong DB. Gọi TRƯỚC mọi so sánh và mọi query.</summary>
        public static string Normalize(string username) =>
            (username ?? string.Empty).Trim().ToLowerInvariant();

        public static bool IsValidUsername(string normalized) =>
            normalized.Length >= USERNAME_MIN &&
            normalized.Length <= USERNAME_MAX &&
            _usernamePattern.IsMatch(normalized);

        public static bool IsValidPassword(string password) =>
            !string.IsNullOrEmpty(password) &&
            password.Length >= PASSWORD_MIN &&
            password.Length <= PASSWORD_MAX;
    }
}
```

---

## Bước 4 — GameServer: trạng thái session

**File mới:** `Server/GameServer/SessionState.cs`

```csharp
namespace MMORPG.GameServer
{
    /// <summary>
    /// Session đi một chiều qua các trạng thái này, không quay lui (trừ Logout về Connected).
    /// Giá trị tăng dần để so sánh được bằng <c>&gt;=</c> — dispatcher dựa vào đó để chặn lệnh gọi sai lúc.
    /// </summary>
    public enum SessionState
    {
        /// <summary>TCP đã nối, chưa biết là ai.</summary>
        Connected = 0,

        /// <summary>Đã đăng nhập, chưa chọn nhân vật.</summary>
        Authenticated = 1,

        /// <summary>Đã vào thế giới (Phase 5).</summary>
        InWorld = 2,
    }
}
```

**Sửa** `Server/GameServer/ClientSession.cs` — thêm phần danh tính:

```csharp
        /// <summary>Trạng thái hiện tại. Chỉ AuthService và WorldService được đổi.</summary>
        public SessionState State { get; private set; } = SessionState.Connected;

        /// <summary>0 khi chưa đăng nhập. Đây là NGUỒN DUY NHẤT cho biết session này là ai.</summary>
        public long AccountId { get; private set; }

        public string Username { get; private set; } = string.Empty;

        public void MarkAuthenticated(long accountId, string username)
        {
            AccountId = accountId;
            Username = username;
            State = SessionState.Authenticated;
        }

        public void MarkLoggedOut()
        {
            AccountId = 0;
            Username = string.Empty;
            State = SessionState.Connected;
        }

        /// <summary>Đóng kết nối từ phía server, có báo lý do cho người chơi trước.</summary>
        public void Kick(string reason)
        {
            Log.Warn($"{Tag} Đá ra: {reason.Yellow()}");
            SendData(NetCmd.Kicked, new KickedNotice { Reason = reason });

            // Cho vòng gửi kịp đẩy gói Kicked đi rồi mới cắt. Cắt ngay thì client
            // chỉ thấy mất kết nối trần và không biết vì sao.
            _ = CloseAfterFlushAsync();
        }

        private async Task CloseAfterFlushAsync()
        {
            await Task.Delay(100);
            _tcpClient.Close();
        }
```

**Sửa** `TcpHandlerAttribute` — thêm yêu cầu trạng thái:

```csharp
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class TcpHandlerAttribute : System.Attribute
    {
        public NetCmd Command { get; }

        /// <summary>Trạng thái tối thiểu để được gọi lệnh này. Mặc định: không cần đăng nhập.</summary>
        public SessionState MinState { get; set; } = SessionState.Connected;

        public TcpHandlerAttribute(NetCmd command) => Command = command;
    }
```

`TcpDispatcher` lưu thêm `MinState` cạnh delegate và kiểm trước khi gọi:

```csharp
        private readonly record struct HandlerEntry(Func<NetRequest, Task<NetResult>> Invoke, SessionState MinState);

        private static readonly Dictionary<NetCmd, HandlerEntry> _handlers = new();
```

```csharp
            if (session.State < entry.MinState)
            {
                SendError(session, cmd, ErrorCode.NotAuthenticated,
                          $"{cmd} cần trạng thái {entry.MinState}, session đang ở {session.State}");
                return;
            }
```

> **Vì sao đặt ở dispatcher chứ không phải đầu mỗi handler.** Vì bảo vệ mà phải nhớ mới có thì sớm muộn sẽ quên —
> và cái handler bạn quên chính là cái bị lợi dụng. Ở đây thì mặc định là *có bảo vệ*: một handler mới quên khai
> `MinState` sẽ chỉ quá dễ dãi ở lệnh đó, còn quên gọi hàm check thì thủng hoàn toàn. Thêm nữa, đọc `NetCmd.cs`
> và các attribute là thấy ngay toàn bộ ma trận quyền — không phải đi đọc 40 file handler.

---

## Bước 5 — GameServer: `AuthService`

**File mới:** `Server/GameServer/Auth/AuthService.cs`

```csharp
using MMORPG.GameServer.Db;
using MMORPG.ServerCore;
using MMORPG.Shared.Db;
using MMORPG.Shared.Dto;
using MMORPG.Shared.Dto.Db;
using MMORPG.Shared.Net;

namespace MMORPG.GameServer.Auth
{
    /// <summary>
    /// Toàn bộ nghiệp vụ đăng ký / đăng nhập. Handler không chứa gì ngoài lời gọi vào đây.
    /// </summary>
    public sealed class AuthService
    {
        private readonly DbClient _db;
        private readonly LoginRateLimiter _rateLimiter;

        public AuthService(DbClient db, LoginRateLimiter rateLimiter)
        {
            _db = db;
            _rateLimiter = rateLimiter;
        }

        public async Task<AuthResponse> RegisterAsync(ClientSession session, RegisterRequest request)
        {
            string username = AccountNameRules.Normalize(request.Username);

            if (!AccountNameRules.IsValidUsername(username))
                return Fail(ErrorCode.InvalidInput);

            if (!AccountNameRules.IsValidPassword(request.Password))
                return Fail(ErrorCode.InvalidInput);

            (byte[] hash, byte[] salt, int iterations) = PasswordHasher.Hash(request.Password);

            var result = await _db.CallAsync<AccountCreateRequest, AccountCreateResponse>(
                DbCmd.AccountCreate,
                new AccountCreateRequest
                {
                    Username = username,
                    PasswordHash = hash,
                    Salt = salt,
                    Iterations = iterations,
                });

            if (!result.Created)
                return Fail(ErrorCode.AccountExists);

            Log.Info($"{session.Tag} Tạo tài khoản {username.Cyan()} (id {result.AccountId.ToString().Green()})");

            // Đăng ký xong đăng nhập luôn — người chơi không phải gõ lại.
            return Authenticate(session, result.AccountId, username);
        }

        public async Task<AuthResponse> LoginAsync(ClientSession session, LoginRequest request)
        {
            if (!_rateLimiter.TryConsume(session.Id))
                return Fail(ErrorCode.TooManyAttempts);

            string username = AccountNameRules.Normalize(request.Username);

            // Định dạng sai thì chắc chắn không có trong DB — nhưng vẫn trả InvalidCredentials
            // chứ không phải InvalidInput. Nói "tên này sai định dạng" là đã tiết lộ một mẩu thông tin.
            if (!AccountNameRules.IsValidUsername(username) ||
                !AccountNameRules.IsValidPassword(request.Password))
            {
                PasswordHasher.BurnEquivalentTime();
                return Fail(ErrorCode.InvalidCredentials);
            }

            var account = await _db.CallAsync<AccountGetRequest, AccountGetResponse>(
                DbCmd.AccountGetByName, new AccountGetRequest { Username = username });

            if (!account.Found)
            {
                // Không có tài khoản thì trả về ngay sẽ NHANH hơn hẳn trường hợp có tài khoản
                // (vì bỏ qua 100.000 vòng PBKDF2). Kẻ tấn công chỉ cần bấm giờ là dò được
                // tài khoản nào tồn tại. Đốt đúng lượng thời gian đó để hai đường bằng nhau.
                PasswordHasher.BurnEquivalentTime();
                return Fail(ErrorCode.InvalidCredentials);
            }

            if (!PasswordHasher.Verify(request.Password, account.PasswordHash, account.Salt, account.Iterations))
                return Fail(ErrorCode.InvalidCredentials);

            if (account.IsBanned)
                return Fail(ErrorCode.InvalidCredentials);

            _rateLimiter.Reset(session.Id);
            KickPreviousSession(session, account.AccountId);

            _ = _db.CallAsync<AccountTouchLoginRequest, DbOkResponse>(
                DbCmd.AccountTouchLogin, new AccountTouchLoginRequest { AccountId = account.AccountId });

            Log.Info($"{session.Tag} {username.Cyan()} đăng nhập thành công");
            return Authenticate(session, account.AccountId, username);
        }

        public AuthResponse Logout(ClientSession session)
        {
            SessionTokens.Revoke(session.AccountId);
            session.MarkLoggedOut();

            return new AuthResponse { Success = true };
        }

        /// <summary>
        /// Đá session cũ của cùng tài khoản. Chọn "người mới thắng" vì tình huống thật hay gặp nhất
        /// là người chơi rớt mạng: session cũ còn treo trên server nhưng đã chết ở phía họ.
        /// Nếu chọn "người cũ thắng" thì họ phải ngồi chờ hết timeout mới vào lại được.
        /// </summary>
        private static void KickPreviousSession(ClientSession current, long accountId)
        {
            foreach (ClientSession other in SessionRegistry.All)
            {
                if (other.Id == current.Id || other.AccountId != accountId)
                    continue;

                other.Kick("Tài khoản của bạn vừa đăng nhập ở nơi khác.");
            }
        }

        private static AuthResponse Authenticate(ClientSession session, long accountId, string username)
        {
            session.MarkAuthenticated(accountId, username);

            return new AuthResponse
            {
                Success = true,
                Username = username,
                SessionToken = SessionTokens.Issue(accountId),
            };
        }

        private static AuthResponse Fail(ErrorCode error) =>
            new() { Success = false, Error = error };
    }
}
```

**File mới:** `Server/GameServer/Auth/SessionTokens.cs`

```csharp
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace MMORPG.GameServer.Auth
{
    /// <summary>
    /// Token phiên, giữ trong RAM. Phase 7 dùng để nối lại sau khi rớt mạng mà không phải gõ mật khẩu.
    ///
    /// Server restart là mất hết token — đúng như mong muốn ở giai đoạn này: người chơi đăng nhập lại,
    /// không có gì hỏng. Muốn token sống qua restart thì phải lưu DB, và lúc đó phải nghĩ tới hạn dùng
    /// và thu hồi — chưa cần.
    /// </summary>
    public static class SessionTokens
    {
        private static readonly ConcurrentDictionary<long, string> _byAccount = new();

        public static string Issue(long accountId)
        {
            // 32 byte ngẫu nhiên MẬT MÃ. Không dùng Guid, không dùng Random —
            // cả hai đều đoán được nếu biết đủ mẫu.
            string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            _byAccount[accountId] = token;

            return token;
        }

        public static bool Validate(long accountId, string token) =>
            _byAccount.TryGetValue(accountId, out string? known) &&
            !string.IsNullOrEmpty(token) &&
            CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.ASCII.GetBytes(known),
                System.Text.Encoding.ASCII.GetBytes(token));

        public static void Revoke(long accountId) => _byAccount.TryRemove(accountId, out _);
    }
}
```

**File mới:** `Server/GameServer/Auth/LoginRateLimiter.cs`

```csharp
using System.Collections.Concurrent;

namespace MMORPG.GameServer.Auth
{
    /// <summary>
    /// Giới hạn số lần đăng nhập sai. Không có nó thì một script thử vài trăm nghìn mật khẩu mỗi phút.
    ///
    /// Đếm theo session id là mức tối thiểu — kẻ tấn công chỉ cần nối lại là có bộ đếm mới.
    /// Bản thật phải đếm theo địa chỉ IP; ghi vào Phase 16 cùng với TLS và structured logging.
    /// </summary>
    public sealed class LoginRateLimiter
    {
        private const int MAX_ATTEMPTS = 5;
        private static readonly TimeSpan _window = TimeSpan.FromMinutes(1);

        private readonly ConcurrentDictionary<int, (int Count, DateTime WindowStart)> _attempts = new();

        /// <returns>false nghĩa là đã vượt hạn mức, phải từ chối.</returns>
        public bool TryConsume(int sessionId)
        {
            DateTime now = DateTime.UtcNow;

            (int count, DateTime start) = _attempts.AddOrUpdate(
                sessionId,
                _ => (1, now),
                (_, old) => now - old.WindowStart > _window ? (1, now) : (old.Count + 1, old.WindowStart));

            return count <= MAX_ATTEMPTS;
        }

        public void Reset(int sessionId) => _attempts.TryRemove(sessionId, out _);
    }
}
```

**File mới:** `Server/GameServer/Handlers/AuthHandler.cs`

```csharp
using MMORPG.GameServer.Auth;
using MMORPG.GameServer.Net;
using MMORPG.Shared.Dto;
using MMORPG.Shared.Net;

namespace MMORPG.GameServer.Handlers
{
    /// <summary>
    /// Ba dòng mỗi handler. Nếu có ngày một hàm ở đây dài quá 10 dòng thì nghiệp vụ đang rò rỉ
    /// ra khỏi <see cref="AuthService"/> — kéo nó về.
    /// </summary>
    public static class AuthHandler
    {
        public static AuthService Auth { get; set; } = null!;

        [TcpHandler(NetCmd.Register)]
        public static async Task<NetResult> OnRegister(NetRequest req)
        {
            if (req.Session.State >= SessionState.Authenticated)
                return NetResult.Ok(new AuthResponse { Success = false, Error = ErrorCode.AlreadyAuthenticated });

            return NetResult.Ok(await Auth.RegisterAsync(req.Session, req.GetData<RegisterRequest>()));
        }

        [TcpHandler(NetCmd.Login)]
        public static async Task<NetResult> OnLogin(NetRequest req)
        {
            if (req.Session.State >= SessionState.Authenticated)
                return NetResult.Ok(new AuthResponse { Success = false, Error = ErrorCode.AlreadyAuthenticated });

            return NetResult.Ok(await Auth.LoginAsync(req.Session, req.GetData<LoginRequest>()));
        }

        [TcpHandler(NetCmd.Logout, MinState = SessionState.Authenticated)]
        public static Task<NetResult> OnLogout(NetRequest req) =>
            Task.FromResult(NetResult.Ok(Auth.Logout(req.Session)));
    }
}
```

Nối vào `Server/GameServer/Program.cs`:

```csharp
AuthHandler.Auth = new AuthService(db, new LoginRateLimiter());
```

### ✅ CHECKPOINT A — server đứng vững một mình

Chưa cần Unity. Bật DBServer + GameServer, rồi tạm thêm nút Register/Login vào `NetworkProbe`
(hoặc dùng lại nút Echo, sửa tạm nội dung gửi):

```
INFO  [AuthService] #1 Tạo tài khoản hung (id 1)
```

Kiểm tra trong `mmorpg.db`:
```sql
SELECT id, username, iterations, length(password_hash), length(salt) FROM account;
```
Phải ra `1 | hung | 100000 | 32 | 16`. **Nhìn kỹ: không có cột nào chứa mật khẩu.** Đó là điều bạn muốn thấy.

---

## Bước 6 — Client: UI đăng nhập

**File mới:** `Assets/Game/Scripts/Auth/AuthApi.cs`

```csharp
using MMORPG.Client.Network;
using MMORPG.Shared.Dto;
using MMORPG.Shared.Net;

namespace MMORPG.Client.Auth
{
    /// <summary>
    /// Gom mọi lệnh auth mà client GỬI ĐI. Đối xứng với <see cref="Handlers.AuthNetHandler"/> ở chiều nhận.
    /// Tách ra để chỗ khác không phải biết `NetCmd` nào đi với DTO nào.
    /// </summary>
    public sealed class AuthApi
    {
        private readonly NetService _net;

        public AuthApi(NetService net) => _net = net;

        public void Register(string username, string password) =>
            _net.Send(NetCmd.Register, new RegisterRequest { Username = username, Password = password });

        public void Login(string username, string password) =>
            _net.Send(NetCmd.Login, new LoginRequest { Username = username, Password = password });

        public void Logout() => _net.Send(NetCmd.Logout, new EmptyRequest());
    }
}
```

**File mới:** `Assets/Game/Scripts/Network/Handlers/AuthNetHandler.cs`

```csharp
using System;
using MMORPG.Shared.Dto;
using MMORPG.Shared.Net;

namespace MMORPG.Client.Network.Handlers
{
    public sealed class AuthNetHandler : INetHandlerGroup
    {
        public event Action<AuthResponse> OnRegisterResult;
        public event Action<AuthResponse> OnLoginResult;
        public event Action<AuthResponse> OnLogoutResult;
        public event Action<KickedNotice> OnKicked;

        [NetHandler(NetCmd.Register)]
        private void HandleRegister(NetPacket packet) => OnRegisterResult?.Invoke(packet.GetData<AuthResponse>());

        [NetHandler(NetCmd.Login)]
        private void HandleLogin(NetPacket packet) => OnLoginResult?.Invoke(packet.GetData<AuthResponse>());

        [NetHandler(NetCmd.Logout)]
        private void HandleLogout(NetPacket packet) => OnLogoutResult?.Invoke(packet.GetData<AuthResponse>());

        [NetHandler(NetCmd.Kicked)]
        private void HandleKicked(NetPacket packet) => OnKicked?.Invoke(packet.GetData<KickedNotice>());
    }
}
```

**File mới:** `Assets/Game/Scripts/Auth/AuthErrorText.cs`

```csharp
using MMORPG.Shared.Net;

namespace MMORPG.Client.Auth
{
    /// <summary>
    /// Đổi <see cref="ErrorCode"/> thành câu tiếng Việt cho người chơi.
    ///
    /// Việc dịch nằm ở CLIENT, không phải server. Server trả về mã; client quyết định hiển thị thế nào.
    /// Nhờ vậy đổi câu chữ không phải build lại server, và thêm ngôn ngữ khác chỉ là thêm một bảng tra.
    /// </summary>
    public static class AuthErrorText
    {
        public static string Of(ErrorCode code) => code switch
        {
            ErrorCode.InvalidInput =>
                "Tên đăng nhập 3–16 ký tự (chữ thường, số, gạch dưới). Mật khẩu tối thiểu 6 ký tự.",
            ErrorCode.AccountExists => "Tên đăng nhập này đã có người dùng.",
            ErrorCode.InvalidCredentials => "Sai tài khoản hoặc mật khẩu.",
            ErrorCode.TooManyAttempts => "Bạn thử sai quá nhiều lần. Chờ một phút rồi thử lại.",
            ErrorCode.ServiceUnavailable => "Máy chủ đang bận. Thử lại sau giây lát.",
            _ => "Có lỗi xảy ra. Thử lại sau.",
        };
    }
}
```

**File mới:** `Assets/Game/Scripts/Auth/LoginUi.cs`

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MMORPG.Client.Auth
{
    /// <summary>
    /// View thuần: chỉ vẽ và phát tín hiệu bấm nút. Không biết mạng là gì.
    /// </summary>
    public sealed class LoginUi : MonoBehaviour
    {
        [SerializeField] private TMP_InputField _usernameInput;
        [SerializeField] private TMP_InputField _passwordInput;
        [SerializeField] private Button _loginButton;
        [SerializeField] private Button _registerButton;
        [SerializeField] private TextMeshProUGUI _messageText;
        [SerializeField] private GameObject _root;

        public string Username => _usernameInput.text;
        public string Password => _passwordInput.text;

        public Button LoginButton => _loginButton;
        public Button RegisterButton => _registerButton;

        private void Awake()
        {
            // Ô mật khẩu phải là Password content type — kiểm bằng code vì rất dễ quên set trong Inspector,
            // và quên thì mật khẩu hiện nguyên trên màn hình lúc quay video demo.
            _passwordInput.contentType = TMP_InputField.ContentType.Password;
            _passwordInput.ForceLabelUpdate();
        }

        public void ShowMessage(string text, bool isError)
        {
            _messageText.text = text;
            _messageText.color = isError ? new Color(0.9f, 0.3f, 0.3f) : Color.white;
        }

        public void SetInteractable(bool value)
        {
            _loginButton.interactable = value;
            _registerButton.interactable = value;
        }

        public void SetVisible(bool value) => _root.SetActive(value);
    }
}
```

**File mới:** `Assets/Game/Scripts/Auth/LoginPresenter.cs`

```csharp
using Cysharp.Threading.Tasks;
using HungNT;
using MMORPG.Client.Network;
using MMORPG.Client.Network.Handlers;
using MMORPG.Shared.Dto;
using MMORPG.Shared.Net;
using UnityEngine;
using VContainer;

namespace MMORPG.Client.Auth
{
    /// <summary>
    /// Nối UI với mạng. Đây là chỗ duy nhất biết cả hai bên.
    /// </summary>
    public sealed class LoginPresenter : MonoBehaviour
    {
        [SerializeField] private LoginUi _ui;
        [SerializeField] private string _host = "127.0.0.1";
        [SerializeField] private int _port = 7778;

        private NetService _net;
        private AuthApi _auth;
        private AuthNetHandler _authHandler;

        [Inject]
        public void Construct(NetService net, AuthApi auth, AuthNetHandler authHandler)
        {
            _net = net;
            _auth = auth;
            _authHandler = authHandler;
        }

        private void Awake()
        {
            _ui.LoginButton.onClick.AddListener(() => SubmitAsync(isRegister: false).Forget());
            _ui.RegisterButton.onClick.AddListener(() => SubmitAsync(isRegister: true).Forget());

            _authHandler.OnLoginResult += OnAuthResult;
            _authHandler.OnRegisterResult += OnAuthResult;
            _authHandler.OnKicked += OnKicked;
        }

        private void OnDestroy()
        {
            if (_authHandler == null)
                return;

            _authHandler.OnLoginResult -= OnAuthResult;
            _authHandler.OnRegisterResult -= OnAuthResult;
            _authHandler.OnKicked -= OnKicked;
        }

        private async UniTaskVoid SubmitAsync(bool isRegister)
        {
            // Khoá nút ngay: người chơi bấm 5 lần liên tiếp thì server nhận 5 request,
            // và với rate limiter 5 lần/phút thì họ tự khoá chính mình.
            _ui.SetInteractable(false);
            _ui.ShowMessage("Đang kết nối...", isError: false);

            if (!_net.IsConnected && !await _net.ConnectAsync(_host, _port))
            {
                _ui.ShowMessage("Không kết nối được máy chủ.", isError: true);
                _ui.SetInteractable(true);
                return;
            }

            _ui.ShowMessage(isRegister ? "Đang tạo tài khoản..." : "Đang đăng nhập...", isError: false);

            if (isRegister)
                _auth.Register(_ui.Username, _ui.Password);
            else
                _auth.Login(_ui.Username, _ui.Password);
        }

        private void OnAuthResult(AuthResponse response)
        {
            _ui.SetInteractable(true);

            if (!response.Success)
            {
                _ui.ShowMessage(AuthErrorText.Of(response.Error), isError: true);
                return;
            }

            _ui.ShowMessage($"Xin chào, {response.Username}!", isError: false);
            this.Log($"Đăng nhập xong. Token dài {response.SessionToken.Length} ký tự.");

            // Phase 5 sẽ thay bằng: mở màn hình chọn nhân vật.
            _ui.SetVisible(false);
        }

        private void OnKicked(KickedNotice notice)
        {
            _ui.SetVisible(true);
            _ui.SetInteractable(true);
            _ui.ShowMessage(notice.Reason, isError: true);
        }
    }
}
```

Đăng ký vào `GameLifetimeScope`:

```csharp
            builder.Register<AuthApi>(Lifetime.Singleton);

            builder.Register<AuthNetHandler>(Lifetime.Singleton)
                   .AsSelf()
                   .As<INetHandlerGroup>();
```

Trong scene: một Canvas với 2 `TMP_InputField`, 2 `Button`, 1 `TextMeshProUGUI`, gắn `LoginUi` + `LoginPresenter`,
kéo tham chiếu vào. Thêm object chứa `LoginPresenter` vào *Auto Inject Game Objects* của `GameLifetimeScope`.

> **Vì sao chưa dùng `com.hungnt.ui.panel`:** hiện mới có đúng một màn hình. `PanelManager` giải quyết bài toán
> *nhiều* panel chồng lớp, đóng/mở theo thứ tự — chưa có bài toán đó thì thêm nó chỉ là thêm tầng. Phase 5 có
> màn hình chọn nhân vật là lúc chuyển, và lúc đó việc chuyển sẽ dễ vì `LoginUi` đã tách khỏi `LoginPresenter`.

---

### ✅ CHECKPOINT B — mục tiêu cuối Phase 4

1. Bật DBServer, bật GameServer, Play Unity.
2. Gõ `hung` / `123456`, bấm **Đăng ký** → `Xin chào, hung!`, server log `INFO  [AuthService] #1 Tạo tài khoản hung (id 1)`.
3. Bấm **Đăng ký** lại cùng tên → `Tên đăng nhập này đã có người dùng.`
4. Thoát Play mode, Play lại, bấm **Đăng nhập** cùng tài khoản → `Xin chào, hung!`
5. Gõ sai mật khẩu → `Sai tài khoản hoặc mật khẩu.`
6. Gõ tài khoản không tồn tại → **cùng một câu**, và cảm nhận được là **mất chừng ấy thời gian**.

---

## Bước 7 — Bốn thử nghiệm bắt buộc

**1. Chống đoán tài khoản bằng đồng hồ.** Thêm tạm vào `AuthService.LoginAsync` một `Stopwatch` bao quanh
đoạn từ đầu tới lúc `Fail`, log mili giây. Thử tài khoản có thật + sai mật khẩu, rồi tài khoản không tồn tại.
Hai con số phải xấp xỉ nhau (chênh dưới ~20%). Sau đó **bỏ tạm `BurnEquivalentTime`** và đo lại — bạn sẽ thấy
đường "không tồn tại" nhanh hơn hàng chục lần. Đó chính là lỗ hổng. Khôi phục lại rồi đi tiếp.

**2. Vượt rào trạng thái.** Trong Unity, gửi `NetCmd.Logout` khi **chưa** đăng nhập:
```csharp
_net.Send(NetCmd.Logout, new EmptyRequest());
```
→ Console Unity: `NotAuthenticated — Logout cần trạng thái Authenticated, session đang ở Connected`.
Đây là bằng chứng hàng rào ở dispatcher hoạt động, và là mẫu cho mọi lệnh từ Phase 5 trở đi.

**3. Đăng nhập trùng.** Cần 2 client. Cách nhanh nhất chưa cần ParrelSync: build ra file `.exe`
(`File → Build Settings → Build`) rồi chạy bản build **song song** với Editor.
- Đăng nhập `hung` ở Editor → OK.
- Đăng nhập `hung` ở bản build → OK, và **Editor** hiện `Tài khoản của bạn vừa đăng nhập ở nơi khác.`
- Nếu Editor chỉ mất kết nối mà không có chữ → gói `Kicked` bị cắt trước khi kịp gửi, tăng `Task.Delay` trong
  `CloseAfterFlushAsync`.

**4. Rate limit.** Bấm Đăng nhập sai 6 lần liên tiếp → lần thứ 6 phải ra
`Bạn thử sai quá nhiều lần. Chờ một phút rồi thử lại.`
Rồi **ngắt kết nối và nối lại** → bộ đếm về 0. Đó là giới hạn đã biết của bản đếm theo session id;
ghi nhận và để dành Phase 16.

---

## Troubleshooting

| Triệu chứng | Nguyên nhân | Xử lý |
|-------------|-------------|-------|
| Đăng nhập luôn `InvalidCredentials` dù mật khẩu đúng | `username` không được `Normalize` ở một trong hai đường (tạo / đọc) | Chỉ có `AccountNameRules.Normalize` được phép sinh ra chuỗi đem đi query |
| `AccountCreate` luôn `Created = false` | Đã có dòng cũ từ lần test trước | `DELETE FROM account;` hoặc xoá file `.db` |
| `SqliteException: UNIQUE constraint failed` lọt lên tới GameServer | `catch` bắt sai mã lỗi | Dùng `ex.SqliteExtendedErrorCode == 2067`, **không** phải `ex.SqliteErrorCode` (mã đó là 19) |
| Đăng nhập mất ~1 giây | 100k vòng PBKDF2 chạy ở Debug | Bình thường. Bản Release nhanh hơn nhiều; nếu vẫn khó chịu lúc dev thì hạ `DEFAULT_ITERATIONS` **và nhớ nâng lại** |
| `MarkAuthenticated` chạy rồi mà lệnh sau vẫn `NotAuthenticated` | Client mở kết nối MỚI cho mỗi request | Trạng thái gắn với **kết nối**. Một client = một `ClientSession` sống suốt phiên chơi |
| Bị `Kicked` ngay sau khi vừa đăng nhập | `KickPreviousSession` không loại trừ chính mình | Điều kiện phải có cả `other.Id == current.Id` |
| Session cũ không bị đá | Session chưa được ghi vào `SessionRegistry` | Kiểm tra `SessionRegistry.Add(this)` ở `ClientSession.RunAsync` (thêm ở Phase 3) |
| Unity không thấy `RegisterRequest` | Chưa build lại `Shared` | `dotnet build Server/Shared` |
| Mật khẩu hiện rõ trong ô nhập | Quên `contentType = Password` | Đã set trong `LoginUi.Awake` — kiểm tra `_passwordInput` có được kéo vào Inspector không |

---

## Tự kiểm tra hiểu bài

1. Vì sao "tài khoản không tồn tại" và "sai mật khẩu" phải trả về cùng một `ErrorCode` **và** tốn cùng thời gian?
   Hai điều đó chống hai kiểu tấn công khác nhau — kể tên từng kiểu.
2. Nếu bỏ `salt` và chỉ băm mật khẩu bằng PBKDF2 với salt cố định, kẻ có file DB sẽ làm được gì thêm?
3. Vì sao băm mật khẩu ở client rồi gửi hash lên lại **kém an toàn hơn** gửi mật khẩu thẳng (khi cả hai đều không có TLS)?
4. `CreateAsync` bắt lỗi UNIQUE thay vì `SELECT` trước. Viết ra chuỗi sự kiện cụ thể khiến cách `SELECT` trước bị hỏng.
5. Vì sao `AuthResponse` không chứa `AccountId`? Nếu có thì client lạm dụng được gì?
6. `MinState` đặt ở dispatcher chứ không phải trong từng handler. Kể một kịch bản mà cách "check trong handler"
   thủng còn cách này thì không.
7. Vì sao `AccountGetResponse` mang hash về GameServer thay vì để DBServer tự so sánh?
8. Chọn "người mới đăng nhập thắng" thay vì "người cũ giữ chỗ". Tình huống nào khiến lựa chọn ngược lại tệ hơn?
   Có tình huống nào lựa chọn hiện tại tệ hơn không?
9. `SessionTokens` mất hết khi server restart. Vì sao chấp nhận được ở giai đoạn này, và điều gì sẽ khiến nó
   không còn chấp nhận được?

---

**Xong Phase 4 → [PHASE-5](PHASE-5.md): nhân vật, và ba danh tính khác nhau của một người chơi.**
