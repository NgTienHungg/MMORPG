# PHASE 12 — Data & Config: số ra khỏi code

> **Kết quả cuối Phase 12:** trọng lực, coyote time, tốc độ chạy, lực nhảy, kích thước thân, thời lượng
> đòn đánh, bán kính AOI, map khởi đầu — tất cả nằm trong hai file JSON dưới `Config/`. Sửa file,
> restart server, **không build lại gì**, giá trị mới có hiệu lực ở cả server lẫn client. Gõ `R` trong
> console server là nạp lại không cần restart. Và cả ba thứ đi qua dây — contract, bảng nhân vật, bản
> đồ — đều có **dấu vân tay** để hai đầu tự phát hiện mình đang chạy hai bản khác nhau.
>
> **Điều kiện:** xong [`PHASE-11.md`](PHASE-11.md) — AOI và chuyển map đã chạy.
>
> **Bài học chính:** (1) config cũng phải có **đúng một nguồn**, và nguồn đó là *server* — client không
> đọc file config nào; (2) có **hai loại config** khác nhau về bản chất, chống lệch bằng hai cách khác
> nhau, và nhầm loại là nguồn của lớp bug câm khó chịu nhất trong game online; (3) một phép kiểm tự
> động rẻ hơn rất nhiều so với việc nhớ.

Format như trước: **hướng làm** hiện sẵn, **📖 Lời giải** trong foldout.

> **Soát lại 2026-09-17 cho khớp code sau Phase 9–11.** Bản trước của doc này viết ngày 2026-08-21 và
> đã lệch nhiều: nó nói gom "14 hằng số" trong `MovementRules` (giờ chỉ còn 5), nó chưa biết
> `CharacterProfile` tồn tại, nó bảo đưa bản đồ ra file (Phase 10 làm rồi), và nó dùng
> `System.Text.Json` (dự án đã chốt Newtonsoft ở Phase 10). Bản này viết lại theo code thật.

---

## Đếm lại xem còn bao nhiêu số trong code

Trước khi thiết kế gì, mở code ra đếm. Sau Phase 11, những con số **có thể chỉnh để đổi cảm giác chơi**
nằm ở đúng ba chỗ:

| Ở đâu | Là gì | Ai đọc |
|---|---|---|
| `MovementRules` — 5 `const` | `GRAVITY`, `MAX_FALL_SPEED`, `COYOTE_TICKS`, `JUMP_BUFFER_TICKS`, `DROP_THROUGH_TICKS` | **cả hai bên** (client dự đoán bằng chính `Step`) |
| `CharacterProfiles.Build()` | bảng theo lớp nhân vật: tốc độ, lực nhảy, ba kích thước thân, ba `ActionDefinition` | **cả hai bên** (client dự đoán + co clip hoạt ảnh theo `DurationTicks`) |
| rải rác phía server | `WorldService.AOI_RADIUS_X`, `WorldService.DEFAULT_CLASS_ID`, `MapRegistry.STARTING_MAP_ID` | **chỉ server** |

Ba dòng ấy chính là hai loại config, và cột "ai đọc" là thứ quyết định cách chữa:

| | **Loại A — tham số vận hành** | **Loại B — bảng dữ liệu** |
|---|---|---|
| Trong dự án này | 5 hằng của `MovementRules` · ba số chỉ-server ở trên | **bảng `CharacterProfile`** · **bản đồ** · sắp tới: bảng item (Phase 13), bảng quái (Phase 15) |
| Hình dạng | vài con số rời | bảng có cấu trúc, có khoá, sẽ còn dài ra |
| Cách chống lệch | **chỉ server đọc file**, phần client cần thì đẩy trong `EnterWorldResponse` | **schema** ở `Shared` (một định nghĩa); **dữ liệu** một bản gốc + **dấu vân tay** để so |
| Làm ở đâu | Bước 1 | Bước 2 |

Điểm dễ hiểu sai nhất, nhắc lại vì nó là gốc của cả phase: bệnh của cách làm cũ (kiểu vo-lam-genz)
**không phải** là "gen file rồi copy sang cả hai bên". Copy chỉ là triệu chứng. Bệnh thật là **không ai
kiểm tra hai bản có khớp nhau không** — copy thiếu một lần thì client hiển thị item A trong khi server
xử lý item B; không lỗi biên dịch, không log, chỉ có bug câm.

> Cách chữa không phải "đừng copy" — đôi khi buộc phải có hai bản. Cách chữa là **làm cho việc lệch bị
> phát hiện tự động**: loại A thì không tồn tại bản thứ hai, loại B thì có dấu vân tay so được bằng máy.

### Vì sao client không được đọc file config

Trực giác đầu tiên của mọi người: "để hai bên cùng đọc `game.json` cho đồng bộ". Nghe giống contract một
nguồn — nhưng là bẫy, vì **file giống nhau không có nghĩa là giá trị đang chạy giống nhau**:

- Client build ra mang bản copy của file **tại thời điểm build**. Server sửa config → mọi client ngoài
  kia vẫn chạy số cũ. Đây chính xác là bug "chép tay `NetCmd`" ở dạng dữ liệu.
- File nằm trong máy người chơi thì người chơi sửa được. Với giá trị chỉ-hiển-thị thì vô hại; với
  `Gravity` mà client dùng để dự đoán thì là mời họ tự chỉnh — server vẫn thắng (Phase 6), nhưng họ tự
  gây rubber-band rồi đi report "game lag".

