# PHASE 6 — Game loop & di chuyển authoritative

> **Kết quả cuối Phase 6:** giữ WASD, nhân vật chạy mượt trên map; **server** là bên quyết định vị trí —
> client sửa tốc độ, sửa gói tin kiểu gì cũng không chạy nhanh hơn được. Rớt mạng / thoát game giữa lúc
> đang chạy, vào lại đứng đúng chỗ vừa rời đi (SavePosition của Phase 5 giờ mới có ý nghĩa thật).
>
> **Điều kiện:** xong [`PHASE-5.md`](PHASE-5.md) tới CHECKPOINT B và cả 4 thử nghiệm.
>
> **Bài học chính:** ba ý tưởng nền của mọi game online — **fixed tick** (server có nhịp tim riêng),
> **client gửi ý định, không gửi kết quả**, và **prediction + reconciliation** (client đoán trước để mượt,
> server sửa lại để đúng).

Format như Phase 5: mỗi bước có **hướng làm** hiện sẵn và **📖 Lời giải** trong foldout —
tự code trước, mở lời giải sau để đối chiếu.

---

## Ba dòng thời gian

Từ phase này có **ba** vòng lặp chạy với nhịp khác nhau, và phần lớn bug đồng bộ sinh ra từ việc
nhầm lẫn giữa chúng:

| Vòng lặp | Nhịp | Ai điều khiển |
|----------|------|----------------|
| Frame client (`Update`) | 60–240 fps, **biến thiên** theo máy | GPU/vsync |
| Tick server | 20 tick/s, **cố định** | Server tự quyết |
| Mạng | Gói đến lúc nào tuỳ trời | Không ai cả |

**Vì sao server cần tick cố định?** Ba lý do: (1) `dt` cố định → cùng một chuỗi input luôn cho cùng một
kết quả, dễ suy luận, dễ debug; (2) công bằng — mọi client được xử lý theo cùng một nhịp, không phụ thuộc
gói của ai đến trước; (3) chi phí dự đoán được — 20 tick/s là 20 lần tính/giây dù có 1 hay 100 người,
không phải "mỗi gói đến là một lần tính".

**Vì sao client gửi ý định, không gửi vị trí?** Nếu client gửi `(x, y)` và server tin, thì sửa gói tin là
dịch chuyển tức thời. Client ở đây chỉ gửi **hướng** đang bấm; vị trí do server tính bằng công thức
`pos += dir × speed × dt` với `speed` và `dt` **của server**. Giống bài `EnterWorld` payload rỗng ở
Phase 5: tốc độ không nằm trên gói tin thì không có gì để giả.

**Prediction & reconciliation — vì sao và là gì?** Nếu client bấm phím rồi ngồi chờ server xác nhận mới
nhúc nhích, mỗi bước đi trễ một round-trip — cảm giác như bơi trong mật. Nên client **dự đoán**: áp dụng
ngay input của mình bằng đúng công thức của server, đồng thời đánh số (`Seq`) từng input gửi đi. Server
trả về "vị trí của bạn sau khi tôi xử tới input số N". Client **đối chiếu**: lấy vị trí server làm gốc,
chạy lại (replay) các input sau N mà server chưa xử, ra vị trí "đáng lẽ". Trùng với dự đoán thì thôi;
lệch thì lấy theo server — đó là cú "giật nhẹ" bạn vẫn thấy trong game online khi mạng xấu.

```
Client                                  Server (tick 20Hz)
  │ giữ phím D                            │
  ├─ seq=7 dir=(1,0) ──────────────────►  │ lưu input mới nhất của entity
  │  tự tiến X += speed*dt (dự đoán)      ├─ tick: X += dir*speed*TICK_DT
  ├─ seq=8 dir=(1,0) ──────────────────►  │
  │                                       ├─ tick → MoveState{LastSeq=8, X, Y}
  │  ◄────────────────────────────────────┤
  │  xoá pending ≤ 8, replay 9,10...      │
  │  lệch? → lấy vị trí server làm chuẩn  │
```

Một hệ quả quan trọng của thiết kế "input là **trạng thái** đang bấm": **thả phím cũng là một input** —
client phải gửi `(0,0)`. Không gửi thì server vẫn giữ hướng cũ và nhân vật chạy thẳng tới biên map.

---

## Bước 1 — Shared: hằng số chung + contract

### Hướng làm

**File mới `Server/Shared/World/MovementRules.cs`** — hằng số và **công thức mô phỏng** dùng chung:

- `TICK_RATE = 20`, `TICK_DT = 1f / TICK_RATE`, `MOVE_SPEED = 5f` (đơn vị/giây),
  `WORLD_HALF_EXTENT = 20f` (map tạm là hình vuông quanh gốc toạ độ, chưa có va chạm).
- Một hàm `Step(x, y, dirX, dirY, dt)` → toạ độ mới: cộng dịch chuyển rồi clamp trong biên.

Điểm mấu chốt: client dự đoán bằng **đúng công thức** server dùng — lệch nhau một hằng số hay một phép
clamp là dự đoán sai mãi mãi và nhân vật bị kéo giật liên tục. Cách chắc nhất để hai bên không lệch:
**cùng gọi một hàm** — đặt nó trong Shared, đúng nguyên tắc contract 1 nguồn (golden rule #4, giờ áp
dụng cho cả logic chứ không chỉ DTO). Phase 9 sẽ chuyển các con số vào config; hàm `Step` vẫn ở lại đây.

**`NetCmd`** — mở dải World/Movement (300–399), hai lệnh:

