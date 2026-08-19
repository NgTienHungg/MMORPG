# PHASE 10 — Data & Config: số ra khỏi code

> **Kết quả cuối Phase 10:** tốc độ chạy, điểm spawn, danh sách nghề... nằm trong file `game.json` —
> sửa file, restart server (không build lại gì), giá trị mới có hiệu lực **ở cả server lẫn client**.
> Thêm hot reload: gõ `R` trong console server là nạp lại config không cần restart.
>
> **Điều kiện:** xong [`PHASE-9.md`](PHASE-9.md) tới CHECKPOINT B và cả 3 thử nghiệm.
>
> **Bài học chính:** config cũng phải có **đúng một nguồn** — và nguồn đó là *server*. Client không đọc
> file config nào cả: giá trị nó cần được server **gửi xuống lúc vào world**. Đổi số liệu game là việc
> của server; client chỉ là màn hình.
>
> ⚠️ **Doc này viết khi dự án còn là top-down (lúc đó là Phase 9), và chỉ bàn config *loại A*.**
> Cần bổ sung trước khi làm: (1) `GameConfig` thêm `Gravity`, `JumpForce`, `MaxFallSpeed` của Phase 8;
> (2) một mục về **config loại B** — bảng dữ liệu mà cả client lẫn server đều đọc (bảng item, quái,
> chỉ số theo class). Xem bảng so sánh 2 loại ở [`ROADMAP.md`](../ROADMAP.md) §2b.

Format như trước: **hướng làm** hiện sẵn, **📖 Lời giải** trong foldout.

---

## Vì sao client không được đọc file config

Trực giác đầu tiên của mọi người: "để hai bên cùng đọc `game.json` cho đồng bộ". Nghe giống contract
1 nguồn — nhưng là bẫy, vì **file giống nhau không có nghĩa là giá trị đang chạy giống nhau**:

- Client build ra mang bản copy của file tại thời điểm build. Server sửa config → mọi client ngoài kia
  đang chạy số cũ. Đây chính xác là bug "chép tay NetCmd" ở dạng dữ liệu.
- Người chơi sửa được file trong máy họ. Với giá trị chỉ-hiển-thị thì vô hại; với `moveSpeed` mà client
  dùng để dự đoán thì là mời họ tự chỉnh — server vẫn thắng (Phase 6), nhưng họ tự gây rubber-band và
  đi report "game lag".

Cách đúng rẻ hơn nhiều: **chỉ server đọc file**; giá trị nào client cần cho dự đoán/hiển thị thì đi
trong `EnterWorldResponse`. Client luôn chạy đúng số của server nó đang nối vào — kể cả khi hai server
khác nhau cấu hình khác nhau. Hằng số `MOVE_SPEED` trong `MovementRules` vì thế phải "giáng cấp" từ
hằng số thành **tham số**.

```
Config/game.json ──► GameServer đọc lúc boot (hot reload: phím R)
                          │
                          ├─► WorldService / CharacterService dùng trực tiếp
                          └─► EnterWorldResponse { MoveSpeed, ... } ──► client dùng cho dự đoán
```

Cái gì **không** vào config phase này: `TICK_RATE` (đổi nó là đổi nhịp của mọi phép tính prediction —
để yên), dữ liệu map (`Maps.Map1` giữ trong code — đưa map ra file + gửi qua mạng là bài riêng, phần
"Để dành" cuối doc), và các giá trị thuần client (màu sắc, âm lượng — đó là đất của `com.hungnt.datasave`
/ ScriptableObject, không liên quan server).

---

## Bước 1 — Shared: schema + `Step` nhận speed từ ngoài

### Hướng làm

**File mới `Server/Shared/World/GameConfig.cs`** — POCO thuần mô tả *hình dạng* config (schema nằm trong
Shared để sau này ai cần cũng nói cùng một ngôn ngữ; còn việc *đọc file* là của riêng server):

```csharp
public sealed class GameConfig
{
    public float MoveSpeed { get; set; } = 5f;
    public int SpawnMapId { get; set; } = 1;
    public float SpawnX { get; set; }
    public float SpawnY { get; set; }
    public int DefaultClassId { get; set; } = 1;
}
```

