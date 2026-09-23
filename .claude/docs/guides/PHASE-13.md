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

> **Viết lại 2026-09-22.** Bản trước có 845 dòng nhưng chỉ **6 khối code** — nó tả rất kỹ *vì sao*
> rồi bỏ trống gần hết phần *làm thế nào*: `Inventory` chỉ có mỗi `TryAdd`, `InventoryService` không
> có dòng nào, cả tầng ⑤ và ⑥ chỉ có một class client. Làm theo nó là kẹt ở Bước 2.
>
> Bản này có **đủ code cho từng dòng trong bảng file dưới đây**, và toàn bộ code đã được biên dịch
> thật trước khi đưa vào doc. Tên class cũng đã đổi theo quy ước chốt ở Phase 12:
> `ItemTemplate` → `ItemConfig`, `ItemTemplates` → `ItemConfigContainer` (xem `CONVENTIONS.md` §2).

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

## Danh sách file — tạo gì, sửa gì

Gạch từng dòng khi xong. **Mỗi dòng ở đây có một khối code tương ứng trong foldout lời giải của bước
đó** — thấy một dòng không có code là doc hỏng, báo lại.

**Bước 1 — bảng item (loại B thứ ba)**

| File | Việc |
|---|---|
| `Assets/Game/Resources/Config/items.json` | 🆕 tạo (+ file `.meta`) |
| `Server/Shared/World/Item/ItemKind.cs` | 🆕 tạo |
| `Server/Shared/World/Item/ItemConfig.cs` | 🆕 tạo |
| `Server/Shared/World/Item/ItemTableData.cs` | 🆕 tạo |
| `Server/Shared/World/Item/ItemConfigContainer.cs` | 🆕 tạo |
| `Server/GameServer/Config/ConfigService.cs` | ✏️ **một dòng** `LoadTable<ItemTableData>` + hàm `ValidateItems` |
| *(không đụng `EnterWorldResponse`)* | bảng không đi trên dây — contract không đổi một dòng |
| `Server/Shared/World/ConfigFiles.cs` | ✏️ thêm hằng `ITEMS` |
| `Assets/Game/Scripts/Config/ConfigService.cs` | ✏️ **một dòng** `LoadTable<ItemTableData>(ConfigFiles.ITEMS, ItemConfigContainer.Load)` |

**Bước 2 — DB → RAM server**

| File | Việc |
|---|---|
| `Server/DBServer/Data/Migrator.cs` | ✏️ thêm migration `(4, ...)` |
| `Server/Shared/Dto/Db/InventoryDbDto.cs` | 🆕 tạo |
| `Server/Shared/Db/DbCmd.cs` | ✏️ thêm dải Inventory 1300–1399 |
| `Server/DBServer/Repositories/InventoryRepository.cs` | 🆕 tạo |
| `Server/DBServer/Handlers/InventoryDbHandler.cs` | 🆕 tạo |
| `Server/DBServer/Program.cs` | ✏️ gán `InventoryDbHandler.Repository` |
| `Server/GameServer/World/Inventory.cs` | 🆕 tạo (`ItemStack` + `Inventory`) |
| `Server/GameServer/World/InventoryService.cs` | 🆕 tạo |
| `Server/GameServer/World/PlayerEntity.cs` | ✏️ thêm property `Inventory` |
| `Server/GameServer/World/WorldService.cs` | ✏️ nhận `InventoryService`, gọi `Tick` ở vòng 0b |
| `Server/GameServer/World/CharacterService.cs` | ✏️ nạp túi khi vào world, lưu khi rời |
| `Server/GameServer/Boot/ServerBootstrap.cs` | ✏️ **đăng ký `InventoryService`**, trước `WorldService` |
| `Server/GameServer/Program.cs` | ✏️ phím `G` phát đồ |

**Bước 3 — contract và UI**

| File | Việc |
|---|---|
| `Server/Shared/Net/NetCmd.cs` | ✏️ thêm dải Inventory 400–499 |
| `Server/Shared/Dto/Inventory/InventoryDto.cs` | 🆕 tạo |
| `Server/GameServer/Handlers/InventoryHandler.cs` | 🆕 tạo |
| `Assets/Game/Scripts/Inventory/InventoryModel.cs` | 🆕 tạo |
| `Assets/Game/Scripts/Inventory/InventoryApi.cs` | 🆕 tạo |
| `Assets/Game/Scripts/Inventory/InventoryPresenter.cs` | 🆕 tạo |
| `Assets/Game/Scripts/Inventory/InventoryPanel.cs` | 🆕 tạo |
| `Assets/Game/Scripts/Inventory/InventorySlotView.cs` | 🆕 tạo |
| `Assets/Game/Scripts/Network/Handlers/InventoryNetHandler.cs` | 🆕 tạo |
| `Assets/Game/Scripts/Boot/GameLifetimeScope.cs` | ✏️ **đăng ký 5 thứ** — dòng dễ quên nhất |

---

## Bước 1 — Bảng item: loại B thứ ba

### Hướng làm

Không có gì mới về cơ chế — làm y hệt bảng nhân vật ở Phase 12, và đó chính là điều đáng nói: khuôn đã
dựng xong thì bảng thứ ba tốn một buổi thay vì một phase.

**`Assets/Game/Resources/Config/items.json`** — trong Resources của client, csproj copy sang server:

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

**Đường đi của bảng** giống hệt bảng nhân vật, và đó là toàn bộ điểm đáng nói: khuôn đã dựng xong ở
Phase 12 thì bảng thứ ba không phát minh gì cả.

```
Assets/Game/Resources/Config/items.json
   ├─► client đọc thẳng (lúc khởi động)  ──► ItemConfigContainer.Load
   └─► csproj copy ──► Data/Config/ ──► server đọc ──► ItemConfigContainer.Load
                                              └─► vân tay in ra log (KHÔNG đi trên dây)
```

Vân tay do `ConfigFingerprint.Of(table)` tính — **không** viết `Checksum()` cho bảng item. Nó băm byte
đã tuần tự hoá, tức là băm đúng những trường `[MemoryPackable]` ghi ra, kể cả `Name` và `IconKey`.

> Trước đây doc này nói "chỉ băm thứ đổi hành vi, bỏ qua Description". Lập luận ấy đúng cho **chế độ
> gửi cả bảng** — ở đó một thay đổi cosmetic không nên chặn client cũ. Ở chế độ hai bản thì ngược
> lại: hai file **phải** giống hệt nhau, nên một khác biệt ở `Description` nghĩa là bạn đã sửa một
> bên và quên build bên kia. Báo lệch là đúng.
>
> Bài học: **"băm cái gì" phụ thuộc vào "hai bản được phép khác nhau tới đâu"**, không phải vào bản
> thân dữ liệu.

### ✅ CHECKPOINT A

1. Server boot in: `Bảng items.json: 2 dòng, version 1, checksum XXXXXXXX`.
2. Client vào world, log ra đúng checksum đó.
3. Thêm một item vào `items.json`, restart server, **client không build lại** → client log checksum mới.
4. Ghi `"Kind": "Vuqua"` → server báo lỗi rõ ràng, không im lặng bỏ qua.
5. Hai dòng cùng `TemplateId` → server **từ chối bảng** và la lớn. (Khác `Kind` sai ở chỗ: một id trùng
   thì không có "giá trị mặc định an toàn" nào để lùi về — bảng đã tự mâu thuẫn.)

<details>
<summary><b>📖 Lời giải — bộ ba <code>ItemKind</code> / <code>ItemConfig</code> / <code>ItemTableData</code> / <code>ItemConfigContainer</code></b></summary>

Bốn file, mỗi file một class — `CONVENTIONS.md` §3: một class một file.

**`Server/Shared/World/Item/ItemKind.cs`** (file mới, nguyên văn):

```csharp
namespace MMORPG.Shared.World.Item
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
}
```

**`Server/Shared/World/Item/ItemConfig.cs`** (file mới, nguyên văn) — bản của Phase 13;
Phase 14 thêm `EquipSlot` + `Bonuses`:

```csharp
using MemoryPack;

namespace MMORPG.Shared.World.Item
{
    /// <summary>
    /// Định nghĩa một LOẠI đồ. Bất biến trong suốt phiên chạy: nó là dữ liệu game design, không phải
    /// trạng thái người chơi.
    /// </summary>
    [MemoryPackable]
    public sealed partial class ItemConfig
    {
        /// <summary>Khoá của bảng. Một món đồ trong túi trỏ về đây bằng số này.</summary>
        public int TemplateId { get; set; }

        /// <summary>Tên hiển thị cho người chơi.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Mô tả hiển thị cho người chơi.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Khoá tài nguyên của icon — THUẦN client, server không đọc dòng này bao giờ. Vẫn để chung
        /// bảng vì tách ra là có hai bảng phải khớp nhau theo <see cref="TemplateId"/>.
        /// </summary>
        public string IconKey { get; set; } = string.Empty;

        /// <summary>Quyết định món đồ dùng được hay chỉ để bán, và nằm ở nhóm nào trong túi.</summary>
        public ItemKind Kind { get; set; }

        /// <summary>Tối đa bao nhiêu cái trong một ô. 1 nghĩa là không xếp chồng.</summary>
        public int MaxStack { get; set; } = 1;
    }
}
```

**`Server/Shared/World/Item/ItemTableData.cs`** (file mới, nguyên văn) — chú ý nó **không có**
`Checksum()`: vân tay do `ConfigFingerprint` lo, một hàm cho mọi bảng:

