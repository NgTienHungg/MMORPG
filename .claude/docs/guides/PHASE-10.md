# PHASE 10 — Map: thế giới có hình dạng thật

> **Kết quả cuối Phase 10:** thế giới không còn là một mặt phẳng vô hình. Có sàn thật, tường chặn ngang,
> **bệ xuyên-một-chiều** — nhảy từ dưới lên thì lọt qua, đứng được ở trên, bấm ngồi + nhảy thì tụt xuống.
> Ngồi thì thân thấp lại và chui được vào khe hẹp, nhưng **không đứng dậy được dưới trần thấp**. Và quan
> trọng nhất: hình dạng ấy có **đúng một nguồn** — lớp `Collision` bạn vẽ trong tilemap — được một tool
> Editor xuất ra **file JSON**, rồi cả server lẫn client cùng đọc đúng file đó.
>
> **Điều kiện:** xong [`PHASE-9.md`](PHASE-9.md) tới CHECKPOINT E — hai client thấy nhau chạy, nhảy,
> ngồi, đánh đúng trạng thái.
>
> **Bài học chính:** (1) va chạm là **luật chơi**, nên nó nằm ở `Shared` và cả hai bên chạy đúng một
> hàm; (2) nhân vật hết là một điểm — nó có **thân**, và một trạng thái của thân thì **bị thế giới từ
> chối được**; (3) contract không phải lúc nào cũng chảy từ server sang client: lần này **dữ liệu đi từ
> Unity sang server**, và chiều nào cũng phải để **build** lo chứ không để tay người copy; (4) chọn
> định dạng dữ liệu là chọn xem **thêm một trường mới sau này tốn bao nhiêu** — và đây là phase chốt
> định dạng cho cả dự án.

Format như trước: **hướng làm** hiện sẵn, **📖 Lời giải** trong foldout.

> 📌 Phase này **đã tách đôi và viết lại** (2026-08-24). Bản cũ gộp Map + AOI vào một phase, và gõ hình
> dạng map bằng mảng chuỗi ngay trong `Shared`. Cả hai đều đổi: AOI ra [`PHASE-11.md`](PHASE-11.md)
> riêng, còn map thì đi đường tilemap → file JSON. Nếu bạn còn nhớ chi tiết nào của bản cũ thì quên nó
> đi — nhất là `Maps.cs` và cái gizmo đối chiếu, hai thứ không còn lý do tồn tại.

---

## Một nguồn — và lần này nó nằm trong Unity

Từ Phase 2 tới giờ, mọi contract chảy theo đúng một chiều: viết ở `Server/Shared`, build ra DLL, DLL rơi
sang `Assets/Plugins/Shared/`. Quen tới mức dễ tưởng đó là quy luật.

Map thì ngược. Hình dạng map là thứ bạn **vẽ bằng mắt**, và công cụ vẽ nằm trong Unity. Bắt người ta gõ
lại bản vẽ ấy thành mảng chuỗi trong `Shared` là tạo ra **hai bản vẽ của cùng một map** — một bản để
nhìn, một bản để va chạm — mà không ai kiểm chúng có khớp nhau không. Đó chính xác là loại lỗi cả dự án
này được thiết kế để tránh.

Nên chiều đi của map là chiều ngược lại:

```
        [ Unity ]  lớp Tilemap "Collision"   ← bạn vẽ ở đây, và CHỈ ở đây
                            │
                            │  Tools/MMORPG/Export Map   (Editor script)
                            ▼
              Assets/Game/Resources/Maps/map1.json        ← NGUỒN DUY NHẤT
                       │                    │
   Resources.Load ─────┘                    └───── copy lúc build (GameServer.csproj)
        ▼                                                   ▼
   Client: MapGrid ──► Step() dự đoán            Server: MapGrid ──► Step() sự thật
                           └──────── cùng một hàm trong Shared ───────┘
```

Hai chiều ngược nhau, cùng một nguyên tắc:

| | Contract code (`NetCmd`, DTO) | Hình dạng map |
|---|---|---|
| Sinh ra ở đâu | `Server/Shared` — nơi gõ code | Unity — nơi vẽ hình |
| Chảy đi đâu | post-build của `Shared.csproj` → `Assets/Plugins/Shared/` | Editor export → `Resources/Maps/` → `GameServer.csproj` copy sang output |
| Ai bảo đảm không lệch | **build**, không phải trí nhớ | **build**, không phải trí nhớ |

**Vẫn còn một loại lệch, và phải nói thẳng ra.** Tileset American Forest có cỏ mọc ở mép khối đất, hàng
rào, cây, ba lớp nền trời. Không hàm nào sinh ra hình đó từ ba trạng thái `rỗng / đặc / bệ`. Nên hình
(các lớp `Ground`, `Plants`, `Fences`…) và luật (lớp `Collision`) **vẫn là hai lớp vẽ tay cạnh nhau**.

Nhưng để ý khác biệt so với việc gõ mảng chuỗi trong `Shared`: hai lớp ấy nằm **trong cùng một Scene
view, chồng lên nhau, cùng một cỡ ô**. Lệch nhau là nhìn thấy ngay, không cần công cụ nào — đó là lý do
phase này không có gizmo đối chiếu, thứ mà bản cũ phải viết. Còn loại lệch nguy hiểm hơn — client bảo có
tường, server bảo không — thì **không diễn đạt được**, vì cả hai đọc đúng một file do đúng một tool sinh.

> Luôn biết mình còn loại lệch nào, và nó bị bắt bằng **cơ chế** gì. "Cẩn thận" không phải một cơ chế;
> "hiện ra ngay trên màn hình" thì là.

---

## Định dạng dữ liệu của cả dự án: JSON

Quyết định chốt ở phase này và áp cho **mọi file dữ liệu về sau** (bảng config ở Phase 12, bảng item ở
Phase 13, bảng quái và drop ở Phase 15): **JSON, đọc bằng `Newtonsoft.Json`.**

Câu hỏi quyết định không phải "định dạng nào đẹp" mà là: **map hôm nay chỉ có lưới va chạm, nhưng mai
mốt còn điểm spawn, cổng sang map khác, danh sách quái, vùng an toàn, nhạc nền… — thêm một trường mới
tốn bao nhiêu?**

| | Format text tự chế | **JSON** |
|---|---|---|
| Thêm một trường | sửa parser (đọc), sửa writer (ghi), sửa cả hai cho khớp | thêm một property vào DTO — hết |
| Cấu trúc lồng nhau (danh sách cổng, mỗi cổng có toạ độ + đích) | tự nghĩ ra cú pháp, tự viết parser cho nó | có sẵn |
| File cũ gặp code mới | tự lo | trường thiếu → giá trị mặc định |
| File mới gặp code cũ | thường là nổ | trường lạ → bỏ qua |
| Số thực và locale | **tự nhớ `InvariantCulture`**, quên là điểm spawn nhảy chỗ trên máy người khác | JSON quy định số theo chuẩn, thư viện lo |
| Công cụ sẵn có | không | mọi editor, mọi ngôn ngữ, git diff hiểu được |

Dòng "locale" đáng dừng một chút, vì nó là bài học ngược: format tự chế **không sai** — nó chỉ bắt bạn
phải nhớ một thứ mà quên thì không có lỗi nào báo (`float.Parse("0.5")` trên máy đặt vùng Việt Nam trả
về **5**). Chọn một định dạng có **spec quy định sẵn** là chuyển một việc "phải nhớ" thành một việc
"được bảo đảm". Đó là lý do tốt hơn hẳn "JSON phổ biến".

### Thư viện: `Newtonsoft.Json`, và vì sao không phải `System.Text.Json`

`Shared` chạy ở hai nơi — .NET 8 (server) và Unity (Mono/IL2CPP) — nên thư viện phải sống được ở cả hai.

- **`Newtonsoft.Json`**: Unity đã có sẵn qua UPM (`com.unity.nuget.newtonsoft-json 3.2.1`, đang nằm
  trong `Packages/manifest.json` rồi). Không phải cài gì thêm bên Unity.
- **`System.Text.Json`**: nhanh hơn, nhưng bên Unity phải tự kéo về kèm 3–4 DLL phụ thuộc, và bản
  reflection của nó hay vướng code stripping của IL2CPP.

Ở dự án này, "đã có sẵn và chạy được ở cả hai bên" **thắng** "nhanh hơn 2 lần khi đọc một file 700 byte
lúc khởi động".

### Lưới ô nằm trong JSON dưới dạng mảng CHUỖI

```json
"cells": [
  "................",
  "..........====..",
  "################"
]
```

Chứ không phải `[0,0,0,1,1,2,...]`. Đúng chuẩn JSON cả hai, nhưng 704 con số thì không ai nhìn ra hình,
và `git diff` trở thành vô dụng. Một hàng = một dòng = một chuỗi: mở file ra vẫn **thấy bản đồ**, và
sửa một ô thì diff chỉ đúng một dòng.

> Nguyên tắc mang đi được: JSON lo **cấu trúc**; chỗ nào con người cần đọc bằng mắt thì cho nó một cách
> biểu diễn của con người, đừng ép mọi thứ thành số chỉ vì máy đọc tiện hơn.

### Khoan dung hay nghiêm khắc với trường lạ?

Newtonsoft cho chọn: gặp trường không có trong DTO thì **bỏ qua** (`Ignore`) hay **ném lỗi** (`Error`).
Hai lựa chọn, và câu trả lời đúng phụ thuộc **ai viết ra file**:

| Loại file | Ai viết | Chọn | Vì sao |
|---|---|---|---|
| Map (phase này) | **tool export** | `Ignore` | máy sinh thì không có lỗi chính tả; đổi lại được tính "thêm trường không phá code cũ" — chính là thứ ta trả tiền để mua |
| Config (Phase 12) | **người gõ tay** | `Error` | gõ nhầm `gravty` mà im lặng thì trọng lực về 0 và bạn đi tìm bug ở `MovementRules` |

Nói cách khác: **khoan dung với máy, nghiêm khắc với người.** Đây không phải sở thích — nó suy ra từ
việc mỗi bên sai theo kiểu gì.

### Và `version` vẫn phải có

JSON tự lo được chuyện *thêm* trường, nhưng không lo được chuyện **đổi ý nghĩa** của trường đã có (ví
dụ ngày nào đó `origin` chuyển từ ô sang world unit). Nên trong file vẫn có `"version": 1`, và luật là:

- Thêm trường tuỳ chọn → **không** tăng version. File cũ vẫn đọc được, code cũ vẫn đọc được file mới.
- Đổi ý nghĩa / xoá / đổi tên trường → **tăng** version, và code từ chối đọc version lạ.

---

## Bước 1 — `Shared`: định dạng file map, và `MapGrid`

### Hướng làm

**Chuẩn bị: thêm phụ thuộc.** Trong `Server/Shared/Shared.csproj` thêm
`<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />`.

Bên Unity **không làm gì cả** — `com.unity.nuget.newtonsoft-json` đã cung cấp DLL, và
`MMORPG.Shared.dll` sẽ tự tìm thấy nó. ⚠️ **Đừng** cài `Newtonsoft.Json` qua NuGetForUnity: lúc đó có
hai bản DLL cùng tên trong project và Unity báo lỗi "Multiple precompiled assemblies with the same name".

**Ba loại ô**, và loại thứ ba định nghĩa thể loại platformer:

| Ký tự | `CellType` | Ý nghĩa |
|---|---|---|
| `.` | `Empty` | đi xuyên qua thoải mái |
| `#` | `Solid` | chặn mọi hướng |
| `=` | `OneWay` | **chỉ** chặn khi đang rơi xuống và chân đã ở trên mặt bệ từ trước |

Bệ xuyên-một-chiều là ví dụ đẹp nhất trong cả dự án về việc **va chạm phụ thuộc trạng thái chứ không chỉ
phụ thuộc vị trí**. Cùng một ô, cùng một toạ độ nhân vật, mà chặn hay không còn tuỳ nó đang đi lên hay
đi xuống và trước đó nó ở đâu. Đây chính là lý do Phase 8 phải gom vận tốc vào `MoveState`: không có vận
tốc thì câu hỏi "ô này có chặn không" **không trả lời được**.

**Hai kiểu, hai vai — đừng gộp.** Đây là mẫu dự án đã dùng từ Phase 5 (`CharacterRow` của DB ≠
`PlayerEntity` chạy trong world), giờ lặp lại ở tầng file:

| | `MapDefinition` | `MapGrid` |
|---|---|---|
| Là gì | **DTO của file JSON** — khớp 1-1 với các trường trong file | **kiểu chạy trong game** |
| Hình dạng | property có setter, `List<string>`, cho phép null | bất biến, mảng `CellType[]` phẳng, tra ô bằng `At(cx, cy)` |
| Ai đụng vào | chỉ `MapFile` (và tool export) | mọi chỗ khác |
| Vì sao tách | trường trong file là chuyện của **định dạng**; cách tra ô nhanh là chuyện của **mô phỏng**. Gộp lại thì mỗi lần đổi định dạng là đụng vào thứ chạy 20 lần/giây | |

**Tên trường trên file khai báo tường minh** bằng `[JsonProperty("origin")]`, không để Newtonsoft tự suy
từ tên property. Cùng lý do với việc `NetCmd` ghi số rõ ràng: đổi tên một property C# là chuyện gõ code,
còn đổi tên một trường trong file là **đổi định dạng** — hai việc khác nhau thì không được để một thao
tác `Rename` trong IDE làm cả hai.

**Toạ độ ô là toạ độ ô của Unity, không dịch không chia.** `Grid` trong `Map.prefab` đã có
`cellSize = 1` và đặt tại gốc toạ độ, nên ô `(cx, cy)` của Tilemap chiếm đúng vùng world
`[cx, cx+1] × [cy, cy+1]`. Giữ nguyên hệ đó nghĩa là đổi ô ↔ world chỉ còn `Floor(x)` — không phép chia
đôi, không phép dời gốc, và không có luôn cả lớp bug "lệch nửa ô".

Nhưng map thật **không bắt đầu ở ô (0,0)**: tilemap hiện tại trải từ `x = -17` tới `x ≈ 47`. Nên
`MapGrid` phải mang theo `OriginX`, `OriginY` — góc dưới-trái của vùng đã vẽ — và tự dịch khi tra mảng.
Người gọi không bao giờ thấy phép dịch ấy.

