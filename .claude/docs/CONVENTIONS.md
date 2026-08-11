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
Type có vai trò service → hậu tố `Service`. Field/parameter nhận inject → **bỏ hậu tố `Service`, không viết tắt**:

```csharp
private readonly INetService     _net;        // ✅  không phải _netService, không phải _n
private readonly IEventBusService _eventBus;  // ✅  không phải _bus
```

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

## 3. File & thư mục

- **1 class 1 file**, tên file trùng tên class.
- File > **~400 dòng** là tín hiệu phải tách. Không có ngoại lệ "file này đặc biệt".
- Extension / partial để riêng: `NetService.Send.cs`, `PlayerEntity.Combat.cs`.
- Thư mục chia **theo feature**, không theo loại: `Auth/` chứa cả DTO, handler, presenter, UI của auth.
  (Ngoại lệ: `Shared/Dto/` chia theo feature, `Network/` là hạ tầng nên chia theo lớp.)

## 4. Format

- Indent **4 space**, không tab.
- Brace **Allman** — mở ngoặc xuống dòng riêng (khác default của C#).
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
Log.Info($"{session.Tag} Kết nối từ {endPoint.Green()}");   // server: ANSI
this.Log($"Nhận {count.ToString().Bold()} gói");            // client: rich text Unity
```

`Log` cố tình chỉ tô `LEVEL` và `[Tag]`, chừa phần nội dung — nếu tô cả câu thì mã reset của
mẩu bên trong sẽ cắt màu của phần còn lại. Màu tự tắt khi output bị đẩy ra file hoặc có biến `NO_COLOR`.

## 8. Error handling

- **Không bao giờ** `catch (Exception) { }`. Tối thiểu phải log.
- Lỗi nghiệp vụ (sai mật khẩu, không đủ tiền) → **không** ném exception, trả `ErrorCode` trong response.
- Lỗi hệ thống (mất kết nối DB, packet hỏng) → log ở mức Error + ngắt kết nối nếu cần.
- 1 enum `ErrorCode` duy nhất trong `Shared`, dùng chung 2 bên.

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

## 10. Test

- Logic thuần (codec, damage formula, validate) → unit test được thì phải có test.
- Không cố unit-test MonoBehaviour hoặc socket thật — test qua interface (`ITransport` mock).
- Server: `dotnet test`. Client: Unity Test Framework (EditMode cho logic thuần).
