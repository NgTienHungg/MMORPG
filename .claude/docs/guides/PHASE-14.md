# PHASE 14 — Chỉ số nhân vật: một pipeline, một chiều

> **Kết quả cuối Phase 14:** nhân vật có bộ chỉ số đầy đủ. Bấm `C` mở bảng thông tin, thấy chỉ số gốc,
> chỉ số dẫn xuất và số điểm chưa cộng. Cộng một điểm Sức mạnh → sát thương tăng ngay. Mặc cây kiếm
> trong túi → chỉ số đổi ngay; cởi ra → đổi lại đúng như cũ, **không dư không thiếu một điểm nào**, kể
> cả sau năm mươi lần mặc-cởi. Thoát game vào lại vẫn y nguyên.
>
> **Điều kiện:** xong [`PHASE-13.md`](PHASE-13.md) — có túi đồ, có bảng item, có `PanelManager`.
>
> **Bài học chính:** (1) **ba loại số** hay bị gộp làm một: chỉ số gốc · chỉ số dẫn xuất · trạng thái
> hiện thời; (2) **tính lại từ đầu, không cộng dồn** — và vì sao đây là cùng một bài học với việc dựng
> lại chỉ mục AOI mỗi tick ở Phase 11; (3) một pipeline **một chiều** mà client không có quyền tham gia,
> nhưng vẫn được phép *dự đoán để hiển thị*.

Format như trước: **hướng làm** hiện sẵn, **📖 Lời giải** trong foldout.

> **Viết lại 2026-09-22.** Bản trước có 663 dòng và **2 khối code**: Bước 2 và Bước 3 không có foldout
> lời giải nào, còn Bước 1 thì mở đầu bằng một comment nói *"ba kiểu bạn phải tự viết trước khi đoạn
> dưới biên dịch được"* — tức là chính thức giao lại phần khó nhất.
>
> Bản này có **đủ code cho từng dòng trong bảng file dưới đây**, và toàn bộ code đã được biên dịch
> thật (server `dotnet build`, client `csc` với DLL Shared vừa build) trước khi đưa vào doc.

---

## Ba loại số, và tại sao phải tách

Mọi hệ chỉ số hỏng đều hỏng vì gộp ba thứ này làm một. Tách trước, code sau:

| | **Gốc** (primary) | **Dẫn xuất** (derived) | **Trạng thái hiện thời** |
|---|---|---|---|
| Ví dụ | Sức mạnh, Nhanh nhẹn, Thể lực, Tinh thần | Sát thương, Phòng thủ, Sinh lực **tối đa**, Tỉ lệ chí mạng | Sinh lực **hiện tại**, Nội lực hiện tại |
| Đến từ đâu | bảng theo (lớp, cấp) + điểm cộng + trang bị | **công thức**, tính từ chỉ số gốc | biến đổi trong lúc chơi (bị đánh, uống thuốc) |
| Lưu DB không | điểm cộng thì **có**; phần từ bảng và trang bị thì **không** | **không bao giờ** | **có** |
| Đổi khi nào | lên cấp, cộng điểm, mặc/cởi đồ | mỗi khi chỉ số gốc đổi | liên tục |

Dòng "lưu DB không" là dòng đắt nhất. Cám dỗ lớn nhất của phase này là lưu luôn `MaxHp` xuống DB cho
nhanh — và đó chính xác là căn bệnh mà Phase 12 dành cả một phase để chữa, chỉ là mặc áo khác:

> Lưu một giá trị **tính được** nghĩa là tạo ra bản thứ hai của cùng một sự thật. Ngày bạn sửa công thức
> hoặc sửa bảng, mọi nhân vật đã lưu vẫn mang con số cũ — không lỗi, không log, chỉ là một nửa số người
> chơi mạnh hơn nửa kia mà không ai biết vì sao.

Lưu **nguyên liệu**, đừng lưu **thành phẩm**. Nguyên liệu ở đây là: cấp độ, điểm đã cộng, món đồ đang
mặc. Từ ba thứ đó tính ra được mọi chỉ số, mọi lúc.

Riêng cột "trạng thái hiện thời" thì ngược lại — nó **phải** lưu, vì không có công thức nào tính ra được
"còn bao nhiêu máu". Đó cũng đúng ranh giới của Phase 12 câu 8: *giá trị khởi tạo* vs *trạng thái tích
luỹ*, lần này gặp trong cùng một bảng dữ liệu.

---

## Danh sách file — tạo gì, sửa gì

Gạch từng dòng khi xong. **Mỗi dòng ở đây có một khối code tương ứng trong foldout lời giải của bước
đó** — thấy một dòng không có code là doc hỏng, báo lại.

**Bước 1 — `StatBlock`, bảng chỉ số, pipeline**

| File | Việc |
|---|---|
| `Server/Shared/World/Stat/StatType.cs` | 🆕 tạo (`StatType` + `StatTypes`) |
| `Server/Shared/World/Stat/StatBlock.cs` | 🆕 tạo (`StatBlock` + `StatBonus`) |
| `Server/Shared/World/Stat/ClassStatsTableData.cs` | 🆕 tạo (`ClassStatsConfig` + `ClassStatsTableData`) |
| `Server/Shared/World/Stat/ClassStatsConfigContainer.cs` | 🆕 tạo |
| `Server/Shared/World/Stat/StatCalculator.cs` | 🆕 tạo |
| `Assets/Game/Resources/Config/class-stats.json` | 🆕 tạo (+ file `.meta`) |
| `Server/GameServer/Config/ConfigService.cs` | ✏️ **một dòng** `LoadTable<ClassStatsTableData>` + `ValidateClassStats` |
| *(không đụng `EnterWorldResponse`)* | bảng không đi trên dây — contract không đổi một dòng |
| `Server/Shared/World/ConfigFiles.cs` | ✏️ thêm hằng `CLASS_STATS` |
| `Assets/Game/Scripts/Config/ConfigService.cs` | ✏️ **một dòng** trong `LoadTables()` |

**Bước 2 — điểm cộng**

| File | Việc |
|---|---|
| `Server/DBServer/Data/Migrator.cs` | ✏️ thêm migration `(5, ...)` |
| `Server/Shared/Dto/Db/StatDbDto.cs` | 🆕 tạo |
| `Server/Shared/Db/DbCmd.cs` | ✏️ `CharacterStatsLoad = 1202`, `CharacterStatsSave = 1203` |
| `Server/DBServer/Repositories/CharacterStatRepository.cs` | 🆕 tạo |
| `Server/DBServer/Handlers/CharacterStatDbHandler.cs` | 🆕 tạo |
| `Server/DBServer/Program.cs` | ✏️ gán `CharacterStatDbHandler.Repository` |
| `Server/GameServer/World/PlayerEntity.cs` | ✏️ `Allocated`, `UnspentPoints`, `Stats`, `Hp`, `Mp`, `Recompute`, `LoadStats`, `SpendPoints`, `GrantPoints` |
| `Server/GameServer/World/StatService.cs` | 🆕 tạo |
| `Server/Shared/Net/NetCmd.cs` | ✏️ `StatsUpdate = 201` … `UnequipItem = 204` |
| `Server/Shared/Dto/Character/StatsDto.cs` | 🆕 tạo |
| `Server/GameServer/Handlers/StatHandler.cs` | 🆕 tạo |
| `Server/GameServer/World/WorldService.cs` | ✏️ nhận `StatService`, gọi `Tick` |
| `Server/GameServer/World/CharacterService.cs` | ✏️ nạp/lưu chỉ số |
| `Server/GameServer/Boot/ServerBootstrap.cs` | ✏️ **đăng ký `StatService`** |
| `Server/GameServer/Program.cs` | ✏️ phím `P` thưởng điểm |
| `Assets/Game/Scripts/Stats/StatsModel.cs` | 🆕 tạo |
| `Assets/Game/Scripts/Stats/StatsApi.cs` | 🆕 tạo |
| `Assets/Game/Scripts/Stats/StatsPresenter.cs` | 🆕 tạo |
| `Assets/Game/Scripts/Stats/StatsPanel.cs` | 🆕 tạo (`StatsPanel` + `StatRowView` + `EquipSlotView`) |
| `Assets/Game/Scripts/Network/Handlers/StatsNetHandler.cs` | 🆕 tạo |
| `Assets/Game/Scripts/Boot/GameLifetimeScope.cs` | ✏️ **đăng ký 4 thứ** |

**Bước 3 — trang bị**

| File | Việc |
|---|---|
| `Server/Shared/World/Item/EquipSlot.cs` | 🆕 tạo |
| `Server/Shared/World/Item/ItemConfig.cs` | ✏️ thêm `EquipSlot` + `Bonuses` |
| *(không đụng `ItemTableData`)* | vân tay băm byte tuần tự hoá — hai trường mới tự vào |
| `Assets/Game/Resources/Config/items.json` | ✏️ thêm `EquipSlot` / `Bonuses` cho Kiếm gỗ |
| `Server/DBServer/Data/Migrator.cs` | ✏️ thêm migration `(6, ...)` — cột + hai chỉ mục có điều kiện |
| `Server/Shared/Dto/Db/InventoryDbDto.cs` | ✏️ `InventoryRow` thêm `EquipSlot` |
| `Server/DBServer/Repositories/InventoryRepository.cs` | ✏️ SQL đọc/ghi thêm cột `equip_slot` |
| `Server/GameServer/World/Inventory.cs` | ✏️ `_equipped`, `TryEquip`, `TryUnequip`, `Load`/`ToRows` |
| `Server/GameServer/World/InventoryService.cs` | ✏️ `SendDelta` từ `private` thành `public static` |

---

## Bước 1 — `StatBlock`, bảng chỉ số gốc, và pipeline tính lại

### Hướng làm

**`StatType` là một enum, `StatBlock` là một mảng theo enum ấy** — không phải mười hai property rời.

Lý do đã gặp hai lần rồi (gom 12 field của `MoveState` ở Phase 9, gom `WorldConfig` ở Phase 12), nhưng ở
đây nó còn mạnh hơn: mã nguồn cần **cộng hai bộ chỉ số lại với nhau** ở bốn chỗ khác nhau. Với mảng thì
đó là một vòng `for`; với property rời thì đó là mười hai dòng, lặp lại bốn lần, và lần nào quên một
dòng cũng **không có lỗi biên dịch** — chỉ có một chỉ số âm thầm không bao giờ tăng.

```csharp
public enum StatType : byte
{
    // Gốc — cộng điểm được
    Strength = 0, Agility = 1, Vitality = 2, Spirit = 3,

    // Dẫn xuất — tính ra, không ai cộng thẳng
    MaxHp = 10, MaxMp = 11, Attack = 12, Defense = 13, CritRate = 14,
}
```

Số của nhóm dẫn xuất bắt đầu từ 10 chứ không phải 4: chừa chỗ cho chỉ số gốc thứ năm mà không phải đánh
số lại cái gì. Cùng luật với dải `NetCmd` ở `ROADMAP.md §2`, và cùng lý do — **đánh số lại một enum đã
đi vào DB là một cuộc di cư.**

**Bảng loại B thứ tư: `Config/class-stats.json`.**

```json
{
  "Version": 1,
  "Classes": [
    {
      "ClassId": 1,
      "Base":   [ { "Stat": "Strength", "Value": 10 }, { "Stat": "Vitality", "Value": 12 } ],
      "PerLevel": [ { "Stat": "Strength", "Value": 2 }, { "Stat": "Vitality", "Value": 3 } ],
      "PointsPerLevel": 5
    }
  ]
}
```

Nạp y hệt ba bảng trước (`Fnv1a`, `Load`, đi trong `EnterWorldResponse`). Khuôn không đổi — và đó là
điều đáng nói: bảng thứ tư giờ tốn nửa buổi.

> **Vì sao tách khỏi `characters.json`?** Vì hai bảng ấy trả lời hai câu khác nhau: `characters.json`
> nói *"nhân vật này di chuyển thế nào"* (dữ liệu của mô phỏng, đi vào `MovementRules.Step`);
> `class-stats.json` nói *"nhân vật này mạnh thế nào"* (dữ liệu của chiến đấu). Chúng đổi vì những lý do
> khác nhau và do những người khác nhau chỉnh. Ngược với `IconKey` ở Phase 13 — ở đó là **một thực thể**
> nên một dòng; ở đây là **hai chủ đề** nên hai file.

**`StatCalculator` — một hàm, một chiều, không có trạng thái.**

```
base(classId, level) ─┐
điểm đã cộng        ─┼─► cộng lại ─► StatBlock gốc ─► công thức dẫn xuất ─► StatBlock đầy đủ
trang bị đang mặc   ─┤
buff (Phase sau)    ─┘
```

Ba tính chất bắt buộc, và mỗi tính chất chặn một lớp bug:

1. **Hàm thuần.** Không đọc `DateTime`, không random, không đọc biến toàn cục. Cùng đầu vào → cùng đầu
   ra, ở cả hai đầu dây. Giống `MovementRules.Step`, và vì cùng lý do.
2. **Tính lại TỪ ĐẦU mỗi lần.** Không có `stats.Attack += weapon.Attack` khi mặc và `-=` khi cởi.
3. **Chỉ số dẫn xuất tính SAU CÙNG**, từ chỉ số gốc đã cộng đủ. Không phải cộng `MaxHp` của cái giáp vào
   `MaxHp` đã tính từ Thể lực — mà là cộng Thể lực của cái giáp vào Thể lực, rồi mới tính `MaxHp`.

Điểm 2 là bài học lớn nhất của phase, và bạn đã gặp nó rồi:

> Phase 11 dựng lại **toàn bộ chỉ mục cột từ đầu mỗi tick** thay vì cập nhật tại chỗ. Lý do khi ấy:
> bản cập-nhật-tại-chỗ phải đúng ở *mọi* đường vào và ra, quên một đường là chỉ mục lệch thực tế mà
> không có triệu chứng.
>
> Chỉ số cũng vậy, và còn tệ hơn: cộng/trừ dồn thì mỗi lần mặc-cởi là một cơ hội **trôi** một điểm. Sau
> năm mươi lần, chỉ số sai — và sai theo hướng có lợi cho người chơi thì họ sẽ không báo cho bạn.
> Tính lại từ đầu thì cả lớp bug ấy **không tồn tại**: đúng ba nguyên liệu đó luôn cho ra đúng một kết
> quả, bất kể đã bấm bao nhiêu lần.

Điểm 3 thì tinh vi hơn, và sai nó rất khó thấy: nếu công thức là `MaxHp = Vitality × 10` mà cái giáp
cộng `+5 Vitality`, thì thứ tự đúng cho `+50 MaxHp`, thứ tự sai (cộng `MaxHp` của giáp vào sau) cho `+5`.
Cả hai đều "chạy", chỉ khác nhau một con số mà không ai kiểm.

**Đẩy xuống client bằng `StatsUpdate` — và ở đây là SNAPSHOT, không phải delta.**

Ngược hẳn Phase 13, và lý do đáng hiểu rõ chứ không phải "tuỳ lúc":

> Chỉ số **phụ thuộc lẫn nhau**. Cộng một điểm Sức mạnh thì Sát thương đổi; cộng Thể lực thì cả `MaxHp`
> lẫn Phòng thủ đổi. "Cái gì vừa đổi" gần như luôn là "gần hết", nên delta không tiết kiệm gì. Tệ hơn:
> **một nửa bộ chỉ số là một object vô nghĩa** — Sát thương của bộ cũ ghép với Phòng thủ của bộ mới
> không mô tả nhân vật nào cả.
>
> Túi đồ thì ngược lại: 30 ô độc lập, đổi một ô không đụng 29 ô kia. Đó là điều kiện để delta có nghĩa.

