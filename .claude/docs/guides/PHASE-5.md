# PHASE 5 — Vào thế giới: từ tài khoản tới nhân vật trên map

> **Kết quả cuối Phase 5:** đăng nhập → client tự động vào thẳng thế giới → nhân vật xuất hiện trên map
> ở đúng vị trí lần trước thoát ra, camera bám theo. Lần đăng nhập đầu tiên, nhân vật được **tự tạo**
> với tên trùng tên tài khoản — không có màn hình chọn nhân vật (kiểu Ngọc Rồng Online).
>
> **Điều kiện:** xong [`PHASE-4.md`](PHASE-4.md) tới CHECKPOINT B và cả 4 thử nghiệm ở Bước 7.
>
> **Bài học chính:** `Account`, `Character` và `Entity` là **ba** thứ khác nhau, sống ở ba nơi khác nhau,
> có vòng đời khác nhau — kể cả khi quan hệ Account↔Character là 1-1. Gộp chúng lại là sai lầm kiến trúc
> phổ biến nhất của người mới viết game server, và nó chỉ lộ ra ở Phase 11, lúc quái vật cũng cần là entity
> nhưng chẳng có tài khoản nào.

---

## ⚠️ Cách dùng tài liệu này (format thử nghiệm)

Từ phase này, mỗi bước có **hai tầng**:

1. **Hướng làm** — mô tả cần dựng cái gì, vì sao, các quyết định thiết kế, bẫy cần né, và code khung
   (chữ ký hàm, cấu trúc) đủ để bạn tự triển khai.
2. **📖 Lời giải** — code đầy đủ, nằm trong foldout **mặc định đóng** ngay dưới phần hướng làm.

Quy trình đúng: đọc hướng làm → **tự nghĩ và tự code** → chạy thử → *rồi mới* mở lời giải để đối chiếu
xem có lệch ý gì không. Mở lời giải trước khi tự code thì format này mất hết tác dụng — lúc đó nó chỉ là
doc cũ với thêm một cú click.

Xong phase thì đánh giá: format này có đáng dùng tiếp cho Phase 6+ không.

---

## Thay đổi so với thiết kế cũ

Bản nháp trước của phase này có màn hình chọn nhân vật, mỗi tài khoản 3 slot, tạo/xoá nhân vật, luật đặt tên.
**Đã bỏ hết** — dự án học thì 1 tài khoản = 1 nhân vật là đủ, vào thẳng game như Ngọc Rồng Online.

| Bỏ | Giữ |
|----|-----|
| `CharacterList` / `CharacterCreate` / `CharacterDelete` | Bảng `character` **riêng**, không gộp vào `account` |
| UI chọn nhân vật, slot, xác nhận xoá | `EnterWorld` + snapshot khởi tạo |
| Luật đặt tên nhân vật (`name_key`, chuẩn hoá Unicode) | `PlayerEntity` + `WorldService` trong RAM |
| Xoá mềm `deleted_at` | Lưu vị trí khi rời world / mất kết nối |

Vì sao bảng `character` vẫn riêng dù 1-1? Vì hai bảng chứa hai **loại** dữ liệu có vòng đời khác nhau:
`account` là chuyện đăng nhập (hash mật khẩu, ban, mốc login), `character` là chuyện gameplay (level, exp,
vị trí — Phase 10 thêm túi đồ tham chiếu vào đây). Và nếu sau này muốn nhiều nhân vật, chỉ cần bỏ một ràng
buộc `UNIQUE` + thêm UI — không phải đập lại schema. Thiết kế cũ (3 slot) vẫn nằm trong git history nếu cần.

---

## Ba danh tính

| | Account | Character | Entity |
|---|---------|-----------|--------|
| **Là gì** | Danh tính đăng nhập | Nhân vật trong game | Đối tượng đang sống trong world |
| **Sống ở** | Bảng `account` | Bảng `character` | RAM của GameServer |
| **Id** | `account.id` (long) | `character.id` (long) | `entityId` (int, cấp lúc chạy) |
| **Vòng đời** | Từ lúc đăng ký tới lúc xoá | Tự tạo lần đầu vào world, sống mãi | Từ `EnterWorld` tới lúc rời world |
| **Số lượng** | 1 người 1 account | **1 account = đúng 1 character** (chốt của dự án này) | 1 character online = 1 entity |
| **Client được biết** | ❌ không bao giờ | ⚠️ chỉ dữ liệu nhân vật của chính mình | ✅ entityId của mọi ai trong tầm nhìn |

**Vì sao `Entity` không phải là `Character`?**
Entity mang những thứ chỉ có nghĩa khi đang online: mục tiêu đang đánh, buff còn 3 giây, ô lưới AOI đang đứng,
vector vận tốc. Không thứ nào cần vào DB. Nếu `Character` gánh luôn phần đó thì object nào cũng nửa "dữ liệu
lâu dài" nửa "trạng thái tạm", và bạn sẽ không bao giờ trả lời dứt khoát được câu "cái gì cần lưu?".

**Vì sao `entityId` không dùng thẳng `character.id`?**
Ba lý do, xếp theo mức quan trọng tăng dần: (1) `int` gọn hơn `long` trên gói tin gửi 20 lần/giây;
(2) Phase 11 quái vật cũng cần entity id mà chúng không có character; (3) `character.id` là thông tin lâu dài
về người khác — không có lý do gì để client biết id thật của người bên cạnh.

---

## Luồng sẽ dựng

```
Đăng nhập xong (SessionState.Authenticated)
      │
      └─► client TỰ gửi NetCmd.EnterWorld (payload rỗng — không có CharacterId!)
              │
              └─► CharacterService.EnterWorldAsync(session)
                      │  AccountId lấy từ SESSION, không phải từ gói tin
                      └─► DB: CharacterGetOrCreate(accountId)
                              │  chưa có → tự tạo, tên = username, vị trí spawn mặc định
                              └─► WorldService.Spawn() → PlayerEntity (entityId mới)
                                      └─► session.MarkInWorld(entity)
                                              └─► EnterWorldResponse { EntityId, X, Y, MapId, ... }
                                                      └─► Client: spawn prefab, camera bám
```

Điểm đáng ngẫm nhất của phase: **`EnterWorld` không mang theo trường nào cả.**
Bản thiết kế cũ có `EnterWorldRequest { CharacterId }` — và kèm theo nó là lỗ hổng nghiêm trọng nhất
của phase: quên kiểm "nhân vật này có thuộc account đang hỏi không" thì ai cũng vào được nhân vật của
bất kỳ ai. Giờ client không gửi id nào, server tự tra nhân vật từ `session.AccountId` — lỗ hổng đó
**không thể tồn tại**, vì không có trường nào để giả mạo.

> Trường nguy hiểm nhất trên gói tin là trường mà server tin từ client.
> Trường an toàn nhất là trường không tồn tại.

---

## Bước 1 — Shared: contract

### Hướng làm

**`NetCmd`** — dải Character (200–299) giờ chỉ cần đúng một lệnh:

```csharp
        #region Character (200–299)

        /// <summary>
        /// Vào thế giới. Nhân vật tự tạo trong lần gọi đầu tiên của tài khoản.
        /// Request: <see cref="Dto.EmptyRequest"/> · Response: <see cref="Dto.EnterWorldResponse"/>
        /// Client chủ động gửi ngay sau khi đăng nhập thành công.
        /// </summary>
        EnterWorld = 200,

        #endregion
```

