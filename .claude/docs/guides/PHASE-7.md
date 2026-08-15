# PHASE 7 — Đồng bộ nhiều người chơi

> **Kết quả cuối Phase 7:** mở 2 client (2 tài khoản khác nhau), hai nhân vật thấy nhau chạy **mượt** trên
> cùng map. Một bên thoát thì bên kia thấy biến mất. Không giật, không dịch chuyển tức thời, dù gói tin
> đến không đều.
>
> **Điều kiện:** xong [`PHASE-6.md`](PHASE-6.md) tới CHECKPOINT B và cả 3 thử nghiệm.
> Doc này bám theo *lời giải* Phase 6 — code của bạn lệch chỗ nào thì tự chiếu lại cho khớp.
>
> **Bài học chính:** trạng thái của người khác đến theo **đợt rời rạc** (20 gói/giây, nhịp không đều),
> còn màn hình vẽ 60–144 hình/giây — khoảng trống ở giữa phải **nội suy**. Hệ quả phải chấp nhận: người
> khác trên màn hình của bạn luôn sống ở quá khứ ~150ms.

Format như trước: **hướng làm** hiện sẵn, **📖 Lời giải** trong foldout — tự code trước, đối chiếu sau.

---

## Hai kênh, hai cách xử lý

Từ phase này client nhận vị trí của **hai loại đối tượng**, và chúng cần hai cơ chế khác hẳn nhau:

| | Nhân vật của MÌNH | Nhân vật của NGƯỜI KHÁC |
|---|---|---|
| Có input không? | Có — biết mình đang bấm gì | Không — chỉ biết kết quả server báo |
| Cơ chế | **Prediction + reconciliation** (Phase 6) | **Interpolation** (phase này) |
| Sống ở thời nào | Hiện tại (dự đoán trước server) | Quá khứ (~150ms sau sự thật) |
| Gói tin | `MoveState` (kèm `LastInputSeq`) | `WorldSnapshot` (mảng vị trí) |

Vì sao người khác không prediction được? Prediction cần input — mà input của người khác không bao giờ
tới máy bạn (và không nên: gửi input của mọi người cho mọi người là phí băng thông vô ích). Không có
input thì chỉ còn hai lựa chọn: **ngoại suy** (đoán tiếp hướng cũ — đoán sai là thấy nhân vật lao qua
tường rồi bị giật ngược lại) hoặc **nội suy** (vẽ trễ đi một chút, luôn vẽ giữa hai vị trí *đã biết
chắc* — không bao giờ đoán sai). Game 2D top-down chọn nội suy gần như tuyệt đối.

```
Gói vị trí của B đến máy A:   t=0ms      t=50ms       t=105ms      t=148ms   (nhịp KHÔNG đều)
                                 ●----------●------------●------------●
Màn hình A vẽ B tại:                    ▲ thời điểm vẽ = now − 150ms
                              luôn nằm GIỮA hai mẫu đã có → chỉ việc Lerp, không phải đoán
```

Độ trễ nội suy (`INTERP_DELAY`) là cái đệm: phải đủ lớn để lúc cần vẽ thì mẫu "bên phải" đã đến nơi
(≥ 2–3 tick + jitter), và đủ nhỏ để người khác không quá "cũ". 150ms ≈ 3 tick là điểm cân bằng tốt
cho 20 tick/s.

---

## Bước 1 — Shared: ba gói tin mới

### Hướng làm

Cả ba đều là **server chủ động đẩy**, client không bao giờ gửi:

- `EntitySpawn = 302` — một entity xuất hiện trong world: `EntityId`, `Name`, `ClassId`, `X`, `Y`.
  Gửi cho người cũ khi có người mới vào, **và** gửi hàng loạt cho người mới về những người đang có mặt.
  Một loại gói cho cả hai chiều — đừng đẻ hai loại.
- `EntityDespawn = 303` — entity rời world: chỉ cần `EntityId`.
- `WorldSnapshot = 304` — mảng `{ EntityId, X, Y }` của những người **khác**, đẩy mỗi tick.