Rút thành một câu mang đi được: **delta hợp lý khi các phần độc lập; snapshot hợp lý khi chúng là một
khối.** Kích thước gói chỉ là chuyện thứ yếu.

**Client hiển thị, không tính.** `StatsModel` nhận `StatsUpdate` và giữ nguyên khối đó. Panel đọc từ
model. Không có dòng nào ở client cộng một chỉ số vào chỉ số khác.

Nhưng có một ngoại lệ **hợp lệ**, và ranh giới của nó phải rõ: rê chuột vào cây kiếm trong túi thì UI
muốn hiện `Sát thương 42 → 55`. Con số 55 ấy client **phải tự tính** — server không biết bạn đang rê
chuột vào đâu. Vì vậy `StatCalculator` đặt ở `Shared`, và luật là:

> Client được phép chạy `StatCalculator` để **dự đoán cho việc hiển thị**. Con số đang **có hiệu lực**
> thì luôn là con số server vừa đẩy xuống — không bao giờ là kết quả client tự tính.
>
> Cùng ranh giới với `MovementRules.Step` ở Phase 6: client chạy đúng hàm đó để dự đoán, nhưng vị trí
> thật vẫn là vị trí server nói.

### ✅ CHECKPOINT A

Bước này chưa có gói tin nào — `StatsUpdate` là việc của Bước 2. Kiểm bằng log server và một bài test.

1. Server boot in: `Bảng class-stats: 1 dòng, version 1, vân tay XXXXXXXX`.
2. Sửa `PerLevel` của Thể lực trong file, restart server → vân tay **đổi**.
3. Ghi `{ "Stat": "MaxHp", "Value": 5 }` vào `Base` → server **chết ngay lúc boot** với
   `MaxHp là chỉ số dẫn xuất, không ghi trong bảng nền được` kèm tên file — luật boot/reload của
   Phase 12. Đây là phép kiểm bắt một lỗi mà không có nó thì dòng ấy đọc được, đi qua validate, rồi
   bị `StatCalculator` ghi đè trong im lặng.
4. Ghi `{ "Stat": "Strength", "Value": -5 }` → Warn về đúng trường đó, giá trị về 0.
5. Viết một bài test gọi `StatCalculator.Compute` hai lần liên tiếp với cùng đầu vào rồi `Assert` hai
   kết quả bằng nhau. Nghe thừa; nó là thứ chặn mọi ý định lén cho trạng thái vào hàm này sau này.
6. Viết một bài test nữa cho **thứ tự** ở `ApplyDerived`: một `ItemConfig` cộng `+5 Vitality`, kiểm
   `MaxHp` tăng **50** chứ không phải 5. Đây là bài test cho câu "cả hai đều chạy, chỉ khác nhau một
   con số mà không ai kiểm".

<details>
<summary><b>📖 Lời giải — <code>StatType</code> và <code>StatBlock</code></b></summary>

Năm file, mỗi file một chủ đề. Đặt hết ở `Server/Shared/World/Stat/` — bộ ba quen thuộc cộng
hai kiểu hạ tầng.

**`Server/Shared/World/Stat/StatType.cs`** (file mới, nguyên văn):

```csharp
namespace MMORPG.Shared.World.Stat
{
    /// <summary>
    /// Mọi chỉ số của nhân vật, gốc lẫn dẫn xuất, trong MỘT enum.
    ///
    /// Một enum chứ không hai vì <see cref="StatBlock"/> là một mảng đánh chỉ số bằng nó — hai enum
    /// là hai mảng, và mọi phép cộng phải làm hai lần. Phân biệt gốc/dẫn xuất bằng
    /// <see cref="StatTypes.IsPrimary"/>, không bằng kiểu dữ liệu.
    /// </summary>
    public enum StatType : byte
    {
        // ── Gốc: cộng điểm được, trang bị cộng vào được ──────────────────────────────────────────
        Strength = 0,
        Agility = 1,
        Vitality = 2,
        Spirit = 3,

        // ── Dẫn xuất: TÍNH RA từ nhóm trên, không ai cộng thẳng ─────────────────────────────────
        //
        // Bắt đầu từ 10 chứ không phải 4: chừa chỗ cho chỉ số gốc thứ năm mà không phải đánh số lại
        // cái gì. Cùng luật với dải NetCmd — đánh số lại một enum đã đi vào DB là một cuộc di cư.
        MaxHp = 10,
        MaxMp = 11,
        Attack = 12,
        Defense = 13,
        CritRate = 14,
    }

    public static class StatTypes
    {
        /// <summary>Dài bằng giá trị enum lớn nhất + 1. Vài ô giữa bỏ trống là cái giá của việc chừa chỗ.</summary>
        public const int COUNT = (int)StatType.CritRate + 1;

        /// <summary>
        /// Chỉ số này có cộng điểm vào được không.
        ///
        /// Phải là một HÀM chứ không phải một tính chất của kiểu, vì enum không diễn đạt được nó:
        /// <c>StatType.MaxHp</c> là một giá trị hợp lệ về mặt kiểu, và một client sửa code gửi lên
        /// đúng giá trị đó. Đây là chỗ hiếm hoi mà "chặn bằng kiểu" không làm được — nên phải chặn
        /// bằng câu lệnh, và phải có một bài test cho nó.
        /// </summary>
        public static bool IsPrimary(StatType stat)
        {
            return stat == StatType.Strength
                   || stat == StatType.Agility
                   || stat == StatType.Vitality
                   || stat == StatType.Spirit;
        }
    }
}
```

**`Server/Shared/World/Stat/StatBlock.cs`** (file mới, nguyên văn):

```csharp
using System;
using MemoryPack;

namespace MMORPG.Shared.World.Stat
{
    /// <summary>
    /// Một bộ chỉ số. MẢNG theo <see cref="StatType"/> chứ không phải chín property rời: mã nguồn cần
    /// CỘNG hai bộ lại với nhau ở bốn chỗ khác nhau, và với property rời thì mỗi chỗ là chín dòng chép
    /// tay — quên một dòng không có lỗi biên dịch, chỉ có một chỉ số âm thầm không bao giờ tăng.
    /// </summary>
    [MemoryPackable]
    public sealed partial class StatBlock
    {
        public int[] Values { get; set; } = new int[StatTypes.COUNT];

        /// <summary>
        /// Tra theo enum. Không kiểm biên: chỉ số đến từ mạng đã phải qua <c>Enum.IsDefined</c> ở
        /// handler, và chỉ số trong code thì là hằng.
        /// </summary>
        [MemoryPackIgnore]
        public int this[StatType stat]
        {
            get { return Values[(int)stat]; }
            set { Values[(int)stat] = value; }
        }

        /// <summary>
        /// Cộng dồn bộ khác vào bộ này. Chịu được mảng ngắn hơn: một gói tin từ server phiên bản cũ
        /// có ít chỉ số hơn, và rơi vào IndexOutOfRange vì chuyện đó thì quá đắt.
        /// </summary>
        public void Add(StatBlock other)
        {
            int length = Math.Min(Values.Length, other.Values.Length);

            for (int i = 0; i < length; i++)
                Values[i] += other.Values[i];
        }

        public StatBlock Clone()
        {
            var copy = new StatBlock();

            Array.Copy(Values, copy.Values, Math.Min(Values.Length, copy.Values.Length));

            return copy;
        }
    }

    /// <summary>
    /// Một dòng "chỉ số +N" — của trang bị, và sau này của buff. Struct vì nó là hai con số và đi
    /// thành mảng.
    /// </summary>
    [MemoryPackable]
    public partial struct StatBonus
    {
        /// <summary>Ghi bằng TÊN enum trong file ("Strength"), không phải số.</summary>
        public StatType Stat;

        public int Value;
    }
}
```

</details>

<details>
<summary><b>📖 Lời giải — bảng chỉ số nền: <code>ClassStatsTableData</code> + <code>ClassStatsConfigContainer</code></b></summary>

**`Server/Shared/World/Stat/ClassStatsTableData.cs`** (file mới, nguyên văn):

```csharp
using System;
using MemoryPack;
using Newtonsoft.Json;

namespace MMORPG.Shared.World.Stat
{
    /// <summary>
    /// Chỉ số nền của MỘT lớp nhân vật. Tách khỏi <c>CharacterConfig</c> vì hai bảng trả lời hai câu
    /// khác nhau: characters.json nói "nhân vật này DI CHUYỂN thế nào" (dữ liệu của mô phỏng),
    /// class-stats.json nói "nhân vật này MẠNH thế nào" (dữ liệu của chiến đấu). Chúng đổi vì những
    /// lý do khác nhau và do những người khác nhau chỉnh.
    /// </summary>
    [MemoryPackable]
    public sealed partial class ClassStatsConfig
    {
        public int ClassId { get; set; }

        /// <summary>Chỉ số ở cấp 1. Mảng bonus chứ không phải StatBlock: file người gõ tay chỉ ghi dòng nào có giá trị.</summary>
        public StatBonus[] Base { get; set; } = Array.Empty<StatBonus>();

        /// <summary>Cộng thêm mỗi cấp, từ cấp 2 trở đi.</summary>
        public StatBonus[] PerLevel { get; set; } = Array.Empty<StatBonus>();

        /// <summary>Số điểm tự cộng được thưởng mỗi cấp.</summary>
        public int PointsPerLevel { get; set; } = 5;

        // Dẫn xuất: dựng sẵn thành StatBlock để StatCalculator không phải duyệt mảng bonus mỗi lần
        // gọi. Cùng lý do với ActionData.DurationTicks — Compute chạy mỗi lần mặc/cởi/cộng điểm của
        // mọi người chơi, còn bảng thì chỉ đổi khi bấm R.

        [MemoryPackIgnore] [JsonIgnore] public StatBlock BaseBlock { get; private set; } = new();

        [MemoryPackIgnore] [JsonIgnore] public StatBlock PerLevelBlock { get; private set; } = new();

        /// <summary>Gọi từ <see cref="ClassStatsConfigContainer.Load"/> — đúng một chỗ ở mỗi bên.</summary>
        public void Prepare()
        {
            BaseBlock = ToBlock(Base);
            PerLevelBlock = ToBlock(PerLevel);
        }

        private static StatBlock ToBlock(StatBonus[] bonuses)
        {
            var block = new StatBlock();

            foreach (StatBonus bonus in bonuses)
                block[bonus.Stat] += bonus.Value;

            return block;
        }
    }

    [MemoryPackable]
    public sealed partial class ClassStatsTableData : IConfigFile
    {
        public int Version { get; set; } = 1;

        public ClassStatsConfig[] Classes { get; set; } = Array.Empty<ClassStatsConfig>();

        [MemoryPackIgnore] [JsonIgnore] public int RowCount => Classes.Length;
    }
}
```

**`Server/Shared/World/Stat/ClassStatsConfigContainer.cs`** (file mới, nguyên văn):

```csharp
using System;
using System.Collections.Generic;

namespace MMORPG.Shared.World.Stat
{
    /// <summary>
    /// Bảng tra chỉ số nền theo lớp nhân vật. Bảng loại B thứ tư của dự án, và khuôn không đổi một
    /// dòng nào so với <see cref="Character.CharacterConfigContainer"/> và
    /// <see cref="Item.ItemConfigContainer"/>.
    /// </summary>
    public static class ClassStatsConfigContainer
    {
        private static Dictionary<int, ClassStatsConfig> _byClassId = new();

        public static int Count
        {
            get { return _byClassId.Count; }
        }

        /// <summary>
        /// Chỉ số nền của một lớp. Ném khi bảng chưa nạp, trả bản RỖNG khi lớp không có trong bảng —
        /// hai cách xử lý khác nhau cho hai lỗi khác nhau: chưa nạp là lỗi lập trình (sai thứ tự
        /// khởi động), thiếu một lớp là lỗi dữ liệu (bảng chưa điền xong).
        /// </summary>
        public static ClassStatsConfig Get(int classId)
        {
            if (_byClassId.Count == 0)
                throw new InvalidOperationException("ClassStatsConfigContainer chưa được Load. Cả hai bên nạp từ file của mình lúc khởi động.");

            return _byClassId.TryGetValue(classId, out ClassStatsConfig config) ? config : Empty;
        }

        /// <summary>
        /// Bộ số rỗng dùng chung cho lớp không có trong bảng. Dựng MỘT LẦN: Compute gọi Get mỗi lần
        /// tính lại, và cấp phát một object mới mỗi lần cho một trường hợp lỗi là rác không cần thiết.
        ///
        /// KHÔNG ai được ghi vào nó — nó là bộ số dùng chung, sửa một chỗ là sửa mọi chỗ. StatCalculator
        /// chỉ ĐỌC từ config, nên tính chất đó được giữ bằng kỷ luật, như CharacterConfig.
        /// </summary>
        private static readonly ClassStatsConfig Empty = BuildEmpty();

        public static void Load(ClassStatsTableData table)
        {
            if (table == null)
                throw new ArgumentNullException(nameof(table));

            var built = new Dictionary<int, ClassStatsConfig>();

            foreach (ClassStatsConfig config in table.Classes)
            {
                if (built.ContainsKey(config.ClassId))
                    throw new InvalidOperationException($"Hai lớp cùng ClassId {config.ClassId} trong bảng chỉ số.");

                config.Prepare();
                built[config.ClassId] = config;
            }

            _byClassId = built;
        }

        private static ClassStatsConfig BuildEmpty()
        {
            var config = new ClassStatsConfig { PointsPerLevel = 0 };
            config.Prepare();

            return config;
        }
    }
}
```

**`Config/class-stats.json`** (file mới):

```json
{
  "Version": 1,
  "Classes": [
    {
      "ClassId": 1,
      "Base": [
        { "Stat": "Strength", "Value": 10 },
        { "Stat": "Agility", "Value": 8 },
        { "Stat": "Vitality", "Value": 12 },
        { "Stat": "Spirit", "Value": 6 }
      ],
      "PerLevel": [
        { "Stat": "Strength", "Value": 2 },
        { "Stat": "Vitality", "Value": 3 }
      ],
      "PointsPerLevel": 5
    }
  ]
}
```

</details>

<details>
<summary><b>📖 Lời giải — <code>StatCalculator</code> và bốn dòng nối vào đường ống</b></summary>

**`Server/Shared/World/Stat/StatCalculator.cs`** (file mới, nguyên văn):

