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
```

### Cấu trúc thư mục

| Đường dẫn | Nội dung |
|-----------|----------|
| `Assets/Game/` | Toàn bộ code + asset game client |
| `Packages/com.hungnt.*` | Submodule package riêng của owner (core, eventbus, objectpool, assetload, dataconfig, datasave, ui, ui.panel, ui.tween) |
| `Assets/Plugins/Shared/` | `MMORPG.Shared.dll` build ra từ `Server/Shared` — **không sửa tay** |
| `Server/Shared/` | Contract dùng chung: `NetCmd`, DTO, codec |
| `Server/GameServer/` | Game server .NET 8 |
| `Server/DBServer/` | DB server .NET 8 |
| `.claude/docs/` | Tài liệu kiến trúc + roadmap |
| `.claude/docs/guides/PHASE-*.md` | Tài liệu từng bước owner làm theo |

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
5. **Client**: `[NetHandler(NetCmd.X)] public static void OnX(NetPacket p)` trong `Network/Handlers/<Feature>NetHandler.cs`
   → bắn event → Presenter → UI. Handler đã ở main thread sẵn.
6. UI chỉ đọc state **sau khi** server confirm.

Không đụng vào switch/if-else nào cả — dispatch table tự tìm handler qua attribute.

---

## Conventions

Chi tiết đầy đủ: [`.claude/docs/CONVENTIONS.md`](.claude/docs/CONVENTIONS.md). Tóm tắt:

| Thứ | Quy ước | Ví dụ |
|-----|---------|-------|
| Namespace | `MMORPG.Client.*`, `MMORPG.GameServer.*`, `MMORPG.DBServer.*`, `MMORPG.Shared.*` | |
| Class / Method / Property | PascalCase | `NetService`, `SendAsync` |
| Interface | `I` + PascalCase | `ITransport`, `INetService` |
| Private field | `_camelCase` | `_transport`, `_eventBus` |
| Field nhận inject | bỏ hậu tố `Service`, **không viết tắt** | `_eventBus` chứ không `_bus` |
| Const | UPPER_SNAKE_CASE | `HEADER_SIZE` |
| Brace | Allman (mở ngoặc xuống dòng) | |
| Indent | 4 space | |

**Mọi định danh trong code là tiếng Anh.** Tiếng Việt chỉ dùng cho: comment, XML doc, và chuỗi hiển thị cho người chơi.

### Comment
Comment khi logic **không tự giải thích** (vì sao chọn cấu trúc này, race condition, edge case, workaround).
Giải thích **tại sao**, không mô tả lại từng dòng. Không comment kiểu "trước đây… giờ là…" — code phải đọc như
thể luôn được viết như vậy.

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
- God class: một file > ~400 dòng là tín hiệu phải tách.
- Sửa state game ở client trước khi server xác nhận.
- `catch (Exception) { }` nuốt lỗi.
- Hard-code IP / port / khóa mã hóa trong source (dùng file config, không commit file có secret).
- Đụng Unity API từ socket thread.
