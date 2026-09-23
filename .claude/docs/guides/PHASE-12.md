# PHASE 12 — Data & Config: số ra khỏi code

> **Kết quả cuối Phase 12:** trọng lực, coyote time, tốc độ chạy, lực nhảy, kích thước thân, thời
> lượng đòn đánh, bán kính AOI, map khởi đầu — tất cả nằm trong file JSON, không còn `const` nào
> trong code. Số **loại A** (`Config/game.json`) sửa xong restart server là có hiệu lực ở cả hai
> bên, **không build lại client**. Bảng **loại B** (`Assets/Game/Resources/Config/*.json`) thì cả
> hai bên cùng đọc, nên sửa xong phải build lại GameServer — và mỗi bên in một **dấu vân tay** ra
> log để bạn thấy ngay lần quên. Phép kiểm tự động duy nhất trong phase này là kiểm **contract**
> (Bước 5), không phải kiểm dữ liệu.
>
> **Điều kiện:** xong [`PHASE-11.md`](PHASE-11.md) — AOI và chuyển map đã chạy.
>
> **Bài học chính:** (1) config cũng phải có **đúng một nguồn**, nhưng "một nguồn" nghĩa là một
> nguồn **trên đĩa**, không nhất thiết là một bản trong bộ nhớ; (2) có **hai loại config** khác
> nhau về bản chất, đi hai đường khác nhau, và nhầm loại là nguồn của lớp bug câm khó chịu nhất
> trong game online; (3) **mỗi bên tự quyết định nạp gì** — ép hai bên nạp cùng một tập bảng là một
> ràng buộc sẽ vỡ ngay ở feature thứ ba.

Format như trước: **hướng làm** hiện sẵn, **📖 Lời giải** trong foldout.

> **Viết lại 2026-09-22 — bản này thay hẳn bản 2026-09-17.** Bản cũ hỏng ba chỗ, cả ba đều đủ để làm
> người gõ theo nó bị kẹt — cộng một chỗ mà chính bản viết lại đầu tiên làm hỏng (mục 4):
>
> 1. **Thiếu hẳn nửa client.** Nó tả rất kỹ việc server đọc file, rồi dừng. Chuyện client nạp bảng thế
>    nào chỉ còn một dòng trong bảng Troubleshooting — không có code. Hậu quả đúng như dự đoán:
>    `CharacterConfigContainer.Get(id)` ném vì bảng rỗng.
> 2. **Tên class trong doc khác tên trong code.** Nó viết `WorldRules` / `CharacterProfile` /
>    `CharacterProfiles.Build()`; code thật là `WorldConfig` / `CharacterConfig` /
>    `CharacterConfigContainer.Load()`. Bản này đã đối chiếu từng tên với repo.
> 3. **Thiết kế bắt chép code.** `ConfigService` của nó có một hàm nạp cho mỗi bảng, nên bảng item ở
>    Phase 13 là chép lại cả hàm. Bản này thêm **Bước 3** để chữa: một hàm nạp cho mọi FILE, kể cả `game.json`.
> 4. **Gửi vân tay xuống client để nó tự so (sửa 2026-09-22, lần hai).** Bản viết lại đầu tiên cho
>    `EnterWorldResponse` mang một mảng `ConfigTables[]` và bắt client so từng bảng. Sai: nó ngầm
>    khẳng định hai bên nạp cùng một tập bảng, trong khi server luôn có bảng client không đọc tới
>    (tỉ lệ rơi đồ, AI quái) và Phase 18 còn cho client tải dần từ CDN. Vân tay ở lại — nhưng chỉ
>    như **một dòng log ở mỗi bên**. `MapGrid.Checksum()` cũng đi theo, vì nó là hàm băm viết tay
>    cuối cùng còn sót.
>
> Và thêm **Bước 2** — sổ service phía server — vì trước khi có bảng thứ hai thì `Program.cs` phải hết
> là nơi gán tay từng service bằng static property.
>
> Luật viết doc rút ra từ lần hỏng này nằm ở `CLAUDE.md` §Viết tài liệu phase và skill `phase-doc`.

---

## Đếm lại xem còn bao nhiêu số trong code

Trước khi thiết kế gì, mở code ra đếm. Sau Phase 11, những con số **có thể chỉnh để đổi cảm giác chơi**
nằm ở đúng ba chỗ:

| Ở đâu | Là gì | Ai đọc |
|---|---|---|
| `MovementRules` — 5 `const` | `GRAVITY`, `MAX_FALL_SPEED`, `COYOTE_TICKS`, `JUMP_BUFFER_TICKS`, `DROP_THROUGH_TICKS` | **cả hai bên** (client dự đoán bằng chính `Step`) |
| `CharacterConfigContainer.Build()` | bảng theo lớp nhân vật: tốc độ, lực nhảy, ba kích thước thân, ba `ActionData` | **cả hai bên** (client dự đoán + co clip hoạt ảnh theo `DurationTicks`) |
| rải rác phía server | `WorldService.AOI_RADIUS_X`, `WorldService.DEFAULT_CLASS_ID`, `MapRegistry.STARTING_MAP_ID` | **chỉ server** |

Ba dòng ấy chính là hai loại config, và cột "ai đọc" là thứ quyết định cách chữa:

| | **Loại A — tham số vận hành** | **Loại B — bảng dữ liệu** |
|---|---|---|
| Trong dự án này | 5 hằng của `MovementRules` · ba số chỉ-server ở trên | **bảng `CharacterConfig`** · **bản đồ** · sắp tới: bảng item (Phase 13), bảng quái (Phase 15) |
| Hình dạng | vài con số rời | bảng có cấu trúc, có khoá, sẽ còn dài ra |
| Cách chống lệch | **chỉ server đọc file**, phần client cần thì đẩy trong `EnterWorldResponse` | **schema** ở `Shared` (một định nghĩa); **dữ liệu** một bản gốc + **dấu vân tay** để so |
| Làm ở đâu | Bước 1 | Bước 2 |

Điểm dễ hiểu sai nhất, nhắc lại vì nó là gốc của cả phase: bệnh của cách làm cũ (kiểu vo-lam-genz)
**không phải** là "gen file rồi copy sang cả hai bên". Copy chỉ là triệu chứng. Bệnh thật là **không ai
kiểm tra hai bản có khớp nhau không** — copy thiếu một lần thì client hiển thị item A trong khi server
xử lý item B; không lỗi biên dịch, không log, chỉ có bug câm.

> Cách chữa không phải "đừng copy" — đôi khi buộc phải có hai bản. Cách chữa là **đặt ranh giới cho
> đúng**: loại A thì không tồn tại bản thứ hai, loại B thì mỗi bên nạp bản của mình và in **dấu vân
> tay** ra log để đối chiếu khi nghi ngờ.

### Vì sao client không được đọc `game.json`

Trực giác đầu tiên của mọi người: "để hai bên cùng đọc `game.json` cho đồng bộ". Nghe giống contract
một nguồn — nhưng với **loại A** thì là bẫy, vì **file giống nhau không có nghĩa là giá trị đang chạy
giống nhau**:

- Client build ra mang bản copy của file **tại thời điểm build**. Server sửa config → mọi client ngoài
  kia vẫn chạy số cũ. Đây chính xác là bug "chép tay `NetCmd`" ở dạng dữ liệu.
- File nằm trong máy người chơi thì người chơi sửa được. Với giá trị chỉ-hiển-thị thì vô hại; với
  `Gravity` mà client dùng để dự đoán thì là mời họ tự chỉnh — server vẫn thắng (Phase 6), nhưng họ tự
  gây rubber-band rồi đi report "game lag".

Cả hai lý do trên đều là lý do của **loại A**: người vận hành chỉnh số giữa hai lần restart, không
patch client. Bảng **loại B** thì ngược hẳn — nó chỉ đổi cùng một bản build, và client BẮT BUỘC phải
có nó (tên item phải hiện được trước khi có gói tin nào). Nên loại B: hai bản, mỗi bên một bản.

```
Config/game.json                  ──► CHỈ server đọc (hot reload: phím R)
   (loại A, gốc repo)                   │
                                        ├─► WorldService · CharacterService · PlayerEntity
                                        └─► EnterWorldResponse { World } ──► client

Assets/Game/Resources/Config/     ──┬─► client đọc thẳng (lúc khởi động, TRƯỚC login)
   characters.json  (loại B)        │      └─► CharacterConfigContainer.Load ──► in vân tay ra log
   items.json       (Phase 13)      │
                                    └─► csproj copy ──► Data/Config/ ──► server đọc
                                                 └─► CharacterConfigContainer.Load ──► in vân tay ra log

Không có gì của loại B đi trên dây — kể cả vân tay. Hai dòng log ấy là để NGƯỜI đối chiếu khi
nghi ngờ, không phải để máy so. Vì sao không so bằng máy: xem ngay dưới.
```

> **Vì sao không gửi danh sách vân tay xuống client để nó tự so?** Vì như thế là ép client nạp đúng
> tập bảng của server. Server sẽ có bảng client không bao giờ đọc tới (tỉ lệ rơi đồ, AI quái, thưởng
> nhiệm vụ), client sẽ có bảng server không cần (thoại, mô tả kỹ năng), và Phase 18 còn cho client
> **tải dần** từ CDN — vào world với ba bảng, mở túi đồ mới tải bảng thứ tư. Một phép so "hai danh
> sách phải bằng nhau" chặn hết cả ba chuyện đó.
>
> Thứ chặn được "client chạy dữ liệu cũ" một cách thật sự là **một số phiên bản cho cả gói dữ liệu**,
> kiểm một lần lúc đăng nhập. Đó là việc của trình patch — Phase 18, không phải phase này.

### Cái gì KHÔNG vào config

Danh sách này quan trọng ngang danh sách trên, và lý do của từng dòng còn quan trọng hơn:

| Không vào config | Vì sao |
|---|---|
| `TICK_RATE`, `TICK_DT` | **Hằng số của giao thức**, cùng đẳng cấp với format khung gói tin. Đổi nó là đổi nhịp của toàn bộ prediction/reconciliation ở cả hai bên — phải đổi bằng build có chủ đích, không phải bằng file text lúc nửa đêm |
| `EXPIRED` | Giá trị canh (sentinel) của thuật toán, không phải số liệu game. Chỉnh nó không có nghĩa gì |
| `EDGE` | Số của **phép quét va chạm**, gắn với cách `Floor` xử lý đường biên ô. Người cân bằng game không có lý do nào để chạm vào nó, và chạm vào thì `Grounded` nhấp nháy |
| `MapGrid.CELL_SIZE` | Một phần định nghĩa của format map. Đổi nó là đổi cách đọc mọi bản đồ đã có |
| Màu sắc, âm lượng, phím tắt | Thuần client, không liên quan server. Đó là đất của `com.hungnt.datasave` |

Ranh giới chung: **số liệu game** thì ra file; **hằng số của thuật toán hoặc của giao thức** thì ở lại
trong code, cạnh đoạn code dựa vào nó.

---

## Hai file config nằm ở đâu

Trả lời trước khi bắt đầu, vì mọi bước dưới đây đều giả định nó:

```
MMORPG/                          ← gốc repo
├── Assets/
│   └── Game/Resources/Config/   ← loại B: bảng CẢ HAI BÊN đọc
│       ├── characters.json      ← bước này tạo
│       └── items.json           ← Phase 13 thêm
├── Config/                      ← TẠO MỚI ở bước này
│   └── game.json                ← loại A: luật thế giới + số CHỈ server dùng
├── Server/
│   └── GameServer/
│       └── bin/Debug/net8.0/
│           └── Data/
│               ├── Maps/        ← Phase 10 đã có
│               └── Config/      ← csproj copy CẢ HAI nguồn vào đây lúc build
└── ...
```

**Vì sao `game.json` ở gốc repo chứ không trong `Server/GameServer/`:** nó là **dữ liệu vận hành**,
không phải source của một process. Ngày tách repo server (Phase 21) thì `Config/` đi theo server, và
ngày Phase 19 có file `.lua` thì chúng cũng nằm cạnh nhau ở đây. Một thư mục dữ liệu ở gốc, không
phải ba chỗ khác nhau.

**Vì sao bảng loại B nằm trong `Assets/` chứ không nằm cạnh `game.json`:** client phải đọc được nó
lúc chạy mà không tải gì thêm, và cách rẻ nhất để làm thế trong Unity là `Resources/`. Chiều copy
chỉ có thể là Assets → server: ngược lại thì file nằm ngoài `Assets/` và Unity không nhìn thấy nó.
Phase 18 đổi `Resources/` thành Addressables/CDN; lúc đó chỉ hàm nạp của client đổi.

**Vì sao code đọc từ `Data/Config/` cạnh exe chứ không đọc thẳng `Config/` ở gốc repo:** cùng lý do với
`Data/Maps` của Phase 10 — `AppContext.BaseDirectory` là chỗ duy nhất đúng dù bạn chạy từ Rider, từ
`dotnet run`, hay từ một bản copy đem sang máy khác. Đường dẫn tương đối tới gốc repo chỉ đúng trên máy
bạn, và sai trên mọi máy khác.

**Hệ quả phải nhớ:** `CopyToOutputDirectory="PreserveNewest"` chỉ chạy **lúc build**. Sửa `Config/game.json`
ở gốc repo rồi bấm `R` thì không thấy gì đổi, vì `R` đọc bản trong `bin/`. Lúc thử nhanh thì sửa thẳng
bản trong `bin/Debug/net8.0/Data/Config/`; lúc chốt số thì chép ngược về gốc repo và build lại.

---

## Danh sách file — tạo gì, sửa gì

Doc này dài, và phần "hướng làm" nói bằng lời. Bảng dưới là bản đối chiếu bằng **tên file** để không sót
— gạch từng dòng khi xong. **Mỗi dòng ở đây có một khối code tương ứng trong foldout lời giải của bước
đó.** Thấy một dòng không có code là doc hỏng, báo lại.

**Bước 1 — loại A: luật thế giới ra file**

| File | Việc |
|---|---|
| `Config/game.json` | 🆕 tạo |
| `Server/Shared/World/WorldConfig.cs` | 🆕 tạo |
| `Server/Shared/World/Movement/MovementRules.cs` | ✏️ xoá 5 `const`, `Step` nhận thêm `WorldConfig world` |
| `Server/GameServer/Config/GameConfigData.cs` | 🆕 tạo |
| `Server/GameServer/Config/ConfigService.cs` | 🆕 tạo (bản đầu — Bước 3 viết lại phần bảng) |
| `Server/GameServer/Config/ConfigLimits.cs` | 🆕 tạo |
| `Server/GameServer/GameServer.csproj` | ✏️ copy `Config/*.json` → `Data/Config/` |
| `Server/GameServer/World/MapRegistry.cs` | ✏️ hàm dựng nhận `ConfigService` |
| `Server/GameServer/World/WorldService.cs` | ✏️ hàm dựng nhận `ConfigService`; bán kính AOI từ config |
| `Server/GameServer/World/CharacterService.cs` | ✏️ hàm dựng nhận `ConfigService`; gắn `World` vào response |
| `Server/GameServer/World/PlayerEntity.cs` | ✏️ giữ `WorldConfig` chốt lúc dựng |
| `Server/Shared/Dto/Character/CharacterDto.cs` | ✏️ `EnterWorldResponse` thêm `WorldConfig World` |

**Bước 2 — sổ service phía server**

| File | Việc |
|---|---|
| `Server/GameServer/Boot/ServerServices.cs` | 🆕 tạo |
| `Server/GameServer/Boot/ServerBootstrap.cs` | 🆕 tạo |
| `Server/GameServer/Program.cs` | ✏️ chỉ còn vòng đời process · boot hỏng thì log + `return 1` · **luồng đọc phím đặt TRƯỚC vòng accept** |
| `Server/GameServer/Handlers/AuthHandler.cs` | ✏️ bỏ static property, lấy service từ sổ |
| `Server/GameServer/Handlers/CharacterHandler.cs` | ✏️ như trên |
| `Server/GameServer/Handlers/SystemHandler.cs` | ✏️ như trên |
| `Server/GameServer/ClientSession.cs` | ✏️ `TryGet<CharacterService>` thay cho `CharacterHandler.CharacterService` |

**Bước 3 — loại B ở server: một hàm nạp cho MỌI bảng**

| File | Việc |
|---|---|
| `Assets/Game/Resources/Config/characters.json` | 🆕 tạo (+ file `.meta`) — **trong Resources, không phải gốc repo** |
| `Server/Shared/World/Fnv1a.cs` | 🆕 tạo |
| `Server/Shared/World/IConfigFile.cs` | 🆕 tạo |
| `Server/Shared/World/ConfigFingerprint.cs` | 🆕 tạo |
| `Server/Shared/World/ConfigFiles.cs` | 🆕 tạo |
| `Server/Shared/World/Map/MapGrid.cs` | ✏️ **xoá** `Checksum()` và `Mix` riêng — map không có hàm băm riêng |
| `Server/Shared.Tests/MapGridParserTests.cs` | ✏️ hai bài test bỏ `Checksum()`, so từng ô bằng `AssertSameShape` |
| `Server/Shared/World/Character/CharacterTableData.cs` | 🆕 tạo (`ActionData` + `CharacterTableData`) |
| `Server/Shared/World/Character/CharacterConfig.cs` | ✏️ thành kiểu nạp được; `ActionDefinition` biến mất |
| `Server/Shared/World/Character/CharacterConfigContainer.cs` | ✏️ `Build()` → `Load(CharacterTableData)` |
| `Server/GameServer/Config/ConfigService.cs` | ✏️ gộp `LoadGame()` + `LoadCharacters()` thành `LoadFile<T>` generic + `LoadTable<T>` |
| `Server/GameServer/GameServer.csproj` | ✏️ copy `Resources/Config/*.json` → `Data/Config/` |
| `Server/GameServer/Config/GameConfigData.cs` | ✏️ `: IConfigFile` — `Version` + `RowCount => 1` |
| `Server/Shared.Tests/ConfigFingerprintTests.cs` | 🆕 tạo |

**Bước 4 — loại B ở client: nạp bảng của CHÍNH NÓ** ← *bước mà bản doc cũ thiếu hẳn*

| File | Việc |
|---|---|
| `Assets/Game/Scripts/Config/ConfigService.cs` | 🆕 tạo — đọc file của chính client, in vân tay ra log |
| `Assets/Game/Scripts/Boot/GameLifetimeScope.cs` | ✏️ **đăng ký `Config.ConfigService`** |
| `Assets/Game/Scripts/World/WorldPresenter.cs` | ✏️ `Apply(response)` TRƯỚC khi spawn · `Clear()` khi rời world |
| `Assets/Game/Scripts/World/WorldApi.cs` | ✏️ xoá `public static WorldConfig Config` |
| `Assets/Game/Scripts/World/WorldSpawner.cs` | ✏️ tra `CharacterConfigContainer`, truyền `WorldConfig` xuống motor |
| `Assets/Game/Scripts/World/PlayerMotor.cs` | ✏️ `Init` nhận `CharacterConfig` + `WorldConfig`; dùng ở **cả hai** chỗ gọi `Step` |

**Bước 5 — contract hash**

| File | Việc |
|---|---|
| `Server/Shared/Net/Contract.cs` | 🆕 tạo |
| `Server/Shared/Net/NetCmd.cs` | ✏️ `VersionCheck = 6` |
| `Server/Shared/Dto/SystemDto.cs` | ✏️ thêm `VersionCheckRequest/Response` |
| `Server/Shared/HandshakeDto.cs` | ❌ xoá |
| `Server/GameServer/SessionState.cs` | ✏️ thêm `Verified = 1`, dồn hai bậc sau lên |
| `Server/GameServer/ClientSession.cs` | ✏️ thêm `MarkVerified()`, sửa `MarkLoggedOut()` |
| `Server/GameServer/Handlers/SystemHandler.cs` | ✏️ handler `VersionCheck` · nâng `MinState` của các lệnh khác |
| `Server/GameServer/Handlers/AuthHandler.cs` | ✏️ `Register`/`Login` lên `MinState = Verified` |
| `Assets/Game/Scripts/Network/Handlers/SystemNetHandler.cs` | ✏️ handler + event `OnVersionCheck` |
| `Assets/Game/Scripts/Auth/LoginPresenter.cs` | ✏️ gửi `VersionCheck` sau khi nối, chặn login tới khi có kết quả |

---
## Bước 1 — Loại A: `WorldConfig` ra file, và đi xuống client

### Hướng làm

**Một kiểu, không phải hai.** Phase 9 đã chốt: người thiết kế viết bằng **giây**, mô phỏng đếm bằng
**tick**, và phép quy đổi chạy **một lần** chứ không nằm trong `Step`. Cần giữ đúng điều đó — nhưng
**không** cần hai class để giữ nó.

`WorldConfig` là **một** class, mang cả hai: giây là thứ đọc từ file và đi trên dây; tick là **property
tính ra** từ giây, không lưu và không tuần tự hoá.

> ### Khi nào tách "kiểu file" khỏi "kiểu chạy", khi nào không
>
> Phase 10 tách `MapConfig` khỏi `MapGrid`, và tách đúng — vì **hình dạng hai bên thật sự khác nhau**:
> file là danh sách chuỗi đọc từ trên xuống, kiểu chạy là mảng phẳng `CellType[]` có gốc toạ độ, và
> giữa chúng có một phép lật trục Y. Hai hình dạng khác nhau thì hai kiểu, và phép chuyển đổi là một
> hàm thật.
>
> Ở đây thì **hình dạng giống hệt nhau**: năm con số vào, năm con số ra. Tách làm hai class nghĩa là
> chép tay năm dòng gán — và **đó mới là chỗ sinh bug**, không phải chỗ tránh bug:
>
> ```csharp
> // Thêm một trường vào file? Phải nhớ sửa BA chỗ:
> class WorldConfigData { ...; public float ApexBonusSeconds { get; set; } }   // 1. khai báo
> class WorldConfig     { ...; public int ApexBonusTicks { get; } }            // 2. khai báo lần nữa
> WorldConfig(WorldConfigData d) { /* quên dòng gán thứ 6 */ }                  // 3. và gán
> ```
>
> Quên bước 3 thì trường đó đọc được từ file, đi qua validate, rồi **bị vứt đi trong im lặng** — không
> lỗi biên dịch, không log, và triệu chứng là "sửa số trong file mà không thấy khác gì". Đúng cái loại
> bug câm mà cả phase này sinh ra để chống.
>
> **Tiêu chí:** tách khi phép chuyển đổi là một **hàm thật** (đổi hình dạng, lật trục, dựng chỉ mục);
> gộp khi nó chỉ là một dãy phép gán 1-1. `CharacterRow` ≠ `PlayerEntity` ở Phase 5 là ví dụ tách đúng
> vì lý do thứ ba: chúng có **vòng đời** khác nhau (một cái sống trong DB, một cái sống trong world).

> ### Tính sẵn một lần, hay property tính lại mỗi lần đọc?
>
> Hai cách đều giữ được kỷ luật "giây là nguồn, tick là dẫn xuất", và dự án này **dùng cả hai** — ở hai
> chỗ khác nhau, theo một tiêu chí duy nhất: **số lần đọc trong một tick**.
>
> ```csharp
> // WorldConfig — property tính lại. Ba giá trị, mỗi tick đọc vài lần.
> public int CoyoteTicks => MovementRules.ToTicks(CoyoteSeconds);
>
> // ActionData — field tính sẵn, quy đổi trong Prepare().
> [MemoryPackIgnore] [JsonIgnore] public int DurationTicks;
> ```
>
> **Property tính lại** không có gì để quên: không `Prepare()`, không hai thuộc tính bỏ qua, và không
> có trạng thái "object đã dựng nhưng chưa quy đổi" — thứ mà nếu tồn tại thì sớm muộn sẽ có một đường
> đi tới nó. Giá phải trả là một `MathF.Ceiling` mỗi lần đọc.
>
> **Field tính sẵn** đáng khi giá ấy nhân lên: `ActionData` là **struct** nằm trong mảng, `GetAction()`
> trả về một **bản copy**, và giá trị được đọc lại nhiều lần trong vòng tick của mỗi entity. Đổi lại
> phải nhớ ba thứ: gọi `Prepare()`, đánh dấu `[MemoryPackIgnore] [JsonIgnore]`, và **dùng `for` chứ
> không `foreach`** khi gọi `Prepare()` trên mảng struct — `foreach` cho ra bản copy, `Prepare()` ghi
> vào bản copy ấy rồi vứt đi, và triệu chứng là mọi thời lượng bằng 0 mà không có lỗi nào.
>
> Tiêu chí ngắn gọn: **dưới vài lần mỗi tick thì tính lại; trong vòng lặp nóng thì tính sẵn.** Và khi
> đã tính sẵn thì phép quy đổi phải nằm ở **đúng một chỗ** — với `ActionData` đó là
> `CharacterConfigContainer.Load`, chỗ duy nhất trong cả dự án được dựng bảng.

**`MovementRules` mất 5 `const`, `Step` nhận thêm một tham số.** Chữ ký thành:

```csharp
public static MoveState Step(MoveState state, MoveIntent intent, float dt,
    WorldConfig world, CharacterConfig config, MapGrid map)
```

Sáu tham số — đúng lúc để hỏi câu mà mọi codebase đều gặp: *có nên gom `world` + `config` + `map` thành
một `SimContext` không?* Câu trả lời ở đây là **không**, và lý do đáng nhớ hơn câu trả lời: mở
`MovementRules` ra xem, **chỉ mình `Step` cần `world`**; các hàm riêng (`ResolveHorizontal`,
`ResolveVertical`, `BlocksFall`…) chỉ cần `map` và `config`. Gom lại là ép mọi hàm nhận một gói to hơn
thứ nó dùng. Gom khi bộ ba ấy phải chui qua **nhiều tầng**, không phải khi nó xuất hiện ở một chữ ký.

Ba tham số mới đều là kiểu khác nhau nên gõ nhầm thứ tự là **lỗi biên dịch**, không phải bug thầm lặng.

**File `Config/game.json`** ở gốc repo (cạnh `Server/`, `Assets/` — nó là dữ liệu vận hành, không thuộc
source của process nào):

```json
{
  "Version": 1,
  "World": {
    "Gravity": 30.0,
    "MaxFallSpeed": 20.0,
    "CoyoteSeconds": 0.15,
    "JumpBufferSeconds": 0.15,
    "DropThroughSeconds": 0.3
  },
  "Server": {
    "StartingMapId": 1,
    "DefaultClassId": 1,
    "AoiRadiusX": 24.0
  }
}
```

Hai khối, và ranh giới giữa chúng là ranh giới "client có cần biết không":

| `World` — client nhận | `Server` — server giữ |
|---|---|
| mọi số `Step` đọc | `StartingMapId`, `DefaultClassId` — chỉ dùng lúc *tạo* nhân vật |
| | `AoiRadiusX` — client không tính tầm nhìn, nó chỉ nhận cái server gửi |

`AoiRadiusX` là ví dụ đáng nhớ nhất: nó là số của *thuật toán server*, không phải luật chơi. Client
không cần biết mình đang được cho xem trong bán kính bao nhiêu — và **không nên** biết, vì đó là thông
tin về cách server hoạt động.