```csharp
using System.Collections.Generic;
using MMORPG.Shared.World.Item;

namespace MMORPG.Shared.World.Stat
{
    /// <summary>
    /// Tính bộ chỉ số đầy đủ từ NGUYÊN LIỆU. Hàm thuần: cùng đầu vào luôn cho cùng đầu ra, không đọc
    /// thời gian, không random, không đọc biến toàn cục nào ngoài bảng tĩnh đã nạp.
    ///
    /// Nằm ở Shared để client chạy được — nhưng CHỈ để dự đoán cho việc hiển thị (rê chuột vào món đồ
    /// thì hiện "42 → 55"). Con số đang có hiệu lực luôn là con số server đẩy xuống. Cùng ranh giới
    /// với <c>MovementRules.Step</c> ở Phase 6.
    /// </summary>
    public static class StatCalculator
    {
        /// <summary>
        /// TÍNH LẠI TỪ ĐẦU, mọi lần. Không có phiên bản "cộng thêm khi mặc, trừ đi khi cởi" — cộng
        /// dồn thì mỗi lần mặc-cởi là một cơ hội trôi một điểm, và sau năm mươi lần thì chỉ số sai mà
        /// không có gì báo. Cùng lý do khiến Phase 11 dựng lại chỉ mục cột từ đầu mỗi tick.
        /// </summary>
        /// <param name="classId">Lớp nhân vật.</param>
        /// <param name="level">Cấp hiện tại, tính từ 1.</param>
        /// <param name="allocated">Điểm người chơi đã tự cộng.</param>
        /// <param name="equipped">Món đồ đang mặc. Null hoặc rỗng đều hợp lệ.</param>
        public static StatBlock Compute(int classId, int level, StatBlock allocated, IReadOnlyList<ItemConfig> equipped)
        {
            ClassStatsConfig config = ClassStatsConfigContainer.Get(classId);
            var result = new StatBlock();

            // 1. Nền của lớp nhân vật ở cấp này.
            result.Add(config.BaseBlock);

            // Nhân thay vì cộng trong vòng lặp: cấp 60 là 59 vòng cộng chín số, mỗi lần mặc/cởi đồ
            // của mọi người chơi. Kết quả giống hệt vì PerLevel là hằng theo cấp.
            for (int i = 0; i < StatTypes.COUNT; i++)
                result.Values[i] += config.PerLevelBlock.Values[i] * (level - 1);

            // 2. Điểm người chơi tự cộng.
            if (allocated != null)
                result.Add(allocated);

            // 3. Trang bị. Chỉ cộng vào chỉ số GỐC ở vòng này — xem ghi chú ở bước 4.
            if (equipped != null)
            {
                foreach (ItemConfig item in equipped)
                {
                    foreach (StatBonus bonus in item.Bonuses)
                        result[bonus.Stat] += bonus.Value;
                }
            }

            ApplyDerived(result);

            return result;
        }

        /// <summary>
        /// Chỉ số dẫn xuất, tính SAU CÙNG từ chỉ số gốc đã cộng đủ.
        ///
        /// Thứ tự này không phải chuyện phong cách. Giáp cộng +5 Thể lực, công thức
        /// <c>MaxHp = Vitality × 10</c>: làm đúng thứ tự thì giáp cho +50 MaxHp; cộng MaxHp của giáp
        /// vào SAU khi đã tính thì chỉ được +5. Cả hai đều "chạy", chỉ khác nhau một con số mà không
        /// ai kiểm.
        ///
        /// GÁN chứ không cộng: nếu có món đồ nào ghi bonus thẳng vào MaxHp thì phép gán này xoá nó đi,
        /// và đó là hành vi đúng — bonus thẳng vào chỉ số dẫn xuất là một lối đi vòng qua công thức.
        /// Phase sau muốn có "áo +100 HP" thì thêm một tầng bonus RIÊNG sau dòng này, đừng gỡ phép gán.
        /// </summary>
        private static void ApplyDerived(StatBlock stats)
        {
            stats[StatType.MaxHp] = 50 + stats[StatType.Vitality] * 10;
            stats[StatType.MaxMp] = 20 + stats[StatType.Spirit] * 5;
            stats[StatType.Attack] = stats[StatType.Strength] * 2 + stats[StatType.Agility];
            stats[StatType.Defense] = stats[StatType.Vitality];
            stats[StatType.CritRate] = stats[StatType.Agility] / 2;
        }
    }
}
```

**`Server/GameServer/Config/ConfigService.cs`** — bảng loại B thứ tư, và nó tốn đúng một dòng
cộng một hàm kiểm:

```csharp
        // ... trong Load(), ngay sau hai bảng của Phase 12-13:
        LoadTable<ClassStatsTableData>(ConfigFiles.CLASS_STATS, ValidateClassStats, ClassStatsConfigContainer.Load);
```

```csharp
        /// Kẹp bảng chỉ số nền. Không có trần nào đến từ thuật toán ở đây (khác hẳn MoveSpeed) — chỉ
        /// chặn số âm và chặn một con số gõ nhầm biến nhân vật cấp 1 thành bất tử.
        /// </summary>
        private static void ValidateClassStats(ClassStatsTableData table)
        {
            foreach (ClassStatsConfig config in table.Classes)
            {
                string tag = $"class {config.ClassId}";

                config.PointsPerLevel = (int)Clamp(config.PointsPerLevel, 0f, 100f, 5f, $"{tag}.PointsPerLevel");

                ClampBonuses(config.Base, $"{tag}.Base");
                ClampBonuses(config.PerLevel, $"{tag}.PerLevel");
            }
        }

        private static void ClampBonuses(StatBonus[] bonuses, string tag)
        {
            for (int i = 0; i < bonuses.Length; i++)
            {
                // Chỉ số GỐC mới được ghi trong bảng nền: ghi thẳng MaxHp là đi đường tắt qua công
                // thức dẫn xuất, và StatCalculator sẽ ghi đè nó — tức là một dòng file không có tác
                // dụng gì mà không ai biết.
                if (!StatTypes.IsPrimary(bonuses[i].Stat))
                    throw new InvalidOperationException($"{tag}: {bonuses[i].Stat} là chỉ số dẫn xuất, không ghi trong bảng nền được.");

                bonuses[i].Value = (int)Clamp(bonuses[i].Value, 0f, 9999f, 0f, $"{tag}.{bonuses[i].Stat}");
            }
        }
```

**`Server/Shared/World/ConfigFiles.cs`** — một hằng nữa:

```csharp
        public const string CLASS_STATS = "class-stats";
```

**`Assets/Game/Scripts/Config/ConfigService.cs`** — ⚠️ một dòng trong `LoadTables()`:

```csharp
        private void LoadTables()
        {
            LoadTable<CharacterTableData>(ConfigFiles.CHARACTERS, CharacterConfigContainer.Load);
            LoadTable<ItemTableData>(ConfigFiles.ITEMS, ItemConfigContainer.Load);
            LoadTable<ClassStatsTableData>(ConfigFiles.CLASS_STATS, ClassStatsConfigContainer.Load);
        }
```

**`EnterWorldResponse` không đổi một dòng nào**, và đó là điểm đáng chú ý của bảng thứ tư: bảng
không đi trên dây, nên bảng thứ năm, thứ sáu cũng sẽ không đụng tới contract. Hai dòng ở hai
`ConfigService`, hết.

</details>

---

## Bước 2 — Điểm cộng: dữ liệu người chơi đầu tiên không phải vị trí

### Hướng làm

**① DB — migration 5.** Hai thứ, và chúng ở hai chỗ khác nhau vì hai lý do khác nhau:

```sql
-- Số điểm CHƯA tiêu. Là một con số của nhân vật → thêm cột.
ALTER TABLE character ADD COLUMN stat_points INTEGER NOT NULL DEFAULT 0;

-- Điểm ĐÃ cộng vào từng chỉ số. Là một QUAN HỆ một-nhiều → thêm bảng.
CREATE TABLE character_stat (
    character_id INTEGER NOT NULL REFERENCES character(id) ON DELETE CASCADE,
    stat_type    INTEGER NOT NULL,
    points       INTEGER NOT NULL,
    PRIMARY KEY (character_id, stat_type)
);
```

Vì sao không thêm bốn cột `str`, `agi`, `vit`, `spi` vào `character` cho gọn: vì thêm chỉ số gốc thứ năm
sẽ là một migration `ALTER TABLE` nữa, rồi sửa mọi câu `SELECT`, mọi `INSERT`, mọi DTO. Với bảng quan hệ
thì chỉ số thứ năm là **một giá trị enum mới** — không đụng schema, không đụng SQL.

> Luật: **cái gì có thể nhiều lên thì thành hàng, không thành cột.**

`PRIMARY KEY (character_id, stat_type)` làm luôn việc của `UNIQUE` — cùng bài học "ràng buộc ở DB chứ
không ở code" của Phase 5 và Phase 13.

**③ DbCmd** dải 1200–1299 (Character): `CharacterStatsLoad = 1202`, `CharacterStatsSave = 1203`.

**④ Logic.** `PlayerEntity` giữ ba thứ: `StatBlock Allocated`, `int UnspentPoints`, và `StatBlock Stats`
(**kết quả**, không lưu DB, tính lại mỗi lần nguyên liệu đổi).

`StatService.Allocate(entity, stat, amount)` hỏi đủ trước khi làm:

1. `stat` có phải chỉ số **gốc** không — cộng điểm vào `MaxHp` là đi đường tắt qua công thức.
2. `amount > 0` và `<= UnspentPoints`.
3. Đúng rồi thì: trừ điểm, cộng vào `Allocated`, **`Recompute()`**, đánh dấu dirty, đẩy `StatsUpdate`.

Phép kiểm 1 tồn tại vì enum không diễn đạt được nó — `StatType.MaxHp` là một giá trị hợp lệ về mặt kiểu.
Đây là chỗ hiếm hoi mà "chặn bằng kiểu" không làm được, nên phải chặn bằng câu lệnh **và** phải có một
bài test cho nó. Cách chặn bằng kiểu nếu muốn đi xa hơn: tách hẳn `PrimaryStat` thành enum riêng và để
`StatType` nhận ngầm từ nó.

**Lên cấp cộng điểm ở đâu?** Chưa có EXP (Phase 15), nên phase này cho `stat_points` một nguồn duy nhất:
`PointsPerLevel` lúc **tạo nhân vật**, cộng thêm một phím `P` trên console server để thử. Đường lên cấp
thật sẽ cắm vào đúng chỗ này ở Phase 15 — và lúc đó nó chỉ là một lời gọi, vì cả pipeline đã sẵn.

**⑥ UI.** Bảng thông tin hiện thêm dòng "Điểm chưa cộng: N" và một nút `+` cạnh mỗi chỉ số gốc. Nút `+`
gửi gói rồi **chờ** — không tự cộng, cùng lý do đã nói ở Phase 13 câu 3.

### ✅ CHECKPOINT B

1. Nhân vật mới có `PointsPerLevel` điểm chưa cộng; bảng thông tin hiện đúng số đó.
2. Bấm `+` ở Sức mạnh → sau một nhịp RTT: Sức mạnh +1, Sát thương +2, Điểm chưa cộng −1. **Cả ba đổi
   cùng lúc**, vì chúng đến trong cùng một `StatsUpdate`.
3. Bấm tới khi hết điểm → nút không còn tác dụng; sửa tạm client bấm thêm → server từ chối, log Warn.
4. Sửa tạm client gửi `StatAllocate { Stat = MaxHp }` → server từ chối.
5. Logout, vào lại → điểm đã cộng còn nguyên, chỉ số khớp.
6. Xoá dòng trong `character_stat` bằng DB Browser rồi vào lại → chỉ số về nền, **không lỗi**. (Dữ liệu
   thiếu là chuyện sẽ xảy ra; hàng rào là "không có dòng nào" phải hợp lệ như "có dòng bằng 0".)

<details>
<summary><b>📖 Lời giải — DB: migration 5, DTO, repository, handler</b></summary>

**`Server/DBServer/Data/Migrator.cs`** — thêm vào cuối mảng `_migrations`:

```csharp
            (5, """
                -- Số điểm CHƯA tiêu. Là một con số của nhân vật → thêm cột.
                ALTER TABLE character ADD COLUMN stat_points INTEGER NOT NULL DEFAULT 0;

                -- Điểm ĐÃ cộng vào từng chỉ số. Là một QUAN HỆ một-nhiều → thêm bảng.
                -- Cái gì có thể nhiều lên thì thành HÀNG, không thành CỘT: chỉ số gốc thứ năm sẽ chỉ
                -- là một giá trị enum mới, không phải một ALTER TABLE nữa.
                CREATE TABLE character_stat (
                    character_id INTEGER NOT NULL REFERENCES character(id) ON DELETE CASCADE,
                    stat_type    INTEGER NOT NULL,
                    points       INTEGER NOT NULL,
                    PRIMARY KEY (character_id, stat_type)
                );
                """),
```

**`Server/Shared/Dto/Db/StatDbDto.cs`** (file mới, nguyên văn):

```csharp
using System;
using MemoryPack;
using MMORPG.Shared.World.Stat;

namespace MMORPG.Shared.Dto.Db
{
    /// <summary>Một dòng của bảng <c>character_stat</c>: điểm người chơi đã cộng vào một chỉ số.</summary>
    [MemoryPackable]
    public partial class CharacterStatRow
    {
        public StatType Stat { get; set; }

        public int Points { get; set; }
    }

    [MemoryPackable]
    public partial class CharacterStatsLoadRequest
    {
        public long CharacterId { get; set; }
    }

    [MemoryPackable]
    public partial class CharacterStatsLoadResponse
    {
        public CharacterStatRow[] Stats { get; set; } = Array.Empty<CharacterStatRow>();

        /// <summary>
        /// Điểm CHƯA tiêu, đọc từ cột <c>character.stat_points</c>. Đi cùng gói này chứ không tách
        /// một lệnh riêng: hai con số ấy luôn được đọc cùng nhau, và hai lệnh thì có một khoảng thời
        /// gian client đã có cái này mà chưa có cái kia.
        /// </summary>
        public int UnspentPoints { get; set; }
    }

    /// <summary>
    /// Ghi cả điểm đã cộng lẫn điểm chưa tiêu, trong một transaction. Cùng lý do với
    /// <c>InventorySaveRequest</c>: hai nửa của một sự thật thì đi cùng nhau hoặc không đi.
    /// </summary>
    [MemoryPackable]
    public partial class CharacterStatsSaveRequest
    {
        public long CharacterId { get; set; }

        public CharacterStatRow[] Stats { get; set; } = Array.Empty<CharacterStatRow>();

        public int UnspentPoints { get; set; }
    }
}
```

**`Server/Shared/Db/DbCmd.cs`** — hai giá trị vào **cuối dải Character**, không chèn giữa:

```csharp
        /// <summary>
        /// Đọc điểm đã cộng + điểm chưa tiêu của một nhân vật.
        /// Request: <see cref="Dto.Db.CharacterStatsLoadRequest"/> · Response: <see cref="Dto.Db.CharacterStatsLoadResponse"/>
        /// </summary>
        CharacterStatsLoad = 1202,

        /// <summary>
        /// Ghi cả hai nửa trong một transaction.
        /// Request: <see cref="Dto.Db.CharacterStatsSaveRequest"/> · Response: <see cref="Dto.Db.DbOkResponse"/>
        /// </summary>
        CharacterStatsSave = 1203,
```

**`Server/DBServer/Repositories/CharacterStatRepository.cs`** (file mới, nguyên văn):

