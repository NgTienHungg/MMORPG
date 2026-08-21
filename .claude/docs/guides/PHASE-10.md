# PHASE 10 — Map & AOI: thế giới có hình dạng và tầm nhìn

> **Kết quả cuối Phase 10:** thế giới không còn là một mặt phẳng vô hình ở `y = 0`. Có sàn, có tường
> chặn ngang, có **bệ xuyên-một-chiều** — nhảy từ dưới lên thì lọt qua, đứng được ở trên, bấm xuống
> kèm nhảy thì tụt xuống. Ngồi thì thân thấp lại và chui được vào khe hẹp, nhưng **không đứng dậy được
> dưới trần thấp**. Và AOI (Area of Interest): chỉ nhận gói tin của người ở gần — chạy xa nhau thì biến
> mất khỏi màn hình của nhau, chạy lại thì hiện ra.
>
> **Điều kiện:** xong [`PHASE-9.md`](PHASE-9.md) tới CHECKPOINT E — hai client thấy nhau chạy, nhảy,
> ngồi, đánh đúng trạng thái.
>
> **Bài học chính:** (1) va chạm là **luật chơi** nên nó phải nằm ở server — và vì client cũng dự đoán,
> hình dạng map phải là contract một nguồn y như `NetCmd`; (2) **hình** và **luật** là hai lớp khác
> nhau, và cách duy nhất giữ chúng khớp nhau lâu dài không phải là cẩn thận mà là **sinh cái này ra từ
> cái kia**; (3) MMO không broadcast toàn map — chia ô không gian biến `EntitySpawn`/`EntityDespawn` từ
> "sự kiện vào/ra world" thành "hệ quả của tầm nhìn", và client không phải sửa một dòng nào.

Format như trước: **hướng làm** hiện sẵn, **📖 Lời giải** trong foldout.

---

## Hai việc, một nguyên tắc

Phase này làm hai việc nhìn ngoài chẳng liên quan — hình dạng thế giới và tầm nhìn — nhưng chung một
nguyên tắc: **thế giới được chia thành ô, và mọi câu hỏi không gian trả lời bằng toạ độ ô.**

- **Va chạm**: "ô này là gì?" — lưới ô 1×1 unit, mỗi ô là rỗng, đặc, hoặc bệ xuyên-một-chiều.
- **AOI**: "ai đứng gần ai?" — chia map thành **cột** rộng 12 unit; "gần" nghĩa là trong 3 cột quanh
  mình. Không bao giờ phải tính khoảng cách từng cặp người chơi (O(n²)) — chỉ tra cột.

Hai lưới khác nhau cho hai mục đích khác nhau, và đó là chuyện bình thường: lưới va chạm mịn vì tường
mỏng, lưới tầm nhìn thô vì màn hình rộng.

```
Shared: MapGrid ('#' đặc · '=' bệ · '.' rỗng)  ──┬──► GameServer: Step() va chạm     (SỰ THẬT)
                                                 ├──► Client:     Step() va chạm     (DỰ ĐOÁN — cùng hàm)
                                                 └──► Client:     gizmo đối chiếu    (KIỂM hình vẽ)
```

Chú ý mũi tên thứ ba. Với top-down thì client vẽ map **từ** `MapGrid` là xong: ô đi được vẽ màu này, ô
tường vẽ màu kia. Platformer thì không — tileset American Forest có cỏ, hàng rào, cây, nền trời, và
một hình đẹp **không sinh ra được** từ một lưới "đặc / rỗng".

Nên phase này phải trả lời một câu mà bản top-down né được: **hình và luật đã là hai thứ khác nhau thì
làm sao không cho chúng trôi xa nhau?** Bước 2 dành trọn cho câu đó.

### Một quyết định nhỏ, dọn được cả một lớp bug

Map đặt gốc toạ độ ở **góc dưới-trái**, không phải ở giữa map: ô `(0, 0)` chiếm vùng world
`[0,1] × [0,1]`. Đổi ô ↔ world chỉ còn `Floor(x)` và `cx`, không có `/2f` ở đâu cả.

Bản top-down đặt map giữa gốc toạ độ (vì hồi đó spawn cố định ở `(0,0)`) và phải kèm một cảnh báo
riêng: *"giữ cạnh map là số chẵn, không thì phép chia nguyên khi vẽ hình và phép chia thực khi va chạm
lệch nhau nửa ô"*. Bỏ phép chia đi thì cảnh báo ấy không còn lý do tồn tại. Vị trí spawn đã lấy từ DB
từ Phase 5 nên không mất gì.

> Bug tốt nhất là bug **không diễn đạt được**. Ở Phase 9 ta bỏ `Hurt` khỏi enum client gửi; ở đây ta bỏ
> phép chia đôi. Cùng một cách nghĩ, hai tầng khác nhau.

---

## Bước 1 — Shared: `MapGrid`, thân nhân vật, và va chạm trong `Step`

### Hướng làm

**File mới `Server/Shared/World/MapGrid.cs`.** Ba loại ô, và loại thứ ba định nghĩa thể loại platformer:

| Ký tự | `CellType` | Ý nghĩa |
|---|---|---|
| `.` | `Empty` | đi xuyên qua thoải mái |
| `#` | `Solid` | chặn mọi hướng |
| `=` | `OneWay` | **chỉ** chặn khi đang rơi xuống và chân đã ở trên mặt bệ từ trước |

Bệ xuyên-một-chiều là ví dụ đẹp nhất trong cả dự án về việc **va chạm phụ thuộc trạng thái chứ không
chỉ phụ thuộc vị trí**. Cùng một ô, cùng một toạ độ nhân vật, mà chặn hay không còn tuỳ nó đang đi lên
hay đi xuống và trước đó nó ở đâu. Đây chính là lý do Phase 8 phải gom vận tốc vào `MoveState`: không
có vận tốc thì câu hỏi "ô này có chặn không" **không trả lời được**.

Hàng đầu của mảng chuỗi là mép **trên** map (để code đọc như bản vẽ) nên `Parse` phải lật trục Y —
quyết định rồi ghi comment, không thì ba tháng sau chính bạn vẽ map mới sẽ ngửa mặt hỏi vì sao map lộn
ngược. Ngoài rìa map coi như `Solid`: tường bao ngầm định, và `WORLD_HALF_EXTENT` của Phase 6 xoá được
luôn.

**File mới `Server/Shared/World/Maps.cs`** — `Maps.Map1`. Map phải **rộng hơn tầm nhìn AOI** (Bước 3
dùng 3 cột × 12 unit = 36 unit) thì mới thấy được cảnh người biến mất khi chạy xa. Mặt đất là hàng ô
dưới cùng nên mặt sàn nằm ở `y = 1`.

**Nhân vật hết là một điểm.** Phase 8 ghi nợ chuyện này và giờ phải trả: một điểm thì lọt qua khe
tường, đứng cân bằng trên góc nhọn, và chui đầu qua trần. Thân nhân vật là một hộp:

| Hằng | Giá trị | Ghi chú |
|---|---|---|
| `BODY_HALF_WIDTH` | `0.35f` | nửa bề ngang. Hẹp hơn `0.5` để lọt vừa khe rộng đúng 1 ô |
| `BODY_HEIGHT` | `1.6f` | cao khi đứng |
| `BODY_HEIGHT_CROUCH` | `0.9f` | cao khi ngồi — thấp hơn 1 ô nên chui được vào khe cao 1 ô |

Gốc toạ độ của nhân vật ở **chân** (khớp pivot Bottom đã đặt ở Phase 8), nên thân chiếm vùng
`[X - HW, X + HW] × [Y, Y + H]`.

`BODY_HEIGHT_CROUCH` là món quà từ Phase 9: `Crouching` đã nằm sẵn trong `MoveState` và đã được cả hai
bên mô phỏng, nên "ngồi thì thân thấp lại" là một dòng `if` chứ không phải một tính năng.

**Va chạm — tách trục, X trước Y sau.** Vẫn là ý của bản top-down nhưng lý do đã khác: không còn là
"trượt dọc tường khi đi chéo" mà là **giải hai bài toán một chiều thay vì một bài toán hai chiều**. Hai
chiều cùng lúc thì phải trả lời "đâm vào góc thì coi là đụng tường hay đụng sàn?" — câu hỏi không có
đáp án đúng. Tách ra thì câu hỏi ấy không tồn tại.

```
1. X += VelX·dt   →  quét cạnh đứng theo hướng đi   →  chạm thì X dán sát mép ô, VelX = 0
2. Y += VelY·dt   →  quét cạnh ngang theo hướng đi  →  chạm thì Y dán sát mép ô, VelY = 0
                                                        đi xuống mà chạm  →  Grounded = true
```

**Quét bao nhiêu điểm trên mỗi cạnh?** Thân cao 1.6 mà ô cao 1.0 → hai điểm ở hai đầu là **bỏ sót** ô
ở giữa. Ba điểm (chân, giữa, đầu) cho khoảng cách lớn nhất 0.75 < 1.0 → không ô nào lọt. Với cạnh ngang
thì hai điểm ở hai góc là đủ vì thân rộng 0.7 < 1.0. "Khoảng cách giữa hai điểm quét phải nhỏ hơn cạnh
ô" là **luật**, không phải mẹo — viết nó vào comment, vì ngày ai đó chỉnh `BODY_HEIGHT` lên 2.2 thì ba
điểm không còn đủ.

