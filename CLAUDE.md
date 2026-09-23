# CLAUDE.md — MMORPG (dự án học fullstack)

Hướng dẫn cho Claude khi làm việc trong repo này. **Đọc file này + `.claude/docs/ROADMAP.md` trước khi quét code.**

> Dự án **học**: tự dựng lại một game MMORPG 2D top-down đơn giản từ số 0 — Unity client + GameServer + DBServer,
> lấy kiến trúc tham chiếu từ `vo-lam-genz` nhưng **viết sạch theo chuẩn com.hungnt**.
> Owner tự code theo tài liệu từng phase. Claude **viết tài liệu và giải thích**, không code hộ trừ khi được yêu cầu rõ.

---

## Golden rules

1. **Owner tự làm.** Vai trò mặc định của Claude ở repo này là **soạn tài liệu phase + trả lời "vì sao"**, không phải viết hộ code.
   Chỉ code khi owner nói rõ ("code giúp phần X", "làm hộ file Y"). Nghi ngờ thì hỏi.
   Vì owner gõ theo doc, **doc thiếu code = doc không dùng được**. Luật viết doc ở §[Viết tài liệu phase](#viết-tài-liệu-phase)
   là bắt buộc, không phải gợi ý.
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
                            │  [TcpHandler]    │  ServerBootstrap → ServerServices
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
| `Server/GameServer/Boot/` | `ServerServices` (sổ tra service) + `ServerBootstrap` (composition root) |
| `Server/GameServer/Config/` | `ConfigService` (một hàm nạp cho mọi file), `ConfigLimits`, `GameConfigData` |
| `Server/DBServer/` | DB server .NET 8 |
| `Config/` | Config **loại A** ở gốc repo — `game.json`, chỉ server đọc. csproj copy sang `Data/Config/` lúc build |
| `Assets/Game/Resources/Config/` | Bảng **loại B** — `characters.json`, `items.json`… **Cả hai bên đọc**: client thẳng từ Resources, server từ bản csproj copy sang `Data/Config/` |
| `.claude/docs/` | Tài liệu kiến trúc + roadmap |
| `.claude/docs/guides/PHASE-*.md` | Tài liệu từng bước owner làm theo |
| `.claude/skills/phase-doc/` | Skill bắt buộc khi soạn/sửa tài liệu phase |

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
7. **Server — dễ quên thứ nhì:** nếu lệnh cần một service mới, đăng ký nó vào `ServerBootstrap.Build()`.
   Quên dòng này cũng **không có lỗi biên dịch** — chỉ có `InvalidOperationException` ở gói tin đầu
   tiên chạm tới nó, tức là sau khi server đã boot sạch.
8. UI chỉ đọc state **sau khi** server confirm.

Không đụng vào switch/if-else nào cả — dispatch table tự tìm handler qua attribute.
Khuôn đầy đủ cho một feature dọc (DB → DAL → service → packet → UI): [`FEATURE-TEMPLATE.md`](.claude/docs/FEATURE-TEMPLATE.md).

### DI hai bên — quy tắc sống còn

Mỗi bên có **đúng một** composition root, và mỗi service mới tốn một dòng ở đó:

| Bên | Composition root | Sổ tra | Handler lấy service kiểu gì |
|-----|------------------|--------|------------------------------|
| Client | `GameLifetimeScope.Configure` | VContainer | `[Inject]` vào constructor / method |
| Server | `ServerBootstrap.Build` | `ServerServices` (tự viết) | `private static X X => ServerServices.Get<X>();` |

**Server:** handler là hàm `static` nên không nhét constructor injection vào được — đó là lý do duy
nhất `ServerServices` tồn tại, và nó **chỉ** được dùng ở biên giới ấy. Mọi service khác nhận phụ
thuộc qua **constructor** và không gọi `Get<T>()` trong thân hàm. Trong handler thì dùng **property
`=>`** chứ không field: field static khởi tạo trước `ServerBootstrap.Build()` và sẽ giữ null vĩnh viễn.

`ServerServices.Seal()` chạy sau khi đăng ký hết — từ đó sổ chỉ còn đọc, nên nhiều luồng đọc song song
không cần khoá gì. Đăng ký sau `Seal()` là ném ngay.

**Client:** `GameLifetimeScope.Configure` là chỗ **duy nhất** biết client có những gì. Mỗi khi thêm một
class nhận inject qua constructor, phải đăng ký nó ở đây, kể cả khi nó chỉ là dependency của một
service khác.

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

### Hậu tố theo vai trò — bộ ba của dữ liệu tĩnh

Chốt 2026-09-22. Mọi bảng dữ liệu game design (nhân vật, item, quái, skill…) đi theo **đúng ba kiểu
này**, cùng tên gốc, không sáng tạo thêm:

| Hậu tố | Vai trò | Ví dụ |
|--------|---------|-------|
| `*Config` | **Một dòng** dữ liệu tĩnh — định nghĩa một loại/một lớp. Bất biến trong suốt phiên chạy | `CharacterConfig`, `ItemConfig`, `WorldConfig` |
| `*TableData` | **Cả file**: `Version` + mảng `*Config` + `RowCount`, implements `IConfigFile`. Bản đối chiếu 1-1 với JSON | `CharacterTableData`, `ItemTableData` |
| `*ConfigContainer` | Bảng tra lúc chạy: `static` giữ `Dictionary<id, *Config>`, có `Load(*TableData)` + `Get(id)`/`Find(id)` | `CharacterConfigContainer`, `ItemConfigContainer` |

Cả ba nằm ở `Server/Shared/World/<Feature>/` — client cần đọc bảng nên nó thuộc contract.
`*ConfigContainer.Load` là **cửa duy nhất** ghi vào bảng: mỗi bên gọi sau khi đọc file của mình.
Một bảng, hai đường vào, một hàm dựng.

**Map là cùng bộ ba, chỉ khác tên vì khác hình dạng file** (một file một map, không phải một bảng
nhiều dòng): `MapConfig` (dữ liệu một map) → `MapRegistry` / `MapService` (sổ tra lúc chạy) →
`MapGrid` (dạng để mô phỏng dùng). Nó **không** phải ngoại lệ, và **không** có hàm băm riêng.

**Không viết `Checksum()` trong `*TableData`.** Dấu vân tay tính bằng `ConfigFingerprint.Of(table)` —
một hàm dùng chung, băm byte đã tuần tự hoá. Băm tay từng trường là hai chục dòng mỗi bảng mới **và**
một chế độ hỏng câm: thêm trường mà quên thêm vào hàm băm thì vân tay không còn phát hiện được thay
đổi ở trường đó. Đối chiếu với `Version`: trường đó nằm **trong file** nên thêm sau là một cuộc di cư,
còn vân tay thì **tính ra từ file** nên thêm lúc nào cũng được.

**Vân tay là một dòng log, không phải phép kiểm trên dây.** Mỗi bên tự quyết định nạp bảng nào:
server có bảng client không bao giờ đọc (tỉ lệ rơi đồ, AI quái), client sẽ có bảng thuần hiển thị mà
server không cần, và Phase 18 cho client tải dần từ CDN. Gửi mảng vân tay rồi bắt hai bên khớp là ép
client ship đúng những bảng server nạp. Thứ chặn được "client chạy dữ liệu cũ" là số phiên bản của
**cả gói** dữ liệu, kiểm một lần lúc đăng nhập — việc của trình patch, không phải của `EnterWorldResponse`.

Đừng đặt `*Profile`, `*Definition`, `*Data` trần, `*Profiles` (số nhiều) cho ba vai này nữa —
tên cũ còn sót trong doc phase là lỗi, báo lại.

### Comment

Chốt 2026-09-23 sau khi soát lại code Phase 12. Bốn luật, và luật thứ ba với thứ tư là hai lỗi hay gặp nhất:

1. **Mô tả đúng hiện tại.** Summary (`///`) nói class/hàm làm gì tại thời điểm viết, không hơn.
   **Cấm nhắc phase trong code** (kiểu "Phase 7 sẽ dùng", "bài của Phase 4"): người đọc code không có
   bối cảnh roadmap. Kế hoạch tương lai thuộc về tài liệu phase.
2. **Giải thích idiom không hiển nhiên ngay tại chỗ** — semaphore `Wait`/`Release`, `Interlocked`,
   exception filter `when`, vòng drain queue, `ref` param, struct bị copy trong `foreach`. Nói rõ
   **nó làm gì và vì sao cần ở đây**. Dòng tự hiển nhiên (gán thuần, `i++`) thì không comment.
3. **Cấm kể lịch sử.** Không "trước đây…", không "bản cũ…", không "cách cũ có hai cái giá…", không
   "đừng làm như X nữa", không so với phương án đã bị loại. Code phải đọc như thể **lần đầu đã viết
   đúng như vậy**. Lý do một phương án bị bác bỏ thuộc về tài liệu phase và commit message, không
   thuộc về file `.cs`.
4. **Ngắn và đều tay.**
   - **Ngắn:** summary một tới ba câu; quá ba câu là dấu hiệu nó đang gánh việc của doc. Comment
     trong thân hàm một tới hai dòng.
   - **Đều:** trong một class, các thành viên cùng loại thì cùng mức comment — hoặc mọi field có một
     dòng, hoặc không field nào có. Một class tám field mà chỉ hai field giữa có `///` trông như bị
     bỏ dở. Ngoại lệ hợp lệ: một thành viên có cái bẫy thật mà các thành viên kia không có; lúc đó
     comment **cái bẫy**, đừng mô tả lại tên field.

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
| [`.claude/docs/ROADMAP.md`](.claude/docs/ROADMAP.md) | **Bản đồ toàn dự án** — 20 phase, mục tiêu & thứ tự |
| [`.claude/docs/VOLAMGENZ-REFERENCE.md`](.claude/docs/VOLAMGENZ-REFERENCE.md) | Chắt lọc từ vo-lam-genz: bê gì, tránh gì, file nào đọc để hiểu |
| [`.claude/docs/CONVENTIONS.md`](.claude/docs/CONVENTIONS.md) | Naming, style, quy ước đặt số CMD, layout thư mục |
| [`.claude/docs/FEATURE-TEMPLATE.md`](.claude/docs/FEATURE-TEMPLATE.md) | **Khuôn chuẩn thêm một feature dọc** — thứ tự 8 tầng, chỗ đăng ký ở cả hai bên |
| [`.claude/docs/guides/PHASE-N.md`](.claude/docs/guides/) | Hướng dẫn từng bước, có code đầy đủ + CHECKPOINT |

Khi owner hỏi về một hệ thống đã có doc → **đọc doc trước**, đừng quét lại codebase.
Khi owner hỏi "thêm feature X thì làm gì" → đọc `FEATURE-TEMPLATE.md`.

---

## Viết tài liệu phase

Kích hoạt skill [`phase-doc`](.claude/skills/phase-doc/SKILL.md) mỗi khi soạn hoặc sửa
`.claude/docs/guides/PHASE-N.md`. Năm luật rút ra sau khi Phase 12–14 viết hỏng (2026-09-22):

1. **Mọi file trong bảng "Danh sách file" phải có code trong doc.** 🆕 = nguyên văn cả file; ✏️ = đủ
   phần sửa để dán vào được. Soát cuối bằng cách **đếm**: số dòng bảng file = số khối code. Cấm
   "làm tương tự", cấm "// phần còn lại giữ nguyên", cấm nhắc tên một hàm ở Troubleshooting mà cả
   doc không có code của nó.
2. **Tên phải lấy từ code thật bằng `grep`, không từ trí nhớ và không từ doc cũ.** Owner refactor
   liên tục; doc không tự đổi theo. Trong một doc, một thứ chỉ có một tên — phần "hướng làm" ở trên
   và "lời giải" ở dưới phải trùng khít class, chữ ký hàm, namespace.
3. **Code trong doc phải biên dịch được.** Đủ `using`, đủ `namespace`, đúng `CONVENTIONS.md`. Khi
   cùng lần đó có viết code thật thì **code trước → build pass → chép vào doc**, không viết doc rồi
   hy vọng code khớp.
4. **Phần client không bao giờ được thiếu.** Lỗi lặp lại nhiều nhất: tả server rất kỹ rồi dừng. Mỗi
   phase động tới mạng phải có đủ *bằng code*: DTO, chỗ server gắn dữ liệu, client nhận ở handler
   nào, đưa đi đâu, và **dòng `builder.Register<...>` trong `GameLifetimeScope`**.
5. **Thiết kế trước, hướng dẫn sau.** Luôn tự hỏi "thêm cái thứ hai cùng loại tốn bao nhiêu dòng?".
   Nếu đáp án là "chép lại cả hàm rồi sửa tên" thì sửa thiết kế, đừng hướng dẫn owner chép.

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