DTO đặt ở `Server/Shared/Dto/World/WorldSyncDto.cs`. Câu hỏi thiết kế: vì sao snapshot không chứa `Name`,
`ClassId`? — Vì chúng **không đổi theo tick**. Thứ bất biến đi một lần trong `EntitySpawn`; snapshot 20
lần/giây chỉ chở thứ thay đổi. Mỗi byte thừa trong snapshot bị nhân với `20 × số người × số người xem`.

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`Server/Shared/Net/NetCmd.cs`** — thêm vào cuối region World / Movement:

```csharp
        /// <summary>
        /// Một entity xuất hiện trong tầm quan sát. Chỉ server gửi.
        /// Payload: <see cref="Dto.World.EntitySpawnNotice"/>
        /// </summary>
        EntitySpawn = 302,

        /// <summary>
        /// Một entity rời tầm quan sát. Chỉ server gửi.
        /// Payload: <see cref="Dto.World.EntityDespawnNotice"/>
        /// </summary>
        EntityDespawn = 303,

        /// <summary>
        /// Vị trí của các entity KHÁC đang quan sát được, server đẩy mỗi tick.
        /// Payload: <see cref="Dto.World.WorldSnapshotNotice"/>
        /// </summary>
        WorldSnapshot = 304,
```

**`Server/Shared/Dto/World/WorldSyncDto.cs`**:

```csharp
using System;
using MemoryPack;

namespace MMORPG.Shared.Dto.World
{
    /// <summary>
    /// Phần BẤT BIẾN của một entity — gửi đúng một lần lúc nó xuất hiện.
    /// Thứ đổi theo tick (vị trí) đi trong snapshot, không lặp lại ở đây mỗi tick.
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
    }

    [MemoryPackable]
    public partial class EntityDespawnNotice
    {
        public int EntityId { get; set; }
    }

    /// <summary>Trạng thái một entity tại một tick. Cố tình chỉ có thứ thay đổi theo tick.</summary>
    [MemoryPackable]
    public partial class EntityState
    {
        public int EntityId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
    }

    [MemoryPackable]
    public partial class WorldSnapshotNotice
    {
        public EntityState[] States { get; set; } = Array.Empty<EntityState>();
    }
}
```

</details>

---

## Bước 2 — Server: báo xuất hiện, báo rời đi, đẩy snapshot

### Hướng làm

Ba chỗ sửa, đều trong `WorldService`:

**1. `Spawn`** — sau khi ghi sổ: (a) gửi cho **người mới** một loạt `EntitySpawn` về từng người đang có
mặt; (b) gửi cho **mọi người cũ** một `EntitySpawn` về người mới. Viết một helper
`Broadcast<T>(NetCmd, T, int exceptEntityId)` duyệt `_entities.Values` — sẽ còn dùng nhiều.

**2. `Despawn`** — sau khi gỡ sổ: broadcast `EntityDespawn`.

**3. `Tick`** — sau khi `Integrate` xong **tất cả**, mỗi entity nhận thêm một `WorldSnapshot` chứa mọi
người trừ chính nó. Tách hai vòng lặp: tích phân hết rồi mới gửi — không thì người duyệt trước nhận vị
trí *cũ* của người duyệt sau, hai máy nhìn cùng một tick ra hai bức tranh khác nhau.

Chi phí dựng snapshot là O(n²) và cấp phát mảng mỗi người mỗi tick — **kệ nó**, đúng bài "một dictionary
phẳng là đủ": AOI sẽ xử ở phase sau, đừng tối ưu thứ sắp bị thay.

**Bẫy thứ tự đáng suy nghĩ trước khi code:** `Spawn` chạy trên thread xử lý gói, `Tick` chạy trên luồng
game loop — snapshot của tick đang chạy có thể tới client **trước** gói `EntitySpawn` (hai luồng cùng
enqueue vào một kết nối, ai xếp hàng trước không có gì bảo đảm). Nghĩa là client có thể nhận vị trí của
một `EntityId` nó chưa từng nghe tên. Chọn cách xử ở **client**: id lạ trong snapshot → **bỏ qua**,
`EntitySpawn` sẽ tới trong vài chục ms. Đơn giản, tự lành, không cần khoá gì phía server.

### ✅ CHECKPOINT A — nhìn bằng log trước khi có hình

Cần 2 tài khoản (1 tài khoản = 1 nhân vật!). Mở 2 client — cách nhanh nhất: **ParrelSync** (clone project,
mở 2 Editor), hoặc build một bản `.exe` chạy cạnh Editor.