**Chống tunneling: chỉ quét quãng đường ở trục có thể vượt một ô.** Nhìn con số:

| Trục | Tốc độ tối đa | Quãng/tick | Vượt được 1 ô? |
|---|---|---|---|
| Ngang | `MOVE_SPEED = 5` | 0.25 | không |
| Lên | `JUMP_SPEED = 11` | 0.55 | không |
| **Xuống** | `MAX_FALL_SPEED = 20` | **1.00** | **có — sát kịch trần** |

Nên **chỉ chiều rơi** cần quét cả quãng đường (duyệt từng hàng ô đi qua); ba chiều còn lại kiểm điểm
cuối là đủ. Đây là bản trả nợ tử tế cho món "chống tunneling" của Phase 8, và cái hay là nó chỉ ra
chính xác **chỗ nào** cần quét thay vì quét hết cho chắc.

**Bệ xuyên-một-chiều — ba điều kiện, thiếu cái nào cũng ra một bug kinh điển:**

1. đang **đi xuống** (`VelY <= 0`) — thiếu thì nhảy từ dưới lên bị cộc đầu;
2. chân **đã ở trên** mặt bệ trước khi dịch chuyển (`prevFeetY >= RowTop(cy)`) — thiếu thì đi ngang vào
   cạnh bệ là bị bắn lên mặt bệ;
3. **không** đang chủ động rơi xuyên (`DropThroughTicks == 0`).

Điều (3) là tính năng "ngồi + nhảy để tụt xuống bệ dưới", và nó cần thêm **một field nữa** vào
`MoveState`: `DropThroughTicks`, đặt bằng một hằng lúc bấm tổ hợp, giảm dần mỗi tick. Lại đúng bài học
cũ: Phase 8 trả hai `int` cho coyote time, Phase 9 trả năm field cho hoạt ảnh, giờ thêm một `int` cho
một thao tác mà người chơi thậm chí không biết tên. Không có gì miễn phí ở phía sau "server là source
of truth".

**Không đứng dậy được dưới trần thấp.** Khi người chơi thả nút ngồi, phải hỏi map xem chỗ đó có đủ 1.6
unit trống không; không đủ thì **giữ nguyên tư thế ngồi**. Bỏ qua bước này thì thân nhân vật nở ra bên
trong trần và tick sau bị đẩy ra chỗ khó đoán.

Đây là một ý đáng dừng lại: **trạng thái có thể bị thế giới từ chối.** `Crouching` không còn là "người
chơi có bấm nút không" mà là "người chơi có bấm nút, *và* thế giới có cho phép không". Mọi trạng thái
liên quan tới thân thể sau này (nằm, biến hình, cưỡi thú) đều có dạng ấy.

**Sửa `MovementRules.Step`** — nhận thêm `MapGrid map`, và các phép cuối viết lại:

```
… phép 0, 2–4 giữ nguyên như Phase 9 …
1'. Tư thế        muốn ngồi     → ngồi
                  muốn đứng dậy → chỉ đứng nếu CanStandUp(map, state)
5'. Rơi xuyên     Crouch && Jump && Grounded && ô dưới chân là OneWay
                  → DropThroughTicks = DROP_THROUGH_TICKS, tiêu thụ luôn cú nhảy
5.  Xin hành động (như Phase 9)
6.  Tích phân X   → giải va chạm ngang
7.  Tích phân Y   → giải va chạm dọc (quét quãng, xử lý OneWay)
8.  (xoá — biên map thay cho kẹp WORLD_HALF_EXTENT)
```

Chú ý phép 5': tổ hợp ngồi+nhảy phải **chặn** cú nhảy bình thường của phép 4, nếu không người chơi vừa
tụt xuống vừa bật lên. Cách gọn nhất là xử lý nó **trước** phép nhảy và cho nó tiêu thụ luôn
`TicksSinceJumpRequest`.

Hai chỗ gọi `Step` (server `Integrate`, client `PlayerMotor` — cả bước dự đoán **lẫn vòng replay**)
truyền thêm `Maps.Map1`.

**Và một việc dễ quên: người chơi cũ đang đứng ở đâu?** Vị trí trong DB được lưu từ những phase mà thế
giới còn là mặt phẳng vô hình — `(0, 0)` chẳng hạn, mà `(0, 0)` bây giờ nằm **trong lòng đất**. Cần một
hàm `ResolveSpawn` đẩy điểm spawn lên chỗ đứng được gần nhất.

Đừng coi đây là việc dọn dẹp một lần. **Map là dữ liệu sửa được, còn vị trí người chơi thì đã lưu rồi:**
mỗi lần người thiết kế map xây thêm một bức tường là một lần có ai đó đang offline ở đúng chỗ ấy. Game
thật nào cũng có hàm này.

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`Server/Shared/World/MapGrid.cs`** (file mới):

```csharp
using System;

namespace MMORPG.Shared.World
{
    /// <summary>Loại ô. Ba loại, và loại thứ ba là thứ làm nên thể loại platformer.</summary>
    public enum CellType : byte
    {
        Empty = 0,
        Solid = 1,

        /// <summary>
        /// Bệ xuyên-một-chiều: đứng được ở trên, nhảy từ dưới lên thì lọt qua.
        /// Ô này chặn hay không KHÔNG chỉ phụ thuộc vị trí mà còn phụ thuộc vận tốc và vị trí ở
        /// tick trước — đó là lý do vận tốc phải là một phần của trạng thái chứ không suy ra được.
        /// </summary>
        OneWay = 2,
    }

    /// <summary>
    /// Hình dạng map dạng lưới ô 1×1. Nguồn DUY NHẤT về việc đi được chỗ nào: server va chạm thật,
    /// client va chạm dự đoán, và gizmo đối chiếu đều đọc từ đây.
    ///
    /// Cố tình KHÔNG mô tả hình thức (cỏ, cây, nền trời) — đó là việc của tilemap bên client.
    /// Một lưới ba trạng thái không đủ để vẽ đẹp, và một hình đẹp thì thừa thãi với va chạm.
    ///
    /// Gốc toạ độ ở GÓC DƯỚI-TRÁI: ô (0,0) chiếm vùng world [0,1] × [0,1]. Không có phép chia đôi
    /// nào ở đây, và vì vậy không có lớp bug "lệch nửa ô khi cạnh map lẻ".
    /// </summary>
    public sealed class MapGrid
    {
        public const float CELL_SIZE = 1f;

        public int Width { get; }
        public int Height { get; }

        private readonly CellType[,] _cells;

        private MapGrid(CellType[,] cells, int width, int height)
        {
            _cells = cells;
            Width = width;
            Height = height;
        }

        /// <summary>
        /// Dựng từ mảng chuỗi: '#' đặc, '=' bệ một chiều, mọi ký tự khác là rỗng.
        /// Hàng ĐẦU của mảng là mép TRÊN map (để code đọc như bản vẽ) — nên trục Y phải lật khi
        /// nạp: hàng cuối của mảng thành cy = 0.
        /// </summary>
        public static MapGrid Parse(string[] rows)
        {
            int height = rows.Length;
            int width = rows[0].Length;

            var cells = new CellType[width, height];

            for (int rowIndex = 0; rowIndex < height; rowIndex++)
            {
                // Hàng rào dựng ngay lúc khởi động: map lệch một ký tự thì nổ tại đây, chỗ dễ sửa,
                // thay vì thành một ô tường vô hình mà ba tuần nữa mới có người đi vào.
                if (rows[rowIndex].Length != width)
                    throw new ArgumentException($"Hàng {rowIndex} dài {rows[rowIndex].Length}, các hàng phải cùng {width} ký tự.");

                for (int cx = 0; cx < width; cx++)
                {
                    int cy = height - 1 - rowIndex; // lật trục Y

                    cells[cx, cy] = rows[rowIndex][cx] switch
                    {
                        '#' => CellType.Solid,
                        '=' => CellType.OneWay,
                        _ => CellType.Empty,
                    };
                }
            }

            return new MapGrid(cells, width, height);
        }

        public CellType At(int cx, int cy)
        {
            // Ngoài rìa map là tường đặc ngầm định — không cần viền clamp riêng, và cũng là lý do
            // WORLD_HALF_EXTENT của Phase 6 xoá được.
            if (cx < 0 || cx >= Width || cy < 0 || cy >= Height)
                return CellType.Solid;

            return _cells[cx, cy];
        }

        // Floor chứ không phải ép kiểu (int): cast cắt VỀ PHÍA 0 nên -0.5 thành 0, trong khi
        // Floor(-0.5) = -1. Map nằm ở toạ độ dương nên hôm nay hai phép cho cùng kết quả — nhưng
        // nhân vật bị đẩy ra ngoài mép trái map thì có toạ độ âm, và lúc đó cast trả về ô 0 tức là
        // "vẫn trong map" trong khi thực tế đã ra ngoài.
        public int CellX(float worldX)
        {
            return (int)MathF.Floor(worldX / CELL_SIZE);
        }

        public int CellY(float worldY)
        {
            return (int)MathF.Floor(worldY / CELL_SIZE);
        }

        public float RowTop(int cy)
        {
            return (cy + 1) * CELL_SIZE;
        }

        public float RowBottom(int cy)
        {
            return cy * CELL_SIZE;
        }

        public float ColumnLeft(int cx)
        {
            return cx * CELL_SIZE;
        }

        public float ColumnRight(int cx)
        {
            return (cx + 1) * CELL_SIZE;
        }

        public CellType AtWorld(float x, float y)
        {
            return At(CellX(x), CellY(y));
        }
    }
}
```

