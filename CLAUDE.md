# CLAUDE.md — MMORPG (dự án học fullstack)

Hướng dẫn cho Claude khi làm việc trong repo này. **Đọc file này + `.claude/docs/ROADMAP.md` trước khi quét code.**

> Dự án **học**: tự dựng lại một game MMORPG 2D top-down đơn giản từ số 0 — Unity client + GameServer + DBServer,
> lấy kiến trúc tham chiếu từ `vo-lam-genz` nhưng **viết sạch theo chuẩn com.hungnt**.
> Owner tự code theo tài liệu từng phase. Claude **viết tài liệu và giải thích**, không code hộ trừ khi được yêu cầu rõ.

---

## Golden rules

1. **Owner tự làm.** Vai trò mặc định của Claude ở repo này là **soạn tài liệu phase + trả lời "vì sao"**, không phải viết hộ code.
   Chỉ code khi owner nói rõ ("code giúp phần X", "làm hộ file Y"). Nghi ngờ thì hỏi.
2. **Server là source of truth.** Client gửi *ý định*, không tự sửa state (HP, vị trí, túi đồ). Client chỉ cập nhật sau khi
   server xác nhận. Không có ngoại lệ, kể cả cho tiện lúc prototype.
3. **Client không bao giờ nối DB.** Chuỗi bắt buộc: `Client ──TCP──► GameServer ──TCP nội bộ──► DBServer ──► SQLite/MySQL`.
4. **Contract có đúng 1 nguồn.** `Server/Shared/` là nguồn duy nhất của `NetCmd` + DTO. Client dùng DLL build ra từ đó,
   **không** chép tay enum/DTO sang Unity. Chép tay = sớm muộn lệch số = bug câm.
5. **Mọi callback socket đều KHÔNG ở main thread.** Đụng Unity API phải `await UniTask.SwitchToMainThread()` trước.
6. **Không nhân bản anti-pattern của vo-lam-genz.** Đọc [`VOLAMGENZ-REFERENCE.md`](.claude/docs/VOLAMGENZ-REFERENCE.md) §2
   để biết cái gì đáng bê, cái gì chỉ nên đọc cho hiểu.

---

## Kiến trúc đích

```
Unity Client (Assets/Game)
  UI (com.hungnt.ui.panel)  ─┐
  World / Player            ─┼─► NetService ─► NetDispatcher ─► [NetHandler(cmd)]
  VContainer LifetimeScope  ─┘        │
                                      │ TcpTransport (length-prefix framing)
                                      ▼
                            ┌──────────────────┐
                            │   GameServer     │  tick loop, logic, AOI, entity
                            │  [TcpHandler]    │
                            └────────┬─────────┘
                                     │ TCP nội bộ (DbCmd)
                                     ▼
                            ┌──────────────────┐
                            │    DBServer      │  DAL, SQLite (→ MySQL sau)
                            └──────────────────┘

Shared (MMORPG.Shared.dll) ── NetCmd enum + DTO MemoryPack ── dùng chung cả 3 bên
ServerCore                 ── Log + màu console ─────────── GameServer & DBServer
```

### Cấu trúc thư mục

| Đường dẫn | Nội dung |
|-----------|----------|
| `Assets/Game/` | Toàn bộ code + asset game client |
| `Packages/com.hungnt.*` | Submodule package riêng của owner (core, eventbus, objectpool, assetload, dataconfig, datasave, ui, ui.panel, ui.tween) |
| `Assets/Plugins/Shared/` | `MMORPG.Shared.dll` build ra từ `Server/Shared` — **không sửa tay** |
| `Server/Shared/` | Contract dùng chung: `NetCmd`, DTO, codec — **cũng build ra DLL cho Unity** nên chỉ chứa thứ client cần |
| `Server/ServerCore/` | Hạ tầng chỉ server dùng (`Log`, `AnsiExtensions`). Đối ứng của `com.hungnt.core` bên client |
| `Server/GameServer/` | Game server .NET 8 |
| `Server/DBServer/` | DB server .NET 8 |
| `.claude/docs/` | Tài liệu kiến trúc + roadmap |
| `.claude/docs/guides/PHASE-*.md` | Tài liệu từng bước owner làm theo |

