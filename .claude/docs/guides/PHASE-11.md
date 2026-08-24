# PHASE 11 — AOI: chỉ thấy người ở gần

> **Kết quả cuối Phase 11:** hai người chơi chạy xa nhau thì **biến mất** khỏi màn hình của nhau, chạy
> lại gần thì hiện ra — đúng vị trí, đúng hướng mặt, đi tiếp mượt. Băng thông server gửi cho mỗi người
> tỉ lệ với **mật độ quanh họ**, không phải với tổng số người online. Và client **không sửa một dòng nào**.
>
> **Điều kiện:** xong [`PHASE-10.md`](PHASE-10.md) tới CHECKPOINT C — map có hình dạng thật, hai bên
> chạy đúng một lưới va chạm.
>
> **Bài học chính:** (1) chia không gian phải khớp **hình dạng của thế giới**, không khớp thói quen;
> (2) `EntitySpawn`/`EntityDespawn` đổi từ *"sự kiện vào/ra world"* thành *"hệ quả của tầm nhìn"* mà
> contract không đổi một chữ — đó là phần thưởng cụ thể của việc Phase 7 đặt tên gói theo **điều đã xảy
> ra** chứ không theo **nguyên nhân**.

Format như trước: **hướng làm** hiện sẵn, **📖 Lời giải** trong foldout.

> 📌 Phase này **tách ra từ Phase 10 cũ** (2026-08-24), đúng theo luật rút ra từ Phase 9: việc nào tự
> nó test được thì tách thành phase riêng. Map và AOI chẳng liên quan gì nhau ngoài chữ "không gian".

---

## Một câu hỏi khác hẳn

Phase 10 trả lời *"đi được chỗ nào"*. Phase này trả lời *"thấy được những ai"*. Hai câu hỏi không gian,
hai lưới khác nhau, và đó là chuyện bình thường:

| | Lưới va chạm (Phase 10) | Lưới tầm nhìn (phase này) |
|---|---|---|
| Ô rộng | 1 unit | **12 unit** |
| Vì sao | tường mỏng, phải mịn | màn hình rộng, thô là đủ |
| Chia mấy trục | X và Y | **chỉ X** |
| Sống ở đâu | `Shared` — cả hai bên đọc | **chỉ server** — client không cần biết nó tồn tại |

Dòng cuối đáng dừng lại: AOI là chuyện **hoàn toàn của server**. Client chỉ nhận gói "có người xuất
hiện" / "có người biến mất" và làm đúng như từ Phase 7 tới giờ. Không `NetCmd` mới, không DTO mới, không
một dòng nào ở `Assets/`.

### Vì sao chia cột theo X, không chia lưới ô 2D

Cách quen thuộc (và cách một game top-down phải làm) là chia lưới 2D rồi tra 9 ô quanh mình. Ở
side-scroller thì đó là **trả tiền cho một chiều không dùng**:

| | Bề ngang map | Bề cao map | Màn hình thấy |
|---|---|---|---|
| Kích thước | ~64 unit (và sẽ còn dài ra) | ~11 unit | ~17.8 × 10 unit |

Map cao 11 unit mà một màn hình đã cao 10 — chia trục Y thành ô 12 unit thì **gần như mọi người luôn ở
cùng một hàng**, và ta trả thêm một chiều trong khoá `Dictionary` để nhận về một phép lọc gần như không
lọc gì.

> Chỉ chia ô ở **trục mà thế giới thật sự lớn**. Với side-scroller, đó là trục X — và chỉ trục X.

Nên: **cột** rộng `AOI_COLUMN_WIDTH = 12f`, tầm nhìn = 3 cột quanh mình (`cx-1`, `cx`, `cx+1`). Bán kính
bảo đảm ở trường hợp xấu nhất (đứng sát mép cột) là đúng **12 unit** mỗi bên, so với nửa màn hình ~9 —
dư một chút, đúng như cần.

Cùng lập luận đánh đổi của mọi spatial grid: tầm nhìn không phải hình tròn bán kính r mà là một dải chữ
nhật lệch tuỳ chỗ đứng trong cột. Không sao, vì tầm nhìn chỉ cần **một** tính chất: bán kính bảo đảm ≥
những gì màn hình thấy. Dư ra thì không ai nhận biết.