1. A vào world trước, B vào sau → console A hiện `EntitySpawn` của B; console B hiện `EntitySpawn` của A
   (danh sách người có mặt).
2. Cả hai bắt đầu nhận `WorldSnapshot` đều đặn (log tạm số state mỗi gói — nhớ xoá sau).
3. B thoát → console A hiện `EntityDespawn` đúng id của B.

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`WorldService.cs`** — `Spawn` thêm phần thông báo (đặt sau khi ghi sổ + log):

```csharp
            // Người mới cần biết ai đang có mặt — gửi một loạt EntitySpawn về từng người cũ.
            foreach (PlayerEntity other in _entities.Values)
            {
                if (other.EntityId == entity.EntityId)
                    continue;

                owner.SendData(NetCmd.EntitySpawn, ToSpawnNotice(other));
            }

            // Và người cũ cần biết có người mới.
            Broadcast(NetCmd.EntitySpawn, ToSpawnNotice(entity), exceptEntityId: entity.EntityId);
```

`Despawn` thêm:

```csharp
            Broadcast(NetCmd.EntityDespawn,
                new EntityDespawnNotice { EntityId = entity.EntityId },
                exceptEntityId: entity.EntityId);
```

Helper + Tick:

```csharp
        /// <summary>Gửi một gói cho mọi entity đang trong world, trừ một người (thường là nguồn tin).</summary>
        private void Broadcast<T>(NetCmd cmd, T dto, int exceptEntityId) where T : IMemoryPackable<T>
        {
            // Duyệt ConcurrentDictionary trong lúc có thể có Spawn/Despawn song song là hợp lệ:
            // iterator "weakly consistent" — không ném lỗi, chỉ có thể thiếu/thừa đúng entity
            // đang vào/ra tại khoảnh khắc đó. Với gói thông báo thì sai một tick là vô hại.
            foreach (PlayerEntity entity in _entities.Values)
            {
                if (entity.EntityId == exceptEntityId)
                    continue;

                entity.Owner?.SendData(cmd, dto);
            }
        }

        private static EntitySpawnNotice ToSpawnNotice(PlayerEntity entity)
        {
            return new EntitySpawnNotice
            {
                EntityId = entity.EntityId,
                Name = entity.Name,
                ClassId = entity.ClassId,
                X = entity.X,
                Y = entity.Y,
            };
        }
```

`Tick` — thay thân cũ bằng hai vòng tách bạch:

```csharp
        public void Tick(float dt)
        {
            // Vòng 1: tích phân TẤT CẢ trước. Trộn tích phân với gửi thì người gửi trước
            // mang vị trí cũ của người tích phân sau — hai client nhìn cùng tick ra hai bức tranh.
            foreach (PlayerEntity entity in _entities.Values)
                entity.Integrate(dt);

            // Vòng 2: gửi. MoveState cho chính chủ (đường reconciliation),
            // WorldSnapshot về những người còn lại (đường interpolation).
            foreach (PlayerEntity entity in _entities.Values)
            {
                if (entity.Owner == null)
                    continue;

                entity.Owner.SendData(NetCmd.MoveState, new MoveStateResponse
                {
                    LastInputSeq = entity.LastInputSeq,
                    X = entity.X,
                    Y = entity.Y,
                });

                entity.Owner.SendData(NetCmd.WorldSnapshot, BuildSnapshotFor(entity));
            }
        }

        /// <summary>Mọi entity trừ chính người nhận. O(n²) mỗi tick — chấp nhận cho tới khi có AOI.</summary>
        private WorldSnapshotNotice BuildSnapshotFor(PlayerEntity viewer)
        {
            var states = new List<EntityState>(_entities.Count - 1);

            foreach (PlayerEntity entity in _entities.Values)
            {
                if (entity.EntityId == viewer.EntityId)
                    continue;

                states.Add(new EntityState { EntityId = entity.EntityId, X = entity.X, Y = entity.Y });
            }

            return new WorldSnapshotNotice { States = states.ToArray() };
        }
```

(cần `using MemoryPack;`, `using MMORPG.Shared.Dto.World;`, `using MMORPG.Shared.Net;`)

</details>

---

## Bước 3 — Client: người khác hiện ra và đi lại mượt

### Hướng làm