**Code client không dùng `.asmdef`** — nằm hết trong `Assembly-CSharp`. Assembly do asmdef định nghĩa không
tham chiếu được `Assembly-CSharp-firstpass`, mà DOTween Pro nằm ở đó dưới dạng `.cs` trần. Đừng đề xuất tạo
asmdef cho `Assets/Game/` trừ khi owner nêu vấn đề thời gian compile. `Packages/com.hungnt.*` thì vẫn có
asmdef riêng — đó là package độc lập, chuyện khác.

---

## Khung gói tin (wire format) — thuộc lòng

```
Frame trên TCP:
┌─────────────┬─────────────┬───────────────────────┐
│ int32 len   │ int32 cmd   │ payload (len - 4 byte)│
└─────────────┴─────────────┴───────────────────────┘
  len = 4 + payload.Length ; little-endian

payload:
┌──────────┬───────────────────────┬──────────────────┐
│ flag 1B  │ rawLen 4B (nếu flag=1)│ MemoryPack bytes │
└──────────┴───────────────────────┴──────────────────┘
  flag 0x00 = không nén · 0x01 = LZ4 (chỉ nén khi > 4KB)
```

Cùng scheme với vo-lam-genz. Chi tiết + code: [`guides/PHASE-1.md`](.claude/docs/guides/PHASE-1.md), [`guides/PHASE-2.md`](.claude/docs/guides/PHASE-2.md).

---

## Thêm một lệnh mạng mới — checklist

1. Thêm giá trị vào `NetCmd` (trong `Server/Shared/`) — chọn số **trong dải của feature**, xem bảng dải ở `ROADMAP.md`.
2. Thêm DTO request/response vào `Server/Shared/Dto/<Feature>/` — `[MemoryPackable] public partial class`.
3. Build `Server/Shared` → DLL tự copy sang `Assets/Plugins/Shared/` (post-build target).
4. **Server**: `[TcpHandler(NetCmd.X)] public static NetResult Handle(NetRequest req)` trong `Handlers/<Feature>Handler.cs`.
5. **Client**: `[NetHandler(NetCmd.X)] private void OnX(NetPacket p)` trong `Network/Handlers/<Feature>NetHandler.cs`
   → bắn event → Presenter → UI. Handler đã ở main thread sẵn.
6. **Client — bắt buộc, dễ quên nhất:** nếu đó là **nhóm handler mới**, đăng ký vào `GameLifetimeScope`:
   ```csharp
   builder.Register<XNetHandler>(Lifetime.Singleton).AsSelf().As<INetHandlerGroup>();
   ```
   Server quét cả assembly nên handler mới tự chạy; **client thì không** — handler client là method instance,
   phải có container tạo ra thì mới tồn tại. Quên dòng này thì lệnh rơi vào hư không mà **không có lỗi biên dịch**.
7. UI chỉ đọc state **sau khi** server confirm.

Không đụng vào switch/if-else nào cả — dispatch table tự tìm handler qua attribute.

### DI phía client — quy tắc sống còn

`GameLifetimeScope.Configure` là chỗ **duy nhất** biết client có những gì. Mỗi khi thêm một class nhận
inject qua constructor, phải đăng ký nó ở đây, kể cả khi nó chỉ là dependency của một service khác.

Thiếu một dòng đăng ký thì lỗi **không** chỉ vào chỗ thiếu, mà đổ dây chuyền:

```
VContainerException: Failed to resolve NetworkProbe
  : Failed to resolve NetService
  : No such registration of type: NetDispatcher   ← thủ phạm nằm ở DÒNG CUỐI
NullReferenceException at NetworkProbe.Awake()    ← chỉ là hệ quả, đừng đi sửa chỗ này
```

Đọc lỗi VContainer thì đọc **dòng cuối cùng** của chuỗi `Failed to resolve`. `NullReferenceException`
ngay sau đó là do container chết nên field inject còn null — sửa dòng cuối là hết cả hai.

