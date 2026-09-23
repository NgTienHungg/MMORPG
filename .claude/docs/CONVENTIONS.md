# CONVENTIONS — Quy ước code dự án MMORPG

> Kế thừa từ `BaseCode_Test/CLAUDE.md` (style com.hungnt) + `vo-lam-genz/.claude/docs/bagua-guides/NAMING-EN.md`.
> Chốt một lần, áp dụng cho cả client, server và shared.

---

## 1. Ngôn ngữ

1. **Mọi định danh trong code là tiếng Anh.** Không `viTri`, không `isDangDanh`, không `TuiDoData`.
2. Tiếng Việt chỉ được phép ở **3 chỗ**:
   - Comment và XML doc.
   - Chuỗi hiển thị cho người chơi (`"Sai tài khoản hoặc mật khẩu."`).
   - Tài liệu `.md`.
3. Tên bảng / cột DB cũng là tiếng Anh: `account`, `character`, `inventory_item`.

## 2. Naming

| Thứ | Quy ước | Ví dụ |
|-----|---------|-------|
| Namespace | `MMORPG.<Tier>.<Module>` | `MMORPG.Client.Network`, `MMORPG.GameServer.World`, `MMORPG.ServerCore`, `MMORPG.Shared.Dto` |

| Class / Struct | PascalCase | `NetService`, `PlayerEntity` |
| Interface | `I` + PascalCase | `ITransport`, `IPacketCodec` |
| Method / Property | PascalCase | `SendAsync`, `IsConnected` |
| Private field | `_camelCase` | `_transport`, `_receiveBuffer` |
| Local / parameter | camelCase | `payload`, `cmdId` |
| `[SerializeField]` | `_camelCase` | `[SerializeField] private Button _loginButton;` |
| Const / `static readonly` bất biến | UPPER_SNAKE_CASE | `HEADER_SIZE`, `MAX_PACKET_SIZE` |
| Event | `On` + PascalCase | `OnConnected`, `OnPlayerMoved` |
| Enum value | PascalCase | `NetCmd.LoginRequest` |

**Namespace KHÔNG chứa tên thư mục kỹ thuật.** `Assets`, `Game`, `Scripts` là chỗ chứa file, không phải
tầng kiến trúc — không được xuất hiện trong namespace. File ở `Assets/Game/Scripts/Network/` là
`MMORPG.Client.Network`, **không** phải `Game.Scripts.Network`.

#### Cấu hình Rider để nó tự sinh đúng
Rider dựng namespace = `RootNamespace` của csproj + đường dẫn thư mục. Cần 2 việc, làm một lần:

1. **Unity** → `Edit → Project Settings → Editor → Root Namespace` = `MMORPG.Client`.
   Ghi vào `ProjectSettings/EditorSettings.asset` (đã commit), Unity nhét vào mọi csproj nó sinh ra.
2. **Rider** → trong cửa sổ Solution, chuột phải thư mục `Game` → `Properties` → **Namespace provider = False**.
   Làm y hệt cho thư mục `Scripts`. Rider ghi vào `MMORPG.sln.DotSettings` — file này commit được nên chỉ phải làm một lần.

Sau đó file mới tạo trong `Assets/Game/Scripts/Network/` sẽ tự có `namespace MMORPG.Client.Network`.
Nếu Rider vẫn đề xuất sai, `Alt+Enter` trên tên namespace → *Adjust namespaces* để sửa nhanh.

### Field nhận inject (DI)
Field/parameter trỏ tới một dependency đặt theo **tên type đầy đủ** dạng camelCase (bỏ tiền tố `I`):

```csharp
private readonly NetService _netService;   // ✅  đọc _netService.Send() là biết ngay đang nhờ service nào
private readonly DbClient   _dbClient;     // ✅  không phải _db
```

Lý do: tên cụt lúc khai báo thì vẫn hiểu, nhưng đến dòng thứ 80 chỉ còn thấy `_net.Send(...)` —
người đọc phải nhảy ngược lên đầu file mới biết `_net` là cái gì. Tên theo type thì từng dòng
tự đứng được một mình.

