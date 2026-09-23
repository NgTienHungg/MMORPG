# FEATURE-TEMPLATE — Khuôn chuẩn thêm một feature vào game

> Chốt 2026-09-22, sau khi Phase 12 lộ ra hai lỗi thiết kế cùng một gốc: `ConfigService` phải chép
> lại cả hàm cho mỗi bảng mới, và `Program.cs` gán tay từng service bằng static property.
>
> Đây **không** phải tài liệu phase. Nó là bảng tra: *"thêm feature X thì đụng vào những đâu, theo
> thứ tự nào, và chỗ nào quên là không có lỗi biên dịch"*.

---

## Câu hỏi hỏi trước khi viết dòng đầu tiên

Ba câu, và câu thứ ba là câu hay bị bỏ qua nhất:

1. **Dữ liệu của feature này thuộc loại nào?** Xem bảng [§1](#1-ba-loại-dữ-liệu).
2. **Client cần biết gì?** Thứ nó *không* cần biết thì đừng cho vào `Shared` — bán kính AOI là ví dụ.
3. **Thêm cái THỨ HAI cùng loại tốn bao nhiêu dòng?** Nếu đáp án là "chép lại cả hàm rồi sửa tên"
   thì thiết kế sai. Sửa thiết kế trước, đừng viết cái thứ nhất rồi hẹn dọn sau — cái thứ hai bao
   giờ cũng tới sớm hơn dự định.

---

## 1. Ba loại dữ liệu

Mỗi feature đụng tới ít nhất một loại. Nhầm loại là nguồn của lớp bug câm khó chịu nhất.

| | **A — tham số vận hành** | **B — bảng dữ liệu** | **C — state người chơi** |
|---|---|---|---|
| Ví dụ | trọng lực, bán kính AOI, map khởi đầu | bảng nhân vật, bảng item, bảng quái | vị trí, túi đồ, exp, chỉ số |
| Ai viết ra | người vận hành, trong `Config/game.json` | người thiết kế game, trong `Assets/Game/Resources/Config/` | chính người chơi, qua hành động trong game |
| Nằm ở đâu | file CHỈ server đọc → `ConfigService.Current` | file CẢ HAI BÊN đọc → `*ConfigContainer` | DB → cache RAM server |
| Client biết không | chỉ phần cần, đẩy trong `EnterWorldResponse` | **cả bảng**, đọc từ `Resources/Config/` của chính nó | chỉ phần của chính mình, đẩy khi đổi |
| Chống lệch bằng | client không giữ bản nào | hai bản + **vân tay in ra log cả hai bên** | server là nguồn duy nhất |
| Sống bao lâu | tới lần bấm `R` | tới lần bấm `R` | vĩnh viễn, có migration |

**Loại B luôn là bộ ba** `*Config` / `*TableData` / `*ConfigContainer` — xem `CONVENTIONS.md` §2.

**Loại A và loại B đi qua cùng MỘT hàm nạp** phía server (`ConfigService.LoadFile<T>`); khác biệt duy
nhất là tham số `WhenBroken`: `game.json` hỏng thì về mặc định, bảng hỏng thì giữ nguyên bản đang chạy.

---

## 2. Tám tầng, theo đúng thứ tự này

Làm từ dưới lên: mỗi tầng chỉ dựa vào tầng dưới nó, nên khi một tầng chạy thì nó chạy thật chứ không
chờ tầng trên. Làm ngược từ UI xuống là gõ ba tiếng rồi mới biết cái tầng dưới cùng không khả thi.

```
8. UI / Presenter        Assets/Game/Scripts/<Feature>/
7. Client nhận + gửi     Network/Handlers/<Feature>NetHandler.cs · <Feature>Api.cs
6. Contract              Shared/Net/NetCmd.cs · Shared/Dto/<Feature>/
5. Server handler        GameServer/Handlers/<Feature>Handler.cs
4. Server service        GameServer/World/<Feature>Service.cs      ← nghiệp vụ nằm HẾT ở đây
3. Cache RAM             GameServer/World/<Feature>.cs             (loại C)
2. DB protocol           Shared/Db/DbCmd.cs · Shared/Dto/Db/       + DBServer/Handlers/ + Repositories/
1. Schema                DBServer/Data/Migrator.cs                 (loại C)
0. Bảng tĩnh             Shared/World/<Feature>/ + Assets/Game/Resources/Config/<feature>.json
```

Feature chỉ có loại B (ví dụ: thêm bảng skill) thì **chỉ có tầng 0** — không đụng NetCmd, không đụng DTO.
Feature chỉ có loại C (ví dụ: exp) thì bỏ tầng 0.

---

## 3. Checklist theo tầng — có đánh dấu chỗ QUÊN là không có lỗi biên dịch

### Tầng 0 — Bảng tĩnh (loại B)

**Cả hai bên đọc file của mình.** Nguồn nằm trong `Assets/Game/Resources/Config/` (client cần nó lúc
chạy mà không tải gì thêm), csproj copy sang `Data/Config/` cho server — đúng chiều và đúng khuôn với
file map. Bảng KHÔNG đi trên dây, và `EnterWorldResponse` không mang gì của nó — kể cả vân tay.

- [ ] `Server/Shared/World/<Feature>/<Feature>Config.cs` — một dòng, `[MemoryPackable]`, property có setter
- [ ] `Server/Shared/World/<Feature>/<Feature>TableData.cs` — `: IConfigFile`, có `Version`, mảng,
      `RowCount` (`[MemoryPackIgnore] [JsonIgnore]`). **Không có `Checksum()`** — xem dưới
- [ ] `Server/Shared/World/<Feature>/<Feature>ConfigContainer.cs` — `Load` + `Find`/`Get` + `Count`
- [ ] `Server/Shared/World/ConfigFiles.cs` — thêm một hằng tên bảng (không có đuôi `.json`)
- [ ] `Assets/Game/Resources/Config/<feature>.json` + file `.meta` đi kèm
- [ ] `ConfigService.Load()` **phía server** — một dòng:
      `LoadTable<XTableData>(ConfigFiles.X, ValidateX, XConfigContainer.Load);`
- [ ] `ConfigService.ValidateX` — kẹp miền giá trị. **Chỉ ở server**, và có lý do ở dưới
- [ ] ⚠️ **`Client/Config/ConfigService.LoadTables()` thêm một dòng**
      `LoadTable<XTableData>(ConfigFiles.X, XConfigContainer.Load);`
      — quên dòng này thì bảng rỗng **chỉ ở client**, trong khi server chạy bình thường. Triệu chứng
      tuỳ container: `Get(id)` thì ném ngay lần tra đầu, `Find(id)` thì trả **null** và UI hiện ra
      trống trơn — không exception, không lỗi biên dịch, không dấu hiệu nào ở phía server
Không phải thêm gì vào `EnterWorldResponse`: bảng mới không đi trên dây, nên contract không đổi —
thêm bảng thứ mười cũng không đụng tới một dòng nào của `Shared/Dto/`.

**Ba luật dễ vi phạm ở tầng này:**

1. **Không viết `Checksum()` cho từng bảng.** `ConfigFingerprint.Of(table)` băm byte đã tuần tự hoá,
   dùng chung cho mọi bảng. Băm tay là hai chục dòng mỗi bảng **và** một chế độ hỏng câm: thêm
   trường mà quên thêm vào hàm băm thì vân tay không còn phát hiện được thay đổi ở trường đó.
2. **Kiểm miền giá trị chỉ ở server.** Nếu client cũng kẹp thì một file có `MoveSpeed = 999` cho ra
   hai bên cùng chạy 5, và cái file hỏng ấy không bao giờ bị phát hiện. Server kẹp, client không,
   hai dòng log vân tay lệch nhau, người ta đi sửa file.
3. **Hai bên băm SAU `load()`.** Cùng một thời điểm trong quy trình, ở cả hai bên. Băm lệch thời
   điểm là làm chính phép phát hiện lệch nói dối — xem `ConfigFingerprintTests`.

### Tầng 1–2 — DB (loại C)

- [ ] `Migrator._migrations` — thêm `(N, "CREATE TABLE ...")`. **Migration đã chạy thì không bao giờ
      sửa**, cần đổi thì thêm bản mới
- [ ] Ràng buộc đặt ở **DB** (`UNIQUE`, `REFERENCES ... ON DELETE CASCADE`), không ở code — xem
      `AccountRepository.CreateAsync`
- [ ] `Shared/Db/DbCmd.cs` — thêm mã, **đúng dải của feature**
- [ ] `Shared/Dto/Db/<Feature>DbDto.cs` — request + response
- [ ] `DBServer/Repositories/<Feature>Repository.cs` — chỉ SQL, không nghiệp vụ
- [ ] `DBServer/Handlers/<Feature>DbHandler.cs` — `[DbHandler(DbCmd.X)]`, ba dòng
- [ ] ⚠️ `DBServer/Program.cs` — `<Feature>DbHandler.Repository = new <Feature>Repository(database);`

### Tầng 3–4 — RAM và nghiệp vụ

- [ ] `GameServer/World/<Feature>.cs` — cache RAM cho **một** người chơi (loại C), có dirty flag
- [ ] `GameServer/World/<Feature>Service.cs` — **toàn bộ** nghiệp vụ. Nhận phụ thuộc qua **constructor**
- [ ] ⚠️ `GameServer/Boot/ServerBootstrap.Build()` — `ServerServices.Register(new <Feature>Service(...));`
      đặt **sau** những thứ nó cần. Quên dòng này thì handler ném `InvalidOperationException` ở gói
      tin đầu tiên, không phải lúc boot

### Tầng 5–6 — Contract và handler server

- [ ] `Shared/Net/NetCmd.cs` — số **trong dải của feature** (xem `ROADMAP.md` §2)
- [ ] `Shared/Dto/<Feature>/<Feature>Dto.cs` — `[MemoryPackable] public partial class`
- [ ] `GameServer/Handlers/<Feature>Handler.cs`:
      ```csharp
      private static <Feature>Service <Feature>Service => ServerServices.Get<<Feature>Service>();

      [TcpHandler(NetCmd.X, MinState = SessionState.InWorld)]
      public static async Task<NetResult> OnX(NetRequest req) { ... }
      ```
      Property `=>` chứ không field: field static khởi tạo **trước** `ServerBootstrap.Build()` và sẽ
      giữ null vĩnh viễn
- [ ] Mọi trường đến từ client đều **kiểm miền giá trị** trước khi dùng: `float.IsFinite`, `Clamp`,
      `Enum.IsDefined`. Gói tin là dữ liệu của người lạ
- [ ] `MinState` đặt đúng — quên là lệnh chạy được khi chưa đăng nhập
- [ ] Build `Server/Shared` → DLL tự sang `Assets/Plugins/Shared/`

Server **quét cả assembly** nên handler mới tự chạy, không cần đăng ký.

### Tầng 7–8 — Client

- [ ] `Assets/Game/Scripts/Network/Handlers/<Feature>NetHandler.cs` — `[NetHandler(NetCmd.X)]`,
      bắn event, **không** xử lý nghiệp vụ. Đã ở main thread sẵn
- [ ] `Assets/Game/Scripts/<Feature>/<Feature>Api.cs` — gom mọi lệnh **gửi đi**
- [ ] `Assets/Game/Scripts/<Feature>/<Feature>Presenter.cs` — nối event → UI, đăng ký ở `Start`, **gỡ ở `OnDestroy`**
- [ ] `Assets/Game/Scripts/<Feature>/<Feature>Ui.cs` — chỉ vẽ
- [ ] ⚠️⚠️ **`GameLifetimeScope.Configure`** — đây là chỗ quên nhiều nhất trong cả dự án:
      ```csharp
      builder.Register<<Feature>NetHandler>(Lifetime.Singleton).AsSelf().As<INetHandlerGroup>();
      builder.Register<<Feature>Api>(Lifetime.Singleton);
      builder.RegisterComponentInHierarchy<<Feature>Presenter>();
      ```
      Client **không** quét assembly: handler client là method **instance**, phải có container tạo ra
      thì nó mới tồn tại. Thiếu dòng `.As<INetHandlerGroup>()` thì lệnh rơi vào hư không, **không có
      lỗi biên dịch, không có log**
- [ ] UI chỉ đọc state **sau khi** server confirm

---

## 4. Năm chỗ "quên là không có lỗi biên dịch"

Gom lại một chỗ, vì đây là thứ đáng đọc lại mỗi lần:

| Quên | Triệu chứng | Không có dấu hiệu nào ở |
|---|---|---|
| `builder.Register<XNetHandler>(...).As<INetHandlerGroup>()` | client gửi được, server trả lời, **client im lặng** | biên dịch, log, console |
| `Client/Config/ConfigService.LoadTables` thiếu một `LoadTable` | bảng rỗng **chỉ ở client**: `Get` ném, `Find` trả null (UI trống trơn) | server, biên dịch |
| `ServerServices.Register(new XService(...))` | `InvalidOperationException` ở gói tin đầu chạm tới nó | boot (server vẫn lên bình thường) |
| `XDbHandler.Repository = ...` trong DBServer/Program.cs | `NullReferenceException` ở query đầu tiên | boot |
| `MinState` để mặc định | lệnh chạy được khi chưa đăng nhập | mọi nơi, tới khi có người thử |

Bốn trong năm dòng trên là **một dòng đăng ký ở composition root**. Đó là cái giá của việc dùng
attribute + container thay cho `switch` khổng lồ, và nó là cái giá đáng trả — nhưng phải biết mình
đang trả.

---

## 5. Ba ranh giới không được vượt

1. **Nghiệp vụ nằm ở `*Service`, không ở handler.** Handler dài quá ~10 dòng là nghiệp vụ đang rò rỉ.
2. **Handler không gọi handler khác.** Cần việc của feature kia thì gọi `*Service` của nó qua
   `ServerServices.Get<T>()`. Handler gọi handler là đồ thị phụ thuộc thứ hai mà không ai vẽ ra.
3. **Service nhận phụ thuộc qua constructor, không gọi `ServerServices.Get<T>()` trong thân hàm.**
   Service locator chỉ được dùng ở **đúng một biên giới**: giữa dispatch table static và các service
   instance. Dùng nó ở mọi nơi là ném đi thứ duy nhất mà constructor injection cho ta — nhìn chữ ký
   là biết class này cần gì.

---

## 6. Dải số — tra nhanh

| Dải `NetCmd` | Nhóm | | Dải `DbCmd` | Nhóm |
|---|---|---|---|---|
| `1–99` | Hệ thống | | `1000–1099` | Hệ thống |
| `100–199` | Auth | | `1100–1199` | Account |
| `200–299` | Character, chỉ số, trang bị | | `1200–1299` | Character (gồm chỉ số, điểm cộng, exp) |
| `300–399` | World / Movement | | `1300–1399` | Inventory (gồm trang bị đang mặc) |
| `400–499` | Inventory / Item | | `1400–1499` | Combat / Monster |
| `500–599` | Combat / Monster | | `1500+` | *(trống)* |
| `600–699` | Chat | | | |
| `700–999` | *(trống)* | | | |

**Luôn thêm vào CUỐI dải của feature, không chèn giữa. Không tái dùng số đã xoá.**

Chốt dải **trước** khi viết, kể cả khi feature mới dùng một số. Bài học từ vo-lam-genz: dải Bát Quái
phải dời vì đụng feature khác, và dời một dải sau khi đã có client ngoài kia là không dời được.
