# PHASE 8 — Motor platformer: trọng lực, nhảy, và một hàm cho cả hai bên

> **Kết quả cuối Phase 8:** nhân vật rơi xuống sàn, chạy trái/phải, bấm Space là nhảy lên rồi rơi
> xuống — và **hai client cùng thấy một cú nhảy giống hệt nhau**. Cú nhảy vẫn "đã tay" ngay cả khi
> bạn bấm Space sớm vài phần trăm giây trước lúc chạm đất, hoặc muộn vài phần trăm giây sau khi
> vừa rời mép sàn.
>
> **Điều kiện:** xong [`PHASE-7.md`](PHASE-7.md) tới CHECKPOINT B — hai client đã thấy nhau chạy mượt.
>
> **Bài học chính:** (1) vì sao có server authoritative thì **không dùng được `Rigidbody2D`**;
> (2) entity không còn là một điểm mà là một **trạng thái vật lý** (vị trí + vận tốc + đang-đứng-đất
> + vài bộ đếm) — và reconciliation của Phase 6 phải so sánh *toàn bộ* trạng thái đó chứ không chỉ
> một vector; (3) **nút bấm dạng cạnh** (nhảy) đi qua mạng khác hẳn **trục giữ** (chạy) — quên xử lý
> khác nhau là mất cú nhảy mà không có lỗi nào báo.

Format như trước: **hướng làm** hiện sẵn, **📖 Lời giải** trong foldout.

---

## Vì sao không dùng `Rigidbody2D`

Câu hỏi đầu tiên ai cũng hỏi: Unity có sẵn physics 2D xịn, `Rigidbody2D` + `gravityScale` + `AddForce`
là xong trong 10 phút — sao phải tự viết?

Vì **server không có Unity**. `GameServer` là một process .NET 8 console. Ở đó không tồn tại
`Rigidbody2D`, `Physics2D.Simulate`, hay bất cứ thứ gì của engine. Mà theo golden rule #2, vị trí
nhân vật là **do server quyết**. Nên chỉ có ba lựa chọn:

| Lựa chọn | Hệ quả |
|---|---|
| Client dùng `Rigidbody2D`, server tự tính kiểu khác | Hai bên ra hai kết quả khác nhau **mỗi tick**. Reconciliation luôn thấy lệch → luôn kéo về → **rubber-band vĩnh viễn**. Không phải bug sửa được, mà là hệ quả toán học |
| Client dùng `Rigidbody2D`, server tin client | Vứt bỏ toàn bộ Phase 6. Ai cũng bay được |
| **Cả hai bên gọi chung một hàm thuần C#** | Cùng input → cùng output, tới từng bit. Đây là thứ ta làm |