```csharp
using Microsoft.Data.Sqlite;
using MMORPG.DBServer.Data;
using MMORPG.Shared.Dto.Db;
using MMORPG.Shared.World.Stat;

namespace MMORPG.DBServer.Repositories
{
    /// <summary>
    /// Điểm cộng của nhân vật: bảng <c>character_stat</c> và cột <c>character.stat_points</c>.
    ///
    /// Hai chỗ lưu cho một khái niệm, và đó là đúng: điểm CHƯA tiêu là một con số của nhân vật (→ cột),
    /// điểm ĐÃ cộng là một quan hệ một-nhiều (→ bảng). Đọc và ghi luôn đi cùng nhau nên chúng dùng
    /// chung một cặp hàm, và ghi thì trong cùng một transaction.
    /// </summary>
    public sealed class CharacterStatRepository
    {
        private readonly Database _database;

        public CharacterStatRepository(Database database)
        {
            _database = database;
        }

        public async Task<CharacterStatsLoadResponse> LoadAsync(CharacterStatsLoadRequest request, CancellationToken ct = default)
        {
            await using SqliteConnection connection = await _database.OpenAsync(ct);

            var rows = new List<CharacterStatRow>();

            await using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = """
                                      SELECT stat_type, points
                                      FROM character_stat
                                      WHERE character_id = $characterId;
                                      """;
                command.Parameters.AddWithValue("$characterId", request.CharacterId);

                await using SqliteDataReader reader = await command.ExecuteReaderAsync(ct);

                while (await reader.ReadAsync(ct))
                {
                    rows.Add(new CharacterStatRow
                    {
                        Stat = (StatType)reader.GetInt32(0),
                        Points = reader.GetInt32(1),
                    });
                }
            }

            int unspent = 0;

            await using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT stat_points FROM character WHERE id = $characterId;";
                command.Parameters.AddWithValue("$characterId", request.CharacterId);

                object value = await command.ExecuteScalarAsync(ct);

                // null = không có dòng nhân vật nào. Không ném: chỗ gọi vừa GetOrCreate xong nên
                // chuyện đó gần như không xảy ra, và nếu xảy ra thì 0 điểm là hành vi an toàn.
                if (value != null && value != System.DBNull.Value)
                    unspent = System.Convert.ToInt32(value);
            }

            return new CharacterStatsLoadResponse { Stats = rows.ToArray(), UnspentPoints = unspent };
        }

        /// <summary>
        /// Ghi cả hai nửa trong MỘT transaction. Nửa chừng mà chết thì người chơi hoặc mất điểm đã
        /// cộng (bảng ghi xong, cột chưa) hoặc được nhân đôi điểm (cột ghi xong, bảng chưa) — và cái
        /// thứ hai là một lỗ nhân bản điểm chỉ số.
        /// </summary>
        public async Task SaveAsync(CharacterStatsSaveRequest request, CancellationToken ct = default)
        {
            await using SqliteConnection connection = await _database.OpenAsync(ct);
            await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct);

            await using (SqliteCommand delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM character_stat WHERE character_id = $characterId;";
                delete.Parameters.AddWithValue("$characterId", request.CharacterId);

                await delete.ExecuteNonQueryAsync(ct);
            }

            await using (SqliteCommand insert = connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText = """
                                     INSERT INTO character_stat (character_id, stat_type, points)
                                     VALUES ($characterId, $statType, $points);
                                     """;

                insert.Parameters.AddWithValue("$characterId", request.CharacterId);

                SqliteParameter statType = insert.Parameters.AddWithValue("$statType", 0);
                SqliteParameter points = insert.Parameters.AddWithValue("$points", 0);

                foreach (CharacterStatRow row in request.Stats)
                {
                    // Bỏ dòng 0 điểm: "không có dòng nào" và "có dòng bằng 0" phải cùng nghĩa, và
                    // chọn cách ít dòng hơn thì bảng không phình theo số chỉ số.
                    if (row.Points == 0)
                        continue;

                    statType.Value = (int)row.Stat;
                    points.Value = row.Points;

                    await insert.ExecuteNonQueryAsync(ct);
                }
            }

            await using (SqliteCommand update = connection.CreateCommand())
            {
                update.Transaction = transaction;
                update.CommandText = "UPDATE character SET stat_points = $points WHERE id = $characterId;";
                update.Parameters.AddWithValue("$characterId", request.CharacterId);
                update.Parameters.AddWithValue("$points", request.UnspentPoints);

                await update.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);
        }
    }
}
```

**`Server/DBServer/Handlers/CharacterStatDbHandler.cs`** (file mới, nguyên văn):

```csharp
using MMORPG.DBServer.Net;
using MMORPG.DBServer.Repositories;
using MMORPG.Shared.Db;
using MMORPG.Shared.Dto.Db;

namespace MMORPG.DBServer.Handlers
{
    public static class CharacterStatDbHandler
    {
        /// <summary>Gán một lần trong <c>Program.cs</c> của DBServer.</summary>
        public static CharacterStatRepository Repository { get; set; }

        [DbHandler(DbCmd.CharacterStatsLoad)]
        public static async Task<DbResult> OnLoad(DbRequest req)
        {
            return DbResult.Ok(await Repository.LoadAsync(req.GetData<CharacterStatsLoadRequest>()));
        }

        [DbHandler(DbCmd.CharacterStatsSave)]
        public static async Task<DbResult> OnSave(DbRequest req)
        {
            await Repository.SaveAsync(req.GetData<CharacterStatsSaveRequest>());

            return DbResult.Ok(new DbOkResponse { Success = true });
        }
    }
}
```

**`Server/DBServer/Program.cs`** — ⚠️ một dòng, quên là `NullReferenceException` ở query đầu:

```csharp
CharacterStatDbHandler.Repository = new CharacterStatRepository(database);
```

</details>

<details>
<summary><b>📖 Lời giải — <code>PlayerEntity</code> và <code>StatService</code></b></summary>

**`Server/GameServer/World/PlayerEntity.cs`** — thêm vào sau property `Inventory`:

```csharp
        /// <summary>
        /// Điểm người chơi đã tự cộng. NGUYÊN LIỆU của pipeline chỉ số, và là thứ duy nhất trong ba
        /// nguyên liệu được lưu DB (hai cái kia: cấp độ ở bảng character, trang bị ở bảng inventory).
        /// </summary>
        public StatBlock Allocated { get; private set; } = new();

        /// <summary>Điểm chưa tiêu.</summary>
        public int UnspentPoints { get; private set; }

        /// <summary>
        /// KẾT QUẢ của pipeline. Không bao giờ lưu DB — lưu một giá trị tính được là tạo ra bản thứ
        /// hai của cùng một sự thật, và ngày sửa công thức thì nửa số người chơi mang con số cũ.
        ///
        /// Chỉ <see cref="Recompute"/> được gán vào đây.
        /// </summary>
        public StatBlock Stats { get; private set; } = new();

        /// <summary>Sinh lực hiện tại. TRẠNG THÁI, không tính ra được — nên nó phải lưu DB.</summary>
        public int Hp { get; private set; }

        public int Mp { get; private set; }

        /// <summary>
        /// Tính lại toàn bộ chỉ số từ ba nguyên liệu. Gọi sau MỌI thay đổi của nguyên liệu: lên cấp,
        /// cộng điểm, mặc/cởi đồ, và một lần lúc vào world.
        ///
        /// KẸP Hp/Mp xuống trần mới, nhưng KHÔNG đổ đầy: cởi giáp làm MaxHp tụt thì máu phải tụt theo,
        /// còn mặc lại thì MaxHp lên mà máu GIỮ NGUYÊN. Nếu khôi phục theo tỉ lệ cho "công bằng" thì
        /// người chơi có một nút hồi máu miễn phí — cởi ra, mặc vào, lặp lại.
        /// </summary>
        public void Recompute(IReadOnlyList<ItemConfig> equipped)
        {
            Stats = StatCalculator.Compute(ClassId, Level, Allocated, equipped);

            Hp = Math.Min(Hp, Stats[StatType.MaxHp]);
            Mp = Math.Min(Mp, Stats[StatType.MaxMp]);
        }

        /// <summary>
        /// Nạp nguyên liệu từ DB rồi tính lần đầu. CHỈ StatService gọi, và chỉ một lần mỗi phiên.
        /// Đổ đầy Hp/Mp ở đây là đúng — đây là lúc nhân vật bước vào world, không phải một thao tác
        /// giữa chừng.
        /// </summary>
        public void LoadStats(StatBlock allocated, int unspentPoints, IReadOnlyList<ItemConfig> equipped)
        {
            Allocated = allocated;
            UnspentPoints = unspentPoints;

            Recompute(equipped);

            Hp = Stats[StatType.MaxHp];
            Mp = Stats[StatType.MaxMp];
        }

        /// <summary>Tiêu một số điểm vào một chỉ số. Mọi phép kiểm đã làm ở StatService.</summary>
        public void SpendPoints(StatType stat, int amount)
        {
            Allocated[stat] += amount;
            UnspentPoints -= amount;
        }

        /// <summary>Thưởng điểm. Phase 15 gọi hàm này khi lên cấp; hôm nay là phím P trên console.</summary>
        public void GrantPoints(int amount)
        {
            UnspentPoints += amount;
        }
```

Thêm ba `using`: `MMORPG.Shared.World.Item`, `MMORPG.Shared.World.Stat`, và `System`
(cho `Math.Min`).

**`Server/GameServer/World/StatService.cs`** (file mới, nguyên văn):

```csharp
using System.Collections.Concurrent;
using MMORPG.GameServer.Db;
using MMORPG.ServerCore;
using MMORPG.Shared.Db;
using MMORPG.Shared.Dto.Character;
using MMORPG.Shared.Dto.Db;
using MMORPG.Shared.Dto.Inventory;
using MMORPG.Shared.Net;
using MMORPG.Shared.World.Item;
using MMORPG.Shared.World.Stat;

namespace MMORPG.GameServer.World
{
    /// <summary>
    /// Pipeline chỉ số, một chiều: nguyên liệu đổi → <c>Recompute</c> → đẩy <c>StatsUpdate</c>.
    ///
    /// Mọi đường làm đổi chỉ số đều phải đi qua đây, và mỗi đường kết thúc bằng đúng ba việc: tính
    /// lại, đánh dấu dirty, gửi gói. Bỏ một trong ba là có một trạng thái mà client không biết.
    /// </summary>
    public sealed class StatService
    {
        private readonly DbClient _dbClient;

        // Cùng khuôn với _grants của InventoryService: luồng đọc phím ghi, luồng tick đọc.
        private readonly ConcurrentQueue<int> _grantPoints = new();

        public StatService(DbClient dbClient)
        {
            _dbClient = dbClient;
        }

        /// <summary>Thưởng điểm cho mọi người trong world. Phím `P` trên console gọi hàm này.</summary>
        public void EnqueueGrantPointsAll(int amount)
        {
            _grantPoints.Enqueue(amount);
        }

        /// <summary>
        /// Nạp nguyên liệu từ DB rồi tính lần đầu. Gọi SAU <c>InventoryService.LoadAsync</c> — trang
        /// bị là một trong ba nguyên liệu, và cái túi phải có mặt trước khi tính.
        /// </summary>
        public async Task LoadAsync(PlayerEntity entity)
        {
            var allocated = new StatBlock();
            int unspent = 0;

            try
            {
                var response = await _dbClient.CallAsync<CharacterStatsLoadRequest, CharacterStatsLoadResponse>(
                    DbCmd.CharacterStatsLoad, new CharacterStatsLoadRequest { CharacterId = entity.CharacterId });

                foreach (CharacterStatRow row in response.Stats)
                {
                    // Chỉ số không còn trong enum (bảng cũ, chỉ số bị gỡ): bỏ qua. Cùng cách xử lý
                    // với templateId lạ trong túi — dữ liệu cũ không được chặn người chơi vào game.
                    if (!System.Enum.IsDefined(typeof(StatType), row.Stat))
                        continue;

                    allocated[row.Stat] += row.Points;
                }

                unspent = response.UnspentPoints;
            }
            catch (DbUnavailableException ex)
            {
                // Khác túi đồ: chỉ số tính LẠI được từ bảng + cấp độ, nên mất điểm cộng là mất một
                // phần chứ không phải mất tất cả. Vào world với điểm cộng = 0 vẫn chơi được, và lần
                // lưu kế tiếp sẽ ghi đè — nên phải chặn autosave cho tới khi nạp lại được.
                Log.Error($"Không nạp được chỉ số của {entity.Name.Cyan()}: {ex.Message}. Đá khỏi world.");
                entity.Owner?.Kick("Không đọc được dữ liệu nhân vật. Thử lại sau giây lát.");

                return;
            }

            entity.LoadStats(allocated, unspent, entity.Inventory.EquippedConfigs());

            Log.Info($"Chỉ số {entity.Name.Cyan()}: HP {entity.Stats[StatType.MaxHp]} · " +
                     $"ATK {entity.Stats[StatType.Attack]} · điểm chưa cộng {entity.UnspentPoints}");

            Send(entity);
        }

        public async Task SaveAsync(PlayerEntity entity)
        {
            var rows = new List<CharacterStatRow>();

            foreach (StatType stat in System.Enum.GetValues(typeof(StatType)))
            {
                if (!StatTypes.IsPrimary(stat) || entity.Allocated[stat] == 0)
                    continue;

                rows.Add(new CharacterStatRow { Stat = stat, Points = entity.Allocated[stat] });
            }

            try
            {
                await _dbClient.CallAsync<CharacterStatsSaveRequest, DbOkResponse>(
                    DbCmd.CharacterStatsSave, new CharacterStatsSaveRequest
                    {
                        CharacterId = entity.CharacterId,
                        Stats = rows.ToArray(),
                        UnspentPoints = entity.UnspentPoints,
                    });
            }
            catch (DbUnavailableException ex)
            {
                Log.Warn($"Không lưu được chỉ số của {entity.Name.Cyan()}: {ex.Message}");
            }
        }

        //--------------------------------------------------------------------------------------------
        // Ba lệnh do client xin
        //--------------------------------------------------------------------------------------------

        /// <summary>
        /// Cộng điểm vào một chỉ số gốc.
        ///
        /// Ba phép kiểm, và phép đầu tiên là phép duy nhất KHÔNG diễn đạt được bằng kiểu dữ liệu:
        /// <c>StatType.MaxHp</c> là một giá trị enum hợp lệ, nên một client sửa code gửi lên đúng nó
        /// để đi đường tắt qua công thức dẫn xuất.
        /// </summary>
        public void Allocate(PlayerEntity entity, StatType stat, int amount)
        {
            if (!StatTypes.IsPrimary(stat))
            {
                Log.Warn($"{entity.Name} xin cộng điểm vào {stat.ToString().Red()} — không phải chỉ số gốc.");
                return;
            }

            if (amount <= 0 || amount > entity.UnspentPoints)
            {
                Log.Warn($"{entity.Name} xin cộng {amount} điểm nhưng chỉ có {entity.UnspentPoints}.");
                return;
            }

            entity.SpendPoints(stat, amount);
            entity.Recompute(entity.Inventory.EquippedConfigs());

            Send(entity);
        }

        /// <summary>
        /// Mặc món ở một ô túi. Hai gói đi ra: <c>InventoryDelta</c> (ô túi vừa đổi) và
        /// <c>StatsUpdate</c> — hai model, một thao tác.
        /// </summary>
        public void Equip(PlayerEntity entity, int slot)
        {
            IReadOnlyList<int> changed = entity.Inventory.TryEquip(slot);

            if (changed.Count == 0)
                return;

            AfterEquipChange(entity, changed);
        }

        public void Unequip(PlayerEntity entity, EquipSlot equipSlot)
        {
            IReadOnlyList<int> changed = entity.Inventory.TryUnequip(equipSlot);

            if (changed.Count == 0)
                return;

            AfterEquipChange(entity, changed);
        }

        /// <summary>
        /// Thứ tự ba việc này là bắt buộc: tính lại TRƯỚC khi gửi (nếu không thì client nhận bộ số
        /// cũ), và gửi cả hai gói (nếu không thì một trong hai model sai cho tới thao tác kế tiếp).
        /// </summary>
        private void AfterEquipChange(PlayerEntity entity, IReadOnlyList<int> changedSlots)
        {
            entity.Recompute(entity.Inventory.EquippedConfigs());

            InventoryService.SendDelta(entity, changedSlots);

            Send(entity);
        }

        //--------------------------------------------------------------------------------------------

        /// <summary>
        /// Đẩy TOÀN BỘ bộ chỉ số, không phải delta — ngược hẳn túi đồ, và có lý do:
        ///
        /// Chỉ số phụ thuộc lẫn nhau. Cộng một điểm Sức mạnh thì Sát thương đổi; cộng Thể lực thì cả
        /// MaxHp lẫn Phòng thủ đổi. "Cái gì vừa đổi" gần như luôn là "gần hết". Tệ hơn: một NỬA bộ
        /// chỉ số là một object vô nghĩa — Sát thương của bộ cũ ghép với Phòng thủ của bộ mới không
        /// mô tả nhân vật nào cả.
        ///
        /// Túi đồ thì ngược lại: 30 ô độc lập. Đó mới là điều kiện để delta có nghĩa.
        /// </summary>
        public static void Send(PlayerEntity entity)
        {
            var equipped = new List<EquippedSlotDto>();

            foreach (EquipSlot equipSlot in System.Enum.GetValues(typeof(EquipSlot)))
            {
                if (equipSlot == EquipSlot.None)
                    continue;

                ItemStack stack = entity.Inventory.GetEquipped(equipSlot);

                if (stack.IsEmpty)
                    continue;

                equipped.Add(new EquippedSlotDto { EquipSlot = equipSlot, TemplateId = stack.TemplateId });
            }

            entity.Owner?.SendData(NetCmd.StatsUpdate, new StatsUpdateNotice
            {
                Stats = entity.Stats,
                UnspentPoints = entity.UnspentPoints,
                Hp = entity.Hp,
                Mp = entity.Mp,
                Equipped = equipped.ToArray(),
            });
        }

        /// <summary>Gọi mỗi tick từ <c>WorldService.Tick</c>, ngay sau <c>InventoryService.Tick</c>.</summary>
        public void Tick(ICollection<PlayerEntity> entities)
        {
            while (_grantPoints.TryDequeue(out int amount))
            {
                foreach (PlayerEntity entity in entities)
                {
                    entity.GrantPoints(amount);
                    Send(entity);
                }

                Log.Info($"Thưởng {amount.ToString().Green()} điểm cho {entities.Count} người.");
            }
        }
    }
}
```

