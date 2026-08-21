# PHASE 11 — Data & Config: số ra khỏi code

> **Kết quả cuối Phase 11:** trọng lực, lực nhảy, coyote time, thời lượng đòn đánh, kích thước thân
> nhân vật, điểm spawn, bề rộng cột AOI — tất cả nằm trong `Config/game.json`. Sửa file, restart server
> (**không build lại gì**), giá trị mới có hiệu lực ở cả server lẫn client. Gõ `R` trong console server
> là nạp lại không cần restart. Và bản đồ ra khỏi code, thành file dữ liệu có **hash phiên bản** —
> client lệch phiên bản thì bị chặn vào world thay vì chơi với một map sai.
>
> **Điều kiện:** xong [`PHASE-10.md`](PHASE-10.md) tới CHECKPOINT C và cả ba thử nghiệm.
>
> **Bài học chính:** (1) config cũng phải có **đúng một nguồn**, và nguồn đó là *server* — client không
> đọc file config nào cả; (2) có **hai loại config** khác nhau về bản chất, và nhầm loại là nguồn của
> lớp bug câm khó chịu nhất trong game online; (3) khi số lượng tham số nổ ra thì đó là dấu hiệu chúng
> vốn đã là **một nhóm**, và nhóm ấy phải được trao đổi nguyên khối.

Format như trước: **hướng làm** hiện sẵn, **📖 Lời giải** trong foldout.

---

## Vì sao client không được đọc file config

Trực giác đầu tiên của mọi người: "để hai bên cùng đọc `game.json` cho đồng bộ". Nghe giống contract
một nguồn — nhưng là bẫy, vì **file giống nhau không có nghĩa là giá trị đang chạy giống nhau**:

- Client build ra mang bản copy của file **tại thời điểm build**. Server sửa config → mọi client ngoài
  kia vẫn chạy số cũ. Đây chính xác là bug "chép tay `NetCmd`" ở dạng dữ liệu.
- Người chơi sửa được file trong máy họ. Với giá trị chỉ-hiển-thị thì vô hại; với `Gravity` mà client
  dùng để dự đoán thì là mời họ tự chỉnh — server vẫn thắng (Phase 6), nhưng họ tự gây rubber-band rồi
  đi report "game lag".

Cách đúng rẻ hơn nhiều: **chỉ server đọc file**; giá trị nào client cần cho dự đoán thì đi trong
`EnterWorldResponse`. Client luôn chạy đúng số của server nó đang nối vào — kể cả khi hai server cấu
hình khác nhau.

```
Config/game.json ──► GameServer đọc lúc boot (hot reload: phím R)
                          │
                          ├─► WorldService / CharacterService dùng trực tiếp
                          └─► EnterWorldResponse { Movement } ──► client dùng cho dự đoán
```

---

## Hai loại config — phân biệt từ đây trở đi

Bốn phase vừa qua đã đẻ ra rất nhiều số, và chúng **không cùng một loại**. Nhầm loại là nguồn của lớp
bug câm khó chịu nhất trong game online, nên phân biệt cho rõ ngay bây giờ:

| | **Loại A — tham số vận hành** | **Loại B — bảng dữ liệu** |
|---|---|---|
| Ví dụ trong dự án | `Gravity`, `JumpSpeed`, `AttackTicks`, điểm spawn, bề rộng cột AOI | **bản đồ**, và sắp tới là bảng item, bảng quái, chỉ số gốc theo class |
| Hình dạng | vài chục con số rời | bảng/mảng, lớn, có cấu trúc |
| Ai cần | server xử lý logic; client cần **một phần** để dự đoán | **cả hai bên đều đọc**: server tính logic, client hiển thị/dự đoán |
| Cách chữa lệch | **chỉ server đọc file**, phần client cần thì đẩy trong `EnterWorldResponse` | **schema** đặt ở `Shared` (một định nghĩa); **dữ liệu** có một bản gốc, client kéo về |
| Chống lệch bằng gì | client luôn chạy đúng số của server nó đang nối vào | server gửi **hash phiên bản**; client lệch thì **bị chặn vào world** |
| Làm ở đâu | Bước 1–3 dưới đây | Bước 4 (bản đồ) · bảng item Phase 12 · phân phối qua CDN Phase 17 |

Điểm dễ hiểu sai nhất: bệnh của cách làm cũ (kiểu vo-lam-genz) **không phải** là "gen file rồi copy
sang cả hai bên". Copy chỉ là triệu chứng. Bệnh thật là **không ai kiểm tra hai bản có khớp nhau
không**. Copy thiếu một lần → client hiển thị item A trong khi server xử lý item B; không lỗi biên
dịch, không log, chỉ có bug câm.

> Cách chữa không phải "đừng copy" — đôi khi buộc phải có hai bản (Phase 10 đã gặp: hình và luật).
> Cách chữa là **làm cho việc lệch bị phát hiện tự động**: loại A thì không tồn tại bản thứ hai, loại B
> thì có hash so lúc đăng nhập.

**Cái gì KHÔNG vào config** — và lý do của từng cái quan trọng ngang bằng danh sách:

| Không vào config | Vì sao |
|---|---|
| `TICK_RATE` | Đây là **hằng số của giao thức**, cùng đẳng cấp với format khung gói tin. Đổi nó là đổi nhịp của toàn bộ prediction/reconciliation ở cả hai bên — phải đổi bằng build có chủ đích, không phải bằng file text lúc nửa đêm |
| `EXPIRED` | Giá trị canh (sentinel) của thuật toán, không phải số liệu game. Chỉnh nó không có nghĩa gì |
| `CELL_SIZE` | Là một phần định nghĩa của format map. Đổi nó là đổi cách đọc mọi bản đồ đã có |
| Màu sắc, âm lượng, phím tắt | Thuần client, không liên quan server. Đó là đất của `com.hungnt.datasave` |

---

## Bước 1 — Shared: gom số thành nhóm, và `Step` nhận nhóm ấy

### Hướng làm

Đếm thử số hằng mà `MovementRules.Step` đang dùng sau bốn phase: `MOVE_SPEED`, `GRAVITY`, `JUMP_SPEED`,
`MAX_FALL_SPEED`, `COYOTE_TICKS`, `JUMP_BUFFER_TICKS`, `ATTACK_TICKS`, `ATTACK_COOLDOWN_TICKS`,
`HURT_TICKS`, `DIE_TICKS`, `DROP_THROUGH_TICKS`, `BODY_HALF_WIDTH`, `BODY_HEIGHT`,
`BODY_HEIGHT_CROUCH` — **mười bốn**.

Bản top-down của doc này chỉ có một (`MOVE_SPEED`) nên nó truyền thẳng: `Step(..., float speed, ...)`.
Làm vậy với mười bốn số thì chữ ký `Step` dài hơn thân hàm, và mỗi lần thêm một hằng là sửa bốn chỗ
gọi.

> Khi số lượng tham số nổ ra, đó gần như luôn là dấu hiệu chúng **vốn đã là một nhóm** — và nhóm ấy
> đáng có một cái tên.

Đây đúng là cái trick đã dùng ở Phase 9 với `MoveState` (12 field đi trên dây thành một object), chỉ
khác chỗ áp dụng. Cùng một bài học, gặp lần thứ hai: **gom lại thì thêm field là sửa một chỗ; tách ra
thì thêm field là sửa mọi chỗ và quên một chỗ thì không có lỗi biên dịch.**

**File mới `Server/Shared/World/MovementConfig.cs`** — đúng những số mà `Step` cần, tức là đúng những
số **client cũng phải biết** (vì client chạy chính `Step` ấy để dự đoán). Mỗi property có **giá trị mặc
định hợp lệ** — file thiếu trường nào thì trường đó về mặc định thay vì nổ.

Đánh dấu `[MemoryPackable]`: nhờ vậy nó đi trong `EnterWorldResponse` như **một** field chứ không phải
mười bốn.

**File mới `Server/Shared/World/GameConfig.cs`** — bọc `MovementConfig` cộng thêm phần **chỉ server
dùng**. Ranh giới giữa hai lớp này là ranh giới "client có cần biết không", và nó đáng nghĩ kỹ:

| Vào `MovementConfig` (client nhận) | Chỉ ở `GameConfig` (server giữ) |
|---|---|
| mọi số `Step` đọc | `SpawnMapId`, `SpawnX`, `SpawnY` — chỉ dùng lúc *tạo* nhân vật |
| | `DefaultClassId` — như trên |
| | `AoiColumnWidth` — client không tính tầm nhìn, nó chỉ nhận cái server gửi |
| | đường dẫn file bản đồ |

