# ROADMAP — Dựng lại một MMORPG từ số 0

> **Mục tiêu dự án:** tự tay dựng lại một game MMORPG 2D top-down đơn giản (Unity client + GameServer + DBServer),
> lấy kiến trúc tham chiếu sát nhất từ `vo-lam-genz` nhưng viết sạch theo chuẩn `com.hungnt`.
> Học bằng cách **tự làm và tự gỡ lỗi**, không phải đọc code có sẵn.
>
> **Cách dùng file này:** đây là bản đồ. Mỗi phase có file chi tiết riêng ở `guides/PHASE-N.md` — được viết dần
> khi bạn tiến tới gần phase đó. Làm xong 1 phase thì tự đánh dấu `[x]` ở bảng trạng thái cuối file.

---

## 0. Quyết định nền đã chốt

| Hạng mục | Chốt | Lý do |
|----------|------|-------|
| Kiến trúc tầng | **3-tier ngay từ đầu**: Client ↔ GameServer ↔ DBServer | Giống vo-lam-genz. Hiểu ngay từ sớm vì sao DB phải là process riêng |
| Database | **SQLite trước → MySQL ở Phase 15** | SQLite = 0 setup, chạy được ngay. Đổi sang MySQL sau chính là bài test xem tầng DAL có trừu tượng đủ tốt không |
| Serialize | **MemoryPack + project `Shared` dùng chung** | Đúng phần sạch nhất của vo-lam-genz. Contract 1 nguồn → không bao giờ lệch |
| Nén | LZ4, chỉ khi payload > 4KB | Copy nguyên tắc của vo-lam-genz (`MemoryPackUtility`) |
| Thể loại | 2D top-down, map tilemap, di chuyển tự do | Sát vo-lam-genz nhất → phần map/AOI/sorting đối chiếu được |
| Unity | 6000.2.9f1, URP 2D, DI = VContainer | Theo `BaseCode_Test` |
| Repo | **1 repo duy nhất** chứa cả client + server + shared, tới hết Phase 12 | Đổi contract là sửa cả 2 bên — cùng 1 repo thì gói gọn trong 1 commit, `git checkout` commit cũ luôn cho ra cặp client/server khớp nhau. Tách repo (hoặc submodule) buộc phải commit 2 lần mỗi lần đổi contract; quên bước 2 thì 2 bên lệch mà git không báo gì |
| Tách repo server | **Phase 16**, khi deploy thật | Lúc đó mới có lý do thật: đẩy server lên VPS không cần kéo theo vài GB asset Unity. Tách trước thời điểm đó là chịu chi phí mà chưa nhận được lợi ích |

---

## 1. Bản đồ 17 phase

Nhóm thành 5 chặng. **Không nhảy cóc** — mỗi phase dựa trên phase trước.

### Chặng A — Đường ống mạng (Phase 0–2)
> Kết thúc chặng: client và server nói chuyện được với nhau bằng gói tin có kiểu, không dùng if/else.

| # | Phase | Kết quả cụ thể | Học được gì |
|---|-------|----------------|-------------|
| **0** | **Nền móng dự án** | Unity mở compile sạch, `dotnet build` chạy, cấu trúc thư mục + submodule đủ | Layout repo mono, submodule UPM, VContainer/UniTask/MemoryPack vào Unity thế nào |
| **1** | **Transport: byte đi được 2 chiều** | Bấm nút trong Unity → server log nhận được → trả về → UI hiện RTT | TCP là *stream* không có ranh giới gói · length-prefix framing · buffer ghép gói dở · callback socket ≠ main thread |
| **2** | **Contract & Dispatch** | `NetCmd` + DTO trong `Shared`, build ra DLL cho cả 2 bên; gửi/nhận bằng attribute `[TcpHandler]` / `[NetHandler]` | Dispatch table thay switch · auto-register bằng reflection · MemoryPack + nén LZ4 · vì sao contract phải 1 nguồn |

### Chặng B — Người chơi có danh tính (Phase 3–5)
> Kết thúc chặng: đăng ký → đăng nhập → chọn nhân vật → nhân vật hiện ra trong map.