**Không có trường `size`.** Bản text cũ phải ghi `size 64 11` vì parser cần biết đọc bao nhiêu dòng.
Mảng JSON thì **tự biết độ dài mình**: `height = cells.Count`, `width = cells[0].Length`. Bỏ được một
trường nghĩa là bỏ được một cách để file **tự mâu thuẫn với chính nó** (ghi `size 64` mà có 63 hàng thì
tin ai?). Phép kiểm còn lại — mọi hàng phải dài bằng hàng đầu — vẫn giữ.

**Điểm spawn là một DANH SÁCH có `id`, không phải một điểm.** Hôm nay chỉ dùng đúng một điểm
(`"default"`), nhưng dạng danh sách là thứ tốn 3 dòng bây giờ và tiết kiệm một lần tăng `version` sau
này: cổng từ map khác sang phải trỏ được tới *chỗ nào* trên map này, và "chỗ nào" thì cần có tên.

**Ngoài lưới là gì?** Ba phía, ba câu trả lời, mỗi câu một lý do:

| Phía | Trả về | Vì sao |
|---|---|---|
| Trên đỉnh lưới | `Empty` | trần vô hình ngay trên đầu là thứ người chơi cảm nhận được ngay |
| Hai mép trái/phải | `Empty` | biên ngang do `Step` kẹp bằng `MinX`/`MaxX` đọc từ file, không cần tường ma |
| **Dưới đáy lưới** | **`Solid`** | **cầu chì**: một trạng thái không bao giờ được phép chạy ra vô cực |

Dòng cuối không phải thiết kế game. Vẽ thiếu một ô sàn thì hệ quả tệ nhất là "rơi xuống đáy map rồi đứng
đó" chứ không phải `Y = -3.4e38` rồi mọi phép tính sau đó thành vô nghĩa (và `NaN` thì đi thẳng vào DB —
xem lại `MoveHandler` của Phase 8). Hố thật và chết do rơi là việc của phase sau; lúc đó dòng này đổi
thành một mốc `y` gây `Die`.

**File map trông như thế này** (hàng đầu của `cells` là mép **trên** map, để đọc như nhìn bản vẽ):

```json
{
  "_comment": "Sinh bởi Tools/MMORPG/Export Map — KHÔNG sửa tay.",
  "version": 1,
  "id": 1,
  "name": "Rừng Mở Đầu",
  "origin": { "x": -17, "y": -9 },
  "spawns": [
    { "id": "default", "x": 0.5, "y": 0.0 }
  ],
  "cells": [
    "................................................................",
    "..........................====..................................",
    "###############################.....############################"
  ]
}
```

`_comment` là một trường thật trong DTO chứ không phải chú thích của JSON — JSON chuẩn **không có** cú
pháp chú thích. Gạch dưới ở đầu tên là quy ước quen thuộc cho "trường này dành cho người đọc, code không
dùng".

<details>
<summary><b>📖 Lời giải — <code>MapDefinition.cs</code> (DTO của file)</b></summary>

```csharp
using System.Collections.Generic;
using Newtonsoft.Json;

namespace MMORPG.Shared.World
{
    /// <summary>
    /// Bản đối chiếu 1-1 với file map JSON. KHÔNG dùng để chạy game — chỉ để đọc/ghi file; kiểu chạy
    /// trong game là <see cref="MapGrid"/>.
    ///
    /// Tách hai kiểu vì hai vai khác nhau: trường nào có trong file là chuyện của ĐỊNH DẠNG, còn tra
    /// một ô nhanh cỡ nào là chuyện của MÔ PHỎNG. Gộp lại thì mỗi lần đổi định dạng là đụng vào thứ
    /// chạy 20 lần mỗi giây. Cùng mẫu với CharacterRow (DB) ≠ PlayerEntity (world) ở Phase 5.
    ///
    /// Mọi trường khai báo tên tường minh bằng [JsonProperty]: đổi tên property C# là chuyện gõ code,
    /// đổi tên trường trong file là ĐỔI ĐỊNH DẠNG — không được để một thao tác Rename trong IDE làm cả
    /// hai cùng lúc.
    /// </summary>
    public sealed class MapDefinition
    {
        /// <summary>Dành cho người mở file ra đọc. JSON chuẩn không có cú pháp chú thích nên nó là một trường thật.</summary>
        [JsonProperty("_comment")]
        public string Comment { get; set; } = string.Empty;

        [JsonProperty("version")]
        public int Version { get; set; }

        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        // Cho phép null có chủ đích: file thiếu trường thì Newtonsoft để null, và MapFile.Parse phải
        // nói ra bằng một thông điệp đọc được — thay vì để NullReferenceException nổ ở đâu đó xa hơn.
        [JsonProperty("origin")]
        public CellPoint? Origin { get; set; }

        [JsonProperty("spawns")]
        public List<SpawnPoint>? Spawns { get; set; }

        /// <summary>Lưới ô, mỗi phần tử là MỘT HÀNG. Hàng đầu là mép TRÊN map — đọc file như nhìn bản vẽ.</summary>
        [JsonProperty("cells")]
        public List<string>? Cells { get; set; }
    }

    /// <summary>Một điểm theo toạ độ Ô (số nguyên). Dùng cho origin.</summary>
    public sealed class CellPoint
    {
        [JsonProperty("x")]
        public int X { get; set; }

        [JsonProperty("y")]
        public int Y { get; set; }
    }

    /// <summary>
    /// Một chỗ người chơi có thể xuất hiện, toạ độ WORLD. Có <see cref="Id"/> vì map cần trỏ tới nhau
    /// được bằng tên: một cổng ở map khác phải nói rõ nó dẫn tới điểm nào của map này.
    /// </summary>
    public sealed class SpawnPoint
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("x")]
        public float X { get; set; }

        [JsonProperty("y")]
        public float Y { get; set; }
    }
}
```

</details>

<details>
<summary><b>📖 Lời giải — <code>MapGrid.cs</code> (kiểu chạy trong game)</b></summary>

```csharp
using System;
using System.Collections.Generic;

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
    /// Hình dạng một map dạng lưới ô 1×1, kèm vài con số riêng của nó (id, tên, các điểm spawn).
    /// Nguồn DUY NHẤT về việc đi được chỗ nào: server va chạm thật và client va chạm dự đoán đều đọc
    /// từ đây, và cả hai dựng nó từ cùng một file.
    ///
    /// Cố tình KHÔNG mô tả hình thức (cỏ, cây, nền trời) — đó là việc của các lớp tilemap khác. Một
    /// lưới ba trạng thái không đủ để vẽ đẹp, và một hình đẹp thì thừa thãi với va chạm.
    ///
    /// Toạ độ ô ở đây LÀ toạ độ ô của Tilemap trong Unity: ô (cx, cy) chiếm vùng world
    /// [cx, cx+1] × [cy, cy+1]. Vùng đã vẽ bắt đầu từ (OriginX, OriginY) — có thể âm — và phép dịch
    /// về chỉ số mảng nằm gọn trong hàm At, không ai bên ngoài phải biết tới nó.
    /// </summary>
    public sealed class MapGrid
    {
        public const float CELL_SIZE = 1f;

        /// <summary>Id của điểm spawn mặc định — chỗ người chơi xuất hiện khi không có nguồn nào khác chỉ định.</summary>
        public const string DEFAULT_SPAWN_ID = "default";

        public int MapId { get; }
        public string Name { get; }

        /// <summary>Ô góc dưới-trái của vùng đã vẽ. Âm là chuyện bình thường.</summary>
        public int OriginX { get; }

        public int OriginY { get; }

        public int Width { get; }
        public int Height { get; }

        public IReadOnlyList<SpawnPoint> Spawns => _spawns;

        /// <summary>Điểm spawn mặc định, chốt một lần lúc dựng để chỗ gọi không phải tìm lại mỗi lần.</summary>
        public SpawnPoint DefaultSpawn { get; }

        private readonly SpawnPoint[] _spawns;

        // Mảng một chiều chứ không phải [,]: cùng số ô, ít một tầng gián tiếp, và tiện cho vòng băm ở
        // Checksum. Chỉ số = (cy - OriginY) * Width + (cx - OriginX).
        private readonly CellType[] _cells;

        public MapGrid(int mapId, string name, int originX, int originY, int width, int height,
            IReadOnlyList<SpawnPoint> spawns, CellType[] cells)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentException($"Kích thước map không hợp lệ: {width}×{height}.");

            if (cells.Length != width * height)
                throw new ArgumentException($"Lưới {width}×{height} cần {width * height} ô, nhận {cells.Length}.");

            if (spawns.Count == 0)
                throw new ArgumentException("Map phải có ít nhất một điểm spawn.");

            MapId = mapId;
            Name = name;
            OriginX = originX;
            OriginY = originY;
            Width = width;
            Height = height;
            _cells = cells;

            // Copy ra mảng riêng: người gọi giữ tiếp danh sách của họ và sửa nó cũng không đụng được
            // vào map đang chạy. Bất biến không phải là chuyện phong cách — map bị sửa giữa chừng thì
            // client và server lệch nhau mà không ai biết từ lúc nào.
            _spawns = new SpawnPoint[spawns.Count];
            for (int i = 0; i < spawns.Count; i++)
                _spawns[i] = spawns[i];

            DefaultSpawn = FindDefaultSpawn(_spawns);
        }

        /// <summary>Mép trái/phải của map theo world. Thay cho hằng WORLD_HALF_EXTENT của Phase 6.</summary>
        public float MinX => OriginX * CELL_SIZE;

        public float MaxX => (OriginX + Width) * CELL_SIZE;

        /// <summary>
        /// Ô tại toạ độ ô. Ba phía ngoài lưới trả về ba thứ khác nhau, mỗi thứ một lý do — xem bảng
        /// trong tài liệu phase. Đáy lưới là Solid: đó là CẦU CHÌ chứ không phải thiết kế game. Vẽ
        /// thiếu một ô sàn thì hệ quả tệ nhất là rơi xuống đáy map rồi đứng đó, chứ không phải Y trôi
        /// ra vô cực và mọi phép tính sau đó thành vô nghĩa.
        /// </summary>
        public CellType At(int cx, int cy)
        {
            if (cy < OriginY)
                return CellType.Solid;

            if (cx < OriginX || cx >= OriginX + Width || cy >= OriginY + Height)
                return CellType.Empty;

            return _cells[(cy - OriginY) * Width + (cx - OriginX)];
        }

        public CellType AtWorld(float x, float y)
        {
            return At(CellX(x), CellY(y));
        }

        // Floor chứ không phải ép kiểu (int): cast cắt VỀ PHÍA 0 nên -0.5 thành 0, trong khi
        // Floor(-0.5) = -1. Map này có toạ độ âm ở nửa trái nên đây không phải chuyện lý thuyết.
        public static int CellX(float worldX)
        {
            return (int)MathF.Floor(worldX / CELL_SIZE);
        }

        public static int CellY(float worldY)
        {
            return (int)MathF.Floor(worldY / CELL_SIZE);
        }

        public static float ColumnLeft(int cx)
        {
            return cx * CELL_SIZE;
        }

        public static float ColumnRight(int cx)
        {
            return (cx + 1) * CELL_SIZE;
        }

        public static float RowBottom(int cy)
        {
            return cy * CELL_SIZE;
        }

        public static float RowTop(int cy)
        {
            return (cy + 1) * CELL_SIZE;
        }

        /// <summary>
        /// Dấu vân tay của LƯỚI (FNV-1a). Hai bên in ra cùng một số nghĩa là đang chạy đúng một map —
        /// bằng chứng rẻ nhất có thể có, và là hạt giống cho phép kiểm version ở Phase 12.
        ///
        /// Cố ý không băm danh sách spawn: câu hỏi con số này trả lời là "hai bên có cùng hình dạng va
        /// chạm không", còn điểm spawn thì chỉ server dùng.
        /// </summary>
        public uint Checksum()
        {
            uint hash = 2166136261u;

            hash = Mix(hash, MapId);
            hash = Mix(hash, OriginX);
            hash = Mix(hash, OriginY);
            hash = Mix(hash, Width);
            hash = Mix(hash, Height);

            for (int i = 0; i < _cells.Length; i++)
                hash = Mix(hash, (int)_cells[i]);

            return hash;
        }

        /// <summary>
        /// Điểm mang id "default"; không có thì lấy điểm đầu tiên.
        ///
        /// Lùi về điểm đầu chứ không ném: map thiếu điểm mặc định vẫn là map chơi được, và chết lúc
        /// khởi động vì một cái tên là cái giá quá đắt. Còn map KHÔNG có điểm spawn nào thì đã bị hàm
        /// dựng chặn từ trước — đó mới là thứ không chơi được.
        /// </summary>
        private static SpawnPoint FindDefaultSpawn(SpawnPoint[] spawns)
        {
            for (int i = 0; i < spawns.Length; i++)
            {
                if (spawns[i].Id == DEFAULT_SPAWN_ID)
                    return spawns[i];
            }

            return spawns[0];
        }

        // FNV-1a nuốt từng byte một. Cộng thẳng cả int vào thì hai lưới hoán vị vài ô vẫn có thể ra
        // cùng một số; xor theo byte rồi nhân số nguyên tố ở mỗi bước thì không.
        private static uint Mix(uint hash, int value)
        {
            for (int shift = 0; shift < 32; shift += 8)
            {
                hash ^= (uint)((value >> shift) & 0xFF);
                hash *= 16777619u;
            }

            return hash;
        }
    }
}
```

</details>

<details>
<summary><b>📖 Lời giải — <code>MapFile.cs</code> (đọc/ghi)</b></summary>