```csharp
using System;
using MemoryPack;
using Newtonsoft.Json;

namespace MMORPG.Shared.World.Item
{
    /// <summary>
    /// Cả bảng item, bản đối chiếu 1-1 với <c>items.json</c>. Không đi trên dây: mỗi bên đọc file
    /// của chính nó, và vân tay chỉ để in ra log (<see cref="ConfigFingerprint"/>).
    /// </summary>
    [MemoryPackable]
    public sealed partial class ItemTableData : IConfigFile
    {
        /// <summary>Phiên bản schema, nằm trong file.</summary>
        public int Version { get; set; } = 1;

        /// <summary>Mỗi phần tử là một loại đồ.</summary>
        public ItemConfig[] Items { get; set; } = Array.Empty<ItemConfig>();

        /// <summary>Đếm ra từ <see cref="Items"/>, nên không tuần tự hoá ở cả hai bộ.</summary>
        [MemoryPackIgnore] [JsonIgnore] public int RowCount => Items.Length;
    }
}
```

**`Server/Shared/World/Item/ItemConfigContainer.cs`** (file mới, nguyên văn):

```csharp
using System;
using System.Collections.Generic;

namespace MMORPG.Shared.World.Item
{
    /// <summary>
    /// Bảng tra template item theo id. Cùng khuôn với <see cref="Character.CharacterConfigContainer"/>:
    /// bảng tĩnh NẠP ĐƯỢC, và cả hai bên đều nạp từ file của chính mình.
    /// </summary>
    public static class ItemConfigContainer
    {
        /// <summary>Bảng đang chạy. Chỉ <see cref="Load"/> được gán vào nó.</summary>
        private static Dictionary<int, ItemConfig> _byId = new();

        /// <summary>Số loại đồ trong bảng đang chạy.</summary>
        public static int Count
        {
            get { return _byId.Count; }
        }

        /// <summary>
        /// Template của một id, hoặc null. Trả null chứ không ném vì nguồn của id khác
        /// <c>CharacterConfigContainer.Get</c>: templateId đến từ DB và có thể là đồ của một bảng cũ
        /// hoặc item đã bị gỡ. Cách xử lý đúng cho một dòng như vậy là bỏ qua món đồ đó, không phải
        /// chặn người chơi vào game.
        /// </summary>
        public static ItemConfig Find(int templateId)
        {
            return _byId.TryGetValue(templateId, out ItemConfig config) ? config : null;
        }

        /// <summary>Thay cả bảng. Dựng nguyên bảng mới rồi mới gán — cùng lý do như bảng nhân vật.</summary>
        public static void Load(ItemTableData table)
        {
            if (table == null)
                throw new ArgumentNullException(nameof(table));

            var built = new Dictionary<int, ItemConfig>();

            foreach (ItemConfig config in table.Items)
            {
                // Trùng id thì không có giá trị mặc định nào để lùi về: bảng đã tự mâu thuẫn, và cái
                // nào thắng là chuyện của thứ tự dòng trong file.
                if (built.ContainsKey(config.TemplateId))
                    throw new InvalidOperationException($"Hai item cùng TemplateId {config.TemplateId}: \"{built[config.TemplateId].Name}\" và \"{config.Name}\".");

                built[config.TemplateId] = config;
            }

            _byId = built;
        }
    }
}
```

</details>

<details>
<summary><b>📖 Lời giải — nối bảng item vào đường ống (4 dòng)</b></summary>

Đây là phần thưởng của Bước 3 Phase 12: bảng thứ ba tốn **bốn dòng** cộng một hàm `Validate`.
Đây là phần thưởng của Bước 3 Phase 12: bảng thứ ba tốn **bốn dòng** cộng một hàm `Validate`.
Và không phải đụng vào `EnterWorldResponse`: bảng không đi trên dây, nên bảng thứ tư và thứ năm
cũng sẽ không đụng tới một dòng nào của contract.

**`Server/Shared/World/ConfigFiles.cs`** — một hằng:

```csharp
        public const string ITEMS = "items";
```

**`Server/GameServer/Config/ConfigService.cs`** — một dòng trong `Load()`:

```csharp
        public void Load()
        {
            LoadFile<GameConfigData>(GAME, ValidateGame, ApplyGame, WhenBroken.UseDefaults);

            LoadTable<CharacterTableData>(ConfigFiles.CHARACTERS, ValidateCharacters, CharacterConfigContainer.Load);
            LoadTable<ItemTableData>(ConfigFiles.ITEMS, ValidateItems, ItemConfigContainer.Load);
        }
```

Và hàm kiểm — ngắn hơn `ValidateCharacters` nhiều, vì bảng item có ít con số đi vào mô phỏng hơn:

```csharp
        /// <summary>
        /// Kẹp bảng item. Ít trường hơn bảng nhân vật rất nhiều, và đó là điều bình thường — hàm
        /// Validate dài bao nhiêu là tuỳ bảng có bao nhiêu con số đi vào mô phỏng.
        /// </summary>
        private static void ValidateItems(ItemTableData table)
        {
            foreach (ItemConfig config in table.Items)
            {
                string tag = $"item {config.TemplateId}";

                // TemplateId <= 0 không kẹp được về mặc định nào có nghĩa: id là KHOÁ, và một khoá
                // sai thì cả dòng vô dụng. Ném để người sửa bảng biết ngay, cùng cách với trùng id.
                if (config.TemplateId <= 0)
                    throw new InvalidOperationException($"{tag}: TemplateId phải > 0.");

                if (string.IsNullOrWhiteSpace(config.Name))
                    throw new InvalidOperationException($"{tag}: thiếu Name.");

                // MaxStack = 0 nghĩa là "không bỏ vào túi được cái nào" — không phải một lựa chọn
                // thiết kế, mà là một ô bị bỏ trống trong file. Trần 9999 để một số gõ nhầm không
                // biến thành ô túi chứa cả kho.
                config.MaxStack = (int)Clamp(config.MaxStack, 1f, 9999f, 1f, $"{tag}.MaxStack");
            }
        }
```

**`Assets/Game/Scripts/Config/ConfigService.cs`** — một dòng trong `LoadTables()`, và **đây là dòng
dễ quên nhất của Bước 1**: quên nó thì server hoàn toàn bình thường, còn client có `Find(templateId)`
trả **null** cho mọi món đồ — túi đồ hiện ra trống trơn hoặc toàn ô không tên, không exception nào,
không lỗi biên dịch nào. Đây đúng là kiểu hỏng câm mà cả Phase 12 dựng lên để chống.

```csharp
        /// <summary>
        /// <b>Thêm bảng mới thì thêm đúng một dòng ở đây.</b>
        ///
        /// Nạp hết lúc khởi động là lựa chọn của hôm nay, không phải ràng buộc: hai bảng đọc xong
        /// trong vài mili giây. Ngày cần tải lười hoặc tải từ xa thì chỗ phải sửa là
        /// <see cref="LoadTable{TTable}"/>, và không ai khác biết.
        /// </summary>
        private void LoadTables()
        {
            LoadTable<CharacterTableData>(ConfigFiles.CHARACTERS, CharacterConfigContainer.Load);
            LoadTable<ItemTableData>(ConfigFiles.ITEMS, ItemConfigContainer.Load);
        }
```

Nhớ thêm `using MMORPG.Shared.World.Item;` ở đầu cả hai file.

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

-- Một ô chỉ chứa được một chồng. Ràng buộc ở DB chứ không ở code, cùng lý do với
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

**Nguồn item của phase này là một phím trên console server.** `G` → `InventoryService.EnqueueGrantAll(...)`.
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
<summary><b>📖 Lời giải — DB: migration, DTO, <code>DbCmd</code>, repository, handler</b></summary>

**`Server/DBServer/Data/Migrator.cs`** — thêm vào cuối mảng `_migrations`, KHÔNG sửa ba cái trước:

```csharp
            (4, """
                CREATE TABLE inventory_item (
                    id           INTEGER PRIMARY KEY AUTOINCREMENT,
                    character_id INTEGER NOT NULL REFERENCES character(id) ON DELETE CASCADE,
                    template_id  INTEGER NOT NULL,
                    quantity     INTEGER NOT NULL,
                    slot         INTEGER NOT NULL
                );

                -- Một ô chỉ chứa được một chồng. Ràng buộc ở DB chứ không ở code, cùng lý do với
                -- UNIQUE(account_id) của bảng character.
                CREATE UNIQUE INDEX idx_inventory_slot ON inventory_item (character_id, slot);
                """),
```

**`Server/Shared/Dto/Db/InventoryDbDto.cs`** (file mới, nguyên văn):

```csharp
using System;
using MemoryPack;

namespace MMORPG.Shared.Dto.Db
{
    /// <summary>
    /// Một dòng nguyên vẹn của bảng <c>inventory_item</c>. Chỉ đi trên đường nội bộ GameServer ↔ DBServer.
    ///
    /// Hình dạng của CHỖ DỮ LIỆU NẰM, không phải của thứ chạy trong game — thứ chạy là
    /// <c>ItemStack</c> trong <c>Inventory</c>, và nó không có <c>CharacterId</c> vì cả cái túi đã
    /// thuộc về một nhân vật rồi. Cùng mẫu với CharacterRow ≠ PlayerEntity ở Phase 5.
    /// </summary>
    [MemoryPackable]
    public partial class InventoryRow
    {
        /// <summary>Id của MỘT VẬT cụ thể. DB cấp bằng AUTOINCREMENT — xem bảng itemId ≠ templateId.</summary>
        public long ItemId { get; set; }

        public int TemplateId { get; set; }

        public int Quantity { get; set; }

        public int Slot { get; set; }
    }

    [MemoryPackable]
    public partial class InventoryLoadRequest
    {
        public long CharacterId { get; set; }
    }

    [MemoryPackable]
    public partial class InventoryLoadResponse
    {
        public InventoryRow[] Items { get; set; } = Array.Empty<InventoryRow>();
    }

    /// <summary>
    /// Ghi TOÀN BỘ túi. Không có "lưu một ô": xem bảng so sánh ở Bước 2 — xoá sạch rồi ghi lại là
    /// lựa chọn có ý thức, và nó chỉ đúng khi cả cái túi đi cùng nhau trong một transaction.
    /// </summary>
    [MemoryPackable]
    public partial class InventorySaveRequest
    {
        public long CharacterId { get; set; }

        public InventoryRow[] Items { get; set; } = Array.Empty<InventoryRow>();
    }
}
```