Không có `LeaveWorld`: không còn màn hình chọn nhân vật để "rời về", nên rời world chỉ xảy ra qua
`Logout` (Phase 4 đã có) hoặc mất kết nối. Cả hai đường đều là việc của server, không cần lệnh riêng.

**`DbCmd`** — dải 1200–1299, hai lệnh: `CharacterGetOrCreate = 1200` và `CharacterSavePosition = 1201`.
Ghi XML doc theo đúng kiểu các lệnh Account đang có.

**DTO cần tự viết** (nhìn `AuthDto.cs` / `AccountDto.cs` để theo đúng kiểu):

- `Server/Shared/Dto/Character/CharacterDto.cs` — contract client↔server:
  - `EnterWorldResponse`: `Success`, `Error`, `EntityId` (int), `CharacterId`, `Name`, `ClassId`, `Level`,
    `MapId`, `X`, `Y`, và `ServerTimeMs` (Phase 6 dùng làm gốc đồng bộ tick).
- `Server/Shared/Dto/Db/CharacterDbDto.cs` — contract nội bộ:
  - `CharacterRow`: một dòng nguyên vẹn của bảng `character` (`CharacterId`, `AccountId`, `Name`,
    `ClassId`, `Level`, `Exp`, `MapId`, `X`, `Y`). Chỉ đi trên đường nội bộ.
  - `CharacterGetOrCreateRequest`: `AccountId` + toàn bộ giá trị mặc định cho lần tạo đầu
    (`Name`, `ClassId`, `MapId`, `X`, `Y`).
  - `CharacterGetOrCreateResponse`: `Created` (lần này có phải lần tạo đầu không — để log) + `Character`.
  - `CharacterSavePositionRequest`: `CharacterId`, `MapId`, `X`, `Y`.

**Câu hỏi thiết kế trước khi code:** vì sao giá trị mặc định (tên, vị trí spawn) do **GameServer** truyền
xuống trong request, thay vì để DBServer tự bịa? — Vì "spawn ở đâu, nghề gì" là **luật chơi**, và luật chơi
sống ở GameServer. DBServer chỉ biết lưu và lấy; ngày Phase 9 chuyển spawn point vào bảng config, chỉ
GameServer phải đổi.

**`ErrorCode`** — thêm một mã mới vào cuối:

```csharp
        /// <summary>Nhân vật đang trong world rồi — gọi EnterWorld lần hai, hoặc session cũ chưa dọn xong.</summary>
        CharacterInUse = 11,
```

Xong thì `dotnet build Server/Shared` để DLL tự copy sang Unity.

**Việc vặt tiện tay:** `NetCmd.Echo` và cặp `EchoRequest/EchoResponse` có hẹn "xoá khi Phase 4 xong" —
giờ là lúc (nhớ gỡ luôn handler hai bên và nút thử trong `NetworkProbe` nếu còn).

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`Server/Shared/Db/DbCmd.cs`** — thêm region:

```csharp
        #region Character (1200–1299)

        /// <summary>
        /// Lấy nhân vật của tài khoản; chưa có thì tạo với giá trị mặc định trong request.
        /// Request: <see cref="Dto.Db.CharacterGetOrCreateRequest"/> · Response: <see cref="Dto.Db.CharacterGetOrCreateResponse"/>
        /// </summary>
        CharacterGetOrCreate = 1200,

        /// <summary>
        /// Ghi vị trí cuối của nhân vật khi rời world.
        /// Request: <see cref="Dto.Db.CharacterSavePositionRequest"/> · Response: <see cref="Dto.Db.DbOkResponse"/>
        /// </summary>
        CharacterSavePosition = 1201,

        #endregion
```

**`Server/Shared/Dto/Character/CharacterDto.cs`**:

```csharp
using MemoryPack;

namespace MMORPG.Shared.Dto
{
    [MemoryPackable]
    public partial class EnterWorldResponse
    {
        public bool Success { get; set; }
        public Net.ErrorCode Error { get; set; }

        /// <summary>Id runtime trong world. Chỉ có nghĩa tới khi rời world.</summary>
        public int EntityId { get; set; }

        public long CharacterId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int ClassId { get; set; }
        public int Level { get; set; }

        public int MapId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }

        /// <summary>Mốc thời gian server lúc vào. Phase 6 dùng làm gốc cho đồng bộ tick.</summary>
        public long ServerTimeMs { get; set; }
    }
}
```

**`Server/Shared/Dto/Db/CharacterDbDto.cs`**:

```csharp
using MemoryPack;

namespace MMORPG.Shared.Dto.Db
{
    /// <summary>Một dòng nguyên vẹn của bảng <c>character</c>. Chỉ đi trên đường nội bộ.</summary>
    [MemoryPackable]
    public partial class CharacterRow
    {
        public long CharacterId { get; set; }
        public long AccountId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int ClassId { get; set; }
        public int Level { get; set; }
        public long Exp { get; set; }
        public int MapId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
    }

    /// <summary>
    /// Giá trị mặc định cho lần tạo đầu do GAMESERVER quyết — spawn ở đâu, nghề gì là luật chơi,
    /// không phải việc của tầng lưu trữ.
    /// </summary>
    [MemoryPackable]
    public partial class CharacterGetOrCreateRequest
    {
        public long AccountId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int ClassId { get; set; }
        public int MapId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
    }

    [MemoryPackable]
    public partial class CharacterGetOrCreateResponse
    {
        /// <summary>true = lần này vừa tạo mới (lần vào world đầu tiên của tài khoản).</summary>
        public bool Created { get; set; }

        public CharacterRow Character { get; set; } = new();
    }

    [MemoryPackable]
    public partial class CharacterSavePositionRequest
    {
        public long CharacterId { get; set; }
        public int MapId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
    }
}
```

</details>

---

## Bước 2 — DBServer: bảng `character` + repository

### Hướng làm

**Migration 3** — thêm vào cuối `_migrations` trong `Migrator.cs` (nhớ quy tắc: migration đã chạy thì
không sửa nữa, chỉ thêm mới). Schema:

```sql
CREATE TABLE character (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    account_id INTEGER NOT NULL UNIQUE REFERENCES account(id) ON DELETE CASCADE,
    name       TEXT    NOT NULL,
    class_id   INTEGER NOT NULL,
    level      INTEGER NOT NULL DEFAULT 1,
    exp        INTEGER NOT NULL DEFAULT 0,
    map_id     INTEGER NOT NULL,
    pos_x      REAL    NOT NULL,
    pos_y      REAL    NOT NULL,
    created_at TEXT    NOT NULL
);
```

`UNIQUE` trên `account_id` chính là chỗ quan hệ **1-1 được thi hành** — ở DB, không phải ở code.
Đây là bài của Phase 4 lặp lại: ràng buộc đặt ở DB thì *không thể* bị lách, kể cả bởi bug của chính bạn.
(SQLite tự tạo index cho cột UNIQUE nên không cần `CREATE INDEX` riêng.)

**`Server/DBServer/Repositories/CharacterRepository.cs`** — nhìn `AccountRepository` để theo kiểu. Hai method:

```csharp
public async Task<CharacterGetOrCreateResponse> GetOrCreateAsync(CharacterGetOrCreateRequest request, CancellationToken ct = default)
public async Task SavePositionAsync(CharacterSavePositionRequest request, CancellationToken ct = default)
```

Logic `GetOrCreateAsync`: SELECT theo `account_id` → có thì trả về luôn → chưa có thì INSERT → SELECT lại.

