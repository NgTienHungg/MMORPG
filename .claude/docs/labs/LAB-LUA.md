# LAB — Lua từ số 0, rồi ghép vào MMORPG

> **Dành cho người chưa biết Lua.** Phần 1 học Lua bằng một "playground" trên GameServer — chưa cần
> hiểu gì về mạng, chưa mở Unity, chỉ sửa file `.lua` rồi bấm một phím và đọc console. Phần 2 mới nối
> Lua vào MMORPG qua **một lệnh mạng mới, độc lập hoàn toàn** với di chuyển và chiến đấu.
>
> **Kết quả cuối lab:** trong Unity bấm phím `1` → Console in
> `[ItemDebugProbe] Túi Vàng Nhỏ: nhận 350 vàng`. Bấm `2` → `Rương Gỗ: nhận 1 Bình Máu Nhỏ, 100 vàng`.
> Toàn bộ "nhận gì, bao nhiêu, tỉ lệ ra sao" nằm trong file `.lua` trên server. Sửa file → bấm `R`
> trên console server → bấm lại phím `1` trong Unity → **kết quả khác ngay**. Không build lại server,
> không build lại client, không reconnect, không ai bị rớt.
>
> **Cố tình KHÔNG gắn vào hệ nào chưa có.** Không cần túi đồ, không cần máu, không cần sát thương.
> Lab chỉ thêm đúng một lệnh `UseItem` và dừng ở `Debug.Log`. Khi nào dự án có túi đồ thật thì đổi
> **thân một hàm C#**, script Lua không đổi một chữ — mục 5.2 giải thích chỗ đó.
>
> **Điều kiện:** dự án đang chạy được (login → vào world → di chuyển). Phase 10 (map) đang dở không
> ảnh hưởng gì.
>
> Sửa dự án thật, nên **tạo nhánh trước**:
> ```bash
> git checkout -b lua-item
> ```

| Phần | Nội dung | Thời lượng | Cần Unity? |
|---|---|---|---|
| 1 | Học Lua: cú pháp → gọi qua lại với C# → demo công thức sát thương | ~2.5 giờ | ❌ |
| 2 | Lệnh `UseItem`: client → server → Lua → client `Debug.Log` | ~3 giờ | ✅ |
| 3 | Hot reload, nghịch, và an toàn | ~1.5 giờ | ✅ |

Format như các guide phase: **hướng làm** hiện sẵn, **📖 Lời giải** trong foldout — tự code trước rồi
mới mở.

---

## Trước khi bắt đầu: Lua là gì, và vì sao game dùng nó

Lua là một ngôn ngữ lập trình **nhỏ** (~200 KB) được thiết kế để **nhúng vào một chương trình khác**.
Nó không tự chạy thành ứng dụng như C# — nó sống bên trong chương trình chủ, và chương trình chủ quyết
định script được phép làm gì.

Vì sao game server cần nó, gọn trong một bảng:

| Con số `damage = 40` nằm ở đâu | Để đổi thành 35 phải làm gì |
|---|---|
| Code C# của server | sửa → build → **restart server** → *đá hết người đang chơi* |
| File cấu hình server đọc lại được | sửa file → nạp lại → vài giây, không ai rớt |
| Script Lua trên server | sửa file → nạp lại → vài giây, **và luật có `if` cũng đổi được** |

Dòng cuối là toàn bộ lý do. Một game vận hành lâu dài phải chỉnh số mỗi ngày; mỗi lần chỉnh mà tốn
một lần restart thì không sống nổi.

Còn vì sao là **server** chứ không phải client: file `.lua` trên máy người chơi là văn bản thuần, sửa
dễ hơn sửa DLL. Hot-update ở client làm game **dễ hack hơn**, không phải khó hơn. Golden rule #2 của
dự án — server là source of truth — vẫn đúng nguyên.

Trong dự án này, `../vo-lam-genz-server` đang làm đúng như vậy: `GameServer/KiemThe/LuaSystem/` +
~70 file `.lua` trong `bin/Debug/LuaScripts/`. Lab sẽ dựng lại bộ khung đó ở quy mô nhỏ nhất mà vẫn
thật.

---

# PHẦN 1 — Học Lua từ số 0

## Bước 1 — Dựng playground (30 phút)

Mục tiêu: bấm một phím trên console server → nó chạy file `.lua` → in kết quả ra console. Có vòng lặp
đó rồi thì học cú pháp chỉ là sửa file và bấm phím.

**Vì sao MoonSharp:** đó là Lua **viết lại hoàn toàn bằng C#**, cài bằng một gói NuGet, không kéo theo
file native nào (quan trọng khi sau này deploy Linux), có sandbox sẵn. Và đó chính là thứ vo-lam-genz
đang chạy.

Việc của bạn:

- Thêm gói (đường dẫn tính từ **gốc repo**, không phải từ `Server/`):
  ```bash
  dotnet add Server/GameServer/GameServer.csproj package MoonSharp --version 2.0.0
  ```

  <details>
  <summary><b>🔧 Nếu báo "No .NET SDKs were found"</b></summary>

  Không phải sai lệnh — đó là **`dotnet` trên PATH trỏ vào bản chỉ có runtime**. Kiểm hai chỗ:

  ```bash
  where dotnet
  dir "C:\Program Files\dotnet\sdk"
  ```

  Không có thư mục `sdk` ở đó nghĩa là máy chỉ cài .NET **Runtime**. SDK thật thường nằm ở bản cài
  theo user (script `dotnet-install`, hoặc Rider tự tải về):

  ```bash
  dir "%USERPROFILE%\.dotnet\sdk"
  ```

  Có thì chạy tạm bằng đường dẫn đầy đủ:

  ```bash
  "%USERPROFILE%\.dotnet\dotnet.exe" add Server\GameServer\GameServer.csproj package MoonSharp --version 2.0.0
  ```

  Sửa hẳn: chèn bản user lên trước PATH (PowerShell, chạy **một lần**, rồi mở terminal mới):

  ```powershell
  [Environment]::SetEnvironmentVariable('DOTNET_ROOT', "$env:USERPROFILE\.dotnet", 'User'); [Environment]::SetEnvironmentVariable('Path', "$env:USERPROFILE\.dotnet;" + [Environment]::GetEnvironmentVariable('Path','User'), 'User')
  ```

  Kiểm lại bằng `dotnet --list-sdks` — phải thấy một bản **8.x**, vì `Server/global.json` ghim
  `8.0.0` với `rollForward: latestMinor` (nhận 8.x, **không** nhận SDK 10).

  Cách khác, không cần CLI: mở `Server/GameServer/GameServer.csproj` thêm tay
  `<PackageReference Include="MoonSharp" Version="2.0.0" />` vào một `<ItemGroup>` rồi để Rider
  restore. `dotnet add package` chỉ là công cụ sửa hộ đúng dòng XML đó.

  </details>
