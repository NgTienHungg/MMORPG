# PHASE 5 — Nhân vật & vào thế giới: ba danh tính của một người chơi

> **Kết quả cuối Phase 5:** đăng nhập → màn hình chọn nhân vật → tạo nhân vật → bấm Vào game →
> nhân vật xuất hiện trên map ở đúng vị trí lần trước thoát ra, camera bám theo.
>
> **Điều kiện:** xong [`PHASE-4.md`](PHASE-4.md) tới CHECKPOINT B và cả 4 thử nghiệm ở Bước 7.
>
> **Bài học chính:** `Account`, `Character` và `Entity` là **ba** thứ khác nhau, sống ở ba nơi khác nhau,
> có vòng đời khác nhau. Gộp chúng lại là sai lầm kiến trúc phổ biến nhất của người mới viết game server —
> và nó chỉ lộ ra ở Phase 11, lúc quái vật cũng cần là entity nhưng chẳng có tài khoản nào.

---

## Ba danh tính

| | Account | Character | Entity |
|---|---------|-----------|--------|
| **Là gì** | Danh tính đăng nhập | Nhân vật trong game | Đối tượng đang sống trong world |
| **Sống ở** | Bảng `account` | Bảng `character` | RAM của GameServer |
| **Id** | `account.id` (long) | `character.id` (long) | `entityId` (int, cấp lúc chạy) |
| **Vòng đời** | Từ lúc đăng ký tới lúc xoá | Từ lúc tạo tới lúc xoá | Từ `EnterWorld` tới lúc rời map |
| **Số lượng** | 1 người 1 account | 1 account có tối đa 3 character | 1 character online = 1 entity |
| **Client được biết** | ❌ không bao giờ | ⚠️ chỉ id nhân vật của chính mình | ✅ entityId của mọi ai trong tầm nhìn |

Ba câu hỏi làm rõ vì sao phải tách:

**Vì sao `Entity` không phải là `Character`?**
Entity mang những thứ chỉ có nghĩa khi đang online: mục tiêu đang đánh, buff còn 3 giây, ô lưới AOI đang đứng,
vector vận tốc. Không thứ nào cần vào DB. Nếu `Character` gánh luôn phần đó thì object nào cũng nửa "dữ liệu lâu dài"
nửa "trạng thái tạm", và bạn sẽ không bao giờ trả lời dứt khoát được câu "cái gì cần lưu?".

**Vì sao `entityId` không dùng thẳng `character.id`?**
Ba lý do, xếp theo mức quan trọng tăng dần: (1) `int` gọn hơn `long` trên gói tin gửi 20 lần/giây;
(2) Phase 11 quái vật cũng cần entity id mà chúng không có character; (3) `character.id` là thông tin lâu dài về
người khác — không có lý do gì để client biết id thật của người bên cạnh.

**Vì sao một `Account` có nhiều `Character`?**
Đó là chuẩn của thể loại. Quan trọng hơn: nó ép bạn viết đúng câu hỏi ngay từ đầu — "nhân vật này có thuộc
tài khoản đang hỏi không?" Nếu 1-1 thì câu hỏi đó không tồn tại, và tới lúc cần thì phải sửa khắp nơi.

---

## Luồng sẽ dựng

```
Đăng nhập xong (SessionState.Authenticated)
      │
      ├─► NetCmd.CharacterList ──► CharacterService ──► DB ──► danh sách 0..3 nhân vật
      │
      ├─► NetCmd.CharacterCreate ─► kiểm tên, kiểm slot ──► DB (UNIQUE trên name_key)
      │
      └─► NetCmd.EnterWorld { CharacterId }
              └─► ❗ kiểm nhân vật này CÓ THUỘC account của session không
                   └─► load nhân vật từ DB
                        └─► WorldService.Spawn() → PlayerEntity (entityId mới)
                             └─► session.MarkInWorld(entity)
                                  └─► EnterWorldResponse { EntityId, X, Y, MapId, ... }
                                       └─► Client: load scene game, spawn prefab, camera bám
```

Mũi tên có `❗` là dòng code quan trọng nhất Phase này. Bỏ nó đi thì mọi người chơi vào được nhân vật của bất kỳ ai,
chỉ bằng cách đổi một con số trong gói tin.

---

## Bước 1 — Shared: contract của character

**Sửa** `Server/Shared/Net/NetCmd.cs`:

```csharp
        #region Character (200–299)

        /// <summary>
        /// Xin danh sách nhân vật của tài khoản đang đăng nhập.
        /// Request: <see cref="Dto.EmptyRequest"/> · Response: <see cref="Dto.CharacterListResponse"/>
        /// </summary>
        CharacterList = 200,

        /// <summary>
        /// Tạo nhân vật mới.
        /// Request: <see cref="Dto.CharacterCreateRequest"/> · Response: <see cref="Dto.CharacterCreateResponse"/>
        /// </summary>
        CharacterCreate = 201,

        /// <summary>
        /// Xoá nhân vật.
        /// Request: <see cref="Dto.CharacterDeleteRequest"/> · Response: <see cref="Dto.CharacterDeleteResponse"/>
        /// </summary>
        CharacterDelete = 202,

        /// <summary>
        /// Chọn nhân vật và vào thế giới.
        /// Request: <see cref="Dto.EnterWorldRequest"/> · Response: <see cref="Dto.EnterWorldResponse"/>
        /// </summary>
        EnterWorld = 203,

        /// <summary>
        /// Rời thế giới về màn hình chọn nhân vật (không cắt TCP).
        /// Request: <see cref="Dto.EmptyRequest"/> · Response: <see cref="Dto.LeaveWorldResponse"/>
        /// </summary>
        LeaveWorld = 204,

        #endregion
```

**Sửa** `Server/Shared/Db/DbCmd.cs` — dải `1200–1299`:

```csharp
        #region Character (1200–1299)

        CharacterListByAccount = 1200,
        CharacterCreate = 1201,
        CharacterDelete = 1202,
        CharacterLoad = 1203,
        CharacterSavePosition = 1204,

        #endregion
```

**File mới:** `Server/Shared/Dto/Character/CharacterDto.cs`

