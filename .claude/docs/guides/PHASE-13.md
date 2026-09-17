# PHASE 13 — Túi đồ & item: feature dọc đầu tiên

> **Kết quả cuối Phase 13:** nhân vật có một cái túi 30 ô. Gõ một phím trên console server là mọi người
> nhận được đồ; mở túi trong game thấy nó, dùng được, vứt được, kéo sang ô khác được; thoát game vào
> lại vẫn còn nguyên. Và **đang mở túi mà nhận đồ thì UI tự đúng ngay**, không phải đóng ra mở lại.
>
> **Điều kiện:** xong [`PHASE-12.md`](PHASE-12.md) — có `Fnv1a`, có khuôn "bảng loại B", có `Contract.Hash`.
>
> **Bài học chính:** (1) **khuôn mẫu thêm một feature MMO**, đi đủ sáu tầng DB → DAL → DbCmd → logic →
> NetCmd → UI; (2) **cache RAM + dirty flag** thay cho ghi DB mỗi thao tác, và cái giá phải trả của nó;
> (3) **delta ≠ snapshot**, và vì sao thứ UI cần không phải là "trạng thái mới" mà là "cái gì vừa đổi";
> (4) `itemId` (một vật cụ thể) ≠ `templateId` (một loại vật) — hai khái niệm mà lẫn lộn thì sửa lại
> phải đụng cả DB lẫn contract.

Format như trước: **hướng làm** hiện sẵn, **📖 Lời giải** trong foldout.

---

## Sáu tầng của một feature

Mười hai phase vừa rồi dựng hạ tầng. Từ đây trở đi, mỗi phase là **một feature**, và feature nào cũng
đi qua đúng sáu tầng ấy. Phase này đi chậm để bạn nhìn rõ khuôn; Phase 14, 15, 16 sẽ đi lại nó nhanh hơn
nhiều.

```
 ① DB       bảng inventory_item + migration              (DBServer/Data/Migrator.cs)
 ② DAL      InventoryRepository — SQL, không biết game   (DBServer/Repositories/)
 ③ DbCmd    InventoryLoad / InventorySave + DTO          (Shared/Db, DBServer/Handlers/)
 ④ Logic    Inventory (RAM) + InventoryService           (GameServer/World/)
 ⑤ NetCmd   InventorySnapshot / Delta / Use / Drop / Move(Shared/Net, GameServer/Handlers/)
 ⑥ UI       InventoryModel → InventoryPanel              (Assets/Game/Scripts/Inventory/)
```

Đọc từ dưới lên cũng đúng, và đôi khi đúng hơn: bắt đầu từ *"người chơi thấy gì"* rồi lần xuống. Nhưng
**làm** thì làm từ trên xuống, vì mỗi tầng chỉ test được khi tầng dưới nó đã chạy.

Một điều đáng để ý ngay: trong sáu tầng ấy, **ba tầng giữa không biết gì về nhau**. `InventoryRepository`
không biết `NetCmd` tồn tại; `InventoryService` không biết SQL. Đó không phải là sự sạch sẽ cho vui — nó
là thứ khiến Phase 20 (SQLite → MySQL) chỉ phải sửa tầng ②.

---

## Hai con số, hai khái niệm: `itemId` và `templateId`

Trước khi viết dòng nào, phải tách cho rõ — nhầm chỗ này thì sửa lại phải đụng cả DB lẫn contract:

| | `templateId` | `itemId` |
|---|---|---|
| Là gì | **loại** đồ: "Bình máu nhỏ", "Kiếm gỗ" | **một vật cụ thể** trong túi của một người |
| Ai sinh ra | người viết bảng `items.json` | `AUTOINCREMENT` của DB |
| Bao nhiêu | vài trăm, cố định | hàng triệu, sinh ra và mất đi liên tục |
| Đổi được không | không (đổi là đổi dữ liệu game) | không, nhưng vòng đời rất ngắn |
| Ở đâu | bảng loại B, cả hai bên cùng đọc | chỉ trong DB và RAM server |

Câu hỏi để kiểm tra mình đã hiểu chưa: *hai bình máu trong hai ô khác nhau — cùng `templateId` hay khác?*
Cùng. *Cùng `itemId` không?* Không. Và đó là toàn bộ lý do phải có hai số: nếu chỉ có một, thì "vứt bình
máu ở ô 3" không diễn đạt được mà không nói thêm "ô nào".

> Bẫy quen thuộc: thấy `templateId` là đủ (vì đang xếp chồng theo loại) rồi bỏ `itemId` đi. Nó chạy được
> cho tới ngày món đồ có **thuộc tính riêng** — một cây kiếm +7 và một cây kiếm +0 cùng `templateId` mà
> khác hẳn nhau. Lúc đó thêm `itemId` vào là di cư cả bảng DB và cả contract.

Phase này chưa có đồ cường hoá, nhưng `itemId` vẫn có từ ngày đầu, cùng lý do với trường `version` ở
Phase 12: **thêm nó sau khi đã có người chơi là một cuộc di cư.**

---

## Bước 1 — Bảng item: loại B thứ ba

### Hướng làm

Không có gì mới về cơ chế — làm y hệt bảng nhân vật ở Phase 12, và đó chính là điều đáng nói: khuôn đã
dựng xong thì bảng thứ ba tốn một buổi thay vì một phase.

`Config/items.json`:

```json
{
  "Version": 1,
  "Items": [
    {
      "TemplateId": 1,
      "Name": "Bình máu nhỏ",
      "Description": "Hồi 50 sinh lực.",
      "IconKey": "Items/potion_small",
      "Kind": "Consumable",
      "MaxStack": 20
    },
    {
      "TemplateId": 100,
      "Name": "Kiếm gỗ",
      "Description": "Vũ khí tập luyện.",
      "IconKey": "Items/sword_wood",
      "Kind": "Equipment",
      "MaxStack": 1
    }
  ]
}
```

**`MaxStack` là dữ liệu, không phải một nhánh `if`.** Đây là cùng một bài học với `LocksMovement` của
`ActionDefinition` ở Phase 9: thứ khác nhau giữa các dòng trong bảng thì thuộc về bảng. Viết
`if (templateId == POTION) maxStack = 20;` là ký cam kết sẽ sửa code mỗi lần thêm một loại đồ.

