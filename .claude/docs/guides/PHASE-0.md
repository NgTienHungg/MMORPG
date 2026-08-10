# PHASE 0 — Nền móng dự án

> **Cách dùng file này:** làm lần lượt từng bước. Gặp `✅ CHECKPOINT` thì phải đạt được mới đi tiếp —
> lỗi ở Phase 0 mà bỏ qua sẽ nổ ở Phase 2 với triệu chứng hoàn toàn khác, rất khó lần.
>
> **Kết quả cuối Phase 0:**
> - Unity mở lên compile sạch, có canvas 1920×1080, dùng được package `com.hungnt.*` + VContainer.
> - `cd Server && dotnet build` chạy được 3 project: `Shared`, `GameServer`, `DBServer`.
> - Sửa file trong `Server/Shared` → build → DLL **tự động** xuất hiện trong `Assets/Plugins/Shared/`.
>
> **Thời gian ước tính:** 1–2 tiếng, phần lớn là chờ Unity import.

---

## 0. Đã làm sẵn — chỉ cần đọc để biết có gì

| Việc | Kết quả |
|------|---------|
| 9 submodule `com.hungnt.*` | `Packages/com.hungnt.{core,eventbus,objectpool,assetload,dataconfig,datasave,ui,ui.panel,ui.tween}` |
| `Packages/manifest.json` | Thêm VContainer (git), UniTask, Addressables, Newtonsoft, NuGetForUnity, Odin (git), registry OpenUPM |
| Scene `Assets/Game/Scenes/Bootstrap.unity` | Camera 2D URP + Global Light 2D + `UIRoot` canvas + `EventSystem` |
| Canvas | `Scale With Screen Size`, reference **1920×1080**, Match = **0.75** |
| `ProjectSettings` | Màn hình mặc định 1920×1080, khoá **landscape** (bỏ portrait), company = HungNT |
| `EditorBuildSettings` | Scene 0 = `Bootstrap.unity` |
| `.gitignore` | Thêm mục cho .NET (`bin/`, `obj/`, `*.db`) và mở lại `*.csproj`/`*.sln` cho `Server/` |
| Tài liệu | `CLAUDE.md`, `.claude/docs/{ROADMAP,VOLAMGENZ-REFERENCE,CONVENTIONS}.md` |

**Vì sao Match = 0.75:** `Match Width Or Height` với 0 = bám chiều rộng, 1 = bám chiều cao.
Game landscape chạy trên đủ loại tỉ lệ (16:9 → 20:9). Nếu bám hẳn chiều rộng, máy càng dài thì UI càng
bị bóp nhỏ theo chiều cao. 0.75 nghiêng về chiều cao → UI giữ được kích thước dễ bấm, phần thừa hai bên
chỉ là khoảng trống. Đây đúng là con số `vo-lam-genz` dùng cho canvas chính.

---

## Bước 1 — Mở Unity lần đầu

1. Unity Hub → Add → chọn thư mục `MMORPG` → mở bằng **6000.2.9f1**.
2. Lần đầu Unity sẽ tải VContainer + UniTask + Odin từ git/registry → **mất vài phút**, đừng tắt giữa chừng.
3. Mở scene `Assets/Game/Scenes/Bootstrap.unity`.

### 1a. Kiểm tra EventSystem
Chọn GameObject `EventSystem` trong Hierarchy. Component `Input System UI Input Module` có ô **Actions Asset**:
- Nếu đã tự điền → xong.
- Nếu **trống** → kéo `Assets/InputSystem_Actions.inputactions` vào ô đó, rồi Ctrl+S.

> Vì sao có thể trống: scene được viết tay nên không tham chiếu sẵn từng action con.
> Input System sẽ tự gán bộ action mặc định, nhưng gán tay cho chắc.

### 1b. Kiểm tra Game view
Cửa sổ Game → dropdown resolution → thêm preset **1920×1080 (Full HD)** nếu chưa có, chọn nó.

### ✅ CHECKPOINT A
- Console **không có lỗi đỏ**.
- Window → Package Manager → thấy đủ 9 package `HungNT ...` ở mục *In Project* (custom), + VContainer, UniTask, Odin.
- Trong Scene view thấy khung canvas hình chữ nhật ngang.