**Cho GameServer thấy file.** Đúng khuôn `Data/Maps` của Phase 10: thêm một `ItemGroup` copy
`..\..\Config\*.json` vào `Data\Config\` trong output. Mọi thứ server đọc lúc chạy nằm dưới `Data/` cạnh
exe; nguồn của chúng ở đâu là chuyện của csproj, không phải chuyện của code.

**`ConfigService`** (file mới, `Server/GameServer/Config/ConfigService.cs`) — ba việc:

**(a) `Load()`** — đọc + parse bằng **Newtonsoft** (dự án đã chốt ở Phase 10). Khác file map một điểm
quan trọng: `MissingMemberHandling.Error`, không phải `Ignore`. File map do **tool** sinh nên không có
lỗi chính tả để bắt; `game.json` do **người gõ tay**, và ở đó gõ nhầm `Gravty` mà im lặng thì trọng lực
về mặc định còn bạn đi tìm bug trong `MovementRules`.

Hỏng gì (thiếu file, JSON sai, trường lạ) → **log Warn thật to rồi chạy bằng mặc định**, không chết. Bắt
**đúng loại lỗi dự kiến** (`IOException`, `JsonException`) chứ không `catch (Exception)` — bug trong code
vẫn phải ném lên. Đây là ranh giới giữa "xử lý có chính sách" và "nuốt lỗi" mà `CLAUDE.md` cấm.

**(b) `Validate()`** — config đến từ **con người**, nên phải kiểm. Ngoài khoảng hợp lý thì Warn + trả
riêng trường đó về mặc định, không vứt cả file:

| Trường | Khoảng hợp lệ | Vượt thì gãy ở đâu |
|---|---|---|
| `Gravity` | `> 0` | `0` = lơ lửng · âm = rơi lên trời |
| `MaxFallSpeed` | `0 < v <= CELL_SIZE / TICK_DT` (= 20) | **rơi hơn một ô mỗi tick** → phép quét chống tunneling của Phase 10 hết bảo đảm |
| `MoveSpeed` | `0 < v < CELL_SIZE / TICK_DT` | `ResolveHorizontal` chỉ kiểm **điểm cuối**; đi hơn một ô mỗi tick là xuyên tường mỏng |
| `JumpSpeed` | `0 < v < CELL_SIZE / TICK_DT` | phép kiểm trần cũng chỉ kiểm điểm cuối — bật quá nhanh là chui qua trần dày 1 ô |
| các `*Seconds` | `>= 0` | số âm quy ra tick âm, vòng đếm ngược không bao giờ kết thúc |
| `BodyHalfWidth` | `0 < w < CELL_SIZE / 2` | `>= 0.5` = không lọt nổi khe rộng 1 ô, nhân vật kẹt cứng |
| `BodyHeight` | `0 < h <= 2` | vượt 2 là **ba mức quét của `OverlapsSolid` không còn đủ** — khoảng cách giữa hai mức vượt cạnh ô, và ô ở giữa lọt qua khe kiểm |
| `BodyHeightCrouch` | `0 < h <= min(BodyHeight, CELL_SIZE)` | lớn hơn chiều cao đứng thì ngồi xuống lại cao lên; lớn hơn 1 ô thì không chui được khe ngồi |
| `AoiRadiusX` | `> 0` | 0 = không ai thấy ai |

Bốn dòng giữa đáng dừng lại: chúng **không phải** "số phải dương" mà là **ràng buộc đến từ thuật toán ở
phase khác**. `MaxFallSpeed <= 20` không phải sở thích — nó là điều kiện để phép quét va chạm của
Phase 10 còn đúng. Ghi lý do vào comment ngay tại chỗ kiểm, không thì ba tháng nữa có người nâng lên 40
để "rơi cho đã" và nhận về một bug xuyên sàn ngẫu nhiên.

> Config không phải là "chỗ để số". Nó là **bề mặt điều khiển** mà người vận hành chạm vào — và mọi giả
> định ngầm của thuật toán, nếu không được kiểm ở đây, sẽ bị phá từ đây.

**(c) `Current` + hot reload** — property trả object hiện hành. Reload là **thay nguyên object**
(`_current = mới`), không sửa từng field trên object cũ: gán reference là thao tác nguyên tử nên không
cần lock, và ai đang cầm reference cũ vẫn thấy một bộ giá trị **nhất quán**. Sửa từng field trên object
sống thì luồng tick có thể đọc được `Gravity` mới ghép với `MaxFallSpeed` cũ — một tổ hợp chưa từng tồn
tại trong bất kỳ file nào.
**Vòng đọc phím trong console — sang Bước 2.** Nó thuộc `Program.cs`, và `Program.cs` là thứ Bước 2
viết lại. Ở bước này chỉ cần biết `ConfigService.Load()` là hàm mà phím `R` sẽ gọi.

**Ai dùng config ở đâu:**

- `WorldService`: `AOI_RADIUS_X` thành giá trị đọc từ config. Chú ý `AOI_COLUMN_WIDTH` ăn theo — nó phải
  bằng đúng bán kính (xem Phase 11), nên tính từ bán kính lúc dựng chứ đừng để hai con số rời nhau.
- `MapRegistry`/`CharacterService`: `STARTING_MAP_ID`, `DEFAULT_CLASS_ID` từ config.
- **`PlayerEntity` giữ một reference `WorldConfig`, chốt MỘT lần lúc dựng** — đúng chỗ nó đã giữ
  `_config` từ Phase 9. `Integrate` dùng field đó, **không** đọc `ConfigService.Current` mỗi tick.

Điểm cuối là bài học chính của bước này:

> Bộ số là một phần của **hợp đồng phiên chơi**. Client dự đoán bằng đúng bộ nó nhận lúc vào world;
> server đổi số giữa chừng thì mọi dự đoán của người đang online lệch **ngay lập tức** → rubber-band
> hàng loạt, và họ không làm gì sai cả.
>
> Luật: **hot reload áp dụng cho người vào sau.** Người đang online giữ bộ cũ tới lần vào world kế tiếp.

Và vì đã gom thành một object, "chốt theo phiên" là giữ **một reference** thay vì chép năm field — thêm
số mới không phải nhớ chép thêm, và cũng không thể quên.

**Phía client ở bước này chỉ có một việc: thêm `WorldConfig World` vào `EnterWorldResponse`.** Chuyện
client nạp nó vào đâu và dùng thế nào là **Bước 4** — cả một bước riêng, vì nó không chỉ là "cầm object
rồi truyền đi" như bản doc cũ nói. `MapChangedNotice` thì **không** cần trường này: đổi map không đổi
luật thế giới.

Client **không đọc file config nào**, không copy `game.json` vào build — đó là toàn bộ ý của bước này.

Một chi tiết dễ chịu: `RemotePlayerView` và `CharacterStates.Derive` **không** cần `WorldConfig` — `Derive`
chỉ so sánh dấu, không dùng hằng nào. Một hàm thuần không có tham số cấu hình là một hàm không bao giờ
lệch phiên bản.

### ✅ CHECKPOINT A

Bước này **chưa chạy được đầu-cuối** — client còn chưa biết làm gì với `response.World`. Kiểm bằng log
server thôi, và đó là bình thường: Bước 4 mới nối hai đầu.

1. `dotnet build` sạch. Server boot log một dòng gọn:
   `Config: gravity=30 maxFall=20 coyote=3t jumpBuf=3t drop=6t · aoi=24 · startMap=1`.
2. Sửa `Gravity` trong `bin/Debug/net8.0/Data/Config/game.json` thành `60` → restart → log in `gravity=60`.
3. Xoá tạm `game.json` khỏi output → server vẫn boot, log Warn, chạy bằng mặc định.
4. Ghi `"Gravty": 30` (sai chính tả) → boot lên **báo lỗi trường lạ**, không im lặng. Đây là chỗ khác
   file map, và là lý do phải khác.
5. Ghi `"MaxFallSpeed": 60` → Warn về đúng trường đó, giá trị về 20.
6. `git grep "GRAVITY\|MAX_FALL_SPEED\|COYOTE_TICKS\|JUMP_BUFFER_TICKS\|DROP_THROUGH_TICKS"` trong
   `Server/Shared` → **không còn dòng nào**. Còn dòng nào là còn một nguồn số thứ hai.

<details>
<summary><b>📖 Lời giải — <code>Shared</code>: <code>WorldConfig</code> + <code>MovementRules</code></b></summary>

**`Server/Shared/World/WorldConfig.cs`** (file mới, nguyên văn):

```csharp
using MemoryPack;
using MMORPG.Shared.World.Movement;

namespace MMORPG.Shared.World
{
    /// <summary>
    /// Luật thế giới ở ĐƠN VỊ CỦA NGƯỜI VIẾT SỐ: giây, không phải tick. Vừa là bản đối chiếu 1-1 với
    /// khối "World" trong game.json, vừa là thứ đi trên dây xuống client — hai vai mà cùng một hình
    /// dạng, nên một kiểu là đủ.
    ///
    /// Mọi property có giá trị mặc định HỢP LỆ: file thiếu trường nào thì trường đó về mặc định thay
    /// vì làm cả server đứng.
    /// </summary>
    [MemoryPackable]
    public sealed partial class WorldConfig
    {
        /// <summary>Gia tốc rơi, unit/giây². Lớn hơn 9.81 rất nhiều — trọng lực "đúng vật lý" cho cảm giác lơ lửng.</summary>
        public float Gravity { get; set; } = 30f;

        /// <summary>Trần tốc độ rơi. Trần này là ĐIỀU KIỆN ĐÚNG của phép quét va chạm, không phải sở thích.</summary>
        public float MaxFallSpeed { get; set; } = 20f;

        /// <summary>Còn được nhảy bao lâu sau khi đã rời mép sàn (coyote time).</summary>
        public float CoyoteSeconds { get; set; } = 0.15f;

        /// <summary>Một cú bấm nhảy còn được giữ lại chờ tiếp đất bao lâu (jump buffer).</summary>
        public float JumpBufferSeconds { get; set; } = 0.15f;

        /// <summary>Bỏ qua va chạm với bệ một chiều bao lâu sau khi bấm ngồi + nhảy.</summary>
        public float DropThroughSeconds { get; set; } = 0.3f;
        /// <summary>Bản tick của coyote / jump buffer / drop-through — tính lại mỗi lần đọc, đủ rẻ vì mỗi tick chỉ đọc vài lần.</summary>
        /// <summary>Bản tick của ba giá trị trên — tính lại mỗi lần đọc, đủ rẻ vì mỗi tick chỉ đọc vài lần.</summary>
        public int CoyoteTicks => MovementRules.ToTicks(CoyoteSeconds);
        public int JumpBufferTicks => MovementRules.ToTicks(JumpBufferSeconds);
        public int DropThroughTicks => MovementRules.ToTicks(DropThroughSeconds);
    }
}
```

**`Server/Shared/World/Movement/MovementRules.cs`** — xoá 5 `const`, giữ `TICK_RATE`, `TICK_DT`, `EXPIRED`,
`EDGE`. Đổi chữ ký và mọi chỗ đọc hằng:

```csharp
        public static MoveState Step(MoveState state, MoveIntent intent, float dt,
            WorldConfig world, CharacterConfig config, MapGrid map)
        {
            // ...

            // 3. Trọng lực — luật của thế giới, không theo nhân vật.
            state.VelY -= world.Gravity * dt;
            if (state.VelY < -world.MaxFallSpeed)
                state.VelY = -world.MaxFallSpeed;

            // ...

            if (!locked && intent.Crouch && intent.Jump && state.Grounded &&
                StandingOnOneWay(map, config, state))
            {
                state.DropThroughTicks = world.DropThroughTicks;
                // ...
            }
            else if (!locked &&
                     state.TicksSinceJumpRequest <= world.JumpBufferTicks &&
                     state.TicksSinceGrounded <= world.CoyoteTicks)
            {
                // ...
            }
        }
```

Mọi hàm riêng trong file này (`ResolveHorizontal`, `ResolveVertical`, `BlocksFall`, `OverlapsSolid`,
`CanStandUp`, `StandingOnOneWay`, `BodyHeight`, `ClampX`, `ResolveSpawnY`) đổi tham số
`CharacterProfile profile` → `CharacterConfig config`. Trình biên dịch chỉ tận nơi từng chỗ.

</details>

<details>
<summary><b>📖 Lời giải — <code>GameServer</code>: <code>ConfigService</code> (bản Bước 1) + <code>ConfigLimits</code></b></summary>

**`Server/GameServer/GameServer.csproj`** — thêm, ngay cạnh khối copy map đã có:

```xml
    <ItemGroup>
        <Content Include="..\..\Config\*.json"
                 Link="Data\Config\%(Filename)%(Extension)"
                 CopyToOutputDirectory="PreserveNewest"/>
    </ItemGroup>
```

**`Server/GameServer/Config/GameConfigData.cs`** (file mới, nguyên văn):

```csharp
using MMORPG.Shared.World;
using Newtonsoft.Json;

namespace MMORPG.GameServer.Config
{
    /// <summary>
    /// Bản đối chiếu 1-1 với <c>Config/game.json</c> — tham số vận hành, chỉ server đọc.
    ///
    /// Nằm ở GameServer chứ không ở Shared vì khối <see cref="Server"/> chỉ server được biết: đưa
    /// nó sang Shared là đưa cả bán kính AOI vào DLL của client. Client chỉ nhận phần
    /// <see cref="World"/>, qua <c>EnterWorldResponse</c>.
    /// </summary>
    public sealed class GameConfigData : IConfigFile
    {
        /// <summary>Phiên bản schema, nằm trong file.</summary>
        public int Version { get; set; } = 1;

        /// <summary>Luật thế giới — phần duy nhất của file này client được biết.</summary>
        public WorldConfig World { get; set; } = new();

        /// <summary>Những con số chỉ server dùng.</summary>
        public ServerConfigData Server { get; set; } = new();

        /// <summary>Luôn 1: file này không có mảng nào, và mọi trường đều có mặc định hợp lệ.</summary>
        [JsonIgnore] public int RowCount => 1;
    }

    public sealed class ServerConfigData
    {
        /// <summary>Map của nhân vật mới tạo.</summary>
        public int StartingMapId { get; set; } = 1;

        /// <summary>Lớp nhân vật của nhân vật mới tạo.</summary>
        public int DefaultClassId { get; set; } = 1;

        /// <summary>
        /// Bán kính tầm nhìn theo trục X. Phải lớn hơn nửa bề RỘNG màn hình, nếu không người chơi
        /// thấy entity hiện ra giữa khung hình thay vì ở mép.
        /// </summary>
        public float AoiRadiusX { get; set; } = 24f;
    }
}
```

**`Server/GameServer/Config/ConfigLimits.cs`** (file mới, nguyên văn) — trần/sàn của các con số, tách
riêng vì chúng trả lời câu hỏi khác với `ConfigService`:

```csharp
using MMORPG.Shared.World.Map;
using MMORPG.Shared.World.Movement;

namespace MMORPG.GameServer.Config
{
    /// <summary>
    /// Trần và sàn của các con số trong file config — <b>không</b> phải giá trị mặc định, mà là ranh
    /// giới ngoài đó thì thuật toán va chạm không còn đúng.
    ///
    /// Mỗi hằng ở đây dẫn xuất từ một tính chất của <see cref="MovementRules"/> hoặc
    /// <see cref="MapGrid"/>, nên nó là hằng số của THUẬT TOÁN và ở lại trong code: người cân bằng
    /// game không có lý do nào để nới trần này, và nới nó thì nhân vật xuyên sàn.
    /// </summary>
    public static class ConfigLimits
    {
        /// <summary>
        /// Trần tuyệt đối cho mọi vận tốc. Đi quá một ô trong một tick là vượt qua giả định của phép
        /// kiểm va chạm: ngang và lên chỉ kiểm ĐIỂM CUỐI, xuống thì quét từng hàng ô nhưng chỉ bảo
        /// đảm trong phạm vi một ô mỗi tick.
        /// </summary>
        public const float SPEED_CAP = MapGrid.CELL_SIZE / MovementRules.TICK_DT;

        /// <summary>
        /// Nửa bề ngang thân phải HẸP HƠN nửa ô, không bằng: rộng đúng nửa ô là vừa khít khe 1 ô, và
        /// "vừa khít" trong số thực dấu phẩy động nghĩa là lúc lọt lúc không.
        /// </summary>
        public const float BODY_HALF_WIDTH_CAP = MapGrid.CELL_SIZE * 0.5f - 0.001f;

        /// <summary>
        /// Trần chiều cao thân khi đứng. Đến từ <c>OverlapsSolid</c>: nó quét ba mức cao, và ba mức
        /// chỉ phủ kín khi khoảng cách giữa hai mức nhỏ hơn cạnh ô. Cao hơn là có ô lọt qua khe kiểm.
        /// </summary>
        public const float BODY_HEIGHT_CAP = MapGrid.CELL_SIZE * 2f;

        /// <summary>Chiều cao khi ngồi không được vượt một ô — nếu không thì ngồi cũng không chui được vào khe cao 1 ô.</summary>
        public const float CROUCH_HEIGHT_CAP = MapGrid.CELL_SIZE;
    }
}
```

**`Server/GameServer/Config/ConfigService.cs`** (file mới) — **bản của Bước 1**, chỉ đọc `game.json`
và viết theo cách thẳng nhất. Bước 3 sẽ **tổng quát hoá chính hàm nạp này** thành `LoadFile<T>` dùng
cho mọi file; đừng cố viết sẵn bản generic ở đây, vì lúc chỉ có một file thì chưa nhìn ra cái gì
chung và cái gì riêng:

```csharp
using MMORPG.ServerCore;
using MMORPG.Shared.World;
using Newtonsoft.Json;

namespace MMORPG.GameServer.Config
{
    /// <summary>
    /// Nguồn DUY NHẤT của mọi con số vận hành. Đọc file lúc boot, đọc lại khi được yêu cầu, và không
    /// bao giờ để một file hỏng giết server.
    /// </summary>
    public sealed class ConfigService
    {
        /// <summary>Tên file loại A trong <c>Data/Config/</c>.</summary>
        private const string GAME_FILE = "game.json";

        /// <summary>
        /// <c>MissingMemberHandling.Error</c>, khác file map: mọi file trong Config/ đều do người gõ
        /// tay, nên gõ nhầm "Gravty" phải là lỗi chứ không phải một giá trị âm thầm về mặc định.
        /// </summary>
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Error,
        };

        /// <summary>
        /// Bộ số loại A hiện hành. Đọc ra biến cục bộ rồi dùng, đừng đọc nhiều lần trong một phép
        /// tính: reload thay nguyên object, nên hai lần đọc có thể rơi vào hai bộ khác nhau.
        /// </summary>
        public GameConfigData Current { get; private set; } = new();

        /// <summary>Lối tắt cho chỗ gọi hay dùng nhất. Không phải bản sao — vẫn đúng object trong <see cref="Current"/>.</summary>
        public WorldConfig World
        {
            get { return Current.World; }
        }

        /// <summary>Nạp ngay lúc dựng: không có trạng thái "đã có service nhưng chưa có số".</summary>
        public ConfigService()
        {
            Load();
        }

        /// <summary>Đọc lại TẤT CẢ file config. Gọi lúc boot và mỗi lần bấm phím reload.</summary>
        public void Load()
        {
            LoadGame();
        }

        /// <summary>
        /// Đọc bộ tham số vận hành.
        ///
        /// Thay NGUYÊN object chứ không sửa từng field: gán reference là thao tác nguyên tử nên luồng
        /// tick hoặc thấy trọn bộ cũ, hoặc trọn bộ mới. Sửa tại chỗ thì nó có thể đọc được Gravity mới
        /// ghép với MaxFallSpeed cũ — một tổ hợp chưa từng tồn tại trong file nào.
        /// </summary>
        private void LoadGame()
        {
            string path = PathOf(GAME_FILE);
            GameConfigData data;

            try
            {
                data = JsonConvert.DeserializeObject<GameConfigData>(File.ReadAllText(path), Settings)
                       ?? new GameConfigData();
            }
            // Bắt ĐÚNG hai loại lỗi dự kiến. catch (Exception) ở đây sẽ nuốt luôn NullReferenceException
            // của chính code này — và đó là thứ CLAUDE.md cấm.
            catch (Exception ex) when (ex is IOException || ex is JsonException)
            {
                Log.Warn($"Không đọc được {path.Red()}: {ex.Message}. " +
                         "Server chạy bằng GIÁ TRỊ MẶC ĐỊNH — số trong file KHÔNG có hiệu lực.");
                data = new GameConfigData();
            }

            ValidateGame(data);

            Current = data;

            Log.Info($"Config: gravity={World.Gravity} maxFall={World.MaxFallSpeed} " +
                     $"coyote={World.CoyoteTicks}t jumpBuf={World.JumpBufferTicks}t drop={World.DropThroughTicks}t · " +
                     $"aoi={data.Server.AoiRadiusX} · startMap={data.Server.StartingMapId}");
        }

        /// <summary>
        /// AppContext.BaseDirectory chứ không phải thư mục hiện hành: chỗ gõ lệnh không phải chỗ file
        /// exe nằm, và đường dẫn tương đối tới gốc repo chỉ đúng trên đúng một máy.
        /// </summary>
        private static string PathOf(string fileName)
        {
            return Path.Combine(AppContext.BaseDirectory, "Data", "Config", fileName);
        }

        /// <summary>
        /// Kẹp từng trường về khoảng dùng được. Trả riêng trường hỏng về mặc định chứ không vứt cả
        /// file: một dòng sai không nên xoá sổ ba mươi dòng đúng.
        /// </summary>
        private static void ValidateGame(GameConfigData data)
        {
            var fallback = new WorldConfig();

            data.World.Gravity = Clamp(data.World.Gravity, 0.001f, float.MaxValue, fallback.Gravity, "Gravity");
            data.World.MaxFallSpeed = Clamp(data.World.MaxFallSpeed, 0.001f, ConfigLimits.SPEED_CAP, fallback.MaxFallSpeed, "MaxFallSpeed");
            data.World.CoyoteSeconds = Clamp(data.World.CoyoteSeconds, 0f, 5f, fallback.CoyoteSeconds, "CoyoteSeconds");
            data.World.JumpBufferSeconds = Clamp(data.World.JumpBufferSeconds, 0f, 5f, fallback.JumpBufferSeconds, "JumpBufferSeconds");
            data.World.DropThroughSeconds = Clamp(data.World.DropThroughSeconds, 0f, 5f, fallback.DropThroughSeconds, "DropThroughSeconds");
            data.Server.AoiRadiusX = Clamp(data.Server.AoiRadiusX, 0.001f, float.MaxValue, 24f, "AoiRadiusX");
        }

        /// <summary>Trả giá trị nếu nó nằm trong khoảng, ngược lại LA LỚN rồi trả mặc định.</summary>
        private static float Clamp(float value, float min, float max, float fallback, string field)
        {
            if (value >= min && value <= max)
                return value;

            // LA LỚN chứ không sửa im lặng: người vận hành phải biết số họ gõ đã bị từ chối, nếu không
            // họ sẽ đi tìm lý do vì sao "sửa rồi mà không thấy khác gì".
            Log.Warn($"Config {field.Red()} = {value} ngoài khoảng [{min}, {max}] — dùng {fallback}.");

            return fallback;
        }
    }
}
```

</details>

<details>
<summary><b>📖 Lời giải — <code>GameServer</code>: ba service và <code>PlayerEntity</code> nhận config</b></summary>

**`Server/GameServer/World/MapRegistry.cs`** — `STARTING_MAP_ID` hết là hằng số. Thêm
`using MMORPG.GameServer.Config;` ở đầu file, rồi:

```csharp
        /// <summary>
        /// Map của nhân vật mới toanh. Là LUẬT CHƠI ("người mới bắt đầu ở đâu"), không phải thuộc
        /// tính của map nào — nên nó nằm ở đây chứ không nằm trong file map.
        /// </summary>
        private readonly int _startingMapId;

        public MapRegistry(ConfigService config)
        {
            _startingMapId = config.Current.Server.StartingMapId;

            // ... phần nạp file giữ nguyên, chỉ thay mọi STARTING_MAP_ID bằng _startingMapId ...
        }
```

Xoá `public const int STARTING_MAP_ID = 1;`. Trình biên dịch sẽ chỉ ra hai chỗ còn dùng nó:
`CharacterService.EnterWorldAsync` và chính `MapRegistry`. Chỗ đầu đổi thành `_maps.Starting.MapId` —
`MapRegistry` đã chốt map khởi đầu rồi thì không ai cần tra lại con số.

**`Server/GameServer/World/WorldService.cs`** — bán kính AOI hết là hằng số. Thêm cùng dòng `using`, rồi:

```csharp
        private readonly float _aoiRadiusX;
        private readonly float _aoiColumnWidth;

        private readonly ConfigService _config;

        public WorldService(MapRegistry maps, ConfigService config)
        {
            _maps = maps;
            _config = config;

            // Chốt MỘT LẦN lúc dựng, không đọc config.Current mỗi tick. Cùng lý do với WorldConfig
            // trong PlayerEntity — nhưng ở đây còn thêm một lý do nữa: đổi bán kính giữa chừng làm
            // tập Visible của mọi người lệch với tập đã gửi, và một loạt EntityDespawn giả sinh ra.
            _aoiRadiusX = config.Current.Server.AoiRadiusX;

            // Cột rộng BẰNG ĐÚNG bán kính. Tính từ bán kính chứ không cho nó một dòng
            // config riêng: hai con số rời nhau là hai con số sẽ lệch nhau.
            _aoiColumnWidth = _aoiRadiusX;
        }
```

`Spawn` truyền bộ số xuống entity:

```csharp
            var entity = new PlayerEntity(entityId, row, owner, map, _config.World);
```

`ColumnOf` đang là `static` nên nó không thấy field — đổi thành method thường. Trình biên dịch sẽ nhắc.

**`Server/GameServer/World/CharacterService.cs`** — nhận config và gắn bộ số vào response:

```csharp
        private readonly ConfigService _config;

        public CharacterService(DbClient dbClient, WorldService worldService, MapRegistry maps, ConfigService config)
        {
            _dbClient = dbClient;
            _worldService = worldService;
            _maps = maps;
            _config = config;
        }
```

```csharp
                    ClassId = _config.Current.Server.DefaultClassId,   // thay WorldService.DEFAULT_CLASS_ID
                    MapId = _maps.Starting.MapId,                      // thay MapRegistry.STARTING_MAP_ID
```

```csharp
            return new EnterWorldResponse
            {
                // ... các trường cũ ...

                // Bộ số của PHIÊN này. Client dự đoán bằng đúng bộ server đang dùng cho entity của nó.
                World = _config.World,
            };
```

**`Server/Shared/Dto/Character/CharacterDto.cs`** — `EnterWorldResponse` thêm một property (Bước 3 thêm
ba trường nữa vào đúng chỗ này):

```csharp
using MemoryPack;
using MMORPG.Shared.Net;
using MMORPG.Shared.World;

namespace MMORPG.Shared.Dto.Character
{
    /// <summary>
    /// Gói mở màn của một phiên chơi: chỉ mang những gì RIÊNG của phiên vừa mở.
    ///
    /// Nó không chở dữ liệu tĩnh nào — bảng nhân vật, bảng item, lưới map đều ship cùng bản build
    /// của client, và server đọc bản copy của mình. Ngoại lệ duy nhất là <see cref="World"/>: người
    /// vận hành chỉnh trọng lực giữa hai lần restart server mà không patch client, nên client không
    /// thể có sẵn mấy con số ấy.
    /// </summary>
    [MemoryPackable]
    public partial class EnterWorldResponse
    {
        /// <summary>Sai thì mọi trường dưới đây vô nghĩa, chỉ đọc <see cref="Error"/>.</summary>
        public bool Success { get; set; }

        /// <summary>Lý do từ chối. <c>None</c> khi thành công.</summary>
        public ErrorCode Error { get; set; }

        /// <summary>Id runtime trong world. Chỉ có nghĩa tới khi rời world.</summary>
        public int EntityId { get; set; }

        /// <summary>Id trong DB — sống qua mọi lần vào ra world.</summary>
        public long CharacterId { get; set; }

        /// <summary>Tên nhân vật.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Lớp nhân vật, khoá tra vào bảng nhân vật.</summary>
        public int ClassId { get; set; }

        /// <summary>Cấp hiện tại.</summary>
        public int Level { get; set; }

        /// <summary>Map đang đứng, khoá tra vào sổ map.</summary>
        public int MapId { get; set; }

        /// <summary>Toạ độ world lúc vào, trục ngang.</summary>
        public float X { get; set; }

        /// <summary>Toạ độ world lúc vào, trục dọc.</summary>
        public float Y { get; set; }

        /// <summary>Mốc thời gian server (Unix ms) tại thời điểm vào world.</summary>
        public long ServerTimeMs { get; set; }

        /// <summary>Luật thế giới đang chạy trên server. Null khi <see cref="Success"/> sai.</summary>
        public WorldConfig World { get; set; }
    }
}
```

**`Server/GameServer/World/PlayerEntity.cs`** — giữ `WorldConfig` cạnh `_config`, cùng một lý do:

```csharp
        /// <summary>
        /// Luật thế giới của PHIÊN này, chốt lúc dựng entity. Cố tình KHÔNG đọc ConfigService.Current
        /// mỗi tick: client bên kia đang dự đoán bằng đúng bộ số nó nhận lúc vào world, nên đổi số
        /// giữa chừng là rubber-band hàng loạt cho những người không làm gì sai cả.
        ///
        /// Hot reload áp dụng cho người vào SAU.
        /// </summary>
        private readonly WorldConfig _world;

        public PlayerEntity(int entityId, CharacterRow row, ClientSession owner, MapGrid map, WorldConfig world)
        {
            // ... các dòng cũ giữ nguyên ...

            _config = CharacterConfigContainer.Get(row.ClassId);
            _map = map;
            _world = world;

            State = ResolveSpawn(map, row.X, row.Y, warnIfStuck: true);
        }
