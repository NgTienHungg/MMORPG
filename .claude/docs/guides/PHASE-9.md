# PHASE 9 — State machine trạng thái: hai tầng, và ranh giới ai được quyết cái gì

> **Kết quả cuối Phase 9:** nhân vật đổi hình đúng theo việc nó đang làm — đứng, chạy, bay lên, rơi
> xuống, ngồi. Bấm nút đánh thì **server duyệt** rồi cả hai client mới cùng thấy anim `attack`, đúng
> hướng mặt, đúng độ dài. Và có một nút thử trên console server bắt mọi người `hurt` — chứng minh
> rằng có những trạng thái client **không bao giờ được tự bật**.
>
> **Điều kiện:** xong [`PHASE-8.md`](PHASE-8.md) hết Bước 4 — nhảy có coyote time, hai client thấy
> nhau bay lên rơi xuống theo đường cong.
>
> **Bài học chính:** (1) trạng thái nhân vật có **hai tầng** với hai chủ sở hữu khác nhau, và nhầm
> tầng là phá golden rule #2 ở dạng hình ảnh; (2) cách chặn client xin bậy **rẻ nhất** không phải là
> `if` kiểm tra ở server mà là **kiểu dữ liệu không cho phép diễn đạt điều bậy**; (3) thời lượng một
> trạng thái đếm bằng **tick**, không bao giờ bằng độ dài clip — vì server không có clip nào cả;
> (4) `Animator` của Unity là **máy chiếu phim**; đặt luật chơi vào đó là đặt luật ở nơi server không
> đọc được, không replay được, không test được.

Format như trước: **hướng làm** hiện sẵn, **📖 Lời giải** trong foldout.

---

## Hai tầng trạng thái

Bộ sprite Dragon Warrior có 13 nhóm: `idle` `walk` `jump` `crouch` `attack` `crouch_ATK` `jumpATK`
`strike` `flyKick` `hurt` `die` `dizzy` `win`. Nhìn qua thì cả 13 đều là "trạng thái nhân vật", và
phản xạ tự nhiên là làm **một** máy trạng thái to cho tất cả.

Đó là cái bẫy. Chia đúng phải là **hai tầng, hai chủ sở hữu**:

| | **Tầng 1 — locomotion** | **Tầng 2 — action** |
|---|---|---|
| Gồm | `Idle` `Walk` `Jump` `Fall` `Crouch` | `None` `Attack` `Hurt` `Die` |
| Trả lời câu hỏi | "thân thể đang ở tư thế nào" | "đang làm việc gì" |
| Ai quyết | **suy ra** từ `MoveState` bằng một hàm thuần | **server**, và chỉ server |
| Tốn bao nhiêu byte | 0 cho chính mình, gần 0 cho người khác | 1 byte + bộ đếm |
| Sai một frame thì sao | Không ai chết — hình hơi lệch một nhịp | Người chơi thấy mình trúng đòn mà máu không đổi |

Câu quan trọng nhất của phase, đọc kỹ:

> **Tầng 1 không phải là "client tự quyết". Nó là "client tự *suy ra* từ trạng thái server đã chốt".**
> Tầng 2 là "client *xin phép*, server chốt, rồi mới hiện".

Khác biệt nghe mỏng nhưng nó là toàn bộ ranh giới. `Walk` không tốn byte nào **không phải** vì nó kém
quan trọng, mà vì nó là **hệ quả tất yếu** của `VelX != 0` — mà `VelX` thì server đã chốt và đã gửi
rồi. Suy lại từ dữ liệu đã có không phải là tự quyết; nó là không gửi trùng.

Ngược lại `Hurt` **không** suy ra được từ bất cứ thứ gì client biết. Nó sinh ra từ một sự kiện chỉ
server thấy (ai đánh trúng ai). Client bật `hurt` khi thấy đối phương vung tay là **đoán** — và đoán
sai ở đây nghĩa là người chơi nhìn thấy một sự thật không tồn tại.

```
                 server chốt                                    client suy
                     │                                              │
MoveState { X Y VelX VelY Grounded Crouching Facing } ──────────────┴──► LocomotionState
MoveState { Action ActionTicksLeft } ───────────────────────────────────► ActionState
                     ▲
                     └── chỉ đổi bên trong MovementRules.Step, hoặc bởi lệnh của server
```

Cả hai tầng **đều đọc từ `MoveState`**. Nghĩa là cả hai đều đã được reconciliation của Phase 8 lo hộ:
gói `MoveState` về → replay → ra trạng thái mới → hình ảnh tự đúng theo. Không có đường ống đồng bộ
riêng cho hoạt ảnh, và đó là lý do phase này ngắn hơn vẻ ngoài của nó.

---

## Bước 1 — Shared: hai enum, và một ranh giới do trình biên dịch canh

### Hướng làm

**File mới `Server/Shared/World/CharacterStates.cs`** — ba enum và hai hàm thuần.

**(a) `LocomotionState`** — 5 giá trị: `Idle` `Walk` `Jump` `Fall` `Crouch`, kèm hàm suy:

```csharp
public static LocomotionState Derive(in MoveState state)
```

Thứ tự kiểm là một phần của định nghĩa: **trên không thắng ngồi, ngồi thắng chạy**. Đang bay mà bấm
ngồi thì vẫn là `Jump`, không phải `Crouch`.

Hàm này đặt ở `Shared` dù **server không gọi nó lần nào** — server có vẽ gì đâu. Đây là một tinh chỉnh
đáng nhớ của golden rule #4:

> `Shared` không có nghĩa là "cả hai bên đều dùng". Nó có nghĩa là "**chỉ có một định nghĩa**".

`LocomotionState` là *ý nghĩa* của `MoveState`. Để định nghĩa ấy nằm cạnh dữ liệu nó đọc thì sau này
thêm một field vào `MoveState` là thấy ngay có phải sửa hàm suy không. Để nó bên client thì hai thứ
trôi xa nhau dần, và ngày server cần biết "người này đang ngồi hay đứng" (hộp va chạm thấp hơn khi
ngồi) sẽ phải viết lại lần hai — lần hai không bao giờ giống lần một.

**(b) Hai enum cho tầng action — và đây là ý chính của cả bước:**

```csharp
public enum ActionRequest : byte { None = 0, Attack = 1 }
public enum ActionState   : byte { None = 0, Attack = 1, Hurt = 2, Die = 3 }
```

Hai enum khác nhau cho hai vai trò khác nhau. `ActionRequest` là thứ **client được phép xin**;
`ActionState` là thứ một entity **có thể đang ở**. Client gửi `ActionRequest` — và trong bộ giá trị
đó **không tồn tại** `Hurt` hay `Die`.

Dừng lại ở chỗ này. Cách thông thường là dùng chung một enum rồi chặn ở server:

```csharp
if (input.Action == ActionState.Hurt || input.Action == ActionState.Die)
    return; // client không được xin mấy cái này
```

Đoạn `if` đó **hoạt động** — hôm nay. Nó hỏng vào ngày ai đó thêm `ActionState.Stun` và quên cập nhật
danh sách chặn. Hai enum riêng thì không có gì để quên: thứ không diễn đạt được thì không cần chặn.

> Chặn hành vi sai bằng **kiểu dữ liệu** rẻ hơn chặn bằng **câu lệnh** — vì kiểu dữ liệu được trình
> biên dịch kiểm mỗi lần build, còn câu lệnh chỉ được kiểm bởi trí nhớ của người sửa code tiếp theo.

Nguyên tắc này sẽ quay lại ở mọi feature sau: thứ client gửi lên và thứ server giữ nên là **hai kiểu**
mỗi khi tập giá trị hợp lệ của chúng khác nhau.

**(c) Bảng chuyển tiếp — bằng độ ưu tiên, không bằng `if` lồng nhau.** Viết đủ 16 ô của ma trận 4×4 là
16 nhánh và sẽ sai một ô. Cả bảng gói trong đúng một quy tắc:

| Trạng thái | Ưu tiên |
|---|---|
| `None` | 0 |
| `Attack` | 1 |
| `Hurt` | 2 |
| `Die` | 3 |

> Vào được trạng thái mới **nếu** ưu tiên của nó cao hơn trạng thái hiện tại, **hoặc** trạng thái hiện
> tại đã hết thời lượng. `Die` thì không bao giờ thoát ra.

Đọc lại thành lời: đang đánh dở mà ăn đòn thì `hurt` cắt ngang `attack` (2 > 1) ✓ · đang `hurt` mà bấm
đánh thì không được (1 < 2, và `hurt` chưa hết ticks) ✓ · chết rồi thì mọi thứ ngừng ✓ · đánh xong
(`ticksLeft == 0`) thì đánh tiếp được ✓. Bốn hành vi đúng từ một dòng luật.

**(d) `MoveState` nở thêm 5 field:**

| Field | Kiểu | Vì sao ở đây chứ không ở chỗ khác |
|---|---|---|
| `Crouching` | `bool` | Ngồi làm `VelX = 0` → nó là **sự thật vật lý**, không phải chuyện hình ảnh |
| `FacingLeft` | `bool` | Hướng đánh phải do server chốt. Và nó có **trí nhớ** (đứng yên thì giữ hướng cũ) nên không suy ra được từ `VelX` |
| `Action` | `ActionState` | Tầng 2, phải sống sót qua replay |
| `ActionTicksLeft` | `int` | Đếm ngược trong `Step` → replay tự đúng |
| `TicksSinceAttack` | `int` | Cooldown. Cùng họ với `TicksSinceGrounded` của Phase 8 |

Lại một lần nữa: **một tính năng "chỉ là cảm giác" làm phình trạng thái.** Phase 8 đã trả hai `int` cho
coyote time; giờ là năm field cho hoạt ảnh. Đây không phải thiết kế tồi — đây là cái giá thật của
"server là source of truth", và biết trước nó thì không bị bất ngờ ở Phase 12–14.

**(e) `MoveIntent` nở thêm hai field:** `Crouch` (**trục giữ** — bấm là ngồi, thả là đứng) và `Action`
kiểu `ActionRequest` (**nút cạnh** — như `Jump`). Nhớ luật của Phase 8: trục giữ thì lấy giá trị mới
nhất, nút cạnh thì gộp lại tới khi tiêu thụ.

**(f) Dọn DTO — bỏ hẳn kiểu chép tay từng field.** `MoveStateResponse` hiện chép tay 7 field từ
`MoveState`; sau bước này sẽ là 12. Ba chỗ chép tay (khai báo DTO, server điền, client đọc) × 12 field
= 36 cơ hội gõ nhầm — mà nhầm thì **không có lỗi biên dịch**, chỉ có một field im lặng mang giá trị
mặc định.

Cho `MoveState` và `MoveIntent` đi thẳng trên dây:

```csharp
[MemoryPackable] public partial struct MoveState  { ... }
[MemoryPackable] public partial struct MoveIntent { ... }

[MemoryPackable] public partial class MoveInputRequest  { public int Seq; public MoveIntent Intent; }
[MemoryPackable] public partial class MoveStateResponse { public int LastInputSeq; public MoveState State; }
```

Số chỗ chép tay về **0**. Thêm field vào `MoveState` từ nay là sửa đúng một file.

Có một câu hỏi đúng phải hỏi ở đây: *gắn format gói tin vào cấu trúc nội bộ như vậy có phải là coupling
tồi không?* Với kênh này thì **không**, và lý do đáng nhớ:

> Kênh "chính mình" (`MoveState`) **định nghĩa** là gửi trọn trạng thái — đó chính là reconciliation.
> Kênh "người khác" (`EntityState`) thì ngược lại: chỉ gửi thứ vẽ được. Hai kênh, hai lý do, hai
> format — giữ chúng khác nhau là có chủ đích, không phải quên gộp.

**(g) `EntityState` — thứ người khác cần, và không hơn.** Người xem **không** cần `VelX`, `VelY`,
`TicksSinceGrounded`… Họ cần đủ để vẽ. Thêm đúng hai byte:

```csharp
public byte Flags;   // bit 0 FacingLeft, bit 1 Crouching
public byte Action;  // ActionState
```

Vì sao chỉ hai thứ này chứ không gửi luôn cả `LocomotionState`? Vì phần còn lại **suy được từ chính
vị trí đang gửi**: hai mẫu snapshot liên tiếp cho `ΔY > 0` là đang bay lên, `ΔY < 0` là đang rơi,
`ΔY == 0` là đang chạm đất, rồi `ΔX` phân biệt `Idle` với `Walk`. Chi tiết ở Bước 5. Còn hai thứ
**không** suy được là `Crouching` (ngồi và đứng yên cho cùng một chuỗi vị trí) và `FacingLeft` (đứng
yên thì hướng mặt là trí nhớ, không phải chuyển động).

Bài tập nhỏ tự làm trước khi đọc tiếp: lấy 5 giá trị của `LocomotionState`, tự phân loại cái nào suy
được từ hai mẫu vị trí, cái nào không, và vì sao. Trả lời đúng câu đó là hiểu xong nửa phase.