`AoiColumnWidth` là ví dụ đáng nhớ nhất: nó là số của *thuật toán server*, không phải luật chơi. Client
không cần biết mình đang được cho xem trong bán kính bao nhiêu — và **không nên** biết, vì đó là thông
tin về cách server hoạt động.

**Sửa `MovementRules`**: xoá 14 `const`, `Step` nhận thêm `MovementConfig cfg`. Compile sẽ đỏ ở mọi chỗ
gọi — tốt, trình biên dịch đang lập danh sách việc hộ bạn. Giữ lại `TICK_RATE`, `TICK_DT`, `EXPIRED` vì
lý do đã nói ở bảng trên.

**Sửa `CharacterStates.DurationTicks`** — nó đang đọc `MovementRules.ATTACK_TICKS`. Giờ phải nhận
`MovementConfig`. Đây là chỗ dễ bỏ sót vì nó không nằm trong `Step`: nó được **client** gọi khi co clip
hoạt ảnh cho vừa số tick. Sót nó thì hoạt ảnh chạy theo số cũ trong khi luật chạy theo số mới — đúng
loại lệch mà cả phase này đang chống.

**Sửa `EnterWorldResponse`**: thêm `MovementConfig Movement`.

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`Server/Shared/World/MovementConfig.cs`** (file mới):

```csharp
using MemoryPack;

namespace MMORPG.Shared.World
{
    /// <summary>
    /// Các số mà luật di chuyển đọc — tức là đúng những số CLIENT cũng phải biết, vì client chạy
    /// chính MovementRules.Step ấy để dự đoán.
    ///
    /// Client KHÔNG đọc file config: nó nhận nguyên object này trong EnterWorldResponse. Nhờ vậy
    /// nó luôn chạy đúng số của server nó đang nối vào, kể cả khi hai server cấu hình khác nhau.
    ///
    /// Mọi property đều có mặc định hợp lệ: file config thiếu trường nào thì trường đó về mặc định
    /// thay vì làm sập server. Đây là chính sách "config hỏng một phần, game vẫn đứng dậy được".
    /// </summary>
    [MemoryPackable]
    public partial class MovementConfig
    {
        /// <summary>Tốc độ chạy ngang, world unit/giây.</summary>
        public float MoveSpeed { get; set; } = 5f;

        /// <summary>Gia tốc rơi, unit/giây². Lớn hơn 9.81 rất nhiều — trọng lực "đúng vật lý" cho
        /// cảm giác lơ lửng như trên mặt trăng, không game platformer nào dùng.</summary>
        public float Gravity { get; set; } = 30f;

        /// <summary>Vận tốc bật lên tức thời. Đỉnh nhảy = JumpSpeed² / (2·Gravity).</summary>
        public float JumpSpeed { get; set; } = 11f;

        /// <summary>Trần tốc độ rơi. Cũng là số quyết định trục nào cần quét chống tunneling.</summary>
        public float MaxFallSpeed { get; set; } = 20f;

        public int CoyoteTicks { get; set; } = 3;
        public int JumpBufferTicks { get; set; } = 3;
        public int DropThroughTicks { get; set; } = 6;

        public int AttackTicks { get; set; } = 5;
        public int AttackCooldownTicks { get; set; } = 8;
        public int HurtTicks { get; set; } = 4;
        public int DieTicks { get; set; } = 20;

        public float BodyHalfWidth { get; set; } = 0.35f;
        public float BodyHeight { get; set; } = 1.6f;
        public float BodyHeightCrouch { get; set; } = 0.9f;
    }
}
```

**`Server/Shared/World/GameConfig.cs`** (file mới):

```csharp
namespace MMORPG.Shared.World
{
    /// <summary>
    /// Hình dạng toàn bộ config game. Chỉ SERVER đọc file này; phần client cần nằm gọn trong
    /// <see cref="Movement"/> và được đẩy xuống qua EnterWorldResponse.
    ///
    /// Ranh giới giữa Movement và phần còn lại là ranh giới "client có cần biết không".
    /// </summary>
    public sealed class GameConfig
    {
        /// <summary>Phần client cũng nhận được — vì nó chạy cùng luật để dự đoán.</summary>
        public MovementConfig Movement { get; set; } = new();

        public int SpawnMapId { get; set; } = 1;
        public float SpawnX { get; set; } = 4f;
        public float SpawnY { get; set; } = 1f;

        /// <summary>Nghề gán cho nhân vật tạo lần đầu.</summary>
        public int DefaultClassId { get; set; } = 1;

        /// <summary>
        /// Bề ngang một cột tầm nhìn. Là số của THUẬT TOÁN SERVER, không phải luật chơi —
        /// client không tính tầm nhìn, nó chỉ nhận cái server gửi, và cũng không nên biết mình
        /// đang được cho xem trong bán kính bao nhiêu.
        /// </summary>
        public float AoiColumnWidth { get; set; } = 12f;

        /// <summary>Đường dẫn file bản đồ, tương đối so với thư mục chạy của server.</summary>
        public string MapFile { get; set; } = "Config/map1.txt";
    }
}
```

**`MovementRules.cs`** — xoá 14 `const`, giữ ba cái còn lại, và `Step` nhận config:

```csharp
        /// <summary>Nhịp mô phỏng. KHÔNG vào config: đây là hằng số của giao thức, cùng đẳng cấp
        /// với format khung gói tin — đổi nó là đổi nhịp prediction ở cả hai bên cùng lúc.</summary>
        public const int TICK_RATE = 20;
        public const float TICK_DT = 1f / TICK_RATE;

        /// <summary>Giá trị "hết hạn" cho các bộ đếm. Là giá trị canh của thuật toán, không phải
        /// số liệu game — chỉnh nó không mang ý nghĩa nào.</summary>
        public const int EXPIRED = 999;

        public static MoveState Step(MoveState state, MoveIntent intent, float dt,
                                     MapGrid map, MovementConfig cfg)
        {
            // ... thân hàm y như Phase 10, thay mọi HẰNG bằng cfg.TênTươngỨng ...
            // ví dụ:  state.VelY -= cfg.Gravity * dt;
            //         if (state.VelY < -cfg.MaxFallSpeed) state.VelY = -cfg.MaxFallSpeed;
        }
```

**`CharacterStates.DurationTicks`** — nhận thêm config:

```csharp
        /// <summary>
        /// Thời lượng của một hành động, tính bằng tick. Kiến thức chung của hai bên, nên người xem
        /// tự biết đòn đánh của người khác dài bao lâu mà không cần thêm byte nào trên dây.
        /// Nhận config chứ không đọc hằng: nếu không, hoạt ảnh sẽ co theo số CŨ trong khi luật chạy
        /// theo số MỚI — đúng loại lệch mà cả việc đưa số ra config đang chống.
        /// </summary>
        public static int DurationTicks(ActionState action, MovementConfig cfg)
        {
            switch (action)
            {
                case ActionState.Attack:
                    return cfg.AttackTicks;

                case ActionState.Hurt:
                    return cfg.HurtTicks;

                case ActionState.Die:
                    return cfg.DieTicks;

                default:
                    return 0;
            }
        }
```

**`EnterWorldResponse`** — thêm:

```csharp
        /// <summary>Bộ số server đang áp dụng cho phiên chơi này. Client dự đoán bằng đúng bộ này.</summary>
        public MovementConfig Movement { get; set; } = new();
```

</details>

---

## Bước 2 — Server: đọc file, kiểm hợp lệ, hot reload, và chốt theo phiên

### Hướng làm

**File config `Config/game.json`** đặt ở **gốc repo** (cạnh `Server/`, `Assets/` — nó là dữ liệu vận
hành, không phải source của riêng process nào):

```json
{
  "Movement": {
    "MoveSpeed": 5.0,
    "Gravity": 30.0,
    "JumpSpeed": 11.0,
    "MaxFallSpeed": 20.0,
    "CoyoteTicks": 3,
    "JumpBufferTicks": 3,
    "DropThroughTicks": 6,
    "AttackTicks": 5,
    "AttackCooldownTicks": 8,
    "HurtTicks": 4,
    "DieTicks": 20,
    "BodyHalfWidth": 0.35,
    "BodyHeight": 1.6,
    "BodyHeightCrouch": 0.9
  },
  "SpawnMapId": 1,
  "SpawnX": 4.0,
  "SpawnY": 1.0,
  "DefaultClassId": 1,
  "AoiColumnWidth": 12.0,
  "MapFile": "Config/map1.txt"
}
```