```csharp
using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace MMORPG.Shared.World
{
    /// <summary>
    /// Đọc và ghi file map JSON. Hai chiều nằm cùng một chỗ có chủ đích: tool export gọi Write, hai
    /// đầu dây gọi Parse — nên định dạng chỉ có một bản mô tả duy nhất, và bài test round-trip ở
    /// Shared.Tests kiểm được nó bằng máy thay vì bằng mắt.
    /// </summary>
    public static class MapFile
    {
        /// <summary>
        /// Version của ĐỊNH DẠNG, không phải của map. Luật: thêm một trường tuỳ chọn thì GIỮ NGUYÊN số
        /// này (JSON tự lo — trường thiếu về mặc định, trường lạ bị bỏ qua); chỉ tăng khi đổi ý nghĩa,
        /// đổi tên hoặc xoá một trường đã có. Đọc phải version lạ thì ném ngay chứ không cố đoán: một
        /// file map đọc sai một nửa còn tệ hơn một file map không đọc được.
        /// </summary>
        public const int FORMAT_VERSION = 1;

        public const char CHAR_EMPTY = '.';
        public const char CHAR_SOLID = '#';
        public const char CHAR_ONE_WAY = '=';

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,

            // Trường lạ thì BỎ QUA. Có chủ đích, và ngược với file config gõ tay ở Phase 12 (nơi sẽ
            // dùng Error): file này do TOOL sinh nên không có lỗi chính tả để bắt, còn cái ta cần là
            // code hôm nay đọc được file mà phiên bản mai này thêm trường vào.
            MissingMemberHandling = MissingMemberHandling.Ignore,
        };

        public static MapGrid Parse(string json)
        {
            MapDefinition? definition = JsonConvert.DeserializeObject<MapDefinition>(json, Settings);

            if (definition == null)
                throw new FormatException("File map rỗng hoặc không phải JSON hợp lệ.");

            if (definition.Version != FORMAT_VERSION)
                throw new FormatException($"File map ghi version {definition.Version}, code chỉ đọc được version {FORMAT_VERSION}. Export lại map.");

            // Rút ra biến cục bộ rồi mới kiểm null. Kiểm thẳng trên property cũng chạy đúng, nhưng
            // phân tích nullable của C# chỉ nhớ chắc chắn trạng thái null của BIẾN CỤC BỘ — kiểm trên
            // property rồi dùng nó sau vài dòng là cách rẻ nhất để lãnh một đống cảnh báo CS8604.
            CellPoint? origin = definition.Origin;
            List<string>? rows = definition.Cells;
            List<SpawnPoint>? spawns = definition.Spawns;

            // Ba phép kiểm này là chỗ trả tiền cho việc để DTO cho phép null: thiếu trường thì người
            // đọc log biết THIẾU CÁI GÌ, thay vì một NullReferenceException ở dòng nào đó xa hơn.
            if (origin == null)
                throw new FormatException("Thiếu trường \"origin\".");

            if (rows == null || rows.Count == 0)
                throw new FormatException("Thiếu lưới ô — trường \"cells\" rỗng.");

            if (spawns == null || spawns.Count == 0)
                throw new FormatException("Map phải có ít nhất một điểm trong \"spawns\".");

            // Kích thước SUY RA từ mảng, không đọc từ một trường riêng: không có trường thì không có
            // cách nào để file tự mâu thuẫn với chính nó.
            int height = rows.Count;
            int width = rows[0].Length;

            var cells = new CellType[width * height];

            for (int row = 0; row < height; row++)
            {
                string line = rows[row];

                // Chuỗi rỗng bắt luôn cả trường hợp JSON ghi null trong mảng — không có hàng nào hợp
                // lệ mà rỗng, vì width đã lấy từ hàng đầu và width = 0 thì MapGrid từ chối.
                if (string.IsNullOrEmpty(line))
                    throw new FormatException($"Hàng {row} rỗng.");

                if (line.Length != width)
                    throw new FormatException($"Hàng {row} dài {line.Length} ký tự, hàng đầu dài {width}.");

                // Hàng ĐẦU trong file là mép TRÊN map — để đọc file như nhìn bản vẽ. Nên khi nạp vào
                // lưới (gốc ở dưới) phải lật trục Y. Quyết định một lần, ghi ngay tại đây, vì viết sai
                // chỉ một trong hai chiều thì map lộn ngược mà không có lỗi nào.
                int cy = height - 1 - row;

                for (int cx = 0; cx < width; cx++)
                    cells[cy * width + cx] = ToCell(line[cx], row, cx);
            }

            return new MapGrid(definition.Id, definition.Name, origin.X, origin.Y,
                width, height, spawns, cells);
        }

        public static string Write(MapGrid map)
        {
            var rows = new List<string>(map.Height);
            var line = new StringBuilder(map.Width);

            for (int row = 0; row < map.Height; row++)
            {
                line.Clear();

                // Ghi từ mép TRÊN xuống — đối xứng với phép lật trong Parse.
                int cy = map.OriginY + map.Height - 1 - row;

                for (int cx = map.OriginX; cx < map.OriginX + map.Width; cx++)
                    line.Append(ToChar(map.At(cx, cy)));

                rows.Add(line.ToString());
            }

            var definition = new MapDefinition
            {
                Comment = "Sinh bởi Tools/MMORPG/Export Map — KHÔNG sửa tay. " +
                          "Sửa va chạm = vẽ lại lớp Tilemap \"Collision\" trong Unity rồi export lại.",
                Version = FORMAT_VERSION,
                Id = map.MapId,
                Name = map.Name,
                Origin = new CellPoint { X = map.OriginX, Y = map.OriginY },
                Spawns = new List<SpawnPoint>(map.Spawns),
                Cells = rows,
            };

            return JsonConvert.SerializeObject(definition, Settings);
        }

        private static CellType ToCell(char symbol, int row, int column)
        {
            switch (symbol)
            {
                case CHAR_EMPTY: return CellType.Empty;
                case CHAR_SOLID: return CellType.Solid;
                case CHAR_ONE_WAY: return CellType.OneWay;

                // Không có nhánh "coi như rỗng": một ký tự lạ nghĩa là file đã hỏng ở đâu đó, và đoán
                // bừa chỉ dời thời điểm phát hiện tới lúc có người đi xuyên tường.
                default:
                    throw new FormatException($"Ký tự lạ '{symbol}' ở hàng {row}, cột {column}.");
            }
        }

        private static char ToChar(CellType cell)
        {
            switch (cell)
            {
                case CellType.Solid: return CHAR_SOLID;
                case CellType.OneWay: return CHAR_ONE_WAY;
                default: return CHAR_EMPTY;
            }
        }
    }
}
```

</details>

### ✅ CHECKPOINT A — định dạng tự kiểm được bằng máy

`Server/Shared.Tests` đã có từ Phase 1. Thêm `MapFileTests.cs` với bốn bài:

1. **Round-trip**: dựng một `MapGrid` nhỏ bằng tay → `Write` → `Parse` → so từng ô và so cả
   `Checksum()`. Lưới mẫu phải **không đối xứng theo trục Y** — lưới đối xứng thì lật trục sai vẫn cho
   round-trip xanh, và bài test trở thành lời trấn an vô nghĩa.
2. **Version sai** → `Parse` ném `FormatException`.
3. **Hàng lệch độ dài** → `Parse` ném `FormatException`.
4. **Trường lạ thì bỏ qua** — chèn một mảng `"portals": [...]` vào file rồi `Parse` bình thường. Bài này
   không kiểm code, nó **kiểm quyết định thiết kế**: nó là bằng chứng chạy được rằng thêm dữ liệu mới
   vào map sau này không phá code đang có. Xoá bài này đi là mất cái duy nhất canh giữ tính chất ấy.

```bash
dotnet test Server/Shared.Tests
```

Xanh cả bốn mới đi tiếp.

<details>
<summary><b>📖 Lời giải — <code>MapFileTests.cs</code></b></summary>

```csharp
using MMORPG.Shared.World;

namespace MMORPG.Shared.Tests
{
    public class MapFileTests
    {
        /// <summary>
        /// Lưới 4×3 có đủ ba loại ô và KHÔNG đối xứng theo trục Y — cố ý, để phép lật trục sai thì bài
        /// test đỏ. Origin âm cũng là cố ý: map thật có origin âm.
        /// </summary>
        private static MapGrid BuildSample()
        {
            var cells = new[]
            {
                // cy = OriginY (hàng dưới cùng)
                CellType.Solid, CellType.Solid, CellType.Solid, CellType.Solid,
                // cy = OriginY + 1
                CellType.Empty, CellType.OneWay, CellType.OneWay, CellType.Empty,
                // cy = OriginY + 2
                CellType.Empty, CellType.Empty, CellType.Empty, CellType.Solid,
            };

            var spawns = new List<SpawnPoint>
            {
                new SpawnPoint { Id = MapGrid.DEFAULT_SPAWN_ID, X = 0.5f, Y = 1f },
            };

            return new MapGrid(7, "Test Map", originX: -3, originY: -2, width: 4, height: 3, spawns, cells);
        }

        [Fact]
        public void Write_then_parse_gives_back_the_same_grid()
        {
            MapGrid original = BuildSample();
            MapGrid parsed = MapFile.Parse(MapFile.Write(original));

            Assert.Equal(original.MapId, parsed.MapId);
            Assert.Equal(original.OriginX, parsed.OriginX);
            Assert.Equal(original.OriginY, parsed.OriginY);
            Assert.Equal(original.Width, parsed.Width);
            Assert.Equal(original.Height, parsed.Height);
            Assert.Equal(original.DefaultSpawn.X, parsed.DefaultSpawn.X);

            for (int cy = original.OriginY; cy < original.OriginY + original.Height; cy++)
            {
                for (int cx = original.OriginX; cx < original.OriginX + original.Width; cx++)
                    Assert.Equal(original.At(cx, cy), parsed.At(cx, cy));
            }

            // Và phép so rẻ nhất — chính là phép hai đầu dây sẽ dùng để tự kiểm lúc chạy.
            Assert.Equal(original.Checksum(), parsed.Checksum());
        }

        [Fact]
        public void Parse_rejects_unknown_format_version()
        {
            string json = MapFile.Write(BuildSample()).Replace("\"version\": 1", "\"version\": 99");

            Assert.Throws<FormatException>(() => MapFile.Parse(json));
        }

        [Fact]
        public void Parse_rejects_row_with_wrong_length()
        {
            string json = MapFile.Write(BuildSample()).Replace("\"####\"", "\"###\"");

            Assert.Throws<FormatException>(() => MapFile.Parse(json));
        }

        /// <summary>
        /// Bài này kiểm QUYẾT ĐỊNH THIẾT KẾ, không kiểm code: file map do tool sinh, nên code hôm nay
        /// phải đọc được file mà bản mai này thêm trường vào. Đó là toàn bộ lý do chọn JSON — và đây là
        /// thứ duy nhất canh giữ nó.
        /// </summary>
        [Fact]
        public void Parse_ignores_fields_it_does_not_know()
        {
            string json = MapFile.Write(BuildSample())
                .Replace("\"version\": 1", "\"portals\": [ { \"x\": 3, \"toMapId\": 2 } ],\n  \"version\": 1");

            MapGrid parsed = MapFile.Parse(json);

            Assert.Equal(BuildSample().Checksum(), parsed.Checksum());
        }
    }
}
```

</details>

---

## Bước 2 — Unity: vẽ lớp `Collision`, export, và hai bên cùng đọc một file

### Hướng làm

**Trong `Map.prefab`, thêm một Tilemap nữa tên `Collision`,** ngang hàng với `Ground` / `Plants` /
`Fences`. Lớp này chỉ dùng **hai tile đánh dấu** — một cho ô đặc, một cho bệ một chiều. Lấy hai tile bất
kỳ trong tileset cũng được, nhưng nên tạo hai tile riêng màu phẳng (đỏ / vàng) để nhìn là biết: đây là
lớp **luật**, không phải lớp hình.

Ba thứ phải chỉnh trên lớp này:

| Thứ | Đặt thế nào | Vì sao |
|---|---|---|
| `TilemapRenderer` | tắt lúc chơi, bật lúc vẽ (hoặc để `Sorting Layer` trên cùng, màu alpha ~0.4) | luật là để bạn nhìn, không phải để người chơi nhìn |
| Vị trí `Grid` và `Collision` | **cả hai đúng `(0,0,0)`**, `cellSize = 1` | ô tilemap phải trùng ô `MapGrid`; lệch một chút là lệch cả map |
| Một `Transform` con tên `Spawn_default` | kéo tới chỗ muốn người chơi xuất hiện | điểm spawn là chuyện của map, nên nó đi cùng map |

Đừng vẽ `Collision` bằng cách "tô đè lên đúng từng viên cỏ". Vẽ **hình dạng bạn muốn người chơi cảm
nhận**: mặt đất là một dải liền, bệ gỗ là một hàng `OneWay` ở đúng mặt trên, cây và hàng rào thì thường
**không** có ô nào cả. Hình và luật giống nhau ~90% là bình thường và đúng; ép chúng giống nhau 100% là
tự chuốc lấy việc.

**Component mới `MapCollisionSource`** (runtime script, đặt cạnh `Collision`): chỗ khai báo mọi thứ tool
cần biết — `mapId`, tên map, tilemap nào, tile nào là `Solid`, tile nào là `OneWay`, và **danh sách điểm
spawn** (mỗi điểm gồm một `id` và một `Transform`). Để trong Inspector chứ không hard-code trong tool:
người vẽ map không phải mở code ra sửa.

Danh sách chứ không phải một điểm — hôm nay bạn chỉ điền đúng một dòng, `id = "default"`. Nhưng cái giá
để nó là danh sách ngay từ đầu là ba dòng code, còn cái giá để đổi một trường thành mảng sau khi đã có
map là một lần tăng `FORMAT_VERSION` và một lần export lại tất cả.

**Tool mới `Assets/Game/Editor/MapExporter.cs`** — menu `Tools/MMORPG/Export Map`. Nó chỉ làm bốn việc,
và ba trong bốn là **kiểm tra**:

1. **Kiểm hệ toạ độ.** `tilemap.CellToWorld((3,5,0))` phải ra đúng `(3,5,0)`. Một phép kiểm này bắt trọn
   ba lỗi khác nhau: Grid bị dời, `cellSize` khác 1, hoặc object `Collision` có offset cục bộ. Không có
   nó thì triệu chứng là "map lệch nửa ô" — thứ mất cả buổi tối để lần ra.
2. **Đọc `cellBounds`** sau `CompressBounds()` → đó chính là `origin` và kích thước của map. Vẽ rộng ra
   thì map rộng ra, không có hằng số nào phải sửa.
3. **Dịch tile → `CellType`.** Tile không thuộc hai tile đã khai báo thì **dừng và báo lỗi kèm toạ độ ô**
   — đừng "coi như rỗng". Một viên tile lạc vào lớp `Collision` mà bị bỏ qua âm thầm là một lỗ hổng trên
   sàn không ai nhìn thấy.
4. **Ghi file** bằng `MapFile.Write` — cùng cái hàm mà `Parse` ở CHECKPOINT A đã kiểm.

Tool **không** tự sinh JSON. Nó dựng một `MapGrid` rồi nhờ `Shared` ghi. Nếu tool tự nối chuỗi JSON lấy
thì định dạng có hai bản mô tả, và ta quay lại đúng vấn đề của bản cũ — chỉ khác là lần này lệch giữa
"người ghi" và "người đọc" thay vì giữa "hình" và "luật".

**Đường đi của file, hai đầu:**

