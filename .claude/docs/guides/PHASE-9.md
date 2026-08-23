# PHASE 9 — State machine trạng thái: hai tầng, và ranh giới ai được quyết cái gì

> **Kết quả cuối Phase 9:** nhân vật đổi hình đúng theo việc nó đang làm — đứng, chạy, bay lên, rơi
> xuống, ngồi. Bấm nút đánh thì **server duyệt** rồi cả hai client mới cùng thấy anim `attack`, đúng
> hướng mặt, đúng độ dài. Và có một nút thử trên console server bắt mọi người `hurt` — chứng minh
> rằng có những trạng thái client **không bao giờ được tự bật**.
>
> **Điều kiện:** xong [`PHASE-8.md`](PHASE-8.md) hết Bước 4 — nhảy có coyote time, hai client thấy
> nhau bay lên rơi xuống theo đường cong. Rồi làm **Bước 0** trước khi vào Bước 1: cho sàn phẳng tạm
> trùng với tilemap đã vẽ, và gỡ hai file `MapGrid.cs` / `Map.cs` chép từ bản tài liệu top-down cũ.
>
> **Map thật KHÔNG thuộc phase này.** Phase 9 vẫn chạy trên mặt phẳng tạm của Phase 8; hình dạng map
> (lớp `Collision` trong tilemap → tool export ra file map → server và client cùng đọc) là toàn bộ
> nội dung của [`PHASE-10.md`](PHASE-10.md). Trộn hai việc vào một phase thì lúc nhân vật vẽ sai sẽ
> không biết tại hoạt ảnh hay tại va chạm.
>
> **Bài học chính:** (1) trạng thái nhân vật có **hai tầng** với hai chủ sở hữu khác nhau, và nhầm
> tầng là phá golden rule #2 ở dạng hình ảnh; (2) cách chặn client xin bậy **rẻ nhất** không phải là
> `if` kiểm tra ở server mà là **kiểu dữ liệu không cho phép diễn đạt điều bậy**; (3) con số của luật
> chơi **viết bằng giây** (đơn vị của người thiết kế), **đếm bằng tick** (đơn vị của mô phỏng), và
> **không bao giờ** lấy từ độ dài `AnimationClip`; (4) những con số ấy là **dữ liệu theo từng nhân
> vật**, không phải `const` — nên chúng sống trong một bảng tra được, không nằm rải trong code;
> (5) `Animator` của Unity là **máy chiếu phim**; đặt luật chơi vào đó là đặt luật ở nơi server không
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

## Bước 0 — Cho sàn phẳng tạm trùng với map đã vẽ (10 phút)

Phase 8 để cả thế giới là một mặt phẳng ở `GROUND_Y = 0`, biên ngang `±WORLD_HALF_EXTENT`. Nhưng
trong scene thì `Map.prefab` đã có tilemap vẽ tay: mặt đất nằm ở một cao độ khác 0, và map trải dài
về một phía chứ không đối xứng quanh gốc toạ độ. Không chỉnh thì suốt phase này nhân vật đứng lơ lửng
giữa không khí và kẹt vào một bức tường vô hình giữa map — nhìn cái gì cũng thấy sai, mà sai vì địa
hình chứ không phải vì hoạt ảnh. Debug hoạt ảnh trên một nền đã sai sẵn là tự làm khó mình.

Chọn một trong hai, mất vài phút:

- **Rẻ nhất:** kéo object `Map` trong scene lên/xuống sao cho **mặt trên của hàng đất mà nhân vật
  đứng nằm đúng ở `y = 0`**. Không sửa một dòng code nào.
- Hoặc sửa hằng trong `MovementRules` cho khớp map: `GROUND_Y` = cao độ mặt đất đã vẽ, và thay
  `Math.Clamp(state.X, -WORLD_HALF_EXTENT, WORLD_HALF_EXTENT)` bằng hai hằng `WORLD_MIN_X` /
  `WORLD_MAX_X` lấy từ bề ngang tilemap (`Tilemap.cellBounds` trong Inspector cho biết ngay).

Cả hai đều là **tạm** và cả hai đều bị xoá ở Phase 10. Ghi thẳng điều đó vào comment của hằng số để
sau này không ai tưởng đây là thiết kế.

> ### Hai file phải gỡ trước khi bắt đầu
>
> `Server/Shared/World/MapGrid.cs` và `Server/Shared/World/Map.cs` đang nằm trong staging là chép từ
> **bản cũ** của tài liệu Map — bản viết cho game top-down: lưới chỉ có hai loại ô "đi được / không đi
> được", map cố định kích thước và đặt giữa gốc toạ độ.
>
> Platformer cần khác hẳn: ô **đặc**, ô **rỗng**, và **bệ xuyên-một-chiều** (nhảy xuyên từ dưới lên,
> đứng được từ trên xuống) — ba loại chứ không phải hai. Map cũng không cố định kích thước: mỗi map
> có origin và bề rộng riêng, và hình dạng của nó phải sinh ra từ **chính tilemap bạn vẽ**, không phải
> từ một mảng chuỗi gõ tay trong `Shared` — gõ tay là có hai bản vẽ của cùng một map, và không ai kiểm
> hai bản ấy có khớp nhau không.
>
> `git rm --cached` rồi xoá (hoặc `git stash`) hai file đó đi. Bản đúng dựng lại từ đầu ở Phase 10.
> Giữ lại thì tới Phase 10 sẽ phải xoá đúng lúc đang bận việc khác, và trong lúc chờ thì có một
> `MapGrid` trong `Shared` mà không ai gọi — thứ tệ hơn cả không có gì.

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

**(f) DTO đã gói trọn từ Phase 8 — bước này không phải làm gì, và đó là điểm đáng dừng lại.**
`MoveInputRequest` / `MoveStateResponse` hiện đã mang nguyên `MoveIntent` / `MoveState` chứ không chép
tay từng field:

```csharp
public partial class MoveInputRequest  { public int Seq; public MoveIntent Intent; }
public partial class MoveStateResponse { public int LastInputSeq; public MoveState State; }
```

Nhờ vậy `MoveState` nở từ 7 lên 12 field mà **không có chỗ nào phải chép thêm**. Nếu còn ba chỗ chép
tay (khai báo DTO, server điền, client đọc) thì mỗi field mới là 3 cơ hội gõ nhầm — mà nhầm thì
**không có lỗi biên dịch**, chỉ có một field im lặng mang giá trị mặc định.

**Một thứ phải giữ nguyên khi thêm field:** `MoveState` và `MoveIntent` **không** có `[MemoryPackable]`,
chúng chỉ có `[StructLayout(LayoutKind.Sequential)]`. MemoryPack gặp struct **unmanaged** (mọi field
là kiểu giá trị, không chứa tham chiếu) thì copy nguyên khối bộ nhớ thay vì sinh mã đọc/ghi từng
field — nhanh hơn, nhưng byte trên dây **chính là** bố cục struct trong RAM. Hai hệ quả cho phase này:

- `bool`, `int` và `enum : byte` đều là unmanaged, nên 5 field mới không phá tính chất đó. Thêm một
  `string` hay một mảng thì phá — và lúc ấy phải đổi hẳn cách gửi chứ không phải thêm một dòng.
- Bố cục đổi **là** giao thức đổi. Build `Shared` xong mà DLL chưa sang được Unity thì hai bên đọc
  cùng một chuỗi byte theo hai bố cục khác nhau: không lỗi, không log, chỉ có nhân vật nhấp nháy ở
  những toạ độ vô nghĩa. `NetCmd` không bảo vệ được loại lệch này — nó chỉ bảo đảm hai bên gọi đúng
  handler, không bảo đảm hai bên hiểu payload giống nhau.

Có một câu hỏi đúng phải hỏi ở đây: *gắn format gói tin vào cấu trúc nội bộ như vậy có phải là coupling
tồi không?* Với kênh này thì **không**, và lý do đáng nhớ:

> Kênh "chính mình" (`MoveState`) **định nghĩa** là gửi trọn trạng thái — đó chính là reconciliation.
> Kênh "người khác" (`EntityState`) thì ngược lại: chỉ gửi thứ vẽ được. Hai kênh, hai lý do, hai
> format — giữ chúng khác nhau là có chủ đích, không phải quên gộp.

**(g) `EntityState` — thứ người khác cần, và không hơn.** Người xem **không** cần `VelX`, `VelY`,
`TicksSinceGrounded`… Họ cần đủ để vẽ. Thêm đúng ba trường, **viết thẳng ra, không đóng gói bit**:

```csharp
public bool FacingLeft;
public bool Crouching;
public ActionState Action;
```

Bản đầu của phase này gói hai `bool` vào một `byte Flags` với `1 << 0`, `1 << 1`, `flags |= ...`.
Bỏ đi, có chủ đích, và lý do đáng nhớ hơn cả cái byte tiết kiệm được:

| | `byte Flags` + mặt nạ bit | Ba trường viết thẳng |
|---|---|---|
| Tốn | 2 byte/entity/tick | 3 byte/entity/tick |
| Đọc code | phải nhớ bit nào là gì, sai một bit thì nửa số nhân vật quay ngược mặt | nhìn tên là biết |
| Thêm một cờ mới | sửa hằng số, sửa `Pack`, sửa `Has`, và không được đụng thứ tự bit cũ | thêm một dòng |
| Sai thì báo ở đâu | không đâu cả | không đâu cả — nhưng khó sai hơn nhiều |

Một byte thừa × 20 tick/s × 30 người quanh bạn = **600 byte/giây**. Đường truyền nào cũng nuốt được
con số đó mà không chớp mắt. Đổi lại là code đọc được, và ở một dự án học thì đó là thứ đắt hơn.

> Nén bit là **tối ưu**, không phải thiết kế. Tối ưu đúng lúc là sau khi đã đo; tối ưu sai lúc là trả
> phí bằng thời gian đọc code của chính mình, mỗi lần mở file, mãi mãi.

Ngày nào cờ nhiều lên thật (10–15 cái: choáng, tàng hình, cưỡi thú, đang giao dịch…) và profiler chỉ
đúng vào băng thông snapshot thì hãy nén — và khi đó nén ở **đúng một chỗ** trong `Shared`, không phải
mặt nạ bit chép tay hai bên. Ghi vào "Để dành" rồi đi tiếp.

`Action` gửi thẳng kiểu `ActionState` chứ không phải `byte`: MemoryPack ghi enum đúng bằng 1 byte của
kiểu nền, nên **cùng giá lại không phải cast ở hai đầu**. Cast `(byte)`/`(ActionState)` chỉ là chỗ để
gõ nhầm, không mua được gì.

Vì sao chỉ ba thứ này chứ không gửi luôn cả `LocomotionState`? Vì phần còn lại **suy được từ chính
vị trí đang gửi**: hai mẫu snapshot liên tiếp cho `ΔY > 0` là đang bay lên, `ΔY < 0` là đang rơi,
`ΔY == 0` là đang chạm đất, rồi `ΔX` phân biệt `Idle` với `Walk`. Chi tiết ở Bước 5. Còn hai thứ
**không** suy được là `Crouching` (ngồi và đứng yên cho cùng một chuỗi vị trí) và `FacingLeft` (đứng
yên thì hướng mặt là trí nhớ, không phải chuyển động).