Được rút gọn **chỉ khi** phần giữ lại vẫn là khái niệm trọn vẹn, tự giải thích trong ngữ cảnh
(`LoginRateLimiter` → `_rateLimiter`); không bao giờ rút tới mẩu cụt (`_net`, `_db`, `_auth`, `_ui`, `res`).
Biến local cũng theo tinh thần đó — và tên phải nói đúng **nội dung** nó chứa: `var db = ...` cho một biến
đang giữ *response* của ServerMetaGet là sai hai lần (vừa cụt vừa lạc nghĩa) — đặt `serverMeta`.

### Hậu tố theo vai trò

| Hậu tố | Vai trò | Ví dụ |
|--------|---------|-------|
| `*Service` | Hạ tầng đăng ký vào container | `NetService`, `WorldService` |
| `*Api` | Gom các lệnh *gửi đi* của 1 feature | `AuthApi`, `InventoryApi` |
| `*NetHandler` | Nơi *nhận* packet phía client | `AuthNetHandler` |
| `*Handler` | Nơi *nhận* packet phía server | `AuthHandler` |
| `*Presenter` | Điều phối giữa data và UI | `LoginPresenter` |
| `*Ui` | View, chỉ vẽ | `LoginUi` |
| `*Repository` | Truy cập DB | `AccountRepository` |
| `*Request` / `*Response` | DTO đi qua mạng | `LoginRequest`, `LoginResponse` |
| `*Entity` | Object sống trong world server | `PlayerEntity`, `MonsterEntity` |
| `*Parser` | Đọc/ghi một định dạng file, không giữ state | `MapGridParser` |
| `*Config` | **Một đơn vị** dữ liệu tĩnh — một dòng của bảng, hoặc cả một map | `CharacterConfig`, `ItemConfig`, `MapConfig` |
| `*TableData` | **Cả file** bảng dữ liệu: `Version` + mảng `*Config` + `RowCount` | `CharacterTableData`, `ItemTableData` |
| `*ConfigContainer` | Bảng tra lúc chạy cho một loại `*Config` | `CharacterConfigContainer`, `ItemConfigContainer` |
| `*Registry` | Sổ tra các object **có hành vi**, không phải dữ liệu tĩnh | `MapRegistry`, `SessionRegistry` |

**Hình dạng chỗ dữ liệu NẰM không bao giờ là hình dạng thứ CHẠY** (chốt ở Phase 10). Bản đối chiếu
1-1 với file thì property có setter, cho phép null, và chỉ hàm parse được đụng vào; thứ chạy trong
game thì bất biến và mang tên theo vai của nó. `MapConfig` (file) ≠ `MapGrid` (chạy) — cùng mẫu với
`CharacterRow` (hàng DB) ≠ `PlayerEntity` (world).

Không đặt `*Definition` (dễ hiểu nhầm là nơi khai báo hằng), và không đặt `*Data` trần — hậu tố
`*Data` chỉ đi kèm `*Table`.

> **`MapConfig` KHÔNG phải ngoại lệ** (soát lại 2026-09-22 — bản trước của file này gọi nó là ngoại
> lệ và đề nghị đổi thành `MapFileData`; sai). Nó là dữ liệu tĩnh của MỘT map, đúng vai `*Config`
> như `CharacterConfig` là dữ liệu tĩnh của MỘT lớp nhân vật. File do tool export sinh ra hay do
> người gõ tay không đổi VAI của kiểu — nó chỉ đổi độ chặt lúc parse (map bỏ qua trường lạ vì tool
> còn ghi thêm; bảng người gõ thì trường lạ là lỗi chính tả). Ba vai của map:
> `MapConfig` → `MapRegistry` (server) / `MapService` (client) → `MapGrid`. Và như mọi config khác,
> map **không** có hàm băm riêng.

### Bộ ba `*Config` / `*TableData` / `*ConfigContainer`

Chốt 2026-09-22 khi làm bảng item. Mọi bảng dữ liệu game design — nhân vật, item, quái, skill, drop —
đi theo **đúng ba kiểu này, cùng tên gốc**, đặt ở `Server/Shared/World/<Feature>/`:

```
Server/Shared/World/Item/
├── ItemConfig.cs             một DÒNG: [MemoryPackable], property có setter
├── ItemTableData.cs          cả FILE: Version + ItemConfig[] + RowCount, : IConfigFile
└── ItemConfigContainer.cs    bảng TRA lúc chạy: static, Load(ItemTableData) + Find(id)
```

Vì sao đúng ba kiểu chứ không hai (gộp table vào container) hay bốn (thêm kiểu "file" riêng):

| Kiểu | Tồn tại vì | Nếu bỏ đi |
|---|---|---|
| `*Config` | là thứ code gọi cầm trên tay (`config.MaxStack`) | phải truyền cả bảng + id đi khắp nơi |
| `*TableData` | là hình dạng của FILE, và là thứ `ConfigFingerprint` băm | không có gì để parse JSON vào, và không có gì để băm |
| `*ConfigContainer` | là chỗ tra `id → config` mà **không cần inject** | mọi hàm dùng item phải nhận thêm một tham số bảng |

**Cả hai bên đều nạp từ file của mình** (chốt 2026-09-22, sau khi soát lại cách MMO thật làm):
client đọc `Assets/Game/Resources/Config/`, server đọc bản copy trong `Data/Config/` do csproj chép
sang. Bảng KHÔNG đi trên dây, và `EnterWorldResponse` KHÔNG mang danh sách vân tay để so: mỗi bên
tự quyết định nạp bảng nào.

> Vì sao không gửi cả bảng cho gọn: gói login phình theo số bảng (Phase 15 đã là năm bảng); client
> không hiển thị được tên/icon item **trước khi vào world**; và dữ liệu tĩnh đáng đi đường CDN
> (Phase 18) chứ không đi qua socket game server. Đó cũng là cách WoW, Lineage và mọi MMO mobile
> làm: client ship bản của nó, server ship bản của nó, phiên bản kiểm ở trình patch.
>
> Vì sao cũng **không** gửi danh sách vân tay để so (bản trước của file này bảo có — sai): server
> nạp những bảng client không bao giờ đọc tới (tỉ lệ rơi đồ, AI quái), client nạp những bảng server
> không cần (thoại, mô tả kỹ năng). Bắt hai danh sách khớp nhau là ép client ship đúng tập bảng của
> server, và chặn luôn đường tải dần từ CDN. Vân tay vẫn tính — nhưng nó **in ra log ở cả hai bên**
> cho người đối chiếu, chứ không đi trên dây.
>
> Bệnh của vo-lam-genz là hai bản mà không ai kiểm. Thứ kiểm được nó là **một số phiên bản cho cả
> gói dữ liệu**, kiểm một lần lúc đăng nhập — bài của Phase 18, cùng lúc với trình patch.

**Không viết `Checksum()` trong `*TableData`.** Dùng `ConfigFingerprint.Of(table)` — một hàm dùng
chung, băm byte đã tuần tự hoá. Băm tay từng trường là hai chục dòng mỗi bảng mới **và** một chế độ
hỏng câm: thêm trường mà quên thêm vào hàm băm thì vân tay không còn phát hiện được thay đổi ở
trường đó.

Bốn luật đi kèm, vi phạm cái nào cũng thành bug câm:

1. **`Load` là cửa duy nhất ghi vào bảng.** Mỗi bên gọi sau khi đọc file của mình. Một bảng, hai
   đường vào, một hàm dựng.
2. **`Load` dựng nguyên bảng mới rồi mới gán reference**, không sửa tại chỗ — luồng khác hoặc thấy
   trọn bảng cũ, hoặc trọn bảng mới.
3. **Phép kiểm miền giá trị chỉ chạy ở server** (`ConfigService.Validate*`). Nếu client cũng kẹp thì
   một file có `MoveSpeed = 999` cho ra hai bên cùng chạy 5 — và cái file hỏng ấy không bao giờ bị
   phát hiện. Server kẹp, client không, hai dòng log vân tay lệch nhau, người ta đi sửa file.