| # | Phase | Kết quả cụ thể | Học được gì |
|---|-------|----------------|-------------|
| **3** | **DBServer & tầng DAL** | Process `DBServer` riêng, SQLite, GameServer hỏi DB qua TCP nội bộ | Vì sao tách DB server · request/response async không block game loop · repository pattern |
| **4** | **Đăng ký / Đăng nhập** | UI login trong Unity, tài khoản lưu SQLite, sai mật khẩu báo đúng lỗi | PBKDF2 hash · token session · không bao giờ tin client · chống login trùng |
| **5** | **Nhân vật & vào thế giới** | Tạo nhân vật → chọn → `EnterWorld` → nhân vật xuất hiện, camera bám | Account ≠ Character ≠ Entity · state machine kết nối · snapshot khởi tạo |

### Chặng C — Thế giới sống (Phase 6–9)
> Kết thúc chặng: 2 client chạy song song, thấy nhau di chuyển mượt trên map, server kiểm soát mọi thứ.

| # | Phase | Kết quả cụ thể | Học được gì |
|---|-------|----------------|-------------|
| **6** | **Game loop server & di chuyển authoritative** | Server chạy tick cố định, client gửi ý định move, server quyết vị trí | Fixed tick · client prediction + reconciliation · chống speed hack |
| **7** | **Đồng bộ nhiều người chơi** | Mở 2 client (ParrelSync), thấy nhau chạy mượt, không giật | Snapshot theo tick · interpolation buffer · vì sao không gửi mỗi frame |
| **8** | **Map & AOI** | Map tilemap có va chạm; chỉ nhận gói của người chơi trong tầm nhìn | Spatial grid · interest management · vì sao MMO không broadcast toàn map |
| **9** | **Data & Config** | Bảng config (tốc độ, map, spawn) load được cả 2 bên, sửa không cần build lại | Data-driven · 1 nguồn config · hot reload |

### Chặng D — Nội dung game (Phase 10–12)
> Kết thúc chặng: có một vòng gameplay đủ nhỏ nhưng đầy đủ: đánh quái → rơi đồ → vào túi → lưu DB.

| # | Phase | Kết quả cụ thể | Học được gì |
|---|-------|----------------|-------------|
| **10** | **Feature dọc đầu tiên: Túi đồ** | Nhặt/dùng/vứt item, thoát game vào lại vẫn còn | Quy trình chuẩn thêm feature MMO (DB → DAL → logic → packet → UI) · cache RAM + dirty flag |
| **11** | **Monster, chiến đấu & drop** | Quái spawn, đánh nhau, chết, rơi đồ | Entity ngoài player · AI tick · damage formula authoritative |
| **12** | **Chat** | Chat kênh thế giới / bản đồ / riêng, có chống spam | Broadcast có filter · rate limit · vì sao chat cũng phải đi qua server |

### Chặng E — Hạ tầng & vận hành (Phase 13–16)
> Kết thúc chặng: build được ra bản chạy thật, đổi máy chủ chỉ bằng sửa config.

| # | Phase | Kết quả cụ thể | Học được gì |
|---|-------|----------------|-------------|
| **13** | **Tách package `com.hungnt.network`** | Phần network client thành package riêng, publish được | Thiết kế API tái dùng · tách phần game-specific khỏi infra · versioning |
| **14** | **AssetBundle / Addressables + CDN** | Sửa asset → build bundle → client tự tải bản mới, không build lại app | Hot update pipeline · manifest + MD5 per-file · host CDN |
| **15** | **SQLite → MySQL** | Đổi provider, dữ liệu cũ migrate được | DAL trừu tượng đúng chưa · migration · connection pool |
| **16** | **Vận hành: build, log, deploy** | Client build ra chạy trên máy khác, trỏ IP máy bạn; server có log + đo tick | Config ngoài code · structured logging · graceful shutdown · deploy VPS |

---

## 2. Quy hoạch dải `NetCmd`

Chốt ngay từ đầu để không phải dời số sau (bài học từ vo-lam-genz: dải Bát Quái phải dời vì đụng feature khác).