- **Client** đọc `Assets/Game/Resources/Maps/map1.json` bằng `Resources.Load<TextAsset>("Maps/map1")`
  (Unity coi `.json` là `TextAsset`, và `Resources.Load` bỏ phần đuôi). `Resources` là cách rẻ nhất cho
  hôm nay; Phase 18 chuyển nó sang Addressables/CDN cùng các bảng dữ liệu khác, và lúc đó chỉ
  `MapService` đổi.
- **Server** đọc `Data/Maps/map1.json` cạnh file exe. File tới đó bằng **một target trong
  `GameServer.csproj`**, đối xứng với target đưa `MMORPG.Shared.dll` sang Unity. Copy bằng tay thì sớm
  muộn có một lần quên, và triệu chứng của lần quên đó là *server nói có tường ở chỗ client thấy trống*.

**Nạp map lúc khởi động, không phải lúc cần.** `Program.cs` đọc file **trước khi** listener bắt đầu nhận
kết nối. File hỏng thì server chết ngay lúc khởi động với thông điệp rõ ràng — đúng lúc bạn đang nhìn
console. Nạp lười thì nó chết vào lần đầu có người vào world, tức là 20 phút sau, giữa lúc bạn đang làm
việc khác.

`WorldService` nhận `MapGrid` qua hàm dựng và cho đọc lại qua property. Nhờ đó ba hằng `DEFAULT_MAP_ID` /
`SPAWN_X` / `SPAWN_Y` **biến mất** — chúng là dữ liệu của map, và bây giờ map có chỗ để chứa dữ liệu của
chính nó.

<details>
<summary><b>📖 Lời giải — <code>MapCollisionSource.cs</code> (runtime)</b></summary>

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace MMORPG.Client.World
{
    /// <summary>
    /// Khai báo mọi thứ tool export cần biết về một map: lớp tilemap nào chứa lưới va chạm, tile nào
    /// mang nghĩa gì, map này là map số mấy, người chơi xuất hiện ở những đâu.
    ///
    /// Nằm trong Inspector chứ không hard-code trong tool: người vẽ map đổi tile đánh dấu hay dời điểm
    /// spawn thì không phải mở code ra sửa.
    ///
    /// Là MonoBehaviour runtime (không phải Editor script) để nó lưu được vào prefab; nhưng lúc chơi
    /// thì không ai đọc nó — client đọc file đã export, y như server.
    /// </summary>
    public sealed class MapCollisionSource : MonoBehaviour
    {
        /// <summary>Một điểm spawn đặt bằng tay trong Scene. Id là tên mà map khác sẽ trỏ tới.</summary>
        [Serializable]
        public struct SpawnMarker
        {
            public string Id;
            public Transform Point;
        }

        [SerializeField] private int _mapId = 1;
        [SerializeField] private string _mapName = "Map 1";
        [SerializeField] private Tilemap _collisionTilemap;

        [Header("Tile đánh dấu — mọi tile khác trên lớp Collision đều là lỗi")]
        [SerializeField] private TileBase _solidTile;
        [SerializeField] private TileBase _oneWayTile;

        [Header("Điểm spawn — cần ít nhất một điểm id \"default\"")]
        [SerializeField] private List<SpawnMarker> _spawns = new();

        public int MapId => _mapId;
        public string MapName => _mapName;
        public Tilemap CollisionTilemap => _collisionTilemap;
        public TileBase SolidTile => _solidTile;
        public TileBase OneWayTile => _oneWayTile;
        public IReadOnlyList<SpawnMarker> Spawns => _spawns;
    }
}
```

</details>

<details>
<summary><b>📖 Lời giải — <code>Assets/Game/Editor/MapExporter.cs</code></b></summary>

```csharp
using System.Collections.Generic;
using System.IO;
using HungNT;
using MMORPG.Client.World;
using MMORPG.Shared.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace MMORPG.Client.EditorTools
{
    /// <summary>
    /// Xuất lớp Tilemap "Collision" trong scene ra file map JSON mà cả server lẫn client cùng đọc.
    ///
    /// Tool này KHÔNG tự sinh JSON: nó dựng một MapGrid rồi nhờ MapFile.Write ghi. Tự nối chuỗi lấy là
    /// tạo ra bản mô tả định dạng thứ hai, và lệch giữa người-ghi với người-đọc thì không có bài test
    /// nào bắt được.
    /// </summary>
    public static class MapExporter
    {
        private const string OUTPUT_FOLDER = "Assets/Game/Resources/Maps";

        [MenuItem("Tools/MMORPG/Export Map")]
        public static void Export()
        {
            var source = Object.FindFirstObjectByType<MapCollisionSource>(FindObjectsInactive.Include);

            if (source == null)
            {
                Fail("Không thấy MapCollisionSource nào trong scene đang mở.");
                return;
            }

            Tilemap tilemap = source.CollisionTilemap;

            if (tilemap == null || source.SolidTile == null || source.OneWayTile == null)
            {
                Fail("MapCollisionSource còn ô trống trong Inspector.");
                return;
            }

            if (!TryCollectSpawns(source, out List<SpawnPoint> spawns))
                return;

            // Một phép kiểm bắt trọn ba lỗi: Grid bị dời, cellSize khác 1, object Collision có offset
            // cục bộ. Thiếu nó thì triệu chứng là "map lệch nửa ô" — mất cả buổi tối để lần ra.
            var probe = new Vector3Int(3, 5, 0);

            if (tilemap.CellToWorld(probe) != new Vector3(3f, 5f, 0f))
            {
                Fail($"Hệ toạ độ ô không trùng world: ô (3,5) rơi vào {tilemap.CellToWorld(probe)}. " +
                     "Grid và Tilemap phải ở (0,0,0) với cellSize = 1.");
                return;
            }

            // CompressBounds trước khi đọc: cellBounds giữ lại cả vùng từng vẽ rồi xoá, nên không nén
            // thì map phình ra hàng chục cột rỗng — và origin ghi trong file sẽ sai so với hình.
            tilemap.CompressBounds();
            BoundsInt bounds = tilemap.cellBounds;

            int width = bounds.size.x;
            int height = bounds.size.y;
            var cells = new CellType[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var position = new Vector3Int(bounds.xMin + x, bounds.yMin + y, 0);
                    TileBase tile = tilemap.GetTile(position);

                    if (tile == null)
                    {
                        cells[y * width + x] = CellType.Empty;
                    }
                    else if (tile == source.SolidTile)
                    {
                        cells[y * width + x] = CellType.Solid;
                    }
                    else if (tile == source.OneWayTile)
                    {
                        cells[y * width + x] = CellType.OneWay;
                    }
                    else
                    {
                        // Dừng hẳn thay vì "coi như rỗng": một viên tile lạc vào lớp Collision mà bị bỏ
                        // qua âm thầm là một lỗ trên sàn mà không ai nhìn thấy.
                        Fail($"Tile lạ ở ô ({position.x}, {position.y}): {tile.name}. " +
                             "Lớp Collision chỉ được chứa hai tile đã khai báo.");
                        return;
                    }
                }
            }

            var map = new MapGrid(source.MapId, source.MapName, bounds.xMin, bounds.yMin,
                width, height, spawns, cells);

            Directory.CreateDirectory(OUTPUT_FOLDER);
            string path = $"{OUTPUT_FOLDER}/map{map.MapId}.json";
            File.WriteAllText(path, MapFile.Write(map));

            // Không có dòng này thì file mới nằm trên đĩa nhưng Unity chưa biết, và Resources.Load vẫn
            // trả về nội dung cũ cho tới lần focus lại cửa sổ Editor.
            AssetDatabase.ImportAsset(path);

            DebugEx.Log($"[MapExporter] Đã ghi {path} — {width}×{height} ô, origin ({bounds.xMin}, {bounds.yMin}), " +
                        $"{spawns.Count} điểm spawn, checksum {map.Checksum():X8}");
            DebugEx.Log("[MapExporter] Nhớ build lại GameServer để file map sang được thư mục output của server.");
        }

        /// <summary>
        /// Gom danh sách điểm spawn từ Inspector, và chặn ba kiểu sai mà người điền tay hay mắc.
        /// Tool sinh dữ liệu thì phải khó tính ở đây — vì mọi thứ đọc file về sau đều tin nó.
        /// </summary>
        private static bool TryCollectSpawns(MapCollisionSource source, out List<SpawnPoint> spawns)
        {
            spawns = new List<SpawnPoint>();
            bool hasDefault = false;

            foreach (MapCollisionSource.SpawnMarker marker in source.Spawns)
            {
                if (marker.Point == null || string.IsNullOrWhiteSpace(marker.Id))
                {
                    Fail("Có một dòng trong danh sách Spawns còn thiếu Id hoặc Transform.");
                    return false;
                }

                // Trùng id thì map khác trỏ sang sẽ tới nhầm chỗ — và không ai biết là đã tới nhầm.
                foreach (SpawnPoint existing in spawns)
                {
                    if (existing.Id != marker.Id)
                        continue;

                    Fail($"Hai điểm spawn trùng id \"{marker.Id}\".");
                    return false;
                }

                if (marker.Id == MapGrid.DEFAULT_SPAWN_ID)
                    hasDefault = true;

                Vector3 position = marker.Point.position;
                spawns.Add(new SpawnPoint { Id = marker.Id, X = position.x, Y = position.y });
            }

            if (!hasDefault)
            {
                Fail($"Map phải có một điểm spawn id \"{MapGrid.DEFAULT_SPAWN_ID}\" — đó là chỗ người chơi vào lần đầu.");
                return false;
            }

            return true;
        }

        private static void Fail(string message)
        {
            // Hai đường: dialog để người đang bấm menu thấy ngay, log để còn dấu vết mà đọc lại.
            EditorUtility.DisplayDialog("Export Map thất bại", message, "OK");
            DebugEx.LogError($"[MapExporter] {message}");
        }
    }
}
```

> `DebugEx.Log(...)` dạng static (không phải `this.Log`) vì đây là class static — không có instance nào
> để extension bám vào. Tên tool tự chèn tay trong chuỗi, đúng một lần, ở đúng một file.

</details>

<details>
<summary><b>📖 Lời giải — server đọc file (<code>Program.cs</code>, <code>GameServer.csproj</code>)</b></summary>

**`Server/GameServer/GameServer.csproj`** — thêm vào cuối, trước `</Project>`:

```xml
    <!--
    File map đi NGƯỢC chiều với MMORPG.Shared.dll: nó sinh ra trong Unity (tool export) và chảy sang
    server. Để build lo việc copy, đúng như chiều kia — copy tay thì sớm muộn có một lần quên, và
    triệu chứng của lần quên đó là server nói có tường ở chỗ client thấy trống.
    -->
    <ItemGroup>
        <Content Include="..\..\Assets\Game\Resources\Maps\*.json"
                 Link="Data\Maps\%(Filename)%(Extension)"
                 CopyToOutputDirectory="PreserveNewest"/>
    </ItemGroup>

    <Target Name="CheckMapFolder" BeforeTargets="Build">
        <!-- Glob không khớp file nào thì MSBuild im lặng. Biến sự im lặng đó thành lỗi build. -->
        <Error Condition="!Exists('$(MSBuildProjectDirectory)/../../Assets/Game/Resources/Maps')"
               Text="Không thấy Assets/Game/Resources/Maps. Chạy Tools/MMORPG/Export Map trong Unity trước đã."/>
    </Target>
```

**`Server/GameServer/Program.cs`** — nạp map trước khi mở listener:

```csharp
// Nạp map TRƯỚC khi nhận kết nối. File hỏng thì server chết ngay lúc khởi động với thông điệp rõ ràng,
// đúng lúc bạn đang nhìn console — thay vì chết vào lần đầu có người vào world, hai mươi phút sau.
string mapPath = Path.Combine(AppContext.BaseDirectory, "Data", "Maps", "map1.json");
MapGrid map = MapFile.Parse(File.ReadAllText(mapPath));

Log.Info($"Map {map.Name.Cyan()} #{map.MapId} — {map.Width}×{map.Height} ô, " +
         $"origin ({map.OriginX}, {map.OriginY}), checksum {map.Checksum():X8}");

var worldService = new WorldService(map);
```

(`AppContext.BaseDirectory` chứ không phải `Directory.GetCurrentDirectory()`: thư mục hiện hành là chỗ
bạn *gõ lệnh*, không phải chỗ file exe nằm — chạy `dotnet run` từ thư mục khác là hỏng.)

**`Server/GameServer/World/WorldService.cs`** — nhận map, bỏ ba hằng số:

```csharp
        public const int DEFAULT_CLASS_ID = 1;

        // DEFAULT_MAP_ID, SPAWN_X, SPAWN_Y bị xoá: chúng là dữ liệu CỦA map, và giờ map có chỗ chứa dữ
        // liệu của chính nó. Ai cần thì hỏi Map.MapId / Map.DefaultSpawn.

        /// <summary>Hình dạng thế giới. Một map cho tới khi có cửa chuyển map.</summary>
        public MapGrid Map { get; }

        public WorldService(MapGrid map)
        {
            Map = map;
        }
```

`Spawn` đưa map cho entity lúc dựng — Bước 3 sẽ nói vì sao entity cần map ngay tại đó chứ không phải mỗi
tick:

```csharp
            var entity = new PlayerEntity(entityId, row, owner, Map);
```

**`Server/GameServer/World/CharacterService.cs`** — điểm spawn lấy từ map:

```csharp
                DbCmd.CharacterGetOrCreate, new CharacterGetOrCreateRequest
                {
                    AccountId = session.AccountId,
                    Name = session.Username,
                    ClassId = WorldService.DEFAULT_CLASS_ID,

                    // Ba dòng này từng là hằng số trong WorldService. Giờ chúng là dữ liệu đi cùng map
                    // — dời điểm spawn = kéo Transform trong Unity rồi export, không build lại server.
                    MapId = _worldService.Map.MapId,
                    X = _worldService.Map.DefaultSpawn.X,
                    Y = _worldService.Map.DefaultSpawn.Y,
                }
```

</details>

<details>
<summary><b>📖 Lời giải — client đọc file (<code>MapService.cs</code> + DI)</b></summary>

**`Assets/Game/Scripts/World/MapService.cs`** (file mới):

```csharp
using System.IO;
using HungNT;
using MMORPG.Shared.World;
using UnityEngine;

namespace MMORPG.Client.World
{
    /// <summary>
    /// Nạp hình dạng map từ file đã export. Client đọc ĐÚNG file server đọc — đó là toàn bộ lý do nó
    /// tồn tại, và cũng là lý do ở đây không có tí logic nào về hình dạng map.
    ///
    /// Resources là cách rẻ nhất cho hôm nay. Phase 18 chuyển nó sang Addressables/CDN cùng các bảng dữ
    /// liệu khác; lúc đó chỉ hàm Load này đổi, chỗ gọi giữ nguyên.
    /// </summary>
    public sealed class MapService
    {
        private const string RESOURCE_FOLDER = "Maps";