**Bẫy cần tự xử lý — nghĩ trước khi mở lời giải:** hai request `EnterWorld` của **cùng tài khoản** chạy
song song (client bấm nhanh, hoặc hai client cùng đăng nhập trong khe thời gian trước khi session cũ bị đá).
Cả hai cùng SELECT thấy "chưa có", cả hai cùng INSERT. Đây là check-then-act — đúng cái bẫy
`AccountRepository.CreateAsync` của Phase 4. Cách xử cũng y hệt: **để `UNIQUE` xử**. Kẻ INSERT sau lãnh
`SqliteException` với `SqliteExtendedErrorCode == 2067` — nuốt đúng exception đó (bằng `when`) rồi SELECT
lại là lấy được dòng kẻ đến trước vừa tạo. Không cần transaction, không cần lock.

**`Server/DBServer/Handlers/CharacterDbHandler.cs`** — nhìn `AccountDbHandler`: class static,
property `Repository` gán một lần trong `Program.cs`, mỗi lệnh một method `[DbHandler(DbCmd.X)]` vài dòng.

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`Migrator.cs`** — phần thêm vào `_migrations`:

```csharp
            (3, """
                CREATE TABLE character (
                    id         INTEGER PRIMARY KEY AUTOINCREMENT,
                    account_id INTEGER NOT NULL UNIQUE REFERENCES account(id) ON DELETE CASCADE,
                    name       TEXT    NOT NULL,
                    class_id   INTEGER NOT NULL,
                    level      INTEGER NOT NULL DEFAULT 1,
                    exp        INTEGER NOT NULL DEFAULT 0,
                    map_id     INTEGER NOT NULL,
                    pos_x      REAL    NOT NULL,
                    pos_y      REAL    NOT NULL,
                    created_at TEXT    NOT NULL
                );
                """),
```

**`Server/DBServer/Repositories/CharacterRepository.cs`**:

```csharp
using Microsoft.Data.Sqlite;
using MMORPG.DBServer.Data;
using MMORPG.Shared.Dto.Db;

namespace MMORPG.DBServer.Repositories
{
    public sealed class CharacterRepository
    {
        private const int SQLITE_CONSTRAINT_UNIQUE = 2067;

        private const string SELECT_COLUMNS =
            "id, account_id, name, class_id, level, exp, map_id, pos_x, pos_y";

        private readonly Database _database;

        public CharacterRepository(Database database)
        {
            _database = database;
        }

        public async Task<CharacterGetOrCreateResponse> GetOrCreateAsync(CharacterGetOrCreateRequest request,
                                                                         CancellationToken ct = default)
        {
            await using SqliteConnection connection = await _database.OpenAsync(ct);

            CharacterRow existing = await SelectByAccountAsync(connection, request.AccountId, ct);
            if (existing != null)
                return new CharacterGetOrCreateResponse { Created = false, Character = existing };

            bool created = true;

            await using (SqliteCommand insert = connection.CreateCommand())
            {
                insert.CommandText = """
                    INSERT INTO character (account_id, name, class_id, map_id, pos_x, pos_y, created_at)
                    VALUES ($accountId, $name, $classId, $mapId, $x, $y, datetime('now'));
                    """;
                insert.Parameters.AddWithValue("$accountId", request.AccountId);
                insert.Parameters.AddWithValue("$name", request.Name);
                insert.Parameters.AddWithValue("$classId", request.ClassId);
                insert.Parameters.AddWithValue("$mapId", request.MapId);
                insert.Parameters.AddWithValue("$x", request.X);
                insert.Parameters.AddWithValue("$y", request.Y);

                try
                {
                    await insert.ExecuteNonQueryAsync(ct);
                }
                catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == SQLITE_CONSTRAINT_UNIQUE)
                {
                    // Hai EnterWorld của cùng tài khoản chạy song song: cả hai SELECT thấy "chưa có"
                    // rồi cùng INSERT. UNIQUE(account_id) biến kẻ đến sau thành vô hại — chỉ việc
                    // đọc lại dòng kẻ đến trước vừa tạo. Cùng bài check-then-act của Phase 4.
                    created = false;
                }
            }

            CharacterRow row = await SelectByAccountAsync(connection, request.AccountId, ct);
            return new CharacterGetOrCreateResponse { Created = created, Character = row };
        }

        public async Task SavePositionAsync(CharacterSavePositionRequest request, CancellationToken ct = default)
        {
            await using SqliteConnection connection = await _database.OpenAsync(ct);
            await using SqliteCommand cmd = connection.CreateCommand();

            cmd.CommandText = """
                UPDATE character SET map_id = $mapId, pos_x = $x, pos_y = $y
                WHERE id = $id;
                """;
            cmd.Parameters.AddWithValue("$id", request.CharacterId);
            cmd.Parameters.AddWithValue("$mapId", request.MapId);
            cmd.Parameters.AddWithValue("$x", request.X);
            cmd.Parameters.AddWithValue("$y", request.Y);

            await cmd.ExecuteNonQueryAsync(ct);
        }

        private static async Task<CharacterRow> SelectByAccountAsync(SqliteConnection connection, long accountId,
                                                                     CancellationToken ct)
        {
            await using SqliteCommand cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT {SELECT_COLUMNS} FROM character WHERE account_id = $accountId;";
            cmd.Parameters.AddWithValue("$accountId", accountId);

            await using SqliteDataReader reader = await cmd.ExecuteReaderAsync(ct);

            if (!await reader.ReadAsync(ct))
                return null;

            return new CharacterRow
            {
                CharacterId = reader.GetInt64(0),
                AccountId = reader.GetInt64(1),
                Name = reader.GetString(2),
                ClassId = reader.GetInt32(3),
                Level = reader.GetInt32(4),
                Exp = reader.GetInt64(5),
                MapId = reader.GetInt32(6),
                X = reader.GetFloat(7),
                Y = reader.GetFloat(8),
            };
        }
    }
}
```

**`Server/DBServer/Handlers/CharacterDbHandler.cs`**:

```csharp
using MMORPG.DBServer.Net;
using MMORPG.DBServer.Repositories;
using MMORPG.ServerCore;
using MMORPG.Shared.Db;
using MMORPG.Shared.Dto.Db;

namespace MMORPG.DBServer.Handlers
{
    public static class CharacterDbHandler
    {
        /// <summary>Gán một lần trong <c>Program.cs</c>.</summary>
        public static CharacterRepository Repository { get; set; }

        [DbHandler(DbCmd.CharacterGetOrCreate)]
        public static async Task<DbResult> OnGetOrCreate(DbRequest req)
        {
            var request = req.GetData<CharacterGetOrCreateRequest>();
            CharacterGetOrCreateResponse result = await Repository.GetOrCreateAsync(request);

            if (result.Created)
                Log.Info($"Tạo nhân vật {request.Name.Cyan()} cho account {request.AccountId.ToString().Green()}");

            return DbResult.Ok(result);
        }

        [DbHandler(DbCmd.CharacterSavePosition)]
        public static async Task<DbResult> OnSavePosition(DbRequest req)
        {
            await Repository.SavePositionAsync(req.GetData<CharacterSavePositionRequest>());
            return DbResult.Ok(new DbOkResponse { Success = true });
        }
    }
}
```

**`Server/DBServer/Program.cs`** — cạnh chỗ gán `AccountDbHandler.Repository`:

```csharp
CharacterDbHandler.Repository = new CharacterRepository(database);
```

