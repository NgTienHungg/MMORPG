# PHASE 11 — AOI: chỉ thấy người ở gần

> **Kết quả cuối Phase 11:** hai người chơi chạy xa nhau thì **biến mất** khỏi màn hình của nhau, chạy
> lại gần thì hiện ra — đúng vị trí, đúng hướng mặt, đi tiếp mượt. Băng thông server gửi cho mỗi người
> tỉ lệ với **mật độ quanh họ**, không phải với tổng số người online. Và bước vào một **cổng** thì sang
> hẳn map khác: hình đổi, lưới va chạm đổi, mọi người ở map cũ biến mất — mà không có một dòng code nào
> viết riêng cho việc "báo tin sang map".
>
> **Điều kiện:** xong [`PHASE-10.md`](PHASE-10.md) tới CHECKPOINT C — map có hình dạng thật, hai bên
> chạy đúng một lưới va chạm, `MapRegistry` đã nạp mọi map lúc khởi động.
>
> **Bài học chính:** (1) chia không gian phải khớp **hình dạng của thế giới**, không khớp thói quen;
> (2) `EntitySpawn`/`EntityDespawn` đổi từ *"sự kiện vào/ra world"* thành *"hệ quả của tầm nhìn"* mà
> contract không đổi một chữ — đó là phần thưởng cụ thể của việc Phase 7 đặt tên gói theo **điều đã xảy
> ra** chứ không theo **nguyên nhân**; (3) một cơ chế đủ tổng quát thì **tính năng sau nó gần như miễn
> phí** — chuyển map là ví dụ, và Bước 2 tồn tại để bạn thấy tận mắt điều đó.

Format như trước: **hướng làm** hiện sẵn, **📖 Lời giải** trong foldout.

> 📌 Phase này **tách ra từ Phase 10 cũ** (2026-08-24), đúng theo luật rút ra từ Phase 9: việc nào tự
> nó test được thì tách thành phase riêng. Map và AOI chẳng liên quan gì nhau ngoài chữ "không gian".
>
> 📌 **Thêm Bước 2 "chuyển map"** (2026-09-10), sau khi Phase 10 làm xong. Nó nằm ở đây chứ không ở
> Phase 10 vì một lý do kỹ thuật chứ không phải vì hết chỗ: **làm chuyển map TRƯỚC AOI thì đắt, làm
> SAU thì gần như miễn phí.** Trước AOI, mỗi lần ai đó sang map khác bạn phải tự tay gửi `EntityDespawn`
> cho mọi người ở map cũ, `EntitySpawn` cho mọi người ở map mới, rồi gửi ngược lại danh sách map mới
> cho người vừa sang — ba vòng lặp viết tay, ba chỗ để quên. Sau AOI, khoá chỉ mục đã là `(MapId, Cột)`,
> nên chỉ cần **đổi `MapId` của entity** là toàn bộ việc báo tin do phép diff tầm nhìn lo, đúng cái
> phép diff vẫn chạy mỗi tick. Đó là phần thưởng thật của một cơ chế đủ tổng quát, và là lý do thứ tự
> hai bước trong phase này không đảo được.

---

## Một câu hỏi khác hẳn

Phase 10 trả lời *"đi được chỗ nào"*. Phase này trả lời *"thấy được những ai"*. Hai câu hỏi không gian,
hai lưới khác nhau, và đó là chuyện bình thường:

| | Lưới va chạm (Phase 10) | Lưới tầm nhìn (phase này) |
|---|---|---|
| Ô rộng | 1 unit | **24 unit** (= bán kính tầm nhìn) |
| Vì sao | tường mỏng, phải mịn | màn hình rộng, thô là đủ |
| Chia mấy trục | X và Y | **chỉ X** |
| Sống ở đâu | `Shared` — cả hai bên đọc | **chỉ server** — client không cần biết nó tồn tại |

Dòng cuối đáng dừng lại: AOI là chuyện **hoàn toàn của server**. Client chỉ nhận gói "có người xuất
hiện" / "có người biến mất" và làm đúng như từ Phase 7 tới giờ. Hết Bước 1: không `NetCmd` mới, không
DTO mới, không một dòng nào ở `Assets/`.

Bước 2 (chuyển map) thì có sửa client — nhưng để ý nó sửa **cái gì**: nạp map, dựng hình, đặt lại motor.
Việc "ai phải biến mất khỏi màn hình ai" vẫn hoàn toàn là chuyện của server, đi đúng đường
`EntitySpawn`/`EntityDespawn` mà Bước 1 vừa dựng. Ranh giới ấy giữ nguyên suốt cả phase.

### Vì sao chia cột theo X, không chia lưới ô 2D

Cách quen thuộc (và cách một game top-down phải làm) là chia lưới 2D rồi tra 9 ô quanh mình. Ở
side-scroller thì đó là **trả tiền cho một chiều không dùng**:

| | Bề ngang map | Bề cao map | Màn hình thấy |
|---|---|---|---|
| Kích thước | ~64 unit (và sẽ còn dài ra) | ~11 unit | **32 × 18 unit** (ortho size 9, 16:9) |

Map cao 11 unit mà một màn hình đã cao 18 — chia trục Y thành ô 12 unit thì **gần như mọi người luôn ở
cùng một hàng**, và ta trả thêm một chiều trong khoá `Dictionary` để nhận về một phép lọc gần như không
lọc gì.

> Chỉ chia ô ở **trục mà thế giới thật sự lớn**. Với side-scroller, đó là trục X — và chỉ trục X.

#### Con số duy nhất phải tính đúng: bán kính

`Camera.orthographicSize` là **nửa bề CAO**, không phải nửa bề rộng. Nửa bề RỘNG bằng
`orthographicSize × tỉ lệ khung hình`:

| Ortho size | Tỉ lệ | Nửa bề rộng = bán kính tối thiểu |
|---|---|---|
| 9 | 16:9 | **16 unit** |
| 9 | 21:9 | **21 unit** |

Lấy nhầm con số 9 ở đây thì bán kính tầm nhìn nhỏ hơn cái màn hình đang thấy, và triệu chứng là người
chơi **biến mất khi vẫn còn nằm giữa khung hình** — đúng loại lỗi không ai nghi ngờ vào AOI, vì "đi ra
xa thì mất" nghe rất hợp lý cho tới lúc bạn đo bằng thước.

Nên `AOI_RADIUS_X = 24f`: phủ tới 21:9 và còn dư một quãng đệm, nhờ đó người khác được dựng lên **trước
khi** trôi vào mép màn hình.

#### Hai tầng lọc, và vì sao tầng nào cũng cần

Bản thân lưới cột cho một tầm nhìn **lệch**: viewer ở cột `cx` thấy hết `[cx-1, cx+1]`, tức là nếu đang
đứng sát mép TRÁI của cột mình thì thấy xa 24 unit về bên trái nhưng tới 48 unit về bên phải. Một cái
hộp co giãn theo chỗ đứng.

Cho nên cột là **lọc thô** (khỏi duyệt cả world), còn phép so `|Δx| ≤ AOI_RADIUS_X` mới là **lọc thật**
(cắt ra một hình chữ nhật cân). Chọn `AOI_COLUMN_WIDTH = AOI_RADIUS_X` để 3 cột chắc chắn phủ hết bán
kính, rồi để phép so khoảng cách làm phần còn lại. Bỏ tầng lọc thật đi thì lỗi nhìn như thế này:

> Hai người đứng cạnh nhau. B đi sang **phải** rất xa — A vẫn thấy B. B quay lại đi sang **trái** một
> đoạn ngắn — A mất B ngay giữa màn hình. Cùng một khoảng cách, hai kết quả, chỉ vì A tình cờ đứng gần
> mép nào của cột.

Đây là đánh đổi kinh điển của mọi spatial grid, và cách giải cũng kinh điển: **broad phase bằng lưới,
narrow phase bằng khoảng cách**. Cùng một khuôn với va chạm vật lý.

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

