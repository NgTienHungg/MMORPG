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

## Bước 1 — `StatBlock`, bảng chỉ số gốc, và pipeline tính lại

### Hướng làm

**`StatType` là một enum, `StatBlock` là một mảng theo enum ấy** — không phải mười hai property rời.

Lý do đã gặp hai lần rồi (gom 12 field của `MoveState` ở Phase 9, gom `WorldRules` ở Phase 12), nhưng ở
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

1. Server boot in: `Bảng chỉ số: 1 lớp, checksum XXXXXXXX`.
2. Vào world → client nhận `StatsUpdate`, log đủ 9 chỉ số.
3. Bấm `C` → bảng thông tin mở, chia hai nhóm Gốc / Dẫn xuất, số khớp với log server.
4. Sửa `PerLevel` của Thể lực trong file, restart server → vào lại thấy `MaxHp` đổi theo. **Không build
   lại gì** — và không có dòng nào trong DB phải sửa, vì `MaxHp` chưa bao giờ được lưu.
5. Gọi tạm `StatCalculator.Compute` hai lần liên tiếp với cùng đầu vào rồi `Assert` hai kết quả bằng
   nhau. Nghe thừa; nó là thứ chặn mọi ý định lén cho trạng thái vào hàm này sau này.

<details>
<summary><b>📖 Lời giải — <code>StatBlock</code> và <code>StatCalculator</code></b></summary>

```csharp
using System;
using System.Collections.Generic;
using MemoryPack;

// Ba kiểu bạn phải tự viết trước khi đoạn dưới biên dịch được, cả ba đều theo khuôn đã quen:
//   StatBonus      { StatType Stat; int Value; }          — một dòng bonus trong items.json
//   ClassStats     { StatBlock Base; StatBlock PerLevel; int PointsPerLevel; }
//   ClassStatTable — bảng tĩnh nạp được, y hệt ItemTemplates của Phase 13

namespace MMORPG.Shared.World
{
    /// <summary>
    /// Một bộ chỉ số. MẢNG theo StatType chứ không phải mười hai property rời: mã nguồn cần CỘNG hai
    /// bộ lại với nhau ở bốn chỗ khác nhau, và với property rời thì mỗi chỗ là mười hai dòng chép tay —
    /// quên một dòng không có lỗi biên dịch, chỉ có một chỉ số âm thầm không bao giờ tăng.
    /// </summary>
    [MemoryPackable]
    public sealed partial class StatBlock
    {
        /// <summary>
        /// Dài bằng giá trị enum lớn nhất + 1, chấp nhận vài ô trống ở giữa. Đổi lại: tra chỉ số là
        /// một phép truy cập mảng, và thêm một StatType mới không phải đụng vào bất cứ đâu ngoài enum.
        /// </summary>
        public int[] Values { get; set; } = new int[(int)StatType.CritRate + 1];

        public int this[StatType stat]
        {
            get { return Values[(int)stat]; }
            set { Values[(int)stat] = value; }
        }

        public void Add(StatBlock other)
        {
            for (int i = 0; i < Values.Length; i++)
                Values[i] += other.Values[i];
        }

        public StatBlock Clone()
        {
            var copy = new StatBlock();

            Array.Copy(Values, copy.Values, Values.Length);

            return copy;
        }
    }

    /// <summary>
    /// Tính bộ chỉ số đầy đủ từ NGUYÊN LIỆU. Hàm thuần: cùng đầu vào luôn cho cùng đầu ra, không đọc
    /// thời gian, không random, không đọc biến toàn cục nào ngoài bảng tĩnh đã nạp.
    ///
    /// Nằm ở Shared để client chạy được — nhưng CHỈ để dự đoán cho việc hiển thị (rê chuột vào món đồ
    /// thì hiện "42 → 55"). Con số đang có hiệu lực luôn là con số server đẩy xuống. Cùng ranh giới với
    /// MovementRules.Step ở Phase 6.
    /// </summary>
    public static class StatCalculator
    {
        /// <summary>
        /// TÍNH LẠI TỪ ĐẦU, mọi lần. Không có phiên bản "cộng thêm khi mặc, trừ đi khi cởi" — cộng dồn
        /// thì mỗi lần mặc-cởi là một cơ hội trôi một điểm, và sau năm mươi lần thì chỉ số sai mà không
        /// có gì báo. Cùng lý do khiến Phase 11 dựng lại chỉ mục cột từ đầu mỗi tick.
        /// </summary>
        public static StatBlock Compute(int classId, int level, StatBlock allocated, IReadOnlyList<ItemTemplate> equipped)
        {
            ClassStats table = ClassStatTable.Get(classId);
            var result = new StatBlock();

            // 1. Nền của lớp nhân vật ở cấp này.
            result.Add(table.Base);

            for (int i = 1; i < level; i++)
                result.Add(table.PerLevel);

            // 2. Điểm người chơi tự cộng.
            result.Add(allocated);

            // 3. Trang bị. Chỉ cộng vào chỉ số GỐC ở vòng này — xem comment ở bước 4.
            foreach (ItemTemplate item in equipped)
            {
                foreach (StatBonus bonus in item.Bonuses)
                    result[bonus.Stat] += bonus.Value;
            }

            // 4. Dẫn xuất, tính SAU CÙNG từ chỉ số gốc đã cộng đủ.
            //
            //    Thứ tự này không phải chuyện phong cách. Giáp cộng +5 Thể lực, công thức MaxHp =
            //    Vitality × 10: làm đúng thứ tự thì giáp cho +50 MaxHp; cộng MaxHp của giáp vào SAU khi
            //    đã tính thì chỉ được +5. Cả hai đều "chạy", chỉ khác nhau một con số mà không ai kiểm.
            result[StatType.MaxHp] = 50 + result[StatType.Vitality] * 10;
            result[StatType.MaxMp] = 20 + result[StatType.Spirit] * 5;
            result[StatType.Attack] = result[StatType.Strength] * 2 + result[StatType.Agility];
            result[StatType.Defense] = result[StatType.Vitality];
            result[StatType.CritRate] = result[StatType.Agility] / 2;

            return result;
        }
    }
}
```

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