```csharp
using System;
using MemoryPack;

namespace MMORPG.Shared.Dto
{
    /// <summary>
    /// Một dòng trong màn hình chọn nhân vật. Cố tình gọn — không có toạ độ, không có chỉ số chi tiết.
    /// Màn hình chọn không cần chúng, và thứ không gửi thì không lộ.
    /// </summary>
    [MemoryPackable]
    public partial class CharacterSummary
    {
        public long CharacterId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int ClassId { get; set; }
        public int Level { get; set; }
        public int MapId { get; set; }
    }

    [MemoryPackable]
    public partial class CharacterListResponse
    {
        public CharacterSummary[] Characters { get; set; } = Array.Empty<CharacterSummary>();

        /// <summary>Số slot tối đa, để client tự biết còn tạo được nữa không mà không hard-code.</summary>
        public int MaxSlots { get; set; }
    }

    [MemoryPackable]
    public partial class CharacterCreateRequest
    {
        public string Name { get; set; } = string.Empty;
        public int ClassId { get; set; }
    }

    [MemoryPackable]
    public partial class CharacterCreateResponse
    {
        public bool Success { get; set; }
        public Net.ErrorCode Error { get; set; }
        public CharacterSummary? Character { get; set; }
    }

    [MemoryPackable]
    public partial class CharacterDeleteRequest
    {
        public long CharacterId { get; set; }

        /// <summary>
        /// Người chơi phải gõ lại đúng tên nhân vật. Đây là hàng rào cho chính họ, không phải bảo mật —
        /// nhưng server vẫn kiểm, vì client có thể bỏ qua hộp thoại xác nhận.
        /// </summary>
        public string ConfirmName { get; set; } = string.Empty;
    }

    [MemoryPackable]
    public partial class CharacterDeleteResponse
    {
        public bool Success { get; set; }
        public Net.ErrorCode Error { get; set; }
        public long CharacterId { get; set; }
    }

    [MemoryPackable]
    public partial class EnterWorldRequest
    {
        public long CharacterId { get; set; }
    }

    [MemoryPackable]
    public partial class EnterWorldResponse
    {
        public bool Success { get; set; }
        public Net.ErrorCode Error { get; set; }

        /// <summary>Id runtime trong world. Chỉ có nghĩa tới khi rời map.</summary>
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

    [MemoryPackable]
    public partial class LeaveWorldResponse
    {
        public bool Success { get; set; }
    }
}
```

**File mới:** `Server/Shared/Dto/Db/CharacterDbDto.cs`

```csharp
using System;
using MemoryPack;

namespace MMORPG.Shared.Dto.Db
{
    [MemoryPackable]
    public partial class CharacterListRequest
    {
        public long AccountId { get; set; }
    }

    [MemoryPackable]
    public partial class CharacterListResult
    {
        public CharacterRow[] Characters { get; set; } = Array.Empty<CharacterRow>();
    }

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

    [MemoryPackable]
    public partial class CharacterCreateDbRequest
    {
        public long AccountId { get; set; }
        public string Name { get; set; } = string.Empty;

        /// <summary>Tên đã chuẩn hoá chữ thường — cột có ràng buộc UNIQUE là cột này.</summary>
        public string NameKey { get; set; } = string.Empty;

        public int ClassId { get; set; }
        public int MapId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
    }

    [MemoryPackable]
    public partial class CharacterCreateDbResponse
    {
        public bool Created { get; set; }

        /// <summary>false khi trùng tên, true khi tài khoản đã đủ slot — hai lý do khác nhau.</summary>
        public bool SlotFull { get; set; }

        public long CharacterId { get; set; }
    }

    [MemoryPackable]
    public partial class CharacterLoadRequest
    {
        public long CharacterId { get; set; }

        /// <summary>
        /// Server truyền xuống để DB lọc luôn. KHÔNG phải để tin client —
        /// giá trị này lấy từ session, và đây là tầng phòng thủ thứ hai.
        /// </summary>
        public long AccountId { get; set; }
    }

    [MemoryPackable]
    public partial class CharacterLoadResponse
    {
        public bool Found { get; set; }
        public CharacterRow? Character { get; set; }
    }

    [MemoryPackable]
    public partial class CharacterDeleteDbRequest
    {
        public long CharacterId { get; set; }
        public long AccountId { get; set; }
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

---

## Bước 2 — DBServer: bảng `character`

**Thêm** vào cuối `_migrations` trong `Migrator.cs`:

```csharp
            (3, """
                CREATE TABLE character (
                    id         INTEGER PRIMARY KEY AUTOINCREMENT,
                    account_id INTEGER NOT NULL REFERENCES account(id) ON DELETE CASCADE,
                    name       TEXT    NOT NULL,
                    name_key   TEXT    NOT NULL,
                    class_id   INTEGER NOT NULL,
                    level      INTEGER NOT NULL DEFAULT 1,
                    exp        INTEGER NOT NULL DEFAULT 0,
                    map_id     INTEGER NOT NULL,
                    pos_x      REAL    NOT NULL,
                    pos_y      REAL    NOT NULL,
                    created_at TEXT    NOT NULL,
                    deleted_at TEXT
                );

                -- Chỉ nhân vật CHƯA xoá mới phải giữ tên duy nhất. Nhân vật đã xoá
                -- nằm ngoài index, nên tên của nó được trả lại cho người khác dùng.
                CREATE UNIQUE INDEX idx_character_name_key
                    ON character (name_key) WHERE deleted_at IS NULL;

                CREATE INDEX idx_character_account ON character (account_id) WHERE deleted_at IS NULL;
                """),
```

Ba quyết định đáng giải thích:

**`name` và `name_key` là hai cột.** `name` giữ đúng cách viết hoa người chơi chọn (`HùngKiếm`), `name_key` là bản
chữ thường dùng để so trùng. Không thể dùng một cột cho cả hai: hiển thị thì cần nguyên dạng, so trùng thì cần
chuẩn hoá, và `COLLATE NOCASE` của SQLite không đủ tin cậy với tiếng Việt có dấu.

**Xoá mềm (`deleted_at`), không `DELETE`.** Người chơi xoá nhầm nhân vật cấp 40 là chuyện sẽ xảy ra. Xoá mềm cho bạn
khả năng khôi phục bằng một câu `UPDATE`. Cái giá là **mọi** query từ nay phải có `WHERE deleted_at IS NULL` —
quên một chỗ là nhân vật ma hiện lên. Đó là đánh đổi có thật, và ta chọn vế an toàn cho dữ liệu người chơi.

**Index một phần trả lại tên.** Hệ quả: xoá `HùngKiếm` thì người khác đăng ký được tên đó ngay. Nếu muốn giữ chỗ
(nhiều game làm vậy để tránh mạo danh) thì bỏ mệnh đề `WHERE` khỏi index — và chấp nhận rằng tên đã xoá mất vĩnh viễn.
Ta chọn trả lại, vì dự án học thì việc thử lại tên cũ tiện hơn.

**File mới:** `Server/DBServer/Repositories/CharacterRepository.cs`

```csharp
using Microsoft.Data.Sqlite;
using MMORPG.DBServer.Data;
using MMORPG.Shared.Dto.Db;

namespace MMORPG.DBServer.Repositories
{
    public sealed class CharacterRepository
    {
        private const int SQLITE_CONSTRAINT_UNIQUE = 2067;

        /// <summary>Đặt ở đây vì ràng buộc slot phải được thi hành trong CÙNG transaction với INSERT.</summary>
        public const int MAX_SLOTS = 3;

        private const string SELECT_COLUMNS =
            "id, account_id, name, class_id, level, exp, map_id, pos_x, pos_y";

        private readonly Database _database;

        public CharacterRepository(Database database) => _database = database;