```

```csharp
            State = MovementRules.Step(State, intent, dt, _world, _config, _map);
```

</details>

---

## Bước 2 — Sổ service phía server

### Hướng làm

Bước này **không thêm tính năng nào**. Nó dọn một chỗ mà nếu để nguyên thì Bước 3 sẽ làm nó tệ hơn.

Mở `Program.cs` sau Bước 1 ra đọc. Nó đang làm bốn việc khác nhau trong cùng một file:

```csharp
await using var dbClient = new DbClient("127.0.0.1", dbPort);   // 1. dựng service
dbClient.Start();

SystemHandler.DbClient = dbClient;                               // 2. phát service cho handler
AuthHandler.AuthService = new AuthService(dbClient, new LoginRateLimiter());

var config = new ConfigService();
var maps = new MapRegistry(config);
var worldService = new WorldService(maps, config);
CharacterHandler.CharacterService = new CharacterService(dbClient, worldService, maps, config);

TcpDispatcher.RegisterAll();
var listener = new TcpListener(IPAddress.Any, port);             // 3. vòng đời process
// ... vòng phím, game loop, vòng accept ...                     // 4. vòng lặp
```

Ba vấn đề, xếp theo mức nghiêm trọng:

**(1) Mỗi handler tự mọc một `public static XService XService { get; set; }`, và `Program.cs` gán tay
từng dòng.** Quên một dòng thì **không có lỗi biên dịch** — chỉ có `NullReferenceException` ở gói tin
đầu tiên chạm tới nó, có thể là nhiều phút sau khi server đã lên và bạn đã quên mình vừa thêm gì.

**(2) Handler phải gọi sang handler khác để lấy service.** `AuthHandler.OnLogout` đang viết
`CharacterHandler.CharacterService.LeaveWorldAsync(...)` — nó hỏi một **handler** để lấy một **service**.
Dispatch table vì thế có một đồ thị phụ thuộc thứ hai mà không ai vẽ ra, và nó chỉ lộ ra khi bạn xoá
một handler và một handler khác vỡ.

**(3) Không có đường nào để service A gọi service B mà A không được dựng sau B trong `Program.cs`.**
Hôm nay còn đọc được vì có bốn service. Tới Phase 15 thì `CombatService` cần `InventoryService` (drop đồ)
cần `CharacterService` (cộng exp) cần `WorldService`, và thứ tự các dòng trong `Program.cs` trở thành
một thứ phải giải bằng tay mỗi lần thêm service.

**Cách chữa: một sổ tra service + một composition root.** Đối ứng khái niệm với `GameLifetimeScope` bên
client, nhưng **tự viết** chứ không kéo container vào:

```
ServerServices      sổ tra:  Register<T>(service) · Get<T>() · TryGet<T>(out) · Seal() · ShutdownAsync()
ServerBootstrap     chỗ DUY NHẤT biết server có những gì và ai cần ai
Program.cs          chỉ còn vòng đời process
```

> **Đây là mẫu SERVICE LOCATOR, và nó có cái giá thật:** nhìn chữ ký hàm không biết nó cần gì, phải đọc
> thân hàm. Constructor injection không có nhược điểm đó, và đó là lý do nó vẫn là mặc định.
>
> Nhưng handler của server là hàm **`static`** — `TcpDispatcher` gọi chúng qua reflection, không có chỗ
> nào nhét constructor vào. Nên luật là: **chấp nhận service locator ở ĐÚNG một biên giới** — giữa
> dispatch table static và các service instance. Mọi service khác vẫn nhận phụ thuộc qua **constructor**
> và **không** gọi `ServerServices.Get<T>()` trong thân hàm của mình.
>
> Vi phạm luật đó là ném đi thứ duy nhất constructor injection cho ta: nhìn chữ ký là biết class này
> cần gì.

Bốn chi tiết của sổ, mỗi cái chặn một lỗi cụ thể:

| Chi tiết | Chặn chuyện gì |
|---|---|
| `Register` ném khi trùng kiểu | hai chỗ cùng dựng một service — cái nào thắng là chuyện của thứ tự dòng, tức không ai kiểm được |
| `Seal()` sau khi đăng ký hết | `Dictionary` static bị ghi trong lúc nhiều luồng đang đọc. Đăng ký hết lúc boot rồi đóng sổ thì từ đó **chỉ còn đọc**, và đọc song song không cần khoá gì |
| `Get<T>()` **ném** thay vì trả null | thiếu service là server đang chạy dở dang. Ném ở gói tin đầu tiên vẫn tốt hơn một `NullReferenceException` ở tầng sâu hơn năm phút sau |
| `ShutdownAsync()` dispose **ngược** thứ tự đăng ký | A đăng ký sau B thường là A dùng B, nên dọn A trước là dọn đúng chiều phụ thuộc. Nó cũng thay cho `await using var dbClient` — nay sổ giữ service thì sổ giữ luôn trách nhiệm đóng |

**Trong handler thì dùng `property` chứ không `field`:**

```csharp
private static AuthService AuthService => ServerServices.Get<AuthService>();   // ✅
private static readonly AuthService AuthService = ServerServices.Get<...>();   // ❌ null vĩnh viễn
```

Field static khởi tạo lúc **class được nạp lần đầu**, và với handler thì thời điểm đó có thể là lúc
`TcpDispatcher.RegisterAll()` quét assembly — **trước** khi có service nào trong sổ. Property thì tra
lúc **dùng**. Một phép tra Dictionary mỗi gói tin là vài nanosecond, không phải thứ cần tối ưu.

**Vòng đọc phím dựng lại ở đây.** `Program.cs` hiện **không có** vòng nào: đoạn đọc phím của Phase 9 đã
bị comment lại, và bị comment là đúng, vì nó nằm **bên trong vòng accept**:

```csharp
while (!cts.IsCancellationRequested)
{
    switch (Console.ReadKey(intercept: true).Key) { ... }   // ← chặn ở đây
    TcpClient tcpClient = await listener.AcceptTcpClientAsync(ct);
}
```

`Console.ReadKey` chặn **luồng**, nên không ai vào được game cho tới khi bạn gõ một phím. Cách đúng: một
**luồng riêng** chỉ làm việc đọc phím — `new Thread(...) { IsBackground = true }`, không phải `Task.Run`,
vì đó là blocking I/O chứ không phải việc CPU. Dựng lại vòng này cũng làm sống lại
`WorldService.EnqueueForceAll` / `EnqueueReviveAll`, hiện đang là code chết từ hồi vòng phím bị bỏ đi.

Và một dòng nữa mà chỉ phát hiện khi thử thật: **kiểm `Console.IsInputRedirected` trước khi vào vòng
phím.** Chạy server bằng `MMORPG.GameServer.exe < nul`, qua dịch vụ Windows, hay trong Docker thì
`ReadKey` ném `InvalidOperationException`. Nó ở luồng riêng nên **không ai bắt**, và .NET kết luận
process phải chết — server tắt ngóm vì một phím tiện lợi lúc dev.

### ✅ CHECKPOINT B

1. `dotnet build` sạch. Server boot in thêm một dòng: `Đã đăng ký 6 service.`
2. Chạy `MMORPG.GameServer.exe < nul` (hoặc `dotnet run < /dev/null`) → server **vẫn lên**, log
   `stdin bị chuyển hướng — tắt phím điều khiển`, và **không chết**.
3. Chạy bình thường: gõ `R` → log in lại dòng `Config: ...`. Gõ `H` / `K` / `J` → người chơi trong world
   nhận `hurt` / `die` / hồi sinh.
4. Xoá tạm một dòng `ServerServices.Register` trong `ServerBootstrap` (ví dụ `CharacterService`), build,
   vào game → server ném `InvalidOperationException: Chưa đăng ký CharacterService. Thêm một dòng
   ServerServices.Register vào ServerBootstrap.Build().` **Thông điệp phải nói đúng chỗ cần sửa** — đó
   là toàn bộ lý do `Get<T>()` ném chứ không trả null. Trả lại dòng đã xoá.
5. Thêm tạm một dòng `ServerServices.Register(new ConfigService());` **sau** `Seal()` → ném
   `Đăng ký ConfigService sau khi đã Seal()`. Xoá dòng thử.
6. `git grep "public static.*Service { get; set; }" Server/GameServer` → **không còn dòng nào**.
7. Ctrl+C → log `Đã dừng.` và không có exception nào. Kết nối DBServer đóng sạch (DBServer không log
   lỗi socket).

<details>
<summary><b>📖 Lời giải — <code>ServerServices</code></b></summary>

**`Server/GameServer/Boot/ServerServices.cs`** (file mới, nguyên văn):

```csharp
using MMORPG.ServerCore;

namespace MMORPG.GameServer.Boot
{
    /// <summary>
    /// Sổ tra service của GameServer — đối ứng của <c>GameLifetimeScope</c> bên client. Static vì
    /// handler của server là hàm <c>static</c> (xem <c>TcpDispatcher</c>), nên không có chỗ nào nhét
    /// constructor injection vào.
    ///
    /// Đây là mẫu SERVICE LOCATOR với cái giá thật của nó: nhìn chữ ký hàm không biết nó cần gì. Chấp
    /// nhận giá đó ở ĐÚNG một chỗ — biên giới giữa dispatch table static và các service instance;
    /// mọi service khác nhận phụ thuộc qua constructor. Đăng ký hết trong <see cref="ServerBootstrap"/>
    /// lúc boot, gọi <see cref="Seal"/>, rồi từ đó chỉ còn đọc — đó là thứ giữ cho sổ này an toàn
    /// khi nhiều luồng đọc song song.
    /// </summary>
    public static class ServerServices
    {
        /// <summary>Sổ tra chính: một kiểu, một instance.</summary>
        private static readonly Dictionary<Type, object> _byType = new();

        /// <summary>Thứ tự đăng ký, để lúc tắt thì dispose ngược lại — A đăng ký sau B thường là A dùng B.</summary>
        private static readonly List<object> _order = new();

        /// <summary>Đã đóng sổ chưa. Đăng ký sau khi đóng là ném.</summary>
        private static bool _sealed;

        /// <summary>Thêm một service vào sổ và trả lại chính nó, để chỗ gọi xâu chuỗi được.</summary>
        public static T Register<T>(T service) where T : class
        {
            if (_sealed)
                throw new InvalidOperationException($"Đăng ký {typeof(T).Name} sau khi đã Seal(). Mọi service phải dựng xong trong ServerBootstrap.");

            if (service == null)
                throw new ArgumentNullException(nameof(service));

            // Đăng ký hai lần cùng một kiểu là dấu hiệu của hai composition root, hoặc một dòng copy
            // sót. Cái nào thắng là chuyện của thứ tự dòng, tức là không ai kiểm được — ném ngay.
            if (!_byType.TryAdd(typeof(T), service))
                throw new InvalidOperationException($"{typeof(T).Name} đã được đăng ký rồi.");

            _order.Add(service);

            return service;
        }

        /// <summary>
        /// Service đã đăng ký. Ném khi không có — và ném là hành vi đúng: thiếu một service nghĩa là
        /// server đang chạy dở dang, và biết ngay lúc gói tin đầu tiên chạm tới nó vẫn tốt hơn một
        /// NullReferenceException ở tầng sâu hơn năm phút sau.
        /// </summary>
        public static T Get<T>() where T : class
        {
            if (_byType.TryGetValue(typeof(T), out object service))
                return (T)service;

            throw new InvalidOperationException($"Chưa đăng ký {typeof(T).Name}. Thêm một dòng ServerServices.Register vào ServerBootstrap.Build().");
        }

        /// <summary>Như <see cref="Get{T}"/> nhưng không ném — dành cho đường dọn dẹp, nơi service có thể chưa kịp dựng.</summary>
        public static bool TryGet<T>(out T service) where T : class
        {
            if (_byType.TryGetValue(typeof(T), out object found))
            {
                service = (T)found;
                return true;
            }

            service = null;
            return false;
        }

        /// <summary>Đóng sổ. Từ đây chỉ còn đọc, nên nhiều luồng đọc song song không cần khoá gì.</summary>
        public static void Seal()
        {
            _sealed = true;
            Log.Info($"Đã đăng ký {_byType.Count.ToString().Green()} service.");
        }

        /// <summary>
        /// Dọn theo chiều ngược thứ tự đăng ký. Bọc từng cái trong try riêng: một service ném lúc
        /// đóng không được phép chặn những cái sau nó — lúc tắt thì dọn được bao nhiêu tốt bấy nhiêu.
        /// </summary>
        public static async ValueTask ShutdownAsync()
        {
            for (int i = _order.Count - 1; i >= 0; i--)
            {
                object service = _order[i];

                try
                {
                    switch (service)
                    {
                        case IAsyncDisposable asyncDisposable:
                            await asyncDisposable.DisposeAsync();
                            break;

                        case IDisposable disposable:
                            disposable.Dispose();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Lỗi khi đóng {service.GetType().Name}");
                }
            }

            _order.Clear();
            _byType.Clear();
            _sealed = false;
        }
    }
}
```

</details>

<details>
<summary><b>📖 Lời giải — <code>ServerBootstrap</code> và <code>Program.cs</code></b></summary>

**`Server/GameServer/Boot/ServerBootstrap.cs`** (file mới, nguyên văn):

```csharp
using MMORPG.GameServer.Auth;
using MMORPG.GameServer.Config;
using MMORPG.GameServer.Db;
using MMORPG.GameServer.Net;
using MMORPG.GameServer.World;

namespace MMORPG.GameServer.Boot
{
    /// <summary>
    /// Chỗ duy nhất biết server có những gì và ai cần ai — đối ứng của
    /// <c>GameLifetimeScope.Configure</c> bên client. Tách khỏi Program.cs vì Program.cs lo vòng đời
    /// của process (mở cổng, bắt Ctrl+C, vòng accept), còn danh sách service là chuyện khác hẳn.
    ///
    /// <b>Thứ tự các dòng ở đây là thứ tự phụ thuộc, không phải sở thích.</b> Đặt sai thì một service
    /// nhận vào null và chết ở lần dùng đầu tiên: không có container nào tự giải phụ thuộc hộ. Đổi
    /// lại, đọc từ trên xuống là biết hết.
    /// </summary>
    public static class ServerBootstrap
    {
        /// <summary>
        /// Dựng và đăng ký mọi service, rồi đóng sổ. Gọi đúng một lần, trước khi mở cổng lắng nghe.
        /// </summary>
        public static void Build(string dbHost, int dbPort)
        {
            // Hạ tầng DB trước: mọi thứ nghiệp vụ đều hỏi nó.
            var dbClient = ServerServices.Register(new DbClient(dbHost, dbPort));
            dbClient.Start();

            // Config đọc TRƯỚC mọi thứ thuộc về world: MapRegistry và WorldService đều cần số từ nó.
            var config = ServerServices.Register(new ConfigService());

            var maps = ServerServices.Register(new MapRegistry(config));
            var worldService = ServerServices.Register(new WorldService(maps, config));

            ServerServices.Register(new AuthService(dbClient, new LoginRateLimiter()));
            ServerServices.Register(new CharacterService(dbClient, worldService, maps, config));

            // GameLoop không phải service ai đó gọi tới, nhưng vẫn đăng ký: Program.cs cần nó, và
            // "mọi thứ sống lâu bằng process đều nằm trong một sổ" là luật dễ theo hơn "trừ cái này".
            ServerServices.Register(new GameLoop(worldService));

            ServerServices.Seal();

            // Quét assembly tìm [TcpHandler] — sau Seal() vì handler chạm tới ServerServices ngay
            // khi gói tin đầu tiên tới, và tới lúc đó sổ phải đã đóng.
            TcpDispatcher.RegisterAll();
        }
    }
}
```

`LoginRateLimiter` **không** vào sổ: chỉ `AuthService` dùng nó, và nó không có vòng đời riêng. Sổ dành
cho thứ **nhiều nơi cần** hoặc **cần được dọn lúc tắt** — nhét mọi object vào sổ là quay về đúng cái
"mọi thứ nhìn thấy mọi thứ" mà sổ sinh ra để tránh.

**`Server/GameServer/Program.cs`** (thay nguyên file):

```csharp
using System.Net;
using System.Net.Sockets;
using System.Text;
using MMORPG.GameServer;
using MMORPG.GameServer.Boot;
using MMORPG.GameServer.Config;
using MMORPG.GameServer.World;
using MMORPG.ServerCore;
using MMORPG.Shared.World.Character;

Console.OutputEncoding = Encoding.UTF8;

// 7777 nằm trong dải cổng Windows đã dành riêng cho Hyper-V/WSL trên máy này
// (`netsh int ipv4 show excludedportrange protocol=tcp`) — bind vào đó là SocketException 10013.
const int port = 7778;
const int dbPort = 7779;

// Toàn bộ danh sách service nằm trong ServerBootstrap. File này chỉ còn lo VÒNG ĐỜI CỦA PROCESS:
// mở cổng, nhận kết nối, nghe phím, tắt sạch.
try
{
    ServerBootstrap.Build("127.0.0.1", dbPort);
}
catch (Exception ex)
{
    // Biên của process là chỗ duy nhất được bắt Exception trần: ở đây không còn ai phía trên để xử lý
    // tiếp. Và nguyên nhân hay gặp nhất — một file config hoặc file map gõ sai — cần hiện ở dòng đầu
    // kèm tên file, chứ không nằm sau mười dòng stack của Newtonsoft.
    Log.Error(ex, "Không boot được, server dừng.");

    // Dọn những service đã kịp dựng trước khi hỏng: DbClient đang giữ một kết nối mở tới DBServer.
    await ServerServices.ShutdownAsync();

    return 1;
}

var config = ServerServices.Get<ConfigService>();
var worldService = ServerServices.Get<WorldService>();
var gameLoop = ServerServices.Get<GameLoop>();

var listener = new TcpListener(IPAddress.Any, port);
listener.Start();
Log.Info($"Lắng nghe trên {$"0.0.0.0:{port}".Green()}");

// Ctrl+C để dừng sạch thay vì kill process
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // chặn hành vi kill mặc định
    cts.Cancel();
};

//----------------------------------------------------------------------------------------------------

#region === Console Key ===

// ĐẶT KHỐI NÀY TRƯỚC vòng `while (!cts.IsCancellationRequested) { ... AcceptTcpClientAsync ... }`.
//
// Đây là chỗ dễ sai nhất của cả bước, và sai thì KHÔNG có lỗi biên dịch: đặt nó sau vòng accept thì
// luồng phím chỉ khởi động lúc server đang tắt, tức là phím R không bao giờ có tác dụng — mà triệu
// chứng lại giống hệt "hot reload chưa chạy".
//
// Vòng đọc phím nằm ở LUỒNG RIÊNG, không trộn vào vòng accept. Console.ReadKey chặn cả luồng, nên
// đặt chung là không ai vào được game cho tới khi bạn gõ một phím.
//
// Thread chứ không Task.Run: đây là blocking I/O, không phải việc CPU. Nhét nó vào thread pool là
// chiếm một worker suốt đời process. IsBackground = true để nó không giữ process sống lúc thoát.
var console = new Thread(() =>
{
    // Không có bàn phím thì không có gì để đọc: chạy qua dịch vụ Windows, qua Docker, hay đơn giản
    // là `MMORPG.GameServer.exe < nul`. ReadKey trong hoàn cảnh đó ném InvalidOperationException, và
    // vì nó ở luồng riêng nên exception ấy KHÔNG ai bắt — .NET kết luận process phải chết. Server
    // tắt ngóm vì một phím tiện lợi lúc dev là cái giá không đáng trả.
    if (Console.IsInputRedirected)
    {
        Log.Info("stdin bị chuyển hướng — tắt phím điều khiển (R/H/K/J).");
        return;
    }

    while (!cts.IsCancellationRequested)
    {
        switch (Console.ReadKey(intercept: true).Key)
        {
            case ConsoleKey.R:
                config.Load();
                break;

            // Ba phím thử, chạy cùng vòng lặp này.
            case ConsoleKey.H:
                worldService.EnqueueForceAll(ActionState.Hurt);
                break;

            case ConsoleKey.K:
                worldService.EnqueueForceAll(ActionState.Die);
                break;

            case ConsoleKey.J:
                worldService.EnqueueReviveAll();
                break;
        }
    }
})
{
    IsBackground = true,
};

console.Start();

#endregion

_ = gameLoop.RunAsync(cts.Token);

try
{
    while (!cts.IsCancellationRequested)
    {
        TcpClient tcpClient = await listener.AcceptTcpClientAsync(cts.Token);

        // Mỗi kết nối chạy độc lập. KHÔNG await ở đây — await là chỉ phục vụ được 1 client.
        var session = new ClientSession(tcpClient);
        _ = session.RunAsync(cts.Token);
    }
}
catch (OperationCanceledException)
{
    // dừng theo yêu cầu, không phải lỗi
}
finally
{
    listener.Stop();

    // Đóng mọi service theo chiều ngược thứ tự đăng ký. Thay cho `await using var dbClient` trước
    // đây: nay sổ giữ service thì sổ cũng giữ trách nhiệm đóng chúng.
    await ServerServices.ShutdownAsync();

    Log.Info("Đã dừng.");
}

// 0 = dừng bình thường. Mã thoát phải có ở MỌI đường ra vì nhánh boot hỏng trả 1.
return 0;
```

</details>

<details>
<summary><b>📖 Lời giải — ba handler và <code>ClientSession</code> lấy service từ sổ</b></summary>

**`Server/GameServer/Handlers/AuthHandler.cs`** (thay nguyên file):

```csharp
using MMORPG.GameServer.Auth;
using MMORPG.GameServer.Boot;
using MMORPG.GameServer.Net;
using MMORPG.GameServer.World;
using MMORPG.Shared.Dto.Auth;
using MMORPG.Shared.Net;

namespace MMORPG.GameServer.Handlers
{
    /// <summary>
    /// Ba dòng mỗi handler. Nếu có ngày một hàm ở đây dài quá 10 dòng thì nghiệp vụ đang rò rỉ
    /// ra khỏi <see cref="AuthService"/> — kéo nó về.
    /// </summary>
    public static class AuthHandler
    {
        // Handler là hàm static nên không có constructor để nhận inject — lấy service từ sổ chung.
        // Property chứ không field: tra lúc DÙNG, không lúc class được nạp. Field static khởi tạo
        // sớm hơn ServerBootstrap.Build() một nhịp và sẽ giữ null vĩnh viễn.
        private static AuthService AuthService => ServerServices.Get<AuthService>();

        // MinState = Verified: chưa kiểm phiên bản thì chưa được gõ cửa nào khác. Phép chặn nằm ở
        // dispatcher, không nằm ở thiện chí của client.
        [TcpHandler(NetCmd.Register, MinState = SessionState.Verified)]
        public static async Task<NetResult> OnRegister(NetRequest req)
        {
            if (req.Session.State >= SessionState.Authenticated)
                return NetResult.Ok(new AuthResponse { Success = false, Error = ErrorCode.AlreadyAuthenticated });

            return NetResult.Ok(await AuthService.RegisterAsync(req.Session, req.GetData<RegisterRequest>()));
        }

        [TcpHandler(NetCmd.Login, MinState = SessionState.Verified)]
        public static async Task<NetResult> OnLogin(NetRequest req)
        {
            if (req.Session.State >= SessionState.Authenticated)
                return NetResult.Ok(new AuthResponse { Success = false, Error = ErrorCode.AlreadyAuthenticated });

            return NetResult.Ok(await AuthService.LoginAsync(req.Session, req.GetData<LoginRequest>()));
        }

        [TcpHandler(NetCmd.Logout, MinState = SessionState.Authenticated)]
        public static async Task<NetResult> OnLogout(NetRequest req)
        {
            // Logout khi đang trong world: rời world trước, cùng một đường dọn dẹp với mất kết nối.
            // Hỏi sổ chung chứ không gọi sang CharacterHandler: handler nói chuyện với SERVICE, không
            // nói chuyện với handler khác — nếu không thì dispatch table có thêm một đồ thị phụ thuộc
            // thứ hai mà không ai vẽ ra.
            await ServerServices.Get<CharacterService>().LeaveWorldAsync(req.Session);

            return NetResult.Ok(AuthService.Logout(req.Session));
        }
    }
}
```

**`Server/GameServer/Handlers/CharacterHandler.cs`** (thay nguyên file):

```csharp
using MMORPG.GameServer.Boot;
using MMORPG.GameServer.Net;
using MMORPG.GameServer.World;
using MMORPG.Shared.Net;

namespace MMORPG.GameServer.Handlers
{
    public static class CharacterHandler
    {
        private static CharacterService CharacterService => ServerServices.Get<CharacterService>();

        [TcpHandler(NetCmd.EnterWorld, MinState = SessionState.Authenticated)]
        public static async Task<NetResult> OnEnterWorld(NetRequest req)
        {
            return NetResult.Ok(await CharacterService.EnterWorldAsync(req.Session));
        }
    }
}
```

**`Server/GameServer/Handlers/SystemHandler.cs`** — thêm `using MMORPG.GameServer.Boot;` rồi thay hai
dòng đầu class:

```csharp
        // Xem ghi chú ở AuthHandler: property tra lúc DÙNG, không phải field khởi tạo lúc nạp class.
        private static DbClient DbClient => ServerServices.Get<DbClient>();
```

Phần còn lại của file không đổi một dòng.

**`Server/GameServer/ClientSession.cs`** — đổi `using MMORPG.GameServer.Handlers;` thành
`using MMORPG.GameServer.Boot;`, rồi trong khối `finally` của `RunAsync`:

```csharp
                // Mất kết nối đột ngột cũng phải đi qua đúng đường dọn dẹp như logout chủ động.
                //
                // TryGet chứ không Get: một kết nối có thể đứt trong lúc server đang tắt, và lúc đó
                // ShutdownAsync đã dọn sổ service rồi. Get sẽ ném giữa khối finally — che mất lỗi
                // thật đã làm vòng đọc chết.
                if (ServerServices.TryGet(out CharacterService characterService))
                    await characterService.LeaveWorldAsync(this);