Cho GameServer thấy file: trong `GameServer.csproj` thêm `ItemGroup` copy `..\..\Config\*` vào output
(`CopyToOutputDirectory=PreserveNewest`, `Link=Config\%(Filename)%(Extension)`) — chạy từ Rider hay
`dotnet run` đều tìm thấy ở `Config/` cạnh exe.

**File mới `Server/GameServer/ConfigService.cs`** — ba việc:

**(a) `Load()`** — đọc + parse JSON (`System.Text.Json`). Lỗi gì (file thiếu, JSON hỏng) → log Warn và
dùng `new GameConfig()` mặc định. **Config hỏng không được giết server**, nhưng phải la thật lớn trong
log: chạy bằng mặc định trong khi người vận hành tưởng là số trong file mới là thảm hoạ âm thầm.

Bắt **đúng loại lỗi dự kiến** (`IOException`, `JsonException`) chứ không `catch (Exception)` — bug trong
code vẫn phải ném lên. Đây là ranh giới giữa "xử lý có chính sách" và "nuốt lỗi" mà `CLAUDE.md` cấm.

**(b) `Validate()`** — bốn phase trước chỉ có một con số nên chuyện này bỏ qua được; giờ có mười bốn và
**config đến từ con người**. Kiểm từng trường, ngoài khoảng hợp lý thì Warn + trả trường đó về mặc
định, không vứt cả file:

| Trường | Khoảng hợp lệ | Hỏng thì sao |
|---|---|---|
| `MoveSpeed`, `Gravity`, `JumpSpeed` | `> 0` | `0` = đứng liệt · số âm = đi lùi, rơi lên trời |
| `MaxFallSpeed` | `0 < v <= 1 / TICK_DT` | vượt là **rơi hơn một ô mỗi tick** → phép quét chống tunneling của Phase 10 hết bảo đảm |
| các `*Ticks` | `>= 0` | số âm làm vòng đếm ngược không bao giờ kết thúc |
| `BodyHalfWidth` | `0 < w < 0.5` | `>= 0.5` = không lọt nổi khe rộng 1 ô, nhân vật kẹt cứng |
| `BodyHeight` | `0 < h <= 2` | vượt 2 là **ba mức quét không còn đủ** (khoảng cách giữa hai mức vượt cạnh ô) |
| `BodyHeightCrouch` | `0 < h <= BodyHeight` | lớn hơn chiều cao đứng thì ngồi xuống lại cao lên |

Ba dòng cuối đáng để ý: chúng không phải "số phải dương" mà là **ràng buộc đến từ thuật toán ở phase
khác**. `MaxFallSpeed <= 20` không phải sở thích — nó là điều kiện để phép quét va chạm của Phase 10
còn đúng. Ghi lý do vào comment, không thì ba tháng nữa có người nâng nó lên 40 để "rơi cho đã" và
nhận về một bug xuyên sàn ngẫu nhiên.

> Config không phải là "chỗ để số". Nó là **bề mặt điều khiển** mà người vận hành chạm vào — và mọi
> giả định ngầm của thuật toán, nếu không được kiểm ở đây, sẽ bị phá từ đây.

**(c) `Current` + hot reload** — property trả `GameConfig` hiện hành. Reload là **thay nguyên object**
(`_current = mới`), không sửa từng field trên object cũ: ai đã cầm reference cũ vẫn thấy một bộ giá trị
**nhất quán**, và gán reference là thao tác nguyên tử nên không cần lock.

Sửa từng field trên object sống thì luồng tick có thể đọc được `Gravity` mới cộng `JumpSpeed` cũ — một
tổ hợp chưa từng tồn tại trong bất kỳ file nào.

**Vòng đọc phím đã có từ Phase 9** (`H`/`K`/`J` cho nút thử trạng thái). Thêm một nhánh `R` → `Load()`
+ log giá trị mới. Không phải dựng lại hạ tầng gì cả.

**Ai dùng config ở đâu:**

- `CharacterService.EnterWorldAsync`: dùng `SpawnMapId/X/Y`, `DefaultClassId` khi *tạo* nhân vật (xoá
  các `const` tương ứng trong `WorldService`), và gắn `config.Movement` vào response.
- `WorldService`: `AoiColumnWidth` thay `const AOI_COLUMN_WIDTH`.
- **`PlayerEntity` giữ một reference `MovementConfig`, chốt MỘT lần lúc spawn.** `Integrate` dùng nó,
  **không** đọc `ConfigService.Current` mỗi tick.

Điểm cuối là bài học chính của bước này, và nó mạnh hơn hẳn so với hồi chỉ có một con số:

> Bộ số là một phần của **hợp đồng phiên chơi**. Client dự đoán bằng đúng bộ nó nhận lúc vào world;
> server đổi số giữa chừng thì mọi dự đoán của người đang online lệch **ngay lập tức** → rubber-band
> hàng loạt, và họ không làm gì sai cả.
>
> Luật: **hot reload áp dụng cho người vào sau.** Người đang online giữ bộ cũ tới lần vào world kế tiếp.

Và vì đã gom 14 số thành một object, việc "chốt theo phiên" giờ là **giữ một reference** thay vì chép
14 field — thêm số mới không phải nhớ chép thêm. Đó là món lãi thứ hai của Bước 1.

### ✅ CHECKPOINT A

1. Server boot log: `Config: speed=5 gravity=30 jump=11 spawn=(4,1)@map1 aoi=12`.
2. Sửa `Gravity` thành `60` trong file → **không** build lại gì → restart server → nhân vật rơi nặng
   hẳn, nhảy thấp hẳn, và **không rubber-band** (client nhận 60 qua `EnterWorld`).
3. Sửa `AttackTicks` thành `30` → đòn đánh dài 1.5 giây **và clip tự chậm lại cho vừa** — đó là
   `DurationTicks` đã nhận config đúng. Nếu clip vẫn chạy nhanh rồi đứng hình thì bạn quên sửa nó.
4. Xoá tạm `game.json` khỏi output → server vẫn boot, log Warn, dùng mặc định.
5. Ghi `"BodyHalfWidth": 0.8` → boot lên thấy Warn về trường đó và nó về `0.35`; nhân vật vẫn lọt khe.
6. Đang chạy: sửa file thành `Gravity: 30`, gõ `R` → log reload. Người đang online **vẫn rơi nặng**
   (đúng thiết kế); relog → nhẹ lại.

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`Server/GameServer/GameServer.csproj`** — thêm:

```xml
  <ItemGroup>
    <None Include="..\..\Config\*" Link="Config\%(Filename)%(Extension)" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
```