</details>

---

## Bước 3 — GameServer: world tối thiểu

### Hướng làm

Hai file mới trong `Server/GameServer/World/`:

**`PlayerEntity.cs`** — nhân vật đang sống trong world. Chỉ cần: `EntityId` (int), `CharacterId`,
`AccountId`, `Name`, `ClassId`, `Level` (đọc từ `CharacterRow`, chỉ-đọc), `MapId`/`X`/`Y` (có setter —
Phase 6 sẽ cập nhật mỗi tick), và `Owner` là `ClientSession` đang điều khiển (Phase 11 quái sẽ có
`Owner == null`). Constructor nhận `(int entityId, CharacterRow row, ClientSession owner)`.

**`WorldService.cs`** — sổ đăng ký entity đang sống. Phase 8 sẽ chia theo map và ô lưới AOI; bây giờ một
dictionary phẳng là đủ và không che mất bài học nào. Cần:

- Hằng số mặc định cho nhân vật mới: `DEFAULT_CLASS_ID = 1`, `DEFAULT_MAP_ID = 1`, `SPAWN_X`, `SPAWN_Y`
  (Phase 9 chuyển vào config).
- `Spawn(CharacterRow row, ClientSession owner)` → cấp `entityId` mới, tạo entity, ghi vào sổ, log.
- `Despawn(PlayerEntity entity)` → gỡ khỏi sổ, log.
- `TryGetByAccount(long accountId, out PlayerEntity entity)` → tài khoản này đã có entity trong world chưa.
- `OnlineCount`.

**Hai quyết định phải tự trả lời trước khi code:**

1. *Cấp `entityId` thế nào cho an toàn đa luồng?* Handler chạy trên nhiều luồng —
   `_nextEntityId++` là race. Xem cách `ClientSession` cấp `Id`.
2. *Dùng dictionary thường hay `ConcurrentDictionary`?* Spawn/Despawn được gọi từ handler của nhiều session
   khác nhau đồng thời. Muốn tra được theo cả `entityId` lẫn `accountId` thì cần mấy dictionary?

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`Server/GameServer/World/PlayerEntity.cs`**:

```csharp
using MMORPG.Shared.Dto.Db;

namespace MMORPG.GameServer.World
{
    /// <summary>
    /// Một nhân vật đang sống trong world. Tồn tại từ EnterWorld tới lúc rời world, không lâu hơn.
    ///
    /// Phase 6 thêm vận tốc và lịch sử input, Phase 8 thêm ô lưới AOI, Phase 11 thêm HP và mục tiêu.
    /// Bây giờ chỉ cần đủ để biết ai đang ở đâu.
    /// </summary>
    public sealed class PlayerEntity
    {
        public int EntityId { get; }

        /// <summary>Khoá để lưu về DB. Không bao giờ gửi cho client khác.</summary>
        public long CharacterId { get; }

        public long AccountId { get; }
        public string Name { get; }
        public int ClassId { get; }
        public int Level { get; }

        public int MapId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }

        /// <summary>Session đang điều khiển entity này. null nghĩa là NPC/quái (Phase 11).</summary>
        public ClientSession Owner { get; }

        public PlayerEntity(int entityId, CharacterRow row, ClientSession owner)
        {
            EntityId = entityId;
            CharacterId = row.CharacterId;
            AccountId = row.AccountId;
            Name = row.Name;
            ClassId = row.ClassId;
            Level = row.Level;
            MapId = row.MapId;
            X = row.X;
            Y = row.Y;
            Owner = owner;
        }
    }
}
```

**`Server/GameServer/World/WorldService.cs`**:

```csharp
using System.Collections.Concurrent;
using MMORPG.ServerCore;
using MMORPG.Shared.Dto.Db;

namespace MMORPG.GameServer.World
{
    /// <summary>
    /// Sổ đăng ký entity đang sống. Phase 8 sẽ chia theo map và ô lưới AOI;
    /// bây giờ một dictionary phẳng là đủ và không che mất bài học nào.
    /// </summary>
    public sealed class WorldService
    {
        /// <summary>Giá trị cho nhân vật tạo lần đầu. Phase 9 sẽ đọc từ bảng config.</summary>
        public const int DEFAULT_CLASS_ID = 1;
        public const int DEFAULT_MAP_ID = 1;
        public const float SPAWN_X = 0f;
        public const float SPAWN_Y = 0f;

        private readonly ConcurrentDictionary<int, PlayerEntity> _entities = new();
        private readonly ConcurrentDictionary<long, int> _entityIdByAccount = new();

        private int _nextEntityId;

        public int OnlineCount => _entities.Count;

        public PlayerEntity Spawn(CharacterRow row, ClientSession owner)
        {
            int entityId = Interlocked.Increment(ref _nextEntityId);
            var entity = new PlayerEntity(entityId, row, owner);

            _entities[entityId] = entity;
            _entityIdByAccount[row.AccountId] = entityId;

            Log.Info($"Spawn {entity.Name.Cyan()} entity {entityId.ToString().Green()} " +
                     $"tại map {entity.MapId} ({entity.X:0.##}, {entity.Y:0.##}) — " +
                     $"{OnlineCount} người trong world");

            return entity;
        }

        public void Despawn(PlayerEntity entity)
        {
            _entities.TryRemove(entity.EntityId, out _);
            _entityIdByAccount.TryRemove(entity.AccountId, out _);

            Log.Info($"Despawn {entity.Name.Cyan()} entity {entity.EntityId} — còn {OnlineCount} người");
        }

        /// <summary>
        /// Tài khoản này đã có entity trong world chưa. Cần vì một tài khoản có thể đăng nhập
        /// ở hai chỗ trong khe thời gian trước khi session cũ kịp bị đá.
        /// </summary>
        public bool TryGetByAccount(long accountId, out PlayerEntity entity)
        {
            entity = null;

            return _entityIdByAccount.TryGetValue(accountId, out int entityId) &&
                   _entities.TryGetValue(entityId, out entity);
        }
    }
}
```

</details>

---

## Bước 4 — GameServer: `CharacterService`, handler, và đường dọn dẹp

### Hướng làm

**`Server/GameServer/World/CharacterService.cs`** — nghiệp vụ vào/rời world, theo đúng vai như
`AuthService` bên auth. Hai method:

```csharp
public async Task<EnterWorldResponse> EnterWorldAsync(ClientSession session)
public async Task LeaveWorldAsync(ClientSession session)
```

`EnterWorldAsync`, theo thứ tự:

1. **Chặn vào hai lần.** `session.Entity != null` → trả `CharacterInUse`. Vì sao `MinState` không chặn
   được? — `InWorld (2) >= Authenticated (1)`, dispatcher so bằng `>=` nên session đã trong world vẫn
   lọt qua cửa `MinState = Authenticated`.
2. **Chặn tài khoản đang có entity ở session khác.** `TryGetByAccount` → `CharacterInUse`. Đây là khe
   thời gian giữa lúc session mới đăng nhập (đá session cũ) và lúc session cũ dọn xong.
3. Gọi DB `CharacterGetOrCreate` — `AccountId` lấy từ **session**, tên mặc định là `session.Username`,
   các giá trị mặc định còn lại lấy từ hằng số `WorldService`.
4. `Spawn` → `session.MarkInWorld(entity)` → trả response đầy đủ (kèm `ServerTimeMs`).