**`Server/Shared/Db/DbCmd.cs`** — thêm một region vào cuối enum:

```csharp
        #region Inventory (1300–1399)

        /// <summary>
        /// Đọc cả túi của một nhân vật.
        /// Request: <see cref="Dto.Db.InventoryLoadRequest"/> · Response: <see cref="Dto.Db.InventoryLoadResponse"/>
        /// </summary>
        InventoryLoad = 1300,

        /// <summary>
        /// Ghi TOÀN BỘ túi: xoá sạch rồi ghi lại trong một transaction. Không có "lưu một ô".
        /// Request: <see cref="Dto.Db.InventorySaveRequest"/> · Response: <see cref="Dto.Db.DbOkResponse"/>
        /// </summary>
        InventorySave = 1301,

        #endregion
```

**`Server/DBServer/Repositories/InventoryRepository.cs`** (file mới, nguyên văn):

```csharp
using Microsoft.Data.Sqlite;
using MMORPG.DBServer.Data;
using MMORPG.Shared.Dto.Db;

namespace MMORPG.DBServer.Repositories
{
    /// <summary>
    /// Chỉ SQL, không biết gì về game: không biết túi có bao nhiêu ô, không biết MaxStack là gì,
    /// không biết đồ nào dùng được. Nhờ vậy Phase 20 đổi SQLite sang MySQL chỉ phải sửa tầng này.
    /// </summary>
    public sealed class InventoryRepository
    {
        private readonly Database _database;

        public InventoryRepository(Database database)
        {
            _database = database;
        }

        public async Task<InventoryLoadResponse> LoadAsync(InventoryLoadRequest request, CancellationToken ct = default)
        {
            await using SqliteConnection connection = await _database.OpenAsync(ct);
            await using SqliteCommand command = connection.CreateCommand();

            // ORDER BY slot để chỗ gọi nhận được dãy có thứ tự — không phải vì Inventory cần (nó tra
            // theo chỉ số ô), mà vì log đọc dễ hơn và vì một thứ tự xác định là thứ test dựa vào được.
            command.CommandText = """
                                  SELECT id, template_id, quantity, slot
                                  FROM inventory_item
                                  WHERE character_id = $characterId
                                  ORDER BY slot;
                                  """;
            command.Parameters.AddWithValue("$characterId", request.CharacterId);

            var rows = new List<InventoryRow>();

            await using SqliteDataReader reader = await command.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                rows.Add(new InventoryRow
                {
                    ItemId = reader.GetInt64(0),
                    TemplateId = reader.GetInt32(1),
                    Quantity = reader.GetInt32(2),
                    Slot = reader.GetInt32(3),
                });
            }

            return new InventoryLoadResponse { Items = rows.ToArray() };
        }

        /// <summary>
        /// Ghi TOÀN BỘ túi: xoá sạch rồi ghi lại.
        ///
        /// Trông thô, và nó thô thật — nhưng cách "đúng bài" (diff từng dòng, sinh INSERT/UPDATE/DELETE)
        /// là ~80 dòng với ba nhánh, mỗi nhánh một cách sai. Cái giá của cách này: itemId ĐỔI sau mỗi
        /// lần lưu, vì AUTOINCREMENT cấp số mới. Hôm nay chưa ai dựa vào itemId; ngày có log giao dịch
        /// hoặc đồ khoá theo id thì đây là chỗ phải sửa — và nó là một LỰA CHỌN, không phải sơ suất.
        ///
        /// TRANSACTION là bắt buộc, không phải cẩn thận thừa: giữa DELETE và INSERT mà process chết
        /// thì người chơi mất sạch túi. Một transaction biến "mất sạch" thành "không đổi gì".
        /// </summary>
        public async Task SaveAsync(InventorySaveRequest request, CancellationToken ct = default)
        {
            await using SqliteConnection connection = await _database.OpenAsync(ct);
            await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct);

            await using (SqliteCommand delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM inventory_item WHERE character_id = $characterId;";
                delete.Parameters.AddWithValue("$characterId", request.CharacterId);

                await delete.ExecuteNonQueryAsync(ct);
            }

            await using (SqliteCommand insert = connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText = """
                                     INSERT INTO inventory_item (character_id, template_id, quantity, slot)
                                     VALUES ($characterId, $templateId, $quantity, $slot);
                                     """;

                // Dựng tham số MỘT LẦN rồi chỉ đổi giá trị trong vòng lặp: SQLite chuẩn bị lại câu
                // lệnh mỗi khi tập tham số đổi, và 30 lần chuẩn bị lại cho một lần lưu là lãng phí
                // không có lý do nào.
                insert.Parameters.AddWithValue("$characterId", request.CharacterId);

                SqliteParameter templateId = insert.Parameters.AddWithValue("$templateId", 0);
                SqliteParameter quantity = insert.Parameters.AddWithValue("$quantity", 0);
                SqliteParameter slot = insert.Parameters.AddWithValue("$slot", 0);

                foreach (InventoryRow row in request.Items)
                {
                    templateId.Value = row.TemplateId;
                    quantity.Value = row.Quantity;
                    slot.Value = row.Slot;

                    await insert.ExecuteNonQueryAsync(ct);
                }
            }

            await transaction.CommitAsync(ct);
        }
    }
}
```

**`Server/DBServer/Handlers/InventoryDbHandler.cs`** (file mới, nguyên văn):

```csharp
using MMORPG.DBServer.Net;
using MMORPG.DBServer.Repositories;
using MMORPG.Shared.Db;
using MMORPG.Shared.Dto.Db;

namespace MMORPG.DBServer.Handlers
{
    public static class InventoryDbHandler
    {
        /// <summary>Gán một lần trong <c>Program.cs</c> của DBServer.</summary>
        public static InventoryRepository Repository { get; set; }

        [DbHandler(DbCmd.InventoryLoad)]
        public static async Task<DbResult> OnLoad(DbRequest req)
        {
            return DbResult.Ok(await Repository.LoadAsync(req.GetData<InventoryLoadRequest>()));
        }

        [DbHandler(DbCmd.InventorySave)]
        public static async Task<DbResult> OnSave(DbRequest req)
        {
            await Repository.SaveAsync(req.GetData<InventorySaveRequest>());

            return DbResult.Ok(new DbOkResponse { Success = true });
        }
    }
}
```

**`Server/DBServer/Program.cs`** — một dòng, cạnh ba dòng đã có. ⚠️ **Quên dòng này thì không có
lỗi biên dịch**, chỉ có `NullReferenceException` ở query túi đầu tiên:

```csharp
CharacterDbHandler.Repository = new CharacterRepository(database);
InventoryDbHandler.Repository = new InventoryRepository(database);
```

</details>

<details>
<summary><b>📖 Lời giải — <code>Inventory</code>: cả class, bốn thao tác</b></summary>

**`Server/GameServer/World/Inventory.cs`** (file mới, nguyên văn):