**1. `WorldNetHandler`** — thêm ba event + ba method `[NetHandler]` cho `EntitySpawn` / `EntityDespawn`
/ `WorldSnapshot`. Không đăng ký gì thêm trong `GameLifetimeScope`.

**2. File mới `Assets/Game/Scripts/World/RemotePlayerView.cs`** — trái tim của phase. MonoBehaviour gắn
lên prefab nhân vật-người-khác, giữ **buffer nội suy**:

- `PushState(Vector2 pos)` — mỗi lần snapshot đến: ghi `(Time.time, pos)` vào một `List`, cắt bớt mẫu
  quá cũ (giữ ~1 giây).
- `Update()` — tính `renderTime = Time.time - INTERP_DELAY` (0.15f), tìm **hai mẫu kẹp** quanh
  `renderTime`, `Lerp` theo tỉ lệ thời gian. Ba trường hợp biên phải xử: buffer rỗng (đứng yên),
  `renderTime` cũ hơn mẫu đầu (dùng mẫu đầu), mới hơn mẫu cuối (dùng mẫu cuối — **không ngoại suy**;
  đây là lúc mạng nghẽn, nhân vật khựng lại là hành vi đúng).

Mốc thời gian dùng `Time.time` **lúc gói đến máy mình** — không phải thời gian server. Nhịp gói đến ≈
nhịp tick server + jitter, và jitter chính là thứ `INTERP_DELAY` tồn tại để nuốt.

**3. `WorldSpawner`** — quản lý người khác, giữ `Dictionary<int, RemotePlayerView>`:

- Cần thêm prefab thứ hai (`_remotePrefab` — cùng hình vuông, **khác màu**) và inject thêm `LocalPlayer`
  (để biết id nào là của mình).
- `OnEntitySpawn`: id của mình hoặc id đã có → bỏ qua; còn lại Instantiate + ghi sổ, mồi buffer bằng vị
  trí trong notice.
- `OnEntityDespawn`: Destroy + xoá sổ.
- `OnSnapshot`: với từng state — id của mình → bỏ qua (việc của `MoveState`); **id lạ → bỏ qua** (gói
  `EntitySpawn` đang trên đường tới — bẫy thứ tự ở Bước 2); còn lại `PushState`.
- Logout / bị kick: dọn **cả** người khác (`DespawnAll`), không chỉ nhân vật mình.

Subscribe trong `Start`, unsubscribe trong `OnDestroy` như mọi khi.

### ✅ CHECKPOINT B — mục tiêu cuối Phase 7

1. Hai client, hai tài khoản, cùng vào world → mỗi bên thấy **hai** nhân vật: mình một màu, bạn một màu.
2. A chạy vòng tròn → trên máy B, nhân vật A lượn **mượt**, không giật bậc thang, không dịch chuyển tức thời.
3. Để ý độ trễ: A đổi hướng đột ngột, trên máy B thấy đổi hướng muộn hơn ~150–200ms. Đó không phải bug —
   đó là `INTERP_DELAY` + đường truyền, và là cái giá của "không bao giờ đoán sai".
4. B thoát (cả thoát đẹp lẫn kill process) → nhân vật B biến mất trên máy A trong ~1 giây.
5. A logout về màn hình đăng nhập → màn hình sạch cả mình lẫn bạn; đăng nhập lại → thấy lại đầy đủ.

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`WorldNetHandler.cs`** — thêm:

```csharp
        public event Action<EntitySpawnNotice> OnEntitySpawn;
        public event Action<EntityDespawnNotice> OnEntityDespawn;
        public event Action<WorldSnapshotNotice> OnSnapshot;

        [NetHandler(NetCmd.EntitySpawn)]
        private void HandleEntitySpawn(NetPacket packet)
        {
            OnEntitySpawn?.Invoke(packet.GetData<EntitySpawnNotice>());
        }

        [NetHandler(NetCmd.EntityDespawn)]
        private void HandleEntityDespawn(NetPacket packet)
        {
            OnEntityDespawn?.Invoke(packet.GetData<EntityDespawnNotice>());
        }

        [NetHandler(NetCmd.WorldSnapshot)]
        private void HandleSnapshot(NetPacket packet)
        {
            OnSnapshot?.Invoke(packet.GetData<WorldSnapshotNotice>());
        }
```