</details>

<details>
<summary><b>📖 Lời giải — contract và <code>StatHandler</code></b></summary>

**`Server/Shared/Net/NetCmd.cs`** — bốn giá trị vào **cuối dải Character (200–299)**:

```csharp
        /// <summary>
        /// Toàn bộ chỉ số + điểm chưa cộng + trang bị đang mặc. Server đẩy mỗi lần có gì đó đổi.
        /// Payload: <see cref="Dto.Character.StatsUpdateNotice"/>
        /// </summary>
        StatsUpdate = 201,

        /// <summary>
        /// Cộng điểm vào một chỉ số gốc. Kết quả đi bằng StatsUpdate.
        /// Payload: <see cref="Dto.Character.StatAllocateRequest"/>
        /// </summary>
        StatAllocate = 202,

        /// <summary>
        /// Mặc món ở một ô túi. Kết quả đi bằng InventoryDelta + StatsUpdate.
        /// Payload: <see cref="Dto.Character.EquipItemRequest"/>
        /// </summary>
        EquipItem = 203,

        /// <summary>
        /// Cởi món ở một ô trang bị về túi.
        /// Payload: <see cref="Dto.Character.UnequipItemRequest"/>
        /// </summary>
        UnequipItem = 204,
```

**`Server/Shared/Dto/Character/StatsDto.cs`** (file mới, nguyên văn):

```csharp
using System;
using MemoryPack;
using MMORPG.Shared.World.Item;
using MMORPG.Shared.World.Stat;

namespace MMORPG.Shared.Dto.Character
{
    /// <summary>Một ô trang bị đang có đồ. Chỉ TemplateId — client tra bảng item để lấy tên và icon.</summary>
    [MemoryPackable]
    public partial struct EquippedSlotDto
    {
        public EquipSlot EquipSlot;

        public int TemplateId;
    }

    /// <summary>
    /// TOÀN BỘ trạng thái chỉ số, gửi lại mỗi lần có gì đó đổi. Snapshot chứ không delta — xem ghi
    /// chú ở <c>StatService.Send</c>: một nửa bộ chỉ số là một object vô nghĩa.
    /// </summary>
    [MemoryPackable]
    public partial class StatsUpdateNotice
    {
        public StatBlock Stats { get; set; }

        public int UnspentPoints { get; set; }

        /// <summary>Sinh lực / nội lực HIỆN TẠI. Trạng thái, không tính ra được từ Stats.</summary>
        public int Hp { get; set; }

        public int Mp { get; set; }

        public EquippedSlotDto[] Equipped { get; set; } = Array.Empty<EquippedSlotDto>();
    }

    [MemoryPackable]
    public partial class StatAllocateRequest
    {
        public StatType Stat { get; set; }

        public int Amount { get; set; }
    }

    [MemoryPackable]
    public partial class EquipItemRequest
    {
        /// <summary>Ô TÚI chứa món muốn mặc. Server tự tra EquipSlot từ bảng item.</summary>
        public int Slot { get; set; }
    }

    [MemoryPackable]
    public partial class UnequipItemRequest
    {
        public EquipSlot EquipSlot { get; set; }
    }
}
```

**`Server/GameServer/Handlers/StatHandler.cs`** (file mới, nguyên văn):

```csharp
using MMORPG.GameServer.Boot;
using MMORPG.GameServer.Net;
using MMORPG.GameServer.World;
using MMORPG.Shared.Dto.Character;
using MMORPG.Shared.Net;
using MMORPG.Shared.World.Item;
using MMORPG.Shared.World.Stat;

namespace MMORPG.GameServer.Handlers
{
    public static class StatHandler
    {
        private static StatService StatService => ServerServices.Get<StatService>();

        [TcpHandler(NetCmd.StatAllocate, MinState = SessionState.InWorld)]
        public static Task<NetResult> OnAllocate(NetRequest req)
        {
            PlayerEntity entity = req.Session.Entity;

            if (entity == null)
                return Task.FromResult(NetResult.None);

            var request = req.GetData<StatAllocateRequest>();

            // Enum trên dây chỉ là một byte do MÁY KHÁC gửi: (StatType)77 hợp lệ hoàn toàn với C#.
            // Kiểu dữ liệu bảo vệ code khỏi chính mình; kiểm miền giá trị mới là thứ bảo vệ server
            // khỏi người khác. Phép kiểm "có phải chỉ số GỐC không" thì nằm trong StatService.
            if (!Enum.IsDefined(typeof(StatType), request.Stat))
                return Task.FromResult(NetResult.None);

            StatService.Allocate(entity, request.Stat, request.Amount);

            return Task.FromResult(NetResult.None);
        }

        [TcpHandler(NetCmd.EquipItem, MinState = SessionState.InWorld)]
        public static Task<NetResult> OnEquip(NetRequest req)
        {
            PlayerEntity entity = req.Session.Entity;

            if (entity == null)
                return Task.FromResult(NetResult.None);

            StatService.Equip(entity, req.GetData<EquipItemRequest>().Slot);

            return Task.FromResult(NetResult.None);
        }

        [TcpHandler(NetCmd.UnequipItem, MinState = SessionState.InWorld)]
        public static Task<NetResult> OnUnequip(NetRequest req)
        {
            PlayerEntity entity = req.Session.Entity;

            if (entity == null)
                return Task.FromResult(NetResult.None);

            var request = req.GetData<UnequipItemRequest>();

            if (!Enum.IsDefined(typeof(EquipSlot), request.EquipSlot))
                return Task.FromResult(NetResult.None);

            StatService.Unequip(entity, request.EquipSlot);

            return Task.FromResult(NetResult.None);
        }
    }
}
```

**`Server/GameServer/Boot/ServerBootstrap.cs`** — ⚠️ đăng ký, **trước** `WorldService`:

```csharp
            var inventoryService = ServerServices.Register(new InventoryService(dbClient));

            var statService = ServerServices.Register(new StatService(dbClient));

            var worldService = ServerServices.Register(new WorldService(maps, config, inventoryService, statService));

            ServerServices.Register(new AuthService(dbClient, new LoginRateLimiter()));
            ServerServices.Register(new CharacterService(dbClient, worldService, maps, config, inventoryService, statService));
```

**`Server/GameServer/World/WorldService.cs`** — nhận thêm một service và gọi thêm một Tick:

```csharp
        private readonly StatService _statService;

        public WorldService(MapRegistry maps, ConfigService config, InventoryService inventoryService,
            StatService statService)
        {
            _maps = maps;
            _config = config;
            _inventoryService = inventoryService;
            _statService = statService;
```

```csharp
            _inventoryService.Tick(dt, _entities.Values);
            _statService.Tick(_entities.Values);
```

**`Server/GameServer/World/CharacterService.cs`** — nạp SAU túi, lưu cùng lúc với túi:

```csharp
            await _inventoryService.LoadAsync(entity);

            // SAU túi: trang bị là một trong ba nguyên liệu của pipeline chỉ số, và cái túi phải có
            // mặt trước khi tính.
            await _statService.LoadAsync(entity);
```

```csharp
            await _inventoryService.SaveAsync(entity);
            await _statService.SaveAsync(entity);
```

**`Server/GameServer/Program.cs`** — phím `P`:

```csharp
var statService = ServerServices.Get<StatService>();

// ... trong switch của luồng đọc phím:
            // Nguồn điểm cộng của Phase 14. Phase 15 thay nó bằng đường lên cấp thật.
            case ConsoleKey.P:
                statService.EnqueueGrantPointsAll(5);
                break;
```

</details>

<details>
<summary><b>📖 Lời giải — client: model, api, handler, presenter, panel</b></summary>

**`Assets/Game/Scripts/Network/Handlers/StatsNetHandler.cs`** (file mới, nguyên văn):

```csharp
using System;
using MMORPG.Shared.Dto.Character;
using MMORPG.Shared.Net;

namespace MMORPG.Client.Network.Handlers
{
    /// <summary>
    /// Nhận nhóm lệnh chỉ số. Một lệnh duy nhất, và đó là điểm đáng chú ý: chỉ số chỉ có SNAPSHOT,
    /// không có delta — xem lý do ở <c>StatService.Send</c> phía server.
    /// </summary>
    public sealed class StatsNetHandler : INetHandlerGroup
    {
        public event Action<StatsUpdateNotice> OnStatsUpdate;

        [NetHandler(NetCmd.StatsUpdate)]
        private void HandleStatsUpdate(NetPacket packet)
        {
            OnStatsUpdate?.Invoke(packet.GetData<StatsUpdateNotice>());
        }
    }
}
```

**`Assets/Game/Scripts/Stats/StatsModel.cs`** (file mới, nguyên văn):

```csharp
using System;
using MMORPG.Shared.Dto.Character;
using MMORPG.Shared.World.Item;
using MMORPG.Shared.World.Stat;

namespace MMORPG.Client.Stats
{
    /// <summary>
    /// Bản sao bộ chỉ số của chính mình, do server gửi xuống. Cùng luật với <c>LocalPlayer</c> và
    /// <c>InventoryModel</c>: cache chỉ-đọc, không có setter công khai.
    ///
    /// Không có dòng nào ở đây CỘNG một chỉ số vào chỉ số khác. Client được phép chạy
    /// <see cref="StatCalculator"/> để DỰ ĐOÁN cho việc hiển thị (rê chuột vào món đồ thì hiện
    /// "42 → 55"), nhưng con số đang có hiệu lực luôn là con số trong model này.
    /// </summary>
    public sealed class StatsModel
    {
        /// <summary>
        /// Bộ chỉ số hiện hành. Thay NGUYÊN object mỗi lần nhận gói, không sửa từng ô — cùng lý do
        /// với ConfigService.Current ở server: nửa bộ chỉ số là một object vô nghĩa.
        /// </summary>
        public StatBlock Stats { get; private set; } = new();

        public int UnspentPoints { get; private set; }

        public int Hp { get; private set; }

        public int Mp { get; private set; }

        /// <summary>Trang bị đang mặc, tra theo ô. Rỗng = ô đó đang trống.</summary>
        public EquippedSlotDto[] Equipped { get; private set; } = Array.Empty<EquippedSlotDto>();

        /// <summary>Bắn sau MỖI lần nhận gói. Không có tham số "cái gì đổi" — snapshot thì mọi thứ đều có thể đã đổi.</summary>
        public event Action OnChanged;

        public void Apply(StatsUpdateNotice notice)
        {
            // Gói thiếu Stats nghĩa là server và client chạy hai bản Shared khác nhau; giữ bộ cũ còn
            // hơn thay bằng null và để mọi chỗ đọc chỉ số nổ.
            if (notice.Stats == null)
                return;

            Stats = notice.Stats;
            UnspentPoints = notice.UnspentPoints;
            Hp = notice.Hp;
            Mp = notice.Mp;
            Equipped = notice.Equipped ?? Array.Empty<EquippedSlotDto>();

            OnChanged?.Invoke();
        }

        /// <summary>TemplateId đang mặc ở một ô, hoặc 0.</summary>
        public int EquippedTemplate(EquipSlot equipSlot)
        {
            foreach (EquippedSlotDto slot in Equipped)
            {
                if (slot.EquipSlot == equipSlot)
                    return slot.TemplateId;
            }

            return 0;
        }

        public void Clear()
        {
            Stats = new StatBlock();
            UnspentPoints = 0;
            Hp = 0;
            Mp = 0;
            Equipped = Array.Empty<EquippedSlotDto>();

            OnChanged?.Invoke();
        }
    }
}
```

**`Assets/Game/Scripts/Stats/StatsApi.cs`** (file mới, nguyên văn):

```csharp
using MMORPG.Client.Network;
using MMORPG.Shared.Dto.Character;
using MMORPG.Shared.Net;
using MMORPG.Shared.World.Item;
using MMORPG.Shared.World.Stat;

namespace MMORPG.Client.Stats
{
    /// <summary>
    /// Gom mọi lệnh chỉ số / trang bị mà client GỬI ĐI. Không đụng StatsModel một dòng nào: bấm `+`
    /// thì gửi gói rồi chờ, cùng luật với InventoryApi.
    /// </summary>
    public sealed class StatsApi
    {
        private readonly NetService _netService;

        public StatsApi(NetService netService)
        {
            _netService = netService;
        }

        public void Allocate(StatType stat, int amount)
        {
            _netService.Send(NetCmd.StatAllocate, new StatAllocateRequest { Stat = stat, Amount = amount });
        }

        public void Equip(int slot)
        {
            _netService.Send(NetCmd.EquipItem, new EquipItemRequest { Slot = slot });
        }

        public void Unequip(EquipSlot equipSlot)
        {
            _netService.Send(NetCmd.UnequipItem, new UnequipItemRequest { EquipSlot = equipSlot });
        }
    }
}
```

**`Assets/Game/Scripts/Stats/StatsPresenter.cs`** (file mới, nguyên văn):

