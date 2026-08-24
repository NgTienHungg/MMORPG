# ROADMAP — Dựng lại một MMORPG từ số 0

> **Mục tiêu dự án:** tự tay dựng lại một game MMORPG 2D (Unity client + GameServer + DBServer),
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
| Database | **SQLite trước → MySQL ở Phase 20** | SQLite = 0 setup, chạy được ngay. Đổi sang MySQL sau chính là bài test xem tầng DAL có trừu tượng đủ tốt không |
| Serialize | **MemoryPack + project `Shared` dùng chung** | Đúng phần sạch nhất của vo-lam-genz. Contract 1 nguồn → không bao giờ lệch |
| Nén | LZ4, chỉ khi payload > 4KB | Copy nguyên tắc của vo-lam-genz (`MemoryPackUtility`) |
| Thể loại | **2D platformer góc nhìn ngang** (kiểu Ninja School / Ngọc Rồng Online) | Chốt ở Phase 7. Asset nhân vật/map góc nhìn ngang dễ kiếm hơn top-down nhiều. Đổi lại phải viết motor có trọng lực + nhảy chạy **giống hệt nhau ở 2 bên** — xem Phase 8 |
| Motor di chuyển | **Kinematic tự viết, đặt trong `Shared`. Cấm `Rigidbody2D` cho nhân vật** | Physics của Unity không tồn tại trên server .NET. Hai bên tính khác nhau = rubber-band vĩnh viễn. Đây là "contract 1 nguồn" áp lên *hành vi* chứ không chỉ lên *dữ liệu* |
| Asset | **Dragon Warrior** (nhân vật, 13 nhóm trạng thái + effect fireball/explosion) + tileset **American Forest** | Có sẵn trong `Assets/Game/Textures/`. Chính bộ trạng thái phong phú này là lý do Phase 9 tách riêng thành một phase state machine |
| Hình dạng map | **Vẽ tay bằng Tilemap trong Unity, một lớp `Collision` riêng → tool Editor export ra file map JSON** (ô đặc / bệ xuyên-một-chiều, origin + kích thước bất kỳ) — cả server lẫn client cùng đọc file đó | Chốt ở Phase 9 (2026-08-22). Map platformer không cố định kích thước nên gõ tay mảng chuỗi trong `Shared` là có hai bản vẽ của cùng một map mà không ai kiểm chúng khớp nhau. Lớp `Collision` tách khỏi lớp trang trí: sửa cây cỏ không được đổi va chạm. Config riêng của map (điểm spawn, cửa sang map khác) đi cùng file này; danh sách quái / drop thêm vào từ Phase 15 |
| Định dạng file dữ liệu | **JSON, đọc bằng `Newtonsoft.Json`** — áp cho mọi file dữ liệu của game: bản đồ, bảng config, bảng item, bảng quái. Lưới ô của map nằm dưới dạng **mảng chuỗi**, mỗi hàng một dòng. File do **máy sinh** thì bỏ qua trường lạ (`Ignore`), file **người gõ tay** thì trường lạ là lỗi (`Error`) | Chốt ở Phase 10 (2026-08-24). Câu hỏi quyết định là "thêm một trường mới sau này tốn bao nhiêu" — map hôm nay chỉ có lưới va chạm, mai mốt còn cổng sang map khác, danh sách quái, vùng an toàn. Format tự chế thì mỗi trường mới là sửa cả parser lẫn writer; JSON thì thêm một property. Thêm hai cái được miễn phí: số thực không còn dính bẫy locale (spec JSON quy định sẵn, không phải nhớ `InvariantCulture`), và file cũ ↔ code mới ↔ file mới đọc lẫn nhau được. Chọn Newtonsoft vì Unity **đã có sẵn** qua `com.unity.nuget.newtonsoft-json`; `System.Text.Json` nhanh hơn nhưng phải tự kéo DLL vào Unity và hay vướng code stripping của IL2CPP. Trường `version` vẫn giữ — JSON lo được chuyện *thêm* trường, không lo được chuyện *đổi ý nghĩa* trường đã có |
| Con số của luật chơi | **Viết bằng giây, lưu trong bảng theo lớp nhân vật** (`CharacterProfile` ở `Shared`: tốc độ chạy, độ cao nhảy, thời lượng + hồi chiêu từng hành động), quy ra tick **một lần** lúc dựng bảng | Chốt ở Phase 9 (2026-08-22), thay cho `const` rải trong `MovementRules`. Người thiết kế và hoạ sĩ nói bằng giây, không nói bằng tick; và trong MMO thì mọi con số này đều khác nhau giữa các lớp nhân vật / chiêu thức. Mô phỏng vẫn đếm bằng tick (số nguyên = không sai số, replay lặp lại được). Phase 12 chỉ đổi *nguồn* của bảng (C# → file + kiểm version), không đổi chỗ gọi |
| Trạng thái nhân vật | **Hai tầng**: locomotion client tự suy từ motor · action do **server** quyết và đi trong snapshot | Client tự bật `hurt`/`die` là vi phạm quy tắc "server là source of truth" ở dạng hình ảnh. Xem Phase 9 |
| Unity | 6000.2.9f1, URP 2D, DI = VContainer | Theo `BaseCode_Test` |
| Assembly client | **Không dùng asmdef** — code game nằm hết trong `Assembly-CSharp` | Assembly do asmdef định nghĩa không tham chiếu được `Assembly-CSharp-firstpass`, mà DOTween Pro nằm ở đó dưới dạng `.cs` không asmdef → mất API Pro. Đổi lại phải compile lại toàn bộ mỗi lần sửa; cỡ dự án này vẫn dưới vài giây. Tách asmdef sau, chỉ cho phần không đụng DOTween |
| Nhân vật | **1 tài khoản = 1 nhân vật**, tự tạo trong lần vào world đầu (kiểu Ngọc Rồng Online) | Bản học ưu tiên đơn giản — bỏ màn hình chọn nhân vật. Bảng `character` vẫn tách khỏi `account` nên nâng lên nhiều nhân vật sau này chỉ là bỏ `UNIQUE(account_id)` + thêm UI chọn |
| Phân phối asset | **Addressables** (không dùng AssetBundle API trần) | Addressables *chính là* AssetBundle + tầng quản lý ở trên: catalog, hash, dependency graph, content update workflow. vo-lam-genz dùng bundle trần vì thời đó Addressables chưa chín; dự án này đã cài sẵn Addressables 2.9 cho Unity 6 |
| Hot update logic | **Lua trên server trước** (Phase 19); client hot update để phase mở rộng | Lua server rẻ và dạy đủ khái niệm: script layer, binding C#↔Lua, sandbox, hot reload. Client hot update (xLua hoặc HybridCLR) nặng hơn nhiều — làm sau khi đã hiểu cơ chế |
| Repo | **1 repo duy nhất** chứa cả client + server + shared, tới hết Phase 16 | Đổi contract là sửa cả 2 bên — cùng 1 repo thì gói gọn trong 1 commit, `git checkout` commit cũ luôn cho ra cặp client/server khớp nhau. Tách repo (hoặc submodule) buộc phải commit 2 lần mỗi lần đổi contract; quên bước 2 thì 2 bên lệch mà git không báo gì |
| Tách repo server | **Phase 21**, khi deploy thật | Lúc đó mới có lý do thật: đẩy server lên VPS không cần kéo theo vài GB asset Unity. Tách trước thời điểm đó là chịu chi phí mà chưa nhận được lợi ích |

---

## 1. Bản đồ 22 phase

Nhóm thành 5 chặng. **Không nhảy cóc** — mỗi phase dựa trên phase trước.

> 📌 **Đã đánh số lại ba lần.** (1) Sau khi chốt thể loại platformer; (2) khi tách Phase 9 "State machine"
> — Phase 8 và 9 là hai phase chèn vào, đẩy Map & AOI và Data & Config xuống 10 và 11; (3) **2026-08-24**,
> khi Phase 10 tách đôi: **10 = Map**, **11 = AOI**, mọi phase từ 11 cũ trở đi cộng thêm 1 (Data & Config
> thành 12, Vận hành thành 21). Lý do là đúng cái luật vừa rút ra từ chính Phase 9 ở §3: việc nào tự nó
> test được thì tách thành phase riêng. Gặp doc nào còn nhắc số cũ thì đó là sót — báo lại.

### Chặng A — Đường ống mạng (Phase 0–2)
> Kết thúc chặng: client và server nói chuyện được với nhau bằng gói tin có kiểu, không dùng if/else.

| # | Phase | Kết quả cụ thể | Học được gì |
|---|-------|----------------|-------------|
| **0** | **Nền móng dự án** | Unity mở compile sạch, `dotnet build` chạy, cấu trúc thư mục + submodule đủ | Layout repo mono, submodule UPM, VContainer/UniTask/MemoryPack vào Unity thế nào |
| **1** | **Transport: byte đi được 2 chiều** | Bấm nút trong Unity → server log nhận được → trả về → UI hiện RTT | TCP là *stream* không có ranh giới gói · length-prefix framing · buffer ghép gói dở · callback socket ≠ main thread |
| **2** | **Contract & Dispatch** | `NetCmd` + DTO trong `Shared`, build ra DLL cho cả 2 bên; gửi/nhận bằng attribute `[TcpHandler]` / `[NetHandler]` | Dispatch table thay switch · auto-register bằng reflection · MemoryPack + nén LZ4 · vì sao contract phải 1 nguồn |

### Chặng B — Người chơi có danh tính (Phase 3–5)
> Kết thúc chặng: đăng ký → đăng nhập → vào thẳng thế giới, nhân vật hiện ra trong map.

| # | Phase | Kết quả cụ thể | Học được gì |
|---|-------|----------------|-------------|
| **3** | **DBServer & tầng DAL** | Process `DBServer` riêng, SQLite, GameServer hỏi DB qua TCP nội bộ | Vì sao tách DB server · request/response async không block game loop · repository pattern |
| **4** | **Đăng ký / Đăng nhập** | UI login trong Unity, tài khoản lưu SQLite, sai mật khẩu báo đúng lỗi | PBKDF2 hash · token session · không bao giờ tin client · chống login trùng |
| **5** | **Vào thế giới** | Đăng nhập xong vào thẳng world: nhân vật tự tạo lần đầu (1 tài khoản = 1 nhân vật), xuất hiện đúng vị trí cũ, camera bám | Account ≠ Character ≠ Entity · get-or-create idempotent · UNIQUE thay cho check-then-act · snapshot khởi tạo |

### Chặng C — Thế giới sống (Phase 6–12)
> Kết thúc chặng: 2 client chạy song song, thấy nhau chạy/nhảy/đánh mượt trên map có tường và sàn,
> chỉ thấy nhau khi ở gần, và mọi con số đều là dữ liệu chứ không phải hằng số trong code.

| # | Phase | Kết quả cụ thể | Học được gì |
|---|-------|----------------|-------------|
| **6** | **Game loop server & di chuyển authoritative** | Server chạy tick cố định, client gửi ý định move, server quyết vị trí | Fixed tick · client prediction + reconciliation · chống speed hack |
| **7** | **Đồng bộ nhiều người chơi** | Mở 2 client (ParrelSync), thấy nhau chạy mượt, không giật | Snapshot theo tick · interpolation buffer · vì sao không gửi mỗi frame |
| **8** | **Motor platformer** 🆕 | Đổi từ di chuyển 2 trục tự do sang trọng lực + nhảy + đứng trên sàn. Cả prediction lẫn mô phỏng server dùng **chung một hàm** trong `Shared` | Vì sao không dùng được `Rigidbody2D` khi có server authoritative · entity có **vận tốc + trạng thái grounded** chứ không chỉ vị trí · reconciliation khi state phức tạp hơn 1 vector |
| **9** | **State machine trạng thái nhân vật** 🆕 | Nhân vật đổi hình đúng theo việc nó đang làm: idle / walk / jump / fall / crouch. Bấm nút đánh → **server duyệt** → cả hai client cùng thấy anim `attack`, đúng hướng mặt | **Hai tầng trạng thái**: locomotion *suy ra* từ motor (client tự tính, tốn 0 byte) vs action *do server quyết* (đi trong snapshot) · vì sao client không bao giờ được tự bật `hurt`/`die` · bảng chuyển tiếp có ràng buộc thay vì `if` lồng nhau · thời lượng trạng thái đếm bằng **tick**, không bằng độ dài clip |
| **10** | **Map: hình dạng thật** 🆕 | Vẽ lớp `Collision` trong tilemap → tool Editor export ra file map → **cả server lẫn client cùng đọc đúng file đó**. Có tường chặn, bệ xuyên-một-chiều, khe hẹp phải ngồi mới chui | Va chạm là **luật chơi** nên nó thuộc `Shared` · nhân vật hết là một điểm, nó có **thân** · tách trục X/Y để né câu hỏi không có đáp án đúng · dữ liệu đi **từ Unity sang server** (ngược chiều DLL) và vì sao chiều nào cũng phải để build lo |
| **11** | **AOI — tầm nhìn** 🆕 | Chỉ nhận gói của người ở gần: chạy xa nhau thì biến mất khỏi màn hình của nhau, chạy lại thì hiện ra. Băng thông tỉ lệ với **mật độ quanh mình**, không phải tổng người online | Spatial partition · interest management theo cột trục X · `EntitySpawn`/`EntityDespawn` đổi từ "sự kiện vào/ra world" thành "hệ quả của tầm nhìn" mà **client không sửa một dòng** · vì sao MMO không broadcast toàn map |
| **12** | **Data & Config** | Bảng config (tốc độ, trọng lực, spawn) load được, sửa không cần build lại. Phân biệt rõ **config loại A** (chỉ server đọc) và **config loại B** (bảng dữ liệu 2 bên cùng đọc) | Data-driven · 1 nguồn config · hot reload · vì sao "copy file sang cả 2 bên" là bẫy |

### Chặng D — Nội dung game (Phase 13–16)
> Kết thúc chặng: có một vòng gameplay đủ nhỏ nhưng đầy đủ: mặc đồ → đánh quái → nhận exp và đồ rơi → vào túi → lưu DB.

| # | Phase | Kết quả cụ thể | Học được gì |
|---|-------|----------------|-------------|
| **13** | **Feature dọc đầu tiên: Túi đồ & item** | Nhặt/dùng/vứt item, thoát game vào lại vẫn còn. **Đang mở túi mà nhận/mất đồ thì UI tự đúng ngay** | Quy trình chuẩn thêm feature MMO (DB → DAL → logic → packet → UI) · cache RAM + dirty flag · **delta vs snapshot** · bảng item = config loại B đầu tiên · `itemId` (instance) ≠ `templateId` (loại đồ) |
| **14** | **Chỉ số nhân vật & bảng thông tin** 🆕 | Nhân vật có bộ chỉ số đầy đủ; mặc/cởi trang bị là chỉ số đổi ngay. UI bảng thông tin liệt kê chỉ số theo nhóm | **Pipeline tính lại chỉ số**: `base(class, level) + điểm cộng + trang bị + buff → recompute → đẩy client` · chỉ số gốc vs dẫn xuất · vì sao client không bao giờ tự cộng |
| **15** | **Quái, PvP, sát thương & EXP** | Quái spawn, hai người chơi PK nhau thật: đánh gần trúng đòn thì đối phương `hurt`, hết máu thì `die`. **Fireball là entity của server** — có vị trí, bay theo tick, va chạm, không phải hiệu ứng client | Entity ngoài player · AI tick · **hitbox nằm ở server** (client chỉ vẽ) · **projectile là entity**, không phải particle · **công thức sát thương authoritative** (trừ thẳng vs tỉ lệ, crit, kháng, random, sàn tối thiểu) · đường cong EXP · phạt chênh lệch level · exp lưu DB lúc nào |
| **16** | **Chat** | Chat kênh thế giới / bản đồ / riêng, có chống spam | Broadcast có filter · rate limit · vì sao chat cũng phải đi qua server |

### Chặng E — Hạ tầng & vận hành (Phase 17–21)
> Kết thúc chặng: build được ra bản chạy thật, cập nhật nội dung không cần build lại app, đổi máy chủ chỉ bằng sửa config.

| # | Phase | Kết quả cụ thể | Học được gì |
|---|-------|----------------|-------------|
| **17** | **Tách package `com.hungnt.network`** | Phần network client thành package riêng, publish được | Thiết kế API tái dùng · tách phần game-specific khỏi infra · versioning |
| **18** | **Addressables + CDN** | Sửa asset → build content update → client tự tải bản mới, không build lại app. **Bảng config loại B đi cùng đường này**, kèm kiểm tra version lúc login | Hot update pipeline · remote catalog + hash per-file · host CDN · **chống lệch dữ liệu client/server bằng version check** |
| **19** | **Lua scripting & hot update logic** 🆕 | Công thức sát thương / drop rate / AI quái ra file `.lua`; sửa file, gõ 1 phím trong console server là có hiệu lực, **không restart** | Script layer là gì và vì sao game thật cần nó · binding C# ↔ Lua · sandbox (script không được đụng file/network) · hot reload an toàn giữa lúc đang chạy · giới hạn: cái gì nên ra script, cái gì phải giữ trong C# |
| **20** | **SQLite → MySQL** | Đổi provider, dữ liệu cũ migrate được | DAL trừu tượng đúng chưa · migration · connection pool |
| **21** | **Vận hành: build, log, deploy** | Client build ra chạy trên máy khác, trỏ IP máy bạn; server có log + đo tick | Config ngoài code · structured logging · graceful shutdown · deploy VPS |

### Để dành sau Phase 21

| Việc | Vì sao chưa xếp vào roadmap |
|------|------------------------------|
| **Hot update logic phía client** (xLua / ToLua, hoặc **HybridCLR** hot update C# thật trên IL2CPP) | Đây mới là thứ vo-lam-genz làm. Nhưng nó nặng (binding, IL2CPP, GC, debug khổ) và chỉ có nghĩa khi đã có app build ra thật + CDN chạy được (Phase 18, 21). Sau Phase 19 bạn đã hiểu đủ khái niệm để tự đánh giá nên chọn Lua hay HybridCLR |
| Skill / chiêu thức, buff-debuff có thời hạn | Biến thể của Phase 14 (nguồn chỉ số) + Phase 15 (công thức) + Phase 9 (trạng thái). Tự làm được khi đã vững |
| Party / tổ đội, chia exp theo đội | Biến thể của Phase 15 |

---

## 2. Quy hoạch dải `NetCmd`

Chốt ngay từ đầu để không phải dời số sau (bài học từ vo-lam-genz: dải Bát Quái phải dời vì đụng feature khác).

| Dải | Nhóm | Phase |
|-----|------|-------|
| `0` | `None` — giá trị vô hiệu, không dùng | — |
| `1–99` | **Hệ thống**: ping, handshake, disconnect, error, version check | 1–2, 12, 18 |
| `100–199` | **Auth**: register, login, logout, token | 4 |
| `200–299` | **Character**: enter world, **chỉ số nhân vật, cộng điểm, trang bị** (mở rộng sau: list, create, delete nếu quay lại nhiều nhân vật) | 5, 14 |
| `300–399` | **World / Movement**: move, snapshot, spawn, despawn, **trạng thái hành động** | 6–11 |
| `400–499` | **Inventory / Item** | 13 |
| `500–599` | **Combat / Monster**: sát thương, chết, drop, exp/level, **projectile** | 15 |
| `600–699` | **Chat** | 16 |
| `700–999` | *(trống — feature sau)* | |

**`DbCmd`** — protocol nội bộ GameServer ↔ DBServer, **client không bao giờ thấy**. Enum riêng, dải riêng:

| Dải | Nhóm | Phase |
|-----|------|-------|
| `1000–1099` | **Hệ thống**: ping, server_meta | 3 |
| `1100–1199` | **Account** | 4 |
| `1200–1299` | **Character** (gồm chỉ số, điểm cộng, exp/level) | 5, 14, 15 |
| `1300–1399` | **Inventory** (gồm trang bị đang mặc) | 13 |
| `1400–1499` | **Combat / Monster** | 15 |
| `1500+` | *(trống — feature sau)* | |

**Quy tắc:** thêm lệnh mới → luôn thêm vào **cuối dải của feature**, không chèn giữa. Không tái sử dụng số đã xoá.

---

## 2b. Hai loại config — phân biệt từ Phase 12

Bệnh của vo-lam-genz **không phải** là "gen file byte rồi copy sang cả 2 bên". Copy chỉ là triệu chứng.
Bệnh thật là: **không ai kiểm tra 2 bản có khớp nhau không**. Copy thiếu một lần → client hiển thị item A,
server xử lý item B; không lỗi biên dịch, không log, chỉ có bug câm.

| | **Loại A — tham số vận hành** | **Loại B — bảng dữ liệu** |
|---|---|---|
| Ví dụ | `moveSpeed`, `gravity`, `jumpForce`, điểm spawn | bảng item, bảng quái, chỉ số gốc theo class, drop table |
| Ai cần | server xử lý logic; client chỉ cần vài giá trị để dự đoán | **cả 2 bên đều đọc**: server tính logic, client hiển thị tên / icon / mô tả |
| Cách chữa | **Chỉ server đọc file.** Giá trị nào client cần thì đi trong `EnterWorldResponse` | **Schema** (kiểu C#) đặt ở `Server/Shared/` → 1 nguồn định nghĩa. **Dữ liệu** có 1 bản gốc duy nhất, client kéo về qua Addressables/CDN — **không commit bản copy trong `Assets/`** |
| Chống lệch bằng gì | Client luôn chạy đúng số của server nó đang nối vào, kể cả 2 server cấu hình khác nhau | Server gửi **hash/version của bảng** lúc login; client lệch version thì **bị chặn vào world** cho tới khi tải bản mới |
| Làm ở phase nào | Phase 12 | **Bản đồ** (loại B đầu tiên): Phase 10 · trường version + kiểm lúc login: Phase 12 · schema + bảng item: Phase 13 · đường phân phối qua CDN: Phase 18 |

---

## 3. Cách làm việc giữa bạn và Claude

1. Claude viết chi tiết `guides/PHASE-N.md` **trước** khi bạn tới phase đó.
2. Bạn tự code theo doc. Gặp `✅ CHECKPOINT` thì phải chạy được mới đi tiếp.
3. Kẹt ở đâu → hỏi Claude bằng **triệu chứng cụ thể** ("gửi được nhưng server không nhận", kèm log),
   đừng hỏi "sửa hộ".
4. Làm xong phase → báo Claude để: (a) review chỗ lệch so với doc, (b) viết chi tiết phase kế tiếp,
   (c) **cập nhật `README.md` gốc** — mục tính năng đã làm + tech stack nếu có công nghệ mới.
5. Khi phát hiện code có tính hạ tầng, tái dùng cao → ghi vào `CANDIDATE-PACKAGES.md` để cân nhắc tách package.

**Nguyên tắc:** doc mô tả *cái gì* và *vì sao*, kèm code đủ để chép khi bí. Nhưng gõ lại tay vẫn học được nhiều hơn chép.

**Kích thước một phase** (rút ra sau Phase 9 — 2026-08-23): **một phase = một kết quả chạy được, tối đa
2–3 CHECKPOINT, doc ~400–600 dòng.** Phase 9 dài gấp ba mức đó vì gộp hai việc độc lập (hoạt ảnh theo
tư thế · tầng hành động do server quyết) — hệ quả là khi có lỗi thì không khoanh được vùng, và một dòng
thiếu ở server làm hỏng thứ trông giống lỗi client. Từ nay việc nào **tự nó test được** thì tách thành
phase riêng, kể cả khi hai việc "cùng chủ đề".

**Format doc từ Phase 5:** mỗi bước chia hai tầng — *hướng làm* (mô tả + quyết định thiết kế + code khung)
hiện sẵn, *lời giải đầy đủ* nằm trong foldout `<details>` mặc định đóng ngay dưới. Câu hỏi "Tự kiểm tra
hiểu bài" cũng vậy: mỗi câu một foldout `📖 Đáp án câu N` sát ngay dưới câu hỏi. Tự nghĩ và tự làm trước,
mở lời giải/đáp án sau để đối chiếu.

---

## 4. Trạng thái

| Phase | Trạng thái | Doc chi tiết |
|-------|-----------|--------------|
| 0 — Nền móng | ✅ xong | [`guides/PHASE-0.md`](guides/PHASE-0.md) ✅ |
| 1 — Transport | ✅ xong | [`guides/PHASE-1.md`](guides/PHASE-1.md) ✅ |
| 2 — Contract & Dispatch | ✅ xong | [`guides/PHASE-2.md`](guides/PHASE-2.md) ✅ |
| 3 — DBServer & DAL | ✅ xong | [`guides/PHASE-3.md`](guides/PHASE-3.md) ✅ |
| 4 — Auth | ✅ xong | [`guides/PHASE-4.md`](guides/PHASE-4.md) ✅ |
| 5 — Vào thế giới | ✅ xong | [`guides/PHASE-5.md`](guides/PHASE-5.md) ✅ |
| 6 — Game loop & movement | ✅ xong | [`guides/PHASE-6.md`](guides/PHASE-6.md) ✅ |
| 7 — Multi-player sync | ✅ xong | [`guides/PHASE-7.md`](guides/PHASE-7.md) ✅ |
| 8 — Motor platformer 🆕 | ✅ xong | [`guides/PHASE-8.md`](guides/PHASE-8.md) ✅ |
| 9 — State machine trạng thái 🆕 | ✅ xong | [`guides/PHASE-9.md`](guides/PHASE-9.md) ✅ (đã soát lại 2026-08-24 cho khớp code đã làm xong) |
| **10 — Map: hình dạng thật** 🆕 | ⏳ **làm tiếp theo** | [`guides/PHASE-10.md`](guides/PHASE-10.md) ✅ (viết lại 2026-08-24: tách AOI ra Phase 11, pipeline tilemap → file map **JSON**, khớp `CharacterProfile` của Phase 9) |
| 11 — AOI 🆕 | ⬜ chưa | [`guides/PHASE-11.md`](guides/PHASE-11.md) ✅ (tách ra từ Phase 10 cũ, 2026-08-24) |
| 12 — Data & Config | ⬜ chưa | [`guides/PHASE-12.md`](guides/PHASE-12.md) ⚠️ cần soát: bảng số đã có ở Phase 9 và map đã ra file ở Phase 10, phase này chỉ đổi nguồn + kiểm version |
| 13 — Túi đồ & item | ⬜ chưa | ⬜ chưa viết |
| 14 — Chỉ số nhân vật 🆕 | ⬜ chưa | ⬜ chưa viết |
| 15 — Quái, PvP & EXP | ⬜ chưa | ⬜ chưa viết |
| 16 — Chat | ⬜ chưa | ⬜ chưa viết |
| 17 — Package network | ⬜ chưa | ⬜ chưa viết |
| 18 — Addressables & CDN | ⬜ chưa | ⬜ chưa viết |
| 19 — Lua & hot update logic 🆕 | ⬜ chưa | ⬜ chưa viết |
| 20 — MySQL | ⬜ chưa | ⬜ chưa viết |
| 21 — Vận hành | ⬜ chưa | ⬜ chưa viết |

---

## 5. Những thứ CỐ TÌNH bỏ qua

Ghi lại để sau này không tự hỏi "sao mình không làm cái này":

| Bỏ qua | Vì sao | Bao giờ cần |
|--------|--------|-------------|
| Mã hoá / xáo byte gói tin | Che giấu không phải bảo mật; ưu tiên hiểu framing sạch trước | Khi có người thật chơi |
| Cross-server (KuaFu) | vo-lam-genz có, nhưng chỉ có nghĩa khi đã nhiều server | Rất xa |
| Anti-cheat nâng cao | Đã có nền tảng đúng (server authoritative) là đủ chống 90% | Phase 21+ |
| gRPC / WebSocket | TCP thuần dạy được nhiều nhất về framing | Có thể thử ở Phase 17 như 1 transport thay thế |
| ECS / DOTS | Thêm 1 tầng khó không liên quan bài học chính | Không |
| `Rigidbody2D` cho nhân vật | Physics Unity không chạy được trên server .NET → 2 bên tính khác nhau → rubber-band. Vẫn dùng `Rigidbody2D`/`Collider2D` cho thứ **không cần đồng bộ** (hiệu ứng, vật trang trí) | Không bao giờ, cho entity có server authority |
| Guild / Quest / Skill tree | Đều là biến thể của Phase 13 (feature dọc) + Phase 14 (nguồn chỉ số) | Tự làm khi đã vững |
| Nhiều nhân vật / màn hình chọn nhân vật | 1 tài khoản = 1 nhân vật (kiểu NRO) đủ cho mọi bài học của dự án; màn hình chọn chỉ thêm UI chứ không thêm kiến thức server | Khi muốn làm chuẩn thể loại — bỏ `UNIQUE(account_id)`, thêm CharacterList/Create/Delete; bản thiết kế 3-slot cũ còn trong git history của `PHASE-5.md` |
| Hot update logic phía client (xLua / HybridCLR) | Nặng và chỉ có nghĩa sau khi đã có CDN + app build thật. Phase 19 (Lua server) dạy đủ khái niệm để tự đánh giá | Sau Phase 21 — xem bảng "Để dành" ở §1 |