```

</details>

---

## Bước 3 — Loại B ở server: một hàm nạp cho MỌI bảng

### Hướng làm

Loại B khác loại A ở một điểm duy nhất nhưng điểm đó đổi mọi thứ: **client thật sự cần toàn bộ dữ liệu**,
không phải vài con số. Cách của loại A ("chỉ server đọc, đẩy vài giá trị xuống") không áp dụng được vì
"vài giá trị" ở đây là cả bảng.

Câu hỏi thật là: client lấy cả bảng ấy **bằng đường nào**?

| | **Server gửi cả bảng trong `EnterWorld`** | **Mỗi bên đọc file của CHÍNH NÓ** |
|---|---|---|
| Lệch được không | không | **có** ⇒ phải nhìn thấy được khi nghi ngờ |
| Gói login | phình theo số bảng | không đổi một byte |
| Client trước khi vào world | **không biết gì** về item, lớp nhân vật | biết hết, từ lúc khởi động |
| Bảng chỉ MỘT bên cần | vẫn phải gửi | bên kia không cần biết nó tồn tại |
| Đường lên CDN (Phase 18) | phải đổi kiến trúc | chỉ đổi chỗ chứa file |
| Ai làm thế | (không ai) | **WoW, Lineage, mọi MMO mobile** |

**Chọn cột phải.** Cách thứ nhất nghe an toàn hơn — không có bản thứ hai thì không lệch được — nhưng
nó trả cái an toàn ấy bằng bốn dòng giữa, và cả bốn đều đắt dần theo thời gian.

Dòng dễ bỏ qua nhất là dòng thứ tư, và về lâu dài nó là dòng nặng nhất: **bảng của hai bên không bao
giờ là cùng một tập.** Một feature cỡ Bát Quái bên vo-lam-genz có cả chục bảng — phần lớn chỉ server
đọc để tính, client chỉ cần hai ba bảng để vẽ lên màn hình. Server cũng sẽ có bảng tỉ lệ rơi đồ và
bảng AI quái mà client không được phép biết, còn client sẽ có bảng thoại và mô tả kỹ năng mà server
chẳng dùng vào việc gì.

> **Hệ quả: đừng gửi danh sách vân tay xuống cho client tự so.** Đó là thiết kế đầu tiên của doc
> này và nó **sai** — nó ngầm khẳng định hai bên nạp cùng một tập bảng, tức là ép client ship đúng
> những bảng server nạp, và chặn luôn đường tải dần từ CDN ở Phase 18 (vào world với ba bảng, mở
> túi đồ mới tải bảng thứ tư). Mỗi bên tự quyết định nạp gì.

> **Bệnh của vo-lam-genz không phải là có hai bản.** Đọc lại `ROADMAP.md §2b`: bệnh là **không ai
> kiểm hai bản có khớp nhau không**. Copy thiếu một lần thì client hiển thị item A trong khi server
> xử lý item B — không lỗi biên dịch, không log, chỉ có bug câm.
>
> Cách chữa ở phase này có ba lớp: **một nguồn duy nhất trên đĩa** + **build tool copy** + **mỗi bên
> in dấu vân tay ra log**. Hai lớp đầu làm cho việc lệch gần như không xảy ra được; lớp thứ ba làm
> cho nó *nhìn thấy được trong ba mươi giây* khi nó vẫn xảy ra. Cách chữa triệt để — một số phiên
> bản cho cả gói dữ liệu, kiểm một lần lúc đăng nhập — là bài của Phase 18, cùng lúc với trình patch.

Và đó là điểm đáng chú ý nhất của bước này: **bản đồ đã làm đúng rồi**. Việc còn lại chỉ là áp cùng
khuôn ấy cho bảng nhân vật, thay vì phát minh một đường thứ hai.

| Bảng | Hôm nay | Việc của phase này |
|---|---|---|
| Bản đồ | client đọc `Resources/Maps/`, server đọc bản copy `Data/Maps/` | **không thêm gì** — đã đúng khuôn từ Phase 10 |
| Bảng nhân vật | C# cắm cứng trong `CharacterConfigContainer.Build()`, đi theo DLL | ra file `Resources/Config/`, csproj copy sang server |

**Trường `Version` thì có từ ngày đầu.** Nó nằm **trong file** nên thêm sau khi đã có người chơi là một
cuộc di cư — khác hẳn dấu vân tay, thứ **tính ra từ** file nên thêm lúc nào cũng được. Nhớ sự bất đối
xứng này: nó là lý do `IConfigFile` có `Version` mà không có `Checksum()`.

**3a — Một hàm băm dùng chung, và xoá hàm băm viết tay đang có.** `MapGrid.Checksum()` đang có sẵn
FNV-1a nhưng hàm `Mix` nằm `private` trong đó, và bản thân nó là **hàm băm viết tay**: liệt kê từng
trường bằng tay, `MapId`, `OriginX`, `OriginY`, `Width`, `Height`, rồi từng ô. Đừng nhân bản kiểu ấy
cho bảng thứ hai — hai chục dòng mỗi bảng mới, cộng một chế độ hỏng câm (thêm trường mà quên thêm
vào hàm băm thì vân tay không còn phát hiện được thay đổi ở trường đó). Làm hai việc:

1. Tách phần băm ra `Server/Shared/World/Fnv1a.cs`: `START`, `Mix(hash, ReadOnlySpan<byte>)`,
   `Mix(hash, int)`, `Mix(hash, string)`. Ba cái sau `Contract.Hash` ở Bước 5 sẽ dùng lại.
2. **Xoá hẳn `MapGrid.Checksum()`** và hàm `Mix` private của nó. Map đi cùng luật với mọi config
   khác: không có hàm băm riêng. Bài test round-trip của Phase 10 đang gọi nó — sửa bài test để so
   **từng ô một**, vì một bài test đỏ phải chỉ ra ô NÀO lệch chứ không chỉ nói "hai số khác nhau".

Thay cho tất cả: `ConfigFingerprint.Of(table)` băm **byte đã tuần tự hoá** — MemoryPack ghi đúng
những trường đi trên dây, theo đúng thứ tự khai báo, nên không có cửa quên. Và **không** băm text
thô của file: `core.autocrlf` làm cùng một nội dung ra CRLF trên Windows và LF trên Linux, hai bên
sẽ in ra hai số khác nhau vì một thứ không phải dữ liệu.

**3b — Bộ ba `*Config` / `*TableData` / `*ConfigContainer`.** Đây là quy ước của cả dự án từ đây trở đi
(`CONVENTIONS.md` §2), không phải sáng tạo riêng cho bảng nhân vật:

```
Server/Shared/World/Character/
├── CharacterConfig.cs             một DÒNG: [MemoryPackable], property có setter
├── CharacterTableData.cs          cả FILE: Version + CharacterConfig[] + RowCount, : IConfigFile
└── CharacterConfigContainer.cs    bảng TRA lúc chạy: static, Load / Get / Count
```

Ba kiểu chứ không hai, và mỗi cái tồn tại vì một lý do khác nhau:

| Kiểu | Tồn tại vì | Nếu bỏ đi |
|---|---|---|
| `*Config` | là thứ code gọi cầm trên tay (`config.MoveSpeed`) | phải truyền cả bảng + id đi khắp nơi |
| `*TableData` | là hình dạng của FILE, và là thứ `ConfigFingerprint` băm | không có gì để parse JSON vào, và không có gì để băm |
| `*ConfigContainer` | là chỗ tra `id → config` mà **không cần inject** | mọi hàm dùng bảng phải nhận thêm một tham số |

Và **không** đẻ thêm `CharacterConfigData` để tách "kiểu file" khỏi "kiểu chạy": xem lại tiêu chí ở Bước 1
— phép chuyển đổi ở đây chỉ là giây→tick trên ba con số, tức một `Prepare()`, không phải một class nữa.

Hai thay đổi trên kiểu đã có:

- `CharacterConfig` đổi từ *bất biến, dựng bằng hàm khởi tạo* sang *`[MemoryPackable]`, property có
  setter*. Mất tính bất biến — và bù lại bằng kỷ luật "chỉ `CharacterConfigContainer.Load` được đụng vào".
- `ActionDefinition` (readonly struct, quy đổi trong hàm dựng) **biến mất**, nhập vào `ActionData`. Vẫn
  là struct, vẫn quy đổi một lần — chỉ là phép quy đổi dời từ hàm dựng sang `Prepare()`, vì một kiểu
  tuần tự hoá được thì không tự gọi hàm dựng của bạn.

**`Assets/Game/Resources/Config/characters.json`** — chú ý chỗ đặt: **trong `Resources` của client**,
không phải gốc repo. Client cần đọc nó lúc chạy; server nhận bản copy do csproj chép sang, đúng chiều
và đúng khuôn với file map ở Phase 10.

```json
{
  "Version": 1,
  "Classes": [
    {
      "ClassId": 1,
      "Name": "Dragon Warrior",
      "MoveSpeed": 5.0,
      "JumpSpeed": 16.0,
      "BodyHalfWidth": 0.35,
      "BodyHeight": 1.6,
      "BodyHeightCrouch": 0.9,
      "Actions": [
        { "Action": "Attack", "DurationSeconds": 0.25, "CooldownSeconds": 0.4, "LocksMovement": false },
        { "Action": "Hurt",   "DurationSeconds": 0.2,  "CooldownSeconds": 0.0, "LocksMovement": true },
        { "Action": "Die",    "DurationSeconds": 1.0,  "CooldownSeconds": 0.0, "LocksMovement": true }
      ]
    }
  ]
}
```

> `game.json` thì **ở lại gốc repo**, và đó là cố ý: nó chứa `AoiRadiusX` và `StartingMapId` — số của
> *thuật toán server*, không phải luật chơi. Ship nó trong bản build của client là cho người chơi đọc
> cách server hoạt động. Đây là lúc ranh giới loại A / loại B có hệ quả thật, chứ không còn là cái nhãn.

`"Action": "Attack"` là **tên enum**, không phải số — Newtonsoft tự chuyển sang `ActionState`, và người
sửa file không phải tra bảng để biết `2` nghĩa là gì. Một id lạ thì Newtonsoft ném, và đó là hành vi
đúng cho file người gõ tay.

`Actions` là **mảng**, không phải object có khoá cố định: thêm một hành động mới ở Phase 15 chỉ là thêm
một phần tử, không đụng schema.

**`CharacterConfigContainer` đổi từ bảng tĩnh cắm cứng thành bảng tĩnh được NẠP.** Giữ nguyên
`Get(classId)` — mọi chỗ gọi ở client (`WorldSpawner`, `PlayerMotor`, `RemotePlayerView`) không phải sửa
chữ ký. Thêm `Load(CharacterTableData)` để thay bảng, và bỏ hàm `Build()` cắm cứng.

Ai gọi `Load`: **mỗi bên gọi sau khi đọc file của mình** — server lúc boot, client lúc dựng container.
Một bảng, hai đường vào, một hàm dựng.

Container **không** giữ vân tay và **không** có `Snapshot()`. Vân tay là chuyện của thứ đã nạp bảng
(`ConfigService`), không phải của bảng tra cứu; và không ai cần lấy lại cả bảng vì không ai gửi nó đi.

> Đây là chỗ duy nhất trong phase có state toàn cục thay đổi được, nên phải nói rõ luật của nó: bảng
> chỉ được thay **giữa hai phiên chơi**, không phải giữa chừng. Cùng đúng cái lý do đã chốt
> `WorldConfig` vào `PlayerEntity` — mà lần này còn mạnh hơn, vì `PlayerEntity._config` đang giữ
> reference tới object cũ, nên người đang online **tự động** giữ bộ cũ ngay cả khi bảng bị thay.

**3c — Và đây là phần quan trọng nhất của bước: một hàm nạp, không phải một hàm cho mỗi bảng.**

Bản doc cũ hướng dẫn viết `LoadCharacters()`. Nghe hợp lý cho đến khi có bảng thứ hai, và bảng thứ hai
**tới ngay Phase 13**. Lúc đó `LoadItems()` là chép lại toàn bộ `LoadCharacters()` rồi sửa bốn cái tên:

```csharp
private void LoadCharacters()   //  ← 20 dòng
private void LoadItems()        //  ← 20 dòng gần như y hệt
private void LoadMonsters()     //  ← Phase 15, lại 20 dòng
```

Hỏi câu của `FEATURE-TEMPLATE.md`: *thêm cái thứ hai cùng loại tốn bao nhiêu dòng?* Đáp án "chép lại cả
hàm" nghĩa là **thiết kế sai**, và sai theo kiểu tệ nhất: ba bản sao của cùng một logic xử lý lỗi, nên
ngày sửa cách log lỗi thì sửa được hai chỗ và quên chỗ thứ ba.

Nhìn lại `LoadCharacters()` — và cả `LoadGame()` bạn vừa viết ở Bước 1 — xem cái gì **thật sự** khác
nhau giữa các file. Chỉ có bốn thứ:

| Khác nhau giữa các file | Giống nhau ở MỌI file |
|---|---|
| tên file | đọc file, parse Newtonsoft, bắt `IOException`/`JsonException` |
| hàm `Validate` nào | phát hiện file parse được nhưng RỖNG |
| đổ vào đâu (`apply`) | thứ tự: parse → kiểm rỗng → validate → apply |
| hỏng thì làm gì | bắt `InvalidOperationException` của container (hai dòng trùng id) |

Bốn thứ khác nhau → bốn tham số. Phần giống nhau → thân hàm, viết **một lần**:

```csharp
private TFile LoadFile<TFile>(string name, Action<TFile> validate, Action<TFile> apply, WhenBroken whenBroken)
    where TFile : class, IConfigFile, new()
```

Tham số thứ tư là thứ dễ bỏ sót nhất, và nó chính **là** khác biệt loại A / loại B:

| | `game.json` (loại A) | bảng dữ liệu (loại B) |
|---|---|---|
| Hỏng thì | `WhenBroken.UseDefaults` | `WhenBroken.KeepCurrent` |
| Vì sao | mọi trường đều có mặc định hợp lệ, server vẫn chạy được | "bảng mặc định" là bảng rỗng, tức server sống mà không ai chơi được |
| Lúc boot | về mặc định, server lên bình thường | **chết ngay**, kèm tên file |
| Lúc bấm `R` | về mặc định | giữ nguyên bản đang chạy, người đang online không thấy gì |

Hai dòng cuối là lý do `KeepCurrent` còn phải đọc một cờ `_booted`: lần nạp ĐẦU chưa có "bản đang
chạy" nào để giữ, nên giữ nguyên ở đó là giữ một bảng **rỗng** — và lỗi sẽ chỉ lộ ra ở gói tin đầu
tiên tra tới bảng, cách chỗ gây ra nó hàng phút. Chết ngay tại dòng đọc file thì thông điệp nói đúng
tên file.

Tức là loại A và loại B **không** cần hai hàm nạp. Một hàm, cộng một tham số enum có tên.

Trên `LoadFile` đặt thêm một lớp mỏng dành riêng cho loại B — nó chỉ thêm đúng một dòng log:

```csharp
private void LoadTable<TTable>(string name, Action<TTable> validate, Action<TTable> load)
    where TTable : class, IConfigFile, IMemoryPackable<TTable>, new()
```

Vì sao tách ra thay vì nhét luôn vân tay vào `LoadFile`: ràng buộc `IMemoryPackable` chỉ đúng với
file nào **cần** vân tay. Bắt `GameConfigData` — một file chỉ server đọc, không có bản thứ hai nào
để đối chiếu — mang thêm `[MemoryPackable]` là thêm máy móc cho một dòng log không ai đọc.

Và `Load()` trở thành một danh sách đọc được:

```csharp
public void Load()
{
    LoadFile<GameConfigData>(GAME, ValidateGame, ApplyGame, WhenBroken.UseDefaults);

    LoadTable<CharacterTableData>(ConfigFiles.CHARACTERS, ValidateCharacters, CharacterConfigContainer.Load);
    // Phase 13 thêm đúng MỘT dòng ở đây cho bảng item.
}
```

Ràng buộc `where TFile : IConfigFile` là thứ làm hàm generic này viết được: nó cần hai thông tin từ
file (`Version`, `RowCount`) mà không được biết file đó chứa gì. Interface dừng ở hai thành viên ấy —
thêm gì nữa là ép mọi file tương lai phải có thứ mà chỉ một file dùng.

> `GameConfigData` không phải bảng, nên `RowCount` của nó trả hằng `1`. Đó không phải mẹo lách: con
> số ấy chỉ dùng để phát hiện "file parse được nhưng rỗng", mà một `game.json` parse được thì không
> bao giờ rỗng — nó không có mảng nào, và mọi trường đều có mặc định hợp lệ. Trả `1` là nói đúng
> điều đó.

**Phép kiểm miền giá trị vẫn là một hàm cho mỗi file, và điều đó đúng:** nó là phần **duy nhất** thật
sự khác nhau về nội dung. `ValidateCharacters` kẹp năm con số; `ValidateItems` ở Phase 13 chỉ kẹp
`MaxStack`. Generic hoá phần này là generic hoá thứ không có gì chung.

**Chỗ kiểm phải nằm ở server, không ở `Load` của container.** Client cũng gọi `Load`, và client kẹp
số của chính nó là client âm thầm sửa dữ liệu: một file có `MoveSpeed = 999` sẽ thành 5 ở **cả hai**
bên, và cái file hỏng ấy không bao giờ bị ai phát hiện. Server kẹp rồi băm, client băm bản thô — hai
dòng log vân tay lệch nhau, và người ta đi sửa file.

**3d — Bản đồ: không phải làm gì thêm, chỉ bớt đi.** Phase 10 đã dựng đúng khuôn rồi: một nguồn trên
đĩa, csproj copy sang server, mỗi bên đọc bản của mình. Việc duy nhất của bước này là **xoá**
`MapGrid.Checksum()` (mục 3a), để map thôi là thứ duy nhất trong dự án còn hàm băm viết tay.

> *Client đọc file map trong `Resources` của chính nó, server đọc bản copy trong `Data/Maps` — hai
> bản ấy cùng sinh từ một file trong repo, làm sao lệch được?* Lệch được, bằng đúng con đường đã cắn
> bạn ở ba phase trước: bạn export lại map trong Unity nhưng **chưa build lại GameServer** (bản copy
> trong `bin/` chỉ cập nhật lúc build). Server vẫn chạy map cũ, client đã vẽ map mới, và triệu chứng
> là "tự nhiên có bức tường vô hình ở chỗ này".
>
> Thứ cứu bạn ở đây là **dòng log tên + kích thước + origin** mà cả hai bên đều in lúc nạp map — đặt
> hai dòng cạnh nhau là thấy. Đừng nâng nó thành một phép so tự động trên dây: map thứ hai mươi sẽ
> được tải lười, và lúc đó "so hết mọi map lúc vào world" không còn nghĩa gì nữa.

### ✅ CHECKPOINT C

Vẫn kiểm bằng log server — client nối hai đầu ở Bước 4.

1. Server boot in thêm: `Bảng characters: 1 dòng, version 1, vân tay XXXXXXXX`.
2. `ls Server/GameServer/bin/Debug/net8.0/Data/Config/` → thấy **cả** `game.json` (từ gốc repo) **và**
   `characters.json` (từ `Assets/Game/Resources/Config/`). Hai nguồn, một thư mục đích.
3. Sửa `MoveSpeed` thành `12` trong `Assets/Game/Resources/Config/characters.json` → build lại → vân
   tay **đổi**.
4. Xoá `"Classes"` khỏi file → **server chết ngay lúc boot**, exit code 1, dòng cuối là
   `ERROR [Program] Không boot được, server dừng.` kèm đường dẫn file. Đó là luật boot/reload: lần nạp
   đầu chưa có bảng nào để giữ, nên "giữ nguyên" ở đây là giữ một bảng RỖNG rồi để lỗi nổ ở gói tin
   đầu tiên tra tới nó — xa chỗ gây ra hàng phút.
5. Đổi `"Classes"` thành `"Clases"` → cũng chết lúc boot, thông điệp nói đúng tên trường lạ
   (`MissingMemberHandling.Error`).
6. Thêm một lớp thứ hai **trùng `ClassId = 1`** → cũng chết lúc boot,
   `Hai lớp cùng ClassId 1: "..." và "..."`. Lỗi này đến từ `CharacterConfigContainer.Load` chứ không
   từ Newtonsoft, và nó rơi vào cùng nhánh vì `apply()` nằm TRONG `try`.
7. **Nửa còn lại của luật:** để file đúng, chạy server, rồi sửa `"Classes"` thành `"Clases"` trong bản
   `bin/.../Data/Config/` và bấm `R` → `ERROR [ConfigService] ... GIỮ NGUYÊN bản đang chạy`, server
   **sống**, người đang online không thấy gì khác. Sửa lại rồi bấm `R` → nạp được, vân tay in lại.
8. Gõ sai một trường trong `game.json` rồi boot → server **vẫn lên**, chỉ có
   `WARN ... Chạy bằng GIÁ TRỊ MẶC ĐỊNH`. Loại A không bao giờ chặn boot, vì mọi trường của nó đều có
   mặc định dùng được.
9. Ghi `"BodyHalfWidth": 0.9` → Warn về đúng trường đó, giá trị về `0.35`.
10. Đổi tên thư mục `Assets/Game/Resources/Config` → `dotnet build` **báo lỗi build** có thông điệp rõ
    ràng, không phải im lặng copy 0 file. Trả lại tên.
11. `dotnet test Server/Shared.Tests` → **toàn bộ xanh**. Hai thứ được kiểm ở đây: bài test round-trip
    của Phase 10 vẫn xanh sau khi `MapGrid.Checksum()` biến mất — nó so **từng ô** thay vì so một con
    số — và `ConfigFingerprintTests`: vân tay ổn định, nhạy với đổi giá trị, nhạy với đảo thứ tự dòng.

<details>
<summary><b>📖 Lời giải — <code>Fnv1a</code>, <code>IConfigFile</code>, <code>ConfigFingerprint</code>, <code>ConfigFiles</code></b></summary>

**`Server/Shared/World/Fnv1a.cs`** (file mới, nguyên văn):

```csharp
using System;

namespace MMORPG.Shared.World
{
    /// <summary>
    /// Băm FNV-1a 32 bit — dấu vân tay cho mọi bảng dữ liệu của dự án.
    ///
    /// Không phải hàm băm mật mã, và không cần là: câu hỏi nó trả lời là "hai bên có đang cầm cùng một
    /// bảng không", không phải "có ai cố tình làm giả bảng không". Đổi lại nó ngắn, không cấp phát, và
    /// cho cùng kết quả trên CoreCLR lẫn Mono/IL2CPP.
    /// </summary>
    public static class Fnv1a
    {
        public const uint START = 2166136261u;

        private const uint PRIME = 16777619u;

        /// <summary>
        /// Nuốt cả một dãy byte. Đây là lối vào mà <see cref="ConfigFingerprint"/> dùng — băm thẳng
        /// byte đã tuần tự hoá thay vì liệt kê từng trường bằng tay.
        /// </summary>
        public static uint Mix(uint hash, ReadOnlySpan<byte> bytes)
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash *= PRIME;
            }

            return hash;
        }

        /// <summary>Nuốt từng byte một. Cộng thẳng cả int vào thì hai bảng hoán vị vài ô vẫn ra cùng số.</summary>
        public static uint Mix(uint hash, int value)
        {
            for (int shift = 0; shift < 32; shift += 8)
            {
                hash ^= (uint)((value >> shift) & 0xFF);
                hash *= PRIME;
            }

            return hash;
        }

        public static uint Mix(uint hash, string value)
        {
            if (value == null)
                return Mix(hash, -1);

            // Băm cả độ dài: nếu không thì {"ab","c"} và {"a","bc"} nối lại giống hệt nhau.
            hash = Mix(hash, value.Length);

            for (int i = 0; i < value.Length; i++)
                hash = Mix(hash, value[i]);

            return hash;
        }
    }
}
```

**`Server/Shared/World/IConfigFile.cs`** (file mới, nguyên văn) — thứ làm cho `LoadFile<T>` viết được:

```csharp
namespace MMORPG.Shared.World
{
    /// <summary>
    /// Hình dạng chung của mọi file config, đủ để một hàm nạp duy nhất làm việc được với tất cả:
    /// bảng nhân vật, bảng item, và bộ tham số vận hành của server.
    ///
    /// Interface dừng ở hai thành viên này — thêm nữa là ép mọi file tương lai mang thứ mà chỉ một
    /// file dùng. Dấu vân tay không nằm ở đây vì nó TÍNH RA từ file: xem <see cref="ConfigFingerprint"/>.
    /// </summary>
    public interface IConfigFile
    {
        /// <summary>Phiên bản schema của file. Có từ ngày đầu — thêm sau khi đã có người chơi là một cuộc di cư.</summary>
        int Version { get; }

        /// <summary>
        /// Số mục dữ liệu trong file: hàm nạp dùng nó để phát hiện file parse được nhưng rỗng, và để
        /// in log. File không phải bảng thì trả hằng 1 — nó không rỗng được.
        /// </summary>
        int RowCount { get; }
    }
}
```

**`Server/Shared/World/ConfigFingerprint.cs`** (file mới, nguyên văn) — một hàm băm thay cho
một `Checksum()` viết tay trong từng bảng:

```csharp
using MemoryPack;

namespace MMORPG.Shared.World
{
    /// <summary>
    /// Dấu vân tay của một file config, băm từ byte đã tuần tự hoá. Một hàm cho mọi file.
    ///
    /// Mỗi bên in vân tay của file mình vừa nạp ra log; đặt hai dòng cạnh nhau là biết hai bên có
    /// đang cầm cùng một bộ dữ liệu không. Không có phép so tự động nào trên dây — mỗi bên tự quyết
    /// định nạp file nào, nên hai danh sách không bao giờ bằng nhau.
    /// </summary>
    public static class ConfigFingerprint
    {
        /// <summary>
        /// Băm byte chứ không băm text thô của file: <c>core.autocrlf</c> làm cùng một nội dung ra
        /// CRLF trên Windows và LF trên Linux, còn byte tuần tự hoá thì không có ký tự xuống dòng.
        /// Hai bản build của Shared sinh cùng dãy byte — cả giao thức đã dựa vào điều đó từ khung
        /// gói tin đầu tiên.
        ///
        /// Gọi SAU khi nạp xong (sau <c>Prepare()</c>, sau khi server kẹp miền giá trị), và cả hai
        /// bên gọi ở cùng chỗ ấy; lệch thời điểm là hai số lệch nhau vì một lý do không liên quan
        /// tới nội dung file.
        /// </summary>
        /// <remarks>
        /// Ràng buộc <c>IMemoryPackable</c> giống <see cref="Net.NetPayload.Serialize{T}"/>: nó bắt
        /// lúc biên dịch việc quên <c>[MemoryPackable]</c> trên một bảng mới. File chỉ đi qua
        /// Newtonsoft — file map — thì không in vân tay, và cũng không có hàm băm riêng.
        /// </remarks>
        public static uint Of<TFile>(TFile file)
            where TFile : class, IConfigFile, IMemoryPackable<TFile>
        {
            byte[] bytes = MemoryPackSerializer.Serialize(file);

            return Fnv1a.Mix(Fnv1a.START, bytes);
        }
    }
}
```

**`Server/Shared/World/ConfigFiles.cs`** (file mới, nguyên văn):

```csharp
namespace MMORPG.Shared.World
{
    /// <summary>
    /// Tên những file config mà cả hai bên cùng đọc — client từ <c>Assets/Game/Resources/Config/</c>,
    /// server từ bản copy trong <c>Data/Config/</c>. Chung một hằng để hai bên không lệch chuỗi sau
    /// lần đổi tên file đầu tiên.
    ///
    /// File chỉ một bên đọc thì hằng tên của nó nằm private ở bên đó — <c>game.json</c> là ví dụ.
    /// Không có đuôi <c>.json</c>: server tự thêm, còn <c>Resources.Load</c> luôn bỏ phần đuôi.
    /// </summary>
    public static class ConfigFiles
    {
        public const string CHARACTERS = "characters";

        public const string ITEMS = "items";
    }
}
```

**`Server/Shared/World/Map/MapGrid.cs`** — **xoá** hai thứ: hàm `Checksum()` và hàm `Mix` private
ngay dưới `FindDefaultSpawn`. Không thêm gì vào file này.

```csharp
        // XOÁ cả khối này:
        public uint Checksum()
        {
            uint hash = 2166136261u;

            hash = Mix(hash, MapId);
            // ... và mọi dòng Mix còn lại

            return hash;
        }

        // XOÁ luôn hàm Mix private ở cuối class — Fnv1a.Mix thay nó, và không ai gọi bản này nữa.
        private static uint Mix(uint hash, int value)
        {
            // ...
        }
```

> Bản trước của doc này bảo giữ `Checksum()` viết tay cho map, lý do: `MapGrid` không
> `[MemoryPackable]` nên không có byte tuần tự hoá để băm. Lý do ấy đúng về kỹ thuật nhưng trả lời
> sai câu hỏi — câu hỏi là *có cần một con số để so tự động không*, và câu trả lời là không (xem
> mục 3d). Map nạp lười ở Phase 15 thì phép so "mọi map lúc vào world" cũng hết nghĩa.
>
> Còn `MapRegistry` và `MapService` thì vẫn in một dòng log mỗi khi nạp map — tên, kích thước,
> origin. Đó là thứ đặt cạnh nhau để đối chiếu, và nó không tốn một hàm băm nào.

**`Server/Shared.Tests/MapGridParserTests.cs`** — hai bài test đang gọi `Checksum()`. Thay bằng một
hàm so hình dạng dùng chung, đặt ngay dưới `BuildSample`:

```csharp
        /// <summary>
        /// Hai lưới có cùng hình dạng không: mọi con số của map, cộng từng ô một. So từng ô để bài
        /// test đỏ chỉ ra được Ô NÀO lệch.
        /// </summary>
        private static void AssertSameShape(MapGrid expected, MapGrid actual)
        {
            Assert.Equal(expected.MapId, actual.MapId);
            Assert.Equal(expected.OriginX, actual.OriginX);
            Assert.Equal(expected.OriginY, actual.OriginY);
            Assert.Equal(expected.Width, actual.Width);
            Assert.Equal(expected.Height, actual.Height);
            Assert.Equal(expected.PrefabKey, actual.PrefabKey);
            Assert.Equal(expected.DefaultSpawn.X, actual.DefaultSpawn.X);

            for (int cy = expected.OriginY; cy < expected.OriginY + expected.Height; cy++)
            {
                for (int cx = expected.OriginX; cx < expected.OriginX + expected.Width; cx++)
                    Assert.Equal(expected.At(cx, cy), actual.At(cx, cy));
            }
        }
```

Rồi trong `Write_then_parse_gives_back_the_same_grid` thay cả khối `Assert.Equal` dài bằng
`AssertSameShape(original, parsed);`, và trong `Parse_ignores_fields_it_does_not_know` thay
`Assert.Equal(BuildSample().Checksum(), parsed.Checksum());` bằng
`AssertSameShape(BuildSample(), parsed);`.

**`Server/Shared.Tests/ConfigFingerprintTests.cs`** (file mới, nguyên văn) — sáu bài, và bài thứ
năm là bài đã bắt được một lỗi thật (xem câu (4) ở Bước 4):

```csharp
using MMORPG.Shared.World;
using MMORPG.Shared.World.Character;
using MMORPG.Shared.World.Item;