Đây chính là "contract 1 nguồn" (golden rule #4) nâng lên một bậc: `Shared` không chỉ giữ *dữ liệu*
đi trên dây, mà giữ luôn *hành vi*. `NetCmd` sai một số thì bug câm; `Step` lệch một dòng cũng bug câm
y hệt — chỉ khác là nó biểu hiện thành "game giật".

```
Server/Shared/World/MovementRules.Step()  ←── một hàm thuần, không tham chiếu UnityEngine
        │
        ├──► GameServer.PlayerEntity.Integrate()   mỗi tick — SỰ THẬT
        └──► Client.PlayerMotor.Step()             mỗi tick — DỰ ĐOÁN + REPLAY
```

**`Rigidbody2D` vẫn dùng được** — cho thứ không cần đồng bộ: lá rơi, hòn đá lăn trang trí, mảnh vỡ
hiệu ứng. Chỉ cấm dùng nó cho entity mà server có quyền.

### Điều kiện để "cùng input → cùng output" thành sự thật

Hai runtime khác nhau (CoreCLR trên server, Mono/IL2CPP trên client) chạy cùng một hàm C# **không**
tự động cho cùng kết quả. Bốn luật phải giữ trong `MovementRules`:

1. **Chỉ dùng `+ - * /` và so sánh trên `float`.** Bốn phép này được IEEE-754 quy định chính xác tới
   từng bit, mọi runtime đều phải cho cùng kết quả.
2. **Không gọi `MathF.Sin` / `Cos` / `Pow` / `Exp`.** Đây là hàm thư viện, sai số ở chữ số cuối được
   phép khác nhau giữa các nền tảng. `MathF.Sqrt` thì an toàn (IEEE-754 có quy định cho nó), nhưng
   phase này không cần tới.
3. **Không đọc thời gian, không random, không đọc biến toàn cục.** Hàm phải *thuần*: kết quả chỉ phụ
   thuộc tham số truyền vào.
4. **`dt` luôn là `TICK_DT` cố định.** Không bao giờ truyền `Time.deltaTime`. Đây đã là luật từ Phase 6,
   nhưng có trọng lực thì hậu quả của việc phá luật lớn hơn nhiều: `v += g*dt` với `dt` khác nhau cho
   ra đường bay khác nhau, và sai số **cộng dồn** suốt cú nhảy chứ không tự triệt tiêu.

---

## Bước 1 — Shared: từ "một điểm" thành "một trạng thái vật lý"

### Hướng làm

Đây là bước dài nhất và là toàn bộ linh hồn của phase. Hai bước sau chỉ là đi theo.

**Vấn đề của code hiện tại.** `MovementRules.Step` đang là:

```csharp
public static (float X, float Y) Step(float x, float y, float dirX, float dirY, float dt)
```

Nó giả định hai điều mà platformer phá vỡ cả hai:

- **Y là một trục input.** Người chơi bấm lên → đi lên. Trong platformer, người chơi *không điều khiển
  trục Y*. Y là **hệ quả** của trọng lực và của cú nhảy đã bấm từ trước.
- **Vị trí là toàn bộ trạng thái.** Biết `(x, y)` là biết mọi thứ. Trong platformer thì không: hai nhân
  vật ở đúng cùng một điểm, một đang bay lên và một đang rơi xuống, tick sau sẽ ở hai chỗ khác nhau.
  **Vận tốc là một phần của trạng thái**, không phải thứ suy ra được từ vị trí.

Đây là câu đáng nhớ nhất của phase: **trạng thái là tập nhỏ nhất mà biết nó thì tính được tương lai.**
Với top-down đó là vị trí. Với platformer đó là vị trí + vận tốc + đang-đứng-đất.

**File mới `Server/Shared/World/MoveState.cs`** — hai `struct` POD:

- `MoveIntent` — thứ người chơi bấm được. Chỉ còn **`DirX`** (không còn `DirY`) và **`Jump`**.
- `MoveState` — trạng thái vật lý đầy đủ: `X`, `Y`, `VelX`, `VelY`, `Grounded`.

Dùng `struct` với **field công khai** chứ không phải class có property: đây là túi dữ liệu được copy
theo giá trị hàng chục lần mỗi giây trong vòng replay; property getter/setter không thêm gì ngoài chữ.
Đổi lại phải nhớ nó là *value type* — gán là copy chứ không phải tham chiếu, và đó chính là điều ta
muốn (replay không được vô tình sửa trạng thái gốc).

**`Jump` là cạnh lên, không phải trạng thái giữ.** Quyết định này ảnh hưởng cả ba file sau, nên cân
nhắc kỹ ngay tại đây:

| | `JumpHeld` (nút có đang bị giữ) | **`Jump` (vừa bấm xuống)** ← chọn cái này |
|---|---|---|
| Client lấy giá trị thế nào | `Jump.IsPressed()` đọc đúng lúc tick | `Jump.WasPressedThisFrame()` đọc **mỗi frame**, chốt lại, chờ tick tới lấy |
| Bấm nhanh 30ms giữa hai tick | **Mất cú nhảy** — 20 tick/s nghĩa là 50ms/tick, cú bấm lọt trọn vào khe giữa hai lần đọc | Bắt được: frame chạy 60–300Hz nên không frame nào bỏ lỡ |
| Giữ nút lâu để nhảy cao hơn | Làm được | Không (xem "Để dành") |

Chọn cạnh lên vì **mất input là lỗi không thể chấp nhận**, còn nhảy-cao-theo-độ-giữ chỉ là gia vị.
Cái giá phải trả: cả client lẫn server đều phải **chốt** (latch) giá trị `bool` này — chi tiết ở Bước 2
và Bước 3, và đó là cái bẫy lớn nhất của phase.

**Sửa `Server/Shared/World/MovementRules.cs`** — chữ ký mới, thuần, nhận và trả `MoveState`:

```csharp
public static MoveState Step(MoveState state, MoveIntent intent, float dt)
```

Hằng số cần thêm (đơn vị: world unit và giây — quy ước 1 unit ≈ 1 ô tile):

| Hằng | Giá trị đề xuất | Ý nghĩa |
|---|---|---|
| `GRAVITY` | `30f` | Gia tốc rơi. Trọng lực game platformer luôn lớn hơn 9.81 rất nhiều — để 9.81 thì nhân vật lơ lửng như trên mặt trăng |
| `JUMP_SPEED` | `11f` | Vận tốc bật lên tức thời. Đỉnh nhảy = `JUMP_SPEED² / (2 × GRAVITY)` ≈ **2 unit**, tổng thời gian bay ≈ 0.73s ≈ 15 tick |
| `MAX_FALL_SPEED` | `20f` | Trần tốc độ rơi. Không có nó thì rơi từ trên cao sẽ **xuyên qua sàn** trong một tick (20 unit/s × 0.05s = 1 unit mỗi tick đã là kịch trần an toàn) |
| `GROUND_Y` | `0f` | Sàn tạm — cả thế giới là một mặt phẳng ở y = 0. Map thật là Phase 10 |

**Thứ tự các phép trong `Step` là một phần của contract.** Đổi thứ tự = đổi kết quả = hai bên lệch nhau.
Ghi hẳn thứ tự vào comment:

```
1. Vận tốc ngang  ← đặt thẳng từ DirX (chưa có gia tốc/ma sát — cố ý, phase này dạy một thứ thôi)
2. Trọng lực      ← VelY -= GRAVITY * dt, rồi kẹp ở -MAX_FALL_SPEED
3. Nhảy           ← nếu có yêu cầu VÀ đang đứng đất thì VelY = JUMP_SPEED (ghi đè bước 2)
4. Tích phân      ← X += VelX * dt ; Y += VelY * dt
5. Va chạm sàn    ← nếu Y <= GROUND_Y thì đặt Y = GROUND_Y, VelY = 0, Grounded = true
6. Kẹp biên X     ← giữ nguyên WORLD_HALF_EXTENT của Phase 6, bỏ hẳn phần kẹp Y
```

Bẫy ở phép 3: phải kiểm `state.Grounded` — tức là **trạng thái đầu tick, trước khi tích phân**. Kiểm
sau phép 5 thì nhân vật vừa bật lên đã bị chính phép 5 của tick này kéo về đất.

Cuối cùng thêm một hàm dựng trạng thái ban đầu: `MoveState.AtRest(float x, float y)` — dùng ở chỗ
spawn. Cho `Grounded = false` để tick đầu tiên tự rơi và tự phát hiện sàn, thay vì tin vào toạ độ
lấy từ DB.

**Sửa `Server/Shared/Dto/World/MoveDto.cs`** — `MoveInputRequest` bỏ `DirY`, thêm `Jump`;
`MoveStateResponse` thêm `VelX`, `VelY`, `Grounded`. Đây là **breaking change của contract**: build
`Shared` xong là code cả hai bên đỏ lòm. Đó là tin tốt — trình biên dịch đang chỉ đúng những chỗ cần
sửa. Nếu trước đó đã chép tay DTO sang Unity như anti-pattern trong `CLAUDE.md` thì lúc này sẽ **không
có lỗi biên dịch nào cả**, và bug chỉ lộ ra lúc chạy dưới dạng nhân vật đứng im không hiểu vì sao.

`EntityState` (gói snapshot gửi cho người khác) **không đổi ở phase này**: bộ nội suy vị trí viết ở
Phase 7 vẽ được đường cong nhảy mà không cần biết vận tốc. Phase 9 mới cần thêm trạng thái — và đó
là lý do nó được tách thành một phase riêng.

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`Server/Shared/World/MoveState.cs`** (file mới):

```csharp
namespace MMORPG.Shared.World
{
    /// <summary>
    /// Ý định của người chơi tại một tick — đúng những gì bấm được trên bàn phím, không hơn.
    /// Cố tình không có trục Y: trong platformer người chơi không điều khiển chiều dọc,
    /// chiều dọc là hệ quả của trọng lực và của cú nhảy.
    /// </summary>
    public struct MoveIntent
    {
        /// <summary>Hướng ngang trong [-1, 1]. Người gọi chịu trách nhiệm kẹp; hàm Step không kiểm lại.</summary>
        public float DirX;

        /// <summary>
        /// CẠNH LÊN của nút nhảy: "tại tick này người chơi vừa bấm", không phải "nút đang bị giữ".
        /// Cạnh chứ không phải mức, vì ở 20 tick/s một cú bấm nhanh 30ms sẽ lọt trọn vào khe giữa
        /// hai lần đọc mức — người chơi bấm mà nhân vật không nhảy, không có lỗi nào để lần theo.
        /// </summary>
        public bool Jump;
    }

    /// <summary>
    /// Toàn bộ trạng thái vật lý của một entity — tập nhỏ nhất mà biết nó thì tính được tick kế tiếp.
    /// Vận tốc nằm ở đây chứ không suy ra từ vị trí: hai nhân vật cùng một điểm, một đang bay lên
    /// một đang rơi xuống, tick sau sẽ ở hai chỗ khác nhau.
    ///
    /// Là struct field công khai chứ không phải class có property: nó bị copy hàng chục lần mỗi giây
    /// trong vòng replay của reconciliation, và tính "gán là copy" của value type chính là thứ giữ
    /// cho replay không vô tình sửa trạng thái gốc.
    /// </summary>
    public struct MoveState
    {
        public float X;
        public float Y;
        public float VelX;
        public float VelY;

        /// <summary>Chân có đang chạm sàn ở CUỐI tick trước không. Điều kiện để được nhảy.</summary>
        public bool Grounded;

        /// <summary>
        /// Trạng thái lúc mới vào world. Grounded = false có chủ ý: để tick đầu tiên tự rơi và tự
        /// phát hiện sàn, thay vì tin rằng toạ độ lấy từ DB đang đứng đúng trên mặt đất.
        /// </summary>
        public static MoveState AtRest(float x, float y)
        {
            return new MoveState { X = x, Y = y, VelX = 0f, VelY = 0f, Grounded = false };
        }
    }
}
```

**`Server/Shared/World/MovementRules.cs`** (viết lại):

```csharp
using System;

namespace MMORPG.Shared.World
{
    /// <summary>
    /// Luật di chuyển dùng CHUNG: server mô phỏng thật, client dự đoán trước rồi replay.
    /// Hai bên phải ra cùng một kết quả từ cùng input — vì vậy luật chỉ tồn tại ở đây, một nơi.
    ///
    /// Mọi phép tính trong file này chỉ được dùng + - * / và so sánh trên float. Bốn phép đó được
    /// IEEE-754 quy định tới từng bit nên CoreCLR (server) và Mono/IL2CPP (client) buộc phải cho
    /// cùng kết quả. Gọi MathF.Sin/Pow/Exp là mở cửa cho sai số nền tảng, và sai số ấy cộng dồn
    /// suốt cú nhảy chứ không tự triệt tiêu.
    /// </summary>
    public static class MovementRules
    {
        public const int TICK_RATE = 20;
        public const float TICK_DT = 1f / TICK_RATE;

        /// <summary>Tốc độ chạy ngang, world unit/giây.</summary>
        public const float MOVE_SPEED = 5f;

        /// <summary>
        /// Gia tốc rơi, unit/giây². Lớn hơn 9.81 của đời thật rất nhiều — trọng lực "đúng vật lý"
        /// cho cảm giác lơ lửng như trên mặt trăng, không game platformer nào dùng.
        /// </summary>
        public const float GRAVITY = 30f;

        /// <summary>
        /// Vận tốc bật lên tức thời khi nhảy. Đỉnh nhảy = JUMP_SPEED² / (2·GRAVITY) ≈ 2 unit,
        /// thời gian bay ≈ 2·JUMP_SPEED/GRAVITY ≈ 0.73s ≈ 15 tick — đủ dài để nội suy phía
        /// người xem có mẫu mà vẽ đường cong.
        /// </summary>
        public const float JUMP_SPEED = 11f;

        /// <summary>
        /// Trần tốc độ rơi. Không có nó, rơi từ trên cao đủ lâu sẽ đi hơn một ô mỗi tick và
        /// XUYÊN QUA sàn giữa hai lần kiểm va chạm.
        /// </summary>
        public const float MAX_FALL_SPEED = 20f;

        /// <summary>Cao độ mặt sàn tạm — cả thế giới là một mặt phẳng. Map có hình dạng thật là Phase 10.</summary>
        public const float GROUND_Y = 0f;

        /// <summary>Nửa cạnh vùng đi lại theo trục ngang. Trục dọc không còn bị kẹp: đã có sàn và trọng lực.</summary>
        public const float WORLD_HALF_EXTENT = 20f;

        /// <summary>
        /// Một bước mô phỏng. Hàm THUẦN: không đọc thời gian, không random, không đọc biến ngoài —
        /// cùng (state, intent, dt) luôn cho cùng kết quả, ở cả hai đầu dây.
        ///
        /// THỨ TỰ các phép dưới đây là một phần của contract. Đổi thứ tự là đổi kết quả, và vì
        /// hai bên chạy cùng file nên nó sẽ không lệch ngay — nó lệch vào ngày ai đó sửa một bên.
        /// </summary>
        public static MoveState Step(MoveState state, MoveIntent intent, float dt)
        {
            // 1. Vận tốc ngang đặt thẳng từ input: thả phím là dừng ngay, không trượt.
            //    Chưa có gia tốc/ma sát — thêm được, nhưng phase này chỉ dạy một thứ.
            state.VelX = intent.DirX * MOVE_SPEED;

            // 2. Trọng lực. Kẹp trần rơi TRƯỚC khi tích phân, không phải sau.
            state.VelY -= GRAVITY * dt;
            if (state.VelY < -MAX_FALL_SPEED)
                state.VelY = -MAX_FALL_SPEED;

            // 3. Nhảy — ghi đè hẳn VelY vừa tính ở bước 2.
            //    Điều kiện đọc state.Grounded, tức trạng thái ĐẦU tick. Đọc sau bước 5 thì cú nhảy
            //    vừa bật lên sẽ bị chính phép chạm sàn của tick này kéo về mặt đất.
            if (intent.Jump && state.Grounded)
                state.VelY = JUMP_SPEED;

            // 4. Tích phân Euler tường minh: vận tốc mới nhân dt. Đơn giản, và quan trọng hơn là
            //    dễ viết giống hệt nhau ở hai bên — thứ mà Verlet hay RK4 không cho miễn phí.
            state.X += state.VelX * dt;
            state.Y += state.VelY * dt;

            // 5. Va chạm với sàn phẳng. Đặt lại VelY = 0 chứ không giữ nguyên: giữ nguyên thì
            //    vận tốc rơi cộng dồn mãi trong lúc đứng yên, và tick nào rời sàn cũng lao xuống.
            if (state.Y <= GROUND_Y)
            {
                state.Y = GROUND_Y;
                state.VelY = 0f;
                state.Grounded = true;
            }
            else
            {
                state.Grounded = false;
            }

            // 6. Biên ngang tạm, chờ map thật ở Phase 10.
            state.X = Math.Clamp(state.X, -WORLD_HALF_EXTENT, WORLD_HALF_EXTENT);

            return state;
        }
    }
}
```

**`Server/Shared/Dto/World/MoveDto.cs`**:

```csharp
using MemoryPack;

namespace MMORPG.Shared.Dto.World
{
    /// <summary>
    /// Ý định của client tại một bước dự đoán. Cố tình KHÔNG có trường thời gian:
    /// server tích phân bằng TICK_DT của chính nó — dt mà đi trên gói tin thì dt là chỗ hack tốc độ.
    /// </summary>
    [MemoryPackable]
    public partial class MoveInputRequest
    {
        /// <summary>Số thứ tự client tự đánh, tăng dần. Server echo lại để client biết mình đã được xử tới đâu.</summary>
        public int Seq { get; set; }

        /// <summary>Hướng ngang [-1, 1]. Không còn DirY: chiều dọc do trọng lực và cú nhảy quyết định.</summary>
        public float DirX { get; set; }

        /// <summary>Có yêu cầu nhảy tại tick này không (cạnh lên, không phải nút đang giữ).</summary>
        public bool Jump { get; set; }
    }

    /// <summary>
    /// Trạng thái authoritative của chính người nhận, gửi mỗi tick. Phải mang ĐỦ trạng thái vật lý:
    /// client replay các input còn treo từ đây, mà replay từ vị trí không thôi thì thiếu vận tốc —
    /// nó sẽ tính ra một cú nhảy khác hẳn.
    /// </summary>
    [MemoryPackable]
    public partial class MoveStateResponse
    {
        /// <summary>Input cuối cùng server đã nhận trước tick này. Client xoá pending ≤ số này rồi replay phần còn lại.</summary>
        public int LastInputSeq { get; set; }

        public float X { get; set; }
        public float Y { get; set; }
        public float VelX { get; set; }
        public float VelY { get; set; }
        public bool Grounded { get; set; }
    }
}
```

</details>

### ✅ CHECKPOINT A — build sạch, và hiểu vì sao nó đỏ

Sau bước này **chưa chạy được gì**, và đó là điều đúng. Việc cần làm:

1. `dotnet build Server/Shared` — phải sạch, và DLL phải tự copy sang `Assets/Plugins/Shared/`.
2. `dotnet build Server/GameServer` — **phải đỏ**, ở `PlayerEntity.Integrate` và `MoveHandler`.
3. Unity console — **phải đỏ**, ở `PlayerMotor`.

Đọc lướt danh sách lỗi trước khi sửa. Đó là bản đồ chính xác của "đổi hành vi trong `Shared` thì
lan tới đâu", và nó ngắn hơn bạn tưởng — đúng ba file. Nếu nó dài hơn ba file, nghĩa là logic di
chuyển đã rò rỉ ra ngoài `Shared` ở đâu đó và đáng đi tìm.

---

## Bước 2 — Server: entity mang trạng thái, và cái bẫy chốt nút nhảy

### Hướng làm

**`Server/GameServer/World/PlayerEntity.cs`** — ba thay đổi:

1. Thay `public float X { get; set; }` / `Y` bằng **một** field `MoveState State`, và để lại
   `public float X => State.X;` / `Y` dạng property chỉ-đọc. Cả `WorldService` lẫn `CharacterService`
   đang đọc `entity.X` ở 6 chỗ; giữ hai property này thì chúng không phải sửa dòng nào — và quan
   trọng hơn là **không ai bên ngoài đặt được vị trí nữa**, vị trí chỉ đổi qua `Integrate`.
2. `_inputDirY` biến mất. Thay bằng `_pendingJump`.
3. `Integrate` gọi `MovementRules.Step` với `MoveState` và `MoveIntent` dựng từ input đã lưu.

**Cái bẫy lớn nhất của phase nằm ở `SetInput`.** Hiện tại nó ghi đè:

```csharp
_inputDirX = dirX;   // trục giữ — ghi đè là ĐÚNG
_pendingJump = jump; // cạnh — ghi đè là SAI
```

Vì sao sai: client gửi 20 gói/giây, server tick 20 lần/giây, nhưng **hai nhịp đó không khớp nhau**.
Mạng dồn gói, `Task.Delay(1)` trên Windows dậy trễ ~15ms — chuyện hai gói input tới giữa hai tick là
bình thường. Khi đó:

```
gói #41 { DirX: 1, Jump: true  }   ← người chơi bấm Space
gói #42 { DirX: 1, Jump: false }   ← frame sau, không bấm nữa
                                   ← tick mới chạy tới đây, đọc _pendingJump = false
```

Cú nhảy bốc hơi. Không exception, không log, chỉ là thỉnh thoảng bấm Space mà không nhảy — loại bug
tốn cả buổi tối vì nó không tái hiện đều.

Cách chữa: **chốt bằng OR, xoá lúc tiêu thụ.**

```csharp
_pendingJump |= jump;        // trong SetInput  — gộp, không ghi đè
...
bool jump = _pendingJump;    // trong Integrate — đọc rồi xoá
_pendingJump = false;
```

Nguyên tắc chung đáng ghi nhớ: **trục giữ thì lấy giá trị mới nhất, nút cạnh thì gộp lại tới khi tiêu
thụ.** Mọi input rời rạc sau này (đánh, dùng skill, nhặt đồ) đều theo luật thứ hai.

Bộ đếm `_ticksSinceInput` của Phase 6 (client rớt giữa lúc giữ phím thì entity không chạy mãi) giữ
nguyên, chỉ bỏ dòng đặt `_inputDirY = 0`.

**`Server/GameServer/Handlers/MoveHandler.cs`** — đơn giản đi hẳn:

- Kiểm `float.IsFinite(dirX)` vẫn giữ nguyên, vẫn cần: NaN lây qua mọi phép toán, lọt một lần là
  `X`/`Y` thành NaN vĩnh viễn và theo `SavePosition` vào tận DB.
- Đoạn chuẩn hoá vector bằng `MathF.Sqrt` **xoá đi** — `DirX` giờ là số vô hướng, chống hack tốc độ
  chỉ còn là một phép kẹp: `Math.Clamp(dirX, -1f, 1f)`. Đơn giản hơn *và* an toàn hơn.
- `bool` thì không kiểm gì: `Jump` chỉ có hai giá trị, không gian tấn công bằng không. Kẻ gian lận
  gửi `Jump = true` mỗi tick cũng không được gì — điều kiện `state.Grounded` trong `Step` là thứ
  chặn, và nó chạy ở server.

**`Server/GameServer/World/WorldService.cs`** — chỉ một chỗ: `MoveStateResponse` phải điền thêm
`VelX`, `VelY`, `Grounded` từ `entity.State`. Còn `Spawn` phải khởi tạo `State` từ toạ độ DB —
việc này nằm trong constructor của `PlayerEntity`.

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`Server/GameServer/World/PlayerEntity.cs`** (phần thay đổi):

```csharp
public sealed class PlayerEntity
{
    // ... EntityId, CharacterId, AccountId, Name, ClassId, Level giữ nguyên ...

    public int MapId { get; set; }

    /// <summary>
    /// Trạng thái vật lý authoritative. Chỉ Integrate được sửa — bên ngoài đọc qua X/Y.
    /// Đây là nơi duy nhất trong toàn hệ thống mà vị trí một người chơi là SỰ THẬT.
    /// </summary>
    public MoveState State;

    /// <summary>Vị trí đọc-ra-ngoài (log, snapshot, lưu DB). Không có setter: vị trí chỉ đổi qua Integrate.</summary>
    public float X => State.X;
    public float Y => State.Y;

    public ClientSession Owner { get; private set; }
    public int LastInputSeq { get; private set; }

    // Trục giữ: handler ghi, tick đọc. Hai luồng khác nhau nhưng không cần lock —
    // mỗi field là một phép ghi nguyên tử, tệ nhất tick này dùng input trễ một nhịp.
    private float _intentDirX;

    // Nút dạng CẠNH: phải chốt lại bằng OR chứ không ghi đè. Client gửi 20 gói/s và server tick
    // 20 lần/s, nhưng hai nhịp không khớp: hai gói tới giữa hai tick là chuyện thường. Ghi đè thì
    // gói { Jump: false } ngay sau gói { Jump: true } sẽ nuốt mất cú nhảy — không lỗi, không log,
    // chỉ là "thỉnh thoảng bấm Space mà không nhảy".
    private bool _pendingJump;

    private int _ticksSinceInput;

    public PlayerEntity(int entityId, CharacterRow row, ClientSession owner)
    {
        // ... gán các field định danh như cũ ...
        MapId = row.MapId;
        State = MoveState.AtRest(row.X, row.Y);
        Owner = owner;
    }

    public void SetInput(int seq, float dirX, bool jump)
    {
        LastInputSeq = seq;
        _intentDirX = dirX;

        // |= chứ không =. Xem comment ở khai báo _pendingJump.
        _pendingJump |= jump;

        _ticksSinceInput = 0;
    }

    public void Integrate(float dt)
    {
        // Quá 1 giây không có input mới → coi như đã thả phím. Trạng thái cũ không được sống mãi.
        // Chỉ xoá hướng chạy: cú nhảy đã chốt vẫn phải được tiêu thụ, mất mạng không phải là lý do
        // để nuốt một input người chơi đã bấm thật.
        if (++_ticksSinceInput > MovementRules.TICK_RATE)
            _intentDirX = 0f;

        // Đọc-rồi-xoá: một lần bấm nhảy chỉ được dùng đúng một tick. Không xoá thì nhân vật
        // nhảy lại mỗi lần chạm đất, mãi mãi, cho tới gói input kế tiếp.
        var intent = new MoveIntent { DirX = _intentDirX, Jump = _pendingJump };
        _pendingJump = false;

        State = MovementRules.Step(State, intent, dt);
    }
}
```

**`Server/GameServer/Handlers/MoveHandler.cs`**:

```csharp
[TcpHandler(NetCmd.MoveInput, MinState = SessionState.InWorld)]
public static Task<NetResult> OnMoveInput(NetRequest req)
{
    var input = req.GetData<MoveInputRequest>();
    PlayerEntity entity = req.Session.Entity;

    // MinState đã chặn phần lớn, nhưng LeaveWorld có thể xảy ra giữa lúc gói đang bay.
    if (entity == null)
        return Task.FromResult(NetResult.None);

    // NaN lây qua MỌI phép toán: lọt một lần là X/Y thành NaN vĩnh viễn và theo SavePosition
    // vào tận DB. Chặn ngay cửa. Lưu ý NaN < -1 và NaN > 1 đều FALSE nên Clamp dưới đây
    // không bắt được nó — thứ tự hai phép này không đảo được.
    if (!float.IsFinite(input.DirX))
        return Task.FromResult(NetResult.None);

    // Chống hack tốc độ: DirX = 10 là chạy nhanh gấp 10. Giờ DirX là số vô hướng nên chỉ cần
    // kẹp, không phải chuẩn hoá vector như hồi còn hai trục.
    float dirX = Math.Clamp(input.DirX, -1f, 1f);

    // Jump không cần kiểm: bool chỉ có hai giá trị. Gửi Jump = true mỗi tick cũng vô ích —
    // điều kiện Grounded nằm trong MovementRules.Step và Step chạy ở đây, không ở máy họ.
    entity.SetInput(input.Seq, dirX, input.Jump);

    // Fire-and-forget: không trả lời từng input. Câu trả lời gộp là MoveState mỗi tick.
    return Task.FromResult(NetResult.None);
}
```

**`Server/GameServer/World/WorldService.cs`** — trong `Tick`, phần gửi `MoveState`:

```csharp
entity.Owner.SendData(NetCmd.MoveState, new MoveStateResponse
    {
        LastInputSeq = entity.LastInputSeq,
        X = entity.State.X,
        Y = entity.State.Y,
        VelX = entity.State.VelX,
        VelY = entity.State.VelY,
        Grounded = entity.State.Grounded,
    }
);
```

</details>

### ✅ CHECKPOINT B — server đã có trọng lực, nhìn bằng log

Client chưa sửa nên chưa chạy được — nhưng server thì kiểm được ngay, và kiểm bằng log rẻ hơn nhiều
so với debug qua Unity.

Thêm tạm vào `WorldService.Tick` (nhớ **xoá sau khi xong checkpoint** — 20 dòng/giây là dìm chết console):

```csharp
if (entity.LastInputSeq % 10 == 0)
    Log.Info($"{entity.Name} y={entity.State.Y:0.00} velY={entity.State.VelY:0.00} grounded={entity.State.Grounded}");
```

Chạy `GameServer`, đăng nhập bằng client **cũ** (chưa sửa, nó vẫn gửi `MoveInput` — chỉ là DTO đã đổi
nên `Jump` luôn `false` và `DirX` có thể sai; không sao, ta chỉ xem trọng lực).

Phải thấy: nếu toạ độ trong DB có `y > 0` thì `y` giảm dần theo cấp số cộng của `velY`, `velY` âm dần
tới `-20` rồi dừng, cuối cùng `y=0.00 velY=0.00 grounded=True` và **đứng yên mãi ở đó**.

Nếu `y` tụt xuống âm vô hạn: phép 5 (va chạm sàn) sai hoặc thiếu.
Nếu `grounded` nhấp nháy True/False liên tục lúc đứng yên: bạn đặt `VelY = 0` sau khi kiểm `Y <= GROUND_Y`
thay vì bên trong nhánh đó, hoặc quên gán `Y = GROUND_Y` nên nó cứ dao động quanh mốc 0.

---

## Bước 3 — Client: dự đoán và đối chiếu **một trạng thái**, không phải một điểm

### Hướng làm

**`Assets/Game/Scripts/World/WorldApi.cs`** — đổi chữ ký `Move(int seq, float dirX, bool jump)`.

**`Assets/Game/Scripts/World/PlayerMotor.cs`** — bốn thay đổi, xếp theo độ khó tăng dần:

**(a) Trạng thái mô phỏng.** `private Vector2 _simPos;` thành `private MoveState _simState;`. Chỗ hiển
thị đọc `_simState.X` / `.Y`. `Init` khởi tạo bằng `MoveState.AtRest(spawn.x, spawn.y)`.

**(b) Pending input đổi kiểu.** `PendingInput` giờ giữ `MoveIntent` thay vì `Vector2 Dir`. Đây không
chỉ là đổi tên — nó là điểm mấu chốt: **replay phải tái hiện đúng cả cú nhảy**. Nếu chỉ lưu `DirX`
mà quên `Jump`, thì mỗi lần server trả về gói `MoveState`, vòng replay sẽ tính lại quỹ đạo *không có
cú nhảy* → nhân vật bị kéo tụt xuống → nhảy lên là giật. Đây là biểu hiện kinh điển của "state phức
tạp hơn một vector".

**(c) Đọc input.** Trục ngang lấy từ `Player.Move.ReadValue<Vector2>().x`, kẹp `[-1, 1]` (analog stick
cho giá trị lẻ, và ta phải kẹp giống hệt server — bên nào kẹp khác là bên đó lệch). Trục Y của
`Move` **bỏ hẳn**: bấm W không còn nghĩa gì.

Nút nhảy là chỗ đối xứng với cái bẫy ở Bước 2, nhưng ở phía ngược lại:

```
Update() chạy 60–300 lần/giây     ←── WasPressedThisFrame() chỉ true đúng 1 frame
Step()   chạy 20 lần/giây         ←── và nó có thể không rơi vào đúng frame đó
```

Đọc `WasPressedThisFrame()` bên trong vòng `while (_accumulator >= TICK_DT)` là **bảo đảm mất phần
lớn các cú bấm**. Phải chốt ở tầng `Update`, tiêu thụ ở tầng `Step`:

```csharp
private void Update()
{
    if (_inputActions.Player.Jump.WasPressedThisFrame())
        _jumpLatched = true;      // chốt — giữ tới khi có tick tới lấy

    // ... vòng accumulator ...
}

private void Step(float dirX)
{
    var intent = new MoveIntent { DirX = dirX, Jump = _jumpLatched };
    _jumpLatched = false;         // tiêu thụ
    ...
}
```

Ba tầng nhịp khác nhau (frame → tick client → tick server) và cùng một cú bấm phải sống sót qua cả
ba. Đó là toàn bộ lý do phase này khó hơn vẻ ngoài của nó.

**(d) Reconciliation.** `OnMoveStateResult` dựng lại `MoveState` **đầy đủ** từ gói server rồi replay:

```csharp
var state = new MoveState {
    X = r.X, Y = r.Y, VelX = r.VelX, VelY = r.VelY, Grounded = r.Grounded
};
foreach (PendingInput p in _pending)
    state = MovementRules.Step(state, p.Intent, MovementRules.TICK_DT);
_simState = state;
```

Ngắn hơn bản cũ, vì `Step` giờ nhận và trả đúng một thứ. **Không** được chỉ lấy `X`/`Y` từ gói rồi
giữ `VelY` cũ của mình: vận tốc server là một phần của sự thật, và nó chính là thứ quyết định 15 tick
tiếp theo của cú nhảy.

Phần hiển thị `Vector3.MoveTowards` giữ nguyên ý tưởng nhưng tốc độ đuổi phải tính lại: nhân vật rơi
nhanh tới `MAX_FALL_SPEED = 20`, mà đang đuổi theo `MOVE_SPEED * 1.5f = 7.5`. Hiển thị sẽ tụt lại rất
xa mỗi lần rơi. Dùng `MAX_FALL_SPEED * 1.5f` — hoặc gọn hơn, đuổi theo khoảng cách hiện tại thay vì
một hằng cố định.

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`Assets/Game/Scripts/World/WorldApi.cs`**:

```csharp
public void Move(int seq, float dirX, bool jump)
{
    // Không log ở đây — 20 lần/giây, log là dìm chết console.
    _netService.Send(NetCmd.MoveInput, new MoveInputRequest { Seq = seq, DirX = dirX, Jump = jump });
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
    /// Di chuyển nhân vật của chính mình: đọc phím, dự đoán tại chỗ bằng luật chung,
    /// gửi ý định lên server, và đối chiếu lại khi server trả trạng thái authoritative.
    /// </summary>
    public sealed class PlayerMotor : MonoBehaviour
    {
        private InputSystem_Actions _inputActions;

        /// <summary>Một bước dự đoán chưa được server xác nhận — nguyên liệu để replay.</summary>
        private readonly struct PendingInput
        {
            public readonly int Seq;

            /// <summary>
            /// Giữ nguyên cả MoveIntent chứ không chỉ hướng chạy. Thiếu cờ Jump ở đây thì mỗi lần
            /// reconciliation replay lại, quỹ đạo tính ra là quỹ đạo KHÔNG có cú nhảy — nhân vật
            /// bị kéo tụt về mặt đất đúng lúc đang bay lên.
            /// </summary>
            public readonly MoveIntent Intent;

            public PendingInput(int seq, MoveIntent intent)
            {
                Seq = seq;
                Intent = intent;
            }
        }

        private WorldApi _worldApi;
        private WorldNetHandler _worldNetHandler;

        private readonly List<PendingInput> _pending = new();
        private int _nextSeq;
        private float _accumulator;

        /// <summary>
        /// Cú bấm nhảy đang chờ tick tới tiêu thụ. Cần vì Update chạy 60–300Hz còn Step chỉ chạy
        /// 20Hz: đọc WasPressedThisFrame bên trong vòng tick là bỏ lỡ phần lớn các cú bấm.
        /// </summary>
        private bool _jumpLatched;

        // Trạng thái MÔ PHỎNG (nhảy bậc 20Hz) tách khỏi vị trí HIỂN THỊ (transform, mượt theo frame).
        private MoveState _simState;

        private void Awake()
        {
            _inputActions = new InputSystem_Actions();
            _inputActions.Player.Enable();
        }

        public void Init(WorldApi worldApi, WorldNetHandler worldNetHandler, Vector2 spawnPos)
        {
            _worldApi = worldApi;
            _worldNetHandler = worldNetHandler;
            _simState = MoveState.AtRest(spawnPos.x, spawnPos.y);

            _worldNetHandler.OnMoveStateResult += OnMoveStateResult;
        }

        private void OnDestroy()
        {
            // Wrapper giữ một InputActionAsset runtime — không Dispose thì asset và các
            // callback của nó sống sót qua cả lần đổi scene.
            _inputActions?.Dispose();

            if (_worldNetHandler != null)
                _worldNetHandler.OnMoveStateResult -= OnMoveStateResult;
        }

        private void Update()
        {
            if (_worldApi == null)
                return;

            // Chốt cú bấm ngay tại frame nó xảy ra. Xem comment ở khai báo _jumpLatched.
            if (_inputActions.Player.Jump.WasPressedThisFrame())
                _jumpLatched = true;

            // Chỉ còn trục ngang. Kẹp [-1,1] giống hệt server: analog stick cho giá trị lẻ,
            // và bên nào kẹp khác bên kia là bên đó dự đoán lệch.
            float dirX = Mathf.Clamp(_inputActions.Player.Move.ReadValue<Vector2>().x, -1f, 1f);

            // Vòng accumulator y hệt game loop server: dự đoán theo bậc TICK_DT cố định,
            // không theo frame — frame rate không được ảnh hưởng tốc độ chạy hay độ cao nhảy.
            _accumulator += Time.deltaTime;
            while (_accumulator >= MovementRules.TICK_DT)
            {
                _accumulator -= MovementRules.TICK_DT;
                Step(dirX);
            }

            // Hiển thị đuổi theo mô phỏng. Mốc là MAX_FALL_SPEED chứ không phải MOVE_SPEED:
            // lúc rơi, mô phỏng đi nhanh gấp 4 lần lúc chạy, đuổi bằng tốc độ chạy là tụt lại thấy rõ.
            transform.position = Vector3.MoveTowards(
                transform.position, new Vector3(_simState.X, _simState.Y, 0f),
                MovementRules.MAX_FALL_SPEED * 1.5f * Time.deltaTime
            );
        }

        /// <summary>Một bước dự đoán: mô phỏng trước, ghi nợ, gửi lên server. Gửi CẢ khi đứng yên — thả phím cũng là input.</summary>
        private void Step(float dirX)
        {
            int seq = ++_nextSeq;

            var intent = new MoveIntent { DirX = dirX, Jump = _jumpLatched };

            // Tiêu thụ ngay: một lần bấm sinh đúng một MoveIntent có Jump = true.
            _jumpLatched = false;

            _simState = MovementRules.Step(_simState, intent, MovementRules.TICK_DT);

            _pending.Add(new PendingInput(seq, intent));
            _worldApi.Move(seq, intent.DirX, intent.Jump);
        }

        /// <summary>
        /// Đối chiếu với server: trạng thái server + replay các input server chưa xử = trạng thái "đáng lẽ".
        /// Dự đoán đúng thì kết quả trùng cái đang có; sai thì bị kéo về — đó là cú giật rubber-band.
        /// </summary>
        private void OnMoveStateResult(MoveStateResponse response)
        {
            _pending.RemoveAll(p => p.Seq <= response.LastInputSeq);

            // Lấy TRỌN trạng thái server, không chỉ vị trí. VelY là thứ quyết định 15 tick tiếp theo
            // của cú nhảy: giữ VelY cũ của mình mà chỉ nhận X/Y của server là trộn hai sự thật.
            var state = new MoveState
            {
                X = response.X,
                Y = response.Y,
                VelX = response.VelX,
                VelY = response.VelY,
                Grounded = response.Grounded,
            };

            foreach (PendingInput pending in _pending)
            {
                state = MovementRules.Step(state, pending.Intent, MovementRules.TICK_DT);
            }

            _simState = state;
        }
    }
}
```

</details>

### ✅ CHECKPOINT C — nhảy được, và người kia thấy

1. Một client: nhân vật rơi xuống y = 0, chạy trái/phải bằng A/D, bấm Space thì bay lên ~2 unit rồi rơi
   xuống. Giữ Space không nhảy liên tục; thả ra bấm lại mới nhảy tiếp.
2. Bấm Space giữa không trung → **không có gì xảy ra**. Đây là điều kiện `Grounded` đang chạy ở server.
3. Hai client (ParrelSync): client A nhảy, client B thấy A bay lên rồi rơi xuống theo **đường cong**,
   không phải dịch chuyển tức thời. Bộ nội suy Phase 7 không phải sửa dòng nào — nó nội suy *vị trí*,
   không quan tâm vị trí ấy sinh ra từ luật nào.
4. Nhảy liên tục 20 lần: **không** có cú nào bị nuốt.

Nếu (4) hỏng — thỉnh thoảng bấm mà không nhảy — thì một trong hai chỗ chốt đang ghi đè thay vì gộp.
Đọc lại Bước 2 (`_pendingJump |=`) và Bước 3 (`_jumpLatched` chốt ở `Update`).

---

## Bước 4 — Nhảy cho "đã tay": coyote time + jump buffer

### Hướng làm

Motor ở CHECKPOINT C **đúng** nhưng **khó chịu**. Hai tình huống ai chơi cũng gặp:

| Người chơi làm gì | Cảm giác | Vì sao |
|---|---|---|
| Chạy tới mép sàn, bấm Space **ngay sau** khi rời mép | "Game ăn gian, tôi bấm rồi mà" | Lúc bấm, `Grounded` đã `false` được 1–2 tick |
| Đang rơi xuống, bấm Space **ngay trước** khi chạm đất | "Sao không nhảy tiếp?" | Lúc bấm, `Grounded` còn `false`; lúc `true` thì cú bấm đã bị tiêu thụ và vứt |

Hai vá kinh điển của thể loại platformer, và cả hai là **cùng một ý tưởng**: nới lỏng điều kiện theo
thời gian, ở hai đầu.

- **Coyote time** — vẫn cho nhảy trong N tick *sau khi* rời đất. (Tên lấy từ con sói trong Looney Tunes
  chạy khỏi vách đá mà chưa rơi ngay.)
- **Jump buffer** — nhớ cú bấm trong N tick, chạm đất là dùng ngay.

Cả hai là **quy tắc chơi**, nên theo golden rule #2 chúng phải ở server. Và vì client dự đoán, chúng
phải ở `Shared`. Và vì replay phải tái hiện được, **hai bộ đếm của chúng là một phần của `MoveState`** —
tức là phải đi trên dây trong `MoveStateResponse`.

Đây là bài học đắt nhất của phase, và là lý do nó xứng đáng một bước riêng: **thêm một tính năng "chỉ
là cảm giác" cũng làm phình contract mạng.** Không có cách nào để coyote time nằm riêng ở client — làm
vậy là client cho nhảy trong lúc server từ chối, và kết quả là rubber-band đúng ở khoảnh khắc người
chơi để ý nhất.

**Thêm vào `MoveState`:**

```csharp
public int TicksSinceGrounded;    // 0 = đang đứng đất
public int TicksSinceJumpRequest; // 0 = vừa bấm ở tick này
```

**Thêm hằng vào `MovementRules`:**

```csharp
public const int COYOTE_TICKS = 3;       // 150ms — ~3 tick, đủ tha thứ mà chưa thành bay
public const int JUMP_BUFFER_TICKS = 3;  // 150ms
public const int EXPIRED = 999;          // giá trị "hết hạn", dùng để vô hiệu một bộ đếm
```

**Sửa `Step`** — thứ tự mới, phép 3 tách thành 3a/3b/3c:

```
1. Vận tốc ngang
2. Trọng lực
3a. Cập nhật bộ đếm:  TicksSinceGrounded++
                      TicksSinceJumpRequest = intent.Jump ? 0 : TicksSinceJumpRequest + 1
3b. Điều kiện nhảy:   TicksSinceJumpRequest <= JUMP_BUFFER_TICKS
                   && TicksSinceGrounded    <= COYOTE_TICKS
3c. Nếu nhảy:         VelY = JUMP_SPEED
                      cả HAI bộ đếm := EXPIRED
4. Tích phân
5. Va chạm sàn → nếu chạm thì TicksSinceGrounded = 0 (cùng chỗ đặt Grounded = true)
6. Kẹp biên X
```

Chú ý ba điểm dễ sai:

- Phép 3c phải vô hiệu **cả hai** bộ đếm. Chỉ xoá buffer thì coyote còn hiệu lực → nhảy tiếp được ở
  tick sau → nhảy đôi miễn phí. Chỉ xoá coyote thì buffer còn → chạm đất là tự nhảy lại.
- Điều kiện `Grounded` cũ **biến mất hoàn toàn** khỏi phép nhảy. `TicksSinceGrounded <= COYOTE_TICKS`
  đã bao gồm trường hợp đang đứng đất (khi đó nó bằng 0). Giữ cả hai điều kiện là vô hiệu hoá coyote.
- Bộ đếm không được `++` mãi tới tràn `int`. Kẹp chúng ở `EXPIRED` mỗi lần tăng — hoặc chấp nhận
  rằng phải đứng yên 3.4 tỉ tick (hơn 5 năm) mới tràn, và ghi comment nói rõ là đã cân nhắc. Kẹp
  cho sạch, một dòng.

**`MoveStateResponse`** thêm hai `int`. **`PlayerMotor.OnMoveStateResult`** chép thêm hai `int`.
Không có gì khác phải sửa — và đó là phần thưởng của việc đã gom toàn bộ trạng thái vào một struct
ở Bước 1.

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`MoveState`** — thêm hai field và sửa `AtRest`:

```csharp
/// <summary>
/// Số tick từ lần cuối chạm đất. 0 = đang đứng đất. Cho phép "coyote time": vẫn nhảy được vài
/// tick sau khi đã rời mép sàn — người chơi luôn cảm thấy mình bấm kịp, và với họ thì họ đúng.
/// </summary>
public int TicksSinceGrounded;

/// <summary>
/// Số tick từ lần cuối bấm nhảy. Cho phép "jump buffer": bấm sớm ngay trước lúc tiếp đất thì
/// cú bấm được giữ lại, chạm đất là bật lên ngay thay vì rơi vào hư không.
/// </summary>
public int TicksSinceJumpRequest;

public static MoveState AtRest(float x, float y)
{
    return new MoveState
    {
        X = x, Y = y, VelX = 0f, VelY = 0f,
        Grounded = false,
        // Bắt đầu ở trạng thái hết hạn: vừa vào world thì chưa có tư cách nhảy nào cả.
        TicksSinceGrounded = MovementRules.EXPIRED,
        TicksSinceJumpRequest = MovementRules.EXPIRED,
    };
}
```

**`MovementRules`** — hằng mới và `Step` viết lại phép 3:

```csharp
/// <summary>
/// Số tick còn được nhảy sau khi đã rời mép sàn (coyote time). 3 tick = 150ms: đủ để tha thứ
/// cho phản xạ người, chưa đủ để thành "nhảy giữa không trung".
/// </summary>
public const int COYOTE_TICKS = 3;

/// <summary>Số tick một cú bấm nhảy còn được giữ lại chờ tiếp đất (jump buffer).</summary>
public const int JUMP_BUFFER_TICKS = 3;

/// <summary>
/// Giá trị "hết hạn" cho hai bộ đếm trên — lớn hơn mọi ngưỡng nên điều kiện nhảy luôn sai.
/// Cũng là trần kẹp để bộ đếm không tăng tới tràn int khi người chơi đứng yên lâu.
/// </summary>
public const int EXPIRED = 999;

public static MoveState Step(MoveState state, MoveIntent intent, float dt)
{
    // 1. Vận tốc ngang.
    state.VelX = intent.DirX * MOVE_SPEED;

    // 2. Trọng lực.
    state.VelY -= GRAVITY * dt;
    if (state.VelY < -MAX_FALL_SPEED)
        state.VelY = -MAX_FALL_SPEED;

    // 3a. Hai bộ đếm tha thứ. Kẹp ở EXPIRED để không tăng tới tràn int.
    if (state.TicksSinceGrounded < EXPIRED)
        state.TicksSinceGrounded++;

    if (intent.Jump)
        state.TicksSinceJumpRequest = 0;
    else if (state.TicksSinceJumpRequest < EXPIRED)
        state.TicksSinceJumpRequest++;

    // 3b. Điều kiện nhảy. Lưu ý KHÔNG còn kiểm state.Grounded: đang đứng đất nghĩa là
    //     TicksSinceGrounded == 0, đã nằm trong ngưỡng coyote rồi. Kiểm thêm Grounded
    //     là vô hiệu hoá đúng cái tính năng vừa thêm.
    if (state.TicksSinceJumpRequest <= JUMP_BUFFER_TICKS &&
        state.TicksSinceGrounded <= COYOTE_TICKS)
    {
        // 3c. Bật lên, và tiêu huỷ CẢ HAI tư cách. Chỉ xoá buffer thì coyote còn hiệu lực ở
        //     tick sau → nhảy đôi miễn phí. Chỉ xoá coyote thì buffer còn → vừa chạm đất là
        //     tự nhảy lại, mãi mãi.
        state.VelY = JUMP_SPEED;
        state.TicksSinceJumpRequest = EXPIRED;
        state.TicksSinceGrounded = EXPIRED;
    }

    // 4. Tích phân.
    state.X += state.VelX * dt;
    state.Y += state.VelY * dt;

    // 5. Va chạm sàn.
    if (state.Y <= GROUND_Y)
    {
        state.Y = GROUND_Y;
        state.VelY = 0f;
        state.Grounded = true;
        state.TicksSinceGrounded = 0;
    }
    else
    {
        state.Grounded = false;
    }

    // 6. Biên ngang tạm, chờ map thật ở Phase 10.
    state.X = Math.Clamp(state.X, -WORLD_HALF_EXTENT, WORLD_HALF_EXTENT);

    return state;
}
```

**`MoveStateResponse`** thêm hai trường, **`WorldService.Tick`** điền chúng, và
**`PlayerMotor.OnMoveStateResult`** chép chúng vào `MoveState` trước vòng replay:

```csharp
var state = new MoveState
{
    X = response.X,
    Y = response.Y,
    VelX = response.VelX,
    VelY = response.VelY,
    Grounded = response.Grounded,
    TicksSinceGrounded = response.TicksSinceGrounded,
    TicksSinceJumpRequest = response.TicksSinceJumpRequest,
};
```

</details>

---

## Bước 5 — Gắn sprite Dragon Warrior (bản tạm)

### Hướng làm

Bước này **không phải để đẹp**. Không nhìn thấy nhân vật thì không tune được `GRAVITY` và `JUMP_SPEED`:
đỉnh nhảy "2 unit" chỉ có nghĩa khi biết nhân vật cao mấy unit. Làm vừa đủ để nhìn, rồi dừng —
state machine đầy đủ (13 nhóm trạng thái, cả tầng do server quyết) là **Phase 9**.

**Import sprite.** `Assets/Game/Textures/Dragon Warrior Files/Dragon Warrior PNG/` — chọn cả nhóm
`idle_*`, `walk_*`, `jump_*` rồi đặt một lần trong Inspector:

| Thuộc tính | Giá trị | Vì sao |
|---|---|---|
| Texture Type | Sprite (2D and UI) | |
| Pixels Per Unit | **thử 32, chỉnh sau** | Đây là con số nối *pixel* với *world unit*. Nhân vật cao ~64px thì PPU 32 cho nhân vật cao 2 unit — vừa đúng bằng đỉnh nhảy, nhìn ra ngay là cao hay thấp |
| Filter Mode | **Point (no filter)** | Pixel art mà để Bilinear là nhoè. Đây là lỗi số 1 khi import pixel art |
| Compression | None | Ảnh nhỏ, nén chỉ tổ tạo artefact ở viền |
| Pivot | Bottom | Gốc ở chân nhân vật → `transform.position.y` chính là cao độ chân, khớp thẳng với `GROUND_Y` mà không phải bù trừ |

Pivot Bottom là quyết định đáng để ý: nó làm cho `MoveState.Y` và vị trí hiển thị nói cùng một ngôn
ngữ. Để Center thì nhân vật lún nửa người xuống sàn và bạn sẽ đi thêm một hằng số bù vào code hiển thị —
một hằng số không có ở server, tức là một chỗ hai bên bắt đầu khác nhau.

**Animation.** Ba clip bằng công cụ sẵn có của Unity (kéo dãy sprite vào scene là Unity tự hỏi tạo clip):
`Idle` (6 frame, loop), `Walk` (loop), `Jump` (2 frame, **không** loop). Chưa cần Animator state machine
có transition — Phase 9 làm việc đó tử tế.

**Script tạm** — một `MonoBehaviour` ~20 dòng trên prefab nhân vật, đọc trạng thái từ `PlayerMotor`
qua một property mới `public MoveState SimState => _simState;`:

- `Grounded == false` → clip `Jump`
- `VelX` khác 0 → clip `Walk`
- còn lại → clip `Idle`
- lật mặt: `spriteRenderer.flipX = VelX < 0` (giữ nguyên khi `VelX == 0` — đứng yên thì nhìn về hướng cũ)

Đặt tên file là `PlayerAnimatorTemp.cs` và ghi rõ trong summary rằng nó là bản tạm. Đặt tên "tạm" ngay
từ đầu rẻ hơn nhiều so với việc ba tuần sau phải đoán xem file nào là thật.

Nhân vật của **người khác** (`_remotePrefab`) phase này cứ để nguyên hình cũ — nó chưa có `MoveState`
để đọc, và cấp cho nó một cái chính là nội dung Phase 9.

---

## Ba thử nghiệm bắt buộc

Làm đủ ba, mỗi cái đều dạy một thứ không đọc doc mà ra.

**1. Phá tính thuần của `Step` và xem nó gãy thế nào.**
Trong `MovementRules.Step`, đổi phép 2 thành `state.VelY -= GRAVITY * dt * 1.001f` — **chỉ ở phía
client** (sửa file trong `Assets/Plugins/Shared/` là không được, nên thay bằng: tạm hardcode
`GRAVITY = 30.03f` trong một bản copy `Step` riêng mà `PlayerMotor` gọi). Nhảy vài cái.
Bạn thấy gì? Sai lệch 0.1% mỗi tick nghe như không đáng kể — nhưng nó cộng dồn suốt 15 tick của cú
nhảy, và mỗi gói `MoveState` về lại kéo nhân vật giật một nhịp. Đây chính xác là thứ sẽ xảy ra nếu
dùng `Rigidbody2D` ở client. Trả code về như cũ.

**2. Bỏ `Jump` khỏi `PendingInput`.**
Sửa `PlayerMotor` để `PendingInput` chỉ giữ `DirX`, replay dựng `MoveIntent { DirX = ..., Jump = false }`.
Nhảy. Nhân vật bật lên rồi bị **giật ngược xuống** vài lần trong lúc bay — mỗi gói `MoveState` là một
lần replay tính ra "đáng lẽ không có cú nhảy nào". Đây là hình ảnh trực quan của câu "state phức tạp
hơn một vector": mất một `bool` trong lịch sử input là mất cả quỹ đạo.

**3. Đặt `COYOTE_TICKS = 0` và `JUMP_BUFFER_TICKS = 0`, chơi 2 phút, rồi bật lại.**
Không đo được bằng log, chỉ cảm nhận được. Chạy tới mép sàn (dùng biên `WORLD_HALF_EXTENT` làm mép
tạm) và bấm nhảy dồn dập. Ghi lại cảm giác trước/sau. Đây là lần đầu trong dự án bạn gặp một tính
năng mà **giá trị của nó không đo được bằng log hay unit test** — và nó vẫn buộc contract mạng phải
phình ra hai `int`. Ghi nhớ cảm giác đó cho Phase 9, nơi mọi thứ đều như vậy.

---

## Troubleshooting

| Triệu chứng | Nguyên nhân thường gặp | Chỗ sửa |
|---|---|---|
| Nhân vật rơi xuyên qua sàn, `y` về âm vô hạn | Thiếu phép 5, hoặc kiểm `Y < GROUND_Y` mà quên gán `Y = GROUND_Y` | `MovementRules.Step` |
| Đứng yên mà `grounded` nhấp nháy True/False | Gán `VelY = 0` **ngoài** nhánh `if` chạm sàn, hoặc `GROUND_Y` khác nhau ở hai chỗ | `MovementRules.Step` phép 5 |
| Thỉnh thoảng bấm Space không nhảy | Một trong hai chỗ chốt đang ghi đè: `_pendingJump = jump` thay vì `\|=`, hoặc đọc `WasPressedThisFrame` bên trong vòng tick | `PlayerEntity.SetInput` · `PlayerMotor.Update` |
| Giữ Space là nhảy liên tục như nảy bóng | Quên `_pendingJump = false` sau khi tiêu thụ trong `Integrate`, hoặc quên `_jumpLatched = false` trong `Step` | Cả hai chỗ tiêu thụ |
| Nhảy được hai lần liên tiếp giữa không trung | Phép 3c chỉ vô hiệu một bộ đếm | `MovementRules.Step` phép 3c |
| Vừa chạm đất là tự nhảy lại, lặp vô tận | Phép 3c chỉ xoá `TicksSinceGrounded`, quên `TicksSinceJumpRequest` | `MovementRules.Step` phép 3c |
| Nhảy lên bị giật ngược xuống giữa chừng | `PendingInput` mất cờ `Jump`, hoặc `OnMoveStateResult` chỉ nhận X/Y mà giữ `VelY` cũ | `PlayerMotor` |
| Nhân vật đứng im hoàn toàn, server không log gì | DTO đổi nhưng Unity còn dùng DLL cũ — build `Shared` chưa copy sang `Assets/Plugins/Shared/` | Build lại `Server/Shared`, xem post-build target |
| Hiển thị tụt lại rất xa mỗi lần rơi rồi mới đuổi kịp | `MoveTowards` còn dùng `MOVE_SPEED` làm tốc độ đuổi | `PlayerMotor.Update` |
| Người khác nhìn thấy cú nhảy bị "cắt ngọn" | Không phải lỗi Phase 8: `INTERP_DELAY = 0.15f` của Phase 7 đang cắt. Nếu khó chịu thì đó là bài của Phase 10 (AOI) — đừng sửa vội | `RemotePlayerView` |

---

## Tự kiểm tra hiểu bài

**Câu 1.** Vì sao `MoveState` phải chứa `VelY`, trong khi ở Phase 6 chỉ cần `(X, Y)` là đủ?
<details>
<summary><b>📖 Đáp án câu 1</b></summary>

Vì trạng thái là *tập nhỏ nhất mà biết nó thì tính được tick kế tiếp*. Ở top-down, tick sau =
vị trí hiện tại + input — vận tốc suy ra được từ input nên không cần lưu. Ở platformer, hai nhân vật
đứng cùng một điểm nhưng một đang bay lên (`VelY = +8`) và một đang rơi (`VelY = -8`) sẽ ở hai chỗ
khác nhau ở tick sau, mà input của cả hai đều giống nhau (không ai bấm gì). Vận tốc mang thông tin
về *quá khứ* mà vị trí không mang được, và tương lai phụ thuộc vào nó.

Hệ quả thực tế: `MoveStateResponse` phải mang `VelY`. Nếu chỉ gửi `X`/`Y`, client replay từ vận tốc
cũ của chính mình — tức là trộn một nửa sự thật của server với một nửa phỏng đoán của client.
</details>

**Câu 2.** `_pendingJump |= jump` trong `SetInput` và `_pendingJump = false` trong `Integrate` — vì sao
không viết gọn thành `_pendingJump = jump` như `_intentDirX`?
<details>
<summary><b>📖 Đáp án câu 2</b></summary>

Vì `DirX` là **mức** còn `Jump` là **cạnh**.

Mức: giá trị mới luôn đúng hơn giá trị cũ. "Đang bấm sang phải" hôm nay đè lên "đang bấm sang phải"
của 50ms trước mà không mất gì.

Cạnh: giá trị mang nghĩa *một sự kiện đã xảy ra*, và sự kiện không được phép biến mất. Client gửi
20 gói/s, server tick 20 lần/s, nhưng hai nhịp không khoá pha với nhau — mạng dồn gói, `Task.Delay(1)`
trên Windows dậy trễ ~15ms. Chuyện gói `{Jump: true}` và gói `{Jump: false}` cùng tới giữa hai tick
là bình thường. Ghi đè thì gói thứ hai nuốt sự kiện của gói thứ nhất.

Luật chung cho mọi input về sau: **mức thì lấy mới nhất, cạnh thì gộp tới khi tiêu thụ.** Đánh, dùng
skill, nhặt đồ ở các phase sau đều là cạnh.
</details>

**Câu 3.** Coyote time là thứ "chỉ để cảm giác đã tay". Vì sao không thể làm nó riêng ở client cho gọn,
để server khỏi phải biết?
<details>
<summary><b>📖 Đáp án câu 3</b></summary>

Vì nó là **quy tắc chơi**, và quy tắc chơi mà hai bên hiểu khác nhau thì reconciliation sẽ đánh nhau.

Cụ thể: người chơi rời mép sàn rồi bấm nhảy ở tick thứ 2. Client (có coyote) cho nhảy, dự đoán nhân
vật bay lên. Server (không có coyote) thấy `Grounded == false` nên từ chối, tiếp tục cho rơi. Gói
`MoveState` về mang `VelY` âm → client bị kéo tụt xuống giữa lúc đang vẽ cú bay lên.

Kết quả tệ hơn cả không có coyote time: người chơi thấy nhân vật nhảy lên rồi bị giật ngược xuống,
đúng ở khoảnh khắc họ đang nhìn chăm chú nhất. "Cải tiến cảm giác" bằng cách nói dối client luôn cho
ra cảm giác tệ hơn là thành thật.

Đây là hệ quả trực tiếp của golden rule #2, ở dạng ít ai ngờ tới: kể cả thứ nghe như thuần trình bày
cũng phải hỏi "cái này có ảnh hưởng tới state không?" Nếu có, nó thuộc về server.
</details>

**Câu 4.** Vì sao phép 3c phải đặt **cả hai** bộ đếm về `EXPIRED`? Hình dung điều gì xảy ra nếu chỉ
đặt một cái.
<details>
<summary><b>📖 Đáp án câu 4</b></summary>

Hai bộ đếm là hai *tư cách* độc lập, và điều kiện nhảy là AND của cả hai. Tiêu huỷ một cái thì cái
kia vẫn còn hiệu lực trong vài tick nữa, và chỉ cần tư cách còn lại được tái lập là nhảy lại được.

- **Chỉ xoá `TicksSinceJumpRequest`:** `TicksSinceGrounded` vẫn là 0 hoặc 1 ở tick sau (nhân vật mới
  bật lên, chưa đi xa). Người chơi bấm Space lần nữa ngay lập tức → `TicksSinceJumpRequest` về 0 →
  cả hai điều kiện lại thoả → **nhảy đôi miễn phí**, và nhảy ba nếu bấm đủ nhanh.
- **Chỉ xoá `TicksSinceGrounded`:** `TicksSinceJumpRequest` vẫn nhỏ. Nhân vật bay lên, rơi xuống,
  chạm đất → phép 5 đặt `TicksSinceGrounded = 0` → cả hai điều kiện lại thoả → **tự nhảy lại**. Và
  cứ thế mãi mãi, vì mỗi lần chạm đất lại tái lập đúng tình huống đó.

Cách nghĩ tổng quát: `EXPIRED` không phải "reset bộ đếm" mà là "tiêu huỷ vé". Một cú bấm mua đúng
một vé nhảy, và vé phải bị thu ở **cả hai cửa**.
</details>

**Câu 5.** `MoveState` là `struct` chứ không phải `class`. Nếu đổi thành `class`, vòng replay trong
`OnMoveStateResult` sẽ hỏng thế nào?
<details>
<summary><b>📖 Đáp án câu 5</b></summary>

Với `struct`, `state = MovementRules.Step(state, intent, dt)` là: copy `state` vào tham số, `Step`
sửa **bản copy** của nó, trả về bản copy đã sửa, gán đè lên biến `state`. Trạng thái server gốc mà
ta dựng từ `response` không hề bị đụng tới.

Với `class`, tham số là **tham chiếu**. `Step` sửa thẳng vào đối tượng mà `state` đang trỏ tới. Trong
vòng replay hiện tại thì kết quả cuối vẫn đúng (vì ta gán lại chính nó) — nhưng cái bẫy nằm ở chỗ
`PendingInput`. Ngày nào có ai lưu một `MoveState` vào lịch sử để so sánh về sau (rất tự nhiên khi
gỡ lỗi rubber-band, hoặc khi Phase 9 cần trạng thái của tick trước), mọi bản lưu sẽ cùng trỏ vào một
đối tượng và cùng mang giá trị mới nhất. Lịch sử tự viết lại chính nó, âm thầm.

Nói ngắn: `struct` biến "đừng vô tình chia sẻ trạng thái" từ một quy ước phải nhớ thành một tính chất
của ngôn ngữ. Đổi lại nó bị copy nhiều — với 7 field POD thì rẻ hơn cả một lần cấp phát heap.
</details>

**Câu 6.** Trong `MoveHandler`, vì sao vẫn phải kiểm `float.IsFinite(input.DirX)` khi ngay dòng dưới
đã có `Math.Clamp(dirX, -1f, 1f)`?
<details>
<summary><b>📖 Đáp án câu 6</b></summary>

Vì `Clamp` không bắt được `NaN`. Mọi phép so sánh với `NaN` đều cho `false` — cả `NaN < -1` lẫn
`NaN > 1` — nên `Math.Clamp` trả `NaN` về nguyên vẹn.

Và `NaN` thì lây: `NaN * MOVE_SPEED = NaN`, `X += NaN` làm `X` thành `NaN` vĩnh viễn. Không phép toán
nào sau đó gột được nó ra. Nhân vật biến mất khỏi màn hình mọi người, và `SavePosition` ghi `NaN` vào
DB — lần đăng nhập sau vẫn hỏng, kể cả sau khi restart server.

Đây là lý do phải chặn **ngay tại cửa vào**, ở đúng chỗ giá trị từ ngoài đi vào hệ thống, chứ không
phải ở chỗ nó gây ra triệu chứng. Nguyên tắc chung: mọi `float` đến từ client đều phải qua
`IsFinite` trước khi qua bất cứ phép gì khác.
</details>

**Câu 7.** Phase 7 viết bộ nội suy `RemotePlayerView` cho chuyển động top-down. Phase 8 đổi hẳn luật
di chuyển sang có trọng lực và nhảy — vì sao `RemotePlayerView` không phải sửa một dòng nào?
<details>
<summary><b>📖 Đáp án câu 7</b></summary>

Vì nó nội suy **vị trí theo thời gian**, và nó không biết — cũng không cần biết — vị trí ấy sinh ra
từ luật nào. Đầu vào của nó là một dãy `(thời điểm, toạ độ)`; đầu ra là toạ độ tại một thời điểm nằm
giữa hai mẫu. Đường thẳng hay đường parabol của cú nhảy đều được cắt thành các đoạn thẳng nhỏ 50ms,
và ở kích thước đó thì mắt người không phân biệt được.

Đây là phần thưởng của việc **tách biểu diễn khỏi mô phỏng**. `PlayerMotor` (mô phỏng, cần biết luật)
phải viết lại gần hết ở phase này. `RemotePlayerView` (biểu diễn, chỉ cần biết toạ độ) không đụng tới.
Cùng một dự án, cùng một tính năng mới, hai file chịu ảnh hưởng hoàn toàn khác nhau — vì chúng phụ
thuộc vào hai thứ khác nhau.

Đối chiếu ngược: nếu Phase 7 đã trót cho `RemotePlayerView` "đoán tiếp theo hướng đang đi" (ngoại suy),
thì phase này sẽ phải sửa nó — vì đoán theo đường thẳng trên một quỹ đạo parabol là sai. Quyết định
"qua mẫu cuối thì đứng yên, không đoán" ở Phase 7 vừa trả cổ tức.
</details>

---

## Để dành (ghi lại, chưa làm)

- **Nhảy cao thấp theo độ giữ nút** (giữ lâu nhảy cao). Cần đưa `JumpHeld` vào `MoveIntent` **cạnh
  bên** `Jump`, và cắt `VelY` một nửa ở tick người chơi thả nút trong lúc đang bay lên. Là một `bool`
  nữa trên dây và một nhánh nữa trong `Step` — làm được ngay, nhưng để sau khi map thật có chỗ mà nhảy.
- **Gia tốc và ma sát ngang** (thả phím còn trượt một đoạn). Thay phép 1 bằng tiến `VelX` dần về đích.
  Cảm giác nặng hơn, nhưng thêm một nguồn sai lệch nữa cho reconciliation — thêm sau khi đã tin motor.
- **Bán kính thân nhân vật.** Hiện nhân vật là **một điểm**; va chạm sàn kiểm đúng một toạ độ. Có map
  thật (Phase 10) mới cần hộp va chạm, và khi đó phép 5 tách thành kiểm hai góc dưới.
- **Sàn xuyên-một-chiều** (nhảy từ dưới lên xuyên qua, đứng được ở trên, bấm xuống thì rơi qua). Ràng
  buộc: chỉ chặn khi `VelY <= 0` **và** chân đang ở trên mặt sàn ở tick trước. Đây là nội dung Phase 10.
- **Chống tunneling tử tế** (raycast quãng đường thay vì chỉ kiểm điểm cuối). `MAX_FALL_SPEED` hiện là
  bản vá rẻ tiền và đủ dùng ở tốc độ này. Cần khi có bệ rơi nhanh hoặc nhân vật bị đẩy mạnh.

---

**Xong Phase 8 → luật di chuyển đã là platformer thật, và luật ấy chỉ tồn tại ở một nơi.**
Nhân vật giờ có *vận tốc* và *đang-đứng-đất* — hai thứ mà bước 5 mới chỉ dùng tạm bằng vài dòng `if`.
[PHASE-9](PHASE-9.md) biến chúng thành một **state machine tử tế**, rồi thêm tầng thứ hai mà client
không được phép tự quyết: `attack`, `hurt`, `die`. Đó là lần đầu tiên trong dự án một thứ thuần "hình
ảnh" phải xin phép server — và là nền để Phase 14 chỉ việc gắn sát thương lên trên. (Viết khi bạn báo
xong Phase 8.)