- `MoveInput = 300` — client → server, **fire-and-forget**: handler trả `NetResult.None`, không có
  response riêng cho từng input. Trả lời từng gói là nhân đôi lưu lượng mà không thêm thông tin —
  câu trả lời gộp chính là `MoveState` mỗi tick.
- `MoveState = 301` — server → client, đẩy mỗi tick: vị trí authoritative của **chính** người chơi
  kèm số thứ tự input cuối đã xử lý.

**DTO mới `Server/Shared/Dto/World/MoveDto.cs`** (namespace `MMORPG.Shared.Dto.World`, theo kiểu
`Dto.Character` bạn đã đặt):

- `MoveInputRequest { Seq (int), DirX, DirY (float) }` — chú ý: **không có** trường thời gian.
  Server dùng `TICK_DT` của chính nó; client mà gửi được `dt` thì `dt` chính là chỗ để hack tốc độ.
- `MoveStateResponse { LastInputSeq (int), X, Y (float) }`

Xong `dotnet build Server/Shared` cho DLL sang Unity.

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`Server/Shared/World/MovementRules.cs`**:

```csharp
using System;

namespace MMORPG.Shared.World
{
    /// <summary>
    /// Hằng số và công thức di chuyển dùng CHUNG: server mô phỏng thật, client dự đoán trước.
    /// Hai bên phải ra cùng kết quả từ cùng input — vì vậy công thức chỉ tồn tại ở đây, một nơi.
    /// </summary>
    public static class MovementRules
    {
        public const int TICK_RATE = 20;
        public const float TICK_DT = 1f / TICK_RATE;

        /// <summary>Tốc độ chạy, đơn vị world/giây.</summary>
        public const float MOVE_SPEED = 5f;

        /// <summary>Nửa cạnh vùng đi lại: map tạm là hình vuông [-E, +E] quanh gốc, chưa có va chạm.</summary>
        public const float WORLD_HALF_EXTENT = 20f;

        /// <summary>
        /// Một bước mô phỏng: dịch theo hướng rồi kẹp trong biên map.
        /// dir phải đã chuẩn hoá (độ dài ≤ 1) — người gọi chịu trách nhiệm, hàm này không kiểm lại.
        /// </summary>
        public static (float X, float Y) Step(float x, float y, float dirX, float dirY, float dt)
        {
            x += dirX * MOVE_SPEED * dt;
            y += dirY * MOVE_SPEED * dt;

            x = Math.Clamp(x, -WORLD_HALF_EXTENT, WORLD_HALF_EXTENT);
            y = Math.Clamp(y, -WORLD_HALF_EXTENT, WORLD_HALF_EXTENT);

            return (x, y);
        }
    }
}
```

**`Server/Shared/Net/NetCmd.cs`** — thêm region:

```csharp
        #region World / Movement (300–399)

        /// <summary>
        /// Ý định di chuyển: hướng đang bấm + số thứ tự. Fire-and-forget — không có response riêng,
        /// server trả lời gộp bằng <see cref="MoveState"/> mỗi tick.
        /// Request: <see cref="Dto.World.MoveInputRequest"/>
        /// </summary>
        MoveInput = 300,

        /// <summary>
        /// Vị trí authoritative của chính người chơi, server đẩy mỗi tick.
        /// Payload: <see cref="Dto.World.MoveStateResponse"/>
        /// </summary>
        MoveState = 301,

        #endregion
```

**`Server/Shared/Dto/World/MoveDto.cs`**:

```csharp
using MemoryPack;

namespace MMORPG.Shared.Dto.World
{
    /// <summary>
    /// Trạng thái phím đang bấm tại một bước dự đoán của client. Cố tình KHÔNG có trường thời gian:
    /// server tích phân bằng TICK_DT của chính nó — dt mà đi trên gói tin thì dt là chỗ hack tốc độ.
    /// </summary>
    [MemoryPackable]
    public partial class MoveInputRequest
    {
        /// <summary>Số thứ tự client tự đánh, tăng dần. Server echo lại để client biết mình đã được xử tới đâu.</summary>
        public int Seq { get; set; }

        public float DirX { get; set; }
        public float DirY { get; set; }
    }

    [MemoryPackable]
    public partial class MoveStateResponse
    {
        /// <summary>Input cuối cùng server đã nhận trước tick này. Client xoá pending ≤ số này rồi replay phần còn lại.</summary>
        public int LastInputSeq { get; set; }

        public float X { get; set; }
        public float Y { get; set; }
    }
}
```

</details>

---

## Bước 2 — Server: game loop tick cố định

### Hướng làm

**File mới `Server/GameServer/World/GameLoop.cs`** — nhịp tim của server:

```csharp
public sealed class GameLoop
{
    public GameLoop(WorldService worldService) { ... }
    public async Task RunAsync(CancellationToken ct) { ... }
}
```

Cấu trúc bên trong là **vòng lặp accumulator** — pattern kinh điển, đáng thuộc lòng:

1. Đo thời gian thật đã trôi bằng `Stopwatch` (không phải `DateTime.Now` — độ phân giải thấp và bị
   chỉnh giờ hệ thống ảnh hưởng).
2. Cộng dồn vào `accumulator`; chừng nào `accumulator >= TICK_DT` thì chạy một tick và trừ đi `TICK_DT`.
3. `await Task.Delay(1)` để nhường CPU rồi lặp lại.

**Hai bẫy phải tự xử:**