namespace MMORPG.Shared.Tests
{
    /// <summary>
    /// Vân tay bảng config là thứ DUY NHẤT phát hiện hai bên chạy hai bộ dữ liệu khác nhau — nên nó
    /// phải nhạy đúng chỗ và trơ đúng chỗ. Ba tính chất dưới đây là ba cách nó hỏng câm được.
    /// </summary>
    public class ConfigFingerprintTests
    {
        private static ItemTableData SampleItems()
        {
            return new ItemTableData
            {
                Version = 1,
                Items = new[]
                {
                    new ItemConfig { TemplateId = 1, Name = "Bình máu nhỏ", Kind = ItemKind.Consumable, MaxStack = 20 },
                    new ItemConfig { TemplateId = 100, Name = "Kiếm gỗ", Kind = ItemKind.Equipment, MaxStack = 1 },
                },
            };
        }

        [Fact]
        public void Of_SameContent_SameFingerprint()
        {
            // Nếu tính chất này hỏng thì mọi lần vào world đều báo lệch, kể cả khi hai bên khớp.
            Assert.Equal(ConfigFingerprint.Of(SampleItems()), ConfigFingerprint.Of(SampleItems()));
        }

        [Fact]
        public void Of_ChangedValue_DifferentFingerprint()
        {
            ItemTableData changed = SampleItems();
            changed.Items[0].MaxStack = 99;

            Assert.NotEqual(ConfigFingerprint.Of(SampleItems()), ConfigFingerprint.Of(changed));
        }

        [Fact]
        public void Of_ChangedVersion_DifferentFingerprint()
        {
            ItemTableData changed = SampleItems();
            changed.Version = 2;

            Assert.NotEqual(ConfigFingerprint.Of(SampleItems()), ConfigFingerprint.Of(changed));
        }

        [Fact]
        public void Of_ReorderedRows_DifferentFingerprint()
        {
            // Đảo thứ tự dòng là đảo thứ tự byte. Băm cộng dồn từng trường rời thì hai bảng hoán vị
            // ra cùng số — đó là lý do Fnv1a nuốt từng byte thay vì cộng thẳng giá trị vào.
            ItemTableData reordered = SampleItems();
            (reordered.Items[0], reordered.Items[1]) = (reordered.Items[1], reordered.Items[0]);

            Assert.NotEqual(ConfigFingerprint.Of(SampleItems()), ConfigFingerprint.Of(reordered));
        }

        /// <summary>
        /// Ghim một hành vi của MemoryPack mà đoán sai thì hỏng câm: <b>struct chỉ chứa kiểu
        /// unmanaged bị chép nguyên khối, mọi thuộc tính trên từng trường đều vô hiệu.</b>
        ///
        /// Vì thế <c>ActionData.DurationTicks</c> — thứ <c>Prepare()</c> tính ra — vẫn nằm trong byte
        /// tuần tự hoá, và hai bên bắt buộc phải băm ở CÙNG một thời điểm: cả hai đều băm sau
        /// <c>load()</c>.
        /// </summary>
        [Fact]
        public void Of_BlittableStruct_IncludesDerivedFields()
        {
            CharacterTableData table = SampleCharacters();
            uint beforePrepare = ConfigFingerprint.Of(table);

            foreach (CharacterConfig config in table.Classes)
                config.Prepare();

            Assert.True(table.Classes[0].Actions[0].DurationTicks > 0,
                "Prepare() phải quy ra tick, nếu không phép kiểm này vô nghĩa");

            Assert.NotEqual(beforePrepare, ConfigFingerprint.Of(table));
        }

        [Fact]
        public void Of_AfterPrepare_IsStable()
        {
            // Thứ hai bên thật sự so với nhau: vân tay của bảng ĐÃ nạp xong. Nó phải ổn định.
            CharacterTableData first = SampleCharacters();
            CharacterTableData second = SampleCharacters();

            foreach (CharacterConfig config in first.Classes)
                config.Prepare();

            foreach (CharacterConfig config in second.Classes)
                config.Prepare();

            Assert.Equal(ConfigFingerprint.Of(first), ConfigFingerprint.Of(second));
        }

        private static CharacterTableData SampleCharacters()
        {
            return new CharacterTableData
            {
                Version = 1,
                Classes = new[]
                {
                    new CharacterConfig
                    {
                        ClassId = 1,
                        Name = "Dragon Warrior",
                        Actions = new[]
                        {
                            new ActionData { Action = ActionState.Attack, DurationSeconds = 0.25f, CooldownSeconds = 0.4f },
                        },
                    },
                },
            };
        }
    }
}
```
</details>

<details>
<summary><b>📖 Lời giải — <code>CharacterTableData</code>, <code>CharacterConfig</code>, <code>CharacterConfigContainer</code></b></summary>

**`Server/Shared/World/Character/CharacterTableData.cs`** (file mới, nguyên văn):

```csharp
using System;
using MemoryPack;
using MMORPG.Shared.World.Movement;
using Newtonsoft.Json;

namespace MMORPG.Shared.World.Character
{
    /// <summary>
    /// Các con số của MỘT hành động, đọc từ file và đi được trên dây.
    ///
    /// Là struct có chủ đích: <c>default</c> của nó là "0 tick, không khoá thân", nên
    /// <see cref="CharacterConfig.GetAction"/> trả về được một giá trị hợp lệ cho hành động không có
    /// trong bảng mà chỗ gọi không phải kiểm null.
    ///
    /// Toàn kiểu unmanaged nên MemoryPack chép nguyên khối: MỌI trường đều đi trên dây và vào vân
    /// tay, kể cả hai trường tick do <see cref="Prepare"/> điền.
    /// </summary>
    [MemoryPackable]
    public partial struct ActionData
    {
        /// <summary>Ghi bằng TÊN enum trong file ("Attack"), không phải số — người sửa file không phải tra bảng.</summary>
        public ActionState Action;

        /// <summary>Thời lượng hành động, bằng đơn vị người thiết kế dùng.</summary>
        public float DurationSeconds;

        /// <summary>Thời gian hồi trước khi dùng lại được.</summary>
        public float CooldownSeconds;

        /// <summary>Trong lúc hành động diễn ra thì thân thể có mất quyền điều khiển không.</summary>
        public bool LocksMovement;

        /// <summary>Bản tick của <see cref="DurationSeconds"/> — thứ mô phỏng thật sự đọc.</summary>
        public int DurationTicks;

        /// <summary>Bản tick của <see cref="CooldownSeconds"/>.</summary>
        public int CooldownTicks;

        /// <summary>Quy giây ra tick. Gọi một lần lúc nạp bảng, không gọi trong vòng tick.</summary>
        public void Prepare()
        {
            DurationTicks = MovementRules.ToTicks(DurationSeconds);
            CooldownTicks = MovementRules.ToTicks(CooldownSeconds);
        }
    }

    /// <summary>
    /// Cả bảng nhân vật, bản đối chiếu 1-1 với <c>characters.json</c>. Không đi trên dây: mỗi bên
    /// đọc file của chính nó, và vân tay chỉ để in ra log (<see cref="ConfigFingerprint"/>).
    /// </summary>
    [MemoryPackable]
    public sealed partial class CharacterTableData : IConfigFile
    {
        /// <summary>Phiên bản schema, nằm trong file.</summary>
        public int Version { get; set; } = 1;

        /// <summary>Mỗi phần tử là một lớp nhân vật.</summary>
        public CharacterConfig[] Classes { get; set; } = Array.Empty<CharacterConfig>();

        /// <summary>Đếm ra từ <see cref="Classes"/>, nên không tuần tự hoá ở cả hai bộ.</summary>
        [MemoryPackIgnore] [JsonIgnore] public int RowCount => Classes.Length;
    }
}
```

**`Server/Shared/World/Character/CharacterConfig.cs`** (thay nguyên file — `ActionDefinition` biến mất):

```csharp
using System;
using MemoryPack;

namespace MMORPG.Shared.World.Character
{
    /// <summary>
    /// Bộ số của một lớp nhân vật: vừa là một dòng trong <c>characters.json</c>, vừa là bộ số
    /// <c>MovementRules.Step</c> đọc mỗi tick.
    ///
    /// Property có setter là cái giá của việc tuần tự hoá được — kiểu bất biến thì không bộ tuần tự
    /// hoá nào dựng được nó. Bù lại bằng kỷ luật, không bằng trình biên dịch: CHỈ
    /// <see cref="CharacterConfigContainer.Load"/> được ghi vào, và nó chỉ ghi vào object vừa dựng xong,
    /// chưa ai cầm.
    /// </summary>
    [MemoryPackable]
    public sealed partial class CharacterConfig
    {
        /// <summary>Khoá của bảng. Nhân vật trong DB trỏ về một lớp bằng số này.</summary>
        public int ClassId { get; set; }

        /// <summary>Tên để đọc log và sửa file cho dễ. Mô phỏng không dùng tới.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Tốc độ chạy ngang, world unit/giây.</summary>
        public float MoveSpeed { get; set; } = 5f;

        /// <summary>Vận tốc bật lên tức thời khi nhảy.</summary>
        public float JumpSpeed { get; set; } = 16f;

        /// <summary>Nửa bề ngang thân. Hẹp hơn nửa ô để lọt vừa khe rộng đúng 1 ô.</summary>
        public float BodyHalfWidth { get; set; } = 0.35f;

        /// <summary>Chiều cao thân khi đứng. Gốc toạ độ ở CHÂN nên thân chiếm [Y, Y + cao].</summary>
        public float BodyHeight { get; set; } = 1.6f;

        /// <summary>Chiều cao khi ngồi — thấp hơn 1 ô nên chui được vào khe cao đúng một ô.</summary>
        public float BodyHeightCrouch { get; set; } = 0.9f;

        /// <summary>Mảng chứ không phải object có khoá cố định: thêm một hành động mới là thêm phần tử.</summary>
        public ActionData[] Actions { get; set; } = Array.Empty<ActionData>();

        /// <summary>Quy giây ra tick cho mọi hành động. Gọi từ <see cref="CharacterConfigContainer.Load"/>, tức đúng một chỗ ở mỗi bên.</summary>
        public void Prepare()
        {
            // for chứ không foreach: ActionData là struct, nên foreach cho ra BẢN COPY và Prepare()
            // ghi vào bản copy ấy rồi vứt đi. Không lỗi, không cảnh báo, chỉ là mọi thời lượng bằng 0.
            for (int i = 0; i < Actions.Length; i++)
                Actions[i].Prepare();
        }

        /// <summary>
        /// Số liệu của một hành động. Hành động không có trong bảng (kể cả None) trả về bản rỗng:
        /// 0 tick, không khoá thân — nhờ vậy chỗ gọi không phải kiểm null hay kiểm None.
        ///
        /// Quét thẳng thay vì Dictionary: bảng có ba dòng, và ba phép so bằng trên một mảng liền kề
        /// nhanh hơn một phép băm. Đổi khi nào một lớp nhân vật có vài chục chiêu.
        /// </summary>
        public ActionData GetAction(ActionState action)
        {
            for (int i = 0; i < Actions.Length; i++)
            {
                if (Actions[i].Action == action)
                    return Actions[i];
            }

            return default;
        }
    }
}
```

**`Server/Shared/World/Character/CharacterConfigContainer.cs`** (thay nguyên file):

```csharp
using System;
using System.Collections.Generic;

namespace MMORPG.Shared.World.Character
{
    /// <summary>
    /// Bảng tra config theo lớp nhân vật: tĩnh nhưng NẠP ĐƯỢC, và mỗi bên nạp từ file của chính mình
    /// — server đọc <c>Data/Config/characters.json</c>, client đọc
    /// <c>Resources/Config/characters.json</c>, hai file sinh từ một nguồn trong repo.
    ///
    /// Chỉ được thay bảng GIỮA HAI PHIÊN chơi. Entity đang sống giữ reference tới CharacterConfig cũ
    /// (<c>PlayerEntity._config</c>), nên thay bảng giữa chừng không kéo người đang online sang bộ
    /// mới — và đó là hành vi đúng, không phải thiếu sót.
    /// </summary>
    public static class CharacterConfigContainer
    {
        /// <summary>Lớp mặc định, chỗ lùi về khi một ClassId lạ lọt vào từ DB.</summary>
        public const int DRAGON_WARRIOR = 1;

        /// <summary>Bảng đang chạy. Chỉ <see cref="Load"/> được gán vào nó.</summary>
        private static Dictionary<int, CharacterConfig> _byClassId = new();

        /// <summary>Số lớp trong bảng đang chạy.</summary>
        public static int Count => _byClassId.Count;

        /// <summary>Config của một lớp. ClassId lạ thì lùi về <see cref="DRAGON_WARRIOR"/>.</summary>
        public static CharacterConfig Get(int classId)
        {
            if (_byClassId.TryGetValue(classId, out var config))
                return config;

            // Bảng rỗng (chưa nạp) thì đây là chỗ duy nhất phát hiện ra, và nó phải ném chứ không trả
            // null: null đi tiếp vài tầng rồi mới nổ ở MovementRules.Step, xa chỗ gây ra nó.
            if (_byClassId.Count == 0)
                throw new InvalidOperationException("CharacterConfigContainer chưa được Load. Cả hai bên nạp từ file của mình lúc khởi động.");

            return _byClassId[DRAGON_WARRIOR];
        }

        /// <summary>
        /// Thay cả bảng bằng dữ liệu vừa đọc từ file.
        ///
        /// Dựng NGUYÊN bảng mới rồi mới gán: nửa chừng mà ném thì bảng cũ còn nguyên, và luồng khác
        /// đọc song song hoặc thấy trọn bảng cũ hoặc trọn bảng mới. Đây cũng là chỗ duy nhất gọi
        /// <c>Prepare()</c>, nên không có đường nào để một config lọt vào bảng mà chưa quy ra tick.
        /// </summary>
        public static void Load(CharacterTableData table)
        {
            if (table == null)
                throw new ArgumentNullException(nameof(table));

            var built = new Dictionary<int, CharacterConfig>();

            foreach (CharacterConfig config in table.Classes)
            {
                // Trùng id thì không có giá trị mặc định nào để lùi về: bảng đã tự mâu thuẫn, và cái
                // nào thắng là chuyện của thứ tự dòng trong file.
                if (built.ContainsKey(config.ClassId))
                    throw new InvalidOperationException($"Hai lớp cùng ClassId {config.ClassId}: \"{built[config.ClassId].Name}\" và \"{config.Name}\".");

                config.Prepare();
                built[config.ClassId] = config;
            }

            _byClassId = built;
        }
    }
}
```

</details>

<details>
<summary><b>📖 Lời giải — <code>ConfigService.LoadTable&lt;T&gt;</code>, DTO, csproj</b></summary>

**`Server/GameServer/Config/ConfigService.cs`** — file này bị **viết lại**, không phải thêm vào.
Bản Bước 1 có `LoadGame()` riêng; bản này gộp nó và bảng nhân vật vào cùng một hàm nạp. Bốn khối:

**Khối 1 — cờ `_booted`, hàm dựng, và `Load()` — danh sách file của SERVER:**

```csharp
        /// <summary>Đã qua lần nạp đầu chưa, tức là đã có bản đang chạy để giữ khi file hỏng chưa.</summary>
        private bool _booted;

        /// <summary>Nạp ngay lúc dựng: không có trạng thái "đã có service nhưng chưa có số".</summary>
        public ConfigService()
        {
            Load();

            // Đặt SAU Load(): mọi lần Load() từ đây là reload, và chỉ lúc đó "giữ nguyên bản đang
            // chạy" mới có nghĩa.
            _booted = true;
        }

        /// <summary>
        /// Đọc lại TẤT CẢ file config. Gọi lúc boot và mỗi lần bấm phím reload — một lần nạp, một
        /// phím, nên không có đường nào nạp lại cái này mà quên cái kia.
        ///
        /// <b>Thêm file mới thì thêm đúng một dòng ở đây.</b>
        /// </summary>
        public void Load()
        {
            LoadFile<GameConfigData>(GAME, ValidateGame, ApplyGame, WhenBroken.UseDefaults);
            LoadTable<CharacterTableData>(ConfigFiles.CHARACTERS, ValidateCharacters, CharacterConfigContainer.Load);
        }
```

**Khối 2 — `WhenBroken` + `LoadFile<T>`, hàm nạp dùng cho MỌI file:**

```csharp
        /// <summary>Phải làm gì khi file hỏng — thứ duy nhất khác nhau giữa loại A và loại B.</summary>
        private enum WhenBroken
        {
            /// <summary>Dựng một object mặc định rồi dùng nó. Chỉ hợp lệ khi mọi trường đều có mặc định dùng được.</summary>
            UseDefaults,

            /// <summary>Không áp dụng gì cả, để nguyên thứ đang chạy. Lúc boot thì chưa có gì để giữ nên nó ném.</summary>
            KeepCurrent,
        }

        /// <summary>
        /// Đọc một file config, kiểm, rồi áp dụng. Dùng cho mọi file, kể cả <c>game.json</c>.
        ///
        /// Bốn tham số vì đó đúng là bốn thứ khác nhau giữa các file: đọc ở đâu, kẹp số thế nào, đổ
        /// đi đâu, hỏng thì sao. Phần còn lại giống hệt nhau nên nó ở đây một lần.
        /// </summary>
        /// <returns>File đã áp dụng, hoặc <c>null</c> khi reload hỏng và <see cref="WhenBroken.KeepCurrent"/>.</returns>
        /// <exception cref="InvalidOperationException">File loại B hỏng ngay ở lần nạp đầu.</exception>
        private TFile LoadFile<TFile>(string name, Action<TFile> validate, Action<TFile> apply, WhenBroken whenBroken)
            where TFile : class, IConfigFile, new()
        {
            string path = PathOf(name);

            try
            {
                var file = JsonConvert.DeserializeObject<TFile>(File.ReadAllText(path), Settings);

                // File rỗng ("{}") và file thiếu hẳn mảng dữ liệu có cùng một hậu quả: apply() sẽ thay
                // bảng đang chạy bằng một bảng 0 dòng, hỏng nặng hơn hẳn so với không nạp gì.
                if (file == null || file.RowCount == 0)
                    throw new JsonException("File rỗng — thiếu dữ liệu hoặc không có nội dung.");

                validate(file);

                // apply nằm TRONG try vì container ném khi bảng tự mâu thuẫn (hai dòng trùng id) —
                // lỗi dữ liệu, phải rơi vào cùng nhánh với file gõ sai cú pháp.
                apply(file);

                return file;
            }
            // Bắt đúng ba loại lỗi dự kiến: catch (Exception) ở đây sẽ nuốt luôn NullReferenceException
            // của chính code này.
            catch (Exception ex) when (ex is IOException || ex is JsonException || ex is InvalidOperationException)
            {
                if (whenBroken == WhenBroken.KeepCurrent)
                {
                    // Lúc boot chưa có bản đang chạy nào để giữ, nên "giữ nguyên" là giữ một bảng
                    // rỗng: lỗi sẽ chỉ lộ ra ở gói tin đầu tiên tra tới bảng, xa chỗ gây ra nó. Chết
                    // ngay tại đây, kèm tên file. Không Log.Error trước khi ném để cùng một câu
                    // không in ra hai lần.
                    if (!_booted)
                        throw new InvalidOperationException($"Không nạp được {path}: {ex.Message}", ex);

                    Log.Error($"Không nạp được {path.Red()}: {ex.Message}. GIỮ NGUYÊN bản đang chạy.");
                    return null;
                }

                Log.Warn($"Không đọc được {path.Red()}: {ex.Message}. " +
                         "Chạy bằng GIÁ TRỊ MẶC ĐỊNH — số trong file KHÔNG có hiệu lực.");

                // Vẫn validate bản mặc định: một mặc định biên dịch sẵn nằm ngoài khoảng cho phép là
                // lỗi lập trình, và nó phải lộ ra ở đây.
                var fallback = new TFile();

                validate(fallback);
                apply(fallback);

                return fallback;
            }
        }

```

**Khối 3 — `LoadTable<T>`, lớp mỏng thêm vân tay cho loại B, và `PathOf`:**

```csharp
        /// <summary>
        /// <see cref="LoadFile{TFile}"/> cộng thêm một dòng log có vân tay. Dành cho file loại B —
        /// thứ client cũng đọc, nên đáng có một con số để đối chiếu hai bên bằng mắt.
        ///
        /// Vân tay tính SAU khi apply, và thứ tự ấy có lý do: apply gọi <c>Prepare()</c>, tức là quy
        /// giây ra tick. Với ActionData — một struct toàn kiểu unmanaged nên MemoryPack chép nguyên
        /// khối — mấy con tick ấy NẰM TRONG byte đã tuần tự hoá dù có <c>[MemoryPackIgnore]</c> hay
        /// không. Băm "thứ đang thật sự chạy" thì không phải nhớ trạng thái nào là trạng thái đúng
        /// để băm, và client băm ở cùng chỗ ấy nên hai số so được với nhau.
        /// </summary>
        private void LoadTable<TTable>(string name, Action<TTable> validate, Action<TTable> load)
            where TTable : class, IConfigFile, IMemoryPackable<TTable>, new()
        {
            TTable table = LoadFile(name, validate, load, WhenBroken.KeepCurrent);

            if (table == null)
                return;

            Log.Info($"Bảng {name.Cyan()}: {table.RowCount} dòng, version {table.Version}, " +
                     $"vân tay {ConfigFingerprint.Of(table):X8}");
        }

        /// <summary>
        /// AppContext.BaseDirectory chứ không phải thư mục hiện hành: chỗ gõ lệnh không phải chỗ file
        /// exe nằm, và đường dẫn tương đối tới gốc repo chỉ đúng trên đúng một máy.
        /// </summary>
        private static string PathOf(string name)
        {
            // Tên file truyền vào không có đuôi (Resources.Load bên client không nhận đuôi), nên
            // server thêm vào đây — một chỗ, cho mọi file.
            return Path.Combine(AppContext.BaseDirectory, "Data", "Config", name + ".json");
        }
```

> `PathOf` đổi so với Bước 1: hằng `GAME_FILE = "game.json"` thành `GAME = "game"`, và hàm tự thêm
> đuôi. Lý do nằm ở phía client — `Resources.Load` của Unity **không nhận đuôi file**, nên tên trong
> `ConfigFiles` bắt buộc phải không có `.json`. Để server cũng dùng đúng dạng tên ấy thì nó tự thêm
> đuôi, một chỗ, cho mọi file.

**Khối 4 — `ApplyGame` + `ValidateCharacters`:**

```csharp
        /// <summary>
        /// Thay NGUYÊN object chứ không sửa từng field: gán reference là thao tác nguyên tử nên luồng
        /// tick hoặc thấy trọn bộ cũ, hoặc trọn bộ mới. Sửa tại chỗ thì nó có thể đọc được Gravity
        /// mới ghép với MaxFallSpeed cũ — một tổ hợp chưa từng tồn tại trong file nào.
        /// </summary>
        private void ApplyGame(GameConfigData data)
        {
            Current = data;

            Log.Info($"Config: gravity={World.Gravity} maxFall={World.MaxFallSpeed} " +
                     $"coyote={World.CoyoteTicks}t jumpBuf={World.JumpBufferTicks}t drop={World.DropThroughTicks}t · " +
                     $"aoi={data.Server.AoiRadiusX} · startMap={data.Server.StartingMapId}");
        }

        /// <summary>
        /// Kẹp bộ số của từng lớp nhân vật. Nằm ở SERVER chứ không ở CharacterConfigContainer.Load,
        /// vì client cũng gọi Load — và client kẹp số của chính nó là client tự sửa dữ liệu trong
        /// im lặng, tức là tạo ra đúng cái lệch mà không ai nhìn thấy.
        /// </summary>
        private static void ValidateCharacters(CharacterTableData table)
        {
            var fallback = new CharacterConfig();

            foreach (CharacterConfig config in table.Classes)
            {
                string tag = $"class {config.ClassId}";

                config.MoveSpeed = Clamp(config.MoveSpeed, 0.001f, ConfigLimits.SPEED_CAP - 0.001f, fallback.MoveSpeed, $"{tag}.MoveSpeed");
                config.JumpSpeed = Clamp(config.JumpSpeed, 0.001f, ConfigLimits.SPEED_CAP - 0.001f, fallback.JumpSpeed, $"{tag}.JumpSpeed");

                // < 0.5 chứ không <= : rộng đúng nửa ô là vừa khít khe 1 ô, và "vừa khít" trong số
                // thực dấu phẩy động nghĩa là lúc lọt lúc không.
                config.BodyHalfWidth = Clamp(config.BodyHalfWidth, 0.001f, ConfigLimits.BODY_HALF_WIDTH_CAP,
                    fallback.BodyHalfWidth, $"{tag}.BodyHalfWidth");

                // Trần 2.0 đến từ OverlapsSolid: nó quét ba mức cao, và ba mức chỉ phủ kín khi khoảng
                // cách giữa hai mức nhỏ hơn cạnh ô. Cao hơn 2.0 là có ô lọt qua khe kiểm.
                config.BodyHeight = Clamp(config.BodyHeight, 0.001f, ConfigLimits.BODY_HEIGHT_CAP,
                    fallback.BodyHeight, $"{tag}.BodyHeight");

                config.BodyHeightCrouch = Clamp(config.BodyHeightCrouch, 0.001f,
                    MathF.Min(config.BodyHeight, ConfigLimits.CROUCH_HEIGHT_CAP), fallback.BodyHeightCrouch,
                    $"{tag}.BodyHeightCrouch");
            }
        }
```

Và `using` ở đầu file thêm ba dòng: `MemoryPack`, `MMORPG.Shared.World.Character`, và
`MMORPG.Shared.World` (cho `IConfigFile`, `ConfigFingerprint`, `ConfigFiles`).

**`Server/GameServer/Config/GameConfigData.cs`** — thêm `: IConfigFile` và một property:

```csharp
    public sealed class GameConfigData : IConfigFile
    {
        /// <summary>Phiên bản schema, nằm trong file.</summary>
        public int Version { get; set; } = 1;

        /// <summary>Luật thế giới — phần duy nhất của file này client được biết.</summary>
        public WorldConfig World { get; set; } = new();

        /// <summary>Những con số chỉ server dùng.</summary>
        public ServerConfigData Server { get; set; } = new();

        /// <summary>Luôn 1: file này không có mảng nào, và mọi trường đều có mặc định hợp lệ.</summary>
        [JsonIgnore] public int RowCount => 1;
    }
```

`[JsonIgnore]` cần `using Newtonsoft.Json;`. Không có nó thì Newtonsoft thấy một property chỉ-đọc
tên `RowCount` và cũng không sao — nhưng `MissingMemberHandling.Error` là luật hai chiều trong đầu
người đọc, nên nói rõ "trường này không thuộc file" bằng attribute vẫn hơn.

**`Server/Shared/Dto/Character/CharacterDto.cs` và `Server/Shared/Dto/World/WorldSyncDto.cs` —
KHÔNG đổi gì.** Đây là điểm đáng chú ý nhất của cả bước, và nó là hệ quả trực tiếp của "mỗi bên đọc
file của chính nó": bảng loại B không đi trên dây, nên thêm bảng nhân vật hôm nay — và bảng item ở
Phase 13, bảng quái ở Phase 15 — không chạm vào một dòng nào của contract.

Trong `CharacterService.EnterWorldAsync`, `EnterWorldResponse` đã mang `WorldConfig World` từ
Bước 1, và đó vẫn là dữ liệu tĩnh duy nhất trong gói:

```csharp
            {
                // ... các trường cũ, y như sau Bước 1 ...

                // Bộ số của PHIÊN này, và là dữ liệu tĩnh duy nhất đi trong gói: client không có
                // file game.json nên không có cách nào tự biết mấy con số này.
                World = _config.World,
            };