> Nếu Console báo lỗi liên quan `HungNT.UI.Tween` / `UniTask.DOTween` → **bình thường**, sang Bước 2 xử lý.
> Lỗi khác thì dừng lại, đừng đi tiếp.

---

## Bước 2 — DOTween (bắt buộc, vì `com.hungnt.ui.tween` phụ thuộc)

`com.hungnt.ui.tween` tham chiếu assembly `UniTask.DOTween`. Assembly này chỉ tồn tại khi:
(1) có DOTween trong project, **và** (2) bật define `UNITASK_DOTWEEN_SUPPORT`.

### 2a. Đưa DOTween vào project
Cách nhanh nhất — copy từ dự án cũ của bạn:
```bash
cp -R /Users/ngtienhungg/Documents/UnityProjects/BaseCode_Test/Assets/Plugins/Demigiant \
      /Users/ngtienhungg/Documents/UnityProjects/MMORPG/Assets/Plugins/Demigiant
```
Quay lại Unity, chờ import. Nếu hiện cửa sổ **DOTween Setup** → bấm *Setup DOTween...* → *Apply*.

### 2b. Bật define
Edit → Project Settings → Player → Other Settings → **Scripting Define Symbols** → thêm:
```
UNITASK_DOTWEEN_SUPPORT
```
Enter rồi bấm **Apply**. Chờ Unity compile lại.

> Làm cho platform bạn đang dùng (Standalone). Sau này build sang Android/iOS thì thêm lại cho platform đó.

### ✅ CHECKPOINT B
Console sạch. Tạo thử 1 script bất kỳ có `using HungNT.UI.Panel;` → không báo lỗi namespace. Xoá script test đi.

---

## Bước 3 — MemoryPack vào Unity (qua NuGetForUnity)

DTO mạng sẽ nằm trong DLL build từ `Server/Shared`, nhưng DLL đó **tham chiếu** `MemoryPack.Core.dll`
và `K4os.Compression.LZ4.dll` — nên Unity cũng phải có 2 thư viện này, nếu không sẽ lỗi lúc chạy.

1. Menu **NuGet → Manage NuGet Packages**.
2. Tab *Online*, tìm và Install:
   - `MemoryPack` — version **1.21.4** (khớp với version dùng ở `Server/Shared`)
   - `K4os.Compression.LZ4` — version **1.3.8**
3. Kết quả: `Assets/Packages/MemoryPack.1.21.4/`, `.../MemoryPack.Core.1.21.4/`, `.../K4os.Compression.LZ4.1.3.8/`

> **Vì sao phải khớp version:** DLL `MMORPG.Shared` được biên dịch với đúng version nào thì lúc chạy
> phải tìm thấy đúng version đó. Lệch minor version thường vẫn chạy, lệch major là `TypeLoadException`.

> **Vì sao không tự viết `[MemoryPackable]` trong Unity:** mọi DTO đều nằm ở `Server/Shared`, code sinh tự động
> đã được biên dịch sẵn vào DLL. Unity chỉ cần *đọc* được DLL đó. Nếu sau này bạn có viết `[MemoryPackable]`
> ngay trong Unity thì mới cần source generator chạy trong Unity — lúc đó `MemoryPack.Generator` đã có sẵn rồi.

### ✅ CHECKPOINT C
Console sạch. Edit → Project Settings → Player → **Api Compatibility Level** = `.NET Standard 2.1`.

---

## Bước 4 — Cấu trúc thư mục client

Tạo trong `Assets/Game/`:

```
Assets/Game/
├── Art/                  sprite, tileset, atlas
├── Prefabs/
│   ├── UI/
│   └── World/
├── Resources/            (dùng hạn chế — asset chính đi qua Addressables)
├── Scenes/
│   └── Bootstrap.unity   ✅ đã có
└── Scripts/
    ├── Boot/             GameLifetimeScope, bootstrap
    ├── Network/          transport, codec, dispatcher, service  (Phase 1–2)
    ├── Auth/             (Phase 4)
    ├── World/            (Phase 5+)
    └── UI/               (Phase 4+)
```

Namespace vẫn theo cấu trúc thư mục: `MMORPG.Client.Boot`, `MMORPG.Client.Network`…

### 4a. Không dùng Assembly Definition — quyết định có chủ đích

Code game client nằm hết trong `Assembly-CSharp` mặc định của Unity. **Không tạo `.asmdef`.**