- **`Task.Delay(1)` không ngủ 1ms.** Trên Windows, timer hệ thống mặc định ~15.6ms — delay "1ms" thực tế
  dậy sau ~15ms. Đây chính là lý do phải có accumulator: vòng lặp dậy lúc nào không quan trọng, thời gian
  **nợ** được cộng dồn và trả đủ bằng số tick tương ứng. `Task.Delay(TICK_DT)` trần thì mỗi vòng trễ một
  chút và nhịp trôi dần — sai kiểu tích luỹ, khó thấy bằng mắt.
- **Spiral of death.** Nếu một tick chạy lâu hơn `TICK_DT` (GC, breakpoint, máy lag), nợ tích lại; vòng
  sau phải chạy nhiều tick bù, lại càng lâu, nợ càng phình — server không bao giờ đuổi kịp nữa. Chặn bằng
  trần nợ: `accumulator` vượt quá `MAX_CATCH_UP × TICK_DT` thì **xoá bớt nợ** (chấp nhận thế giới chậm đi
  một nhịp còn hơn chết hẳn). Nhớ bọc `try/catch` quanh tick — một tick ném lỗi không được giết game loop.

**`Program.cs`**: tạo `GameLoop`, chạy `_ = gameLoop.RunAsync(cts.Token);` (fire-and-forget như các
session), đặt **trước** vòng accept.

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`Server/GameServer/World/GameLoop.cs`**:

```csharp
using System.Diagnostics;
using MMORPG.ServerCore;
using MMORPG.Shared.World;

namespace MMORPG.GameServer.World
{
    /// <summary>
    /// Nhịp tim của server: gọi <see cref="WorldService.Tick"/> với bước thời gian cố định,
    /// đều đặn bất kể máy nhanh hay chậm.
    /// </summary>
    public sealed class GameLoop
    {
        /// <summary>
        /// Trần số tick bù trong một lượt. Không có trần: một cú khựng (GC, breakpoint) làm nợ
        /// thời gian phình ra, vòng sau phải bù nhiều tick hơn, lại càng lâu — spiral of death.
        /// Có trần: thế giới chậm lại một nhịp rồi chạy tiếp, xấu nhưng sống.
        /// </summary>
        private const int MAX_CATCH_UP = 5;

        private readonly WorldService _worldService;

        public GameLoop(WorldService worldService)
        {
            _worldService = worldService;
        }

        public async Task RunAsync(CancellationToken ct)
        {
            // Stopwatch chứ không phải DateTime.Now: độ phân giải cao và không bị
            // đổi giờ hệ thống / NTP kéo lùi thời gian.
            var stopwatch = Stopwatch.StartNew();
            double last = 0;
            double accumulator = 0;

            Log.Info($"Game loop {MovementRules.TICK_RATE.ToString().Green()} tick/s " +
                     $"({MovementRules.TICK_DT * 1000:0}ms/tick)");

            while (!ct.IsCancellationRequested)
            {
                // Cộng dồn thời gian thật đã trôi từ vòng trước. Vòng lặp dậy trễ bao nhiêu
                // không quan trọng — nợ được ghi lại đủ ở đây.
                double now = stopwatch.Elapsed.TotalSeconds;
                accumulator += now - last;
                last = now;

                if (accumulator > MovementRules.TICK_DT * MAX_CATCH_UP)
                    accumulator = MovementRules.TICK_DT * MAX_CATCH_UP;

                // Trả nợ: mỗi TICK_DT nợ là một tick, có thể 0 hoặc nhiều tick trong một lượt dậy.
                while (accumulator >= MovementRules.TICK_DT)
                {
                    accumulator -= MovementRules.TICK_DT;

                    try
                    {
                        _worldService.Tick(MovementRules.TICK_DT);
                    }
                    catch (Exception ex)
                    {
                        // Một tick hỏng không được giết nhịp tim của cả server.
                        Log.Error(ex, "Tick ném lỗi");
                    }
                }

                // Nhường CPU. Trên Windows lượt "ngủ 1ms" này thật ra ~15ms do độ phân giải timer —
                // chính vì thế mới cần accumulator thay vì tin vào Delay.
                await Task.Delay(1, ct);
            }
        }
    }
}
```

**`Server/GameServer/Program.cs`** — sau khi tạo `worldService`, trước vòng accept:

```csharp
var gameLoop = new GameLoop(worldService);
_ = gameLoop.RunAsync(cts.Token);
```

</details>

---

## Bước 3 — Server: input → entity → tick

### Hướng làm

Ba mảnh, đi theo đường của một gói `MoveInput`:

**1. `PlayerEntity` thêm phần input + mô phỏng.** Input mới nhất là **trạng thái** (không phải hàng đợi):

```csharp
public int LastInputSeq { get; private set; }
public void SetInput(int seq, float dirX, float dirY)   // handler gọi, thread pool
public void Integrate(float dt)                          // tick gọi, luồng game loop
```

`Integrate` gọi `MovementRules.Step` rồi ghi lại `X/Y`. Chú ý ranh giới luồng: `SetInput` chạy trên
thread xử lý gói, `Integrate` chạy trên luồng game loop. Với vài field `float`/`int` ghi-một-nơi-đọc-một-nơi
thì mỗi phép ghi là nguyên tử, tệ nhất tick này dùng input trễ một nhịp — chấp nhận được. (Ngày nào input
thành struct nhiều field phải nhất quán với nhau thì mới cần lock — chưa phải hôm nay.)

Thêm một hàng rào rẻ tiền: đếm `_ticksSinceInput` — quá 1 giây không nhận input mới thì coi hướng là
`(0,0)`. Không có nó, client treo/rớt giữa lúc giữ phím sẽ để nhân vật chạy tới biên map mãi.