```
Config/game.json        ──► GameServer đọc lúc boot (hot reload: phím R)
Config/characters.json  ──►      │
                                 ├─► WorldService · CharacterService · PlayerEntity dùng trực tiếp
                                 └─► EnterWorldResponse { World, Characters, MapChecksum } ──► client
```

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
├── Config/                      ← TẠO MỚI ở bước này
│   ├── game.json                ← loại A: luật thế giới + số chỉ server dùng
│   └── characters.json          ← loại B: bảng theo lớp nhân vật
├── Server/
│   └── GameServer/
│       └── bin/Debug/net8.0/
│           └── Data/
│               ├── Maps/        ← Phase 10 đã có
│               └── Config/      ← csproj tự copy vào đây lúc build
└── ...
```

**Vì sao ở gốc repo chứ không trong `Server/GameServer/`:** nó là **dữ liệu vận hành**, không phải
source của một process. Ngày tách repo server (Phase 21) thì `Config/` đi theo server — nhưng ngày
Phase 19 có file `.lua` và Phase 18 có bảng đẩy lên CDN thì chúng cũng nằm cạnh nhau ở đây. Một thư mục
dữ liệu ở gốc, không phải ba chỗ khác nhau.

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
— gạch từng dòng khi xong.

**Bước 1 — loại A**

| File | Việc |
|---|---|
| `Config/game.json` | 🆕 tạo |
| `Server/Shared/World/WorldRules.cs` | 🆕 tạo |
| `Server/Shared/World/MovementRules.cs` | ✏️ xoá 5 `const`, `Step` nhận thêm `WorldRules world` |
| `Server/GameServer/GameConfigData.cs` | 🆕 tạo |
| `Server/GameServer/ConfigService.cs` | 🆕 tạo |
| `Server/GameServer/GameServer.csproj` | ✏️ copy `Config/*.json` → `Data/Config/` + target kiểm thư mục |
| `Server/GameServer/Program.cs` | ✏️ dựng `ConfigService` đầu tiên · **luồng đọc phím đặt TRƯỚC vòng accept** |
| `Server/GameServer/World/MapRegistry.cs` | ✏️ hàm dựng nhận `ConfigService` |
| `Server/GameServer/World/WorldService.cs` | ✏️ hàm dựng nhận `ConfigService`; bán kính AOI từ config |
| `Server/GameServer/World/CharacterService.cs` | ✏️ hàm dựng nhận `ConfigService`; gắn `World` vào response |
| `Server/GameServer/World/PlayerEntity.cs` | ✏️ giữ `WorldRules` chốt lúc dựng |
| `Server/Shared/Dto/Character/CharacterDto.cs` | ✏️ `EnterWorldResponse` thêm `WorldRules World` |
| `Assets/Game/Scripts/World/LocalPlayer.cs` | ✏️ giữ `WorldRules`, gọi `Prepare()` trong `Apply` |
| `Assets/Game/Scripts/World/WorldSpawner.cs` | ✏️ truyền `WorldRules` vào `motor.Init` |
| `Assets/Game/Scripts/World/PlayerMotor.cs` | ✏️ nhận `WorldRules`, dùng ở **cả hai** chỗ gọi `Step` |

**Bước 2 — loại B**

| File | Việc |
|---|---|
| `Config/characters.json` | 🆕 tạo |
| `Server/Shared/World/Fnv1a.cs` | 🆕 tạo |
| `Server/Shared/World/MapGrid.cs` | ✏️ `Checksum()` gọi sang `Fnv1a`, xoá `Mix` riêng |
| `Server/Shared/World/CharacterTableData.cs` | 🆕 tạo |
| `Server/Shared/World/ActionDefine.cs` | ✏️ `CharacterProfile` thành kiểu nạp được · xoá `ActionDefinition` · `CharacterProfiles.Build()` → `Load()` |
| `Server/GameServer/ConfigService.cs` | ✏️ đọc thêm `characters.json`, gọi `CharacterProfiles.Load` |
| `Server/Shared/Dto/Character/CharacterDto.cs` | ✏️ `EnterWorldResponse` thêm `Characters` + `MapChecksum` |
| `Server/Shared/Dto/World/WorldSyncDto.cs` | ✏️ `MapChangedNotice` thêm `MapChecksum` |
| `Assets/Game/Scripts/World/WorldSpawner.cs` | ✏️ so checksum sau khi `MapService.Load` |

**Bước 3 — contract hash**

| File | Việc |
|---|---|
| `Server/Shared/Net/Contract.cs` | 🆕 tạo |
| `Server/Shared/Net/NetCmd.cs` | ✏️ `VersionCheck = 6` |
| `Server/Shared/Dto/SystemDto.cs` | ✏️ thêm `VersionCheckRequest/Response` |
| `Server/Shared/HandshakeDto.cs` | ❌ xoá |
| `Server/GameServer/SessionState.cs` | ✏️ thêm `Verified = 1`, dồn hai bậc sau lên |
| `Server/GameServer/ClientSession.cs` | ✏️ thêm `MarkVerified()` |
| `Server/GameServer/Handlers/SystemHandler.cs` | ✏️ handler `VersionCheck` · nâng `MinState` của các lệnh khác |
| `Server/GameServer/Handlers/AuthHandler.cs` | ✏️ `Register`/`Login` lên `MinState = Verified` |
| `Assets/Game/Scripts/Network/Handlers/SystemNetHandler.cs` | ✏️ handler + event `OnVersionCheck` |
| `Assets/Game/Scripts/Auth/LoginPresenter.cs` | ✏️ gửi `VersionCheck` sau khi nối, chặn login tới khi có kết quả |

---

## Bước 1 — Loại A: `WorldRules` ra file, và đi xuống client

### Hướng làm

**Một kiểu, không phải hai.** Phase 9 đã chốt: người thiết kế viết bằng **giây**, mô phỏng đếm bằng
**tick**, và phép quy đổi chạy **một lần** chứ không nằm trong `Step`. Cần giữ đúng điều đó — nhưng
**không** cần hai class để giữ nó.

`WorldRules` là **một** class, mang cả hai: giây là thứ đọc từ file và đi trên dây; tick là trường
**dẫn xuất**, tính một lần trong `Prepare()` và không tuần tự hoá.

> ### Khi nào tách "kiểu file" khỏi "kiểu chạy", khi nào không
>
> Phase 10 tách `MapFileData` khỏi `MapGrid`, và tách đúng — vì **hình dạng hai bên thật sự khác nhau**:
> file là danh sách chuỗi đọc từ trên xuống, kiểu chạy là mảng phẳng `CellType[]` có gốc toạ độ, và
> giữa chúng có một phép lật trục Y. Hai hình dạng khác nhau thì hai kiểu, và phép chuyển đổi là một
> hàm thật.
>
> Ở đây thì **hình dạng giống hệt nhau**: năm con số vào, năm con số ra. Tách làm hai class nghĩa là
> chép tay năm dòng gán — và **đó mới là chỗ sinh bug**, không phải chỗ tránh bug:
>
> ```csharp
> // Thêm một trường vào file? Phải nhớ sửa BA chỗ:
> class WorldRulesData { ...; public float ApexBonusSeconds { get; set; } }   // 1. khai báo
> class WorldRules     { ...; public int ApexBonusTicks { get; } }            // 2. khai báo lần nữa
> WorldRules(WorldRulesData d) { /* quên dòng gán thứ 6 */ }                  // 3. và gán
> ```
>
> Quên bước 3 thì trường đó đọc được từ file, đi qua validate, rồi **bị vứt đi trong im lặng** — không
> lỗi biên dịch, không log, và triệu chứng là "sửa số trong file mà không thấy khác gì". Đúng cái loại
> bug câm mà cả phase này sinh ra để chống.
>
> **Tiêu chí:** tách khi phép chuyển đổi là một **hàm thật** (đổi hình dạng, lật trục, dựng chỉ mục);
> gộp khi nó chỉ là một dãy phép gán 1-1. `CharacterRow` ≠ `PlayerEntity` ở Phase 5 là ví dụ tách đúng
> vì lý do thứ ba: chúng có **vòng đời** khác nhau (một cái sống trong DB, một cái sống trong world).

Cách gộp mà vẫn giữ được kỷ luật "quy đổi một lần": trường dẫn xuất phải **nói không với cả hai bộ tuần
tự hoá**, và phải có đúng **hai chỗ** gọi `Prepare()` trong cả dự án — `ConfigService.Load()` bên server,
`LocalPlayer.Apply()` bên client. Cũng chính là hai chỗ đã gọi `CharacterProfiles.Load` ở Bước 2, nên
không có thêm thứ gì mới để nhớ.

**`MovementRules` mất 5 `const`, `Step` nhận thêm một tham số.** Chữ ký thành:

```csharp
public static MoveState Step(MoveState state, MoveIntent intent, float dt,
    WorldRules world, CharacterProfile profile, MapGrid map)