Đóng gói bit ở **một chỗ duy nhất** trong `Shared` (`EntityFlags.Pack` / `.Has`) — mặt nạ bit chép tay
hai bên là đúng anti-pattern số 1 của repo, chỉ khác chỗ nạn nhân là một con số `1 << 1`.

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`Server/Shared/World/CharacterStates.cs`** (file mới):

```csharp
namespace MMORPG.Shared.World
{
    /// <summary>
    /// Tư thế thân thể — SUY RA từ trạng thái vật lý, không đi trên dây và không ai "quyết" nó cả.
    /// </summary>
    public enum LocomotionState : byte
    {
        Idle = 0,
        Walk = 1,
        Jump = 2,
        Fall = 3,
        Crouch = 4,
    }

    /// <summary>
    /// Hành động mà CLIENT được phép xin. Cố tình không có Hurt/Die: hai trạng thái đó sinh ra từ
    /// sự kiện chỉ server thấy, nên chúng phải nằm ngoài tầm diễn đạt của một gói tin gửi lên.
    /// Chặn bằng kiểu dữ liệu chứ không bằng câu lệnh — câu lệnh chỉ được kiểm bởi trí nhớ của
    /// người sửa code tiếp theo, còn kiểu thì được kiểm mỗi lần build.
    /// </summary>
    public enum ActionRequest : byte
    {
        None = 0,
        Attack = 1,
    }

    /// <summary>
    /// Hành động một entity CÓ THỂ đang ở. Tập này rộng hơn <see cref="ActionRequest"/> đúng ở phần
    /// mà chỉ server có quyền đặt.
    ///
    /// Giá trị số cũng chính là ĐỘ ƯU TIÊN: lớn hơn thì cắt ngang được cái nhỏ hơn.
    /// </summary>
    public enum ActionState : byte
    {
        None = 0,
        Attack = 1,
        Hurt = 2,
        Die = 3,
    }

    /// <summary>Luật của tầng trạng thái: suy tư thế, và ai được cắt ngang ai.</summary>
    public static class CharacterStates
    {
        /// <summary>
        /// Tư thế thân thể tại một trạng thái vật lý. Hàm THUẦN, và thứ tự kiểm là một phần của
        /// định nghĩa: trên không thắng ngồi, ngồi thắng chạy. Đang bay mà bấm ngồi thì vẫn là Jump.
        ///
        /// Server không gọi hàm này lần nào — server không vẽ gì cả. Nó nằm ở đây vì đây là nơi
        /// duy nhất định nghĩa "MoveState này nghĩa là tư thế gì", và định nghĩa phải ở cạnh dữ
        /// liệu nó đọc: thêm một field vào MoveState là thấy ngay có phải sửa chỗ này không.
        /// </summary>
        public static LocomotionState Derive(in MoveState state)
        {
            if (!state.Grounded)
            {
                // Mốc 0 chứ không phải chia theo dấu vận tốc lúc bấm nhảy: đúng đỉnh parabol VelY
                // đi qua 0, và ở đó gọi là Fall hợp lý hơn Jump — thân đã ngừng bốc lên.
                return state.VelY > 0f ? LocomotionState.Jump : LocomotionState.Fall;
            }

            if (state.Crouching)
                return LocomotionState.Crouch;

            return state.VelX != 0f ? LocomotionState.Walk : LocomotionState.Idle;
        }

        /// <summary>
        /// Có được chuyển sang <paramref name="next"/> không. Toàn bộ ma trận 4×4 gói trong một
        /// dòng luật: ưu tiên cao hơn thì cắt ngang được, bằng hoặc thấp hơn thì phải chờ hết
        /// thời lượng. Viết 16 nhánh cho 16 ô là cách chắc chắn để sai một ô mà không ai biết.
        /// </summary>
        public static bool CanEnter(ActionState current, int ticksLeft, ActionState next)
        {
            // Chết là hết. Không có đường ra khỏi Die bằng luật; hồi sinh là lệnh riêng của server
            // đặt thẳng trạng thái, không đi qua hàm này.
            if (current == ActionState.Die)
                return false;

            if (next > current)
                return true;

            return ticksLeft <= 0;
        }

        /// <summary>
        /// Hành động có khoá thân thể lại không: ăn đòn và chết thì không tự đi được nữa.
        /// Đánh thì vẫn chạy được — bộ sprite có cả jumpATK lẫn flyKick, khoá chân là tự mâu thuẫn.
        /// </summary>
        public static bool BlocksMovement(ActionState action)
        {
            return action == ActionState.Hurt || action == ActionState.Die;
        }
    }
}
```

**`Server/Shared/World/MoveState.cs`** — thêm 5 field vào `MoveState`, 2 field vào `MoveIntent`, và
`[MemoryPackable]` cho cả hai:

```csharp
using MemoryPack;

namespace MMORPG.Shared.World
{
    /// <summary>
    /// Ý định của người chơi tại một tick — đúng những gì bấm được trên bàn phím, không hơn.
    /// Đi thẳng trên dây (bọc trong MoveInputRequest) nên mọi field ở đây đều là thứ kẻ lạ
    /// điều khiển được: người nhận phải kiểm, không được tin.
    /// </summary>
    [MemoryPackable]
    public partial struct MoveIntent
    {
        /// <summary>Hướng ngang trong [-1, 1]. Người gọi chịu trách nhiệm kẹp; Step không kiểm lại.</summary>
        public float DirX;

        /// <summary>CẠNH LÊN của nút nhảy: "vừa bấm tại tick này", không phải "nút đang bị giữ".</summary>
        public bool Jump;

        /// <summary>Trục GIỮ: bấm là ngồi, thả là đứng. Khác Jump — lấy giá trị mới nhất, không gộp.</summary>
        public bool Crouch;

        /// <summary>
        /// Hành động vừa xin, dạng cạnh như Jump. Kiểu ActionRequest chứ không phải ActionState:
        /// tập giá trị này cố tình hẹp hơn, để "xin được Hurt" là chuyện không viết ra được.
        /// </summary>
        public ActionRequest Action;
    }

    /// <summary>
    /// Toàn bộ trạng thái của một entity — tập nhỏ nhất mà biết nó thì tính được tick kế tiếp,
    /// VÀ vẽ được nhân vật ra màn hình. Hai vai trò trong một struct là có chủ đích: nhờ vậy
    /// hoạt ảnh được reconciliation lo hộ, không cần đường đồng bộ riêng.
    /// </summary>
    [MemoryPackable]
    public partial struct MoveState
    {
        public float X;
        public float Y;
        public float VelX;
        public float VelY;

        /// <summary>Chân có đang chạm sàn ở CUỐI tick trước không. Điều kiện để được nhảy.</summary>
        public bool Grounded;

        public int TicksSinceGrounded;
        public int TicksSinceJumpRequest;

        /// <summary>
        /// Đang ngồi. Là sự thật vật lý chứ không phải chuyện hình ảnh: ngồi thì VelX bị ép về 0,
        /// nên nó phải nằm trong trạng thái mà cả hai bên cùng mô phỏng.
        /// </summary>
        public bool Crouching;

        /// <summary>
        /// Hướng mặt. Không suy ra được từ VelX vì nó có TRÍ NHỚ — đứng yên thì giữ hướng cũ.
        /// Và vì hướng quyết định đòn đánh nhắm về phía nào nên nó phải do server chốt.
        /// </summary>
        public bool FacingLeft;

        /// <summary>Hành động đang thực hiện. Chỉ Step (từ ý định) hoặc lệnh của server đặt được.</summary>
        public ActionState Action;

        /// <summary>Số tick còn lại của hành động. Đếm ngược trong Step nên replay tự đúng.</summary>
        public int ActionTicksLeft;

        /// <summary>Số tick kể từ lần đánh gần nhất — nền của cooldown.</summary>
        public int TicksSinceAttack;

        public static MoveState AtRest(float x, float y)
        {
            return new MoveState
            {
                X = x, Y = y, VelX = 0f, VelY = 0f,
                Grounded = false,
                TicksSinceGrounded = MovementRules.EXPIRED,
                TicksSinceJumpRequest = MovementRules.EXPIRED,
                Crouching = false,
                FacingLeft = false,
                Action = ActionState.None,
                ActionTicksLeft = 0,

                // Hết cooldown sẵn: vừa vào world là đánh được ngay.
                TicksSinceAttack = MovementRules.EXPIRED,
            };
        }
    }
}
```

**`Server/Shared/Dto/World/MoveDto.cs`** — thu gọn còn hai lớp bọc:

```csharp
using MemoryPack;
using MMORPG.Shared.World;

namespace MMORPG.Shared.Dto.World
{
    /// <summary>
    /// Ý định của client tại một bước dự đoán. Cố tình KHÔNG có trường thời gian:
    /// server tích phân bằng TICK_DT của chính nó — dt mà đi trên gói tin thì dt là chỗ hack tốc độ.
    /// </summary>
    [MemoryPackable]
    public partial class MoveInputRequest
    {
        /// <summary>Số thứ tự client tự đánh, tăng dần. Server echo lại để client biết đã xử tới đâu.</summary>
        public int Seq { get; set; }

        /// <summary>
        /// Nguyên vẹn ý định, không tách field. Tách ra rồi ráp lại ở đầu kia là chép tay contract:
        /// thêm một field mà quên một chỗ ráp thì field đó im lặng mang giá trị mặc định.
        /// </summary>
        public MoveIntent Intent { get; set; }
    }

    /// <summary>
    /// Trạng thái authoritative của chính người nhận, gửi mỗi tick.
    /// Mang TRỌN MoveState vì đó đúng là định nghĩa của reconciliation: client replay từ đây,
    /// thiếu một field là replay ra một tương lai khác.
    /// </summary>
    [MemoryPackable]
    public partial class MoveStateResponse
    {
        /// <summary>Input cuối server đã nhận trước tick này. Client xoá pending ≤ số này rồi replay phần còn lại.</summary>
        public int LastInputSeq { get; set; }

        public MoveState State { get; set; }
    }
}
```

**`Server/Shared/Dto/World/WorldSyncDto.cs`** — `EntityState` và `EntitySpawnNotice` thêm hai byte,
kèm chỗ đóng gói bit dùng chung:

```csharp
using System;
using MemoryPack;
using MMORPG.Shared.World;

namespace MMORPG.Shared.Dto.World
{
    /// <summary>
    /// Đóng/mở gói các cờ nhị phân của một entity trong snapshot. Một chỗ duy nhất biết bit nào là
    /// gì — mặt nạ bit chép tay ở hai đầu dây là chép tay contract, và lệch một bit thì cả nửa số
    /// nhân vật quay mặt ngược mà không có lỗi nào để lần theo.
    /// </summary>
    public static class EntityFlags
    {
        public const byte FACING_LEFT = 1 << 0;
        public const byte CROUCHING = 1 << 1;

        public static byte Pack(in MoveState state)
        {
            byte flags = 0;

            if (state.FacingLeft)
                flags |= FACING_LEFT;

            if (state.Crouching)
                flags |= CROUCHING;

            return flags;
        }

        public static bool Has(byte flags, byte bit)
        {
            return (flags & bit) != 0;
        }
    }

    /// <summary>
    /// Phần BẤT BIẾN của một entity — gửi đúng một lần lúc nó xuất hiện.
    /// Thứ đổi theo tick đi trong snapshot, không lặp lại ở đây mỗi tick.
    /// </summary>
    [MemoryPackable]
    public partial class EntitySpawnNotice
    {
        public int EntityId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int ClassId { get; set; }

        /// <summary>Vị trí lúc xuất hiện — mồi đầu tiên cho buffer nội suy phía client.</summary>
        public float X { get; set; }
        public float Y { get; set; }

        /// <summary>Cờ lúc xuất hiện — để nhân vật hiện ra đã đúng hướng mặt, không quay đầu một nhịp sau.</summary>
        public byte Flags { get; set; }

        public byte Action { get; set; }
    }

    [MemoryPackable]
    public partial class EntityDespawnNotice
    {
        public int EntityId { get; set; }
    }

    /// <summary>
    /// Trạng thái một entity tại một tick. Cố tình chỉ có thứ NGƯỜI XEM cần để vẽ — không phải trọn
    /// MoveState như kênh của chính mình. Người xem không replay ai cả, nên vận tốc và các bộ đếm
    /// là byte thừa, 20 lần mỗi giây, nhân với số người quanh họ.
    /// </summary>
    [MemoryPackable]
    public partial class EntityState
    {
        public int EntityId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }

        /// <summary>Hai thứ KHÔNG suy được từ chuỗi vị trí: hướng mặt (có trí nhớ) và tư thế ngồi.</summary>
        public byte Flags { get; set; }

        /// <summary>Tầng action — do server quyết, người xem không có cách nào tự biết.</summary>
        public byte Action { get; set; }
    }

    [MemoryPackable]
    public partial class WorldSnapshotNotice
    {
        public EntityState[] States { get; set; } = Array.Empty<EntityState>();
    }
}
```

</details>

### ✅ CHECKPOINT A — build đỏ, và danh sách việc tự hiện ra

`dotnet build Server/Shared` phải **sạch**; DLL tự copy sang `Assets/Plugins/Shared/`.