**`Server/GameServer/ConfigService.cs`** (file mới):

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

        // Hot reload = THAY nguyên object, không sửa field trên object cũ. Gán reference là nguyên
        // tử: luồng khác hoặc thấy trọn bản cũ, hoặc trọn bản mới, không bao giờ thấy Gravity mới
        // ghép với JumpSpeed cũ — một tổ hợp chưa từng tồn tại trong bất kỳ file nào.
        private volatile GameConfig _current = new();

        public GameConfig Current => _current;

        public void Load()
        {
            GameConfig loaded;

            try
            {
                string json = File.ReadAllText(CONFIG_PATH);

                // Deserialize trả null khi file chứa đúng chữ "null" — hiếm nhưng rẻ để chặn.
                loaded = JsonSerializer.Deserialize<GameConfig>(json) ?? new GameConfig();
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                // Bắt ĐÚNG loại lỗi dự kiến, không catch (Exception): bug trong code vẫn phải ném lên.
                // Config hỏng không được giết server — nhưng phải la lớn, vì chạy bằng mặc định
                // trong khi vận hành tưởng là số trong file mới là thảm hoạ âm thầm.
                Log.Warn($"Không đọc được {CONFIG_PATH} ({ex.GetType().Name}: {ex.Message}) — dùng mặc định.");
                loaded = new GameConfig();
            }

            Validate(loaded);
            _current = loaded;

            MovementConfig m = loaded.Movement;
            Log.Info($"Config: speed={m.MoveSpeed.ToString().Green()} gravity={m.Gravity.ToString().Green()} " +
                     $"jump={m.JumpSpeed.ToString().Green()} " +
                     $"spawn=({loaded.SpawnX},{loaded.SpawnY})@map{loaded.SpawnMapId} aoi={loaded.AoiColumnWidth}");
        }

        /// <summary>
        /// Trả từng trường vô lý về mặc định thay vì vứt cả file. Config đến từ con người, và con
        /// người gõ nhầm; hỏng một trường không phải lý do để mất mười ba trường còn lại.
        ///
        /// Chú ý các ràng buộc KHÔNG hiển nhiên bên dưới: chúng không phải sở thích mà là điều kiện
        /// để thuật toán ở chỗ khác còn đúng. Không kiểm ở đây thì chúng bị phá từ đây.
        /// </summary>
        private static void Validate(GameConfig config)
        {
            var fallback = new MovementConfig();
            MovementConfig m = config.Movement;

            if (m.MoveSpeed <= 0f)
            {
                Log.Warn($"MoveSpeed={m.MoveSpeed} phải dương — về {fallback.MoveSpeed}.");
                m.MoveSpeed = fallback.MoveSpeed;
            }

            // ... Gravity, JumpSpeed và các *Ticks kiểm y hệt: ngoài khoảng → Warn + về mặc định ...

            // Rơi quá 1 ô mỗi tick thì phép quét chống tunneling của va chạm dọc hết bảo đảm:
            // nó quét theo từng hàng ô, và một cú rơi dài hơn cạnh ô có thể bỏ qua một tấm bệ.
            if (m.MaxFallSpeed <= 0f || m.MaxFallSpeed > MapGrid.CELL_SIZE / MovementRules.TICK_DT)
            {
                Log.Warn($"MaxFallSpeed={m.MaxFallSpeed} ngoài khoảng an toàn — về {fallback.MaxFallSpeed}.");
                m.MaxFallSpeed = fallback.MaxFallSpeed;
            }

            // Rộng từ nửa ô trở lên là không lọt nổi khe rộng đúng 1 ô — nhân vật kẹt cứng ở mọi lối hẹp.
            if (m.BodyHalfWidth <= 0f || m.BodyHalfWidth >= MapGrid.CELL_SIZE * 0.5f)
            {
                Log.Warn($"BodyHalfWidth={m.BodyHalfWidth} ngoài khoảng an toàn — về {fallback.BodyHalfWidth}.");
                m.BodyHalfWidth = fallback.BodyHalfWidth;
            }

            // Cao quá 2 ô thì ba mức quét ngang (chân/giữa/đầu) cách nhau hơn một cạnh ô,
            // và có ô lọt giữa hai lần kiểm — nhân vật đi xuyên tường mỏng.
            if (m.BodyHeight <= 0f || m.BodyHeight > MapGrid.CELL_SIZE * 2f)
            {
                Log.Warn($"BodyHeight={m.BodyHeight} ngoài khoảng an toàn — về {fallback.BodyHeight}.");
                m.BodyHeight = fallback.BodyHeight;
            }

            if (m.BodyHeightCrouch <= 0f || m.BodyHeightCrouch > m.BodyHeight)
            {
                Log.Warn($"BodyHeightCrouch={m.BodyHeightCrouch} không hợp lệ — về {fallback.BodyHeightCrouch}.");
                m.BodyHeightCrouch = fallback.BodyHeightCrouch;
            }

            if (config.AoiColumnWidth <= 0f)
            {
                Log.Warn($"AoiColumnWidth={config.AoiColumnWidth} không hợp lệ — về 12.");
                config.AoiColumnWidth = 12f;
            }
        }
    }
}
```

> Mười bốn khối `if` gần giống nhau nhìn thừa thãi, và bạn sẽ muốn gom lại thành một hàm chung. Gom
> được — nhưng chú ý: `MovementConfig` dùng **property**, mà property thì không truyền được bằng `ref`.
> Muốn gom thì hoặc đổi sang field, hoặc truyền cặp getter/setter, hoặc dùng reflection. Cả ba đều đắt
> hơn mười bốn khối `if` ở quy mô này — đây là lúc lặp lại rẻ hơn trừu tượng hoá.
>
> Điều **bắt buộc** thì chỉ có một: mỗi phép kiểm phải log **tên trường**. Không có tên thì người vận
> hành thấy game chạy sai mà không biết trường nào vừa bị từ chối, và họ sẽ đi sửa nhầm chỗ.

**`Program.cs`** — thêm `R` vào vòng đọc phím đã dựng từ Phase 9:

```csharp
            case ConsoleKey.R:
                configService.Load();
                break;
```

**`PlayerEntity`** — giữ reference bộ số của phiên:

```csharp
        /// <summary>
        /// Bộ số áp dụng cho entity này, chốt MỘT lần lúc spawn — không đọc config mỗi tick.
        ///
        /// Client dự đoán bằng đúng bộ nó nhận lúc vào world; server đổi số giữa phiên thì dự đoán
        /// của người đang online lệch ngay lập tức và họ bị rubber-band oan. Hot reload chỉ áp dụng
        /// cho người vào sau.
        ///
        /// Giữ nguyên REFERENCE chứ không chép từng field: thêm một số mới vào config thì chỗ này
        /// không phải sửa, và cũng không thể quên sửa.
        /// </summary>
        public MovementConfig Movement { get; }
```

(gán trong constructor; `Integrate` truyền `Movement` vào `Step`.)

**`CharacterService`** nhận `ConfigService` qua constructor, trong `EnterWorldAsync`:

```csharp
            GameConfig config = _configService.Current;