```

Sáu tham số — đúng lúc để hỏi câu mà mọi codebase đều gặp: *có nên gom `world` + `profile` + `map` thành
một `SimContext` không?* Câu trả lời ở đây là **không**, và lý do đáng nhớ hơn câu trả lời: mở
`MovementRules` ra xem, **chỉ mình `Step` cần `world`**; các hàm riêng (`ResolveHorizontal`,
`ResolveVertical`, `BlocksFall`…) chỉ cần `map` và `profile`. Gom lại là ép mọi hàm nhận một gói to hơn
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

**`ConfigService`** (file mới, `Server/GameServer/ConfigService.cs`) — ba việc:

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

**Vòng đọc phím trong console — phải dựng lại cho đúng.** `Program.cs` hiện **không có** vòng nào: đoạn
đọc phím của Phase 9 đã bị comment lại, và bị comment là đúng, vì nó nằm **bên trong vòng accept**:

```csharp
while (!cts.IsCancellationRequested)
{
    switch (Console.ReadKey(intercept: true).Key) { ... }   // ← chặn ở đây
    TcpClient tcpClient = await listener.AcceptTcpClientAsync(ct);
}
```

`Console.ReadKey` chặn **luồng**, nên không ai vào được game cho tới khi bạn gõ một phím. Cách đúng:
một **luồng riêng** chỉ làm việc đọc phím (`new Thread(...) { IsBackground = true }` — không phải
`Task.Run`, vì đó là blocking I/O chứ không phải việc CPU), đẩy lệnh vào world qua đúng những hàng đợi
đã có. Dựng lại vòng này cũng làm sống lại `WorldService.EnqueueForceAll` / `EnqueueReviveAll` — hiện
đang là code chết, không ai gọi, từ hồi vòng phím bị bỏ đi.

**Ai dùng config ở đâu:**

- `Program.cs`: `ConfigService.Load()` **trước** khi dựng `MapRegistry`/`WorldService`, và in ra log.
- `WorldService`: `AOI_RADIUS_X` thành giá trị đọc từ config. Chú ý `AOI_COLUMN_WIDTH` ăn theo — nó phải
  bằng đúng bán kính (xem Phase 11), nên tính từ bán kính lúc dựng chứ đừng để hai con số rời nhau.
- `MapRegistry`/`CharacterService`: `STARTING_MAP_ID`, `DEFAULT_CLASS_ID` từ config.
- **`PlayerEntity` giữ một reference `WorldRules`, chốt MỘT lần lúc dựng** — đúng chỗ nó đã giữ
  `_profile` từ Phase 9. `Integrate` dùng field đó, **không** đọc `ConfigService.Current` mỗi tick.

Điểm cuối là bài học chính của bước này:

> Bộ số là một phần của **hợp đồng phiên chơi**. Client dự đoán bằng đúng bộ nó nhận lúc vào world;
> server đổi số giữa chừng thì mọi dự đoán của người đang online lệch **ngay lập tức** → rubber-band
> hàng loạt, và họ không làm gì sai cả.
>
> Luật: **hot reload áp dụng cho người vào sau.** Người đang online giữ bộ cũ tới lần vào world kế tiếp.

Và vì đã gom thành một object, "chốt theo phiên" là giữ **một reference** thay vì chép năm field — thêm
số mới không phải nhớ chép thêm, và cũng không thể quên.

**Phía client — ba chỗ, đều là "cầm object rồi truyền đi":**

- `EnterWorldResponse` thêm `WorldRules World`; `MapChangedNotice` thì **không** cần (đổi map không
  đổi luật thế giới).
- `LocalPlayer.Apply` dựng `WorldRules` từ nó và giữ lại — cache server-confirmed, đúng luật cũ: không
  có setter công khai.
- `PlayerMotor.Init` nhận `WorldRules`, dùng ở **cả hai** chỗ gọi `Step`: bước dự đoán trong `Step(...)`
  và **vòng replay** trong `OnMoveStateResult`. Sót chỗ nào là rubber-band ở đúng chỗ đó — lần này
  trình biên dịch chỉ tận nơi vì chữ ký `Step` đã đổi.

Client **không đọc file config nào**, không copy `game.json` vào build — đó là toàn bộ ý của bước này.

Một chi tiết dễ chịu: `RemotePlayerView` và `CharacterStates.Derive` **không** cần `WorldRules` — `Derive`
chỉ so sánh dấu, không dùng hằng nào. Một hàm thuần không có tham số cấu hình là một hàm không bao giờ
lệch phiên bản.

### ✅ CHECKPOINT A

1. Server boot log một dòng gọn: `Config: gravity=30 maxFall=20 coyote=3t jumpBuf=3t drop=6t · aoi=24 · startMap=1`.
2. Sửa `Gravity` thành `60` → **không build lại gì** → restart server → nhân vật rơi nặng hẳn, nhảy thấp
   hẳn, và **không rubber-band** (client nhận 60 qua `EnterWorld`).
3. Xoá tạm `game.json` khỏi output → server vẫn boot, log Warn, chạy bằng mặc định.
4. Ghi `"Gravty": 30` (sai chính tả) → boot lên **báo lỗi trường lạ**, không im lặng. Đây là chỗ khác
   file map, và là lý do phải khác.
5. Ghi `"MaxFallSpeed": 60` → Warn về đúng trường đó, giá trị về 20, nhân vật không xuyên sàn.
6. Đang chạy: sửa file rồi gõ `R` → log in số mới. Người đang online **không đổi gì** (đúng thiết kế);
   relog thì mới đổi.

<details>
<summary><b>📖 Lời giải — <code>Shared</code>: <code>WorldRules</code></b></summary>

**`Server/Shared/World/WorldRules.cs`** (file mới):

```csharp
using MemoryPack;
using Newtonsoft.Json;

namespace MMORPG.Shared.World
{
    /// <summary>
    /// Luật của thế giới — thứ đúng với mọi nhân vật, khác với CharacterProfile là bộ số của riêng
    /// một lớp. Ba vai trong một kiểu: bản đối chiếu với khối "World" của game.json, gói tin đi xuống
    /// client, và bộ số mà MovementRules.Step đọc.
    ///
    /// Ba vai nhưng MỘT hình dạng, nên một kiểu là đủ. Tách ra thành "kiểu file" và "kiểu chạy" chỉ
    /// đáng khi phép chuyển đổi là một hàm thật; ở đây nó sẽ là năm dòng gán chép tay, và dòng thứ sáu
    /// bị quên sẽ không có lỗi biên dịch nào.
    ///
    /// Mọi property có giá trị mặc định HỢP LỆ: file thiếu trường nào thì trường đó về mặc định thay
    /// vì làm cả server đứng.
    /// </summary>
    [MemoryPackable]
    public sealed partial class WorldRules
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

        // ── Dưới đây là trường DẪN XUẤT: không có trong file, không đi trên dây. ──────────────────
        //
        // Hai thuộc tính bỏ qua là bắt buộc và mỗi cái chặn một chuyện: [MemoryPackIgnore] để gói tin
        // không mang theo thứ tính lại được (và để hai bên không có cửa gửi cho nhau hai con số khác
        // nhau cho cùng một giây); [JsonIgnore] để người mở file ra không thấy một trường mà sửa nó
        // thì chẳng có tác dụng gì.
        //
        // Vì sao quy ra tick MỘT LẦN chứ không tính trong Step: MovementRules chỉ được dùng + - * /
        // và so sánh (xem comment đầu file đó), mà ToTicks thì gọi MathF.Ceiling.

        [MemoryPackIgnore] [JsonIgnore] public int CoyoteTicks { get; private set; }

        [MemoryPackIgnore] [JsonIgnore] public int JumpBufferTicks { get; private set; }

        [MemoryPackIgnore] [JsonIgnore] public int DropThroughTicks { get; private set; }