**`MapId` chuyển từ phép lọc rời rạc thành một nửa của khoá chỉ mục.** Cuối Phase 10, `Spawn` và
`BuildSnapshotFor` đã có ba phép `if (other.MapId != viewer.MapId) continue;` rải ở ba chỗ. Bước này
xoá cả ba — không phải vì chúng sai, mà vì `(MapId, Cột)` làm đúng việc đó **một lần, ở một chỗ**, và
map khác nhau thì tự nhiên rơi vào ô chỉ mục khác nhau. Ba phép lọc rời rạc là ba chỗ để quên; một
khoá thì không có chỗ nào để quên.

Đó cũng chính là thứ khiến Bước 2 gần như miễn phí: đổi `MapId` của một entity là đổi khoá của nó, và
mọi hệ quả tự chảy ra từ phép diff.

**Bốn việc trong `WorldService`:**

1. **Xoá mọi đường thông báo trực tiếp trong `Spawn`/`Despawn`**: vòng "gửi danh sách người đang có mặt
   cho người mới", hai lời gọi `Broadcast(NetCmd.EntitySpawn/EntityDespawn, …)`, và helper `Broadcast<T>`
   (kể cả tham số `mapId` vừa thêm cuối Phase 10 — nó chết cùng với hàm). Từ giờ **mọi** thông báo
   xuất hiện/biến mất do vòng tick phát ra. Cần lại thì git history còn.
2. **`PlayerEntity` thêm `HashSet<int> Visible`** — tập entityId đang trong tầm nhìn của người này. Chỉ
   luồng tick đọc/ghi, ghi comment ranh giới luồng như đã làm với input ở Phase 8.
3. **Thêm pha dựng chỉ mục cột** vào `Tick`, sau pha tích phân: `Dictionary<(int MapId, int Column), List<PlayerEntity>>`,
   **dựng lại từ đầu mỗi tick**.
4. **Pha gửi đổi thành pha so**: với từng người → gom mọi entity trong 3 cột quanh mình → so với `Visible`
   → ai mới thì `EntitySpawn`, ai mất thì `EntityDespawn` → snapshot **chỉ chứa** những người trong tầm.
   `BuildSnapshotFor(viewer)` đổi tên thành `BuildSnapshot()` **không tham số**: nó không còn duyệt
   `_entities` và tự lọc nữa, nó dựng thẳng từ tập vừa gom. Đổi tên vì việc nó làm đã đổi.

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
            int entityId = Interlocked.Increment(ref _nextEntityId);
            MapGrid map = _maps.ResolveFor(row);
            var entity = new PlayerEntity(entityId, row, owner, map);

            _entities[entityId] = entity;
            _entityIdByAccount[entity.AccountId] = entity.EntityId;

            Log.Info($"Spawn {entity.Name.Cyan()} entity {entityId.ToString().Green()} " +
                     $"tại map {entity.MapId} ({entity.X:0.##}, {entity.Y:0.##}) — {OnlineCount} người trong world");

            // Không thông báo gì ở đây nữa. Ai thấy được người này thì tick kế tiếp sẽ tự phát hiện —
            // "vừa vào world" chỉ là MỘT cách để lọt vào tầm nhìn ai đó, không phải cách duy nhất.
            return entity;