(thêm `using MMORPG.Shared.Dto.World;`)

**`Assets/Game/Scripts/World/RemotePlayerView.cs`**:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace MMORPG.Client.World
{
    /// <summary>
    /// Hiển thị nhân vật của NGƯỜI KHÁC: nhận vị trí rời rạc từ snapshot, vẽ mượt bằng nội suy.
    /// Luôn vẽ trễ INTERP_DELAY so với gói mới nhất — đổi độ trễ lấy sự chắc chắn không phải đoán.
    /// </summary>
    public sealed class RemotePlayerView : MonoBehaviour
    {
        /// <summary>
        /// Độ trễ nội suy: phải ≥ 2–3 khoảng tick (50ms) + jitter thì mẫu "bên phải" mới kịp đến
        /// trước lúc cần vẽ. Nhỏ quá → hết mẫu, nhân vật giật; to quá → người khác càng "cũ".
        /// </summary>
        private const float INTERP_DELAY = 0.15f;

        /// <summary>Giữ mẫu trong chừng này rồi cắt — quá khứ xa hơn không bao giờ cần vẽ lại.</summary>
        private const float BUFFER_KEEP = 1f;

        private readonly struct Sample
        {
            public readonly float Time;
            public readonly Vector2 Pos;

            public Sample(float time, Vector2 pos)
            {
                Time = time;
                Pos = pos;
            }
        }

        private readonly List<Sample> _buffer = new();

        /// <summary>Gọi mỗi lần snapshot đến. Mốc thời gian là đồng hồ MÁY MÌNH lúc nhận.</summary>
        public void PushState(Vector2 pos)
        {
            _buffer.Add(new Sample(Time.time, pos));

            // Cắt đầu buffer: chỉ cần giữ đủ để nội suy, không phải lịch sử cả trận.
            while (_buffer.Count > 2 && _buffer[0].Time < Time.time - BUFFER_KEEP)
                _buffer.RemoveAt(0);
        }

        private void Update()
        {
            if (_buffer.Count == 0)
                return;

            // Vẽ tại thời điểm quá khứ: mọi thứ cần biết về khoảnh khắc này đã nằm sẵn trong buffer.
            float renderTime = Time.time - INTERP_DELAY;

            // Trước mẫu đầu (mới xuất hiện) → đứng ở mẫu đầu.
            if (renderTime <= _buffer[0].Time)
            {
                transform.position = _buffer[0].Pos;
                return;
            }

            // Tìm hai mẫu kẹp renderTime rồi nội suy theo tỉ lệ thời gian giữa chúng.
            for (int i = 0; i < _buffer.Count - 1; i++)
            {
                Sample a = _buffer[i];
                Sample b = _buffer[i + 1];

                if (renderTime > b.Time)
                    continue;

                float t = (renderTime - a.Time) / (b.Time - a.Time);
                transform.position = Vector2.Lerp(a.Pos, b.Pos, t);
                return;
            }

            // Qua cả mẫu cuối: mạng đang nghẽn, KHÔNG đoán tiếp — đứng ở vị trí chắc chắn cuối cùng.
            // Ngoại suy ở đây là đổi "khựng nhẹ" lấy "lao qua tường rồi bị giật ngược", lỗ vốn.
            transform.position = _buffer[^1].Pos;
        }
    }
}
```

**`WorldSpawner.cs`** — thêm phần quản lý người khác:

```csharp
        [SerializeField] private GameObject _remotePrefab;

        private readonly Dictionary<int, RemotePlayerView> _remotes = new();

        private LocalPlayer _localPlayer;