```csharp
using MMORPG.Shared.Dto.Db;
using MMORPG.Shared.Dto.Inventory;
using MMORPG.Shared.World.Item;

namespace MMORPG.GameServer.World
{
    /// <summary>Một ô túi trong RAM server. Struct: 30 ô là một mảng liền kề, không phải 30 object.</summary>
    public struct ItemStack
    {
        /// <summary>Id của một VẬT cụ thể. 0 khi món đồ vừa sinh ra và chưa qua lần lưu nào.</summary>
        public long ItemId;

        /// <summary>0 = ô TRỐNG. Dùng chính trường này làm dấu "trống" nên không cần một cờ riêng.</summary>
        public int TemplateId;

        public int Quantity;

        public bool IsEmpty
        {
            get { return TemplateId == 0; }
        }
    }

    /// <summary>
    /// Túi đồ của MỘT nhân vật, sống trong RAM server. Nạp từ DB một lần lúc vào world, đổi trong
    /// RAM, ghi xuống DB khi rời world hoặc khi autosave thấy <see cref="IsDirty"/>.
    ///
    /// <b>CHỈ LUỒNG TICK được chạm vào.</b> Không có khoá nào ở đây, và đó là cố ý: mọi lối vào đều
    /// đi qua <see cref="InventoryService"/>, và service ấy xếp lệnh vào hàng đợi để tick tiêu thụ.
    /// Thêm một lối vào từ luồng khác là phải thêm khoá vào tất cả — rẻ hơn nhiều là đừng thêm.
    ///
    /// Mọi thao tác trả về <b>danh sách ô vừa đổi</b>, không phải bool: tầng gửi gói cần đúng thứ đó
    /// để dựng delta, và tầng UI cần đúng thứ đó để vẽ lại. Trả bool rồi để chỗ gọi tự đoán đã đổi ô
    /// nào là mời nó đoán sai.
    /// </summary>
    public sealed class Inventory
    {
        public const int SLOT_COUNT = 30;

        private readonly ItemStack[] _slots = new ItemStack[SLOT_COUNT];

        // Bộ đệm của TryAdd, giữ làm field và Clear() mỗi lần dùng — cùng lý do với ba bộ đệm của
        // vòng tick trong WorldService: thứ chạy thường xuyên thì hình dạng bộ nhớ của nó là thiết kế.
        private readonly List<(int Slot, int Amount)> _scratch = new();

        /// <summary>
        /// Id tạm cho đồ vừa sinh ra, đếm LÙI từ -1. Âm để không bao giờ đụng id thật của DB
        /// (AUTOINCREMENT luôn dương), nên nhìn một con số là biết nó đã qua DB hay chưa.
        /// </summary>
        private long _nextTempId = -1;

        /// <summary>Có thay đổi chưa được ghi xuống DB không. Autosave đọc cờ này.</summary>
        public bool IsDirty { get; private set; }

        public int UsedSlots
        {
            get
            {
                int count = 0;

                for (int i = 0; i < _slots.Length; i++)
                {
                    if (!_slots[i].IsEmpty)
                        count++;
                }

                return count;
            }
        }

        /// <summary>Nạp từ DB. Bỏ qua dòng hỏng thay vì ném — xem ghi chú trong thân hàm.</summary>
        public void Load(InventoryRow[] rows)
        {
            System.Array.Clear(_slots, 0, _slots.Length);

            foreach (InventoryRow row in rows)
            {
                // Ba loại dòng hỏng, và cả ba đều BỎ QUA chứ không ném: ô ngoài phạm vi (túi từng
                // rộng hơn rồi bị thu lại), template không còn trong bảng (item bị gỡ), số lượng vô
                // nghĩa. Ném ở đây nghĩa là một dòng DB rác chặn hẳn người chơi vào game — và người
                // chơi thì không sửa được dòng đó.
                if (row.Slot < 0 || row.Slot >= SLOT_COUNT)
                    continue;

                if (row.Quantity <= 0 || ItemConfigContainer.Find(row.TemplateId) == null)
                    continue;

                _slots[row.Slot] = new ItemStack
                {
                    ItemId = row.ItemId,
                    TemplateId = row.TemplateId,
                    Quantity = row.Quantity,
                };
            }

            // Vừa nạp từ DB thì RAM và DB đang khớp nhau — nếu để dirty thì autosave đầu tiên ghi lại
            // đúng thứ vừa đọc lên, 30 lượt ghi không có lý do.
            IsDirty = false;
        }

        /// <summary>Kết xuất để ghi DB. Chỉ ô có đồ — ô trống không cần một dòng để nói rằng nó trống.</summary>
        public InventoryRow[] ToRows()
        {
            var rows = new List<InventoryRow>(UsedSlots);

            for (int slot = 0; slot < _slots.Length; slot++)
            {
                if (_slots[slot].IsEmpty)
                    continue;

                rows.Add(new InventoryRow
                {
                    ItemId = _slots[slot].ItemId,
                    TemplateId = _slots[slot].TemplateId,
                    Quantity = _slots[slot].Quantity,
                    Slot = slot,
                });
            }

            return rows.ToArray();
        }

        /// <summary>Toàn bộ túi cho gói snapshot. Chỉ ô có đồ, cùng lý do với <see cref="ToRows"/>.</summary>
        public InventorySlotDto[] ToSnapshot()
        {
            var slots = new List<InventorySlotDto>(UsedSlots);

            for (int slot = 0; slot < _slots.Length; slot++)
            {
                if (_slots[slot].IsEmpty)
                    continue;

                slots.Add(ToDto(slot));
            }

            return slots.ToArray();
        }

        /// <summary>Một ô dưới dạng gói tin. Ô trống ra <c>TemplateId = 0</c> — client hiểu đó là "xoá ô".</summary>
        public InventorySlotDto ToDto(int slot)
        {
            return new InventorySlotDto
            {
                Slot = slot,
                TemplateId = _slots[slot].TemplateId,
                Quantity = _slots[slot].Quantity,
            };
        }

        public void MarkSaved()
        {
            IsDirty = false;
        }

        //--------------------------------------------------------------------------------------------
        // Bốn thao tác. Tất cả trả về danh sách ô vừa đổi; RỖNG = không làm gì cả.
        //--------------------------------------------------------------------------------------------

        /// <summary>
        /// Nhận đồ vào túi. Trả danh sách ô vừa đổi; RỖNG nghĩa là không nhận được gì.
        ///
        /// Hoặc nhận TRỌN, hoặc từ chối TRỌN. Nhận một phần nghĩa là phần còn lại bốc hơi mà không có
        /// gì báo — và người chơi sẽ không bao giờ tha thứ cho điều đó, kể cả khi nó chỉ là một bình
        /// máu. Phần thừa đi đâu là quyết định của chỗ GỌI, không phải của cái túi.
        /// </summary>
        public IReadOnlyList<int> TryAdd(int templateId, int quantity)
        {
            ItemConfig config = ItemConfigContainer.Find(templateId);

            if (config == null || quantity <= 0)
                return System.Array.Empty<int>();

            // THỬ trên bản nháp trước, chỉ ghi vào thật khi chắc chắn đủ chỗ. Cách còn lại — ghi dần
            // rồi hoàn tác khi hết chỗ — là tự viết một transaction bằng tay, và hoàn tác sai thì
            // người chơi mất đồ.
            _scratch.Clear();
            int remaining = quantity;

            // Vòng 1: dồn vào chồng đã có cùng loại, còn chỗ. Trước vòng 2 có chủ đích — dồn xong mới
            // mở ô mới, nếu không thì mỗi lần nhặt một cái là chiếm thêm một ô trong khi ô cũ còn chỗ.
            for (int slot = 0; slot < _slots.Length && remaining > 0; slot++)
            {
                if (_slots[slot].TemplateId != templateId || _slots[slot].Quantity >= config.MaxStack)
                    continue;

                int take = System.Math.Min(remaining, config.MaxStack - _slots[slot].Quantity);

                _scratch.Add((slot, take));
                remaining -= take;
            }

            // Vòng 2: ô trống.
            for (int slot = 0; slot < _slots.Length && remaining > 0; slot++)
            {
                if (!_slots[slot].IsEmpty)
                    continue;

                int take = System.Math.Min(remaining, config.MaxStack);

                _scratch.Add((slot, take));
                remaining -= take;
            }

            if (remaining > 0)
                return System.Array.Empty<int>();

            var changed = new List<int>(_scratch.Count);

            foreach ((int slot, int amount) in _scratch)
            {
                // Ô trống thì đây là lúc món đồ RA ĐỜI — và ra đời nghĩa là nhận một itemId mới.
                // Id âm tạm thời do server cấp; DB cấp id thật ở lần lưu kế tiếp. Client không bao
                // giờ đọc con số này, nó chỉ cần số ô.
                if (_slots[slot].IsEmpty)
                {
                    _slots[slot].TemplateId = templateId;
                    _slots[slot].ItemId = _nextTempId--;
                    _slots[slot].Quantity = 0;
                }

                _slots[slot].Quantity += amount;
                changed.Add(slot);
            }

            IsDirty = true;

            return changed;
        }

        /// <summary>
        /// Dùng một món ở ô này: giảm đúng một cái. Chỉ đồ <see cref="ItemKind.Consumable"/> —
        /// tác dụng thật (hồi máu) là việc của Phase 14, ở đây mới chỉ có phép trừ.
        /// </summary>
        public IReadOnlyList<int> TryUse(int slot)
        {
            if (!IsValidSlot(slot) || _slots[slot].IsEmpty)
                return System.Array.Empty<int>();

            ItemConfig config = ItemConfigContainer.Find(_slots[slot].TemplateId);

            if (config == null || config.Kind != ItemKind.Consumable)
                return System.Array.Empty<int>();

            return TryRemove(slot, 1);
        }

        /// <summary>Bỏ bớt số lượng ở một ô. Về 0 thì ô thành trống.</summary>
        public IReadOnlyList<int> TryRemove(int slot, int quantity)
        {
            if (!IsValidSlot(slot) || _slots[slot].IsEmpty || quantity <= 0)
                return System.Array.Empty<int>();

            // Bỏ nhiều hơn số đang có: từ chối TRỌN chứ không bỏ hết những gì có. Cùng luật
            // all-or-nothing của TryAdd — một yêu cầu vô nghĩa không được biến thành một hành động
            // gần đúng.
            if (quantity > _slots[slot].Quantity)
                return System.Array.Empty<int>();

            _slots[slot].Quantity -= quantity;

            if (_slots[slot].Quantity == 0)
                _slots[slot] = default;

            IsDirty = true;

            return new[] { slot };
        }

        /// <summary>
        /// Đổi chỗ hai ô. Không dồn chồng — kéo chồng này lên chồng kia cùng loại thì hai ô ĐỔI CHỖ,
        /// không cộng vào nhau.
        ///
        /// Vì sao không dồn: "dồn" và "đổi chỗ" là hai ý định khác nhau của người chơi, và một thao
        /// tác kéo-thả không nói được họ muốn cái nào. Đoán sai thì người chơi mất bố cục túi mà
        /// không hoàn tác được. Nút "sắp xếp túi" là chỗ của phép dồn — xem "Để dành".
        /// </summary>
        public IReadOnlyList<int> TryMove(int from, int to)
        {
            if (!IsValidSlot(from) || !IsValidSlot(to) || from == to)
                return System.Array.Empty<int>();

            // Kéo một ô trống đi đâu cũng là không làm gì. Trả rỗng thì không có gói nào được gửi.
            if (_slots[from].IsEmpty)
                return System.Array.Empty<int>();

            (_slots[from], _slots[to]) = (_slots[to], _slots[from]);

            IsDirty = true;

            return new[] { from, to };
        }

        private static bool IsValidSlot(int slot)
        {
            return slot >= 0 && slot < SLOT_COUNT;
        }
    }
}
```

</details>

<details>
<summary><b>📖 Lời giải — <code>InventoryService</code> và chỗ nối nó vào world</b></summary>

**`Server/GameServer/World/InventoryService.cs`** (file mới, nguyên văn):