4. **Hai bên băm ở CÙNG một thời điểm trong quy trình: sau `load()`.** Đây là ràng buộc thật, không
   phải sở thích — `ActionData` là struct toàn kiểu unmanaged nên MemoryPack chép nguyên khối, kể cả
   những trường `Prepare()` vừa điền. Băm ở hai thời điểm khác nhau là làm chính phép phát hiện lệch
   nói dối. Có bài test ghim việc này: `ConfigFingerprintTests`.

## 3. File & thư mục

- **1 class 1 file**, tên file trùng tên class.
- File > **~400 dòng** là tín hiệu phải tách. Không có ngoại lệ "file này đặc biệt".
- Extension / partial để riêng: `NetService.Send.cs`, `PlayerEntity.Combat.cs`.
- Thư mục chia **theo feature**, không theo loại: `Auth/` chứa cả DTO, handler, presenter, UI của auth.
  (Ngoại lệ: `Shared/Dto/` chia theo feature, `Network/` là hạ tầng nên chia theo lớp.)

## 4. Format

- Indent **4 space**, không tab.
- Brace **Allman** — mở ngoặc xuống dòng riêng (khác default của C#).
- **Method / constructor luôn có thân `{ }` đầy đủ, không expression-bodied** (`void Ten() => ...;`),
  kể cả khi thân chỉ một dòng. Lý do: thân một dòng hôm nay thành năm dòng ngày mai — có sẵn `{ }`
  thì thêm log / guard / breakpoint không phải đổi hình dạng hàm, diff cũng gọn.
  Expression-bodied chỉ chấp nhận cho **property getter thuần** (`public int Count => _list.Count;`).
  Switch expression và lambda không liên quan quy tắc này — chúng nằm *trong* thân `{ }`.
- Dòng ~100–120 ký tự.
- `using` gom nhóm: `System` → `UnityEngine` → thư viện ngoài → namespace dự án.
- Thứ tự trong class: field → property → constructor → public method → protected → private.
- `#region` chỉ dùng khi nhóm ≥ 3 method cùng chủ đề.

## 5. Comment

- XML doc `///` cho **mọi public member** của hạ tầng (network, service, DTO dùng chung).
- Comment `//` khi logic **không tự giải thích**: vì sao chọn cấu trúc này, race condition, edge case, workaround.
- **Giải thích *tại sao*, không mô tả lại code.** `// tăng i lên 1` là rác.
- **Cấm nhắc lịch sử migration** — không "trước đây…", "giờ là…", "thay cho…". Code phải đọc như thể luôn viết vậy.
- Không dán code ví dụ vào XML summary.

## 6. Quy ước riêng cho network

1. `NetCmd` mới → thêm vào **cuối dải của feature** (bảng dải ở `ROADMAP.md` §2). Không chèn giữa, không tái dùng số đã xoá.
2. Mỗi cmd có XML doc ghi rõ: **request là gì, response là gì, ai chủ động gửi**.
   ```csharp
   /// <summary>
   /// Client xin toàn bộ túi đồ khi mở UI.
   /// Request: rỗng · Response: <see cref="InventorySnapshot"/>
   /// </summary>
   InventoryGetAll = 400,
   ```
3. DTO đi qua mạng: `[MemoryPackable] public partial class`, **chỉ property auto**, không logic.
4. Request/Response đi theo cặp, đặt cạnh nhau trong cùng file.
5. Handler **không** chứa business logic — chỉ giải mã, gọi service, đóng gói kết quả.

## 7. Log

Cấm `Debug.Log` / `Console.WriteLine` trần. Mỗi bên có đúng một cổng log:

| Bên | API | Nằm ở |
|-----|-----|-------|
| Client | `this.Log()` · `this.LogWarning()` · `this.LogError()` | `HungNT.DebugEx` (`com.hungnt.core`) |
| Server | `Log.Debug()` · `Log.Info()` · `Log.Warn()` · `Log.Error()` | `MMORPG.ServerCore.Log` |

**Không viết tên class vào nội dung log** — cả hai bên đều tự chèn `[TênClass]`:
client lấy runtime từ `GetType().Name`, server lấy lúc biên dịch từ `[CallerFilePath]`.

```csharp
this.LogWarning($"Không có handler cho {cmd}");    // ✅
this.LogWarning($"[NetService] Không có handler"); // ❌ in ra 2 lần tên class
```

Vì sao hai API khác nhau: client có `this` để bám vào nên dùng extension; server handler đều là
class **static**, không có `this`, nên phải là API static.

### Phủ log cho luồng nghiệp vụ

Log ở **điểm xử lý logic then chốt**, không log theo gói tin: client log lệnh nghiệp vụ **gửi gì đi**
(`AuthApi.Login` in username/password), service log **quyết định gì và vì sao** — kể cả lý do từ chối thật,
thứ mà response về client phải giấu — DB handler log **đọc/ghi gì**. Test một tính năng mà console
không kể lại được chuyện vừa xảy ra nghĩa là thiếu log.

**KHÔNG log trong hot path** (dispatcher, transport): từ Phase 6 gói di chuyển/AOI chạy liên tục,
mỗi gói một dòng log là console thành thác nước và tốn CPU vô ích. Cần soi wire thì thêm tạm rồi gỡ.

Đang giai đoạn dev, in thẳng mật khẩu tài khoản test ra console là chấp nhận được (chốt 2026-08-14) —
tiện đối chiếu nhập-gì-gửi-nấy. Token phiên vẫn chỉ nên in độ dài. Tới phase hardening (TLS)
phải rà lại toàn bộ log secret trước khi có người chơi thật.

### Mức log

| Mức | Dùng cho | Ví dụ |
|-----|----------|-------|
| `Debug` | Chi tiết theo từng gói / từng entity, tắt khi chạy thật | `Echo -> SystemHandler.OnEcho` |
| `Info` | Mốc vòng đời | server lên, client kết nối / rớt |
| `Warn` | Sai nhưng chạy tiếp được | handler trùng cmd, payload hỏng của một client |
| `Error` | Hỏng thật, cần người xem | handler ném exception |

`Log.Error(ex, "...")` in nguyên stack trace. **Đừng chỉ log `ex.Message`** — mất chỗ ném là mất tất.
Server đặt `Log.MinLevel = LogLevel.Info` khi chạy thật để bớt nhiễu.

`DebugEx` bên client gắn `[Conditional("DEBUG")]` nên **cả lời gọi lẫn tham số đều bị xoá** trong
build release. Được cái không tốn CPU, nhưng đừng nhét việc có tác dụng phụ vào trong đối số:
`this.Log(Consume())` sẽ làm `Consume()` biến mất khi build thật.

### Màu

Tô màu **mẩu chữ quan trọng** bên trong câu, không tô cả câu:

```csharp
Log.Info($"{session.Tag} Kết nối từ {endPoint.Green()}");  // server: ANSI
this.Log($"Nhận {count.ToString().Bold()} gói");           // client: rich text Unity
```

Bên server dùng **lối tắt theo tên màu** (`.Green()`, `.Red()`, `.Cyan()`…). Dạng đầy đủ
`.Color(Color.Green)` vẫn còn, nhưng chỉ cần đến khi màu là **biến** — ví dụ chọn màu theo mức máu.

Các hàm này nhận `string`, nên số phải `.ToString()` trước. Cố tình không có overload cho `int`:
một cách viết thì grep ra hết được, hai cách thì lần nào cũng phải nhớ đang dùng cái nào.

**Dùng đúng màu cho đúng loại thông tin** — mục đích là đọc console theo phản xạ, không phải cho vui mắt:

| Màu | Dùng cho | Ví dụ |
|-----|----------|-------|
| `Green` | Địa chỉ, đường dẫn, số đếm thành công | `0.0.0.0:7778`, đường dẫn file DB, `Đăng ký 3 handler` |
| `Magenta` | Định danh: id phiên (qua `Tag`), request id | `#7`, `reqId 42` |
| `Cyan` | Tên lệnh: `NetCmd` / `DbCmd` | `ServerMetaGet` |
| `Yellow` | Thứ bị bỏ qua lúc khởi động | handler sai chữ ký, `DbCmd` trùng |
| `Red` | Mã lỗi, tên exception | `ServiceUnavailable`, `SocketException` |

Mỗi phiên phải có một `Tag` tính sẵn trong constructor (`ClientSession`, `DbSession`) rồi chèn đầu mọi dòng
log của phiên đó. Đừng nội suy `#{Id}` rải rác — sẽ có chỗ quên tô màu và mắt không bám được một phiên nữa.

> **Không tô nội dung exception.** Nơi bắt exception thường bọc cả `ex.Message` trong một màu; nếu bên trong
> `Message` đã có sẵn một mẩu màu thì mã reset của nó **cắt ngang** màu bên ngoài và phần đuôi câu mất màu:
> ```
> \e[91mChưa nối được DBServer khi gọi \e[96mServerMetaGet\e[0m.\e[0m
>                                                          ↑ từ đây hết đỏ
> ```
> Quy tắc: **tô ở chỗ log, không ở chỗ throw.**

`Log` cố tình chỉ tô `LEVEL` và `[Tag]`, chừa phần nội dung — nếu tô cả câu thì mã reset của
mẩu bên trong sẽ cắt màu của phần còn lại. Màu tự tắt khi output bị đẩy ra file hoặc có biến `NO_COLOR`.

## 8. Error handling

- **Không bao giờ** `catch (Exception) { }`. Tối thiểu phải log.
- Lỗi nghiệp vụ (sai mật khẩu, không đủ tiền) → **không** ném exception, trả `ErrorCode` trong response.
- Lỗi hệ thống (mất kết nối DB, packet hỏng) → log ở mức Error + ngắt kết nối nếu cần.
- 1 enum `ErrorCode` duy nhất trong `Shared`, dùng chung 2 bên.
- **Nullable reference types TẮT ở GameServer + DBServer** (`<Nullable>disable</Nullable>` trong csproj,
  đồng bộ với Unity client vốn tắt mặc định) — vì vậy trong 2 project đó **không viết** `string?`,
  `= null!`, `[NotNullWhen]`; property gán-một-lần từ `Program.cs` chỉ cần `{ get; set; }` trần.
  Quên gán → `NullReferenceException` lúc chạy trỏ đúng chỗ, chấp nhận được. Nghi ngờ null thì
  kiểm bằng `if (x == null)` thường. Riêng `Shared`/`ServerCore` (thư viện) giữ nullable bật.

## 9. Git

- Commit format: `type(scope): mô tả ngắn`
  `feat` / `fix` / `refactor` / `docs` / `chore` / `test`
  scope: `net` · `server` · `db` · `shared` · `ui` · `world` · `docs`
- **Chỉ commit khi owner yêu cầu rõ.**
- Sửa trong `Packages/com.hungnt.*` là sửa **submodule** — commit trong submodule trước, rồi mới commit con trỏ ở repo gốc.
  Xem trạng thái mọi submodule:
  ```bash
  git submodule foreach --quiet 'echo "== $name"; git status --short'
  ```

## 10. Unity UI event

- `AddListener` phải có `RemoveListener` **đối xứng** trong `OnDestroy` — nghĩa là listener phải là
  **method có tên**, không phải lambda: lambda mỗi lần viết là một delegate mới, `RemoveListener`
  không bao giờ khớp, listener cũ bám mãi vào Button nếu Button sống lâu hơn component.
- Ngoại lệ duy nhất: widget dạng item được `Bind` lại nhiều lần (slot danh sách) — dùng
  `RemoveAllListeners()` ngay trước `AddListener`, kèm chú thích tại chỗ.
- Gỡ listener UI đặt **trước** guard null của dependency inject trong `OnDestroy` — UI là serialized
  field nên luôn tồn tại, kể cả khi container build lỗi và các field inject còn null.
- Mọi request khoá UI chờ response phải có **timeout** mở khoá lại (xem `LoginPresenter.ArmResponseTimeout`,
  Phase 4) — một response không tới không được phép treo UI vĩnh viễn.

## 11. Test

- Logic thuần (codec, damage formula, validate) → unit test được thì phải có test.
- Không cố unit-test MonoBehaviour hoặc socket thật — test qua interface (`ITransport` mock).
- Server: `dotnet test`. Client: Unity Test Framework (EditMode cho logic thuần).