**`Kind`** thì ngược lại — nó **là** một nhánh, vì mỗi giá trị kéo theo một đoạn xử lý riêng
(`Consumable` thì dùng được, `Equipment` thì mặc được ở Phase 14). Enum ghi bằng tên trong file, như
`ActionState` ở Phase 12.

**Ranh giới đáng nghĩ: `IconKey` có nên ở đây không?** Nó là thông tin **thuần client** — server không
bao giờ đọc nó. Nhưng nó thuộc về *định nghĩa của loại đồ*, và tách ra file riêng nghĩa là có hai bảng
phải khớp nhau theo `TemplateId` — tức là dựng lại đúng cái bệnh của cả Phase 12. Để chung, và chấp nhận
server mang theo vài chuỗi nó không dùng.

> Luật rút ra: **một thực thể, một dòng.** Chia bảng theo "ai đọc" nghe có lý nhưng tạo ra hai bảng phải
> đồng bộ; chia theo "cái gì" thì chỉ có một chỗ để sai.

**Đường đi của bảng** giống hệt bảng nhân vật: `ConfigService` đọc file → `ItemTemplates.Load(...)` →
`EnterWorldResponse` mang cả bảng xuống → client gọi `ItemTemplates.Load(...)` với đúng dữ liệu ấy.
Checksum bằng `Fnv1a` (bỏ qua `Name`/`Description`/`IconKey` — chúng không đổi hành vi nào, xem lý do ở
`CharacterTableData.Checksum` của Phase 12).

Và đây là lúc để lại một dấu mốc, vì nó sẽ tới sớm hơn bạn nghĩ:

> `EnterWorldResponse` giờ mang: luật thế giới, bảng nhân vật, bảng item, checksum map. Phase 15 thêm
> bảng quái và bảng drop. **Tới bảng thứ năm là gói EnterWorld thành cái xe tải**, và đó đúng là lúc
> chuyển sang chế độ "client cache, server chỉ gửi hash" của Phase 18. Trường `Version` trong mỗi bảng
> đã có sẵn cho ngày ấy.

### ✅ CHECKPOINT A

1. Server boot in: `Bảng item: 2 loại, checksum XXXXXXXX`.
2. Client vào world, log ra đúng checksum đó.
3. Thêm một item vào `items.json`, restart server, **client không build lại** → client log checksum mới.
4. Ghi `"Kind": "Vuqua"` → server báo lỗi rõ ràng, không im lặng bỏ qua.
5. Hai dòng cùng `TemplateId` → server **từ chối bảng** và la lớn. (Khác `Kind` sai ở chỗ: một id trùng
   thì không có "giá trị mặc định an toàn" nào để lùi về — bảng đã tự mâu thuẫn.)

<details>
<summary><b>📖 Lời giải — <code>Shared/World/ItemTable.cs</code></b></summary>

```csharp
using System;
using System.Collections.Generic;
using MemoryPack;

namespace MMORPG.Shared.World
{
    /// <summary>
    /// Loại đồ quyết định nó LÀM ĐƯỢC GÌ, nên nó là một nhánh xử lý chứ không phải một con số —
    /// khác hẳn MaxStack, thứ chỉ khác nhau về giá trị giữa các dòng.
    /// </summary>
    public enum ItemKind : byte
    {
        /// <summary>Đồ linh tinh: chỉ nằm trong túi, vứt được, không dùng được.</summary>
        Misc = 0,

        /// <summary>Dùng một lần, giảm số lượng.</summary>
        Consumable = 1,

        /// <summary>Mặc được. Phase 13 chưa mặc được gì — đó là việc của Phase 14.</summary>
        Equipment = 2,
    }

    /// <summary>
    /// Định nghĩa một LOẠI đồ. Bất biến trong suốt phiên chạy: nó là dữ liệu game design, không phải
    /// trạng thái người chơi.
    /// </summary>
    [MemoryPackable]
    public sealed partial class ItemTemplate
    {
        public int TemplateId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Khoá tài nguyên của icon — THUẦN client, server không đọc dòng này bao giờ. Vẫn để chung
        /// bảng: tách ra là có hai bảng phải khớp nhau theo TemplateId, tức dựng lại đúng cái bệnh
        /// mà Phase 12 vừa chữa.
        /// </summary>
        public string IconKey { get; set; } = string.Empty;

        public ItemKind Kind { get; set; }

        /// <summary>Tối đa bao nhiêu cái trong một ô. 1 nghĩa là không xếp chồng.</summary>
        public int MaxStack { get; set; } = 1;
    }

    [MemoryPackable]
    public sealed partial class ItemTableData
    {
        public int Version { get; set; } = 1;

        public ItemTemplate[] Items { get; set; } = Array.Empty<ItemTemplate>();

        /// <summary>
        /// Chỉ băm thứ ĐỔI HÀNH VI. Sửa Description cho hay hơn mà bắt cả hai bên báo lệch phiên bản
        /// là dạy người ta bỏ qua cảnh báo — và một cảnh báo bị bỏ qua thì không còn là cảnh báo.
        /// </summary>
        public uint Checksum()
        {
            uint hash = Fnv1a.START;

            hash = Fnv1a.Mix(hash, Version);

            foreach (ItemTemplate item in Items)
            {
                hash = Fnv1a.Mix(hash, item.TemplateId);
                hash = Fnv1a.Mix(hash, (int)item.Kind);
                hash = Fnv1a.Mix(hash, item.MaxStack);
            }

            return hash;
        }
    }

    /// <summary>
    /// Bảng tra template theo id. Cùng khuôn với CharacterProfiles ở Phase 12: bảng tĩnh NẠP ĐƯỢC,
    /// server nạp từ file, client nạp từ gói EnterWorld, một hàm dựng cho cả hai.
    /// </summary>
    public static class ItemTemplates
    {
        private static Dictionary<int, ItemTemplate> _byId = new();

        public static uint Checksum { get; private set; }

        public static int Count => _byId.Count;

        /// <summary>
        /// Template của một id, hoặc null. Trả null chứ không ném: một id lạ đến từ DB (đồ của bảng
        /// cũ, item bị gỡ khỏi bảng) là chuyện sẽ xảy ra, và cách xử lý đúng là bỏ qua món đồ đó chứ
        /// không phải chặn người chơi vào game.
        /// </summary>
        public static ItemTemplate Find(int templateId)
        {
            return _byId.TryGetValue(templateId, out ItemTemplate template) ? template : null;
        }

        /// <summary>Thay cả bảng. Dựng nguyên bảng mới rồi mới gán — xem Phase 12 câu 4.</summary>
        public static void Load(ItemTableData table)
        {
            var built = new Dictionary<int, ItemTemplate>();

            foreach (ItemTemplate item in table.Items)
            {
                // Trùng id thì KHÔNG có giá trị mặc định nào để lùi về: bảng đã tự mâu thuẫn, và cái
                // nào thắng là chuyện của thứ tự dòng trong file. Ném, để người sửa bảng biết ngay.
                if (built.ContainsKey(item.TemplateId))
                    throw new InvalidOperationException($"Hai item cùng TemplateId {item.TemplateId}: \"{built[item.TemplateId].Name}\" và \"{item.Name}\".");

                built[item.TemplateId] = item;
            }

            _byId = built;
            Checksum = table.Checksum();
        }
    }
}
```