```csharp
using System.Collections.Concurrent;
using MMORPG.GameServer.Db;
using MMORPG.ServerCore;
using MMORPG.Shared.Db;
using MMORPG.Shared.Dto.Db;
using MMORPG.Shared.Dto.Inventory;
using MMORPG.Shared.Net;
using MMORPG.Shared.World.Item;

namespace MMORPG.GameServer.World
{
    /// <summary>
    /// Toàn bộ nghiệp vụ túi đồ: nạp lúc vào world, gửi snapshot/delta, autosave, lưu lúc rời world.
    ///
    /// Handler không chứa gì ngoài lời gọi vào đây — nếu có ngày một hàm trong
    /// <c>InventoryHandler</c> dài quá mười dòng thì nghiệp vụ đang rò rỉ ra khỏi chỗ này.
    /// </summary>
    public sealed class InventoryService
    {
        /// <summary>
        /// Bao lâu ghi DB một lần nếu túi bẩn. Đây là con số của một ĐÁNH ĐỔI, không phải một hằng
        /// tuỳ ý: nó là số giây tối đa người chơi mất khi server chết đột ngột, đổi lấy số lượt ghi
        /// DB tiết kiệm được. Hạ xuống 5 thì mất ít hơn, ghi nhiều hơn.
        /// </summary>
        private const float AUTOSAVE_SECONDS = 30f;

        private readonly DbClient _dbClient;

        // Bên GHI là luồng đọc phím, bên ĐỌC là luồng tick. Chỉ hàng đợi này đi qua ranh giới luồng;
        // Inventory thì không ai ngoài tick được chạm vào. Cùng khuôn với _forcedActions ở WorldService.
        private readonly ConcurrentQueue<GrantCommand> _grants = new();

        private float _autosaveTimer;

        public InventoryService(DbClient dbClient)
        {
            _dbClient = dbClient;
        }

        /// <summary>Lệnh phát đồ, xếp hàng chờ tick tiêu thụ.</summary>
        private readonly struct GrantCommand
        {
            public readonly int TemplateId;
            public readonly int Quantity;

            public GrantCommand(int templateId, int quantity)
            {
                TemplateId = templateId;
                Quantity = quantity;
            }
        }

        /// <summary>
        /// Xin phát đồ cho TẤT CẢ người trong world. Gọi được từ luồng bất kỳ — lệnh chỉ được xếp
        /// hàng ở đây, và chỉ thật sự có hiệu lực ở đầu tick kế tiếp.
        /// </summary>
        public void EnqueueGrantAll(int templateId, int quantity)
        {
            _grants.Enqueue(new GrantCommand(templateId, quantity));
        }

        /// <summary>
        /// Nạp túi từ DB và gửi snapshot. Gọi từ <c>CharacterService.EnterWorldAsync</c>, SAU khi
        /// entity đã spawn — snapshot là gói đầu tiên client nhận về túi, và nó phải tới sau
        /// EnterWorldResponse để client đã có bảng item mà tra.
        /// </summary>
        public async Task LoadAsync(PlayerEntity entity)
        {
            try
            {
                var response = await _dbClient.CallAsync<InventoryLoadRequest, InventoryLoadResponse>(
                    DbCmd.InventoryLoad, new InventoryLoadRequest { CharacterId = entity.CharacterId });

                entity.Inventory.Load(response.Items);
            }
            catch (DbUnavailableException ex)
            {
                // Vào world với túi RỖNG thì tệ hơn nhiều so với không vào được: người chơi sẽ tưởng
                // mất đồ, và lần autosave kế tiếp sẽ GHI ĐÈ cái túi rỗng ấy xuống DB — mất thật.
                Log.Error($"Không nạp được túi của {entity.Name.Cyan()}: {ex.Message}. Đá khỏi world.");
                entity.Owner?.Kick("Không đọc được dữ liệu nhân vật. Thử lại sau giây lát.");

                return;
            }

            Log.Info($"Túi của {entity.Name.Cyan()}: {entity.Inventory.UsedSlots}/{Inventory.SLOT_COUNT} ô");

            entity.Owner?.SendData(NetCmd.InventorySnapshot, new InventorySnapshotNotice
            {
                Slots = entity.Inventory.ToSnapshot(),
            });
        }

        /// <summary>
        /// Ghi túi xuống DB nếu bẩn. Gọi từ <c>CharacterService.LeaveWorldAsync</c> TRƯỚC khi
        /// <c>Despawn</c> — sau đó thì entity đã ra khỏi sổ và không ai còn cầm cái túi nữa.
        /// </summary>
        public async Task SaveAsync(PlayerEntity entity)
        {
            if (!entity.Inventory.IsDirty)
                return;

            try
            {
                await _dbClient.CallAsync<InventorySaveRequest, DbOkResponse>(
                    DbCmd.InventorySave, new InventorySaveRequest
                    {
                        CharacterId = entity.CharacterId,
                        Items = entity.Inventory.ToRows(),
                    });

                entity.Inventory.MarkSaved();
            }
            catch (DbUnavailableException ex)
            {
                // Cùng lý do với SavePosition ở Phase 5: mất một lần lưu thì khó chịu, nhưng làm sập
                // đường ngắt kết nối thì tệ hơn — session không dọn được, entity treo lại mãi mãi.
                Log.Warn($"Không lưu được túi của {entity.Name.Cyan()}: {ex.Message}");
            }
        }

        //--------------------------------------------------------------------------------------------
        // Ba thao tác do client xin. Mọi phép kiểm biên đã làm trong Inventory — ở đây chỉ còn việc
        // gửi delta khi có gì đó thật sự đổi.
        //--------------------------------------------------------------------------------------------

        public void Use(PlayerEntity entity, int slot)
        {
            SendDelta(entity, entity.Inventory.TryUse(slot));
        }

        public void Drop(PlayerEntity entity, int slot, int quantity)
        {
            IReadOnlyList<int> changed = entity.Inventory.TryRemove(slot, quantity);

            if (changed.Count > 0)
                Log.Debug($"{entity.Name} vứt {quantity} ở ô {slot}");

            SendDelta(entity, changed);
        }

        public void Move(PlayerEntity entity, int from, int to)
        {
            SendDelta(entity, entity.Inventory.TryMove(from, to));
        }

        /// <summary>
        /// Gửi những ô vừa đổi. Danh sách RỖNG thì KHÔNG gửi gì — một gói delta không có ô nào bắt
        /// client vẽ lại vì không có lý do, và tệ hơn là nó nói dối rằng có chuyện vừa xảy ra.
        /// </summary>
        private static void SendDelta(PlayerEntity entity, IReadOnlyList<int> changedSlots)
        {
            if (changedSlots.Count == 0)
                return;

            var slots = new InventorySlotDto[changedSlots.Count];

            for (int i = 0; i < changedSlots.Count; i++)
                slots[i] = entity.Inventory.ToDto(changedSlots[i]);

            entity.Owner?.SendData(NetCmd.InventoryDelta, new InventoryDeltaNotice { Slots = slots });
        }

        //--------------------------------------------------------------------------------------------
        // Vòng tick
        //--------------------------------------------------------------------------------------------

        /// <summary>
        /// Gọi mỗi tick từ <c>WorldService.Tick</c>: tiêu thụ lệnh phát đồ rồi đếm giờ autosave.
        ///
        /// Nhận cả danh sách entity thay vì tự giữ một sổ riêng: "ai đang trong world" đã có đúng một
        /// nguồn là <c>WorldService</c>, và sổ thứ hai thì sớm muộn lệch với sổ thứ nhất.
        ///
        /// ICollection chứ không IReadOnlyCollection: <c>ConcurrentDictionary.Values</c> trả về
        /// <c>ICollection</c>, và hai interface ấy KHÔNG kế thừa nhau trong .NET.
        /// </summary>
        public void Tick(float dt, ICollection<PlayerEntity> entities)
        {
            while (_grants.TryDequeue(out GrantCommand command))
                GrantAll(command, entities);

            _autosaveTimer += dt;

            if (_autosaveTimer < AUTOSAVE_SECONDS)
                return;

            _autosaveTimer = 0f;

            foreach (PlayerEntity entity in entities)
            {
                if (!entity.Inventory.IsDirty)
                    continue;

                // KHÔNG await trong vòng tick: một lượt đi-về DBServer là vài ms, nhân với số người
                // online là cả nhịp tim server đứng lại. Bắn đi rồi quên — SaveAsync tự log khi hỏng.
                _ = SaveAsync(entity);
            }
        }

        private static void GrantAll(GrantCommand command, ICollection<PlayerEntity> entities)
        {
            ItemConfig config = ItemConfigContainer.Find(command.TemplateId);

            if (config == null)
            {
                Log.Warn($"Phát đồ: không có template {command.TemplateId.ToString().Red()} trong bảng.");
                return;
            }

            foreach (PlayerEntity entity in entities)
            {
                IReadOnlyList<int> changed = entity.Inventory.TryAdd(command.TemplateId, command.Quantity);

                if (changed.Count == 0)
                {
                    Log.Warn($"Túi của {entity.Name.Cyan()} đầy, không nhận {config.Name}");
                    continue;
                }

                Log.Info($"+{command.Quantity} {config.Name.Cyan()} → ô {string.Join(", ", changed)} " +
                         $"({entity.Name})");

                SendDelta(entity, changed);
            }
        }
    }
}
```

**`Server/GameServer/World/PlayerEntity.cs`** — thêm một property, đặt cạnh `Visible`:

```csharp
        /// <summary>
        /// Túi đồ, sống cùng entity. Dựng rỗng ngay tại đây rồi InventoryService nạp nội dung từ DB:
        /// nhờ vậy không có khoảnh khắc nào entity tồn tại mà Inventory còn null, và không chỗ nào
        /// phải kiểm null trước khi chạm vào túi.
        ///
        /// CHỈ LUỒNG TICK đọc/ghi, như Visible.
        /// </summary>
        public Inventory Inventory { get; } = new();
```

**`Server/GameServer/World/WorldService.cs`** — nhận `InventoryService` qua constructor:

```csharp
        private readonly InventoryService _inventoryService;

        public WorldService(MapRegistry maps, ConfigService config, InventoryService inventoryService)
        {
            _maps = maps;
            _config = config;
            _inventoryService = inventoryService;

            // Chốt MỘT LẦN lúc dựng, không đọc config.Current mỗi tick. Cùng lý do với WorldConfig
            // trong PlayerEntity — nhưng ở đây còn thêm một lý do nữa: đổi bán kính giữa chừng làm
            // tập Visible của mọi người lệch với tập đã gửi, và một loạt EntityDespawn giả sinh ra.
            _aoiRadiusX = config.Current.Server.AoiRadiusX;

            // Cột rộng BẰNG ĐÚNG bán kính (Phase 11). Tính từ bán kính chứ không cho nó một dòng
            // config riêng: hai con số rời nhau là hai con số sẽ lệch nhau.
            _aoiColumnWidth = _aoiRadiusX;
        }
```