`dotnet build Server/GameServer` và Unity console phải **đỏ**, ở đúng những chỗ này:

| Đỏ ở đâu | Vì sao |
|---|---|
| `PlayerEntity.SetInput` · `Integrate` | `MoveIntent` đổi hình dạng |
| `MoveHandler.OnMoveInput` | `input.DirX` không còn — giờ là `input.Intent.DirX` |
| `WorldService.Tick` | `MoveStateResponse` không còn 7 field rời |
| `PlayerMotor` (Unity) | cả ba chỗ trên, phía client |
| `WorldSpawner` · `RemotePlayerView` | **không đỏ** — `EntityState` chỉ *thêm* field |

Chỗ cuối đáng để ý và là bài học miễn phí: thêm field vào một DTO thì bên đọc **không** đỏ, nó chỉ
lặng lẽ bỏ qua field mới. Đó chính là loại bug câm mà mục (f) vừa dọn cho kênh `MoveState` — và là lý
do ở kênh snapshot ta phải **tự nhớ** đi cập nhật bên đọc, vì không có ai nhắc.

---

## Bước 2 — Shared: `Step` biết tư thế, hướng mặt và hành động

### Hướng làm

Toàn bộ tầng 2 sống trong `MovementRules.Step`. Đây là quyết định đáng cân nhắc nhất của phase, nên
nói rõ vì sao:

Cách khác là để server đặt `Action` bên ngoài `Step` (trong `WorldService`), coi hành động là "sự kiện"
tách khỏi "mô phỏng". Nghe sạch hơn. Nhưng thử nghĩ tiếp: client có replay không? Có — mỗi gói
`MoveState` về là replay lại toàn bộ pending input. Nếu `Attack` sinh ra **ngoài** `Step` thì vòng
replay không tái hiện được nó, và cứ mỗi lần đối chiếu thì cú đánh đang diễn ra sẽ **biến mất rồi hiện
lại**. Đúng cái bug "quên `Jump` trong `PendingInput`" của Phase 8, khoác áo mới.

> Thứ nào thay đổi `MoveState` theo thời gian thì phải nằm trong `Step`. Không có ngoại lệ — vì `Step`
> là định nghĩa của "một tick trôi qua", và replay chỉ biết gọi `Step`.

Server vẫn cần đặt được `Hurt`/`Die` từ bên ngoài — nhưng đó là **đặt một lần**, không phải "diễn tiến
theo thời gian". Phần diễn tiến (đếm ngược, hết hạn, cắt ngang) vẫn ở trong `Step`. Ranh giới: *sự kiện
rời rạc thì đặt từ ngoài, nhịp thời gian thì ở trong.*

**Hằng số mới trong `MovementRules`** — tất cả đếm bằng **tick**:

| Hằng | Giá trị | Ra giây | Ghi chú |
|---|---|---|---|
| `ATTACK_TICKS` | `5` | 0.25s | Clip `attack` có 3 frame; 0.25s cho 3 frame là 12fps, vừa mắt |
| `ATTACK_COOLDOWN_TICKS` | `8` | 0.4s | Đếm từ lúc **bắt đầu** đánh, không phải lúc kết thúc |
| `HURT_TICKS` | `4` | 0.2s | Clip `hurt` 2 frame |
| `DIE_TICKS` | `20` | 1.0s | Clip `die` 10 frame. Hết ticks vẫn **ở lại** `Die` |

**Vì sao đếm bằng tick chứ không bằng độ dài clip** — đây là ý mà ROADMAP gọi tên riêng, và nó có ba
tầng lý do:

1. **Server không có clip.** `GameServer` là process .NET, ở đó không tồn tại `AnimationClip`. Muốn
   server biết "đòn đánh kéo dài bao lâu" thì con số ấy phải là con số, không phải asset.
2. **Clip là tài sản của người làm hình.** Hôm nay `attack` 3 frame, mai hoạ sĩ thêm 2 frame vung tay
   cho đẹp. Nếu thời lượng đòn đánh ăn theo clip thì vừa sửa một file `.anim` là **đổi cân bằng game**
   — mà không ai review nó như đổi cân bằng.
3. **Tick là đơn vị duy nhất hai bên cùng đếm được.** Giây thì mỗi máy một đồng hồ; frame thì mỗi máy
   một tốc độ. Tick là nhịp của giao thức.

Hệ quả ngược lại, và là việc của Bước 4: **clip phải co giãn cho vừa số tick**, không phải ngược lại.

**Thứ tự các phép trong `Step`** — vẫn là một phần của contract, giờ dài hơn:

```
0.  Nhịp tầng action   ActionTicksLeft-- (sàn 0) ; TicksSinceAttack++ (kẹp EXPIRED)
                       hết ticks và không phải Die  →  Action = None
1.  Tư thế             Crouching = intent.Crouch && Grounded && !BlocksMovement(Action)
2.  Vận tốc ngang      bị khoá (Hurt/Die) hoặc đang ngồi  →  VelX = 0
                       ngược lại                          →  VelX = DirX * MOVE_SPEED
    Hướng mặt          VelX != 0 và Action == None  →  FacingLeft = VelX < 0
3.  Trọng lực          (như Phase 8)
4a. Bộ đếm nhảy        (như Phase 8)
4b. Điều kiện nhảy     (như Phase 8) + không bị khoá thân
4c. Nếu nhảy           (như Phase 8)
5.  Xin hành động      intent.Action == Attack
                       && TicksSinceAttack >= ATTACK_COOLDOWN_TICKS
                       && CanEnter(Action, ActionTicksLeft, Attack)
                       →  Action = Attack ; ActionTicksLeft = ATTACK_TICKS ; TicksSinceAttack = 0
6.  Tích phân          (như Phase 8)
7.  Va chạm sàn        (như Phase 8)
8.  Kẹp biên X         (như Phase 8)
```

Bốn chỗ dễ sai, đọc kỹ trước khi gõ:

- **Phép 0 phải chạy trước phép 5.** Chạy sau thì cú đánh vừa bắt đầu ở tick này đã bị chính phép 0
  trừ mất một tick. Cùng loại bẫy với "kiểm `Grounded` trước hay sau va chạm sàn" ở Phase 8.
- **Phép 0 không được xoá `Die`.** Chết rồi thì hết `ActionTicksLeft` là hết *hoạt ảnh*, không phải
  hết *trạng thái*. Bỏ sót nhánh loại trừ này thì xác chết đứng dậy đi tiếp sau 1 giây.
- **Hướng mặt khoá khi `Action != None`.** Đang vung tay mà xoay được người là đòn đánh đổi hướng giữa
  chừng — ở Phase 14 khi đòn đánh có hộp va chạm thật thì đó là lỗ hack: bấm đánh rồi xoay để quét cả
  hai bên.
- **Cooldown đếm từ lúc bắt đầu.** `TicksSinceAttack = 0` đặt cùng lúc với `ActionTicksLeft = ATTACK_TICKS`.
  Đếm từ lúc kết thúc thì tổng nhịp đánh = `ATTACK_TICKS + COOLDOWN`, và mỗi lần chỉnh độ dài anim là
  vô tình chỉnh luôn tốc độ đánh — đúng thứ vừa nói ở lý do (2).

Chú ý một điều tinh tế: phép 5 có **hai** điều kiện chặn (`CanEnter` và cooldown) và chúng khác nhau.
`CanEnter` trả lời "trạng thái hiện tại có cho phép không" (đang `hurt` thì không). Cooldown trả lời
"nhịp đánh đã tới chưa". Gộp hai thứ này vào một số là mất khả năng diễn đạt "đang choáng thì cấm đánh
kể cả đã hết cooldown".

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`Server/Shared/World/MovementRules.cs`** — hằng mới và `Step` đầy đủ:

```csharp
        /// <summary>Thời lượng một đòn đánh, tính bằng TICK. Không tính bằng độ dài clip: server
        /// không có clip nào, và độ dài clip là tài sản của người làm hình chứ không phải của luật chơi.</summary>
        public const int ATTACK_TICKS = 5;

        /// <summary>
        /// Nhịp đánh tối thiểu, đếm từ lúc BẮT ĐẦU đòn trước. Đếm từ lúc kết thúc thì mỗi lần chỉnh
        /// độ dài đòn đánh là vô tình chỉnh luôn tốc độ đánh.
        /// </summary>
        public const int ATTACK_COOLDOWN_TICKS = 8;

        /// <summary>Thời lượng choáng khi trúng đòn.</summary>
        public const int HURT_TICKS = 4;

        /// <summary>Thời lượng hoạt ảnh gục. Hết số tick này thì hoạt ảnh xong, nhưng trạng thái Die ở lại.</summary>
        public const int DIE_TICKS = 20;

        public static MoveState Step(MoveState state, MoveIntent intent, float dt)
        {
            // 0. Nhịp của tầng action. PHẢI chạy trước phép 5: chạy sau thì đòn vừa bắt đầu ở tick
            //    này bị trừ mất một tick ngay khi chưa kịp diễn.
            if (state.ActionTicksLeft > 0)
                state.ActionTicksLeft--;

            if (state.TicksSinceAttack < EXPIRED)
                state.TicksSinceAttack++;

            // Hết thời lượng thì về None — TRỪ Die. Chết rồi thì hết ticks là hết hoạt ảnh, không
            // phải hết trạng thái; bỏ nhánh loại trừ này là xác chết đứng dậy đi tiếp sau một giây.
            if (state.ActionTicksLeft <= 0 && state.Action != ActionState.Die)
                state.Action = ActionState.None;

            // 1. Tư thế. Ngồi chỉ có nghĩa khi chân chạm đất và thân thể còn nghe lời.
            state.Crouching = intent.Crouch
                              && state.Grounded
                              && !CharacterStates.BlocksMovement(state.Action);

            // 2. Vận tốc ngang. Ăn đòn / gục thì mất quyền điều khiển; ngồi thì đứng yên tại chỗ.
            if (CharacterStates.BlocksMovement(state.Action) || state.Crouching)
            {
                state.VelX = 0f;
            }
            else
            {
                state.VelX = intent.DirX * MOVE_SPEED;
            }

            // Hướng mặt: chỉ đổi khi đang thật sự dịch chuyển VÀ không vướng hành động nào.
            // Đứng yên thì giữ hướng cũ (đó là lý do FacingLeft phải là trạng thái, không phải suy ra).
            // Khoá hướng trong lúc hành động: vung tay mà xoay được người thì đòn đánh quét cả hai bên.
            if (state.VelX != 0f && state.Action == ActionState.None)
                state.FacingLeft = state.VelX < 0f;

            // 3. Trọng lực.
            state.VelY -= GRAVITY * dt;
            if (state.VelY < -MAX_FALL_SPEED)
                state.VelY = -MAX_FALL_SPEED;

            // 4a. Hai bộ đếm tha thứ.
            if (state.TicksSinceGrounded < EXPIRED)
                state.TicksSinceGrounded++;

            if (intent.Jump)
                state.TicksSinceJumpRequest = 0;
            else if (state.TicksSinceJumpRequest < EXPIRED)
                state.TicksSinceJumpRequest++;

            // 4b/4c. Nhảy — thêm điều kiện thân thể còn nghe lời.
            if (!CharacterStates.BlocksMovement(state.Action) &&
                state.TicksSinceJumpRequest <= JUMP_BUFFER_TICKS &&
                state.TicksSinceGrounded <= COYOTE_TICKS)
            {
                state.VelY = JUMP_SPEED;
                state.TicksSinceJumpRequest = EXPIRED;
                state.TicksSinceGrounded = EXPIRED;
            }

            // 5. Xin hành động. HAI điều kiện chặn khác nhau, cố tình không gộp:
            //    CanEnter  = "trạng thái hiện tại có cho phép không" (đang choáng thì không)
            //    cooldown  = "nhịp đánh đã tới chưa"
            //    Gộp vào một số là mất khả năng diễn đạt "hết cooldown rồi nhưng đang choáng nên vẫn cấm".
            if (intent.Action == ActionRequest.Attack &&
                state.TicksSinceAttack >= ATTACK_COOLDOWN_TICKS &&
                CharacterStates.CanEnter(state.Action, state.ActionTicksLeft, ActionState.Attack))
            {
                state.Action = ActionState.Attack;
                state.ActionTicksLeft = ATTACK_TICKS;
                state.TicksSinceAttack = 0;
            }

            // 6. Tích phân.
            state.X += state.VelX * dt;
            state.Y += state.VelY * dt;

            // 7. Va chạm với sàn phẳng.
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

            // 8. Biên ngang tạm.
            state.X = Math.Clamp(state.X, -WORLD_HALF_EXTENT, WORLD_HALF_EXTENT);

            return state;
        }
```

</details>

### ✅ CHECKPOINT B — luật chạy được, chưa cần nhìn thấy gì

Chưa có hình, nhưng luật thì kiểm được ngay — và kiểm bằng một hàm `Main` tạm rẻ hơn nhiều so với
mở Unity. Tạo tạm một console project (hoặc nhét vào đầu `Program.cs` của GameServer rồi xoá) chạy
`Step` bằng tay và in ra:

```csharp
var state = MoveState.AtRest(0f, 0f);
var idle = new MoveIntent();
var attack = new MoveIntent { Action = ActionRequest.Attack };

for (int tick = 0; tick < 20; tick++)
{
    // Bấm đánh ở tick 2 và tick 5 — cú thứ hai phải bị cooldown chặn.
    MoveIntent intent = tick == 2 || tick == 5 ? attack : idle;
    state = MovementRules.Step(state, intent, MovementRules.TICK_DT);

    Log.Info($"t={tick} action={state.Action} left={state.ActionTicksLeft} " +
             $"sinceAtk={state.TicksSinceAttack} loco={CharacterStates.Derive(state)}");
}
```

Phải thấy đúng bốn điều:

1. Tick 0–1: `action=None loco=Fall` rồi `Fall` → `Idle` khi chạm sàn.
2. Tick 2: `action=Attack left=5`. Tick 3→7 đếm ngược. Tick 7 về `None`.
3. Tick 5: bấm đánh lần hai **không có tác dụng** — `TicksSinceAttack` mới là 3, chưa tới 8.
4. `loco` vẫn là `Idle` suốt lúc đánh. Đó là hai tầng đang chạy độc lập: đánh không phải là một tư thế.

Nếu (2) cho `left=4` ngay tại tick bắt đầu → phép 0 đang chạy sau phép 5.
Nếu (3) lại đánh được → thiếu điều kiện cooldown, hoặc đặt `TicksSinceAttack = 0` sai chỗ.

---

## Bước 3 — Server: chốt nút đánh, và hai trạng thái client không được xin

### Hướng làm

**`Server/GameServer/World/PlayerEntity.cs`** — ba việc:

**(a) Hai loại input mới, hai cách chốt khác nhau.** Đây là chỗ ôn lại luật của Phase 8 và nó vẫn
đúng nguyên:

```csharp
private bool _intentCrouch;             // trục GIỮ  → ghi đè, lấy giá trị mới nhất
private ActionRequest _pendingAction;   // nút CẠNH  → giữ lại tới khi tiêu thụ
```

Chốt action không dùng được `|=` như `bool` (nó là enum), nhưng ý tưởng y hệt: **chỉ ghi khi có gì để
ghi, xoá khi tiêu thụ.**

```csharp
if (action != ActionRequest.None)
    _pendingAction = action;
```

Gói `{ Action: None }` đến sau gói `{ Action: Attack }` mà ghi đè thẳng thì cú đánh bốc hơi — đúng
kịch bản "thỉnh thoảng bấm mà không nhảy" của Phase 8, lần này là "thỉnh thoảng bấm mà không đánh".

**(b) `ForceAction(ActionState action, int ticks)`** — cửa để **server** đặt trạng thái, và là cửa duy
nhất. Nó vẫn phải hỏi `CanEnter`: server có quyền hơn client, nhưng không có quyền phá luật (gây
`hurt` cho một xác chết là vô nghĩa, và ở Phase 14 nó còn là chỗ để một con quái hồi sinh mục tiêu
bằng cách đánh nó).

Đặt `ForceAction` trên `PlayerEntity` chứ không rải rác trong `WorldService`: một chỗ duy nhất sửa
được tầng 2 thì sau này truy "ai bật `Die`" là đọc đúng một hàm.

**(c) `Integrate`** dựng `MoveIntent` đủ 4 field, tiêu thụ `_pendingAction` giống hệt `_pendingJump`.
Bộ đếm `_ticksSinceInput` (mất mạng thì thả phím) giờ xoá thêm `_intentCrouch` — nhưng **không** xoá
`_pendingAction`: cùng lý do như `_pendingJump`, mất mạng không phải cớ để nuốt một cú bấm đã xảy ra
thật.

**`Server/GameServer/Handlers/MoveHandler.cs`** — thêm một lớp kiểm mà lần đầu dự án gặp:

```csharp
if (!Enum.IsDefined(intent.Action))
    intent.Action = ActionRequest.None;
```

Vì sao cần, khi ta vừa nói "kiểu dữ liệu chặn hộ rồi"? Vì kiểu dữ liệu chặn **người viết code**, không
chặn **byte trên dây**. `ActionRequest` là `byte`; kẻ gian gửi số `77` thì MemoryPack dựng ra
`(ActionRequest)77` — hợp lệ hoàn toàn về mặt C#, `switch` không khớp nhánh nào, và tuỳ chỗ dùng mà nó
im lặng hoặc nổ. Enum trên dây **luôn** phải kiểm miền giá trị.

> Ranh giới đáng nhớ: kiểu dữ liệu bảo vệ *code của bạn* khỏi chính bạn. Kiểm miền giá trị bảo vệ
> *server của bạn* khỏi người khác. Hai việc khác nhau, cần cả hai.

**`Server/GameServer/World/WorldService.cs`** — hai chỗ:
- gửi `MoveState`: giờ chỉ còn `State = entity.State`, hết chép tay;
- dựng `EntityState`: thêm `Flags = EntityFlags.Pack(entity.State)` và `Action = (byte)entity.State.Action`.
- `Spawn` cũng điền `Flags`/`Action` vào `EntitySpawnNotice` — người mới lọt vào tầm nhìn phải hiện ra
  đã đúng hướng mặt, không quay đầu một nhịp sau.

**`Server/GameServer/Program.cs` — nút thử.** Tầng 2 có ba trạng thái nhưng mới một cái có nguồn phát
(client xin `Attack`). `Hurt` và `Die` chưa có ai gây ra — hệ thống sát thương là chuyện của Chặng D.
Không có nguồn phát thì không kiểm được, mà không kiểm được thì coi như chưa làm.

Thêm một vòng đọc phím trên thread riêng:

| Phím | Việc |
|---|---|
| `H` | mọi người trong world nhận `Hurt` |
| `K` | mọi người `Die` |
| `J` | mọi người về `None` (hồi sinh tạm) |

`Console.ReadKey` **chặn luồng** nên phải chạy trong `Task.Run`, và vì nó đụng vào `_entities` từ ngoài
luồng tick nên phải đi qua một hàng đợi lệnh — hoặc đơn giản hơn cho bản học: `lock` đúng chỗ duyệt.
Ghi rõ ranh giới luồng trong comment; đây là lần đầu có thứ ngoài tick sửa entity, và nó sẽ không phải
lần cuối.

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`Server/GameServer/World/PlayerEntity.cs`** (phần thay đổi):

```csharp
    // Trục giữ: handler ghi, tick đọc. Ghi đè là đúng — chỉ giá trị mới nhất có nghĩa.
    private float _intentDirX;
    private bool _intentCrouch;

    // Nút dạng CẠNH: chốt lại tới khi tick tiêu thụ. Client gửi 20 gói/s và server tick 20 lần/s
    // nhưng hai nhịp không khớp; ghi đè thì gói { Action: None } ngay sau gói { Action: Attack }
    // sẽ nuốt mất cú đánh — không lỗi, không log, chỉ là "thỉnh thoảng bấm mà không đánh".
    private bool _pendingJump;
    private ActionRequest _pendingAction;

    private int _ticksSinceInput;

    public void SetInput(int seq, in MoveIntent intent)
    {
        LastInputSeq = seq;

        _intentDirX = intent.DirX;
        _intentCrouch = intent.Crouch;

        _pendingJump |= intent.Jump;

        // Cùng ý với |= ở trên: chỉ ghi khi có gì để ghi, đừng để None xoá mất Attack vừa tới.
        if (intent.Action != ActionRequest.None)
            _pendingAction = intent.Action;

        _ticksSinceInput = 0;
    }

    /// <summary>
    /// Cửa DUY NHẤT để phía server đặt trạng thái hành động (trúng đòn, gục, hồi sinh).
    /// Vẫn phải hỏi luật: server có quyền hơn client nhưng không có quyền phá bảng chuyển tiếp —
    /// gây choáng cho một xác chết là vô nghĩa dù ai ra lệnh.
    /// Một chỗ duy nhất sửa được tầng 2 thì sau này truy "ai bật Die" là đọc đúng một hàm.
    /// </summary>
    public bool ForceAction(ActionState action, int ticks)
    {
        if (!CharacterStates.CanEnter(State.Action, State.ActionTicksLeft, action))
            return false;

        State.Action = action;
        State.ActionTicksLeft = ticks;
        return true;
    }

    public void Integrate(float dt)
    {
        // Quá 1 giây không có input mới → coi như đã thả phím. Chỉ xoá thứ dạng GIỮ; cú bấm dạng
        // cạnh đã chốt vẫn phải được tiêu thụ — mất mạng không phải cớ để nuốt input người chơi đã bấm.
        if (++_ticksSinceInput > MovementRules.TICK_RATE)
        {
            _intentDirX = 0f;
            _intentCrouch = false;
        }

        var intent = new MoveIntent
        {
            DirX = _intentDirX,
            Jump = _pendingJump,
            Crouch = _intentCrouch,
            Action = _pendingAction,
        };

        // Đọc-rồi-xoá: một lần bấm chỉ được dùng đúng một tick.
        _pendingJump = false;
        _pendingAction = ActionRequest.None;

        State = MovementRules.Step(State, intent, dt);
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

    MoveIntent intent = input.Intent;

    // NaN lây qua MỌI phép toán: lọt một lần là X/Y thành NaN vĩnh viễn và theo SavePosition vào
    // tận DB. Lưu ý NaN < -1 và NaN > 1 đều FALSE nên Clamp bên dưới không bắt được nó — thứ tự
    // hai phép này không đảo được.
    if (!float.IsFinite(intent.DirX))
        return Task.FromResult(NetResult.None);

    // Chống hack tốc độ: DirX = 10 là chạy nhanh gấp 10.
    intent.DirX = Math.Clamp(intent.DirX, -1f, 1f);

    // Enum trên dây chỉ là một byte do MÁY KHÁC gửi: (ActionRequest)77 hợp lệ hoàn toàn với C#,
    // không khớp nhánh nào và tuỳ chỗ dùng mà im lặng hoặc nổ. Kiểu dữ liệu bảo vệ code khỏi
    // chính mình; kiểm miền giá trị mới là thứ bảo vệ server khỏi người khác.
    if (!Enum.IsDefined(intent.Action))
        intent.Action = ActionRequest.None;

    // Jump và Crouch không cần kiểm: bool chỉ có hai giá trị. Gửi Jump = true mỗi tick cũng vô ích —
    // điều kiện coyote nằm trong MovementRules.Step, và Step chạy ở đây chứ không ở máy họ.
    entity.SetInput(input.Seq, intent);

    // Fire-and-forget: không trả lời từng input. Câu trả lời gộp là MoveState mỗi tick.
    return Task.FromResult(NetResult.None);
}
```

**`Server/GameServer/World/WorldService.cs`** — phần gửi trong `Tick`:

```csharp
    entity.Owner.SendData(NetCmd.MoveState, new MoveStateResponse
        {
            LastInputSeq = entity.LastInputSeq,

            // Trọn trạng thái, không tách field. Thêm field vào MoveState từ nay không phải
            // sờ vào dòng này — và cũng không thể quên nó.
            State = entity.State,
        }
    );
```

và phần dựng snapshot:

```csharp
    states[i] = new EntityState
    {
        EntityId = other.EntityId,
        X = other.State.X,
        Y = other.State.Y,
        Flags = EntityFlags.Pack(other.State),
        Action = (byte)other.State.Action,
    };
```

và `EntitySpawnNotice` trong `Spawn` thêm hai dòng tương ứng.

**`Server/GameServer/World/WorldService.cs`** — ba hàm cho nút thử:

```csharp
        /// <summary>
        /// Gây trạng thái cho TẤT CẢ entity đang trong world. Chỉ dùng cho nút thử trên console —
        /// nguồn phát thật của Hurt/Die là hệ thống sát thương.
        ///
        /// Gọi từ luồng đọc phím, tức NGOÀI luồng tick. Khoá quanh vòng duyệt để không đụng
        /// _entities giữa lúc tick đang thêm/xoá; đây là lần đầu có thứ ngoài tick sửa entity.
        /// </summary>
        public void ForceActionAll(ActionState action, int ticks)
        {
            lock (_entities)
            {
                foreach (PlayerEntity entity in _entities.Values)
                    entity.ForceAction(action, ticks);
            }
        }
```

(và `Tick` cũng phải `lock (_entities)` quanh vòng duyệt của nó — nếu chưa có.)

**`Server/GameServer/Program.cs`** — vòng đọc phím:

```csharp
// Console điều khiển. Console.ReadKey CHẶN luồng gọi nó, nên phải có luồng riêng —
// đặt trong vòng accept là treo cả server.
_ = Task.Run(() =>
{
    while (!cts.IsCancellationRequested)
    {
        switch (Console.ReadKey(intercept: true).Key)
        {
            case ConsoleKey.H:
                worldService.ForceActionAll(ActionState.Hurt, MovementRules.HURT_TICKS);
                Log.Info($"[thử] Toàn map {"HURT".Yellow()}");
                break;

            case ConsoleKey.K:
                worldService.ForceActionAll(ActionState.Die, MovementRules.DIE_TICKS);
                Log.Info($"[thử] Toàn map {"DIE".Red()}");
                break;

            case ConsoleKey.J:
                worldService.ForceActionAll(ActionState.None, 0);
                Log.Info($"[thử] Toàn map {"hồi sinh".Green()}");
                break;
        }
    }
});
```

