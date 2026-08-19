# PHASE 9 — Map & AOI: thế giới có hình dạng và tầm nhìn

> **Kết quả cuối Phase 9:** map có tường thật — client sửa kiểu gì cũng không xuyên được, đi sát tường
> thì **trượt** dọc theo nó chứ không dính cứng. Và AOI (Area of Interest): chỉ nhận gói tin của những
> người ở gần; hai người đi xa nhau thì biến mất khỏi màn hình của nhau, lại gần thì hiện ra.
>
> **Điều kiện:** xong [`PHASE-8.md`](PHASE-8.md) tới CHECKPOINT B và cả 3 thử nghiệm.
>
> **Bài học chính:** (1) va chạm là **luật chơi** nên phải nằm ở server — và vì client cũng dự đoán,
> dữ liệu map phải là contract 1 nguồn y như `NetCmd`; (2) MMO không broadcast toàn map — spatial grid
> + so-sánh-tầm-nhìn-mỗi-tick biến `EntitySpawn`/`EntityDespawn` từ "sự kiện" thành "hệ quả của tầm nhìn".
>
> ⚠️ **Doc này viết khi dự án còn là top-down (lúc đó là Phase 8).** Sau khi chốt thể loại platformer
> ngang, hai chỗ cần viết lại trước khi làm: (1) phần va chạm — thêm **sàn xuyên-một-chiều** và bỏ
> nhánh trượt dọc tường theo trục Y; (2) phần AOI — tầm nhìn tính theo **zone + khoảng cách trục X**
> thay vì ô lưới 2D. Phần diff-tầm-nhìn-mỗi-tick và toàn bộ lập luận thì giữ nguyên giá trị.


Format như trước: **hướng làm** hiện sẵn, **📖 Lời giải** trong foldout.

---

## Hai việc, một nguyên tắc

Phase này làm hai việc nhìn ngoài chẳng liên quan — tường và tầm nhìn — nhưng chung một nguyên tắc:
**thế giới được chia thành lưới ô, và mọi câu hỏi không gian trả lời bằng toạ độ ô.**

- **Va chạm**: "ô này đứng được không?" — lưới ô 1×1 unit, mỗi ô là sàn hoặc tường.
- **AOI**: "ai đứng gần ai?" — lưới ô to hơn (8×8 unit), mỗi entity thuộc một ô; "gần" nghĩa là trong
  9 ô quanh mình. Không bao giờ phải tính khoảng cách từng cặp người chơi (O(n²)) — chỉ tra ô.

Map ở phase này định nghĩa **bằng code trong `Shared`** — một mảng chuỗi, `#` là tường, `.` là sàn.
Nghe thô, nhưng nó cho đúng thứ cần nhất: server (va chạm thật), client (va chạm dự đoán) và client
(vẽ hình) cùng đọc **một** định nghĩa — không tồn tại khả năng map hai bên lệch nhau. Phase 10 sẽ đưa
map ra file; hôm nay học cơ chế trước, học đường ống dữ liệu sau.

```
Shared: MapGrid ("#..#...")  ──┬──► GameServer: Step() né tường (sự thật)
                               ├──► Client: Step() né tường (dự đoán — CÙNG hàm, không rubber-band)
                               └──► Client: MapRenderer vẽ tilemap từ đúng lưới đó
```

---

## Bước 1 — Shared: MapGrid + va chạm trong `Step`

### Hướng làm

**File mới `Server/Shared/World/MapGrid.cs`**:

- `MapGrid.Parse(string[] rows)` → đối tượng giữ `bool[,]` walkable. Hàng **đầu tiên** của mảng chuỗi là
  mép **trên** của map (cho dễ "vẽ" trong code), nên khi parse phải lật trục Y — quyết định rồi ghi
  comment, không thì ba tháng sau chính bạn vẽ map mới sẽ ngửa mặt hỏi vì sao map lộn ngược.
- Map đặt **giữa** gốc toạ độ (spawn `(0,0)` của Phase 5 nằm giữa map): ô `(cx, cy)` chiếm vùng world
  `[cx*CELL - W/2, ...]`. Viết `IsWalkableWorld(float x, float y)`: đổi toạ độ world → ô, **ngoài rìa
  map = không đi được** (tường bao ngầm định — không cần WORLD_HALF_EXTENT nữa).