```

`PlayerEntity` cũng không đổi: bạn **không** cần mở property `Map` nào cả, vì không có checksum map
nào phải lấy. `WorldService.TryTakePortal` giữ nguyên gói `MapChangedNotice` hai trường của Phase 10.

**`Server/GameServer/GameServer.csproj`** — hai khối copy, và ranh giới giữa chúng là ranh giới loại A / loại B:

```xml
    Config loại A: CHỈ server đọc. Ở gốc repo vì nó là dữ liệu vận hành, và cố tình KHÔNG nằm dưới
    Assets/ — game.json chứa bán kính AOI và map khởi đầu, là số của thuật toán server. Ship nó
    trong bản build của client là cho người chơi đọc cách server hoạt động.
    -->
    <ItemGroup>
        <Content Include="..\..\Config\*.json"
                 Link="Data\Config\%(Filename)%(Extension)"
                 CopyToOutputDirectory="PreserveNewest"/>
    </ItemGroup>

    <!--
    Config loại B: CẢ HAI BÊN cùng đọc. Nguồn nằm trong Resources của client — đó là bên phải có
    file lúc chạy mà không tải gì thêm — và build copy sang server, đúng chiều và đúng khuôn với
    file map ở trên.

    Một nguồn trên đĩa, hai bản trong hai output. Hai bản thì lệch được (build lại một bên mà quên
    bên kia), nên EnterWorldResponse mang vân tay từng bảng để client tự phát hiện — xem
    ConfigService ở cả hai bên.
    -->
    <ItemGroup>
        <Content Include="..\..\Assets\Game\Resources\Config\*.json"
                 Link="Data\Config\%(Filename)%(Extension)"
                 CopyToOutputDirectory="PreserveNewest"/>
    </ItemGroup>

    <Target Name="CheckDataFolders" BeforeTargets="Build">
        <!-- Glob không khớp file nào thì MSBuild im lặng. Biến sự im lặng đó thành lỗi build. -->
        <Error Condition="!Exists('$(MSBuildProjectDirectory)/../../Assets/Game/Resources/Maps')"
               Text="Không thấy Assets/Game/Resources/Maps. Chạy Tools/MMORPG/Export Map trong Unity trước đã."/>
        <Error Condition="!Exists('$(MSBuildProjectDirectory)/../../Assets/Game/Resources/Config')"
               Text="Không thấy Assets/Game/Resources/Config. Bảng loại B (characters.json, items.json) nằm ở đó."/>
        <Error Condition="!Exists('$(MSBuildProjectDirectory)/../../Config')"
               Text="Không thấy Config/ ở gốc repo. game.json (config loại A, chỉ server đọc) nằm ở đó."/>
    </Target>
```

</details>

---

## Bước 4 — Loại B ở client: nạp bảng của CHÍNH NÓ

### Hướng làm

**Đây là bước mà bản doc cũ thiếu hẳn, và thiếu nó thì ba bước trên không chạy được.** Server nạp
bảng của SERVER xong — nhưng nếu client không nạp bảng của CLIENT thì
`CharacterConfigContainer.Get(id)` ném `InvalidOperationException: chưa được Load`, và triệu chứng là
**vào world xong không thấy nhân vật nào**.

Năm câu hỏi, trả lời đủ năm là xong bước này.

**(1) Ai nạp?** Một `ConfigService` phía client — **cùng tên, cùng vai** với bên server. Đặt ở
`Assets/Game/Scripts/Config/`, namespace `MMORPG.Client.Config`. Nó là **chỗ duy nhất** client nạp
config, và mỗi bảng mới thêm đúng một dòng ở đó — đối xứng với `ConfigService.Load()` bên server.

**(2) Nạp lúc nào?** **Trong hàm dựng**, tức là lúc VContainer dựng container — *trước màn hình login,
trước mọi gói tin*. Không đợi `EnterWorld`.

Đây là khác biệt lớn nhất so với cách "server gửi bảng", và nó mua một thứ cụ thể: client hiển thị
được **tên và icon item trước khi vào world**. Cửa hàng ở màn hình chờ, bảng xếp hạng có icon, màn
hình chọn nhân vật — không cái nào chờ được `EnterWorld`. Hôm nay dự án chưa có màn hình nào như thế
(1 tài khoản = 1 nhân vật, vào thẳng), nhưng kiến trúc thì không chặn nữa.

**(3) Client có kiểm miền giá trị không? KHÔNG — và đây là chỗ tinh vi nhất của cả bước.**

Trực giác nói "hai bên đọc cùng file thì hai bên phải xử lý giống nhau, nên client cũng phải kẹp".
Sai, và sai theo hướng làm hỏng đúng thứ vừa dựng:

| | Client CŨNG kẹp | Chỉ server kẹp *(đúng)* |
|---|---|---|
| File có `MoveSpeed = 999` | server chạy 5, client chạy 5 | server chạy 5, client chạy 999 |
| Hai dòng log vân tay | **giống hệt nhau** | **khác nhau** |
| Kết quả | file hỏng không ai phát hiện ra, và không ai biết bảng đang chạy khác bảng trong file | hai số lệch nhau ngay trên console, kèm log Warn của server nói đúng trường nào vừa bị từ chối |

Nói cách khác: cho client kẹp là làm dấu vân tay **thôi phát hiện được** đúng thứ nó sinh ra để phát
hiện. Server kẹp rồi băm, client băm bản thô — cái file hỏng ấy tự khai ra.

> Đây là một biến thể của luật cũ ("kiểm nằm ở server, không ở `Load`"), nhưng lý do đã đổi. Hồi bảng
> còn đi trên dây, lý do là *"client không có quyền phán xét dữ liệu server gửi"*. Giờ client đọc file
> của chính nó nên lý do đó hết áp dụng — và may là có một lý do khác, mạnh hơn, dẫn tới cùng kết luận.

**(4) Băm lúc nào? SAU `load()`, ở cả hai bên.** Không phải chuyện phong cách:

`ActionData` là struct chỉ chứa kiểu unmanaged, nên MemoryPack xếp nó vào loại **blittable** và chép
nguyên khối bộ nhớ — **bỏ qua `[MemoryPackIgnore]`**. Nghĩa là `DurationTicks` (thứ `Prepare()` điền)
*nằm trong* byte đã tuần tự hoá. Băm trước `load()` ở một bên và sau ở bên kia là hai số khác nhau cho
cùng một file.

Vô hại về mặt dữ liệu (ticks tính ra từ seconds, đã băm rồi), nhưng là một ràng buộc thật. Có bài test
ghim nó: `ConfigFingerprintTests.Of_BlittableStruct_IncludesDerivedFields`.

> Bài học rộng hơn: **một thuộc tính không phải lúc nào cũng có tác dụng.** `[MemoryPackIgnore]` trên
> field của struct blittable là một dòng khẳng định một điều sai — và nếu không có bài test thì không
> ai biết. Thấy một thuộc tính "chắc là nó lo rồi" thì viết một bài test bắt nó lo thật.

**(5) `WorldApi.Config` static phải chết.** Hôm nay `PlayerMotor` đọc `WorldApi.Config` — một field
`public static` trên một class chuyên **gửi gói tin**. Ba chỗ sai trong một dòng: sai người giữ
(`WorldApi` là chiều gửi, không phải chỗ chứa state), sai vòng đời (static sống qua cả lần logout), và
sai kiểu phụ thuộc (không ai nhìn chữ ký `PlayerMotor` mà biết nó cần `WorldConfig`).

Thay bằng: `PlayerMotor.Init` **nhận** `CharacterConfig` và `WorldConfig` làm tham số. `WorldSpawner` tra
chúng một lần rồi đưa xuống. Nhận cả hai cùng lúc còn làm hiển nhiên một điều kiện dễ quên — **cả hai
phải đến từ cùng một gói `EnterWorld`**.

**Vì sao `WorldSpawner` tra container chứ `PlayerMotor` không tự tra:** `WorldSpawner` là chỗ biết gói
tin nói gì (`response.ClassId`, `notice.ClassId`); `PlayerMotor` chỉ biết mô phỏng. Và tra ở chỗ dựng
nghĩa là **một** chỗ tra cho cả người chơi mình lẫn người chơi khác — `RemotePlayerView.Init` cũng lấy
config từ đúng đó, bằng `ClassId` **của người kia**, vì hai lớp nhân vật có thời lượng hành động khác
nhau và người xem phải co clip theo bảng của người bị xem.

**Và dòng dễ quên nhất của cả phase:**

```csharp
builder.Register<ConfigService>(Lifetime.Singleton);
```

Không có dòng này trong `GameLifetimeScope.Configure` thì VContainer không dựng nổi `WorldPresenter`, và
lỗi **không** chỉ vào `ConfigService` — nó đổ dây chuyền, và thủ phạm nằm ở **dòng cuối** của chuỗi
`Failed to resolve` (xem `CLAUDE.md` §DI hai bên).

**Bản đồ: không có phép so nào cả.** `MapService.Load` chỉ nạp rồi in một dòng log — tên, kích
thước, origin — đúng bộ số mà `MapRegistry` in bên server. Không `MapMatches`, không checksum trong
gói tin: cùng lý do với bảng, và ở đây còn mạnh hơn, vì map thì client nạp **theo id**, lười, chứ
không nạp hết lúc khởi động. Một phép so "mọi map phải khớp lúc vào world" sẽ hết nghĩa ngay ở map
thứ ba.

**`LocalPlayer` không đổi một dòng.** Nó là cache dữ liệu **nhân vật của mình** (tên, level, entityId);
config là chuyện khác và thuộc `ConfigService`. Hai loại dữ liệu có **vòng đời khác nhau** (một cái
theo nhân vật, một cái theo phiên nối server) không nên ở cùng một class.

### ✅ CHECKPOINT D

Bước này mới là bước **chạy được đầu-cuối**. Từ đây các phép thử là quan sát bằng mắt trong game.

1. Unity compile sạch. **Chưa cần login** — console client đã in hai dòng ngay lúc vào scene:
   `[ConfigService] Bảng characters: 1 dòng, version 1, vân tay XXXXXXXX`
   `[ConfigService] Bảng items: 2 dòng, version 1, vân tay YYYYYYYY`
   **Hai số này phải trùng đúng hai dòng server in lúc boot.**
2. Vào game → thêm một dòng `Luật thế giới: gravity=30 maxFall=20 ...`. Đặt nó cạnh dòng
   `Config: gravity=30 ...` server in lúc boot: cùng số.
3. Sửa `Gravity` thành `60` trong `bin/.../Data/Config/game.json` → restart server → vào game → nhân vật
   rơi nặng hẳn, **không rubber-band**. Client không build lại gì. (`game.json` là loại A — chỉ server
   có, nên client không cần biết gì thêm.)
4. Sửa `MoveSpeed` thành `12` trong `Assets/Game/Resources/Config/characters.json`, **build lại
   GameServer**, vào game → chạy nhanh hẳn, **không rubber-band**, hai vân tay vẫn khớp.
5. **Phép thử quan trọng nhất của bước:** sửa `MoveSpeed` thành `13` rồi **cố tình KHÔNG build lại
   GameServer** → vào game → nhân vật **rubber-band** khi chạy. Mở hai console đặt cạnh nhau: client
   in `vân tay XXXXXXXX`, server in một số khác. Đó đúng là lần "quên một bên" mà dòng log này sinh
   ra để bạn nhận ra trong ba mươi giây thay vì nửa buổi. Build lại → hết rubber-band, hai số bằng nhau.
6. Ghi `"MoveSpeed": 999` (ngoài khoảng cho phép) → build lại **cả hai** → server log Warn
   `class 1.MoveSpeed = 999 ngoài khoảng ... dùng 5`, và hai vân tay **khác nhau dù hai file giống
   hệt nhau**. Đọc hai dòng đó cùng nhau là ra nguyên nhân: server đã kẹp, client thì không. Đây là
   câu (3) ở trên, nhìn thấy được.
7. Đổi tên `characters.json` thành `characters.bak` trong Resources → client báo
   `Không thấy Resources/Config/characters.json`, rồi lúc spawn ném
   `InvalidOperationException: CharacterConfigContainer chưa được Load`. **Chỉ ở client** — server
   vẫn boot và chạy bình thường. Đó chính là lý do dòng `LoadTable` bên client bị đánh dấu ⚠️ trong
   `FEATURE-TEMPLATE.md`: quên nó thì không có lỗi biên dịch, và phía server không có dấu hiệu nào.
8. Export lại một map trong Unity (thêm một ô sàn), **cố tình không build lại GameServer** → nhân
   vật đứng trên không hoặc kẹt tường vô hình. Chỗ để nhìn là hai dòng log map
   (`Map Forest #1 — 64×10 ô, origin (-17, -9)`), một của `MapRegistry`, một của `MapService`.
   Kích thước khác nhau thì thấy ngay; cùng kích thước mà khác nội dung thì phải diff file. Đó là
   cái giá của việc map không có vân tay, và nó rẻ hơn một phép so sẽ chặn đường nạp map lười.
9. Xoá tạm dòng `builder.Register<Config.ConfigService>` → Unity báo `VContainerException: Failed to
   resolve ... : No such registration of type: MMORPG.Client.Config.ConfigService`. Đọc **dòng cuối**
   của chuỗi lỗi. Trả lại dòng.
10. Mở 2 client (Multiplayer Play Mode) → thấy nhau, cả hai chạy bằng đúng bộ số server.

<details>
<summary><b>📖 Lời giải — client <code>ConfigService</code> và <code>GameLifetimeScope</code></b></summary>

**`Assets/Game/Scripts/Config/ConfigService.cs`** (file mới, nguyên văn):

```csharp
using System;
using HungNT;
using MMORPG.Shared.Dto.Character;
using MMORPG.Shared.World;
using MMORPG.Shared.World.Character;
using MMORPG.Shared.World.Item;
using Newtonsoft.Json;
using UnityEngine;

namespace MMORPG.Client.Config
{
    /// <summary>
    /// Chỗ duy nhất phía client nạp config, và cửa duy nhất để phần còn lại của client hỏi "luật thế
    /// giới đang là gì". Đối ứng của <c>MMORPG.GameServer.Config.ConfigService</c>.
    ///
    /// Danh sách trong <see cref="LoadTables"/> là danh sách của CLIENT, không phải bản sao của
    /// server: server có bảng tỉ lệ rơi đồ và bảng AI quái mà client không đọc tới, còn client sẽ có
    /// bảng thoại và mô tả kỹ năng mà server không cần.
    ///
    /// Bảng dữ liệu thì client đọc file của chính nó ngay lúc khởi động, nên tên và icon item hiện
    /// được trước khi vào world. Luật thế giới (<see cref="World"/>) thì server gửi, vì người vận
    /// hành chỉnh nó giữa hai lần restart mà không patch client.
    /// </summary>
    public sealed class ConfigService
    {
        /// <summary>Thư mục trong Resources. Cùng nguồn với bản server đọc — xem GameServer.csproj.</summary>
        private const string RESOURCE_FOLDER = "Config";

        /// <summary>
        /// Đúng bộ settings của server: hai bên đọc cùng một file bằng hai luật parse khác nhau là
        /// cùng byte vào, hai object khác nhau ra.
        /// </summary>
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Error,
        };

        /// <summary>
        /// Luật thế giới của phiên này. Có mặc định để code chạm vào nó trước khi vào world không
        /// phải kiểm null — nhưng <see cref="IsInWorld"/> mới là thứ nói nó đã thật hay chưa.
        /// </summary>
        public WorldConfig World { get; private set; } = new WorldConfig();

        /// <summary>
        /// Đã nhận luật thế giới từ server chưa. Sai nghĩa là <see cref="World"/> đang là mặc định
        /// biên dịch sẵn — dùng nó để dự đoán là tự tạo ra rubber-band.
        /// </summary>
        public bool IsInWorld { get; private set; }

        /// <summary>Nạp bảng ngay lúc VContainer dựng container: trước màn hình login, trước mọi gói tin.</summary>
        public ConfigService()
        {
            LoadTables();
        }

        /// <summary>
        /// <b>Thêm bảng mới thì thêm đúng một dòng ở đây.</b>
        ///
        /// Nạp hết lúc khởi động là lựa chọn của hôm nay, không phải ràng buộc: hai bảng đọc xong
        /// trong vài mili giây. Ngày cần tải lười hoặc tải từ xa thì chỗ phải sửa là
        /// <see cref="LoadTable{TTable}"/>, và không ai khác biết.
        /// </summary>
        private void LoadTables()
        {
            LoadTable<CharacterTableData>(ConfigFiles.CHARACTERS, CharacterConfigContainer.Load);
            LoadTable<ItemTableData>(ConfigFiles.ITEMS, ItemConfigContainer.Load);
        }

        /// <summary>
        /// Đọc một bảng từ Resources rồi nạp vào container của nó. Cùng tham số như bên server trừ
        /// hàm validate, và đó là khác biệt cố ý: phép kẹp miền giá trị chỉ chạy ở server.
        ///
        /// Client kẹp số của chính nó là client âm thầm sửa dữ liệu — một file có
        /// <c>MoveSpeed = 999</c> sẽ thành 5 ở cả hai bên và không ai phát hiện ra. Client băm bản
        /// thô, server băm bản đã kẹp, hai dòng log lệch nhau, và người ta đi sửa file.
        /// </summary>
        private void LoadTable<TTable>(string name, Action<TTable> load)
            where TTable : class, IConfigFile, MemoryPack.IMemoryPackable<TTable>
        {
            string path = $"{RESOURCE_FOLDER}/{name}";

            // Không có đuôi .json trong đường dẫn: Resources.Load luôn bỏ phần đuôi file.
            var asset = Resources.Load<TextAsset>(path);

            if (asset == null)
            {
                this.LogError($"Không thấy Resources/{path}.json — bảng {name} sẽ rỗng, và mọi chỗ tra nó sẽ hỏng.");
                return;
            }

            try
            {
                var table = JsonConvert.DeserializeObject<TTable>(asset.text, Settings);

                if (table == null || table.RowCount == 0)
                    throw new JsonException("Bảng rỗng — thiếu mảng dữ liệu hoặc file không có nội dung.");

                load(table);

                // Băm SAU load, đúng chỗ server băm. Lệch thời điểm thì hai con số thôi so được với
                // nhau, mà chẳng có gì báo là chúng đã thôi so được.
                this.Log($"Bảng {name}: {table.RowCount} dòng, version {table.Version}, " +
                         $"vân tay {ConfigFingerprint.Of(table):X8}");
            }
            // Cùng danh sách với server. InvalidOperationException vì container ném nó khi bảng tự
            // mâu thuẫn (hai dòng trùng id).
            //
            // Khác server ở chỗ client KHÔNG chết: bảng giữ nguyên (rỗng, nếu đây là lần nạp đầu) và
            // lỗi lộ ra ở lần tra đầu tiên. Server chặn boot vì một bảng hỏng nghĩa là không ai chơi
            // được và người vận hành phải biết ngay; ở client thì chỉ máy đó hỏng, và log là chỗ đúng.
            catch (Exception ex) when (ex is JsonException || ex is InvalidOperationException)
            {
                this.LogError($"Bảng {name} hỏng: {ex.Message}");
            }
        }

        /// <summary>
        /// Nhận luật thế giới. Gọi ngay khi nhận <c>EnterWorldResponse</c>, trước khi spawn bất cứ thứ gì.
        ///
        /// Trả false chỉ có một lý do, và nó không phải lỗi dữ liệu: gói thiếu hẳn khối World, tức
        /// hai bên đang chạy hai bản MMORPG.Shared khác nhau.
        /// </summary>
        public bool Apply(EnterWorldResponse response)
        {
            if (response.World == null)
            {
                this.LogError("Gói EnterWorld thiếu WorldConfig — server và client đang chạy hai bản " +
                              "MMORPG.Shared khác nhau? Build lại Server/Shared.");
                return false;
            }

            World = response.World;
            IsInWorld = true;

            this.Log($"Luật thế giới: gravity={World.Gravity} maxFall={World.MaxFallSpeed} " +
                     $"coyote={World.CoyoteTicks}t jumpBuf={World.JumpBufferTicks}t drop={World.DropThroughTicks}t");

            return true;
        }

        /// <summary>
        /// Quên luật thế giới khi rời world. Cố tình không xoá container: bảng đọc từ file của client
        /// nên chúng đúng cả khi không ở trong world, và một túi đồ đang mở vẫn cần tra tên item.
        /// </summary>
        public void Clear()
        {
            World = new WorldConfig();
            IsInWorld = false;
        }
    }
}
```

**`Assets/Game/Scripts/Boot/GameLifetimeScope.cs`** — thêm **một dòng**, đặt trước nhóm World:

```csharp
            // Config: client đọc file của chính nó trong hàm dựng, TRƯỚC cả màn hình login. Đăng ký
            // trước nhóm World vì WorldPresenter và WorldSpawner đều inject nó.
            builder.Register<ConfigService>(Lifetime.Singleton);

            // World, EnterWorld
            builder.Register<MapService>(Lifetime.Singleton);
            builder.Register<WorldApi>(Lifetime.Singleton);
            builder.Register<LocalPlayer>(Lifetime.Singleton);
            builder.Register<WorldNetHandler>(Lifetime.Singleton).AsSelf().As<INetHandlerGroup>();
            builder.RegisterComponentInHierarchy<WorldSpawner>();
            builder.RegisterComponentInHierarchy<WorldPresenter>();
```

`Config.ConfigService` viết đủ tiền tố namespace vì bên server cũng có một class tên y hệt —
đọc `Config.ConfigService` là biết ngay đang nói về bên nào.

</details>

<details>
<summary><b>📖 Lời giải — <code>WorldPresenter</code> và <code>WorldApi</code></b></summary>

**`Assets/Game/Scripts/World/WorldPresenter.cs`** (thay nguyên file):

```csharp
using HungNT;
using MMORPG.Client.Config;
using MMORPG.Client.Network.Handlers;
using MMORPG.Shared.Dto.Auth;
using MMORPG.Shared.Dto.Character;
using UnityEngine;
using VContainer;

namespace MMORPG.Client.World
{
    /// <summary>
    /// Nối auth với world: đăng nhập xong tự gửi EnterWorld, nhận response thì nạp config rồi spawn.
    /// Không có UI riêng — phase này client vào thẳng game.
    /// </summary>
    public sealed class WorldPresenter : MonoBehaviour
    {
        [SerializeField] private WorldSpawner _worldSpawner;

        private WorldApi _worldApi;
        private WorldNetHandler _worldNetHandler;
        private AuthNetHandler _authNetHandler;
        private LocalPlayer _localPlayer;
        private ConfigService _configService;

        [Inject]
        public void Construct(WorldApi worldApi, WorldNetHandler worldNetHandler,
            AuthNetHandler authNetHandler, LocalPlayer localPlayer, ConfigService configService)
        {
            _worldApi = worldApi;
            _worldNetHandler = worldNetHandler;
            _authNetHandler = authNetHandler;
            _localPlayer = localPlayer;
            _configService = configService;
        }

        private void Start()
        {
            _authNetHandler.OnLoginResult += OnLoggedIn;
            _authNetHandler.OnLogoutResult += OnLoggedOut;
            _authNetHandler.OnKicked += OnKicked;
            _worldNetHandler.OnEnterWorldResult += OnEnterWorldResult;
        }

        private void OnDestroy()
        {
            if (_authNetHandler == null)
                return;

            _authNetHandler.OnLoginResult -= OnLoggedIn;
            _authNetHandler.OnLogoutResult -= OnLoggedOut;
            _authNetHandler.OnKicked -= OnKicked;
            _worldNetHandler.OnEnterWorldResult -= OnEnterWorldResult;
        }

        private void OnLoggedIn(AuthResponse response)
        {
            if (!response.Success)
                return;

            _worldApi.EnterWorld();
        }

        private void OnEnterWorldResult(EnterWorldResponse response)
        {
            if (!response.Success)
            {
                this.LogWarning($"EnterWorld thất bại: {response.Error}");
                return;
            }

            // Hai lý do: (1) WorldSpawner gọi PlayerMotor.Init, mà Init tra CharacterConfigContainer
            // ngay dòng đầu — bảng hỏng thì container ném và triệu chứng là "vào world xong không
            // có nhân vật nào"; (2) Apply trả false khi bảng lệch server, và lúc đó KHÔNG được vào
            // world: chơi bằng bộ số khác server là rubber-band không có tên.
            if (!_configService.Apply(response))
                return;

            _localPlayer.Apply(response);
            _worldSpawner.SpawnLocalPlayer(response);
        }

        private void OnLoggedOut(AuthResponse response)
        {
            _worldSpawner.DespawnLocalPlayer();
            _localPlayer.Clear();
            _configService.Clear();
        }

        private void OnKicked(KickedNotice notice)
        {
            _worldSpawner.DespawnLocalPlayer();
            _localPlayer.Clear();
            _configService.Clear();
        }
    }
}
```

**`Assets/Game/Scripts/World/WorldApi.cs`** — **xoá** dòng `public static WorldConfig Config;`
và `using MMORPG.Shared.World;` không còn dùng:

```csharp
using HungNT;
using MMORPG.Client.Network;
using MMORPG.Shared.Dto;
using MMORPG.Shared.Dto.World;
using MMORPG.Shared.Net;
using MMORPG.Shared.World.Movement;

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
            this.Log("Enter World");
            _netService.Send(NetCmd.EnterWorld, new EmptyRequest());
        }

        /// <summary>Gửi đúng cái intent vừa dùng để dự đoán — không dàn nó ra thành từng tham số rồi ráp lại.</summary>
        public void Move(int seq, in MoveIntent intent)
        {
            // Không log ở đây — 20 lần/giây, log là dìm chết console.
            _netService.Send(NetCmd.MoveInput, new MoveInputRequest { Seq = seq, Intent = intent });
        }
    }
}
```

</details>

<details>
<summary><b>📖 Lời giải — <code>WorldSpawner</code> (tra bảng, đưa hai bộ số xuống motor)</b></summary>

**`Assets/Game/Scripts/World/WorldSpawner.cs`** — ba thay đổi: inject `ConfigService`, tra
`CharacterConfigContainer` ở hai chỗ, và truyền `WorldConfig` vào `motor.Init`. **Không** có phép
so checksum map nào — xem mục "Bản đồ" ở phần hướng làm.

Phần đầu file:

```csharp
using System.Collections.Generic;
using HungNT;
using MMORPG.Client.Network.Handlers;
using MMORPG.Shared.Dto.Character;
using MMORPG.Shared.Dto.World;
using MMORPG.Shared.World.Character;
using MMORPG.Shared.World.Map;
using UnityEngine;
using VContainer;

namespace MMORPG.Client.World
{
    /// <summary>
    /// Dựng và gỡ biểu diễn hình ảnh (GameObject) cho nhân vật của chính mình, trỏ camera bám theo.
    /// </summary>
    public class WorldSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject _playerPrefab;
        [SerializeField] private GameObject _remotePrefab;
        [SerializeField] private Transform _entityRoot;
        [SerializeField] private CameraFollow _cameraFollow;
        [SerializeField] private MapView _mapView;

        private WorldApi _worldApi;
        private WorldNetHandler _worldNetHandler;
        private LocalPlayer _localPlayer;
        private MapService _mapService;
        private Config.ConfigService _configService;

        private GameObject _localPlayerObject;
        private readonly Dictionary<int, RemotePlayerView> _remotes = new();

        [Inject]
        public void Construct(WorldApi worldApi, WorldNetHandler worldNetHandler, LocalPlayer localPlayer,
            MapService mapService, Config.ConfigService configService)
        {
            _worldApi = worldApi;
            _worldNetHandler = worldNetHandler;
            _localPlayer = localPlayer;
            _mapService = mapService;
            _configService = configService;
        }
```

`Start`, `OnDestroy`, `DespawnLocalPlayer`, `OnEntityDespawn`, `OnSnapshot`,
`DespawnAllRemotes` **không đổi một dòng**. Ba hàm còn lại:

```csharp
        public void SpawnLocalPlayer(EnterWorldResponse response)
        {
            if (_localPlayerObject != null)
                DespawnLocalPlayer();

            // Nạp LUẬT trước, dựng HÌNH sau, rồi mới Init motor: motor cần lưới va chạm ngay từ tick
            // dự đoán đầu tiên.
            MapGrid map = _mapService.Load(response.MapId);

            _mapView.Show(map);

            _localPlayerObject = Instantiate(_playerPrefab, new Vector3(response.X, response.Y), Quaternion.identity, _entityRoot);
            _localPlayerObject.name = $"Player_{response.EntityId}_{response.Name}";

            // Prefab sinh lúc runtime — VContainer không tự inject. Đưa phụ thuộc vào tay.
            //
            // Bảng tra được CHÍNH Ở ĐÂY chứ không truyền CharacterConfig từ ngoài vào: container đã
            // nạp xong ở WorldPresenter một nhịp trước, và tra tại chỗ dùng thì không có đường nào để
            // một chỗ gọi khác đưa vào bộ số của lớp nhân vật khác.
            var motor = _localPlayerObject.GetComponent<PlayerMotor>();
            motor.Init(_worldApi, _worldNetHandler, new Vector2(response.X, response.Y),
                CharacterConfigContainer.Get(response.ClassId), _configService.World, map);

            _cameraFollow.SetTarget(_localPlayerObject.transform);

            // Bám mượt là để đuổi theo người đang chạy; ở đây chưa có gì để đuổi, nên nhảy thẳng tới
            // nơi — không có dòng này thì frame đầu của world là cảnh camera bay từ gốc toạ độ tới.
            _cameraFollow.SnapToTarget();

            this.Log($"Vào map {response.MapId} tại {response.X:0.##}:{response.Y:0.##} - entity {response.EntityId}");
        }
```