```

`Despawn` cũng chỉ còn gỡ sổ và log — không gửi gì.

Thêm hằng, ba bộ đệm dùng lại, và `Tick` mới:

```csharp
        /// <summary>
        /// Bán kính tầm nhìn theo trục X, world unit. Phải lớn hơn nửa bề RỘNG màn hình: camera
        /// orthographic size 9 cho nửa bề CAO là 9, còn nửa bề RỘNG = 9 × tỉ lệ khung hình — 16 unit
        /// ở 16:9, 21 unit ở 21:9. Lấy nhầm con số 9 thì người chơi biến mất trong khi vẫn còn nằm
        /// giữa khung hình.
        ///
        /// 24 phủ tới tận 21:9 và còn dư một quãng đệm, nhờ đó người khác được dựng lên TRƯỚC khi
        /// trôi vào mép màn hình — hiện ra là đã ở đúng chỗ, không đột ngột nhảy vào giữa hình.
        ///
        /// Chỉ chặn theo trục X: map cao ~11 unit mà một màn hình đã cao 18, nên chặn cả trục Y là
        /// tốn thêm một phép so để nhận về một phép lọc gần như không lọc gì.
        /// </summary>
        private const float AOI_RADIUS_X = 24f;

        /// <summary>
        /// Bề ngang một cột chỉ mục, CỐ Ý bằng đúng bán kính tầm nhìn: khi đó 3 cột (cx-1, cx, cx+1)
        /// chắc chắn chứa mọi người trong bán kính, dù viewer đứng chỗ nào trong cột của mình.
        ///
        /// Cột chỉ là phép lọc THÔ để khỏi duyệt cả world; phép lọc THẬT là khoảng cách trong
        /// <see cref="CollectVisible"/>. Tự thân lưới cột cho một hình chữ nhật LỆCH — đứng sát mép
        /// trái một cột thì thấy xa 24 unit về bên trái nhưng tới 48 unit về bên phải — nên bỏ phép
        /// so khoảng cách là tầm nhìn đổi theo chỗ đứng, với triệu chứng "đi sang phải mãi không ai
        /// biến mất, đi sang trái một đoạn ngắn đã mất".
        /// </summary>
        private const float AOI_COLUMN_WIDTH = AOI_RADIUS_X;

        // Ba bộ đệm của vòng tick, giữ làm field và Clear() mỗi lần dùng. Cấp phát mới mỗi tick là
        // rác GC đều đặn 20 lần/giây suốt đời server — thứ chạy mỗi tick thì hình dạng bộ nhớ của nó
        // là một phần thiết kế. Chỉ luồng tick chạm vào, nên không cần đồng bộ gì.
        private readonly Dictionary<(int MapId, int Column), List<PlayerEntity>> _columns = new();
        private readonly List<PlayerEntity> _visibleNow = new();

        // Cùng nội dung với _visibleNow nhưng chỉ id, để phép "ai vừa rời tầm nhìn" hỏi trong O(1).
        // Không có nó thì vòng RemoveWhere phải quét cả _visibleNow cho MỖI id cũ — O(n·m) mỗi người
        // mỗi tick, tức O(n²·m) cho cả server, và đó đúng là con số AOI sinh ra để giết.
        private readonly HashSet<int> _visibleIds = new();

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
                    if (_visibleIds.Contains(id))
                        return false;

                    viewer.Owner.SendData(NetCmd.EntityDespawn, new EntityDespawnNotice { EntityId = id });

                    return true;
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
        /// Đổ vào <see cref="_visibleNow"/> mọi entity cùng map, cách viewer không quá
        /// <see cref="AOI_RADIUS_X"/> theo trục X, trừ chính viewer.
        ///
        /// Hai tầng lọc, và tầng nào cũng cần: 3 cột quanh viewer thu phạm vi phải duyệt từ "cả
        /// world" xuống "vài người quanh đây", rồi phép so khoảng cách cắt ra đúng một hình chữ nhật
        /// CÂN — không có nó thì tầm nhìn rộng hẹp tuỳ chỗ viewer đứng trong cột.
        ///
        /// Lọc MapId là ranh giới CỨNG: hai người ở hai map khác nhau không bao giờ thấy nhau dù toạ
        /// độ X của họ bằng nhau — và nó miễn phí vì MapId đã là một nửa khoá của chỉ mục.
        /// </summary>
        private void CollectVisible(PlayerEntity viewer)
        {
            _visibleNow.Clear();
            _visibleIds.Clear();

            (int mapId, int column) = ColumnOf(viewer);
            float viewerX = viewer.State.X;

            for (int offset = -1; offset <= 1; offset++)
            {
                if (!_columns.TryGetValue((mapId, column + offset), out List<PlayerEntity> cell))
                    continue;

                foreach (PlayerEntity entity in cell)
                {
                    if (entity.EntityId == viewer.EntityId)
                        continue;

                    // Phép lọc thật. Cùng một ngưỡng cho cả chiều vào lẫn chiều ra, nên người đứng
                    // đúng mốc 24 unit sẽ nhấp nháy hiện/biến — xem ghi chú hysteresis ở cuối tài
                    // liệu Phase 11. Chấp nhận được vì mốc ấy nằm ngoài khung hình.
                    if (MathF.Abs(entity.State.X - viewerX) > AOI_RADIUS_X)
                        continue;

                    _visibleNow.Add(entity);
                    _visibleIds.Add(entity.EntityId);
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

### ✅ CHECKPOINT A — tầm nhìn

1. Hai client vào world cạnh nhau → thấy nhau (như Phase 9, nhưng giờ qua đường tầm nhìn — trễ tối đa
   một tick so với trước, không nhận ra được bằng mắt).
2. Một người chạy xa: tới đúng **24 unit** thì người kia biến mất — và vì con số đó lớn hơn nửa bề rộng
   màn hình (16 unit ở 16:9) nên lúc biến mất họ đã **ra khỏi khung hình từ trước**. Thử cả hai chiều
   trái và phải: khoảng cách biến mất phải **như nhau**. Lệch hai bên là dấu hiệu phép so khoảng cách
   bị bỏ, và tầm nhìn đang là hình chữ nhật méo của lưới cột.
3. Chạy ngược lại → hiện ra lại đúng vị trí, **đúng hướng mặt**, đi tiếp mượt (buffer nội suy được mồi
   lại từ `EntitySpawn`).
4. Đứng gần nhau, một người thoát hẳn (logout hoặc tắt client) → người kia vẫn thấy despawn. Đường cũ
   giờ do diff đảm nhiệm: entity rời sổ → rời `visibleNow` → despawn ở tick kế tiếp.
5. Log tạm kích thước snapshot: đứng cạnh nhau = 1 state, đi xa = **0** state.

Điểm (5) là cả Bước 1 gói trong một con số: băng thông tỉ lệ với **mật độ quanh mình**, không phải tổng
người online. Đó là câu trả lời cho "vì sao MMO gánh được nghìn người mà không nổ đường truyền".

---

## Bước 2 — Chuyển map: đổi một con số, để cơ chế lo phần còn lại

### Hướng làm

Bước này có một tính chất hiếm gặp: **phần khó nhất đã xong rồi mà bạn không phải viết gì.** Sau Bước 1,
khoá chỉ mục là `(MapId, Cột)`. Đổi `MapId` của một entity là chuyển nó sang một ô chỉ mục khác, và ngay
tick kế tiếp phép diff tự sinh ra **cả bốn chiều** thông báo:

| Ai | Thấy gì | Do đâu |
|---|---|---|
| Người ở map cũ | người vừa đi **biến mất** | họ rơi khỏi `visibleNow` → `EntityDespawn` |
| Người đi | **mọi người map cũ biến mất** | `CollectVisible` trả về ô của map mới → cả tập `Visible` cũ rơi ra |
| Người đi | **mọi người map mới hiện ra** | cùng phép so đó, chiều ngược lại |
| Người ở map mới | người mới **hiện ra** | họ lọt vào `visibleNow` |

Bốn dòng ấy là bốn vòng lặp bạn sẽ phải viết tay nếu làm chuyển map trước AOI. Ở đây tổng cộng: **không
dòng nào**. Việc còn lại chỉ là ba thứ mà cơ chế không thể tự đoán — *lấy gì làm cổng*, *đặt người ta
xuống đâu*, và *nói cho client biết để nó đổi hình*.

**Cổng là dữ liệu của map, nên nó nằm trong file map.** Đây là lần trả tiền đầu tiên của quyết định chọn
JSON ở Phase 10, và trả đúng như đã hứa: thêm `Portals` là **thêm một trường tuỳ chọn** →
`FORMAT_VERSION` **không tăng**, file cũ chưa có cổng vẫn đọc được (trường thiếu → không có cổng nào),
code cũ chưa biết cổng vẫn đọc được file mới (trường lạ → bỏ qua). Không có bước migrate, không phải
export lại map nào.

Một cổng gồm sáu số: một hình chữ nhật world (`X`, `Y` là **tâm**, `Width`, `Height`), map đích
(`ToMapId`), và **tên** điểm đến (`ToSpawnId`). Trường cuối chính là lý do Phase 10 làm `spawns` thành
một *danh sách có id* thay vì một điểm — ba dòng thừa lúc đó, hôm nay thành thứ khiến cổng trỏ được vào
đúng chỗ mà không phải gõ toạ độ của map khác vào file map này.

**Cổng KHÔNG vào `Checksum()`.** Dấu vân tay ấy trả lời đúng một câu: *"hai bên có cùng hình dạng va chạm
không"*. Cổng thì chỉ server đọc — client không dự đoán chuyển map (xem dưới) — nên nó cùng loại với
`PrefabKey`: đi trong file, không vào vân tay.

**Server quyết, client KHÔNG dự đoán.** Đây là ngoại lệ có chủ đích với nguyên tắc "client dự đoán mọi
thứ" của Phase 8:

> Dự đoán được thứ gì là dự đoán được **hệ quả** của nó. Client dự đoán một cú nhảy thì hệ quả nằm gọn
> trong `MoveState`, và sai thì reconciliation kéo về trong 50ms. Client dự đoán một cú chuyển map thì
> hệ quả là **nạp một map chưa nạp, huỷ mọi entity đang thấy, dựng lại toàn bộ hình** — sai một lần là
> người chơi thấy mình teleport nhầm map rồi bị giật ngược. Cái giá của việc đoán sai quyết định việc
> **có nên** đoán hay không, chứ không phải việc đoán có khả thi hay không.

Hệ quả cụ thể trong code: cờ chống lặp cổng **không** vào `MoveState`, khác hẳn `DropThroughTicks` của
Phase 10. `DropThroughTicks` phải vào vì vòng replay bên client cần tái hiện nó; cái này thì client
chẳng bao giờ chạy tới, nên nó là field thường của `PlayerEntity`. Đó là phép thử để biết một trạng thái
có thuộc về contract hay không: **client có phải mô phỏng lại nó không?**

**Cạnh, không phải mức.** Điều kiện kích cổng là *vừa BƯỚC VÀO*, không phải *đang ĐỨNG TRONG*. Thiếu
phân biệt này thì điểm đến nằm trong cổng chiều ngược lại là hai map ném người chơi qua lại 20 lần mỗi
giây. Cùng một phân biệt "cạnh vs giữ" đã dùng cho nút nhảy ở Phase 8 — lần này ở phía server, và lần
này quên thì không phải mất một cú nhảy mà là treo hẳn client.

Cách rẻ nhất và không có số ma: một cờ `_portalArmed`. Ra khỏi mọi cổng → bật; kích cổng → tắt; vừa tới
map mới → tắt. Không cần hằng cooldown nào, và người chơi xuất hiện ngay giữa cổng về cũng không sao.

**Chỗ đặt phép kiểm cổng: giữa pha tích phân và pha dựng chỉ mục.** Sau tích phân vì vị trí phải chốt
xong mới biết có bước vào cổng không; trước dựng chỉ mục vì chính tick này chỉ mục phải thấy họ **đã ở
map mới** — nhờ vậy phép diff làm việc ngay, không trễ thêm một nhịp.

**Bốn việc bên client, và không việc nào là logic:**

1. `MapService.Load(mapId)` — đã viết ở Phase 10, đã có cache theo id.
2. `MapView.Show(map)` — đã viết ở Phase 10, đã tự huỷ map cũ.
3. `PlayerMotor.SetMap(...)` — mới, và là chỗ duy nhất có gì đó để nghĩ.
4. `CameraFollow.SnapToTarget()` — mới, một dòng, và quên thì rất dễ nhận ra: `SmoothDamp` coi cú nhảy
   sang map như một cú chạy, nên camera **lướt qua cả bản đồ** mất nửa giây trước khi dừng đúng chỗ.
   Cùng một hàm đó cũng nên gọi lúc vào world, vì lý do y hệt.

**Đừng gọi `DespawnAllRemotes()` khi sang map.** Cám dỗ rất lớn, và nó **sai về thiết kế**: server đã gửi
`EntityDespawn` cho từng người ở map cũ ngay tick sau. Dọn tay ở client là đường thứ hai làm cùng một
việc, và hai đường thì sớm muộn lệch nhau. Chịu một tick (50ms) ma đứng im, đổi lại **một đường duy
nhất** cho mọi lý do biến mất — đúng bài học của cả phase này.

**Cuộc đua duy nhất phải xử lý: gói ack đến muộn.** Client gửi input liên tục; server ack bằng
`MoveState` mang `LastInputSeq`. Ngay lúc chuyển map, những gói ack cho input **của map cũ** vẫn đang
trên đường. Nuốt một gói như thế sau khi đã `SetMap` là bị kéo ngược về toạ độ map cũ, một nhịp sau khi
đã sang map mới.

Chặn bằng một **mốc seq**: ghi lại `_nextSeq` tại lúc đổi map, và bỏ qua mọi `MoveState` ack một input
cũ hơn mốc đó. Tốn 0 byte đường truyền.

> Cách khác: nhét `MapId` vào `MoveStateResponse` rồi so thẳng. Rõ ràng hơn hẳn, đổi lại 4 byte × 20
> lần/giây × mỗi người, trên đúng gói nóng nhất của cả hệ thống. Ở quy mô này cả hai đều đúng — nhưng
> phải **biết mình đang chọn gì**: một bên trả bằng băng thông, một bên trả bằng một bất biến phải nhớ
> ("seq chỉ tăng, không bao giờ reset").

<details>
<summary><b>📖 Lời giải — <code>Shared</code>: cổng trong file map</b></summary>

**`Server/Shared/World/MapFileData.cs`** — thêm một property vào `MapFileData` và một lớp mới:

```csharp
        /// <summary>
        /// Cổng sang map khác. Trường TUỲ CHỌN: map chưa nối đi đâu thì file thiếu hẳn trường này, và
        /// đó là lý do FORMAT_VERSION không phải tăng khi thêm nó.
        /// </summary>
        public List<Portal>? Portals { get; set; }
```

```csharp
    /// <summary>
    /// Một vùng chữ nhật world mà BƯỚC VÀO là sang map khác. (X, Y) là TÂM của vùng.
    ///
    /// Điểm đến ghi bằng TÊN chứ không phải toạ độ: file map này không được phép biết toạ độ bên trong
    /// map kia — vẽ lại map kia là mọi cổng trỏ tới nó phải sửa theo, mà không có gì nhắc.
    /// </summary>
    public sealed class Portal
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }

        public int ToMapId { get; set; }

        /// <summary>Id điểm spawn ở map đích. Không có điểm nào mang tên này thì về điểm mặc định.</summary>
        public string ToSpawnId { get; set; } = string.Empty;
    }
```

Không có hậu tố `FileData` — cùng lý do với `SpawnPoint` và `CellPoint`: đây là những mẩu dữ liệu **đi
thẳng vào kiểu chạy trong game**, không phải bản đối chiếu của cả file.

**`Server/Shared/World/MapGrid.cs`** — mang theo danh sách cổng và hai phép tra:

```csharp
        public IReadOnlyList<Portal> Portals => _portals;

        private readonly Portal[] _portals;
```

Hàm dựng nhận thêm một tham số **ngay sau `spawns`**, và cho phép `null`:

```csharp
        public MapGrid(int mapId, string name, string prefabKey, int originX, int originY,
            int width, int height, IReadOnlyList<SpawnPoint> spawns, IReadOnlyList<Portal>? portals,
            CellType[] cells)
        {
            // ... ba phép kiểm và toàn bộ phần gán cũ giữ nguyên, kể cả vòng copy _spawns ...

            // null nghĩa là "map không có cổng nào" — hợp lệ và là trường hợp thường gặp, khác hẳn
            // spawns rỗng (đã bị chặn ở trên) vì map không có chỗ đứng thì không chơi được.
            _portals = new Portal[portals?.Count ?? 0];

            for (int i = 0; i < _portals.Length; i++)
                _portals[i] = portals[i];

            // Giữ nguyên ở CUỐI: DefaultSpawn vẫn phải chốt sau khi _spawns đã copy xong.
            DefaultSpawn = FindDefaultSpawn(_spawns);
        }
```

```csharp
        /// <summary>
        /// Điểm spawn mang tên này; không có thì về điểm mặc định.
        ///
        /// Lùi về mặc định chứ không ném: một cổng trỏ sai tên là lỗi dữ liệu đáng sửa, nhưng ném ở đây
        /// thì người chơi kẹt lại giữa hai map và không có đường nào ra.
        /// </summary>
        public SpawnPoint FindSpawn(string id)
        {
            for (int i = 0; i < _spawns.Length; i++)
            {
                if (_spawns[i].Id == id)
                    return _spawns[i];
            }

            return DefaultSpawn;
        }

        /// <summary>Cổng chứa điểm world này, hoặc null. Số cổng mỗi map đếm trên đầu ngón tay nên quét thẳng.</summary>
        public Portal PortalAt(float x, float y)
        {
            for (int i = 0; i < _portals.Length; i++)
            {
                Portal portal = _portals[i];

                float halfWidth = portal.Width * 0.5f;
                float halfHeight = portal.Height * 0.5f;

                if (x >= portal.X - halfWidth && x <= portal.X + halfWidth &&
                    y >= portal.Y - halfHeight && y <= portal.Y + halfHeight)
                {
                    return portal;
                }
            }

            return null;
        }
```

`Checksum()` **không đổi** — cổng không vào vân tay, cùng lý do với `PrefabKey`: vân tay canh hình dạng
va chạm, mà cổng thì không phải hình dạng va chạm.

Điểm được kiểm là `(State.X, State.Y)` — tức **bàn chân** nhân vật, không phải tâm thân. Hệ quả phải biết
khi vẽ cổng: **vẽ vùng cổng từ mặt đất lên**, đừng vẽ lơ lửng ngang tầm ngực.

**`Server/Shared/World/MapFile.cs`** — `Parse` chuyền thẳng, `Write` bỏ qua khi rỗng:

```csharp
            return new MapGrid(definition.Id, definition.Name, definition.PrefabKey,
                origin.X, origin.Y, width, height, spawns, definition.Portals, cells);
```

```csharp
                Spawns = new List<SpawnPoint>(map.Spawns),

                // Map không có cổng thì file KHÔNG có trường Portals, chứ không phải có mà rỗng:
                // NullValueHandling.Ignore ở Settings lo phần đó. Một mảng rỗng nằm trong file là một
                // câu hỏi thừa cho người mở file ra đọc.
                Portals = map.Portals.Count > 0 ? new List<Portal>(map.Portals) : null,
```

và thêm một dòng vào `Settings`:

```csharp
            // Trường null thì không ghi ra. Áp đúng cho các trường TUỲ CHỌN — mọi trường bắt buộc đều
            // đã có giá trị lúc Write, nên dòng này không giấu được thứ gì đáng lẽ phải hiện.
            NullValueHandling = NullValueHandling.Ignore,
```

`FORMAT_VERSION` **giữ nguyên `2`**. Đây là lần đầu luật version ở Phase 10 được dùng đúng chiều thuận:
thêm trường tuỳ chọn → không tăng số.

⚠️ `MapFileTests.BuildSample` gọi hàm dựng `MapGrid` nên nó **đỏ ngay** sau thay đổi này — truyền
`portals: null` là xong. Thêm một bài thứ sáu: dựng map có một cổng → `Write` → `Parse` → so lại
`ToMapId` và `ToSpawnId`. Round-trip của Phase 10 không kiểm cổng, nên không có bài này thì `Portals`
là trường duy nhất trong file không ai canh.

</details>

<details>
<summary><b>📖 Lời giải — Unity: vẽ cổng và export</b></summary>

**`Assets/Game/Scripts/World/MapCollisionSource.cs`** — khai báo cổng cạnh danh sách spawn:

```csharp
        /// <summary>Một cổng đặt bằng tay trong Scene. Size là world unit, tâm ở Transform.</summary>
        [Serializable]
        public struct PortalMarker
        {
            public Transform Point;
            public Vector2 Size;
            public int ToMapId;
            public string ToSpawnId;
        }

        [Header("Cổng sang map khác — để trống nếu map này chưa nối đi đâu")]
        [SerializeField] private List<PortalMarker> _portals = new();

        public IReadOnlyList<PortalMarker> Portals => _portals;
```

Thêm `OnDrawGizmos` để nhìn thấy cổng lúc vẽ — cổng là thứ duy nhất trong map **không có hình**, nên
không vẽ gizmo thì nó vô hình đúng nghĩa đen:

```csharp
        // Cổng không có sprite, không có tile — không vẽ ra thì bạn đặt nó bằng trí tưởng tượng.
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.35f);

            foreach (PortalMarker marker in _portals)
            {
                if (marker.Point == null)
                    continue;

                Gizmos.DrawCube(marker.Point.position, new Vector3(marker.Size.x, marker.Size.y, 0.1f));
            }
        }
```

**`Assets/Game/Editor/MapExporter.cs`** — gom cổng, và khó tính y như với spawn:

```csharp
        /// <summary>
        /// Gom danh sách cổng từ Inspector. KHÔNG kiểm được ToSpawnId có tồn tại ở map đích không —
        /// map đích có thể chưa được export lần nào. Đó là phép kiểm của lúc CHẠY, và MapGrid.FindSpawn
        /// đã lùi về điểm mặc định thay vì ném.
        /// </summary>
        private static bool TryCollectPortals(MapCollisionSource source, out List<Portal> portals)
        {
            portals = new List<Portal>();

            foreach (MapCollisionSource.PortalMarker marker in source.Portals)
            {
                if (marker.Point == null)
                {
                    Fail("Có một dòng trong danh sách Portals còn thiếu Transform.");
                    return false;
                }

                // Cổng rộng hoặc cao 0 thì không ai bước vào được — và không có triệu chứng nào ngoài
                // "cái cổng đó không hoạt động", loại lỗi mất cả buổi để nghĩ ra chỗ mà nhìn.
                if (marker.Size.x <= 0f || marker.Size.y <= 0f)
                {
                    Fail($"Cổng tại {marker.Point.name} có Size = {marker.Size}. Cả hai chiều phải lớn hơn 0.");
                    return false;
                }

                if (marker.ToMapId == source.MapId)
                {
                    Fail($"Cổng tại {marker.Point.name} trỏ về chính map {source.MapId}.");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(marker.ToSpawnId))
                {
                    Fail($"Cổng tại {marker.Point.name} chưa điền ToSpawnId.");
                    return false;
                }

                Vector3 position = marker.Point.position;

                portals.Add(new Portal
                {
                    X = position.x,
                    Y = position.y,
                    Width = marker.Size.x,
                    Height = marker.Size.y,
                    ToMapId = marker.ToMapId,
                    ToSpawnId = marker.ToSpawnId,
                });
            }

            return true;
        }
```

Gọi nó cạnh `TryCollectSpawns`, truyền vào `MapGrid`, và thêm số cổng vào dòng log tổng kết:

```csharp
            if (!TryCollectSpawns(source, out List<SpawnPoint> spawns))
                return;

            if (!TryCollectPortals(source, out List<Portal> portals))
                return;
```

```csharp
            var map = new MapGrid(source.MapId, source.MapName, prefabKey, bounds.xMin, bounds.yMin,
                width, height, spawns, portals, cells);
```

⚠️ **Map thứ hai:** tạo `Assets/Game/Resources/Maps/Map2.prefab` (nhớ nằm dưới `Resources`), đặt
`MapId = 2` trong `MapCollisionSource`, vẽ lớp `Collision`, đặt điểm spawn `default` **và** một điểm tên
`from_map1`, rồi export. `MapRegistry` tự nạp cả hai lúc khởi động — bạn không phải khai báo map mới ở
đâu cả, đó là phần thưởng của việc nó quét thư mục thay vì đọc một danh sách.

</details>

<details>
<summary><b>📖 Lời giải — server: <code>PlayerEntity</code> và <code>WorldService</code></b></summary>

**`Server/GameServer/World/PlayerEntity.cs`** — bỏ `readonly` khỏi `_map`, gom phép gỡ spawn ra hàm
riêng, thêm cờ chống lặp cổng:

```csharp
        // Bỏ readonly: entity đổi map được kể từ khi có cổng. Vẫn chỉ luồng tick ghi.
        private MapGrid _map;

        /// <summary>
        /// Đã ra khỏi mọi cổng chưa — điều kiện để lần bước vào tới được tính.
        ///
        /// CẠNH chứ không phải MỨC. Không có cờ này thì điểm đến nằm trong cổng chiều ngược lại là hai
        /// map ném người chơi qua lại 20 lần mỗi giây. Cùng phân biệt với nút nhảy ở Phase 8.
        ///
        /// KHÔNG nằm trong MoveState, khác DropThroughTicks: client không mô phỏng lại việc chuyển map,
        /// nên đây không phải một phần của contract.
        /// </summary>
        private bool _portalArmed = true;
```

Hàm dựng gọi hàm chung thay vì tự tính:

```csharp
            _profile = CharacterProfiles.Get(row.ClassId);
            _map = map;
            State = ResolveSpawn(map, row.X, row.Y, warnIfStuck: true);
```

```csharp
        /// <summary>
        /// Dời entity sang map khác. CHỈ GỌI TỪ LUỒNG TICK.
        ///
        /// Ba thứ đổi CÙNG MỘT LÚC: lưới va chạm, id map, vị trí. Đổi lẻ một thứ là có đúng một tick mô
        /// phỏng thân người ở map mới bằng lưới của map cũ — triệu chứng là rơi xuyên sàn đúng một lần
        /// ngay lúc sang map, thứ khó tái hiện nhất trên đời.
        /// </summary>
        public void MoveToMap(MapGrid map, float x, float y)
        {
            _map = map;
            MapId = map.MapId;
            State = ResolveSpawn(map, x, y, warnIfStuck: false);

            // Tới nơi có thể đang đứng ngay giữa cổng chiều về. Tắt cờ để nó không kích lại.
            _portalArmed = false;
        }

        /// <summary>
        /// Cổng mà entity vừa BƯỚC VÀO ở tick này, hoặc null. CHỈ GỌI TỪ LUỒNG TICK.
        /// Gọi đúng MỘT lần mỗi tick cho mỗi entity: nó có tác dụng phụ (lên/xuống cờ).
        /// </summary>
        public Portal TakePortal()
        {
            Portal portal = _map.PortalAt(State.X, State.Y);

            if (portal == null)
            {
                _portalArmed = true;
                return null;
            }

            if (!_portalArmed)
                return null;

            _portalArmed = false;

            return portal;
        }

        /// <summary>
        /// Gỡ một điểm spawn ra khỏi tường. Dùng chung cho lần vào world đầu và cho mỗi lần sang map:
        /// hai đường mà tính khác nhau thì sớm muộn một đường quên gỡ.
        /// </summary>
        private MoveState ResolveSpawn(MapGrid map, float x, float y, bool warnIfStuck)
        {
            float spawnX = MovementRules.ClampX(map, _profile, x);
            float spawnY = MovementRules.ResolveSpawnY(map, _profile, spawnX, y);

            if (warnIfStuck && (Math.Abs(spawnX - x) > 0.1f || Math.Abs(spawnY - y) > 0.1f))
            {
                // LA LỚN chứ không im lặng sửa: một người bị đẩy là chuyện thường, ba trăm người bị đẩy
                // nghĩa là vừa có ai đó export một map hỏng.
                Log.Warn($"{Name} spawn kẹt tại ({x:0.##}, {y:0.##}) — đẩy về ({spawnX:0.##}, {spawnY:0.##})");
            }

            return MoveState.AtRest(spawnX, spawnY);
        }
```

**`Server/GameServer/World/WorldService.cs`** — một pha mới trong `Tick`, đặt **giữa** tích phân và dựng
chỉ mục:

```csharp
            // Vòng 1: tích phân TẤT CẢ trước.
            foreach (PlayerEntity entity in _entities.Values)
                entity.Integrate(dt);

            // Vòng 1b: cổng. SAU tích phân vì vị trí phải chốt xong mới biết có bước vào cổng không;
            // TRƯỚC dựng chỉ mục vì chính tick này chỉ mục phải thấy họ đã ở map mới — nhờ vậy phép
            // diff ở vòng 3 báo tin ngay, không trễ thêm một nhịp.
            foreach (PlayerEntity entity in _entities.Values)
                TryTakePortal(entity);
```

```csharp
        /// <summary>
        /// Đưa entity qua cổng nếu nó vừa bước vào một cái. CHỈ GỌI TỪ LUỒNG TICK.
        ///
        /// Chú ý cái KHÔNG có ở đây: không gửi EntityDespawn cho ai, không gửi EntitySpawn cho ai,
        /// không dọn danh sách nào cả. Đổi MapId là đổi khoá chỉ mục, và phép diff tầm nhìn ở vòng 3 tự
        /// sinh ra đủ bốn chiều thông báo. Đó là toàn bộ lý do bước này nằm SAU Bước 1.
        /// </summary>
        private void TryTakePortal(PlayerEntity entity)
        {
            Portal portal = entity.TakePortal();

            if (portal == null)
                return;

            if (!_maps.TryGet(portal.ToMapId, out MapGrid target))
            {
                // Dữ liệu hỏng chứ không phải người chơi làm sai — đứng yên còn hơn ném họ đi đâu đó.
                Log.Warn($"Cổng ở map {entity.MapId} trỏ tới map {portal.ToMapId} không có trong registry.");
                return;
            }

            SpawnPoint spawn = target.FindSpawn(portal.ToSpawnId);
            int fromMapId = entity.MapId;

            entity.MoveToMap(target, spawn.X, spawn.Y);

            // Gói DUY NHẤT phải gửi tay ở đây — và nó không nói với ai ngoài chính người đi.
            entity.Owner?.SendData(NetCmd.MapChanged, new MapChangedNotice
            {
                MapId = entity.MapId,
                State = entity.State,
            });

            Log.Info($"{entity.Name.Cyan()} map {fromMapId} → {entity.MapId.ToString().Green()} " +
                     $"tại \"{portal.ToSpawnId}\" ({entity.X:0.##}, {entity.Y:0.##})");
        }
```

`CharacterService.LeaveWorldAsync` không phải sửa gì: nó đã lưu `entity.MapId` từ Phase 5, và giờ con số
đó mới thật sự biết đổi.

</details>

<details>
<summary><b>📖 Lời giải — contract và client</b></summary>

**`Server/Shared/Net/NetCmd.cs`** — thêm vào **cuối** dải World, đúng luật ở `ROADMAP.md` §2:

```csharp
        /// <summary>
        /// Người chơi vừa sang map khác. Chỉ gửi cho chính người đi; những người khác biết qua
        /// EntitySpawn/EntityDespawn của tầm nhìn, như mọi lý do xuất hiện/biến mất khác.
        /// Payload: <see cref="Dto.World.MapChangedNotice"/>
        /// </summary>
        MapChanged = 305,
```

**`Server/Shared/Dto/World/WorldSyncDto.cs`**:

```csharp
    [MemoryPackable]
    public partial class MapChangedNotice
    {
        public int MapId { get; set; }

        /// <summary>
        /// Trạng thái ĐẦY ĐỦ ở map mới, không chỉ toạ độ. Gửi mỗi (x, y) thì vận tốc và hai bộ đếm
        /// coyote/jump-buffer của map cũ còn nguyên, và tick đầu ở map mới diễn ra với một cú nhảy
        /// đang dở.
        /// </summary>
        public MoveState State { get; set; }
    }
```

**`Assets/Game/Scripts/Network/Handlers/WorldNetHandler.cs`** — thêm một event và một handler, đúng khuôn
bốn cái đã có:

```csharp
        public event Action<MapChangedNotice> OnMapChanged;
```

```csharp
        [NetHandler(NetCmd.MapChanged)]
        private void HandleMapChanged(NetPacket packet)
        {
            OnMapChanged?.Invoke(packet.GetData<MapChangedNotice>());
        }
```

Không phải đụng `GameLifetimeScope`: `WorldNetHandler` đã đăng ký từ Phase 7, và lệnh mới chỉ là một
method nữa trong nhóm đã tồn tại.

**`Assets/Game/Scripts/World/PlayerMotor.cs`**:

```csharp
        /// <summary>
        /// Mốc seq của lần đổi map gần nhất. Gói MoveState ack một input CŨ HƠN mốc này là ack của map
        /// trước — nuốt nó vào là bị kéo ngược về toạ độ map cũ, một nhịp sau khi đã sang map mới.
        /// </summary>
        private int _mapEpochSeq;

        /// <summary>Đặt lại mô phỏng sang map mới. Gọi khi nhận MapChanged.</summary>
        public void SetMap(MapGrid map, MoveState state)
        {
            _map = map;
            _simState = state;
            _prevSimState = state;

            // BẮT BUỘC. Input xếp hàng cho map cũ mà đem replay trên lưới map mới thì ra quỹ đạo vô
            // nghĩa — đây là thứ dễ quên nhất của cả tính năng.
            _pending.Clear();

            // Dịch chuyển thì không có gì để làm mượt: _renderOffset sinh ra để giấu cú sửa vài
            // centimet, không phải để trượt qua nửa bản đồ.
            _renderOffset = Vector2.zero;
            _mapEpochSeq = _nextSeq;

            transform.position = new Vector2(state.X, state.Y);
        }
```

và một dòng đầu `OnMoveStateResult`:

```csharp
        private void OnMoveStateResult(MoveStateResponse response)
        {
            // Ack của map trước — xem _mapEpochSeq.
            if (response.LastInputSeq < _mapEpochSeq)
                return;

            ... // phần còn lại giữ nguyên
        }
```

**`Assets/Game/Scripts/World/WorldSpawner.cs`** — đăng ký trong `Start`, huỷ trong `OnDestroy`, và xử lý:

```csharp
            _worldNetHandler.OnMapChanged += OnMapChanged;
```

```csharp
        private void OnMapChanged(MapChangedNotice notice)
        {
            if (_localPlayerObject == null)
                return;

            // Cùng ba dòng như lúc vào world — nạp LUẬT, dựng HÌNH, rồi mới đặt lại motor.
            MapGrid map = _mapService.Load(notice.MapId);
            _mapView.Show(map);

            _localPlayerObject.GetComponent<PlayerMotor>().SetMap(map, notice.State);

            // KHÔNG gọi DespawnAllRemotes(). Server đã gửi EntityDespawn cho từng người ở map cũ ngay
            // tick sau — dọn tay ở đây là đường thứ hai làm cùng một việc, và hai đường thì sớm muộn
            // lệch nhau. Chịu một tick ma đứng im, đổi lại một đường duy nhất cho mọi lý do biến mất.
            this.Log($"Sang map {notice.MapId} tại {notice.State.X:0.##}:{notice.State.Y:0.##}");
        }
```

</details>

### ✅ CHECKPOINT B — mục tiêu cuối Phase 11

1. Export cả hai map. Console server lúc khởi động in **hai** dòng map kèm checksum, rồi
   `Đã nạp 2 map, khởi đầu ở #1`.
2. Mở `map_1.json`: có mảng `Portals`. Mở `map_2.json` (nếu map 2 chưa có cổng về): **không có** trường
   `Portals` — trường vắng mặt, chứ không phải một mảng rỗng.
3. `dotnet test Server/Shared.Tests` — sáu bài xanh, gồm bài round-trip cổng mới thêm.
4. Chạy vào cổng ở map 1 → sang map 2: **hình đổi**, nhân vật đứng đúng điểm `from_map1`, đi lại bình
   thường, va chạm đúng lưới map 2 (thử đâm vào một bức tường chỉ map 2 mới có).
5. Đứng yên ngay trên điểm đến vài giây → **không** bị ném ngược lại. Đó là cờ `_portalArmed` làm việc.
6. Hai client: A ở map 1, B đi từ map 1 sang map 2. Trên màn hình A, B **biến mất** — mà bạn không viết
   một dòng nào để làm việc đó. Log tạm kích thước snapshot của A: về **0**.
7. B đi ngược về map 1 gần chỗ A đứng → A thấy B **hiện ra**, đúng hướng mặt.
8. B thoát game lúc đang ở map 2, đăng nhập lại → vào thẳng map 2 đúng chỗ. `MapId` đã đi xuống DB từ
   Phase 5; bây giờ con số ấy mới thật sự có hai giá trị.

Điểm (6) là cả Bước 2 gói trong một dòng: **tính năng mới, không có code báo tin mới.**

---

## Bốn thử nghiệm bắt buộc

**1. Nhảy múa ở ranh giới AOI.**
Hai người cách nhau đúng **24 unit** (`AOI_RADIUS_X`), một người bước qua-lại quanh mốc đó → người kia
liên tục nhận một cặp gói spawn/despawn, và client liên tục dựng/huỷ một GameObject.

Mốc 24 nằm **ngoài khung hình** nên lần này bạn không thấy bằng mắt — nhìn cửa sổ **Hierarchy** thay
vào đó: object `Remote_…` xuất hiện rồi biến mất theo nhịp bước chân. (Đó cũng là một bài học: bán kính
tầm nhìn phải lớn hơn màn hình thì mới không có triệu chứng nhìn thấy được, nhưng chi phí thì vẫn còn
nguyên.)

Đây là flicker kinh điển của AOI không có hysteresis (vào và ra dùng **cùng một ngưỡng**). Không sửa ở
phase này — nhưng phải **thấy nó** và trả lời được câu 4 bên dưới.

**2. Đo cái AOI mua được.**
Log tạm tổng số `EntityState` server gửi mỗi giây. Hai client đứng cạnh nhau: ~40/giây (20 tick × 2
người × 1 state). Đi xa nhau: **0**.

Với broadcast của Phase 7 con số này không bao giờ về 0 dù map to cỡ nào — và nó tăng theo **bình phương**
tổng người online, trong khi bản này tăng theo mật độ cục bộ. Cùng một cảnh chơi, hai đường cong khác hẳn.

**3. Client không biết gì cả.**
Sau **Bước 1**, mở `git diff` và xác nhận: **không một file nào trong `Assets/` bị sửa**. Server thay
toàn bộ logic phát sinh `EntitySpawn`/`EntityDespawn` mà client cũ chạy nguyên.

Đây là thử nghiệm dễ nhất và đáng nhớ nhất. Nếu Phase 7 đã trót đặt tên gói là `PlayerJoinedWorld` /
`PlayerLeftWorld` thì hôm nay cái tên ấy **nói dối** — gói vẫn chạy đúng, nhưng mỗi người đọc code sau
này sẽ hiểu sai một chút, và không có lỗi biên dịch nào báo.

(Bước 2 thì có sửa client, nhưng để ý sửa **cái gì**: nạp map, dựng hình, đặt lại motor. Không một dòng
nào bên client biết rằng "sang map" nghĩa là ai đó phải biến mất — phần ấy vẫn đi đường
`EntityDespawn` cũ.)

**4. Cắt đứt cổng giữa chừng.**
Sửa tạm `TryTakePortal` để nó **quên** gọi `entity.Owner?.SendData(NetCmd.MapChanged, …)` — chỉ
`MoveToMap` rồi thôi. Chạy vào cổng.

Server đã đưa bạn sang map 2 và mọi người ở map 1 thấy bạn biến mất, nhưng client bạn vẫn vẽ map 1 và
vẫn dự đoán bằng **lưới va chạm của map 1**. Bạn đi xuyên tường của map cũ, rồi bị `MoveState` của
server kéo giật liên tục về một toạ độ không ăn nhập với thứ đang thấy.

Đây là hình ảnh sạch nhất trong cả dự án của một câu đã nói từ Phase 8: *server là source of truth,
nhưng client phải được cho biết đủ để mô phỏng lại cùng một luật.* Thiếu một gói thông báo thì "source
of truth" biến thành "hai thế giới song song". Trả code về như cũ.

---

## Troubleshooting

| Triệu chứng | Nguyên nhân thường gặp | Chỗ sửa |
|---|---|---|
| Người kia không bao giờ biến mất dù chạy rất xa | còn broadcast của Phase 7 trong `Spawn`, hoặc snapshot vẫn dựng từ toàn bộ `_entities` | `WorldService.Spawn` · `BuildSnapshot` |
| Người kia biến mất rồi không hiện lại | sai thứ tự ba thao tác: phải là báo-mới → báo-đi → cập-nhật-tập | `Tick` vòng 3 |
| Không ai despawn bao giờ | tập `Visible` bị ghi đè trước khi so | `Tick` vòng 3, thao tác (3) đang nằm trên (2) |
| Vào world xong thấy chính mình nhân đôi | `CollectVisible` quên loại `viewer.EntityId` | `CollectVisible` |
| Người ở nửa trái map (X âm) nhìn thấy người ở nửa phải | dùng cast `(int)` thay cho `MathF.Floor` khi tính cột | `ColumnOf` |
| Đi sang phải mãi không ai biến mất, đi sang trái một đoạn ngắn đã mất | thiếu phép so `\|Δx\| ≤ AOI_RADIUS_X` — tầm nhìn đang là hình chữ nhật méo của lưới cột | `CollectVisible` |
| Người kia biến mất trong khi **vẫn còn trong khung hình** | bán kính nhỏ hơn nửa bề RỘNG màn hình (nhầm với `orthographicSize`, vốn là nửa bề CAO) | `AOI_RADIUS_X` |
| Người ở map khác vẫn nhìn thấy nhau | khoá chỉ mục quên `MapId` | `ColumnOf` |
| Nhấp nháy hiện/biến ở một khoảng cách nhất định | flicker ranh giới AOI — hành vi đã biết, chưa sửa ở phase này | thử nghiệm 1; sửa thật thì cần hysteresis (câu 4) |
| Người hiện ra quay sai hướng rồi một nhịp sau mới quay lại | `ToSpawnNotice` thiếu `FacingLeft`/`Crouching`/`Action` | `WorldService.ToSpawnNotice` |
| GC spike đều đặn 20 lần/giây | cấp phát `Dictionary`/`List` mới mỗi tick thay vì `Clear()` bộ đệm | `WorldService` — ba field `_columns`, `_visibleNow`, `_visibleIds` |

**Bước 2 — chuyển map:**

| Triệu chứng | Nguyên nhân thường gặp | Chỗ sửa |
|---|---|---|
| Bước vào cổng thì bị ném qua-lại hai map liên tục | thiếu cờ `_portalArmed` — đang kiểm "đứng trong cổng" thay vì "vừa bước vào" | `PlayerEntity.TakePortal` · `MoveToMap` phải tắt cờ |
| Sang map mới rồi đi xuyên tường, bị giật liên tục | client chưa nhận `MapChanged`, hoặc `PlayerMotor._map` chưa đổi | thử nghiệm 4 dựng lại đúng cảnh này · `WorldSpawner.OnMapChanged` |
| Vừa sang map thì bị kéo ngược về toạ độ map cũ đúng một nhịp | gói ack của map cũ tới sau `SetMap` | `PlayerMotor._mapEpochSeq` — thiếu dòng chặn đầu `OnMoveStateResult` |
| Sang map xong nhân vật rơi xuyên sàn đúng một lần | `_map` và `MapId` đổi mà `State` chưa đổi (hoặc ngược lại) — một tick chạy lưới cũ | `PlayerEntity.MoveToMap` phải đổi cả ba thứ cùng lúc |
| Sang map xong quỹ đạo nhảy loạn vài tick | `_pending` chưa `Clear()` — replay input map cũ trên lưới map mới | `PlayerMotor.SetMap` |
| Người ở map cũ vẫn thấy mình đứng im mãi | còn sót đường broadcast tay của Phase 10 và nó thắng phép diff | `WorldService` — `Broadcast<T>` phải bị xoá hẳn ở Bước 1 |
| Bước vào cổng nhưng không có gì xảy ra | `Size` của cổng bằng 0, hoặc cổng vẽ lơ lửng trên cao trong khi phép kiểm dùng toạ độ **bàn chân** | `MapCollisionSource` Inspector · xem gizmo |
| `Cổng ở map N trỏ tới map M không có trong registry` | map đích chưa export, hoặc `MapId` trong `MapCollisionSource` của nó điền sai | export map đích, kiểm `MapId` |
| Sang map đúng nhưng đứng nhầm chỗ (rơi vào điểm mặc định) | `ToSpawnId` không khớp id nào ở map đích — `FindSpawn` đã lùi về mặc định | danh sách Spawns của map đích |
| File map có `"Portals": null` hoặc `[]` | thiếu `NullValueHandling.Ignore`, hoặc `Write` đang tạo list rỗng thay vì `null` | `MapFile.Settings` · `MapFile.Write` |
| `MapFileTests` đỏ sau khi thêm cổng | `BuildSample` gọi hàm dựng `MapGrid` cũ | truyền `portals: null` |
| Sang map xong camera bay ngang qua cả bản đồ rồi mới dừng | `SmoothDamp` đang làm mượt một cú dịch chuyển | `WorldSpawner.OnMapChanged` thiếu `CameraFollow.SnapToTarget()` |
| `Hai map cùng id 1: "…" và "…"` lúc server khởi động | đổi tên file map (`map1.json` → `map_1.json`) nhưng file CŨ còn nằm trong `bin/.../Data/Maps` — `CopyToOutputDirectory` chỉ thêm, không bao giờ xoá | `dotnet clean Server/GameServer`, hoặc xoá tay thư mục `Data/Maps` trong output |
| Export Map ghi đè nhầm file map khác | hai `MapCollisionSource` cùng **bật** trong scene, tool lấy cái nào là do thứ tự hierarchy | tắt bớt; `MapExporter.TryFindSource` đã chặn và báo tên cả hai |

---

## Tự kiểm tra hiểu bài

**Câu 1.** Vì sao AOI chia **cột** theo X mà không chia lưới ô 2D?
<details>
<summary><b>📖 Đáp án câu 1</b></summary>

Vì chỉ nên chia ô ở trục mà thế giới **thật sự lớn**. Map cao ~11 unit trong khi một màn hình đã cao 18
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

Vào và ra tầm nhìn dùng **cùng một ngưỡng** (`AOI_RADIUS_X`), nên người đứng ngay mốc đó chỉ cần dao
động vài centimet là đổi trạng thái — mỗi lần đổi là một cặp gói spawn/despawn và một lần dựng/huỷ
GameObject.

Hysteresis tách hai ngưỡng: **vào** tầm nhìn ở bán kính hẹp (24), chỉ **ra** khi vượt bán kính rộng hơn
(28). Người ở giữa hai ngưỡng **giữ nguyên trạng thái hiện có**, nên dao động nhỏ quanh một điểm không
đổi được trạng thái nữa. Trong code là một dòng ở `CollectVisible`:
`float limit = viewer.Visible.Contains(entity.EntityId) ? AOI_EXIT_RADIUS : AOI_RADIUS_X;` — và nhớ nới
`AOI_COLUMN_WIDTH` lên bằng ngưỡng RA, nếu không 3 cột không còn phủ hết bán kính ra.

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

**Câu 6.** `DropThroughTicks` (Phase 10) nằm trong `MoveState` và đi trên dây mỗi tick. Cờ
`_portalArmed` (phase này) thì chỉ là một field bình thường của `PlayerEntity`, không đi đâu cả. Hai cái
đều là "trạng thái server giữ để chống lặp một thao tác" — vì sao một cái thuộc contract còn cái kia
không?
<details>
<summary><b>📖 Đáp án câu 6</b></summary>

Phép thử chỉ có một câu: **client có phải mô phỏng lại nó không?**

`DropThroughTicks` thì có. Client dự đoán mọi bước di chuyển, và vòng replay của reconciliation phải
chạy lại đúng những tick đã chạy — trong đó có tick người chơi tụt xuyên bệ. Thiếu con số ấy trong
`MoveState` thì replay ra một quỹ đạo khác với dự đoán, và nhân vật rung ở đúng chỗ cái bệ.

`_portalArmed` thì không. Client **không dự đoán chuyển map** — nó chỉ ngồi đợi gói `MapChanged` rồi
tuân theo. Không mô phỏng thì không cần tái hiện, không cần tái hiện thì không việc gì phải trả 4 byte
mỗi tick cho mỗi người, và quan trọng hơn: không phải mở rộng contract. Mở rộng `MoveState` là đổi giao
thức, mà DLL cũ bên Unity sẽ đọc ra những con số vô nghĩa **mà không báo lỗi**.

Bài học mang đi: "server giữ trạng thái này" và "trạng thái này thuộc contract" là **hai câu hỏi khác
nhau**. Câu thứ hai chỉ được trả lời *có* khi phía bên kia thật sự phải tính lại cùng một thứ.

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
- **Màn hình chờ khi chuyển map.** Hôm nay map mới dựng ngay trong một frame vì `Resources.Load` là đồng
  bộ và map thì nhỏ. Từ Phase 18 (Addressables/CDN) thì nạp là **bất đồng bộ**, và lúc đó cần một trạng
  thái "đang chuyển map" ở client: khoá input, che màn hình, chờ nạp xong mới `SetMap`. Server không
  phải đổi gì — nó đã gửi `MapChanged` rồi tiếp tục mô phỏng, y như bây giờ.
- **Chống lạm dụng cổng.** Server hiện tin rằng vị trí của entity là do chính nó mô phỏng ra, nên đứng
  trong cổng là bước vào cổng thật. Đúng chừng nào `Step` vẫn là nơi duy nhất đổi vị trí — ngày nào có
  lệnh dịch chuyển từ ngoài (skill nhảy, admin teleport) thì phải hỏi lại: dịch chuyển **vào** một cổng
  có được tính là qua cổng không?
- **Cổng một chiều và cổng có điều kiện.** "Cần chìa khoá", "cần level 10", "chỉ mở ban đêm" — tất cả là
  một phép kiểm thêm trong `TryTakePortal`, nhưng dữ liệu để kiểm thì chưa tồn tại (Phase 13 túi đồ,
  Phase 14 chỉ số). Đừng thêm trường vào `Portal` trước khi có thứ để đọc: một trường không ai đọc còn
  tệ hơn không có gì.

---

**Xong Phase 11 → thế giới sống: có hình dạng, có tầm nhìn, có nhiều map nối với nhau, băng thông theo
mật độ.**
[PHASE-12](PHASE-12.md) trả nốt món nợ rải khắp năm phase vừa qua: `GRAVITY`, `MAX_FALL_SPEED`,
`AOI_COLUMN_WIDTH`, `MapRegistry.STARTING_MAP_ID`, cả bảng `CharacterProfiles` — tất cả đang là hằng số
nằm cứng trong code. Đưa chúng ra dữ liệu, sửa không cần build lại, và phân biệt cho rõ hai loại config
khác nhau về bản chất — trong đó **file map của Phase 10 chính là ví dụ loại B đầu tiên**, và phép so
checksum bằng mắt sẽ thành phép kiểm version bằng máy lúc login.