</details>

---

## Bước 2 — Từ DB lên tới RAM server

### Hướng làm

**① DB — migration 4.** Nhớ luật bất di bất dịch của `Migrator`: migration đã chạy thì không bao giờ
sửa, cần đổi gì thì thêm migration mới.

```sql
CREATE TABLE inventory_item (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    character_id INTEGER NOT NULL REFERENCES character(id) ON DELETE CASCADE,
    template_id  INTEGER NOT NULL,
    quantity     INTEGER NOT NULL,
    slot         INTEGER NOT NULL
);

-- Một ô chỉ chứa được một chồng. Ràng buộc ở DB chứ không ở code: xem Phase 5, cùng lý do với
-- UNIQUE(account_id) của bảng character.
CREATE UNIQUE INDEX idx_inventory_slot ON inventory_item (character_id, slot);
```

`ON DELETE CASCADE` đáng dừng lại một giây: xoá nhân vật là xoá sạch túi của họ, và việc ấy do **DB**
làm, không phải do một dòng code nào đó phải nhớ gọi. Tính đúng đắn đặt ở chỗ không quên được thì nó
không bị quên.

**② DAL — `InventoryRepository`.** Hai hàm, và chỉ hai:

- `LoadAsync(characterId)` → `InventoryRow[]`
- `SaveAsync(characterId, InventoryRow[])` → xoá sạch rồi ghi lại, **trong một transaction**

Cách "xoá sạch rồi ghi lại" trông thô, và nó thô thật. Nhưng hãy so với cách "đúng bài" — diff từng
dòng, sinh ra `INSERT`/`UPDATE`/`DELETE` tương ứng:

| | Xoá + ghi lại | Diff từng dòng |
|---|---|---|
| Code | ~15 dòng, không có nhánh nào | ~80 dòng, ba nhánh, mỗi nhánh một cách sai |
| Chi phí | 30 dòng mỗi lần lưu | vài dòng mỗi lần lưu |
| Bao lâu một lần | mỗi 30 giây + lúc rời world | như trên |
| `itemId` sau khi lưu | **đổi** (AUTOINCREMENT cấp số mới) | giữ nguyên |

Dòng cuối là cái giá thật, và nó chỉ mất tiền vào ngày món đồ có **lịch sử** (log giao dịch, đồ khoá
theo id). Hôm nay chưa có. Ghi lại lựa chọn này ở đây để ngày ấy biết chỗ mà sửa — **và biết rằng nó là
một lựa chọn, không phải một sơ suất.**

**③ DbCmd** — dải 1300–1399 theo `ROADMAP.md §2`:

```
InventoryLoad = 1300
InventorySave = 1301
```

**④ Logic — `Inventory` trong RAM.** Đây là phần có nội dung thật của bước này.

`Inventory` là mảng cố định `SLOT_COUNT = 30` ô, mỗi ô là `ItemStack { ItemId, TemplateId, Quantity }`
hoặc rỗng. Ba thao tác, và **cả ba đều trả về danh sách ô vừa đổi**:

```csharp
IReadOnlyList<int> TryAdd(int templateId, int quantity);   // rỗng = không đủ chỗ
IReadOnlyList<int> TryUse(int slot);
IReadOnlyList<int> TryRemove(int slot, int quantity);
IReadOnlyList<int> TryMove(int from, int to);
```

Vì sao trả **danh sách ô đổi** chứ không trả `bool`: vì tầng ⑤ cần đúng thứ đó để gửi delta, và tầng ⑥
cần đúng thứ đó để vẽ lại. Trả `bool` rồi để chỗ gọi tự đoán đã đổi ô nào là mời nó đoán sai.

`TryAdd` là hàm duy nhất có thuật toán, và thuật toán ấy có một thứ tự **bắt buộc**:

1. Dồn vào các ô **đã có cùng loại và chưa đầy**, theo thứ tự ô tăng dần.
2. Còn thừa thì xuống ô **trống** đầu tiên, tối đa `MaxStack` mỗi ô.
3. Hết chỗ mà vẫn còn thừa → **không nhận gì cả**, trả danh sách rỗng.

Bước 3 là chỗ dễ sai nhất: cám dỗ là "nhận được bao nhiêu thì nhận". Đừng. Nhận một phần nghĩa là phần
còn lại **biến mất**, và người chơi mất đồ mà không có gì báo. Hoặc nhận trọn, hoặc từ chối trọn —
và để chỗ gọi quyết định phần thừa đi đâu (rơi xuống đất ở Phase 15).

> Đây là "all-or-nothing" ở tầng nghiệp vụ, cùng tinh thần với transaction ở tầng DB. Mọi thao tác chạm
> vào tài sản người chơi đều nên có tính chất này, và nó phải được quyết ngay lần đầu viết hàm — sửa sau
> thì phải đi tìm mọi chỗ gọi.