```csharp
using HungNT;
using MMORPG.Client.Network.Handlers;
using MMORPG.Shared.Dto.Character;
using MMORPG.Shared.World.Stat;
using UnityEngine;
using VContainer;

namespace MMORPG.Client.Stats
{
    /// <summary>
    /// Nối mạng với <see cref="StatsModel"/>. MonoBehaviour cắm sẵn trong scene, không phải panel —
    /// cùng lý do với InventoryPresenter: gói StatsUpdate vẫn tới khi bảng thông tin đang đóng.
    /// </summary>
    public sealed class StatsPresenter : MonoBehaviour
    {
        private StatsNetHandler _statsNetHandler;
        private StatsModel _statsModel;

        [Inject]
        public void Construct(StatsNetHandler statsNetHandler, StatsModel statsModel)
        {
            _statsNetHandler = statsNetHandler;
            _statsModel = statsModel;
        }

        private void Start()
        {
            _statsNetHandler.OnStatsUpdate += OnStatsUpdate;
        }

        private void OnDestroy()
        {
            if (_statsNetHandler == null)
                return;

            _statsNetHandler.OnStatsUpdate -= OnStatsUpdate;
        }

        private void OnStatsUpdate(StatsUpdateNotice notice)
        {
            _statsModel.Apply(notice);

            this.Log($"Chỉ số: HP {_statsModel.Hp}/{_statsModel.Stats[StatType.MaxHp]} · " +
                     $"ATK {_statsModel.Stats[StatType.Attack]} · điểm chưa cộng {_statsModel.UnspentPoints}");
        }
    }
}
```

**`Assets/Game/Scripts/Stats/StatsPanel.cs`** (file mới, nguyên văn) — ba class trong một file
vì hai class sau là *view con* của class đầu, không dùng được ở đâu khác:

```csharp
using System.Collections.Generic;
using HungNT.UI.Panel;
using MMORPG.Client.Inventory;
using MMORPG.Shared.World.Item;
using MMORPG.Shared.World.Stat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace MMORPG.Client.Stats
{
    /// <summary>
    /// Bảng thông tin: chỉ số gốc (có nút +), chỉ số dẫn xuất (chỉ đọc), điểm chưa cộng, và các ô
    /// trang bị.
    ///
    /// Không một dòng nào ở đây tính chỉ số. Bấm `+` thì gửi gói rồi chờ — số trên màn hình chỉ đổi
    /// khi <see cref="StatsModel"/> nhận được gói từ server.
    /// </summary>
    public sealed class StatsPanel : UIPanelBase
    {
        [SerializeField] private TMP_Text _unspentText;
        [SerializeField] private StatRowView[] _primaryRows;
        [SerializeField] private StatRowView[] _derivedRows;
        [SerializeField] private EquipSlotView[] _equipSlots;

        private StatsModel _statsModel;
        private StatsApi _statsApi;

        [Inject]
        public void Construct(StatsModel statsModel, StatsApi statsApi)
        {
            _statsModel = statsModel;
            _statsApi = statsApi;
        }

        private void OnEnable()
        {
            if (_statsModel == null)
                return;

            _statsModel.OnChanged += Render;

            // Vẽ ngay khi mở: trong lúc panel đóng, model vẫn nhận gói. Không vẽ lại thì bảng hiển
            // thị trạng thái của lần đóng trước. Cùng bẫy với InventoryPanel.
            Render();
        }

        private void OnDisable()
        {
            if (_statsModel == null)
                return;

            _statsModel.OnChanged -= Render;
        }

        /// <summary>Nút `+` gọi hàm này. Gửi rồi thôi — không tự cộng một điểm nào lên màn hình.</summary>
        public void RequestAllocate(StatType stat)
        {
            _statsApi.Allocate(stat, 1);
        }

        public void RequestUnequip(EquipSlot equipSlot)
        {
            _statsApi.Unequip(equipSlot);
        }

        /// <summary>
        /// Vẽ lại TOÀN BỘ, không có phiên bản "vẽ lại phần vừa đổi".
        ///
        /// Ngược hẳn InventoryPanel, và đúng vì cùng lý do khiến chỉ số đi bằng snapshot: các chỉ số
        /// phụ thuộc lẫn nhau, nên "cái gì vừa đổi" gần như luôn là "gần hết". Chín dòng text thì vẽ
        /// lại hết rẻ hơn hẳn việc giữ một danh sách ô bẩn.
        /// </summary>
        private void Render()
        {
            _unspentText.text = _statsModel.UnspentPoints.ToString();

            foreach (StatRowView row in _primaryRows)
                row.Render(_statsModel.Stats[row.Stat], _statsModel.UnspentPoints > 0);

            foreach (StatRowView row in _derivedRows)
                row.Render(_statsModel.Stats[row.Stat], canAllocate: false);

            foreach (EquipSlotView slot in _equipSlots)
                slot.Render(_statsModel.EquippedTemplate(slot.EquipSlot));
        }

        /// <summary>
        /// Chỉ số sẽ thành bao nhiêu NẾU mặc món này — cho tooltip "Sát thương 42 → 55".
        ///
        /// Đây là chỗ quyết định "đặt StatCalculator ở Shared" trả tiền. Và cũng là chỗ phải nhớ lại
        /// ranh giới: con số trả về đây chỉ là một NHÃN trên màn hình. Nó không bao giờ được ghi vào
        /// StatsModel — con số có hiệu lực là con số server gửi sau khi thật sự mặc.
        /// </summary>
        public StatBlock Preview(int classId, int level, ItemConfig candidate)
        {
            var equipped = new List<ItemConfig>();

            foreach (EquipSlot equipSlot in System.Enum.GetValues(typeof(EquipSlot)))
            {
                if (equipSlot == EquipSlot.None || equipSlot == candidate.EquipSlot)
                    continue;

                ItemConfig config = ItemConfigContainer.Find(_statsModel.EquippedTemplate(equipSlot));

                if (config != null)
                    equipped.Add(config);
            }

            equipped.Add(candidate);

            return StatCalculator.Compute(classId, level, _statsModel.Stats, equipped);
        }
    }

    /// <summary>Một dòng "Tên chỉ số — giá trị — nút +". Chỉ vẽ và báo bấm.</summary>
    public sealed class StatRowView : MonoBehaviour
    {
        [SerializeField] private StatsPanel _panel;
        [SerializeField] private StatType _stat;
        [SerializeField] private TMP_Text _valueText;
        [SerializeField] private Button _plusButton;

        public StatType Stat
        {
            get { return _stat; }
        }

        private void Awake()
        {
            // Nút + chỉ tồn tại trên dòng chỉ số gốc; dòng dẫn xuất để trống ô này trong prefab.
            if (_plusButton != null)
                _plusButton.onClick.AddListener(OnClickPlus);
        }

        private void OnDestroy()
        {
            if (_plusButton != null)
                _plusButton.onClick.RemoveListener(OnClickPlus);
        }

        public void Render(int value, bool canAllocate)
        {
            _valueText.text = value.ToString();

            if (_plusButton != null)
                _plusButton.gameObject.SetActive(canAllocate);
        }

        private void OnClickPlus()
        {
            _panel.RequestAllocate(_stat);
        }
    }

    /// <summary>Một ô trang bị. Click = cởi ra.</summary>
    public sealed class EquipSlotView : MonoBehaviour
    {
        [SerializeField] private StatsPanel _panel;
        [SerializeField] private EquipSlot _equipSlot;
        [SerializeField] private Image _icon;
        [SerializeField] private Button _button;

        public EquipSlot EquipSlot
        {
            get { return _equipSlot; }
        }

        private void Awake()
        {
            _button.onClick.AddListener(OnClick);
        }

        private void OnDestroy()
        {
            _button.onClick.RemoveListener(OnClick);
        }

        public void Render(int templateId)
        {
            ItemConfig config = templateId == 0 ? null : ItemConfigContainer.Find(templateId);

            _icon.enabled = config != null;

            if (config != null)
                _icon.sprite = Resources.Load<Sprite>(config.IconKey);
        }

        private void OnClick()
        {
            _panel.RequestUnequip(_equipSlot);
        }
    }
}
```

**`Assets/Game/Scripts/Boot/GameLifetimeScope.cs`** — ⚠️ bốn dòng:

```csharp
            // Chỉ số. Cùng bộ bốn dòng như túi đồ ở Phase 13 — và thiếu dòng nào cũng KHÔNG có
            // lỗi biên dịch.
            builder.Register<StatsNetHandler>(Lifetime.Singleton).AsSelf().As<INetHandlerGroup>();
            builder.Register<StatsModel>(Lifetime.Singleton);
            builder.Register<StatsApi>(Lifetime.Singleton);
            builder.RegisterComponentInHierarchy<StatsPresenter>();
```

</details>

---

## Bước 3 — Trang bị: nối cái túi vào pipeline

### Hướng làm

**Mở rộng bảng item, không đổi format.** `ItemConfig` thêm hai trường **tuỳ chọn**:

```json
{
  "TemplateId": 100,
  "Name": "Kiếm gỗ",
  "Kind": "Equipment",
  "MaxStack": 1,
  "EquipSlot": "Weapon",
  "Bonuses": [ { "Stat": "Strength", "Value": 3 } ]
}
```

Trường tuỳ chọn **thêm vào** thì `Version` giữ nguyên — đúng luật đã chốt ở `MapFile.FORMAT_VERSION`
(Phase 10) và đã dùng một lần cho `Portals` (Phase 11). File cũ không có hai trường này vẫn đọc được,
`EquipSlot` về `None`, `Bonuses` về mảng rỗng. Ba lần áp dụng cùng một luật là lúc nó thành phản xạ.

Vân tay **tự động** đổi theo hai trường mới — `ConfigFingerprint` băm byte đã tuần tự hoá nên không
phải nhớ thêm gì. Và đổi là đúng: một server chạy bảng có bonus và một client chạy bảng không có là
hai thế giới khác nhau.

**① DB — migration 6.**

```sql
-- 0 = đang nằm trong túi; > 0 = đang mặc ở ô trang bị đó.
ALTER TABLE inventory_item ADD COLUMN equip_slot INTEGER NOT NULL DEFAULT 0;
```

Món đồ **đang mặc rời khỏi ô túi** (`slot = -1`), nên hai chỉ mục UNIQUE phải thành **chỉ mục có điều
kiện**:

```sql
DROP INDEX idx_inventory_slot;

CREATE UNIQUE INDEX idx_inventory_slot ON inventory_item (character_id, slot) WHERE slot >= 0;
CREATE UNIQUE INDEX idx_inventory_equip ON inventory_item (character_id, equip_slot) WHERE equip_slot > 0;
```

Hai dòng ấy phát biểu bằng SQL đúng hai luật của game: *một ô túi giữ một chồng* và **một ô trang bị mặc
một món**. Không dòng code C# nào phải nhớ hai luật đó nữa — và cái không phải nhớ thì không bị quên.

> Đây là lần thứ ba dự án đẩy một bất biến xuống DB (Phase 4: `UNIQUE(username)`; Phase 5:
> `UNIQUE(account_id)`; hôm nay: hai chỉ mục có điều kiện). Khuôn chung: **bất biến nào phát biểu được
> bằng schema thì đừng phát biểu bằng `if`.** `if` chỉ đúng khi mọi đường đi đều nhớ gọi nó; schema thì
> đúng kể cả khi có người sửa DB bằng tay.

**Vì sao trang bị nằm trong bảng `inventory_item` chứ không phải bảng riêng.** Vì mặc một món đồ **không
tạo ra một vật mới** — vẫn đúng cái `itemId` ấy, chỉ là nó đang ở chỗ khác. Tách bảng nghĩa là mặc/cởi
thành `DELETE` + `INSERT` ở hai bảng, và ngày nào một trong hai nửa thất bại thì món đồ hoặc **biến mất**
hoặc **nhân đôi**. `ROADMAP.md §2` đã xếp trang bị vào dải Inventory (1300–1399) từ đầu, chính vì lý do
này.

**④ Logic — hai lệnh, và một chuỗi thao tác phải trọn vẹn.**

`Inventory.TryEquip(slot)` — và nó ngắn hơn bạn nghĩ, nhờ một quan sát:

1. Ô `slot` có đồ không; config có `Kind == Equipment` và `EquipSlot != None` không.
2. (Phase sau: kiểm cấp độ, kiểm lớp nhân vật.)
3. **Hoán đổi một-đổi-một**: món cũ ở ô trang bị về đúng ô túi mà món mới vừa rời khỏi.

Bước 3 là chỗ đáng dừng lại. Cách viết tự nhiên là: *"cởi món cũ về túi trước; túi đầy thì từ chối cả
thao tác"* — đúng tinh thần all-or-nothing của `TryAdd`, nhưng **thừa một phép kiểm**. Ô túi vừa nhấc
món mới ra đang **trống**, nên món cũ luôn có sẵn ít nhất chỗ đó để về. Không phải tìm ô trống, không
có nhánh thất bại nào:

```csharp
ItemStack incoming = _slots[slot];
ItemStack outgoing = GetEquipped(config.EquipSlot);   // rỗng nếu chưa mặc gì

_slots[slot] = outgoing;
_equipped[config.EquipSlot] = incoming;
```

> Bài học chung: **trước khi viết một phép kiểm, hỏi xem cách sắp xếp thao tác có làm nó thành thừa
> không.** Một nhánh lỗi không tồn tại thì không có gì để test và không có gì để sai.

Chiều ngược lại (`TryUnequip`) thì **vẫn cần** phép kiểm ấy: cởi ra là thêm một món vào túi mà không
lấy món nào ra, nên túi đầy thì từ chối trọn.

Sau khi túi đã đổi, `StatService.AfterEquipChange` làm nốt ba việc, và **thứ tự là bắt buộc**:

1. `entity.Recompute(...)` — tính lại chỉ số, và **kẹp `Hp`/`Mp` về trần mới**.
2. `InventoryService.SendDelta(...)` — ô túi vừa đổi.
3. `StatService.Send(...)` — bộ chỉ số mới.

**Bước kẹp `Hp` là chỗ có một cái bẫy đáng tiền.** Cởi giáp làm `MaxHp` tụt từ 200 xuống 150 trong khi
máu hiện tại đang là 180 — phải kẹp xuống 150. Đến đây ai cũng đồng ý. Câu hỏi thật là chiều ngược
lại: **mặc lại giáp thì máu có lên lại 180 không?**

Đáp án là **không**. Máu giữ nguyên 150, chỉ `MaxHp` lên 200. Vì nếu bạn khôi phục theo tỉ lệ cho "công
bằng" thì người chơi có một cái nút hồi máu miễn phí: cởi ra, mặc vào, lặp lại. Mọi hệ thống hồi phục
đều bị dò tìm theo kiểu này, và cách chặn không phải là thêm cooldown mà là **đừng tạo ra nguồn hồi phục
ở chỗ không định tạo**.

> Quy tắc mang đi được: **một thao tác không nhằm hồi phục thì không được làm tăng trạng thái hiện thời.**
> Đổi trần (`MaxHp`) là chuyện của chỉ số; đổ đầy tới trần là chuyện của hồi phục. Hai việc khác nhau,
> đừng để một thao tác làm cả hai.

**⑥ UI.** Bảng thông tin thêm khu vực ô trang bị. Kéo một món từ túi vào ô — hoặc double-click cho
nhanh. Cởi thì ngược lại. Cả hai chỉ gửi gói và chờ.

Và phần thưởng: rê chuột vào món đồ trong túi thì hiện `Sát thương 42 → 55`, tính bằng
`StatCalculator.Compute` với danh sách trang bị giả định. Đây là lúc quyết định "đặt `StatCalculator` ở
`Shared`" trả tiền — và cũng là lúc phải nhớ lại ranh giới: con số 55 kia chỉ là **nhãn trên màn hình**
cho tới khi server gửi `StatsUpdate` thật.