        public async Task<CharacterRow[]> ListByAccountAsync(long accountId, CancellationToken ct = default)
        {
            await using SqliteConnection connection = await _database.OpenAsync(ct);
            await using SqliteCommand cmd = connection.CreateCommand();

            cmd.CommandText = $"""
                SELECT {SELECT_COLUMNS} FROM character
                WHERE account_id = $accountId AND deleted_at IS NULL
                ORDER BY id;
                """;
            cmd.Parameters.AddWithValue("$accountId", accountId);

            var rows = new List<CharacterRow>();
            await using SqliteDataReader reader = await cmd.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
                rows.Add(Read(reader));

            return rows.ToArray();
        }

        public async Task<CharacterLoadResponse> LoadAsync(long characterId, long accountId,
                                                           CancellationToken ct = default)
        {
            await using SqliteConnection connection = await _database.OpenAsync(ct);
            await using SqliteCommand cmd = connection.CreateCommand();

            // account_id nằm trong WHERE, không phải kiểm sau khi đọc lên.
            // Nhân vật của người khác thì query này trả về 0 dòng — không có đường nào để rò rỉ.
            cmd.CommandText = $"""
                SELECT {SELECT_COLUMNS} FROM character
                WHERE id = $id AND account_id = $accountId AND deleted_at IS NULL;
                """;
            cmd.Parameters.AddWithValue("$id", characterId);
            cmd.Parameters.AddWithValue("$accountId", accountId);

            await using SqliteDataReader reader = await cmd.ExecuteReaderAsync(ct);

            if (!await reader.ReadAsync(ct))
                return new CharacterLoadResponse { Found = false };

            return new CharacterLoadResponse { Found = true, Character = Read(reader) };
        }

        public async Task<CharacterCreateDbResponse> CreateAsync(CharacterCreateDbRequest request,
                                                                 CancellationToken ct = default)
        {
            await using SqliteConnection connection = await _database.OpenAsync(ct);

            // Đếm slot và INSERT phải nằm trong cùng một transaction. Tách ra thì hai request
            // tạo nhân vật gửi cùng lúc đều đếm ra 2/3 và đều tạo được — thành 4 nhân vật.
            // Đây là đúng cái bẫy check-then-act của Phase 4, ở một hình dạng khác.
            await using SqliteTransaction tx = (SqliteTransaction)await connection.BeginTransactionAsync(ct);

            await using (SqliteCommand count = connection.CreateCommand())
            {
                count.Transaction = tx;
                count.CommandText =
                    "SELECT COUNT(*) FROM character WHERE account_id = $accountId AND deleted_at IS NULL;";
                count.Parameters.AddWithValue("$accountId", request.AccountId);

                if (Convert.ToInt32(await count.ExecuteScalarAsync(ct)) >= MAX_SLOTS)
                    return new CharacterCreateDbResponse { Created = false, SlotFull = true };
            }

            await using SqliteCommand insert = connection.CreateCommand();
            insert.Transaction = tx;
            insert.CommandText = """
                INSERT INTO character (account_id, name, name_key, class_id, map_id, pos_x, pos_y, created_at)
                VALUES ($accountId, $name, $nameKey, $classId, $mapId, $x, $y, datetime('now'))
                RETURNING id;
                """;
            insert.Parameters.AddWithValue("$accountId", request.AccountId);
            insert.Parameters.AddWithValue("$name", request.Name);
            insert.Parameters.AddWithValue("$nameKey", request.NameKey);
            insert.Parameters.AddWithValue("$classId", request.ClassId);
            insert.Parameters.AddWithValue("$mapId", request.MapId);
            insert.Parameters.AddWithValue("$x", request.X);
            insert.Parameters.AddWithValue("$y", request.Y);

            try
            {
                long id = Convert.ToInt64(await insert.ExecuteScalarAsync(ct));
                await tx.CommitAsync(ct);

                return new CharacterCreateDbResponse { Created = true, CharacterId = id };
            }
            catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == SQLITE_CONSTRAINT_UNIQUE)
            {
                await tx.RollbackAsync(ct);
                return new CharacterCreateDbResponse { Created = false, SlotFull = false };
            }
        }

        public async Task<bool> SoftDeleteAsync(long characterId, long accountId, CancellationToken ct = default)
        {
            await using SqliteConnection connection = await _database.OpenAsync(ct);
            await using SqliteCommand cmd = connection.CreateCommand();

            cmd.CommandText = """
                UPDATE character SET deleted_at = datetime('now')
                WHERE id = $id AND account_id = $accountId AND deleted_at IS NULL;
                """;
            cmd.Parameters.AddWithValue("$id", characterId);
            cmd.Parameters.AddWithValue("$accountId", accountId);

            // Số dòng bị ảnh hưởng là câu trả lời: 0 nghĩa là không có nhân vật đó,
            // hoặc nó của người khác, hoặc đã xoá rồi. Ba trường hợp, một phản hồi — cố ý.
            return await cmd.ExecuteNonQueryAsync(ct) == 1;
        }

        public async Task SavePositionAsync(CharacterSavePositionRequest request, CancellationToken ct = default)
        {
            await using SqliteConnection connection = await _database.OpenAsync(ct);
            await using SqliteCommand cmd = connection.CreateCommand();

            cmd.CommandText = """
                UPDATE character SET map_id = $mapId, pos_x = $x, pos_y = $y
                WHERE id = $id AND deleted_at IS NULL;
                """;
            cmd.Parameters.AddWithValue("$id", request.CharacterId);
            cmd.Parameters.AddWithValue("$mapId", request.MapId);
            cmd.Parameters.AddWithValue("$x", request.X);
            cmd.Parameters.AddWithValue("$y", request.Y);

            await cmd.ExecuteNonQueryAsync(ct);
        }

        private static CharacterRow Read(SqliteDataReader reader) => new()
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
```

**File mới:** `Server/DBServer/Handlers/CharacterDbHandler.cs`

```csharp
using MMORPG.DBServer.Net;
using MMORPG.DBServer.Repositories;
using MMORPG.Shared.Db;
using MMORPG.Shared.Dto.Db;

namespace MMORPG.DBServer.Handlers
{
    public static class CharacterDbHandler
    {
        public static CharacterRepository Repository { get; set; } = null!;

        [DbHandler(DbCmd.CharacterListByAccount)]
        public static async Task<DbResult> OnList(DbRequest req)
        {
            var request = req.GetData<CharacterListRequest>();

            return DbResult.Ok(new CharacterListResult
            {
                Characters = await Repository.ListByAccountAsync(request.AccountId),
            });
        }

        [DbHandler(DbCmd.CharacterCreate)]
        public static async Task<DbResult> OnCreate(DbRequest req) =>
            DbResult.Ok(await Repository.CreateAsync(req.GetData<CharacterCreateDbRequest>()));