```csharp
        private void OnEntitySpawn(EntitySpawnNotice notice)
        {
            // Gói về chính mình (nếu có) hoặc gói lặp — bỏ qua, không nhân bản.
            if (notice.EntityId == _localPlayer.EntityId || _remotes.ContainsKey(notice.EntityId))
                return;

            GameObject remote = Instantiate(_remotePrefab, new Vector3(notice.X, notice.Y, 0f), Quaternion.identity, _entityRoot);
            remote.name = $"Remote_{notice.EntityId}_{notice.Name}";

            var view = remote.GetComponent<RemotePlayerView>();

            // Bảng số tra từ ClassId của NGƯỜI KIA, không phải của mình: hai lớp nhân vật có thời
            // lượng hành động khác nhau, và người xem phải co clip theo bảng của người bị xem.
            view.Init(CharacterConfigContainer.Get(notice.ClassId));
            view.PushState(new Vector2(notice.X, notice.Y), notice.FacingLeft, notice.Crouching, notice.Action);

            _remotes[notice.EntityId] = view;
        }
```

```csharp
        private void OnMapChanged(MapChangedNotice notice)
        {
            if (_localPlayerObject == null)
                return;

            // Cùng ba dòng như lúc vào world — nạp LUẬT, dựng HÌNH, rồi mới đặt lại motor.
            MapGrid map = _mapService.Load(notice.MapId);

            _mapView.Show(map);

            _localPlayerObject.GetComponent<PlayerMotor>().SetMap(map, notice.State);

            // Cùng lý do như lúc vào world: sang map là một cú DỊCH CHUYỂN. Để camera bám mượt thì nó
            // lướt qua cả bản đồ mới trong nửa giây trước khi dừng đúng chỗ.
            _cameraFollow.SnapToTarget();

            // KHÔNG gọi DespawnAllRemotes(). Server đã gửi EntityDespawn cho từng người ở map cũ ngay
            // tick sau — dọn tay ở đây là đường thứ hai làm cùng một việc, và hai đường thì sớm muộn
            // lệch nhau. Chịu một tick ma đứng im, đổi lại một đường duy nhất cho mọi lý do biến mất.
            this.Log($"Sang map {notice.MapId} tại {notice.State.X:0.##}:{notice.State.Y:0.##}");
        }
```

Lưu ý thứ tự trong `SpawnLocalPlayer`: `_mapService.Load` chạy **trước** `Instantiate`, vì
`motor.Init` cần lưới va chạm ngay từ tick dự đoán đầu tiên.

</details>

<details>
<summary><b>📖 Lời giải — <code>PlayerMotor</code> nhận hai bộ số</b></summary>

**`Assets/Game/Scripts/World/PlayerMotor.cs`** — bốn chỗ. Thêm `using MMORPG.Shared.World;` ở đầu file.

**(1) Thêm field, cạnh `_characterConfig`:**

```csharp
        /// <summary>
        /// Bộ số của lớp nhân vật mình đang chơi. Client PHẢI dự đoán bằng đúng bảng server dùng —
        /// lệch một con số là lệch quỹ đạo, và reconciliation sẽ kéo giật liên tục mà không rõ vì sao.
        /// </summary>
        private CharacterConfig _characterConfig;

        /// <summary>
        /// Luật thế giới của PHIÊN này, chốt lúc Init. Giữ reference thay vì đọc ConfigService mỗi
        /// tick: server cũng chốt một bản cho entity lúc spawn, nên hai bên phải đóng băng ở cùng một
        /// thời điểm thì replay mới ra cùng kết quả.
        /// </summary>
        private WorldConfig _worldConfig;

        /// <summary>
        /// Lưới va chạm client dự đoán bằng. PHẢI là đúng lưới server đang chạy — hai bên đọc cùng
        /// một file nên chuyện đó được bảo đảm bằng cơ chế, không bằng trí nhớ.
        /// </summary>
        private MapGrid _map;
```

**(2) `Init` đổi chữ ký** — bỏ `int classId`, nhận hai bộ số đã tra sẵn:

```csharp
        /// <summary>
        /// Nhận CharacterConfig và WorldConfig đã tra sẵn thay vì tự đi tra: WorldSpawner mới là chỗ
        /// biết gói EnterWorld nói gì, và nó tra một lần rồi đưa xuống. Nhận cả hai bộ số cùng lúc
        /// cũng làm hiển nhiên một điều kiện dễ quên — cả hai phải đến từ CÙNG một gói EnterWorld.
        /// </summary>
        public void Init(WorldApi worldApi, WorldNetHandler worldNetHandler, Vector2 spawnPos,
            CharacterConfig characterConfig, WorldConfig worldConfig, MapGrid map)
        {
            _worldApi = worldApi;
            _worldNetHandler = worldNetHandler;

            _characterConfig = characterConfig;
            _worldConfig = worldConfig;
            _map = map;

            _simState = MoveState.AtRest(spawnPos.x, spawnPos.y);
            _prevSimState = _simState;

            // Animator cần cùng bảng đó, nhưng chỉ để co clip cho vừa thời lượng.
            _characterAnimator.Init(_characterConfig);

            _worldNetHandler.OnMoveStateResult += OnMoveStateResult;
        }
```

**(3) Bước dự đoán trong `Step`:**

```csharp
            _simState = MovementRules.Step(_simState, intent, MovementRules.TICK_DT, _worldConfig, _characterConfig, _map);
```

**(4) Vòng replay trong `OnMoveStateResult`** — **chỗ dễ quên nhất**, và quên thì triệu chứng là
RUNG ở sát tường chứ không phải "sai vị trí": dự đoán chặn, replay cho qua, mỗi gói `MoveState`
là một lần đổi ý.

```csharp
            foreach (PendingInput pending in _pending)
            {
                previous = state;

                // Vòng replay PHẢI dùng đúng map của bước dự đoán. Đây là chỗ dễ quên nhất trong cả
                // phase, và triệu chứng của việc quên không phải "sai vị trí" mà là RUNG ở sát tường:
                // dự đoán chặn, replay cho qua, mỗi gói MoveState là một lần đổi ý.
                state = MovementRules.Step(state, pending.Intent, MovementRules.TICK_DT,
                    _worldConfig, _characterConfig, _map);
            }
```

Không có chỗ nào khác trong client đọc `WorldConfig` — `RemotePlayerView` và
`CharacterStates.Derive` không cần nó, vì `Derive` chỉ so sánh dấu và không dùng hằng nào.

</details>

---

## Bước 5 — Giết hẳn con bug "DLL cũ"

### Hướng làm

Mở lại bảng Troubleshooting của Phase 8, 9, 10. Cả ba đều có cùng một dòng:

> *"Nhân vật đứng im hoàn toàn, không lỗi gì — Unity còn dùng DLL cũ, build `Shared` chưa copy sang
> `Assets/Plugins/Shared/`."*

Ba phase, cùng một bug, và mỗi lần đều mất thời gian vì **nó không có triệu chứng riêng**. Đã tới lúc
giết hẳn nó thay vì ghi chú nó thêm lần nữa.

Cách làm: `Shared` tự tính một **dấu vân tay của chính contract** bằng reflection — duyệt mọi giá trị
`NetCmd` và mọi kiểu `[MemoryPackable]` cùng danh sách property của chúng, băm lại thành một `uint`.
Client gửi số đó ngay sau khi nối; server so với số của mình; lệch thì trả lỗi rõ ràng rồi ngắt.

Ba điểm phải hiểu đúng:

- **Vì sao dùng reflection chứ không một `const VERSION` gõ tay.** Số gõ tay phải nhớ tăng, mà cái cần
  chống ở đây chính là *quên*. Một hằng số mà người ta quên tăng còn **tệ hơn không có**: nó tạo cảm
  giác đã được bảo vệ.
- **Nó bắt được gì.** Thêm/xoá/đổi số một `NetCmd`, thêm/xoá/đổi tên/đổi kiểu một property DTO — tức gần
  như mọi cách mà "client chạy DLL cũ" biểu hiện.
- **Nó KHÔNG bắt được gì.** Đổi *hành vi* mà không đổi *hình dạng*: sửa công thức trong `Step`, đổi thứ
  tự các phép trong đó, hay chỉnh một hằng còn sót — dấu vân tay y nguyên, mà hai bên đã mô phỏng khác
  nhau. Triệu chứng của loại ấy là rubber-band, nên ít nhất nó **có** triệu chứng.

> Một phép kiểm mà người dùng tưởng là toàn diện thì **nguy hiểm hơn không có**. Mọi lớp bảo vệ phải kèm
> một câu ghi rõ nó **không** bảo vệ cái gì.

**Bẫy riêng của Unity — phải biết trước.** Reflection quét `[MemoryPackable]` trong assembly `Shared`.
Trong Editor (Mono) mọi kiểu đều còn đó. Nhưng bản build IL2CPP có **managed code stripping**: một DTO mà
không dòng code client nào nhắc tên sẽ bị cắt khỏi assembly, và dấu vân tay của client tự nhiên khác
server — một lỗi **chỉ xuất hiện trong bản build**, tức loại đắt nhất. Hôm nay chưa build player nên
chưa gặp; ghi lại ở đây để ngày đó không mất buổi chiều. Cách chữa là `link.xml` giữ nguyên assembly
`MMORPG.Shared`, đặt cùng Phase 21.

**Luồng bắt tay:**

```
client nối TCP  ──► VersionCheck { ContractHash }  ──► server so
                ◄── VersionCheckResponse { Ok, ServerHash }
                    Ok=false → client hiện lỗi, ngắt, KHÔNG cho gõ login
```

**Chặn bằng kiểu, không bằng trí nhớ.** Thêm một bậc vào `SessionState`:

```csharp
Connected = 0,      // TCP đã nối, chưa biết là ai, chưa biết có cùng contract không
Verified = 1,       // đã qua kiểm phiên bản
Authenticated = 2,
InWorld = 3,
```

Vì dispatcher so bằng `>=`, mọi handler đang đặt `MinState = Authenticated` giữ nguyên nghĩa. Việc còn
lại là nâng `Register`/`Login`/`Echo`/`ServerInfo` lên `MinState = SessionState.Verified`, chừa đúng
`VersionCheck` (và `Ping`) ở `Connected`. Một client chưa qua cửa thì **không gõ được cửa nào khác** —
và đó là bảo đảm của dispatcher, không phải của người viết handler tiếp theo.

Và `HandshakeDto` từ Phase 0 — cái DTO thử mang comment "không còn luồng nào dùng, xoá được" — cuối cùng
cũng được thay bằng thứ có việc làm. Xoá nó.

### ✅ CHECKPOINT E — mục tiêu cuối Phase 12

1. Server boot in: `Contract hash=XXXXXXXX`. Client lúc nối in đúng số đó.
2. Thêm một giá trị bất kỳ vào `NetCmd`, build `Shared`, **khôi phục lại DLL cũ trong `Assets/Plugins/Shared/`**
   (giả lập đúng cảnh "quên copy"), chạy client → client báo **"phiên bản không khớp"** kèm hai số, và
   **không vào được màn hình login**.
3. Copy DLL đúng → vào bình thường.
4. Thử đường vòng: sửa client bỏ qua bước `VersionCheck`, gửi thẳng `Login` → server từ chối bằng
   `NotAuthenticated`. Phép chặn nằm ở dispatcher, không nằm ở thiện chí của client.
5. Chạy lại toàn bộ CHECKPOINT A → D — không cái nào được vỡ.

Bước (2) là phần thưởng lớn nhất của phase: một loại bug đã xuất hiện ở ba phase liên tiếp, mỗi lần đều
phải đoán, giờ tự báo tên nó ra.

<details>
<summary><b>📖 Lời giải — <code>Contract</code></b></summary>

**`Server/Shared/Net/Contract.cs`** (file mới):

```csharp
using System;
using System.Collections.Generic;
using System.Reflection;
using MemoryPack;
using MMORPG.Shared.World;

namespace MMORPG.Shared.Net
{
    /// <summary>
    /// Dấu vân tay của HÌNH DẠNG contract: mọi giá trị NetCmd + mọi DTO [MemoryPackable] và danh sách
    /// property của chúng. Hai bên tính từ cùng một assembly (MMORPG.Shared) nên hai số khác nhau chỉ
    /// có một nghĩa: hai bên đang chạy hai bản DLL khác nhau.
    ///
    /// Tính bằng reflection chứ không phải một const gõ tay, vì thứ cần chống chính là QUÊN tăng số.
    /// Một hằng số mà người ta quên tăng còn tệ hơn không có — nó tạo cảm giác đã được bảo vệ.
    ///
    /// GIỚI HẠN, phải biết: nó bắt đổi HÌNH DẠNG, không bắt đổi HÀNH VI. Sửa công thức trong
    /// MovementRules.Step thì số này y nguyên.
    /// </summary>
    public static class Contract
    {
        /// <summary>Tính một lần cho cả đời process — reflection không rẻ, mà kết quả thì không đổi.</summary>
        public static uint Hash { get; } = Compute();

        private static uint Compute()
        {
            uint hash = Fnv1a.START;

            // OrderBy theo tên, KHÔNG theo thứ tự reflection trả về: thứ tự ấy không được bảo đảm và
            // trên thực tế khác nhau giữa CoreCLR và Mono. Bỏ phép sắp xếp là hai bên ra hai số khác
            // nhau dù cùng một DLL — và bạn sẽ đi tìm lỗi ở chỗ không có lỗi.
            var cmdNames = new List<string>(Enum.GetNames(typeof(NetCmd)));
            cmdNames.Sort(StringComparer.Ordinal);

            foreach (string name in cmdNames)
            {
                hash = Fnv1a.Mix(hash, name);
                hash = Fnv1a.Mix(hash, (int)Enum.Parse(typeof(NetCmd), name));
            }

            var dtoTypes = new List<Type>();

            foreach (Type type in typeof(Contract).Assembly.GetTypes())
            {
                if (type.GetCustomAttribute<MemoryPackableAttribute>() != null)
                    dtoTypes.Add(type);
            }

            dtoTypes.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));

            foreach (Type type in dtoTypes)
            {
                hash = Fnv1a.Mix(hash, type.FullName);

                var properties = new List<PropertyInfo>(
                    type.GetProperties(BindingFlags.Public | BindingFlags.Instance));

                properties.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

                foreach (PropertyInfo property in properties)
                {
                    hash = Fnv1a.Mix(hash, property.Name);

                    // Băm cả KIỂU: đổi int thành long mà giữ nguyên tên là đổi số byte trên dây, và
                    // đó đúng là loại lệch im lặng nhất — gói vẫn giải mã được, chỉ là ra số khác.
                    hash = Fnv1a.Mix(hash, property.PropertyType.FullName);
                }
            }

            return hash;
        }
    }
}
```

**`Server/Shared/Dto/SystemDto.cs`** — thêm cặp DTO, xoá `HandshakeDto.cs`:

```csharp
    [MemoryPackable]
    public partial class VersionCheckRequest
    {
        public uint ContractHash { get; set; }
    }

    [MemoryPackable]
    public partial class VersionCheckResponse
    {
        public bool Ok { get; set; }

        /// <summary>
        /// Gửi cả số của server dù client không cần để quyết định gì: nó là thứ người ta dán vào
        /// báo lỗi. "Phiên bản không khớp" không giúp ai; "client A1B2C3D4, server E5F6A7B8" thì có.
        /// </summary>
        public uint ServerHash { get; set; }
    }
```

**`Server/GameServer/SessionState.cs`** — chèn một bậc vào GIỮA, không thêm vào cuối:

```csharp
    public enum SessionState
    {
        /// <summary>TCP đã nối, chưa biết là ai, và chưa biết có cùng contract không.</summary>
        Connected = 0,

        /// <summary>
        /// Đã qua kiểm phiên bản. Bậc này nằm GIỮA Connected và Authenticated chứ không thêm vào cuối:
        /// dispatcher so bằng >=, nên thứ tự các bậc LÀ luật. Thêm vào cuối thì "đã vào world" không
        /// còn hàm ý "đã kiểm phiên bản".
        /// </summary>
        Verified = 1,

        /// <summary>Đã đăng nhập, chưa vào thế giới.</summary>
        Authenticated = 2,

        /// <summary>Đã vào thế giới, đang điều khiển một entity.</summary>
        InWorld = 3,
    }
```

Đổi số của `Authenticated` và `InWorld` là an toàn vì enum này **chỉ sống trong RAM của GameServer** —
nó không đi trên dây, không xuống DB. Đối chiếu với `NetCmd`: ở đó đánh số lại là đổi contract.

**`Server/GameServer/ClientSession.cs`** — thêm cạnh ba hàm `Mark*` đã có:

```csharp
        /// <summary>
        /// Contract khớp. Không mang theo dữ liệu gì — nó chỉ mở cửa cho các lệnh khác.
        ///
        /// Không có đường lùi: một session đã kiểm xong thì kiểm lại cũng vô nghĩa, và MarkLoggedOut
        /// cố tình hạ về Verified chứ không về Connected — xem bên dưới.
        /// </summary>
        public void MarkVerified()
        {
            State = SessionState.Verified;
        }
```

Và `MarkLoggedOut` phải sửa: nó đang đặt `State = SessionState.Connected`, tức là **đăng xuất xong thì
mất luôn quyền đăng nhập lại** vì `Login` giờ cần `MinState = Verified`. Đổi thành:

```csharp
        public void MarkLoggedOut()
        {
            AccountId = 0;
            Username = string.Empty;

            // Verified chứ không Connected: đăng xuất là quên DANH TÍNH, không phải quên kết quả kiểm
            // phiên bản. Hạ về Connected thì người chơi bấm Đăng xuất xong không đăng nhập lại được,
            // và lỗi trả về là NotAuthenticated — một thông điệp chỉ sai hướng hoàn toàn.
            State = SessionState.Verified;
        }
```

**`Server/GameServer/Handlers/SystemHandler.cs`** — handler mới, và nâng bậc cho các lệnh cũ:

```csharp
        [TcpHandler(NetCmd.Echo, MinState = SessionState.Verified)]
        // ... và tương tự cho ServerInfo. Ping thì để nguyên ở Connected: đo độ trễ không cần biết
        // contract, và nó là thứ hữu ích nhất khi mọi thứ khác đang bị từ chối.
```

```csharp
        [TcpHandler(NetCmd.VersionCheck)]
        public static Task<NetResult> OnVersionCheck(NetRequest req)
        {
            var request = req.GetData<VersionCheckRequest>();
            bool ok = request.ContractHash == Contract.Hash;

            if (ok)
            {
                req.Session.MarkVerified();
            }
            else
            {
                Log.Warn($"{req.Session.Tag} Contract lệch: client {request.ContractHash:X8} " +
                         $"≠ server {Contract.Hash.ToString("X8").Red()}");
            }

            return Task.FromResult(NetResult.Ok(new VersionCheckResponse
            {
                Ok = ok,
                ServerHash = Contract.Hash,
            }));
        }
```

</details>

<details>
<summary><b>📖 Lời giải — <code>NetCmd</code>, <code>SystemNetHandler</code>, <code>LoginPresenter</code></b></summary>

**`Server/Shared/Net/NetCmd.cs`** — thêm một giá trị vào **cuối dải Hệ thống**, không chèn giữa:

```csharp
        /// <summary>
        /// Đối chiếu vân tay contract ngay sau khi nối. Client chủ động gửi trước mọi lệnh khác.
        /// Request/Response: <see cref="Dto.VersionCheckRequest"/> / <see cref="Dto.VersionCheckResponse"/>
        /// </summary>
        VersionCheck = 6,
```

**`Assets/Game/Scripts/Network/Handlers/SystemNetHandler.cs`** — thêm một event và một handler:

```csharp
        /// <summary>
        /// Kết quả kiểm phiên bản contract. Lệnh ĐẦU TIÊN của mọi phiên: tới khi nó về thì session
        /// còn ở bậc Connected và server từ chối gần như mọi lệnh khác.
        /// </summary>
        public event Action<VersionCheckResponse> OnVersionCheck;

        [NetHandler(NetCmd.VersionCheck)]
        private void HandleVersionCheck(NetPacket packet)
        {
            var response = packet.GetData<VersionCheckResponse>();

            // Log ngay tại đây chứ không để chỗ nghe event lo: hai con số này là thứ người ta dán vào
            // báo lỗi, và nó phải có mặt kể cả khi chưa ai đăng ký event.
            if (response.Ok)
                this.Log($"Contract khớp: {Contract.Hash:X8}");
            else
                this.LogError($"Contract LỆCH — client {Contract.Hash:X8} ≠ server {response.ServerHash:X8}. " +
                              "Build lại Server/Shared để DLL trong Assets/Plugins/Shared/ khớp server.");

            OnVersionCheck?.Invoke(response);
        }
```

`SystemNetHandler` **đã** có trong `GameLifetimeScope` từ Phase 1 — không thêm dòng đăng ký nào.

**`Assets/Game/Scripts/Auth/LoginPresenter.cs`** — năm chỗ.

**(1) Thêm ba `using`:**

```csharp
using MMORPG.Shared.Dto;
using MMORPG.Shared.Net;
```

(`MMORPG.Shared.Dto` cho `VersionCheckRequest/Response`, `MMORPG.Shared.Net` cho `NetCmd` và `Contract`.)

**(2) Nhận thêm `SystemNetHandler`, và một field nhớ kết quả:**

```csharp
        private NetService _netService;
        private AuthApi _authApi;
        private AuthNetHandler _authNetHandler;
        private SystemNetHandler _systemNetHandler;
        private NetworkSettings _networkSettings;
        private SavedLoginStore _savedLoginStore;
        private CancellationTokenSource _responseTimeout;

        /// <summary>
        /// Kết quả kiểm contract của lần nối hiện tại. null = chưa hỏi hoặc chưa về.
        ///
        /// Đặt ở đây chứ không ở NetService: nó là trạng thái của một LẦN NỐI, và màn hình login là
        /// thứ duy nhất phải phản ứng với nó. Ngày có màn hình khác cần biết thì đẩy xuống NetService.
        /// </summary>
        private bool? _contractOk;

        [Inject]
        public void Construct(NetService netService, AuthApi authApi, AuthNetHandler authNetHandler,
            SystemNetHandler systemNetHandler, NetworkSettings networkSettings, SavedLoginStore savedLoginStore)
        {
            _netService = netService;
            _authApi = authApi;
            _authNetHandler = authNetHandler;
            _systemNetHandler = systemNetHandler;
            _networkSettings = networkSettings;
            _savedLoginStore = savedLoginStore;
        }
```

**(3) Đăng ký / gỡ event** — thêm một dòng vào `Awake` và một dòng vào `OnDestroy`:

```csharp
            _systemNetHandler.OnVersionCheck += OnVersionCheck;   // trong Awake
            _systemNetHandler.OnVersionCheck -= OnVersionCheck;   // trong OnDestroy
```

**(4) Chặn ở `SubmitAsync`**, ngay sau khối kết nối:

```csharp
            // Kiểm phiên bản TRƯỚC khi gửi bất cứ lệnh nào khác. Server đặt Register/Login ở
            // MinState = Verified, nên bỏ qua bước này là mọi lệnh bị từ chối bằng NotAuthenticated —
            // một thông điệp chỉ sai hướng hoàn toàn.
            if (!await EnsureContractAsync())
            {
                _loginUi.SetInteractable(true);
                return;
            }
```

**(5) Hai hàm mới**, đặt trước `OnKicked`:

```csharp
        /// <summary>
        /// Gửi <c>VersionCheck</c> nếu chưa hỏi lần nào cho kết nối này, rồi chờ kết quả.
        ///
        /// Chờ bằng cách đợi <see cref="_contractOk"/> đổi khỏi null thay vì bằng một TaskCompletionSource:
        /// handler chạy ở main thread (NetDispatcher bảo đảm), nên vòng UniTask.WaitUntil ở main thread
        /// nhìn thấy nó ngay khi gói về, và không có chuyện hai luồng cùng đụng một biến.
        /// </summary>
        private async UniTask<bool> EnsureContractAsync()
        {
            if (_contractOk == true)
                return true;

            if (_contractOk == null)
            {
                _loginUi.ShowMessage("Đang kiểm phiên bản...", isError: false);
                _netService.Send(NetCmd.VersionCheck, new VersionCheckRequest { ContractHash = Contract.Hash });

                bool timedOut = await UniTask
                    .WaitUntil(() => _contractOk != null)
                    .Timeout(TimeSpan.FromSeconds(RESPONSE_TIMEOUT_SECONDS))
                    .SuppressCancellationThrow();

                if (timedOut)
                {
                    _loginUi.ShowMessage("Máy chủ không phản hồi. Thử lại sau giây lát.", isError: true);
                    return false;
                }
            }

            if (_contractOk == false)
            {
                // Không cho gõ tiếp: mọi lệnh sau đây đều sẽ bị server từ chối, và để người chơi thử
                // đi thử lại một việc không bao giờ thành công là tệ hơn một thông báo dứt khoát.
                _loginUi.ShowMessage("Phiên bản game không khớp máy chủ. Cập nhật lại bản mới.", isError: true);
                return false;
            }

            return true;
        }

        private void OnVersionCheck(VersionCheckResponse response)
        {
            _contractOk = response.Ok;
        }
```

**Vì sao `_contractOk` là `bool?` chứ không hai `bool`:** ba trạng thái thật sự khác nhau — *chưa hỏi*,
*hỏng*, *khớp* — và `bool?` diễn đạt đúng ba, trong khi hai `bool` rời (`_checked`, `_ok`) cho phép một
tổ hợp vô nghĩa (`_checked = false, _ok = true`) mà không ai chặn.

**Vì sao không tự gửi `VersionCheck` ngay trong `NetService.ConnectAsync`:** vì lúc đó chưa ai sẵn sàng
nghe kết quả, và `NetService` là **hạ tầng** — nó không nên biết game có luật "phải kiểm phiên bản
trước". Đặt ở `LoginPresenter` thì luật ấy nằm ở chỗ nó có nghĩa, và chỗ đó cũng chính là nơi có UI để
báo lỗi.

</details>

---

## Bốn thử nghiệm bắt buộc

**1. Config rác.**
Ghi `"Gravity": "nặng lắm"` → restart: server sống, log Warn, dùng mặc định. Ghi JSON hỏng hẳn (thiếu
một dấu `}`): như trên. Ghi `"MaxFallSpeed": 60` → Warn về đúng trường đó. Ghi `"Gravty": 30` → báo
trường lạ.

Server không bao giờ được chết vì file người vận hành gõ tay — nhưng cũng không bao giờ được **im lặng**
chạy bằng số khác với số họ tưởng.

**2. Client cứng đầu.**
Sửa tạm client bỏ qua `response.World`, dự đoán bằng một `WorldConfig` tự chế với `Gravity = 10` →
rubber-band liên tục theo chiều dọc, server thắng. Kết luận của Phase 6 vẫn nguyên giá trị khi số đã
thành dữ liệu. Trả lại code.

**3. Đo cái gì thật sự đổi khi hot reload.**
Hai client online. Sửa `Gravity` rồi gõ `R`. Ghi lại: (a) log server in số mới; (b) hai người đang chơi
**không đổi gì**; (c) một người relog thì chỉ người đó đổi — **hai người chơi hai bộ số khác nhau trong
cùng một thế giới, và không ai rubber-band**, vì ai cũng dự đoán bằng đúng bộ server dùng cho mình.

Rồi thử ngược lại: sửa `PlayerEntity.Integrate` cho nó đọc `ConfigService.Current` mỗi tick, gõ `R`, và
xem cả hai người giật cùng lúc. Đây là cách nhanh nhất để hiểu vì sao "tươi hơn" không phải lúc nào cũng
đúng hơn. Trả lại code.

**4. Thêm một bảng giả, đo bằng số dòng phải gõ.**
Đây là phép thử cho thiết kế của Bước 3, và nó là phép thử **rẻ nhất** trong cả phase.

Dựng một bảng loại B giả, tối thiểu: `Assets/Game/Resources/Config/test.json` với `{ "Version": 1, "Rows": [ { "Id": 1 } ] }`,
một `TestConfig`, một `TestTableData : IConfigFile`, một `TestConfigContainer`. Rồi đếm xem để server
nạp được nó cần thêm bao nhiêu dòng trong `ConfigService`.