```

dùng `config.DefaultClassId / SpawnMapId / SpawnX / SpawnY` cho `CharacterGetOrCreateRequest`, truyền
`config.Movement` vào `_worldService.Spawn(...)`, và response thêm `Movement = entity.Movement`.

</details>

---

## Bước 3 — Client: dùng số server đưa

### Hướng làm

Ba chỗ, đều nhỏ — và cả ba đều là "cầm lấy object rồi truyền đi", không phải "chép 14 số":

- `LocalPlayer`: thêm `MovementConfig Movement { get; private set; }`, gán trong `Apply`. Cache
  server-confirmed, đúng luật cũ: không có setter công khai.
- `WorldSpawner.SpawnLocalPlayer`: truyền `response.Movement` vào `motor.Init(...)`.
- `PlayerMotor`: nhận `MovementConfig` trong `Init`, dùng nó ở **cả ba** chỗ: bước dự đoán, vòng replay
  trong `OnMoveStateResult`, và tốc độ `MoveTowards` khi hiển thị (`cfg.MaxFallSpeed * 1.5f`). Sót chỗ
  nào là rubber-band ở đúng chỗ đó — nhưng lần này compile sẽ chỉ tận nơi vì `Step` đổi chữ ký.
- `CharacterAnimator`: `DurationTicks` giờ cần config → animator phải biết nó. Truyền vào `Apply`, hoặc
  gán một lần lúc khởi tạo. Đây là chỗ **duy nhất** không được compiler nhắc nếu bạn lỡ giữ một bản
  hằng số riêng — kiểm bằng CHECKPOINT A bước (3).

Client **không đọc file nào**, không copy `game.json` vào build — đó là toàn bộ ý của phase.

Một chi tiết dễ bỏ qua: `RemotePlayerView` cũng gọi `CharacterStates.Derive` và animator, nhưng nó
**không** cần `MovementConfig` — vì `Derive` chỉ so sánh dấu, không dùng hằng nào. Đáng chú ý: một hàm
thuần không có tham số cấu hình là một hàm không bao giờ lệch phiên bản. Càng ít số, càng ít chỗ sai.

### ✅ CHECKPOINT B

1. `Gravity = 60` trong json, restart server, client **không build lại** → rơi nặng, mượt, không
   rubber-band.
2. Hai client cùng online, server hot reload sang `30` → cả hai vẫn rơi nặng (số chốt theo phiên); một
   người relog → người đó rơi nhẹ, người kia vẫn nặng — **hai người chơi với hai bộ số khác nhau trong
   cùng một thế giới, và không ai rubber-band**, vì ai cũng dự đoán bằng đúng bộ server dùng cho mình.
3. Đổi `SpawnX/SpawnY` → tài khoản **mới** spawn ở chỗ mới; tài khoản cũ vào lại vẫn ở vị trí đã lưu
   của họ. Hiểu vì sao: spawn config chỉ dùng lúc *tạo* nhân vật.

Bước (2) đáng dừng lại: nó nghe như một bug ("hai người chơi hai luật khác nhau!") nhưng thực ra là
kiến trúc đang làm đúng việc của nó. Trong một game thật, đó chính là cách bạn đổi cân bằng mà không
đá ai ra khỏi server.

---

## Bước 4 — Loại B: bản đồ ra file, và hai phép kiểm phiên bản

### Hướng làm

Bản đồ là **config loại B đầu tiên** của dự án: cả hai bên đều đọc nó (server va chạm thật, client va
chạm dự đoán và vẽ gizmo), nó có cấu trúc, và nó sẽ còn to ra. Nó không chữa được bằng cách của loại A
("chỉ server đọc") vì client thật sự cần **toàn bộ** dữ liệu chứ không phải vài con số.

**4a — Bản đồ ra file, server gửi cho client.**

- Nguồn duy nhất: `Config/map1.txt` ở gốc repo. `Maps.cs` với mảng chuỗi hằng bị xoá.
- Server đọc lúc boot (đường dẫn lấy từ `GameConfig.MapFile`), `MapGrid.Parse`, và tính **hash** nội
  dung. Hàm hash đặt ở `Shared` để hai bên tính ra cùng một số.
- `EnterWorldResponse` mang thêm `MapRows` (nội dung map) và `MapVersion` (hash).
- Client dựng `MapGrid` từ đúng dữ liệu nhận được, tự tính lại hash và so — lệch thì **không vào world**.

Câu hỏi đúng phải hỏi: *server đã gửi map rồi thì hash để làm gì, làm sao lệch được?* Hôm nay thì
không lệch được thật — và đó chính là lý do cách này đúng **khi dữ liệu còn nhỏ**. Map 48×16 chỉ 768
byte, gửi lại mỗi lần đăng nhập rẻ hơn mọi cơ chế cache.

Nhưng bảng item, bảng quái, bảng chỉ số theo class thì không nhỏ như vậy, và ở Phase 17 chúng đi bằng
đường khác: **client giữ bản cache, server chỉ gửi hash, client tải lại khi hash khác.** Trường
`MapVersion` hôm nay là chỗ nối cho ngày ấy — và nó bắt đầu có ích ngay bây giờ ở dạng một dòng log:
hai server chạy hai bản map khác nhau thì nhìn hash là biết, không phải đoán.

> Loại B đi theo một trong hai chế độ: **gửi cả dữ liệu** (nhỏ, đơn giản, không lệch được) hoặc **gửi
> hash rồi client tự tải** (to, phức tạp, lệch được nên phải kiểm). Chọn chế độ theo kích thước, nhưng
> **trường version thì có từ ngày đầu** — thêm nó sau khi đã có người chơi là một cuộc di cư.

Chi tiết dễ chịu: khi map lớn hơn 4KB thì đường nén LZ4 dựng ở Phase 2 **tự động** hoạt động, không
phải sửa gì. Đây là lần đầu trong dự án có payload đủ to để nó chạy thật.

**Gizmo trong Editor thì lấy map ở đâu?** Lúc chưa chạy game thì chưa có server, mà quy trình vẽ map
của Phase 10 lại cần nhìn gizmo trong Scene view. Lời giải: gizmo đọc **thẳng file gốc** bằng
`#if UNITY_EDITOR`, đường dẫn `Application.dataPath + "/../Config/map1.txt"`.

Đây **không** phải là phá luật "client không đọc file config". Ba điều kiện làm nó hợp lệ, và thiếu
một cái là thành vi phạm:

1. Chỉ chạy trong **Editor**, không có trong bản build;
2. Đọc **đúng file gốc**, không phải một bản copy nằm trong `Assets/`;
3. Kết quả chỉ dùng để **vẽ gizmo**, không tham gia mô phỏng dòng nào.

Nói cách khác: đây là **công cụ của người làm game**, không phải một phần của game.

**4b — Kiểm phiên bản contract lúc kết nối.**

Mở lại bảng Troubleshooting của Phase 8, 9, 10. Cả ba đều có cùng một dòng:

> *"Nhân vật đứng im hoàn toàn, không lỗi gì — Unity còn dùng DLL cũ, build `Shared` chưa copy sang
> `Assets/Plugins/Shared/`."*

Ba phase, cùng một bug, và mỗi lần đều mất thời gian vì **nó không có triệu chứng riêng**. Đã tới lúc
giết hẳn nó thay vì ghi chú nó thêm lần nữa.

Cách làm: `Shared` tự tính một **hash của chính contract** bằng reflection — duyệt mọi giá trị `NetCmd`
và mọi kiểu `[MemoryPackable]` cùng danh sách thành viên của chúng, băm lại thành một `int`. Client gửi
số đó ngay sau khi nối; server so với số của mình; lệch thì trả lỗi rõ ràng rồi ngắt.

Hai điểm phải hiểu đúng:

- **Vì sao dùng reflection chứ không một `const VERSION` gõ tay.** Số gõ tay phải nhớ tăng, mà cái cần
  chống ở đây chính là *quên*. Một hằng số mà người ta quên tăng còn tệ hơn không có: nó tạo cảm giác
  đã được bảo vệ.
- **Nó bắt được gì.** Thêm/xoá/đổi số một `NetCmd`, thêm/xoá/đổi tên một field DTO — tức gần như mọi
  thay đổi contract. Nó **không** bắt được thay đổi *hành vi* mà không đổi *hình dạng*: sửa `Step` hay
  đổi thứ tự các phép trong đó thì hash vẫn y nguyên. Biết giới hạn của một phép kiểm cũng quan trọng
  ngang việc có nó.

Và `HandshakeDto` từ Phase 0 — cái DTO thử với comment "không còn luồng nào dùng, xoá được" — cuối
cùng cũng có việc làm. Xoá nó, thay bằng cặp `VersionCheckRequest`/`VersionCheckResponse`.

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`Server/Shared/World/Maps.cs`** — thay mảng chuỗi hằng bằng hàm dựng từ nội dung file:

```csharp
using System;

namespace MMORPG.Shared.World
{
    /// <summary>
    /// Nạp bản đồ từ nội dung dạng chữ và tính hash phiên bản của nó.
    /// Bản đồ không còn là hằng số trong code: nó là DỮ LIỆU, có một bản gốc duy nhất ở file,
    /// và client nhận đúng bản mà server đang chạy.
    /// </summary>
    public static class Maps
    {
        /// <summary>Tách nội dung file thành các hàng, bỏ dòng trống và ký tự xuống dòng của Windows.</summary>
        public static string[] SplitRows(string content)
        {
            return content.Replace("\r", string.Empty)
                          .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// Hash phiên bản của một bảng dữ liệu. FNV-1a 32-bit: không phải hàm băm mật mã, chỉ cần
        /// hai tính chất — cùng nội dung cho cùng số, và đổi một ký tự thì số đổi. Đặt ở Shared để
        /// hai bên tính ra cùng kết quả; mỗi bên tự viết một hàm băm là chép tay contract.
        /// </summary>
        public static int Version(string[] rows)
        {
            unchecked
            {
                const int OFFSET = (int)2166136261;
                const int PRIME = 16777619;

                int hash = OFFSET;

                foreach (string row in rows)
                {
                    foreach (char c in row)
                    {
                        hash = (hash ^ c) * PRIME;
                    }

                    // Băm cả ranh giới hàng: nếu không, hai map cắt hàng khác nhau mà nối lại
                    // ra cùng một chuỗi sẽ cho cùng hash.
                    hash = (hash ^ '\n') * PRIME;
                }

                return hash;
            }
        }
    }
}
```

**`Server/Shared/Net/Contract.cs`** (file mới):