Bài tập nhỏ tự làm trước khi đọc tiếp: lấy 5 giá trị của `LocomotionState`, tự phân loại cái nào suy
được từ hai mẫu vị trí, cái nào không, và vì sao. Trả lời đúng câu đó là hiểu xong nửa phase.

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

        // Cố tình KHÔNG có hàm kiểu BlocksMovement(action) ở đây: "hành động này có khoá thân không"
        // là một CON SỐ của từng hành động, không phải một luật chung — nó nằm trong ActionDefinition.
    }
}
```

**`Server/Shared/World/MoveState.cs`** — thêm 2 field vào `MoveIntent`, 5 field vào `MoveState`.
Giữ nguyên `[StructLayout(LayoutKind.Sequential)]` và **không** thêm `[MemoryPackable]`: hai struct
này vẫn unmanaged nên MemoryPack vẫn copy nguyên khối — xem mục (f).

```csharp
using System.Runtime.InteropServices;

namespace MMORPG.Shared.World
{
    /// <summary>
    /// Ý định của người chơi tại một tick — đúng những gì bấm được trên bàn phím, không hơn.
    /// Cố tình không có trục Y: trong platformer người chơi không điều khiển chiều dọc,
    /// chiều dọc là hệ quả của trọng lực và của cú nhảy.
    ///
    /// Đi thẳng trên dây (bọc trong MoveInputRequest) nên mọi field ở đây đều là thứ kẻ lạ điều
    /// khiển được: bên nhận phải kiểm, không được tin.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
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

        /// <summary>
        /// MỨC của nút ngồi: bấm là ngồi, thả là đứng. Ngược hẳn với Jump — ngồi là một tư thế kéo
        /// dài nên chỉ giá trị mới nhất có nghĩa, gộp lại là ngồi mãi không đứng dậy được.
        /// </summary>
        public bool Crouch;