`LeaveWorldAsync` — **một hàm cho mọi đường rời world**: logout chủ động, mất kết nối, bị kick. Tách
thành hai đường là sớm muộn một đường quên lưu vị trí. Logic: session không có entity thì thôi;
có thì `MarkLeftWorld` → `Despawn` → gọi DB lưu vị trí. **Riêng chỗ lưu vị trí phải bọc
`try/catch (DbUnavailableException)`** và chỉ log warn — mất vị trí một lần chơi thì khó chịu, nhưng để
exception ném xuyên qua đường dọn dẹp ngắt kết nối thì session không dọn được, entity treo trong world
mãi mãi. (Đây là ngoại lệ có chủ đích của luật "không nuốt lỗi" — ghi comment giải thích tại chỗ.)

**Sửa `ClientSession`** — gắn entity vào vòng đời session:

```csharp
/// <summary>Entity đang điều khiển. null khi chưa vào world.</summary>
public World.PlayerEntity Entity { get; private set; }

public void MarkInWorld(World.PlayerEntity entity)   // Entity = entity; State = InWorld;
public void MarkLeftWorld()                          // Entity = null;  State = Authenticated;
```

và trong khối `finally` của `RunAsync`, **trước** `SessionRegistry.Remove(this)`, gọi
`LeaveWorldAsync(this)` — mất kết nối đột ngột cũng phải đi qua đúng đường dọn dẹp như logout chủ động.
(`CharacterService` là property static trên handler — nhớ kiểm null trước khi gọi, phòng lúc server đang
khởi động dở.)

**Sửa `AuthHandler.OnLogout`** — logout khi đang trong world phải rời world trước: thêm
`await CharacterHandler.CharacterService.LeaveWorldAsync(req.Session);` trước khi gọi `AuthService.Logout`.
Không có dòng này: người chơi logout xong, entity vẫn đứng trong world, và lần `EnterWorld` sau lãnh
`CharacterInUse` oan.

**`Server/GameServer/Handlers/CharacterHandler.cs`** — theo kiểu `AuthHandler`: class static, property
`CharacterService` gán trong `Program.cs`, một handler:

```csharp
[TcpHandler(NetCmd.EnterWorld, MinState = SessionState.Authenticated)]
```

**`Program.cs`**: tạo `WorldService`, tạo `CharacterService(dbClient, worldService)`, gán vào handler.
Tiện thể có thể cho `SystemHandler.OnServerInfo` báo `world.OnlineCount` (số người *trong world*) thay vì
số kết nối — tuỳ bạn.

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`Server/GameServer/World/CharacterService.cs`**:

```csharp
using MMORPG.GameServer.Db;
using MMORPG.ServerCore;
using MMORPG.Shared.Db;
using MMORPG.Shared.Dto;
using MMORPG.Shared.Dto.Db;
using MMORPG.Shared.Net;

namespace MMORPG.GameServer.World
{
    /// <summary>
    /// Nghiệp vụ vào / rời thế giới. Handler không chứa gì ngoài lời gọi vào đây.
    /// </summary>
    public sealed class CharacterService
    {
        private readonly DbClient _dbClient;
        private readonly WorldService _worldService;

        public CharacterService(DbClient dbClient, WorldService worldService)
        {
            _dbClient = dbClient;
            _worldService = worldService;
        }

        public async Task<EnterWorldResponse> EnterWorldAsync(ClientSession session)
        {
            // InWorld >= Authenticated nên MinState không chặn được lần gọi thứ hai — phải tự chặn.
            if (session.Entity != null)
                return Fail(ErrorCode.CharacterInUse);

            // Khe thời gian giữa lúc session mới đăng nhập (đá session cũ) và lúc session cũ dọn xong.
            if (_worldService.TryGetByAccount(session.AccountId, out _))
            {
                Log.Warn($"{session.Tag} EnterWorld bị chặn: account {session.AccountId} " +
                         "đang có entity của session khác chưa dọn xong");
                return Fail(ErrorCode.CharacterInUse);
            }

            var result = await _dbClient.CallAsync<CharacterGetOrCreateRequest, CharacterGetOrCreateResponse>(
                DbCmd.CharacterGetOrCreate,
                new CharacterGetOrCreateRequest
                {
                    AccountId = session.AccountId,
                    Name = session.Username,
                    ClassId = WorldService.DEFAULT_CLASS_ID,
                    MapId = WorldService.DEFAULT_MAP_ID,
                    X = WorldService.SPAWN_X,
                    Y = WorldService.SPAWN_Y,
                });

            if (result.Created)
                Log.Info($"{session.Tag} Lần vào world đầu tiên — tạo nhân vật {result.Character.Name.Cyan()} " +
                         $"(id {result.Character.CharacterId.ToString().Green()})");

            PlayerEntity entity = _worldService.Spawn(result.Character, session);
            session.MarkInWorld(entity);

            return new EnterWorldResponse
            {
                Success = true,
                EntityId = entity.EntityId,
                CharacterId = entity.CharacterId,
                Name = entity.Name,
                ClassId = entity.ClassId,
                Level = entity.Level,
                MapId = entity.MapId,
                X = entity.X,
                Y = entity.Y,
                ServerTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
        }

        /// <summary>
        /// Rời world: bỏ entity rồi lưu vị trí. Gọi cả khi logout chủ động lẫn khi mất kết nối —
        /// hai đường phải đi qua đúng một hàm, nếu không sớm muộn một đường sẽ quên lưu.
        /// </summary>
        public async Task LeaveWorldAsync(ClientSession session)
        {
            PlayerEntity entity = session.Entity;
            if (entity == null)
                return;

            session.MarkLeftWorld();
            _worldService.Despawn(entity);

            try
            {
                await _dbClient.CallAsync<CharacterSavePositionRequest, DbOkResponse>(
                    DbCmd.CharacterSavePosition,
                    new CharacterSavePositionRequest
                    {
                        CharacterId = entity.CharacterId,
                        MapId = entity.MapId,
                        X = entity.X,
                        Y = entity.Y,
                    });
            }
            catch (DbUnavailableException ex)
            {
                // Mất vị trí của một lần chơi thì khó chịu, nhưng làm sập đường ngắt kết nối
                // thì tệ hơn nhiều: session không dọn được, entity treo lại trong world mãi mãi.
                Log.Warn($"Không lưu được vị trí của {entity.Name.Cyan()}: {ex.Message}");
            }
        }

        private static EnterWorldResponse Fail(ErrorCode error)
        {
            return new EnterWorldResponse { Success = false, Error = error };
        }
    }
}
```

**`Server/GameServer/Handlers/CharacterHandler.cs`**:

```csharp
using MMORPG.GameServer.Net;
using MMORPG.GameServer.World;
using MMORPG.Shared.Net;

namespace MMORPG.GameServer.Handlers
{
    public static class CharacterHandler
    {
        /// <summary>Gán một lần trong <c>Program.cs</c>.</summary>
        public static CharacterService CharacterService { get; set; }

        [TcpHandler(NetCmd.EnterWorld, MinState = SessionState.Authenticated)]
        public static async Task<NetResult> OnEnterWorld(NetRequest req)
        {
            return NetResult.Ok(await CharacterService.EnterWorldAsync(req.Session));
        }
    }
}
```

**`ClientSession.cs`** — thêm phần world:

```csharp
        /// <summary>Entity đang điều khiển. null khi chưa vào world.</summary>
        public World.PlayerEntity Entity { get; private set; }

        public void MarkInWorld(World.PlayerEntity entity)
        {
            Entity = entity;
            State = SessionState.InWorld;
        }

        public void MarkLeftWorld()
        {
            Entity = null;
            State = SessionState.Authenticated;
        }
```

và trong khối `finally` của `RunAsync`, trước `SessionRegistry.Remove(this)`:

```csharp
                // Mất kết nối đột ngột cũng phải đi qua đúng đường dọn dẹp như logout chủ động.
                if (Handlers.CharacterHandler.CharacterService != null)
                    await Handlers.CharacterHandler.CharacterService.LeaveWorldAsync(this);
```

**`AuthHandler.OnLogout`** — rời world trước khi đăng xuất:

```csharp
        [TcpHandler(NetCmd.Logout, MinState = SessionState.Authenticated)]
        public static async Task<NetResult> OnLogout(NetRequest req)
        {
            // Logout khi đang trong world: rời world trước, cùng một đường dọn dẹp với mất kết nối.
            await CharacterHandler.CharacterService.LeaveWorldAsync(req.Session);
            return NetResult.Ok(AuthService.Logout(req.Session));
        }
```

**`Program.cs`** (GameServer) — cạnh chỗ gán `AuthHandler.AuthService`:

```csharp
var worldService = new WorldService();
CharacterHandler.CharacterService = new CharacterService(dbClient, worldService);
```

</details>

### ✅ CHECKPOINT A — kiểm bằng gói tin trước khi làm phần client hoàn chỉnh

Thêm tạm vào `NetworkProbe` một nút gửi `EnterWorld` (payload `EmptyRequest`). Trình tự kiểm:

1. Đăng nhập → bấm EnterWorld. Lần đầu server phải log đủ chuỗi:
   ```
   INFO  [CharacterService] #1 Lần vào world đầu tiên — tạo nhân vật hung (id 1)
   INFO  [WorldService]     Spawn hung entity 1 tại map 1 (0, 0) — 1 người trong world
   ```
2. Bấm EnterWorld **lần nữa** → client nhận `CharacterInUse`, server **không** spawn entity thứ hai.
3. Thoát Play mode → server log `Despawn hung entity 1 — còn 0 người`.
4. Play lại, đăng nhập lại, EnterWorld → **không** còn dòng "Tạo nhân vật" (get-or-create idempotent),
   chỉ có Spawn.

---

## Bước 5 — Client: vào thế giới

### Hướng làm

Điểm dễ chịu của phase này phía client: **không có màn hình UI mới nào**. Đăng nhập xong là tự vào world.
Năm file mới + đăng ký DI:

**`Assets/Game/Scripts/World/WorldApi.cs`** — chiều gửi, đối xứng với `AuthApi`: một method
`EnterWorld()` gửi `NetCmd.EnterWorld` với `EmptyRequest`.

**`Assets/Game/Scripts/Network/Handlers/WorldNetHandler.cs`** — chiều nhận, đối xứng với `AuthNetHandler`:
một event `OnEnteredWorld` bắn `EnterWorldResponse`.

**`Assets/Game/Scripts/World/LocalPlayer.cs`** — bản sao dữ liệu nhân vật của *chính mình* do server gửi
xuống. **Cache chỉ-đọc, không phải nguồn sự thật**: mọi property setter đều private, thay đổi duy nhất qua
`Apply(EnterWorldResponse)` và `Clear()`, chỉ được gọi từ chỗ nhận gói server. Ngày nào có `player.Level++`
ở đâu đó trong code client là ngày golden rule #2 bị phá.

**`Assets/Game/Scripts/World/WorldSpawner.cs`** — MonoBehaviour dựng biểu diễn hình ảnh:
`SpawnLocalPlayer(EnterWorldResponse)` instantiate prefab tại `(response.X, response.Y)` (nhất định
**từ response**, không phải hằng số — không thì bug "nhân vật luôn hiện ở (0,0)" sẽ im lặng chờ bạn),
đặt tên object cho dễ debug, trỏ camera theo. `DespawnLocalPlayer()` cho đường logout/kick.
Phase 7 sẽ thêm entity của người khác, Phase 8 mới cần `com.hungnt.objectpool` — giờ `Instantiate` là đủ.

**`Assets/Game/Scripts/World/CameraFollow.cs`** — bám mượt bằng `SmoothDamp`.
Câu hỏi thiết kế: vì sao phải là `LateUpdate` chứ không phải `Update`?

**`Assets/Game/Scripts/World/WorldPresenter.cs`** — MonoBehaviour nối các mảnh:
nghe `AuthNetHandler.OnLoginResult` (thành công → gọi `WorldApi.EnterWorld()`),
nghe `WorldNetHandler.OnEnteredWorld` (thành công → `LocalPlayer.Apply` + `WorldSpawner.SpawnLocalPlayer`),
nghe `OnKicked`/`OnLogoutResult` để despawn + `Clear`. Subscribe/unsubscribe theo đúng kiểu `LoginPresenter`.

**Đăng ký DI — bước dễ quên nhất phase, đọc lại CLAUDE.md nếu quên vì sao:**

```csharp
builder.Register<WorldApi>(Lifetime.Singleton);
builder.Register<LocalPlayer>(Lifetime.Singleton);
builder.Register<WorldNetHandler>(Lifetime.Singleton).AsSelf().As<INetHandlerGroup>();
builder.RegisterComponentInHierarchy<WorldSpawner>();
builder.RegisterComponentInHierarchy<WorldPresenter>();
```

Quên dòng `WorldNetHandler` thì `EnterWorldResponse` rơi vào hư không **không lỗi biên dịch, không log** —
đó là anti-pattern số 4 của repo.

**Trong scene:** một sprite bất kỳ làm prefab nhân vật (ô vuông màu là đủ), một `Grid + Tilemap` vẽ vài ô
sàn để nhìn ra camera đang di chuyển, `CameraFollow` gắn lên Main Camera, `WorldSpawner` + `WorldPresenter`
trên GameObject trong scene, kéo tham chiếu trong Inspector.

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`Assets/Game/Scripts/World/WorldApi.cs`**:

```csharp
using HungNT;
using MMORPG.Client.Network;
using MMORPG.Shared.Dto;
using MMORPG.Shared.Net;

namespace MMORPG.Client.World
{
    /// <summary>
    /// Gom mọi lệnh world mà client GỬI ĐI. Đối xứng với <see cref="Network.Handlers.WorldNetHandler"/> ở chiều nhận.
    /// </summary>
    public sealed class WorldApi
    {
        private readonly NetService _netService;

        public WorldApi(NetService netService)
        {
            _netService = netService;
        }

        public void EnterWorld()
        {
            this.Log("EnterWorld");
            _netService.Send(NetCmd.EnterWorld, new EmptyRequest());
        }
    }
}
```

**`Assets/Game/Scripts/Network/Handlers/WorldNetHandler.cs`**:

```csharp
using System;
using MMORPG.Shared.Dto;
using MMORPG.Shared.Net;

namespace MMORPG.Client.Network.Handlers
{
    public sealed class WorldNetHandler : INetHandlerGroup
    {
        public event Action<EnterWorldResponse> OnEnteredWorld;

        [NetHandler(NetCmd.EnterWorld)]
        private void HandleEnterWorld(NetPacket packet)
        {
            OnEnteredWorld?.Invoke(packet.GetData<EnterWorldResponse>());
        }
    }
}
```

**`Assets/Game/Scripts/World/LocalPlayer.cs`**:

```csharp
using MMORPG.Shared.Dto;

namespace MMORPG.Client.World
{
    /// <summary>
    /// Bản sao dữ liệu nhân vật của CHÍNH mình, do server gửi xuống.
    ///
    /// Đây là cache chỉ-đọc, không phải nguồn sự thật. Không có setter công khai:
    /// mọi thay đổi đều đi qua <see cref="Apply"/> và chỉ được gọi từ handler nhận gói của server.
    /// Ngày nào có `player.Level++` ở đâu đó trong code client là ngày golden rule #2 bị phá.
    /// </summary>
    public sealed class LocalPlayer
    {
        public bool IsInWorld { get; private set; }

        public int EntityId { get; private set; }
        public long CharacterId { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public int ClassId { get; private set; }
        public int Level { get; private set; }
        public int MapId { get; private set; }
        public float X { get; private set; }
        public float Y { get; private set; }

        public void Apply(EnterWorldResponse response)
        {
            IsInWorld = true;
            EntityId = response.EntityId;
            CharacterId = response.CharacterId;
            Name = response.Name;
            ClassId = response.ClassId;
            Level = response.Level;
            MapId = response.MapId;
            X = response.X;
            Y = response.Y;
        }

        public void Clear()
        {
            IsInWorld = false;
            EntityId = 0;
            CharacterId = 0;
            Name = string.Empty;
        }
    }
}
```

**`Assets/Game/Scripts/World/WorldSpawner.cs`**:

```csharp
using HungNT;
using MMORPG.Shared.Dto;
using UnityEngine;

namespace MMORPG.Client.World
{
    /// <summary>
    /// Dựng biểu diễn hình ảnh của entity. Phase 7 sẽ thêm entity của người khác,
    /// Phase 8 dùng <c>com.hungnt.objectpool</c> vì AOI spawn/despawn liên tục.
    /// Bây giờ chỉ một nhân vật, <c>Instantiate</c> là đủ.
    /// </summary>
    public sealed class WorldSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject _playerPrefab;
        [SerializeField] private Transform _entityRoot;
        [SerializeField] private CameraFollow _cameraFollow;

        private GameObject _localPlayerObject;

        public void SpawnLocalPlayer(EnterWorldResponse response)
        {
            if (_localPlayerObject != null)
                Destroy(_localPlayerObject);

            _localPlayerObject = Instantiate(
                _playerPrefab, new Vector3(response.X, response.Y, 0f), Quaternion.identity, _entityRoot);
            _localPlayerObject.name = $"Player_{response.EntityId}_{response.Name}";

            _cameraFollow.SetTarget(_localPlayerObject.transform);

            this.Log($"Vào map {response.MapId} tại ({response.X:0.##}, {response.Y:0.##}) — entity {response.EntityId}");
        }

        public void DespawnLocalPlayer()
        {
            if (_localPlayerObject == null)
                return;

            Destroy(_localPlayerObject);
            _localPlayerObject = null;
            _cameraFollow.SetTarget(null);
        }
    }
}
```

**`Assets/Game/Scripts/World/CameraFollow.cs`**:

```csharp
using UnityEngine;

namespace MMORPG.Client.World
{
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] private float _smoothTime = 0.15f;
        [SerializeField] private Vector3 _offset = new(0f, 0f, -10f);

        private Transform _target;
        private Vector3 _velocity;

        public void SetTarget(Transform target)
        {
            _target = target;
        }

        // LateUpdate chứ không phải Update: nhân vật phải di chuyển xong rồi camera mới bám theo.
        // Làm ngược lại thì camera luôn trễ một frame và hình bị rung nhẹ.
        private void LateUpdate()
        {
            if (_target == null)
                return;

            transform.position = Vector3.SmoothDamp(
                transform.position, _target.position + _offset, ref _velocity, _smoothTime);
        }
    }
}
```

**`Assets/Game/Scripts/World/WorldPresenter.cs`**:

```csharp
using HungNT;
using MMORPG.Client.Network.Handlers;
using MMORPG.Shared.Dto;
using UnityEngine;
using VContainer;

namespace MMORPG.Client.World
{
    /// <summary>
    /// Nối auth với world: đăng nhập xong tự gửi EnterWorld, nhận response thì spawn.
    /// Không có UI riêng — phase này client vào thẳng game.
    /// </summary>
    public sealed class WorldPresenter : MonoBehaviour
    {
        [SerializeField] private WorldSpawner _worldSpawner;

        private WorldApi _worldApi;
        private WorldNetHandler _worldNetHandler;
        private AuthNetHandler _authNetHandler;
        private LocalPlayer _localPlayer;

        [Inject]
        public void Construct(WorldApi worldApi, WorldNetHandler worldNetHandler,
                              AuthNetHandler authNetHandler, LocalPlayer localPlayer)
        {
            _worldApi = worldApi;
            _worldNetHandler = worldNetHandler;
            _authNetHandler = authNetHandler;
            _localPlayer = localPlayer;
        }

        private void Start()
        {
            _authNetHandler.OnLoginResult += OnLoggedIn;
            _authNetHandler.OnLogoutResult += OnLoggedOut;
            _authNetHandler.OnKicked += OnKicked;
            _worldNetHandler.OnEnteredWorld += OnEnteredWorld;
        }

        private void OnDestroy()
        {
            if (_authNetHandler == null)
                return;

            _authNetHandler.OnLoginResult -= OnLoggedIn;
            _authNetHandler.OnLogoutResult -= OnLoggedOut;
            _authNetHandler.OnKicked -= OnKicked;
            _worldNetHandler.OnEnteredWorld -= OnEnteredWorld;
        }

        private void OnLoggedIn(AuthResponse response)
        {
            if (!response.Success)
                return;

            _worldApi.EnterWorld();
        }

        private void OnEnteredWorld(EnterWorldResponse response)
        {
            if (!response.Success)
            {
                this.LogWarning($"EnterWorld thất bại: {response.Error}");
                return;
            }

            _localPlayer.Apply(response);
            _worldSpawner.SpawnLocalPlayer(response);
        }

        private void OnLoggedOut(AuthResponse response)
        {
            _worldSpawner.DespawnLocalPlayer();
            _localPlayer.Clear();
        }

        private void OnKicked(KickedNotice notice)
        {
            _worldSpawner.DespawnLocalPlayer();
            _localPlayer.Clear();
        }
    }
}
```

**`GameLifetimeScope.Configure`** — thêm cuối:

```csharp
            builder.Register<WorldApi>(Lifetime.Singleton);
            builder.Register<LocalPlayer>(Lifetime.Singleton);
            builder.Register<WorldNetHandler>(Lifetime.Singleton).AsSelf().As<INetHandlerGroup>();

            // MonoBehaviour có sẵn trong scene — đăng ký instance, không phải type.
            builder.RegisterComponentInHierarchy<WorldSpawner>();
            builder.RegisterComponentInHierarchy<WorldPresenter>();
```

</details>

### ✅ CHECKPOINT B — mục tiêu cuối Phase 5

1. Bật DBServer, GameServer, Play Unity.
2. Đăng nhập → **không cần bấm gì thêm**, nhân vật (ô vuông) hiện ra tại `(0, 0)`, camera bám vào nó.
3. Server log đủ chuỗi:
   ```
   INFO  [AuthService]      #1 hung đăng nhập thành công
   INFO  [CharacterService] #1 Lần vào world đầu tiên — tạo nhân vật hung (id 1)
   INFO  [WorldService]     Spawn hung entity 1 tại map 1 (0, 0) — 1 người trong world
   ```