Mỗi property có **giá trị mặc định hợp lệ** — file thiếu trường nào thì trường đó về mặc định thay vì
nổ. Đó là chính sách "config hỏng một phần, game vẫn đứng dậy được".

**Sửa `MovementRules`**: xoá `const MOVE_SPEED`, `Step` nhận thêm `float speed`. Compile sẽ đỏ ở mọi
chỗ gọi — tốt, trình biên dịch đang lập danh sách việc hộ bạn: server truyền từ config, client truyền
từ giá trị server gửi xuống (Bước 3).

**Sửa `EnterWorldResponse`**: thêm `float MoveSpeed` — giá trị server đang dùng, cấp cho client ngay
lúc vào world.

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`Server/Shared/World/GameConfig.cs`**:

```csharp
namespace MMORPG.Shared.World
{
    /// <summary>
    /// Hình dạng của config game. Chỉ server đọc file; client nhận giá trị nó cần qua mạng
    /// (EnterWorldResponse) — nhờ vậy client luôn chạy đúng số của server đang nối vào.
    /// Mọi trường đều có mặc định hợp lệ: file thiếu trường nào thì trường đó về mặc định.
    /// </summary>
    public sealed class GameConfig
    {
        /// <summary>Tốc độ chạy, đơn vị world/giây.</summary>
        public float MoveSpeed { get; set; } = 5f;

        public int SpawnMapId { get; set; } = 1;
        public float SpawnX { get; set; }
        public float SpawnY { get; set; }

        /// <summary>Nghề gán cho nhân vật tạo lần đầu.</summary>
        public int DefaultClassId { get; set; } = 1;
    }
}
```

**`MovementRules.cs`** — bỏ `MOVE_SPEED`, `Step` thành:

```csharp
        public static (float X, float Y) Step(float x, float y, float dirX, float dirY,
                                              float speed, float dt, MapGrid map)
        {
            float nx = x + dirX * speed * dt;
            float ny = y + dirY * speed * dt;
            // ... phần va chạm tách trục giữ nguyên ...
        }
```

**`EnterWorldResponse`** — thêm:

```csharp
        /// <summary>Tốc độ chạy server đang áp dụng. Client dùng đúng số này để dự đoán.</summary>
        public float MoveSpeed { get; set; }
```

</details>

---

## Bước 2 — Server: đọc file, phát giá trị, hot reload

### Hướng làm

**File config `Config/game.json`** đặt ở **gốc repo** (cạnh `Server/`, `Assets/` — nó là dữ liệu vận
hành, không phải source của riêng process nào):

```json
{
  "MoveSpeed": 5.0,
  "SpawnMapId": 1,
  "SpawnX": 0,
  "SpawnY": 0,
  "DefaultClassId": 1
}
```

Cho GameServer thấy file: trong `GameServer.csproj` thêm `ItemGroup` copy `..\..\Config\game.json`
vào output (`CopyToOutputDirectory=PreserveNewest`, `Link=Config\game.json`) — chạy từ Rider hay
`dotnet run` đều tìm thấy ở `Config/game.json` cạnh exe.

**File mới `Server/GameServer/ConfigService.cs`**:

- `Load()`: đọc + parse JSON (`System.Text.Json`), lỗi gì (file thiếu, JSON hỏng) → log Warn và dùng
  `new GameConfig()` mặc định — **config hỏng không được giết server**, nhưng phải la lớn trong log.
- `Current` — property trả `GameConfig` hiện hành. Hot reload là **thay nguyên object**
  (`_current = mới`), không sửa từng field trên object cũ: ai đã cầm reference cũ vẫn thấy một bộ giá
  trị nhất quán; gán reference là nguyên tử nên không cần lock. Đây là bài "immutable swap" — cùng họ
  với cách xử input đa luồng ở Phase 6.
- Vòng đọc phím trong `Program.cs`: `R` → `Load()` lại + log giá trị mới.

**Ai dùng config ở đâu:**