**2. Handler mới `Server/GameServer/Handlers/MoveHandler.cs`** — `[TcpHandler(NetCmd.MoveInput,
MinState = SessionState.InWorld)]`. Việc của nó: **kiểm dịch input rồi đặt lên entity**, trả
`NetResult.None`. Hai lớp kiểm không được quên:

- `float.IsFinite` cho cả hai trục — client gửi `NaN` mà lọt vào phép cộng là `X/Y` thành `NaN` vĩnh viễn
  (NaN lây qua mọi phép toán) và theo `SavePosition` vào tận DB.
- Độ dài vector > 1 thì chuẩn hoá lại — gửi `dir=(10,0)` là cách hack tốc độ ngây thơ nhất, và dòng này
  là thứ vô hiệu nó.

Server quét attribute tự động nên **không phải đăng ký gì thêm** — chỉ client mới có nghi thức
`GameLifetimeScope`.

**3. `WorldService.Tick(float dt)`** — game loop gọi mỗi tick: duyệt mọi entity, `Integrate(dt)`, rồi
`entity.Owner?.SendData(NetCmd.MoveState, ...)` đẩy vị trí cho chính chủ. 20 gói nhỏ/giây/người là chấp
nhận được ở quy mô này; lọc "chỉ gửi khi có thay đổi" để dành khi làm đồng bộ nhiều người chơi.

### ✅ CHECKPOINT A — một gói tin, một bài học

Thêm tạm vào `NetworkProbe` nút gửi **một** gói `MoveInput { Seq = 1, DirX = 1, DirY = 0 }` (sau khi đã
đăng nhập + vào world). Quan sát:

1. Client console nhận `MoveState` dồn dập ~20 gói/giây, `X` tăng dần đều.
2. Nhân vật **không dừng lại** — chạy miết về phía Đông rồi dừng ở biên `WORLD_HALF_EXTENT`... hoặc dừng
   sau đúng 1 giây nếu bạn đã làm hàng rào `_ticksSinceInput`. Cả hai đều chứng minh: input là **trạng
   thái**, một gói có hiệu lực cho tới gói tiếp theo — và vì thế thả phím phải gửi `(0,0)`.
3. Gửi `DirX = 10` → tốc độ **không đổi** so với `DirX = 1`. Chuẩn hoá phía server đang làm việc.

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`PlayerEntity.cs`** — thêm:

```csharp
        /// <summary>Input cuối đã nhận. Echo lại cho client trong MoveState để nó biết replay từ đâu.</summary>
        public int LastInputSeq { get; private set; }

        // Hướng đang bấm, do handler ghi / tick đọc. Hai luồng khác nhau nhưng không cần lock:
        // mỗi field là một phép ghi nguyên tử, tệ nhất tick này dùng input trễ một nhịp.
        private float _inputDirX;
        private float _inputDirY;

        // Số tick đã trôi từ input cuối. Client treo/rớt giữa lúc giữ phím mà không có bộ đếm này
        // thì entity chạy theo hướng cũ mãi mãi.
        private int _ticksSinceInput;

        public void SetInput(int seq, float dirX, float dirY)
        {
            LastInputSeq = seq;
            _inputDirX = dirX;
            _inputDirY = dirY;
            _ticksSinceInput = 0;
        }

        public void Integrate(float dt)
        {
            // Quá 1 giây không có input mới → coi như đã thả phím. Trạng thái cũ không được sống mãi.
            if (++_ticksSinceInput > Shared.World.MovementRules.TICK_RATE)
            {
                _inputDirX = 0;
                _inputDirY = 0;
            }

            (X, Y) = Shared.World.MovementRules.Step(X, Y, _inputDirX, _inputDirY, dt);
        }
```

**`Server/GameServer/Handlers/MoveHandler.cs`**:

```csharp
using MMORPG.GameServer.Net;
using MMORPG.GameServer.World;
using MMORPG.Shared.Dto.World;
using MMORPG.Shared.Net;

namespace MMORPG.GameServer.Handlers
{
    public static class MoveHandler
    {
        [TcpHandler(NetCmd.MoveInput, MinState = SessionState.InWorld)]
        public static Task<NetResult> OnMoveInput(NetRequest req)
        {
            var input = req.GetData<MoveInputRequest>();
            PlayerEntity entity = req.Session.Entity;

            // MinState đã chặn phần lớn, nhưng LeaveWorld có thể xảy ra giữa lúc gói đang bay.
            if (entity == null)
                return Task.FromResult(NetResult.None);

            float dirX = input.DirX;
            float dirY = input.DirY;

            // NaN lây qua MỌI phép toán: lọt một lần là X/Y thành NaN vĩnh viễn và theo
            // SavePosition vào tận DB. Chặn ngay cửa.
            if (!float.IsFinite(dirX) || !float.IsFinite(dirY))
                return Task.FromResult(NetResult.None);

            // Vector dài hơn 1 là gian lận tốc độ (dir=(10,0) = chạy nhanh gấp 10).
            // Chuẩn hoá lại — client tử tế gửi ≤ 1 nên không bị ảnh hưởng.
            float length = MathF.Sqrt(dirX * dirX + dirY * dirY);
            if (length > 1f)
            {
                dirX /= length;
                dirY /= length;
            }

            entity.SetInput(input.Seq, dirX, dirY);

            // Fire-and-forget: không trả lời từng input. Câu trả lời gộp là MoveState mỗi tick.
            return Task.FromResult(NetResult.None);
        }
    }
}
```

**`WorldService.cs`** — thêm:

```csharp
        /// <summary>Game loop gọi mỗi tick: mô phỏng mọi entity rồi báo vị trí cho chính chủ.</summary>
        public void Tick(float dt)
        {
            foreach (PlayerEntity entity in _entities.Values)
            {
                entity.Integrate(dt);

                entity.Owner?.SendData(NetCmd.MoveState, new MoveStateResponse
                {
                    LastInputSeq = entity.LastInputSeq,
                    X = entity.X,
                    Y = entity.Y,
                });
            }
        }
```

(cần `using MMORPG.Shared.Dto.World;` và `using MMORPG.Shared.Net;` ở đầu file)

</details>

---

## Bước 4 — Client: dự đoán, gửi, đối chiếu

### Hướng làm

**1. Chiều gửi + chiều nhận** — mỗi bên một dòng, theo pattern có sẵn:

- `WorldApi` thêm `Move(int seq, float dirX, float dirY)` gửi `NetCmd.MoveInput`.
- `WorldNetHandler` thêm event `OnMoveState` + method `[NetHandler(NetCmd.MoveState)]`.
- `GameLifetimeScope`: **không thêm dòng nào** — cả hai class đã đăng ký từ Phase 5. Lần đầu tiên
  thêm lệnh mạng mà không đụng DI.

**2. File mới `Assets/Game/Scripts/World/PlayerMotor.cs`** — MonoBehaviour gắn lên **prefab nhân vật**.
Prefab được `Instantiate` lúc chạy nên VContainer không inject vào nó được — `WorldSpawner` sau khi
Instantiate phải gọi `motor.Init(worldApi, worldNetHandler, vị trí spawn)` để đưa phụ thuộc vào tay
(và `WorldSpawner` nhận hai thứ đó qua `[Inject]`). Nhớ unsubscribe trong `OnDestroy`.

Ruột motor gồm ba phần:

- **Vòng dự đoán** (`Update`): đọc `Input.GetAxisRaw("Horizontal"/"Vertical")`, chuẩn hoá nếu dài quá 1
  (đi chéo!). Cộng dồn `Time.deltaTime` vào accumulator, mỗi khi đủ `TICK_DT` thì chạy một **bước**:
  `seq++` → `_simPos = MovementRules.Step(_simPos, dir, TICK_DT)` → lưu `(seq, dir)` vào danh sách
  pending → `WorldApi.Move(...)`. Đúng vòng accumulator của server, phiên bản client — hai bên cùng nhịp
  20 bước/giây, mỗi bước một gói, **kể cả khi dir = (0,0)**.
- **Đối chiếu** (`OnMoveState`): xoá pending có `Seq <= LastInputSeq`; lấy `(X, Y)` server làm gốc,
  replay các pending còn lại qua `MovementRules.Step`; kết quả là vị trí "đáng lẽ" — gán vào `_simPos`.
  Nếu dự đoán đúng, kết quả trùng `_simPos` cũ và không ai thấy gì; nếu sai (gói mất, server chỉnh),
  nhân vật được kéo về đúng chỗ.
- **Làm mượt hình ảnh**: `_simPos` nhảy theo bậc 20Hz, gán thẳng vào `transform.position` sẽ giật lụp
  bụp ở 144fps. Tách **vị trí mô phỏng** khỏi **vị trí hiển thị**: mỗi frame `MoveTowards` transform về
  phía `_simPos` với tốc độ hơi cao hơn `MOVE_SPEED`. Camera vẫn bám transform như cũ, không sửa gì.

**3. `WorldSpawner`**: lấy `PlayerMotor` từ object vừa spawn, gọi `Init`. `[Inject]` thêm `WorldApi`
và `WorldNetHandler` vào spawner.

**Câu hỏi thiết kế nghĩ trước khi code:** vì sao replay dùng `TICK_DT` cố định chứ không phải thời gian
thật giữa hai lần gửi? — Vì server cũng tích phân mỗi input đúng một `TICK_DT`; replay phải nhại lại
server từng phép tính, không phải nhại lại đồng hồ của chính mình.

### ✅ CHECKPOINT B — mục tiêu cuối Phase 6

1. Đăng nhập → vào world → **WASD chạy mượt**, camera bám, không giật khi mạng nội bộ.
2. Nhân vật dừng đúng lúc thả phím; không trôi thêm.
3. Đi tới biên map → dừng lại ở `±WORLD_HALF_EXTENT`, không xuyên.
4. Chạy một đoạn xa, **thoát Play mode** → server log Despawn + vị trí được lưu → vào lại → đứng đúng
   chỗ vừa rời. (SavePosition Phase 5 giờ có ý nghĩa thật.)
5. `Ctrl+C` GameServer giữa lúc đang chạy → bật lại, vào world → đứng ở vị trí đã lưu lần cuối
   (lần Despawn gần nhất — có thể "cũ" hơn vị trí lúc chết server một chút; hiểu vì sao).

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`WorldApi.cs`** — thêm:

```csharp
        public void Move(int seq, float dirX, float dirY)
        {
            // Không log ở đây — 20 lần/giây, log là dìm chết console.
            _netService.Send(NetCmd.MoveInput, new MoveInputRequest { Seq = seq, DirX = dirX, DirY = dirY });
        }
```

(thêm `using MMORPG.Shared.Dto.World;`)

**`WorldNetHandler.cs`** — thêm:

```csharp
        public event Action<MoveStateResponse> OnMoveState;

        [NetHandler(NetCmd.MoveState)]
        private void HandleMoveState(NetPacket packet)
        {
            OnMoveState?.Invoke(packet.GetData<MoveStateResponse>());
        }
```

**`Assets/Game/Scripts/World/PlayerMotor.cs`**:

```csharp
using System.Collections.Generic;
using MMORPG.Client.Network.Handlers;
using MMORPG.Shared.Dto.World;
using MMORPG.Shared.World;
using UnityEngine;

namespace MMORPG.Client.World
{
    /// <summary>
    /// Di chuyển nhân vật của chính mình: đọc phím, dự đoán tại chỗ bằng công thức chung,
    /// gửi ý định lên server, và đối chiếu lại khi server trả vị trí authoritative.
    /// </summary>
    public sealed class PlayerMotor : MonoBehaviour
    {
        /// <summary>Một bước dự đoán chưa được server xác nhận — nguyên liệu để replay.</summary>
        private readonly struct PendingInput
        {
            public readonly int Seq;
            public readonly Vector2 Dir;

            public PendingInput(int seq, Vector2 dir)
            {
                Seq = seq;
                Dir = dir;
            }
        }

        private WorldApi _worldApi;
        private WorldNetHandler _worldNetHandler;

        private readonly List<PendingInput> _pending = new();
        private int _nextSeq;
        private float _accumulator;

        // Vị trí MÔ PHỎNG (nhảy bậc 20Hz) tách khỏi vị trí HIỂN THỊ (transform, mượt theo frame).
        private Vector2 _simPos;

        public void Init(WorldApi worldApi, WorldNetHandler worldNetHandler, Vector2 spawnPos)
        {
            _worldApi = worldApi;
            _worldNetHandler = worldNetHandler;
            _simPos = spawnPos;

            _worldNetHandler.OnMoveState += OnMoveState;
        }

        private void OnDestroy()
        {
            if (_worldNetHandler != null)
                _worldNetHandler.OnMoveState -= OnMoveState;
        }

        private void Update()
        {
            if (_worldApi == null)
                return;

            var dir = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

            // Đi chéo là (1,1) — dài √2. Không chuẩn hoá thì đi chéo nhanh hơn đi thẳng,
            // và server (cũng chuẩn hoá) sẽ không đồng ý với dự đoán của ta.
            if (dir.sqrMagnitude > 1f)
                dir.Normalize();

            // Vòng accumulator y hệt game loop server: dự đoán theo bậc TICK_DT cố định,
            // không theo frame — frame rate không được ảnh hưởng tốc độ chạy.
            _accumulator += Time.deltaTime;
            while (_accumulator >= MovementRules.TICK_DT)
            {
                _accumulator -= MovementRules.TICK_DT;
                Step(dir);
            }

            // Hiển thị đuổi theo mô phỏng, hơi nhanh hơn tốc độ chạy để không bao giờ tụt lại xa.
            transform.position = Vector3.MoveTowards(
                transform.position, new Vector3(_simPos.x, _simPos.y, 0f),
                MovementRules.MOVE_SPEED * 1.5f * Time.deltaTime);
        }

        /// <summary>Một bước dự đoán: mô phỏng trước, ghi nợ, gửi lên server. Gửi CẢ khi dir = (0,0) — thả phím cũng là input.</summary>
        private void Step(Vector2 dir)
        {
            int seq = ++_nextSeq;

            (_simPos.x, _simPos.y) = MovementRules.Step(_simPos.x, _simPos.y, dir.x, dir.y, MovementRules.TICK_DT);

            _pending.Add(new PendingInput(seq, dir));
            _worldApi.Move(seq, dir.x, dir.y);
        }

        /// <summary>
        /// Đối chiếu với server: vị trí server + replay các input server chưa xử = vị trí "đáng lẽ".
        /// Dự đoán đúng thì kết quả trùng cái đang có; sai thì bị kéo về — đó là cú giật rubber-band.
        /// </summary>
        private void OnMoveState(MoveStateResponse state)
        {
            _pending.RemoveAll(p => p.Seq <= state.LastInputSeq);

            var pos = new Vector2(state.X, state.Y);
            foreach (PendingInput pending in _pending)
            {
                (pos.x, pos.y) = MovementRules.Step(pos.x, pos.y, pending.Dir.x, pending.Dir.y, MovementRules.TICK_DT);
            }

            _simPos = pos;
        }
    }
}
```

**`WorldSpawner.cs`** — nhận thêm phụ thuộc và truyền vào motor:

```csharp
        private WorldApi _worldApi;
        private WorldNetHandler _worldNetHandler;

        [Inject]
        public void Construct(WorldApi worldApi, WorldNetHandler worldNetHandler)
        {
            _worldApi = worldApi;
            _worldNetHandler = worldNetHandler;
        }
```

và trong `SpawnLocalPlayer`, sau khi `Instantiate`:

```csharp
            // Prefab sinh lúc runtime — VContainer không tự inject. Đưa phụ thuộc vào tay.
            var motor = _localPlayerObject.GetComponent<PlayerMotor>();
            motor.Init(_worldApi, _worldNetHandler, new Vector2(response.X, response.Y));
```

(thêm `using MMORPG.Client.Network.Handlers;`, `using VContainer;` — và gắn component `PlayerMotor`
vào prefab nhân vật trong Editor)

</details>

---

## Bước 5 — Ba thử nghiệm bắt buộc

**1. Hack tốc độ bằng vector.** Sửa tạm `PlayerMotor.Step`: gửi `dir * 5f` (nhưng dự đoán vẫn dùng
`dir` thường). Nhân vật **không** nhanh hơn — server chuẩn hoá về độ dài 1. Giờ sửa cả dự đoán dùng
`dir * 5f`: nhân vật lao nhanh... rồi bị **kéo giật về** liên tục — dự đoán một đằng, sự thật một nẻo.
Đó chính là hình dáng của "hack thất bại" nhìn từ phía kẻ hack.