Muốn VContainer inject vào MonoBehaviour có sẵn trong scene thì phải
`builder.RegisterComponentInHierarchy<T>()`; không có dòng đó, `[Inject]` không bao giờ chạy và
field vẫn null mà chẳng có exception nào cả.

---

## Conventions

Chi tiết đầy đủ: [`.claude/docs/CONVENTIONS.md`](.claude/docs/CONVENTIONS.md). Tóm tắt:

| Thứ | Quy ước | Ví dụ |
|-----|---------|-------|
| Namespace | `MMORPG.Client.*`, `MMORPG.GameServer.*`, `MMORPG.DBServer.*`, `MMORPG.ServerCore.*`, `MMORPG.Shared.*` | |
| Class / Method / Property | PascalCase | `NetService`, `SendAsync` |
| Interface | `I` + PascalCase | `ITransport`, `INetService` |
| Private field | `_camelCase` | `_transport`, `_eventBus` |
| Field nhận inject | tên type đầy đủ dạng camelCase, **không cắt cụt** | `_netService` chứ không `_net`, `_dbClient` chứ không `_db` |
| Const | UPPER_SNAKE_CASE | `HEADER_SIZE` |
| Brace | Allman (mở ngoặc xuống dòng) | |
| Thân method / constructor | **luôn `{ }` đầy đủ, không expression-bodied** — kể cả thân 1 dòng; chỉ property getter thuần được dùng `=>` | `void Send() { _net.Send(); }` chứ không `void Send() => _net.Send();` |
| Indent | 4 space | |

**Mọi định danh trong code là tiếng Anh.** Tiếng Việt chỉ dùng cho: comment, XML doc, và chuỗi hiển thị cho người chơi.

### Comment
Comment khi logic **không tự giải thích** (vì sao chọn cấu trúc này, race condition, edge case, workaround).
Giải thích **tại sao**, không mô tả lại từng dòng. Không comment kiểu "trước đây… giờ là…" — code phải đọc như
thể luôn được viết như vậy.

### Log — không dùng `Debug.Log` / `Console.WriteLine` trần

| Bên | Dùng | Ví dụ |
|-----|------|-------|
| Client | `DebugEx` của `com.hungnt.core` (`using HungNT;`) | `this.Log(...)` · `this.LogWarning(...)` · `this.LogError(...)` |
| Server | `Log` của `MMORPG.ServerCore` | `Log.Debug/Info/Warn/Error(...)` · `Log.Error(ex, "...")` |

**Không lặp lại tên class trong nội dung log.** Cả hai bên đều tự chèn `[TênClass]` — client lấy từ
`GetType().Name`, server lấy từ `[CallerFilePath]` lúc biên dịch.

```csharp
this.LogWarning($"Không có handler cho {cmd}");   // ✅  → [NetService] Không có handler cho Ping
this.LogWarning($"[NetService] Không có...");     // ❌  → [NetService] [NetService] Không có...
```

`this.Log(...)` chạy được cả trong class thường lẫn MonoBehaviour (extension trên `object`).
Server handler đều là class **static** nên không dùng được kiểu extension — vì vậy server có API static `Log.Info(...)`.

**Tô màu** để thông tin quan trọng nổi trên console:

```csharp
// Server — ANSI, qua MMORPG.ServerCore.AnsiExtensions
Log.Info($"{session.Tag} Kết nối từ {endPoint.Green()}");
Log.Warn($"Lỗi {cmd}: {code.ToString().Red()}");

// Client — rich text của Unity, qua HungNT.StringExtensions
this.Log($"Nhận {count.ToString().Bold()} gói");
```

`Log` chỉ tô màu phần `LEVEL` và `[Tag]`, cố tình chừa phần nội dung ra để màu bạn đặt bên trong
không bị mã reset của tầng ngoài ăn mất. Màu tự tắt khi output bị đẩy ra file hoặc có biến `NO_COLOR`.