        [DbHandler(DbCmd.CharacterLoad)]
        public static async Task<DbResult> OnLoad(DbRequest req)
        {
            var request = req.GetData<CharacterLoadRequest>();
            return DbResult.Ok(await Repository.LoadAsync(request.CharacterId, request.AccountId));
        }

        [DbHandler(DbCmd.CharacterDelete)]
        public static async Task<DbResult> OnDelete(DbRequest req)
        {
            var request = req.GetData<CharacterDeleteDbRequest>();
            bool deleted = await Repository.SoftDeleteAsync(request.CharacterId, request.AccountId);

            return DbResult.Ok(new DbOkResponse { Success = deleted });
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

Trong `Server/DBServer/Program.cs`:

```csharp
CharacterDbHandler.Repository = new CharacterRepository(database);
```

---

## Bước 3 — GameServer: world tối thiểu

**File mới:** `Server/GameServer/World/PlayerEntity.cs`

```csharp
namespace MMORPG.GameServer.World
{
    /// <summary>
    /// Một nhân vật đang sống trong world. Tồn tại từ EnterWorld tới lúc rời map, không lâu hơn.
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
        public ClientSession? Owner { get; }

        public PlayerEntity(int entityId, Shared.Dto.Db.CharacterRow row, ClientSession owner)
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

**File mới:** `Server/GameServer/World/WorldService.cs`

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
        /// <summary>Vị trí xuất phát của nhân vật mới. Phase 9 sẽ đọc từ bảng config.</summary>
        public const int DEFAULT_MAP_ID = 1;
        public const float SPAWN_X = 0f;
        public const float SPAWN_Y = 0f;

        private readonly ConcurrentDictionary<int, PlayerEntity> _entities = new();
        private readonly ConcurrentDictionary<long, int> _entityIdByCharacter = new();

        private int _nextEntityId;

        public int OnlineCount => _entities.Count;

        public PlayerEntity Spawn(CharacterRow row, ClientSession owner)
        {
            int entityId = Interlocked.Increment(ref _nextEntityId);
            var entity = new PlayerEntity(entityId, row, owner);

            _entities[entityId] = entity;
            _entityIdByCharacter[row.CharacterId] = entityId;

            Log.Info($"Spawn {entity.Name.Cyan()} entity {entityId.ToString().Green()} " +
                     $"tại map {entity.MapId} ({entity.X:0.##}, {entity.Y:0.##}) — " +
                     $"{OnlineCount} người trong world");

            return entity;
        }

        public void Despawn(PlayerEntity entity)
        {
            _entities.TryRemove(entity.EntityId, out _);
            _entityIdByCharacter.TryRemove(entity.CharacterId, out _);

            Log.Info($"Despawn {entity.Name.Cyan()} entity {entity.EntityId} — còn {OnlineCount} người");
        }

        /// <summary>
        /// Nhân vật này đã có ai đang chơi chưa. Cần vì một tài khoản có thể đăng nhập
        /// ở hai chỗ trong khe thời gian trước khi session cũ kịp bị đá.
        /// </summary>
        public bool TryGetByCharacter(long characterId, out PlayerEntity? entity)
        {
            entity = null;

            return _entityIdByCharacter.TryGetValue(characterId, out int entityId) &&
                   _entities.TryGetValue(entityId, out entity);
        }
    }
}
```

**File mới:** `Server/GameServer/World/CharacterNameRules.cs`

```csharp
using System.Globalization;

namespace MMORPG.GameServer.World
{
    /// <summary>
    /// Luật đặt tên nhân vật. Khác luật tài khoản: tên nhân vật được hiện cho người khác thấy
    /// nên phải chặt hơn về ký tự lạ, nhưng lại cho phép chữ có dấu.
    /// </summary>
    public static class CharacterNameRules
    {
        public const int NAME_MIN = 2;
        public const int NAME_MAX = 12;

        /// <summary>Nghề hợp lệ. Phase 9 sẽ đọc từ config thay vì hằng số ở đây.</summary>
        public static readonly int[] VALID_CLASS_IDS = { 1, 2, 3 };

        public static bool IsValidName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            // Đếm theo "text element" chứ không phải theo char: một chữ có dấu tiếng Việt
            // ở dạng tổ hợp chiếm 2 char nhưng người chơi thấy 1 ký tự.
            var elements = new StringInfo(name);
            if (elements.LengthInTextElements < NAME_MIN || elements.LengthInTextElements > NAME_MAX)
                return false;

            foreach (char c in name)
            {
                if (!char.IsLetterOrDigit(c))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Sinh khoá so trùng. Chuẩn hoá Unicode về dạng tổ hợp sẵn TRƯỚC khi hạ chữ thường —
        /// không có bước này thì "Hùng" gõ bằng hai kiểu bàn phím khác nhau ra hai chuỗi byte khác nhau
        /// và cả hai đều tạo được nhân vật.
        /// </summary>
        public static string ToKey(string name) =>
            name.Normalize(NormalizationForm.FormC).Trim().ToLowerInvariant();

        public static bool IsValidClass(int classId) => Array.IndexOf(VALID_CLASS_IDS, classId) >= 0;
    }
}
```

---

## Bước 4 — GameServer: `CharacterService` và `EnterWorld`

**File mới:** `Server/GameServer/World/CharacterService.cs`

```csharp
using MMORPG.GameServer.Db;
using MMORPG.ServerCore;
using MMORPG.Shared.Db;
using MMORPG.Shared.Dto;
using MMORPG.Shared.Dto.Db;
using MMORPG.Shared.Net;

namespace MMORPG.GameServer.World
{
    public sealed class CharacterService
    {
        private readonly DbClient _db;
        private readonly WorldService _world;

        public CharacterService(DbClient db, WorldService world)
        {
            _db = db;
            _world = world;
        }

        public async Task<CharacterListResponse> ListAsync(ClientSession session)
        {
            var result = await _db.CallAsync<CharacterListRequest, CharacterListResult>(
                DbCmd.CharacterListByAccount, new CharacterListRequest { AccountId = session.AccountId });

            return new CharacterListResponse
            {
                MaxSlots = 3,
                Characters = result.Characters.Select(ToSummary).ToArray(),
            };
        }

        public async Task<CharacterCreateResponse> CreateAsync(ClientSession session,
                                                               CharacterCreateRequest request)
        {
            string name = (request.Name ?? string.Empty).Trim();

            if (!CharacterNameRules.IsValidName(name) || !CharacterNameRules.IsValidClass(request.ClassId))
                return new CharacterCreateResponse { Success = false, Error = ErrorCode.InvalidInput };

            var result = await _db.CallAsync<CharacterCreateDbRequest, CharacterCreateDbResponse>(
                DbCmd.CharacterCreate,
                new CharacterCreateDbRequest
                {
                    AccountId = session.AccountId,
                    Name = name,
                    NameKey = CharacterNameRules.ToKey(name),
                    ClassId = request.ClassId,
                    MapId = WorldService.DEFAULT_MAP_ID,
                    X = WorldService.SPAWN_X,
                    Y = WorldService.SPAWN_Y,
                });

            if (!result.Created)
            {
                return new CharacterCreateResponse
                {
                    Success = false,
                    Error = result.SlotFull ? ErrorCode.SlotFull : ErrorCode.NameTaken,
                };
            }

            Log.Info($"{session.Tag} Tạo nhân vật {name.Cyan()} (id {result.CharacterId.ToString().Green()})");

            return new CharacterCreateResponse
            {
                Success = true,
                Character = new CharacterSummary
                {
                    CharacterId = result.CharacterId,
                    Name = name,
                    ClassId = request.ClassId,
                    Level = 1,
                    MapId = WorldService.DEFAULT_MAP_ID,
                },
            };
        }

        public async Task<CharacterDeleteResponse> DeleteAsync(ClientSession session,
                                                               CharacterDeleteRequest request)
        {
            var load = await _db.CallAsync<CharacterLoadRequest, CharacterLoadResponse>(
                DbCmd.CharacterLoad,
                new CharacterLoadRequest { CharacterId = request.CharacterId, AccountId = session.AccountId });

            // Không tìm thấy CÓ THỂ nghĩa là nhân vật của người khác. Trả về cùng một lỗi
            // cho cả hai trường hợp — đừng xác nhận giúp kẻ dò rằng id đó có tồn tại.
            if (!load.Found || load.Character == null)
                return new CharacterDeleteResponse { Success = false, Error = ErrorCode.NotFound };

            if (!string.Equals(load.Character.Name, request.ConfirmName, StringComparison.Ordinal))
                return new CharacterDeleteResponse { Success = false, Error = ErrorCode.InvalidInput };

            if (_world.TryGetByCharacter(request.CharacterId, out _))
                return new CharacterDeleteResponse { Success = false, Error = ErrorCode.CharacterInUse };

            var result = await _db.CallAsync<CharacterDeleteDbRequest, DbOkResponse>(
                DbCmd.CharacterDelete,
                new CharacterDeleteDbRequest { CharacterId = request.CharacterId, AccountId = session.AccountId });

            return new CharacterDeleteResponse
            {
                Success = result.Success,
                Error = result.Success ? ErrorCode.None : ErrorCode.NotFound,
                CharacterId = request.CharacterId,
            };
        }

        public async Task<EnterWorldResponse> EnterWorldAsync(ClientSession session, EnterWorldRequest request)
        {
            // ❗ Dòng quan trọng nhất Phase 5.
            // AccountId lấy từ SESSION — thứ server tự gán ở Phase 4 — chứ không phải từ gói tin.
            // Client gửi lên id nhân vật của người khác thì query trả về 0 dòng.
            var load = await _db.CallAsync<CharacterLoadRequest, CharacterLoadResponse>(
                DbCmd.CharacterLoad,
                new CharacterLoadRequest { CharacterId = request.CharacterId, AccountId = session.AccountId });

            if (!load.Found || load.Character == null)
            {
                Log.Warn($"{session.Tag} (account {session.AccountId}) xin vào nhân vật " +
                         $"{request.CharacterId.ToString().Red()} — không thuộc tài khoản này.");

                return new EnterWorldResponse { Success = false, Error = ErrorCode.NotFound };
            }

            if (_world.TryGetByCharacter(request.CharacterId, out _))
                return new EnterWorldResponse { Success = false, Error = ErrorCode.CharacterInUse };

            PlayerEntity entity = _world.Spawn(load.Character, session);
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
        /// Rời world: lưu vị trí rồi bỏ entity. Gọi cả khi chủ động Leave lẫn khi mất kết nối —
        /// hai đường phải đi qua đúng một hàm, nếu không sớm muộn một đường sẽ quên lưu.
        /// </summary>
        public async Task LeaveWorldAsync(ClientSession session)
        {
            PlayerEntity? entity = session.Entity;
            if (entity == null)
                return;

            session.MarkLeftWorld();
            _world.Despawn(entity);

            try
            {
                await _db.CallAsync<CharacterSavePositionRequest, DbOkResponse>(
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
                Log.Warn($"Không lưu được vị trí của {entity.Name.Cyan()}: {ex.Message.Red()}");
            }
        }

        private static CharacterSummary ToSummary(CharacterRow row) => new()
        {
            CharacterId = row.CharacterId,
            Name = row.Name,
            ClassId = row.ClassId,
            Level = row.Level,
            MapId = row.MapId,
        };
    }
}
```

**Thêm** vào `ErrorCode`:

```csharp
        /// <summary>Không có đối tượng đó — hoặc có nhưng không thuộc về bạn. Cố tình không phân biệt.</summary>
        NotFound = 11,

        /// <summary>Tên nhân vật đã có người dùng.</summary>
        NameTaken = 12,

        /// <summary>Đã đủ số nhân vật tối đa.</summary>
        SlotFull = 13,

        /// <summary>Nhân vật đang được chơi ở nơi khác.</summary>
        CharacterInUse = 14,
```

**Sửa** `ClientSession` — thêm phần world:

```csharp
        /// <summary>Entity đang điều khiển. null khi chưa vào world.</summary>
        public World.PlayerEntity? Entity { get; private set; }

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

và trong khối `finally` của `RunAsync`, **trước** `SessionRegistry.Remove(this)`:

```csharp
                // Mất kết nối đột ngột cũng phải đi qua đúng đường dọn dẹp như Leave chủ động.
                await Handlers.CharacterHandler.Characters.LeaveWorldAsync(this);
```

**File mới:** `Server/GameServer/Handlers/CharacterHandler.cs`

```csharp
using MMORPG.GameServer.Net;
using MMORPG.GameServer.World;
using MMORPG.Shared.Dto;
using MMORPG.Shared.Net;

namespace MMORPG.GameServer.Handlers
{
    public static class CharacterHandler
    {
        public static CharacterService Characters { get; set; } = null!;

        [TcpHandler(NetCmd.CharacterList, MinState = SessionState.Authenticated)]
        public static async Task<NetResult> OnList(NetRequest req) =>
            NetResult.Ok(await Characters.ListAsync(req.Session));

        [TcpHandler(NetCmd.CharacterCreate, MinState = SessionState.Authenticated)]
        public static async Task<NetResult> OnCreate(NetRequest req) =>
            NetResult.Ok(await Characters.CreateAsync(req.Session, req.GetData<CharacterCreateRequest>()));

        [TcpHandler(NetCmd.CharacterDelete, MinState = SessionState.Authenticated)]
        public static async Task<NetResult> OnDelete(NetRequest req) =>
            NetResult.Ok(await Characters.DeleteAsync(req.Session, req.GetData<CharacterDeleteRequest>()));

        [TcpHandler(NetCmd.EnterWorld, MinState = SessionState.Authenticated)]
        public static async Task<NetResult> OnEnterWorld(NetRequest req) =>
            NetResult.Ok(await Characters.EnterWorldAsync(req.Session, req.GetData<EnterWorldRequest>()));

        [TcpHandler(NetCmd.LeaveWorld, MinState = SessionState.InWorld)]
        public static async Task<NetResult> OnLeaveWorld(NetRequest req)
        {
            await Characters.LeaveWorldAsync(req.Session);
            return NetResult.Ok(new LeaveWorldResponse { Success = true });
        }
    }
}
```

> Chú ý `MinState` của từng lệnh. `CharacterList` cần `Authenticated` — chưa đăng nhập thì `session.AccountId`
> bằng 0 và query sẽ trả danh sách rỗng, nhưng dựa vào "may mà nó rỗng" là chờ ngày hỏng. `LeaveWorld` cần `InWorld`
> vì không có entity thì chẳng có gì để rời.

Trong `Program.cs`:

```csharp
var world = new WorldService();
CharacterHandler.Characters = new CharacterService(db, world);
```

Và `SystemHandler.OnServerInfo` giờ báo đúng số người **trong world** thay vì số kết nối:

```csharp
                OnlineCount = world.OnlineCount,
```
(truyền `world` vào `SystemHandler` giống cách truyền `Db`).

### ✅ CHECKPOINT A — kiểm bằng gói tin trước khi làm UI

Trong `NetworkProbe`, thêm tạm 3 nút gửi `CharacterList`, `CharacterCreate`, `EnterWorld`. Server phải log:

```
INFO  [CharacterService] #1 Tạo nhân vật HùngKiếm (id 1)
INFO  [WorldService] Spawn HùngKiếm entity 1 tại map 1 (0, 0) — 1 người trong world
```

**Rồi thử tấn công chính mình.** Tạo tài khoản thứ hai, đăng nhập bằng nó, gửi `EnterWorld` với
`CharacterId = 1` (nhân vật của tài khoản đầu):

```
WARN  [CharacterService] #3 (account 2) xin vào nhân vật 1 — không thuộc tài khoản này.
```
và client nhận `NotFound`. **Nếu vào được thì dừng lại sửa ngay** — đó là lỗ hổng nghiêm trọng nhất
có thể có ở phase này.

---

## Bước 5 — Client: chọn nhân vật

Giờ đã có 2 màn hình (Login, CharacterSelect) nên đây là lúc đưa `com.hungnt.ui.panel` vào —
đúng thời điểm nó bắt đầu giải quyết vấn đề thật. Đọc API của package trước khi viết; phần dưới dùng
`MonoBehaviour` trần cho dễ đối chiếu, chuyển sang `PanelManager` sau khi CHECKPOINT B chạy được.

**File mới:** `Assets/Game/Scripts/Character/CharacterApi.cs`

```csharp
using MMORPG.Client.Network;
using MMORPG.Shared.Dto;
using MMORPG.Shared.Net;

namespace MMORPG.Client.Character
{
    public sealed class CharacterApi
    {
        private readonly NetService _net;

        public CharacterApi(NetService net) => _net = net;

        public void RequestList() => _net.Send(NetCmd.CharacterList, new EmptyRequest());

        public void Create(string name, int classId) =>
            _net.Send(NetCmd.CharacterCreate, new CharacterCreateRequest { Name = name, ClassId = classId });

        public void Delete(long characterId, string confirmName) =>
            _net.Send(NetCmd.CharacterDelete,
                      new CharacterDeleteRequest { CharacterId = characterId, ConfirmName = confirmName });

        public void EnterWorld(long characterId) =>
            _net.Send(NetCmd.EnterWorld, new EnterWorldRequest { CharacterId = characterId });

        public void LeaveWorld() => _net.Send(NetCmd.LeaveWorld, new EmptyRequest());
    }
}
```

**File mới:** `Assets/Game/Scripts/Network/Handlers/CharacterNetHandler.cs`

```csharp
using System;
using MMORPG.Shared.Dto;
using MMORPG.Shared.Net;

namespace MMORPG.Client.Network.Handlers
{
    public sealed class CharacterNetHandler : INetHandlerGroup
    {
        public event Action<CharacterListResponse> OnList;
        public event Action<CharacterCreateResponse> OnCreated;
        public event Action<CharacterDeleteResponse> OnDeleted;
        public event Action<EnterWorldResponse> OnEnteredWorld;
        public event Action<LeaveWorldResponse> OnLeftWorld;

        [NetHandler(NetCmd.CharacterList)]
        private void HandleList(NetPacket p) => OnList?.Invoke(p.GetData<CharacterListResponse>());

        [NetHandler(NetCmd.CharacterCreate)]
        private void HandleCreate(NetPacket p) => OnCreated?.Invoke(p.GetData<CharacterCreateResponse>());

        [NetHandler(NetCmd.CharacterDelete)]
        private void HandleDelete(NetPacket p) => OnDeleted?.Invoke(p.GetData<CharacterDeleteResponse>());

        [NetHandler(NetCmd.EnterWorld)]
        private void HandleEnter(NetPacket p) => OnEnteredWorld?.Invoke(p.GetData<EnterWorldResponse>());

        [NetHandler(NetCmd.LeaveWorld)]
        private void HandleLeave(NetPacket p) => OnLeftWorld?.Invoke(p.GetData<LeaveWorldResponse>());
    }
}
```

**File mới:** `Assets/Game/Scripts/Character/LocalPlayer.cs`

```csharp
using MMORPG.Shared.Dto;

namespace MMORPG.Client.Character
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

        public void Apply(EnterWorldResponse res)
        {
            IsInWorld = true;
            EntityId = res.EntityId;
            CharacterId = res.CharacterId;
            Name = res.Name;
            ClassId = res.ClassId;
            Level = res.Level;
            MapId = res.MapId;
            X = res.X;
            Y = res.Y;
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

**File mới:** `Assets/Game/Scripts/Character/CharacterSelectPresenter.cs`

```csharp
using System.Linq;
using MMORPG.Client.Network.Handlers;
using MMORPG.Shared.Dto;
using MMORPG.Shared.Net;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace MMORPG.Client.Character
{
    public sealed class CharacterSelectPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Transform _slotContainer;
        [SerializeField] private CharacterSlotUi _slotPrefab;
        [SerializeField] private TMP_InputField _nameInput;
        [SerializeField] private Button _createButton;
        [SerializeField] private Button _enterButton;
        [SerializeField] private TextMeshProUGUI _messageText;

        private CharacterApi _characters;
        private CharacterNetHandler _handler;
        private AuthNetHandler _authHandler;
        private LocalPlayer _player;
        private WorldSpawner _spawner;

        private long _selectedId;

        [Inject]
        public void Construct(CharacterApi characters, CharacterNetHandler handler,
                              AuthNetHandler authHandler, LocalPlayer player, WorldSpawner spawner)
        {
            _characters = characters;
            _handler = handler;
            _authHandler = authHandler;
            _player = player;
            _spawner = spawner;
        }

        private void Awake()
        {
            _createButton.onClick.AddListener(() => _characters.Create(_nameInput.text, classId: 1));
            _enterButton.onClick.AddListener(OnEnterClicked);

            _handler.OnList += OnList;
            _handler.OnCreated += OnCreated;
            _handler.OnEnteredWorld += OnEnteredWorld;
            _authHandler.OnLoginResult += OnLoggedIn;

            _root.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_handler == null)
                return;

            _handler.OnList -= OnList;
            _handler.OnCreated -= OnCreated;
            _handler.OnEnteredWorld -= OnEnteredWorld;
            _authHandler.OnLoginResult -= OnLoggedIn;
        }

        private void OnLoggedIn(AuthResponse res)
        {
            if (!res.Success)
                return;

            _root.SetActive(true);
            _characters.RequestList();
        }

        private void OnList(CharacterListResponse res)
        {
            foreach (Transform child in _slotContainer)
                Destroy(child.gameObject);

            foreach (CharacterSummary summary in res.Characters)
            {
                CharacterSlotUi slot = Instantiate(_slotPrefab, _slotContainer);
                slot.Bind(summary, () => Select(summary.CharacterId));
            }

            _createButton.interactable = res.Characters.Length < res.MaxSlots;
            _enterButton.interactable = false;

            _messageText.text = res.Characters.Length == 0
                ? "Chưa có nhân vật nào. Đặt tên rồi bấm Tạo."
                : $"{res.Characters.Length}/{res.MaxSlots} nhân vật.";

            if (res.Characters.Length > 0)
                Select(res.Characters.First().CharacterId);
        }

        private void Select(long characterId)
        {
            _selectedId = characterId;
            _enterButton.interactable = true;
        }

        private void OnCreated(CharacterCreateResponse res)
        {
            if (!res.Success)
            {
                _messageText.text = ErrorText(res.Error);
                return;
            }

            _nameInput.text = string.Empty;
            _characters.RequestList();
        }

        private void OnEnterClicked()
        {
            _enterButton.interactable = false;
            _characters.EnterWorld(_selectedId);
        }

        private void OnEnteredWorld(EnterWorldResponse res)
        {
            if (!res.Success)
            {
                _messageText.text = ErrorText(res.Error);
                _enterButton.interactable = true;
                _characters.RequestList();
                return;
            }

            _player.Apply(res);
            _spawner.SpawnLocalPlayer(res);
            _root.SetActive(false);
        }

        private static string ErrorText(ErrorCode code) => code switch
        {
            ErrorCode.NameTaken => "Tên này đã có người dùng.",
            ErrorCode.SlotFull => "Bạn đã đủ số nhân vật tối đa.",
            ErrorCode.InvalidInput => "Tên nhân vật 2–12 ký tự, chỉ chữ và số.",
            ErrorCode.CharacterInUse => "Nhân vật này đang được chơi ở nơi khác.",
            ErrorCode.NotFound => "Không tìm thấy nhân vật.",
            _ => "Có lỗi xảy ra. Thử lại sau.",
        };
    }
}
```

**File mới:** `Assets/Game/Scripts/Character/CharacterSlotUi.cs`

```csharp
using System;
using MMORPG.Shared.Dto;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MMORPG.Client.Character
{
    public sealed class CharacterSlotUi : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _infoText;
        [SerializeField] private Button _selectButton;

        public void Bind(CharacterSummary summary, Action onSelect)
        {
            _nameText.text = summary.Name;
            _infoText.text = $"Cấp {summary.Level} · Nghề {summary.ClassId}";

            _selectButton.onClick.RemoveAllListeners();
            _selectButton.onClick.AddListener(() => onSelect());
        }
    }
}
```

---

## Bước 6 — Client: nhân vật hiện ra trên map

**File mới:** `Assets/Game/Scripts/World/WorldSpawner.cs`

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
        [SerializeField] private CameraFollow _camera;

        private GameObject _localPlayerObject;

        public void SpawnLocalPlayer(EnterWorldResponse res)
        {
            if (_localPlayerObject != null)
                Destroy(_localPlayerObject);

            _localPlayerObject = Instantiate(
                _playerPrefab, new Vector3(res.X, res.Y, 0f), Quaternion.identity, _entityRoot);
            _localPlayerObject.name = $"Player_{res.EntityId}_{res.Name}";

            _camera.SetTarget(_localPlayerObject.transform);

            this.Log($"Vào map {res.MapId} tại ({res.X:0.##}, {res.Y:0.##}) — entity {res.EntityId}");
        }

        public void DespawnLocalPlayer()
        {
            if (_localPlayerObject == null)
                return;

            Destroy(_localPlayerObject);
            _localPlayerObject = null;
            _camera.SetTarget(null);
        }
    }
}
```

**File mới:** `Assets/Game/Scripts/World/CameraFollow.cs`

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

        public void SetTarget(Transform target) => _target = target;

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

Đăng ký vào `GameLifetimeScope`:

```csharp
            builder.Register<CharacterApi>(Lifetime.Singleton);
            builder.Register<LocalPlayer>(Lifetime.Singleton);