**2. Hack tốc độ bằng hằng số.** Đổi `MOVE_SPEED` chỉ trong đầu client (nhân 2 trong lời gọi `Step` của
dự đoán). Rubber-band y như trên. Kết luận thuộc lòng: mọi thứ client tự ý làm khác server đều bị
`MoveState` cải chính trong vòng một round-trip.

**3. Vị trí sống sót qua rớt mạng.** Chạy tới một góc map, tắt client kiểu phũ nhất (Stop Play ngay giữa
lúc giữ phím). Server log Despawn + lưu vị trí. Vào lại: đứng đúng góc đó. Lặp lại nhưng `Ctrl+C`
GameServer trước — vị trí vào lại là vị trí của lần **Despawn gần nhất**, không phải khoảnh khắc server
chết. Hiểu rõ khoảng hở này (và tại sao game thật autosave định kỳ — bài của phase vận hành).

---

## Troubleshooting

| Triệu chứng | Nguyên nhân | Xử lý |
|-------------|-------------|-------|
| Bấm phím không nhúc nhích, không lỗi | `PlayerMotor` chưa `Init` (quên gọi trong `WorldSpawner`) hoặc chưa gắn vào prefab | Kiểm `GetComponent<PlayerMotor>()` khác null; log trong `Init` |
| Không nhận `MoveState` nào | Quên thêm `[NetHandler(NetCmd.MoveState)]`, hoặc gói gửi trước khi vào world bị `NotAuthenticated`/`InWorld` chặn | Client console xem gói Error; chỉ gửi Move sau `OnEnteredWorld` |
| Nhân vật giật lùi liên tục khi đi | Dự đoán lệch server: quên chuẩn hoá đi chéo, `MOVE_SPEED`/clamp hai bên khác nhau, hoặc không dùng chung `MovementRules.Step` | So từng phép tính hai bên — lý tưởng là cùng gọi một hàm |
| Đi chéo nhanh hơn đi thẳng | Thiếu `Normalize` phía client (server có chuẩn hoá nên còn kèm rubber-band) | Chuẩn hoá trước khi dự đoán |
| Thả phím nhân vật vẫn trôi tới biên | Client chỉ gửi khi `dir != 0` | Gửi mọi bước, kể cả `(0,0)` — input là trạng thái |
| Nhân vật thỉnh thoảng khựng nửa giây rồi nhảy | GC/khựng ở server, tick bù dồn | Bình thường ở mức nhẹ; xem log nếu xảy ra liên tục |
| Vị trí thành `NaN`, nhân vật biến mất | Thiếu `float.IsFinite` ở `MoveHandler`, có gói input hỏng | Thêm guard; xoá dòng DB hỏng bằng `UPDATE character SET pos_x=0, pos_y=0` |
| Server CPU 100% | Vòng game loop thiếu `Task.Delay` | Xem lại vòng lặp accumulator |
| Tick log báo trễ liên tục | `Task.Delay(1)` dậy ~15ms là bình thường; chỉ bất thường khi tick > 50ms | Đo thân `Tick`, tìm gì đang chậm |
| `Input.GetAxisRaw` ném lỗi hoặc luôn 0 | Project đặt Active Input Handling = Input System Package (mất Input Manager cũ) | Edit → Project Settings → Player → Active Input Handling = Both |

---

## Tự kiểm tra hiểu bài

Tự trả lời từng câu xong mới mở đáp án của câu đó.

**Câu 1.** Vì sao client gửi *hướng* chứ không gửi *vị trí*? Nếu server nhận `(x, y)` từ client và chỉ
"kiểm tra tính hợp lệ" (khoảng cách không quá xa vị trí cũ) thì còn lỗ hổng gì?
<details>
<summary>📖 Đáp án câu 1</summary>

Vị trí là **kết quả**, hướng là **ý định** — server chỉ tin ý định rồi tự tính kết quả bằng tốc độ và
thời gian của chính nó. Nếu nhận vị trí và kiểm "không quá xa": kẻ gian di chuyển từng bước nhỏ vừa đúng
ngưỡng cho phép nhưng **mỗi gói một bước** với tần suất cao — vẫn nhanh hơn thật; hoặc xuyên qua vật cản
mỏng hơn ngưỡng. Kiểm tra kết quả luôn là đuổi theo; không nhận kết quả ngay từ đầu thì không phải đuổi.

</details>

**Câu 2.** Vì sao server không xử lý di chuyển ngay khi gói `MoveInput` đến, mà đợi tới tick?
<details>
<summary>📖 Đáp án câu 2</summary>

Xử ngay là để **nhịp đến của gói tin** điều khiển mô phỏng: ai gửi dày hơn được tích phân nhiều lần hơn
(lại thành hack tốc độ bằng tần suất), kết quả phụ thuộc thứ tự gói đến, và không thể suy luận "tại tick
T thế giới trông thế nào". Tick cố định biến input thành *trạng thái được lấy mẫu đều*: gửi 1 gói hay 100
gói trong một tick, hiệu lực như nhau — đúng một lần `Step` với `TICK_DT`.

</details>

**Câu 3.** `MOVE_SPEED` và hàm `Step` nằm trong `Server/Shared` — vì sao? Chuyện gì xảy ra nếu client
có bản `Step` riêng và một ngày server đổi công thức clamp?
<details>
<summary>📖 Đáp án câu 3</summary>

Dự đoán của client phải nhại server **từng phép tính** — cách duy nhất bảo đảm điều đó dài hạn là hai bên
gọi chung một hàm, cùng chỗ với `NetCmd`/DTO (contract 1 nguồn, mở rộng từ dữ liệu sang logic). Nếu client
có bản riêng và server đổi clamp: không lỗi biên dịch, không lỗi runtime — chỉ có rubber-band ở đúng rìa
map, loại bug "thỉnh thoảng mới thấy" khó lần nhất.