và gọi `Tick` ở đầu vòng tick, ngay sau vòng 0 (tiêu thụ `_forcedActions`):

```csharp
            // Vòng 0b: túi đồ. Ở đây chứ không ở GameLoop vì nó cần đúng tập entity mà sổ này giữ —
            // "ai đang trong world" có một nguồn, và sổ thứ hai thì sớm muộn lệch với sổ thứ nhất.
            _inventoryService.Tick(dt, _entities.Values);
```

**`Server/GameServer/World/CharacterService.cs`** — ba chỗ.

Nhận service:

```csharp
        private readonly InventoryService _inventoryService;

        public CharacterService(DbClient dbClient, WorldService worldService, MapRegistry maps,
            ConfigService config, InventoryService inventoryService)
        {
            _dbClient = dbClient;
            _worldService = worldService;
            _maps = maps;
            _config = config;
            _inventoryService = inventoryService;
        }
```

Nạp túi trong `EnterWorldAsync`:

```csharp
            session.MarkInWorld(entity);

            // Nạp túi SAU khi vào world: LoadAsync gửi luôn gói snapshot, và gói ấy phải tới sau
            // EnterWorldResponse — client cần bảng item trong response đó để tra tên và icon.
            //
            // Không await ở đây thì snapshot có thể vượt mặt response. Await thì EnterWorld chậm thêm
            // một lượt đi-về DB, và đó là cái giá đúng để trả: nó chỉ xảy ra một lần mỗi phiên.
            await _inventoryService.LoadAsync(entity);
```

Lưu túi trong `LeaveWorldAsync`:

```csharp
            session.MarkLeftWorld();

            // Lưu túi TRƯỚC Despawn: sau Despawn thì entity đã ra khỏi sổ, và lưu một thứ không còn
            // ai cầm là mở đường cho "lưu nhầm bản cũ".
            await _inventoryService.SaveAsync(entity);

            _worldService.Despawn(entity);
```

**`Server/GameServer/Boot/ServerBootstrap.cs`** — ⚠️ **hai dòng, và thứ tự là bắt buộc**:
`InventoryService` phải đăng ký TRƯỚC `WorldService` vì `WorldService` nhận nó qua constructor.

```csharp
            var maps = ServerServices.Register(new MapRegistry(config));

            // TRƯỚC WorldService: vòng tick của world gọi InventoryService.Tick mỗi nhịp.
            var inventoryService = ServerServices.Register(new InventoryService(dbClient));

            var worldService = ServerServices.Register(new WorldService(maps, config, inventoryService));

            ServerServices.Register(new AuthService(dbClient, new LoginRateLimiter()));
            ServerServices.Register(new CharacterService(dbClient, worldService, maps, config, inventoryService));
```

**`Server/GameServer/Program.cs`** — phím `G`, nguồn item duy nhất của phase này:

```csharp
// Id của "Bình máu nhỏ" trong items.json. Hằng số CỦA PHÍM THỬ, không phải của game — ngày có quái
// rơi đồ thì phím này biến mất cùng nó.
const int POTION_TEMPLATE_ID = 1;

// ... sau ServerBootstrap.Build():
var inventoryService = ServerServices.Get<InventoryService>();

// ... trong switch của luồng đọc phím:
            // Nguồn item DUY NHẤT của Phase 13. Quái và đồ rơi dưới đất là Phase 15.
            case ConsoleKey.G:
                inventoryService.EnqueueGrantAll(POTION_TEMPLATE_ID, 3);
                break;
```

Sửa luôn dòng log của `Console.IsInputRedirected` cho khớp: `(R/H/K/J/G)`.

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
2. `slot` có nằm trong `[0, SLOT_COUNT)` không → client sửa code gửi `slot = -1` là `IndexOutOfRangeException`
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
<summary><b>📖 Lời giải — contract: <code>NetCmd</code>, DTO, <code>InventoryHandler</code></b></summary>

**`Server/Shared/Net/NetCmd.cs`** — thêm một region vào cuối enum:

```csharp
        #region Inventory / Item (400–499)

        /// <summary>
        /// Toàn bộ túi. Server đẩy MỘT LẦN ngay sau EnterWorld.
        /// Payload: <see cref="Dto.Inventory.InventorySnapshotNotice"/>
        /// </summary>
        InventorySnapshot = 400,

        /// <summary>
        /// Những ô vừa đổi. Server đẩy sau mỗi thao tác thành công.
        /// Payload: <see cref="Dto.Inventory.InventoryDeltaNotice"/>
        /// </summary>
        InventoryDelta = 401,

        /// <summary>
        /// Dùng đồ ở một ô. Response đi bằng InventoryDelta chứ không có gói riêng — thứ client cần
        /// sau khi dùng đúng là "ô nào vừa đổi".
        /// Payload: <see cref="Dto.Inventory.ItemUseRequest"/>
        /// </summary>
        ItemUse = 402,

        /// <summary>
        /// Vứt bớt số lượng ở một ô. Payload: <see cref="Dto.Inventory.ItemDropRequest"/>
        /// </summary>
        ItemDrop = 403,

        /// <summary>
        /// Kéo ô này sang ô kia. Payload: <see cref="Dto.Inventory.ItemMoveRequest"/>
        /// </summary>
        ItemMove = 404,

        #endregion
```

**`Server/Shared/Dto/Inventory/InventoryDto.cs`** (file mới, nguyên văn):

```csharp
using System;
using MemoryPack;

namespace MMORPG.Shared.Dto.Inventory
{
    /// <summary>
    /// Một ô túi trên dây. Struct vì nó nhỏ và đi thành mảng — và vì <c>default</c> của nó
    /// (<c>TemplateId = 0</c>) đã đúng nghĩa "ô trống", nên không cần một giá trị canh riêng.
    ///
    /// KHÔNG mang ItemId: client không có việc gì với id của một vật cụ thể — nó thao tác bằng
    /// SỐ Ô. Gửi thêm một trường mà người nhận không dùng là mời họ dùng nó sai.
    /// </summary>
    [MemoryPackable]
    public partial struct InventorySlotDto
    {
        public int Slot;

        /// <summary>0 = ô này TRỐNG. Không có gói "xoá ô" riêng — xem Bước 3.</summary>
        public int TemplateId;

        public int Quantity;
    }

    /// <summary>
    /// Toàn bộ túi. Gửi ĐÚNG MỘT LẦN, ngay sau EnterWorld, khi client chưa có gì để mà "đổi từ".
    /// Mọi hệ đồng bộ đều có hình dạng này: một trạng thái đầy đủ ban đầu, rồi một dòng thay đổi.
    /// </summary>
    [MemoryPackable]
    public partial class InventorySnapshotNotice
    {
        /// <summary>Chỉ các ô CÓ ĐỒ. Ô trống không cần một dòng để nói rằng nó trống.</summary>
        public InventorySlotDto[] Slots { get; set; } = Array.Empty<InventorySlotDto>();
    }

    /// <summary>Chỉ những ô VỪA ĐỔI. Đây là thứ UI cần — xem "snapshot ≠ delta" ở Bước 3.</summary>
    [MemoryPackable]
    public partial class InventoryDeltaNotice
    {
        public InventorySlotDto[] Slots { get; set; } = Array.Empty<InventorySlotDto>();
    }

    [MemoryPackable]
    public partial class ItemUseRequest
    {
        public int Slot { get; set; }
    }

    [MemoryPackable]
    public partial class ItemDropRequest
    {
        public int Slot { get; set; }

        public int Quantity { get; set; }
    }

    [MemoryPackable]
    public partial class ItemMoveRequest
    {
        public int FromSlot { get; set; }

        public int ToSlot { get; set; }
    }
}
```

**`Server/GameServer/Handlers/InventoryHandler.cs`** (file mới, nguyên văn):

```csharp
using MMORPG.GameServer.Boot;
using MMORPG.GameServer.Net;
using MMORPG.GameServer.World;
using MMORPG.Shared.Dto.Inventory;
using MMORPG.Shared.Net;

namespace MMORPG.GameServer.Handlers
{
    /// <summary>
    /// Ba lệnh, và cả ba đều KHÔNG trả response: kết quả đi bằng <c>InventoryDelta</c> mà service
    /// tự gửi. Đó là cố ý — nếu có cả response lẫn delta thì client có hai nguồn cho cùng một sự
    /// thật, và hai nguồn thì sớm muộn lệch nhau.
    /// </summary>
    public static class InventoryHandler
    {
        private static InventoryService InventoryService => ServerServices.Get<InventoryService>();

        [TcpHandler(NetCmd.ItemUse, MinState = SessionState.InWorld)]
        public static Task<NetResult> OnUse(NetRequest req)
        {
            PlayerEntity entity = req.Session.Entity;

            // MinState đã chặn phần lớn, nhưng LeaveWorld có thể xảy ra giữa lúc gói đang bay.
            if (entity == null)
                return Task.FromResult(NetResult.None);

            InventoryService.Use(entity, req.GetData<ItemUseRequest>().Slot);

            return Task.FromResult(NetResult.None);
        }

        [TcpHandler(NetCmd.ItemDrop, MinState = SessionState.InWorld)]
        public static Task<NetResult> OnDrop(NetRequest req)
        {
            PlayerEntity entity = req.Session.Entity;

            if (entity == null)
                return Task.FromResult(NetResult.None);

            var request = req.GetData<ItemDropRequest>();
            InventoryService.Drop(entity, request.Slot, request.Quantity);

            return Task.FromResult(NetResult.None);
        }

        [TcpHandler(NetCmd.ItemMove, MinState = SessionState.InWorld)]
        public static Task<NetResult> OnMove(NetRequest req)
        {
            PlayerEntity entity = req.Session.Entity;

            if (entity == null)
                return Task.FromResult(NetResult.None);

            var request = req.GetData<ItemMoveRequest>();
            InventoryService.Move(entity, request.FromSlot, request.ToSlot);

            return Task.FromResult(NetResult.None);
        }
    }
}
```