**`Server/Shared/World/Maps.cs`** (file mới):

```csharp
namespace MMORPG.Shared.World
{
    /// <summary>
    /// Hình dạng các map, viết bằng chữ: '#' đặc · '=' bệ xuyên-một-chiều · '.' rỗng.
    /// Đọc như bản vẽ — hàng đầu là mép trên map, hàng cuối là mặt đất.
    /// </summary>
    public static class Maps
    {
        public static readonly MapGrid Map1 = MapGrid.Parse(new[]
        {
            "################################################",
            "#..............................................#",
            "#........===...................===.............#",
            "#..............................................#",
            "#..####..................####..................#",
            "#..............................................#",
            "#.............===.................===..........#",
            "#..............................................#",
            "#......####.................####...............#",
            "#..............................................#",
            "#...........===.......................===......#",
            "#..............................................#",
            "#....###.........####...............###........#",
            "#.........########.............................#",
            "#..............................................#",
            "################################################",
        });
    }
}
```

> Map của bạn cứ tự vẽ theo ý — chỉ cần: viền `#` kín, **mọi hàng cùng độ dài** (`Parse` ném lỗi ngay
> lúc khởi động nếu lệch, đó là hàng rào của bạn), rộng hơn 36 unit để thấy được AOI, có vài bệ `=` để
> thử xuyên qua, và ít nhất một chỗ **khe cao đúng 1 ô** để thử ngồi-chui (ở map trên là dải `########`
> ở hàng áp chót — nó tạo một hành lang cao 1 unit ngay trên mặt đất).

</details>

<details>
<summary><b>📖 Lời giải — va chạm trong <code>MovementRules</code></b></summary>

**`Server/Shared/World/MoveState.cs`** — thêm một field:

```csharp
        /// <summary>
        /// Số tick còn được phép rơi xuyên bệ một chiều. Đặt khi người chơi bấm ngồi + nhảy, giảm
        /// dần mỗi tick. Phải nằm trong trạng thái (chứ không phải một biến riêng ở server) vì
        /// client cũng mô phỏng bước này, và replay phải tái hiện được nó.
        /// </summary>
        public int DropThroughTicks;
```

**`Server/Shared/World/MovementRules.cs`** — hằng thân nhân vật và các hàm va chạm:

```csharp
        /// <summary>Nửa bề ngang thân. Hẹp hơn nửa ô để nhân vật lọt vừa khe rộng đúng 1 ô.</summary>
        public const float BODY_HALF_WIDTH = 0.35f;

        /// <summary>Chiều cao thân khi đứng. Gốc toạ độ ở CHÂN nên thân chiếm [Y, Y + cao].</summary>
        public const float BODY_HEIGHT = 1.6f;

        /// <summary>Chiều cao khi ngồi — thấp hơn 1 ô nên chui được vào khe cao đúng một ô.</summary>
        public const float BODY_HEIGHT_CROUCH = 0.9f;

        /// <summary>Số tick bỏ qua va chạm với bệ một chiều sau khi bấm ngồi + nhảy.</summary>
        public const int DROP_THROUGH_TICKS = 6;

        /// <summary>Lùi vào trong một chút khi quét mép thân, để điểm quét không rơi đúng đường biên ô.</summary>
        private const float EDGE = 0.01f;

        private static float BodyHeight(bool crouching)
        {
            return crouching ? BODY_HEIGHT_CROUCH : BODY_HEIGHT;
        }

        private static bool IsSolid(MapGrid map, float x, float y)
        {
            return map.AtWorld(x, y) == CellType.Solid;
        }

        /// <summary>
        /// Thân (đặt tại x, y, cao height) có đè lên ô đặc nào không. Bệ một chiều KHÔNG tính:
        /// nó chỉ chặn theo chiều rơi, còn đứng lọt trong nó là chuyện bình thường.
        ///
        /// Quét 6 điểm = 2 mép ngang × 3 mức cao. Ba mức vì thân cao 1.6 mà ô cao 1.0: hai điểm ở
        /// hai đầu thì ô ở giữa lọt qua khe kiểm. LUẬT: khoảng cách giữa hai mức phải NHỎ HƠN cạnh
        /// ô — với 1.6 thì ba mức cách nhau 0.75, an toàn; nâng BODY_HEIGHT quá 2.0 là phải thêm mức.
        /// </summary>
        private static bool OverlapsSolid(MapGrid map, float x, float y, float height)
        {
            float left = x - BODY_HALF_WIDTH;
            float right = x + BODY_HALF_WIDTH;

            float footY = y + EDGE;
            float midY = y + height * 0.5f;
            float headY = y + height - EDGE;

            return IsSolid(map, left, footY) || IsSolid(map, right, footY)
                || IsSolid(map, left, midY) || IsSolid(map, right, midY)
                || IsSolid(map, left, headY) || IsSolid(map, right, headY);
        }

        /// <summary>Có đủ chỗ trống để đứng thẳng dậy tại chỗ đang đứng không.</summary>
        public static bool CanStandUp(MapGrid map, in MoveState state)
        {
            return !OverlapsSolid(map, state.X, state.Y, BODY_HEIGHT);
        }

        /// <summary>Hàng ô ngay dưới chân có phải bệ một chiều không — điều kiện để được tụt xuống.</summary>
        private static bool StandingOnOneWay(MapGrid map, in MoveState state)
        {
            float probeY = state.Y - EDGE;

            return map.AtWorld(state.X - BODY_HALF_WIDTH, probeY) == CellType.OneWay
                || map.AtWorld(state.X + BODY_HALF_WIDTH, probeY) == CellType.OneWay;
        }

        /// <summary>
        /// Dịch theo trục X rồi dán lại nếu đâm tường. Tách khỏi trục Y để không phải trả lời câu
        /// "đâm vào góc thì tính là đụng tường hay đụng sàn" — câu không có đáp án đúng.
        /// Chỉ kiểm điểm cuối: 5 unit/giây là 0.25 unit mỗi tick, không cách nào vượt qua một ô.
        /// </summary>
        private static MoveState ResolveHorizontal(MapGrid map, MoveState state, float dt)
        {
            state.X += state.VelX * dt;

            if (state.VelX == 0f)
                return state;

            float height = BodyHeight(state.Crouching);
            float footY = state.Y + EDGE;
            float midY = state.Y + height * 0.5f;
            float headY = state.Y + height - EDGE;

            if (state.VelX > 0f)
            {
                float edgeX = state.X + BODY_HALF_WIDTH;

                if (IsSolid(map, edgeX, footY) || IsSolid(map, edgeX, midY) || IsSolid(map, edgeX, headY))
                {
                    state.X = map.ColumnLeft(map.CellX(edgeX)) - BODY_HALF_WIDTH;
                    state.VelX = 0f;
                }
            }
            else
            {
                float edgeX = state.X - BODY_HALF_WIDTH;

                if (IsSolid(map, edgeX, footY) || IsSolid(map, edgeX, midY) || IsSolid(map, edgeX, headY))
                {
                    state.X = map.ColumnRight(map.CellX(edgeX)) + BODY_HALF_WIDTH;
                    state.VelX = 0f;
                }
            }

            return state;
        }

        /// <summary>
        /// Dịch theo trục Y rồi dán lại nếu chạm trần hoặc chạm sàn.
        ///
        /// Chiều xuống là chiều DUY NHẤT phải quét cả quãng đường: rơi kịch trần là 20 unit/giây,
        /// tức đúng 1.00 unit mỗi tick — vừa đủ để lọt qua một tấm bệ dày 1 ô giữa hai lần kiểm.
        /// Chiều lên (0.55 unit/tick) và chiều ngang (0.25) thì kiểm điểm cuối là đủ.
        /// </summary>
        private static MoveState ResolveVertical(MapGrid map, MoveState state, float dt)
        {
            float prevFeetY = state.Y;
            state.Y += state.VelY * dt;

            float height = BodyHeight(state.Crouching);

            if (state.VelY > 0f)
            {
                float headY = state.Y + height;

                // Bệ một chiều KHÔNG chặn chiều lên — đó là toàn bộ ý nghĩa của nó.
                if (IsSolid(map, state.X - BODY_HALF_WIDTH, headY) ||
                    IsSolid(map, state.X + BODY_HALF_WIDTH, headY))
                {
                    state.Y = map.RowBottom(map.CellY(headY)) - height;
                    state.VelY = 0f;
                }

                state.Grounded = false;
                return state;
            }

            // Quét từ hàng ô dưới chân lúc đầu tick xuống tới hàng ô dưới chân lúc cuối tick.
            // Quét ở mức "dưới chân một chút" chứ không đúng bằng chân: đứng yên trên mặt sàn thì
            // chân nằm ĐÚNG đường biên hai ô, và ô chứa nó là ô TRỐNG phía trên — kiểm ở đó thì
            // Grounded nhấp nháy true/false mỗi tick.
            int fromRow = map.CellY(prevFeetY - EDGE);
            int toRow = map.CellY(state.Y - EDGE);

            for (int row = fromRow; row >= toRow; row--)
            {
                if (!BlocksFall(map, state, row, prevFeetY))
                    continue;

                state.Y = map.RowTop(row);
                state.VelY = 0f;
                state.Grounded = true;
                state.TicksSinceGrounded = 0;
                return state;
            }

            state.Grounded = false;
            return state;
        }

        /// <summary>
        /// Hàng ô <paramref name="row"/> có chặn cú rơi này không.
        /// Ô đặc thì luôn chặn. Bệ một chiều chỉ chặn khi ĐỦ CẢ HAI: chân đã ở trên mặt bệ từ đầu
        /// tick (thiếu điều kiện này thì đi ngang vào cạnh bệ là bị bắn lên mặt bệ), và người chơi
        /// không đang chủ động tụt xuống.
        /// </summary>
        private static bool BlocksFall(MapGrid map, in MoveState state, int row, float prevFeetY)
        {
            int leftCell = map.CellX(state.X - BODY_HALF_WIDTH);
            int rightCell = map.CellX(state.X + BODY_HALF_WIDTH);

            for (int cx = leftCell; cx <= rightCell; cx++)
            {
                CellType cell = map.At(cx, row);

                if (cell == CellType.Solid)
                    return true;

                if (cell == CellType.OneWay &&
                    state.DropThroughTicks <= 0 &&
                    prevFeetY >= map.RowTop(row))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Đẩy một điểm spawn lên chỗ đứng được gần nhất. Cần vì vị trí người chơi đã LƯU trong DB
        /// còn hình dạng map thì sửa được: mỗi lần ai đó xây thêm một bức tường là một lần có người
        /// đang offline ở đúng chỗ ấy.
        /// </summary>
        public static float ResolveSpawnY(MapGrid map, float x, float y)
        {
            for (int guard = 0; guard < map.Height; guard++)
            {
                if (!OverlapsSolid(map, x, y, BODY_HEIGHT))
                    return y;

                // Nhảy lên mặt trên của hàng ô đang kẹt, rồi thử lại — tường dày mấy hàng thì lặp.
                y = map.RowTop(map.CellY(y));
            }

            return y;
        }
```