        /// <summary>Map đang đứng. Null cho tới lần Load đầu tiên.</summary>
        public MapGrid Current { get; private set; }

        public MapGrid Load(int mapId)
        {
            // Đã nạp đúng map này rồi thì thôi: Load được gọi mỗi lần vào world, mà vào world lại sau
            // khi logout là chuyện thường.
            if (Current != null && Current.MapId == mapId)
                return Current;

            // Không có đuôi .json trong đường dẫn: Resources.Load luôn bỏ phần đuôi file.
            var asset = Resources.Load<TextAsset>($"{RESOURCE_FOLDER}/map{mapId}");

            if (asset == null)
                throw new FileNotFoundException($"Không thấy Resources/{RESOURCE_FOLDER}/map{mapId}.json. Chạy Tools/MMORPG/Export Map.");

            Current = MapFile.Parse(asset.text);

            // In checksum ra để đối chiếu với dòng server in lúc khởi động. Hai số khác nhau nghĩa là
            // hai bên đang chạy hai map khác nhau — biết ngay ở đây, thay vì đoán qua triệu chứng.
            this.Log($"Map {Current.Name} #{Current.MapId} — {Current.Width}×{Current.Height} ô, " +
                     $"checksum {Current.Checksum():X8}");

            return Current;
        }
    }
}
```

**`GameLifetimeScope.Configure`** — thêm một dòng vào cụm World:

```csharp
            builder.Register<MapService>(Lifetime.Singleton);
```

Quên dòng này thì `WorldSpawner` không resolve được và **cả container chết** — đọc dòng cuối cùng của
chuỗi `Failed to resolve` như `CLAUDE.md` đã dặn.

**`WorldSpawner`** — nhận `MapService`, nạp map trước khi dựng nhân vật, và đưa map cho motor:

```csharp
        private MapService _mapService;

        [Inject]
        public void Construct(WorldApi worldApi, WorldNetHandler worldNetHandler,
            LocalPlayer localPlayer, MapService mapService)
        {
            ...
            _mapService = mapService;
        }

        public void SpawnLocalPlayer(EnterWorldResponse response)
        {
            ...
            // Nạp map TRƯỚC khi Init motor: motor cần lưới va chạm ngay từ tick dự đoán đầu tiên.
            MapGrid map = _mapService.Load(response.MapId);

            var motor = _localPlayerObject.GetComponent<PlayerMotor>();
            motor.Init(_worldApi, _worldNetHandler, new Vector2(response.X, response.Y), response.ClassId, map);
        }
```

</details>

### ✅ CHECKPOINT B — hai bên in ra cùng một dấu vân tay

1. Vẽ lớp `Collision` cho map hiện có: mặt đất liền một dải, ít nhất **một hàng `OneWay`** làm bệ, và ít
   nhất **một khe cao đúng 1 ô** (trần thấp) để lát nữa thử ngồi-chui. Đặt một `Transform` làm điểm
   spawn và điền vào danh sách với id `default`.
2. `Tools/MMORPG/Export Map` → Console hiện dòng `Đã ghi Assets/Game/Resources/Maps/map1.json` kèm kích
   thước và checksum. Mở file bằng editor: **nhìn thấy bản đồ** trong mảng `cells`, hàng đầu là mép trên.
3. Thử ba lần cho tool sập đúng chỗ nó phải sập: dời `Grid` đi `0.5` rồi export → phải báo lỗi hệ toạ
   độ. Kéo một viên tile cỏ vào lớp `Collision` → phải báo tile lạ kèm toạ độ ô. Đổi id điểm spawn thành
   `"start"` → phải báo thiếu điểm `default`. Trả lại như cũ sau mỗi lần.
4. `dotnet build Server/GameServer` rồi chạy server: dòng log khởi động hiện tên map, kích thước,
   checksum.
5. Chạy client, đăng nhập: dòng `[MapService]` hiện **cùng một checksum**.

Hai con số ấy bằng nhau là bằng chứng của cả bước này. Không bằng nhau thì gần như chắc chắn là server
đang chạy với file cũ trong `bin/` — build lại `GameServer`, đừng đi tìm ở chỗ khác.

---

## Bước 3 — `Shared`: nhân vật có thân, và `Step` biết va chạm

### Hướng làm

**Nhân vật hết là một điểm.** Phase 8 ghi nợ chuyện này và giờ phải trả: một điểm thì lọt qua khe tường,
đứng cân bằng trên góc nhọn, và chui đầu qua trần. Thân nhân vật là một hộp, gốc toạ độ ở **chân** (khớp
pivot Bottom đã đặt ở Phase 8), chiếm vùng `[X - HW, X + HW] × [Y, Y + H]`.

Ba con số ấy đi vào **`CharacterProfile`**, không phải thành `const` trong `MovementRules`:

| Số | Dragon Warrior | Ghi chú |
|---|---|---|
| `BodyHalfWidth` | `0.35` | hẹp hơn nửa ô để lọt vừa khe rộng đúng 1 ô |
| `BodyHeight` | `1.6` | cao khi đứng |
| `BodyHeightCrouch` | `0.9` | thấp hơn 1 ô nên chui được vào khe cao đúng một ô |

Vì sao vào bảng chứ không thành hằng: đúng lý do đã chốt ở Phase 9 cho tốc độ chạy và thời lượng đòn —
**trong MMO thì mọi con số này khác nhau giữa các lớp nhân vật**. Và Phase 9 đã hứa trước điều này trong
mục "để dành": hộp va chạm theo tư thế cần `standHeight` / `crouchHeight` trong profile. Trả nợ ở đây thì
Phase 15 (né đạn bằng cách ngồi) không phải mở lại `Shared` lần nữa.

Đo bằng mắt: kéo một object có `SpriteRenderer` ô vuông 1×1 đứng cạnh nhân vật trong Scene, rồi chỉnh
tới khi hộp ôm vừa thân — đừng ôm cả tóc và cả vũ khí.

**Va chạm — tách trục, X trước Y sau.** Không phải mẹo, mà là **giải hai bài toán một chiều thay vì một
bài toán hai chiều**. Hai chiều cùng lúc thì phải trả lời "đâm vào góc thì coi là đụng tường hay đụng
sàn?" — câu hỏi không có đáp án đúng, vì hai lựa chọn đều sai trong một nửa số tình huống. Tách ra thì
câu hỏi ấy không tồn tại:

```
1. X += VelX·dt   →  quét cạnh đứng theo hướng đi   →  chạm thì X dán sát mép ô, VelX = 0
2. Y += VelY·dt   →  quét cạnh ngang theo hướng đi  →  chạm thì Y dán sát mép ô, VelY = 0
                                                        đi xuống mà chạm  →  Grounded = true
```

**Quét bao nhiêu điểm trên mỗi cạnh?** Thân cao 1.6 mà ô cao 1.0 → hai điểm ở hai đầu là **bỏ sót** ô ở
giữa. Ba điểm (chân, giữa, đầu) cho khoảng cách lớn nhất 0.75 < 1.0 → không ô nào lọt. Với cạnh ngang
thì hai điểm ở hai góc là đủ vì thân rộng 0.7 < 1.0.

> **Luật, không phải mẹo:** khoảng cách giữa hai điểm quét phải **nhỏ hơn cạnh ô**. Từ khi chiều cao
> thân là *dữ liệu trong profile*, luật này thành một ràng buộc lên dữ liệu: lớp nhân vật nào cao quá
> `2.0` là ba mức không còn đủ. Viết nó vào comment ngay chỗ quét.

**Chống tunneling: chỉ quét quãng đường ở trục có thể vượt một ô.** Nhìn con số của Dragon Warrior:

| Trục | Tốc độ tối đa | Quãng/tick | Vượt được 1 ô? |
|---|---|---|---|
| Ngang | `profile.MoveSpeed = 5` | 0.25 | không |
| Lên | `profile.JumpSpeed = 11` | 0.55 | không |
| **Xuống** | `MAX_FALL_SPEED = 20` | **1.00** | **có — sát kịch trần** |

Nên **chỉ chiều rơi** cần quét cả quãng đường (duyệt từng hàng ô đi qua); ba chiều còn lại kiểm điểm
cuối là đủ. Cái hay của phép so này là nó chỉ ra **chính xác chỗ nào** cần quét, thay vì quét hết cho
chắc. Và nó để lại một điều kiện phải nhớ: ngày nào có bệ nhún hay cú đẩy làm tốc độ ngang vượt 20 thì
trục ngang cũng phải quét.

**Bệ xuyên-một-chiều — ba điều kiện, thiếu cái nào cũng ra một bug kinh điển:**

1. đang **đi xuống** (`VelY <= 0`) — thiếu thì nhảy từ dưới lên bị cộc đầu;
2. chân **đã ở trên** mặt bệ trước khi dịch chuyển (`prevFeetY >= RowTop(cy)`) — thiếu thì đi ngang vào
   cạnh bệ là bị bắn lên mặt bệ;
3. **không** đang chủ động rơi xuyên (`DropThroughTicks == 0`).

Điều (3) là tính năng "ngồi + nhảy để tụt xuống bệ dưới", và nó cần thêm **một field nữa** vào
`MoveState`: `DropThroughTicks`, đặt bằng một hằng lúc bấm tổ hợp, giảm dần mỗi tick. Lại đúng bài học
cũ: Phase 8 trả hai `int` cho coyote time, Phase 9 trả năm field cho hoạt ảnh, giờ thêm một `int` cho
một thao tác mà người chơi thậm chí không biết tên. Không có gì miễn phí ở phía sau "server là source of
truth" — và nhớ rằng thêm field vào `MoveState` là **đổi giao thức**: DLL cũ bên Unity sẽ đọc ra những
con số vô nghĩa mà không báo lỗi (xem `<remarks>` của chính struct đó).

Tổ hợp ngồi + nhảy phải **chặn** cú nhảy bình thường, nếu không người chơi vừa tụt xuống vừa bật lên.
Cách gọn nhất: xử lý nó **trước** phép nhảy, cho nó tiêu thụ luôn `TicksSinceJumpRequest`, và đặt cả
`TicksSinceGrounded = EXPIRED` để coyote time không cho nhảy giữa không trung ngay tick sau.

**Không đứng dậy được dưới trần thấp.** Khi người chơi thả nút ngồi, phải hỏi map xem chỗ đó có đủ chiều
cao đứng không; không đủ thì **giữ nguyên tư thế ngồi**. Bỏ qua bước này thì thân nở ra bên trong trần và
tick sau bị đẩy ra chỗ khó đoán.

Đây là ý đáng dừng lại lâu nhất của phase: **một trạng thái có thể bị thế giới từ chối.** `Crouching`
không còn là "người chơi có bấm nút không" mà là "người chơi có bấm nút, *và* thế giới có cho phép
không". Mọi trạng thái liên quan tới thân thể sau này (nằm, biến hình, cưỡi thú) đều có dạng ấy.

**`Step` nhận thêm `MapGrid`**, và ba phép cuối viết lại:

```
… phép 0, 2, 3, 4a giữ nguyên như Phase 9 …
1'. Tư thế        muốn ngồi     → ngồi
                  muốn đứng dậy → chỉ đứng nếu CanStandUp(map, profile, state)
4b'. Rơi xuyên    Crouch && Jump && Grounded && ô dưới chân là OneWay
                  → DropThroughTicks = DROP_THROUGH_TICKS, tiêu thụ luôn cú nhảy
4c. Nhảy          (như Phase 9, nằm ở nhánh else)
5.  Xin hành động (như Phase 9)
6.  Tích phân X   → giải va chạm ngang
7.  Tích phân Y   → giải va chạm dọc (quét quãng, xử lý OneWay)
8.  Kẹp X vào [map.MinX + HW, map.MaxX − HW]   ← thay cho WORLD_HALF_EXTENT
```

`GROUND_Y` và `WORLD_HALF_EXTENT` **xoá hẳn**. Đó là hai hằng tạm mà Phase 8 và Phase 9 đã ghi rõ trong
comment là sẽ chết ở phase này — giữ lại một trong hai là có hai nguồn nói về cùng một thứ.

**Ba chỗ gọi `Step` phải truyền map**, và chỗ thứ ba là chỗ hay quên nhất:

| Chỗ gọi | File |
|---|---|
| Mô phỏng thật | `PlayerEntity.Integrate` |
| Dự đoán | `PlayerMotor.Step` |
| **Vòng replay của reconciliation** | `PlayerMotor.OnMoveStateResult` |

Quên chỗ thứ ba thì không có lỗi biên dịch nào (nếu bạn còn giữ một overload cũ) và triệu chứng là nhân
vật **rung** ở sát tường — xem "Ba thử nghiệm bắt buộc" ở dưới, thử nghiệm 1 dựng lại đúng cảnh đó.

**Và một việc dễ quên: người chơi cũ đang đứng ở đâu?** Vị trí trong DB được lưu từ những phase mà thế
giới còn là mặt phẳng vô hình — `(0, 0)` chẳng hạn, mà `(0, 0)` bây giờ có thể nằm trong lòng đất. Cần
một hàm đẩy điểm spawn lên chỗ đứng được gần nhất.

Đừng coi đây là việc dọn dẹp một lần. **Map là dữ liệu sửa được, còn vị trí người chơi thì đã lưu rồi:**
mỗi lần bạn vẽ thêm một bức tường là một lần có ai đó đang offline ở đúng chỗ ấy. Game thật nào cũng có
hàm này, và nó phải `Log.Warn` chứ không im lặng sửa — một người bị đẩy là chuyện thường, ba trăm người
bị đẩy nghĩa là bạn vừa export một map hỏng.

<details>
<summary><b>📖 Lời giải — <code>CharacterProfile</code> và <code>MoveState</code></b></summary>

**`Server/Shared/World/ActionDefine.cs`** — profile mang thêm hình dạng thân:

```csharp
        /// <summary>Nửa bề ngang thân, world unit. Hẹp hơn nửa ô để lọt vừa khe rộng đúng 1 ô.</summary>
        public float BodyHalfWidth { get; }

        /// <summary>Chiều cao thân khi đứng. Gốc toạ độ ở CHÂN nên thân chiếm [Y, Y + cao].</summary>
        public float BodyHeight { get; }

        /// <summary>Chiều cao khi ngồi — thấp hơn 1 ô nên chui được vào khe cao đúng một ô.</summary>
        public float BodyHeightCrouch { get; }

        public CharacterProfile(int classId, float moveSpeed, float jumpSpeed,
            float bodyHalfWidth, float bodyHeight, float bodyHeightCrouch,
            Dictionary<ActionState, ActionDefinition> actions)
        {
            ...
            BodyHalfWidth = bodyHalfWidth;
            BodyHeight = bodyHeight;
            BodyHeightCrouch = bodyHeightCrouch;
            ...
        }