        /// <summary>
        /// Hành động vừa xin, dạng CẠNH như Jump. Kiểu ActionRequest chứ không phải ActionState:
        /// tập giá trị này cố tình hẹp hơn, để "xin được Hurt" là câu không viết ra được.
        /// </summary>
        public ActionRequest Action;
    }

    /// <summary>
    /// Toàn bộ trạng thái của một entity — tập nhỏ nhất mà biết nó thì tính được tick kế tiếp,
    /// VÀ vẽ được nhân vật ra màn hình. Hai vai trò trong một struct là có chủ đích: nhờ vậy hoạt
    /// ảnh được reconciliation lo hộ, không cần một đường đồng bộ riêng.
    ///
    /// Vận tốc nằm ở đây chứ không suy ra từ vị trí: hai nhân vật cùng một điểm, một đang bay lên
    /// một đang rơi xuống, tick sau sẽ ở hai chỗ khác nhau.
    ///
    /// Là struct field công khai chứ không phải class có property: nó bị copy hàng chục lần mỗi giây
    /// trong vòng replay của reconciliation, và tính "gán là copy" của value type chính là thứ giữ
    /// cho replay không vô tình sửa trạng thái gốc.
    /// </summary>
    /// <remarks>
    /// Struct này đi thẳng trên dây bên trong <c>MoveStateResponse</c>. Vì mọi field đều là kiểu
    /// unmanaged, MemoryPack không sinh mã đọc/ghi từng field mà **copy nguyên khối bộ nhớ** — nhanh,
    /// nhưng đổi lại byte trên dây chính là bố cục struct trong RAM. Ba ràng buộc đi kèm:
    /// (1) <see cref="LayoutKind.Sequential"/> ghi tường minh để server (CoreCLR) và client
    /// (Mono/IL2CPP) chắc chắn xếp field giống nhau; (2) thêm một field kiểu tham chiếu (string,
    /// mảng...) là phá tính unmanaged — lúc đó phải đổi cách gửi, không phải chỉ thêm một dòng;
    /// (3) mỗi lần thêm/bớt/đổi thứ tự field là một lần đổi giao thức, DLL cũ bên Unity sẽ đọc ra
    /// những con số vô nghĩa mà không báo lỗi.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    public struct MoveState
    {
        public float X;
        public float Y;
        public float VelX;
        public float VelY;

        /// <summary>Chân có đang chạm sàn ở CUỐI tick trước không. Dùng cho hiển thị; điều kiện nhảy dùng bộ đếm bên dưới.</summary>
        public bool Grounded;

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

        /// <summary>Số tick kể từ lần đánh gần nhất — nền của cooldown. Cùng họ với TicksSinceGrounded.</summary>
        public int TicksSinceAttack;

        /// <summary>
        /// Trạng thái lúc mới vào world. Grounded = false có chủ ý: để tick đầu tiên tự rơi và tự
        /// phát hiện sàn, thay vì tin rằng toạ độ lấy từ DB đang đứng đúng trên mặt đất.
        /// </summary>
        public static MoveState AtRest(float x, float y)
        {
            return new MoveState
            {
                X = x, Y = y, VelX = 0f, VelY = 0f,
                Grounded = false,
                // Bắt đầu ở trạng thái hết hạn: vừa vào world thì chưa có tư cách nhảy nào cả.
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

**`Server/Shared/Dto/World/MoveDto.cs`** — **không sửa một chữ.** Hai lớp bọc đã đúng hình dạng từ
Phase 8, và `MoveState` nở thêm bao nhiêu field thì chúng cũng không đổi:

```csharp
    public partial class MoveInputRequest
    {
        public int Seq { get; set; }
        public MoveIntent Intent { get; set; }
    }

    public partial class MoveStateResponse
    {
        public int LastInputSeq { get; set; }
        public MoveState State { get; set; }
    }
```

Đây là lần đầu trong dự án bạn **hưởng** một quyết định thiết kế cũ thay vì trả giá cho nó: hôm ở
Phase 8 đổi từ 7 field rời sang gói trọn struct tốn khoảng nửa tiếng; hôm nay nó trả về 5 field × 3
chỗ chép tay = 15 dòng không phải viết, và 15 cơ hội gõ nhầm không tồn tại.

**`Server/Shared/Dto/World/WorldSyncDto.cs`** — `EntityState` và `EntitySpawnNotice` thêm ba trường:

```csharp
using System;
using MemoryPack;
using MMORPG.Shared.World;

namespace MMORPG.Shared.Dto.World
{
    /// <summary>
    /// Phần BẤT BIẾN của một entity — gửi đúng một lần lúc nó xuất hiện.
    /// Thứ đổi theo tick đi trong snapshot, không lặp lại ở đây mỗi tick.
    /// </summary>
    [MemoryPackable]
    public partial class EntitySpawnNotice
    {
        public int EntityId { get; set; }
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Lớp nhân vật. Người nhận tra bảng bằng số này để biết đối phương chạy nhanh bao nhiêu và
        /// mỗi hành động của họ dài bao lâu — nhờ vậy các con số ấy không phải đi trên dây.
        /// </summary>
        public int ClassId { get; set; }

        /// <summary>Vị trí lúc xuất hiện — mồi đầu tiên cho buffer nội suy phía client.</summary>
        public float X { get; set; }
        public float Y { get; set; }

        /// <summary>Tư thế lúc xuất hiện — để nhân vật hiện ra đã đúng hướng, không quay đầu một nhịp sau.</summary>
        public bool FacingLeft { get; set; }
        public bool Crouching { get; set; }
        public ActionState Action { get; set; }
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
        public bool FacingLeft { get; set; }
        public bool Crouching { get; set; }

        /// <summary>Tầng action — do server quyết, người xem không có cách nào tự biết.</summary>
        public ActionState Action { get; set; }
    }

    [MemoryPackable]
    public partial class WorldSnapshotNotice
    {
        public EntityState[] States { get; set; } = Array.Empty<EntityState>();
    }
}
```

</details>

### ✅ CHECKPOINT A — build **xanh**, và đó mới là chỗ đáng sợ

`dotnet build Server/Shared` phải sạch; DLL tự copy sang `Assets/Plugins/Shared/`.

Rồi build `Server/GameServer` và mở Unity: **cũng xanh nốt**, không một dòng đỏ.

Dừng lại một nhịp, vì kết quả này ngược trực giác. Vừa thêm 5 field vào `MoveState`, 2 field vào
`MoveIntent`, 2 field vào `EntityState` — mà không chỗ nào hỏng: `new MoveIntent { DirX = ..., Jump = ... }`
cũ vẫn hợp lệ (field mới nhận giá trị mặc định), mọi chỗ đọc cũ vẫn đọc đúng thứ nó vốn đọc.

Trình biên dịch im lặng nghĩa là **danh sách việc phải tự viết ra**:

| Chỗ | Làm ở bước | Quên thì triệu chứng là gì |
|---|---|---|
| `MovementRules.Step` | Bước 2 | Năm field mới vĩnh viễn 0/false — nhân vật không bao giờ ngồi, không bao giờ đánh |
| `PlayerEntity.SetInput` · `Integrate` | Bước 3 | Server không bao giờ nhìn thấy nút ngồi và nút đánh |
| `MoveHandler.OnMoveInput` | Bước 3 | Hai field mới không qua cửa kiểm — client gửi gì server tin nấy |
| `WorldService` (snapshot + spawn) | Bước 3 | Người khác luôn quay mặt phải, không bao giờ ngồi |
| `PlayerMotor` | Bước 4 | Không gửi ngồi/đánh, và không vẽ hoạt ảnh nào |
| `RemotePlayerView` · `WorldSpawner` | Bước 5 | Ba trường mới tới nơi rồi bị vứt đi |

Bài học miễn phí, và đắt hơn vẻ ngoài: **contract nở ra thì trình biên dịch không còn đứng về phía
bạn.** Xoá hay đổi tên một field thì có lỗi đỏ dẫn đường tới từng chỗ dùng; **thêm** một field thì chỉ
có bug câm. Đó cũng là lý do mục (f) đáng giá: kênh `MoveState` gửi trọn struct nên nó tự đúng, và
danh sách trên chỉ còn các chỗ *sinh ra* dữ liệu — không còn chỗ nào *chép lại* dữ liệu.

---

## Bước 2 — Shared: `Step` biết tư thế, hướng mặt, hành động — và số riêng của từng nhân vật

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

### Thời lượng: người ta viết bằng **giây**, mô phỏng đếm bằng **tick**

Đây là chỗ bản đầu của phase làm sai, và sai theo kiểu càng về sau càng đắt. Nó viết:

```csharp
public const int ATTACK_TICKS = 5;
public const int HURT_TICKS = 4;
```

Hai vấn đề, và cái thứ hai nặng hơn cái thứ nhất:

**(1) Đơn vị sai với người sẽ đọc con số đó.** Hoạ sĩ nói "đòn này diễn khoảng một phần tư giây",
game designer nói "hồi chiêu 3 giây". Không ai trong hai người biết tick là gì, và họ không nên phải
biết: tick là chi tiết *cài đặt* của server. Bắt họ quy đổi là bắt họ làm một phép nhân trong đầu mỗi
lần chỉnh số — và ngày server đổi từ 20 lên 30 tick/s thì **mọi con số họ từng viết đều sai**.

**(2) `const` nghĩa là "cả thế giới chỉ có một giá trị".** Trong MMO thì mọi con số này đều **theo
nhân vật**: chiến binh vung kiếm 0.25s, pháp sư đọc chú 1.2s; giày tăng tốc chạy; nội tại giảm hồi
chiêu. `const` không diễn đạt được điều đó, và ngày cần thì không "sửa số" được — phải sửa **hình
dạng** của code, ở mọi chỗ đang gọi.

Chữa bằng ba mảnh, và mỗi mảnh trả lời đúng một câu:

| Mảnh | Trả lời | Ở đâu |
|---|---|---|
| `ActionDefinition` | "một hành động gồm những con số nào" — thời lượng, hồi chiêu, có khoá thân không | `Shared` |
| `CharacterProfile` | "**nhân vật này** chạy nhanh bao nhiêu, các hành động của nó dài bao lâu" | `Shared`, tra theo `ClassId` |
| `MovementRules.ToTicks(seconds)` | "giây quy ra tick thế nào" — đúng một công thức, cho cả hai đầu dây | `Shared` |

Viết vào bảng bằng **giây**, quy ra tick **một lần** lúc dựng bảng, rồi từ đó trong `Step` chỉ còn số
nguyên tick:

```csharp
new ActionDefinition(durationSeconds: 0.25f, cooldownSeconds: 0.4f, locksMovement: false)
```

**Vì sao mô phỏng vẫn phải đếm bằng tick, không đếm thẳng bằng giây.** Ba lý do độc lập:

1. **Số nguyên không có sai số.** `ActionTicksLeft--` mỗi tick rồi so `<= 0` là chính xác tuyệt đối ở
   cả hai đầu dây. Cộng dồn `elapsedSeconds += dt` thì hai bên có thể lệch ở chữ số cuối, và một
   hành động kết thúc sớm/muộn **một tick** giữa client và server là một lần rubber-band.
2. **Replay phải cho đúng kết quả cũ.** Reconciliation chạy lại `Step` hàng chục lần mỗi giây; thứ
   nào đếm bằng số nguyên thì chạy lại bao nhiêu lần cũng ra đúng con số ấy.
3. **Tick là nhịp của giao thức.** Giây thì mỗi máy một đồng hồ, frame thì mỗi máy một tốc độ.

**Giá phải trả, và phải nói trước cho người thiết kế biết:** thời lượng bị **lượng tử hoá theo 50ms**.
Viết `0.23s` hay `0.25s` đều ra 5 tick; viết `0.26s` thì thành 6 tick (0.3s). Quy tắc quy đổi vì thế
phải nằm ở một chỗ và làm tròn **lên**, có sàn 1 tick — một hành động 10ms mà thành 0 tick thì nó
không tồn tại, và một `ActionTicksLeft = 0` sẽ bị phép 0 xoá ngay tick sau.

**Còn `MOVE_SPEED` và `JUMP_SPEED`?** Cũng chuyển vào `CharacterProfile` — cùng lý do (2): tốc độ chạy
là chỉ số của nhân vật, không phải hằng số của vũ trụ. Ngược lại `GRAVITY`, `MAX_FALL_SPEED`,
`COYOTE_TICKS`, `JUMP_BUFFER_TICKS` thì **ở lại** `MovementRules`: chúng là luật của *thế giới*, ai
vào cũng chịu như nhau. Ranh giới để tự phân loại: *đổi con số này thì một người đổi, hay cả thế giới
đổi?*

**Bảng nằm ở đâu bây giờ, và ở đâu về sau.** Phase này dựng bảng **bằng C# ngay trong `Shared`** —
một hàm `Build()` trả về `Dictionary<int, CharacterProfile>`. Đó chưa phải đích đến, nhưng nó đã đúng
**hình dạng**: tra theo id, viết bằng giây, quy ra tick một lần. Phase 11 chỉ thay *nguồn* của bảng
(đọc từ file, sửa không cần build lại) mà **không đụng một dòng nào ở chỗ gọi** — `profile.MoveSpeed`,
`profile.GetAction(...)` giữ nguyên. Đó là toàn bộ ý nghĩa của việc dựng đúng seam trước khi cần tới nó.

Và vì client cũng phải đọc bảng này để dự đoán (nó là **config loại B** trong bảng phân loại ở
ROADMAP §2b), bảng phải sống ở `Shared` chứ không phải ở `GameServer`. Ngày bảng ra file, hai bên đọc
**cùng một file**, và lệch version thì bị chặn vào world — cũng là bài của Phase 11.

**Thứ tự các phép trong `Step`** — vẫn là một phần của contract, giờ dài hơn:

```
     Tra bảng          current = profile.GetAction(state.Action)   (sau phép 0)
0.  Nhịp tầng action   ActionTicksLeft-- (sàn 0) ; TicksSinceAttack++ (kẹp EXPIRED)
                       hết ticks và không phải Die  →  Action = None
1.  Tư thế             Crouching = intent.Crouch && Grounded && !locked
2.  Vận tốc ngang      locked (Hurt/Die) hoặc đang ngồi  →  VelX = 0
                       ngược lại                        →  VelX = DirX * profile.MoveSpeed
    Hướng mặt          VelX != 0 và Action == None  →  FacingLeft = VelX < 0
3.  Trọng lực          (như Phase 8)
4a. Bộ đếm nhảy        (như Phase 8)
4b. Điều kiện nhảy     (như Phase 8) + không bị khoá thân
4c. Nếu nhảy           VelY = profile.JumpSpeed
5.  Xin hành động      intent.Action == Attack
                       && TicksSinceAttack >= attack.CooldownTicks
                       && CanEnter(Action, ActionTicksLeft, Attack)
                       →  Action = Attack ; ActionTicksLeft = attack.DurationTicks ; TicksSinceAttack = 0
6.  Tích phân          (như Phase 8)
7.  Va chạm sàn        (như Phase 8)
8.  Kẹp biên X         (như Phase 8)
```

Năm chỗ dễ sai, đọc kỹ trước khi gõ:

- **Phép 0 phải chạy trước phép 5.** Chạy sau thì cú đánh vừa bắt đầu ở tick này đã bị chính phép 0
  trừ mất một tick. Cùng loại bẫy với "kiểm `Grounded` trước hay sau va chạm sàn" ở Phase 8.
- **Tra bảng SAU phép 0.** Phép 0 có thể vừa đưa `Action` về `None`; tra trước thì cả tick này thân
  thể vẫn bị khoá theo hành động đã hết hạn — trễ một nhịp, đủ để cảm thấy "nhân vật đơ".
- **Phép 0 không được xoá `Die`.** Chết rồi thì hết `ActionTicksLeft` là hết *hoạt ảnh*, không phải
  hết *trạng thái*. Bỏ sót nhánh loại trừ này thì xác chết đứng dậy đi tiếp sau 1 giây.
- **Hướng mặt khoá khi `Action != None`.** Đang vung tay mà xoay được người là đòn đánh đổi hướng giữa
  chừng — ở Phase 14 khi đòn đánh có hộp va chạm thật thì đó là lỗ hack: bấm đánh rồi xoay để quét cả
  hai bên.
- **Cooldown đếm từ lúc bắt đầu.** `TicksSinceAttack = 0` đặt cùng lúc với `ActionTicksLeft`. Đếm từ
  lúc kết thúc thì tổng nhịp đánh = thời lượng + hồi chiêu, và mỗi lần chỉnh độ dài đòn đánh là vô
  tình chỉnh luôn tốc độ đánh.

Chú ý một điều tinh tế: phép 5 có **hai** điều kiện chặn (`CanEnter` và cooldown) và chúng khác nhau.
`CanEnter` trả lời "trạng thái hiện tại có cho phép không" (đang `hurt` thì không). Cooldown trả lời
"nhịp đánh đã tới chưa". Gộp hai thứ này vào một số là mất khả năng diễn đạt "đang choáng thì cấm đánh
kể cả đã hết cooldown".

Và `locksMovement` giờ là **dữ liệu**, không phải một `switch` trong code: hôm nay `Hurt`/`Die` khoá
thân còn `Attack` thì không, mai thêm một chiêu "đứng yên đọc chú" thì chỉ là một ô `true` trong bảng.
Đây là cùng một bài học với (c) ở Bước 1, ở tầng khác: *luật thì viết bằng code, số thì viết bằng dữ liệu.*

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`Server/Shared/World/CharacterProfile.cs`** (file mới):

```csharp
using System.Collections.Generic;

namespace MMORPG.Shared.World
{
    /// <summary>
    /// Các con số của MỘT hành động. Người thiết kế viết bằng giây — đơn vị của họ; hàm dựng quy ra
    /// tick ngay tại đây để từ đó về sau mô phỏng chỉ còn làm việc với số nguyên.
    /// </summary>
    public readonly struct ActionDefinition
    {
        /// <summary>Hành động kéo dài bao nhiêu tick.</summary>
        public readonly int DurationTicks;

        /// <summary>Nhịp tối thiểu giữa hai lần dùng, đếm từ lúc BẮT ĐẦU lần trước.</summary>
        public readonly int CooldownTicks;

        /// <summary>
        /// Trong lúc hành động này diễn ra thì thân thể có mất quyền điều khiển không.
        /// Là dữ liệu chứ không phải một nhánh switch: thêm một chiêu "đứng yên đọc chú" chỉ là
        /// thêm một ô true, không phải sửa luật.
        /// </summary>
        public readonly bool LocksMovement;

        public ActionDefinition(float durationSeconds, float cooldownSeconds, bool locksMovement)
        {
            DurationTicks = MovementRules.ToTicks(durationSeconds);
            CooldownTicks = MovementRules.ToTicks(cooldownSeconds);
            LocksMovement = locksMovement;
        }
    }

    /// <summary>
    /// Bộ số của một lớp nhân vật: chạy nhanh bao nhiêu, nhảy cao bao nhiêu, mỗi hành động dài bao lâu.
    ///
    /// Cả server lẫn client đều đọc — server để mô phỏng, client để dự đoán và để co hoạt ảnh cho vừa
    /// thời lượng. Vì vậy nó phải ở Shared: hai bên đọc hai bảng khác nhau thì mọi thứ vẫn chạy, chỉ
    /// là lệch dần, và không có lỗi nào để lần theo.
    /// </summary>
    public sealed class CharacterProfile
    {
        public int ClassId { get; }

        /// <summary>Tốc độ chạy ngang, world unit/giây.</summary>
        public float MoveSpeed { get; }

        /// <summary>Vận tốc bật lên tức thời khi nhảy.</summary>
        public float JumpSpeed { get; }

        private readonly Dictionary<ActionState, ActionDefinition> _actions;

        public CharacterProfile(int classId, float moveSpeed, float jumpSpeed,
            Dictionary<ActionState, ActionDefinition> actions)
        {
            ClassId = classId;
            MoveSpeed = moveSpeed;
            JumpSpeed = jumpSpeed;
            _actions = actions;
        }

        /// <summary>
        /// Số liệu của một hành động. Hành động không có trong bảng (kể cả None) trả về bản rỗng:
        /// 0 tick, không khoá thân — nhờ vậy chỗ gọi không phải kiểm null hay kiểm None.
        /// </summary>
        public ActionDefinition GetAction(ActionState action)
        {
            if (!_actions.TryGetValue(action, out ActionDefinition definition))
                return default;

            return definition;
        }
    }

    /// <summary>
    /// Bảng tra profile theo lớp nhân vật. Hiện dựng bằng C# ngay trong Shared; khi bảng chuyển sang
    /// đọc từ file thì chỉ hàm Build đổi, mọi chỗ gọi Get giữ nguyên.
    /// </summary>
    public static class CharacterProfiles
    {
        public const int DRAGON_WARRIOR = 1;

        private static readonly Dictionary<int, CharacterProfile> _byClassId = Build();

        public static CharacterProfile Get(int classId)
        {
            if (!_byClassId.TryGetValue(classId, out CharacterProfile profile))
                return _byClassId[DRAGON_WARRIOR];

            return profile;
        }

        private static Dictionary<int, CharacterProfile> Build()
        {
            var dragonWarrior = new CharacterProfile(
                DRAGON_WARRIOR,
                moveSpeed: 5f,
                jumpSpeed: 11f,
                new Dictionary<ActionState, ActionDefinition>
                {
                    // Mọi con số dưới đây viết bằng GIÂY. Đòn đánh 0.25s cho clip 3 frame là 12fps,
                    // vừa mắt; hồi chiêu 0.4s là nhịp bấm liên tục mà không thành máy khoan.
                    [ActionState.Attack] = new ActionDefinition(0.25f, 0.4f, locksMovement: false),

                    // Choáng thì khoá thân: mất quyền điều khiển là toàn bộ ý nghĩa của trúng đòn.
                    [ActionState.Hurt] = new ActionDefinition(0.2f, 0f, locksMovement: true),

                    // Hết 1 giây là hết HOẠT ẢNH gục; trạng thái Die thì ở lại cho tới khi hồi sinh.
                    [ActionState.Die] = new ActionDefinition(1f, 0f, locksMovement: true),
                });

            return new Dictionary<int, CharacterProfile>
            {
                [dragonWarrior.ClassId] = dragonWarrior,
            };
        }
    }
}
```

**`Server/Shared/World/MovementRules.cs`** — bỏ `MOVE_SPEED` / `JUMP_SPEED` (đã sang `CharacterProfile`),
thêm `ToTicks`, và `Step` nhận thêm profile:

```csharp
        /// <summary>
        /// Quy giây ra tick. Chạy MỘT LẦN lúc dựng bảng, không nằm trong Step — nhờ vậy vòng mô phỏng
        /// chỉ còn làm việc với số nguyên, và hai đầu dây không có cửa nào để lệch nhau ở chữ số cuối.
        ///
        /// Làm tròn LÊN và có sàn 1: một hành động 10ms mà quy ra 0 tick thì nó không tồn tại — phép 0
        /// của Step sẽ xoá nó ngay tick sau. Hệ quả phải nói trước với người viết số: thời lượng bị
        /// lượng tử hoá theo 50ms, viết 0.23s hay 0.25s đều ra 5 tick.
        /// </summary>
        public static int ToTicks(float seconds)
        {
            if (seconds <= 0f)
                return 0;

            int ticks = (int)MathF.Ceiling(seconds * TICK_RATE);

            return ticks < 1 ? 1 : ticks;
        }

        /// <summary>
        /// Một bước mô phỏng. Hàm THUẦN: không đọc thời gian, không random, không đọc biến ngoài —
        /// cùng (state, intent, dt, profile) luôn cho cùng kết quả, ở cả hai đầu dây.
        ///
        /// profile là bộ số của CHÍNH nhân vật đang được mô phỏng. Nó vào bằng tham số chứ không nằm
        /// trong MoveState: MoveState đi trên dây mỗi tick, còn profile thì hai bên tra được từ ClassId
        /// — gửi kèm là trả tiền băng thông 20 lần mỗi giây cho một thứ không bao giờ đổi.
        ///
        /// THỨ TỰ các phép dưới đây là một phần của contract. Đổi thứ tự là đổi kết quả, và vì
        /// hai bên chạy cùng file nên nó sẽ không lệch ngay — nó lệch vào ngày ai đó sửa một bên.
        /// </summary>
        public static MoveState Step(MoveState state, MoveIntent intent, float dt, CharacterProfile profile)
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

            // Tra bảng SAU phép 0, vì phép 0 vừa có thể đưa Action về None. Tra trước thì cả tick này
            // thân thể còn bị khoá theo một hành động đã hết hạn — trễ một nhịp, đủ để thấy "đơ".
            bool locked = profile.GetAction(state.Action).LocksMovement;

            // 1. Tư thế. Ngồi chỉ có nghĩa khi chân chạm đất và thân thể còn nghe lời.
            state.Crouching = intent.Crouch && state.Grounded && !locked;

            // 2. Vận tốc ngang. Ăn đòn / gục thì mất quyền điều khiển; ngồi thì đứng yên tại chỗ.
            if (locked || state.Crouching)
            {
                state.VelX = 0f;
            }
            else
            {
                state.VelX = intent.DirX * profile.MoveSpeed;
            }

            // Hướng mặt: chỉ đổi khi đang thật sự dịch chuyển VÀ không vướng hành động nào.
            // Đứng yên thì giữ hướng cũ (đó là lý do FacingLeft phải là trạng thái, không phải suy ra).
            // Khoá hướng trong lúc hành động: vung tay mà xoay được người thì đòn đánh quét cả hai bên.
            if (state.VelX != 0f && state.Action == ActionState.None)
                state.FacingLeft = state.VelX < 0f;

            // 3. Trọng lực — luật của thế giới, không theo nhân vật.
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
            if (!locked &&
                state.TicksSinceJumpRequest <= JUMP_BUFFER_TICKS &&
                state.TicksSinceGrounded <= COYOTE_TICKS)
            {
                state.VelY = profile.JumpSpeed;
                state.TicksSinceJumpRequest = EXPIRED;
                state.TicksSinceGrounded = EXPIRED;
            }

            // 5. Xin hành động. HAI điều kiện chặn khác nhau, cố tình không gộp:
            //    CanEnter  = "trạng thái hiện tại có cho phép không" (đang choáng thì không)
            //    cooldown  = "nhịp đánh đã tới chưa"
            //    Gộp vào một số là mất khả năng diễn đạt "hết cooldown rồi nhưng đang choáng nên vẫn cấm".
            ActionDefinition attack = profile.GetAction(ActionState.Attack);

            if (intent.Action == ActionRequest.Attack &&
                state.TicksSinceAttack >= attack.CooldownTicks &&
                CharacterStates.CanEnter(state.Action, state.ActionTicksLeft, ActionState.Attack))
            {
                state.Action = ActionState.Attack;
                state.ActionTicksLeft = attack.DurationTicks;
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

            // 8. Biên ngang tạm (hoặc hai hằng WORLD_MIN_X / WORLD_MAX_X nếu bạn chọn cách thứ
            //    hai ở Bước 0). Cả hai đều biến mất ở Phase 10 khi map có tường thật.
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
CharacterProfile profile = CharacterProfiles.Get(CharacterProfiles.DRAGON_WARRIOR);

var state = MoveState.AtRest(0f, 0f);
var idle = new MoveIntent();
var attack = new MoveIntent { Action = ActionRequest.Attack };

for (int tick = 0; tick < 20; tick++)
{
    // Bấm đánh ở tick 2 và tick 5 — cú thứ hai phải bị cooldown chặn.
    MoveIntent intent = tick == 2 || tick == 5 ? attack : idle;
    state = MovementRules.Step(state, intent, MovementRules.TICK_DT, profile);

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

Để ý hai con số `5` và `8` trong log: chúng **không có trong code** ở đâu cả. Trong bảng bạn viết
`0.25f` và `0.4f`; `ToTicks` biến chúng thành 5 và 8. Thử đổi `0.25f` thành `0.26f` rồi chạy lại — vẫn
ra 6 tick chứ không phải 5.2: đó là lượng tử hoá 50ms, tận mắt.

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

**(b) `ForceAction(ActionState action)`** — cửa để **server** đặt trạng thái, và là cửa duy nhất.

Chú ý nó **không nhận số tick**. Thời lượng là chuyện của nhân vật *bị tác động*, nên `ForceAction` tự
tra `profile.GetAction(action).DurationTicks`. Người ra lệnh chỉ nói **cái gì xảy ra**, không nói **kéo
dài bao lâu** — nhờ vậy hai nhân vật khác lớp cùng ăn một đòn sẽ choáng khác nhau mà chỗ gọi không cần
biết gì về chuyện đó. Đây là hệ quả trực tiếp của việc bỏ `const` ở Bước 2, và là chỗ đầu tiên nó trả lãi.

Nó vẫn phải hỏi `CanEnter`: server có quyền hơn client, nhưng không có quyền phá luật (gây `hurt` cho
một xác chết là vô nghĩa, và ở Phase 14 nó còn là chỗ để một con quái vô tình "hồi sinh" mục tiêu bằng
cách đánh nó).

Đặt `ForceAction` trên `PlayerEntity` chứ không rải rác trong `WorldService`: một chỗ duy nhất sửa
được tầng 2 thì sau này truy "ai bật `Die`" là đọc đúng một hàm.

Hai chi tiết nhỏ mà bỏ qua là mất thời gian:

- **`State` là property trả về struct**, nên `State.Action = action;` **không biên dịch được** (bạn
  đang sửa vào một bản copy tạm mà C# vứt đi ngay sau đó — trình biên dịch chặn thẳng). Phải
  copy-sửa-gán lại: `MoveState state = State; state.Action = ...; State = state;`. Đây là mặt trái
  của chính tính chất đã cứu vòng replay ở Phase 8: **gán là copy**.
- **Chỉ gọi `ForceAction` từ luồng tick.** Xem (d).

**(c) `Integrate`** dựng `MoveIntent` đủ 4 field, tiêu thụ `_pendingAction` giống hệt `_pendingJump`.
Bộ đếm `_ticksSinceInput` (mất mạng thì coi như thả phím) giờ xoá thêm `_intentCrouch` — nhưng
**không** xoá `_pendingAction`: cùng lý do như `_pendingJump`, mất mạng không phải cớ để nuốt một cú
bấm đã xảy ra thật.

**(d) Lệnh đến từ ngoài luồng tick thì phải xếp hàng.** `WorldService` nhận yêu cầu `Hurt`/`Die` từ
luồng đọc phím (mục dưới), nhưng **không** được sửa entity ngay tại đó. `MoveState` là struct hơn 40
byte; ghi nó từ luồng này trong lúc luồng tick đang đọc thì người đọc có thể thấy **nửa cũ nửa mới** —
không exception, không log, chỉ là một tick với toạ độ vô nghĩa. Cách chữa rẻ và đúng:

```csharp
_forcedActions.Enqueue(...)   // luồng bất kỳ, chỉ ghi vào hàng đợi
// ... đầu Tick(): TryDequeue rồi mới áp dụng lên entity   ← luồng tick, một mình
```

`lock` quanh vòng duyệt cũng chạy được, nhưng hàng đợi mới là hình dạng thật của một game server:
**mọi thứ muốn đổi thế giới đều xếp hàng, thế giới chỉ đổi bên trong tick.** Phase 14 sẽ cần đúng
hình dạng này khi sát thương đến từ một entity khác, và Phase 15 cần nó cho chat.

**`Server/GameServer/Handlers/MoveHandler.cs`** — hai field mới đi qua đúng cửa kiểm mà `DirX` đã đi,
cộng thêm một lớp mà dự án lần đầu gặp:

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

Nhớ giữ nguyên nếp đang có ở file này: **dựng một `MoveIntent` mới từ các trường đã kiểm**, không sửa
tại chỗ rồi dùng lại object của client. Gói tin là dữ liệu của người lạ; chỉ thứ đã qua cửa mới được
đi tiếp vào mô phỏng.

**`Server/GameServer/World/WorldService.cs`** — bốn chỗ:

- `Tick`: gửi `MoveState` thì **không đổi gì** (`State = entity.State` đã gửi trọn struct);
- `BuildSnapshotFor`: mỗi `EntityState` thêm `FacingLeft`, `Crouching`, `Action` — ba phép gán thẳng;
- `Spawn` → `ToSpawnNotice`: cũng thêm hai field đó — người mới lọt vào tầm nhìn phải hiện ra đã đúng
  hướng mặt, không quay đầu một nhịp sau;
- thêm hàng đợi lệnh + hai hàm `EnqueueForceAll` / `EnqueueReviveAll` cho nút thử bên dưới, và drain
  hàng đợi ở **đầu** `Tick`.

**`Server/GameServer/Program.cs` — nút thử.** Tầng 2 có ba trạng thái nhưng mới một cái có nguồn phát
(client xin `Attack`). `Hurt` và `Die` chưa có ai gây ra — hệ thống sát thương là chuyện của Chặng D.
Không có nguồn phát thì không kiểm được, mà không kiểm được thì coi như chưa làm.

Thêm một vòng đọc phím trên thread riêng:

| Phím | Việc |
|---|---|
| `H` | mọi người trong world nhận `Hurt` |
| `K` | mọi người `Die` |
| `J` | mọi người về `None` (hồi sinh tạm) |

`Console.ReadKey` **chặn luồng** nên phải chạy trong `Task.Run` — gọi thẳng trong vòng accept là treo
cả server.

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

    /// <summary>
    /// Bộ số của lớp nhân vật này: tốc độ chạy, độ cao nhảy, thời lượng và hồi chiêu từng hành động.
    /// Tra đúng một lần lúc dựng entity — nó không đổi trong suốt đời entity.
    /// </summary>
    private readonly CharacterProfile _profile;

    public PlayerEntity(int entityId, CharacterRow row, ClientSession owner)
    {
        // ... các dòng gán cũ giữ nguyên ...
        State = MoveState.AtRest(row.X, row.Y);
        Owner = owner;

        // DÒNG DỄ QUÊN NHẤT CỦA CẢ PHASE. Thiếu nó thì Step nhận profile null và ném
        // NullReferenceException ở MỌI tick; GameLoop nuốt lỗi (một tick hỏng không được giết nhịp
        // tim server) nên triệu chứng KHÔNG phải crash mà là: không ai được tích phân, không gói
        // MoveState/WorldSnapshot nào được gửi — người khác thấy bạn đứng hình.
        _profile = CharacterProfiles.Get(row.ClassId);
    }

    /// <summary>Nhận ý định đã được handler làm sạch. Chạy ở luồng IO, không phải luồng tick.</summary>
    public void SetInput(int seq, MoveIntent intent)
    {
        LastInputSeq = seq;

        _intentDirX = intent.DirX;
        _intentCrouch = intent.Crouch;

        // |= chứ không =. Xem comment ở khai báo _pendingJump.
        _pendingJump |= intent.Jump;

        // Cùng ý với |= ở trên: chỉ ghi khi có gì để ghi, đừng để None xoá mất Attack vừa tới.
        if (intent.Action != ActionRequest.None)
            _pendingAction = intent.Action;

        _ticksSinceInput = 0;
    }

    /// <summary>
    /// Cửa DUY NHẤT để phía server đặt trạng thái hành động (trúng đòn, gục). Vẫn phải hỏi luật:
    /// server có quyền hơn client nhưng không có quyền phá bảng chuyển tiếp — gây choáng cho một
    /// xác chết là vô nghĩa dù ai ra lệnh. Một chỗ duy nhất sửa được tầng 2 thì sau này truy
    /// "ai bật Die" là đọc đúng một hàm.
    ///
    /// CHỈ GỌI TỪ LUỒNG TICK. Lệnh sinh ra ở luồng khác phải đi qua hàng đợi của WorldService.
    /// </summary>
    public bool ForceAction(ActionState action)
    {
        // State là property trả về struct nên "State.Action = ..." không biên dịch được: nó sẽ là
        // phép sửa vào một bản copy tạm rồi vứt đi. Copy ra biến, sửa, gán lại — mặt trái của
        // đúng cái tính chất "gán là copy" đã cứu vòng replay ở Phase 8.
        MoveState state = State;

        if (!CharacterStates.CanEnter(state.Action, state.ActionTicksLeft, action))
            return false;

        state.Action = action;

        // Thời lượng tra từ bảng của CHÍNH nhân vật này, không nhận từ người gọi: cùng một đòn thì
        // hai lớp nhân vật choáng khác nhau, và chỗ ra lệnh không cần biết điều đó.
        state.ActionTicksLeft = _profile.GetAction(action).DurationTicks;
        State = state;

        return true;
    }

    /// <summary>
    /// Đặt lại tầng action về None, BỎ QUA bảng chuyển tiếp. Phải là đường riêng chứ không mượn
    /// ForceAction, vì CanEnter chặn mọi lối ra khỏi Die — mà đó là chủ ý: hồi sinh là quyết định
    /// hành chính của server, không phải một bước chuyển trạng thái trong luật chơi.
    /// </summary>
    public void Revive()
    {
        MoveState state = State;

        state.Action = ActionState.None;
        state.ActionTicksLeft = 0;
        State = state;
    }

    public void Integrate(float dt)
    {
        // Quá 1 giây không có input mới → coi như đã thả phím. Chỉ xoá thứ dạng GIỮ; cú bấm dạng
        // cạnh đã chốt vẫn phải được tiêu thụ — mất mạng không phải cớ để nuốt input đã bấm thật.
        if (++_ticksSinceInput > MovementRules.TICK_RATE)
        {
            _intentDirX = 0f;
            _intentCrouch = false;

            // Kẹp lại luôn để bộ đếm không leo tới tràn int khi một client treo hàng năm trời.
            _ticksSinceInput = MovementRules.TICK_RATE + 1;
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

        State = MovementRules.Step(State, intent, dt, _profile);
    }
```

**`Server/GameServer/Handlers/MoveHandler.cs`** — phần dựng intent đã làm sạch:

```csharp
            // Dựng lại intent đã làm sạch thay vì dùng thẳng cái client gửi: gói tin là dữ liệu của
            // người lạ, chỉ những trường đã qua kiểm mới được đi tiếp vào mô phỏng.
            var intent = new MoveIntent
            {
                // Chống hack tốc độ: DirX = 10 là chạy nhanh gấp 10.
                DirX = Math.Clamp(input.Intent.DirX, -1f, 1f),

                // Jump và Crouch không cần kiểm: bool chỉ có hai giá trị. Gửi Jump = true mỗi tick
                // cũng vô ích — điều kiện coyote/buffer nằm trong MovementRules.Step, và Step chạy
                // ở đây chứ không ở máy họ.
                Jump = input.Intent.Jump,
                Crouch = input.Intent.Crouch,

                // Enum trên dây chỉ là một byte do MÁY KHÁC gửi: (ActionRequest)77 hợp lệ hoàn toàn
                // với C#, không khớp nhánh nào và tuỳ chỗ dùng mà im lặng hoặc nổ. Kiểu dữ liệu bảo
                // vệ code khỏi chính mình; kiểm miền giá trị mới là thứ bảo vệ server khỏi người khác.
                Action = Enum.IsDefined(input.Intent.Action) ? input.Intent.Action : ActionRequest.None,
            };

            entity.SetInput(input.Seq, intent);
```

**`Server/GameServer/World/WorldService.cs`** — snapshot và spawn mang thêm ba trường:

```csharp
        private static EntitySpawnNotice ToSpawnNotice(PlayerEntity entity)
        {
            return new EntitySpawnNotice
            {
                EntityId = entity.EntityId,
                Name = entity.Name,
                ClassId = entity.ClassId,
                X = entity.X,
                Y = entity.Y,

                // Hiện ra là đã đúng hướng mặt và đúng tư thế, không quay đầu một nhịp sau.
                FacingLeft = entity.State.FacingLeft,
                Crouching = entity.State.Crouching,
                Action = entity.State.Action,
            };
        }
```

```csharp
                states.Add(new EntityState
                {
                    EntityId = entity.EntityId,
                    X = entity.X,
                    Y = entity.Y,
                    FacingLeft = entity.State.FacingLeft,
                    Crouching = entity.State.Crouching,
                    Action = entity.State.Action,
                });
```

**`Server/GameServer/World/WorldService.cs`** — hàng đợi lệnh cho nút thử:

```csharp
        /// <summary>
        /// Một lệnh đổi trạng thái đến từ NGOÀI luồng tick. Hiện chỉ có nút thử trên console phát ra;
        /// từ Phase 14 thì sát thương của quái và của người chơi khác cũng đi đường này.
        /// </summary>
        private readonly struct ForcedActionCommand
        {
            public readonly ActionState Action;

            /// <summary>Bỏ qua bảng chuyển tiếp — chỉ dùng cho hồi sinh, vì Die không có lối ra hợp lệ.</summary>
            public readonly bool BypassRules;

            public ForcedActionCommand(ActionState action, bool bypassRules)
            {
                Action = action;
                BypassRules = bypassRules;
            }
        }

        // ConcurrentQueue vì bên ghi là luồng đọc phím còn bên đọc là luồng tick. Chỉ hàng đợi này
        // đi qua ranh giới luồng; entity thì không ai ngoài tick được chạm vào.
        private readonly ConcurrentQueue<ForcedActionCommand> _forcedActions = new();

        /// <summary>
        /// Xin gây trạng thái cho TẤT CẢ entity trong world. Gọi được từ luồng bất kỳ — lệnh chỉ
        /// được xếp hàng ở đây, và chỉ thật sự có hiệu lực ở đầu tick kế tiếp.
        ///
        /// Vì sao không sửa thẳng entity tại đây: MoveState là struct hơn 40 byte, ghi nó trong lúc
        /// luồng tick đang đọc thì người đọc có thể thấy nửa cũ nửa mới. Không exception, không log,
        /// chỉ là một tick mang toạ độ vô nghĩa — loại lỗi đắt nhất để tìm.
        /// </summary>
        public void EnqueueForceAll(ActionState action)
        {
            _forcedActions.Enqueue(new ForcedActionCommand(action, bypassRules: false));
        }

        public void EnqueueReviveAll()
        {
            _forcedActions.Enqueue(new ForcedActionCommand(ActionState.None, bypassRules: true));
        }
```

và `Tick` mọc thêm một vòng ở **trước** vòng tích phân:

```csharp
        public void Tick(float dt)
        {
            // Vòng 0: tiêu thụ lệnh đến từ ngoài. Đặt trước vòng tích phân để trạng thái vừa bị áp
            // đặt được chính tick này diễn tiến (đếm ngược, khoá di chuyển), thay vì trễ một nhịp.
            while (_forcedActions.TryDequeue(out ForcedActionCommand command))
            {
                foreach (PlayerEntity entity in _entities.Values)
                {
                    if (command.BypassRules)
                        entity.Revive();
                    else
                        entity.ForceAction(command.Action);
                }
            }

            // Vòng 1: tích phân TẤT CẢ trước. (như cũ)
            ...
        }
```

**`Server/GameServer/Program.cs`** — vòng đọc phím, đặt ngay sau khi `gameLoop` chạy:

```csharp
// Console điều khiển — nguồn phát TẠM cho Hurt/Die tới khi có hệ thống sát thương ở Phase 14.
// Console.ReadKey CHẶN luồng gọi nó, nên phải có luồng riêng: đặt trong vòng accept là treo cả server.
_ = Task.Run(() =>
{
    while (!cts.IsCancellationRequested)
    {
        switch (Console.ReadKey(intercept: true).Key)
        {
            case ConsoleKey.H:
                // Không truyền thời lượng: mỗi entity tra bảng của lớp mình.
                worldService.EnqueueForceAll(ActionState.Hurt);
                Log.Info($"[thử] Toàn map {"HURT".Yellow()}");
                break;

            case ConsoleKey.K:
                worldService.EnqueueForceAll(ActionState.Die);
                Log.Info($"[thử] Toàn map {"DIE".Red()}");
                break;

            case ConsoleKey.J:
                worldService.EnqueueReviveAll();
                Log.Info($"[thử] Toàn map {"hồi sinh".Green()}");
                break;
        }
    }
});
```

Lưu ý vì sao hồi sinh phải là hàm **riêng** chứ không phải `EnqueueForceAll(ActionState.None, 0)`:
`ForceAction` đi qua `CanEnter`, mà `CanEnter` chặn mọi đường ra khỏi `Die`. Gọi cách kia thì bấm `J`
sẽ không có tác dụng gì — và đó là bảng chuyển tiếp đang làm đúng việc của nó, không phải lỗi.

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

**Tốc độ phát: clip phải co cho vừa thời lượng mà LUẬT quy định.** Đây là chỗ trả nợ lời hứa ở Bước 2,
và cũng là câu trả lời cho một câu hỏi rất đúng: *nếu hoạ sĩ đổi số frame hoặc đổi độ dài clip thì phải
sửa gì ở server?*

**Không sửa gì cả.** Hai con số này thuộc hai thế giới khác nhau:

| | Thời lượng hành động | Độ dài clip |
|---|---|---|
| Là gì | **luật chơi** — 0.25s ghi trong `CharacterProfile` | **tài sản mỹ thuật** — hệ quả của số frame và sample rate |
| Ai đổi | game designer, và đó là **đổi cân bằng** | hoạ sĩ, và đó là **đổi hình** |
| Ai đọc | server (mô phỏng) + client (dự đoán) | chỉ client, chỉ để vẽ |
| Đổi thì phải làm gì | sửa bảng → cả hai bên đổi theo, review như một thay đổi cân bằng | không phải làm gì hết |

Ràng buộc giữa hai thế giới nằm gọn trong một dòng ở client:

```csharp
_animator.speed = clip.length / (ticks * MovementRules.TICK_DT);
```

Clip dài hơn thời lượng thì phát nhanh lên cho vừa; ngắn hơn thì phát chậm lại. Nhờ đó hoạ sĩ thêm 2
frame vung tay cho đẹp là **màn hình vẫn đúng 0.25s**, server không biết chuyện đó xảy ra, và không ai
vô tình đổi cân bằng bằng một file `.anim`. Chỉ khi thật sự muốn đòn đánh *dài hơn* thì mới sửa
`durationSeconds` trong bảng — một chỗ, hai bên cùng đổi.

Và `ticks` lấy ở đâu? **Không cần gửi qua mạng.** Người xem biết `ClassId` của đối phương (từ
`EntitySpawnNotice`) nên tra được đúng `CharacterProfile` của họ:

```csharp
int ticks = _profile.GetAction(action).DurationTicks;
```

Đây là phần thưởng thứ hai của việc để bảng ở `Shared`: thời lượng là **kiến thức chung**, nên không
tốn byte nào để đồng bộ — kể cả khi mỗi lớp nhân vật có một bộ số riêng.

**`Assets/Game/Scripts/World/PlayerMotor.cs`** — năm sửa đổi nhỏ, và một chỗ **không** được đụng vào:

1. Chốt nút đánh dạng cạnh y hệt nút nhảy: `_attackLatched` đặt trong `Update`, tiêu thụ trong `Step`.
   Đọc `WasPressedThisFrame` bên trong vòng tick là mất phần lớn cú bấm — bài cũ, lỗi cũ.
2. Đọc `Crouch` dạng **giữ**: `_inputActions.Player.Crouch.IsPressed()`, đọc ở `Update` rồi truyền
   vào `Step`. Trục giữ lấy mức tại thời điểm dựng tick là đúng, khác hẳn nút cạnh — sai chỗ này là
   hiểu sai bài của Phase 8.
3. `Step` dựng `MoveIntent` đủ 4 field và gửi nguyên vẹn qua `WorldApi.Move(seq, intent)`.
4. `Init` nhận thêm `classId` (từ `EnterWorldResponse`), tra `CharacterProfiles.Get(classId)` cất vào
   `_profile`, và chuyển tiếp cho `_characterAnimator.Init(...)`. Ba lời gọi `MovementRules.Step`
   (trong `Step` và trong vòng replay của `OnMoveStateResult`) đều nhận thêm tham số này.
5. Cuối `Update`, đẩy trạng thái sang animator:
   `_characterAnimator.Apply(CharacterStates.Derive(_simState), _simState.Action, _simState.FacingLeft)`.
   Gọi trong `Update` (mỗi frame) chứ không trong `Step` (mỗi tick) — hình ảnh chạy theo frame.

**Không đụng vào `OnMoveStateResult`.** Đây là chỗ dễ vô tình phá nhất, vì hàm này đã được viết lại ở
cuối Phase 8: nó giữ `_prevSimState` để có đầu trái cho đoạn nội suy, và đẩy phần chênh lệch sau
reconciliation vào `_renderOffset` cho tan dần. Chép đè một bản `OnMoveStateResult` "gọn hơn" vào đây
là mất cả hai thứ đó, và triệu chứng (giật nhẹ mỗi lần server sửa) trông **không** giống lỗi của
Phase 9 nên sẽ tốn cả buổi để tìm.

Mà thật ra hàm đó **không cần** sửa gì cả: `response.State` mang trọn `MoveState`, nên `Action`,
`ActionTicksLeft`, `FacingLeft`, `Crouching` được đối chiếu và replay bằng đúng cơ chế đã có — không
có đường đồng bộ riêng nào cho hoạt ảnh. Vòng `Update` cũng giữ nguyên phần
`transform.position = InterpolatedPosition() + _renderOffset;`, chỉ thêm một dòng gọi animator.

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

**`Server/Shared/`** — không có file mới nào ở bước này: `CharacterProfile.GetAction(...)` dựng ở
Bước 2 đã trả lời đủ cả hai câu hỏi mà client cần ("hành động này dài bao nhiêu tick").

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

        /// <summary>
        /// Bộ số của nhân vật đang được vẽ — cần đúng một thứ trong đó: mỗi hành động dài bao nhiêu
        /// tick, để co clip cho vừa. Nhân vật của mình lấy từ ClassId trong EnterWorldResponse,
        /// nhân vật người khác lấy từ ClassId trong EntitySpawnNotice.
        /// </summary>
        private CharacterProfile _profile;

        /// <summary>Gọi ngay sau khi Instantiate, trước lần Apply đầu tiên.</summary>
        public void Init(CharacterProfile profile)
        {
            _profile = profile;
        }

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
            int ticks = _profile.GetAction(action).DurationTicks;
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

**`Assets/Game/Scripts/World/PlayerMotor.cs`** (chỉ những chỗ đổi — phần nội suy và reconciliation của
Phase 8 giữ nguyên):

```csharp
        [SerializeField] private CharacterAnimator _characterAnimator;

        /// <summary>
        /// Bộ số của lớp nhân vật mình đang chơi. Client PHẢI dự đoán bằng đúng bảng server dùng —
        /// lệch một con số là lệch quỹ đạo, và reconciliation sẽ kéo giật liên tục mà không rõ vì sao.
        /// </summary>
        private CharacterProfile _profile;

        public void Init(WorldApi worldApi, WorldNetHandler worldNetHandler, Vector2 spawnPos, int classId)
        {
            _worldApi = worldApi;
            _worldNetHandler = worldNetHandler;
            _profile = CharacterProfiles.Get(classId);

            _simState = MoveState.AtRest(spawnPos.x, spawnPos.y);
            _prevSimState = _simState;

            // Animator cần cùng bảng đó, nhưng chỉ để co clip cho vừa thời lượng.
            _characterAnimator.Init(_profile);

            _worldNetHandler.OnMoveStateResult += OnMoveStateResult;
        }

        /// <summary>
        /// Cú bấm đánh đang chờ tick tới tiêu thụ. Cùng lý do như _jumpLatched: Update chạy 60–300Hz
        /// còn Step chỉ 20Hz, đọc WasPressedThisFrame bên trong vòng tick là bỏ lỡ phần lớn cú bấm.
        /// </summary>
        private bool _attackLatched;

        private void Update()
        {
            if (_worldApi == null)
                return;

            // Hai nút dạng CẠNH: chốt ngay tại frame chúng xảy ra.
            if (_inputActions.Player.Jump.WasPressedThisFrame())
                _jumpLatched = true;

            if (_inputActions.Player.Attack.WasPressedThisFrame())
                _attackLatched = true;

            float dirX = Mathf.Clamp(_inputActions.Player.Move.ReadValue<Vector2>().x, -1f, 1f);

            // Trục GIỮ: lấy MỨC tại lúc dựng tick, không chốt và không gộp. Ngồi là một tư thế kéo
            // dài — thả phím ra là phải đứng dậy ngay tick sau.
            bool crouch = _inputActions.Player.Crouch.IsPressed();

            _accumulator += Time.deltaTime;
            while (_accumulator >= MovementRules.TICK_DT)
            {
                _accumulator -= MovementRules.TICK_DT;

                _prevSimState = _simState;
                Step(dirX, crouch);
            }

            _renderOffset *= Mathf.Exp(-CORRECTION_DECAY * Time.deltaTime);

            transform.position = InterpolatedPosition() + _renderOffset;

            // Hình chạy theo FRAME, không theo tick — gọi ở đây chứ không trong Step.
            // Đọc thẳng _simState (tick mới nhất) chứ không nội suy: VỊ TRÍ thì nội suy cho mượt,
            // còn TRẠNG THÁI thì không có "một nửa giữa idle và walk" để nội suy.
            _characterAnimator.Apply(
                CharacterStates.Derive(_simState), _simState.Action, _simState.FacingLeft);
        }

        /// <summary>Một bước dự đoán: mô phỏng trước, ghi nợ, gửi lên server. Gửi CẢ khi đứng yên — thả phím cũng là input.</summary>
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

            _simState = MovementRules.Step(_simState, intent, MovementRules.TICK_DT, _profile);

            _pending.Add(new PendingInput(seq, intent));
            _worldApi.Move(seq, intent);
        }
```

Trong `OnMoveStateResult` **sửa đúng một chỗ** — vòng replay cũng phải mang profile theo:

```csharp
                state = MovementRules.Step(state, pending.Intent, MovementRules.TICK_DT, _profile);
```

Ngoài dòng đó ra thì `OnMoveStateResult`, `InterpolatedPosition`, `PendingInput` và hai hằng
`CORRECTION_DECAY` / `SNAP_DISTANCE` **không sửa gì**. `PendingInput` đã giữ nguyên cả `MoveIntent` từ
Phase 8 nên hai field mới (`Crouch`, `Action`) tự động có mặt trong vòng replay — nếu hồi đó lưu riêng
`dirX` và `jump` thì hôm nay đã phải quay lại sửa, và quên thì cú đánh sẽ biến mất mỗi lần server ack.

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

**Mở rộng `Sample`** thành `(Time, Pos, FacingLeft, Crouching, Action)`, và `PushState` nhận thêm ba
tham số cùng tên. Thêm một `Init(CharacterProfile)` nữa để chuyển bảng số cho animator — người xem cần
nó để co clip cho vừa thời lượng của **lớp nhân vật kia**, không phải của mình.

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
    Crouching = a.Crouching,
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

**`WorldSpawner`** truyền ba trường mới ở cả `OnEntitySpawn` lẫn `OnSnapshot`, và gọi thêm
`view.Init(CharacterProfiles.Get(notice.ClassId))` lúc spawn. Nhân vật của mình cũng vậy:
`motor.Init(_worldApi, _worldNetHandler, spawnPos, response.ClassId)`. Đây chính là chỗ CHECKPOINT A đã
cảnh báo: thêm field vào DTO không làm bên đọc đỏ, nên phải tự nhớ.

**Prefab.** Gần như không phải làm gì: `Player_Remote` đang là **prefab variant** của `Player_Main`
(nó gỡ `PlayerMotor` ra và thêm `RemotePlayerView` vào). Gắn `CharacterAnimator` lên object con
`DragonWarrior` của `Player_Main` là `Player_Remote` có luôn, và mọi lần chỉnh clip về sau cũng vậy —
đúng tinh thần "một nguồn", lần này ở tầng asset và Unity làm hộ.

Chỉ nhớ **gán tham chiếu `_characterAnimator`** ở cả hai chỗ: trên `PlayerMotor` (prefab gốc) và trên
`RemotePlayerView` (prefab variant). Variant kế thừa component nhưng field của component *mới thêm*
thì vẫn phải kéo, và bỏ trống thì không có lỗi biên dịch — chỉ có `NullReferenceException` lúc người
thứ hai vào world.

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
            public readonly bool FacingLeft;
            public readonly bool Crouching;
            public readonly ActionState Action;

            public Sample(float time, Vector2 pos, bool facingLeft, bool crouching, ActionState action)
            {
                Time = time;
                Pos = pos;
                FacingLeft = facingLeft;
                Crouching = crouching;
                Action = action;
            }
        }

        private readonly List<Sample> _buffer = new();

        /// <summary>Gọi mỗi lần snapshot đến. Mốc thời gian là đồng hồ MÁY MÌNH lúc nhận.</summary>
        /// <summary>Bảng số của LỚP NHÂN VẬT KIA — cần để co clip cho vừa thời lượng của họ.</summary>
        public void Init(CharacterProfile profile)
        {
            _characterAnimator.Init(profile);
        }

        /// <summary>Gọi mỗi lần snapshot đến. Mốc thời gian là đồng hồ MÁY MÌNH lúc nhận.</summary>
        public void PushState(Vector2 pos, bool facingLeft, bool crouching, ActionState action)
        {
            _buffer.Add(new Sample(Time.time, pos, facingLeft, crouching, action));

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

                Crouching = a.Crouching,
            };

            _characterAnimator.Apply(CharacterStates.Derive(sampled), a.Action, a.FacingLeft);
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

            // Bảng số tra từ ClassId của NGƯỜI KIA, không phải của mình.
            view.Init(CharacterProfiles.Get(notice.ClassId));
            view.PushState(new Vector2(notice.X, notice.Y), notice.FacingLeft, notice.Crouching, notice.Action);

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

                view.PushState(new Vector2(state.X, state.Y), state.FacingLeft, state.Crouching, state.Action);
            }
        }