</details>

**Câu 4.** Vì sao `MoveInputRequest` không có trường thời gian (`dt` hay timestamp)?
<details>
<summary>📖 Đáp án câu 4</summary>

Quãng đường = hướng × tốc độ × **thời gian**. Hướng bị chuẩn hoá, tốc độ là hằng server — nếu client gửi
được thời gian thì đó là biến duy nhất còn lại để gian lận (`dt = 10` → một gói đi mười giây đường).
Server dùng `TICK_DT` của chính nó cho mọi người: biến thời gian rời khỏi tầm tay client hoàn toàn.

</details>

**Câu 5.** Vì sao `MoveInput` là fire-and-forget (`NetResult.None`) thay vì trả response cho từng gói?
<details>
<summary>📖 Đáp án câu 5</summary>

Trả lời từng input là 20 response/giây chỉ để nói "đã nhận" — nhân đôi lưu lượng mà không thêm thông tin,
vì thứ client thật sự cần là *vị trí sau khi xử lý* — thứ mà `MoveState` mỗi tick đã chứa, kèm
`LastInputSeq` đóng vai trò ack gộp cho mọi input đã đến.

</details>

**Câu 6.** Gói `MoveInput` seq=9 bị mất trên đường, seq=10 đến nơi. Kể chuỗi sự kiện phía client từ lúc
nhận `MoveState{LastInputSeq=10}` — vì sao nhân vật chỉ giật nhẹ chứ không hỏng hẳn?
<details>
<summary>📖 Đáp án câu 6</summary>

Server không bao giờ thấy input 9 — tick giữa 9 và 10 nó dùng trạng thái của input 8 (input là trạng
thái, không phải hàng đợi). Client thì đã *dự đoán* có bước 9. Khi `MoveState{LastInputSeq=10}` về:
client xoá pending ≤ 10 (cả 9 lẫn 10), lấy vị trí server làm gốc, replay pending còn lại (11, 12...).
Vị trí gốc ấy thiếu đóng góp của bước 9 so với dự đoán cũ → `_simPos` bị kéo lệch một bước ≈ vài cm —
cú giật nhỏ, rồi hệ tự đồng bộ tiếp. Sai số không tích luỹ vì mỗi `MoveState` là một lần đặt lại gốc
từ sự thật.

</details>

**Câu 7.** Vì sao thả phím vẫn phải gửi `(0,0)`? Và vì sao server vẫn cần thêm hàng rào
`_ticksSinceInput` dù client đã gửi đều?
<details>
<summary>📖 Đáp án câu 7</summary>

Input là **trạng thái có hiệu lực tới khi bị thay thế** — không gửi `(0,0)` thì trạng thái cuối cùng
server biết vẫn là "đang bấm", và nhân vật chạy tiếp. Hàng rào phía server vẫn cần vì "client gửi đều"
là giả định về client **tử tế**: client treo, mất mạng đột ngột, hoặc bị sửa để ngừng gửi sau khi bấm —
server không được để trạng thái cũ sống vô hạn dựa trên lời hứa của phía không đáng tin.

</details>

**Câu 8.** Spiral of death là gì và trần `MAX_CATCH_UP` đánh đổi cái gì lấy cái gì?
<details>
<summary>📖 Đáp án câu 8</summary>

Một tick chạy lâu hơn `TICK_DT` → nợ thời gian tăng → vòng sau phải chạy nhiều tick bù → lượt đó càng
lâu → nợ càng tăng — vòng xoáy không lối ra, server "sống" nhưng không bao giờ đuổi kịp hiện tại. Trần
nợ cắt vòng xoáy bằng cách **vứt bớt thời gian**: đánh đổi tính đúng của mô phỏng (thế giới trôi chậm
lại một nhịp so với đồng hồ thật) lấy tính sống của server. Trong game online, chậm-mà-đều thắng
đúng-mà-chết.

</details>

**Câu 9.** `SetInput` chạy trên thread xử lý gói, `Integrate` chạy trên luồng game loop, không có lock —
vì sao chấp nhận được ở đây, và đến lúc nào thì **không** chấp nhận được nữa?
<details>
<summary>📖 Đáp án câu 9</summary>

Mỗi field `float`/`int` là một phép ghi nguyên tử trên .NET — không bao giờ đọc được giá trị "rách đôi".
Tệ nhất là tick đọc `dirX` mới nhưng `dirY` cũ trong đúng một tick — sai số một nhịp 50ms, tự hết ở tick
sau, không ai nhìn thấy. Hết chấp nhận được khi các field phải **nhất quán với nhau như một khối**: ví
dụ input kèm "vị trí mục tiêu + cờ dùng kỹ năng" mà đọc nửa nọ nửa kia là thi triển kỹ năng sai chỗ —
lúc đó cần gói input vào một object bất biến gán qua một reference (ghi reference cũng nguyên tử), hoặc
lock.

</details>

---

**Xong Phase 6.** Nhân vật của bạn đã di chuyển theo đúng luật của mọi game online tử tế: client đề nghị,
server quyết định, client sửa mình theo. [PHASE-7](PHASE-7.md) mở rộng đúng cơ chế này ra **nhiều người**:
mở 2 client, thấy nhau chạy mượt — snapshot, interpolation buffer, và vì sao người khác trên màn hình
của bạn luôn sống ở quá khứ ~100ms. (Tài liệu Phase 7 sẽ được viết khi bạn báo xong Phase 6.)