Lưu ý `ForceActionAll(ActionState.None, 0)` đi qua `CanEnter`, mà `CanEnter` chặn mọi đường ra khỏi
`Die`. Nên hàm hồi sinh phải là đường **riêng**, không mượn `ForceAction`:

```csharp
        /// <summary>Đặt lại tầng action về None, bỏ qua bảng chuyển tiếp. Hồi sinh là quyết định
        /// hành chính của server, không phải một bước chuyển trạng thái trong luật chơi.</summary>
        public void ReviveAll()
        {
            lock (_entities)
            {
                foreach (PlayerEntity entity in _entities.Values)
                    entity.Revive();
            }
        }
```

với `PlayerEntity.Revive()` đặt thẳng `State.Action = ActionState.None; State.ActionTicksLeft = 0;`.

</details>

### ✅ CHECKPOINT C — tầng 2 chạy, nhìn bằng log

Client chưa sửa nên chưa thấy gì trên màn hình, nhưng server thì kiểm được. Thêm log tạm vào
`WorldService.Tick` (**xoá sau khi xong checkpoint**):

```csharp
if (entity.State.Action != ActionState.None)
    Log.Info($"{entity.Name} action={entity.State.Action} left={entity.State.ActionTicksLeft}");
```

1. Đăng nhập bằng client **cũ** (chưa sửa) — nó gửi `Intent` với `Action = None`, không sao.
2. Gõ `H` trong console server → thấy 4 dòng `action=Hurt left=3,2,1,0` rồi im. Đó là tầng 2 đi hết
   một vòng đời mà **client không tham gia gì cả** — đúng ý nghĩa của "server quyết".
3. Gõ `K` → `action=Die`, đếm về 0 rồi **ở lại `Die`**, không tự thoát.
4. Gõ `H` sau khi `K` → **không có gì xảy ra**. `CanEnter` đang chặn: 2 < 3 và `Die` không có đường ra.
5. Gõ `J` → về `None`, gõ `H` lại thì có tác dụng.

Bước (4) là bằng chứng bảng chuyển tiếp hoạt động. Nếu `H` sau `K` vẫn ăn thì `ForceAction` đang đặt
thẳng trạng thái mà quên hỏi `CanEnter`.

---

## Bước 4 — Client: `Animator` là máy chiếu phim, không phải nơi đặt luật

### Hướng làm

Phản xạ Unity chuẩn khi nghe "state machine nhân vật" là mở Animator Controller, kéo 8 state, nối 20
mũi tên transition, cắm `bool isGrounded`, `trigger doAttack`, `Has Exit Time`… **Đừng.**

Lý do không phải là "Animator dở", mà là:

| Luật chơi đặt trong Animator Controller | Luật chơi đặt trong `Shared` |
|---|---|
| Server không đọc được (nó là asset của Unity) | Server chạy đúng cùng một hàm |
| `Step` replay không tái hiện được | Replay chỉ việc gọi `Step` |
| Không viết được unit test | Là hàm thuần, test bằng vòng `for` |
| Sửa bằng cách kéo mũi tên, `git diff` ra YAML không đọc nổi | `git diff` ra vài dòng C# |
| Ai được cắt ngang ai nằm rải trong các ô "Conditions" | Nằm gọn trong `CanEnter` |

Ta **đã có** một state machine rồi — nó ở `MovementRules.Step` và `CharacterStates`, và nó chạy ở cả
hai đầu dây. Thêm một cái thứ hai bên trong Unity là có hai nguồn sự thật về cùng một câu hỏi, tức là
đúng thứ golden rule #4 cấm.

> `Animator` giữ đúng một việc: **phát đúng clip được bảo phát, với tốc độ được bảo phát.**
> Không transition, không parameter, không `Has Exit Time`. Nó là máy chiếu phim; kịch bản ở nơi khác.

**Dựng Animator Controller.** Mở `Assets/Game/Animations/DragonWarrior/dragon_warrior.controller`,
tạo 8 state **rời nhau, không một mũi tên nào**:

| State | Clip | Từ sprite | Loop |
|---|---|---|---|
| `idle` | `dw_idle` | `idle_*` (6 frame) | ✔ |
| `walk` | `dw_walk` | `walk_*` (6 frame) | ✔ |
| `jump` | `dw_jump` | `jump_01` (1 frame) | ✖ |
| `fall` | `dw_fall` | `jump_02` (1 frame) | ✖ |
| `crouch` | `dw_crouch` | `crouch_*` (5 frame) | ✖ |
| `attack` | `dw_attack` | `attack_*` (3 frame) | ✖ |
| `hurt` | `dw_hurt` | `hurt_*` (2 frame) | ✖ |
| `die` | `dw_die` | `die_*` (10 frame) | ✖ |

Chú ý `jump` và `fall`: bộ sprite chỉ có 2 frame `jump`, và đó là **hai tư thế khác nhau** (bốc lên /
rơi xuống) chứ không phải hai frame của một chuyển động. Tách thành hai clip 1 frame là đúng ý người
vẽ. Clip 1 frame nghe kỳ nhưng hoàn toàn hợp lệ.

Bảy nhóm còn lại (`crouch_ATK` `jumpATK` `strike` `flyKick` `dizzy` `win`) chưa dùng — xem "Để dành".

**File mới `Assets/Game/Scripts/World/CharacterAnimator.cs`** — dùng chung cho **cả** nhân vật của mình
lẫn nhân vật người khác. Một API duy nhất:

```csharp
public void Apply(LocomotionState locomotion, ActionState action, bool facingLeft)
```

Ba tham số, không hơn. Không có `Init`, không có phụ thuộc nào — nó không biết mạng là gì, không biết
ai là chủ nó. Đó là điều làm nó dùng được cho cả hai phía, và là lý do Bước 5 gần như không phải viết
gì thêm.

Bên trong:
- lật hình: `_spriteRenderer.flipX = facingLeft`;
- chọn clip: `action != None` → clip của action; ngược lại → clip của locomotion;
- **chỉ gọi `Play` khi clip đổi** — gọi mỗi frame là hoạt ảnh giậm chân tại frame 0 vĩnh viễn.

**Tốc độ phát: clip phải co cho vừa số tick.** Đây là chỗ trả nợ lời hứa ở Bước 2. Server nói "đòn
đánh dài 5 tick" = 0.25s. Clip `dw_attack` dài bao nhiêu là chuyện của người dựng clip. Nếu clip dài
0.4s thì hoạt ảnh **bị cắt ngọn** khi trạng thái hết; nếu dài 0.1s thì nó đứng hình chờ. Cả hai đều
sai, và cả hai đều "sửa được bằng cách chỉnh clip" — tức là mời người làm hình đi chỉnh cân bằng game.

Chữa bằng một dòng:

```csharp
_animator.speed = clip.length / (ticks * MovementRules.TICK_DT);
```

Và `ticks` lấy ở đâu? Không cần gửi qua mạng: `ATTACK_TICKS`, `HURT_TICKS`, `DIE_TICKS` là hằng số
trong `Shared`, **người xem tự biết**. Thêm một hàm tra vào `CharacterStates`:

```csharp
public static int DurationTicks(ActionState action)
```

Đây là phần thưởng thứ hai của việc đếm bằng tick: thời lượng là **kiến thức chung**, nên không tốn
byte nào để đồng bộ.

**`Assets/Game/Scripts/World/PlayerMotor.cs`** — bốn sửa đổi nhỏ:

1. Chốt nút đánh dạng cạnh y hệt nút nhảy: `_attackLatched` đặt trong `Update`, tiêu thụ trong `Step`.
   Đọc `WasPressedThisFrame` bên trong vòng tick là mất phần lớn cú bấm — bài cũ, lỗi cũ.
2. Đọc `Crouch` dạng **giữ**: `_inputActions.Player.Crouch.IsPressed()`. Trục giữ đọc trong vòng tick
   là đúng, khác hẳn nút cạnh — sai chỗ này là hiểu sai bài của Phase 8.
3. `Step` dựng `MoveIntent` đủ 4 field và gửi nguyên vẹn qua `WorldApi.Move(seq, intent)`.
4. Sau khi mô phỏng xong, đẩy sang animator: `_characterAnimator.Apply(CharacterStates.Derive(_simState), _simState.Action, _simState.FacingLeft)`.
   Gọi trong `Update` (mỗi frame) chứ không trong `Step` (mỗi tick) — hình ảnh chạy theo frame.

`Player.Attack` và `Player.Crouch` đã có sẵn trong `InputSystem_Actions` (mặc định: chuột trái và
phím `C`). Đổi binding cho hợp tay thì mở asset ra sửa, không cần code.

**Không có `NetCmd` mới ở phase này.** Đáng dừng lại một nhịp: checklist trong `CLAUDE.md` bắt đầu
bằng "thêm giá trị vào `NetCmd`", nhưng ở đây ta thêm hẳn một tầng trạng thái mà không thêm lệnh nào.

Vì đánh **không phải một sự kiện riêng**, nó là **một phần của ý định mỗi tick**. Gửi nó bằng một gói
riêng thì phải tự lo thứ tự (`Attack` tới trước hay sau `MoveInput` của cùng tick?) và tự lo việc
replay tìm lại nó. Đi chung `MoveInput` thì hai vấn đề đó không tồn tại.

Luật rút ra: **thứ nào có mặt ở mọi tick thì đi trong dòng input; thứ nào thỉnh thoảng mới xảy ra và
không liên quan tới tick nào cả (mở túi đồ, gửi tin nhắn) thì mới xứng đáng một `NetCmd` riêng.**

(Ghi chú cho tương lai: đi chung dòng input an toàn vì transport là **TCP** — không mất gói. Ngày nào
đổi sang UDP thì nút dạng cạnh phải được gửi kèm vài tick liên tiếp, vì mất đúng gói mang nó là mất
cú bấm.)

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`Server/Shared/World/CharacterStates.cs`** — thêm hàm tra thời lượng:

```csharp
        /// <summary>
        /// Thời lượng của một hành động, tính bằng tick. Là kiến thức CHUNG: nhờ vậy người xem
        /// tự biết đòn đánh của người khác dài bao lâu mà không cần gửi thêm byte nào.
        /// </summary>
        public static int DurationTicks(ActionState action)
        {
            switch (action)
            {
                case ActionState.Attack:
                    return MovementRules.ATTACK_TICKS;

                case ActionState.Hurt:
                    return MovementRules.HURT_TICKS;

                case ActionState.Die:
                    return MovementRules.DIE_TICKS;

                default:
                    return 0;
            }
        }
```

**`Assets/Game/Scripts/World/CharacterAnimator.cs`** (file mới):

```csharp
using System;
using System.Collections.Generic;
using MMORPG.Shared.World;
using UnityEngine;

namespace MMORPG.Client.World
{
    /// <summary>
    /// Chiếu đúng clip ứng với trạng thái nhân vật. Không giữ luật, không quyết định gì:
    /// nhận (tư thế, hành động, hướng mặt) rồi phát. Nhờ vậy dùng chung được cho nhân vật của
    /// mình lẫn nhân vật người khác — hai nguồn dữ liệu hoàn toàn khác nhau, cùng một cách vẽ.
    ///
    /// Animator Controller ở đây cố tình KHÔNG có transition nào: bảng chuyển tiếp đã nằm trong
    /// CharacterStates và chạy ở cả hai đầu dây. Dựng thêm một máy trạng thái nữa bên trong Unity
    /// là có hai nguồn sự thật cho cùng một câu hỏi, và cái thứ hai thì server không đọc được.
    /// </summary>
    public sealed class CharacterAnimator : MonoBehaviour
    {
        [Serializable]
        private struct LocomotionClip
        {
            public LocomotionState State;
            public AnimationClip Clip;
        }

        [Serializable]
        private struct ActionClip
        {
            public ActionState Action;
            public AnimationClip Clip;
        }

        [SerializeField] private Animator _animator;
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private LocomotionClip[] _locomotionClips;
        [SerializeField] private ActionClip[] _actionClips;

        // Tên state trong Animator Controller trùng tên clip (mặc định khi kéo clip vào controller).
        // Băm sẵn ra hash một lần: Animator.Play nhận hash, còn StringToHash mỗi frame là phí.
        private readonly Dictionary<LocomotionState, int> _locomotionHashes = new();
        private readonly Dictionary<ActionState, int> _actionHashes = new();
        private readonly Dictionary<ActionState, float> _actionLengths = new();

        /// <summary>Clip đang phát. Chỉ gọi Animator.Play khi giá trị này đổi — gọi mỗi frame thì
        /// hoạt ảnh bị đặt lại về frame 0 liên tục và đứng hình.</summary>
        private int _currentHash;

        private void Awake()
        {
            foreach (LocomotionClip entry in _locomotionClips)
                _locomotionHashes[entry.State] = Animator.StringToHash(entry.Clip.name);

            foreach (ActionClip entry in _actionClips)
            {
                _actionHashes[entry.Action] = Animator.StringToHash(entry.Clip.name);
                _actionLengths[entry.Action] = entry.Clip.length;
            }
        }

        /// <summary>
        /// Cập nhật hình theo trạng thái. Gọi mỗi frame; nó tự lọc những lần không có gì đổi.
        /// </summary>
        public void Apply(LocomotionState locomotion, ActionState action, bool facingLeft)
        {
            _spriteRenderer.flipX = facingLeft;

            // Tầng 2 đè tầng 1: đang đánh thì vẽ đánh, dù chân vẫn đang chạy.
            if (action != ActionState.None)
            {
                PlayAction(action);
                return;
            }

            PlayLocomotion(locomotion);
        }

        private void PlayLocomotion(LocomotionState locomotion)
        {
            if (!_locomotionHashes.TryGetValue(locomotion, out int hash) || hash == _currentHash)
                return;

            _currentHash = hash;
            _animator.speed = 1f;
            _animator.Play(hash, 0, 0f);
        }

        private void PlayAction(ActionState action)
        {
            if (!_actionHashes.TryGetValue(action, out int hash) || hash == _currentHash)
                return;

            _currentHash = hash;

            // Co clip cho vừa số tick mà LUẬT quy định, thay vì để độ dài clip quyết định luật.
            // Không co thì clip dài hơn bị cắt ngọn giữa chừng, clip ngắn hơn đứng hình chờ —
            // và cách "sửa" hiển nhiên là đi chỉnh clip, tức là mời người làm hình chỉnh cân bằng game.
            int ticks = CharacterStates.DurationTicks(action);
            float wanted = ticks * MovementRules.TICK_DT;

            _animator.speed = wanted > 0f ? _actionLengths[action] / wanted : 1f;
            _animator.Play(hash, 0, 0f);
        }
    }
}
```