### ✅ CHECKPOINT C — mục tiêu cuối Phase 14

1. Gõ `G` nhận một Kiếm gỗ. Bảng thông tin: Sát thương 42.
2. Mặc vào → ô túi trống đi, ô Vũ khí có kiếm, Sát thương 48. **Một thao tác, hai model cùng đúng.**
3. Cởi ra → về đúng 42, và kiếm về ô túi trống đầu tiên.
4. **Mặc-cởi năm mươi lần** (giữ phím), rồi so Sát thương với lúc đầu → **đúng bằng 42**. Đây là bài
   kiểm tra cho "tính lại từ đầu"; bản cộng-dồn sẽ trôi và bạn sẽ thấy nó ngay.
5. Túi đầy 30 ô, đang mặc kiếm A, mặc kiếm B từ ô cuối → **vẫn mặc được**, và A về đúng ô B vừa rời.
   Đây là phép hoán đổi một-đổi-một: nó không có nhánh thất bại. Rồi thử chiều ngược lại — túi đầy
   30 ô mà bấm CỞI → **không có gì xảy ra**, đúng chủ đích: cởi ra là thêm một món mà không lấy món
   nào ra.
6. Giả lập mất máu (phím trên console), cởi giáp → máu bị kẹp xuống `MaxHp` mới. Mặc lại → `MaxHp` lên,
   **máu không lên**.
7. Logout, vào lại → vẫn đang mặc đúng món đó, chỉ số khớp, máu đúng.
8. Sửa tạm client gửi `EquipItem` lên một ô trống, rồi lên một Bình máu → server từ chối cả hai, log
   Warn, không tick nào chết.


<details>
<summary><b>📖 Lời giải — mở rộng bảng item: <code>EquipSlot</code> + <code>Bonuses</code></b></summary>

**`Server/Shared/World/Item/EquipSlot.cs`** (file mới, nguyên văn):

```csharp
namespace MMORPG.Shared.World.Item
{
    /// <summary>
    /// Ô trang bị trên người. <c>None = 0</c> để <c>default</c> đúng nghĩa "không mặc được" — cùng
    /// quy ước với <c>TemplateId = 0</c> nghĩa là ô túi trống.
    ///
    /// Giá trị này cũng đi thẳng vào cột <c>inventory_item.equip_slot</c>, nên nó là một phần của
    /// LƯỢC ĐỒ DB: đánh số lại nó là một cuộc di cư, không phải một thao tác Rename.
    /// </summary>
    public enum EquipSlot : byte
    {
        None = 0,
        Weapon = 1,
        Armor = 2,
        Helmet = 3,
        Boots = 4,
    }
}
```

**`Server/Shared/World/Item/ItemConfig.cs`** — hai property TUỲ CHỌN, thêm vào cuối class:

```csharp
        /// <summary>
        /// Mặc vào ô nào. None = không mặc được. Trường TUỲ CHỌN thêm ở Phase 14: file cũ không có
        /// nó vẫn đọc được và về None, nên Version của bảng giữ nguyên — đúng luật đã chốt ở
        /// MapGridParser.FORMAT_VERSION.
        /// </summary>
        public EquipSlot EquipSlot { get; set; }

        /// <summary>Chỉ số món đồ cộng thêm khi ĐANG MẶC. Nằm trong túi thì không cộng gì.</summary>
        public StatBonus[] Bonuses { get; set; } = Array.Empty<StatBonus>();
```

Thêm hai `using`: `System` và `MMORPG.Shared.World.Stat`.

**`ItemTableData` không đổi một dòng nào**, và đó là phần thưởng của việc bỏ `Checksum()` viết tay ở
Phase 12: `ConfigFingerprint` băm byte đã tuần tự hoá, nên hai trường mới **tự** vào vân tay ngay khi
chúng có `[MemoryPackable]`. Không có chỗ nào để quên.

> Với bản cũ (băm tay từng trường) thì đây đúng là chỗ hỏng câm điển hình: thêm `Bonuses` vào
> `ItemConfig` mà quên thêm hai dòng `Fnv1a.Mix` thì vân tay vẫn tính ra bình thường — chỉ là nó
> không còn phát hiện được thay đổi ở bonus nữa. Một server có bonus và một client không có sẽ báo
> "khớp".

**`Assets/Game/Resources/Config/items.json`** — Kiếm gỗ giờ mặc được:

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

</details>

<details>
<summary><b>📖 Lời giải — DB: migration 6 và cột <code>equip_slot</code></b></summary>

**`Server/DBServer/Data/Migrator.cs`**:

```csharp
            (6, """
                -- 0 = đang nằm trong túi; > 0 = đang mặc ở ô trang bị đó.
                ALTER TABLE inventory_item ADD COLUMN equip_slot INTEGER NOT NULL DEFAULT 0;

                -- Món đang mặc RỜI KHỎI ô túi (slot = -1), nên chỉ mục cũ phải thành chỉ mục CÓ ĐIỀU
                -- KIỆN — nếu không thì hai món đang mặc cùng có slot = -1 và vi phạm UNIQUE.
                DROP INDEX idx_inventory_slot;

                CREATE UNIQUE INDEX idx_inventory_slot ON inventory_item (character_id, slot) WHERE slot >= 0;
                CREATE UNIQUE INDEX idx_inventory_equip ON inventory_item (character_id, equip_slot) WHERE equip_slot > 0;
                """),
```

**`Server/Shared/Dto/Db/InventoryDbDto.cs`** — `InventoryRow` thêm một trường và đổi nghĩa
một trường:

```csharp
        /// <summary>Ô túi. -1 khi món đồ ĐANG MẶC — nó rời khỏi lưới túi và sống ở ô trang bị.</summary>
        public int Slot { get; set; }

        /// <summary>None = đang trong túi. Khác None = đang mặc ở ô đó.</summary>
        public EquipSlot EquipSlot { get; set; }
```

**`Server/DBServer/Repositories/InventoryRepository.cs`** — bốn chỗ, tất cả là SQL:

```csharp
            // LoadAsync: thêm cột vào SELECT và vào phép dựng InventoryRow
            command.CommandText = """
                                  SELECT id, template_id, quantity, slot, equip_slot
                                  FROM inventory_item
                                  WHERE character_id = $characterId
                                  ORDER BY slot;
                                  """;

                    EquipSlot = (EquipSlot)reader.GetInt32(4),
```

```csharp
            // SaveAsync: thêm cột vào INSERT và một tham số nữa
                insert.CommandText = """
                                     INSERT INTO inventory_item (character_id, template_id, quantity, slot, equip_slot)
                                     VALUES ($characterId, $templateId, $quantity, $slot, $equipSlot);
                                     """;

                SqliteParameter equipSlot = insert.Parameters.AddWithValue("$equipSlot", 0);

                    equipSlot.Value = (int)row.EquipSlot;
```

</details>

<details>
<summary><b>📖 Lời giải — <code>Inventory</code>: mặc và cởi</b></summary>

**`Server/GameServer/World/Inventory.cs`** — `Load` và `ToRows` phải biết đến món đang mặc:

```csharp
        /// <summary>Nạp từ DB. Bỏ qua dòng hỏng thay vì ném — xem ghi chú trong thân hàm.</summary>
        public void Load(InventoryRow[] rows)
        {
            System.Array.Clear(_slots, 0, _slots.Length);
            _equipped.Clear();

            foreach (InventoryRow row in rows)
            {
                // Hai loại dòng hỏng, và cả hai đều BỎ QUA chứ không ném: template không còn trong
                // bảng (item bị gỡ), số lượng vô nghĩa. Ném ở đây nghĩa là một dòng DB rác chặn hẳn
                // người chơi vào game — và người chơi thì không sửa được dòng đó.
                if (row.Quantity <= 0 || ItemConfigContainer.Find(row.TemplateId) == null)
                    continue;

                // Món ĐANG MẶC không có ô túi — nó đi vào _equipped, còn Slot của nó trong DB là -1.
                if (row.EquipSlot != EquipSlot.None)
                {
                    _equipped[row.EquipSlot] = ToStack(row);
                    continue;
                }

                // Ô ngoài phạm vi: túi từng rộng hơn rồi bị thu lại. Cũng bỏ qua.
                if (row.Slot < 0 || row.Slot >= SLOT_COUNT)
                    continue;

                _slots[row.Slot] = ToStack(row);
            }

            // Vừa nạp từ DB thì RAM và DB đang khớp nhau — nếu để dirty thì autosave đầu tiên ghi lại
            // đúng thứ vừa đọc lên, 30 lượt ghi không có lý do.
            IsDirty = false;
        }
```

```csharp
        private static ItemStack ToStack(InventoryRow row)
        {
            return new ItemStack
            {
                ItemId = row.ItemId,
                TemplateId = row.TemplateId,
                Quantity = row.Quantity,
            };
        }
```

```csharp
        /// <summary>
        /// Kết xuất để ghi DB: ô túi có đồ, CỘNG các món đang mặc. Ô trống không cần một dòng để nói
        /// rằng nó trống.
        ///
        /// Món đang mặc ghi <c>Slot = -1</c> — đó là điều kiện của chỉ mục
        /// <c>idx_inventory_slot ... WHERE slot >= 0</c>: nhiều món cùng có -1 thì không vi phạm gì.
        /// </summary>
        public InventoryRow[] ToRows()
        {
            var rows = new List<InventoryRow>(UsedSlots + _equipped.Count);

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
                    EquipSlot = EquipSlot.None,
                });
            }

            foreach (KeyValuePair<EquipSlot, ItemStack> pair in _equipped)
            {
                rows.Add(new InventoryRow
                {
                    ItemId = pair.Value.ItemId,
                    TemplateId = pair.Value.TemplateId,
                    Quantity = pair.Value.Quantity,
                    Slot = -1,
                    EquipSlot = pair.Key,
                });
            }

            return rows.ToArray();
        }
```

Và khối trang bị, thêm vào cuối class (trước `IsValidSlot`):

```csharp
        //--------------------------------------------------------------------------------------------
        // Trang bị (Phase 14). Món đang mặc RỜI KHỎI lưới túi: nó nằm trong _equipped, không nằm
        // trong _slots. Một món đồ ở đúng một chỗ tại một thời điểm — hai chỗ là hai nguồn sự thật.
        //--------------------------------------------------------------------------------------------

        private readonly Dictionary<EquipSlot, ItemStack> _equipped = new();

        /// <summary>Món đang mặc ở một ô, hoặc ô rỗng.</summary>
        public ItemStack GetEquipped(EquipSlot equipSlot)
        {
            return _equipped.TryGetValue(equipSlot, out ItemStack stack) ? stack : default;
        }

        /// <summary>
        /// Danh sách config của mọi món đang mặc — thứ <c>StatCalculator.Compute</c> nhận vào.
        ///
        /// Dựng mới mỗi lần thay vì giữ một danh sách đồng bộ: danh sách thứ hai là thứ phải nhớ cập
        /// nhật ở mọi đường vào/ra, và quên một đường thì chỉ số sai mà không có triệu chứng. Cùng lý
        /// do khiến Phase 11 dựng lại chỉ mục cột từ đầu mỗi tick.
        /// </summary>
        public List<ItemConfig> EquippedConfigs()
        {
            var list = new List<ItemConfig>(_equipped.Count);

            foreach (ItemStack stack in _equipped.Values)
            {
                ItemConfig config = ItemConfigContainer.Find(stack.TemplateId);

                if (config != null)
                    list.Add(config);
            }

            return list;
        }

        /// <summary>
        /// Mặc món ở ô túi <paramref name="slot"/>. Trả danh sách ô túi vừa đổi; RỖNG = không làm gì.
        ///
        /// HOẶC TRỌN HOẶC KHÔNG: nếu ô trang bị đang có món khác thì món cũ phải về được túi trước.
        /// Túi đầy thì từ chối cả thao tác — cởi ra rồi không có chỗ cất là làm bốc hơi món đồ.
        /// </summary>
        public IReadOnlyList<int> TryEquip(int slot)
        {
            if (!IsValidSlot(slot) || _slots[slot].IsEmpty)
                return System.Array.Empty<int>();

            ItemConfig config = ItemConfigContainer.Find(_slots[slot].TemplateId);

            if (config == null || config.Kind != ItemKind.Equipment || config.EquipSlot == EquipSlot.None)
                return System.Array.Empty<int>();

            ItemStack incoming = _slots[slot];
            ItemStack outgoing = GetEquipped(config.EquipSlot);

            // Ô túi vừa nhấc món mới ra đang TRỐNG, nên món cũ luôn có ít nhất chỗ này để về. Đặt nó
            // vào đúng ô ấy là phép hoán đổi một-đổi-một: không cần tìm ô trống, không thể thất bại.
            _slots[slot] = outgoing;
            _equipped[config.EquipSlot] = incoming;

            IsDirty = true;

            return new[] { slot };
        }

        /// <summary>
        /// Cởi món ở một ô trang bị về túi. Túi đầy thì TỪ CHỐI — không có chỗ cất thì không cởi.
        /// </summary>
        public IReadOnlyList<int> TryUnequip(EquipSlot equipSlot)
        {
            ItemStack stack = GetEquipped(equipSlot);

            if (stack.IsEmpty)
                return System.Array.Empty<int>();

            int free = FirstEmptySlot();

            if (free < 0)
                return System.Array.Empty<int>();

            _slots[free] = stack;
            _equipped.Remove(equipSlot);

            IsDirty = true;

            return new[] { free };
        }

        private int FirstEmptySlot()
        {
            for (int slot = 0; slot < _slots.Length; slot++)
            {
                if (_slots[slot].IsEmpty)
                    return slot;
            }

            return -1;
        }
```

Thêm `using MMORPG.Shared.World.Item;` nếu chưa có.

**`Server/GameServer/World/InventoryService.cs`** — `SendDelta` đổi từ `private static`
thành `public static`: `StatService.AfterEquipChange` cần gửi delta túi sau khi mặc/cởi.

```csharp
        public static void SendDelta(PlayerEntity entity, IReadOnlyList<int> changedSlots)
```

</details>

---

## Ba thử nghiệm bắt buộc

**1. Cộng dồn trôi như thế nào.**
Sửa tạm `EquipItem` sang kiểu cộng dồn (`Stats[stat] += bonus` khi mặc, `-=` khi cởi). Chạy CHECKPOINT C
bước 4. Nếu công thức dẫn xuất được tính đúng một lần lúc mặc, bạn sẽ thấy Sát thương trôi ngay lần thứ
hai — vì phần dẫn xuất bị cộng vào rồi trừ đi theo hai công thức khác nhau.

Rồi thử tình huống khó hơn: mặc kiếm → **lên một cấp** (phím console) → cởi kiếm. Bản cộng-dồn trừ đi
đúng con số đã cộng vào lúc trước, trong khi nền đã đổi. Trả code về.

Đây là loại bug **không tái hiện được bằng thao tác đơn lẻ** — nó cần một thứ tự cụ thể, và vì vậy nó
sống sót qua mọi lần test thủ công.

**2. Đo cái mình không lưu.**
Mở `mmorpg.db`, tìm cột nào chứa `MaxHp` hay `Attack`. Không có. Giờ sửa `class-stats.json` cho Thể lực
mạnh gấp đôi, restart, vào lại: mọi nhân vật đã tồn tại đều khoẻ lên — **không có migration nào, không
có script cập nhật nào**.