và `Step` với chữ ký mới:

```csharp
        public static MoveState Step(MoveState state, MoveIntent intent, float dt, MapGrid map)
        {
            // 0. Nhịp của tầng action (như trước), thêm bộ đếm rơi xuyên.
            if (state.ActionTicksLeft > 0)
                state.ActionTicksLeft--;

            if (state.TicksSinceAttack < EXPIRED)
                state.TicksSinceAttack++;

            if (state.DropThroughTicks > 0)
                state.DropThroughTicks--;

            if (state.ActionTicksLeft <= 0 && state.Action != ActionState.Die)
                state.Action = ActionState.None;

            // 1. Tư thế. Muốn ngồi thì ngồi được ngay; muốn ĐỨNG DẬY thì còn phải hỏi thế giới —
            //    trần thấp thì không đứng lên được, và giữ nguyên tư thế ngồi là câu trả lời đúng.
            //    Bỏ qua phép hỏi này thì thân nở ra bên trong trần và tick sau bị đẩy đi đâu không biết.
            bool wantCrouch = intent.Crouch
                              && state.Grounded
                              && !CharacterStates.BlocksMovement(state.Action);

            if (wantCrouch)
            {
                state.Crouching = true;
            }
            else if (state.Crouching)
            {
                state.Crouching = !CanStandUp(map, state);
            }

            // 2. Vận tốc ngang + hướng mặt (như trước).
            if (CharacterStates.BlocksMovement(state.Action) || state.Crouching)
            {
                state.VelX = 0f;
            }
            else
            {
                state.VelX = intent.DirX * MOVE_SPEED;
            }

            if (state.VelX != 0f && state.Action == ActionState.None)
                state.FacingLeft = state.VelX < 0f;

            // 3. Trọng lực (như trước).
            state.VelY -= GRAVITY * dt;
            if (state.VelY < -MAX_FALL_SPEED)
                state.VelY = -MAX_FALL_SPEED;

            // 4a. Hai bộ đếm tha thứ (như trước).
            if (state.TicksSinceGrounded < EXPIRED)
                state.TicksSinceGrounded++;

            if (intent.Jump)
                state.TicksSinceJumpRequest = 0;
            else if (state.TicksSinceJumpRequest < EXPIRED)
                state.TicksSinceJumpRequest++;

            // 4b. Rơi xuyên bệ — xử lý TRƯỚC cú nhảy và tiêu thụ luôn yêu cầu nhảy, nếu không thì
            //     người chơi vừa tụt xuống vừa bật lên trong cùng một tick.
            if (intent.Crouch && intent.Jump && state.Grounded && StandingOnOneWay(map, state))
            {
                state.DropThroughTicks = DROP_THROUGH_TICKS;
                state.TicksSinceJumpRequest = EXPIRED;
                state.Grounded = false;
            }
            // 4c. Nhảy (như trước).
            else if (!CharacterStates.BlocksMovement(state.Action) &&
                     state.TicksSinceJumpRequest <= JUMP_BUFFER_TICKS &&
                     state.TicksSinceGrounded <= COYOTE_TICKS)
            {
                state.VelY = JUMP_SPEED;
                state.TicksSinceJumpRequest = EXPIRED;
                state.TicksSinceGrounded = EXPIRED;
            }

            // 5. Xin hành động (như trước).
            if (intent.Action == ActionRequest.Attack &&
                state.TicksSinceAttack >= ATTACK_COOLDOWN_TICKS &&
                CharacterStates.CanEnter(state.Action, state.ActionTicksLeft, ActionState.Attack))
            {
                state.Action = ActionState.Attack;
                state.ActionTicksLeft = ATTACK_TICKS;
                state.TicksSinceAttack = 0;
            }

            // 6 & 7. Tích phân có va chạm. Thứ tự X trước Y là một phần của contract.
            state = ResolveHorizontal(map, state, dt);
            state = ResolveVertical(map, state, dt);

            return state;
        }
```

</details>

### ✅ CHECKPOINT A — tường là thật, và không ai xuyên được

Chưa cần vẽ gì — thử bằng client cũ trên nền cũ cũng thấy được, vì `MapGrid` đã có hình dạng thật.

1. Vào world: nhân vật rơi xuống và **dừng ở `y = 1`** (mặt trên hàng ô dưới cùng), đứng yên mãi.
   Nếu nó rơi xuyên xuống âm vô hạn → `BlocksFall` chưa bao giờ trả `true`, kiểm lại phép lật trục Y
   trong `Parse`.
2. Chạy sang trái tới hết map: dừng **sát mép**, không rung, không rubber-band. Không rubber-band là
   bằng chứng client và server đang chạy đúng cùng một `Step` — nếu giật thì một trong hai bên (rất
   hay là **vòng replay**) còn gọi bản `Step` cũ không truyền map.
3. Nhảy lên đụng trần: dừng, rơi xuống, **không dính vào trần**.
4. Nhảy từ dưới lên xuyên qua một bệ `=`: lọt qua, rồi **đứng được ở trên**.
5. Đứng trên bệ `=`, bấm ngồi + nhảy: **tụt xuống** bệ dưới.
6. Đi vào hành lang cao 1 ô: đứng thì không vào được, ngồi thì chui vào được. Đang ở trong đó mà thả
   nút ngồi → **vẫn ngồi**. Ra khỏi hành lang mới đứng dậy được.
7. `grounded` không nhấp nháy lúc đứng yên — thêm log tạm mà xem. Nhấp nháy nghĩa là đang quét ở đúng
   cao độ chân thay vì thấp hơn một chút.

Bước (5) và (6) là hai thứ mà một điểm không có thân thể **không làm được**. Đó là lý do phase này phải
cho nhân vật một cái hộp.

---

## Bước 2 — Client: hình và luật là hai lớp, và cách không cho chúng trôi xa nhau

### Hướng làm

Bản top-down giải quyết chuyện này bằng một câu gọn: *client vẽ map từ chính `MapGrid`, không paint
tay — paint tay là chép tay contract.* Đúng, và với ô vuông hai màu thì làm được thật.

Platformer thì câu đó **không dùng được**, và phải nói thẳng vì sao: tileset American Forest có cỏ mọc
ở mép trên khối đất, có hàng rào, có cây, có nền trời nhiều lớp. Không có hàm nào sinh ra hình đó từ
một lưới `Empty/Solid/OneWay` — thông tin ở lưới ít hơn hẳn thông tin ở hình.

Vậy là ta buộc phải có **hai lớp dữ liệu**, và phải chấp nhận điều đó thay vì giả vờ là không:

| | **Lớp HÌNH** | **Lớp LUẬT** |
|---|---|---|
| Là gì | Tilemap paint tay trong Unity, tileset American Forest | `MapGrid` trong `Shared` |
| Ai đọc | chỉ client, chỉ để vẽ | server (sự thật) **và** client (dự đoán) |
| Sai thì sao | nhìn hơi kỳ | **đi xuyên tường / tường vô hình** |
| Sửa bằng | cọ vẽ | ký tự trong `Maps.cs` |