| Dải | Nhóm | Phase |
|-----|------|-------|
| `0` | `None` — giá trị vô hiệu, không dùng | — |
| `1–99` | **Hệ thống**: ping, handshake, disconnect, error | 1–2 |
| `100–199` | **Auth**: register, login, logout, token | 4 |
| `200–299` | **Character**: list, create, delete, enter world | 5 |
| `300–399` | **World / Movement**: move, snapshot, spawn, despawn | 6–8 |
| `400–499` | **Inventory / Item** | 10 |
| `500–599` | **Combat / Monster** | 11 |
| `600–699` | **Chat** | 12 |
| `700–999` | *(trống — feature sau)* | |
| `1000+` | **DbCmd** — protocol nội bộ GameServer ↔ DBServer, **client không bao giờ thấy** | 3 |

**Quy tắc:** thêm lệnh mới → luôn thêm vào **cuối dải của feature**, không chèn giữa. Không tái sử dụng số đã xoá.

---

## 3. Cách làm việc giữa bạn và Claude

1. Claude viết chi tiết `guides/PHASE-N.md` **trước** khi bạn tới phase đó.
2. Bạn tự code theo doc. Gặp `✅ CHECKPOINT` thì phải chạy được mới đi tiếp.
3. Kẹt ở đâu → hỏi Claude bằng **triệu chứng cụ thể** ("gửi được nhưng server không nhận", kèm log),
   đừng hỏi "sửa hộ".
4. Làm xong phase → báo Claude để: (a) review chỗ lệch so với doc, (b) viết chi tiết phase kế tiếp.
5. Khi phát hiện code có tính hạ tầng, tái dùng cao → ghi vào `CANDIDATE-PACKAGES.md` để cân nhắc tách package.

**Nguyên tắc:** doc mô tả *cái gì* và *vì sao*, kèm code đủ để chép khi bí. Nhưng gõ lại tay vẫn học được nhiều hơn chép.

---

## 4. Trạng thái

| Phase | Trạng thái | Doc chi tiết |
|-------|-----------|--------------|
| 0 — Nền móng | ⏳ đang làm | [`guides/PHASE-0.md`](guides/PHASE-0.md) ✅ |
| 1 — Transport | ⬜ chưa | [`guides/PHASE-1.md`](guides/PHASE-1.md) ✅ |
| 2 — Contract & Dispatch | ⬜ chưa | [`guides/PHASE-2.md`](guides/PHASE-2.md) ✅ |
| 3 — DBServer & DAL | ⬜ chưa | ⬜ chưa viết |
| 4 — Auth | ⬜ chưa | ⬜ chưa viết |
| 5 — Character & EnterWorld | ⬜ chưa | ⬜ chưa viết |
| 6 — Game loop & movement | ⬜ chưa | ⬜ chưa viết |
| 7 — Multi-player sync | ⬜ chưa | ⬜ chưa viết |
| 8 — Map & AOI | ⬜ chưa | ⬜ chưa viết |
| 9 — Data & Config | ⬜ chưa | ⬜ chưa viết |
| 10 — Inventory | ⬜ chưa | ⬜ chưa viết |
| 11 — Monster & Combat | ⬜ chưa | ⬜ chưa viết |
| 12 — Chat | ⬜ chưa | ⬜ chưa viết |
| 13 — Package network | ⬜ chưa | ⬜ chưa viết |
| 14 — AssetBundle & CDN | ⬜ chưa | ⬜ chưa viết |
| 15 — MySQL | ⬜ chưa | ⬜ chưa viết |
| 16 — Vận hành | ⬜ chưa | ⬜ chưa viết |

---

## 5. Những thứ CỐ TÌNH bỏ qua

Ghi lại để sau này không tự hỏi "sao mình không làm cái này":

| Bỏ qua | Vì sao | Bao giờ cần |
|--------|--------|-------------|
| Mã hoá / xáo byte gói tin | Che giấu không phải bảo mật; ưu tiên hiểu framing sạch trước | Khi có người thật chơi |
| Cross-server (KuaFu) | vo-lam-genz có, nhưng chỉ có nghĩa khi đã nhiều server | Rất xa |
| Anti-cheat nâng cao | Đã có nền tảng đúng (server authoritative) là đủ chống 90% | Phase 16+ |
| gRPC / WebSocket | TCP thuần dạy được nhiều nhất về framing | Có thể thử ở Phase 13 như 1 transport thay thế |
| ECS / DOTS | Thêm 1 tầng khó không liên quan bài học chính | Không |
| Guild / Quest / Skill tree | Đều là biến thể của Phase 10 (feature dọc) | Tự làm khi đã vững |