**Cache RAM + dirty flag.** `PlayerEntity` giữ một `Inventory`. Nạp từ DB **một lần** lúc vào world, đổi
trong RAM, ghi xuống DB khi (a) rời world, (b) mỗi 30 giây nếu `IsDirty`.

Vì sao không ghi thẳng mỗi thao tác (write-through): mỗi lần nhặt một đồng tiền là một vòng TCP sang
DBServer + một transaction SQLite. Ở 100 người chơi đánh quái thì đó là vài trăm lượt ghi mỗi giây cho
một dữ liệu mà **không ai đọc ngoài chính chủ**.

Cái giá phải trả, nói thẳng ra: **server chết đột ngột là mất mọi thay đổi từ lần lưu cuối.** Đó là một
đánh đổi có ý thức, không phải một lỗ hổng — và thử nghiệm 2 ở cuối bài bắt bạn nhìn thấy nó bằng mắt.

**Nguồn item của phase này là một phím trên console server.** `G` → `InventoryService.GrantToAll(...)`.
Không có quái, không có đồ rơi dưới đất — cả hai là Phase 15. Xếp hàng qua `ConcurrentQueue` rồi tiêu
thụ ở đầu tick, y hệt `EnqueueForceAll` của Phase 9: **luồng đọc phím không được chạm vào entity.**

### ✅ CHECKPOINT B

1. Vào world → server log `Túi của Hùng: 0/30 ô`.
2. Gõ `G` → log `+3 Bình máu nhỏ → ô 0`.
3. Gõ `G` thêm 6 lần → 20 cái dồn đầy ô 0, phần dư sang ô 1. Đúng `MaxStack`.
4. Chờ 30 giây → log `Lưu túi`. Mở file `mmorpg.db` bằng DB Browser → thấy đúng số dòng.
5. Logout → vào lại → túi còn nguyên.
6. Đổ đầy 30 ô rồi gõ `G` → log `Túi đầy, không nhận` và **không có ô nào đổi**.

<details>
<summary><b>📖 Lời giải — <code>Inventory.TryAdd</code></b></summary>

```csharp
        /// <summary>
        /// Nhận đồ vào túi. Trả danh sách ô vừa đổi; RỖNG nghĩa là không nhận được gì.
        ///
        /// Hoặc nhận TRỌN, hoặc từ chối TRỌN. Nhận một phần nghĩa là phần còn lại bốc hơi mà không có
        /// gì báo — và người chơi sẽ không bao giờ tha thứ cho điều đó, kể cả khi nó chỉ là một bình
        /// máu. Phần thừa đi đâu là quyết định của chỗ GỌI, không phải của cái túi.
        ///
        /// CHỈ GỌI TỪ LUỒNG TICK.
        /// </summary>
        public IReadOnlyList<int> TryAdd(int templateId, int quantity)
        {
            ItemTemplate template = ItemTemplates.Find(templateId);

            if (template == null || quantity <= 0)
                return Array.Empty<int>();

            // THỬ trên bản nháp trước, chỉ ghi vào thật khi chắc chắn đủ chỗ. Cách còn lại — ghi dần
            // rồi hoàn tác khi hết chỗ — là tự viết một transaction bằng tay, và hoàn tác sai thì
            // người chơi mất đồ.
            _scratch.Clear();
            int remaining = quantity;

            // Vòng 1: dồn vào chồng đã có cùng loại, còn chỗ. Trước vòng 2 có chủ đích — dồn xong mới
            // mở ô mới, nếu không thì mỗi lần nhặt một cái là chiếm thêm một ô trong khi ô cũ còn chỗ.
            for (int slot = 0; slot < _slots.Length && remaining > 0; slot++)
            {
                ref ItemStack stack = ref _slots[slot];

                if (stack.TemplateId != templateId || stack.Quantity >= template.MaxStack)
                    continue;

                int take = Math.Min(remaining, template.MaxStack - stack.Quantity);

                _scratch.Add((slot, take));
                remaining -= take;
            }

            // Vòng 2: ô trống.
            for (int slot = 0; slot < _slots.Length && remaining > 0; slot++)
            {
                if (_slots[slot].TemplateId != 0)
                    continue;

                int take = Math.Min(remaining, template.MaxStack);

                _scratch.Add((slot, take));
                remaining -= take;
            }

            if (remaining > 0)
                return Array.Empty<int>();

            var changed = new List<int>(_scratch.Count);

            foreach ((int slot, int amount) in _scratch)
            {
                ref ItemStack stack = ref _slots[slot];

                // Ô trống thì đây là lúc món đồ RA ĐỜI — và ra đời nghĩa là nhận một itemId mới.
                // Id âm tạm thời do server cấp: DB sẽ cấp id thật ở lần lưu kế tiếp. Client không bao
                // giờ đọc con số này, nó chỉ cần slot; id chỉ có nghĩa với DB và với log.
                if (stack.TemplateId == 0)
                {
                    stack.TemplateId = templateId;
                    stack.ItemId = NextTempId();
                    stack.Quantity = 0;
                }

                stack.Quantity += amount;
                changed.Add(slot);
            }

            IsDirty = true;

            return changed;
        }
```

</details>

---

## Bước 3 — Ra tới màn hình: snapshot một lần, delta từ đó về sau

### Hướng làm

**⑤ NetCmd** — dải 400–499:

```
InventorySnapshot = 400   // server → client, TOÀN BỘ túi. Gửi một lần, ngay sau EnterWorld.
InventoryDelta    = 401   // server → client, chỉ những ô vừa đổi.
ItemUse           = 402   // client → server, "dùng ô N"
ItemDrop          = 403   // client → server, "vứt ô N, số lượng Q"
ItemMove          = 404   // client → server, "kéo ô A sang ô B"
```

**Vì sao không phải snapshot cho mọi thứ.** Nói thẳng trước: ở quy mô này, gửi lại cả 30 ô mỗi lần đổi
**cũng chạy tốt** — chưa tới 400 byte. Nếu lý do duy nhất là băng thông thì delta chưa đáng. Nhưng có
một lý do khác, và nó mới là lý do thật:

> Snapshot trả lời *"bây giờ trong túi có gì"*. UI cần trả lời một câu khác: *"cái gì vừa đổi"* — để
> chạy chữ `+3` bay lên, để nháy sáng đúng ô đó, để phát tiếng nhặt đồ. Từ hai snapshot liên tiếp mà
> suy ra "cái gì vừa đổi" là **tự viết lại phép diff mà server đã biết sẵn** — và viết ở phía không có
> đủ thông tin nhất.

Đó là lý do ba hàm `Try*` ở Bước 2 trả về danh sách ô. Thông tin "ô nào vừa đổi" sinh ra ở tầng ④, và
mọi tầng trên đều cần nó. Vứt nó đi ở tầng ⑤ rồi tính lại ở tầng ⑥ là làm hai lần một việc, lần sau tệ
hơn lần trước.

**Snapshot vẫn phải có**, cho đúng một lúc: ngay sau khi vào world, khi client chưa có gì để mà "đổi
từ". Mọi hệ đồng bộ đều có hình dạng này — **một trạng thái đầy đủ ban đầu, rồi một dòng thay đổi**. Bạn
đã gặp nó ở Phase 11 (`EntitySpawn` rồi `WorldSnapshot`), và sẽ gặp lại ở Phase 14 và 15.

**Hình dạng một delta:**

```csharp
[MemoryPackable]
public partial class InventoryDeltaNotice
{
    public InventorySlotDto[] Slots { get; set; }
}

[MemoryPackable]
public partial struct InventorySlotDto
{
    public int Slot;
    public int TemplateId;   // 0 = ô này giờ TRỐNG
    public int Quantity;
}
```

`TemplateId = 0` nghĩa là ô trống — **không có gói "xoá ô" riêng**. Một loại gói thì client có một
nhánh xử lý; hai loại thì có hai nhánh và một câu hỏi "cái nào tới trước". Diễn đạt "trống" bằng một
giá trị trong chính cấu trúc đã có luôn rẻ hơn thêm một cấu trúc.

**Tầng ⑤ không được tin client.** Ba lệnh từ client mang theo số thứ tự ô, và số ấy do **người chơi**
gửi lên. Handler phải hỏi đủ, theo đúng thứ tự này:

1. Session đã `InWorld` chưa (`MinState`, dispatcher lo).
2. `slot` có nằm trong `[0, SLOT_COUNT)` không → client sửa code gửi `slot = -1` là `IndexOutOfRange`
   trong luồng tick, tức là **một tick chết cho cả server**.
3. Ô ấy có đồ không.
4. Món đồ ấy có `Kind` cho phép thao tác này không (`ItemUse` trên một cây kiếm thì từ chối).
5. `quantity` có `> 0` và `<=` số đang có không.

Không phép nào trong năm phép ấy là thừa, và mỗi phép tương ứng với một dòng mà một client sửa được sẽ
gửi. Đây không phải sự hoang tưởng: `MoveHandler` của Phase 6 đã kẹp `DirX` vì đúng lý do này.

**⑥ UI.** Ba mảnh, và ranh giới giữa chúng là bài học của tầng này:

| Mảnh | Việc | Cấm |
|---|---|---|
| `InventoryModel` | giữ 30 ô, nhận snapshot/delta, bắn `OnSlotsChanged(int[] slots)` | không có setter công khai |
| `InventoryPanel` | vẽ 30 ô từ model; vẽ lại **đúng những ô** trong event | không tự sửa model |
| `InventoryApi` | gửi `ItemUse` / `ItemDrop` / `ItemMove` | không đụng model |

Bấm "dùng" thì UI **không** giảm số lượng. Nó gửi gói, rồi chờ. Server đổi, server gửi delta, model đổi,
UI vẽ lại. Có một nhịp trễ bằng RTT, và nhịp trễ ấy là **cái giá của việc luôn hiển thị sự thật** — golden
rule #2 của `CLAUDE.md`, lần này ở dạng nhìn thấy được.

> Cám dỗ "cho nó mượt": giảm ngay ở client rồi sửa lại nếu server từ chối (optimistic update). Đúng về
> mặt kỹ thuật, nhưng đừng làm ở phase này. Ở Phase 6 bạn đã làm optimistic cho **vị trí** — và phải
> viết cả reconciliation lẫn replay để nó không nói dối. Túi đồ chưa đáng trả cái giá ấy: sai một lần là
> người chơi thấy món đồ "hiện ra rồi biến mất", thứ trông giống hệt một vụ mất đồ.

**Panel đầu tiên đi qua `com.hungnt.ui.panel`.** Màn hình login hiện là MonoBehaviour cắm sẵn trong
scene — được, vì nó có đúng một cái. Túi đồ là panel **mở ra đóng vào**, và Phase 14 sẽ thêm panel thứ
hai ngay sau đó. Đó là lúc `PanelManager` bắt đầu đáng giá: đăng ký nó một lần
(`builder.RegisterComponentInHierarchy<PanelManager>().As<IUIManager>()`), prefab nằm trong
`Resources/UI/`, và từ đây mỗi panel mới chỉ tốn một prefab + một class.

Nhớ dòng dễ quên nhất của cả dự án — `CLAUDE.md` §"Thêm một lệnh mạng mới", bước 6:

```csharp
builder.Register<InventoryNetHandler>(Lifetime.Singleton).AsSelf().As<INetHandlerGroup>();
```

Thiếu dòng này thì `InventorySnapshot` và `InventoryDelta` rơi vào hư không, **không có lỗi biên dịch**,
và triệu chứng là "mở túi ra thấy trống" — trông y hệt một bug ở tầng ④ hoặc ①.

### ✅ CHECKPOINT C — mục tiêu cuối Phase 13

1. Vào world → client log `Nhận snapshot túi: 30 ô, 2 ô có đồ`.
2. Bấm phím `I` → panel túi mở ra, vẽ đúng icon và số lượng.
3. **Để panel đang mở**, gõ `G` trên console server → ô tương ứng tự cập nhật, **không phải đóng mở lại**.
   Đây là câu trong mục tiêu của phase, và nó chỉ đúng nếu đường delta → model → event → panel thông suốt.