        /// <summary>
        /// Tính các trường dẫn xuất. Gọi ĐÚNG HAI CHỖ trong cả dự án: ConfigService.Load() sau khi đọc
        /// file, và LocalPlayer.Apply() sau khi nhận gói EnterWorld.
        ///
        /// Quên gọi thì cả ba bộ đếm bằng 0, và triệu chứng là "nhảy lúc được lúc không" — coyote time
        /// và jump buffer biến mất. Đó là lý do hai chỗ gọi ấy được ghim vào cùng một dòng với
        /// CharacterProfiles.Load: một việc, không phải hai việc phải nhớ.
        /// </summary>
        public void Prepare()
        {
            CoyoteTicks = MovementRules.ToTicks(CoyoteSeconds);
            JumpBufferTicks = MovementRules.ToTicks(JumpBufferSeconds);
            DropThroughTicks = MovementRules.ToTicks(DropThroughSeconds);
        }
    }
}
```

**`Server/Shared/World/MovementRules.cs`** — xoá 5 `const`, giữ `TICK_RATE`, `TICK_DT`, `EXPIRED`,
`EDGE`. Đổi chữ ký và mọi chỗ đọc hằng:

```csharp
        public static MoveState Step(MoveState state, MoveIntent intent, float dt,
            WorldRules world, CharacterProfile profile, MapGrid map)
        {
            // ...

            // 3. Trọng lực — luật của THẾ GIỚI, không theo nhân vật. Đó chính là lý do nó nằm ở
            //    WorldRules chứ không ở CharacterProfile: đổi nó là đổi cảm giác của cả server.
            state.VelY -= world.Gravity * dt;
            if (state.VelY < -world.MaxFallSpeed)
                state.VelY = -world.MaxFallSpeed;

            // ...

            if (!locked && intent.Crouch && intent.Jump && state.Grounded &&
                StandingOnOneWay(map, profile, state))
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

</details>

<details>
<summary><b>📖 Lời giải — <code>GameServer</code>: <code>ConfigService</code> và vòng phím</b></summary>

**`Server/GameServer/GameServer.csproj`** — thêm, ngay cạnh khối copy map đã có:

```xml
    <ItemGroup>
        <Content Include="..\..\Config\*.json"
                 Link="Data\Config\%(Filename)%(Extension)"
                 CopyToOutputDirectory="PreserveNewest"/>
    </ItemGroup>
```

**`Server/GameServer/GameConfigData.cs`** (file mới) — bản đối chiếu với `game.json`:

```csharp
using MMORPG.Shared.World;

namespace MMORPG.GameServer
{
    /// <summary>
    /// Bản đối chiếu 1-1 với Config/game.json. Nằm ở GameServer chứ không ở Shared vì khối Server chỉ
    /// server được biết — đưa nó sang Shared là đưa cả bán kính AOI vào DLL của client.
    /// </summary>
    public sealed class GameConfigData
    {
        public int Version { get; set; } = 1;

        public WorldRules World { get; set; } = new WorldRules();

        public ServerConfigData Server { get; set; } = new ServerConfigData();
    }

    public sealed class ServerConfigData
    {
        public int StartingMapId { get; set; } = 1;

        public int DefaultClassId { get; set; } = 1;

        /// <summary>Bán kính tầm nhìn theo trục X. Xem Phase 11 để biết vì sao nó phải lớn hơn nửa bề RỘNG màn hình.</summary>
        public float AoiRadiusX { get; set; } = 24f;
    }
}
```

**`Server/GameServer/ConfigService.cs`** (file mới):

```csharp
using MMORPG.ServerCore;
using MMORPG.Shared.World;
using Newtonsoft.Json;

namespace MMORPG.GameServer
{
    /// <summary>
    /// Nguồn DUY NHẤT của mọi con số vận hành. Đọc file lúc boot, đọc lại khi được yêu cầu, và không
    /// bao giờ để một file hỏng giết server.
    /// </summary>
    public sealed class ConfigService
    {
        private const string GAME_FILE = "game.json";

        private const string CHARACTERS_FILE = "characters.json";

        // MissingMemberHandling.Error, KHÁC file map: game.json do người gõ tay. Gõ nhầm "Gravty" mà
        // bỏ qua trong im lặng thì trọng lực về mặc định còn người ta đi tìm bug trong MovementRules.
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Error,
        };

        /// <summary>
        /// Bộ số hiện hành. Đọc property này ra BIẾN CỤC BỘ rồi dùng, đừng đọc nhiều lần trong một
        /// phép tính: reload thay nguyên object, nên hai lần đọc có thể rơi vào hai bộ khác nhau.
        /// </summary>
        public GameConfigData Current { get; private set; } = new GameConfigData();

        /// <summary>Lối tắt cho chỗ gọi hay dùng nhất. Không phải bản sao — vẫn đúng object trong Current.</summary>
        public WorldRules World => Current.World;

        public ConfigService()
        {
            Load();
        }

        /// <summary>
        /// Đọc lại file. Gọi lúc boot và mỗi lần bấm phím reload.
        ///
        /// Thay NGUYÊN object chứ không sửa từng field: gán reference là thao tác nguyên tử nên luồng
        /// tick hoặc thấy trọn bộ cũ, hoặc trọn bộ mới. Sửa tại chỗ thì nó có thể đọc được Gravity mới
        /// ghép với MaxFallSpeed cũ — một tổ hợp chưa từng tồn tại trong file nào.
        /// </summary>
        public void Load()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Data", "Config", GAME_FILE);
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

            Validate(data);

            // Quy giây ra tick NGAY SAU validate, trước khi phát hành object. Đảo thứ tự thì có một
            // khoảnh khắc luồng tick đọc được bộ số mới với ba bộ đếm bằng 0.
            data.World.Prepare();

            Current = data;

            Log.Info($"Config: gravity={World.Gravity} maxFall={World.MaxFallSpeed} " +
                     $"coyote={World.CoyoteTicks}t jumpBuf={World.JumpBufferTicks}t drop={World.DropThroughTicks}t · " +
                     $"aoi={data.Server.AoiRadiusX} · startMap={data.Server.StartingMapId}");

            // Bảng nhân vật (Bước 2) đi cùng đường, và cố ý nằm cùng hàm này: hai file, một lần nạp,
            // một phím R. Tách ra hai hàm là mở đường cho "nạp lại cái này mà quên cái kia".
            LoadCharacters();
        }

        /// <summary>
        /// Kẹp từng trường về khoảng dùng được. Trả riêng trường hỏng về mặc định chứ không vứt cả
        /// file: một dòng sai không nên xoá sổ ba mươi dòng đúng.
        /// </summary>
        private static void Validate(GameConfigData data)
        {
            var fallback = new WorldRules();

            // Trần tuyệt đối cho mọi vận tốc: đi quá một ô trong một tick là vượt qua giả định của
            // phép kiểm va chạm ở Phase 10 (ngang và lên chỉ kiểm ĐIỂM CUỐI, xuống thì quét từng hàng
            // ô nhưng chỉ bảo đảm trong phạm vi một ô mỗi tick).
            float speedCap = MapGrid.CELL_SIZE / MovementRules.TICK_DT;

            data.World.Gravity = Clamp(data.World.Gravity, 0.001f, float.MaxValue, fallback.Gravity, "Gravity");
            data.World.MaxFallSpeed = Clamp(data.World.MaxFallSpeed, 0.001f, speedCap, fallback.MaxFallSpeed, "MaxFallSpeed");
            data.World.CoyoteSeconds = Clamp(data.World.CoyoteSeconds, 0f, 5f, fallback.CoyoteSeconds, "CoyoteSeconds");
            data.World.JumpBufferSeconds = Clamp(data.World.JumpBufferSeconds, 0f, 5f, fallback.JumpBufferSeconds, "JumpBufferSeconds");
            data.World.DropThroughSeconds = Clamp(data.World.DropThroughSeconds, 0f, 5f, fallback.DropThroughSeconds, "DropThroughSeconds");

            data.Server.AoiRadiusX = Clamp(data.Server.AoiRadiusX, 0.001f, float.MaxValue, 24f, "AoiRadiusX");
        }

        private static float Clamp(float value, float min, float max, float fallback, string field)
        {
            if (value >= min && value <= max)
                return value;

            // LA LỚN chứ không sửa im lặng: người vận hành phải biết số họ gõ đã bị từ chối, nếu không
            // họ sẽ đi tìm lý do vì sao "sửa rồi mà không thấy khác gì".
            Log.Warn($"Config {field.Red()} = {value} ngoài khoảng [{min}, {max}] — dùng {fallback}.");

            return fallback;
        }

        /// <summary>
        /// Bảng nhân vật (Bước 2). Hỏng thì GIỮ NGUYÊN bảng đang chạy thay vì về mặc định — khác hẳn
        /// game.json, và khác vì một lý do: ở đây không có "giá trị mặc định an toàn" nào. Một bảng
        /// nhân vật rỗng nghĩa là mọi người chơi mất hết bộ số, tức là server sống mà không ai chơi
        /// được. Giữ bảng cũ thì ít nhất người đang online không bị gì.
        /// </summary>
        private void LoadCharacters()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Data", "Config", CHARACTERS_FILE);

            try
            {
                var table = JsonConvert.DeserializeObject<CharacterTableData>(File.ReadAllText(path), Settings);

                if (table == null || table.Classes.Length == 0)
                    throw new JsonException("Bảng rỗng — thiếu mảng \"Classes\".");

                ValidateCharacters(table);
                CharacterProfiles.Load(table);

                Log.Info($"Bảng nhân vật: {table.Classes.Length} lớp, checksum {CharacterProfiles.Checksum:X8}");
            }
            catch (Exception ex) when (ex is IOException || ex is JsonException || ex is InvalidOperationException)
            {
                Log.Error($"Không nạp được {path.Red()}: {ex.Message}. " +
                          "GIỮ NGUYÊN bảng đang chạy.");
            }
        }

        /// <summary>
        /// Kẹp bộ số của từng lớp. Nằm ở SERVER chứ không ở CharacterProfiles.Load, vì client cũng gọi
        /// Load — và client không có quyền phán xét dữ liệu server gửi xuống, nó chỉ có quyền tin. Đặt
        /// phép kiểm vào Load là để client âm thầm sửa số của server, tức tạo ra đúng cái lệch mà cả
        /// phase này đang chống.
        /// </summary>
        private static void ValidateCharacters(CharacterTableData table)
        {
            var fallback = new CharacterProfile();
            float speedCap = MapGrid.CELL_SIZE / MovementRules.TICK_DT;

            foreach (CharacterProfile profile in table.Classes)
            {
                string tag = $"class {profile.ClassId}";

                profile.MoveSpeed = Clamp(profile.MoveSpeed, 0.001f, speedCap - 0.001f, fallback.MoveSpeed, $"{tag}.MoveSpeed");
                profile.JumpSpeed = Clamp(profile.JumpSpeed, 0.001f, speedCap - 0.001f, fallback.JumpSpeed, $"{tag}.JumpSpeed");

                // < 0.5 chứ không <= : rộng đúng nửa ô là vừa khít khe 1 ô, và "vừa khít" trong số
                // thực dấu phẩy động nghĩa là lúc lọt lúc không.
                profile.BodyHalfWidth = Clamp(profile.BodyHalfWidth, 0.001f, MapGrid.CELL_SIZE * 0.5f - 0.001f,
                    fallback.BodyHalfWidth, $"{tag}.BodyHalfWidth");

                // Trần 2.0 đến từ OverlapsSolid: nó quét ba mức cao, và ba mức chỉ phủ kín khi khoảng
                // cách giữa hai mức nhỏ hơn cạnh ô. Cao hơn 2.0 là có ô lọt qua khe kiểm.
                profile.BodyHeight = Clamp(profile.BodyHeight, 0.001f, 2f, fallback.BodyHeight, $"{tag}.BodyHeight");

                profile.BodyHeightCrouch = Clamp(profile.BodyHeightCrouch, 0.001f,
                    MathF.Min(profile.BodyHeight, MapGrid.CELL_SIZE), fallback.BodyHeightCrouch, $"{tag}.BodyHeightCrouch");
            }
        }
    }
}
```

**`Server/GameServer/World/MapRegistry.cs`** — `STARTING_MAP_ID` hết là hằng số:

```csharp
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

**`Server/GameServer/World/WorldService.cs`** — bán kính AOI hết là hằng số:

```csharp
        private readonly float _aoiRadiusX;
        private readonly float _aoiColumnWidth;

        public WorldService(MapRegistry maps, ConfigService config)
        {
            _maps = maps;

            // Chốt MỘT LẦN lúc dựng, không đọc config.Current mỗi tick. Cùng lý do với WorldRules
            // trong PlayerEntity — nhưng ở đây còn thêm một lý do nữa: đổi bán kính giữa chừng làm
            // tập Visible của mọi người lệch với tập đã gửi, và một loạt EntityDespawn giả sinh ra.
            _aoiRadiusX = config.Current.Server.AoiRadiusX;

            // Cột rộng BẰNG ĐÚNG bán kính (Phase 11). Tính từ bán kính chứ không cho nó một dòng
            // config riêng: hai con số rời nhau là hai con số sẽ lệch nhau.
            _aoiColumnWidth = _aoiRadiusX;
        }
```

`ColumnOf` đang là `static` nên nó không thấy field — đổi thành method thường. Trình biên dịch sẽ nhắc.

**`Server/GameServer/World/CharacterService.cs`** — nhận config và gắn bộ số vào response:

```csharp
        private readonly ConfigService _config;

        public CharacterService(DbClient dbClient, WorldService worldService, MapRegistry maps, ConfigService config)
        {
            // ... ba dòng cũ ...
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

**`Server/GameServer/Program.cs`** — hai việc, và việc thứ hai là chỗ dễ sai nhất của cả bước:

```csharp
// Config đọc TRƯỚC mọi thứ khác: MapRegistry và WorldService đều cần số từ nó.
var config = new ConfigService();

var maps = new MapRegistry(config);
var worldService = new WorldService(maps, config);
CharacterHandler.CharacterService = new CharacterService(dbClient, worldService, maps, config);

// ─────────────────────────────────────────────────────────────────────────────────────────────
// ...(TcpDispatcher.RegisterAll, listener.Start, cts, gameLoop — giữ nguyên thứ tự cũ)...
// ─────────────────────────────────────────────────────────────────────────────────────────────

// ĐẶT KHỐI NÀY TRƯỚC vòng `while (!cts.IsCancellationRequested) { ... AcceptTcpClientAsync ... }`.
//
// Đây là chỗ dễ sai nhất của cả bước, và sai thì KHÔNG có lỗi biên dịch: đặt nó sau vòng accept thì
// luồng phím chỉ khởi động lúc server đang tắt, tức là phím R không bao giờ có tác dụng — mà triệu
// chứng lại giống hệt "hot reload chưa chạy".
//
// Vòng đọc phím nằm ở LUỒNG RIÊNG, không trộn vào vòng accept. Console.ReadKey chặn cả luồng, nên
// đặt chung là không ai vào được game cho tới khi bạn gõ một phím — đó là lý do đoạn code cũ ở đây
// từng bị comment lại.
//
// Thread chứ không Task.Run: đây là blocking I/O, không phải việc CPU. Nhét nó vào thread pool là
// chiếm một worker suốt đời process. IsBackground = true để nó không giữ process sống lúc thoát.
var console = new Thread(() =>
{
    while (!cts.IsCancellationRequested)
    {
        switch (Console.ReadKey(intercept: true).Key)
        {
            case ConsoleKey.R:
                config.Load();
                break;

            // Ba phím thử của Phase 9, sống lại cùng vòng lặp này.
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
```

**`Server/GameServer/World/PlayerEntity.cs`** — giữ `WorldRules` cạnh `_profile`, cùng một lý do:

```csharp
        /// <summary>
        /// Luật thế giới của PHIÊN này, chốt lúc dựng entity. Cố tình KHÔNG đọc ConfigService.Current
        /// mỗi tick: client bên kia đang dự đoán bằng đúng bộ số nó nhận lúc vào world, nên đổi số
        /// giữa chừng là rubber-band hàng loạt cho những người không làm gì sai cả.
        ///
        /// Hot reload áp dụng cho người vào SAU.
        /// </summary>
        private readonly WorldRules _world;
```

```csharp
            State = MovementRules.Step(State, intent, dt, _world, _profile, _map);
```

</details>

---

## Bước 2 — Loại B: bảng nhân vật ra file, bản đồ nhận dấu vân tay

### Hướng làm

Loại B khác loại A ở một điểm duy nhất nhưng điểm đó đổi mọi thứ: **client thật sự cần toàn bộ dữ liệu**,
không phải vài con số. Cách của loại A ("chỉ server đọc, đẩy vài giá trị xuống") không áp dụng được vì
"vài giá trị" ở đây là cả bảng.

Dự án đang có **hai** bảng loại B, và chúng đang ở hai tình trạng khác nhau:

| Bảng | Hôm nay | Việc của phase này |
|---|---|---|
| Bản đồ | đã là file JSON từ Phase 10; **client đọc bản trong `Resources/Maps/`, server đọc bản copy trong `Data/Maps/`** | hai bản ⇒ **lệch được** ⇒ phải so `Checksum()` bằng máy |
| Bảng `CharacterProfile` | còn là C# trong `CharacterProfiles.Build()`, đi theo `MMORPG.Shared.dll` | đưa ra file + **gửi cả bảng** xuống client |

Hai tình trạng ấy chính là **hai chế độ** của loại B, và chọn chế độ nào là theo **kích thước**:

> - **Gửi cả dữ liệu**: nhỏ, đơn giản, *không lệch được về mặt cấu trúc*. Bảng nhân vật đi đường này.
> - **Client giữ bản riêng, server chỉ gửi dấu vân tay**: to, phức tạp, *lệch được nên bắt buộc phải
>   kiểm*. Bản đồ đi đường này (map 64×11 đã là vài KB, và một map thật thì lớn hơn nhiều).
>
> **Trường version thì có từ ngày đầu, ở cả hai chế độ.** Thêm nó sau khi đã có người chơi là một cuộc
> di cư.

**2a — Một hàm băm dùng chung.** `MapGrid.Checksum()` đang có sẵn FNV-1a nhưng hàm `Mix` nằm `private`
trong đó. Tách ra `Server/Shared/World/Fnv1a.cs`: `START`, `Mix(hash, int)`, `Mix(hash, float)`,
`Mix(hash, string)`. `MapGrid.Checksum()` gọi sang đó và **phải cho ra đúng số cũ** — đây là refactor
thuần, và bài test round-trip của Phase 10 sẽ nói cho bạn biết nếu không.

Ba bảng loại B sắp tới (nhân vật hôm nay, item ở Phase 13, quái ở Phase 15) đều cần đúng hàm này. Băm
`float` thì băm **bit** (`BitConverter.SingleToInt32Bits`), không băm chuỗi in ra: `0.1f` in ra bao nhiêu
chữ số là chuyện của `ToString`, còn bit thì giống nhau ở mọi nền tảng.

**2b — Bảng nhân vật ra file.** Cùng khuôn với `WorldRules`, và **cùng kết luận**: `CharacterProfile`
hiện có đang là kiểu chạy; cho nó thêm hai vai "đọc từ file" và "đi trên dây" là đủ, không đẻ thêm
`CharacterProfileData` nào cả.

Hai thay đổi trên kiểu đã có:

- `CharacterProfile` đổi từ *bất biến, dựng bằng hàm khởi tạo* sang *`[MemoryPackable]`, property có
  setter*. Mất tính bất biến — và bù lại bằng kỷ luật "chỉ `CharacterProfiles.Load` được đụng vào".
- `ActionDefinition` (readonly struct, quy đổi trong hàm dựng) **biến mất**, nhập vào `ActionData`. Vẫn
  là struct, vẫn quy đổi một lần — chỉ là phép quy đổi dời từ hàm dựng sang `Prepare()`, vì một kiểu
  tuần tự hoá được thì không tự gọi hàm dựng của bạn.

Đọc lại bảng ở đầu Bước 1 thì thấy đây không phải ngoại lệ mà là cùng một tiêu chí: phép chuyển đổi
giữa "kiểu file" và "kiểu chạy" ở đây chỉ là **giây → tick trên ba con số**, không đổi hình dạng gì.
Thứ đó là một `Prepare()`, không phải một class thứ hai.

`Config/characters.json`:

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

`"Action": "Attack"` là **tên enum**, không phải số — Newtonsoft tự chuyển sang `ActionState`, và người
sửa file không phải tra bảng để biết `2` nghĩa là gì. Một id lạ thì Newtonsoft ném, và đó là hành vi
đúng cho file người gõ tay.

`Actions` là **mảng**, không phải object có khoá cố định: thêm một hành động mới ở Phase 15 chỉ là thêm
một phần tử, không đụng schema.

**`CharacterProfiles` đổi từ bảng tĩnh cắm cứng thành bảng tĩnh được NẠP.** Giữ nguyên `Get(classId)` —
mọi chỗ gọi ở client (`WorldSpawner`, `PlayerMotor`, `RemotePlayerView`) không phải sửa một dòng. Thêm
`Load(CharacterTableData)` để thay bảng, và bỏ hàm `Build()` cắm cứng.

Ai gọi `Load`: server gọi sau khi đọc file; client gọi khi nhận `EnterWorldResponse`. **Một bảng, hai
đường vào.**

> Đây là chỗ duy nhất trong phase có state toàn cục thay đổi được, nên phải nói rõ luật của nó: bảng
> chỉ được thay **giữa hai phiên chơi**, không phải giữa chừng. Cùng đúng cái lý do đã chốt
> `WorldRules` vào `PlayerEntity` — mà lần này còn mạnh hơn, vì `PlayerEntity._profile` đang giữ
> reference tới object cũ, nên người đang online **tự động** giữ bộ cũ ngay cả khi bảng bị thay.

**Bảng này cũng phải qua `Validate`.** Năm dòng `MoveSpeed`, `JumpSpeed`, `BodyHalfWidth`, `BodyHeight`,
`BodyHeightCrouch` trong bảng ràng buộc ở Bước 1 thuộc về **file này**, không phải `game.json` — kiểm
chúng ngay trong `ConfigService` sau khi đọc `characters.json`, trước khi gọi `CharacterProfiles.Load`.
Chỗ kiểm phải nằm ở **server**, không ở `CharacterProfiles.Load`: client cũng gọi hàm `Load` đó, và
client thì không có quyền phán xét dữ liệu server gửi xuống — nó chỉ có quyền tin. Đặt phép kiểm vào
`Load` là để client âm thầm sửa số của server, tức tạo ra đúng cái lệch mà cả phase đang chống.

**2c — Bản đồ: chỉ còn phép so.** Không dựng lại pipeline gì — Phase 10 làm xong rồi. Việc còn lại:

- `EnterWorldResponse` và `MapChangedNotice` mang thêm `uint MapChecksum`.
- Client sau khi `MapService.Load(mapId)` thì so `map.Checksum()` với số server gửi. Lệch thì **không
  vào world**: log Error nói rõ hai số, và trả người chơi về màn hình login.

Câu hỏi đúng phải hỏi: *client đọc file trong `Resources` của chính nó, server đọc bản copy trong
`Data/Maps` — hai bản ấy cùng sinh từ một file trong repo, làm sao lệch được?* Lệch được, và bằng đúng
con đường đã cắn bạn ở ba phase trước: bạn export lại map trong Unity nhưng **chưa build lại GameServer**
(bản copy trong `bin/` chỉ cập nhật lúc build). Server vẫn chạy map cũ, client đã vẽ map mới, và triệu
chứng là "tự nhiên có bức tường vô hình ở chỗ này".

Không có phép so này thì triệu chứng đó không có tên. Có nó thì nó tự báo tên ra, kèm hai con số.

### ✅ CHECKPOINT B

1. Server boot in thêm một dòng: `Bảng nhân vật: 1 lớp, checksum XXXXXXXX`.
2. Sửa `MoveSpeed` thành `12` trong `characters.json` → restart server → **client không build lại** →
   chạy nhanh hẳn, **không rubber-band**.
3. Sửa `Actions[Attack].DurationSeconds` thành `1.5` → đòn đánh dài một giây rưỡi **và clip tự chậm lại
   cho vừa**. Không phải sửa gì ở `CharacterAnimator`: nó nhận `CharacterProfile`, mà profile giờ đến từ
   file. Nếu clip vẫn chạy nhanh rồi đứng hình thì bảng chưa tới được client.
4. Export lại một map trong Unity (thêm một ô sàn), **cố tình không build lại GameServer**, vào game →
   client báo **"map lệch phiên bản"** kèm hai checksum, và **không vào world**.
5. Build lại GameServer → vào bình thường, hai checksum trùng.
6. Xoá `"Classes"` khỏi `characters.json` → server boot lên báo lỗi rõ ràng và dùng bảng mặc định, không
   chết.

<details>
<summary><b>📖 Lời giải — <code>Fnv1a</code> và DTO bảng nhân vật</b></summary>

**`Server/Shared/World/Fnv1a.cs`** (file mới):

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

        /// <summary>
        /// Băm BIT của float, không băm chuỗi in ra. 0.1f in ra mấy chữ số là chuyện của ToString và
        /// của culture; bit thì giống nhau ở mọi nền tảng — mà cái ta cần so là giá trị, không phải
        /// cách viết nó.
        /// </summary>
        public static uint Mix(uint hash, float value)
        {
            return Mix(hash, BitConverter.SingleToInt32Bits(value));
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

**`Server/Shared/World/CharacterTableData.cs`** (file mới):

```csharp
using System;
using MemoryPack;

namespace MMORPG.Shared.World
{
    /// <summary>
    /// Các con số của MỘT hành động. Thay cho ActionDefinition của Phase 9 — cùng nội dung, khác ở chỗ
    /// nó đọc được từ file và đi được trên dây.
    ///
    /// Vẫn là STRUCT, và đó không phải chuyện phong cách: <c>default</c> của nó là "0 tick, không khoá
    /// thân", nên <see cref="CharacterProfile.GetAction"/> trả về được một giá trị hợp lệ cho hành động
    /// không có trong bảng mà chỗ gọi không phải kiểm null. Đổi sang class là mọi chỗ gọi mọc thêm một
    /// phép kiểm, và một trong số đó sẽ bị quên.
    /// </summary>
    [MemoryPackable]
    public partial struct ActionData
    {
        /// <summary>Ghi bằng TÊN enum trong file ("Attack"), không phải số — người sửa file không phải tra bảng.</summary>
        public ActionState Action;

        public float DurationSeconds;

        public float CooldownSeconds;

        /// <summary>
        /// Trong lúc hành động này diễn ra thì thân thể có mất quyền điều khiển không. Là dữ liệu chứ
        /// không phải một nhánh switch: thêm chiêu "đứng yên đọc chú" chỉ là thêm một ô true.
        /// </summary>
        public bool LocksMovement;

        // Dẫn xuất — xem ghi chú ở WorldRules, cùng lý do và cùng cặp thuộc tính bỏ qua.
        [MemoryPackIgnore] [JsonIgnore] public int DurationTicks;

        [MemoryPackIgnore] [JsonIgnore] public int CooldownTicks;

        public void Prepare()
        {
            DurationTicks = MovementRules.ToTicks(DurationSeconds);
            CooldownTicks = MovementRules.ToTicks(CooldownSeconds);
        }
    }

    /// <summary>
    /// Cả bảng. Vừa là bản đối chiếu với characters.json, vừa là thứ đi trong EnterWorldResponse —
    /// bảng này nhỏ nên chọn chế độ "gửi cả dữ liệu", và ở chế độ đó thì lệch là chuyện không xảy ra
    /// được. Checksum vẫn có: nó là một dòng log để so hai server, và là chỗ nối cho Phase 18.
    /// </summary>
    [MemoryPackable]
    public sealed partial class CharacterTableData
    {
        public int Version { get; set; } = 1;

        public CharacterProfile[] Classes { get; set; } = Array.Empty<CharacterProfile>();

        /// <summary>
        /// Dấu vân tay của NỘI DUNG bảng. Không băm Name: nó chỉ để người đọc file dễ chịu, đổi nó
        /// không đổi một hành vi nào — mà dấu vân tay phải trả lời "hai bên có chạy cùng luật không".
        /// </summary>
        public uint Checksum()
        {
            uint hash = Fnv1a.START;

            hash = Fnv1a.Mix(hash, Version);

            foreach (CharacterProfile profile in Classes)
            {
                hash = Fnv1a.Mix(hash, profile.ClassId);
                hash = Fnv1a.Mix(hash, profile.MoveSpeed);
                hash = Fnv1a.Mix(hash, profile.JumpSpeed);
                hash = Fnv1a.Mix(hash, profile.BodyHalfWidth);
                hash = Fnv1a.Mix(hash, profile.BodyHeight);
                hash = Fnv1a.Mix(hash, profile.BodyHeightCrouch);

                foreach (ActionData action in profile.Actions)
                {
                    hash = Fnv1a.Mix(hash, (int)action.Action);
                    hash = Fnv1a.Mix(hash, action.DurationSeconds);
                    hash = Fnv1a.Mix(hash, action.CooldownSeconds);
                    hash = Fnv1a.Mix(hash, action.LocksMovement ? 1 : 0);
                }
            }

            return hash;
        }
    }
}
```

**`Server/Shared/World/ActionDefine.cs`** — `ActionDefinition` biến mất, `CharacterProfile` thành kiểu
nạp được, `CharacterProfiles` đổi từ *cắm cứng* sang *nạp được*:

```csharp
    /// <summary>
    /// Bộ số của một lớp nhân vật. Ba vai, một hình dạng: dòng trong characters.json, phần tử của gói
    /// EnterWorld, và bộ số MovementRules.Step đọc.
    ///
    /// Property có setter là cái giá của việc tuần tự hoá được — kiểu bất biến thì không bộ tuần tự
    /// hoá nào dựng được nó. Bù lại bằng kỷ luật, không bằng trình biên dịch: CHỈ
    /// <see cref="CharacterProfiles.Load"/> được ghi vào, và nó chỉ ghi vào object vừa dựng xong,
    /// chưa ai cầm.
    /// </summary>
    [MemoryPackable]
    public sealed partial class CharacterProfile
    {
        public int ClassId { get; set; }

        /// <summary>Tên để đọc log và sửa file cho dễ. Mô phỏng không dùng, nên nó cũng không vào Checksum.</summary>
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

        /// <summary>
        /// Quy giây ra tick cho mọi hành động. Gọi từ <see cref="CharacterProfiles.Load"/>, tức là
        /// đúng một chỗ ở mỗi bên — cùng cơ chế với <see cref="WorldRules.Prepare"/>.
        /// </summary>
        public void Prepare()
        {
            // for chứ không foreach: ActionData là struct, foreach cho ra BẢN COPY và Prepare() sẽ ghi
            // vào bản copy ấy rồi vứt đi. Không lỗi, không cảnh báo, chỉ là mọi thời lượng bằng 0 —
            // đúng mặt trái của tính chất "gán là copy" đã cứu vòng replay ở Phase 8.
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

    /// <summary>
    /// Bảng tra profile theo lớp nhân vật. Bảng TĨNH nhưng NẠP ĐƯỢC: server nạp từ file lúc boot,
    /// client nạp từ gói EnterWorld. Một bảng, hai đường vào, một hàm dựng — nên hai bên không có cửa
    /// nào cầm hai bộ số khác nhau.
    ///
    /// Chỉ được thay bảng GIỮA HAI PHIÊN chơi. Entity đang sống giữ reference tới CharacterProfile cũ
    /// (PlayerEntity._profile), nên thay bảng giữa chừng không kéo được người đang online sang bộ mới —
    /// và đó là hành vi đúng, không phải thiếu sót.
    /// </summary>
    public static class CharacterProfiles
    {
        public const int DRAGON_WARRIOR = 1;

        private static Dictionary<int, CharacterProfile> _byClassId = new();

        /// <summary>Checksum của bảng đang nạp — để in log và để so hai đầu dây.</summary>
        public static uint Checksum { get; private set; }

        public static CharacterProfile Get(int classId)
        {
            if (_byClassId.TryGetValue(classId, out CharacterProfile profile))
                return profile;

            // Bảng rỗng (chưa nạp) thì đây là chỗ duy nhất phát hiện ra, và nó phải ném chứ không trả
            // null: null đi tiếp vài tầng rồi mới nổ ở MovementRules.Step, xa chỗ gây ra nó.
            if (_byClassId.Count == 0)
                throw new InvalidOperationException("CharacterProfiles chưa được Load. Server nạp lúc boot, client nạp khi vào world.");

            return _byClassId[DRAGON_WARRIOR];
        }

        /// <summary>
        /// Thay cả bảng bằng dữ liệu vừa đọc (server) hoặc vừa nhận (client). Dựng NGUYÊN bảng mới rồi
        /// mới gán — xem câu 4 phần tự kiểm tra.
        ///
        /// Đây cũng là chỗ DUY NHẤT gọi Prepare(), nên không có đường nào để một profile lọt vào bảng
        /// mà chưa quy ra tick.
        /// </summary>
        public static void Load(CharacterTableData table)
        {
            var built = new Dictionary<int, CharacterProfile>();

            foreach (CharacterProfile profile in table.Classes)
            {
                profile.Prepare();
                built[profile.ClassId] = profile;
            }

            _byClassId = built;
            Checksum = table.Checksum();
        }
    }
```

</details>

---

## Bước 3 — Giết hẳn con bug "DLL cũ"

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

### ✅ CHECKPOINT C — mục tiêu cuối Phase 12

1. Server boot in: `Contract hash=XXXXXXXX`. Client lúc nối in đúng số đó.
2. Thêm một giá trị bất kỳ vào `NetCmd`, build `Shared`, **khôi phục lại DLL cũ trong `Assets/Plugins/Shared/`**
   (giả lập đúng cảnh "quên copy"), chạy client → client báo **"phiên bản không khớp"** kèm hai số, và
   **không vào được màn hình login**.
3. Copy DLL đúng → vào bình thường.
4. Thử đường vòng: sửa client bỏ qua bước `VersionCheck`, gửi thẳng `Login` → server từ chối bằng
   `NotAuthenticated`. Phép chặn nằm ở dispatcher, không nằm ở thiện chí của client.
5. Chạy lại toàn bộ CHECKPOINT A và B — không cái nào được vỡ.

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
    /// MovementRules.Step thì số này y nguyên. Đọc thêm ở "Để dành" của Phase 12.
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
        /// cố tình hạ về Connected chứ không về Verified — xem bên dưới.
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

---

## Ba thử nghiệm bắt buộc

**1. Config rác.**
Ghi `"Gravity": "nặng lắm"` → restart: server sống, log Warn, dùng mặc định. Ghi JSON hỏng hẳn (thiếu
một dấu `}`): như trên. Ghi `"MaxFallSpeed": 60` → Warn về đúng trường đó. Ghi `"Gravty": 30` → báo
trường lạ.

Server không bao giờ được chết vì file người vận hành gõ tay — nhưng cũng không bao giờ được **im lặng**
chạy bằng số khác với số họ tưởng.

**2. Client cứng đầu.**
Sửa tạm client bỏ qua `response.World`, dự đoán bằng một `WorldRules` tự chế với `Gravity = 10` →
rubber-band liên tục theo chiều dọc, server thắng. Kết luận của Phase 6 vẫn nguyên giá trị khi số đã
thành dữ liệu. Trả lại code.

**3. Đo cái gì thật sự đổi khi hot reload.**
Hai client online. Sửa `Gravity` rồi gõ `R`. Ghi lại: (a) log server in số mới; (b) hai người đang chơi
**không đổi gì**; (c) một người relog thì chỉ người đó đổi — **hai người chơi hai bộ số khác nhau trong
cùng một thế giới, và không ai rubber-band**, vì ai cũng dự đoán bằng đúng bộ server dùng cho mình.

Rồi thử ngược lại: sửa `PlayerEntity.Integrate` cho nó đọc `ConfigService.Current` mỗi tick, gõ `R`, và
xem cả hai người giật cùng lúc. Đây là cách nhanh nhất để hiểu vì sao "tươi hơn" không phải lúc nào cũng
đúng hơn. Trả lại code.

---

## Troubleshooting

| Triệu chứng | Nguyên nhân thường gặp | Chỗ sửa |
|---|---|---|
| Server báo không đọc được config dù file có | chạy từ thư mục khác, hoặc csproj chưa copy | kiểm `bin/Debug/net8.0/Data/Config/game.json` có tồn tại |
| Build xong mà `Data/Config/` rỗng | chưa tạo `Config/` ở **gốc repo** — glob không khớp file nào thì MSBuild im lặng | tạo thư mục + hai file; thêm target `CheckConfigFolder` giống `CheckMapFolder` để lần sau nó thành lỗi build |
| Phím `R` không có tác dụng, phím `H`/`K`/`J` cũng không | khối `new Thread(...)` đặt **sau** vòng `while … AcceptTcpClientAsync` nên chỉ chạy lúc server tắt | chuyển cả khối lên **trước** vòng accept |
| Nhảy lúc được lúc không, coyote time như biến mất | quên `WorldRules.Prepare()` → ba bộ đếm bằng 0 | `ConfigService.Load` (server) · `LocalPlayer.Apply` (client) |
| Mọi hành động kết thúc ngay tick sau (đánh không ra đòn) | quên `CharacterProfile.Prepare()`, hoặc `Prepare()` chạy trên **bản copy** của struct vì dùng `foreach` thay `for` | `CharacterProfile.Prepare` |
| Đăng xuất xong không đăng nhập lại được, lỗi `NotAuthenticated` | `MarkLoggedOut` hạ `State` về `Connected` trong khi `Login` cần `Verified` | `ClientSession.MarkLoggedOut` |
| Sửa json mà số không đổi | đang sửa file ở gốc repo, nhưng bản copy trong `bin/` chỉ cập nhật lúc **build** — `R` đọc bản trong `bin/` | build lại, hoặc sửa thẳng bản trong `bin/` khi thử nhanh |
| `JsonSerializationException: Could not find member` | đúng chủ đích — `MissingMemberHandling.Error`. Tên trường gõ sai | đối chiếu tên với `GameConfigData` |
| Rubber-band dọc sau khi đổi `Gravity` | client còn chỗ dùng `WorldRules` cũ — hay gặp nhất là **vòng replay** trong `OnMoveStateResult` | tìm mọi lời gọi `Step` trong `PlayerMotor` |
| Người online bị giật ngay khi bấm `R` | `Integrate` đọc `ConfigService.Current` mỗi tick thay vì `_world` chốt lúc dựng | `PlayerEntity` |
| Hoạt ảnh đòn đánh không khớp thời lượng luật | bảng nhân vật chưa tới client, animator đang dùng bảng cắm cứng cũ | `CharacterProfiles.Load` phía client, gọi trong `LocalPlayer.Apply` |
| `InvalidOperationException: CharacterProfiles chưa được Load` ở client | có chỗ gọi `Get` **trước** khi `EnterWorldResponse` về | thứ tự trong `WorldPresenter.OnEnterWorldResult` — `Load` bảng trước, spawn sau |
| Nhân vật kẹt cứng không đi được sau khi sửa `characters.json` | `BodyHalfWidth >= 0.5` — thân rộng hơn khe 1 ô | `Validate`, và đọc dòng Warn |
| Client báo map lệch checksum mà file map vừa export | quên build lại GameServer → bản trong `Data/Maps` còn cũ | đó chính là việc phép kiểm này sinh ra để làm |
| Hai checksum map lệch dai dẳng dù đã build | còn file map **cũ** sót trong `bin/.../Data/Maps` (đổi tên file thì `CopyToOutputDirectory` không xoá bản cũ) | `dotnet clean Server/GameServer` |
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

**Câu 2.** `WorldRules` chốt vào `PlayerEntity` lúc dựng thay vì `Integrate` đọc `ConfigService.Current`
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

**Câu 7.** Bản đồ và bảng nhân vật cùng là loại B, nhưng bản đồ phải so checksum còn bảng nhân vật thì
"không lệch được". Khác nhau ở đâu?
<details>
<summary><b>📖 Đáp án câu 7</b></summary>

Ở **chế độ phân phối**, không ở loại. Bảng nhân vật nhỏ nên server **gửi cả dữ liệu** trong
`EnterWorldResponse` — client dựng bảng từ đúng byte server vừa gửi, nên không tồn tại bản thứ hai để mà
lệch. Bản đồ thì client **giữ bản riêng** trong `Resources/` (nó cần cả map để dự đoán va chạm và để
dựng hình), server giữ bản copy trong `Data/Maps` — hai bản, sinh ra từ một nguồn nhưng **cập nhật bằng
hai thao tác khác nhau** (export trong Unity · build lại GameServer). Chỗ nào có hai thao tác thì chỗ đó
quên được một.

Chọn chế độ theo **kích thước**. Nhưng **trường version có ở cả hai** ngay từ ngày đầu: hôm nay bảng
nhân vật chỉ dùng nó làm một dòng log để so hai server, tới Phase 18 thì chính nó là thứ cho phép đổi
sang chế độ "client cache, server chỉ gửi hash" mà không phải đổi contract.

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

---

## Để dành (ghi lại, chưa làm)

- **Đẩy config nóng cho người đang online.** Một gói `ConfigUpdate` broadcast khi reload, client thay
  `WorldRules` và server thay `entity._world` **trong cùng một tick**. Cạm bẫy: giữa lúc client còn input
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
- **Bảng nhân vật theo level.** `CharacterProfile` hôm nay là một dòng cho mỗi lớp; Phase 14 sẽ cần
  `base(class, level)`, tức bảng hai chiều. Schema hôm nay chịu được việc đó bằng cách thêm một mảng,
  không phải đổi format.

---

**Xong Phase 12 → hết Chặng C.** Thế giới sống: nhiều người thấy nhau ở phạm vi có giới hạn, map có hình
dạng thật, nhân vật biết diễn, và mọi con số đều là **dữ liệu** chứ không phải hằng số trong code.

Chặng D bắt đầu vòng gameplay thật — [PHASE-13](PHASE-13.md): túi đồ, feature dọc đầu tiên đi đủ
DB → DAL → logic → packet → UI, khuôn mẫu cho mọi feature về sau. Nó cũng là nơi **bảng item** trở thành
bảng loại B thứ ba, đi đúng con đường mà `Fnv1a` và `CharacterTableData` vừa mở ra hôm nay.