```

`Construct` nhận thêm `LocalPlayer localPlayer` và gán. Subscribe:

```csharp
        private void Start()
        {
            _worldNetHandler.OnEntitySpawn += OnEntitySpawn;
            _worldNetHandler.OnEntityDespawn += OnEntityDespawn;
            _worldNetHandler.OnSnapshot += OnSnapshot;
        }

        private void OnDestroy()
        {
            if (_worldNetHandler == null)
                return;

            _worldNetHandler.OnEntitySpawn -= OnEntitySpawn;
            _worldNetHandler.OnEntityDespawn -= OnEntityDespawn;
            _worldNetHandler.OnSnapshot -= OnSnapshot;
        }

        private void OnEntitySpawn(EntitySpawnNotice notice)
        {
            // Gói về chính mình (nếu có) hoặc gói lặp — bỏ qua, không nhân bản.
            if (notice.EntityId == _localPlayer.EntityId || _remotes.ContainsKey(notice.EntityId))
                return;

            GameObject remote = Instantiate(
                _remotePrefab, new Vector3(notice.X, notice.Y, 0f), Quaternion.identity, _entityRoot);
            remote.name = $"Remote_{notice.EntityId}_{notice.Name}";

            var view = remote.GetComponent<RemotePlayerView>();
            view.PushState(new Vector2(notice.X, notice.Y));

            _remotes[notice.EntityId] = view;
        }

        private void OnEntityDespawn(EntityDespawnNotice notice)
        {
            if (!_remotes.TryGetValue(notice.EntityId, out RemotePlayerView view))
                return;

            Destroy(view.gameObject);
            _remotes.Remove(notice.EntityId);
        }

        private void OnSnapshot(WorldSnapshotNotice snapshot)
        {
            foreach (EntityState state in snapshot.States)
            {
                // Vị trí của mình đi đường MoveState — snapshot chỉ dành cho người khác.
                if (state.EntityId == _localPlayer.EntityId)
                    continue;

                // Id lạ: snapshot của tick này vượt mặt gói EntitySpawn (hai luồng server cùng
                // enqueue, thứ tự không bảo đảm). Bỏ qua — EntitySpawn sẽ đến trong vài chục ms.
                if (!_remotes.TryGetValue(state.EntityId, out RemotePlayerView view))
                    continue;

                view.PushState(new Vector2(state.X, state.Y));
            }
        }

        private void DespawnAllRemotes()
        {
            foreach (RemotePlayerView view in _remotes.Values)
                Destroy(view.gameObject);

            _remotes.Clear();
        }