4. Bấm dùng một bình máu → số lượng giảm 1 sau một nhịp RTT. Dùng tới cái cuối → ô thành trống.
5. Kéo một chồng sang ô trống khác → đổi chỗ. Kéo lên chính nó → không có gói nào được gửi.
6. Vứt một chồng → biến mất. Logout, vào lại → đúng như lúc vứt.
7. Sửa tạm client gửi `ItemUse { Slot = 999 }` → server từ chối, **log Warn**, và **không tick nào chết**.

<details>
<summary><b>📖 Lời giải — client: model và đường đi của delta</b></summary>

```csharp
using System;
using MMORPG.Shared.Dto.Inventory;

namespace MMORPG.Client.Inventory
{
    /// <summary>
    /// Bản sao túi đồ của chính mình, do server gửi xuống. Cùng luật với LocalPlayer ở Phase 5:
    /// cache chỉ-đọc, không phải nguồn sự thật, không có setter công khai.
    ///
    /// Ngày nào có `inventory[3].Quantity--` ở đâu đó trong code UI là ngày golden rule #2 bị phá —
    /// và triệu chứng sẽ là "số lượng hiển thị sai cho tới lúc relog", loại bug không ai tìm ra.
    /// </summary>
    public sealed class InventoryModel
    {
        public const int SLOT_COUNT = 30;

        private readonly InventorySlotDto[] _slots = new InventorySlotDto[SLOT_COUNT];

        /// <summary>
        /// Những ô VỪA ĐỔI, không phải "túi đã đổi". Panel dùng đúng danh sách này để vẽ lại — vẽ lại
        /// cả 30 ô cũng chạy, nhưng lúc đó panel không còn biết ô nào đáng nháy sáng.
        /// </summary>
        public event Action<int[]> OnSlotsChanged;

        public InventorySlotDto Get(int slot)
        {
            return _slots[slot];
        }

        public void ApplySnapshot(InventorySnapshotNotice snapshot)
        {
            Array.Clear(_slots, 0, _slots.Length);

            var changed = new int[SLOT_COUNT];

            for (int i = 0; i < SLOT_COUNT; i++)
                changed[i] = i;

            foreach (InventorySlotDto slot in snapshot.Slots)
                _slots[slot.Slot] = slot;

            // Snapshot = "mọi ô vừa đổi". Nhờ vậy panel chỉ có MỘT đường vẽ lại, không phải hai.
            OnSlotsChanged?.Invoke(changed);
        }

        public void ApplyDelta(InventoryDeltaNotice delta)
        {
            var changed = new int[delta.Slots.Length];

            for (int i = 0; i < delta.Slots.Length; i++)
            {
                InventorySlotDto slot = delta.Slots[i];

                // Gói tin đến từ mạng, và mạng thì không bảo đảm gì cả. Kiểm biên ở đây chứ không tin:
                // server hiện tại đúng, nhưng một server phiên bản khác thì chưa chắc.
                if (slot.Slot < 0 || slot.Slot >= SLOT_COUNT)
                    continue;

                _slots[slot.Slot] = slot;
                changed[i] = slot.Slot;
            }

            OnSlotsChanged?.Invoke(changed);
        }
    }
}
```

</details>

---

## Ba thử nghiệm bắt buộc

**1. Client nói dối.**
Sửa tạm `InventoryApi` gửi lần lượt: `Slot = -1`, `Slot = 999`, `Quantity = -5`, `ItemUse` lên một ô
trống, `ItemMove` từ ô trống sang ô trống. Sau mỗi lần: server phải **vẫn sống**, log Warn, và người
chơi không nhận thêm/mất đi gì.

Nếu có lần nào server ném exception trong luồng tick thì bạn vừa tìm ra một cách để bất kỳ ai **làm đứng
cả thế giới** bằng một gói tin. `GameLoop` nuốt lỗi để một tick hỏng không giết nhịp tim, nên triệu
chứng sẽ không phải là crash — nó là "mọi người đứng im một nhịp", mỗi lần kẻ kia bấm nút.

**2. Đo cái giá của cache.**
Gõ `G` nhận đồ, rồi **kill server ngay** (Task Manager, không phải Ctrl+C — Ctrl+C có đường dọn sạch đi
qua `LeaveWorldAsync`). Vào lại: đồ mất.

Đó **không phải bug**. Đó là hoá đơn của quyết định "cache RAM + dirty flag", và bạn vừa nhìn thấy nó.
Giờ hạ chu kỳ autosave xuống 5 giây và làm lại — mất ít hơn, ghi DB nhiều hơn. Không có con số nào đúng
tuyệt đối; có một cái cần (đo bằng giây mất mát) và một cái trả (đo bằng lượt ghi/giây).

**3. Snapshot đắt hơn delta bao nhiêu — thật sự.**
Log số byte của mỗi gói gửi đi. Nhận một bình máu: delta = 1 ô. Rồi sửa tạm server gửi snapshot thay cho
delta và đo lại.

Con số sẽ nhỏ đến mức buồn cười, và **đó là kết luận đúng**: ở quy mô này delta không mua được băng
thông đáng kể. Nó mua thứ khác — thông tin "ô nào vừa đổi", thứ mà UI cần và snapshot không có. Biết
một tối ưu **không** mua được cái gì thì mới biết vì sao mình làm nó.

---

## Troubleshooting