**`Assets/Game/Scripts/World/WorldApi.cs`**:

```csharp
        public void Move(int seq, in MoveIntent intent)
        {
            // Không log ở đây — 20 lần/giây, log là dìm chết console.
            _netService.Send(NetCmd.MoveInput, new MoveInputRequest { Seq = seq, Intent = intent });
        }
```

**`Assets/Game/Scripts/World/PlayerMotor.cs`** (phần thay đổi):

```csharp
        [SerializeField] private CharacterAnimator _characterAnimator;

        /// <summary>Cú bấm nhảy đang chờ tick tới tiêu thụ.</summary>
        private bool _jumpLatched;

        /// <summary>Cú bấm đánh đang chờ tick tới tiêu thụ — cùng lý do như _jumpLatched:
        /// Update chạy 60–300Hz còn Step chỉ 20Hz, đọc WasPressedThisFrame trong vòng tick là mất bấm.</summary>
        private bool _attackLatched;

        private MoveState _simState;

        /// <summary>Trạng thái mô phỏng hiện tại — nguồn để vẽ hình cho chính mình.</summary>
        public MoveState SimState => _simState;

        private void Update()
        {
            if (_worldApi == null)
                return;

            // Nút dạng CẠNH: chốt ngay tại frame nó xảy ra.
            if (_inputActions.Player.Jump.WasPressedThisFrame())
                _jumpLatched = true;

            if (_inputActions.Player.Attack.WasPressedThisFrame())
                _attackLatched = true;

            float dirX = Mathf.Clamp(_inputActions.Player.Move.ReadValue<Vector2>().x, -1f, 1f);

            // Trục GIỮ: đọc mức tại thời điểm tick là đúng — không chốt, không gộp.
            bool crouch = _inputActions.Player.Crouch.IsPressed();

            _accumulator += Time.deltaTime;
            while (_accumulator >= MovementRules.TICK_DT)
            {
                _accumulator -= MovementRules.TICK_DT;
                Step(dirX, crouch);
            }

            transform.position = Vector3.MoveTowards(
                transform.position, new Vector3(_simState.X, _simState.Y, 0f),
                MovementRules.MAX_FALL_SPEED * 1.5f * Time.deltaTime
            );

            // Hình chạy theo FRAME, không theo tick — vẽ ở đây chứ không trong Step.
            _characterAnimator.Apply(
                CharacterStates.Derive(_simState), _simState.Action, _simState.FacingLeft);
        }

        /// <summary>Một bước dự đoán: mô phỏng trước, ghi nợ, gửi lên server.</summary>
        private void Step(float dirX, bool crouch)
        {
            int seq = ++_nextSeq;

            var intent = new MoveIntent
            {
                DirX = dirX,
                Jump = _jumpLatched,
                Crouch = crouch,
                Action = _attackLatched ? ActionRequest.Attack : ActionRequest.None,
            };

            // Tiêu thụ ngay: một lần bấm sinh đúng một MoveIntent mang nó.
            _jumpLatched = false;
            _attackLatched = false;

            _simState = MovementRules.Step(_simState, intent, MovementRules.TICK_DT);

            _pending.Add(new PendingInput(seq, intent));
            _worldApi.Move(seq, intent);
        }

        private void OnMoveStateResult(MoveStateResponse response)
        {
            _pending.RemoveAll(p => p.Seq <= response.LastInputSeq);

            // Trọn trạng thái server, không nhặt từng field. Hoạt ảnh cũng nằm trong đó, nên nó
            // được đối chiếu bằng đúng cơ chế đã có — không cần đường đồng bộ riêng cho hình ảnh.
            MoveState state = response.State;

            foreach (PendingInput pending in _pending)
            {
                state = MovementRules.Step(state, pending.Intent, MovementRules.TICK_DT);
            }

            _simState = state;
        }
```

</details>

**Mở rộng miễn phí (làm hay không tuỳ bạn).** Bộ sprite có `crouch_ATK` và `jumpATK` — đánh trong tư
thế ngồi và đánh trên không. Server **không cần biết** chúng tồn tại: nó vẫn chỉ gửi `Action = Attack`.
Client tự chọn clip theo **cặp** (tư thế, hành động):

```csharp
        private int ResolveActionHash(LocomotionState locomotion, ActionState action)
        {
            if (action == ActionState.Attack && locomotion == LocomotionState.Crouch)
                return _crouchAttackHash;

            if (action == ActionState.Attack &&
                (locomotion == LocomotionState.Jump || locomotion == LocomotionState.Fall))
                return _jumpAttackHash;

            return _actionHashes[action];
        }
```

Ba nhánh, **0 byte thêm trên dây, 0 dòng sửa ở server**. Đây là món lãi cụ thể của việc chia hai tầng:
tầng 1 và tầng 2 độc lập nhau nên *tổ hợp* của chúng miễn phí. Nếu ban đầu làm một enum phẳng 13 giá
trị thì `crouch_ATK` sẽ là một giá trị enum mới → một số mới trên dây → sửa cả server.

### ✅ CHECKPOINT D — nhân vật của mình đã biết diễn

Một client:

1. Đứng yên → `idle` lặp. Chạy → `walk`. Bấm nhảy → `jump` lúc bay lên, đổi sang `fall` ở đỉnh.
2. Chạy sang trái → nhân vật **lật mặt**. Dừng lại → **giữ nguyên hướng đang nhìn**, không quay về mặc định.
3. Bấm `C` → `crouch`, và nhân vật **đứng im dù vẫn giữ A/D**. Thả `C` → đứng dậy, đi tiếp.
4. Bấm chuột trái → `attack` chạy đúng ~0.25s rồi tự về `idle`/`walk`. Bấm dồn dập → chỉ ăn 1 đòn mỗi
   0.4s, phần bấm thừa rơi vào hư không (đúng — cooldown ở server).
5. Đang đánh mà bấm A/D → nhân vật **vẫn chạy** nhưng **không quay đầu**. Đó là khoá hướng của phép 2.
6. Gõ `H` ở console server → thấy `hurt`, và trong 0.2s đó **bấm gì cũng vô ích**.
7. Gõ `K` → `die`, nằm im vĩnh viễn. Gõ `J` → đứng dậy.

Bước (6) và (7) là bằng chứng quan trọng nhất của cả phase: **client vừa hiển thị một trạng thái mà
nó không hề xin, không hề tự tính, và không có cách nào từ chối.** Đó là "server là source of truth"
áp lên hình ảnh.

---

## Bước 5 — Người khác: suy tư thế từ hai mẫu nội suy

### Hướng làm

`RemotePlayerView` của Phase 7 giữ một buffer mẫu `(thời điểm, vị trí)` và nội suy giữa hai mẫu kẹp
`renderTime`. Nó đã có sẵn **đúng thứ cần** cho tầng 1: hai vị trí liên tiếp và khoảng thời gian giữa
chúng — tức là vận tốc.

**Mở rộng `Sample`** thành `(Time, Pos, Flags, Action)` và `PushState` nhận thêm hai byte.

**Suy tư thế từ hai mẫu đang nội suy:**

```
Δ = b.Pos - a.Pos
|Δy| > EPS   →  Δy > 0 ? Jump : Fall
crouch flag  →  Crouch
|Δx| > EPS   →  Walk
ngược lại    →  Idle
```

Thứ tự giống hệt `CharacterStates.Derive` — vì nó **là** `Derive`, chỉ khác nguồn dữ liệu. Cách gọn
nhất và ít cơ hội lệch nhất: dựng một `MoveState` giả từ hai mẫu rồi gọi thẳng `Derive`:

```csharp
var fake = new MoveState
{
    VelX = delta.x / dt,
    VelY = delta.y / dt,
    Grounded = Mathf.Abs(delta.y) < EPS,
    Crouching = EntityFlags.Has(a.Flags, EntityFlags.CROUCHING),
};
LocomotionState locomotion = CharacterStates.Derive(fake);
```

Xấu về mặt thẩm mỹ (một `MoveState` chỉ điền 4 field) nhưng **đúng về mặt kiến trúc**: định nghĩa
"tư thế là gì" vẫn chỉ có một bản. Thêm `LocomotionState` mới ở `Shared` là người khác tự đúng theo,
không cần ai nhớ đi sửa chỗ thứ hai.

Vì sao `Grounded` suy được từ `Δy == 0`: sàn phẳng ở `GROUND_Y`, và phép 7 của `Step` **gán thẳng**
`Y = GROUND_Y` nên hai tick liên tiếp trên mặt đất cho **đúng** cùng một số float. Trên không thì
trọng lực bảo đảm `Y` đổi mỗi tick. Cần `EPS` chứ không so `== 0` vì hai mẫu có thể là hai tick không
liền nhau (mạng dồn gói) — nhưng ngưỡng để rất nhỏ (`0.0001f`) là đủ.

**Chỗ này sai được, và sai thì không sao.** Nếu server gửi trùng một vị trí hai lần (mạng nghẽn rồi
dồn), người xem sẽ thấy `Δy = 0` giữa lúc đối phương đang bay và vẽ nhầm `idle` **một frame**.

> Biểu diễn được phép đoán. Mô phỏng thì không.
> Đoán sai một frame trên hoạt ảnh của người khác không ai nhận ra; đoán sai một tick trong `Step` là
> rubber-band. Đây là lý do việc suy tư thế nằm ở tầng 1 chứ không phải tầng 2 — và là lý do
> `Crouching` với `FacingLeft` **không** được đoán: chúng sai thì sai **kéo dài**, không phải một frame.

**Cờ và action lấy từ mẫu nào?** Từ mẫu `a` — mẫu đang được vẽ tại `renderTime`. Lấy mẫu mới nhất
(`_buffer[^1]`) là để hình chạy trước vị trí `INTERP_DELAY = 0.15s`: nhân vật vung tay ở chỗ nó chưa
đứng tới.

**`WorldSpawner`** truyền hai byte mới ở cả `OnEntitySpawn` lẫn `OnSnapshot`. Đây chính là chỗ
CHECKPOINT A đã cảnh báo: thêm field vào DTO không làm bên đọc đỏ, nên phải tự nhớ.

**Prefab.** `Player_Remote` cần đúng bộ `Animator` + `CharacterAnimator` như `Player_Main`. Cách rẻ
nhất là tách phần nhìn thấy được (`SpriteRenderer` + `Animator` + `CharacterAnimator`) thành một prefab
con `DragonWarriorView`, rồi cả hai prefab cùng nhúng nó. Sửa hoạt ảnh một lần, hai bên cùng đổi —
cùng tinh thần "một nguồn", lần này ở tầng asset.

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`Assets/Game/Scripts/World/RemotePlayerView.cs`**:

```csharp
using System.Collections.Generic;
using MMORPG.Shared.Dto.World;
using MMORPG.Shared.World;
using UnityEngine;

namespace MMORPG.Client.World
{
    /// <summary>
    /// Hiển thị nhân vật của NGƯỜI KHÁC: nhận vị trí rời rạc từ snapshot, vẽ mượt bằng nội suy,
    /// và suy tư thế từ chính chuỗi vị trí đó.
    /// Luôn vẽ trễ INTERP_DELAY so với gói mới nhất — đổi độ trễ lấy sự chắc chắn không phải đoán.
    /// </summary>
    public class RemotePlayerView : MonoBehaviour
    {
        private const float INTERP_DELAY = 0.15f;
        private const float BUFFER_KEEP = 1f;

        /// <summary>
        /// Ngưỡng coi hai mẫu là "không dịch chuyển". Không so == 0 vì hai mẫu có thể cách nhau
        /// hơn một tick khi mạng dồn gói; nhưng để rất nhỏ, vì trên sàn phẳng Y được gán thẳng
        /// bằng GROUND_Y nên hai tick đứng yên cho đúng cùng một số float.
        /// </summary>
        private const float EPS = 0.0001f;

        [SerializeField] private CharacterAnimator _characterAnimator;

        private readonly struct Sample
        {
            public readonly float Time;
            public readonly Vector2 Pos;
            public readonly byte Flags;
            public readonly ActionState Action;

            public Sample(float time, Vector2 pos, byte flags, byte action)
            {
                Time = time;
                Pos = pos;
                Flags = flags;
                Action = (ActionState)action;
            }
        }

        private readonly List<Sample> _buffer = new();

        /// <summary>Gọi mỗi lần snapshot đến. Mốc thời gian là đồng hồ MÁY MÌNH lúc nhận.</summary>
        public void PushState(Vector2 pos, byte flags, byte action)
        {
            _buffer.Add(new Sample(Time.time, pos, flags, action));

            while (_buffer.Count > 2 && _buffer[0].Time < Time.time - BUFFER_KEEP)
                _buffer.RemoveAt(0);
        }

        private void Update()
        {
            if (_buffer.Count == 0)
                return;

            float renderTime = Time.time - INTERP_DELAY;

            if (renderTime <= _buffer[0].Time)
            {
                transform.position = _buffer[0].Pos;
                Draw(_buffer[0], _buffer[0], 1f);
                return;
            }

            for (int i = 0; i < _buffer.Count - 1; i++)
            {
                Sample a = _buffer[i];
                Sample b = _buffer[i + 1];

                if (renderTime > b.Time)
                    continue;

                float t = (renderTime - a.Time) / (b.Time - a.Time);
                transform.position = Vector2.Lerp(a.Pos, b.Pos, t);
                Draw(a, b, b.Time - a.Time);
                return;
            }

            // Qua cả mẫu cuối: mạng đang nghẽn, KHÔNG đoán tiếp — đứng ở vị trí chắc chắn cuối cùng.
            Sample last = _buffer[^1];
            transform.position = last.Pos;
            Draw(last, last, 1f);
        }

        /// <summary>
        /// Suy tư thế từ hai mẫu ĐANG được nội suy rồi giao cho animator.
        ///
        /// Dựng một MoveState chỉ điền 4 field trông xấu, nhưng đó là cái giá để định nghĩa
        /// "tư thế là gì" vẫn chỉ có MỘT bản, ở Shared: thêm một LocomotionState mới thì người
        /// khác tự đúng theo, không ai phải nhớ đi sửa chỗ thứ hai.
        ///
        /// Cờ và hành động lấy từ mẫu a — mẫu đang được vẽ. Lấy mẫu mới nhất là để hoạt ảnh chạy
        /// trước vị trí 0.15 giây: nhân vật vung tay ở chỗ nó chưa đứng tới.
        /// </summary>
        private void Draw(in Sample a, in Sample b, float dt)
        {
            Vector2 delta = b.Pos - a.Pos;

            var sampled = new MoveState
            {
                VelX = delta.x / dt,
                VelY = delta.y / dt,

                // Trên sàn phẳng, hai tick liên tiếp cho đúng cùng một Y. Trên không thì trọng lực
                // bảo đảm Y đổi mỗi tick. Suy sai ở đây chỉ hỏng MỘT frame hoạt ảnh của người khác —
                // biểu diễn được phép đoán, mô phỏng thì không.
                Grounded = Mathf.Abs(delta.y) < EPS,

                Crouching = EntityFlags.Has(a.Flags, EntityFlags.CROUCHING),
            };

            _characterAnimator.Apply(
                CharacterStates.Derive(sampled),
                a.Action,
                EntityFlags.Has(a.Flags, EntityFlags.FACING_LEFT));
        }
    }
}
```

**`Assets/Game/Scripts/World/WorldSpawner.cs`** — hai chỗ truyền thêm byte:

```csharp
        private void OnEntitySpawn(EntitySpawnNotice notice)
        {
            if (notice.EntityId == _localPlayer.EntityId || _remotes.ContainsKey(notice.EntityId))
                return;

            GameObject remote = Instantiate(_remotePrefab, new Vector3(notice.X, notice.Y, 0f),
                                            Quaternion.identity, _entityRoot);
            remote.name = $"Remote_{notice.EntityId}_{notice.Name}";

            var view = remote.GetComponent<RemotePlayerView>();
            view.PushState(new Vector2(notice.X, notice.Y), notice.Flags, notice.Action);

            _remotes[notice.EntityId] = view;
        }

        private void OnSnapshot(WorldSnapshotNotice snapshot)
        {
            foreach (EntityState state in snapshot.States)
            {
                if (state.EntityId == _localPlayer.EntityId)
                    continue;

                if (!_remotes.TryGetValue(state.EntityId, out RemotePlayerView view))
                    continue;

                view.PushState(new Vector2(state.X, state.Y), state.Flags, state.Action);
            }
        }
```

</details>

### ✅ CHECKPOINT E — mục tiêu cuối Phase 9

Hai client bằng ParrelSync:

1. A chạy → B thấy A `walk`, đúng hướng. A dừng → B thấy A `idle`, **giữ hướng cũ**.
2. A nhảy → B thấy `jump` lúc lên, `fall` lúc xuống, và tiếp đất là về `idle` ngay chứ không kẹt ở `fall`.
3. A ngồi (`C`) → B thấy `crouch`. Đây là thứ **không suy được** — nó tới từ 1 bit trong `Flags`.
   Thử tạm bỏ bit `CROUCHING` khỏi `Pack` để thấy: A ngồi mà B thấy A đứng. Trả lại code.
4. A đánh → B thấy `attack` đúng độ dài, đúng hướng A đang nhìn, rồi về `idle`. Không có gói `NetCmd`
   mới nào tham gia — mở log ra kiểm nếu không tin.
5. Gõ `K` ở console server → **cả hai màn hình** đều thấy **cả hai nhân vật** gục. Gõ `J` → cùng đứng dậy.
6. A đứng yên quay mặt sang trái rồi **thoát và vào lại** → B thấy A hiện ra đã quay trái sẵn (nhờ
   `Flags` trong `EntitySpawnNotice`), không quay đầu một nhịp sau.

Bước (5) đáng ăn mừng: một sự kiện phát sinh hoàn toàn ở server, không client nào xin, đã đi qua đúng
đường ống có sẵn (`Step` → `MoveState`/snapshot → animator) tới hai màn hình khác nhau và khớp nhau.
Đó là toàn bộ mục đích của kiến trúc này.

---

## Ba thử nghiệm bắt buộc

Làm đủ ba. Mỗi cái dạy một thứ mà đọc doc không ra.

**1. Cho client tự bật `Hurt` và xem nó sống được bao lâu.**
Thêm tạm vào `PlayerMotor.Update`:

```csharp
if (Keyboard.current.xKey.wasPressedThisFrame)
{
    _simState.Action = ActionState.Hurt;
    _simState.ActionTicksLeft = MovementRules.HURT_TICKS;
}
```

Bấm `X`. Nhân vật của bạn co rúm lại… rồi **bật dậy sau chưa tới 1/20 giây**. Gói `MoveState` kế tiếp
mang `Action = None` của server, replay chạy từ đó, và lời nói dối bị xoá sạch — không cần một dòng
code chống gian lận nào cả. Bây giờ hỏi tiếp câu quan trọng: **người chơi khác có bao giờ thấy bạn
`hurt` không?** Mở client thứ hai mà xem. Không. Vì `Action` họ nhận đến từ `entity.State` ở server,
không từ máy bạn.

Đây là hình ảnh trực quan nhất của golden rule #2 trong toàn bộ dự án. Trả code về như cũ.

**2. Gửi một giá trị enum không tồn tại.**
Sửa tạm `PlayerMotor.Step` thành `Action = (ActionRequest)77`. Chạy. Server không nổ, không đánh —
`Enum.IsDefined` đã đổi nó về `None`.

Giờ **xoá tạm** dòng `Enum.IsDefined` ở `MoveHandler` và chạy lại. Vẫn không nổ, vẫn không đánh: phép 5
so `== ActionRequest.Attack` nên `77` rơi ra ngoài. Vậy dòng kiểm ấy vô dụng?

Không — nó vô dụng **hôm nay**. Viết thử một `switch (intent.Action)` có `default: throw` xem, hoặc
tưởng tượng ngày `Action` được dùng làm chỉ số vào một mảng cấu hình đòn đánh. Bài học không phải là
"dòng đó cứu bạn hôm nay" mà là: **mọi giá trị đến từ dây đều phải được đưa về miền hợp lệ ngay tại
cửa**, trước khi nó kịp lan vào trong nơi mọi người đều giả định nó sạch. Trả cả hai chỗ về như cũ.

**3. Đổi `ATTACK_TICKS` từ 5 thành 30 và không đụng vào clip.**
Build lại `Shared`, chơi. Đòn đánh giờ kéo 1.5 giây và clip `attack` **tự chậm lại cho vừa**, không bị
lặp, không đứng hình. Đổi tiếp thành 2 → đòn đánh nhoáng qua, clip chạy vống lên.

Rồi làm ngược lại: giữ `ATTACK_TICKS = 5` nhưng vào Unity chỉnh sample rate của clip `dw_attack` cho
nó dài gấp đôi. **Không có gì thay đổi trên màn hình.** Đó là điều đáng nhớ: người làm hình sửa clip
thoải mái mà không đụng được vào cân bằng game; người làm luật sửa một hằng số là cả hai đầu dây đổi
theo. Ranh giới đúng chỗ.

---

## Troubleshooting

| Triệu chứng | Nguyên nhân thường gặp | Chỗ sửa |
|---|---|---|
| Hoạt ảnh đứng ở frame 0, giật liên tục | Gọi `Animator.Play` mỗi frame thay vì chỉ khi clip đổi | `CharacterAnimator` — kiểm `hash == _currentHash` |
| Đòn đánh bị cắt ngọn / đứng hình chờ | Quên `_animator.speed`, hoặc `DurationTicks` trả 0 cho action đó | `CharacterAnimator.PlayAction` |
| Đánh xong nhân vật kẹt ở `attack` mãi | Phép 0 không đưa `Action` về `None` khi hết ticks | `MovementRules.Step` phép 0 |
| Xác chết đứng dậy sau 1 giây | Phép 0 thiếu nhánh loại trừ `Die` | `MovementRules.Step` phép 0 |
| Đòn đánh chỉ dài 4 tick thay vì 5 | Phép 0 chạy **sau** phép 5 | Thứ tự trong `Step` |
| Bấm đánh dồn dập thì nhân vật đánh không ngừng | Thiếu điều kiện cooldown, hoặc `TicksSinceAttack = 0` đặt nhầm chỗ | `MovementRules.Step` phép 5 |
| Thỉnh thoảng bấm đánh mà không đánh | `_pendingAction = intent.Action` ghi đè thay vì chỉ ghi khi khác `None`; hoặc đọc `WasPressedThisFrame` trong vòng tick | `PlayerEntity.SetInput` · `PlayerMotor.Update` |
| Đánh xong hoạt ảnh nháy một cái rồi mới về `idle` | `_currentHash` không được cập nhật khi đổi giữa hai action | `CharacterAnimator` |
| Cú đánh biến mất rồi hiện lại liên tục | `Attack` đang được đặt ngoài `Step`, nên replay không tái hiện được | Chuyển vào phép 5 của `Step` |
| Nhân vật quay đầu giữa lúc vung tay | Thiếu điều kiện `Action == None` ở phép cập nhật `FacingLeft` | `MovementRules.Step` phép 2 |
| Đứng yên là nhân vật tự quay về phải | Đang suy `FacingLeft` từ `VelX` mỗi tick thay vì giữ trạng thái cũ | `MovementRules.Step` phép 2 |
| Ngồi mà vẫn chạy được | Phép 2 không ép `VelX = 0` khi `Crouching` | `MovementRules.Step` phép 2 |
| Bấm `C` giữa không trung là ngồi luôn | Thiếu điều kiện `state.Grounded` ở phép 1 | `MovementRules.Step` phép 1 |
| Người khác không bao giờ hiện `crouch` | Quên bit `CROUCHING` trong `EntityFlags.Pack`, hoặc `WorldSpawner` chưa truyền `Flags` | `WorldService` · `WorldSpawner` |
| Người khác luôn quay mặt phải | Như trên, với bit `FACING_LEFT`; hoặc `flipX` gán nhầm dấu | `CharacterAnimator.Apply` |
| Người khác lúc nào cũng `idle` dù đang chạy | `Draw` không được gọi, hoặc `dt` truyền vào bằng 0 → `VelX` thành NaN/Infinity | `RemotePlayerView.Update` |
| Người khác kẹt ở `fall` sau khi tiếp đất | `EPS` quá nhỏ so với sai số, hoặc lấy mẫu `_buffer[^1]` thay vì hai mẫu đang nội suy | `RemotePlayerView.Draw` |
| Người khác vung tay ở chỗ họ chưa đứng tới | Lấy `Flags`/`Action` từ mẫu mới nhất chứ không từ mẫu đang vẽ | `RemotePlayerView.Draw` |
| Nhân vật đứng im hoàn toàn sau khi build `Shared` | Unity còn dùng DLL cũ — post-build chưa copy sang `Assets/Plugins/Shared/` | Build lại `Server/Shared` |
| `MemoryPack` báo lỗi lúc chạy về `MoveState` | Quên `[MemoryPackable]` hoặc quên `partial` trên struct | `MoveState.cs` |
| Gõ `H` ở console mà server đơ | `Console.ReadKey` gọi thẳng trong luồng chính chứ không trong `Task.Run` | `Program.cs` |