```

</details>

### ✅ CHECKPOINT E — mục tiêu cuối Phase 9

Hai client bằng ParrelSync:

1. A chạy → B thấy A `walk`, đúng hướng. A dừng → B thấy A `idle`, **giữ hướng cũ**.
2. A nhảy → B thấy `jump` lúc lên, `fall` lúc xuống, và tiếp đất là về `idle` ngay chứ không kẹt ở `fall`.
3. A ngồi (`C`) → B thấy `crouch`. Đây là thứ **không suy được** — nó tới từ trường `Crouching` trong
   snapshot. Thử tạm bỏ dòng gán `Crouching` ở `BuildSnapshotFor` để thấy: A ngồi mà B thấy A đứng.
   Trả lại code.
4. A đánh → B thấy `attack` đúng độ dài, đúng hướng A đang nhìn, rồi về `idle`. Không có gói `NetCmd`
   mới nào tham gia — mở log ra kiểm nếu không tin.
5. Gõ `K` ở console server → **cả hai màn hình** đều thấy **cả hai nhân vật** gục. Gõ `J` → cùng đứng dậy.
6. A đứng yên quay mặt sang trái rồi **thoát và vào lại** → B thấy A hiện ra đã quay trái sẵn (nhờ
   `FacingLeft` trong `EntitySpawnNotice`), không quay đầu một nhịp sau.

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
    _simState.ActionTicksLeft = _profile.GetAction(ActionState.Hurt).DurationTicks;
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

Giờ **bỏ tạm** phép kiểm ở `MoveHandler` (`Action = input.Intent.Action` thẳng, không qua
`Enum.IsDefined`) rồi chạy lại. Vẫn không nổ, vẫn không đánh: phép 5
so `== ActionRequest.Attack` nên `77` rơi ra ngoài. Vậy dòng kiểm ấy vô dụng?

Không — nó vô dụng **hôm nay**. Viết thử một `switch (intent.Action)` có `default: throw` xem, hoặc
tưởng tượng ngày `Action` được dùng làm chỉ số vào một mảng cấu hình đòn đánh. Bài học không phải là
"dòng đó cứu bạn hôm nay" mà là: **mọi giá trị đến từ dây đều phải được đưa về miền hợp lệ ngay tại
cửa**, trước khi nó kịp lan vào trong nơi mọi người đều giả định nó sạch. Trả cả hai chỗ về như cũ.

**3. Đổi `durationSeconds` của `Attack` từ `0.25f` thành `1.5f` và không đụng vào clip.**
Build lại `Shared`, chơi. Đòn đánh giờ kéo 1.5 giây và clip `attack` **tự chậm lại cho vừa**, không bị
lặp, không đứng hình. Đổi tiếp thành `0.1f` → đòn đánh nhoáng qua, clip chạy vống lên.

Rồi làm **ngược lại**: trả về `0.25f`, nhưng vào Unity chỉnh sample rate của clip `dw_attack` cho nó
dài gấp đôi. **Không có gì thay đổi trên màn hình, và không phải sửa gì ở server.** Đó là câu trả lời
cho "đổi hoạt ảnh thì phải cập nhật gì": *không cập nhật gì cả* — người làm hình sửa clip thoải mái mà
không đụng được vào cân bằng; người làm cân bằng sửa một ô trong bảng là cả hai đầu dây đổi theo.

Thử nốt cái thứ ba cho đủ bộ: đổi `moveSpeed` của `DRAGON_WARRIOR` từ `5f` thành `9f`, **chỉ build lại
`Shared` cho server mà cố tình không copy DLL sang Unity**. Chạy: nhân vật chạy nhanh trên máy bạn rồi
bị kéo giật về liên tục. Đó là hình ảnh sống của "hai bên đọc hai bảng khác nhau" — và là lý do bảng
này phải nằm ở `Shared`, và vì sao Phase 11 sẽ cần kiểm version bảng lúc đăng nhập.

---

## Troubleshooting

| Triệu chứng | Nguyên nhân thường gặp | Chỗ sửa |
|---|---|---|
| Hoạt ảnh đứng ở frame 0, giật liên tục | Gọi `Animator.Play` mỗi frame thay vì chỉ khi clip đổi | `CharacterAnimator` — kiểm `hash == _currentHash` |
| Đòn đánh bị cắt ngọn / đứng hình chờ | Quên `_animator.speed`, hoặc `GetAction(...)` trả 0 tick (hành động chưa có trong bảng) | `CharacterAnimator.PlayAction` · `CharacterProfiles.Build` |
| `NullReferenceException` trong `PlayAction` | Quên gọi `Init(profile)` sau `Instantiate` | `WorldSpawner` |
| Nhân vật chạy nhanh/chậm rồi bị kéo giật liên tục | Client và server đang đọc hai bảng số khác nhau — DLL chưa được copy sang Unity | Build lại `Server/Shared` |
| Hành động ngắn (< 0.05s) không bao giờ diễn ra | `ToTicks` làm tròn xuống thay vì lên, hoặc thiếu sàn 1 tick | `MovementRules.ToTicks` |
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
| Người khác không bao giờ hiện `crouch` | Quên gán `Crouching` khi dựng `EntityState`, hoặc `WorldSpawner` chưa truyền nó | `WorldService` · `WorldSpawner` |
| Người khác luôn quay mặt phải | Như trên, với bit `FACING_LEFT`; hoặc `flipX` gán nhầm dấu | `CharacterAnimator.Apply` |
| Người khác lúc nào cũng `idle` dù đang chạy | `Draw` không được gọi, hoặc `dt` truyền vào bằng 0 → `VelX` thành NaN/Infinity | `RemotePlayerView.Update` |
| Người khác kẹt ở `fall` sau khi tiếp đất | `EPS` quá nhỏ so với sai số, hoặc lấy mẫu `_buffer[^1]` thay vì hai mẫu đang nội suy | `RemotePlayerView.Draw` |
| Người khác vung tay ở chỗ họ chưa đứng tới | Lấy tư thế/`Action` từ mẫu mới nhất chứ không từ mẫu đang vẽ | `RemotePlayerView.Draw` |
| Nhân vật đứng im hoàn toàn sau khi build `Shared` | Unity còn dùng DLL cũ — post-build chưa copy sang `Assets/Plugins/Shared/` | Build lại `Server/Shared` |
| `MemoryPack` ném lỗi, hoặc nhân vật nhấp nháy ở những toạ độ vô nghĩa sau khi thêm field | Unity còn DLL cũ nên đang đọc bố cục struct cũ — thêm field vào `MoveState` là đổi giao thức | Build lại `Server/Shared`, kiểm ngày sửa của `Assets/Plugins/Shared/MMORPG.Shared.dll` |
| `State.Action = ...` không biên dịch được | `State` là property trả struct — phải copy ra biến, sửa, rồi gán lại | `PlayerEntity.ForceAction` |
| Gõ `H` mà thỉnh thoảng không ăn, hoặc nhân vật nháy một cái sang toạ độ lạ | Đang sửa `State` thẳng từ luồng đọc phím thay vì xếp hàng cho luồng tick áp dụng | `WorldService` — hàng đợi `_forcedActions` |
| Bấm `J` mà không ai đứng dậy | Hồi sinh đang đi qua `ForceAction`, mà `CanEnter` chặn mọi lối ra khỏi `Die` | `WorldService.EnqueueReviveAll` · `PlayerEntity.Revive` |
| Gõ `H` ở console mà server đơ | `Console.ReadKey` gọi thẳng trong luồng chính chứ không trong `Task.Run` | `Program.cs` |
| **Người khác đứng hình hoàn toàn; nhân vật mình vẫn chạy được nhưng server không bao giờ sửa** | `_profile` chưa được gán trong constructor `PlayerEntity` → `Step` ném NRE mỗi tick, `GameLoop` nuốt lỗi nên không có gói nào được gửi. **Nhìn console server: có `Tick ném lỗi` lặp 20 lần/giây** | `PlayerEntity` constructor |
| Người khác di chuyển được nhưng không có hoạt ảnh nào | `RemotePlayerView` chưa gọi `Draw`, hoặc `WorldSpawner` chưa gọi `view.Init(...)` | Bước 5 |
| Nhân vật đứng lơ lửng trên không, hoặc lún vào mặt đất đã vẽ | `GROUND_Y` chưa khớp cao độ mặt đất của tilemap | Bước 0 |
| Chạy một đoạn là kẹt vào tường vô hình giữa map | Biên `±WORLD_HALF_EXTENT` hẹp hơn bề ngang map đã vẽ | Bước 0 |
| Hoạt ảnh của mình giật nhẹ mỗi lần server ack (không liên quan trạng thái) | `OnMoveStateResult` bị chép đè bản không có `_prevSimState` / `_renderOffset` của Phase 8 | `PlayerMotor.OnMoveStateResult` |

---

## Tự kiểm tra hiểu bài

Tự trả lời từng câu xong mới mở đáp án của câu đó.

**Câu 1.** Doc nói tầng locomotion "tốn 0 byte", nhưng `EntityState` vẫn phải thêm 3 trường. Mâu thuẫn
ở đâu, và phát biểu cho đúng thì phải nói thế nào?
<details>
<summary><b>📖 Đáp án câu 1</b></summary>

Không mâu thuẫn, mà là phát biểu thiếu chính xác. Đúng phải là: **tầng locomotion không cần byte nào
cho riêng nó** — 5 giá trị `Idle/Walk/Jump/Fall/Crouch` không bao giờ được gửi. Ba trường thêm vào là
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

**Câu 4.** Thời lượng một hành động được **viết** bằng giây nhưng được **đếm** bằng tick, và **không
bao giờ** lấy từ độ dài `AnimationClip`. Giải thích cả ba vế.
<details>
<summary><b>📖 Đáp án câu 4</b></summary>

**Viết bằng giây** vì đó là đơn vị của người sinh ra con số: hoạ sĩ và game designer nói "một phần tư
giây", "hồi chiêu 3 giây". Bắt họ viết bằng tick là bắt họ biết chi tiết cài đặt của server, và ngày
đổi tick rate thì mọi số họ từng viết đều sai.

**Đếm bằng tick** vì mô phỏng cần số nguyên: `ActionTicksLeft--` rồi so `<= 0` là chính xác tuyệt đối
ở cả hai đầu dây và chạy lại bao nhiêu lần cũng ra đúng thế (replay!). Cộng dồn `elapsedSeconds += dt`
thì hai bên lệch được ở chữ số cuối, và lệch một tick là một lần rubber-band. Chuyển đổi vì thế nằm ở
`ToTicks`, chạy một lần lúc dựng bảng — cái giá là thời lượng bị lượng tử hoá theo 50ms.

**Không lấy từ clip** vì ba lý do độc lập: (1) `GameServer` là process .NET, ở đó không tồn tại
`AnimationClip`; (2) clip là tài sản của người làm hình — thời lượng ăn theo clip thì thêm 2 frame
vung tay cho đẹp là **đổi cân bằng game**, mà không ai review một file `.anim` như review cân bằng;
(3) độ dài clip là con số của *một* máy client, không phải của giao thức.

Hệ quả kéo theo: clip phải co giãn cho vừa thời lượng (`animator.speed`), không phải ngược lại.

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

## Bốn thứ bạn định làm tiếp — và ai sở hữu cái gì

Bốn tính năng dưới đây chưa làm ở phase này, nhưng đáng phân loại **ngay bây giờ**: mỗi cái rơi vào một
ô khác nhau của cùng một bảng, và biết ô nào là biết trước sẽ tốn bao nhiêu.

| Tính năng | Ai sở hữu | Tốn gì trên dây | Làm ở đâu |
|---|---|---|---|
| Khói bốc lên ở chân khi tiếp đất | **Client, hoàn toàn** | 0 byte | Ngay sau Phase 9 nếu muốn |
| Fireball bay ra khi đánh | **Server** — nó là entity | 1 lệnh spawn + vị trí mỗi tick | Phase 14 |
| Explosion khi fireball trúng | **Client**, kích bởi sự kiện của server | đi ké gói despawn của projectile | Phase 14 |
| Ngồi để né đạn | **Server** — hộp va chạm | 0 byte (đã có `Crouching`) | Phase 14 |

**Khói ở chân — thứ rẻ nhất, và là bài kiểm tra xem bạn đã tách tầng đúng chưa.** Nó là *hệ quả nhìn
thấy được* của một chuyển tiếp đã có sẵn trong dữ liệu: `Fall` → (`Idle`|`Walk`). Client chỉ cần nhớ
`LocomotionState` của frame trước và so với frame này; đổi từ `Fall` sang thứ khác là spawn khói. Đúng
**một** chỗ làm được cho cả mình lẫn người khác — `CharacterAnimator.Apply` nhận đủ dữ liệu rồi.

Không có `NetCmd` nào, không thêm field nào, server không biết khói tồn tại. Vì sao được phép? Vì khói
**không ảnh hưởng tới ai**: sai một frame thì không ai chết. Ngày nào khói làm chậm người chạy qua nó
thì nó thành luật chơi, và luật chơi thì phải về `Shared` + `Step`.

> Một cảnh báo nhỏ: với **người khác**, `Grounded` đang được *suy* từ `Δy ≈ 0` giữa hai mẫu snapshot.
> Mạng dồn gói có thể cho hai mẫu trùng vị trí giữa lúc họ đang bay → một lần "tiếp đất" giả → khói nở
> giữa không trung. Hoạt ảnh sai một frame thì không ai thấy, nhưng một cụm khói thì có. Nếu gặp, cách
> chữa là gửi thẳng `Grounded` trong `EntityState` — thêm đúng 1 byte, và lúc đó nó **xứng đáng**.

**Fireball — chỗ trực giác Unity sai nặng nhất.** Phản xạ tự nhiên: bấm đánh → `Instantiate(fireball)`
→ `OnTriggerEnter2D` → trừ máu. Cả ba bước đều ở client, và cả ba đều sai:

| | Fireball là particle của client | Fireball là entity của server |
|---|---|---|
| Ai thấy nó | chỉ người bắn | mọi người trong tầm nhìn, ở **cùng một chỗ** |
| Ai quyết định trúng | máy của người bắn | server |
| Hack bằng cách nào | sửa client là bắn 100 quả, trúng từ bên kia map | không có gì để sửa |
| Người bị bắn thấy gì | một quả cầu tự nhiên bốc hơi, máu tự trừ | quả cầu bay tới, chạm, nổ |

Nên fireball có `X`, `Y`, `VelX`, một `entityId`, và **được `Step` mỗi tick ở server** y như người chơi.
Client chỉ vẽ nó — đúng vai trò `RemotePlayerView` đang làm với người khác. Đó là bài của Phase 14, và
Phase 9 đã dựng sẵn hai mảnh cho nó: hướng bắn (`FacingLeft` do server chốt, khoá trong lúc đánh) và
nhịp thời gian (`ActionTicksLeft`).

Mảnh còn thiếu, ghi lại để Phase 14 nhớ: **quả cầu bay ra ở tick thứ mấy của đòn đánh?** Không phải
"khi hoạt ảnh tới frame 2" — client không được quyết cái đó. Nó là thêm một con số trong
`ActionDefinition`, viết bằng giây như mọi con số khác:

```csharp
new ActionDefinition(durationSeconds: 0.25f, cooldownSeconds: 0.4f, locksMovement: false,
                     hitAtSeconds: 0.1f)