| Triệu chứng | Nguyên nhân thường gặp | Chỗ sửa |
|---|---|---|
| Mở túi thấy trống dù server nói có đồ | quên `builder.Register<InventoryNetHandler>()...As<INetHandlerGroup>()` | `GameLifetimeScope` — xem `CLAUDE.md` bước 6 |
| Panel không tự cập nhật khi đang mở | panel subscribe `OnSlotsChanged` trong `Awake` nhưng model bắn event trước khi panel tồn tại | subscribe trong `OnEnable`, và vẽ lại toàn bộ một lần ngay sau đó |
| Nhặt một cái là chiếm một ô mới dù ô cũ còn chỗ | `TryAdd` chạy vòng "ô trống" trước vòng "dồn chồng" | thứ tự hai vòng trong `TryAdd` |
| Nhận đồ khi túi gần đầy thì mất phần thừa | `TryAdd` nhận một phần thay vì all-or-nothing | kiểm `remaining > 0` **trước** khi ghi vào `_slots` |
| Icon không hiện | `IconKey` trỏ sai, hoặc file không nằm dưới một thư mục `Resources/` | đối chiếu `items.json` với cây thư mục |
| Số lượng hiển thị nhảy về giá trị cũ | UI đang tự sửa model rồi bị delta của server ghi đè | bỏ mọi phép ghi vào model ngoài `ApplySnapshot`/`ApplyDelta` |
| Logout xong vào lại mất đồ | `LeaveWorldAsync` chưa gọi lưu túi, hoặc gọi **sau** `Despawn` (entity đã bị gỡ) | `CharacterService.LeaveWorldAsync` — lưu **trước** khi bỏ entity |
| `SQLite Error 19: UNIQUE constraint failed` lúc lưu | hai dòng cùng `(character_id, slot)` — mảng RAM có hai ô cùng số thứ tự | `SaveAsync` phải xoá sạch trong **cùng transaction** với phần ghi |
| Mọi người đứng im một nhịp mỗi lần ai đó bấm nút túi | handler ném exception trong luồng tick | thêm phép kiểm biên; xem thử nghiệm 1 |
| Đồ của tài khoản này hiện trong túi tài khoản kia | `LoadAsync` quên `WHERE character_id = $id` | `InventoryRepository` |
| Server boot báo `Hai item cùng TemplateId` | đúng chủ đích — bảng tự mâu thuẫn | `Config/items.json` |

---

## Tự kiểm tra hiểu bài

**Câu 1.** `itemId` và `templateId` — nêu một tính năng cụ thể mà nếu chỉ có một trong hai thì không làm
được.
<details>
<summary><b>📖 Đáp án câu 1</b></summary>

Đồ có **thuộc tính riêng**: cường hoá +7, đá gắn, độ bền, người chế tạo. Hai cây kiếm cùng `templateId`
nhưng là hai vật khác nhau, và cái phân biệt chúng chỉ có thể là `itemId`.

Ngược lại, bỏ `templateId` đi thì mỗi vật phải mang theo bản sao của tên, icon, `MaxStack` — tức là chép
bảng loại B vào từng dòng dữ liệu người chơi, và ngày bạn sửa tên một món đồ thì phải sửa hàng triệu
dòng.

Ranh giới đằng sau chính là ranh giới của Phase 12 câu 8: `templateId` trỏ vào **dữ liệu game design**,
`itemId` là **dữ liệu người chơi**.

</details>

**Câu 2.** Vì sao ba hàm `Try*` trả về *danh sách ô đã đổi* thay vì `bool`?
<details>
<summary><b>📖 Đáp án câu 2</b></summary>

Vì thông tin "ô nào vừa đổi" **sinh ra ở đó** và **được cần ở hai tầng trên**: tầng ⑤ gửi delta, tầng ⑥
vẽ lại đúng ô đó và chạy hiệu ứng trên nó. Trả `bool` là vứt thông tin đi ở chỗ nó rẻ nhất, rồi bắt tầng
trên tính lại ở chỗ nó đắt nhất và thiếu dữ liệu nhất.

Luật chung: **hàm trả về thứ nó biết, không phải thứ chỗ gọi đầu tiên hỏi.** Danh sách rỗng đã đủ diễn
đạt "thất bại", nên không mất gì cả.

</details>

**Câu 3.** Bấm "dùng", UI không giảm số ngay mà chờ server. Vì sao chấp nhận độ trễ ở đây trong khi
Phase 6 lại dự đoán trước cho di chuyển?
<details>
<summary><b>📖 Đáp án câu 3</b></summary>

Vì **cái giá của việc đoán sai** khác nhau, không phải vì kỹ thuật khác nhau.

Di chuyển: 20 lần mỗi giây, và không đoán thì nhân vật nhích theo nhịp RTT — không chơi được. Nên phải
đoán, và phải trả tiền: reconciliation + replay + render offset, gần như cả Phase 6 và 8.

Túi đồ: vài lần mỗi phút, và độ trễ 50ms không ai nhận ra. Đoán sai thì món đồ "hiện ra rồi biến mất" —
trông y hệt một vụ mất đồ, và mất đồ là thứ người chơi không tha thứ.

Luật: **optimistic update đáng làm khi thao tác dày đặc và hậu quả của sai là thẩm mỹ; không đáng khi
thao tác thưa và hậu quả của sai là tài sản.**

</details>

**Câu 4.** `TryAdd` từ chối trọn thay vì nhận một phần. Nêu một tình huống mà "nhận một phần" nghe hợp
lý hơn — và cách xử lý đúng cho tình huống ấy.
<details>
<summary><b>📖 Đáp án câu 4</b></summary>

Đánh quái xong, quái rơi 10 món mà túi chỉ còn chỗ cho 6. Từ chối trọn thì người chơi không nhận được gì
— khó chịu và vô lý.

Nhưng cách chữa **không phải** là cho `TryAdd` nhận một phần. Nó là: chỗ gọi hỏi trước bằng một hàm
`CountFreeSpaceFor(templateId)`, rồi tự chia — 6 món vào túi, 4 món **rơi lại xuống đất** thành entity
của Phase 15. Người chơi thấy rõ chuyện gì xảy ra, và không có gì bốc hơi.

Điểm chung: quyết định "phần thừa đi đâu" thuộc về chỗ **có bối cảnh**, không thuộc về cái túi. Cái túi
chỉ được trả lời có hoặc không.

</details>

**Câu 5.** Cache RAM + dirty flag đánh đổi cái gì lấy cái gì? Kịch bản nào làm đánh đổi này trở thành
sai lầm?
<details>
<summary><b>📖 Đáp án câu 5</b></summary>