- Tạo hai thư mục: `Server/GameServer/LuaSystem/` (code C#) và `Server/GameServer/LuaScripts/`
  (file `.lua`).
- Viết `LuaSystem/LuaScriptPaths.cs` — chỗ duy nhất biết file `.lua` nằm ở đâu.
  **Bẫy:** server chạy từ `bin/Debug/net8.0/` nên đường dẫn tương đối không trỏ vào thư mục nguồn.
  Và **đừng** dùng `CopyToOutputDirectory` — làm vậy thì server đọc bản copy trong `bin/`, sửa file
  nguồn không có tác dụng, mà đó lại là toàn bộ điểm của lab. Cách đơn giản nhất: dò ngược lên các
  thư mục cha cho tới khi thấy `LuaScripts`.
- Viết `LuaSystem/LuaPlayground.cs` với một hàm static `RunFile(string)`:
  máy ảo dùng một lần rồi vứt, `print` của Lua nối vào `Log`, và **bắt riêng hai loại exception**
  (`SyntaxErrorException` — sai cú pháp, lộ ngay lúc nạp; `ScriptRuntimeException` — chạy tới dòng đó
  mới nổ). Cả hai có `DecoratedMessage` kèm tên file + số dòng.
- Trong `Program.cs`, thêm phím `L` vào khối `Console.ReadKey` đang có (chỗ phím `H`/`K`/`J` của
  Phase 9).
- Viết `LuaScripts/01_hello.lua` in ra một dòng.

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự làm</b></summary>

**`Server/GameServer/LuaSystem/LuaScriptPaths.cs`**:

```csharp
using MMORPG.ServerCore;

namespace MMORPG.GameServer.LuaSystem
{
    /// <summary>
    /// Chỗ duy nhất biết thư mục script nằm ở đâu. Cố tình đọc thẳng từ thư mục nguồn thay vì bản
    /// copy trong bin/: có vậy sửa file lúc server đang chạy mới có tác dụng.
    /// </summary>
    public static class LuaScriptPaths
    {
        private const string FOLDER_NAME = "LuaScripts";

        private static string _cached;

        /// <summary>Dò ngược từ thư mục chạy (bin/Debug/net8.0) lên tới khi thấy LuaScripts.</summary>
        public static string Dir
        {
            get
            {
                if (_cached != null)
                    return _cached;

                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                while (dir != null)
                {
                    string candidate = Path.Combine(dir.FullName, FOLDER_NAME);
                    if (Directory.Exists(candidate))
                    {
                        _cached = candidate;
                        return _cached;
                    }

                    dir = dir.Parent;
                }

                Log.Error($"Không tìm thấy thư mục {FOLDER_NAME} từ {AppContext.BaseDirectory} trở lên.");
                _cached = AppContext.BaseDirectory;
                return _cached;
            }
        }

        /// <summary>Đọc nội dung một script. Trả chuỗi rỗng nếu không có file — chỗ gọi tự báo lỗi.</summary>
        public static string Read(string relativePath)
        {
            string path = Path.Combine(Dir, relativePath);
            if (!File.Exists(path))
            {
                Log.Error($"Không có file {relativePath} trong {Dir}");
                return string.Empty;
            }

            return File.ReadAllText(path);
        }
    }
}
```

**`Server/GameServer/LuaSystem/LuaPlayground.cs`**:

```csharp
using MMORPG.ServerCore;
using MoonSharp.Interpreter;

namespace MMORPG.GameServer.LuaSystem
{
    /// <summary>
    /// Chỗ chạy thử script khi học. Mỗi lần chạy dựng một máy ảo mới rồi vứt, nên không bao giờ
    /// chạm vào máy ảo đang phục vụ game — console là luồng khác, và máy ảo Lua không an toàn đa luồng.
    /// </summary>
    public static class LuaPlayground
    {
        public static void RunFile(string relativePath)
        {
            string code = LuaScriptPaths.Read(relativePath);
            if (string.IsNullOrEmpty(code))
                return;

            // Preset_SoftSandbox: có sẵn string/math/table/os.time, nhưng đã cắt io, os.execute,
            // require, load — script không đọc/ghi file, không chạy lệnh hệ thống, không nạp thêm mã.
            var script = new Script(CoreModules.Preset_SoftSandbox);

            // print() của Lua mặc định đi thẳng ra stdout, không tag không màu. Nối vào Log.
            script.Options.DebugPrint = message => Log.Info($"{"[lua]".Cyan()} {message}");

            try
            {
                // Tham số thứ 3 là tên hiển thị trong thông báo lỗi. Không truyền thì lỗi ghi
                // "chunk_1:12" và không ai biết chunk_1 là file nào.
                DynValue result = script.DoString(code, null, relativePath);

                if (result.Type != DataType.Void && result.Type != DataType.Nil)
                    Log.Info($"{relativePath} trả về: {result.ToPrintString().Green()}");
            }
            catch (SyntaxErrorException ex)
            {
                // Sai cú pháp: phát hiện ngay lúc nạp, chưa chạy dòng nào.
                Log.Error($"Sai cú pháp trong {relativePath}: {ex.DecoratedMessage.Red()}");
            }
            catch (ScriptRuntimeException ex)
            {
                // Lỗi lúc chạy: gọi hàm không tồn tại, cộng chuỗi với số... chỉ nổ khi tới đúng dòng đó.
                Log.Error($"Lỗi khi chạy {relativePath}: {ex.DecoratedMessage.Red()}");
            }
        }
    }
}
```

**`Server/GameServer/Program.cs`** — thêm `using MMORPG.GameServer.LuaSystem;` và một nhánh vào khối
`Console.ReadKey` đang có:

```csharp
case ConsoleKey.L:
    LuaPlayground.RunFile("01_hello.lua");
    break;
```

**`Server/GameServer/LuaScripts/01_hello.lua`**:

```lua
-- Dòng bắt đầu bằng -- là comment.
print("Xin chào từ Lua!")
print("2 + 3 =", 2 + 3)

return "xong"
```

</details>

**✅ CHECKPOINT 1:** Chạy server, bấm `L`:

```
INFO  [LuaPlayground] [lua] Xin chào từ Lua!
INFO  [LuaPlayground] [lua] 2 + 3 =	5
INFO  [LuaPlayground] 01_hello.lua trả về: xong
```

Tiếng Việt có dấu hiện đúng vì `Program.cs` đã đặt `Console.OutputEncoding = Encoding.UTF8` từ Phase 1,
và `File.ReadAllText` tự nhận UTF-8. Ra một đống `?` hoặc `Ã¬` thì kiểm ba chỗ theo thứ tự: dòng
`OutputEncoding` còn đó không; file `.lua` đã lưu bằng **UTF-8** chưa (Rider/VS Code mặc định đúng,
Notepad cũ thì không); và font của cửa sổ console có phải font Unicode không (Consolas / Cascadia Mono —
Raster Fonts không vẽ được chữ có dấu).

Rồi **cố ý gõ sai**: xoá dấu `)` ở dòng đầu, bấm `L` lại → phải thấy
`ERROR ... Sai cú pháp trong 01_hello.lua:(2,20-21)` và **server vẫn sống**. Sửa lại, bấm `L`, chạy
tiếp. Vòng lặp "sửa file → bấm L → xem log" này là công cụ học của cả Phần 1.

---

## Bước 1.5 — Cho IDE hiểu Lua (15 phút)

Không có bước này thì viết Lua như viết trong Notepad: không màu, không gợi ý, gõ sai tên hàm tới lúc
chạy mới biết.

**Cài plugin.** Rider → Settings → Plugins → Marketplace → **EmmyLua**
(`plugins.jetbrains.com/plugin/9768-emmylua`). Cài được vào mọi IDE JetBrains, không riêng IntelliJ
IDEA. JetBrains không có plugin Lua chính thức nào nên đây là lựa chọn thực tế duy nhất trên Rider.
Restart xong là có tô màu, đi tới định nghĩa, đổi tên biến.

**Nhưng plugin không cứu được chỗ quan trọng nhất.** Lua không có kiểu tĩnh, nên khi tới Bước 5 bạn
viết:

```lua
function WoodenChest:OnUse(ctx)
    ctx:                  -- ← IDE không biết ctx là cái gì, không gợi ý được gì
```

`ctx` là một đối tượng C# do server truyền vào lúc chạy. Cách duy nhất để IDE biết nó có những hàm
nào là **tự khai báo**, bằng chú thích EmmyLua — một file `.lua` chứa các hàm **thân rỗng** chỉ để
IDE đọc, không bao giờ chạy:

```lua
---@class Vidu
local Vidu = {}

--- Mô tả hiện ra khi rê chuột
---@param ten string
---@return number
function Vidu:Demo(ten) end
```

Rồi ở chỗ dùng, gắn kiểu cho tham số:

```lua
---@param ctx LuaUseContext
function WoodenChest:OnUse(ctx)
    ctx:Add            -- ← giờ mới ra AddGold, AddItem
```

Đây không phải mẹo vặt: `../vo-lam-genz-server` có nguyên một bộ khai báo như vậy trong
`GameServer/bin/Debug/LuaScripts/.vscode/jx/` (`Lua_Item.lua`, `Lua_Player.lua`, `Lua_Scene.lua`…),
và `KTLuaScript.Init()` có đúng một dòng `if (path.Contains(".vscode")) continue;` để bỏ qua chúng
khi quét script thật. Mở một file trong đó ra đọc — nó là ví dụ tốt nhất về việc mô tả một API C#
cho người viết script.

**File khai báo cho lab này** viết ở Bước 5, sau khi bề mặt API đã chốt. Ở đây chỉ cần cài plugin và
hiểu ba chú thích `---@class`, `---@param`, `---@return` để lát nữa đọc được.

> **Muốn gợi ý mạnh hơn nữa:** mở riêng thư mục `Server/GameServer/LuaScripts/` bằng VS Code +
> extension **Lua** của sumneko (Lua Language Server) — nó phân tích tốt hơn EmmyLua, và **dùng đúng
> bộ chú thích ấy**, không phải viết lại. Team vo-lam-genz làm vậy: cạnh thư mục `jx/` của họ có
> `settings.json` với `"Lua.workspace.library": [".vscode/jx"]`. Giữ C# ở Rider, Lua ở VS Code là một
> cách chia hợp lý — nhưng chỉ đáng làm khi bạn viết Lua nhiều.

**✅ CHECKPOINT 1.5:** Mở `01_hello.lua` trong Rider → chữ có màu, gõ `pri` ra gợi ý `print`.

---

## Bước 2 — Cú pháp Lua qua bảy mẩu (60 phút)

Đọc từng mẩu, tự viết vào `LuaScripts/02_syntax.lua`, bấm `L` (nhớ đổi tên file trong `Program.cs`
hoặc thêm phím khác). Mỗi mẩu có một câu **"khác C# ở đâu"** — đó là chỗ dễ mắc lỗi nhất, và vì Lua
không có kiểu tĩnh nên mọi lỗi đều chỉ lộ ra lúc chạy.

### 2.1 Biến — và cái bẫy `local`

```lua
local ten = "Hùng"        -- biến cục bộ, chỉ sống trong file/khối này
diem = 100                -- KHÔNG có local => biến TOÀN CỤC của cả máy ảo

print("tên:", ten, "| điểm:", diem)
```

Không khai kiểu, không `int`/`string`/`var` gì cả. **Khác C#:** quên `local` không phải lỗi — nó âm
thầm tạo biến toàn cục. Máy ảo dùng chung cho mọi script, nên một file quên `local` là làm bẩn không
gian của mọi file khác. Đây là **nguồn bug số một** của hệ Lua nhiều file. Quy tắc: *luôn `local`, trừ
khi thật sự muốn chia sẻ.*

### 2.2 Kiểu dữ liệu và "đúng/sai"

```lua
local a = 10          -- number  (Lua không phân biệt int/float; MoonSharp lưu tất cả là double)
local b = "chuỗi"     -- string
local c = true        -- boolean
local d = nil         -- nil = "không có gì", tương đương null

-- type() cho biết kiểu của một giá trị. tostring() cần cho nil vì print bỏ qua giá trị nil đứng cuối.
print("kiểu của a:", type(a), "| b:", type(b), "| c:", type(c), "| d:", tostring(d))
```

**Khác C# — nhớ kỹ:** chỉ `nil` và `false` là "sai". **`0` là đúng. `""` là đúng.**

```lua
local hp = 0
if hp then print("hp = 0 mà vẫn vào được nhánh này! C# thì không.") end
if hp == 0 then print("muốn kiểm tra 0 thì phải so sánh tường minh") end
```

### 2.3 `table` — cấu trúc dữ liệu **duy nhất** của Lua

Không có class, không có struct, không có array riêng, không có List, không có Dictionary. Chỉ có
`table`, và nó làm hết mọi vai.

```lua
-- dùng như MẢNG (chú ý: đếm từ 1, không phải 0)
local tui = { "kiếm", "khiên", "giáp" }
print("phần tử đầu:", tui[1])                 -- kiếm
print("phần tử số 0:", tostring(tui[0]))      -- nil  ← không có phần tử số 0
print("số phần tử:", #tui)                    -- 3    (# là toán tử "độ dài")

-- dùng như OBJECT / DICTIONARY
local item = { id = 1001, ten = "Bình Máu Nhỏ", gia = 50 }
print("tên vật phẩm:", item.ten)              -- Bình Máu Nhỏ
print("giá:", item["gia"])                    -- 50   (hai cách viết tương đương)

-- LỒNG NHAU — đây là cách một bảng cấu hình game trông như thế nào
local phanThuong = {
    { ten = "vàng",  soLuong = 100, tyLe = 60 },
    { ten = "ngọc",  soLuong = 1,   tyLe = 40 },
}
print("phần thưởng đầu tiên:", phanThuong[1].ten, "x", phanThuong[1].soLuong)
print("có mấy loại phần thưởng:", #phanThuong)

-- xoá một khoá = gán nil
item.gia = nil
print("giá sau khi xoá:", tostring(item.gia))  -- nil
```

**Khác C#:** đếm từ **1**; `#t` chỉ đúng khi mảng liền mạch (có lỗ `nil` ở giữa là con số vô nghĩa);
và một table vừa là mảng vừa là dictionary cùng lúc cũng hợp lệ.

### 2.4 Hàm

```lua
local function tinhTong(a, b)
    return a + b
end

print("2 + 3 =", tinhTong(2, 3))     -- 5
-- print(tinhTong(2))                -- LỖI: b là nil, nil + number không cộng được.
                                     -- Bỏ dấu -- ở dòng trên để xem thông báo lỗi trông thế nào.

-- TRẢ VỀ NHIỀU GIÁ TRỊ — chuyện thường ở Lua
local function chiaLayDu(a, b)
    return math.floor(a / b), a % b
end
local thuong, du = chiaLayDu(17, 5)
print("17 chia 5 được", thuong, "dư", du)

-- hàm là GIÁ TRỊ: gán được, truyền được, nhét vào table được
local phepTinh = { cong = tinhTong }
print("gọi hàm nằm trong table:", phepTinh.cong(1, 1))
```

**Khác C#:** không khai kiểu tham số, gọi thiếu tham số **không phải lỗi** — tham số thiếu thành
`nil` và chỉ nổ khi bị dùng tới. Gọi thừa tham số thì phần thừa bị bỏ lặng lẽ.

### 2.5 Điều kiện và vòng lặp

```lua
local hp = 30

if hp <= 0 then
    print("đã chết")
elseif hp < 50 then           -- elseif, viết liền
    print("nguy kịch")
else
    print("còn khoẻ")
end

-- for theo số: from, to, step (to là BAO GỒM, khác C#)
for i = 1, 3 do print("đếm lên:", i) end
for i = 10, 1, -2 do print("đếm ngược:", i) end

-- for theo mảng: ipairs cho phần mảng, pairs cho mọi khoá
for index, giaTri in ipairs(tui) do print("ô", index, "chứa", giaTri) end
for khoa, giaTri in pairs(item) do print("khoá", khoa, "=", giaTri) end

-- while
local n = 0
while n < 3 do n = n + 1 end

-- KHÔNG có continue. MoonSharp cũng không hỗ trợ 'goto'. Phải đảo ngược điều kiện:
for i = 1, 5 do
    if i % 2 == 1 then
        print("số lẻ:", i)
    end
end
```

**Khác C#:** `then`/`do`/`end` thay cho `{ }`; `~=` thay cho `!=`; **không có `++`, không có `+=`**
(phải viết `n = n + 1`); `and` / `or` / `not` thay cho `&&` / `||` / `!`; và `for i = 1, 3` chạy cả
`i = 3`.

### 2.6 Chuỗi

```lua
local ten = "Hùng"
print("Xin chào " .. ten)                            -- .. là nối chuỗi (không phải +)
print("Sát thương: " .. 42)                          -- số tự đổi thành chuỗi
print(("%s gây %d sát thương"):format(ten, 42))      -- format kiểu printf
print(("Rơi ra %s x%d"):format("Ngọc Rồng", 2))
```

Chú ý `("..."):format(...)` — dấu ngoặc quanh chuỗi là **bắt buộc** khi gọi method trực tiếp trên một
chuỗi hằng.

**Bẫy tiếng Việt — đáng biết ngay lúc này:** với Lua, một chuỗi là **một dãy byte**, không phải một
dãy ký tự. Chữ có dấu trong UTF-8 chiếm 2–3 byte, nên:

```lua
print(#"Hung")            -- 4
print(#"Hùng")            -- 5  ← 'ù' chiếm 2 byte
print(("Hùng"):sub(1, 2)) -- "H" + nửa ký tự 'ù' => ra rác
print(("Hùng"):upper())   -- "HùNG" — upper() chỉ biết bảng chữ ASCII
```

Rút ra: **tiếng Việt dùng thoải mái để hiển thị** (nối chuỗi, `format`, in ra) — chỗ nào cũng đúng.
Nhưng `#`, `sub`, `upper`, `lower` thì chỉ tin được với chuỗi thuần ASCII. Trong lab này ta chỉ hiển
thị nên không vướng, còn khi nào cần cắt chuỗi tiếng Việt thì phải làm bên C# rồi truyền sang.

Đó cũng là lý do quy ước của dự án (`CLAUDE.md`) bắt **mọi định danh trong code là tiếng Anh**, tiếng
Việt chỉ dùng cho comment và chuỗi hiển thị: đặt tên biến/khoá table bằng tiếng Việt có dấu là tự chuốc
lấy đúng nhóm bẫy này.

### 2.7 `.` và `:` — mẩu quan trọng nhất

Phần 2 sống chết vì mẩu này, đọc chậm.

```lua
local NhanVat = {}

-- Khai bằng DẤU CHẤM: như static, không có 'self'
function NhanVat.Tao(ten)
    local o = { ten = ten, hp = 100 }
    setmetatable(o, { __index = NhanVat })   -- xem giải thích bên dưới
    return o
end

-- Khai bằng DẤU HAI CHẤM: tự có biến 'self'
function NhanVat:GioiThieu()
    return ("Tôi là %s, máu %d"):format(self.ten, self.hp)
end

local a = NhanVat.Tao("Chiến binh")
print(a:GioiThieu())            -- gọi bằng ':' để truyền 'a' vào self
print(NhanVat.GioiThieu(a))     -- Y HỆT dòng trên, chỉ là viết tường minh
```

Hai điều phải khắc vào đầu:

1. **`function T:M(a, b)` chỉ là đường cú pháp của `function T.M(self, a, b)`.** Dấu `:` không tạo ra
   phép màu gì — nó chỉ thêm một tham số ẩn tên `self` vào đầu. Tương tự, `obj:M(x)` là cách viết
   ngắn của `obj.M(obj, x)`.
2. **`metatable` là cơ chế OOP duy nhất của Lua.** `a` không hề có trường `GioiThieu`; khi Lua tra
   `a.GioiThieu` không thấy, nó hỏi metatable, thấy `__index = NhanVat`, và tra tiếp trong `NhanVat`.
   "Kế thừa" của Lua chỉ có vậy — một con trỏ `__index`.

Hệ quả trực tiếp cho Phần 2: khi **C# gọi một hàm Lua khai bằng `:`**, C# phải **tự tay truyền
`self`**. Quên là mọi tham số lệch một nấc, và lỗi hiện ra ở tận bên trong script dưới dạng
`attempt to index a nil value` — nhìn thông báo đó thì tưởng tham số bị null, đi sửa đúng chỗ không
có lỗi. Đây là lỗi tốn thời gian nhất của người mới nhúng Lua.

### 2.8 Bắt lỗi: `pcall`

```lua
local ok, err = pcall(function()
    error("có chuyện gì đó")
end)
print("chạy ổn không:", ok, "| lỗi:", err)
-- chạy ổn không:	false	| lỗi:	02_syntax.lua:2: có chuyện gì đó
```

Không có `try/catch`. `pcall` (protected call) chạy một hàm và trả về `ok, kết quả-hoặc-lỗi`.

**✅ CHECKPOINT 2:** Chạy được cả 8 mẩu. Ba câu tự hỏi, trả lời được là xong bước này:
`tui[0]` là gì? Vì sao `if 0 then` vào được nhánh trong? `a:GioiThieu()` khác `a.GioiThieu()` chỗ nào?

---

## Bước 3 — C# ↔ Lua: gọi qua lại, và demo sửa công thức sát thương (60 phút)

Học cú pháp xong thì Lua vẫn còn vô dụng: nó phải nói chuyện được với C#. Có đúng hai chiều, và bước
này làm cả hai.

### 3.1 Chiều Lua → C#: cho script gọi hàm của mình

```csharp
var script = new Script(CoreModules.Preset_SoftSandbox);

// Cách 1 — gắn một delegate: Lua gọi được ngay.
script.Globals["Log"] = (Action<string>)(message => Log.Info($"[lua] {message}"));

// Cách 2 — gắn cả một class static: Lua gọi được mọi hàm public của nó.
UserData.RegisterType(typeof(GameLib));   // BẮT BUỘC đăng ký trước
script.Globals["Game"] = typeof(GameLib);
```

Trong Lua:

```lua
Log("script vừa chạy tới đây")
local n = Game.Random(100)
```

**Cạm bẫy C# chắc chắn gặp:** class `static` **không** dùng được `UserData.RegisterType<T>()` — C#
không cho static class làm tham số generic. Phải gọi bản nhận `Type`:

```csharp
UserData.RegisterType(typeof(GameLib));   // ✅
UserData.RegisterType<GameLib>();         // ❌ không biên dịch được
```

### 3.2 Chiều C# → Lua: lấy hàm trong script ra rồi gọi

```csharp
// File .lua return một table chứa các hàm:
//   return { CalcDamage = function(atk, def) ... end }
DynValue module = script.DoString(code, null, "03_damage.lua");

DynValue fn = module.Table.Get("CalcDamage");        // lấy hàm ra
DynValue result = script.Call(fn, 100, 20);         // gọi với 2 tham số

double damage = result.Number;                       // đọc kết quả
```

Ba điều phải biết:

- **`DynValue` là "một giá trị Lua bất kỳ".** Đọc ra kiểu C# bằng `.Number`, `.String`, `.Boolean`,
  `.Table`. Kiểm `result.Type` trước khi tin — `DataType.Number`, `DataType.String`, `DataType.Nil`…
- **Mọi số trong MoonSharp là `double`.** Lua 5.2 không có kiểu integer, nên ép về `int`/`float` là
  việc của phía C#.
- **`Table.Get("khong-ton-tai")` trả `DynValue.Nil`, không ném exception.** Nên gõ sai tên trường là
  một **bug câm**: `.Number` ra `0` và không ai báo gì. Trường nào bắt buộc thì phải tự kiểm
  `Type != DataType.Nil` rồi log lỗi rõ ràng.

### 3.3 Demo: công thức sát thương sửa được mà không build

Đây là bài tập chính của Phần 1, và là ví dụ nhỏ nhất cho thấy vì sao Lua đáng giá.

Công thức sát thương là thứ **planner chỉnh mỗi ngày**: đổi hệ số, thêm ngưỡng, thêm chí mạng. Nếu nó
nằm trong C# thì mỗi lần chỉnh là một lần build + restart. Đưa vào Lua thì:

```
sửa 03_damage.lua  →  bấm L  →  bảng số mới hiện ra ngay
```

Việc của bạn:

- `LuaScripts/03_damage.lua` `return` một table có hàm
  `CalcDamage(attack, defense, isCrit)` trả về **một con số**. Công thức đầu tiên cứ đơn giản:
  `(attack - defense * 0.5)`, chí mạng thì `× 2`, và không bao giờ nhỏ hơn 1.
- Thêm một hàm C# `LuaPlayground.RunDamageDemo()`: nạp file, lấy hàm ra, gọi với **ba bộ số cố định**
  rồi in thành bảng. Bộ số cố định là có chủ đích — nhìn cùng ba dòng đó trước/sau khi sửa công thức
  mới so sánh được.
- Nối vào phím `D` trên console.

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`Server/GameServer/LuaScripts/03_damage.lua`**:

```lua
-- 03_damage.lua — công thức sát thương.
-- Sửa file này rồi bấm D trên console server là thấy số mới ngay, không build lại gì.

local M = {}

local CRIT_MULTIPLIER = 2.0
local DEFENSE_FACTOR  = 0.5
local MIN_DAMAGE      = 1

--- @param attack  number  chỉ số công của người đánh
--- @param defense number  chỉ số phòng của mục tiêu
--- @param isCrit  boolean  có chí mạng không
--- @return number sát thương cuối cùng
function M.CalcDamage(attack, defense, isCrit)
    local damage = attack - defense * DEFENSE_FACTOR

    if isCrit then
        damage = damage * CRIT_MULTIPLIER
    end

    -- Đánh vào mục tiêu phòng ngự cực cao vẫn phải trúng ít nhất 1 — không thì người chơi
    -- nghĩ là game lỗi. Đúng loại luật mà planner muốn tự chỉnh.
    if damage < MIN_DAMAGE then
        damage = MIN_DAMAGE
    end

    return math.floor(damage)
end

return M
```

**`Server/GameServer/LuaSystem/LuaPlayground.cs`** — thêm method:

```csharp
/// <summary>
/// Gọi công thức sát thương viết bằng Lua với ba bộ số cố định. Bộ số cố định là có chủ đích:
/// so sánh cùng ba dòng đó trước và sau khi sửa công thức mới thấy được thay đổi.
/// </summary>
public static void RunDamageDemo()
{
    const string FILE = "03_damage.lua";

    string code = LuaScriptPaths.Read(FILE);
    if (string.IsNullOrEmpty(code))
        return;

    var script = new Script(CoreModules.Preset_SoftSandbox);
    script.Options.DebugPrint = message => Log.Info($"{"[lua]".Cyan()} {message}");

    try
    {
        DynValue module = script.DoString(code, null, FILE);
        if (module.Type != DataType.Table)
        {
            Log.Error($"{FILE} phải return một table.");
            return;
        }

        DynValue function = module.Table.Get("CalcDamage");
        if (function.Type != DataType.Function)
        {
            // Get trên khoá không tồn tại trả Nil chứ không ném lỗi — nên gõ sai tên hàm
            // là một bug hoàn toàn im lặng nếu không kiểm ở đây.
            Log.Error($"{FILE} thiếu hàm CalcDamage.");
            return;
        }

        Log.Info($"{"atk".Yellow()}  {"def".Yellow()}  {"crit".Yellow()}  → sát thương");

        RunCase(script, function, 100, 20, false);
        RunCase(script, function, 100, 20, true);
        RunCase(script, function, 100, 500, false);
    }
    catch (SyntaxErrorException ex)
    {
        Log.Error($"Sai cú pháp trong {FILE}: {ex.DecoratedMessage.Red()}");
    }
    catch (ScriptRuntimeException ex)
    {
        Log.Error($"Lỗi khi chạy {FILE}: {ex.DecoratedMessage.Red()}");
    }
}

private static void RunCase(Script script, DynValue function, int attack, int defense, bool isCrit)
{
    DynValue result = script.Call(function, attack, defense, isCrit);

    // Mọi số trong MoonSharp là double — ép kiểu là việc của phía C#.
    int damage = (int)result.Number;

    Log.Info($"{attack,3}  {defense,3}  {isCrit,-5} → {damage.ToString().Green()}");
}
```

**`Program.cs`**:

```csharp
case ConsoleKey.D:
    LuaPlayground.RunDamageDemo();
    break;
```

</details>

**✅ CHECKPOINT 3 — lần đầu thấy Lua đáng giá.** Bấm `D`:

```
atk  def  crit  → sát thương
100   20  False → 90
100   20  True  → 180
100  500  False → 1
```

Giờ **không tắt server**. Mở `03_damage.lua`, đổi `CRIT_MULTIPLIER = 2.0` thành `3.5`, thêm một luật
mới — ví dụ *"phòng ngự cao hơn công thì chỉ ăn 5% sát thương"*:

```lua
if defense > attack then
    damage = attack * 0.05
end
```

Lưu, bấm `D`:

```
atk  def  crit  → sát thương
100   20  False → 90
100   20  True  → 315
100  500  False → 5
```

**Không biên dịch, không restart.** Đó là toàn bộ ý tưởng, và Phần 2 chỉ là đưa cơ chế này ra tới
người chơi thật.

---

# PHẦN 2 — Ghép vào MMORPG

## Bước 4 — Lệnh mạng `UseItem`: đường ống trước, Lua sau (60 phút)

**Quy tắc của bước này: chưa có Lua gì cả.** Server trả về một câu cố định. Lý do: nếu nối cả đường
ống *và* Lua trong một bước rồi client không thấy gì, bạn sẽ không biết hỏng ở đâu — mạng, DI của
client, hay script. Tách hai bước là tự cho mình một điểm mốc để chia đôi vùng nghi vấn.

Đi theo đúng checklist "thêm một lệnh mạng mới" trong `CLAUDE.md`:

**1. `NetCmd`** — dải inventory là 400–499 (xem `ROADMAP.md` §2), nên `UseItem = 400`. Chỉ cần **một**
giá trị: `NetResult.Ok(...)` trả response về đúng cmd của request.

**2. DTO** trong `Server/Shared/Dto/Inventory/ItemDto.cs`. Ba lớp, tất cả
`[MemoryPackable] public partial class`:

```csharp
UseItemRequest   { int ItemId }
UseItemResponse  { bool Ok, string Message, RewardLine[] Rewards }
RewardLine       { string Name, int Count }
```

Vì sao response có **cả `Message` lẫn `Rewards`**, nghe như trùng: `Message` là câu cho người đọc
("Bạn mở Rương Gỗ và nhận được…"), `Rewards` là **dữ liệu** để sau này UI vẽ icon từng món. Lab dừng
ở `Debug.Log` nên tạm thời in cả hai, nhưng hình dạng dữ liệu thì đúng ngay từ đầu — đổi hình dạng DTO
về sau là đổi giao thức, đắt hơn nhiều.

**3. Build `Shared`** để DLL tự sang Unity (target `CopySharedToUnity` đã có sẵn):
```bash
dotnet build Server/Shared/Shared.csproj
```

**4. Handler server** `Server/GameServer/Handlers/ItemHandler.cs`:
`[TcpHandler(NetCmd.UseItem, MinState = SessionState.InWorld)]`. Bước này trả cứng một
`UseItemResponse`. `MinState = SessionState.InWorld` vì dùng item cần có nhân vật trong world — cùng
mức với `MoveHandler`.

**5. Client** — bốn mảnh, theo đúng khuôn `WorldApi` / `WorldNetHandler` đã có:
- `Assets/Game/Scripts/Inventory/ItemApi.cs` — chiều **gửi**.
- `Assets/Game/Scripts/Network/Handlers/ItemNetHandler.cs` — chiều **nhận**, bắn event.
- `Assets/Game/Scripts/Inventory/ItemDebugProbe.cs` — MonoBehaviour bấm phím `1`/`2`/`3` để gửi, và
  `Debug.Log` kết quả nhận về.
- **Đăng ký vào `GameLifetimeScope`** — đây là dòng dễ quên nhất và **không có lỗi biên dịch**:
  ```csharp
  builder.Register<ItemApi>(Lifetime.Singleton);
  builder.Register<ItemNetHandler>(Lifetime.Singleton).AsSelf().As<INetHandlerGroup>();
  builder.RegisterComponentInHierarchy<ItemDebugProbe>();
  ```
  Thiếu `.As<INetHandlerGroup>()` thì gói server gửi về rơi vào hư không, im lặng. Thiếu
  `RegisterComponentInHierarchy` thì `[Inject]` không bao giờ chạy và field vẫn `null`, cũng im lặng.
- Gắn `ItemDebugProbe` lên một GameObject trong scene `Bootstrap`.

Input: client dùng Input System, nên đọc phím bằng `Keyboard.current.digit1Key.wasPressedThisFrame`
(`using UnityEngine.InputSystem;`) — không cần sửa file `.inputactions`.

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`Server/Shared/Net/NetCmd.cs`** — thêm một region mới sau region World:

```csharp
#region Inventory (400–499)

/// <summary>
/// Dùng một vật phẩm. Hiệu ứng do script Lua trên server quyết định.
/// Request: <see cref="Dto.Inventory.UseItemRequest"/> · Response: <see cref="Dto.Inventory.UseItemResponse"/>
/// Client chủ động gửi.
/// </summary>
UseItem = 400,

#endregion
```

**`Server/Shared/Dto/Inventory/ItemDto.cs`** (file mới):

```csharp
using MemoryPack;

namespace MMORPG.Shared.Dto.Inventory
{
    /// <summary>Client xin dùng một vật phẩm. Chỉ gửi id — mọi thứ khác server tự tra.</summary>
    [MemoryPackable]
    public partial class UseItemRequest
    {
        public int ItemId { get; set; }
    }

    /// <summary>Một dòng phần thưởng. Dữ liệu để UI vẽ được, không phải câu chữ.</summary>
    [MemoryPackable]
    public partial class RewardLine
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    /// <summary>
    /// Kết quả dùng vật phẩm.
    ///
    /// Có cả Message lẫn Rewards là chủ ý: Message là câu cho người đọc (script tự viết, đổi được
    /// nóng), Rewards là dữ liệu có cấu trúc để UI sau này vẽ icon từng món. Lab in cả hai ra
    /// Debug.Log, nhưng hình dạng DTO thì đúng ngay từ đầu — đổi DTO về sau là đổi giao thức.
    /// </summary>
    [MemoryPackable]
    public partial class UseItemResponse
    {
        public bool Ok { get; set; }

        /// <summary>Câu hiển thị cho người chơi. Rỗng khi Ok = false và không có lý do cụ thể.</summary>
        public string Message { get; set; } = string.Empty;

        public RewardLine[] Rewards { get; set; } = System.Array.Empty<RewardLine>();
    }
}
```

**`Server/GameServer/Handlers/ItemHandler.cs`** (bản Bước 4 — trả cứng):

```csharp
using MMORPG.GameServer.Net;
using MMORPG.GameServer.World;
using MMORPG.Shared.Dto.Inventory;
using MMORPG.Shared.Net;

namespace MMORPG.GameServer.Handlers
{
    public static class ItemHandler
    {
        [TcpHandler(NetCmd.UseItem, MinState = SessionState.InWorld)]
        public static Task<NetResult> OnUseItem(NetRequest req)
        {
            var request = req.GetData<UseItemRequest>();
            PlayerEntity entity = req.Session.Entity;

            // MinState đã chặn phần lớn, nhưng LeaveWorld có thể xảy ra giữa lúc gói đang bay.
            if (entity == null)
                return Task.FromResult(NetResult.None);

            var response = new UseItemResponse
            {
                Ok = true,
                Message = $"(tạm) {entity.Name} dùng vật phẩm {request.ItemId}",
                Rewards = new[] { new RewardLine { Name = "vàng", Count = 123 } },
            };

            return Task.FromResult(NetResult.Ok(response));
        }
    }
}
```

**`Assets/Game/Scripts/Inventory/ItemApi.cs`**:

```csharp
using HungNT;
using MMORPG.Client.Network;
using MMORPG.Shared.Dto.Inventory;
using MMORPG.Shared.Net;

namespace MMORPG.Client.Inventory
{
    /// <summary>
    /// Gom mọi lệnh vật phẩm mà client GỬI ĐI. Đối xứng với
    /// <see cref="Network.Handlers.ItemNetHandler"/> ở chiều nhận.
    /// </summary>
    public sealed class ItemApi
    {
        private readonly NetService _netService;

        public ItemApi(NetService netService)
        {
            _netService = netService;
        }

        public void UseItem(int itemId)
        {
            this.Log($"Xin dùng vật phẩm {itemId}");
            _netService.Send(NetCmd.UseItem, new UseItemRequest { ItemId = itemId });
        }
    }
}
```

**`Assets/Game/Scripts/Network/Handlers/ItemNetHandler.cs`**:

```csharp
using System;
using MMORPG.Shared.Dto.Inventory;
using MMORPG.Shared.Net;

namespace MMORPG.Client.Network.Handlers
{
    public class ItemNetHandler : INetHandlerGroup
    {
        public event Action<UseItemResponse> OnUseItemResult;

        [NetHandler(NetCmd.UseItem)]
        private void HandleUseItem(NetPacket packet)
        {
            OnUseItemResult?.Invoke(packet.GetData<UseItemResponse>());
        }
    }
}
```

**`Assets/Game/Scripts/Inventory/ItemDebugProbe.cs`**:

```csharp
using System.Text;
using HungNT;
using MMORPG.Client.Network.Handlers;
using MMORPG.Shared.Dto.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace MMORPG.Client.Inventory
{
    /// <summary>
    /// Bàn thử vật phẩm: phím 1/2/3 gửi lệnh dùng vật phẩm, kết quả in ra Console.
    /// Chưa có UI túi đồ nên đây là toàn bộ "giao diện" của tính năng.
    /// </summary>
    public sealed class ItemDebugProbe : MonoBehaviour
    {
        /// <summary>Id ứng với phím 1, 2, 3 — khớp bảng trong config.lua bên server.</summary>
        [SerializeField] private int[] _itemIds = { 1001, 1002, 1003 };

        private ItemApi _itemApi;
        private ItemNetHandler _itemNetHandler;

        [Inject]
        public void Construct(ItemApi itemApi, ItemNetHandler itemNetHandler)
        {
            _itemApi = itemApi;
            _itemNetHandler = itemNetHandler;
        }

        private void Start()
        {
            _itemNetHandler.OnUseItemResult += OnUseItemResult;
        }

        private void OnDestroy()
        {
            // Construct chưa chạy nếu container build lỗi — đừng để OnDestroy nổ chồng lên lỗi gốc.
            if (_itemNetHandler == null)
                return;

            // ItemNetHandler là singleton, sống lâu hơn scene này.
            _itemNetHandler.OnUseItemResult -= OnUseItemResult;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.digit1Key.wasPressedThisFrame)
                Use(0);
            else if (keyboard.digit2Key.wasPressedThisFrame)
                Use(1);
            else if (keyboard.digit3Key.wasPressedThisFrame)
                Use(2);
        }

        private void Use(int slot)
        {
            if (slot >= _itemIds.Length)
                return;

            _itemApi.UseItem(_itemIds[slot]);
        }

        private void OnUseItemResult(UseItemResponse response)
        {
            if (!response.Ok)
            {
                this.LogWarning($"Không dùng được: {response.Message}");
                return;
            }

            var builder = new StringBuilder(response.Message);

            foreach (RewardLine reward in response.Rewards)
                builder.Append($"\n   + {reward.Count} {reward.Name}");

            this.Log(builder.ToString());
        }
    }
}
```

**`Assets/Game/Scripts/Boot/GameLifetimeScope.cs`** — thêm vào cuối `Configure`:

```csharp
// Inventory, UseItem
builder.Register<ItemApi>(Lifetime.Singleton);
builder.Register<ItemNetHandler>(Lifetime.Singleton).AsSelf().As<INetHandlerGroup>();
builder.RegisterComponentInHierarchy<ItemDebugProbe>();
```

(kèm `using MMORPG.Client.Inventory;`)

</details>

**✅ CHECKPOINT 4:** Chạy DBServer + GameServer, vào game, bấm `1`:

```
[ItemApi] Xin dùng vật phẩm 1001
[ItemDebugProbe] (tạm) Hung dùng vật phẩm 1001
   + 123 vàng
```

Thấy đủ hai dòng nghĩa là **đường ống thông cả hai chiều** và DI của client đã đúng. Chưa có Lua nào
tham gia. Nếu không thấy dòng thứ hai: xem lại ba dòng đăng ký trong `GameLifetimeScope`, và xem
`ItemDebugProbe` đã gắn lên GameObject trong scene `Bootstrap` chưa.

---

## Bước 5 — Cho Lua quyết định (75 phút)

### 5.1 Bề mặt API — quyết định quan trọng nhất

Trước khi viết dòng Lua nào, phải chốt: **script gọi được những gì?** Đây là hợp đồng; khi đã có 20
file script đang chạy thì đổi tên một hàm là gãy hết.

Ba tầng, chép đúng cấu trúc vo-lam-genz:

| Tầng | Lớp | Lua thấy | Vai trò |
|---|---|---|---|
| Miền | `PlayerEntity` | ❌ **không bao giờ** | state thật, hàng chục thành viên |
| Bọc | `LuaUseContext` | ✅ `ctx:AddGold(100)` | lớp mỏng, chỉ hàm script được phép |
| Thư viện | `GameLib` (global `Game`) | ✅ `Game.Random(100)` | tiện ích chung |

Vì sao phải có tầng bọc? Vì `UserData.RegisterType` mở **toàn bộ thành viên public** của kiểu đó cho
script — không chọn lọc từng cái được. Đăng ký thẳng `PlayerEntity` là ngày mai một file `.lua` viết
được `player.MapId = 99` hoặc gọi `player.Integrate(...)`.

Lớp bọc còn là **chỗ neo của hợp đồng**: đổi tên `PlayerEntity.Name` chỉ cần sửa một dòng trong
`LuaUseContext`, mọi script đang chạy không hề hấn gì.

Bề mặt tối thiểu của lab — cố tình nhỏ, thêm thì dễ, bớt thì không:

```
ctx  (LuaUseContext)                    Game  (thư viện)
  :GetItemName()                          .Random(max)        → 1..max
  :GetPlayerName()
  :GetPlayerLevel()                     Item  (thư viện)
  :Say(message)                           .GetClass(name)
  :AddGold(amount)
  :AddItem(name, count)
  :Fail(reason)
```

### 5.2 `LuaUseContext` là một **phiếu kết quả**, không phải cái ví

Chỗ này là mấu chốt để lab không phải chờ hệ túi đồ:

`ctx:AddGold(350)` **không** cộng vàng vào đâu cả. Nó **ghi một dòng vào phiếu**. Script chạy xong,
C# đọc phiếu, đóng thành `UseItemResponse` gửi về client, client `Debug.Log`.

Vì sao đó là thiết kế đúng chứ không phải cắt góc:

- Script viết **y hệt** như khi có túi đồ thật. Nó gọi `ctx:AddGold(350)` và không quan tâm phía sau
  là ghi DB, là in log, hay là gửi mail.
- Ngày dự án có túi đồ thật, bạn sửa **thân hàm `AddGold`** để cộng vào `PlayerEntity` và lưu DB.
  **Không một file `.lua` nào phải sửa.** Đó chính là giá trị của lớp bọc, và bạn đang được thấy nó
  trước khi cần tới nó.
- Nếu bây giờ cho script sửa thẳng state, thì lúc có túi đồ thật bạn sẽ phải đi sửa tất cả script.

### 5.3 Chạy trên luồng nào — bẫy kỹ thuật thật của bước này

`ItemHandler` chạy trên **luồng đọc socket của session**. Mỗi client một luồng riêng, nên **hai người
chơi bấm dùng item cùng lúc là hai luồng cùng gọi vào máy ảo Lua** — mà MoonSharp **không an toàn đa
luồng**. Hỏng ở đây không phải exception mà là bộ nhớ trong của máy ảo bị xoắn, kiểu hỏng không tái
hiện được.

Cách xử trong lab: **một `lock` quanh lời gọi script.** Đúng, đơn giản, và đủ — dùng vật phẩm là hành
động **thưa** (vài lần mỗi giây cho cả server), khác hẳn di chuyển 20 gói/giây/người. Lock không thành
nút cổ chai.

Cách đúng ở quy mô lớn: đẩy lời gọi vào **hàng đợi của luồng tick**, hoặc một **worker riêng có
timeout** — đó là điều `KTLuaScript` bên vo-lam-genz làm (`Channel` + `ExecuteFunctionAsync`). Đổi lại
handler thành bất đồng bộ và phải nghĩ về thứ tự. Biết là có, đừng nhét vào lab.

Reload cũng đi qua **cùng cái lock đó** — nhờ vậy hoán đổi môi trường không bao giờ xảy ra giữa lúc
một script đang chạy.

### 5.4 Việc của bạn

- `LuaSystem/LuaEnvironment.cs` — máy ảo + sandbox + bề mặt API + bảng `className → Table` +
  `CallMethod` **tự truyền `self`**.
- `LuaSystem/LuaUseContext.cs` — phiếu kết quả.
- `LuaSystem/GameLib.cs` — `Random`.
- `LuaSystem/ItemScriptHost.cs` — nạp `config.lua` + các script, `lock`, và `UseItem(entity, itemId)`
  trả về `UseItemResponse`.
- `LuaScripts/config.lua` — bảng vật phẩm: id, tên, script.
- `LuaScripts/Items/GoldPouch.lua`, `LuaScripts/Items/WoodenChest.lua`.
- `LuaScripts/_meta/api.lua` — file khai báo cho IDE (Bước 1.5). Viết nó **ngay sau khi chốt bề mặt
  API ở mục 5.1**, trước khi viết script đầu tiên: có nó thì gõ `ctx:` là ra danh sách hàm, không có
  thì tra ngược sang file C# mỗi lần.
- `Program.cs`: dựng `ItemScriptHost`, gán vào `ItemHandler` (khuôn `SystemHandler.DbClient` đã có),
  và sửa `ItemHandler` gọi nó thay vì trả cứng.

Gợi ý cho `config.lua` — hình dạng nhỏ nhất mà vẫn đủ dùng:

```lua
return {
    items = {
        { id = 1001, name = "Túi Vàng Nhỏ", script = "GoldPouch" },
        { id = 1002, name = "Rương Gỗ",     script = "WoodenChest" },
    },
}
```

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`Server/GameServer/LuaSystem/GameLib.cs`**:

```csharp
namespace MMORPG.GameServer.LuaSystem
{
    /// <summary>
    /// Tiện ích chung cho mọi script, lộ ra dưới tên global "Game".
    /// Đối ứng thu nhỏ của KTLuaLib_Math/KTLuaLib_System bên vo-lam-genz.
    /// </summary>
    public static class GameLib
    {
        private static readonly Random _random = new Random();

        /// <summary>
        /// Số nguyên 1..max. Lua có math.random, nhưng script phải dùng nguồn của server để sau
        /// này còn ghi lại và dựng lại được một phiên chơi khi cần điều tra khiếu nại.
        /// </summary>
        public static int Random(int max)
        {
            if (max <= 1)
                return 1;

            // lock vì hàm này gọi từ luồng của nhiều session; Random của .NET không thread-safe.
            lock (_random)
            {
                return _random.Next(1, max + 1);
            }
        }
    }
}
```

**`Server/GameServer/LuaSystem/LuaUseContext.cs`**:

```csharp
using MMORPG.GameServer.World;
using MMORPG.Shared.Dto.Inventory;

namespace MMORPG.GameServer.LuaSystem
{
    /// <summary>
    /// PHIẾU KẾT QUẢ của một lần dùng vật phẩm, và cũng là thứ duy nhất script Lua nhìn thấy về
    /// người chơi. Đối ứng của Lua_Player + Lua_Item bên vo-lam-genz, gộp lại cho gọn.
    ///
    /// AddGold/AddItem KHÔNG cộng vào đâu cả — chúng ghi một dòng vào phiếu, C# đọc phiếu rồi gói
    /// thành UseItemResponse. Ngày dự án có túi đồ thật thì sửa THÂN hai hàm đó để ghi vào entity
    /// và lưu DB; không một file .lua nào phải sửa. Đó là toàn bộ lý do tồn tại của lớp bọc này.
    ///
    /// Mọi thành viên public ở đây là một cam kết: đã có script gọi thì không đổi tên được nữa.
    /// </summary>
    public sealed class LuaUseContext
    {
        private readonly PlayerEntity _entity;
        private readonly string _itemName;
        private readonly List<RewardLine> _rewards = new();
        private readonly List<string> _messages = new();

        private string _failReason;

        public LuaUseContext(PlayerEntity entity, string itemName)
        {
            _entity = entity;
            _itemName = itemName;
        }

        public string GetItemName()
        {
            return _itemName;
        }

        public string GetPlayerName()
        {
            return _entity.Name;
        }

        public int GetPlayerLevel()
        {
            return _entity.Level;
        }

        /// <summary>Thêm một câu vào thông báo gửi về client.</summary>
        public void Say(string message)
        {
            if (!string.IsNullOrEmpty(message))
                _messages.Add(message);
        }

        public void AddGold(double amount)
        {
            AddItem("vàng", amount);
        }

        public void AddItem(string name, double count)
        {
            // Script là dữ liệu do người khác viết — kiểm ở biên giới, y như kiểm gói tin của client.
            // NaN/Infinity lây qua mọi phép toán, và số âm ở đây là "cấp phần thưởng âm".
            if (string.IsNullOrEmpty(name) || !double.IsFinite(count) || count <= 0)
                return;

            _rewards.Add(new RewardLine { Name = name, Count = (int)count });
        }

        /// <summary>Script tự từ chối: không đủ điều kiện dùng. Ghi nhận lý do và bỏ mọi phần thưởng.</summary>
        public void Fail(string reason)
        {
            _failReason = string.IsNullOrEmpty(reason) ? "Không dùng được vật phẩm này." : reason;
        }

        /// <summary>Đóng phiếu thành gói tin trả về client. Chỉ C# gọi — không lộ sang Lua.</summary>
        internal UseItemResponse ToResponse()
        {
            if (_failReason != null)
                return new UseItemResponse { Ok = false, Message = _failReason };

            return new UseItemResponse
            {
                Ok = true,
                Message = _messages.Count > 0
                    ? string.Join(" ", _messages)
                    : $"Đã dùng {_itemName}.",
                Rewards = _rewards.ToArray(),
            };
        }
    }
}
```

> `ToResponse` là `internal`: cùng assembly C# gọi được, còn MoonSharp chỉ thấy thành viên `public`
> nên script không gọi được. `internal` là công cụ chính để giữ bề mặt API đúng bằng cái mình muốn.

**`Server/GameServer/LuaSystem/LuaEnvironment.cs`**:

```csharp
using MMORPG.ServerCore;
using MoonSharp.Interpreter;

namespace MMORPG.GameServer.LuaSystem
{
    /// <summary>
    /// Một máy ảo Lua trọn vẹn: sandbox, bề mặt API lộ cho script, bảng class đã nạp, và cách gọi
    /// callback từ C#. Đối ứng thu nhỏ của KTLuaEnvironment bên vo-lam-genz.
    ///
    /// Cả đối tượng này là MỘT PHIÊN BẢN của luật chơi: muốn đổi luật thì dựng cái mới rồi hoán
    /// đổi, không sửa cái đang chạy — xem <see cref="ItemScriptHost"/>.
    /// </summary>
    public sealed class LuaEnvironment
    {
        private readonly Script _script;

        /// <summary>Bảng "tên class → table Lua". Script tự đăng ký vào đây qua Item.GetClass.</summary>
        private readonly Dictionary<string, Table> _classes = new();

        public LuaEnvironment()
        {
            _script = new Script(CoreModules.Preset_SoftSandbox);
            _script.Options.DebugPrint = message => Log.Info($"{"[lua]".Cyan()} {message}");

            // Chỉ những kiểu đăng ký ở đây mới qua được biên giới. Đây CHÍNH LÀ bề mặt API.
            UserData.RegisterType<LuaUseContext>();
            // GameLib là class static nên không dùng được bản generic — phải truyền Type.
            UserData.RegisterType(typeof(GameLib));

            // Gán một Type làm global => Lua gọi được hàm static của nó: Game.Random(100)
            _script.Globals["Game"] = typeof(GameLib);

            // "Item" là table thường vì GetClass phải chạm vào state của chính instance này
            // (bảng _classes), nên không làm static như bên server thật được.
            var itemLib = new Table(_script);
            itemLib["GetClass"] = (Func<string, Table>)GetOrCreateClass;
            _script.Globals["Item"] = itemLib;
        }

        /// <summary>
        /// Trả về table của class, tạo mới nếu chưa có. Script gọi hàm này ở dòng đầu tiên rồi gắn
        /// callback vào table nhận được — nên nạp file xong là C# đã có sẵn mọi hàm.
        /// </summary>
        private Table GetOrCreateClass(string className)
        {
            if (_classes.TryGetValue(className, out Table existing))
                return existing;

            var table = new Table(_script);

            // __index trỏ về chính nó: đủ để 'obj:Method()' tìm được hàm nếu script tạo instance
            // từ class này. Đây là toàn bộ "hệ OOP" của Lua — xem mẩu 2.7.
            var meta = new Table(_script);
            meta["__index"] = table;
            table.MetaTable = meta;

            _classes[className] = table;
            return table;
        }

        /// <summary>Nạp và chạy một chunk. Trả về giá trị chunk đó return, hoặc Nil nếu lỗi.</summary>
        public DynValue Load(string relativePath, string code)
        {
            if (string.IsNullOrEmpty(code))
                return DynValue.Nil;

            try
            {
                return _script.DoString(code, null, relativePath);
            }
            catch (SyntaxErrorException ex)
            {
                Log.Error($"Sai cú pháp trong {relativePath}: {ex.DecoratedMessage.Red()}");
            }
            catch (ScriptRuntimeException ex)
            {
                Log.Error($"Lỗi khi nạp {relativePath}: {ex.DecoratedMessage.Red()}");
            }

            return DynValue.Nil;
        }

        public bool HasClass(string className)
        {
            return _classes.ContainsKey(className);
        }

        /// <summary>
        /// Gọi một callback của class. Trả false nếu class/hàm không có hoặc script nổ — một script
        /// hỏng chỉ được phép làm hỏng đúng vật phẩm của nó.
        /// </summary>
        public bool CallMethod(string className, string methodName, params object[] args)
        {
            if (!_classes.TryGetValue(className, out Table classTable))
            {
                Log.Error($"Không có script tên '{className}'.");
                return false;
            }

            DynValue function = classTable.Get(methodName);
            if (function.Type != DataType.Function)
            {
                Log.Error($"'{className}' không có hàm {methodName}.");
                return false;
            }

            // 'function T:M(a)' là đường cú pháp của 'function T.M(self, a)' — xem mẩu 2.7.
            // Tham số đầu BẮT BUỘC là chính table class, không thì mọi tham số lệch một nấc.
            var arguments = new object[args.Length + 1];
            arguments[0] = classTable;
            args.CopyTo(arguments, 1);

            try
            {
                _script.Call(function, arguments);
                return true;
            }
            catch (ScriptRuntimeException ex)
            {
                Log.Error($"Lỗi trong {className}.{methodName}: {ex.DecoratedMessage.Red()}");
                return false;
            }
        }
    }
}
```

**`Server/GameServer/LuaSystem/ItemScriptHost.cs`**:

```csharp
using MMORPG.GameServer.World;
using MMORPG.ServerCore;
using MMORPG.Shared.Dto.Inventory;
using MoonSharp.Interpreter;

namespace MMORPG.GameServer.LuaSystem
{
    /// <summary>
    /// Giữ phiên bản luật vật phẩm đang có hiệu lực và cách thay nó khi server đang chạy.
    /// Đối ứng thu nhỏ của KTLuaScript bên vo-lam-genz.
    /// </summary>
    public sealed class ItemScriptHost
    {
        private const string CONFIG_FILE = "config.lua";
        private const string USE_METHOD = "OnUse";

        /// <summary>Một dòng trong bảng vật phẩm, đọc từ config.lua.</summary>
        private sealed class ItemDef
        {
            public int Id;
            public string Name;
            public string ScriptName;
        }

        /// <summary>
        /// Handler chạy trên luồng đọc socket của từng session, nên nhiều người chơi bấm cùng lúc là
        /// nhiều luồng cùng gọi vào máy ảo — mà MoonSharp KHÔNG an toàn đa luồng. Reload cũng đi qua
        /// đúng cái lock này, nhờ vậy hoán đổi môi trường không xảy ra giữa lúc script đang chạy.
        ///
        /// Lock là đủ vì dùng vật phẩm là hành động THƯA. Nếu sau này có thứ gọi script mỗi tick thì
        /// phải đổi sang hàng đợi/worker, không phải nới lock ra.
        /// </summary>
        private readonly object _gate = new();

        private LuaEnvironment _environment;
        private Dictionary<int, ItemDef> _itemsById = new();

        /// <summary>
        /// Dựng bộ luật mới từ đĩa và chỉ hoán đổi khi chắc chắn nó sống được. Trả false nghĩa là
        /// bản đang chạy vẫn nguyên vẹn — người chơi không hề biết có chuyện gì.
        /// </summary>
        public bool Reload()
        {
            var candidate = new LuaEnvironment();

            DynValue configValue = candidate.Load(CONFIG_FILE, LuaScriptPaths.Read(CONFIG_FILE));
            if (configValue.Type != DataType.Table)
            {
                Log.Error($"{CONFIG_FILE} phải return một table — giữ nguyên bộ luật đang chạy.");
                return false;
            }

            DynValue itemsValue = configValue.Table.Get("items");
            if (itemsValue.Type != DataType.Table)
            {
                Log.Error($"{CONFIG_FILE} thiếu mảng 'items'.");
                return false;
            }

            var items = new Dictionary<int, ItemDef>();

            // Values duyệt phần mảng của table (phần đánh số 1..n).
            foreach (DynValue entry in itemsValue.Table.Values)
            {
                if (entry.Type != DataType.Table)
                    continue;

                Table row = entry.Table;

                // Mọi số trong MoonSharp là double — ép kiểu là việc của phía C#.
                var def = new ItemDef
                {
                    Id = (int)row.Get("id").Number,
                    Name = row.Get("name").String,
                    ScriptName = row.Get("script").String,
                };

                if (def.Id <= 0 || string.IsNullOrEmpty(def.Name) || string.IsNullOrEmpty(def.ScriptName))
                {
                    // Get trên khoá không tồn tại trả Nil chứ không ném lỗi, nên gõ sai tên trường
                    // sẽ là bug câm nếu không kiểm ở đây.
                    Log.Error($"{CONFIG_FILE}: một dòng items thiếu id/name/script.");
                    return false;
                }

                if (!LoadItemScript(candidate, def.ScriptName))
                    return false;

                items[def.Id] = def;
            }

            if (items.Count == 0)
            {
                Log.Error($"{CONFIG_FILE}: bảng items rỗng.");
                return false;
            }

            // Toàn bộ việc hoán đổi nằm trong khối này. Cùng lock với chỗ gọi script, nên không có
            // lời gọi nào đang chạy giữa lúc đổi.
            lock (_gate)
            {
                _environment = candidate;
                _itemsById = items;
            }

            Log.Info($"{"[lua]".Cyan()} Nạp xong {items.Count.ToString().Green()} vật phẩm: " +
                     $"{string.Join(", ", items.Values.Select(i => $"{i.Id} {i.Name}"))}");
            return true;
        }

        /// <summary>Nạp một file script và kiểm nó có thật sự dùng được không.</summary>
        private static bool LoadItemScript(LuaEnvironment environment, string scriptName)
        {
            if (environment.HasClass(scriptName))
                return true;

            string relativePath = Path.Combine("Items", $"{scriptName}.lua");
            environment.Load(relativePath, LuaScriptPaths.Read(relativePath));

            // Nạp xong mà class không xuất hiện = file sai cú pháp, hoặc quên dòng Item.GetClass.
            if (!environment.HasClass(scriptName))
            {
                Log.Error($"{relativePath} không đăng ký được class '{scriptName}' — huỷ đợt nạp.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Người chơi dùng một vật phẩm. Trả về gói tin để handler gửi thẳng về client.
        /// Gọi được từ luồng bất kỳ.
        /// </summary>
        public UseItemResponse UseItem(PlayerEntity entity, int itemId)
        {
            lock (_gate)
            {
                if (_environment == null)
                    return Fail("Hệ vật phẩm chưa sẵn sàng.");

                if (!_itemsById.TryGetValue(itemId, out ItemDef def))
                    return Fail($"Không có vật phẩm {itemId}.");

                var context = new LuaUseContext(entity, def.Name);

                if (!_environment.CallMethod(def.ScriptName, USE_METHOD, context))
                    return Fail($"Script của {def.Name} lỗi — xem log server.");

                return context.ToResponse();
            }
        }

        private static UseItemResponse Fail(string message)
        {
            return new UseItemResponse { Ok = false, Message = message };
        }
    }
}
```

**`Server/GameServer/Handlers/ItemHandler.cs`** — thay phần trả cứng:

```csharp
public static class ItemHandler
{
    /// <summary>Gán một lần lúc khởi động, cùng khuôn với SystemHandler.DbClient.</summary>
    public static ItemScriptHost ScriptHost;

    [TcpHandler(NetCmd.UseItem, MinState = SessionState.InWorld)]
    public static Task<NetResult> OnUseItem(NetRequest req)
    {
        var request = req.GetData<UseItemRequest>();
        PlayerEntity entity = req.Session.Entity;

        if (entity == null)
            return Task.FromResult(NetResult.None);

        UseItemResponse response = ScriptHost.UseItem(entity, request.ItemId);
        return Task.FromResult(NetResult.Ok(response));
    }
}
```

**`Server/GameServer/Program.cs`** — cạnh mấy dòng gán handler đang có:

```csharp
var itemScriptHost = new ItemScriptHost();
itemScriptHost.Reload();
ItemHandler.ScriptHost = itemScriptHost;
```

**`Server/GameServer/LuaScripts/config.lua`**:

```lua
-- config.lua — bảng vật phẩm của server.
-- Thêm một dòng ở đây + một file trong Items/ là có vật phẩm mới, không build lại gì.

return {
    items = {
        { id = 1001, name = "Túi Vàng Nhỏ", script = "GoldPouch" },
        { id = 1002, name = "Rương Gỗ",     script = "WoodenChest" },
    },
}
```

**`Server/GameServer/LuaScripts/Items/GoldPouch.lua`**:

```lua
-- GoldPouch.lua — túi vàng: số vàng nhận được phụ thuộc cấp nhân vật.
-- Đây là thứ KHÔNG diễn đạt được bằng một file cấu hình thuần: nó là một công thức.

local GoldPouch = Item.GetClass("GoldPouch")

local BASE_GOLD      = 100
local GOLD_PER_LEVEL = 50

---@param ctx LuaUseContext
function GoldPouch:OnUse(ctx)
    local level = ctx:GetPlayerLevel()
    local gold = BASE_GOLD + level * GOLD_PER_LEVEL

    ctx:AddGold(gold)
    ctx:Say(("%s mở %s (cấp %d)."):format(ctx:GetPlayerName(), ctx:GetItemName(), level))
end
```

**`Server/GameServer/LuaScripts/Items/WoodenChest.lua`**:

```lua
-- WoodenChest.lua — rương ngẫu nhiên theo tỉ lệ.
-- Bảng quà nằm ngay trong script: đổi tỉ lệ, thêm phần thưởng = sửa file này rồi bấm R.

local WoodenChest = Item.GetClass("WoodenChest")

local MIN_LEVEL = 2

-- Tổng ty_le nên bằng 100 cho dễ nghĩ, nhưng thuật toán dưới không bắt buộc thế.
local REWARDS = {
    { ty_le = 55, ten = "vàng",          so_luong = 100 },
    { ty_le = 30, ten = "Bình Máu Nhỏ",  so_luong = 1   },
    { ty_le = 14, ten = "Đá Cường Hoá",  so_luong = 1   },
    { ty_le = 1,  ten = "Ngọc Rồng",     so_luong = 1   },
}

---@param ctx LuaUseContext
function WoodenChest:OnUse(ctx)
    -- Script tự từ chối được: đây là luật chơi, nên nó thuộc về script chứ không phải C#.
    if ctx:GetPlayerLevel() < MIN_LEVEL then
        ctx:Fail(("Cần đạt cấp %d mới mở được %s."):format(MIN_LEVEL, ctx:GetItemName()))
        return
    end

    local roll = Game.Random(100)
    local moc = 0

    for _, qua in ipairs(REWARDS) do
        moc = moc + qua.ty_le

        if roll <= moc then
            ctx:AddItem(qua.ten, qua.so_luong)
            ctx:Say(("Mở %s, nhận được %s! (roll %d)"):format(ctx:GetItemName(), qua.ten, roll))
            return
        end
    end

    -- Tới đây nghĩa là tổng ty_le < 100 và roll rơi vào khoảng trống. Không im lặng: một phần
    -- thưởng "biến mất" mà không ai biết là loại bug tệ nhất của hệ ngẫu nhiên.
    ctx:Say(("Mở %s nhưng rỗng không (roll %d) — kiểm lại tổng tỉ lệ."):format(ctx:GetItemName(), roll))
end
```

**`Server/GameServer/LuaScripts/_meta/api.lua`** — file khai báo cho IDE (Bước 1.5):

```lua
---@meta
--- File này KHÔNG BAO GIỜ CHẠY. Mọi hàm ở đây thân rỗng, chỉ tồn tại để EmmyLua (Rider) và
--- Lua Language Server (VS Code) biết script được phép gọi những gì.
---
--- ItemScriptHost chỉ nạp đúng những file mà config.lua nhắc tới, nên thư mục _meta không bao
--- giờ được nạp. (vo-lam-genz làm cùng ý nhưng phải lọc tay: KTLuaScript.Init bỏ qua mọi đường
--- dẫn chứa ".vscode".)
---
--- MỖI LẦN THÊM MỘT HÀM VÀO LuaUseContext / GameLib BÊN C# THÌ THÊM MỘT DÒNG Ở ĐÂY.
--- Quên thì không có lỗi gì cả — chỉ là IDE im lặng không gợi ý, và bạn sẽ tưởng hàm đó không tồn tại.

---------------------------------------------------------------------
--- Phiếu kết quả của một lần dùng vật phẩm. C# truyền vào OnUse.
---@class LuaUseContext
local LuaUseContext = {}

--- Tên vật phẩm đang dùng, lấy từ config.lua.
---@return string
function LuaUseContext:GetItemName() end

---@return string
function LuaUseContext:GetPlayerName() end

---@return number
function LuaUseContext:GetPlayerLevel() end

--- Thêm một câu vào thông báo gửi về client. Gọi nhiều lần thì các câu nối lại.
---@param message string
function LuaUseContext:Say(message) end

--- Ghi một dòng phần thưởng "vàng" vào phiếu.
---@param amount number
function LuaUseContext:AddGold(amount) end

--- Ghi một dòng phần thưởng vào phiếu. Số âm, NaN hoặc tên rỗng bị bỏ qua.
---@param name string
---@param count number
function LuaUseContext:AddItem(name, count) end

--- Từ chối: không đủ điều kiện dùng. Bỏ mọi phần thưởng đã ghi.
---@param reason string
function LuaUseContext:Fail(reason) end

---------------------------------------------------------------------
--- Tiện ích chung, global "Game".
---@class GameLib
Game = {}

--- Số nguyên ngẫu nhiên trong [1, max].
---@param max number
---@return number
function Game.Random(max) end

---------------------------------------------------------------------
--- Đăng ký class, global "Item".
---@class ItemLib
Item = {}

--- Lấy (hoặc tạo) bảng class để gắn callback vào. Luôn là dòng đầu của một file script.
---@param name string
---@return table
function Item.GetClass(name) end
```

</details>

> Nếu dùng VS Code cho thư mục `LuaScripts/`, thêm `.vscode/settings.json` để Lua Language Server
> nạp thư mục khai báo — giống hệt cấu hình của vo-lam-genz:
> ```json
> {
>   "Lua.workspace.library": ["_meta"],
>   "Lua.diagnostics.globals": ["Game", "Item"]
> }
> ```
> Rider + EmmyLua thì không cần cấu hình gì: file `_meta/api.lua` nằm trong cây thư mục của project
> nên nó tự index.

**✅ CHECKPOINT 5:** Trong Unity bấm `1` rồi bấm `2`:

```
[ItemDebugProbe] Hung mở Túi Vàng Nhỏ (cấp 1).
   + 150 vàng

[ItemDebugProbe] Không dùng được: Cần đạt cấp 2 mới mở được Rương Gỗ.
```

Toàn bộ hai câu đó do **file `.lua`** viết ra. Đổi `MIN_LEVEL = 2` thành `1`, khởi động lại server,
bấm `2` vài lần → phần thưởng khác nhau theo tỉ lệ. Bước 6 làm cho khỏi phải khởi động lại.

---

# PHẦN 3 — Hot reload, nghịch, và an toàn

## Bước 6 — Sửa file là có hiệu lực ngay (20 phút)

`ItemScriptHost.Reload()` đã viết ở Bước 5 và đã tự lo phần khoá. Giờ chỉ cần một cách gọi nó mà
không tắt server: thêm phím `R` vào khối `Console.ReadKey`.

```csharp
case ConsoleKey.R:
    itemScriptHost.Reload();
    break;
```

Vì sao ở đây gọi thẳng được, trong khi phím `H`/`K` của Phase 9 phải qua hàng đợi? Vì `Reload` **tự
lấy `_gate`**, còn mọi lời gọi script cũng nằm trong `_gate` — nên tự nó đã tuần tự hoá. Còn
`EnqueueForceAll` phải qua hàng đợi vì nó sửa `MoveState` mà luồng tick đang đọc, không có khoá nào ở
giữa.

Đó cũng là bài học chung: **hoặc khoá, hoặc hàng đợi — nhưng phải chọn một, và phải biết mình đang
dùng cái nào.**

**✅ CHECKPOINT 6 — khoảnh khắc chính của cả lab.** Client **đang chạy, đang đăng nhập, không tắt gì**:

1. Bấm `2` trong Unity → thấy kết quả rương.
2. Mở `WoodenChest.lua`, đổi `{ ty_le = 1, ten = "Ngọc Rồng" }` thành `{ ty_le = 100, ten = "Ngọc Rồng" }`
   (và giảm các dòng khác về 0). Lưu.
3. Bấm `R` trên console server → `[lua] Nạp xong 2 vật phẩm`.
4. Bấm `2` trong Unity → **lần nào cũng ra Ngọc Rồng**.

Server không restart, client không reconnect, không build lại một dòng C#. Đó là giá trị bạn đang tìm
hiểu, và giờ bạn đã tự tay làm ra nó.

*(Tuỳ chọn, 20 phút: thêm `FileSystemWatcher` trỏ vào `LuaScriptPaths.Dir` với
`IncludeSubdirectories = true`, sự kiện `Changed` gọi `Reload()`. Hai bẫy: sự kiện bắn 2–3 lần cho một
lần lưu (trình soạn thảo ghi nội dung rồi ghi metadata) — nên phải chống dội bằng cờ hoặc mốc thời
gian; và nó bắn trên luồng của watcher — nhưng `_gate` đã lo chuyện đó. Làm xong thì vòng lặp thành:
`Ctrl+S` → bấm phím trong Unity.)*

## Bước 7 — Nghịch, và thêm vật phẩm mới không build (30 phút)

Mỗi bài chỉ sửa `.lua` + bấm `R`. Làm hết là bạn đã dùng gần hết những gì học ở Bước 2.

**7.1 Rương phụ thuộc cấp.** Trong `WoodenChest.lua`, cho số lượng vàng nhân theo cấp:
`so_luong = 100 * ctx:GetPlayerLevel()`. *(Gợi ý: bảng `REWARDS` khai ở ngoài hàm nên không thấy
`ctx`; phải chuyển việc nhân vào trong `OnUse`.)*

**7.2 Rương may mắn.** 10% cơ hội mở ra **hai** phần thưởng: gọi `ctx:AddItem` hai lần và `ctx:Say`
thêm một câu. Cho thấy `ctx` là *phiếu*, ghi bao nhiêu dòng cũng được.

**7.3 Chuỗi may rủi.** Dùng `Game.Random(100)` hai lần rồi nghĩ xem vì sao **không nên** gọi hai lần
cho cùng một quyết định. *(Đáp: mỗi lần gọi là một con số khác — cần dùng lại thì phải `local roll = Game.Random(100)`.)*

**7.4 Đá Cường Hoá — vật phẩm có tỉ lệ thất bại.** Tạo `Items/UpgradeStone.lua`: 70% thành công
(`ctx:Say("Cường hoá thành công!")`), 30% thất bại (`ctx:Fail("Cường hoá thất bại, vật phẩm đã mất.")`).
Thêm `{ id = 1003, name = "Đá Cường Hoá", script = "UpgradeStone" }` vào `config.lua`, bấm `R`, rồi
bấm phím `3` trong Unity.

**Vật phẩm thứ ba vừa xuất hiện trong game mà bạn không biên dịch gì cả** — id `1003` đã có sẵn trong
`_itemIds` của `ItemDebugProbe` từ Bước 4. Đó là hình dạng thật của công việc vận hành nội dung.

**7.5 Bảng quà chung.** Cả hai rương đều muốn dùng chung một bảng phần thưởng. Với sandbox hiện tại
thì `require` bị cắt, nên cách làm là: khai bảng đó vào một biến **toàn cục** trong một script được
nạp trước. Thử đi, rồi đọc mục 8.5 để biết vì sao **đừng** làm thế và cách đúng là gì.

## Bước 8 — Sống chung với script hỏng (30 phút)

Năm kiểu hỏng. Bốn cái đầu code đã đỡ sẵn — việc của bạn là **thử cho hỏng** rồi đọc lại xem lớp nào
đã cứu.

**8.1 Sai cú pháp / thiếu class.** `Reload` bắt và **không hoán đổi**. Thử: bỏ một chữ `end` trong
`WoodenChest.lua`, bấm `R` → báo lỗi, và bấm `2` trong Unity vẫn ra kết quả của bản cũ.

**8.2 Nạp được nhưng chạy thì nổ.** `ctx:AddGoldd(100)` gõ thừa chữ. `CallMethod` bọc `try/catch` và
trả `false`, nên client nhận `Ok = false` với câu "Script của … lỗi — xem log server". **Một vật phẩm
hỏng, server vẫn chạy, mọi vật phẩm khác vẫn dùng được.** Nguyên tắc: **script không bao giờ được ném
exception xuyên qua biên giới vào code khung.**

**8.3 Số vô nghĩa.** `ctx:AddGold(0/0)` hoặc `ctx:AddGold(-500)`. `LuaUseContext.AddItem` chặn ngay
cửa. Cùng đúng một lý do mà `MoveHandler` kiểm `float.IsFinite` trên gói tin của client: **script cũng
là dữ liệu của người khác**.

**8.4 Sandbox.** Thử `os.execute("calc")` hay `io.open("C:/x.txt", "w")` trong một script → bị chặn.
Không phải vì nghi ngờ người viết script, mà vì **một file luật chạy được trên máy này phải chạy y hệt
trên máy khác** — script đọc file ngoài là mất tính tái lập.

**8.5 Vòng lặp vô hạn, và biến toàn cục.** Hai giới hạn thật, sandbox không đỡ được:

- `while true do end` treo luôn luồng gọi nó. MoonSharp không có bộ đếm lệnh sẵn. Ở lab thì treo một
  session; ở production thì phải chạy script trên **worker có timeout** (vo-lam-genz làm vậy). Thử
  một lần cho biết giới hạn tồn tại.
- Biến toàn cục (bài 7.5) **sống xuyên giữa các script trong cùng máy ảo**. Nghe tiện, nhưng nó là
  cửa hậu: hai script vô tình dùng cùng một tên là ghi đè nhau, và bug hiện ra ở script *khác* với
  script gây lỗi. Cách đúng để chia sẻ dữ liệu: một script **nạp trước** `return` ra một table, C#
  giữ table đó rồi **truyền vào** cho các script khác — nghĩa là chia sẻ đi qua bề mặt API, không đi
  qua không gian toàn cục.

Và một quy tắc âm thầm nhưng quan trọng: **không lưu `DynValue` ra ngoài `LuaEnvironment`.** Giữ một
`Table` của máy ảo cũ là giữ cho cả máy ảo cũ sống mãi sau mỗi lần reload. Code ở trên theo đúng quy
tắc này — để ý `_itemsById` chỉ chứa `int` và `string`, không chứa `Table` nào.

---

## Tự kiểm tra hiểu bài

Tự trả lời từng câu xong mới mở đáp án của câu đó.

**Câu 1.** Bảng vật phẩm chỉ có `id`, `name`, `script`. Nếu chỉ cần *"đổi tên vật phẩm và số vàng"*
thì có cần Lua không?
<details>
<summary><b>📖 Đáp án câu 1</b></summary>

Không. Đó là **dữ liệu thuần** — một file JSON mà server đọc lại được là đủ, và còn tốt hơn: có
schema, kiểm được kiểu, ít cách viết sai.

Lua chỉ đáng khi dữ liệu **là một quyết định**: `100 + level * 50` (`GoldPouch`), tỉ lệ rẽ nhánh
(`WoodenChest`), điều kiện từ chối (`MIN_LEVEL`). Tiêu chí một dòng: *chỉ số và tên → file cấu hình;
có `if` hoặc có phép tính → Lua.*

Dấu hiệu chọn sai: file JSON của bạn mọc ra những trường tên kiểu `conditionType`, `operator`,
`thresholdValue`. Đó là lúc bạn đang tự phát minh một ngôn ngữ lập trình bên trong JSON, rồi sẽ phải
tự viết trình thông dịch cho nó.

</details>

**Câu 2.** `function WoodenChest:OnUse(ctx)` nhận **một** tham số, nhưng `CallMethod` truyền **hai**
giá trị vào `Script.Call`. Vì sao? Quên thì lỗi hiện ra thế nào?
<details>
<summary><b>📖 Đáp án câu 2</b></summary>

Vì dấu `:` chỉ là đường cú pháp: `function T:OnUse(ctx)` biên dịch thành
`function T.OnUse(self, ctx)`. Hàm thật sự có hai tham số; Lua chỉ tự điền `self` khi *gọi* cũng bằng
dấu `:`. C# gọi qua `Script.Call` là gọi hàm trần, không có cú pháp `:` nào — nên phải tự đưa table
class vào vị trí đầu.

Quên thì tham số lệch một nấc: `ctx` nhận **table class**, và dòng đầu tiên chạm tới nó sẽ nổ
`attempt to call a nil value (method 'GetPlayerLevel')`. Nhìn thông báo đó thì tưởng `ctx` truyền vào
bị null, rồi đi sửa đúng chỗ không có lỗi.

</details>

**Câu 3.** `ctx:AddGold(350)` không cộng vàng cho ai cả. Vì sao đó là thiết kế đúng chứ không phải
cắt góc, và ngày có túi đồ thật thì phải sửa ở đâu?
<details>
<summary><b>📖 Đáp án câu 3</b></summary>

Vì script viết **y hệt** như khi có túi đồ thật: nó gọi `ctx:AddGold(350)` và không quan tâm phía sau
là ghi DB, in log, hay gửi mail. Ngày có túi đồ thật, bạn sửa **thân hàm `AddGold`** trong
`LuaUseContext` để cộng vào `PlayerEntity` và lưu qua `DbClient` — **không một file `.lua` nào phải
sửa**.

Ngược lại, nếu bây giờ cho script sửa thẳng state (`player.Gold = player.Gold + 350`), thì lúc có túi
đồ thật bạn phải đi sửa tất cả script, và mỗi script lại tự quyết định có lưu DB hay không, có kiểm
túi đầy hay không.

Đó chính là lý do vo-lam-genz có `Lua_Player` mỏng thay vì đưa `KPlayer` trần cho script: **lớp bọc
là chỗ neo của hợp đồng**, để phần sau nó đổi bao nhiêu lần cũng được.

</details>

**Câu 4.** Vì sao `ItemScriptHost` cần `lock`, trong khi `WorldService` của Phase 9 lại dùng hàng đợi
cho phím `H`/`K`?
<details>
<summary><b>📖 Đáp án câu 4</b></summary>

Vì hai chỗ có hình dạng vấn đề khác nhau.

`ItemHandler` chạy trên **luồng đọc socket của từng session**, nên hai người chơi bấm dùng item cùng
lúc là hai luồng cùng vào máy ảo Lua — mà MoonSharp không an toàn đa luồng. Ở đây không có "một luồng
sở hữu" nào để đẩy việc sang, nên `lock` là câu trả lời trực tiếp và đúng. Nó đủ vì dùng item là hành
động **thưa**.

Phím `H`/`K` thì khác: nó sửa `MoveState` của entity, mà `MoveState` **thuộc về luồng tick** — luồng
tick đọc nó 20 lần/giây và không có khoá nào. Ghi vào đó từ luồng khác thì người đọc có thể thấy nửa
cũ nửa mới. Ở đây đã có sẵn một luồng sở hữu, nên cách rẻ nhất và đúng nhất là **xếp việc vào hàng đợi
cho luồng đó tự làm**, thay vì bắt luồng tick phải lấy khoá 20 lần/giây.

Quy tắc: **có một luồng sở hữu rõ ràng → hàng đợi. Không có → khoá.** Và phải biết mình đang dùng cái
nào, đừng trộn.

</details>

**Câu 5.** Vì sao `Reload()` dựng `LuaEnvironment` mới thay vì `DoString` đè lên máy ảo đang chạy?
<details>
<summary><b>📖 Đáp án câu 5</b></summary>

Vì `DoString` **cộng thêm** vào trạng thái cũ chứ không thay thế. Ba kiểu hỏng:

1. Biến toàn cục cũ còn nguyên — script mới "chạy được" nhờ rác của bản cũ, rồi chết trên máy sạch.
2. Hàm đã xoá khỏi file vẫn tồn tại trong bảng nếu còn ai giữ tham chiếu.
3. Closure cũ tiếp tục dùng biến cũ, trong khi code mới đọc biến mới cùng tên.

Kết quả là bạn debug một trạng thái không ứng với **bất kỳ** phiên bản file nào — nửa cũ nửa mới.
Dựng mới rồi hoán đổi thì "cái đang chạy" luôn đúng bằng "cái đang có trên đĩa".

Và vì bản mới được **kiểm trước khi hoán đổi** (config có ra bảng không, mọi script có đăng ký được
class không), một file `.lua` viết sai không bao giờ tới được tay người chơi.

</details>

**Câu 6.** Sau lab này, hệ nào trong MMORPG nên cho Lua vào tiếp, hệ nào tuyệt đối không?
<details>
<summary><b>📖 Đáp án câu 6</b></summary>

Ba tiêu chí phải thoả **cả ba**: **thưa** (không chạy mỗi tick cho mọi entity), **hay đổi** (nội
dung, không phải cơ chế), và **chỉ server chạy** (client không cần biết luật đó để dự đoán).

| Nên | Không nên |
|---|---|
| hiệu ứng vật phẩm | `MovementRules.Step` |
| hội thoại NPC, nhiệm vụ | `CharacterProfile` (speed, jump, thời lượng đòn) |
| bảng rơi đồ, phần thưởng đăng nhập | đồng bộ state, AOI, tick loop |
| sự kiện theo mùa, lệnh GM | bất cứ thứ gì trong `Shared/` |

Cột phải có một lý do đặc thù của dự án này, và nó quan trọng: **`Server/Shared/` được build thành
`MMORPG.Shared.dll` cho Unity dùng.** Client gọi `MovementRules.Step` và `CharacterProfiles.Get` mỗi
frame để **dự đoán** (Phase 8). Sửa nóng `moveSpeed` ở server thì server chạy `12` còn client dự đoán
bằng `5` → lệch mỗi tick → reconciliation kéo về liên tục → giật, cao su. Không có exception nào, chỉ
là "game tự nhiên chơi rất tệ".

Thêm nữa, reconciliation **replay** hàng chục tick cũ, mà replay chỉ đúng nếu luật không đổi giữa
chừng. Một hàm sửa được lúc chạy thì không replay được.

Đúng bằng danh sách vo-lam-genz để trong `LuaScripts/`: `Item/`, `Npc/`, `Monster/AI`, `GM/`.

</details>

---

## Gỡ lab

Nếu đã làm trên nhánh riêng thì `git checkout main` là xong. Nếu lỡ làm trên `main`:

```bash
git rm -r Server/GameServer/LuaSystem Server/GameServer/LuaScripts \
          Server/GameServer/Handlers/ItemHandler.cs \
          Server/Shared/Dto/Inventory \
          Assets/Game/Scripts/Inventory \
          Assets/Game/Scripts/Network/Handlers/ItemNetHandler.cs
git checkout -- Server/GameServer/GameServer.csproj Server/GameServer/Program.cs \
                Server/Shared/Net/NetCmd.cs \
                Assets/Game/Scripts/Boot/GameLifetimeScope.cs
```

Rồi build lại `Shared` để DLL trong Unity trở về bản không có `Dto.Inventory`, và xoá component
`ItemDebugProbe` khỏi scene `Bootstrap`.

## Đi tiếp

1. **`FileSystemWatcher`** (cuối Bước 6) — vòng lặp `Ctrl+S` → bấm phím trong Unity là thứ dùng hàng
   ngày.
2. **Đọc code thật**: `../vo-lam-genz-server/GameServer/GameServer/KiemThe/LuaSystem/`. Thứ tự:
   `KTLuaEnvironment.cs` → `KTLuaScript.cs` → một file `Logic/KTLuaLib_*.cs` → `bin/Debug/LuaScripts/Item/Common/RandomBox.lua`.
   Bạn vừa dựng lại chính bộ khung đó nên sẽ đọc rất nhanh. Chú ý riêng: `KTLuaScript` nạp **lười**
   theo `ScriptID` thay vì nạp hết lúc khởi động, và nó có worker channel cho mục 8.5.
3. **Khi có túi đồ thật**: sửa thân `LuaUseContext.AddItem`/`AddGold` để ghi vào entity + lưu DB, thêm
   kiểm túi đầy. Script không đổi. Đó là lúc bạn thu hoạch cái đã đầu tư ở mục 5.2.
4. **Khi có chiến đấu thật**: `ctx:Damage(target, amount)` là hàm thư viện tiếp theo, và `OnUse` của
   một chiêu thức trông y như `OnUse` của một vật phẩm.
5. **Sách**: *Programming in Lua* bản 1 miễn phí ở `lua.org/pil` — chương metatable đọc hai lần.
   `moonsharp.org` mục "Compatibility" liệt kê chỗ nó khác Lua chuẩn.