- **File mới `Server/Shared/World/Maps.cs`**: `public static readonly MapGrid Map1 = MapGrid.Parse(...)`
  — map ~40×16 ô, viền `#` kín, thêm vài cụm tường bên trong để có gì mà né. Map phải **to hơn tầm nhìn
  AOI** (Bước 3 dùng 3×3 ô × 8 unit = 24×24) thì mới thấy được cảnh người biến mất khi đi xa.

**Sửa `MovementRules.Step`** — nhận thêm `MapGrid map`, thay `Math.Clamp` bằng va chạm **tách trục**:

```
thử đi cả (nx, ny)      → được thì đi
thử đi ngang (nx, y)    → được thì trượt ngang
thử đi dọc  (x, ny)     → được thì trượt dọc
                        → không thì đứng yên
```

Tách trục chính là thứ tạo cảm giác "trượt dọc tường" khi đi chéo vào tường — chuẩn hành vi 2D top-down.
Nhân vật coi như **một điểm** (chưa có bán kính thân) — chấp nhận ở bản học, ghi chú lại.

Hai chỗ gọi `Step` (server `Integrate`, client `PlayerMotor`) truyền thêm `Maps.Map1`. `WorldService`
kiểm spawn point: ô spawn phải walkable — `assert` lúc khởi động còn hơn nhân vật chôn trong tường.

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`Server/Shared/World/MapGrid.cs`**:

```csharp
using System;

namespace MMORPG.Shared.World
{
    /// <summary>
    /// Bản đồ dạng lưới ô vuông 1×1: mỗi ô là sàn hoặc tường. Là nguồn DUY NHẤT về hình dạng map —
    /// server va chạm thật, client va chạm dự đoán và vẽ hình đều đọc từ đây.
    /// </summary>
    public sealed class MapGrid
    {
        public const float CELL_SIZE = 1f;

        public int Width { get; }
        public int Height { get; }

        private readonly bool[,] _walkable;

        private MapGrid(bool[,] walkable, int width, int height)
        {
            _walkable = walkable;
            Width = width;
            Height = height;
        }

        /// <summary>
        /// Dựng từ mảng chuỗi: '#' tường, mọi ký tự khác là sàn. Hàng ĐẦU của mảng là mép TRÊN map
        /// (để code đọc như bản vẽ) — nên trục Y phải lật khi nạp vào lưới: hàng cuối là cy = 0.
        /// </summary>
        public static MapGrid Parse(string[] rows)
        {
            int height = rows.Length;
            int width = rows[0].Length;

            var walkable = new bool[width, height];

            for (int rowIndex = 0; rowIndex < height; rowIndex++)
            {
                if (rows[rowIndex].Length != width)
                    throw new ArgumentException($"Hàng {rowIndex} dài {rows[rowIndex].Length}, các hàng phải cùng {width} ký tự.");

                for (int cx = 0; cx < width; cx++)
                {
                    int cy = height - 1 - rowIndex; // lật trục Y
                    walkable[cx, cy] = rows[rowIndex][cx] != '#';
                }
            }

            return new MapGrid(walkable, width, height);
        }

        public bool IsWalkable(int cx, int cy)
        {
            // Ngoài rìa map là tường ngầm định — không cần viền clamp riêng nữa.
            if (cx < 0 || cx >= Width || cy < 0 || cy >= Height)
                return false;

            return _walkable[cx, cy];
        }

        /// <summary>Map đặt GIỮA gốc toạ độ: world (0,0) là tâm map — khớp spawn point mặc định.</summary>
        public bool IsWalkableWorld(float x, float y)
        {
            // Floor chứ không phải cast (int): cast cắt về 0 nên -0.5 thành 0 — âm dương
            // hai bên gốc toạ độ sẽ rơi vào cùng một ô và va chạm lệch nửa ô ở phần map bên trái.
            int cx = (int)MathF.Floor(x / CELL_SIZE + Width / 2f);
            int cy = (int)MathF.Floor(y / CELL_SIZE + Height / 2f);

            return IsWalkable(cx, cy);
        }
    }
}
```

**`Server/Shared/World/Maps.cs`**:

```csharp
namespace MMORPG.Shared.World
{
    /// <summary>
    /// Các map của game, định nghĩa bằng chữ: '#' tường, '.' sàn.
    /// Đọc như bản vẽ — hàng đầu là mép trên map.
    /// </summary>
    public static class Maps
    {
        public static readonly MapGrid Map1 = MapGrid.Parse(new[]
        {
            "########################################",
            "#......................................#",
            "#......####............####............#",
            "#......#..................#............#",
            "#......#..................#............#",
            "#......................................#",
            "#..........#####.......................#",
            "#..............#.......................#",
            "#..............#..........####.........#",
            "#..............#..........#............#",
            "#..........................#...........#",
            "#......................................#",
            "#......####............................#",
            "#......................................#",
            "#......................................#",
            "########################################",
        });
    }
}
```

> Map của bạn cứ tự vẽ theo ý — chỉ cần: viền `#` kín, mọi hàng cùng độ dài, **hai cạnh là số chẵn**
> (map trên là 16×40 — xem ghi chú phép chia ở `MapRenderer`),
> vùng quanh tâm map (spawn) là sàn, và có vài cụm tường bên trong để có gì mà né.
> `Parse` ném lỗi ngay lúc khởi động nếu các hàng lệch độ dài — đó là hàng rào của bạn.

**`MovementRules.Step`** — bản mới (thay hẳn bản clamp, xoá `WORLD_HALF_EXTENT`):

```csharp
        /// <summary>
        /// Một bước mô phỏng có va chạm. Tách trục để trượt dọc tường: đi chéo vào tường thì
        /// thành phần song song với tường vẫn đi tiếp, chỉ thành phần đâm vào tường bị chặn.
        /// Nhân vật coi như một điểm, chưa có bán kính thân.
        /// </summary>
        public static (float X, float Y) Step(float x, float y, float dirX, float dirY, float dt, MapGrid map)
        {
            float nx = x + dirX * MOVE_SPEED * dt;
            float ny = y + dirY * MOVE_SPEED * dt;

            if (map.IsWalkableWorld(nx, ny))
                return (nx, ny);

            if (map.IsWalkableWorld(nx, y))
                return (nx, y);

            if (map.IsWalkableWorld(x, ny))
                return (x, ny);

            return (x, y);
        }
```

Hai chỗ gọi sửa thành `MovementRules.Step(..., Maps.Map1)`:
- `PlayerEntity.Integrate` (server)
- `PlayerMotor.Step` và vòng replay trong `OnMoveState` (client)

</details>

---

## Bước 2 — Client: vẽ map từ chính MapGrid

### Hướng làm

**File mới `Assets/Game/Scripts/World/MapRenderer.cs`** — MonoBehaviour cầm một `Tilemap` (URP 2D:
GameObject → 2D Object → Tilemap → Rectangular) và hai `TileBase` (sàn / tường — hai sprite vuông trơn
khác màu là đủ; tạo Tile asset từ sprite bằng Create → 2D → Tiles → Rule/hoặc Tile thường).

`Start()`: duyệt toàn bộ ô của `Maps.Map1`, `SetTile` sàn hoặc tường. Điểm sống còn: phép đổi
**ô → world** của renderer phải trùng khớp `IsWalkableWorld` (map đặt giữa gốc toạ độ) — lệch nửa ô là
nhân vật "đụng tường vô hình" cạnh tường thấy được. Cách chắc ăn: đặt `Tilemap` tại gốc, `SetTile` theo
`cx - Width/2, cy - Height/2` với anchor mặc định của Grid (cell size 1).

Vẽ map bằng code từ MapGrid thay vì tự paint tilemap bằng tay — vì paint tay là **chép tay contract**:
đúng loại anti-pattern số 1 của repo, chỉ khác chỗ nạn nhân là map thay vì enum.

### ✅ CHECKPOINT A — tường là thật

1. Vào world: thấy map có sàn/tường, spawn giữa map.
2. Đi thẳng vào tường: dừng **sát mép ô tường**, không rung, không rubber-band (client và server cùng
   một `Step` — dự đoán trùng sự thật tuyệt đối).
3. Đi **chéo** vào tường: trượt mượt dọc theo tường.
4. Thử hack xuyên tường: sửa tạm client bỏ qua va chạm trong dự đoán (truyền map "trống" vào `Step`
   phía client) → nhân vật lao vào tường trên máy mình rồi bị **kéo giật lại** liên tục — server không
   cho qua. Trả code về như cũ. Đây là "thử nghiệm 1 Phase 6" phiên bản có tường.

---

## Bước 3 — Server: AOI — tầm nhìn quyết định mọi thứ

### Hướng làm