Tổng quát hoá để mang đi: cấu trúc chia không gian phải khớp **hình dạng của thế giới**. Game top-down
map vuông thì lưới 2D là đúng; game bay trong không gian thì phải là octree; side-scroller thì là cột.

---

## Bước 1 — Server: chỉ mục cột và phép so tầm nhìn

### Hướng làm

Tư tưởng quan trọng nhất của phase, viết ra một lần cho rõ:

> **`EntitySpawn`/`EntityDespawn` không còn là "sự kiện vào/ra world" nữa — chúng là hệ quả của việc ai
> đó VÀO/RA TẦM NHÌN của bạn.**

Người mới vào world chỉ là *một cách* để lọt vào tầm nhìn; đi bộ lại gần là cách khác. Một cơ chế phục
vụ cả hai.

**Lọc theo `MapId` trước.** Hiện chỉ có một map nên nó chưa lọc gì, nhưng nó là **ranh giới cứng**: hai
người ở hai map khác nhau không bao giờ thấy nhau, dù `X` của họ bằng nhau. Viết nó ngay bây giờ rẻ hơn
nhiều so với đi tìm lý do vì sao người ở hang động nhìn thấy người ở đồng cỏ.

**Bốn việc trong `WorldService`:**

1. **Xoá hai đoạn broadcast trong `Spawn`/`Despawn`** (phần thêm ở Phase 7 — bỏ cả vòng "gửi danh sách
   người đang có mặt cho người mới"). Từ giờ **mọi** thông báo xuất hiện/biến mất do vòng tick phát ra.
   Helper `Broadcast<T>` không còn ai gọi — xoá luôn, cần thì git history còn.
2. **`PlayerEntity` thêm `HashSet<int> Visible`** — tập entityId đang trong tầm nhìn của người này. Chỉ
   luồng tick đọc/ghi, ghi comment ranh giới luồng như đã làm với input ở Phase 8.
3. **Thêm pha dựng chỉ mục cột** vào `Tick`, sau pha tích phân: `Dictionary<(int MapId, int Column), List<PlayerEntity>>`,
   **dựng lại từ đầu mỗi tick**.
4. **Pha gửi đổi thành pha so**: với từng người → gom mọi entity trong 3 cột quanh mình → so với `Visible`
   → ai mới thì `EntitySpawn`, ai mất thì `EntityDespawn` → snapshot **chỉ chứa** những người trong tầm.

**Vì sao dựng lại chỉ mục từ đầu mỗi tick thay vì cập nhật tại chỗ?** Bản cập-nhật-tại-chỗ nhanh hơn,
nhưng nó phải đúng ở **mọi** đường vào và ra: spawn, despawn, mất kết nối, chuyển map, và mỗi lần ai đó
bước qua ranh giới cột. Quên một đường là chỉ mục lệch thực tế — mà chỉ mục lệch thì không có triệu chứng
nào ngoài "thỉnh thoảng có người vô hình". Dựng lại là O(n) mỗi tick và **không có trạng thái nào sống
qua tick**, nên cả lớp bug đó không tồn tại. Đổi khi nào profiler chỉ đúng vào đây, không đổi trước.

**Thứ tự ba thao tác diff là một phần của luật:**

```
1. báo người MỚI  (visibleNow có, Visible chưa có)   → EntitySpawn
2. báo người ĐI   (Visible có, visibleNow không có)  → EntityDespawn
3. cập nhật tập Visible = visibleNow
```

Đảo (3) lên trước là tập đã bị ghi đè trước khi kịp so — và triệu chứng là **không ai despawn bao giờ**.

**Một chi tiết nhỏ mà đắt: đừng cấp phát mới mỗi tick.** 20 tick/giây × (một `Dictionary` + một `List`
cho mỗi cột + một `List` cho mỗi người) là rác GC đều đặn suốt đời server. Giữ chúng làm field và
`Clear()` — cùng loại tối ưu như buffer đọc gói ở Phase 1, và cùng lý do: thứ chạy mỗi tick thì hình
dạng bộ nhớ của nó là một phần thiết kế, không phải chi tiết cài đặt.

<details>
<summary><b>📖 Lời giải — <code>PlayerEntity</code></b></summary>

```csharp
        /// <summary>
        /// Tập entityId đang trong tầm nhìn của người này — bộ nhớ để tick sau so ra ai vừa xuất hiện,
        /// ai vừa rời đi.
        ///
        /// CHỈ LUỒNG TICK đọc/ghi, vì vậy không cần lock và không được đụng tới từ handler.
        /// </summary>
        public HashSet<int> Visible { get; } = new();
```

</details>

<details>
<summary><b>📖 Lời giải — <code>WorldService</code></b></summary>

Xoá trong `Spawn` cả vòng "giới thiệu người cũ cho người mới" lẫn dòng `Broadcast(NetCmd.EntitySpawn, …)`,
xoá dòng `Broadcast(NetCmd.EntityDespawn, …)` trong `Despawn`, xoá luôn helper `Broadcast<T>`. `Spawn`
chỉ còn ghi sổ và log:

```csharp
            _entities[entityId] = entity;
            _entityIdByAccount[entity.AccountId] = entity.EntityId;

            Log.Info($"Spawn {entity.Name.Cyan()} entity {entityId.ToString().Green()} " +
                     $"tại map {entity.MapId} ({entity.X:0.##}, {entity.Y:0.##}) — {OnlineCount} người trong world");

            // Không thông báo gì ở đây nữa. Ai thấy được người này thì tick kế tiếp sẽ tự phát hiện —
            // "vừa vào world" chỉ là MỘT cách để lọt vào tầm nhìn ai đó, không phải cách duy nhất.
            return entity;
```

Thêm hằng, hai bộ đệm dùng lại, và `Tick` mới:

```csharp
        /// <summary>
        /// Bề ngang một cột tầm nhìn. Tầm nhìn = 3 cột → bán kính bảo đảm 12 unit mỗi bên, rộng hơn
        /// nửa màn hình (~9 unit) một chút.
        ///
        /// Chỉ chia theo trục X: map cao ~11 unit mà một màn hình đã cao 10, nên chia trục Y là tốn
        /// thêm một chiều trong khoá để nhận về một phép lọc gần như không lọc gì.
        /// </summary>
        private const float AOI_COLUMN_WIDTH = 12f;

        // Hai bộ đệm của vòng tick, giữ làm field và Clear() mỗi lần dùng. Cấp phát mới mỗi tick là
        // rác GC đều đặn 20 lần/giây suốt đời server — thứ chạy mỗi tick thì hình dạng bộ nhớ của nó
        // là một phần thiết kế. Chỉ luồng tick chạm vào, nên không cần đồng bộ gì.
        private readonly Dictionary<(int MapId, int Column), List<PlayerEntity>> _columns = new();
        private readonly List<PlayerEntity> _visibleNow = new();

        public void Tick(float dt)
        {
            // Vòng 0: tiêu thụ lệnh đến từ ngoài (như Phase 9).
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

            // Vòng 1: tích phân TẤT CẢ trước (như Phase 7 — trộn tích phân với gửi thì hai client
            // nhìn cùng một tick ra hai bức tranh khác nhau).
            foreach (PlayerEntity entity in _entities.Values)
                entity.Integrate(dt);

            // Vòng 2: dựng lại chỉ mục cột từ đầu. O(n), và không có trạng thái nào sống qua tick nên
            // không tồn tại lớp bug "chỉ mục lệch thực tế" (quên gỡ cột cũ, entity chết còn nằm lại...).
            _columns.Clear();

            foreach (PlayerEntity entity in _entities.Values)
            {
                (int, int) key = ColumnOf(entity);

                if (!_columns.TryGetValue(key, out List<PlayerEntity> column))
                {
                    column = new List<PlayerEntity>();
                    _columns[key] = column;
                }

                column.Add(entity);
            }

            // Vòng 3: với từng người — tầm nhìn mới, so với tầm nhìn cũ, phát spawn/despawn, gửi trạng thái.
            foreach (PlayerEntity viewer in _entities.Values)
            {
                if (viewer.Owner == null)
                    continue;

                CollectVisible(viewer);

                // (1) Ai mới lọt vào tầm nhìn → giới thiệu họ với viewer.
                foreach (PlayerEntity seen in _visibleNow)
                {
                    if (!viewer.Visible.Contains(seen.EntityId))
                        viewer.Owner.SendData(NetCmd.EntitySpawn, ToSpawnNotice(seen));
                }

                // (2) Ai vừa rời tầm nhìn → báo biến mất. PHẢI làm trước khi ghi đè tập Visible;
                //     đảo thứ tự thì tập cũ mất trước khi kịp so, và không ai despawn bao giờ.
                viewer.Visible.RemoveWhere(id =>
                {
                    bool stillVisible = _visibleNow.Exists(entity => entity.EntityId == id);

                    if (!stillVisible)
                        viewer.Owner.SendData(NetCmd.EntityDespawn, new EntityDespawnNotice { EntityId = id });

                    return !stillVisible;
                });

                // (3) Chốt tập mới.
                foreach (PlayerEntity seen in _visibleNow)
                    viewer.Visible.Add(seen.EntityId);

                // Vị trí của chính mình vẫn đi đường riêng — đường reconciliation, không dính AOI:
                // bạn luôn nhìn thấy chính mình.
                viewer.Owner.SendData(NetCmd.MoveState, new MoveStateResponse
                    {
                        LastInputSeq = viewer.LastInputSeq,
                        State = viewer.State,
                    }
                );

                viewer.Owner.SendData(NetCmd.WorldSnapshot, BuildSnapshot());
            }
        }

        private static (int MapId, int Column) ColumnOf(PlayerEntity entity)
        {
            // Floor chứ không phải cast: toạ độ X âm (nửa trái của map) phải rơi về cột bên trái,
            // không gom hết về cột 0 — cast cắt về phía 0 nên -5 và +5 sẽ cùng ra cột 0.
            return (entity.MapId, (int)MathF.Floor(entity.State.X / AOI_COLUMN_WIDTH));
        }

        /// <summary>
        /// Đổ vào <see cref="_visibleNow"/> mọi entity trong 3 cột quanh viewer, cùng map, trừ chính
        /// viewer.
        ///
        /// Lọc MapId là ranh giới CỨNG: hai người ở hai map khác nhau không bao giờ thấy nhau dù toạ
        /// độ X của họ bằng nhau. Hiện chỉ có một map nên nó chưa lọc gì — nhưng viết bây giờ rẻ hơn
        /// nhiều so với đi tìm lý do người ở hang động nhìn thấy người ở đồng cỏ.
        /// </summary>
        private void CollectVisible(PlayerEntity viewer)
        {
            _visibleNow.Clear();

            (int mapId, int column) = ColumnOf(viewer);

            for (int offset = -1; offset <= 1; offset++)
            {
                if (!_columns.TryGetValue((mapId, column + offset), out List<PlayerEntity> cell))
                    continue;

                foreach (PlayerEntity entity in cell)
                {
                    if (entity.EntityId != viewer.EntityId)
                        _visibleNow.Add(entity);
                }
            }
        }

        /// <summary>Snapshot dựng từ tập vừa gom — không còn duyệt toàn bộ world như Phase 7.</summary>
        private WorldSnapshotNotice BuildSnapshot()
        {
            var states = new EntityState[_visibleNow.Count];

            for (int i = 0; i < _visibleNow.Count; i++)
            {
                PlayerEntity entity = _visibleNow[i];

                states[i] = new EntityState
                {
                    EntityId = entity.EntityId,
                    X = entity.X,
                    Y = entity.Y,
                    FacingLeft = entity.State.FacingLeft,
                    Crouching = entity.State.Crouching,
                    Action = entity.State.Action,
                };
            }

            return new WorldSnapshotNotice { States = states };
        }
```

`ToSpawnNotice` giữ nguyên như Phase 9 — nó đã điền sẵn `FacingLeft` / `Crouching` / `Action`, và bây
giờ mới thấy hết giá trị của việc đó: người hiện ra khi bạn chạy tới gần phải hiện ra **đã đúng tư thế**,
chứ không phải đứng thẳng nhìn sang phải rồi một nhịp sau mới quay đầu.

</details>

### ✅ CHECKPOINT A — mục tiêu cuối Phase 11

1. Hai client vào world cạnh nhau → thấy nhau (như Phase 9, nhưng giờ qua đường tầm nhìn — trễ tối đa
   một tick so với trước, không nhận ra được bằng mắt).
2. Một người chạy xa: tới khoảng 12–24 unit thì người kia **biến mất** khỏi màn hình.
3. Chạy ngược lại → hiện ra lại đúng vị trí, **đúng hướng mặt**, đi tiếp mượt (buffer nội suy được mồi
   lại từ `EntitySpawn`).
4. Đứng gần nhau, một người thoát hẳn (logout hoặc tắt client) → người kia vẫn thấy despawn. Đường cũ
   giờ do diff đảm nhiệm: entity rời sổ → rời `visibleNow` → despawn ở tick kế tiếp.
5. Log tạm kích thước snapshot: đứng cạnh nhau = 1 state, đi xa = **0** state.

Điểm (5) là cả phase gói trong một con số: băng thông tỉ lệ với **mật độ quanh mình**, không phải tổng
người online. Đó là câu trả lời cho "vì sao MMO gánh được nghìn người mà không nổ đường truyền".

---

## Ba thử nghiệm bắt buộc

**1. Nhảy múa ở ranh giới AOI.**
Hai người đứng hai bên một ranh giới cột (`x = 12`, `x = 24`…), một người bước qua-lại quanh ranh giới →
người kia thấy bạn mình **nhấp nháy** hiện/biến, mỗi lần là một cặp gói spawn/despawn và một lần
dựng/huỷ GameObject.

Đây là flicker kinh điển của AOI không có hysteresis (vào và ra dùng **cùng một ngưỡng**). Không sửa ở
phase này — nhưng phải **thấy nó bằng mắt** và trả lời được câu 4 bên dưới.

**2. Đo cái AOI mua được.**
Log tạm tổng số `EntityState` server gửi mỗi giây. Hai client đứng cạnh nhau: ~40/giây (20 tick × 2
người × 1 state). Đi xa nhau: **0**.

Với broadcast của Phase 7 con số này không bao giờ về 0 dù map to cỡ nào — và nó tăng theo **bình phương**
tổng người online, trong khi bản này tăng theo mật độ cục bộ. Cùng một cảnh chơi, hai đường cong khác hẳn.

**3. Client không biết gì cả.**
Mở `git diff` và xác nhận: **không một file nào trong `Assets/` bị sửa** ở phase này. Server thay toàn bộ
logic phát sinh `EntitySpawn`/`EntityDespawn` mà client cũ chạy nguyên.

Đây là thử nghiệm dễ nhất và đáng nhớ nhất. Nếu Phase 7 đã trót đặt tên gói là `PlayerJoinedWorld` /
`PlayerLeftWorld` thì hôm nay cái tên ấy **nói dối** — gói vẫn chạy đúng, nhưng mỗi người đọc code sau
này sẽ hiểu sai một chút, và không có lỗi biên dịch nào báo.

---

## Troubleshooting

| Triệu chứng | Nguyên nhân thường gặp | Chỗ sửa |
|---|---|---|
| Người kia không bao giờ biến mất dù chạy rất xa | còn broadcast của Phase 7 trong `Spawn`, hoặc snapshot vẫn dựng từ toàn bộ `_entities` | `WorldService.Spawn` · `BuildSnapshot` |
| Người kia biến mất rồi không hiện lại | sai thứ tự ba thao tác: phải là báo-mới → báo-đi → cập-nhật-tập | `Tick` vòng 3 |
| Không ai despawn bao giờ | tập `Visible` bị ghi đè trước khi so | `Tick` vòng 3, thao tác (3) đang nằm trên (2) |
| Vào world xong thấy chính mình nhân đôi | `CollectVisible` quên loại `viewer.EntityId` | `CollectVisible` |
| Người ở nửa trái map (X âm) nhìn thấy người ở nửa phải | dùng cast `(int)` thay cho `MathF.Floor` khi tính cột | `ColumnOf` |
| Người ở map khác vẫn nhìn thấy nhau | khoá chỉ mục quên `MapId` | `ColumnOf` |
| Nhấp nháy hiện/biến ở một khoảng cách nhất định | flicker ranh giới AOI — hành vi đã biết, chưa sửa ở phase này | thử nghiệm 1; sửa thật thì cần hysteresis (câu 4) |
| Người hiện ra quay sai hướng rồi một nhịp sau mới quay lại | `ToSpawnNotice` thiếu `FacingLeft`/`Crouching`/`Action` | `WorldService.ToSpawnNotice` |
| GC spike đều đặn 20 lần/giây | cấp phát `Dictionary`/`List` mới mỗi tick thay vì `Clear()` bộ đệm | `WorldService` — hai field `_columns`, `_visibleNow` |

---

## Tự kiểm tra hiểu bài

**Câu 1.** Vì sao AOI chia **cột** theo X mà không chia lưới ô 2D?
<details>
<summary><b>📖 Đáp án câu 1</b></summary>

Vì chỉ nên chia ô ở trục mà thế giới **thật sự lớn**. Map cao ~11 unit trong khi một màn hình đã cao 10
— chia trục Y thành ô 12 unit thì gần như mọi người luôn nằm cùng một hàng, và ta trả thêm một chiều
trong khoá `Dictionary` để nhận về một phép lọc gần như không lọc gì.

Trục X thì ngược lại: map dài ~64 unit và sẽ còn dài ra, còn màn hình chỉ thấy ~18 — chia ở đây lọc được
thật.

Tổng quát: cấu trúc chia không gian phải khớp **hình dạng của thế giới**, không phải khớp thói quen. Một
game top-down map vuông thì lưới 2D là đúng; một game bay trong không gian thì phải là octree.

</details>

**Câu 2.** Server đổi hoàn toàn *nguyên nhân* sinh ra `EntitySpawn`/`EntityDespawn` mà client không phải
sửa gì. Thiết kế nào của Phase 7 mua được điều đó?
<details>
<summary><b>📖 Đáp án câu 2</b></summary>

Client Phase 7 được viết theo **message**, không theo **nguyên nhân**: nó chỉ biết "có gói bảo X xuất
hiện thì dựng X, có gói bảo X biến mất thì dọn X" — không hỏi vì sao. Server thay toàn bộ logic phát
sinh (từ sự kiện vào/ra world sang diff tầm nhìn mỗi tick) mà **contract không đổi**, nên client cũ chạy
nguyên.

Đây là phần thưởng cụ thể của việc tách "điều đã xảy ra" (message) khỏi "vì sao nó xảy ra" (logic
server). Cùng bài học với việc đặt tên: `EntitySpawn` mô tả *điều đã xảy ra với người nhận*, còn
`PlayerJoinedWorld` sẽ mô tả một *nguyên nhân* — và nguyên nhân thì đổi được, còn cái tên thì ở lại.

</details>

**Câu 3.** Vì sao dựng lại chỉ mục cột từ đầu mỗi tick thay vì cập nhật tại chỗ khi có người di chuyển?
<details>
<summary><b>📖 Đáp án câu 3</b></summary>

Vì bản cập-nhật-tại-chỗ phải đúng ở **mọi** đường vào và ra: spawn, despawn, mất kết nối, chuyển map, và
mỗi lần ai đó bước qua ranh giới cột. Quên một đường thôi là chỉ mục lệch thực tế — mà triệu chứng của
chỉ mục lệch không phải là exception, nó là "thỉnh thoảng có người vô hình", loại lỗi không tái hiện
được theo yêu cầu.

Dựng lại từ đầu là O(n) mỗi tick và **không có trạng thái nào sống qua tick**, nên cả lớp bug đó không
tồn tại — không phải "ít xảy ra hơn" mà là *không diễn đạt được*, cùng một cách nghĩ với việc bỏ `Hurt`
khỏi enum client gửi ở Phase 9.

Đổi sang cập nhật tại chỗ khi nào? Khi profiler chỉ đúng vào vòng dựng chỉ mục — chứ không phải khi bạn
đoán rằng nó chậm.

</details>

**Câu 4.** Flicker ở ranh giới AOI (thử nghiệm 1): nguyên nhân chính xác là gì, và hysteresis sửa nó thế
nào? Cái giá phải trả là gì?
<details>
<summary><b>📖 Đáp án câu 4</b></summary>

Vào và ra tầm nhìn dùng **cùng một ngưỡng** (ranh giới cột), nên người đứng ngay ranh giới chỉ cần dao
động vài centimet là đổi trạng thái — mỗi lần đổi là một cặp gói spawn/despawn và một lần dựng/huỷ
GameObject.

Hysteresis tách hai ngưỡng: **vào** tầm nhìn ở phạm vi hẹp (3 cột), chỉ **ra** khi vượt phạm vi rộng hơn
(5 cột). Người ở giữa hai ngưỡng **giữ nguyên trạng thái hiện có**, nên dao động nhỏ quanh một điểm
không đổi được trạng thái nữa.

Giá phải trả: tầm "ra" rộng hơn tầm "vào" một vành đai, tức là giữ đồng bộ thêm vài người mà lẽ ra đã bỏ
được — đổi băng thông lấy sự ổn định. Và code phức tạp hơn: `CollectVisible` phải trả lời hai câu hỏi
khác nhau thay vì một, tuỳ người đó đã ở trong `Visible` hay chưa.

</details>

**Câu 5.** Một người chơi mất kết nối đột ngột (rút mạng). Ai gửi `EntityDespawn` cho những người đang
nhìn thấy họ, và ở thời điểm nào?
<details>
<summary><b>📖 Đáp án câu 5</b></summary>

Không ai gửi *vì* họ mất kết nối — không còn đoạn code nào làm việc đó. Chuỗi thật là: session chết →
`LeaveWorldAsync` → `WorldService.Despawn` gỡ entity khỏi `_entities` → **tick kế tiếp** chỉ mục cột
dựng lại không có họ → mọi viewer thấy họ biến khỏi `visibleNow` → diff phát `EntityDespawn`.

Trễ tối đa một tick (50ms), và đi qua **đúng một đường** với mọi lý do biến mất khác. Đó chính là điều
đáng giá: trước phase này có hai đường (broadcast lúc despawn, và không có gì cho chuyện đi xa); giờ có
một. Ít đường thì ít chỗ để quên.

</details>

---

## Để dành (ghi lại, chưa làm)

- **Hysteresis** (câu 4). Rẻ, và nên làm ngay khi nào flicker bắt đầu gây khó chịu thật.
- **AOI cho entity không phải người chơi.** Quái và projectile của Phase 15 cũng phải đi qua đúng cơ chế
  này. Lúc đó `_columns` chứa `Entity` chứ không phải `PlayerEntity`, và đó là dịp để tách một lớp cơ sở
  — đừng tách trước, sẽ tách nhầm chỗ.
- **Tầm nhìn khác nhau theo tình huống.** Đang trong hang thì thấy gần hơn; dùng kính viễn vọng thì thấy
  xa hơn. Cấu trúc hiện tại chịu được: bán kính đang là hằng, đổi thành một con số trong `PlayerEntity`
  là xong — nhưng chỉ mục cột thì không đổi.
- **Gửi delta thay vì snapshot đầy đủ.** Snapshot hiện gửi cả 5 trường mỗi tick cho mỗi người trong tầm.
  Phần lớn không đổi giữa hai tick. Đây là cùng bài toán với "delta vs snapshot" của Phase 13 (túi đồ),
  và nên làm sau khi đã gặp nó ở đó.
- **Ưu tiên theo khoảng cách.** Người ở xa không cần cập nhật 20 lần/giây. Chia tần suất theo cột (cột
  giữa 20Hz, hai cột bên 10Hz) là một dòng, và là bước đầu tiên của mọi hệ thống "interest management"
  nghiêm túc.

---

**Xong Phase 11 → thế giới sống: có hình dạng, có tầm nhìn, băng thông theo mật độ.**
[PHASE-12](PHASE-12.md) trả nốt món nợ rải khắp năm phase vừa qua: `GRAVITY`, `MAX_FALL_SPEED`,
`AOI_COLUMN_WIDTH`, cả bảng `CharacterProfiles` — tất cả đang là hằng số nằm cứng trong code. Đưa chúng
ra dữ liệu, sửa không cần build lại, và phân biệt cho rõ hai loại config khác nhau về bản chất — trong
đó **file map của Phase 10 chính là ví dụ loại B đầu tiên**, và phép so checksum bằng mắt sẽ thành phép
kiểm version bằng máy lúc login.