```csharp
using System;
using System.Linq;
using System.Reflection;
using MemoryPack;

namespace MMORPG.Shared.Net
{
    /// <summary>
    /// Vân tay của contract mạng: một số tính từ chính hình dạng của NetCmd và các DTO.
    ///
    /// Có nó thì "client chạy DLL cũ" — bug không triệu chứng đã xuất hiện ở ba phase liên tiếp —
    /// biến thành một dòng lỗi rõ ràng ngay lúc kết nối.
    ///
    /// Tính bằng reflection chứ không phải một hằng số gõ tay, vì thứ cần chống ở đây chính là
    /// việc QUÊN tăng hằng số ấy. Một hằng số mà người ta quên tăng còn tệ hơn không có: nó tạo
    /// cảm giác đã được bảo vệ.
    ///
    /// Bắt được: thêm/xoá/đổi số một NetCmd, thêm/xoá/đổi tên một thành viên DTO.
    /// KHÔNG bắt được: đổi HÀNH VI mà không đổi hình dạng — sửa công thức trong MovementRules thì
    /// vân tay này y nguyên. Biết giới hạn của một phép kiểm quan trọng ngang việc có nó.
    /// </summary>
    public static class Contract
    {
        private static readonly Lazy<int> _hash = new(Compute);

        public static int Hash => _hash.Value;

        private static int Compute()
        {
            var parts = new System.Collections.Generic.List<string>();

            // Sắp xếp theo tên chứ không theo thứ tự reflection trả về: thứ tự ấy không được bảo
            // đảm giữa các runtime, mà client (Mono/IL2CPP) và server (CoreCLR) là hai runtime khác nhau.
            foreach (string name in Enum.GetNames(typeof(NetCmd)).OrderBy(n => n, StringComparer.Ordinal))
            {
                parts.Add($"{name}={(int)Enum.Parse<NetCmd>(name)}");
            }

            Assembly shared = typeof(Contract).Assembly;

            foreach (Type type in shared.GetTypes()
                         .Where(t => t.GetCustomAttribute<MemoryPackableAttribute>() != null)
                         .OrderBy(t => t.FullName, StringComparer.Ordinal))
            {
                parts.Add(type.FullName);

                foreach (PropertyInfo p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                             .OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    parts.Add($"{p.Name}:{p.PropertyType.Name}");
                }

                foreach (FieldInfo f in type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                             .OrderBy(f => f.Name, StringComparer.Ordinal))
                {
                    parts.Add($"{f.Name}:{f.FieldType.Name}");
                }
            }

            return World.Maps.Version(parts.ToArray());
        }
    }
}
```

**`Server/Shared/Net/NetCmd.cs`** — thêm vào dải hệ thống:

```csharp
        /// <summary>
        /// Đối chiếu vân tay contract ngay sau khi nối. Client chủ động gửi trước mọi lệnh khác.
        /// Request/Response: <see cref="Dto.VersionCheckRequest"/> / <see cref="Dto.VersionCheckResponse"/>
        /// </summary>
        VersionCheck = 6,
```

**`Server/Shared/Dto/SystemDto.cs`** — thêm (và xoá `HandshakeDto.cs`):

```csharp
    /// <summary>Vân tay contract của client, gửi ngay sau khi nối.</summary>
    [MemoryPackable]
    public partial class VersionCheckRequest
    {
        public int ContractHash { get; set; }
    }

    [MemoryPackable]
    public partial class VersionCheckResponse
    {
        public bool Ok { get; set; }
        public int ServerContractHash { get; set; }
        public string ServerName { get; set; } = string.Empty;
    }
```

**`Server/GameServer/Handlers/SystemHandler.cs`** — thêm handler:

```csharp
[TcpHandler(NetCmd.VersionCheck)]
public static Task<NetResult> OnVersionCheck(NetRequest req)
{
    var request = req.GetData<VersionCheckRequest>();
    bool ok = request.ContractHash == Contract.Hash;

    if (!ok)
    {
        // La lớn ở phía server luôn: người vận hành cần biết có client lệch bản đang gõ cửa,
        // và con số cụ thể là thứ đầu tiên họ sẽ hỏi.
        Log.Warn($"{req.Session.Tag} contract lệch: client={request.ContractHash} server={Contract.Hash}");
    }

    return Task.FromResult(NetResult.Data(NetCmd.VersionCheck, new VersionCheckResponse
    {
        Ok = ok,
        ServerContractHash = Contract.Hash,
        ServerName = "MMORPG Dev",
    }));
}
```

(và nếu `SessionState` của bạn có chỗ, thêm một trạng thái `Verified` đứng trước `Authenticated` rồi
đặt `MinState = SessionState.Verified` cho các handler còn lại — gọn hơn là chỉ dựa vào việc ngắt kết
nối.)

**`Server/GameServer/Program.cs`** — đọc map lúc boot:

```csharp
GameConfig config = configService.Current;

string[] mapRows = Maps.SplitRows(File.ReadAllText(config.MapFile));
MapGrid map = MapGrid.Parse(mapRows);
int mapVersion = Maps.Version(mapRows);

Log.Info($"Map {config.MapFile}: {map.Width}x{map.Height} version={mapVersion.ToString("X8").Green()}");
Log.Info($"Contract hash={Contract.Hash.ToString("X8").Green()}");
```

`EnterWorldResponse` thêm `string[] MapRows` và `int MapVersion`; `CharacterService` điền chúng.

**`Assets/Game/Scripts/World/MapCollisionGizmo.cs`** — hai nguồn map, tách bạch:

```csharp
        private MapGrid _map;

        /// <summary>Gọi từ WorldSpawner sau khi vào world — đây là bản map THẬT, đến từ server.</summary>
        public void SetMap(MapGrid map)
        {
            _map = map;
        }

        private MapGrid ResolveMap()
        {
            if (_map != null)
                return _map;

#if UNITY_EDITOR
            // Lúc chưa chạy game thì chưa có server, mà quy trình vẽ map lại cần nhìn gizmo trong
            // Scene view. Đọc thẳng FILE GỐC — không phải bản copy trong Assets/.
            //
            // Đây không phải ngoại lệ của luật "client không đọc file config", vì đủ ba điều kiện:
            // chỉ chạy trong Editor, đọc đúng file gốc, và kết quả chỉ để vẽ gizmo — không dòng nào
            // tham gia mô phỏng. Nói cách khác: đây là công cụ của người làm game, không phải game.
            string path = Application.dataPath + "/../Config/map1.txt";

            if (System.IO.File.Exists(path))
                return MapGrid.Parse(Maps.SplitRows(System.IO.File.ReadAllText(path)));
#endif

            return null;
        }
```

**`Assets/Game/Scripts/World/WorldSpawner.cs`** — dựng map từ gói server và kiểm hash:

```csharp
        public void SpawnLocalPlayer(EnterWorldResponse response)
        {
            // Tự tính lại hash trên đúng dữ liệu vừa nhận. Hôm nay server gửi cả map nên phép so
            // này không thể sai — nhưng ngày client giữ bản cache và server chỉ gửi hash thì đây
            // là chỗ chặn "chơi bằng map cũ", và lúc đó thêm nó vào là quá muộn.
            int localVersion = Maps.Version(response.MapRows);

            if (localVersion != response.MapVersion)
            {
                this.LogError($"Map lệch phiên bản: server={response.MapVersion:X8} local={localVersion:X8}");
                return;
            }

            MapGrid map = MapGrid.Parse(response.MapRows);
            _mapCollisionGizmo.SetMap(map);

            // ... Instantiate như cũ ...
            motor.Init(_worldApi, _worldNetHandler, new Vector2(response.X, response.Y), map, response.Movement);
        }
```

</details>

### ✅ CHECKPOINT C — mục tiêu cuối Phase 11

1. Server boot log in ra kích thước map, `version=XXXXXXXX`, và `Contract hash=XXXXXXXX`.
2. Sửa `Config/map1.txt` (thêm một bệ `=`), restart server, **không build lại gì** → vào game thấy bệ
   mới, đứng lên được, và gizmo vẽ đúng chỗ đó.
3. Xoá một ký tự trong một hàng map → server **không boot**, `Parse` ném lỗi nói rõ hàng nào. Hàng rào
   dựng từ Phase 10 vẫn đứng.
4. Sửa `Server/Shared/Net/NetCmd.cs` (thêm một giá trị bất kỳ), build `Shared`, **cố tình không để DLL
   sang Unity** (khôi phục file DLL cũ), chạy client → client báo **"phiên bản không khớp"** và không
   vào được, thay vì đứng im không hiểu vì sao.
5. Copy DLL đúng → vào bình thường.

Bước (4) là phần thưởng lớn nhất của phase: một loại bug đã xuất hiện ở ba phase liên tiếp, mỗi lần đều
phải đoán, giờ tự báo tên nó ra.

---

## Ba thử nghiệm bắt buộc

**1. Config rác.**
Ghi `"Gravity": "nặng lắm"` vào json → restart: server sống, log Warn, dùng mặc định. Ghi JSON hỏng hẳn
(thiếu một dấu `}`): như trên. Rồi ghi `"MaxFallSpeed": 60` → boot lên thấy Warn về đúng trường đó.