```

và trong `CharacterProfiles.Build`:

```csharp
            var dragonWarrior = new CharacterProfile(
                DRAGON_WARRIOR,
                moveSpeed: 5f,
                jumpSpeed: 11f,

                // Ba con số này là hình dạng THÂN, đơn vị world (không phải giây như bảng hành động).
                // Ở đây chứ không thành const trong MovementRules vì cùng lý do với moveSpeed: hai lớp
                // nhân vật có thân khác nhau, và hộp va chạm của Phase 15 sẽ đọc đúng ba số này.
                bodyHalfWidth: 0.35f,
                bodyHeight: 1.6f,
                bodyHeightCrouch: 0.9f,
                new Dictionary<ActionState, ActionDefinition> { ... });
```

**`Server/Shared/World/MoveState.cs`** — thêm một field (và nhớ: đây là đổi giao thức):

```csharp
        /// <summary>
        /// Số tick còn được phép rơi xuyên bệ một chiều. Đặt khi bấm ngồi + nhảy, giảm dần mỗi tick.
        /// Phải nằm trong trạng thái chứ không phải một biến riêng ở server, vì client cũng mô phỏng
        /// bước này và vòng replay phải tái hiện được nó.
        /// </summary>
        public int DropThroughTicks;
```

`AtRest` thêm `DropThroughTicks = 0`.

</details>

<details>
<summary><b>📖 Lời giải — <code>MovementRules</code>: hằng số và các hàm va chạm</b></summary>

Xoá `GROUND_Y` và `WORLD_HALF_EXTENT`. Thêm:

```csharp
        /// <summary>Số tick bỏ qua va chạm với bệ một chiều sau khi bấm ngồi + nhảy.</summary>
        public const int DROP_THROUGH_TICKS = 6;

        /// <summary>
        /// Lùi vào trong một chút khi quét mép thân. Cần vì đứng trên sàn thì chân nằm ĐÚNG đường
        /// biên hai ô, mà Floor đưa đường biên về ô PHÍA TRÊN — tức ô trống. Quét đúng ở cao độ
        /// chân thì tick nào cũng kết luận "không có gì dưới chân" và Grounded nhấp nháy 20 lần/giây.
        /// </summary>
        private const float EDGE = 0.01f;