4. **Sửa toạ độ trong DB** để chứng minh vị trí thật sự được nạp từ đĩa:
   ```sql
   UPDATE character SET pos_x = 5, pos_y = 3 WHERE id = 1;
   ```
   Thoát Play mode, Play lại, đăng nhập → nhân vật xuất hiện ở `(5, 3)`.
5. Thoát Play mode giữa lúc trong world → server log `Despawn ... — còn 0 người`.
6. Đăng nhập lại lần nữa → **không** có dòng "tạo nhân vật" (chỉ tạo đúng một lần).

---

## Bước 6 — Bốn thử nghiệm bắt buộc

**1. Hai client cùng tài khoản.** Chạy bản build song song Editor (hoặc ParrelSync), đăng nhập cùng tài
khoản ở cả hai. Client thứ hai đá client thứ nhất (cơ chế Phase 4) → session cũ đóng → `LeaveWorldAsync`
trong `finally` chạy → entity cũ biến mất → client mới vào world bình thường.
- Nếu client mới lãnh `CharacterInUse` mãi → entity của session cũ chưa được dọn: kiểm `LeaveWorldAsync`
  có nằm trong `finally` của `RunAsync` không.
- Nếu log có **hai** entity cùng một tài khoản → `TryGetByAccount` chưa được kiểm trước khi spawn.

**2. EnterWorld hai lần trên cùng session.** Bấm nút probe hai lần liên tiếp → lần hai nhận
`CharacterInUse`, world vẫn chỉ có một entity. Đây là chỗ chứng minh vì sao `MinState` một mình không đủ.

**3. Logout rồi vào lại.** Đang trong world → Logout → server log Despawn, client despawn ô vuông →
đăng nhập lại → vào lại bình thường (không `CharacterInUse`). Nếu kẹt `CharacterInUse` thì
`AuthHandler.OnLogout` chưa gọi `LeaveWorldAsync`.

**4. Tắt GameServer đột ngột (`Ctrl+C`) khi đang trong world.** Bật lại, đăng nhập, vào world → vị trí
vẫn đúng như DB. Phase này chưa di chuyển được nên giá trị chưa đổi; Phase 6 sẽ biến thử nghiệm này thành
có ý nghĩa thật (di chuyển → rớt mạng → vào lại đúng chỗ).

---

## Troubleshooting

| Triệu chứng | Nguyên nhân | Xử lý |
|-------------|-------------|-------|
| `EnterWorld` không có phản hồi, không lỗi | `WorldNetHandler` chưa đăng ký trong `GameLifetimeScope` | Thêm `.AsSelf().As<INetHandlerGroup>()` — anti-pattern số 4 |
| `NotAuthenticated` khi gửi `EnterWorld` | Gửi trước khi login xong, hoặc gửi trên kết nối khác | `EnterWorld` chỉ gửi từ callback `OnLoginResult` thành công; một client giữ **một** kết nối suốt phiên |
| `CharacterInUse` mãi không hết | Entity ma còn trong `WorldService` — đường dọn dẹp thiếu | Kiểm `LeaveWorldAsync` có trong `finally` của `RunAsync` **và** trong `OnLogout` |
| `SqliteException: no such table: character` | Migration 3 chưa chạy | Kiểm đã thêm vào `_migrations`; lúc dev lỡ sửa migration cũ thì xoá `mmorpg.db`, `.db-wal`, `.db-shm` |
| `foreign key constraint failed` khi tạo nhân vật | `account_id` không tồn tại, hoặc `PRAGMA foreign_keys` chưa bật | Kiểm `Database` có bật foreign keys lúc mở connection |
| Mỗi lần đăng nhập lại tạo nhân vật mới | Thiếu `UNIQUE` trên `account_id`, hoặc SELECT-trước-INSERT sai cột | Kiểm schema migration 3 và `SelectByAccountAsync` |
| Nhân vật hiện ở `(0,0)` dù DB ghi khác | Client dựng vị trí từ hằng số thay vì từ response | `WorldSpawner` phải dùng `response.X`, `response.Y` |
| Camera không bám | `SetTarget` chưa được gọi, hoặc tham chiếu Inspector thiếu | Kiểm `_cameraFollow` trong `WorldSpawner` |
| `VContainerException: ... WorldSpawner` (hoặc field inject null không lỗi) | Quên `RegisterComponentInHierarchy` | Đọc lỗi từ **dòng cuối** chuỗi `Failed to resolve` — xem CLAUDE.md |
| Unity không thấy `EnterWorldResponse` | Chưa build `Shared` | `dotnet build Server/Shared` |

---

## Tự kiểm tra hiểu bài

1. Nêu một thứ chỉ có ở `Entity`, một thứ chỉ có ở `Character`, và giải thích vì sao thứ đầu không nên vào DB.
2. Vì sao `entityId` là `int` cấp lúc chạy chứ không dùng thẳng `character.id`? Nêu **ba** lý do khác nhau.
3. Bản thiết kế cũ có `EnterWorldRequest { CharacterId }` và kèm theo nó là một lỗ hổng bảo mật phải nhớ
   kiểm thủ công. Bản này client không gửi gì cả. Lỗ hổng đó biến đi đâu?
4. `EnterWorld` là lệnh riêng — vì sao không nhét luôn dữ liệu world vào `AuthResponse` cho đỡ một round-trip?
   (Gợi ý: Phase 8 đổi map, client cần thời gian load scene, và `Logout` không cắt TCP.)
5. `GetOrCreateAsync` không dùng transaction mà vẫn an toàn trước hai request song song. Cái gì đang gánh
   vai trò "khoá"? Chuỗi sự kiện cụ thể khi hai INSERT đua nhau là gì?
6. `MinState = Authenticated` không chặn được `EnterWorld` gọi hai lần. Vì sao? Và vì sao ta chọn so `>=`
   trong dispatcher thay vì so `==`?
7. `LeaveWorldAsync` nuốt `DbUnavailableException` thay vì để nó ném lên, trong khi repo cấm nuốt lỗi.
   Vì sao ở **đây** thì nuốt là đúng?
8. Quan hệ Account↔Character là 1-1 nhưng vẫn tách hai bảng. Nêu hai lợi ích cụ thể của việc tách,
   và một chi phí phải trả.
9. `LocalPlayer` chỉ có setter private và một hàm `Apply`. Nếu mở setter công khai cho tiện thì golden rule
   nào bị phá, và triệu chứng đầu tiên sẽ xuất hiện ở phase nào?
10. Logout khi đang trong world phải gọi `LeaveWorldAsync` trước `AuthService.Logout`. Nếu đảo thứ tự
    (logout trước, rời world sau) thì hỏng ở đâu? (Gợi ý: `MarkLoggedOut` đặt `AccountId = 0`.)

---

**Xong Phase 5 → kết thúc Chặng B.** Người chơi có danh tính, nhân vật, và một chỗ đứng trong thế giới.
Chặng C bắt đầu bằng [PHASE-6](PHASE-6.md): server chạy tick cố định, client gửi *ý định* di chuyển,
server quyết vị trí — và bạn sẽ hiểu vì sao nhân vật trong game online luôn hơi "trượt" một chút.
(Tài liệu Phase 6 sẽ được viết khi bạn báo đã xong Phase 5 — kèm đánh giá format hướng-làm/lời-giải này.)