---

## Tự kiểm tra hiểu bài

Tự trả lời từng câu xong mới mở đáp án của câu đó.

**Câu 1.** Doc nói tầng locomotion "tốn 0 byte", nhưng `EntityState` vẫn phải thêm 2 byte. Mâu thuẫn ở
đâu, và phát biểu cho đúng thì phải nói thế nào?
<details>
<summary><b>📖 Đáp án câu 1</b></summary>

Không mâu thuẫn, mà là phát biểu thiếu chính xác. Đúng phải là: **tầng locomotion không cần byte nào
cho riêng nó** — 5 giá trị `Idle/Walk/Jump/Fall/Crouch` không bao giờ được gửi. Hai byte thêm vào là
cho *nguyên liệu* mà việc suy không tự có: `Crouching` (ngồi và đứng yên cho cùng một chuỗi vị trí nên
không phân biệt được) và `FacingLeft` (có trí nhớ), cộng với `Action` vốn thuộc tầng 2.

Với nhân vật của **chính mình** thì con số 0 là đúng tuyệt đối: `MoveState` đã phải gửi trọn vì
reconciliation, nên suy tư thế từ nó là hoàn toàn miễn phí.

</details>

**Câu 2.** Vì sao dùng **hai** enum `ActionRequest` / `ActionState` thay vì một enum chung rồi chặn
`Hurt`/`Die` bằng `if` ở `MoveHandler`? Cả hai đều chạy đúng hôm nay.
<details>
<summary><b>📖 Đáp án câu 2</b></summary>

Vì hai cách khác nhau ở chỗ **ai chịu trách nhiệm nhớ**. Bản `if` đúng chừng nào còn có người nhớ cập
nhật danh sách chặn mỗi lần thêm giá trị mới — thêm `Stun` mà quên thì client xin được `Stun`, và
không có lỗi biên dịch nào báo. Bản hai enum thì "client xin `Hurt`" là câu **không viết ra được**:
trình biên dịch từ chối, mỗi lần build, vĩnh viễn, không cần ai nhớ gì.

Nguyên tắc tổng quát: khi tập giá trị hợp lệ của "thứ client gửi lên" khác với "thứ server giữ", đó
là hai **kiểu** khác nhau chứ không phải một kiểu cộng một phép kiểm.

</details>

**Câu 3.** `Attack` được đặt bên trong `MovementRules.Step`, còn `Hurt`/`Die` thì đặt từ bên ngoài
(`ForceAction`). Vì sao không thống nhất một chỗ?
<details>
<summary><b>📖 Đáp án câu 3</b></summary>

Vì hai thứ khác nhau về bản chất và ranh giới là: *sự kiện rời rạc thì đặt từ ngoài, nhịp thời gian
thì ở trong*.

`Attack` sinh ra từ **input của client**, mà input thì client có bản sao và **replay lại mỗi lần
reconciliation**. Đặt nó ngoài `Step` thì vòng replay không tái hiện được, và cú đánh sẽ biến mất rồi
hiện lại mỗi lần gói `MoveState` về — đúng bug "quên `Jump` trong `PendingInput`" của Phase 8.

`Hurt`/`Die` sinh ra từ sự kiện chỉ server thấy; client không có nguyên liệu để replay chúng và cũng
không được phép. Chúng đến qua kênh `MoveState` như một **sự thật đã rồi**.

Nhưng phần *diễn tiến* của cả ba (đếm ngược, hết hạn, ai cắt ngang ai) thì vẫn nằm trọn trong `Step` —
vì `Step` là định nghĩa của "một tick trôi qua", và replay chỉ biết gọi `Step`.

</details>

**Câu 4.** Nêu ba lý do độc lập vì sao thời lượng một hành động đếm bằng **tick** chứ không bằng độ
dài `AnimationClip`.
<details>
<summary><b>📖 Đáp án câu 4</b></summary>

(1) **Server không có clip.** `GameServer` là process .NET; ở đó không tồn tại `AnimationClip`. Muốn
server biết đòn đánh dài bao lâu thì con số ấy phải là con số.

(2) **Clip là tài sản của người làm hình.** Nếu thời lượng ăn theo clip thì thêm 2 frame vung tay cho
đẹp là **đổi cân bằng game** — mà không ai review một file `.anim` như review một thay đổi cân bằng.

(3) **Tick là đơn vị duy nhất hai bên cùng đếm được.** Giây thì mỗi máy một đồng hồ, frame thì mỗi máy
một tốc độ. Tick là nhịp của giao thức, và cả prediction lẫn replay đều đếm bằng nó.

Hệ quả kéo theo: clip phải co giãn cho vừa số tick (`animator.speed`), không phải ngược lại.

</details>

**Câu 5.** Vì sao không dựng bảng chuyển tiếp bằng transition + parameter trong Animator Controller —
công cụ Unity làm sẵn cho đúng việc này?
<details>
<summary><b>📖 Đáp án câu 5</b></summary>

Vì luật chuyển trạng thái là **luật chơi**, mà luật chơi phải chạy được ở server (golden rule #2) và
chỉ được tồn tại một bản (#4). Animator Controller thì: server không đọc được (asset của Unity),
`Step` replay không tái hiện được, không viết được unit test, `git diff` ra YAML không đọc nổi, và
điều kiện chuyển thì nằm rải trong hàng chục ô "Conditions" không nhìn tổng thể được.

Ta **đã có** một máy trạng thái ở `CharacterStates.CanEnter`, chạy ở cả hai đầu dây. Dựng thêm một cái
nữa trong Unity là có hai nguồn sự thật cho cùng một câu hỏi — và cái thứ hai thì server mù.

`Animator` vẫn dùng, nhưng đúng một việc: phát clip được bảo phát, với tốc độ được bảo phát.

</details>

**Câu 6.** Người xem suy được `Grounded` từ `Δy == 0` giữa hai mẫu, nhưng không suy được `Crouching`.
Khác nhau chỗ nào, và vì sao "suy sai" ở hai chỗ này có giá khác nhau?
<details>
<summary><b>📖 Đáp án câu 6</b></summary>

`Grounded` có **dấu vết trong dữ liệu đang gửi**: sàn phẳng nên hai tick đứng trên đất cho đúng cùng
một `Y`, còn trên không thì trọng lực bảo đảm `Y` đổi mỗi tick. `Crouching` thì **không để lại dấu vết
nào**: ngồi im và đứng im sinh ra cùng một chuỗi vị trí, mãi mãi.

Giá của việc suy sai cũng khác: `Grounded` suy sai chỉ xảy ra khi hai mẫu trùng vị trí do mạng dồn gói
— hỏng **một frame** hoạt ảnh của người khác, không ai nhận ra. `Crouching` mà đoán thì sai **kéo dài**
suốt thời gian người ta ngồi.

Đó là tiêu chí chung để quyết định gửi hay suy: không phải "có suy được không" mà là **"suy sai thì
sai trong bao lâu"**.

</details>

**Câu 7.** Phase này thêm cả một tầng trạng thái mà **không thêm `NetCmd` nào**. Khi nào một tính năng
xứng đáng có lệnh riêng, khi nào thì đi ké dòng input?
<details>
<summary><b>📖 Đáp án câu 7</b></summary>

Đi ké dòng input khi nó **có mặt ở mọi tick** và **gắn với một tick cụ thể**: hướng chạy, nhảy, ngồi,
đánh. Gửi riêng thì phải tự lo thứ tự so với `MoveInput` cùng tick, và phải tự lo cho vòng replay tìm
lại nó — hai vấn đề tự biến mất khi đi chung.

Xứng đáng `NetCmd` riêng khi nó **thỉnh thoảng mới xảy ra** và **không thuộc về tick nào**: mở túi đồ,
gửi tin nhắn, đăng nhập. Nhét những thứ đó vào gói input 20 lần/giây là trả phí băng thông cho một
trường gần như luôn rỗng.

</details>

**Câu 8.** `MoveStateResponse` mang **trọn** `MoveState`, còn `EntityState` thì nhặt tay từng field.
Vì sao không thống nhất một cách cho cả hai?
<details>
<summary><b>📖 Đáp án câu 8</b></summary>

Vì hai kênh phục vụ hai việc khác nhau, và "thống nhất" ở đây sẽ hỏng theo hai hướng ngược nhau.

Kênh **chính mình** *định nghĩa* là gửi trọn trạng thái — đó chính là reconciliation: client replay từ
đó, thiếu một field là replay ra một tương lai khác. Nhặt tay ở kênh này là tạo ra một danh sách phải
nhớ cập nhật, và quên thì không có lỗi biên dịch.

Kênh **người khác** thì ngược lại: người xem không replay ai cả, nên `VelY`, `TicksSinceGrounded`,
`TicksSinceJumpRequest`… là byte thừa — nhân 20 lần/giây, nhân số người quanh họ. Gửi trọn ở đây là
trả tiền băng thông cho dữ liệu không ai đọc.

Ranh giới thật không phải "gửi trọn hay nhặt tay" mà là **"bên nhận có mô phỏng tiếp không"**. Có thì
gửi trọn; không thì gửi đúng thứ vẽ được.

</details>

---

## Để dành (ghi lại, chưa làm)

- **Bảy nhóm sprite còn lại.** `strike` `flyKick` `dizzy` `win` cần giá trị `ActionState` mới → một số
  mới trên dây → sửa cả hai bên. Còn `crouch_ATK` và `jumpATK` thì **không cần gì cả** — chúng là tổ
  hợp (tư thế × hành động), client tự chọn clip. Ranh giới đó là thước đo xem hai tầng có chia đúng
  không: thứ nào là *tổ hợp* thì miễn phí, thứ nào là *hành động mới* thì phải trả phí contract.
- **Hồi sinh tử tế.** `J` hiện là nút thử đặt thẳng trạng thái. Bản thật cần: đếm ngược trước khi được
  hồi sinh, điểm hồi sinh, và một `NetCmd` riêng (đây **là** sự kiện rời rạc — xem câu 7).
- **Huỷ đòn (cancel).** Bấm nhảy giữa lúc đánh thì huỷ đòn hay giữ nguyên? Hiện là giữ nguyên (nhảy
  vẫn chạy vì `Attack` không khoá thân). Muốn cho huỷ thì thêm một nhánh vào `CanEnter` — và đó là lúc
  bảng ưu tiên một chiều bắt đầu không đủ, phải có bảng thật.
- **Hộp va chạm theo tư thế.** Ngồi thì thân thấp hơn nên né được đòn cao. Lúc đó server sẽ gọi
  `CharacterStates.Derive` lần đầu tiên — và ta sẽ mừng vì đã để nó ở `Shared` từ hôm nay.
- **Blend giữa hai clip.** Đẹp hơn, nhưng thời gian blend là thời gian **không thuộc về tick nào**, và
  nó làm nhoè đúng cái ranh giới mà phase này vừa dựng. Nếu làm thì blend phải nằm trọn trong số tick
  của trạng thái, không được kéo dài nó.
- **Nén `Flags` + `Action` vào một byte.** `Action` chỉ cần 2 bit, `Flags` đang dùng 2 bit. Tiết kiệm
  1 byte × số entity × 20 lần/giây. Chưa đáng làm bây giờ, nhưng đáng biết là chỗ đó còn dư.
- **Hoạt ảnh cho hiệu ứng** (`fireball`, `explosion` trong `Textures/Dragon Warrior Files/Effects`).
  Đây **không** phải trạng thái nhân vật mà là entity riêng — và cái bẫy lớn nhất của nó (projectile
  là entity của server, không phải particle của client) là bài của Phase 14.

---

**Xong Phase 9 → nhân vật đã biết diễn, và ranh giới "ai quyết cái gì" đã rõ ở tầng hình ảnh.**
Thế giới thì vẫn là một mặt phẳng vô hình ở `y = 0`, và mọi người vẫn nhận gói của tất cả mọi người.
[PHASE-10](PHASE-10.md) cho thế giới một hình dạng thật — sàn, tường, bệ xuyên-một-chiều — và cho tầm
nhìn một giới hạn.