Tư tưởng quan trọng nhất phase: **`EntitySpawn`/`EntityDespawn` không còn là "sự kiện vào/ra world"
nữa — chúng là hệ quả của việc ai đó VÀO/RA TẦM NHÌN của bạn.** Người mới vào world chỉ là một cách để
lọt vào tầm nhìn; đi bộ lại gần là cách khác — một cơ chế phục vụ cả hai, và **client không phải sửa
một dòng nào** (phần thưởng của thiết kế message-driven Phase 7).

Sửa `WorldService`:

**1. Xoá broadcast trong `Spawn`/`Despawn`** (phần thêm ở Phase 7 Bước 2 — bỏ cả vòng "gửi danh sách
người có mặt cho người mới"). Từ giờ mọi thông báo xuất hiện/biến mất đều do vòng tick phát ra.

**2. `PlayerEntity` thêm `HashSet<int> Visible`** — tập entityId đang nằm trong tầm nhìn của người này.
Chỉ luồng tick đọc/ghi → không cần lock (ghi comment ranh giới luồng như đã làm với input).

**3. `Tick` thêm pha tầm nhìn**, sau pha tích phân, trước pha gửi:

- Dựng chỉ mục ô: `Dictionary<(int, int), List<PlayerEntity>>` — ô AOI của entity =
  `(Floor(X / AOI_CELL), Floor(Y / AOI_CELL))` với `AOI_CELL = 8f`. **Dựng lại từ đầu mỗi tick** —
  O(n), đơn giản tuyệt đối, không có trạng thái để sai.
- Với từng entity: gom mọi entity trong **9 ô** quanh ô của mình (trừ chính mình) → `visibleNow`.
- So với `entity.Visible`:
  - có trong `visibleNow` mà chưa có trong `Visible` → gửi chủ nhân `EntitySpawn` của người đó;
  - có trong `Visible` mà biến mất khỏi `visibleNow` → gửi `EntityDespawn`.
- Thay `Visible` bằng `visibleNow`; snapshot bây giờ chỉ chứa `visibleNow` (hết O(n²) toàn server).

**Câu hỏi phải tự trả lời trước khi code:** người chơi ở góc dưới-trái ô AOI thì tầm nhìn thực về phía
dưới-trái chỉ còn 8 unit, về phía trên-phải tới 16 unit — tầm nhìn "vuông và lệch". Có sao không? —
Không, miễn là **bán kính bảo đảm** (8 unit ở trường hợp xấu nhất) lớn hơn màn hình nhìn thấy. Đổi lấy
điều đó: tra 9 ô thay vì tính n² khoảng cách. Đây là đánh đổi kinh điển của spatial grid.

### ✅ CHECKPOINT B — mục tiêu cuối Phase 9

1. Hai client vào world cạnh nhau → thấy nhau (như Phase 7, giờ qua đường tầm nhìn).
2. Một người chạy xa (map 40 ô ngang đủ chỗ) → tới ranh giới ~16–24 unit, người kia **biến mất** khỏi
   màn hình; console hiện `EntityDespawn`.
3. Chạy ngược lại → hiện ra lại đúng vị trí, đi tiếp mượt (buffer nội suy mồi lại từ `EntitySpawn`).
4. Đứng gần nhau, một người thoát hẳn → người kia vẫn thấy despawn (đường cũ nay do diff đảm nhiệm:
   entity rời sổ → rời `visibleNow` → despawn).
5. Log tạm kích thước snapshot: đứng cạnh nhau = 1 state, đi xa = 0 state — băng thông tỉ lệ với
   **mật độ quanh mình**, không phải tổng người online. Đó là câu trả lời cho "vì sao MMO gánh được
   nghìn người".

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`PlayerEntity.cs`** — thêm:

```csharp
        /// <summary>
        /// Tập entityId đang trong tầm nhìn của người này — bộ nhớ để tick sau so ra ai vừa
        /// xuất hiện / vừa rời đi. Chỉ luồng tick đọc/ghi, vì vậy không cần lock.
        /// </summary>
        public HashSet<int> Visible { get; } = new();
```

**`WorldService.cs`** — xoá hai đoạn broadcast thêm ở Phase 7 trong `Spawn`/`Despawn` (giữ nguyên phần
ghi sổ + log), rồi thay `Tick`:

```csharp
        /// <summary>Cạnh ô lưới AOI. Tầm nhìn = 9 ô quanh mình → bán kính bảo đảm tối thiểu 1 ô.</summary>
        private const float AOI_CELL = 8f;

        public void Tick(float dt)
        {
            // Pha 1: tích phân tất cả.
            foreach (PlayerEntity entity in _entities.Values)
                entity.Integrate(dt);

            // Pha 2: dựng chỉ mục ô AOI — làm lại từ đầu mỗi tick. O(n) và không có trạng thái
            // để sai; bản cập-nhật-tại-chỗ nhanh hơn nhưng phải đúng ở mọi đường vào/ra — chưa đáng.
            var byCell = new Dictionary<(int, int), List<PlayerEntity>>();

            foreach (PlayerEntity entity in _entities.Values)
            {
                (int, int) cell = CellOf(entity);

                if (!byCell.TryGetValue(cell, out List<PlayerEntity> list))
                {
                    list = new List<PlayerEntity>();
                    byCell[cell] = list;
                }

                list.Add(entity);
            }

            // Pha 3: với từng người — tầm nhìn mới, so với tầm nhìn cũ, phát spawn/despawn, gửi snapshot.
            foreach (PlayerEntity viewer in _entities.Values)
            {
                if (viewer.Owner == null)
                    continue;

                List<PlayerEntity> visibleNow = CollectVisible(viewer, byCell);

                // Ai mới lọt vào tầm nhìn → giới thiệu họ với viewer.
                foreach (PlayerEntity seen in visibleNow)
                {
                    if (!viewer.Visible.Contains(seen.EntityId))
                        viewer.Owner.SendData(NetCmd.EntitySpawn, ToSpawnNotice(seen));
                }

                // Ai vừa rời tầm nhìn → báo biến mất. Duyệt bản sao vì sắp sửa chính Visible.
                viewer.Visible.RemoveWhere(id =>
                {
                    bool stillVisible = visibleNow.Exists(e => e.EntityId == id);

                    if (!stillVisible)
                        viewer.Owner.SendData(NetCmd.EntityDespawn, new EntityDespawnNotice { EntityId = id });

                    return !stillVisible;
                });

                foreach (PlayerEntity seen in visibleNow)
                    viewer.Visible.Add(seen.EntityId);

                // Pha 4: gửi trạng thái — MoveState cho chính mình, snapshot CHỈ những ai trong tầm.
                viewer.Owner.SendData(NetCmd.MoveState, new MoveStateResponse
                {
                    LastInputSeq = viewer.LastInputSeq,
                    X = viewer.X,
                    Y = viewer.Y,
                });

                var states = new EntityState[visibleNow.Count];
                for (int i = 0; i < visibleNow.Count; i++)
                {
                    states[i] = new EntityState
                    {
                        EntityId = visibleNow[i].EntityId,
                        X = visibleNow[i].X,
                        Y = visibleNow[i].Y,
                    };
                }

                viewer.Owner.SendData(NetCmd.WorldSnapshot, new WorldSnapshotNotice { States = states });
            }
        }

        private static (int, int) CellOf(PlayerEntity entity)
        {
            // Floor chứ không phải cast: toạ độ âm phải rơi về ô bên trái, không gom về ô 0.
            return ((int)MathF.Floor(entity.X / AOI_CELL), (int)MathF.Floor(entity.Y / AOI_CELL));
        }

        /// <summary>Mọi entity trong 9 ô quanh viewer, trừ chính viewer.</summary>
        private static List<PlayerEntity> CollectVisible(
            PlayerEntity viewer, Dictionary<(int, int), List<PlayerEntity>> byCell)
        {
            var result = new List<PlayerEntity>();
            (int cx, int cy) = CellOf(viewer);

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (!byCell.TryGetValue((cx + dx, cy + dy), out List<PlayerEntity> cell))
                        continue;

                    foreach (PlayerEntity entity in cell)
                    {
                        if (entity.EntityId != viewer.EntityId)
                            result.Add(entity);
                    }
                }
            }

            return result;
        }
```

(`Broadcast` helper của Phase 7 không còn ai gọi — xoá luôn cho sạch, cần thì git history còn.)

**`Assets/Game/Scripts/World/MapRenderer.cs`**:

```csharp
using MMORPG.Shared.World;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace MMORPG.Client.World
{
    /// <summary>
    /// Vẽ tilemap từ MapGrid trong Shared. Vẽ bằng code chứ không paint tay — paint tay là chép tay
    /// contract, sớm muộn hình một đằng va chạm một nẻo.
    /// </summary>
    public sealed class MapRenderer : MonoBehaviour
    {
        [SerializeField] private Tilemap _tilemap;
        [SerializeField] private TileBase _floorTile;
        [SerializeField] private TileBase _wallTile;

        private void Start()
        {
            MapGrid map = Maps.Map1;

            for (int cx = 0; cx < map.Width; cx++)
            {
                for (int cy = 0; cy < map.Height; cy++)
                {
                    // Cùng phép dời tâm với IsWalkableWorld: ô (cx,cy) đặt tại world (cx - W/2, cy - H/2).
                    var cell = new Vector3Int(cx - map.Width / 2, cy - map.Height / 2, 0);
                    _tilemap.SetTile(cell, map.IsWalkable(cx, cy) ? _floorTile : _wallTile);
                }
            }
        }
    }
}
```

> Lưu ý phép chia: `Width / 2` trong `MapRenderer` là chia **nguyên**, còn `Width / 2f` trong
> `IsWalkableWorld` là chia **thực** — với map cạnh chẵn hai phép cho cùng kết quả; map cạnh lẻ sẽ lệch
> nửa ô. Giữ cạnh map là số **chẵn**, hoặc thống nhất cả hai về `/ 2f` + `Mathf.FloorToInt`.

Trong Editor: tạo Grid + Tilemap (cell size 1, tại gốc toạ độ), hai Tile asset từ hai sprite vuông,
gắn `MapRenderer`, kéo tham chiếu. Xoá Grid/Tilemap "vẽ vài ô sàn" của Phase 5 nếu còn.

</details>

---

## Bước 4 — Ba thử nghiệm bắt buộc

**1. Hack xuyên tường "thông minh".** Sửa client gửi thẳng input hướng vào tường liên tục (không cần bỏ
dự đoán — cứ ép `dir` đâm vào tường). Server trượt/chặn — nhân vật đứng sát tường, không bao giờ lọt qua,
kể cả khe tường 1 ô. Vì kiểm tra nằm trong `Step` của server, không phải trong thiện chí của client.

**2. Nhảy múa ở ranh giới AOI.** Hai người đứng hai bên ranh giới ô AOI, một người bước qua-lại quanh
ranh giới → người kia thấy bạn mình **nhấp nháy** hiện/biến. Đây là flicker kinh điển của AOI không có
hysteresis (vào và ra cùng một ngưỡng). Không sửa ở phase này — nhưng phải **thấy nó bằng mắt** và trả
lời được câu 7 bên dưới về cách sửa.

**3. Đo cái AOI mua được.** Log tạm tổng số `EntityState` server gửi mỗi giây. Hai client đứng cạnh
nhau: ~40/giây (20 tick × 2 người × 1 state). Đi xa nhau: **0**. Với broadcast Phase 7 con số này không
bao giờ về 0 dù map to cỡ nào — và tăng theo bình phương tổng người online thay vì theo mật độ cục bộ.

---

## Troubleshooting

| Triệu chứng | Nguyên nhân | Xử lý |
|-------------|-------------|-------|
| Đụng "tường vô hình" cạnh tường thật, lệch ~nửa ô | Phép ô→world của `MapRenderer` lệch với `IsWalkableWorld` (chia nguyên vs chia thực, map cạnh lẻ) | Xem ghi chú cuối lời giải Bước 3; giữ cạnh map chẵn |
| Va chạm sai hết ở nửa map bên trái/dưới | Dùng cast `(int)` thay `MathF.Floor` với toạ độ âm | Đọc comment trong `IsWalkableWorld` / `CellOf` |
| Map hiển thị lộn ngược so với chuỗi trong code | Quên lật trục Y trong `Parse` | Hàng đầu của mảng = mép trên map |
| Rubber-band khi đi sát tường | Client và server không cùng một `Step` (một bên còn bản clamp cũ) | Build lại Shared, kiểm cả replay trong `OnMoveState` cũng truyền map |
| `Parse` ném lỗi lúc khởi động | Các hàng map lệch độ dài (thiếu một dấu chấm) | Đó là hàng rào hoạt động đúng — sửa map |
| Spawn chôn trong tường | Ô quanh (0,0) là `#`, hoặc map cạnh lẻ làm tâm lệch | Chừa sàn quanh tâm map |
| Người kia không bao giờ biến mất dù đi rất xa | Vẫn còn broadcast của Phase 7 trong `Spawn`, hoặc snapshot vẫn build từ toàn bộ `_entities` | Mọi thông báo phải đi từ pha diff của `Tick` |
| Người kia biến mất rồi không hiện lại | `Visible` không được cập nhật đúng (xoá mà không thêm lại) | So thứ tự ba thao tác: spawn-mới → despawn-cũ → cập nhật tập |
| Nhấp nháy hiện/biến ở một khoảng cách nhất định | Flicker ranh giới AOI — hành vi đã biết | Thử nghiệm 2; sửa thật thì cần hysteresis (câu 7) |
| Client crash `NullReference` trong `SetTile` | Tile asset chưa kéo vào Inspector | Kiểm `_floorTile`/`_wallTile` |

---

## Tự kiểm tra hiểu bài

Tự trả lời từng câu xong mới mở đáp án của câu đó.

**Câu 1.** Vì sao dữ liệu map phải nằm trong `Shared` chứ không phải "server giữ map, client tự vẽ
tilemap giống giống là được"?
<details>
<summary>📖 Đáp án câu 1</summary>

Vì **ba** bên tiêu thụ cùng một sự thật: server va chạm thật, client va chạm dự đoán, client vẽ hình.
Client tự vẽ tay là chép tay contract — giống hệt chép tay `NetCmd`: không lỗi biên dịch, chỉ có
"tường nhìn thấy mà đi xuyên được" và "tường vô hình" xuất hiện dần theo mỗi lần sửa map một bên.
Riêng va chạm dự đoán mà lệch server là rubber-band vĩnh viễn tại đúng chỗ lệch.

</details>

**Câu 2.** Va chạm tách trục (thử cả hai → thử ngang → thử dọc) tạo ra hành vi gì mà kiểm tra
"đi được cả hai trục hay đứng yên" không có? Vì sao hành vi đó đáng giá?
<details>
<summary>📖 Đáp án câu 2</summary>

Trượt dọc tường: đi chéo vào tường thì thành phần song song vẫn tiến, chỉ thành phần đâm vào bị chặn.
Không tách trục thì chạm tường là **dính cứng** — muốn đi dọc tường phải nhả phím chéo và bấm lại đúng
trục, cảm giác điều khiển tệ đi rõ rệt. Điều khiển mượt ở sát tường quan trọng vì tường là nơi người
chơi ở cạnh thường xuyên nhất.

</details>

**Câu 3.** Vì sao `IsWalkableWorld` và `CellOf` phải dùng `Floor` chứ không phải ép kiểu `(int)`?
Mô tả bug cụ thể nếu dùng cast.
<details>
<summary>📖 Đáp án câu 3</summary>

Cast cắt **về phía 0**: `-0.7` thành `0`, trong khi `Floor(-0.7) = -1`. Mọi toạ độ trong khoảng
`(-1, 1)` bị gom về ô 0 — ô cạnh gốc toạ độ "rộng gấp đôi", và toàn bộ phần map toạ độ âm bị lệch một ô:
va chạm sai đúng nửa map, AOI xếp người vào nhầm ô ở nửa map đó. Bug đối xứng lệch kiểu này rất khó nhìn
ra bằng chơi thử vì nửa map còn lại hoàn toàn bình thường.

</details>

**Câu 4.** Vì sao chuyển `EntitySpawn`/`EntityDespawn` từ "broadcast lúc vào/ra world" sang "hệ quả của
diff tầm nhìn mỗi tick" lại khiến client **không phải sửa gì**? Thiết kế nào của Phase 7 mua được điều đó?
<details>
<summary>📖 Đáp án câu 4</summary>

Client Phase 7 được viết theo **message**, không theo **nguyên nhân**: nó chỉ biết "có gói bảo X xuất
hiện thì dựng X, có gói bảo X biến mất thì dọn X" — không quan tâm vì sao. Server đổi hoàn toàn logic
phát sinh các gói đó (từ sự kiện vào/ra world sang diff tầm nhìn) mà contract không đổi, nên client cũ
chạy nguyên. Đây là phần thưởng cụ thể của việc tách "điều đã xảy ra" (message) khỏi "vì sao nó xảy ra"
(logic server).

</details>

**Câu 5.** Spatial grid trả lời "ai gần ai" bằng cách tra 9 ô. So với tính khoảng cách mọi cặp (O(n²)),
nó đánh đổi cái gì? Tầm nhìn "vuông và lệch theo vị trí trong ô" vì sao chấp nhận được?
<details>
<summary>📖 Đáp án câu 5</summary>

Đổi **độ chính xác hình học** lấy **độ phức tạp**: tầm nhìn không phải hình tròn bán kính r mà là vùng
9 ô — vuông, và lệch tuỳ bạn đứng đâu trong ô (bảo đảm tối thiểu 1 cạnh ô, tối đa 2). Chấp nhận được vì
tầm nhìn chỉ cần một tính chất: **bán kính bảo đảm ≥ những gì màn hình thấy** — dư ra bao nhiêu không ai
nhận biết (thứ ngoài màn hình có được sync sớm một chút cũng vô hại). Trong khi cái mua được là mỗi tick
chỉ tra vài ô thay vì n² phép so — chính là thứ cho phép nghìn người online.

</details>

**Câu 6.** Dựng lại chỉ mục ô từ đầu mỗi tick (O(n)) thay vì cập nhật khi entity đổi ô. Lập luận cho
lựa chọn này, và khi nào nó không còn đúng?
<details>
<summary>📖 Đáp án câu 6</summary>

Bản dựng-lại không có trạng thái sống qua tick → không có lớp bug "chỉ mục lệch thực tế" (quên gỡ ô cũ,
quên thêm ô mới, entity chết còn trong ô...). Giá của nó là O(n) + cấp phát mỗi tick — với vài trăm
entity ở 20Hz là không đáng kể. Nó hết đúng khi n lớn tới mức đo được chi phí (nhiều nghìn entity kèm
GC pressure) — lúc đó chuyển sang cập-nhật-tại-chỗ *kèm* một bản kiểm tra đối chiếu định kỳ, vì lớp bug
kia sẽ quay lại cùng hiệu năng.

</details>

**Câu 7.** Flicker ở ranh giới AOI (thử nghiệm 2): nguyên nhân chính xác là gì và hysteresis sửa nó
thế nào?
<details>
<summary>📖 Đáp án câu 7</summary>

Vào và ra tầm nhìn dùng **cùng một ngưỡng** (ranh giới ô), nên người đứng ngay ranh giới chỉ cần dao
động 1cm là đổi trạng thái — mỗi lần đổi là một cặp gói spawn/despawn và một lần dựng/huỷ GameObject.
Hysteresis tách hai ngưỡng: **vào** tầm nhìn ở bán kính nhỏ (ví dụ 9 ô), chỉ **ra** khỏi tầm nhìn khi
vượt bán kính lớn hơn (ví dụ 5×5 ô) — người đứng giữa hai ngưỡng giữ nguyên trạng thái hiện có. Dao động
nhỏ quanh một điểm không còn đổi trạng thái được nữa; giá phải trả là tầm "ra" rộng hơn tầm "vào" một
vành đai.

</details>

**Câu 8.** Sau AOI, băng thông server tỉ lệ với đại lượng nào thay vì tổng số người online? Vì sao đó
là câu trả lời cho "MMO gánh nghìn người kiểu gì"?
<details>
<summary>📖 Đáp án câu 8</summary>

Tỉ lệ với `số người × mật độ trung bình quanh mỗi người` — tức **mật độ cục bộ**, thứ bị giới hạn bởi
diện tích màn hình/tầm nhìn chứ không phải bởi tổng dân số. Nghìn người rải trên map lớn thì mỗi người
chỉ trả tiền cho vài chục người quanh mình; tổng chi phí tăng tuyến tính theo dân số thay vì bình
phương. (Và khi nghìn người cố tình dồn vào một ô — sự kiện, boss chung — thì mật độ cục bộ nổ, đó chính
là lý do MMO thật lag ở chỗ đông dù server "gánh nghìn người" ngon lành khi họ tản ra.)

</details>

---

**Xong Phase 9 → thế giới có tường, có tầm nhìn, băng thông theo mật độ.** [PHASE-10](PHASE-10.md) trả nốt
món nợ rải khắp ba phase vừa qua: các con số (tốc độ, spawn, map) đang nằm cứng trong code — đưa chúng
ra dữ liệu, sửa không cần build lại, và hiểu vì sao config cũng phải có đúng một nguồn.