### Commit (chỉ commit khi owner yêu cầu)
`type(scope): mô tả ngắn` — `feat` / `fix` / `refactor` / `docs` / `chore`.
Scope: `net`, `server`, `db`, `ui`, `world`, `shared`, `docs`.
VD: `feat(net): thêm framing length-prefix cho TcpTransport`

---

## Package com.hungnt — dùng gì ở đâu

| Package | Dùng cho |
|---------|----------|
| `com.hungnt.core` | `DebugEx` logging, `MonoSingleton`, `IAppLifecycle`, `CoreInstaller` (VContainer) |
| `com.hungnt.eventbus` | Bắn event từ NetHandler → Presenter/UI, tránh coupling |
| `com.hungnt.objectpool` | Pool GameObject nhân vật / quái / hiệu ứng khi AOI spawn liên tục |
| `com.hungnt.assetload` | Load asset qua Addressables (map, prefab nhân vật) |
| `com.hungnt.dataconfig` | Config tĩnh phía client (ScriptableObject + import GSheet) |
| `com.hungnt.datasave` | Setting local của client (âm lượng, phím tắt) — **không** dùng cho state game |
| `com.hungnt.ui` / `.panel` / `.tween` | Base UI, PanelManager theo layer, tween show/hide |

**DI = VContainer** (`jp.hadashikick.vcontainer`) — bắt buộc, mọi package `com.hungnt.*` phụ thuộc nó.
Sửa code trong `Packages/com.hungnt.*` là sửa **submodule** → `git status` ở repo gốc chỉ hiện 1 dòng ` m Packages/...`.
Muốn xem chi tiết: `git submodule foreach --quiet 'echo "== $name"; git status --short'`.

---

## Tài liệu

| File | Nội dung |
|------|----------|
| [`.claude/docs/ROADMAP.md`](.claude/docs/ROADMAP.md) | **Bản đồ toàn dự án** — 17 phase, mục tiêu & thứ tự |
| [`.claude/docs/VOLAMGENZ-REFERENCE.md`](.claude/docs/VOLAMGENZ-REFERENCE.md) | Chắt lọc từ vo-lam-genz: bê gì, tránh gì, file nào đọc để hiểu |
| [`.claude/docs/CONVENTIONS.md`](.claude/docs/CONVENTIONS.md) | Naming, style, quy ước đặt số CMD, layout thư mục |
| [`.claude/docs/guides/PHASE-N.md`](.claude/docs/guides/) | Hướng dẫn từng bước, có code đầy đủ + CHECKPOINT |

Khi owner hỏi về một hệ thống đã có doc → **đọc doc trước**, đừng quét lại codebase.

---

## Repo tham chiếu (chỉ đọc, không sửa)

- `../vo-lam-genz` — Unity client MMORPG thật đang chạy. Có `.claude/docs/` rất chi tiết.
- `../vo-lam-genz-server` — GameServer + GameDBServer C#. Nguồn tham chiếu cho kiến trúc server.
- `../BaseCode_Test` — sandbox package `com.hungnt.*` của owner. Nguồn tham chiếu cho style code + VContainer.

⚠️ vo-lam-genz là codebase **kế thừa, nhiều đời dev**: phần mới sạch, phần cũ bẩn.
Luôn đối chiếu `VOLAMGENZ-REFERENCE.md` trước khi bắt chước bất cứ thứ gì trong đó.

---

## Anti-patterns (tuyệt đối tránh trong repo này)

- Chép tay `NetCmd` / DTO sang Unity thay vì dùng DLL từ `Server/Shared`.
- `switch (cmd)` khổng lồ thay cho dispatch table.
- `Debug.Log` / `Console.WriteLine` trần thay cho `DebugEx` / `MMORPG.ServerCore.Log`.
- Thêm nhóm handler client mà quên đăng ký vào `GameLifetimeScope` (không có lỗi biên dịch, lệnh im lặng rơi mất).
- God class: một file > ~400 dòng là tín hiệu phải tách.
- Sửa state game ở client trước khi server xác nhận.
- `catch (Exception) { }` nuốt lỗi.
- Hard-code IP / port / khóa mã hóa trong source (dùng file config, không commit file có secret).
- Đụng Unity API từ socket thread.