**Vì sao:** asmdef đổi lấy tốc độ compile bằng việc phải khai báo tường minh **mọi** dependency, và
quan trọng hơn — một assembly do asmdef định nghĩa **không thể tham chiếu tới các assembly dựng sẵn**
(`Assembly-CSharp`, `Assembly-CSharp-firstpass`). Mà DOTween Pro nằm ở `Assets/Plugins/Demigiant/DOTweenPro/`
dưới dạng **file `.cs` không có asmdef** → chúng rơi vào `Assembly-CSharp-firstpass` → code trong
`MMORPG.Client.asmdef` sẽ không dùng được `DOTweenAnimation` và các API Pro khác. Đây là loại vướng
chỉ lộ ra khi đã viết được kha khá code, lúc đó gỡ ra rất phiền.

**Cái giá phải trả:** sửa một dòng là Unity compile lại toàn bộ `Assembly-CSharp`. Với dự án cỡ này
(vài trăm file) thì vẫn dưới vài giây — chấp nhận được. Khi nào thấy compile chậm rõ rệt thì tách asmdef
cho những phần **không** đụng DOTween (`Network/` là ứng viên đầu tiên, nó thuần C#).

> `Packages/com.hungnt.*` vẫn có asmdef riêng — đó là package độc lập, chuyện khác. Code trong
> `Assembly-CSharp` tham chiếu chúng tự động, không cần khai báo gì.

### ✅ CHECKPOINT D
Console sạch. Tạo thử một script trong `Assets/Game/Scripts/` có `using VContainer;` và
`using HungNT.Core;` → không báo lỗi namespace.

---

## Bước 5 — Solution .NET cho server

Máy bạn đang có .NET SDK **8.0.418** — đúng bản cần.

**Kết quả cần đạt** (dù làm bằng Rider hay CLI):

```
MMORPG/
└── Server/
    ├── MMORPG.Server.sln
    ├── Shared/       MMORPG.Shared.csproj        (class library)
    ├── GameServer/   MMORPG.GameServer.csproj    (console app)
    └── DBServer/     MMORPG.DBServer.csproj      (console app)
```

Chú ý: **tên project ≠ tên thư mục.** Thư mục ngắn (`Shared`) cho dễ đọc đường dẫn trong tài liệu,
tên project đầy đủ (`MMORPG.Shared`) vì nó thành tên DLL và tên assembly. Rider mặc định lấy tên thư mục
theo tên project — phải sửa tay ô *Location*, xem 5A.2 bên dưới.

---

### 5A. Cách làm bằng Rider

> Solution server là một solution **riêng**, không liên quan gì tới `MMORPG.sln` ở thư mục gốc
> (file đó do Unity tự sinh cho code trong `Assets/`, đừng đụng vào). Mở nó ở một cửa sổ Rider riêng.

#### 5A.1. Tạo solution + project `MMORPG.Shared`

`File → New Solution…`

| Ô | Điền |
|---|------|
| Template (cột trái) | **Class Library** (mục *.NET*) |
| Solution name | `MMORPG.Server` |
| Project name | `MMORPG.Shared` |
| Solution directory | `/Users/ngtienhungg/Documents/UnityProjects/MMORPG/Server` |
| ☐ Put solution and project in the same directory | **bỏ tick** |
| Language | C# |
| Framework | `net8.0` (bất kỳ — bước 5b sẽ ghi đè bằng multi-target) |

Bấm **Create**.

#### ⚠️ Kiểm tra ngay: Rider hay lồng thêm một tầng thư mục

Tuỳ phiên bản, Rider có thể hiểu ô *Solution directory* là **thư mục cha** rồi tự tạo thêm
`<Solution name>/` bên trong — kết quả là `Server/MMORPG.Server/MMORPG.Server.sln` thay vì
`Server/MMORPG.Server.sln`. Kiểm tra ngay, đừng đi tiếp:

```bash
ls /Users/ngtienhungg/Documents/UnityProjects/MMORPG/Server
```

Phải thấy `MMORPG.Server.sln` **ngay tại đây**. Nếu thấy một thư mục `MMORPG.Server/` thì bị lồng dư —
đóng Rider rồi kéo mọi thứ lên một tầng:

```bash
cd /Users/ngtienhungg/Documents/UnityProjects/MMORPG/Server
mv MMORPG.Server/* MMORPG.Server/.[!.]* . 2>/dev/null
rmdir MMORPG.Server
```

Đường dẫn trong `.sln` là tương đối nên chuyển cả cụm cùng lúc thì vẫn đúng. Mở lại `.sln`.

> **Vì sao phải chặn ngay ở đây:** lệch một tầng thư mục thì mọi thứ vẫn build xanh, nhưng target copy DLL
> ở bước 5a dùng đường dẫn tương đối `../../` sẽ trỏ ra `Server/Assets/Plugins/Shared/` thay vì
> `Assets/Plugins/Shared/` của Unity. File vẫn được tạo, build vẫn báo thành công, Unity vẫn không thấy gì.
> (`<Error>` ở bước 5a chính là để bắt tình huống này — nhưng phát hiện sớm ở đây vẫn đỡ mất công hơn.)

Rider sẽ tạo `Server/MMORPG.Shared/`. Ta muốn thư mục tên `Shared`:
trong cửa sổ *Solution* bên trái, chuột phải thư mục `MMORPG.Shared` → `Refactor → Rename…` →
chọn **Rename directory only** (không đổi tên project) → gõ `Shared` → Enter.

> Nếu Rider phiên bản của bạn không cho tách như vậy: đóng Rider, đổi tên thư mục ngoài Finder,
> mở lại `.sln` — Rider sẽ báo project không tìm thấy và cho bạn trỏ lại đường dẫn mới.
> Hoặc đơn giản hơn: **cứ để nguyên `MMORPG.Shared/`** và tự thay đường dẫn khi đọc tài liệu.
> Không có gì sai cả — chỉ là dài hơn.

Xoá file `Class1.cs` Rider tạo sẵn.

#### 5A.2. Thêm `MMORPG.GameServer` và `MMORPG.DBServer`

Chuột phải vào **solution** `MMORPG.Server` (dòng trên cùng) → `Add → New Project…`

| Ô | GameServer | DBServer |
|---|-----------|----------|
| Template | **Console Application** | **Console Application** |
| Name | `MMORPG.GameServer` | `MMORPG.DBServer` |
| Location | `.../MMORPG/Server/GameServer` | `.../MMORPG/Server/DBServer` |
| Framework | `net8.0` | `net8.0` |

**Ô `Location` là chỗ dễ sai nhất.** Rider tự điền `<solution dir>/<Name>` →
sửa đuôi `MMORPG.GameServer` thành `GameServer`.

#### 5A.3. Nối project reference

Chuột phải `MMORPG.GameServer` → `Add → Reference…` → tab **Projects** →
tick `MMORPG.Shared` → **OK**. Làm y hệt cho `MMORPG.DBServer`.

Kiểm tra: mở `MMORPG.GameServer.csproj` (chuột phải project → `Edit → Edit 'MMORPG.GameServer.csproj'`),
phải thấy:
```xml
<ItemGroup>
  <ProjectReference Include="..\Shared\MMORPG.Shared.csproj" />
</ItemGroup>
```

#### 5A.4. Cài NuGet package cho `MMORPG.Shared`

Chuột phải `MMORPG.Shared` → `Manage NuGet Packages`. Ở tab **Packages**, tìm và cài đúng version:

| Package | Version |
|---------|---------|
| `MemoryPack` | **1.21.4** |
| `K4os.Compression.LZ4` | **1.3.8** |

Cách cài: gõ tên vào ô search → chọn package ở danh sách trái → panel phải chọn đúng **Version** trong dropdown
→ bấm dấu **`+`** ở dòng project `MMORPG.Shared`.

> Phải chọn version thủ công. Rider mặc định cài bản mới nhất, mà bản mới nhất có thể lệch với
> version bạn cài bên Unity ở Bước 3 → `TypeLoadException` lúc chạy. Hai bên phải **khớp tuyệt đối**.

> `MemoryPack` là meta-package: cài nó sẽ tự kéo theo `MemoryPack.Core` (thư viện) và
> `MemoryPack.Generator` (source generator). Không cần cài riêng.

#### 5A.5. Tạo run configuration để chạy server

Rider tự tạo sẵn config khi có Console App. Ở thanh công cụ trên cùng, dropdown cạnh nút ▶ chọn
**MMORPG.GameServer** → bấm ▶. Cửa sổ *Run* hiện output console.

Từ Phase 1 trở đi bạn sẽ chạy server bằng nút này thay vì `dotnet run`. Tiện hơn hẳn:
đặt breakpoint được, xem biến được, dừng bằng nút ■.

> **Tip cho Phase 7** (test 2 client): dropdown run config → `Edit Configurations…` → tick
> **Allow multiple instances** để chạy được nhiều tiến trình server/client cùng lúc.

---

### 5B. Cách làm bằng CLI (nếu thích gõ lệnh)

```bash
cd /Users/ngtienhungg/Documents/UnityProjects/MMORPG
mkdir -p Server && cd Server

dotnet new sln     -n MMORPG.Server
dotnet new classlib -o Shared    -n MMORPG.Shared    -f netstandard2.1
dotnet new console  -o GameServer -n MMORPG.GameServer -f net8.0
dotnet new console  -o DBServer   -n MMORPG.DBServer   -f net8.0

dotnet sln add Shared/MMORPG.Shared.csproj GameServer/MMORPG.GameServer.csproj DBServer/MMORPG.DBServer.csproj
dotnet add GameServer/MMORPG.GameServer.csproj reference Shared/MMORPG.Shared.csproj
dotnet add DBServer/MMORPG.DBServer.csproj   reference Shared/MMORPG.Shared.csproj

dotnet add Shared/MMORPG.Shared.csproj package MemoryPack --version 1.21.4
dotnet add Shared/MMORPG.Shared.csproj package K4os.Compression.LZ4 --version 1.3.8

rm Shared/Class1.cs
```

---

### 5a. Sửa `Server/Shared/MMORPG.Shared.csproj`

Trong Rider: chuột phải project → `Edit → Edit 'MMORPG.Shared.csproj'`.

Thay toàn bộ nội dung bằng (giữ nguyên phần `<PackageReference>` Rider vừa thêm — nội dung dưới đã có sẵn):

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <!-- netstandard2.1 để Unity đọc được; net8.0 để server chạy nhanh hơn -->
    <TargetFrameworks>netstandard2.1;net8.0</TargetFrameworks>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <RootNamespace>MMORPG.Shared</RootNamespace>
    <AssemblyName>MMORPG.Shared</AssemblyName>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <!-- 1591 = "thiếu XML doc cho public member" — bật lại khi contract đã ổn định -->
    <NoWarn>1591</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MemoryPack" Version="1.21.4" />
    <PackageReference Include="K4os.Compression.LZ4" Version="1.3.8" />
  </ItemGroup>

  <!--
    Sau mỗi lần build bản netstandard2.1, copy DLL + XML sang Unity.
    Nhờ target này mà contract chỉ có MỘT nguồn: sửa ở đây, Unity nhận ngay.
  -->
  <Target Name="CopySharedToUnity" AfterTargets="Build" Condition="'$(TargetFramework)' == 'netstandard2.1'">
    <PropertyGroup>
      <UnityProjectDir>$(MSBuildProjectDirectory)/../../</UnityProjectDir>
      <UnityPluginDir>$(UnityProjectDir)Assets/Plugins/Shared/</UnityPluginDir>
    </PropertyGroup>

    <!--
      Đường dẫn tương đối sai vẫn "chạy được": MakeDir sẽ vui vẻ tạo ra một thư mục
      Assets/Plugins/Shared ở chỗ vô nghĩa và build vẫn báo thành công. Kiểm tra mốc
      ProjectVersion.txt để biến lỗi câm đó thành lỗi build có thông điệp rõ ràng.
    -->
    <Error Condition="!Exists('$(UnityProjectDir)ProjectSettings/ProjectVersion.txt')"
           Text="Không thấy project Unity ở '$(UnityProjectDir)'. Solution phải nằm tại &lt;UnityProject&gt;/Server/ và project này tại &lt;UnityProject&gt;/Server/Shared/." />

    <MakeDir Directories="$(UnityPluginDir)" />
    <Copy SourceFiles="$(TargetPath)" DestinationFolder="$(UnityPluginDir)" />
    <Copy SourceFiles="$(TargetDir)$(AssemblyName).xml" DestinationFolder="$(UnityPluginDir)" />
    <Message Importance="high" Text="[Shared] Copied $(AssemblyName).dll -> $(UnityPluginDir)" />
  </Target>

</Project>
```

> **Vì sao có `<Error>`:** đây là bài học đáng nhớ hơn cả đoạn copy. Một target copy dùng đường dẫn tương đối
> mà trỏ sai chỗ sẽ **không hề báo lỗi** — `MakeDir` tạo thư mục mới, `Copy` chép file vào đó, build xanh lè,
> chỉ có Unity là không bao giờ thấy DLL. Người dùng đi tìm nguyên nhân ở Unity, ở NuGet, ở asmdef — mọi nơi
> trừ chỗ thật sự sai. Thêm một mốc kiểm tra (`ProjectVersion.txt` chỉ tồn tại ở gốc project Unity) là đủ
> để biến "sai câm lặng" thành "sai kèm hướng dẫn sửa".
>
> Cũng vì thế mà bỏ `ContinueOnError="true"` ở 2 dòng `Copy`: khi đã chắc đường dẫn đúng, copy thất bại
> là chuyện nghiêm trọng, phải làm gãy build chứ không được nuốt.

### 5a-bis. Đặt `AssemblyName` cho GameServer và DBServer

Rider đặt `RootNamespace` = `MMORPG.GameServer` nhưng `AssemblyName` mặc định lấy theo **tên project** (`GameServer`).
Thêm dòng này vào `<PropertyGroup>` của **cả hai** csproj:

```xml
<AssemblyName>MMORPG.GameServer</AssemblyName>   <!-- và MMORPG.DBServer ở project kia -->
```

> **Không phải chuyện thẩm mỹ.** Ở Phase 2, `TcpDispatcher.RegisterAll()` quét handler bằng cách lọc
> `assembly.FullName.StartsWith("MMORPG.")`. Nếu assembly tên `GameServer` thì nó bị loại khỏi vòng quét
> → `Đăng ký 0 handler` → không lệnh nào chạy, mà chẳng có lỗi nào chỉ ra nguyên nhân. Sửa ngay từ đây.

> **Vì sao 2 target framework:** Unity (Api Compatibility `.NET Standard 2.1`) không nạp được DLL biên dịch cho `net8.0`.
> Server thì muốn `net8.0` để có API mới + tối ưu. Multi-target giải quyết cả hai từ **một** source duy nhất.
> Đây chính xác là cách `vo-lam-genz` làm với `MemoryPackSerializerLib`, chỉ khác là họ copy DLL **bằng tay**
> theo `BuildDataTut.md` — ta tự động hoá bước đó vì copy tay là chỗ dễ quên nhất, và quên copy = lệch contract câm lặng.

### 5b. File thử để chứng minh đường ống chạy

`Server/Shared/HandshakeDto.cs`:
```csharp
using MemoryPack;

namespace MMORPG.Shared
{
    /// <summary>
    /// DTO thử để kiểm chứng đường ống Shared → Unity đã thông.
    /// Phase 1 sẽ thay bằng contract thật.
    /// </summary>
    [MemoryPackable]
    public partial class HandshakeDto
    {
        public int ProtocolVersion { get; set; }
        public string ServerName { get; set; } = string.Empty;
    }
}
```

### 5c. Build

**Rider:** menu `Build → Build Solution` (⌘F9). Cửa sổ *Build* hiện log.

**CLI:**
```bash
cd /Users/ngtienhungg/Documents/UnityProjects/MMORPG/Server
dotnet build
```

### ✅ CHECKPOINT E
- Log build có dòng `[Shared] Copied MMORPG.Shared.dll -> .../Assets/Plugins/Shared/`
  *(trong Rider, nếu không thấy: cửa sổ Build → icon bánh răng → đổi mức log lên `Normal`,
  hoặc mở tab **Output** thay vì **Sync**. Không thấy dòng log không có nghĩa là target không chạy —
  kiểm tra bằng cách xem file có ra không, đó mới là bằng chứng.)*
- `Build succeeded`, **0 error**
- File tồn tại: `Assets/Plugins/Shared/MMORPG.Shared.dll`

```bash
ls -la /Users/ngtienhungg/Documents/UnityProjects/MMORPG/Assets/Plugins/Shared/
```

Phải thấy `MMORPG.Shared.dll` **và** `MMORPG.Shared.xml`. Nếu chỉ có `.dll` mà thiếu `.xml` →
thiếu `<GenerateDocumentationFile>true</GenerateDocumentationFile>`. Không chặn gì cả, nhưng mất
XML doc lúc gõ code bên Unity — mà đó chính là nơi bạn ghi "cmd này request gì, response gì".

---

## Bước 6 — Chứng minh Unity đọc được DLL

Tạo `Assets/Game/Scripts/Boot/SharedDllProbe.cs`:

```csharp
using MMORPG.Shared;
using UnityEngine;

namespace MMORPG.Client.Boot
{
    /// <summary>
    /// Script tạm để xác nhận Unity nạp được MMORPG.Shared.dll + MemoryPack.
    /// Xoá sau khi Phase 0 xong.
    /// </summary>
    public class SharedDllProbe : MonoBehaviour
    {
        private void Start()
        {
            var dto = new HandshakeDto { ProtocolVersion = 1, ServerName = "local" };

            byte[] bytes = MemoryPack.MemoryPackSerializer.Serialize(dto);
            var back = MemoryPack.MemoryPackSerializer.Deserialize<HandshakeDto>(bytes);

            Debug.Log($"[Probe] serialize {bytes.Length} byte → deserialize OK: " +
                      $"v{back.ProtocolVersion} / {back.ServerName}");
        }
    }
}
```

Gắn script này lên một GameObject rỗng trong `Bootstrap.unity`, bấm Play.

### ✅ CHECKPOINT F
Console in ra:
```
[Probe] serialize 13 byte → deserialize OK: v1 / local
```
(Con số byte có thể khác chút, không sao.)

**Đây là checkpoint quan trọng nhất của Phase 0.** Nó chứng minh: Unity ↔ DLL ↔ MemoryPack đã thông.
Toàn bộ Phase 1–2 dựa trên đường ống này.

---

## Bước 7 — VContainer LifetimeScope đầu tiên

Tạo `Assets/Game/Scripts/Boot/GameLifetimeScope.cs`:

```csharp
using HungNT.Core;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MMORPG.Client.Boot
{
    /// <summary>
    /// Container gốc của client. Mọi service dùng chung toàn game đăng ký tại đây.
    /// Đặt trên 1 GameObject trong scene Bootstrap, DontDestroyOnLoad.
    /// </summary>
    public class GameLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.InstallCore();   // DebugEx, IAppLifecycle... từ com.hungnt.core
        }
    }
}
```

1. Trong `Bootstrap.unity`, tạo GameObject rỗng tên `GameLifetimeScope`, gắn script vừa tạo.
2. Bấm Play.

### ✅ CHECKPOINT G
Không có exception trong Console. Container build được.

> **Vì sao dựng DI ngay từ Phase 0:** `NetService` ở Phase 1 sẽ là service đầu tiên đăng ký vào đây.
> Nếu để sau mới bọc DI thì phải sửa lại hết chỗ gọi — mà đó chính là lỗi `vo-lam-genz` mắc phải
> (`GameInstance.Game` static, giờ không gỡ ra được nữa).

---

## Bước 8 — Dọn dẹp & commit

```bash
cd /Users/ngtienhungg/Documents/UnityProjects/MMORPG
rm -rf Assets/Scenes                 # scene mẫu của Unity, không dùng
```
(Trong Unity, xoá luôn `Assets/Scenes` từ Project window để nó dọn cả file `.meta`.)

Xoá `SharedDllProbe.cs` và GameObject gắn nó — đã làm xong nhiệm vụ.

Rồi commit (khi bạn thấy sẵn sàng):
```bash
git add -A
git commit -m "chore(setup): nền móng dự án — submodule com.hungnt, canvas 1920x1080, solution server, contract Shared"
```

---

## Troubleshooting

| Triệu chứng | Nguyên nhân | Cách xử lý |
|-------------|-------------|------------|
| `The type or namespace 'VContainer' could not be found` | Unity chưa tải xong package git | Package Manager → chờ resolve; kiểm tra mạng; Window → Package Manager → nút refresh |
| `Assembly 'UniTask.DOTween' not found` | Chưa có DOTween hoặc chưa bật define | Làm lại Bước 2, nhớ bấm **Apply** sau khi thêm define |
| `The type or namespace 'MMORPG' could not be found` (trong Unity) | DLL chưa copy sang, hoặc chưa `dotnet build` | Chạy lại `dotnet build` trong `Server/`, kiểm tra `Assets/Plugins/Shared/` |
| `TypeLoadException: MemoryPack.MemoryPackSerializer` lúc Play | Version MemoryPack trong Unity ≠ version trong `Shared.csproj` | Cài lại đúng 1.21.4 qua NuGetForUnity |
| `Could not load file or assembly 'K4os.Compression.LZ4'` | Thiếu package LZ4 trong Unity | NuGetForUnity → cài `K4os.Compression.LZ4` 1.3.8 |
| Build .NET báo `NETSDK1045 ... does not support targeting netstandard2.1` | Thiếu targeting pack | `dotnet workload repair`, hoặc kiểm tra `dotnet --list-sdks` |
| Sửa `Shared` xong nhưng Unity không thấy thay đổi | Quên build, hoặc Unity đang khoá file DLL | Build lại; nếu Unity khoá thì tắt Play mode rồi build lại |
| **Build xanh, không thấy log `[Shared] Copied`, Unity không có gì thay đổi** | Solution bị lồng dư một tầng (`Server/MMORPG.Server/...`) → `../../` trỏ sai, DLL rơi vào `Server/Assets/Plugins/Shared/` | Kiểm tra `ls Server` — phải thấy `.sln` ngay tại đó. Xem mục ⚠️ ở 5A.1 để kéo lên một tầng, xoá `Server/Assets`, xoá `bin`/`obj` rồi build lại. Bản csproj có `<Error>` sẽ báo thẳng tình huống này |
| Không thấy dòng `[Shared] Copied` dù đường dẫn đúng | Rider ẩn message mức `high` ở tab *Sync* | Bằng chứng thật là **file có ra hay không**, không phải dòng log. Chạy `ls Assets/Plugins/Shared/` |
| Rider: `Add → Reference…` không có tab *Projects* | Đang chuột phải vào file thay vì vào project | Chuột phải đúng dòng project (icon hình khối), không phải file `.cs` |
| Rider tạo thư mục `MMORPG.GameServer/` thay vì `GameServer/` | Quên sửa ô *Location* lúc tạo project | Không sao — cứ để nguyên, chỉ cần tự quy đổi đường dẫn khi đọc tài liệu. Hoặc đóng Rider, đổi tên thư mục ngoài Finder, sửa `<ProjectReference Include>` trong 2 csproj cho khớp |
| Rider: `Manage NuGet Packages` cài nhầm bản mới nhất | Không chọn version trong dropdown trước khi bấm `+` | Gỡ đi cài lại đúng version, hoặc sửa thẳng số version trong csproj rồi build lại |
| Rider báo `MMORPG.Shared` có 2 target framework, không biết chạy cái nào | Bình thường với multi-target class library | Class library không chạy, chỉ build. Chỉ `GameServer`/`DBServer` mới có run config |
| Rider mở nhầm `MMORPG.sln` ở thư mục gốc | Đó là solution Unity tự sinh cho `Assets/`, không chứa project server | Mở `Server/MMORPG.Server.sln` ở cửa sổ riêng. Hai solution này độc lập, không cần biết nhau |
| Canvas hiện dọc thay vì ngang | Game view đang ở preset dọc | Game view → chọn preset 1920×1080 |
| Submodule trống rỗng (`Packages/com.hungnt.core` không có file) | Clone repo mà chưa init submodule | `git submodule update --init --recursive` |

---

## Tự kiểm tra hiểu bài

Trả lời được hết thì sang Phase 1:

1. Vì sao `MMORPG.Shared` phải multi-target `netstandard2.1` **và** `net8.0`?
2. Nếu bạn thêm 1 DTO mới vào `Server/Shared` mà quên `dotnet build`, chuyện gì xảy ra ở Unity? Bạn sẽ phát hiện lúc nào?
3. Vì sao contract (enum cmd + DTO) **không** được chép tay sang Unity, dù chép tay nhanh hơn?
4. Dự án này cố tình **không** dùng asmdef cho code client. Đánh đổi cụ thể là gì, và điều kiện nào khiến quyết định đó đảo ngược?
5. `Match Width Or Height = 0.75` nghĩa là canvas ưu tiên bám theo chiều nào? Đổi thành 0 thì UI trên màn 21:9 sẽ ra sao?
6. Vì sao dựng VContainer LifetimeScope ngay từ Phase 0 thay vì đợi đến khi có nhiều service?

---

**Xong Phase 0 → [`PHASE-1.md`](PHASE-1.md): làm cho byte đi được 2 chiều giữa Unity và server.**