```

Client thì làm điều ngược lại của `animator.speed`: canh clip sao cho **frame vung tay rơi đúng vào
tick đó**. Hình phục vụ luật, không phải luật chạy theo hình.

**Explosion** thì lại là client thuần, giống khói: server gửi "projectile 77 biến mất vì trúng entity
12", client dựng hiệu ứng ở chỗ đó. Sự kiện là của server, hạt lửa là của client.

**Ngồi để né đạn — món này Phase 9 đã trả trước gần hết tiền.** Server cần biết thân người cao bao
nhiêu tại thời điểm va chạm, mà `Crouching` thì **đã nằm trong `MoveState`** và đã được cả hai bên mô
phỏng giống nhau. Việc còn lại ở Phase 14 chỉ là hai con số nữa trong `CharacterProfile`
(`standHeight`, `crouchHeight` — đơn vị world, không phải giây) và một phép kiểm hình chữ nhật.

Đây chính là lý do ở Bước 1 ta nhất quyết coi `Crouching` là **sự thật vật lý** chứ không phải chuyện
hình ảnh, và nhất quyết để `CharacterStates.Derive` ở `Shared` dù server chưa gọi nó lần nào. Nếu hồi
đó cho `Crouching` sống ở client (một `bool` trong `PlayerMotor`, "chỉ để đổi sprite thôi mà") thì hôm
nay muốn né đạn sẽ phải làm lại từ đầu: thêm field vào state, thêm vào snapshot, sửa `Step`, sửa cả
prediction — và trong lúc chưa làm xong thì có một bug rất khó chịu: **người chơi thấy mình ngồi né
được, server thì không thấy.**

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
- **Nén cờ vào bit.** `FacingLeft` + `Crouching` + `Action` hiện tốn 3 byte và chỉ dùng hết 4 bit.
  Ngày số cờ lên 10–15 (choáng, tàng hình, cưỡi thú, đang giao dịch…) **và** profiler chỉ đúng vào băng
  thông snapshot thì mới nén — nén ở đúng một chỗ trong `Shared`, không phải mặt nạ bit chép tay hai bên.
  Trước lúc đó thì đây là tối ưu mù, và cái giá của nó là mọi lần đọc code về sau.
- **Bảng số ra file.** `CharacterProfiles.Build()` đang dựng bằng C#. Phase 11 đổi nó thành đọc từ file
  (sửa số không cần build lại) + kiểm version lúc đăng nhập để client không bao giờ chạy bảng khác
  server. Chỗ gọi (`profile.MoveSpeed`, `profile.GetAction(...)`) thì không đổi một dòng.
- **Từ hành động sang chiêu thức.** `ActionState` hiện là một enum nhỏ đủ cho đánh thường. Khi có skill,
  hình dạng đúng là `MoveState` mang thêm `skillId`, và bảng tra đổi từ `ActionState → ActionDefinition`
  thành `skillId → SkillDefinition` (thời lượng, hồi chiêu, thời điểm ra đòn, có khoá thân không —
  **vẫn viết bằng giây**). Vì `Step` đã nhận bảng qua tham số nên đổi này không lan ra khắp nơi.
- **Hoạt ảnh cho hiệu ứng** (`fireball`, `explosion` trong `Textures/Dragon Warrior Files/Effects`) —
  xem mục "Bốn thứ bạn định làm tiếp" ở trên.

---

**Xong Phase 9 → nhân vật đã biết diễn, và ranh giới "ai quyết cái gì" đã rõ ở tầng hình ảnh.**
Thế giới thì vẫn là mặt phẳng tạm của Bước 0, và mọi người vẫn nhận gói của tất cả mọi người.

[PHASE-10](PHASE-10.md) cho thế giới hình dạng thật, và làm theo đúng cách một game thật làm: thêm
một lớp `Collision` vào tilemap (vẽ tay bằng tile màu: ô đặc, bệ xuyên-một-chiều), một tool trong
Editor **export ra file map** — kích thước và origin bất kỳ, kèm config riêng của map (điểm spawn,
cửa sang map khác) — rồi **cả server lẫn client cùng đọc đúng file đó**. Hình dạng map từ đó có một
nguồn duy nhất, và nguồn ấy chính là thứ bạn nhìn thấy trong Scene view.