- `CharacterService.EnterWorldAsync`: defaults khi tạo nhân vật (`DefaultClassId`, `SpawnMapId/X/Y` —
  thay các `const` của `WorldService`, xoá chúng đi) và gắn `MoveSpeed` vào response.
- **Chốt speed vào entity lúc spawn**: `PlayerEntity.MoveSpeed` gán một lần từ config — `Integrate`
  dùng nó, KHÔNG đọc `Current` mỗi tick. Vì client dự đoán bằng số nhận lúc vào world; nếu server đổi
  số giữa chừng qua hot reload thì người đang online sẽ lệch dự đoán → rubber-band oan. Luật: **hot
  reload áp dụng cho người vào sau**; người đang online giữ số cũ tới lần vào world sau. (Muốn đổi
  nóng cho người đang chơi thì phải đẩy gói báo số mới — ghi vào "Để dành".)

### ✅ CHECKPOINT A

1. Server boot log: `Config: MoveSpeed=5 Spawn=(0,0)@map1 ...`.
2. Sửa `MoveSpeed` thành `8` trong file → **không** build lại, restart server → log số mới → vào game
   chạy nhanh hơn rõ rệt, **không rubber-band** (client nhận 8 qua EnterWorld).
3. Xoá tạm file `game.json` khỏi output → server vẫn boot, log Warn + dùng mặc định.
4. Đang chạy: sửa file thành `6`, gõ `R` trong console server → log reload. Người đang online **vẫn
   chạy 8** (đúng thiết kế); relog → chạy 6.

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`Server/GameServer/GameServer.csproj`** — thêm:

```xml
  <ItemGroup>
    <None Include="..\..\Config\game.json" Link="Config\game.json" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
```

**`Server/GameServer/ConfigService.cs`**:

```csharp
using System.Text.Json;
using MMORPG.ServerCore;
using MMORPG.Shared.World;

namespace MMORPG.GameServer
{
    /// <summary>
    /// Nạp và giữ config game. Nguồn duy nhất về "giá trị đang chạy" trong toàn server.
    /// </summary>
    public sealed class ConfigService
    {
        private const string CONFIG_PATH = "Config/game.json";

        // Hot reload = THAY nguyên object, không sửa field trên object cũ. Gán reference là
        // nguyên tử: ai đang cầm bản cũ vẫn thấy một bộ giá trị nhất quán, không cần lock.
        private volatile GameConfig _current = new();

        public GameConfig Current => _current;

        public void Load()
        {
            try
            {
                string json = File.ReadAllText(CONFIG_PATH);
                GameConfig loaded = JsonSerializer.Deserialize<GameConfig>(json);

                // Deserialize trả null khi file chứa đúng chữ "null" — hiếm nhưng rẻ để chặn.
                _current = loaded ?? new GameConfig();
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                // Config hỏng không được giết server — nhưng phải la lớn, vì chạy bằng số
                // mặc định trong khi vận hành tưởng là số trong file mới là thảm hoạ âm thầm.
                Log.Warn($"Không đọc được {CONFIG_PATH} ({ex.GetType().Name}: {ex.Message}) — dùng mặc định.");
                _current = new GameConfig();
            }

            GameConfig c = _current;
            Log.Info($"Config: MoveSpeed={c.MoveSpeed.ToString().Green()} " +
                     $"Spawn=({c.SpawnX},{c.SpawnY})@map{c.SpawnMapId} DefaultClass={c.DefaultClassId}");
        }
    }
}
```

**`Program.cs`** — sau phần tạo services:

```csharp
var configService = new ConfigService();
configService.Load();
```

truyền `configService` vào `CharacterService` (constructor thêm tham số), và thêm vòng đọc phím
(trước vòng accept, chạy nền):

```csharp
// Console điều khiển: R = nạp lại config. Chạy trên thread riêng vì Console.ReadKey chặn.
_ = Task.Run(() =>
{
    while (!cts.IsCancellationRequested)
    {
        if (Console.ReadKey(intercept: true).Key == ConsoleKey.R)
            configService.Load();
    }
});
```

**`PlayerEntity`** — thêm:

```csharp
        /// <summary>
        /// Tốc độ chạy, chốt MỘT lần lúc spawn từ config — không đọc config mỗi tick.
        /// Client dự đoán bằng đúng số nhận lúc vào world; server mà đổi số giữa phiên
        /// thì dự đoán của họ lệch và rubber-band oan. Hot reload chỉ áp dụng cho người vào sau.
        /// </summary>
        public float MoveSpeed { get; }
```

(gán trong constructor — constructor nhận thêm `float moveSpeed`; `Integrate` truyền `MoveSpeed` vào
`Step`.)

**`CharacterService`** — nhận `ConfigService` qua constructor; trong `EnterWorldAsync`:

```csharp
            GameConfig config = _configService.Current;
```

dùng `config.DefaultClassId / SpawnMapId / SpawnX / SpawnY` cho `CharacterGetOrCreateRequest` (xoá các
`const` tương ứng trong `WorldService`), truyền `config.MoveSpeed` vào `_worldService.Spawn(...)` →
constructor entity, và response thêm:

```csharp
                MoveSpeed = entity.MoveSpeed,
```

</details>

---

## Bước 3 — Client: dùng số server đưa

### Hướng làm

Ba chỗ, đều nhỏ:

- `LocalPlayer`: thêm `MoveSpeed { get; private set; }`, gán trong `Apply` — cache server-confirmed,
  đúng luật cũ.
- `WorldSpawner.SpawnLocalPlayer`: truyền speed vào `motor.Init(...)`.
- `PlayerMotor`: nhận `float moveSpeed` trong `Init`, dùng nó ở **cả ba** chỗ: bước dự đoán, vòng replay
  trong `OnMoveState`, và tốc độ `MoveTowards` hiển thị. Sót chỗ nào là rubber-band ở đúng chỗ đó —
  compile lỗi (vì `Step` đổi chữ ký) sẽ chỉ tận nơi.

Client **không đọc file nào**, không copy `game.json` vào build — đó là toàn bộ ý của phase.

### ✅ CHECKPOINT B — mục tiêu cuối Phase 10

1. `MoveSpeed = 8` trong json, restart server, client **không build lại** → chạy nhanh hơn, mượt,
   không rubber-band.
2. Hai client cùng online, server hot reload sang `6` → cả hai vẫn chạy 8 (số chốt theo phiên);
   một người relog → người đó chạy 6, người kia vẫn 8 — hai người khác tốc độ nhưng **không ai
   rubber-band**, vì ai cũng dự đoán bằng đúng số server dùng cho mình.
3. Đổi `SpawnX/SpawnY` trong json → tài khoản **mới** spawn ở chỗ mới (tài khoản cũ vào lại vẫn ở vị trí
   đã lưu của họ — hiểu vì sao: spawn config chỉ dùng lúc *tạo* nhân vật).

---

## Ba thử nghiệm bắt buộc

**1. Config rác.** Ghi `"MoveSpeed": "nhanh lắm"` vào json → restart: server sống, log Warn, dùng mặc
định. Ghi JSON hỏng hẳn (thiếu dấu `}`): như trên. Server không bao giờ được chết vì file người vận hành
gõ tay.

**2. Client cứng đầu.** Sửa tạm client bỏ qua `response.MoveSpeed`, dự đoán bằng số tự chọn (10) →
rubber-band liên tục, server thắng — kết luận của Phase 6 vẫn nguyên giá trị khi số thành dữ liệu.
Trả lại code.

**3. Giá trị vô lý.** `MoveSpeed: -5` hoặc `0` trong file — chuyện gì xảy ra? (Đi lùi input! / đứng
liệt.) Thêm một lớp kiểm hợp lệ vào `Load()`: giá trị ngoài khoảng chấp nhận (`<= 0` hoặc `> 50`) →
Warn + dùng mặc định cho trường đó. Config đến từ con người; con người gõ nhầm.

---

## Troubleshooting