Đó là phần thưởng của việc lưu nguyên liệu chứ không lưu thành phẩm, và nó là lý do một game thật cân
bằng lại được sau khi phát hành.

**3. Client tự tính thì sao.**
Sửa tạm client bỏ qua `StatsUpdate`, tự chạy `StatCalculator.Compute` sau mỗi lần mặc đồ. Mọi thứ trông
**đúng** — cho tới khi bạn sửa một con số trong `class-stats.json` ở server rồi vào lại bằng một client
mang bảng cũ: hai bên hiển thị hai bộ chỉ số, và không có gì báo.

Đó chính là thứ `Contract.Hash` (Phase 12) **không** bắt được, vì hình dạng không đổi. Và là lý do luật
"con số có hiệu lực luôn đến từ server" không phải sự cẩn thận thừa. Trả code về.

---

## Troubleshooting

| Triệu chứng | Nguyên nhân thường gặp | Chỗ sửa |
|---|---|---|
| Chỉ số trôi dần sau nhiều lần mặc-cởi | đang cộng dồn thay vì tính lại từ đầu | `StatService` — mọi đường đổi nguyên liệu phải kết thúc bằng `entity.Recompute(...)` |
| Giáp +5 Thể lực mà `MaxHp` chỉ tăng 5 | cộng trang bị **sau** khi đã tính dẫn xuất | thứ tự bốn bước trong `StatCalculator.Compute` |
| `MaxHp` đúng nhưng máu hiện tại vượt trần | thiếu hai dòng `Math.Min` | `PlayerEntity.Recompute` |
| Mặc lại giáp thì máu đầy lại | đang khôi phục theo tỉ lệ — đó là một nút hồi máu miễn phí | bỏ hẳn; xem Bước 3 |
| Cộng điểm được vào `MaxHp` | thiếu phép kiểm "phải là chỉ số gốc" | `StatService.Allocate` |
| Chỉ số về 0 sau khi relog | `character_stat` chưa được lưu, hoặc lưu sau khi entity đã bị `Despawn` | `LeaveWorldAsync` — lưu **trước** khi bỏ entity |
| `UNIQUE constraint failed: inventory_item.equip_slot` | chỉ mục chưa có `WHERE equip_slot > 0`, nên mọi món trong túi (`equip_slot = 0`) đụng nhau | migration 6 |
| Món đồ biến mất khi mặc | `Inventory.ToRows` chưa xuất `_equipped`, nên lần lưu kế tiếp xoá sạch món đang mặc | `Inventory.ToRows` — phải duyệt CẢ `_slots` lẫn `_equipped` |
| Túi đầy thì cởi đồ không có tác dụng gì | đúng chủ đích — `TryUnequip` trả rỗng khi `FirstEmptySlot()` = -1 | không phải bug; vứt bớt một món rồi cởi |
| Bảng thông tin không tự cập nhật | quên `builder.Register<StatsNetHandler>()...As<INetHandlerGroup>()` | `GameLifetimeScope` |
| Preview khi rê chuột lệch với số thật sau khi mặc | client đang tính bằng bảng cũ, hoặc quên loại món đang mặc ra khỏi danh sách giả định | `StatsPanel` — dựng danh sách "sau khi mặc" đúng: bỏ món cũ ở ô đó ra rồi mới thêm món mới |

---

## Tự kiểm tra hiểu bài

**Câu 1.** `MaxHp` không lưu DB còn `Hp` thì có. Phát biểu quy tắc chung, và nêu một cột mà quy tắc ấy
nói là đang sai nếu bạn thấy nó trong `character`.
<details>
<summary><b>📖 Đáp án câu 1</b></summary>

Quy tắc: **lưu nguyên liệu, đừng lưu thành phẩm.** Cái gì tính lại được từ dữ liệu khác thì không được
có bản riêng trong DB — bản ấy sẽ lệch vào ngày công thức hoặc bảng thay đổi, và lệch im lặng.

`Hp` không tính lại được từ bất cứ thứ gì: nó là kết quả của một chuỗi sự kiện đã xảy ra. Nên nó phải
lưu.

Cột "đang sai": `attack`, `defense`, hoặc bất kỳ chỉ số dẫn xuất nào. Cũng sai nếu thấy `class_name`
(tính được từ `class_id` + bảng) — cùng một bệnh ở dạng chuỗi.

</details>

**Câu 2.** "Tính lại từ đầu" nghe lãng phí hơn "cộng thêm rồi trừ đi". Biện minh, và chỉ ra phase nào
trong dự án đã dùng đúng lập luận này.
<details>
<summary><b>📖 Đáp án câu 2</b></summary>

Vì bản cộng-dồn phải đúng ở **mọi** đường vào và ra: mặc, cởi, đổi món khác, lên cấp, cộng điểm, buff hết
hạn, món đồ bị xoá. Quên một đường là chỉ số lệch — mà chỉ số lệch **không có triệu chứng**, nó chỉ trôi.

Tính lại từ đầu thì cả lớp bug ấy không tồn tại: đúng ba nguyên liệu đó luôn cho ra đúng một kết quả.
Chi phí là vài chục phép cộng, chạy vài lần mỗi phút — không đo được.

**Phase 11**, chỉ mục cột AOI: dựng lại từ đầu mỗi tick thay vì cập nhật tại chỗ, đúng lập luận ấy, và ở
đó nó còn chạy 20 lần mỗi giây. Nếu chấp nhận được ở tần suất đó thì chắc chắn chấp nhận được ở đây.

Khi nào lập luận này sai: khi phép tính lại **đắt thật** và tần suất **cao thật**. Lúc đó cách đúng không
phải là cộng dồn mà là **đánh dấu bẩn rồi tính lại một lần trước khi cần đọc**.

</details>

**Câu 3.** Chỉ số đẩy xuống bằng snapshot, túi đồ bằng delta. Phát biểu tiêu chí, và nói chỉ số HP trong
combat (Phase 15) sẽ đi đường nào.
<details>
<summary><b>📖 Đáp án câu 3</b></summary>

Tiêu chí: **các phần có độc lập với nhau không.** 30 ô túi độc lập — đổi một ô không đụng 29 ô kia, nên
"ô nào vừa đổi" là thông tin có giá trị. Chỉ số thì phụ thuộc chéo — đổi Thể lực là đổi `MaxHp` và Phòng
thủ, nên "cái gì vừa đổi" gần như luôn là "gần hết", và **một nửa bộ chỉ số là một object vô nghĩa**.

Kích thước gói là chuyện thứ yếu; ở quy mô này cả hai đều nhỏ.

HP trong combat: **đường riêng, và không phải cái nào trong hai cái trên.** Nó đổi 20 lần mỗi giây cho
mỗi entity đang đánh nhau, nó phải đi kèm AOI (chỉ ai thấy mới nhận), và nó là **trạng thái**, không phải
chỉ số. Nó thuộc về snapshot của world, cạnh vị trí — tức là đường đã dựng từ Phase 7.

</details>

**Câu 4.** `StatCalculator` ở `Shared` để client dùng, nhưng "client không bao giờ tự cộng". Hai câu đó
có mâu thuẫn không?
<details>
<summary><b>📖 Đáp án câu 4</b></summary>

Không, vì chúng nói về hai việc: **dự đoán để hiển thị** và **giá trị có hiệu lực**.

Client chạy `Compute` để trả lời "nếu mặc món này thì sát thương thành bao nhiêu" — câu hỏi mà server
không thể trả lời vì nó không biết bạn đang rê chuột vào đâu. Kết quả ấy là một **nhãn**, sống trong một
ô tooltip, không bao giờ được ghi vào `StatsModel`.

Đúng cùng ranh giới với `MovementRules.Step` ở Phase 6: client chạy chính hàm đó để dự đoán, nhưng vị trí
thật vẫn là vị trí server nói, và có lệch thì server thắng.

Phép thử để biết mình có đang vi phạm không: **xoá đường `StatsUpdate` đi thì màn hình có sai không?**
Nếu không sai, bạn đang tự tính; nếu sai ngay, bạn đang hiển thị đúng thứ server nói.

</details>

**Câu 5.** `character_stat` là một bảng quan hệ thay vì bốn cột trong `character`. Được gì, mất gì?
<details>
<summary><b>📖 Đáp án câu 5</b></summary>

**Được:** thêm chỉ số gốc thứ năm là thêm một giá trị enum — không migration, không sửa `SELECT`, không
sửa DTO. Và "không có dòng" tự nhiên mang nghĩa "0 điểm", nên dữ liệu thiếu vẫn hợp lệ.

**Mất:** một lượt `JOIN` (hoặc một query thứ hai) mỗi lần nạp nhân vật, và dữ liệu của một nhân vật nằm
ở hai bảng nên phải nhớ lưu cả hai.

Đổi ở quy mô này thì được nhiều hơn mất, vì số chỉ số **sẽ** tăng. Tiêu chí chung: *cái gì có thể nhiều
lên thì thành hàng, không thành cột* — và nó cũng là tiêu chí đã cho `inventory_item` thành bảng riêng ở
Phase 13 thay vì 30 cột trong `character`.

</details>

**Câu 6.** Trang bị nằm trong `inventory_item` với cột `equip_slot` thay vì một bảng `equipment` riêng.
Nêu kịch bản hỏng cụ thể của phương án bảng riêng.
<details>
<summary><b>📖 Đáp án câu 6</b></summary>

Mặc một món trở thành `DELETE FROM inventory_item` + `INSERT INTO equipment`. Hai câu lệnh, hai bảng. Nếu
không nằm trong cùng một transaction và server chết ở giữa: món đồ **biến mất** (đã xoá, chưa thêm) hoặc
**nhân đôi** (đã thêm, chưa xoá).

Nhân đôi còn tệ hơn biến mất: nó là một máy in đồ, và người chơi nào phát hiện ra sẽ không báo cho bạn.

Gốc rễ: mặc một món **không tạo ra một vật mới**, vẫn đúng `itemId` ấy, chỉ là nó đang ở chỗ khác. Mô
hình dữ liệu nên nói đúng điều đó — một dòng, đổi một cột — chứ không mô tả nó như một cuộc di cư giữa
hai bảng.

</details>

**Câu 7.** Cởi giáp làm máu bị kẹp xuống, mặc lại thì máu không lên. Biện minh, và nêu một hệ thống khác
trong game có cùng cái bẫy này.
<details>
<summary><b>📖 Đáp án câu 7</b></summary>

Vì khôi phục theo tỉ lệ biến mặc/cởi thành một **nút hồi máu miễn phí, không cooldown**. Mọi hệ thống hồi
phục đều bị dò tìm theo kiểu này, và cách chặn đúng không phải thêm cooldown mà là đừng tạo ra nguồn hồi
phục ở chỗ không định tạo.

Quy tắc: **một thao tác không nhằm hồi phục thì không được làm tăng trạng thái hiện thời.** Đổi trần là
chuyện của chỉ số; đổ đầy tới trần là chuyện của hồi phục.

Cùng cái bẫy ở chỗ khác: hồi sinh / chuyển map / đăng nhập lại mà "đặt máu về đầy" — thế là chết có lợi
hơn uống thuốc, hoặc relog trở thành kỹ năng. Và ở Phase 15: một buff `+MaxHp` hết hạn thì phải kẹp, còn
lúc nhận buff thì không được đổ đầy.

</details>

**Câu 8.** Thêm `EquipSlot` và `Bonuses` vào `items.json` nhưng **không** tăng `Version`, trong khi
dấu vân tay thì đổi. Hai quyết định ấy có nhất quán không?
<details>
<summary><b>📖 Đáp án câu 8</b></summary>

Có, vì chúng trả lời hai câu hỏi khác nhau.

`Version` là **phiên bản của ĐỊNH DẠNG**: nó trả lời "code này có đọc nổi file kia không". Thêm một
trường tuỳ chọn thì đọc vẫn nổi ở cả hai chiều — file cũ thiếu trường thì về mặc định, code cũ gặp trường
lạ thì bỏ qua. Nên giữ nguyên. (Đúng luật đã ghi ở `MapFile.FORMAT_VERSION` và đã dùng cho `Portals`.)

Dấu **vân tay** là của NỘI DUNG: nó trả lời "hai bên có đang cầm cùng dữ liệu không". Bonus
đổi hành vi, nên nó phải vào vân tay — một server chạy bảng có bonus và một client chạy bảng không có là
hai thế giới khác nhau, và người chơi sẽ thấy một con số khác nhau ở hai bên.

Phép thử để phân loại một trường: *thiếu nó thì file có đọc được không* (→ `Version`) và *đổi nó thì hành
vi có đổi không* (→ vân tay).

Chú ý là bạn không phải *làm* gì để vân tay đổi: `ConfigFingerprint` băm byte đã tuần tự hoá, nên
một trường `[MemoryPackable]` mới tự vào. Đó chính là lý do bỏ `Checksum()` viết tay — ở bản cũ,
câu hỏi này sẽ có thêm một vế: "và bạn có nhớ thêm hai dòng `Fnv1a.Mix` không?"

</details>

---

## Để dành (ghi lại, chưa làm)

- **Buff / debuff có thời hạn.** Nguồn thứ tư của pipeline, và nó là nguồn duy nhất **tự hết hạn** — tức
  là `Recompute()` phải chạy từ vòng tick chứ không chỉ từ các lệnh người chơi. Chỗ cắm đã sẵn: thêm một
  tham số vào `Compute`.
- **Chỉ số ảnh hưởng tới di chuyển** (giày +tốc chạy). Đây là chỗ hai hệ thống đụng nhau: `MoveSpeed` nằm
  trong `CharacterConfig` và đi thẳng vào `MovementRules.Step`, mà `Step` thì **client cũng chạy**. Đổi
  nó giữa phiên là mở lại đúng vấn đề "hợp đồng phiên chơi" của Phase 12 — phải đẩy số mới xuống client
  trong cùng một tick, và phải xử lý đống input đang treo. Đừng làm kèm phase này.
- **Giới hạn cộng điểm** (tối đa theo cấp, reset điểm bằng vật phẩm). Toàn bộ nằm trong `StatService.Allocate`.
- **`PrimaryStat` thành enum riêng**, để "chỉ cộng điểm vào chỉ số gốc" được chặn bằng **kiểu** thay vì
  bằng một câu lệnh `if` và một bài test.
- **Bộ trang bị (set bonus).** Nguồn thứ năm, và là nguồn đầu tiên **không cộng tuyến tính** — "mặc đủ 3
  món thì +10%". Pipeline tính-lại-từ-đầu chịu được nó không sửa gì; bản cộng-dồn thì không thể.
- **So sánh hai món đồ cạnh nhau** trong tooltip. Hàm đã có, chỉ là gọi `Compute` thêm một lần nữa.

---

**Xong Phase 14.** Nhân vật giờ có số để mà mạnh lên, và có chỗ để nhét đồ vào cho mạnh thêm — nhưng
chưa có gì để đánh.

[PHASE-15](PHASE-15.md) mang quái vào thế giới và dùng hết những thứ vừa dựng: `Attack` và `Defense` vào
công thức sát thương, `Hp` thành trạng thái thật sự biến động, `ActionState.Hurt`/`Die` của Phase 9 cuối
cùng cũng có nguồn phát ngoài phím console, và cái túi của Phase 13 nhận đồ rơi từ đúng chỗ nó nên nhận —
mặt đất.