```

và gọi `DespawnAllRemotes()` bên trong `DespawnLocalPlayer()` — logout/kick là rời world, mọi thứ của
world phải sạch.

Trong Editor: nhân bản prefab người chơi → đổi màu → gắn `RemotePlayerView` (bỏ `PlayerMotor`!) →
kéo vào `_remotePrefab`.

</details>

---

## Bước 4 — Ba thử nghiệm bắt buộc

**1. Đường nào ra đường nấy.** Tắt tạm dòng gửi `WorldSnapshot` phía server: hai client vẫn vào được,
vẫn thấy nhau xuất hiện (EntitySpawn), nhưng người kia đứng im vĩnh viễn — còn nhân vật mình vẫn chạy
bình thường. Bật lại. Hiểu rõ: `MoveState` và `WorldSnapshot` là hai kênh độc lập, hỏng kênh nào lộ
triệu chứng ấy.

**2. Kill process giữa trận.** Không thoát đẹp — kill hẳn process client B (End Task). Máy A phải thấy
B biến mất sau khi server phát hiện đứt kết nối (vòng đọc ném lỗi → `finally` → `LeaveWorldAsync` →
Despawn → broadcast). Đây là kiểm tra tổng cho chuỗi dọn dẹp của Phase 5 dưới điều kiện bẩn nhất.

**3. Nghịch `INTERP_DELAY`.** Đặt `0.02f` (nhỏ hơn một khoảng tick): người khác giật như tranh vẽ từng
nét — buffer liên tục cạn, rơi vào nhánh "đứng ở mẫu cuối". Đặt `0.5f`: mượt tuyệt đối nhưng người khác
trễ nửa giây, đuổi bắt nhau thấy rõ độ "cũ". Trả về `0.15f` và ghi nhớ hai đầu của cái cân này.

---

## Troubleshooting

| Triệu chứng | Nguyên nhân | Xử lý |
|-------------|-------------|-------|
| Không thấy nhân vật người kia | Quên đăng ký handler mới? Không — nhóm đã đăng ký từ Phase 5. Khả năng cao: quên gắn `RemotePlayerView` vào prefab remote, hoặc `_remotePrefab` chưa kéo trong Inspector | Kiểm console có log lỗi `GetComponent` null |
| Thấy nhau nhưng đứng im | Snapshot không đến (Bước 2 chưa gửi) hoặc `OnSnapshot` bỏ qua nhầm (so sánh id sai) | Log tạm số state mỗi snapshot |
| Người kia giật bậc thang | `INTERP_DELAY` quá nhỏ, hoặc `PushState` không được gọi đều | Xem thử nghiệm 3 |
| Người kia trôi rồi giật ngược | Ai đó thêm ngoại suy "cho mượt" | Đọc lại comment cuối `Update` của `RemotePlayerView` |
| Nhân vật MÌNH bị nhân đôi | `OnEntitySpawn` không lọc `EntityId` của mình, hoặc `LocalPlayer.EntityId` chưa được `Apply` trước khi gói spawn đến | So thứ tự: `EnterWorldResponse` luôn đến trước (cùng kết nối, server gửi response trước khi Spawn broadcast — kiểm lại nếu bạn đổi thứ tự trong `EnterWorldAsync`) |
| Warning id lạ liên tục (nếu bạn log) | Bình thường ở mức 1–2 gói lúc có người vào — bẫy thứ tự hai luồng | Chỉ bất thường khi kéo dài — lúc đó `EntitySpawn` thật sự thất lạc |
| Người kia biến mất khi MÌNH logout rồi vào lại thấy thiếu | `DespawnAllRemotes` chưa gọi khi logout, sổ `_remotes` còn id cũ nên `EntitySpawn` mới bị `ContainsKey` chặn | Dọn sổ ở `DespawnLocalPlayer` |
| Hai máy nhìn vị trí lệch nhau xa | Một máy build cũ (Shared DLL khác bản) | Build lại Shared + cả hai client |

---

## Tự kiểm tra hiểu bài

Tự trả lời từng câu xong mới mở đáp án của câu đó.

**Câu 1.** Vì sao nhân vật của mình dùng prediction còn của người khác dùng interpolation? Điều gì thiếu
khiến không prediction hộ người khác được?
<details>
<summary>📖 Đáp án câu 1</summary>

Prediction là chạy trước công thức mô phỏng với **input** — của mình thì input có ngay tại chỗ trước cả
server. Input của người khác không bao giờ tới máy mình (và gửi nó cho mọi người là phí vô ích), nên với
họ chỉ có chuỗi *kết quả* rời rạc — lựa chọn còn lại là đoán tiếp (ngoại suy, sẽ đoán sai lúc họ đổi
hướng) hoặc vẽ trễ giữa hai kết quả đã chắc (nội suy). Nội suy không bao giờ sai — chỉ trễ.

</details>

**Câu 2.** Vì sao `EntitySpawn` chở `Name`/`ClassId` còn `WorldSnapshot` thì không? Ước lượng thử chi phí
nếu nhét `Name` vào snapshot với 50 người chơi đứng gần nhau.
<details>
<summary>📖 Đáp án câu 2</summary>

Snapshot lặp 20 lần/giây và bị nhân ba lần: `20 × số người trong tầm × số người xem`. Tên ~10 byte UTF-8:
50 người × 49 người xem × 20 lần × 10 byte ≈ **490 KB/giây** chỉ để gửi đi gửi lại thứ không bao giờ đổi.
Nguyên tắc: dữ liệu bất biến đi một lần lúc xuất hiện; kênh lặp chỉ chở delta thật.

</details>

**Câu 3.** Vì sao `Tick` phải tích phân **tất cả** entity xong rồi mới gửi snapshot, thay vì tích phân
tới đâu gửi tới đó?
<details>
<summary>📖 Đáp án câu 3</summary>

Trộn hai việc thì snapshot gửi cho người duyệt sớm chứa vị trí *chưa tích phân* của người duyệt muộn —
cùng một tick nhưng A thấy B ở t, B thấy A ở t+1. Mỗi snapshot phải là **một lát cắt nhất quán** của
thế giới tại đúng một thời điểm; muốn vậy pha "đổi thế giới" và pha "chụp ảnh thế giới" phải tách rời.

</details>

**Câu 4.** Client nhận snapshot chứa `EntityId` chưa từng thấy. Vì sao chuyện này xảy ra dù TCP bảo đảm
thứ tự, và vì sao "bỏ qua" là cách xử đúng?
<details>
<summary>📖 Đáp án câu 4</summary>

TCP bảo đảm thứ tự **các byte đã đưa vào hàng gửi** — nhưng `EntitySpawn` (từ thread xử lý gói) và
snapshot (từ luồng tick) được *enqueue* bởi hai luồng khác nhau; ai enqueue trước không có gì hứa. Bỏ qua
là đúng vì hệ tự lành: `EntitySpawn` chắc chắn sẽ đến (cùng kết nối, không mất gói), và trong lúc chờ,
thiếu một-hai mẫu vị trí của người vừa xuất hiện là không ai nhận ra. Giải pháp thay thế (khoá để ép thứ
tự giữa hai luồng server) đắt hơn hẳn cái nó mua.

</details>

**Câu 5.** `INTERP_DELAY = 0.15f` — điều gì quyết định con số này? Chuyện gì xảy ra ở hai thái cực?
<details>
<summary>📖 Đáp án câu 5</summary>

Nó phải **đủ lớn** để tại thời điểm vẽ, mẫu "bên phải" đã có mặt: tối thiểu một khoảng tick (50ms) +
jitter đường truyền + dự phòng một gói tới muộn — nên 2–3 khoảng tick là hợp lý. Quá nhỏ: buffer cạn
liên tục, rơi vào "đứng ở mẫu cuối", nhìn như giật. Quá lớn: mượt tuyệt đối nhưng người khác sống ở quá
khứ sâu — mọi tương tác nhắm vào họ (đuổi bắt, sau này là đánh nhau) đều lệch cảm giác.

</details>

**Câu 6.** Buffer cạn (renderTime vượt mẫu mới nhất). Vì sao đứng im tại mẫu cuối tốt hơn ngoại suy
tiếp theo hướng cũ?
<details>
<summary>📖 Đáp án câu 6</summary>

Ngoại suy là cá cược "họ vẫn đi tiếp như cũ". Thắng cược: đỡ một cú khựng. Thua cược (họ vừa dừng/đổi
hướng/đụng tường đúng lúc mạng nghẽn): nhân vật ma lao tiếp qua chỗ không thể có mặt, rồi khi gói thật
đến bị **giật ngược** — vừa xấu hơn cú khựng, vừa gây hiểu nhầm trạng thái (tưởng họ ở chỗ X mà thật ra
chưa từng). Khựng tại vị trí chắc chắn cuối cùng là sai số *có trần*; ngoại suy là sai số không trần.

</details>

**Câu 7.** Khi logout, vì sao phải dọn cả `_remotes` chứ không chỉ nhân vật của mình? Triệu chứng nếu quên?
<details>
<summary>📖 Đáp án câu 7</summary>

Rời world là vứt toàn bộ tri thức về world — sổ `_remotes` là tri thức cũ. Quên dọn: lần vào lại,
`EntitySpawn` của người vẫn-đang-online bị `ContainsKey` chặn ("có rồi mà") trong khi GameObject của họ
đã bị Destroy hoặc trỏ vào object của phiên trước — người đó thành tàng hình hoặc đứng im vĩnh viễn.
Loại bug "chỉ xuất hiện từ lần đăng nhập thứ hai" — khó lần đúng vì lần đầu luôn sạch.

</details>

**Câu 8.** Snapshot hiện là O(n²) và cấp phát mảng mới mỗi người mỗi tick. Vì sao *không* tối ưu ngay
bây giờ là quyết định đúng?
<details>
<summary>📖 Đáp án câu 8</summary>

Vì cấu trúc này **sắp bị thay hình**: AOI làm snapshot chỉ còn chứa người trong tầm nhìn — bài toán từ
"n² toàn server" thành "n × số người gần". Tối ưu code sắp bị thay là trả lãi cho món nợ sắp xoá. Quy
tắc chung của dự án: làm bản thẳng-đơn-giản trước, tối ưu khi (a) đo được nó chậm, hoặc (b) thiết kế
mới yêu cầu — AOI là trường hợp (b), và nó ở ngay phase sau.

</details>

---

**Xong Phase 7.** Thế giới đã thật sự "multi": nhiều người, một sự thật, mỗi màn hình một góc nhìn trễ
vài phần trăm giây. [PHASE-8](PHASE-8.md) cho thế giới một **hình dạng**: map có tường thật (server
kiểm va chạm — hết xuyên tường), và AOI để chỉ những ai gần nhau mới tốn băng thông của nhau.