---

## Bước 3 — Trang bị: nối cái túi vào pipeline

### Hướng làm

**Mở rộng bảng item, không đổi format.** `ItemTemplate` thêm hai trường **tuỳ chọn**:

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

Nhưng `Checksum()` thì **phải** băm thêm hai trường mới — chúng đổi hành vi. Số vân tay đổi, và đó là
đúng: một server chạy bảng có bonus và một client chạy bảng không có là hai thế giới khác nhau.

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

`EquipItem(slot)`:

1. Ô `slot` có đồ không; template có `Kind == Equipment` và `EquipSlot != None` không.
2. (Phase sau: kiểm cấp độ, kiểm lớp nhân vật.)
3. Nếu ô trang bị ấy **đang có món khác** → cởi món cũ về túi **trước**. Túi đầy? → **từ chối cả thao
   tác**, không cởi gì cả. (All-or-nothing, đúng như `TryAdd` ở Phase 13.)
4. Chuyển món mới: `slot = -1`, `equip_slot = X`.
5. `Recompute()`.
6. **Kẹp sinh lực hiện tại về `MaxHp` mới.**
7. Đẩy `InventoryDelta` (ô túi vừa đổi) **và** `StatsUpdate`. Hai gói, hai model, một thao tác.

**Bước 6 là chỗ có một cái bẫy đáng tiền.** Cởi giáp làm `MaxHp` tụt từ 200 xuống 150 trong khi máu hiện
tại đang là 180 — phải kẹp xuống 150. Đến đây ai cũng đồng ý. Câu hỏi thật là chiều ngược lại: **mặc lại
giáp thì máu có lên lại 180 không?**

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
5. Túi đầy 30 ô, đang mặc kiếm A, mặc kiếm B từ ô cuối → **từ chối trọn**, A vẫn trên người, B vẫn trong
   túi, không có ô nào đổi.
6. Giả lập mất máu (phím trên console), cởi giáp → máu bị kẹp xuống `MaxHp` mới. Mặc lại → `MaxHp` lên,
   **máu không lên**.