Ba handler đều ngắn đúng như vậy, và cả ba đều **không kiểm gì ngoài `entity == null`** — mọi
phép kiểm biên nằm trong `Inventory`: `IsValidSlot`, `IsEmpty`, `quantity <= 0`,
`quantity > Quantity`, `Kind != Consumable`. Đặt chúng ở đó chứ không ở handler vì đó là chỗ
**duy nhất** mọi đường vào đều đi qua: phím `G` của console cũng gọi `TryAdd`, và nó không đi
qua handler nào cả.

</details>

<details>
<summary><b>📖 Lời giải — client: <code>InventoryNetHandler</code>, <code>InventoryModel</code>, <code>InventoryApi</code>, <code>InventoryPresenter</code></b></summary>

**`Assets/Game/Scripts/Network/Handlers/InventoryNetHandler.cs`** (file mới, nguyên văn):

```csharp
using System;
using MMORPG.Shared.Dto.Inventory;
using MMORPG.Shared.Net;

namespace MMORPG.Client.Network.Handlers
{
    /// <summary>
    /// Nhận nhóm lệnh túi đồ. Handler chỉ giải mã rồi bắn event — không đụng model, không đụng UI.
    ///
    /// Vì sao không cho nó ghi thẳng vào InventoryModel: handler là tầng MẠNG, model là tầng DỮ LIỆU.
    /// Nối thẳng thì ngày có thứ hai cần nghe cùng gói tin (âm thanh nhặt đồ, nhiệm vụ "thu thập 10
    /// cái") là phải sửa handler. Bắn event thì chỉ thêm một người nghe.
    /// </summary>
    public sealed class InventoryNetHandler : INetHandlerGroup
    {
        public event Action<InventorySnapshotNotice> OnSnapshot;
        public event Action<InventoryDeltaNotice> OnDelta;

        [NetHandler(NetCmd.InventorySnapshot)]
        private void HandleSnapshot(NetPacket packet)
        {
            OnSnapshot?.Invoke(packet.GetData<InventorySnapshotNotice>());
        }

        [NetHandler(NetCmd.InventoryDelta)]
        private void HandleDelta(NetPacket packet)
        {
            OnDelta?.Invoke(packet.GetData<InventoryDeltaNotice>());
        }
    }
}
```

**`Assets/Game/Scripts/Inventory/InventoryModel.cs`** (file mới, nguyên văn):

```csharp
using System;
using MMORPG.Shared.Dto.Inventory;

namespace MMORPG.Client.Inventory
{
    /// <summary>
    /// Bản sao túi đồ của chính mình, do server gửi xuống. Cùng luật với LocalPlayer ở Phase 5:
    /// cache chỉ-đọc, không phải nguồn sự thật, không có setter công khai.
    ///
    /// Ngày nào có <c>inventory[3].Quantity--</c> ở đâu đó trong code UI là ngày golden rule #2 bị phá —
    /// và triệu chứng sẽ là "số lượng hiển thị sai cho tới lúc relog", loại bug không ai tìm ra.
    /// </summary>
    public sealed class InventoryModel
    {
        /// <summary>
        /// Phải khớp <c>Inventory.SLOT_COUNT</c> bên server. Hai hằng số rời nhau ở hai bên là hai
        /// con số sẽ lệch nhau — nhưng đưa nó vào Shared thì nó thành một phần của contract, và đổi
        /// số ô là đổi contract thật. Chấp nhận hai hằng, và bù bằng phép kiểm biên ở ApplyDelta.
        /// </summary>
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

            foreach (InventorySlotDto slot in snapshot.Slots)
            {
                if (slot.Slot < 0 || slot.Slot >= SLOT_COUNT)
                    continue;

                _slots[slot.Slot] = slot;
            }

            var changed = new int[SLOT_COUNT];

            for (int i = 0; i < SLOT_COUNT; i++)
                changed[i] = i;

            // Snapshot = "mọi ô vừa đổi". Nhờ vậy panel chỉ có MỘT đường vẽ lại, không phải hai.
            OnSlotsChanged?.Invoke(changed);
        }

        public void ApplyDelta(InventoryDeltaNotice delta)
        {
            // Dựng danh sách bằng List chứ không mảng cùng độ dài với delta: một ô ngoài phạm vi bị
            // bỏ qua sẽ để lại số 0 trong mảng, và số 0 là một ô HỢP LỆ — panel sẽ vẽ lại ô 0 mà
            // không có lý do nào.
            var changed = new System.Collections.Generic.List<int>(delta.Slots.Length);

            foreach (InventorySlotDto slot in delta.Slots)
            {
                // Gói tin đến từ mạng, và mạng thì không bảo đảm gì cả. Kiểm biên ở đây chứ không tin:
                // server hiện tại đúng, nhưng một server phiên bản khác thì chưa chắc.
                if (slot.Slot < 0 || slot.Slot >= SLOT_COUNT)
                    continue;

                _slots[slot.Slot] = slot;
                changed.Add(slot.Slot);
            }

            if (changed.Count == 0)
                return;

            OnSlotsChanged?.Invoke(changed.ToArray());
        }

        /// <summary>Quên sạch khi rời world. Gọi cùng chỗ với <c>LocalPlayer.Clear</c>.</summary>
        public void Clear()
        {
            Array.Clear(_slots, 0, _slots.Length);
        }
    }
}
```

**`Assets/Game/Scripts/Inventory/InventoryApi.cs`** (file mới, nguyên văn):

```csharp
using MMORPG.Client.Network;
using MMORPG.Shared.Dto.Inventory;
using MMORPG.Shared.Net;

namespace MMORPG.Client.Inventory
{
    /// <summary>
    /// Gom mọi lệnh túi đồ mà client GỬI ĐI. Đối xứng với
    /// <see cref="Network.Handlers.InventoryNetHandler"/> ở chiều nhận.
    ///
    /// Không đụng InventoryModel một dòng nào — và đó là toàn bộ ý nghĩa của class này. Bấm "dùng"
    /// thì UI KHÔNG giảm số lượng: nó gửi gói, rồi chờ. Server đổi, server gửi delta, model đổi, UI
    /// vẽ lại. Có một nhịp trễ bằng RTT, và nhịp trễ ấy là cái giá của việc luôn hiển thị sự thật.
    /// </summary>
    public sealed class InventoryApi
    {
        private readonly NetService _netService;

        public InventoryApi(NetService netService)
        {
            _netService = netService;
        }

        public void Use(int slot)
        {
            _netService.Send(NetCmd.ItemUse, new ItemUseRequest { Slot = slot });
        }

        public void Drop(int slot, int quantity)
        {
            _netService.Send(NetCmd.ItemDrop, new ItemDropRequest { Slot = slot, Quantity = quantity });
        }

        public void Move(int fromSlot, int toSlot)
        {
            // Kéo lên chính nó là không có ý định gì — chặn ở đây để không tốn một vòng đi-về chỉ để
            // server trả lời "không đổi gì". Server vẫn kiểm lại: client là dữ liệu của người lạ.
            if (fromSlot == toSlot)
                return;

            _netService.Send(NetCmd.ItemMove, new ItemMoveRequest { FromSlot = fromSlot, ToSlot = toSlot });
        }
    }
}
```

**`Assets/Game/Scripts/Inventory/InventoryPresenter.cs`** (file mới, nguyên văn) — MonoBehaviour
cắm sẵn trong scene, **không** phải panel:

```csharp
using HungNT;
using MMORPG.Client.Network.Handlers;
using MMORPG.Shared.Dto.Inventory;
using UnityEngine;
using VContainer;

namespace MMORPG.Client.Inventory
{
    /// <summary>
    /// Nối mạng với model, và mở/đóng panel. Đây là chỗ DUY NHẤT biết cả hai bên — panel không biết
    /// mạng tồn tại, handler không biết panel tồn tại.
    ///
    /// MonoBehaviour cắm sẵn trong scene (không phải panel): nó phải sống cả khi túi đang đóng, vì
    /// gói delta vẫn tới khi người chơi không mở túi. Bỏ qua chúng lúc đóng là mở ra lại thấy dữ
    /// liệu cũ.
    /// </summary>
    public sealed class InventoryPresenter : MonoBehaviour
    {
        private InventoryNetHandler _inventoryNetHandler;
        private InventoryModel _inventoryModel;

        [Inject]
        public void Construct(InventoryNetHandler inventoryNetHandler, InventoryModel inventoryModel)
        {
            _inventoryNetHandler = inventoryNetHandler;
            _inventoryModel = inventoryModel;
        }

        private void Start()
        {
            _inventoryNetHandler.OnSnapshot += OnSnapshot;
            _inventoryNetHandler.OnDelta += OnDelta;
        }

        private void OnDestroy()
        {
            if (_inventoryNetHandler == null)
                return;

            _inventoryNetHandler.OnSnapshot -= OnSnapshot;
            _inventoryNetHandler.OnDelta -= OnDelta;
        }

        private void OnSnapshot(InventorySnapshotNotice snapshot)
        {
            this.Log($"Nhận snapshot túi: {InventoryModel.SLOT_COUNT} ô, {snapshot.Slots.Length} ô có đồ");
            _inventoryModel.ApplySnapshot(snapshot);
        }

        private void OnDelta(InventoryDeltaNotice delta)
        {
            _inventoryModel.ApplyDelta(delta);
        }
    }
}
```

</details>

<details>
<summary><b>📖 Lời giải — client: <code>InventoryPanel</code> và <code>InventorySlotView</code></b></summary>

**`Assets/Game/Scripts/Inventory/InventorySlotView.cs`** (file mới, nguyên văn):