Server không bao giờ được chết vì file người vận hành gõ tay — nhưng cũng không bao giờ được **im lặng**
chạy bằng số khác với số họ tưởng.

**2. Client cứng đầu.**
Sửa tạm client bỏ qua `response.Movement`, dự đoán bằng một `new MovementConfig()` tự chế với
`Gravity = 10` → rubber-band liên tục theo chiều dọc, server thắng. Kết luận của Phase 6 vẫn nguyên giá
trị khi số đã thành dữ liệu. Trả lại code.

**3. Đo cái gì thật sự đổi khi hot reload.**
Hai client online. Sửa `Gravity` trong file rồi gõ `R`. Ghi lại: (a) log server in số mới; (b) hai người
đang chơi **không đổi gì**; (c) một người relog thì chỉ người đó đổi. Rồi thử ngược lại — sửa
`PlayerEntity` cho `Integrate` đọc `ConfigService.Current` mỗi tick, gõ `R` và xem cả hai người bị giật
cùng lúc như thế nào. Đây là cách nhanh nhất để hiểu vì sao "tươi hơn" không phải lúc nào cũng đúng
hơn. Trả lại code.

---

## Troubleshooting

| Triệu chứng | Nguyên nhân | Xử lý |
|---|---|---|
| Server báo không đọc được config dù file có | Chạy từ thư mục khác (working dir ≠ cạnh exe), hoặc csproj chưa copy | Kiểm `bin/Debug/net8.0/Config/game.json` có tồn tại |
| Sửa json mà số không đổi | Đang sửa file ở gốc repo nhưng bản copy trong `bin/` chỉ cập nhật lúc build — `R` đọc bản trong `bin/` | Build lại, hoặc sửa thẳng bản trong `bin/` khi thử nhanh |
| Rubber-band sau khi đổi số | Client còn chỗ dùng hằng cũ — hay gặp nhất là `MoveTowards` khi hiển thị và **vòng replay** | Tìm mọi lời gọi `Step` và mọi chỗ đọc `cfg` |
| Người online bị giật ngay khi bấm `R` | `Integrate` đọc `Current` mỗi tick thay vì dùng `entity.Movement` chốt lúc spawn | Đọc lại comment trên `PlayerEntity.Movement` |
| Hoạt ảnh đòn đánh dài/ngắn không khớp luật | `DurationTicks` còn đọc hằng số cũ thay vì nhận config | `CharacterStates.DurationTicks` |
| `JsonException` tên trường không khớp | Tên trong json khác tên property | Giữ trùng PascalCase, hoặc bật `PropertyNameCaseInsensitive` |
| Trường trong `Movement` không được nạp | Quên lồng chúng trong object `"Movement": { ... }` | Xem lại hình dạng json ở Bước 2 |
| Nhân vật mới spawn trong tường sau khi đổi `SpawnX/Y` | Config trỏ vào ô đặc — người gõ config không nhìn map | `ResolveSpawnY` đã đẩy lên và đã Warn; đọc dòng log đó |
| Client báo map lệch phiên bản | `Maps.Version` hai bên khác nhau → DLL lệch | Chính là lúc `Contract.Hash` phải báo trước — kiểm xem đã nối phép kiểm đó vào chưa |
| Client báo contract không khớp mà DLL vừa copy | Unity chưa nạp lại DLL | Đợi Unity biên dịch xong, hoặc Reimport thư mục `Assets/Plugins/Shared/` |
| `Contract.Hash` hai bên khác nhau dù cùng DLL | Đang sắp xếp theo thứ tự reflection trả về thay vì `OrderBy` theo tên | `Contract.Compute` |
| Gizmo không vẽ gì trong Scene view | Chưa chạy game và không tìm thấy `Config/map1.txt` theo đường dẫn tương đối | Kiểm `Application.dataPath + "/../Config/map1.txt"` |

---

## Tự kiểm tra hiểu bài

Tự trả lời từng câu xong mới mở đáp án của câu đó.

**Câu 1.** Vì sao "client và server cùng đọc chung một file config" nghe giống contract một nguồn nhưng
thực ra là bẫy? Nêu hai kịch bản cụ thể nó hỏng.
<details>
<summary><b>📖 Đáp án câu 1</b></summary>

Vì thứ cần đồng bộ là **giá trị đang chạy**, không phải nội dung file. (1) Client build ra mang bản
copy tại thời điểm build — server sửa file xong, mọi client ngoài kia vẫn chạy số cũ, lệch mà không ai
báo; (2) file nằm trong máy người chơi thì người chơi sửa được — với giá trị tham gia dự đoán là họ tự
gây rubber-band rồi đi report "game lag".

Server phát giá trị qua mạng thì cả hai kịch bản biến mất **về mặt cấu trúc**, không cần ai kỷ luật.

</details>

**Câu 2.** Vì sao `MovementConfig` chốt vào `PlayerEntity` lúc spawn thay vì `Integrate` đọc
`ConfigService.Current` mỗi tick — "tươi" hơn cơ mà?
<details>
<summary><b>📖 Đáp án câu 2</b></summary>

Vì bên kia đầu dây có một client đang **dự đoán bằng bộ số nó nhận lúc vào world**. Server đổi số giữa
phiên (hot reload) thì mọi dự đoán của người đang online lệch ngay lập tức → rubber-band hàng loạt, và
họ không làm gì sai cả.

Bộ số là một phần của **hợp đồng phiên chơi**: chốt lúc vào, muốn đổi thì phải thông báo (đẩy gói số
mới) — chưa làm cơ chế thông báo thì chưa được đổi ngầm. Luật: hot reload áp dụng cho người vào sau.

Bên lề: vì đã gom 14 số thành một object, "chốt theo phiên" là giữ **một reference** — thêm số mới
không phải nhớ chép thêm, và cũng không thể quên.

</details>

**Câu 3.** Config hỏng → server dùng mặc định và chạy tiếp, trong khi `CLAUDE.md` cấm nuốt lỗi. Biện
minh — và điểm nào trong cách xử lý là **bắt buộc** để nó không thành nuốt lỗi?
<details>
<summary><b>📖 Đáp án câu 3</b></summary>

Lựa chọn thật là: chết ngay lúc boot vì một dấu phẩy, hay đứng dậy bằng bộ giá trị an toàn đã biết. Với
dữ liệu do người vận hành gõ tay, phương án hai đúng hơn — *miễn là* (1) chỉ bắt **đúng loại lỗi dự
kiến** (`IOException`, `JsonException`; bug trong code vẫn phải ném lên), và (2) **log Warn to rõ, kèm
tên trường bị từ chối**.

Nuốt lỗi bị cấm là nuốt *không dấu vết, không chủ đích*. Đây là xử lý **có chính sách**: hỏng cái gì,
thay bằng cái gì, và ai được biết.

</details>

**Câu 4.** Hot reload thay nguyên object `GameConfig` thay vì sửa từng field trên object đang dùng.
Cơ chế nào làm cách này an toàn đa luồng mà không cần lock?
<details>
<summary><b>📖 Đáp án câu 4</b></summary>

Gán một reference là thao tác **nguyên tử** — luồng khác hoặc thấy trọn object cũ, hoặc trọn object
mới, không bao giờ thấy nửa nọ nửa kia; object cũ thì không ai sửa nữa từ lúc phát hành, nên ai đang
cầm cứ dùng tiếp một bộ giá trị nhất quán.

Sửa từng field trên object sống thì luồng tick có thể đọc được `Gravity` mới ghép với `JumpSpeed` cũ —
một tổ hợp **chưa từng tồn tại trong bất kỳ file nào**, tức là một trạng thái không tái hiện được và
không debug được.

</details>

**Câu 5.** `Gravity` vào config còn `TICK_RATE` thì không. Ranh giới ở đâu, và điều gì gãy nếu người
vận hành đổi `TICK_RATE` từ 20 thành 30 trong file?
<details>
<summary><b>📖 Đáp án câu 5</b></summary>

Ranh giới: `Gravity` là **số liệu game** (đổi nó là đổi trải nghiệm), `TICK_RATE` là **hằng số của giao
thức** (đổi nó là đổi cách hai bên nói chuyện) — cùng đẳng cấp với format khung gói tin.