7. Logout, vào lại → vẫn đang mặc đúng món đó, chỉ số khớp, máu đúng.
8. Sửa tạm client gửi `EquipItem` lên một ô trống, rồi lên một Bình máu → server từ chối cả hai, log
   Warn, không tick nào chết.

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
| Chỉ số trôi dần sau nhiều lần mặc-cởi | đang cộng dồn thay vì tính lại từ đầu | `StatService` — mọi đường đổi nguyên liệu phải kết thúc bằng `Recompute()` |
| Giáp +5 Thể lực mà `MaxHp` chỉ tăng 5 | cộng trang bị **sau** khi đã tính dẫn xuất | thứ tự bốn bước trong `StatCalculator.Compute` |
| `MaxHp` đúng nhưng máu hiện tại vượt trần | thiếu bước kẹp sau `Recompute` | `StatService.Recompute` |
| Mặc lại giáp thì máu đầy lại | đang khôi phục theo tỉ lệ — đó là một nút hồi máu miễn phí | bỏ hẳn; xem Bước 3 |
| Cộng điểm được vào `MaxHp` | thiếu phép kiểm "phải là chỉ số gốc" | `StatService.Allocate` |
| Chỉ số về 0 sau khi relog | `character_stat` chưa được lưu, hoặc lưu sau khi entity đã bị `Despawn` | `LeaveWorldAsync` — lưu **trước** khi bỏ entity |
| `UNIQUE constraint failed: inventory_item.equip_slot` | chỉ mục chưa có `WHERE equip_slot > 0`, nên mọi món trong túi (`equip_slot = 0`) đụng nhau | migration 6 |
| Món đồ biến mất khi mặc | `slot = -1` nhưng UI vẫn vẽ theo `slot`, và `-1` rơi ngoài mảng | `InventoryModel.ApplyDelta` phải coi `slot < 0` là "rời túi" |
| Túi đầy thì mặc đồ làm mất món đang mặc | thiếu all-or-nothing ở bước 3 của `EquipItem` | kiểm chỗ trống **trước** khi động vào món nào |
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
`Checksum` thì đổi. Hai quyết định ấy có nhất quán không?
<details>
<summary><b>📖 Đáp án câu 8</b></summary>

Có, vì chúng trả lời hai câu hỏi khác nhau.

`Version` là **phiên bản của ĐỊNH DẠNG**: nó trả lời "code này có đọc nổi file kia không". Thêm một
trường tuỳ chọn thì đọc vẫn nổi ở cả hai chiều — file cũ thiếu trường thì về mặc định, code cũ gặp trường
lạ thì bỏ qua. Nên giữ nguyên. (Đúng luật đã ghi ở `MapFile.FORMAT_VERSION` và đã dùng cho `Portals`.)

`Checksum` là dấu vân tay của **NỘI DUNG**: nó trả lời "hai bên có đang cầm cùng dữ liệu không". Bonus
đổi hành vi, nên nó phải vào vân tay — một server chạy bảng có bonus và một client chạy bảng không có là
hai thế giới khác nhau, và người chơi sẽ thấy một con số khác nhau ở hai bên.

Phép thử để phân loại một trường: *thiếu nó thì file có đọc được không* (→ `Version`) và *đổi nó thì hành
vi có đổi không* (→ `Checksum`).

</details>

---

## Để dành (ghi lại, chưa làm)

- **Buff / debuff có thời hạn.** Nguồn thứ tư của pipeline, và nó là nguồn duy nhất **tự hết hạn** — tức
  là `Recompute()` phải chạy từ vòng tick chứ không chỉ từ các lệnh người chơi. Chỗ cắm đã sẵn: thêm một
  tham số vào `Compute`.
- **Chỉ số ảnh hưởng tới di chuyển** (giày +tốc chạy). Đây là chỗ hai hệ thống đụng nhau: `MoveSpeed` nằm
  trong `CharacterProfile` và đi thẳng vào `MovementRules.Step`, mà `Step` thì **client cũng chạy**. Đổi
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