| Triệu chứng | Nguyên nhân | Xử lý |
|-------------|-------------|-------|
| Server báo không đọc được config dù file có | Chạy từ thư mục khác (working dir ≠ cạnh exe) hoặc csproj chưa copy | Kiểm `bin/Debug/net8.0/Config/game.json` tồn tại |
| Sửa json mà số không đổi | Sửa file ở gốc repo nhưng bản copy trong `bin/` chưa cập nhật (chỉ copy lúc build) — hot reload `R` đọc bản trong `bin/` | Build lại (copy chạy), hoặc sửa thẳng bản trong `bin/` khi thử nghiệm nhanh |
| Rubber-band sau khi đổi speed | Client còn chỗ dùng số cũ/hằng số cũ (một trong ba chỗ của `PlayerMotor`) | Tìm mọi lời gọi `Step` + `MoveTowards` |
| Người online bị rubber-band ngay khi bấm `R` | `Integrate` đọc `Current` mỗi tick thay vì dùng `entity.MoveSpeed` chốt lúc spawn | Đọc lại comment trên `PlayerEntity.MoveSpeed` |
| `JsonException` property không khớp | Tên trường json khác tên property (JSON mặc định phân biệt hoa thường theo cấu hình serializer) | Giữ tên trường trùng property PascalCase, hoặc bật `PropertyNameCaseInsensitive` |
| Nhân vật mới spawn trong tường sau khi đổi SpawnX/Y | Config trỏ vào ô `#` — người gõ config không nhìn map | Thêm kiểm `IsWalkableWorld(spawn)` lúc `Load()`: Warn + giữ mặc định |
| Client build lỗi thiếu `MOVE_SPEED` | DLL Shared mới nhưng code client chưa sửa hết theo chữ ký `Step` mới | Đó là danh sách việc — sửa từng chỗ đỏ |

---

## Tự kiểm tra hiểu bài

Tự trả lời từng câu xong mới mở đáp án của câu đó.

**Câu 1.** Vì sao "client và server cùng đọc chung một file config" nghe giống contract 1 nguồn nhưng
thực ra là bẫy? Nêu hai kịch bản cụ thể nó hỏng.
<details>
<summary>📖 Đáp án câu 1</summary>

Vì thứ cần đồng bộ là **giá trị đang chạy**, không phải nội dung file. (1) Client build mang bản copy
tại thời điểm build — server sửa file xong, mọi client ngoài kia chạy số cũ, lệch mà không ai báo;
(2) file nằm trong máy người chơi thì người chơi sửa được — với giá trị tham gia dự đoán là tự gây
rubber-band, với giá trị hiển thị là hiện sai thông tin. Server phát giá trị qua mạng thì cả hai kịch
bản biến mất về mặt cấu trúc.

</details>

**Câu 2.** Vì sao `MoveSpeed` chốt vào `PlayerEntity` lúc spawn thay vì `Integrate` đọc
`ConfigService.Current` mỗi tick — "tươi" hơn cơ mà?
<details>
<summary>📖 Đáp án câu 2</summary>

Vì bên kia đầu dây có một client đang **dự đoán bằng số nó nhận lúc vào world**. Server đổi số giữa
phiên (hot reload) thì mọi dự đoán của người đang online lệch ngay lập tức → rubber-band hàng loạt,
oan (họ không làm gì sai). Tốc độ là một phần của *hợp đồng phiên chơi*: chốt lúc vào, đổi thì phải
thông báo (đẩy gói số mới) — chưa làm cơ chế thông báo thì chưa được đổi ngầm.

</details>

**Câu 3.** Config hỏng → server dùng mặc định và chạy tiếp, trong khi CLAUDE.md cấm nuốt lỗi. Biện minh
— và điểm nào trong cách xử lý là bắt buộc để nó không thành "nuốt lỗi"?
<details>
<summary>📖 Đáp án câu 3</summary>

Lựa chọn thật là: chết ngay lúc boot vì một dấu phẩy, hay đứng dậy bằng bộ giá trị an toàn đã biết.
Với dữ liệu do người vận hành gõ tay, phương án hai đúng hơn — *miễn là* (1) chỉ bắt đúng loại lỗi
dự kiến (`IOException`, `JsonException` — bug code vẫn ném lên), và (2) **log Warn to rõ**: chạy bằng
mặc định trong khi vận hành tưởng là số trong file mới là thảm hoạ âm thầm. Nuốt lỗi bị cấm là nuốt
*không dấu vết, không chủ đích* — đây là xử lý có chính sách.