Đổi nó thì `TICK_DT` ăn theo, và toàn bộ prediction/reconciliation xây trên giả định hai bên **cùng
nhịp**: client bơm input 20 bước/giây trong khi server tiêu 30 tick/giây, replay của client tính mỗi
input một `TICK_DT` khác server → dự đoán lệch có hệ thống, rubber-band toàn dân.

Thứ như vậy phải đổi bằng một build có chủ đích ở cả hai phía, không phải bằng một file text lúc nửa
đêm.

</details>

**Câu 6.** Bản đồ thuộc loại config nào, và vì sao cách chống lệch của nó khác hẳn `Gravity`?
<details>
<summary><b>📖 Đáp án câu 6</b></summary>

Loại B. Khác ở chỗ **client thật sự cần toàn bộ dữ liệu**, không phải vài con số: nó va chạm dự đoán
trên cả map và vẽ gizmo trên cả map. Cách của loại A ("chỉ server đọc, đẩy vài giá trị xuống") không áp
dụng được — "vài giá trị" ở đây là cả bảng.

Nên loại B chống lệch bằng **phiên bản**: server gửi hash của bảng, client so. Hôm nay server gửi luôn
cả nội dung (map nhỏ, gửi lại mỗi lần đăng nhập rẻ hơn mọi cơ chế cache) nên hash chỉ là chỗ nối; khi
bảng to lên thì đổi sang "client cache, server chỉ gửi hash, lệch thì tải lại" mà không phải đổi
contract.

Điểm đáng nhớ: **trường version phải có từ ngày đầu.** Thêm nó sau khi đã có người chơi là một cuộc di cư.

**Câu 7.** `MaxFallSpeed` bị `Validate` chặn ở `CELL_SIZE / TICK_DT`. Đây là sở thích hay ràng buộc?
Nếu vượt thì cái gì gãy, ở đâu?
<details>
<summary><b>📖 Đáp án câu 7</b></summary>

Ràng buộc, và nó đến từ một phase khác. Va chạm dọc ở Phase 10 chống tunneling bằng cách **quét từng
hàng ô** mà chân đi qua trong một tick. Phép quét ấy đúng vì quãng rơi mỗi tick không vượt cạnh một ô —
`20 unit/s × 0.05s = 1.00`, sát kịch trần. Nâng `MaxFallSpeed` lên 40 thì mỗi tick rơi 2 ô, và một tấm
bệ dày 1 ô có thể nằm trọn giữa hai lần kiểm: **rơi xuyên sàn, ngẫu nhiên, chỉ khi rơi từ đủ cao**.

Bài học rộng hơn: **config là bề mặt điều khiển mà người vận hành chạm vào, nên mọi giả định ngầm của
thuật toán phải được kiểm ở đó.** Trước khi có config, `MAX_FALL_SPEED = 20` là một hằng số nằm cạnh
đoạn code dựa vào nó. Sau khi ra file, nó thành một ô trống mời người ta điền — và giả định ngầm kia
không đi theo. Viết lý do vào `Validate` là cách duy nhất để nó đi theo.

</details>

**Câu 8.** Đổi `SpawnX/SpawnY` trong config, người chơi cũ vào lại vẫn đứng chỗ cũ của họ. Bug hay
tính năng? Nó tiết lộ gì về hai loại dữ liệu trong bảng `character`?
<details>
<summary><b>📖 Đáp án câu 8</b></summary>

Tính năng. Spawn config là giá trị **khởi tạo** — chỉ dùng đúng một lần lúc *tạo* nhân vật; từ đó vị
trí là **trạng thái của người chơi**, thuộc về họ, lưu trong DB, và config không có quyền đè.

Ranh giới này — *giá trị khởi tạo* vs *trạng thái tích luỹ* — chính là ranh giới giữa "dữ liệu game
design" và "dữ liệu người chơi". Nhầm bên là hoặc reset đồ người ta (đè trạng thái bằng config), hoặc
không tài nào cân bằng lại game (biến thứ đáng lẽ là config thành trạng thái đã lưu).

Đối chiếu với `ResolveSpawnY` ở Phase 10 để thấy ranh giới ấy không tuyệt đối: vị trí là của người
chơi, nhưng nếu nó rơi vào chỗ **không hợp lệ** thì server vẫn phải sửa. "Không đè" nghĩa là không đè
vì lý do cân bằng, chứ không phải không bao giờ chạm vào.

</details>

**Câu 9.** `Contract.Hash` bắt được loại lệch nào giữa client và server, và **không** bắt được loại
nào? Vì sao việc biết giới hạn ấy lại quan trọng?
<details>
<summary><b>📖 Đáp án câu 9</b></summary>

**Bắt được:** đổi hình dạng contract — thêm/xoá/đổi số một `NetCmd`, thêm/xoá/đổi tên/đổi kiểu một
thành viên DTO. Đó là gần như mọi cách mà "client chạy DLL cũ" biểu hiện, và nó là bug đã tốn thời gian
ở ba phase liên tiếp vì **không có triệu chứng riêng**.

**Không bắt được:** đổi **hành vi** mà không đổi hình dạng. Sửa công thức trong `MovementRules.Step`,
đổi thứ tự các phép, chỉnh một hằng còn sót trong code — hash y nguyên, mà hai bên đã mô phỏng khác
nhau. Triệu chứng của nó là rubber-band chứ không phải im lặng, nên ít nhất nó **có** triệu chứng.

Vì sao phải biết: một phép kiểm mà người dùng tưởng là toàn diện thì **nguy hiểm hơn không có** — gặp
rubber-band, họ sẽ loại trừ "lệch phiên bản" ngay từ đầu vì "đã có contract hash rồi" và đi tìm sai
hướng. Mọi lớp bảo vệ đều phải kèm một câu ghi rõ nó **không** bảo vệ cái gì.

</details>

---

## Để dành (ghi lại, chưa làm)

- **Đẩy config nóng cho người đang online.** Một gói `ConfigUpdate` broadcast khi reload, client thay
  `MovementConfig` đang dùng và server thay `entity.Movement` **trong cùng một tick** cho khớp. Cạm bẫy:
  giữa lúc client còn input treo chưa được xác nhận, đổi số là replay bằng bộ số mới trên trạng thái
  sinh ra từ bộ số cũ. Làm khi có nhu cầu thật, và làm cẩn thận.
- **Config từ Google Sheet** (pipeline của `com.hungnt.dataconfig`): xuất sheet → json. Đáng làm khi
  bảng số bắt đầu dày — tức là Phase 12–14 với item, quái, công thức sát thương.
- **Nhiều map trong config.** `MapFile` hiện là một chuỗi; thành `Dictionary<int, string>` là mở đường
  cho cửa chuyển map ở Phase 10 "Để dành".
- **Tách file config theo môi trường** (`game.dev.json` / `game.prod.json`), chọn bằng biến môi trường.
  Cùng lúc đó là chỗ để bắt đầu **không commit file có secret** (`CLAUDE.md` §Anti-patterns) — hôm nay
  config chưa có gì bí mật, ngày có chuỗi kết nối MySQL ở Phase 19 thì có.
- **`SessionState.Verified`** đứng trước `Authenticated`, và `MinState` cho mọi handler khác — để một
  client chưa qua kiểm phiên bản không gõ được cửa nào ngoài `VersionCheck`.
- **Kiểm cả hành vi, không chỉ hình dạng.** Giới hạn ở câu 9 chữa được bằng cách băm luôn **nội dung**
  của `MovementRules.Step` — ví dụ chạy một chuỗi input cố định qua `Step` lúc khởi động rồi băm dãy
  trạng thái ra. Rẻ bất ngờ, và nó bắt đúng loại lệch nguy hiểm nhất còn sót lại.

---

**Xong Phase 11 → hết Chặng C.** Thế giới sống: nhiều người thấy nhau ở phạm vi có giới hạn, map có
hình dạng thật, nhân vật biết diễn, và mọi con số đều là **dữ liệu** chứ không phải hằng số trong code.

Chặng D bắt đầu vòng gameplay thật — [PHASE-12](PHASE-12.md): túi đồ, feature dọc đầu tiên đi đủ
DB → DAL → logic → packet → UI, khuôn mẫu cho mọi feature về sau. Nó cũng là nơi **bảng item** trở thành
config loại B thứ hai, đi đúng con đường mà bản đồ vừa mở ra hôm nay. (Viết khi bạn báo xong Phase 11.)