```

và các hàm va chạm:

```csharp
        private static float BodyHeight(CharacterProfile profile, bool crouching)
        {
            return crouching ? profile.BodyHeightCrouch : profile.BodyHeight;
        }

        private static bool IsSolid(MapGrid map, float x, float y)
        {
            return map.AtWorld(x, y) == CellType.Solid;
        }

        /// <summary>
        /// Thân (đặt tại x, y, cao height) có đè lên ô đặc nào không. Bệ một chiều KHÔNG tính: nó chỉ
        /// chặn theo chiều rơi, còn đứng lọt trong nó là chuyện bình thường.
        ///
        /// Quét 6 điểm = 2 mép ngang × 3 mức cao. Ba mức vì thân cao 1.6 mà ô cao 1.0: hai điểm ở hai
        /// đầu thì ô ở giữa lọt qua khe kiểm. LUẬT: khoảng cách giữa hai mức phải NHỎ HƠN cạnh ô —
        /// với 1.6 thì ba mức cách nhau 0.75, an toàn. Vì chiều cao thân giờ là DỮ LIỆU trong profile,
        /// luật này thành ràng buộc lên dữ liệu: lớp nhân vật nào cao quá 2.0 là phải thêm mức quét.
        /// </summary>
        private static bool OverlapsSolid(MapGrid map, CharacterProfile profile, float x, float y, float height)
        {
            float left = x - profile.BodyHalfWidth;
            float right = x + profile.BodyHalfWidth;

            float footY = y + EDGE;
            float midY = y + height * 0.5f;
            float headY = y + height - EDGE;

            return IsSolid(map, left, footY) || IsSolid(map, right, footY)
                || IsSolid(map, left, midY) || IsSolid(map, right, midY)
                || IsSolid(map, left, headY) || IsSolid(map, right, headY);
        }

        /// <summary>Có đủ chỗ trống để đứng thẳng dậy tại chỗ đang đứng không.</summary>
        public static bool CanStandUp(MapGrid map, CharacterProfile profile, in MoveState state)
        {
            return !OverlapsSolid(map, profile, state.X, state.Y, profile.BodyHeight);
        }

        /// <summary>Ô ngay dưới chân có phải bệ một chiều không — điều kiện để được chủ động tụt xuống.</summary>
        private static bool StandingOnOneWay(MapGrid map, CharacterProfile profile, in MoveState state)
        {
            float probeY = state.Y - EDGE;

            return map.AtWorld(state.X - profile.BodyHalfWidth, probeY) == CellType.OneWay
                || map.AtWorld(state.X + profile.BodyHalfWidth, probeY) == CellType.OneWay;
        }

        /// <summary>
        /// Dịch theo trục X rồi dán lại nếu đâm tường. Chỉ kiểm điểm cuối: 5 unit/giây là 0.25 unit
        /// mỗi tick, không cách nào vượt qua một ô rộng 1.0.
        /// </summary>
        private static MoveState ResolveHorizontal(MapGrid map, CharacterProfile profile, MoveState state, float dt)
        {
            state.X += state.VelX * dt;

            if (state.VelX == 0f)
                return state;

            float height = BodyHeight(profile, state.Crouching);
            float footY = state.Y + EDGE;
            float midY = state.Y + height * 0.5f;
            float headY = state.Y + height - EDGE;

            if (state.VelX > 0f)
            {
                float edgeX = state.X + profile.BodyHalfWidth;

                if (IsSolid(map, edgeX, footY) || IsSolid(map, edgeX, midY) || IsSolid(map, edgeX, headY))
                {
                    state.X = MapGrid.ColumnLeft(MapGrid.CellX(edgeX)) - profile.BodyHalfWidth;
                    state.VelX = 0f;
                }
            }
            else
            {
                float edgeX = state.X - profile.BodyHalfWidth;

                if (IsSolid(map, edgeX, footY) || IsSolid(map, edgeX, midY) || IsSolid(map, edgeX, headY))
                {
                    state.X = MapGrid.ColumnRight(MapGrid.CellX(edgeX)) + profile.BodyHalfWidth;
                    state.VelX = 0f;
                }
            }

            return state;
        }

        /// <summary>
        /// Dịch theo trục Y rồi dán lại nếu chạm trần hoặc chạm sàn.
        ///
        /// Chiều xuống là chiều DUY NHẤT phải quét cả quãng đường: rơi kịch trần là 20 unit/giây, tức
        /// đúng 1.00 unit mỗi tick — vừa đủ để lọt qua một tấm bệ dày 1 ô giữa hai lần kiểm. Chiều lên
        /// (0.55 unit/tick) và chiều ngang (0.25) thì kiểm điểm cuối là đủ.
        /// </summary>
        private static MoveState ResolveVertical(MapGrid map, CharacterProfile profile, MoveState state, float dt)
        {
            float prevFeetY = state.Y;
            state.Y += state.VelY * dt;

            float height = BodyHeight(profile, state.Crouching);

            if (state.VelY > 0f)
            {
                float headY = state.Y + height;

                // Bệ một chiều KHÔNG chặn chiều lên — đó là toàn bộ ý nghĩa của nó.
                if (IsSolid(map, state.X - profile.BodyHalfWidth, headY) ||
                    IsSolid(map, state.X + profile.BodyHalfWidth, headY))
                {
                    state.Y = MapGrid.RowBottom(MapGrid.CellY(headY)) - height;
                    state.VelY = 0f;
                }

                state.Grounded = false;
                return state;
            }

            // Quét từ hàng ô dưới chân lúc ĐẦU tick xuống tới hàng ô dưới chân lúc CUỐI tick.
            // Quét ở mức "dưới chân một chút" (xem comment của EDGE), không đúng bằng chân.
            int fromRow = MapGrid.CellY(prevFeetY - EDGE);
            int toRow = MapGrid.CellY(state.Y - EDGE);

            for (int row = fromRow; row >= toRow; row--)
            {
                if (!BlocksFall(map, profile, state, row, prevFeetY))
                    continue;

                state.Y = MapGrid.RowTop(row);
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
        /// Ô đặc thì luôn chặn. Bệ một chiều chỉ chặn khi ĐỦ CẢ HAI: chân đã ở trên mặt bệ từ đầu tick
        /// (thiếu điều kiện này thì đi ngang vào cạnh bệ là bị bắn lên mặt bệ), và người chơi không
        /// đang chủ động tụt xuống.
        /// </summary>
        private static bool BlocksFall(MapGrid map, CharacterProfile profile, in MoveState state, int row, float prevFeetY)
        {
            int leftCell = MapGrid.CellX(state.X - profile.BodyHalfWidth);
            int rightCell = MapGrid.CellX(state.X + profile.BodyHalfWidth);

            for (int cx = leftCell; cx <= rightCell; cx++)
            {
                CellType cell = map.At(cx, row);

                if (cell == CellType.Solid)
                    return true;

                if (cell == CellType.OneWay &&
                    state.DropThroughTicks <= 0 &&
                    prevFeetY >= MapGrid.RowTop(row))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Kẹp vào biên ngang của map. Biên là DỮ LIỆU đọc từ file, không còn là hằng số.</summary>
        public static float ClampX(MapGrid map, CharacterProfile profile, float x)
        {
            return Math.Clamp(x, map.MinX + profile.BodyHalfWidth, map.MaxX - profile.BodyHalfWidth);
        }

        /// <summary>
        /// Đẩy một điểm spawn lên chỗ đứng được gần nhất. Cần vì vị trí người chơi đã LƯU trong DB còn
        /// hình dạng map thì sửa được bất cứ lúc nào: mỗi lần bạn vẽ thêm một bức tường là một lần có
        /// ai đó đang offline ở đúng chỗ ấy.
        /// </summary>
        public static float ResolveSpawnY(MapGrid map, CharacterProfile profile, float x, float y)
        {
            // Trần lặp = chiều cao map: tường dày mấy hàng cũng thoát ra được, mà không có đường nào
            // để vòng lặp này chạy mãi nếu một ngày nào đó At() đổi cách trả lời.
            for (int guard = 0; guard < map.Height + 1; guard++)
            {
                if (!OverlapsSolid(map, profile, x, y, profile.BodyHeight))
                    return y;

                // Nhảy lên mặt trên của hàng ô đang kẹt rồi thử lại.
                y = MapGrid.RowTop(MapGrid.CellY(y));
            }

            return y;
        }
```

</details>

<details>
<summary><b>📖 Lời giải — <code>Step</code> với chữ ký mới</b></summary>

```csharp
        public static MoveState Step(MoveState state, MoveIntent intent, float dt,
            CharacterProfile profile, MapGrid map)
        {
            // 0. Nhịp của tầng action, thêm bộ đếm rơi xuyên.
            if (state.ActionTicksLeft > 0)
                state.ActionTicksLeft--;

            if (state.TicksSinceAttack < EXPIRED)
                state.TicksSinceAttack++;

            if (state.DropThroughTicks > 0)
                state.DropThroughTicks--;

            if (state.ActionTicksLeft <= 0 && state.Action != ActionState.Die)
                state.Action = ActionState.None;

            bool locked = profile.GetAction(state.Action).LocksMovement;

            // 1. Tư thế. Muốn ngồi thì ngồi được ngay; muốn ĐỨNG DẬY thì còn phải hỏi thế giới —
            //    trần thấp thì không đứng lên được, và giữ nguyên tư thế ngồi là câu trả lời đúng.
            //    Bỏ phép hỏi này thì thân nở ra bên trong trần và tick sau bị đẩy đi đâu không biết.
            bool wantCrouch = intent.Crouch && state.Grounded && !locked;

            if (wantCrouch)
            {
                state.Crouching = true;
            }
            else if (state.Crouching)
            {
                state.Crouching = !CanStandUp(map, profile, state);
            }

            // 2. Vận tốc ngang + hướng mặt (như Phase 9).
            if (locked || state.Crouching)
            {
                state.VelX = 0f;
            }
            else
            {
                state.VelX = intent.DirX * profile.MoveSpeed;
            }

            if (state.VelX != 0f && state.Action == ActionState.None)
                state.FacingLeft = state.VelX < 0f;

            // 3. Trọng lực — luật của thế giới, không theo nhân vật.
            state.VelY -= GRAVITY * dt;
            if (state.VelY < -MAX_FALL_SPEED)
                state.VelY = -MAX_FALL_SPEED;

            // 4a. Hai bộ đếm tha thứ (như Phase 9).
            if (state.TicksSinceGrounded < EXPIRED)
                state.TicksSinceGrounded++;

            if (intent.Jump)
                state.TicksSinceJumpRequest = 0;
            else if (state.TicksSinceJumpRequest < EXPIRED)
                state.TicksSinceJumpRequest++;

            // 4b. Rơi xuyên bệ — xử lý TRƯỚC cú nhảy và tiêu thụ luôn yêu cầu nhảy, nếu không thì
            //     người chơi vừa tụt xuống vừa bật lên trong cùng một tick. Đặt cả TicksSinceGrounded
            //     về EXPIRED để coyote time không cho một cú nhảy giữa không trung ngay tick sau.
            if (!locked && intent.Crouch && intent.Jump && state.Grounded &&
                StandingOnOneWay(map, profile, state))
            {
                state.DropThroughTicks = DROP_THROUGH_TICKS;
                state.TicksSinceJumpRequest = EXPIRED;
                state.TicksSinceGrounded = EXPIRED;
                state.Grounded = false;
            }
            // 4c. Nhảy (như Phase 9).
            else if (!locked &&
                     state.TicksSinceJumpRequest <= JUMP_BUFFER_TICKS &&
                     state.TicksSinceGrounded <= COYOTE_TICKS)
            {
                state.VelY = profile.JumpSpeed;
                state.TicksSinceJumpRequest = EXPIRED;
                state.TicksSinceGrounded = EXPIRED;
            }

            // 5. Xin hành động (như Phase 9).
            ActionDefinition attack = profile.GetAction(ActionState.Attack);

            if (intent.Action == ActionRequest.Attack &&
                state.TicksSinceAttack >= attack.CooldownTicks &&
                CharacterStates.CanEnter(state.Action, state.ActionTicksLeft, ActionState.Attack))
            {
                state.Action = ActionState.Attack;
                state.ActionTicksLeft = attack.DurationTicks;
                state.TicksSinceAttack = 0;
            }

            // 6 & 7. Tích phân có va chạm. Thứ tự X trước Y là một phần của contract.
            state = ResolveHorizontal(map, profile, state, dt);
            state = ResolveVertical(map, profile, state, dt);

            // 8. Biên ngang — giờ là hai mép map đọc từ file, không còn WORLD_HALF_EXTENT.
            state.X = ClampX(map, profile, state.X);

            return state;
        }
```

</details>

<details>
<summary><b>📖 Lời giải — ba chỗ gọi <code>Step</code></b></summary>

**Server — `PlayerEntity`:** nhận map lúc dựng (nó cần map ngay để gỡ điểm spawn khỏi tường):

```csharp
        private readonly CharacterProfile _profile;
        private readonly MapGrid _map;

        public PlayerEntity(int entityId, CharacterRow row, ClientSession owner, MapGrid map)
        {
            ...
            _profile = CharacterProfiles.Get(row.ClassId);
            _map = map;

            // Vị trí trong DB có từ thời thế giới còn là mặt phẳng vô hình, và map thì sửa được bất
            // cứ lúc nào. Gỡ ra trước khi entity tồn tại — chứ không phải để tick đầu tiên tự xoay xở
            // với một cái thân đang nằm trong đá.
            float spawnX = MovementRules.ClampX(map, _profile, row.X);
            float spawnY = MovementRules.ResolveSpawnY(map, _profile, spawnX, row.Y);

            if (spawnY != row.Y || spawnX != row.X)
            {
                // LA LỚN chứ không im lặng sửa: một người bị đẩy là chuyện thường, ba trăm người bị
                // đẩy nghĩa là vừa có ai đó export một map hỏng.
                Log.Warn($"{row.Name} spawn kẹt tại ({row.X:0.##}, {row.Y:0.##}) — đẩy về ({spawnX:0.##}, {spawnY:0.##})");
            }

            State = MoveState.AtRest(spawnX, spawnY);
        }

        public void Integrate(float dt)
        {
            ...
            State = MovementRules.Step(State, intent, dt, _profile, _map);
        }
```

`WorldService.Spawn` đổi đúng một dòng: `new PlayerEntity(entityId, row, owner, Map)`.

**Client — `PlayerMotor`:** giữ map, truyền vào **cả hai** chỗ gọi:

```csharp
        private MapGrid _map;

        public void Init(WorldApi worldApi, WorldNetHandler worldNetHandler, Vector2 spawnPos, int classId, MapGrid map)
        {
            ...
            _map = map;
        }

        private void Step(float dirX, bool crouch)
        {
            ...
            _simState = MovementRules.Step(_simState, intent, MovementRules.TICK_DT, _profile, _map);
            ...
        }

        private void OnMoveStateResult(MoveStateResponse response)
        {
            ...
            foreach (PendingInput pending in _pending)
            {
                previous = state;

                // Vòng replay PHẢI dùng đúng map của bước dự đoán. Đây là chỗ dễ quên nhất trong cả
                // phase, và triệu chứng của việc quên không phải "sai vị trí" mà là RUNG ở sát tường:
                // dự đoán chặn, replay cho qua, mỗi gói MoveState là một lần đổi ý.
                state = MovementRules.Step(state, pending.Intent, MovementRules.TICK_DT, _profile, _map);
            }
            ...
        }
```

</details>

### ✅ CHECKPOINT C — mục tiêu cuối Phase 10

Bảy phép thử, làm đủ:

1. Vào world: nhân vật rơi xuống và **dừng trên mặt đất**, đứng yên mãi. Rơi xuyên xuống mãi → phép lật
   trục Y trong `Parse` hoặc `BlocksFall` chưa bao giờ trả `true`.
2. Chạy sang trái tới hết map: dừng **sát mép**, không rung, không rubber-band. Không rubber-band là bằng
   chứng client và server đang chạy đúng một `Step` trên đúng một map.
3. Nhảy lên đụng trần: dừng, rơi xuống, **không dính vào trần**.
4. Nhảy từ dưới lên xuyên qua một bệ `OneWay`: lọt qua, rồi **đứng được ở trên**.
5. Đứng trên bệ, bấm ngồi + nhảy: **tụt xuống**, và không bật lên cùng lúc.
6. Đi vào hành lang cao 1 ô: đứng thì không vào được, ngồi thì chui vào được. Đang ở trong đó mà thả nút
   ngồi → **vẫn ngồi**. Ra khỏi hành lang mới đứng dậy được.
7. `Grounded` không nhấp nháy lúc đứng yên — thêm log tạm mà xem. Nhấp nháy nghĩa là đang quét ở đúng cao
   độ chân thay vì thấp hơn `EDGE`.

Bước (5) và (6) là hai thứ mà một cái điểm không có thân thể **không làm được**. Đó là lý do phase này
phải cho nhân vật một cái hộp.

---

## Ba thử nghiệm bắt buộc

**1. Hack xuyên tường, kiểu thông minh.**
Sửa tạm `PlayerMotor` để **vòng replay** trong `OnMoveStateResult` truyền một map rỗng (parse một file
toàn dấu chấm) trong khi bước dự đoán vẫn dùng map thật. Chạy vào tường.

Bạn sẽ thấy một thứ tinh vi hơn "bị kéo lại": nhân vật **rung** ở sát tường — dự đoán chặn nó, replay
cho nó qua, mỗi gói `MoveState` là một lần đổi ý. Đây là hình ảnh của **hai bản luật lệch nhau bên trong
cùng một client**, và nó dạy vì sao "chỉ có một `Step`" phải hiểu là *một* — kể cả hai chỗ gọi trong
cùng một file. Trả code về như cũ.

**2. Vẽ hỏng một ô, và đếm xem mất bao lâu để thấy.**
Xoá một ô `Solid` giữa mặt đất trong lớp `Collision` (đừng đụng lớp `Ground`), export, chạy lại. Mở
`map1.json` ra và **nhìn thấy đúng cái lỗ ấy** trong mảng `cells` — đó là món quà của việc để lưới ở
dạng mảng chuỗi thay vì mảng số. Trong game thì bạn có một cái hố vô hình: hình vẫn là đất liền, luật
thì thủng. Đi vào đó.

Rồi bật lại renderer của lớp `Collision` và nhìn Scene view: chỗ thủng hiện ra ngay. Đó là toàn bộ lý do
lớp luật được vẽ **cạnh** lớp hình trong cùng một Scene thay vì gõ thành chữ ở project khác. Trả lại như cũ.

**3. Đổi map mà quên build server.**
Vẽ thêm một bức tường, export, chạy **client mới với server cũ** (đừng build lại `GameServer`). Chạy vào
bức tường mới: client dự đoán dừng lại, server nói đi tiếp, và bạn bị đẩy xuyên qua tường của chính mình.

So hai dòng checksum trong console: chúng khác nhau. Ghi nhớ triệu chứng ấy — *bị đẩy đi ngược ý mình ở
một chỗ cụ thể trên map* — vì nó sẽ còn quay lại, và Phase 12 sẽ giết nó bằng phép kiểm version lúc login
thay vì để bạn tự nhìn hai dòng log.

---

## Troubleshooting

| Triệu chứng | Nguyên nhân thường gặp | Chỗ sửa |
|---|---|---|
| Map lộn ngược | `Parse` hoặc `Write` lật trục Y một bên mà không lật bên kia | `MapFile` — và bài round-trip ở CHECKPOINT A phải đỏ, nếu nó xanh thì lưới mẫu đang đối xứng |
| Map lệch nửa ô / lệch hẳn một đoạn | `Grid` hoặc `Collision` không ở `(0,0,0)`, hoặc `cellSize ≠ 1` | phép kiểm `CellToWorld` trong `MapExporter` — nếu nó không kêu thì bạn chưa viết nó |
| Map rộng hơn hình, có hàng chục cột rỗng | quên `CompressBounds()` — `cellBounds` giữ cả vùng từng vẽ rồi xoá | `MapExporter` |
| `FormatException: Ký tự lạ` lúc khởi động | ai đó sửa tay mảng `cells` | export lại, và đọc trường `_comment` ở đầu file |
| `JsonReaderException` lúc khởi động | file JSON hỏng cú pháp — thường do sửa tay rồi thiếu dấu phẩy, hoặc file bị cắt cụt | export lại; đừng vá tay |
| Unity: `Multiple precompiled assemblies with the same name: Newtonsoft.Json` | đã cài `Newtonsoft.Json` qua NuGetForUnity trong khi UPM cũng cung cấp nó | gỡ khỏi `Assets/packages.config`, giữ bản UPM |
| Server chạy được, Unity báo `Could not load file or assembly Newtonsoft.Json` | `com.unity.nuget.newtonsoft-json` bị gỡ khỏi `Packages/manifest.json` | thêm lại — `Shared.dll` phụ thuộc nó |
| `Map phải có ít nhất một điểm trong "spawns"` | tool export chạy trước khi bạn điền danh sách Spawns | `MapCollisionSource` trong Inspector |
| Rơi xuyên sàn xuống đáy map | lớp `Collision` chưa vẽ ở chỗ đó — cầu chì đáy lưới đang làm việc | bật renderer lớp `Collision` mà nhìn |
| `Grounded` nhấp nháy true/false lúc đứng yên | quét va chạm ở đúng cao độ chân thay vì thấp hơn `EDGE` | `ResolveVertical` |
| Rơi từ trên cao thì lọt qua bệ mỏng | chiều rơi kiểm điểm cuối thay vì quét cả quãng | `ResolveVertical` — vòng `for` theo hàng |
| Nhảy từ dưới lên bị cộc đầu vào bệ | nhánh `VelY > 0` đang chặn cả `OneWay` | `ResolveVertical` |
| Đi ngang vào cạnh bệ thì bị bắn lên mặt bệ | thiếu điều kiện `prevFeetY >= RowTop(row)` | `BlocksFall` |
| Ngồi + nhảy thì vừa tụt xuống vừa bật lên | nhánh rơi xuyên không tiêu thụ `TicksSinceJumpRequest`, hoặc đặt sau phép nhảy | `Step` phép 4b |
| Tụt xuống rồi lập tức đứng lại trên chính bệ đó | `DROP_THROUGH_TICKS` quá nhỏ so với thời gian rơi hết bề dày bệ | `MovementRules` |
| Kẹt cứng trong trần sau khi đứng dậy | thiếu phép hỏi `CanStandUp` khi thả nút ngồi | `Step` phép 1 |
| Nhân vật lọt qua khe hẹp hơn thân | quét ngang thiếu mức giữa (2 điểm thay vì 3) | `ResolveHorizontal` |
| Rung ở sát tường | hai chỗ gọi `Step` bên client không cùng một map — hay gặp nhất là **vòng replay** | `PlayerMotor.OnMoveStateResult` |
| Bị đẩy xuyên qua tường mình đang thấy | client và server đang chạy hai file map khác nhau | so hai dòng checksum; build lại `GameServer` |
| Spawn kẹt trong tường | vị trí cũ trong DB nằm trong lòng đất | `ResolveSpawnY` — và đọc dòng `Log.Warn` nó in ra |
| `Không thấy Resources/Maps/map1.json` | chưa export, hoặc export ra ngoài thư mục `Resources` | `MapExporter.OUTPUT_FOLDER` |
| Nhân vật đứng im hoàn toàn, không lỗi gì | Unity còn dùng DLL cũ — build `Shared` chưa copy sang `Assets/Plugins/Shared/`. **Lần thứ ba dòng này xuất hiện** — Phase 12 giết hẳn nó bằng phép kiểm vân tay contract | build lại `Server/Shared`, xem post-build target |

---

## Tự kiểm tra hiểu bài

Tự trả lời từng câu xong mới mở đáp án của câu đó.

**Câu 1.** Contract code chảy từ `Shared` sang Unity, còn map thì chảy từ Unity sang server — hai chiều
ngược nhau. Điều gì **giống nhau** ở hai chiều đó, và vì sao điều ấy mới là cái quan trọng?
<details>
<summary><b>📖 Đáp án câu 1</b></summary>

Giống nhau ở chỗ: **cả hai chiều đều do build lo, không do tay người**. `Shared.csproj` có target copy
DLL; `GameServer.csproj` có target copy file map. Không ai phải nhớ bước nào cả.

Chiều đi chỉ là hệ quả của "nguồn nằm ở đâu": contract sinh ra nơi gõ code, map sinh ra nơi vẽ hình. Cái
quan trọng là **một nguồn + một đường tự động**. Bỏ chữ "tự động" đi thì hai bên vẫn khớp — cho tới lần
đầu tiên có người quên, và lần đó không có lỗi biên dịch nào báo, chỉ có "server nói có tường ở chỗ
client thấy trống".

Đây cũng là câu trả lời cho vì sao **không** để tool export ghi thẳng vào thư mục của server: một nguồn,
một đường, một chỗ để nhìn khi nghi ngờ.

</details>

**Câu 2.** Hình (lớp `Ground`) và luật (lớp `Collision`) vẫn là hai lớp vẽ tay. Vậy ta được gì so với
bản cũ — gõ hình dạng map thành mảng chuỗi trong `Shared`?
<details>
<summary><b>📖 Đáp án câu 2</b></summary>

Bản cũ có **ba** bản mô tả của cùng một map: tilemap để nhìn, mảng chuỗi trong `Shared`, và trong đầu
người vẽ. Bản này có **hai**, và cả hai nằm chồng lên nhau trong cùng một Scene view, cùng cỡ ô, bật
tắt bằng một cái checkbox. Lệch nhau là **nhìn thấy được**, không cần công cụ — đó là lý do phase này
không cần cái gizmo đối chiếu mà bản cũ phải viết.

Còn loại lệch nguy hiểm hơn — client với server — thì cả hai bản đều đã dọn sạch, nhưng bằng hai cách
khác nhau: bản cũ bằng "cùng đọc một DLL", bản này bằng "cùng đọc một file". Và bản này còn thêm một
phép tự kiểm rẻ tiền mà bản cũ không có chỗ để gắn vào: **checksum in ở cả hai bên**.

Ý chung: không phải lúc nào cũng gộp được về một nguồn. Khi không gộp được thì việc cần làm là **giảm số
bản, và làm cho phần còn lệch được phát hiện bằng cơ chế nào đó** — mắt nhìn cũng là một cơ chế, "cẩn
thận" thì không.

</details>

**Câu 3.** File map đọc bằng `MissingMemberHandling.Ignore` (trường lạ thì bỏ qua), còn file config ở
Phase 12 sẽ dùng `Error` (trường lạ thì ném). Cùng một thư viện, cùng một dự án — vì sao hai lựa chọn
khác nhau?
<details>
<summary><b>📖 Đáp án câu 3</b></summary>

Vì **ai viết ra file** khác nhau, nên **kiểu sai** của hai file cũng khác nhau.

File map do **tool sinh**: nó không bao giờ gõ nhầm `spwan`, nên `Error` chẳng bắt được gì. Đổi lại,
`Ignore` mua được thứ có giá trị thật: code hôm nay đọc được file mà bản mai này thêm trường
(`portals`, `monsters`…) — chính là lý do chọn JSON ngay từ đầu.

File config do **người gõ tay**: ở đó lỗi chính tả là kiểu sai phổ biến nhất, và `Ignore` sẽ nuốt nó —
`gravty: 30` bị bỏ qua, `Gravity` về mặc định, và bạn đi tìm bug trong `MovementRules`.

Gọn lại: **khoan dung với máy, nghiêm khắc với người.** Không phải sở thích — nó suy ra từ việc mỗi bên
sai theo kiểu gì. Và để ý là cả hai lựa chọn đều phục vụ đúng một mục tiêu: *làm cho lỗi nổ ra sớm nhất
có thể ở chỗ dễ sửa nhất*.

</details>

**Câu 4.** Định dạng cũ có trường `size 64 11`; bản JSON bỏ hẳn nó đi và suy kích thước từ mảng `cells`.
Được gì?
<details>
<summary><b>📖 Đáp án câu 4</b></summary>

Được **một cách để file tự mâu thuẫn với chính nó biến mất**. Với `size` thì file có thể ghi `64` trong
khi mảng chỉ có 63 hàng — và lúc đó không có câu trả lời đúng cho "tin ai": tin `size` thì đọc tràn, tin
mảng thì tại sao lại ghi `size` ra làm gì.

Parser text cũ *buộc* phải có `size` vì nó đọc từng dòng và cần biết khi nào dừng. Mảng JSON thì tự biết
độ dài mình, nên trường ấy trở thành **dữ liệu thừa** — và dữ liệu thừa luôn là dữ liệu có thể sai.

Bài học chung: mỗi sự thật nên có đúng một chỗ ghi. Đây là chính cái nguyên tắc "contract một nguồn" của
cả dự án, thu nhỏ lại còn bằng một trường trong một file.

</details>

**Câu 5.** `MapGrid.At` trả về `Empty` khi ra ngoài lưới ở ba phía nhưng `Solid` khi xuống dưới đáy. Vì
sao không cho cả bốn phía giống nhau?
<details>
<summary><b>📖 Đáp án câu 5</b></summary>

Vì bốn phía đang trả lời bốn câu hỏi khác nhau:

- **Hai mép trái/phải**: biên ngang đã có người lo — `Step` kẹp `X` vào `[MinX + HW, MaxX − HW]`. Cho
  ngoài rìa là `Solid` nữa là hai cơ chế cùng nói về một thứ, và tới ngày chúng lệch nhau (ai đó sửa một
  bên) thì không ai biết bên nào đúng.
- **Trên đỉnh**: `Empty`, vì trần vô hình ngay trên đầu là thứ người chơi cảm nhận được ngay và sẽ báo
  là bug.
- **Dưới đáy**: `Solid`, và đây **không phải thiết kế game mà là cầu chì**. Vẽ thiếu một ô sàn là chuyện
  sẽ xảy ra; hệ quả tệ nhất được phép có là "rơi xuống đáy map rồi đứng đó", không phải `Y` trôi về vô
  cực rồi thành `NaN` và đi thẳng vào DB.

Bài học rộng hơn: một giá trị mặc định là một **câu trả lời**, nên nó phải có lý do riêng cho từng câu
hỏi. Chọn một giá trị cho cả bốn phía vì "cho đồng nhất" là chọn mà không trả lời.

</details>

**Câu 6.** Bệ xuyên-một-chiều có ba điều kiện chặn. Bỏ từng cái ra thì gặp bug gì?
<details>
<summary><b>📖 Đáp án câu 6</b></summary>

- Bỏ **"đang đi xuống"** (`VelY <= 0`): nhảy từ dưới lên bị cộc đầu vào bệ — mất đúng tính năng cốt lõi.
- Bỏ **"chân đã ở trên mặt bệ từ đầu tick"**: đi ngang vào cạnh bệ là bị **bắn lên** mặt bệ, vì trong
  tick đó chân vừa tụt xuống dưới mép trên và phép kiểm coi đó là hạ cánh.
- Bỏ **`DropThroughTicks == 0`**: mất tính năng chủ động tụt xuống — bấm ngồi + nhảy thì hạ xuống rồi bị
  chính bệ đó bắt lại ngay tick sau.

Điểm chung đáng nhớ: cả ba điều kiện đều **không** hỏi "nhân vật đang ở đâu" mà hỏi "nó đang làm gì và
vừa ở đâu". Va chạm ở đây là hàm của *trạng thái*, không phải của *vị trí* — đó là lý do vận tốc phải
nằm trong `MoveState` từ Phase 8.

</details>

**Câu 7.** Vì sao chỉ chiều rơi cần quét cả quãng đường, còn ba chiều kia kiểm điểm cuối là đủ?
<details>
<summary><b>📖 Đáp án câu 7</b></summary>

Vì tunneling chỉ xảy ra khi quãng đi trong một tick **vượt được cạnh một ô**. Nhìn số: ngang 5 unit/s =
0.25 unit/tick; lên 11 unit/s = 0.55; xuống kịch trần 20 unit/s = **1.00** — đúng bằng cạnh ô, vừa đủ
để lọt qua một tấm bệ dày 1 ô giữa hai lần kiểm.

Cái hay là phép so sánh này cho biết **chính xác chỗ nào** cần quét thay vì quét hết cho chắc. Và nó để
lại một điều kiện phải nhớ: hôm nào có bệ nhún hay cú đẩy làm tốc độ ngang vượt 20 unit/s thì trục ngang
cũng phải quét. Con số là một phần của lập luận, không phải một hằng số vô danh.

Lưu ý thêm: từ phase này `MoveSpeed` và `JumpSpeed` là **dữ liệu trong profile**, nên "0.25" và "0.55" ở
trên là con số của *Dragon Warrior*, không phải của mọi lớp nhân vật. Thêm một lớp chạy 25 unit/s là
phải quay lại đọc lại đúng bảng này.

</details>

**Câu 8.** Vì sao phép quét va chạm dọc lấy mốc "dưới chân một chút" (`Y - EDGE`) chứ không đúng bằng
`Y`? Mô tả bug cụ thể nếu lấy đúng `Y`.
<details>
<summary><b>📖 Đáp án câu 8</b></summary>

Vì đứng trên sàn thì chân nằm **đúng đường biên** giữa hai hàng ô, và `Floor` đưa đường biên về ô **phía
trên** — tức ô trống. Kiểm ở đó thì tick nào đứng yên cũng kết luận "không có gì dưới chân" →
`Grounded = false` → tick sau trọng lực kéo xuống một chút → lúc này ô dưới chân là ô đặc →
`Grounded = true` và dán về mặt sàn → tick sau lại `false`…

Kết quả là `Grounded` nhấp nháy 20 lần mỗi giây. Mà `Grounded` là điều kiện của nhảy, của ngồi, và của
`LocomotionState` — nên hoạt ảnh giật giữa `idle` và `fall`, và cú nhảy thì lúc ăn lúc không. Một epsilon
đặt đúng chỗ dọn sạch cả chuỗi hệ quả đó.

</details>

**Câu 9.** `Crouching` từ Phase 9 là "người chơi có bấm nút không". Phase này nó thành gì, và vì sao đó
là một thay đổi lớn hơn vẻ ngoài?
<details>
<summary><b>📖 Đáp án câu 9</b></summary>

Nó thành "người chơi có bấm nút, **và** thế giới có cho phép không". Thả nút ngồi dưới trần thấp thì
trạng thái **không đổi** — ý định bị từ chối.

Lớn hơn vẻ ngoài vì đây là lần đầu tiên trong dự án một trạng thái của thân thể **phụ thuộc vào hình
dạng thế giới**, chứ không chỉ phụ thuộc input và luật nội tại. Hệ quả kéo theo: `Step` không còn tính
được nếu thiếu map — đó là lý do `MapGrid` phải là tham số của `Step` chứ không phải một biến toàn cục ở
đâu đó, và cũng là lý do vòng replay của client bắt buộc phải truyền đúng map ấy.

Mọi trạng thái về thân thể sau này (nằm, biến hình, cưỡi thú) đều có đúng dạng này. Và nhân tiện: nó
cũng là lý do `Crouching` phải là sự thật vật lý trong `MoveState` từ Phase 9 chứ không phải một `bool`
riêng ở client "chỉ để đổi sprite thôi mà".

</details>

**Câu 10.** `ResolveSpawnY` đẩy người chơi lên khi vị trí lưu trong DB nằm trong tường. Vì sao đây không
phải một đoạn code dọn dẹp dùng một lần rồi xoá?
<details>
<summary><b>📖 Đáp án câu 10</b></summary>

Vì nó không sửa một sự cố quá khứ mà xử lý một **mâu thuẫn thường trực**: hình dạng map là dữ liệu **sửa
được bất cứ lúc nào**, còn vị trí người chơi thì **đã lưu rồi**. Mỗi lần bạn vẽ thêm một bức tường là
một lần có ai đó đang offline ở đúng chỗ ấy, và họ sẽ đăng nhập vào bên trong đá.

Ở dự án học thì "ai đó" là chính bạn ở lần chạy trước, nên nó xảy ra **thường xuyên hơn** ở game thật
chứ không phải ít hơn.

Đó cũng là lý do nó phải `Log.Warn` chứ không im lặng sửa: một người bị đẩy là chuyện thường, ba trăm
người bị đẩy nghĩa là bạn vừa export một map hỏng và cần biết ngay.

</details>

---

## Để dành (ghi lại, chưa làm)

- **Hố và chết do rơi.** Đáy lưới đang là `Solid` — cầu chì, không phải thiết kế. Có hố thật thì cần một
  mốc `y` mà rơi quá là chết, và "chết" thì đã có sẵn `ActionState.Die` từ Phase 9, chỉ thiếu người gọi.
- **Nhiều map và cổng chuyển map.** Đây là chỗ định dạng JSON trả tiền lần đầu, nên đáng nói cho hết.
  Thêm cổng vào file là thêm **một trường**, và **không** tăng `FORMAT_VERSION`:

  ```json
  "portals": [
    { "x": 46.0, "y": 0.0, "width": 1.0, "height": 2.0, "toMapId": 2, "toSpawnId": "east_gate" }
  ]
  ```

  ```csharp
  // MapDefinition.cs — thêm đúng một property
  [JsonProperty("portals")]
  public List<PortalDefinition>? Portals { get; set; }
  ```

  File map **cũ** (chưa có cổng) vẫn đọc được: trường thiếu → `null` → coi như không có cổng nào. Code
  **cũ** vẫn đọc được file mới: trường lạ → bỏ qua (`MissingMemberHandling.Ignore`, và bài test thứ tư
  ở CHECKPOINT A canh đúng tính chất này). Không có bước migrate nào, không phải export lại map cũ.

  Phần còn lại của việc chuyển map thì không rẻ như vậy: một `Dictionary<int, MapGrid>` thay cho
  `WorldService.Map`, một `NetCmd` chuyển map, và phần khó nhất là **client phải nạp map mới trước khi
  bước chân qua cổng**, nếu không nó dự đoán bằng lưới của map cũ.

  Nhắc lại vì sao Phase 10 **không** làm sẵn phần schema này: một mảng dữ liệu không ai đọc là thứ tệ
  hơn cả không có gì — đúng câu đã viết ở Bước 0 của Phase 9 về `MapGrid.cs` nằm không.
- **Dốc (slope).** Đắt hơn vẻ ngoài nhiều: `Grounded` không còn là "ô dưới chân đặc" mà là một phép
  chiếu, và tốc độ chạy phải chiếu theo mặt dốc. Đụng vào cả `Derive` của Phase 9.
- **Bệ di động.** Khó nhất trong danh sách: bệ là một entity **chuyển động** tham gia va chạm, nên nó
  phải nằm trong `Step` — tức là vị trí của bệ phải tới được client **trước khi** client dự đoán. Là bài
  học "thứ gì tham gia va chạm thì thứ đó thuộc về contract".
- **Thang, dây leo.** Một `CellType` nữa và một `LocomotionState` nữa — đúng chỗ để kiểm tra xem hai tầng
  trạng thái của Phase 9 có chia đúng không.
- **Nén file map.** Map hiện ~1.5KB (JSON tốn hơn text thô một chút — cái giá của cấu trúc, và rẻ).
  Map to hơn hoặc nhiều map thì đây là payload lớn đầu tiên vượt 4KB — dịp để đường nén LZ4 của Phase 2
  chạy thật, nếu tới lúc đó map được **gửi qua mạng** thay vì đi kèm client. JSON nén rất tốt vì nó lặp
  lại nhiều: đó là lý do "JSON tốn dung lượng" gần như không bao giờ là lý do thật để bỏ JSON.
- **Kiểm map giữa client và server bằng máy.** Hôm nay là hai dòng checksum và một đôi mắt. Phase 12 biến
  nó thành phép kiểm lúc login: lệch thì **chặn vào world**, kèm thông điệp bảo người chơi tải bản mới.
- **Sinh lớp `Collision` từ lớp `Ground`.** Cám dỗ lớn, và câu trả lời là *không* — trừ khi bạn chấp nhận
  cho hình quyết định luật. Xem lại bảng ở đầu phase: viên cỏ nào cũng chặn thì hàng rào và bụi cây cũng
  thành tường.

---

**Xong Phase 10 → thế giới có hình dạng thật, và hình dạng ấy có đúng một nguồn.**
[PHASE-11](PHASE-11.md) đổi câu hỏi từ *"đi được chỗ nào"* sang *"thấy được những ai"*: chia map thành
cột, và biến `EntitySpawn`/`EntityDespawn` từ "sự kiện vào/ra world" thành "hệ quả của tầm nhìn" — mà
client thì không phải sửa một dòng nào.