Câu hỏi đúng không phải "làm sao để chỉ có một lớp" mà là: **lệch nhau thì bị phát hiện lúc nào?**

Có ba mức trả lời, và nên hiểu rõ mình đang chọn mức nào:

| Cách | Lệch client–server | Lệch hình–luật |
|---|---|---|
| Mỗi bên tự giữ map riêng | **im lặng, chết người** | im lặng |
| **`MapGrid` ở `Shared`, tilemap paint tay** ← chọn cái này | **không thể xảy ra** | im lặng, nhưng *nhìn thấy được* |
| Sinh `MapGrid` từ chính tilemap | không thể xảy ra | không thể xảy ra |

Mức 2 đã dọn sạch loại nguy hiểm: hai đầu dây đọc **cùng một DLL**, nên chuyện "server bảo có tường,
client bảo không" **không diễn đạt được**. Cái còn lại — hình vẽ đẹp mà luật nói khác — chỉ gây khó
chịu, và quan trọng hơn là nó **hiện ra trên màn hình**.

Việc của bước này là làm cho nó hiện ra **rõ**: một overlay vẽ đè lưới luật lên hình.

**File mới `Assets/Game/Scripts/World/MapCollisionGizmo.cs`** — `OnDrawGizmos` duyệt `Maps.Map1` và vẽ
mỗi ô một khung màu:

| Loại ô | Màu |
|---|---|
| `Solid` | đỏ, khung kín |
| `OneWay` | vàng, chỉ vẽ **cạnh trên** (đúng phần thật sự chặn) |
| `Empty` | không vẽ gì |

Vẽ `OneWay` bằng đúng một đoạn thẳng ở cạnh trên không phải để đẹp — nó là **hình ảnh chính xác của
luật**: chỉ mặt trên của bệ mới chặn, ba cạnh kia trong suốt. Gizmo mà vẽ cả khung thì nó đang nói dối
về chính thứ nó có nhiệm vụ kiểm tra.

Đặt gizmo lên `Map.prefab` cạnh tilemap. Bật Gizmos trong Scene view là thấy ngay: khối cỏ nào không có
khung đỏ = đi xuyên được; khung đỏ nào lơ lửng giữa trời = tường vô hình.

**Quy trình vẽ map từ nay** (dán vào đầu `Maps.cs` cho khỏi quên):

```
1. Sửa chuỗi trong Maps.cs          ← luật
2. dotnet build Server/Shared        ← DLL sang Assets/Plugins/Shared/
3. Mở Scene, nhìn gizmo              ← luật hiện ra trên nền hình
4. Paint tilemap cho khớp gizmo      ← hình đuổi theo luật, không phải ngược lại
```

Thứ tự **luật trước, hình sau** là có chủ đích. Vẽ hình trước rồi cố mô tả lại nó bằng ký tự là dịch
xuôi từ thứ giàu thông tin sang thứ nghèo thông tin — luôn mất mát, và mất ở đâu thì không ai biết.

**Nếu muốn đi tới mức 3** (sinh `MapGrid` từ tilemap): thêm một lớp `Tilemap` tên `Collision` chỉ dùng
hai tile đánh dấu (một cho `Solid`, một cho `OneWay`), tắt renderer của nó, rồi viết một Editor script
đọc lớp đó và **sinh ra** `Server/Shared/World/Maps.Generated.cs`. Sau đó build `Shared` như thường.

Đáng làm khi nào? Khi bạn có map thứ hai. Với một map thì gõ 16 dòng chữ nhanh hơn viết công cụ; với
năm map và một người khác cùng vẽ thì ngược lại. Ghi vào `CANDIDATE-PACKAGES.md` và làm khi thấy phiền.

Điều đáng nhớ ở đây không phải là chọn mức nào, mà là: **luôn biết mình đang ở mức nào, và loại lệch
nào còn sót lại thì được phát hiện bằng cách gì.** "Cẩn thận" không phải là một cơ chế.

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`Assets/Game/Scripts/World/MapCollisionGizmo.cs`** (file mới):

```csharp
using MMORPG.Shared.World;
using UnityEngine;

namespace MMORPG.Client.World
{
    /// <summary>
    /// Vẽ đè lưới va chạm (MapGrid trong Shared) lên tilemap vẽ tay, để mắt thường thấy ngay chỗ
    /// hình và luật lệch nhau.
    ///
    /// Hai lớp này KHÔNG sinh ra từ nhau: tileset có cỏ, hàng rào, cây — không lưới ba trạng thái
    /// nào mô tả nổi. Chấp nhận hai lớp thì phải đổi lại bằng một cách phát hiện lệch, và đây là nó.
    /// Loại lệch nguy hiểm hơn (client với server) thì không thể xảy ra: cả hai đọc chung một DLL.
    /// </summary>
    public sealed class MapCollisionGizmo : MonoBehaviour
    {
        [SerializeField] private bool _draw = true;

        private void OnDrawGizmos()
        {
            if (!_draw)
                return;

            MapGrid map = Maps.Map1;

            for (int cx = 0; cx < map.Width; cx++)
            {
                for (int cy = 0; cy < map.Height; cy++)
                {
                    CellType cell = map.At(cx, cy);

                    if (cell == CellType.Empty)
                        continue;

                    float left = map.ColumnLeft(cx);
                    float right = map.ColumnRight(cx);
                    float bottom = map.RowBottom(cy);
                    float top = map.RowTop(cy);

                    if (cell == CellType.Solid)
                    {
                        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);
                        Gizmos.DrawWireCube(
                            new Vector3((left + right) * 0.5f, (bottom + top) * 0.5f, 0f),
                            new Vector3(right - left, top - bottom, 0f));
                    }
                    else
                    {
                        // Chỉ vẽ CẠNH TRÊN: đó là đúng phần chặn được của một bệ một chiều.
                        // Vẽ cả khung là gizmo nói dối về chính thứ nó có nhiệm vụ kiểm tra.
                        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
                        Gizmos.DrawLine(new Vector3(left, top, 0f), new Vector3(right, top, 0f));
                    }
                }
            }
        }
    }
}
```

Trong Editor: gắn `MapCollisionGizmo` lên `Map.prefab` (cạnh Grid/Tilemap), bật Gizmos trong Scene
view. Đặt `Grid` tại gốc toạ độ với cell size 1 để ô tilemap `(cx, cy)` trùng ô `MapGrid` `(cx, cy)`.

</details>

### ✅ CHECKPOINT B — hình khớp luật

1. Bật Gizmos: mọi khối đất/đá có khung đỏ, mọi bệ gỗ có vạch vàng ở mép trên, trời trống thì không có gì.
2. Đi hết map một vòng: không chỗ nào "đụng tường vô hình", không chỗ nào đi xuyên qua thứ nhìn thấy được.
3. Cố tình sửa một ô `#` thành `.` trong `Maps.cs`, build `Shared`, chạy lại: khung đỏ biến mất ở đúng ô
   đó **trong khi hình vẫn còn khối đất** — và bạn đi xuyên qua được. Đó là hình ảnh của "hình và luật
   lệch nhau", và nó mất **hai giây** để nhìn ra thay vì một buổi tối để lần. Trả lại như cũ.

---

## Bước 3 — Server: AOI — tầm nhìn quyết định mọi thứ

### Hướng làm

Tư tưởng quan trọng nhất của phase, và nó không dính gì tới platformer hay top-down:

> **`EntitySpawn`/`EntityDespawn` không còn là "sự kiện vào/ra world" nữa — chúng là hệ quả của việc
> ai đó VÀO/RA TẦM NHÌN của bạn.**

Người mới vào world chỉ là *một cách* để lọt vào tầm nhìn; đi bộ lại gần là cách khác. Một cơ chế phục
vụ cả hai — và **client không phải sửa một dòng nào**. Đó là phần thưởng cụ thể của việc Phase 7 viết
client theo **message** ("có gói bảo X xuất hiện thì dựng X") chứ không theo **nguyên nhân**.

**Chia không gian theo cột, không theo lưới 2D.** Bản top-down chia ô 8×8 và tra 9 ô quanh mình. Ở
platformer ngang thì làm vậy là trả tiền cho một chiều không dùng:

| | Bề ngang map | Bề cao map | Màn hình thấy |
|---|---|---|---|
| Kích thước | 48 unit (và sẽ còn dài ra) | 16 unit | ~17.8 × 10 unit |

Map cao 16 unit, mà một màn hình đã cao 10 — chia trục Y thành ô 12 unit thì gần như **mọi người luôn
ở cùng một hàng ô**, và ta trả thêm một chiều trong khoá `Dictionary` để nhận về một phép lọc gần như
không lọc gì.

> Chỉ chia ô ở **trục mà thế giới thật sự lớn**. Với side-scroller, đó là trục X — và chỉ trục X.

Nên: **cột** rộng `AOI_COLUMN_WIDTH = 12f`, tầm nhìn = 3 cột quanh mình (`cx-1`, `cx`, `cx+1`). Bán kính
bảo đảm ở trường hợp xấu nhất (đứng sát mép cột) là đúng **12 unit** mỗi bên, so với nửa màn hình ~9 —
dư một chút, đúng như cần.