</details>

**Câu 4.** Hot reload thay nguyên object `GameConfig` thay vì sửa từng field trên object đang dùng.
Cơ chế nào làm cách này an toàn đa luồng mà không cần lock, và nó cùng họ với bài nào ở Phase 6?
<details>
<summary>📖 Đáp án câu 4</summary>

Gán một reference là thao tác **nguyên tử** — luồng khác hoặc thấy trọn object cũ, hoặc trọn object
mới, không bao giờ thấy nửa nọ nửa kia; object cũ bất biến từ lúc phát hành nên ai đang cầm cứ dùng
tiếp một bộ giá trị nhất quán. Sửa từng field trên object sống thì luồng đọc có thể thấy
`MoveSpeed` mới + `SpawnX` cũ. Cùng họ với đáp án câu 9 Phase 6: nhiều giá trị phải nhất quán như một
khối → gói vào object bất biến, trao đổi bằng một phép gán reference.

</details>

**Câu 5.** Đổi `SpawnX/SpawnY` trong config, người chơi cũ vào lại vẫn đứng chỗ cũ của họ. Đây là bug
hay tính năng? Nó tiết lộ gì về hai loại dữ liệu trong bảng `character`?
<details>
<summary>📖 Đáp án câu 5</summary>

Tính năng. Spawn config là giá trị **khởi tạo** — chỉ dùng đúng một lần lúc *tạo* nhân vật; từ đó vị
trí là **trạng thái của người chơi**, thuộc về họ, lưu trong DB, config không có quyền đè. Ranh giới
này (giá trị khởi tạo vs trạng thái tích luỹ) chính là ranh giới giữa "dữ liệu game design" và "dữ
liệu người chơi" — nhầm bên là hoặc reset đồ người ta (đè trạng thái bằng config), hoặc không tài nào
cân bằng lại game (trạng thái hoá thứ đáng lẽ là config).

</details>

**Câu 6.** `TICK_RATE` cố tình không vào config. Điều gì gãy nếu người vận hành đổi nó từ 20 thành 30
trong file?
<details>
<summary>📖 Đáp án câu 6</summary>

`TICK_DT` ăn theo (`1/TICK_RATE`) — toàn bộ nhịp prediction/reconciliation của client xây trên giả
định hai bên cùng nhịp: client bơm input 20 bước/giây trong khi server tiêu 30 tick/giây, replay của
client tính mỗi input một `TICK_DT` khác server → dự đoán lệch hệ thống, rubber-band toàn dân. Nó không
phải "số liệu game" mà là **hằng số của giao thức** — cùng đẳng cấp với format khung gói tin; đổi nó là
đổi protocol, phải đổi bằng build có chủ đích ở cả hai phía, không phải bằng file text lúc nửa đêm.

</details>

---

## Để dành (ghi lại, chưa làm)

- **Đẩy config nóng cho người đang online**: gói `ConfigUpdate` server broadcast khi reload → client
  cập nhật speed đang dùng + entity server đổi theo. Làm khi có nhu cầu thật.
- **Map ra file + gửi qua mạng lúc EnterWorld**: map là payload lớn đầu tiên (> 4KB) — dịp để đường nén
  LZ4 của Phase 2 chạy thật. Kèm bài toán cache client (hash map, chỉ tải khi đổi) — chạm vào tư duy
  của Phase 16 (hot update asset).
- **Config từ Google Sheet** (pipeline của `com.hungnt.dataconfig`): xuất sheet → json. Đáng làm khi
  bảng số bắt đầu dày (Phase 11–13: item, quái, damage).

---

**Xong Phase 10 → hết Chặng C.** Thế giới sống: nhiều người thấy nhau, map có hình, số liệu là dữ liệu.
Chặng D bắt đầu vòng gameplay thật — [PHASE-11](PHASE-11.md): túi đồ, feature dọc đầu tiên đi đủ
DB → DAL → logic → packet → UI, khuôn mẫu cho mọi feature về sau. (Viết khi bạn báo xong Phase 10.)