Đáp án phải là **một** (dòng `LoadTable<TestTableData>(...)`) **cộng một hàm `ValidateTest` rỗng**. Nếu
bạn thấy mình đang chép lại cả một hàm nạp thì `LoadFile<T>` chưa đủ generic — sửa nó trước khi sang
Phase 13, vì Phase 13 mở đầu bằng đúng việc này với bảng item.

Xoá bảng giả đi sau khi đếm xong.

---

## Troubleshooting

| Triệu chứng | Nguyên nhân thường gặp | Chỗ sửa |
|---|---|---|
| **`InvalidOperationException: CharacterConfigContainer chưa được Load` ở client** | **quên Bước 4** — client chưa gọi `CharacterConfigContainer.Load` bao giờ | `Config/ConfigService.Apply`, và nó phải được gọi trong `WorldPresenter.OnEnterWorldResult` **trước** `SpawnLocalPlayer` |
| **Vào world xong không thấy nhân vật nào, console có một exception** | như trên — `PlayerMotor.Init` ném ngay dòng đầu nên `Instantiate` xong mà không ai chạy được | như trên |
| **`VContainerException: No such registration of type: ConfigService`** | quên dòng `builder.Register<Config.ConfigService>(...)` | `GameLifetimeScope.Configure`. Đọc **dòng cuối** của chuỗi `Failed to resolve` |
| **`InvalidOperationException: Chưa đăng ký XService`** khi gói tin tới | quên một dòng `ServerServices.Register` | `ServerBootstrap.Build()` — thông điệp lỗi đã nói đúng chỗ |
| **Server chết ngay khi chạy không có console** (dịch vụ, Docker, `< nul`) | `Console.ReadKey` ném ở luồng riêng, không ai bắt | thêm `if (Console.IsInputRedirected) return;` đầu luồng phím |
| Server báo không đọc được config dù file có | chạy từ thư mục khác, hoặc csproj chưa copy | kiểm `bin/Debug/net8.0/Data/Config/game.json` có tồn tại |
| Build xong mà `Data/Config/` rỗng | chưa tạo `Config/` ở **gốc repo** — glob không khớp file nào thì MSBuild im lặng | tạo thư mục + file; thêm target `CheckConfigFolder` giống `CheckMapFolder` để lần sau nó thành lỗi build |
| Phím `R` không có tác dụng, phím `H`/`K`/`J` cũng không | khối `new Thread(...)` đặt **sau** vòng `while … AcceptTcpClientAsync` nên chỉ chạy lúc server tắt | chuyển cả khối lên **trước** vòng accept |
| Handler nhận `NullReferenceException` ở service | dùng `private static readonly XService X = ServerServices.Get<...>()` — field static khởi tạo **trước** `ServerBootstrap.Build()` | đổi sang property `=>` |
| Mọi hành động kết thúc ngay tick sau (đánh không ra đòn) | quên `CharacterConfig.Prepare()`, hoặc `Prepare()` chạy trên **bản copy** của struct vì dùng `foreach` thay `for` | `CharacterConfig.Prepare` |
| Đăng xuất xong không đăng nhập lại được, lỗi `NotAuthenticated` | `MarkLoggedOut` hạ `State` về `Connected` trong khi `Login` cần `Verified` | `ClientSession.MarkLoggedOut` |
| Sửa json mà số không đổi | đang sửa file ở gốc repo, nhưng bản copy trong `bin/` chỉ cập nhật lúc **build** — `R` đọc bản trong `bin/` | build lại, hoặc sửa thẳng bản trong `bin/` khi thử nhanh |
| `JsonSerializationException: Could not find member` | đúng chủ đích — `MissingMemberHandling.Error`. Tên trường gõ sai | đối chiếu tên với `GameConfigData` / `CharacterTableData` |
| Server boot báo `Bảng rỗng` dù file có dữ liệu | tên mảng trong file không khớp property (`"Clases"` thay `"Classes"`) — Newtonsoft ném trước, `RowCount` không bao giờ được hỏi | đối chiếu tên mảng |
| Rubber-band dọc sau khi đổi `Gravity` | client còn chỗ dùng `WorldConfig` cũ — hay gặp nhất là **vòng replay** trong `OnMoveStateResult` | tìm **mọi** lời gọi `Step` trong `PlayerMotor`, phải có đúng hai |
| Nhân vật RUNG khi đứng sát tường | đúng triệu chứng của việc chỉ sửa một trong hai chỗ gọi `Step`: dự đoán chặn, replay cho qua | như trên |
| Người online bị giật ngay khi bấm `R` | `Integrate` đọc `ConfigService.Current` mỗi tick thay vì `_world` chốt lúc dựng | `PlayerEntity` |
| Hoạt ảnh đòn đánh không khớp thời lượng luật | client chưa nạp bảng nhân vật | `Config/ConfigService.LoadTables` có dòng `LoadTable<CharacterTableData>` không |
| Nhân vật kẹt cứng không đi được sau khi sửa `characters.json` | `BodyHalfWidth >= 0.5` — thân rộng hơn khe 1 ô | `ValidateCharacters`, và đọc dòng Warn |
| Client vẽ map khác server (tường vô hình) mà file map vừa export | quên build lại GameServer → bản trong `Data/Maps` còn cũ | so hai dòng log map của `MapRegistry` và `MapService` |
| Hai dòng log map lệch dai dẳng dù đã build | còn file map **cũ** sót trong `bin/.../Data/Maps` (đổi tên file thì `CopyToOutputDirectory` không xoá bản cũ) | `dotnet clean Server/GameServer` |
| Vân tay bảng hai bên lệch dù cùng file | quên build lại GameServer sau khi sửa file trong Resources — bản trong `bin/` chỉ cập nhật lúc build | build lại, rồi so lại hai dòng log |
| Vân tay lệch dai dẳng dù vừa build | một bên băm TRƯỚC `load()`, bên kia SAU | cả hai phải băm sau `load()` — xem `ConfigFingerprintTests` |
| `Get(id)` ném `CharacterConfigContainer chưa được Load` — **chỉ ở client** | quên một dòng `LoadTable` trong `Client/Config/ConfigService.LoadTables()`, hoặc file không nằm dưới một thư mục `Resources/` | `LoadTables()` |
| `Contract.Hash` hai bên khác nhau dù cùng DLL | đang duyệt theo thứ tự reflection trả về thay vì `Sort` theo tên | `Contract.Compute` |
| Client báo contract không khớp mà DLL vừa copy | Unity chưa nạp lại DLL | đợi biên dịch xong, hoặc Reimport `Assets/Plugins/Shared/` |
| Bản build player báo contract lệch, Editor thì không | IL2CPP stripping đã cắt mất DTO không được code client nhắc tên | `link.xml` giữ assembly `MMORPG.Shared` (Phase 21) |

---

## Tự kiểm tra hiểu bài

Tự trả lời từng câu xong mới mở đáp án của câu đó.

**Câu 1.** Vì sao "client và server cùng đọc chung một file config" nghe giống contract một nguồn nhưng
thực ra là bẫy? Nêu hai kịch bản cụ thể nó hỏng.
<details>
<summary><b>📖 Đáp án câu 1</b></summary>

Vì thứ cần đồng bộ là **giá trị đang chạy**, không phải nội dung file. (1) Client build ra mang bản copy
tại thời điểm build — server sửa file xong, mọi client ngoài kia vẫn chạy số cũ, lệch mà không ai báo;
(2) file nằm trong máy người chơi thì người chơi sửa được — với giá trị tham gia dự đoán là họ tự gây
rubber-band rồi đi report "game lag".

Server phát giá trị qua mạng thì cả hai kịch bản biến mất **về mặt cấu trúc**, không cần ai kỷ luật.

</details>

**Câu 2.** `WorldConfig` chốt vào `PlayerEntity` lúc dựng thay vì `Integrate` đọc `ConfigService.Current`
mỗi tick — "tươi" hơn cơ mà?
<details>
<summary><b>📖 Đáp án câu 2</b></summary>

Vì bên kia đầu dây có một client đang **dự đoán bằng bộ số nó nhận lúc vào world**. Server đổi số giữa
phiên thì mọi dự đoán của người đang online lệch ngay lập tức → rubber-band hàng loạt, và họ không làm
gì sai cả.

Bộ số là một phần của **hợp đồng phiên chơi**: chốt lúc vào, muốn đổi thì phải có cơ chế thông báo —
chưa làm cơ chế đó thì chưa được đổi ngầm. Luật: hot reload áp dụng cho người vào sau.

Bên lề: vì đã gom thành một object, "chốt theo phiên" là giữ **một reference**; thêm số mới không phải
nhớ chép thêm, và cũng không thể quên.

</details>

**Câu 3.** Config hỏng → server dùng mặc định và chạy tiếp, trong khi `CLAUDE.md` cấm nuốt lỗi. Biện
minh — và điểm nào trong cách xử lý là **bắt buộc** để nó không thành nuốt lỗi?
<details>
<summary><b>📖 Đáp án câu 3</b></summary>

Lựa chọn thật là: chết ngay lúc boot vì một dấu phẩy, hay đứng dậy bằng bộ giá trị an toàn đã biết. Với
dữ liệu do người vận hành gõ tay, phương án hai đúng hơn — *miễn là* (1) chỉ bắt **đúng loại lỗi dự
kiến** (`IOException`, `JsonException`; bug trong code vẫn phải ném lên), và (2) **log Warn to rõ, kèm
tên trường bị từ chối và giá trị thay thế**.

Nuốt lỗi bị cấm là nuốt *không dấu vết, không chủ đích*. Đây là xử lý **có chính sách**: hỏng cái gì,
thay bằng cái gì, và ai được biết.

</details>

**Câu 4.** Hot reload thay nguyên object thay vì sửa từng field trên object đang dùng. Cơ chế nào làm
cách này an toàn đa luồng mà không cần lock?
<details>
<summary><b>📖 Đáp án câu 4</b></summary>

Gán một reference là thao tác **nguyên tử** — luồng khác hoặc thấy trọn object cũ, hoặc trọn object mới,
không bao giờ thấy nửa nọ nửa kia; object cũ thì không ai sửa nữa từ lúc phát hành, nên ai đang cầm cứ
dùng tiếp một bộ giá trị nhất quán.

Sửa từng field trên object sống thì luồng tick có thể đọc `Gravity` mới ghép với `MaxFallSpeed` cũ — một
tổ hợp **chưa từng tồn tại trong bất kỳ file nào**, tức một trạng thái không tái hiện được.

Hệ quả thực hành: đọc `Current` ra **biến cục bộ** rồi dùng, đừng đọc nó hai lần trong cùng một phép
tính — hai lần đọc có thể rơi vào hai bộ khác nhau, và bạn vừa dựng lại đúng cái bug vừa tránh.

</details>

**Câu 5.** `Gravity` vào config còn `TICK_RATE` thì không. Ranh giới ở đâu, và điều gì gãy nếu người vận
hành đổi `TICK_RATE` từ 20 thành 30 trong file?
<details>
<summary><b>📖 Đáp án câu 5</b></summary>

Ranh giới: `Gravity` là **số liệu game** (đổi nó là đổi trải nghiệm), `TICK_RATE` là **hằng số của giao
thức** (đổi nó là đổi cách hai bên nói chuyện) — cùng đẳng cấp với format khung gói tin.

Đổi nó thì `TICK_DT` ăn theo, và toàn bộ prediction/reconciliation xây trên giả định hai bên **cùng
nhịp**: client bơm input 20 bước/giây trong khi server tiêu 30 tick/giây, replay của client tính mỗi
input một `TICK_DT` khác server → dự đoán lệch có hệ thống, rubber-band toàn dân.

Cùng lý do với `EDGE` và `CELL_SIZE`: chúng là số của **thuật toán**, không phải của game. Thứ như vậy
phải đổi bằng một build có chủ đích ở cả hai phía.

</details>

**Câu 6.** `MaxFallSpeed` bị `Validate` chặn ở `CELL_SIZE / TICK_DT`. Đây là sở thích hay ràng buộc? Nếu
vượt thì cái gì gãy, ở đâu?
<details>
<summary><b>📖 Đáp án câu 6</b></summary>

Ràng buộc, và nó đến từ một phase khác. Va chạm dọc ở Phase 10 chống tunneling bằng cách **quét từng
hàng ô** mà chân đi qua trong một tick; phép quét ấy đúng vì quãng rơi mỗi tick không vượt cạnh một ô —
`20 unit/s × 0.05s = 1.00`, sát kịch trần. Nâng lên 40 thì mỗi tick rơi 2 ô, và một tấm bệ dày 1 ô có
thể nằm trọn giữa hai lần kiểm: **rơi xuyên sàn, ngẫu nhiên, chỉ khi rơi từ đủ cao**.

`MoveSpeed` và `JumpSpeed` có cùng ràng buộc mà lý do còn thẳng hơn: hai trục ấy chỉ kiểm **điểm cuối**,
nên đi hơn một ô mỗi tick là xuyên thẳng qua tường mỏng.

Bài học rộng hơn: **config là bề mặt điều khiển mà người vận hành chạm vào, nên mọi giả định ngầm của
thuật toán phải được kiểm ở đó.** Trước khi ra file, `MAX_FALL_SPEED = 20` là một hằng số nằm cạnh đoạn
code dựa vào nó. Sau khi ra file, nó thành một ô trống mời người ta điền — và giả định ngầm kia không đi
theo. Viết lý do vào `Validate` là cách duy nhất để nó đi theo.

</details>

**Câu 7.** Cách "server gửi cả bảng trong `EnterWorld`" nghe an toàn hơn hẳn — không có bản thứ hai thì
không lệch được. Vì sao dự án vẫn chọn hai bản, mỗi bên đọc file của mình?
<details>
<summary><b>📖 Đáp án câu 7</b></summary>

Vì "không lệch được" là thứ **duy nhất** nó mua, và nó trả bằng ba thứ đắt dần theo thời gian:

1. **Gói login phình theo số bảng.** Hôm nay hai bảng; Phase 13 thêm item, Phase 14 thêm chỉ số,
   Phase 15 thêm quái + drop. Với nội dung thật thì đó là hàng trăm KB **mỗi lần đăng nhập**, cho một
   thứ client đã có sẵn trong bản build của nó.
2. **Client không biết gì trước khi vào world.** Không hiện được tên/icon item ở cửa hàng màn hình
   chờ, không dựng được màn hình chọn nhân vật. Dữ liệu tĩnh mà chỉ có sau `EnterWorld` là một ràng
   buộc tự chuốc lấy.
3. **Phase 18 thành đổi kiến trúc thay vì đổi chỗ chứa file.** Dữ liệu tĩnh đáng đi CDN; đi qua socket
   game server là tốn băng thông đắt nhất của hệ thống cho thứ rẻ nhất để phân phối.

Và cái "an toàn" kia hoá ra mua được bằng cách khác, rẻ hơn: **một nguồn duy nhất trên đĩa + build
tool copy**. Bản đồ đã làm đúng thế từ Phase 10 — bước này chỉ áp cùng khuôn cho bảng nhân vật thay
vì phát minh một đường thứ hai.

Còn một cái giá thứ tư mà cột trái không trả nổi bằng cách nào: **hai bên không bao giờ nạp cùng một
tập bảng.** Server có bảng tỉ lệ rơi đồ và bảng AI quái mà client không được phép biết; client có
bảng thoại mà server không dùng. "Gửi cả bảng" bắt buộc phải chọn gửi cái gì, và mọi cách chọn đều
sai với một trong hai bên.

Đáng nhớ nhất: **bệnh của vo-lam-genz không phải là có hai bản.** Bệnh là không ai kiểm hai bản có
khớp không. Nhưng cách chữa **không** phải là một phép so danh sách vân tay trong gói `EnterWorld` —
đó là bản thiết kế đầu tiên của doc này, và nó sai vì đúng lý do ở đoạn trên. Cách chữa thật có hai
tầng: hôm nay là **mỗi bên in vân tay ra log** (rẻ, và đủ để nhận ra trong ba mươi giây), Phase 18 là
**một số phiên bản cho cả gói dữ liệu, kiểm lúc đăng nhập** — đúng chỗ mọi MMO thật đặt nó, tức là ở
trình patch chứ không ở giao thức game.

Ngoại lệ đúng chiều ngược lại là `WorldConfig` (loại A): năm con số người vận hành chỉnh **giữa hai
lần restart server, không qua patch client**. Client không thể có sẵn, nên server phải gửi. Ranh giới
không phải "to hay nhỏ" mà là **"client có ship được thứ này trong bản build của nó không"**.

</details>

**Câu 8.** Đổi `StartingMapId` / điểm spawn trong config, người chơi cũ vào lại vẫn đứng chỗ cũ của họ.
Bug hay tính năng? Nó tiết lộ gì về hai loại dữ liệu trong bảng `character`?
<details>
<summary><b>📖 Đáp án câu 8</b></summary>

Tính năng. Spawn config là giá trị **khởi tạo** — chỉ dùng đúng một lần lúc *tạo* nhân vật; từ đó vị trí
là **trạng thái của người chơi**, thuộc về họ, lưu trong DB, và config không có quyền đè.

Ranh giới này — *giá trị khởi tạo* vs *trạng thái tích luỹ* — chính là ranh giới giữa "dữ liệu game
design" và "dữ liệu người chơi". Nhầm bên là hoặc reset đồ người ta, hoặc không tài nào cân bằng lại
game được.

Đối chiếu với `ResolveSpawnY` và `MapRegistry.ResolveFor` ở Phase 10 để thấy ranh giới ấy không tuyệt
đối: vị trí là của người chơi, nhưng nếu nó rơi vào chỗ **không hợp lệ** (trong tường, ở một map đã bị
xoá) thì server vẫn phải sửa. "Không đè" nghĩa là không đè vì lý do cân bằng, chứ không phải không bao
giờ chạm vào.

</details>

**Câu 9.** `Contract.Hash` bắt được loại lệch nào, **không** bắt được loại nào, và vì sao việc biết giới
hạn ấy lại quan trọng?
<details>
<summary><b>📖 Đáp án câu 9</b></summary>

**Bắt được:** đổi hình dạng contract — thêm/xoá/đổi số một `NetCmd`, thêm/xoá/đổi tên/đổi kiểu một
property DTO. Đó là gần như mọi cách mà "client chạy DLL cũ" biểu hiện, và nó là bug đã tốn thời gian ở
ba phase liên tiếp vì **không có triệu chứng riêng**.

**Không bắt được:** đổi **hành vi** mà không đổi hình dạng — sửa công thức trong `MovementRules.Step`,
đổi thứ tự các phép, chỉnh một hằng còn sót trong code. Hash y nguyên, mà hai bên đã mô phỏng khác nhau.
Triệu chứng của nó là rubber-band, nên ít nhất nó **có** triệu chứng.

Vì sao phải biết: một phép kiểm mà người dùng tưởng là toàn diện thì **nguy hiểm hơn không có** — gặp
rubber-band, họ sẽ loại trừ "lệch phiên bản" ngay từ đầu vì "đã có contract hash rồi" và đi tìm sai
hướng.

</details>

**Câu 10.** Vì sao vòng đọc phím phải nằm ở luồng riêng chứ không nhét vào vòng `accept` như code cũ, và
vì sao dùng `Thread` chứ không `Task.Run`?
<details>
<summary><b>📖 Đáp án câu 10</b></summary>

`Console.ReadKey` **chặn luồng** cho tới khi có phím. Đặt nó trong vòng accept thì mỗi vòng lặp đứng ở
đó, và không client nào nối được cho tới khi bạn gõ một phím — server trông như treo. Đó chính là lý do
đoạn code cũ bị comment lại.

`Thread` chứ không `Task.Run` vì đây là **blocking I/O**, không phải việc CPU. `Task.Run` mượn một worker
của thread pool, và một worker bị chặn vĩnh viễn là một worker biến mất khỏi pool — pool ấy còn phải
phục vụ mọi `await` khác của server. Thread riêng, `IsBackground = true` để nó không giữ process sống
lúc thoát, là đúng công cụ.

</details>

**Câu 11.** `ServerServices` là service locator, và service locator nổi tiếng là anti-pattern. Vì sao
dự án này vẫn dùng, và luật nào giữ nó không lan ra?
<details>
<summary><b>📖 Đáp án câu 11</b></summary>

Nhược điểm của service locator là thật: nhìn chữ ký hàm **không biết nó cần gì**, phải đọc thân hàm.
Constructor injection không có nhược điểm đó, và vì thế nó vẫn là mặc định của dự án.

Nhưng handler của server là hàm **`static`** — `TcpDispatcher` gọi chúng qua reflection, không có chỗ
nào nhét constructor vào. Đó là một ràng buộc thật, không phải sự lười. Ba phương án còn lại đều tệ hơn:
(a) mỗi handler một `public static XService { get; set; }` gán tay — chính cái vừa bỏ, quên một dòng là
`NullReferenceException` không có lỗi biên dịch; (b) đổi handler thành instance và kéo cả một container
vào — nhiều máy móc hơn hẳn cho một dự án học; (c) truyền mọi service qua `NetRequest` — biến contract
của dispatcher thành danh sách mọi service hiện có.

**Luật giữ nó không lan:** dùng `ServerServices.Get<T>()` ở **đúng một biên giới** — bên trong thân
handler `static`. Mọi service khác nhận phụ thuộc qua **constructor** và không bao giờ gọi `Get<T>()`.
Vi phạm luật đó là ném đi thứ duy nhất constructor injection cho ta.

Và có một mẹo làm nhược điểm bớt đau: đặt `private static XService XService => ServerServices.Get<...>();`
ngay **dòng đầu class**. Nhìn đầu file là biết handler nhóm này cần gì — không bằng chữ ký hàm, nhưng
gần bằng.

</details>

**Câu 12.** `ConfigService.LoadTable<T>` nhận ba tham số: tên file, hàm validate, hàm load. Vì sao đúng
ba, và vì sao `Validate` **không** được generic hoá chung vào đó?
<details>
<summary><b>📖 Đáp án câu 12</b></summary>

Ba, vì đó đúng là ba thứ khác nhau giữa các bảng. Đọc file, parse, bắt `IOException`/`JsonException`,
kiểm bảng rỗng, in log số dòng + version + vân tay, và xử lý hỏng theo luật boot/reload — **giống hệt
nhau ở mọi bảng**, nên chúng ở trong thân hàm, viết một lần.

`Validate` không generic hoá được vì nó là phần **duy nhất thật sự khác nhau về nội dung**:
`ValidateCharacters` kẹp năm con số theo năm ràng buộc đến từ thuật toán va chạm; `ValidateItems` chỉ
kẹp `MaxStack` và ném khi `TemplateId <= 0`. Hai hàm ấy không có gì chung ngoài việc cùng tên có chữ
"Validate". Generic hoá thứ không có gì chung là tạo ra một abstraction rỗng — rồi bảng thứ ba sẽ phá nó.

Phép thử chung: **gom cái giống nhau, đừng gom cái chỉ trông giống nhau.** Ranh giới nằm ở chỗ "thêm cái
thứ ba thì hàm chung có phải sửa không". `LoadTable` thì không; một `ValidateGeneric` thì có.

</details>

**Câu 13.** Client gọi đúng hàm `CharacterConfigContainer.Load` mà server gọi, nhưng **không** chạy
`ValidateCharacters`. Đó là thiếu sót hay cố ý?
<details>
<summary><b>📖 Đáp án câu 13</b></summary>

Cố ý, và đây là một trong những ranh giới quan trọng nhất của cả dự án.

Phép kiểm miền giá trị trả lời câu hỏi *"người vận hành có gõ một con số phá thuật toán không"*. Người
vận hành ngồi ở **server**. Dữ liệu client nhận không đến từ người gõ tay — nó đến từ **server**, tức
từ nguồn sự thật.

Nếu client cũng kiểm và cũng kẹp, thì với một bảng mà server đã chấp nhận `MoveSpeed = 19.9` còn client
kẹp về `5`, hai bên mô phỏng bằng hai con số khác nhau → rubber-band vĩnh viễn, và **không ai báo lỗi
gì** vì mỗi bên đều thấy mình hợp lệ. Tức là phép kiểm đặt sai chỗ **tạo ra** đúng cái lệch mà cả phase
sinh ra để chống.

Luật rộng hơn: **client không có quyền phán xét dữ liệu server gửi xuống, nó chỉ có quyền tin.** Nếu
không tin được thì vấn đề là kết nối, không phải con số — và đó là việc của `Contract.Hash` ở Bước 5.

</details>

**Câu 14.** Trong `WorldPresenter.OnEnterWorldResult`, `_configService.Apply()` phải chạy trước
`_worldSpawner.SpawnLocalPlayer()`. Đảo lại thì hỏng ở đâu, và vì sao không có lỗi biên dịch nào chặn?
<details>
<summary><b>📖 Đáp án câu 14</b></summary>

`SpawnLocalPlayer` gọi `CharacterConfigContainer.Get(response.ClassId)`. Bảng chưa nạp thì `Get` **ném**
`InvalidOperationException` — cố ý ném chứ không trả null, vì null sẽ đi tiếp vài tầng rồi mới nổ ở
`MovementRules.Step`, xa chỗ gây ra nó.

Triệu chứng: `Instantiate` đã chạy xong nên có một GameObject nhân vật trong scene, nhưng `motor.Init`
ném giữa chừng nên không ai chạy được, camera không bám, và console có một exception mà thoạt nhìn
không liên quan gì tới "thứ tự hai dòng".

Không có lỗi biên dịch vì **thứ tự thực thi không phải là thứ kiểu dữ liệu diễn đạt được**. Đây cùng
họ với những "quên một dòng thì không có lỗi biên dịch" khác của dự án: quên `builder.Register`, quên
`ServerServices.Register`, quên `.As<INetHandlerGroup>()`. Danh sách đầy đủ ở `FEATURE-TEMPLATE.md` §4.

Cách duy nhất chống lại loại này là **làm cho nó tự khai**: `Get` ném với thông điệp nói rõ "server nạp
lúc boot, client nạp khi vào world" — đọc thông điệp là biết phải đi tìm gì.

</details>


---

## Để dành (ghi lại, chưa làm)

- **Đẩy config nóng cho người đang online.** Một gói `ConfigUpdate` broadcast khi reload, client thay
  `WorldConfig` và server thay `entity._world` **trong cùng một tick**. Cạm bẫy: giữa lúc client còn input
  treo chưa được xác nhận, đổi số là replay bằng bộ mới trên trạng thái sinh ra từ bộ cũ.
- **Kiểm cả hành vi, không chỉ hình dạng.** Giới hạn ở câu 9 chữa được bằng cách chạy một chuỗi input cố
  định qua `MovementRules.Step` lúc khởi động rồi băm dãy trạng thái ra, và băm số đó vào `Contract.Hash`.
  Rẻ bất ngờ, và nó bắt đúng loại lệch nguy hiểm nhất còn sót lại.
- **Config từ Google Sheet** (pipeline của `com.hungnt.dataconfig`): xuất sheet → json. Đáng làm khi bảng
  số bắt đầu dày — tức là Phase 13–15 với item, quái, công thức sát thương.
- **Tách file config theo môi trường** (`game.dev.json` / `game.prod.json`), chọn bằng biến môi trường.
  Cùng lúc đó là chỗ bắt đầu **không commit file có secret** — hôm nay config chưa có gì bí mật, ngày có
  chuỗi kết nối MySQL ở Phase 20 thì có.
- **`link.xml` cho `MMORPG.Shared`**, để `Contract.Hash` còn đúng trong bản build IL2CPP. Xếp vào
  Phase 21 cùng các việc build/deploy khác.
- **Bảng nhân vật theo level.** `CharacterConfig` hôm nay là một dòng cho mỗi lớp; Phase 14 sẽ cần
  `base(class, level)`, tức bảng hai chiều. Schema hôm nay chịu được việc đó bằng cách thêm một mảng,
  không phải đổi format.

---

**Xong Phase 12 → hết Chặng C.** Thế giới sống: nhiều người thấy nhau ở phạm vi có giới hạn, map có hình
dạng thật, nhân vật biết diễn, và mọi con số đều là **dữ liệu** chứ không phải hằng số trong code.

Chặng D bắt đầu vòng gameplay thật — [PHASE-13](PHASE-13.md): túi đồ, feature dọc đầu tiên đi đủ
DB → DAL → logic → packet → UI, khuôn mẫu cho mọi feature về sau. Nó cũng là nơi **bảng item** trở thành
bảng loại B thứ ba, đi đúng con đường mà `Fnv1a` và `CharacterTableData` vừa mở ra hôm nay.