Cùng lập luận đánh đổi của spatial grid: tầm nhìn không phải hình tròn bán kính r mà là một dải chữ
nhật lệch tuỳ chỗ đứng trong cột. Không sao, vì tầm nhìn chỉ cần **một** tính chất: bán kính bảo đảm ≥
những gì màn hình thấy. Dư ra thì không ai nhận biết.

**Lọc theo `MapId` trước.** Hiện chỉ có một map nên nó chưa lọc gì, nhưng nó là **ranh giới cứng**:
hai người ở hai map khác nhau thì không bao giờ thấy nhau, dù `X` của họ bằng nhau. Viết nó ngay bây
giờ rẻ hơn nhiều so với đi tìm lý do vì sao người ở hang động nhìn thấy người ở đồng cỏ.

**Sửa `WorldService`:**

1. **Xoá broadcast trong `Spawn`/`Despawn`** (phần thêm ở Phase 7 — bỏ cả vòng "gửi danh sách người có
   mặt cho người mới"). Từ giờ mọi thông báo xuất hiện/biến mất đều do vòng tick phát ra.
2. **`PlayerEntity` thêm `HashSet<int> Visible`** — tập entityId đang trong tầm nhìn của người này. Chỉ
   luồng tick đọc/ghi, ghi comment ranh giới luồng như đã làm với input.
3. **`Tick` thêm pha tầm nhìn**, sau pha tích phân, trước pha gửi:
   - dựng chỉ mục cột: `Dictionary<(int MapId, int Column), List<PlayerEntity>>`, **dựng lại từ đầu mỗi
     tick** — O(n), không có trạng thái nào sống qua tick nên không có lớp bug "chỉ mục lệch thực tế";
   - với từng người: gom mọi entity trong 3 cột quanh mình (trừ chính mình) → `visibleNow`;
   - so với `Visible`: mới → gửi `EntitySpawn`; mất → gửi `EntityDespawn`;
   - snapshot chỉ chứa `visibleNow`.

Thứ tự ba thao tác (báo người mới → báo người đi → cập nhật tập) quan trọng: đảo lại thì tập đã bị ghi
đè trước khi kịp so.

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`PlayerEntity.cs`** — thêm:

```csharp
        /// <summary>
        /// Tập entityId đang trong tầm nhìn của người này — bộ nhớ để tick sau so ra ai vừa xuất
        /// hiện, ai vừa rời đi. Chỉ luồng tick đọc/ghi, vì vậy không cần lock.
        /// </summary>
        public HashSet<int> Visible { get; } = new();
```

**`WorldService.cs`** — xoá hai đoạn broadcast của Phase 7 trong `Spawn`/`Despawn` (giữ phần ghi sổ +
log), rồi thay `Tick`:

```csharp
        /// <summary>
        /// Bề ngang một cột tầm nhìn. Tầm nhìn = 3 cột → bán kính bảo đảm 12 unit mỗi bên, rộng hơn
        /// nửa màn hình (~9 unit) một chút.
        ///
        /// Chỉ chia theo trục X: map cao 16 unit mà một màn hình đã cao 10, nên chia trục Y sẽ tốn
        /// thêm một chiều trong khoá để nhận về một phép lọc gần như không lọc gì.
        /// </summary>
        private const float AOI_COLUMN_WIDTH = 12f;

        public void Tick(float dt)
        {
            // Pha 1: tích phân tất cả.
            foreach (PlayerEntity entity in _entities.Values)
                entity.Integrate(dt);

            // Pha 2: dựng chỉ mục cột — làm lại từ đầu mỗi tick. O(n) và không có trạng thái nào
            // sống qua tick, nên không tồn tại lớp bug "chỉ mục lệch thực tế" (quên gỡ cột cũ,
            // entity chết còn nằm trong cột...). Bản cập-nhật-tại-chỗ nhanh hơn nhưng phải đúng ở
            // mọi đường vào/ra — chưa đáng đổi.
            var byColumn = new Dictionary<(int MapId, int Column), List<PlayerEntity>>();

            foreach (PlayerEntity entity in _entities.Values)
            {
                (int, int) key = ColumnOf(entity);

                if (!byColumn.TryGetValue(key, out List<PlayerEntity> list))
                {
                    list = new List<PlayerEntity>();
                    byColumn[key] = list;
                }

                list.Add(entity);
            }

            // Pha 3: với từng người — tầm nhìn mới, so với tầm nhìn cũ, phát spawn/despawn, gửi trạng thái.
            foreach (PlayerEntity viewer in _entities.Values)
            {
                if (viewer.Owner == null)
                    continue;

                List<PlayerEntity> visibleNow = CollectVisible(viewer, byColumn);

                // Ai mới lọt vào tầm nhìn → giới thiệu họ với viewer.
                foreach (PlayerEntity seen in visibleNow)
                {
                    if (!viewer.Visible.Contains(seen.EntityId))
                        viewer.Owner.SendData(NetCmd.EntitySpawn, ToSpawnNotice(seen));
                }

                // Ai vừa rời tầm nhìn → báo biến mất. Phải làm TRƯỚC khi ghi đè tập Visible.
                viewer.Visible.RemoveWhere(id =>
                {
                    bool stillVisible = visibleNow.Exists(e => e.EntityId == id);

                    if (!stillVisible)
                        viewer.Owner.SendData(NetCmd.EntityDespawn, new EntityDespawnNotice { EntityId = id });

                    return !stillVisible;
                });

                foreach (PlayerEntity seen in visibleNow)
                    viewer.Visible.Add(seen.EntityId);

                // Pha 4: MoveState cho chính mình, snapshot CHỈ những ai trong tầm.
                viewer.Owner.SendData(NetCmd.MoveState, new MoveStateResponse
                {
                    LastInputSeq = viewer.LastInputSeq,
                    State = viewer.State,
                });

                var states = new EntityState[visibleNow.Count];
                for (int i = 0; i < visibleNow.Count; i++)
                {
                    PlayerEntity seen = visibleNow[i];

                    states[i] = new EntityState
                    {
                        EntityId = seen.EntityId,
                        X = seen.State.X,
                        Y = seen.State.Y,
                        Flags = EntityFlags.Pack(seen.State),
                        Action = (byte)seen.State.Action,
                    };
                }

                viewer.Owner.SendData(NetCmd.WorldSnapshot, new WorldSnapshotNotice { States = states });
            }
        }

        private static (int MapId, int Column) ColumnOf(PlayerEntity entity)
        {
            // Floor chứ không phải cast: toạ độ âm (bị đẩy ra ngoài mép trái map) phải rơi về cột
            // bên trái, không gom hết về cột 0.
            return (entity.MapId, (int)MathF.Floor(entity.State.X / AOI_COLUMN_WIDTH));
        }

        /// <summary>
        /// Mọi entity trong 3 cột quanh viewer, cùng map, trừ chính viewer.
        /// Lọc MapId là ranh giới CỨNG: hai người ở hai map khác nhau không bao giờ thấy nhau dù
        /// toạ độ X của họ bằng nhau. Hiện chỉ có một map nên nó chưa lọc gì — nhưng viết bây giờ
        /// rẻ hơn nhiều so với đi tìm lý do người ở hang động nhìn thấy người ở đồng cỏ.
        /// </summary>
        private static List<PlayerEntity> CollectVisible(
            PlayerEntity viewer, Dictionary<(int MapId, int Column), List<PlayerEntity>> byColumn)
        {
            var result = new List<PlayerEntity>();
            (int mapId, int column) = ColumnOf(viewer);

            for (int dx = -1; dx <= 1; dx++)
            {
                if (!byColumn.TryGetValue((mapId, column + dx), out List<PlayerEntity> cell))
                    continue;

                foreach (PlayerEntity entity in cell)
                {
                    if (entity.EntityId != viewer.EntityId)
                        result.Add(entity);
                }
            }

            return result;
        }
```

(`Broadcast` helper của Phase 7 không còn ai gọi — xoá cho sạch, cần thì git history còn.)

**`WorldService.Spawn`** — dùng `ResolveSpawnY` khi dựng entity:

```csharp
            // Vị trí lưu trong DB có từ thời map chưa có hình dạng — và map thì sửa được bất cứ lúc
            // nào. Đẩy lên chỗ đứng được gần nhất, và LA LỚN nếu phải đẩy: đó là dấu hiệu ai đó vừa
            // xây tường lên đầu một người đang offline.
            float spawnY = MovementRules.ResolveSpawnY(Maps.Map1, row.X, row.Y);

            if (spawnY != row.Y)
                Log.Warn($"{row.Name} spawn kẹt trong tường tại ({row.X:0.##},{row.Y:0.##}) — đẩy lên {spawnY:0.##}");
```

</details>

### ✅ CHECKPOINT C — mục tiêu cuối Phase 10

1. Hai client vào world cạnh nhau → thấy nhau (như Phase 9, giờ qua đường tầm nhìn).
2. Một người chạy xa: tới khoảng 12–24 unit thì người kia **biến mất** khỏi màn hình; console hiện
   `EntityDespawn`.
3. Chạy ngược lại → hiện ra lại đúng vị trí, đúng hướng mặt, đi tiếp mượt (buffer nội suy mồi lại từ
   `EntitySpawn`, và `Flags` trong gói đó lo phần hướng mặt).
4. Đứng gần nhau, một người thoát hẳn → người kia vẫn thấy despawn (đường cũ nay do diff đảm nhiệm:
   entity rời sổ → rời `visibleNow` → despawn).
5. Log tạm kích thước snapshot: đứng cạnh nhau = 1 state, đi xa = **0** state — băng thông tỉ lệ với
   **mật độ quanh mình**, không phải tổng người online. Đó là câu trả lời cho "vì sao MMO gánh được
   nghìn người".

---

## Ba thử nghiệm bắt buộc

**1. Hack xuyên tường, kiểu thông minh.**
Sửa tạm `PlayerMotor` để **vòng replay** trong `OnMoveStateResult` truyền một `MapGrid` rỗng (parse một
map toàn dấu chấm) trong khi bước dự đoán vẫn dùng map thật. Chạy vào tường.

Bạn sẽ thấy một thứ tinh vi hơn "bị kéo lại": nhân vật **rung** ở sát tường — dự đoán chặn nó, replay
cho nó qua, mỗi gói `MoveState` là một lần đổi ý. Đây là hình ảnh của **hai bản luật lệch nhau bên
trong cùng một client**, và nó dạy vì sao "chỉ có một `Step`" phải hiểu là *một* — kể cả hai chỗ gọi
trong cùng một file. Trả code về như cũ.

**2. Nhảy múa ở ranh giới AOI.**
Hai người đứng hai bên ranh giới cột, một người bước qua-lại quanh ranh giới → người kia thấy bạn mình
**nhấp nháy** hiện/biến, mỗi lần là một cặp gói spawn/despawn và một lần dựng/huỷ GameObject.

Đây là flicker kinh điển của AOI không có hysteresis (vào và ra dùng **cùng một ngưỡng**). Không sửa ở
phase này — nhưng phải **thấy nó bằng mắt** và trả lời được câu 8 bên dưới.

**3. Đo cái AOI mua được.**
Log tạm tổng số `EntityState` server gửi mỗi giây. Hai client đứng cạnh nhau: ~40/giây (20 tick × 2
người × 1 state). Đi xa nhau: **0**. Với broadcast của Phase 7 con số này không bao giờ về 0 dù map to
cỡ nào — và nó tăng theo **bình phương** tổng người online thay vì theo mật độ cục bộ.

---

## Troubleshooting

| Triệu chứng | Nguyên nhân thường gặp | Chỗ sửa |
|---|---|---|
| Rơi xuyên sàn xuống âm vô hạn | `Parse` chưa lật trục Y, hoặc `BlocksFall` không bao giờ trả `true` | `MapGrid.Parse` · `MovementRules.BlocksFall` |
| `Grounded` nhấp nháy true/false lúc đứng yên | Quét va chạm ở đúng cao độ chân thay vì thấp hơn `EDGE` | `ResolveVertical` |
| Rơi từ trên cao thì lọt qua bệ mỏng | Chiều rơi kiểm điểm cuối thay vì quét cả quãng | `ResolveVertical` — vòng `for` theo hàng |
| Nhảy từ dưới lên bị cộc đầu vào bệ `=` | Nhánh `VelY > 0` đang chặn cả `OneWay` | `ResolveVertical` |
| Đi ngang vào cạnh bệ thì bị bắn lên mặt bệ | Thiếu điều kiện `prevFeetY >= RowTop(row)` | `BlocksFall` |
| Ngồi + nhảy thì vừa tụt xuống vừa bật lên | Nhánh rơi xuyên không tiêu thụ `TicksSinceJumpRequest`, hoặc đặt sau phép nhảy | `Step` phép 4b |
| Tụt xuống rồi lập tức đứng lại trên chính bệ đó | `DROP_THROUGH_TICKS` quá nhỏ so với thời gian rơi hết bề dày bệ | `MovementRules` |
| Kẹt cứng trong trần sau khi đứng dậy | Thiếu phép hỏi `CanStandUp` khi thả nút ngồi | `Step` phép 1 |
| Nhân vật lọt qua khe hẹp hơn thân | Quét ngang thiếu mức giữa (chỉ 2 điểm thay vì 3) | `ResolveHorizontal` |
| Đụng "tường vô hình" cạnh tường thật | Hình và luật lệch — không phải bug code | Bật gizmo, sửa `Maps.cs` hoặc paint lại |
| Rung ở sát tường | Client và server không cùng một `Step`; hay gặp nhất là **vòng replay** quên truyền map | `PlayerMotor.OnMoveStateResult` |
| `Parse` ném lỗi lúc khởi động | Các hàng map lệch độ dài | Đó là hàng rào đang làm việc — đếm lại ký tự |
| Spawn kẹt trong tường | Vị trí cũ trong DB nằm trong lòng đất | `ResolveSpawnY` — và đọc dòng `Log.Warn` nó in ra |
| Người kia không bao giờ biến mất dù chạy rất xa | Còn broadcast của Phase 7 trong `Spawn`, hoặc snapshot vẫn dựng từ toàn bộ `_entities` | `WorldService` |
| Người kia biến mất rồi không hiện lại | Sai thứ tự ba thao tác: phải là báo-mới → báo-đi → cập-nhật-tập | `WorldService.Tick` pha 3 |
| Người kia hiện ra quay sai hướng | `ToSpawnNotice` chưa điền `Flags` | `WorldService` |
| Nhấp nháy hiện/biến ở một khoảng cách nhất định | Flicker ranh giới AOI — hành vi đã biết | Thử nghiệm 2; sửa thật thì cần hysteresis (câu 8) |
| Người ở map khác vẫn nhìn thấy nhau | Khoá chỉ mục quên `MapId` | `ColumnOf` |
| Nhân vật đứng im hoàn toàn, không lỗi gì | Unity còn dùng DLL cũ — build `Shared` chưa copy sang `Assets/Plugins/Shared/`. **Lần thứ ba dòng này xuất hiện** — Phase 11 giết hẳn nó bằng phép kiểm vân tay contract | Build lại `Server/Shared`, xem post-build target |

---

## Tự kiểm tra hiểu bài

Tự trả lời từng câu xong mới mở đáp án của câu đó.

**Câu 1.** Bản top-down nói "client vẽ map từ `MapGrid`, paint tay là chép tay contract". Vì sao câu đó
không dùng được ở platformer, và ta đổi nó bằng gì?
<details>
<summary><b>📖 Đáp án câu 1</b></summary>

Vì hình platformer **giàu thông tin hơn** lưới va chạm rất nhiều: cỏ ở mép trên khối đất, hàng rào,
cây, nền trời nhiều lớp — không hàm nào sinh ra được chúng từ ba trạng thái `Empty/Solid/OneWay`. Ép
sinh hình từ lưới nghĩa là chấp nhận map trông như ô vuông hai màu.

Nên ta chấp nhận **hai lớp**, nhưng chỉ sau khi đã dọn sạch loại lệch nguy hiểm: lưới va chạm nằm ở
`Shared` nên client và server đọc **cùng một DLL** — "server bảo có tường, client bảo không" là chuyện
không diễn đạt được. Loại lệch còn lại (hình đẹp mà luật nói khác) chỉ gây khó chịu và **nhìn thấy
được**, nên ta đầu tư vào việc làm nó dễ thấy: gizmo vẽ đè luật lên hình.

Ý chung: không phải lúc nào cũng gộp được về một nguồn. Khi không gộp được thì việc cần làm là **biết
loại lệch nào còn sót và nó bị phát hiện bằng cơ chế gì** — "cẩn thận" không phải một cơ chế.

</details>

**Câu 2.** Bệ xuyên-một-chiều có ba điều kiện chặn. Bỏ từng cái ra thì gặp bug gì?
<details>
<summary><b>📖 Đáp án câu 2</b></summary>

- Bỏ **"đang đi xuống"** (`VelY <= 0`): nhảy từ dưới lên bị cộc đầu vào bệ — mất đúng tính năng cốt lõi.
- Bỏ **"chân đã ở trên mặt bệ từ đầu tick"**: đi ngang vào cạnh bệ là bị **bắn lên** mặt bệ, vì trong
  tick đó chân vừa tụt xuống dưới mép trên và phép kiểm coi đó là hạ cánh.
- Bỏ **`DropThroughTicks == 0`**: mất tính năng chủ động tụt xuống — bấm ngồi+nhảy thì hạ xuống rồi
  bị chính bệ đó bắt lại ngay tick sau.

Điểm chung đáng nhớ: cả ba điều kiện đều **không** hỏi "nhân vật đang ở đâu" mà hỏi "nó đang làm gì và
vừa ở đâu". Va chạm ở đây là hàm của *trạng thái*, không phải của *vị trí* — đó là lý do vận tốc phải
nằm trong `MoveState` từ Phase 8.

</details>

**Câu 3.** Vì sao chỉ chiều rơi cần quét cả quãng đường, còn ba chiều kia kiểm điểm cuối là đủ?
<details>
<summary><b>📖 Đáp án câu 3</b></summary>

Vì tunneling chỉ xảy ra khi quãng đi trong một tick **vượt được cạnh một ô**. Nhìn số: ngang 5 unit/s
= 0.25 unit/tick; lên 11 unit/s = 0.55; xuống kịch trần 20 unit/s = **1.00** — đúng bằng cạnh ô, vừa
đủ để lọt qua một tấm bệ dày 1 ô giữa hai lần kiểm.

Cái hay là phép so sánh này cho biết **chính xác chỗ nào** cần quét thay vì quét hết cho chắc. Và nó
để lại một điều kiện phải nhớ: hôm nào có bệ nhún hay cú đẩy làm tốc độ ngang vượt 20 unit/s thì trục
ngang cũng phải quét. Con số là một phần của lập luận, không phải một hằng số vô danh.

</details>

**Câu 4.** Vì sao phép quét va chạm dọc lấy mốc "dưới chân một chút" (`Y - EDGE`) chứ không đúng bằng
`Y`? Mô tả bug cụ thể nếu lấy đúng `Y`.
<details>
<summary><b>📖 Đáp án câu 4</b></summary>

Vì đứng trên sàn thì chân nằm **đúng đường biên** giữa hai hàng ô, và `Floor` đưa đường biên về ô
**phía trên** — tức ô trống. Kiểm ở đó thì tick nào đứng yên cũng kết luận "không có gì dưới chân" →
`Grounded = false` → tick sau trọng lực kéo xuống 0.05 unit → lúc này ô dưới chân là ô đặc →
`Grounded = true` và dán về mặt sàn → tick sau lại `false`…

Kết quả là `Grounded` nhấp nháy 20 lần mỗi giây. Mà `Grounded` là điều kiện của nhảy, của ngồi, và của
`LocomotionState` — nên hoạt ảnh giật giữa `idle` và `fall`, và cú nhảy thì lúc ăn lúc không. Một
epsilon đặt đúng chỗ dọn sạch cả chuỗi hệ quả đó.

</details>

**Câu 5.** Va chạm tách trục X rồi Y. Ở top-down lý do là "trượt dọc tường khi đi chéo". Ở platformer
lý do là gì?
<details>
<summary><b>📖 Đáp án câu 5</b></summary>

Là **tránh một câu hỏi không có đáp án đúng**: xử lý hai chiều cùng lúc thì khi đâm vào góc phải quyết
định "coi là đụng tường (dừng ngang, tiếp tục rơi) hay đụng sàn (dừng rơi, tiếp tục chạy)" — hai lựa
chọn đều sai trong một nửa số tình huống.

Tách ra thì mỗi phép chỉ trả lời một câu một chiều, và kết quả tự nhiên là hành vi đúng: chạy vào tường
lúc đang rơi thì dừng ngang mà vẫn rơi tiếp; đáp xuống mép bệ thì dừng rơi mà vẫn chạy tiếp.

Bên lề: hệ quả "trượt dọc tường" của top-down vẫn còn — ở platformer nó biểu hiện thành *áp vào tường
mà vẫn rơi mượt*, thứ mà không tách trục thì thành *dính cứng vào tường giữa không trung*.

</details>

**Câu 6.** Vì sao AOI chia **cột** theo X mà không chia lưới ô 2D như bản top-down?
<details>
<summary><b>📖 Đáp án câu 6</b></summary>

Vì chỉ nên chia ô ở trục mà thế giới **thật sự lớn**. Map cao 16 unit trong khi một màn hình đã cao
10 — chia trục Y thành ô 12 unit thì gần như mọi người luôn nằm cùng một hàng, và ta trả thêm một
chiều trong khoá `Dictionary` để nhận về một phép lọc gần như không lọc gì.

Trục X thì ngược lại: map dài 48 unit và sẽ còn dài ra, còn màn hình chỉ thấy ~18 — chia ở đây lọc
được thật.

Tổng quát: cấu trúc chia không gian phải khớp **hình dạng của thế giới**, không phải khớp thói quen.
Cùng lý do đó, một game top-down map vuông thì lưới 2D là đúng, và một game bay không gian thì phải
là octree.

</details>

**Câu 7.** Server đổi hoàn toàn *nguyên nhân* sinh ra `EntitySpawn`/`EntityDespawn` mà client không
phải sửa gì. Thiết kế nào của Phase 7 mua được điều đó?
<details>
<summary><b>📖 Đáp án câu 7</b></summary>

Client Phase 7 được viết theo **message**, không theo **nguyên nhân**: nó chỉ biết "có gói bảo X xuất
hiện thì dựng X, có gói bảo X biến mất thì dọn X" — không hỏi vì sao. Server thay toàn bộ logic phát
sinh (từ sự kiện vào/ra world sang diff tầm nhìn mỗi tick) mà **contract không đổi**, nên client cũ
chạy nguyên.

Đây là phần thưởng cụ thể của việc tách "điều đã xảy ra" (message) khỏi "vì sao nó xảy ra" (logic
server). Nếu Phase 7 đã trót đặt tên gói là `PlayerJoinedWorld` / `PlayerLeftWorld` thì hôm nay cái
tên ấy sẽ nói dối — và không có lỗi biên dịch nào báo cho biết.

</details>

**Câu 8.** Flicker ở ranh giới AOI (thử nghiệm 2): nguyên nhân chính xác là gì, và hysteresis sửa nó
thế nào?
<details>
<summary><b>📖 Đáp án câu 8</b></summary>

Vào và ra tầm nhìn dùng **cùng một ngưỡng** (ranh giới cột), nên người đứng ngay ranh giới chỉ cần dao
động vài centimet là đổi trạng thái — mỗi lần đổi là một cặp gói spawn/despawn và một lần dựng/huỷ
GameObject.

Hysteresis tách hai ngưỡng: **vào** tầm nhìn ở phạm vi hẹp (3 cột), chỉ **ra** khi vượt phạm vi rộng
hơn (5 cột). Người ở giữa hai ngưỡng giữ nguyên trạng thái hiện có, nên dao động nhỏ quanh một điểm
không đổi được trạng thái nữa. Giá phải trả: tầm "ra" rộng hơn tầm "vào" một vành đai, tức là giữ đồng
bộ thêm vài người mà lẽ ra đã bỏ được.

</details>

**Câu 9.** `ResolveSpawnY` đẩy người chơi lên khi vị trí lưu trong DB nằm trong tường. Vì sao đây
không phải một đoạn code dọn dẹp dùng một lần rồi xoá?
<details>
<summary><b>📖 Đáp án câu 9</b></summary>

Vì nó không sửa một sự cố quá khứ mà xử lý một **mâu thuẫn thường trực**: hình dạng map là dữ liệu
**sửa được bất cứ lúc nào**, còn vị trí người chơi thì **đã lưu rồi**. Mỗi lần người thiết kế xây thêm
một bức tường là một lần có ai đó đang offline ở đúng chỗ ấy, và họ sẽ đăng nhập vào bên trong đá.

Đó cũng là lý do nó phải `Log.Warn` chứ không im lặng sửa: một người bị đẩy là chuyện thường, ba trăm
người bị đẩy nghĩa là ai đó vừa sửa map hỏng và cần biết ngay.

</details>

---

## Để dành (ghi lại, chưa làm)

- **Hysteresis cho AOI** (câu 8). Rẻ, và nên làm ngay khi nào flicker bắt đầu gây khó chịu thật.
- **Hố và chết do rơi.** Map hiện kín đáy. Có hố thì cần một mốc `y` mà rơi quá là chết — và "chết" thì
  đã có sẵn `ActionState.Die` từ Phase 9, chỉ thiếu người gọi.
- **Nhiều map và cửa chuyển map.** `MapId` đã nằm sẵn trong entity và trong khoá AOI; thiếu đúng
  `Dictionary<int, MapGrid>` và một lệnh chuyển.
- **Dốc (slope).** Đắt hơn vẻ ngoài nhiều: `Grounded` không còn là "ô dưới chân đặc" mà là một phép
  chiếu, và tốc độ chạy phải chiếu theo mặt dốc. Đụng vào cả `Derive` của Phase 9.
- **Bệ di động.** Khó nhất trong danh sách này: bệ là một entity **chuyển động** ảnh hưởng va chạm, nên
  nó phải nằm trong `Step` (tức trong `Shared`, tức phải đồng bộ vị trí bệ tới client trước khi client
  dự đoán được). Là bài học "thứ gì tham gia va chạm thì thứ đó thuộc về contract".
- **Thang, dây leo.** Một `CellType` nữa, và một trạng thái locomotion nữa — đúng chỗ để kiểm tra xem
  hai tầng của Phase 9 có chia đúng không.
- **Sinh `MapGrid` từ tilemap** (mức 3 ở Bước 2): thêm lớp `Tilemap` tên `Collision`, Editor script
  sinh `Maps.Generated.cs`. Đáng làm khi có map thứ hai.
- **Map ra file thay vì hằng số trong code.** Map là payload lớn đầu tiên (> 4KB) — dịp để đường nén
  LZ4 của Phase 2 chạy thật, kèm bài toán cache theo hash phía client.

---

**Xong Phase 10 → thế giới có hình dạng, có tầm nhìn, băng thông theo mật độ.**
[PHASE-11](PHASE-11.md) trả nốt món nợ rải khắp bốn phase vừa qua: `GRAVITY`, `JUMP_SPEED`,
`ATTACK_TICKS`, `AOI_COLUMN_WIDTH`, cả bản đồ — tất cả đang là hằng số nằm cứng trong code. Đưa chúng
ra dữ liệu, sửa không cần build lại, và phân biệt cho rõ hai loại config khác nhau về bản chất.