```csharp
using MMORPG.Shared.Dto.Inventory;
using MMORPG.Shared.World.Item;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MMORPG.Client.Inventory
{
    /// <summary>
    /// Một ô túi trên màn hình. Chỉ VẼ và báo người chơi vừa chạm vào nó — không biết mạng, không
    /// biết model, không tự đổi gì.
    ///
    /// Kéo-thả làm bằng ba interface của EventSystem thay vì tự đọc chuột trong Update: Unity đã lo
    /// chuyện "con trỏ đang ở trên UI nào" rồi, và tự làm lại thì sai ở đúng chỗ khó thấy nhất —
    /// khi có hai panel chồng nhau.
    /// </summary>
    public sealed class InventorySlotView : MonoBehaviour,
        IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _quantityText;
        [SerializeField] private CanvasGroup _canvasGroup;

        private InventoryPanel _panel;

        /// <summary>Số thứ tự ô, gán một lần lúc panel dựng lưới. Đây là thứ mọi lệnh gửi lên server dùng.</summary>
        public int Slot { get; private set; }

        public void Init(InventoryPanel panel, int slot)
        {
            _panel = panel;
            Slot = slot;
        }

        /// <summary>
        /// Vẽ lại ô theo dữ liệu model. Nhận DTO chứ không tự hỏi model: ô không có lý do gì để biết
        /// model tồn tại, và nhận dữ liệu vào thì test nó bằng một dòng.
        /// </summary>
        public void Render(InventorySlotDto data)
        {
            // TemplateId = 0 là ô trống — cùng quy ước với server, và là lý do không có gói "xoá ô".
            if (data.TemplateId == 0)
            {
                _icon.enabled = false;
                _quantityText.text = string.Empty;
                return;
            }

            ItemConfig config = ItemConfigContainer.Find(data.TemplateId);

            // Id lạ: bảng item của client cũ hơn server. Vẽ ô trống chứ không ném — người chơi mất
            // một icon, không mất cả màn hình.
            if (config == null)
            {
                _icon.enabled = false;
                _quantityText.text = "?";
                return;
            }

            _icon.enabled = true;
            _icon.sprite = Resources.Load<Sprite>(config.IconKey);

            // Số "1" trên mọi ô là nhiễu thị giác — chỉ hiện khi thật sự có một chồng.
            _quantityText.text = data.Quantity > 1 ? data.Quantity.ToString() : string.Empty;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Chuột phải = dùng. Kéo-thả đã chiếm chuột trái, nên nút còn lại là chỗ cho thao tác
            // thứ hai — cùng quy ước với phần lớn MMO, tức là thứ người chơi đã biết sẵn.
            if (eventData.button == PointerEventData.InputButton.Right)
                _panel.RequestUse(Slot);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // Tắt raycast của CHÍNH ô đang kéo: nếu không thì nó chắn con trỏ và OnDrop của ô bên
            // dưới không bao giờ chạy — thả ở đâu cũng "không có gì xảy ra".
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.alpha = 0.6f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Không di chuyển ô thật: chỉ đổi hình con trỏ. Ô là một phần của lưới layout, kéo nó ra
            // khỏi chỗ là layout tính lại và cả lưới nhảy.
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.alpha = 1f;
        }

        public void OnDrop(PointerEventData eventData)
        {
            var source = eventData.pointerDrag == null
                ? null
                : eventData.pointerDrag.GetComponent<InventorySlotView>();

            if (source == null || source == this)
                return;

            _panel.RequestMove(source.Slot, Slot);
        }
    }
}
```

**`Assets/Game/Scripts/Inventory/InventoryPanel.cs`** (file mới, nguyên văn):

```csharp
using HungNT.UI.Panel;
using UnityEngine;
using VContainer;

namespace MMORPG.Client.Inventory
{
    /// <summary>
    /// Panel túi đồ: dựng lưới 30 ô, vẽ lại ĐÚNG những ô model báo vừa đổi, và chuyển thao tác của
    /// người chơi sang <see cref="InventoryApi"/>.
    ///
    /// Không có một dòng nào ghi vào model. Bấm dùng thì gửi gói rồi chờ delta — xem ghi chú ở
    /// InventoryApi về nhịp trễ RTT.
    /// </summary>
    public sealed class InventoryPanel : UIPanelBase
    {
        [SerializeField] private InventorySlotView _slotPrefab;
        [SerializeField] private Transform _slotRoot;

        private readonly InventorySlotView[] _slots = new InventorySlotView[InventoryModel.SLOT_COUNT];

        private InventoryModel _inventoryModel;
        private InventoryApi _inventoryApi;

        /// <summary>
        /// Panel sinh lúc runtime từ prefab, nên VContainer không tự inject — PanelManager gọi
        /// Instantiate, không phải container. Đưa phụ thuộc vào tay ngay sau ShowPanel.
        /// </summary>
        [Inject]
        public void Construct(InventoryModel inventoryModel, InventoryApi inventoryApi)
        {
            _inventoryModel = inventoryModel;
            _inventoryApi = inventoryApi;
        }

        private void Awake()
        {
            for (int slot = 0; slot < _slots.Length; slot++)
            {
                InventorySlotView view = Instantiate(_slotPrefab, _slotRoot);
                view.name = $"Slot_{slot}";
                view.Init(this, slot);

                _slots[slot] = view;
            }
        }

        /// <summary>
        /// Đăng ký ở OnEnable chứ không Awake, và vẽ lại TOÀN BỘ ngay sau đó.
        ///
        /// Lý do thứ nhất: panel có thể được cache và bật/tắt nhiều lần (xem <c>CanCache</c>), nên
        /// Awake chỉ chạy một lần còn OnEnable chạy mỗi lần mở.
        ///
        /// Lý do thứ hai, quan trọng hơn: trong lúc panel đóng, model vẫn nhận delta — presenter
        /// sống độc lập với panel. Không vẽ lại lúc mở thì lưới hiển thị trạng thái của lần đóng
        /// trước, và nó sẽ đúng dần lên theo từng delta tiếp theo, tức là sai theo cách khó tin nhất.
        /// </summary>
        private void OnEnable()
        {
            if (_inventoryModel == null)
                return;

            _inventoryModel.OnSlotsChanged += OnSlotsChanged;

            RenderAll();
        }

        private void OnDisable()
        {
            if (_inventoryModel == null)
                return;

            _inventoryModel.OnSlotsChanged -= OnSlotsChanged;
        }

        public void RequestUse(int slot)
        {
            _inventoryApi.Use(slot);
        }

        public void RequestMove(int from, int to)
        {
            _inventoryApi.Move(from, to);
        }

        public void RequestDrop(int slot, int quantity)
        {
            _inventoryApi.Drop(slot, quantity);
        }

        /// <summary>Vẽ lại đúng những ô vừa đổi. Danh sách này đến từ server, đi qua model, không ai tính lại.</summary>
        private void OnSlotsChanged(int[] slots)
        {
            foreach (int slot in slots)
            {
                if (slot < 0 || slot >= _slots.Length)
                    continue;

                _slots[slot].Render(_inventoryModel.Get(slot));
            }
        }

        private void RenderAll()
        {
            for (int slot = 0; slot < _slots.Length; slot++)
                _slots[slot].Render(_inventoryModel.Get(slot));
        }
    }
}
```

**Prefab phải dựng bằng tay trong Unity** (code không thay được phần này):

- `Resources/UI/InventoryPanel.prefab` — gắn `InventoryPanel`, một `GridLayoutGroup` làm
  `_slotRoot`, và trỏ `_slotPrefab` sang prefab ô.
- `Resources/UI/InventorySlot.prefab` — gắn `InventorySlotView`, bên trong có `Image` (icon),
  `TMP_Text` (số lượng), và một `CanvasGroup` **trên chính GameObject của ô** (kéo-thả cần nó).
- Scene cần một `EventSystem`; không có nó thì không interface kéo-thả nào chạy, và **không có
  lỗi nào cả**.

</details>

<details>
<summary><b>📖 Lời giải — <code>GameLifetimeScope</code>: năm dòng dễ quên nhất</b></summary>

**`Assets/Game/Scripts/Boot/GameLifetimeScope.cs`** — thêm vào cuối `Configure`:

```csharp
            // Túi đồ. Năm dòng, và thiếu dòng nào cũng KHÔNG có lỗi biên dịch:
            //   · thiếu .As<INetHandlerGroup>()  → gói InventorySnapshot/Delta rơi vào hư không
            //   · thiếu InventoryModel           → VContainer không dựng nổi InventoryPresenter
            //   · thiếu RegisterComponentInHierarchy → [Inject] không chạy, field giữ null
            builder.Register<InventoryNetHandler>(Lifetime.Singleton).AsSelf().As<INetHandlerGroup>();
            builder.Register<InventoryModel>(Lifetime.Singleton);
            builder.Register<InventoryApi>(Lifetime.Singleton);
            builder.RegisterComponentInHierarchy<InventoryPresenter>();

            // PanelManager có sẵn trong scene — đăng ký một lần ở đây, từ đây mỗi panel mới chỉ
            // tốn một prefab + một class.
            builder.RegisterComponentInHierarchy<PanelManager>().As<IUIManager>();
```

Thêm hai `using`: `MMORPG.Client.Inventory` và `HungNT.UI.Panel`.

`InventoryPanel` **không** đăng ký ở đây: nó sinh từ prefab lúc runtime, nên VContainer không
thấy nó. Chỗ nào gọi `ShowPanel<InventoryPanel>` thì phải tự inject vào — cùng cách
`WorldSpawner` đưa phụ thuộc vào `PlayerMotor` ở Phase 12:

```csharp
            var panel = _uiManager.ShowPanel<InventoryPanel>(new PanelOptions("UI/InventoryPanel"));
            _objectResolver.Inject(panel);
```

(`IObjectResolver` inject được vào bất kỳ class nào VContainer quản lý — xin nó qua constructor
như mọi service khác.)

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