            builder.Register<CharacterNetHandler>(Lifetime.Singleton)
                   .AsSelf()
                   .As<INetHandlerGroup>();

            // WorldSpawner và CameraFollow là MonoBehaviour trong scene — đăng ký instance, không phải type.
            builder.RegisterComponentInHierarchy<WorldSpawner>();
```

Trong scene: một `Sprite` bất kỳ làm prefab nhân vật (một hình vuông màu là đủ), một `Grid + Tilemap` vẽ vài ô sàn
để nhìn ra được là camera đang di chuyển, `CameraFollow` gắn lên Main Camera.

### ✅ CHECKPOINT B — mục tiêu cuối Phase 5

1. Bật DBServer, GameServer, Play Unity.
2. Đăng nhập → màn hình chọn nhân vật hiện ra, ghi `Chưa có nhân vật nào.`
3. Gõ `HùngKiếm`, bấm **Tạo** → slot hiện ra `HùngKiếm · Cấp 1 · Nghề 1`.
4. Bấm **Vào game** → UI chọn nhân vật đóng, ô vuông hiện tại `(0, 0)`, camera bám vào nó.
5. Server log đủ chuỗi:
   ```
   INFO  [AuthService]      #1 hung đăng nhập thành công
   INFO  [CharacterService] #1 Tạo nhân vật HùngKiếm (id 1)
   INFO  [WorldService]     Spawn HùngKiếm entity 1 tại map 1 (0, 0) — 1 người trong world
   ```
6. **Sửa toạ độ trong DB** để chứng minh vị trí thật sự được nạp từ đĩa:
   ```sql
   UPDATE character SET pos_x = 5, pos_y = 3 WHERE id = 1;
   ```
   Thoát Play mode, Play lại, đăng nhập, vào game → nhân vật xuất hiện ở `(5, 3)`.
7. Thoát Play mode giữa lúc đang trong world → server log `INFO  [WorldService] Despawn ... — còn 0 người`.

---

## Bước 7 — Năm thử nghiệm bắt buộc

**1. Vào nhân vật của người khác.** Đã làm ở CHECKPOINT A. Làm lại sau khi có UI, lần này sửa
`CharacterApi.EnterWorld` gửi tạm `characterId + 1`. Phải nhận `NotFound`.

**2. Vào world hai lần cùng một nhân vật.** Chạy bản build song song Editor, đăng nhập **cùng tài khoản**
ở cả hai, cùng chọn một nhân vật.
- Client thứ hai đá client thứ nhất (cơ chế Phase 4) → session cũ đóng → `LeaveWorldAsync` chạy → entity biến mất.
- Nếu bạn thấy `CharacterInUse` thì entity của session cũ chưa được dọn — kiểm tra `LeaveWorldAsync` có được gọi
  trong `finally` của `RunAsync` không.
- Nếu thấy **hai** entity cùng một `CharacterId` trong log thì `TryGetByCharacter` chưa được kiểm trước khi spawn.

**3. Trùng tên nhân vật.** Hai tài khoản khác nhau, cùng đặt tên `HùngKiếm` → tài khoản thứ hai nhận `NameTaken`.
Rồi thử `hùngkiếm` (chữ thường) → **cũng phải** `NameTaken`, nhờ `name_key`.

**4. Đủ slot.** Tạo 3 nhân vật → nút Tạo mờ đi, và nếu gửi thẳng gói `CharacterCreate` thứ 4 (bỏ qua UI)
thì nhận `SlotFull`. Hàng rào ở UI là cho đẹp, hàng rào ở server mới là thật.

**5. Vị trí được lưu khi rớt mạng.** Sửa tạm `WorldSpawner` cho phép kéo ô vuông bằng chuột và ghi ngược
`entity.X/Y` lên server — **chưa cần**, Phase 6 làm việc đó. Cách nhanh hơn để kiểm cùng một điều: đang trong world,
`Ctrl+C` **GameServer**. Bật lại, đăng nhập lại, vào game → vị trí vẫn là vị trí cũ trong DB.
Bây giờ chưa di chuyển được nên giá trị chưa đổi; Phase 6 sẽ biến thử nghiệm này thành có ý nghĩa thật.

---

## Troubleshooting

| Triệu chứng | Nguyên nhân | Xử lý |
|-------------|-------------|-------|
| `CharacterList` trả rỗng dù DB có dòng | `session.AccountId` bằng 0 — chưa đăng nhập, hoặc gửi trên kết nối khác | Kiểm `MinState = Authenticated` có được đặt; một client phải giữ **một** kết nối suốt phiên |
| Tạo nhân vật luôn `NameTaken` | Nhân vật cũ đã xoá vẫn nằm trong index | Kiểm tra index có mệnh đề `WHERE deleted_at IS NULL` |
| `SqliteException: no such column: deleted_at` | Migration 3 chưa chạy vì file DB cũ đã ở version 3 do sửa migration cũ | Đừng sửa migration đã chạy. Lúc dev: xoá `mmorpg.db`, `.db-wal`, `.db-shm` |
| `foreign key constraint failed` khi tạo nhân vật | `account_id` không tồn tại, hoặc `PRAGMA foreign_keys` không bật | Kiểm `Database.InitAsync` chạy trước mọi query |
| Entity treo lại sau khi client thoát | `LeaveWorldAsync` không được gọi trong `finally` | Thêm vào `ClientSession.RunAsync`, trước `SessionRegistry.Remove` |
| `CharacterInUse` mãi không hết | Cùng nguyên nhân trên — entity ma còn trong `WorldService` | Restart GameServer để xác nhận, rồi sửa đường dọn dẹp |
| Nhân vật hiện ở `(0,0)` dù DB ghi khác | Client dựng vị trí từ hằng số thay vì từ `EnterWorldResponse` | `WorldSpawner` phải dùng `res.X`, `res.Y` |
| Camera không bám | `SetTarget` chưa được gọi, hoặc `CameraFollow` chưa kéo vào `WorldSpawner` | Kiểm tham chiếu trong Inspector |
| `VContainerException: WorldSpawner is not registered` | Quên `RegisterComponentInHierarchy` | Thêm vào `GameLifetimeScope`; object phải có sẵn trong scene |
| Tên có dấu bị coi là 2 ký tự | Dùng `name.Length` thay vì `StringInfo` | Xem `CharacterNameRules.IsValidName` |
| Unity không thấy DTO mới | Chưa build `Shared` | `dotnet build Server/Shared` |

---

## Tự kiểm tra hiểu bài

1. Nêu một thứ chỉ có ở `Entity`, một thứ chỉ có ở `Character`, và giải thích vì sao thứ đầu không nên vào DB.
2. Vì sao `entityId` là `int` cấp lúc chạy chứ không dùng thẳng `character.id`? Nêu **ba** lý do khác nhau.
3. Trong `EnterWorldAsync`, `AccountId` lấy từ đâu? Điều gì xảy ra nếu lấy từ `EnterWorldRequest` cho tiện?
4. `LoadAsync` đặt `account_id` trong mệnh đề `WHERE` thay vì đọc lên rồi so sánh trong C#.
   Hai cách cho cùng kết quả — vì sao cách đầu vẫn tốt hơn?
5. `CreateAsync` bọc đếm-slot và INSERT trong một transaction. Viết ra chuỗi sự kiện khiến việc tách chúng ra bị hỏng.
6. Vì sao cần **cả** `name` lẫn `name_key`? Bỏ `name_key` và dùng `COLLATE NOCASE` thì hỏng ở đâu với tiếng Việt?
7. Xoá mềm bắt mọi query phải mang theo `WHERE deleted_at IS NULL`. Đó là chi phí thật —
   nó mua lại được gì mà `DELETE` không cho?
8. `LeaveWorldAsync` nuốt `DbUnavailableException` thay vì để nó ném lên. Vì sao ở **đây** thì nuốt là đúng,
   trong khi `CONVENTIONS.md` §7 cấm nuốt lỗi?
9. `CharacterDelete` trả `NotFound` cho cả "không có id đó" lẫn "id đó của người khác". Điều này chống được gì?
10. `LocalPlayer` chỉ có setter private và một hàm `Apply`. Nếu mở setter công khai cho tiện thì golden rule nào bị phá,
    và triệu chứng đầu tiên sẽ xuất hiện ở phase nào?

---

**Xong Phase 5 → kết thúc Chặng B.** Người chơi đã có danh tính, nhân vật, và một chỗ đứng trong thế giới.
Chặng C bắt đầu bằng [PHASE-6](PHASE-6.md): server chạy tick cố định, client gửi *ý định* di chuyển,
server quyết vị trí — và bạn sẽ hiểu vì sao nhân vật trong game online luôn hơi "trượt" một chút.
(Tài liệu Phase 6 sẽ được viết khi bạn báo đã xong Phase 5.)