Đổi **độ bền khi server chết** lấy **số lượt ghi DB**. Mỗi thao tác một transaction là đúng tuyệt đối
nhưng không chịu nổi tải; lưu theo chu kỳ thì mất tối đa một chu kỳ khi server chết đột ngột.

Nó thành sai lầm khi món đồ **có giá trị ngoài game** — giao dịch giữa người chơi, đồ mua bằng tiền
thật. Ở đó, mất 30 giây thao tác không phải khó chịu mà là mất tiền của người khác, và những đường ấy
phải **ghi thẳng, trong transaction, trước khi báo thành công**.

Nghĩa là: một game thật có **hai chế độ lưu**, và biết thao tác nào thuộc chế độ nào quan trọng hơn là
chọn được "chế độ đúng".

</details>

**Câu 6.** Bảng item nên nằm ở `Shared` hay ở `GameServer`? Trả lời kèm cách kiểm chứng.
<details>
<summary><b>📖 Đáp án câu 6</b></summary>

`Shared`. Cách kiểm chứng là hỏi đúng một câu — *client có cần dữ liệu này để làm việc của nó không?* —
và câu trả lời là có: nó vẽ tên, icon, mô tả, và nó cần `MaxStack` để biết một ô đầy chưa.

Đó là định nghĩa của **loại B** ở Phase 12. Đối chiếu ngược lại: `ServerConfigData.AoiRadiusX` thì client
không cần và không nên biết, nên nó ở lại `GameServer`.

Lưu ý phần dễ nhầm: "ở `Shared`" nói về **schema** (định nghĩa kiểu), không phải về **dữ liệu**. Dữ liệu
vẫn có đúng một bản gốc — `Config/items.json` cạnh server — và client nhận nó qua dây, không đọc file.

</details>

**Câu 7.** Ở tầng ⑤ có năm phép kiểm trên dữ liệu client gửi lên. Bỏ phép kiểm biên `slot` thì hậu quả
cụ thể là gì, và vì sao nó tệ hơn "một người chơi gian lận"?
<details>
<summary><b>📖 Đáp án câu 7</b></summary>

`_slots[999]` ném `IndexOutOfRangeException` **trong luồng tick**. `GameLoop` bắt exception để một tick
hỏng không giết nhịp tim server, nên không có crash — cả thế giới chỉ **đứng im một nhịp**: không ai được
tích phân, không gói `MoveState`/`WorldSnapshot` nào được gửi.

Tệ hơn gian lận vì nó ảnh hưởng **mọi người**, không chỉ kẻ gửi. Một gói tin của một người làm giật cả
server, và triệu chứng ("thỉnh thoảng game khựng") không chỉ về phía nguyên nhân chút nào.

Bài học: ranh giới tin cậy không nằm ở "người chơi có xấu không" mà ở **"dữ liệu này đến từ đâu"**. Đến
từ mạng thì kiểm, hết.

</details>

**Câu 8.** Snapshot gửi một lần rồi delta từ đó về sau. Chuyện gì xảy ra nếu một gói delta bị **mất**,
và vì sao dự án này không cần lo?
<details>
<summary><b>📖 Đáp án câu 8</b></summary>

Client sẽ lệch **vĩnh viễn** cho tới lần snapshot kế tiếp — đó là tính chất cố hữu của mọi hệ delta: nó
giả định **mọi bản tin đều tới, và tới đúng thứ tự**.

Dự án này không cần lo vì transport là **TCP** (Phase 1): TCP đã bảo đảm đúng hai tính chất ấy. Nếu một
ngày đổi sang UDP cho gói di chuyển thì `InventoryDelta` **không được đi cùng đường đó** — nó phải ở kênh
tin cậy, hoặc phải mang số thứ tự để client phát hiện lỗ hổng và xin snapshot lại.

Đây là chỗ đáng nhớ rằng một quyết định ở Phase 1 vẫn đang đỡ cho thiết kế ở Phase 13 — và rằng đổi
transport thì phải rà lại mọi thứ đang âm thầm dựa vào nó.

</details>

---

## Để dành (ghi lại, chưa làm)

- **Item có thuộc tính riêng** (cường hoá, đá gắn, độ bền). Thêm một cột `attributes TEXT` dạng JSON vào
  `inventory_item` là đủ cho quy mô này; `itemId` đã sẵn sàng làm khoá cho nó.
- **Giao dịch giữa hai người chơi.** Đây là chỗ "cache RAM + dirty flag" phải nhường chỗ cho ghi thẳng
  có transaction — và là bài học về **hai chế độ lưu** ở câu 5.
- **Đồ rơi dưới đất** (`WorldItem` là entity của server, có vị trí, có AOI, có thời hạn). Phase 15, và
  nó là chỗ tiêu thụ phần thừa của `TryAdd` ở câu 4.
- **Sắp xếp túi / dồn chồng tự động.** Một nút, một hàm ở tầng ④, và nó trả về... danh sách ô đã đổi. Cả
  đường ống còn lại không phải sửa gì — đó là phần thưởng của việc chọn đúng kiểu trả về hôm nay.
- **Log giao dịch** (`item_log`): ai nhận gì lúc nào. Bắt buộc khi có giá trị thật, và nó là lý do sẽ
  phải bỏ cách lưu "xoá sạch rồi ghi lại".
- **Panel túi dùng object pool** (`com.hungnt.objectpool`) cho 30 ô. Hôm nay 30 ô dựng một lần là xong;
  đáng làm khi có nhiều trang túi hoặc danh sách cửa hàng dài.

---

**Xong Phase 13.** Bạn vừa đi trọn một feature qua sáu tầng, và khuôn ấy không đổi nữa: Phase 14 là chỉ
số nhân vật, Phase 15 là quái và sát thương, Phase 16 là chat — cả ba đi đúng con đường này, chỉ khác
nội dung.

[PHASE-14](PHASE-14.md) lấy luôn cái túi vừa dựng làm nguyên liệu: `Kind = Equipment` từ hôm nay chỉ nằm
im trong túi, ngày mai mặc được — và mặc vào là **cả bộ chỉ số tính lại**, bằng một pipeline mà client
không có quyền tham gia.
