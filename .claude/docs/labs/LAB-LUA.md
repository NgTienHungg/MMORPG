# LAB — Nhúng Lua: đổi dữ liệu và luật chơi mà không build lại

> **Đây không phải một phase của MMORPG.** Đây là lab học riêng, sống trong đúng một thư mục
> `Assets/_Sandbox/LuaLab/`, có asmdef riêng, không ai trong `Assets/Game/` biết nó tồn tại. Xoá thư
> mục đó là dự án trở về y như cũ.
>
> **Kết quả cuối lab:** một scene Unity trong đó (1) bảng dữ liệu game nằm trong file `.lua`, sửa file
> rồi bấm `R` là số liệu đổi ngay **giữa Play mode**, không biên dịch lại; (2) hành vi của từng vật
> phẩm là một file `.lua` riêng — thêm loại vật phẩm mới **không sửa một dòng C# nào**; (3) một nút
> "Kiểm tra cập nhật" tải script mới từ một thư mục đóng vai CDN về máy, nạp vào, và vật phẩm đổi
> hành vi — đúng mô hình *"đổi mà không cần build hay up lại game"*.
>
> **Bài học chính:** (1) hot-update có **ba mức**, và mức rẻ nhất không cần Lua — nhảy thẳng lên Lua
> khi chỉ cần mức 1 là tự làm khổ mình; (2) thứ quyết định hệ Lua thành hay bại không phải cú pháp
> Lua mà là **bề mặt API bạn mở cho nó** — đó là một hợp đồng, mở sai là gãy hết script về sau;
> (3) trong game online, chỗ đúng của Lua là **server**, không phải client, và vo-lam-genz đúng là
> làm thế.

Format như các guide phase: **hướng làm** hiện sẵn, **📖 Lời giải** trong foldout — tự code trước rồi
mới mở.

---

# PHẦN A — Vì sao có Lua trong game

## A1. Vòng đời của một con số khi không có Lua

Planner nhắn: *"giảm sát thương Cầu Lửa từ 40 xuống 35"*. Một dòng code. Nhưng:

| Nếu con số đó nằm trong… | Để người chơi thấy con số mới, phải làm gì |
|---|---|
| Code client (C#) | sửa → build → QA → nộp store → **chờ Apple duyệt 1–3 ngày** → người chơi tải bản mới → *ai chưa tải thì vẫn 40* |
| Code server (C#) | sửa → build → **restart GameServer** → *toàn bộ người đang chơi bị rớt* |
| File cấu hình tải từ server | sửa file → người chơi vào lại là có → **vài phút** |
| Script Lua trên server | sửa file → nạp lại script → **có ngay, không ai rớt** |

Hai dòng dưới là toàn bộ lý do Lua tồn tại trong ngành game. Không phải vì Lua nhanh hay đẹp, mà vì
**nó tách nhịp thay đổi của luật chơi ra khỏi nhịp phát hành phần mềm**. Game vận hành lâu dài là
game phải chỉnh số mỗi ngày; mỗi lần chỉnh mà tốn một lần phát hành thì không sống nổi.

## A2. Ba mức "đổi mà không build" — và mức nào thật sự cần Lua

Đây là chỗ dễ vung tay quá trán nhất. Có ba mức, độ phức tạp tăng gấp bội mỗi mức:

**Mức 1 — dữ liệu thuần: số, tên, bảng.** `damage = 35`, `dropRate = 0.25`, danh sách phần thưởng
đăng nhập. Giải pháp đúng: **file cấu hình tải từ server / CDN** (JSON, XML, ScriptableObject tải
động). **Không cần Lua.** Ước chừng 80% nhu cầu "đổi mà không build" nằm ở mức này.

**Mức 2 — dữ liệu có nhánh và công thức riêng cho từng thứ.** *"Bình máu hồi 50 HP"* là mức 1.
*"Bình máu hồi 50 HP, nhưng dưới 30% máu thì hồi gấp đôi, và không dùng được khi đang giao chiến"*
là mức 2. Bạn có hai đường: hoặc đẻ thêm 15 trường vào JSON (`healBonusThreshold`,
`healBonusMultiplier`, `forbidInCombat`…) và mỗi vật phẩm lạ lại thêm trường, hoặc để **hành vi là
một hàm** — và đó là Lua.

**Mức 3 — vá logic đã ship.** Bug trong code C# của bản đã lên store. Cần **hotfix**: xLua/ToLua
(thay thân hàm C# bằng Lua lúc chạy) hoặc HybridCLR (nạp assembly C# dạng thông dịch). Đắt, phức
tạp, và chỉ đáng làm khi vòng phát hành thật sự đau.

> **Dấu hiệu bạn đang ở mức 2 và cần Lua:** file JSON của bạn bắt đầu mọc ra những trường tên kiểu
> `conditionType`, `operator`, `thresholdValue`. Đó là lúc bạn đang tự phát minh một ngôn ngữ lập
> trình bên trong JSON, và sớm muộn sẽ phải tự viết trình thông dịch cho nó — trong khi có sẵn một
> cái tên là Lua, 200 KB, đã chạy 30 năm.

## A3. vo-lam-genz làm chuyện này như thế nào (có dẫn chứng)

Điều bạn nghe được là thật, và nó nằm ở **GameServer chứ không phải client**. Trong
`../vo-lam-genz-server`:

```
GameServer/KiemThe/LuaSystem/
├── KTLuaEnvironment.cs          ← dựng môi trường, gắn thư viện C# vào Lua
├── KTLuaScript.cs               ← quét thư mục .lua, nạp lười, chạy qua hàng đợi worker
└── Logic/KTLuaLib_*.cs          ← 15 file: Player, Item, Dialog, Timer, GUI, Math...

GameServer/bin/Debug/LuaScripts/  ← thư mục script chạy thật, ~70 file .lua
└── Item/Common/RandomBox.lua
```

Bốn mảnh ghép, và lab này sẽ dựng lại đúng bốn mảnh đó ở quy mô nhỏ:

**(1) Một máy ảo MoonSharp duy nhất, gắn các lớp static C# thành biến global của Lua** —
`KTLuaEnvironment.Init()`:

```csharp
LuaEnv.Globals["Player"] = typeof(KTLuaLib_Player);
LuaEnv.Globals["Item"]   = typeof(KTLuaLib_Class);
LuaEnv.Globals["Dialog"] = typeof(KTLuaLib_Dialog);
LuaEnv.Globals["Timer"]  = typeof(KTLuaLib_Timer);
```

Bề mặt API mở cho Lua chỉ gồm những lớp `KTLuaLib_*` này — **không phải toàn bộ codebase**. Đây là
quyết định thiết kế quan trọng nhất của cả hệ.

**(2) Mỗi vật phẩm "có script" trỏ tới một file `.lua` bằng `ScriptID`.** Item nào có
`Genre = 18 (item_script)` và `ScriptID != -1` thì lúc người chơi bấm dùng, server không chạy code
C# nào của riêng nó cả — nó đi tìm script.

**(3) File script khai báo một "class" rồi cài các hàm callback vào đó** —
`LuaScripts/Item/Common/RandomBox.lua`, chép nguyên văn:

```lua
local RandomBox = Item.GetClass("RandomBox")

function RandomBox:OnPreCheckCondition(scene, item, player, otherParams)
  return true
end

function RandomBox:OnUse(scene, item, player, otherParams)
  local boxName = item:GetName()

  if not Player.OpenRandomBox(player, item:GetID()) then
    return
  end

  Player.RemoveItem(player, item:GetID())
  player:AddNotification("Mở " .. boxName .. ", nhận được phần thưởng ngẫu nhiên!")
end
```

Để ý hai kiểu gọi cùng tồn tại — chúng sẽ quay lại ở Bước 3 của lab:
`player:AddNotification(...)` là **method trên đối tượng bọc**, còn `Player.RemoveItem(player, id)`
là **hàm thư viện** nhận đối tượng làm tham số.

**(4) C# gọi ngược vào script, và truyền vào đối tượng *bọc* chứ không phải đối tượng game trần** —
`KTLuaEnvironment_Methods.ExecuteItemScript_OnUse`:

```csharp
Lua_Item luaItem = new() { RefObject = itemGD, CurrentScene = luaScene };
Lua_Player luaPlayer = new() { RefObject = player, CurrentScene = luaScene };

KTLuaScript.ExecuteFunctionAsync(LuaEnv, scriptID, "OnUse",
    new object[] { luaScene, luaItem, luaPlayer, ReverseKey(otherParams) }, ...);
```

`KPlayer` — lớp người chơi thật với hàng trăm thành viên — **không bao giờ lộ sang Lua**. Cái lộ ra
là `Lua_Player`, một lớp mỏng chỉ có đúng những hàm script được phép gọi.

**Thành quả:** thêm một loại Hộp Thái Cực mới = thêm một dòng trong file cấu hình item + (nếu là
loại hành vi mới) một file `.lua`. **Không build lại GameServer, không đụng client, không ai bị rớt.**
Đó chính xác là câu bạn nghe được.

Và một ranh giới phải nhớ, vì nó là golden rule #2 của dự án này: hệ Lua ấy nằm ở **server**. Client
chỉ nhận kết quả. Nếu đặt luật ở client thì dù có Lua hay không, người chơi vẫn sửa được — hot-update
không cứu được một kiến trúc tin client.

## A4. Cái giá — nói ra được thì mới là hiểu

| Được | Mất |
|---|---|
| Đổi luật không cần build | **Mất kiểm tra kiểu lúc biên dịch.** Gõ nhầm tên hàm thì phải chạy đúng nhánh đó mới biết |
| Người không phải lập trình viên cũng sửa được số | Sai một dấu chấm là im lặng trả `nil`, không nổ ngay |
| Nội dung mới không tốn một lần phát hành | Chậm hơn C# ~10–100 lần; và mỗi lần qua lại C#↔Lua đều tốn |
| Cô lập được lỗi (một script hỏng ≠ sập server) | Debug khó: stack trace hai tầng, IDE không nhảy được vào |
| | **Bề mặt API thành hợp đồng vĩnh viễn** — đổi tên một hàm trong `KTLuaLib_Player` là gãy hết script đang chạy |
| | Ở client mobile IL2CPP còn thêm chuyện AOT/reflection |

Câu tóm lại: **Lua không làm code dễ hơn, nó làm việc phát hành dễ hơn.** Đổi lấy một phần an toàn
lúc biên dịch để mua tốc độ vận hành.

---

# PHẦN B — LAB

Lab dựng lại đúng bốn mảnh ghép ở mục A3, nhưng trong Unity cho dễ bấm Play. **Mô hình giống hệt
khi bê sang server** — cùng thư viện MoonSharp, cùng cách gắn global, cùng cách gọi callback; chỉ
khác chỗ ngồi.

## Những file sẽ có

```
Assets/_Sandbox/LuaLab/
├── LuaLab.asmdef
├── Plugins/
│   └── MoonSharp.Interpreter.dll
├── Scripts/
│   ├── PlayerState.cs          ← đối tượng game thật, KHÔNG lộ sang Lua
│   ├── LuaPlayer.cs            ← lớp bọc mỏng, đây mới là thứ Lua thấy
│   ├── PlayerLib.cs            ← thư viện hàm cho Lua gọi (đối ứng KTLuaLib_Player)
│   ├── LuaEnvironment.cs       ← máy ảo + sandbox + đăng ký global (đối ứng KTLuaEnvironment)
│   ├── LuaScriptStore.cs       ← script lấy từ đâu: dev / bản tải về / bản đóng gói
│   ├── LuaUpdater.cs           ← tải manifest + script mới từ "CDN"
│   └── LuaLabRunner.cs         ← MonoBehaviour trong scene, phím tắt điều khiển
├── Resources/LuaBundled/       ← bản đóng gói theo build (.lua.txt để Unity nhận là TextAsset)
│   ├── config.lua.txt
│   ├── HealPotion.lua.txt
│   └── RandomBox.lua.txt
├── LuaScripts/                 ← bản dev, sửa trực tiếp khi đang chạy trong Editor
│   ├── config.lua
│   ├── HealPotion.lua
│   └── RandomBox.lua
├── RemoteRoot~/                ← "CDN" giả lập; Unity bỏ qua mọi thư mục kết thúc bằng ~
│   ├── manifest.json
│   └── (các .lua phiên bản mới)
└── Scenes/
    └── LuaLab.unity
```

---

## Bước 0 — Setup: đưa MoonSharp vào Unity (25 phút)

### 0.1 Chọn thư viện

| | MoonSharp | xLua / ToLua |
|---|---|---|
| Là gì | Lua **viết lại hoàn toàn bằng C#** | bọc thư viện Lua gốc viết bằng C |
| Cài | thả 1 file DLL | import package + chạy generate code binding |
| Tốc độ | chậm hơn | nhanh hơn nhiều |
| IL2CPP / iOS | cần cẩn thận reflection | có sinh code sẵn, đã chinh chiến nhiều |
| Hotfix code C# đã ship | không | **có** (`[Hotfix]`) |
| Dùng ở | server .NET, tool, prototype | client mobile production |

Lab dùng **MoonSharp** vì đúng ba lý do: setup 2 phút, chạy được cả trong .NET server lẫn Unity, và
**đó chính là thứ vo-lam-genz đang dùng**. Học xong hiểu nguyên lý thì chuyển sang xLua chỉ là đổi
cú pháp binding.

### 0.2 Ba quyết định cách ly

**Quyết định 1 — lab có asmdef riêng, dù `Assets/Game/` thì không.**
`CLAUDE.md` cấm asmdef cho `Assets/Game/` vì code game phải với tới DOTween Pro nằm trong
`Assembly-CSharp-firstpass`. Lab không cần DOTween, mà lại rất cần điều ngược lại: **cách ly**.
Không asmdef thì file lab rơi vào `Assembly-CSharp` chung với toàn bộ game — một lỗi cú pháp lúc học
sẽ làm cả dự án không biên dịch được.

**Quyết định 2 — MoonSharp đặt trong thư mục lab, không cài qua NuGetForUnity.**
NuGetForUnity ghi vào `Assets/packages.config` và `Assets/Packages/` — tức chạm vào dự án chính.
Thả thẳng DLL vào `Assets/_Sandbox/LuaLab/Plugins/` thì xoá thư mục lab là sạch trơn.

**Quyết định 3 — tắt Auto Reference của DLL.**
Mặc định mọi DLL trong `Assets/` được `Assembly-CSharp` tự tham chiếu, nghĩa là code game *nhìn thấy*
MoonSharp. Tắt đi thì chỉ assembly nào khai báo tường minh mới dùng được.

### 0.3 Việc của bạn

- Lấy DLL: vào `https://www.nuget.org/packages/MoonSharp` → Download package → đổi đuôi `.nupkg`
  thành `.zip` → giải nén → trong `lib/` chọn `netstandard2.0` (không có thì `net40`) → chép
  `MoonSharp.Interpreter.dll` vào `Assets/_Sandbox/LuaLab/Plugins/`.
  *(Cách khác: `../vo-lam-genz/Assets/Plugins/MoonSharp/` có sẵn bản mã nguồn — nhưng chép nguyên
  thư mục source vào sẽ kéo theo cả debugger, nặng hơn cần thiết. Dùng DLL.)*
- Chọn DLL trong Unity → Inspector → **bỏ tick Auto Reference** → Apply.
- Tạo `LuaLab.asmdef` ở gốc thư mục lab. Ba trường dễ quên: `overrideReferences: true` và
  `precompiledReferences: ["MoonSharp.Interpreter.dll"]` (bắt buộc vì vừa tắt Auto Reference), và
  `autoReferenced: false` (để code game không thấy lab).
- Tạo scene rỗng `Scenes/LuaLab.unity`.
- Viết một MonoBehaviour tạm 5 dòng chạy thử `new Script().DoString("return 1+1")` để chắc chắn DLL
  đã nạp được.

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự làm</b></summary>

**`Assets/_Sandbox/LuaLab/LuaLab.asmdef`**:

```json
{
    "name": "LuaLab",
    "rootNamespace": "LuaLab",
    "references": [
        "HungNT.Core"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "MoonSharp.Interpreter.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

Bản chạy thử:

```csharp
using HungNT;
using MoonSharp.Interpreter;
using UnityEngine;

namespace LuaLab
{
    public sealed class LuaSmokeTest : MonoBehaviour
    {
        private void Start()
        {
            DynValue result = new Script().DoString("return 1 + 1");
            this.Log($"Lua nói 1 + 1 = {result.Number}");
        }
    }
}
```

</details>

**✅ CHECKPOINT 0:** Console in `[LuaSmokeTest] Lua nói 1 + 1 = 2`. Rồi mở một file bất kỳ trong
`Assets/Game/` gõ `MoonSharp.` — IDE **không** gợi ý gì. Đó là bằng chứng lab đã cách ly.

---

## Bước 1 — Viết Lua: chín chỗ nó khác C# (45 phút)

Lua nhỏ đến mức cả ngôn ngữ vừa một buổi chiều. Chín chỗ dưới đây là nơi người quen C# mắc lỗi, và
tất cả đều là lỗi **chạy mới biết** vì Lua không có kiểu tĩnh:

1. **Biến mặc định là global.** Quên `local` là ghi vào không gian chung của cả máy ảo — mà máy ảo
   thì dùng chung cho mọi script. Đây là nguồn bug số một của hệ Lua nhiều file.
2. **`table` là cấu trúc dữ liệu *duy nhất*.** Mảng, dictionary, object, module, namespace — tất cả
   là table. Không có class, không có struct.
3. **Đánh số từ 1.** `t[1]` là phần tử đầu. `#t` chỉ đúng khi mảng liền mạch không có lỗ `nil`.
4. **Chỉ `nil` và `false` là falsy.** `0` là **true**, `""` là **true**. `if hp then` với `hp = 0`
   vẫn chạy vào trong.
5. **Xoá phần tử = gán `nil`.**
6. **Toán tử lạ:** nối chuỗi là `..`, khác là `~=`, không có `++`, không có `+=`.
7. **Trả về nhiều giá trị:** `local a, b = f()`. Thiếu thì bù `nil`, thừa thì cắt — không báo lỗi.
8. **Hàm là giá trị,** và có hai cách gắn vào table: `function T.f(x)` (như static) vs
   `function T:f(x)` (tự có `self`, như method). **Nhớ kỹ chỗ này** — Bước 3 sống chết vì nó.
9. **Metatable là cơ chế OOP duy nhất.** `__index` trỏ tới table khác chính là "kế thừa".

Thêm hai điều: **không có `continue`** (Lua 5.2+ có `goto ::label::`, nhưng MoonSharp không hỗ trợ
`goto` — phải đảo ngược điều kiện), và **`pcall`** là cách duy nhất bắt lỗi. Cũng là bài học đầu về
ngôn ngữ nhúng: **"Lua" của mỗi chương trình là một phương ngữ hơi khác nhau**; luôn đọc phần
"differences from standard Lua" của bản mình đang dùng trước khi trách mình viết sai.

Việc của bạn: viết `00_basics.lua` tự chứng minh 9 điều trên bằng `print`, và chạy nó. Chưa cần
`LuaScriptStore` — đọc file bằng `File.ReadAllText` với đường dẫn thẳng cũng được, Bước 4 sẽ thay.

Một chi tiết nhỏ nhưng phải làm ngay từ giờ: nối `print` của Lua vào Console của Unity qua
`script.Options.DebugPrint`, và bắt riêng **hai** loại exception — `SyntaxErrorException` (sai cú
pháp, lộ ngay lúc nạp) và `ScriptRuntimeException` (chạy tới dòng đó mới nổ). Cả hai đều có
`DecoratedMessage` chứa tên file + số dòng. Không làm việc này thì mọi lỗi Lua sẽ hiện ra dưới dạng
exception C# đỏ chóe không ai đọc được.

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`Scripts/00_basics.lua`** (đặt tạm trong `LuaScripts/`):

```lua
-- 00_basics.lua — chín chỗ Lua khác C#, tự chứng minh bằng print.

print("=== 1. local vs global ===")
diem = 10                 -- không có 'local' => biến toàn cục của cả máy ảo
local diemCucBo = 20
print("global thi moi script deu thay:", diem, "| local thi khong:", diemCucBo)

print("=== 2 & 3. table la tat ca, va dem tu 1 ===")
local mang = { "kiem", "khien", "giap" }
print("phan tu dau:", mang[1], "| mang[0] la:", tostring(mang[0]), "| #mang =", #mang)

local tuDien = { hp = 100, mp = 50 }
print("truy cap kieu object:", tuDien.hp, "| kieu key:", tuDien["mp"])

mang.chuSoHuu = "Hung"    -- cùng một table vừa là mảng vừa là dictionary
print("#mang van la", #mang, "vi key chu khong tinh vao do dai")

print("=== 4. chi nil va false la falsy ===")
local hp = 0
if hp then print("hp = 0 nhung van vao nhanh nay! (khac C#)") end
if hp == 0 then print("muon kiem tra 0 thi phai so sanh tuong minh") end

print("=== 5. xoa = gan nil ===")
tuDien.mp = nil
print("sau khi gan nil, tuDien.mp =", tostring(tuDien.mp))

print("=== 6. toan tu ===")
print("noi chuoi bang '..':", "sat thuong " .. 42)
print("khac nhau dung '~=':", 1 ~= 2)

print("=== 7. tra ve nhieu gia tri ===")
local function chiaLayDu(a, b)
    return math.floor(a / b), a % b
end
local thuong, du = chiaLayDu(17, 5)
print("17 / 5 =", thuong, "du", du)

print("=== 8. dot vs colon — CHO NAY QUAN TRONG NHAT ===")
local Vatpham = {}
function Vatpham.Tao(ten)           -- dau '.' : nhu static
    return setmetatable({ ten = ten }, { __index = Vatpham })
end
function Vatpham:MoTa()             -- dau ':' : tu co bien 'self'
    return "Vat pham: " .. self.ten
end
-- 'function T:f()' chi la duong cu phap cua 'function T.f(self)'.
-- Nen goi tu C# phai TU TAY truyen table lam tham so dau tien.

print("=== 9. metatable = ke thua ===")
local binh = Vatpham.Tao("Binh mau nho")
print(binh:MoTa())                  -- goi bang ':' de truyen 'binh' vao self
print("binh khong he co truong MoTa; Lua khong tim thay nen hoi metatable.__index")

print("=== bonus: khong co continue ===")
-- MoonSharp khong ho tro 'goto', nen phai dao nguoc dieu kien.
local soLe = ""
for i = 1, 5 do
    if i % 2 == 1 then
        soLe = soLe .. i .. " "
    end
end
print("so le:", soLe)

print("=== bonus: pcall de bat loi ===")
local ok, err = pcall(function() error("co chuyen gi do") end)
print("pcall tra ve:", ok, err)

return "00_basics chay xong"
```

Bản chạy tạm (sẽ bị `LuaEnvironment` ở Bước 3 thay thế):

```csharp
using System.IO;
using HungNT;
using MoonSharp.Interpreter;
using UnityEngine;

namespace LuaLab
{
    public sealed class LuaBasicsRunner : MonoBehaviour
    {
        private void Start()
        {
            string path = Path.Combine(Application.dataPath, "_Sandbox/LuaLab/LuaScripts/00_basics.lua");
            var script = new Script(CoreModules.Preset_SoftSandbox);

            // print() của Lua mặc định đi ra stdout — Unity không thấy. Nối vào Console.
            script.Options.DebugPrint = message => this.Log(message);

            try
            {
                // Tham số thứ 3 là tên hiển thị trong thông báo lỗi; không truyền thì lỗi ghi
                // "chunk_1:12" và không ai biết chunk_1 là file nào.
                DynValue result = script.DoString(File.ReadAllText(path), null, "00_basics.lua");
                this.Log($"Lua tra ve: {result.ToPrintString()}");
            }
            catch (SyntaxErrorException ex)
            {
                // Sai cú pháp: lộ ngay lúc nạp, chưa chạy dòng nào.
                this.LogError($"Sai cu phap: {ex.DecoratedMessage}");
            }
            catch (ScriptRuntimeException ex)
            {
                // Lỗi lúc chạy: gọi nil, cộng chuỗi với số... chỉ nổ khi tới đúng dòng đó.
                this.LogError($"Loi khi chay: {ex.DecoratedMessage}");
            }
        }
    }
}
```

</details>

**✅ CHECKPOINT 1:** Console in đủ 9 mục. Kiểm ba chỗ dễ sốc nhất: `mang[0]` là `nil`, `hp = 0` vẫn
vào nhánh `if hp then`, `binh:MoTa()` chạy được dù `binh` không có trường đó. Rồi cố ý gõ sai một
chữ trong file `.lua` → Console phải in `Sai cu phap: 00_basics.lua:(12,4-5)...` chứ **không** văng
exception C#.

---

## Bước 2 — Lua làm bảng dữ liệu, và hot-reload (45 phút)

Đây là mức 1½ của mục A2: dữ liệu vẫn chủ yếu là số, nhưng đã được viết bằng Lua để chuẩn bị cho
mức 2. Mục tiêu của bước này là **cảm nhận được vòng lặp sửa-thấy-ngay**.

`config.lua` `return` một table:

```lua
return {
    version = 1,
    dropRate = 0.25,
    dailyReward = { gold = 100, exp = 50 },
    items = {
        { id = 1001, name = "Bình máu nhỏ",     script = "HealPotion", value = 50 },
        { id = 1002, name = "Rương ngẫu nhiên", script = "RandomBox" },
    },
}
```

Ba điều phải nắm khi đọc table Lua từ C#:

- **Mọi số trong MoonSharp đều là `double`.** Lua 5.2 không có kiểu integer, nên `DynValue.Number`
  luôn là `double`; ép về `int`/`float` là việc của bạn.
- **`table.Pairs` duyệt mọi cặp key/value; `table.Values` duyệt phần mảng.** Với `items` (mảng) thì
  dùng `Values`; với table dạng object thì dùng `Get("tên")`.
- **`Get` trên key không tồn tại trả về `DynValue.Nil`, không ném exception.** Nên `skill.Get("gia")
  .Number` sẽ ra `0` chứ không báo lỗi — sai chính tả tên trường là bug câm. Nếu trường bắt buộc thì
  phải tự kiểm `Type != DataType.Nil` và báo lỗi rõ ràng.

Việc của bạn: viết `config.lua`, một class `GameConfig` bên C# đọc nó ra, và trong `LuaLabRunner`
thêm phím `R` để **dựng lại máy ảo và nạp lại**.

Quyết định thiết kế của phím `R`: **vứt cả máy ảo cũ đi, tạo máy ảo mới**, không chạy đè `DoString`
lên máy ảo đang sống. Chạy đè thì biến global cũ còn nguyên, hàm bị xoá trong file mới vẫn tồn tại
trong bộ nhớ — bạn sẽ debug một trạng thái nửa cũ nửa mới. (Hệ hot-update sản phẩm thật *có* vá tại
chỗ để giữ state người chơi, và đó chính là lý do chúng khó.)

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`LuaScripts/config.lua`**: như đoạn trên.

**`Scripts/GameConfig.cs`**:

```csharp
using System.Collections.Generic;
using HungNT;
using MoonSharp.Interpreter;

namespace LuaLab
{
    /// <summary>Một dòng trong bảng vật phẩm, đọc từ config.lua.</summary>
    public sealed class ItemDef
    {
        public int Id;
        public string Name;
        public string ScriptName;   // tên file .lua chứa hành vi; rỗng = vật phẩm thường
        public int Value;
    }

    /// <summary>Ảnh chụp toàn bộ cấu hình game tại một thời điểm, dựng từ table Lua.</summary>
    public sealed class GameConfig
    {
        public int Version;
        public float DropRate;
        public int DailyGold;
        public int DailyExp;
        public readonly List<ItemDef> Items = new List<ItemDef>();

        /// <summary>Dựng cấu hình từ giá trị mà config.lua return. Trả null nếu dữ liệu không hợp lệ.</summary>
        public static GameConfig FromLua(DynValue value)
        {
            if (value.Type != DataType.Table)
            {
                DebugEx.LogError("[GameConfig] config.lua phải return một table.");
                return null;
            }

            Table root = value.Table;
            var config = new GameConfig
            {
                // Mọi số trong MoonSharp là double — ép kiểu là việc của phía C#.
                Version = (int)root.Get("version").Number,
                DropRate = (float)root.Get("dropRate").Number,
            };

            DynValue daily = root.Get("dailyReward");
            if (daily.Type == DataType.Table)
            {
                config.DailyGold = (int)daily.Table.Get("gold").Number;
                config.DailyExp = (int)daily.Table.Get("exp").Number;
            }

            DynValue items = root.Get("items");
            if (items.Type != DataType.Table)
            {
                DebugEx.LogError("[GameConfig] config.lua thiếu bảng 'items'.");
                return config;
            }

            // Values duyệt phần mảng của table (phần đánh số 1..n).
            foreach (DynValue entry in items.Table.Values)
            {
                if (entry.Type != DataType.Table)
                {
                    continue;
                }

                Table row = entry.Table;
                config.Items.Add(new ItemDef
                {
                    Id = (int)row.Get("id").Number,
                    Name = row.Get("name").String,
                    // Get trên key không tồn tại trả Nil chứ không ném lỗi — nên .String ra null,
                    // và sai chính tả tên trường sẽ là một bug hoàn toàn im lặng.
                    ScriptName = row.Get("script").String,
                    Value = (int)row.Get("value").Number,
                });
            }

            return config;
        }
    }
}
```

Phần thêm vào `LuaLabRunner` (bản đầy đủ ở Bước 3):

```csharp
private void Update()
{
    if (Input.GetKeyDown(KeyCode.R))
    {
        this.Log("Nạp lại toàn bộ script...");
        Reload();
    }
}
```

</details>

**✅ CHECKPOINT 2:** Play → Console in ra version, drop rate và 2 vật phẩm. **Không thoát Play mode**,
mở `config.lua`, đổi `dropRate = 0.25` thành `0.9` và thêm một vật phẩm thứ ba, lưu, quay lại Unity
bấm `R` → số mới hiện ra. Không domain reload, không biên dịch lại. Đây là bản thu nhỏ của thứ đang
chạy trên server vo-lam-genz.

---

## Bước 3 — Hành vi cũng là script: dựng lại khuôn của vo-lam-genz (90 phút)

Giờ mới tới mức 2. Yêu cầu nghiệp vụ:

- **Bình máu nhỏ**: hồi 50 HP; nếu máu dưới 30% thì hồi gấp đôi; máu đầy thì không cho dùng.
- **Rương ngẫu nhiên**: mở ra một trong ba phần thưởng theo tỉ lệ, rồi tự trừ chính nó.

Thử nhét vào JSON đi rồi sẽ thấy vì sao phải là Lua.

### 3.1 Bề mặt API — quyết định quan trọng nhất của cả hệ

Trước khi viết dòng Lua nào, phải chốt: **script được phép gọi những gì?** Đây là hợp đồng, và một
khi có 50 file script đang chạy thì không đổi được nữa.

Chép đúng cấu trúc của vo-lam-genz, ba tầng:

| Tầng | Lớp | Lua thấy | Vai trò |
|---|---|---|---|
| Miền | `PlayerState` | ❌ **không bao giờ** | trạng thái thật: HP, vàng, túi đồ |
| Bọc | `LuaPlayer` | ✅ `player:GetHp()` | lớp mỏng, chỉ có hàm script được phép |
| Thư viện | `PlayerLib` | ✅ `Player.Heal(player, 50)` | thao tác cần kiểm tra/ghi log/đụng hệ thống khác |

Vì sao phải có tầng bọc mà không đưa thẳng `PlayerState` cho Lua? Vì `UserData.RegisterType<T>()`
mở **toàn bộ thành viên public** của `T` cho script. Đưa lớp thật ra là ngày mai ai đó viết
`player.Hp = 99999` trong một file `.lua` mà không qua kiểm tra nào. Lớp bọc biến "mọi thứ public"
thành "đúng những gì tôi cho phép" — và đó cũng chính là lý do vo-lam-genz có
`Lua_Player`/`Lua_Item` chứ không đưa `KPlayer` trần.

Vì sao lại có **hai** kiểu gọi (`player:GetHp()` và `Player.Heal(player, ...)`)? Quy ước thực dụng:
**đọc thì gọi method trên đối tượng; thao tác có hệ quả thì gọi hàm thư viện.** Nhìn vào script là
biết ngay dòng nào chỉ xem, dòng nào làm thay đổi thế giới.

### 3.2 Cái bẫy `:` và `self`

`function RandomBox:OnUse(item, player)` chỉ là đường cú pháp của
`function RandomBox.OnUse(self, item, player)`. Nghĩa là khi C# gọi hàm này, nó **phải tự tay truyền
table class làm tham số đầu tiên**:

```csharp
_script.Call(fn, classTable, luaItem, luaPlayer);
//               ^^^^^^^^^^ chính là 'self'
```

Quên nó thì `item` nhận nhầm giá trị của `self`, `player` nhận nhầm `item`, và lỗi hiện ra ở tận
dòng nào đó bên trong script dưới dạng "attempt to index a userdata value" — mất nửa buổi.

### 3.3 Việc của bạn

- `PlayerState.cs`, `LuaPlayer.cs`, `PlayerLib.cs` theo bảng ba tầng ở trên.
- `LuaEnvironment.cs` — đối ứng của `KTLuaEnvironment`: dựng `Script` sandbox, `UserData.RegisterType`
  cho các lớp lộ ra, gắn global `Player` và `Item`, quản lý bảng `className → Table`, và có hàm
  `CallMethod(className, methodName, args)` tự truyền `self`.
  `Item.GetClass(name)` bên C# **tạo mới table nếu chưa có** rồi trả về — để script chỉ việc
  `local X = Item.GetClass("X")` rồi gắn hàm vào.
- `HealPotion.lua` và `RandomBox.lua`.
- `LuaLabRunner`: phím `1` dùng bình máu, `2` mở rương, `R` nạp lại.

Phép thử cuối cùng của bước này: **thêm loại vật phẩm thứ ba mà không mở Visual Studio.**

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`Scripts/PlayerState.cs`**:

```csharp
using System.Collections.Generic;

namespace LuaLab
{
    /// <summary>Trạng thái người chơi thật. Lớp này KHÔNG được đăng ký với MoonSharp.</summary>
    public sealed class PlayerState
    {
        public string Name = "Hùng";
        public int Hp = 30;
        public int MaxHp = 100;
        public int Gold = 0;
        public readonly List<string> Bag = new List<string>();

        public float HpRatio
        {
            get { return MaxHp <= 0 ? 0f : (float)Hp / MaxHp; }
        }
    }
}
```

**`Scripts/LuaPlayer.cs`**:

```csharp
using HungNT;

namespace LuaLab
{
    /// <summary>
    /// Lớp bọc mỏng quanh PlayerState — đây là thứ duy nhất script Lua nhìn thấy.
    /// Mọi thành viên public ở đây là một cam kết: đã có script gọi thì không đổi tên được nữa.
    /// </summary>
    public sealed class LuaPlayer
    {
        private readonly PlayerState _state;

        public LuaPlayer(PlayerState state)
        {
            _state = state;
        }

        /// <summary>Trạng thái thật, chỉ cho C# trong cùng assembly — internal nên Lua không thấy.</summary>
        internal PlayerState State
        {
            get { return _state; }
        }

        public string GetName()
        {
            return _state.Name;
        }

        public int GetHp()
        {
            return _state.Hp;
        }

        public int GetMaxHp()
        {
            return _state.MaxHp;
        }

        /// <summary>Tỉ lệ máu 0..1 — để script khỏi phải tự chia và tự lo chia cho 0.</summary>
        public float GetHpRatio()
        {
            return _state.HpRatio;
        }

        public void AddNotification(string message)
        {
            this.Log($"<color=#7fd67f>[Người chơi] {message}</color>");
        }
    }
}
```

**`Scripts/PlayerLib.cs`**:

```csharp
using HungNT;

namespace LuaLab
{
    /// <summary>
    /// Thư viện hàm cho Lua gọi, lộ ra dưới tên global "Player" — đối ứng của KTLuaLib_Player
    /// bên vo-lam-genz. Mọi thao tác làm thay đổi thế giới đều đi qua đây để còn kiểm tra và ghi log.
    /// </summary>
    public static class PlayerLib
    {
        /// <summary>Hồi máu. Trả false khi không hồi được, để script tự quyết định làm gì tiếp.</summary>
        public static bool Heal(LuaPlayer player, int amount)
        {
            if (player == null || amount <= 0)
            {
                return false;
            }

            PlayerState state = player.State;
            if (state.Hp >= state.MaxHp)
            {
                return false;
            }

            int before = state.Hp;
            state.Hp = UnityEngine.Mathf.Min(state.MaxHp, state.Hp + amount);
            DebugEx.Log($"[PlayerLib] Hồi máu {before} → {state.Hp}");
            return true;
        }

        public static void AddGold(LuaPlayer player, int amount)
        {
            player.State.Gold += amount;
            DebugEx.Log($"[PlayerLib] Vàng +{amount} = {player.State.Gold}");
        }

        public static void AddItem(LuaPlayer player, string itemName)
        {
            player.State.Bag.Add(itemName);
            DebugEx.Log($"[PlayerLib] Nhận {itemName} (túi có {player.State.Bag.Count} món)");
        }

        /// <summary>Số ngẫu nhiên 1..max. Lua có math.random, nhưng bản server phải dùng RNG chung
        /// để còn replay được — nên tập thói quen gọi qua thư viện ngay từ đầu.</summary>
        public static int Random(int max)
        {
            return UnityEngine.Random.Range(1, max + 1);
        }
    }
}
```

**`Scripts/LuaEnvironment.cs`**:

```csharp
using System.Collections.Generic;
using HungNT;
using MoonSharp.Interpreter;

namespace LuaLab
{
    /// <summary>
    /// Máy ảo Lua của lab: sandbox, bề mặt API lộ cho script, bảng class đã nạp, và cách gọi
    /// callback từ C#. Đối ứng thu nhỏ của KTLuaEnvironment + KTLuaScript bên vo-lam-genz.
    /// </summary>
    public sealed class LuaEnvironment
    {
        private readonly Script _script;

        /// <summary>Bảng "tên class → table Lua". Script tự đăng ký vào đây qua Item.GetClass.</summary>
        private readonly Dictionary<string, Table> _classes = new Dictionary<string, Table>();

        public LuaEnvironment()
        {
            // SoftSandbox đã cắt io, phần lớn os, require, load, dofile: script không mở được file,
            // không chạy được lệnh hệ thống. Xem Bước 5 để hiểu vì sao đây là mặc định đúng.
            _script = new Script(CoreModules.Preset_SoftSandbox);
            _script.Options.DebugPrint = message => DebugEx.Log($"[Lua] {message}");

            // Đăng ký kiểu C# mà script được phép cầm. Chỉ những kiểu ở đây mới qua được biên giới.
            UserData.RegisterType<LuaPlayer>();
            UserData.RegisterType<PlayerLib>();

            // Gán một Type làm global => Lua gọi được các hàm static của nó: Player.Heal(...)
            _script.Globals["Player"] = typeof(PlayerLib);

            // "Item" là một table thường có đúng một hàm, vì GetClass cần chạm vào state của
            // instance này (bảng _classes) nên không làm static được như bên server.
            var itemLib = new Table(_script);
            itemLib["GetClass"] = (System.Func<string, Table>)GetOrCreateClass;
            _script.Globals["Item"] = itemLib;
        }

        /// <summary>
        /// Trả về table của class, tạo mới nếu chưa có. Script gọi hàm này ở dòng đầu tiên rồi gắn
        /// các callback vào table nhận được — nên sau khi nạp file xong, C# đã có sẵn mọi hàm.
        /// </summary>
        private Table GetOrCreateClass(string className)
        {
            if (_classes.TryGetValue(className, out Table existing))
            {
                return existing;
            }

            var table = new Table(_script);

            // __index trỏ về chính nó: đủ để 'obj:Method()' tìm được hàm nếu sau này script tạo
            // instance từ class. Đây là toàn bộ "hệ OOP" của Lua.
            var meta = new Table(_script);
            meta["__index"] = table;
            table.MetaTable = meta;

            _classes[className] = table;
            return table;
        }

        /// <summary>Nạp và chạy một chunk Lua. Trả về giá trị chunk đó return.</summary>
        public DynValue Load(string chunkName, string code)
        {
            try
            {
                return _script.DoString(code, null, chunkName);
            }
            catch (SyntaxErrorException ex)
            {
                DebugEx.LogError($"[LuaEnvironment] Sai cú pháp trong {chunkName}: {ex.DecoratedMessage}");
            }
            catch (ScriptRuntimeException ex)
            {
                DebugEx.LogError($"[LuaEnvironment] Lỗi khi nạp {chunkName}: {ex.DecoratedMessage}");
            }

            return DynValue.Nil;
        }

        public bool HasClass(string className)
        {
            return _classes.ContainsKey(className);
        }

        /// <summary>
        /// Tạo một table thuộc về máy ảo này. Table phải có chủ thì mới truyền qua biên giới được —
        /// MoonSharp kiểm tra quyền sở hữu để không cho giá trị của máy ảo này lọt sang máy ảo khác.
        /// </summary>
        public Table NewTable()
        {
            return new Table(_script);
        }

        /// <summary>
        /// Gọi một callback của class. Trả về Nil nếu class/hàm không tồn tại hoặc script nổ —
        /// một script hỏng không được phép làm hỏng cả hệ thống.
        /// </summary>
        public DynValue CallMethod(string className, string methodName, params object[] args)
        {
            if (!_classes.TryGetValue(className, out Table classTable))
            {
                DebugEx.LogError($"[LuaEnvironment] Không có script tên '{className}'.");
                return DynValue.Nil;
            }

            DynValue function = classTable.Get(methodName);
            if (function.Type != DataType.Function)
            {
                DebugEx.LogWarning($"[LuaEnvironment] '{className}' không có hàm {methodName}.");
                return DynValue.Nil;
            }

            // 'function T:M(a, b)' là đường cú pháp của 'function T.M(self, a, b)'.
            // Nên tham số đầu tiên BẮT BUỘC là chính table class, không thì mọi tham số lệch một nấc.
            var arguments = new object[args.Length + 1];
            arguments[0] = classTable;
            args.CopyTo(arguments, 1);

            try
            {
                return _script.Call(function, arguments);
            }
            catch (ScriptRuntimeException ex)
            {
                DebugEx.LogError($"[LuaEnvironment] Lỗi trong {className}.{methodName}: {ex.DecoratedMessage}");
                return DynValue.Nil;
            }
        }
    }
}
```

**`LuaScripts/HealPotion.lua`**:

```lua
-- HealPotion.lua — hành vi của bình máu.
-- Đây là thứ KHÔNG diễn đạt được bằng JSON: có điều kiện, có nhánh, có thông báo khác nhau.

local HealPotion = Item.GetClass("HealPotion")

-- Trả false thì C# sẽ không gọi OnUse. Tách riêng phần kiểm tra giúp UI làm mờ nút được
-- mà không phải chạy thử hành vi.
function HealPotion:OnPreCheckCondition(item, player)
    if player:GetHp() >= player:GetMaxHp() then
        player:AddNotification("Máu đang đầy, không cần dùng " .. item.name .. ".")
        return false
    end
    return true
end

function HealPotion:OnUse(item, player)
    local amount = item.value

    -- Luật "dưới 30% máu thì hồi gấp đôi": ba dòng này là toàn bộ lý do tồn tại của Lua ở đây.
    if player:GetHpRatio() < 0.3 then
        amount = amount * 2
        player:AddNotification("Nguy kịch! " .. item.name .. " phát huy gấp đôi công dụng.")
    end

    if not Player.Heal(player, amount) then
        return
    end

    player:AddNotification(("Dùng %s, hồi %d HP. Còn %d/%d.")
        :format(item.name, amount, player:GetHp(), player:GetMaxHp()))
end
```

**`LuaScripts/RandomBox.lua`** (viết theo đúng khuôn file thật của vo-lam-genz):

```lua
-- RandomBox.lua — rương quà ngẫu nhiên.
-- Bảng quà nằm ngay trong script: thêm phần thưởng mới = sửa file này, không build lại.

local RandomBox = Item.GetClass("RandomBox")

local PHAN_THUONG = {
    { ty_le = 60, loai = "gold", so_luong = 100, ten = "100 vàng" },
    { ty_le = 30, loai = "item", ten = "Bình máu nhỏ" },
    { ty_le = 10, loai = "gold", so_luong = 1000, ten = "1000 vàng" },
}

function RandomBox:OnPreCheckCondition(item, player)
    return true
end

function RandomBox:OnUse(item, player)
    local roll = Player.Random(100)
    local moc = 0

    for _, qua in ipairs(PHAN_THUONG) do
        moc = moc + qua.ty_le
        if roll <= moc then
            if qua.loai == "gold" then
                Player.AddGold(player, qua.so_luong)
            else
                Player.AddItem(player, qua.ten)
            end
            player:AddNotification(("Mở %s, nhận được %s! (roll %d)")
                :format(item.name, qua.ten, roll))
            return
        end
    end
end
```

**`Scripts/LuaLabRunner.cs`** (bản Bước 3):

```csharp
using System.Collections.Generic;
using HungNT;
using MoonSharp.Interpreter;
using UnityEngine;

namespace LuaLab
{
    /// <summary>
    /// Scene test của lab. Phím 1/2 dùng vật phẩm, R nạp lại toàn bộ script.
    /// </summary>
    public sealed class LuaLabRunner : MonoBehaviour
    {
        private LuaEnvironment _environment;
        private GameConfig _config;
        private PlayerState _state;
        private LuaPlayer _luaPlayer;

        private void Start()
        {
            _state = new PlayerState();
            _luaPlayer = new LuaPlayer(_state);
            Reload();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                Reload();
            }
            else if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                UseItem(1001);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                UseItem(1002);
            }
        }

        private void Reload()
        {
            // Vứt máy ảo cũ, dựng máy ảo mới. Chạy đè lên máy ảo cũ sẽ để lại global và hàm của
            // bản script trước — trạng thái nửa cũ nửa mới là thứ không debug nổi.
            _environment = new LuaEnvironment();

            _config = GameConfig.FromLua(_environment.Load("config.lua", LuaScriptStore.Read("config.lua")));
            if (_config == null)
            {
                return;
            }

            // Nạp mọi file hành vi mà bảng vật phẩm có nhắc tới. Nạp xong là các class đã nằm
            // trong LuaEnvironment, sẵn sàng để CallMethod.
            var loaded = new HashSet<string>();
            foreach (ItemDef item in _config.Items)
            {
                if (string.IsNullOrEmpty(item.ScriptName) || !loaded.Add(item.ScriptName))
                {
                    continue;
                }

                string fileName = $"{item.ScriptName}.lua";
                _environment.Load(fileName, LuaScriptStore.Read(fileName));
            }

            this.Log($"Đã nạp config v{_config.Version}: {_config.Items.Count} vật phẩm, " +
                     $"{loaded.Count} script hành vi. Máu {_state.Hp}/{_state.MaxHp}.");
        }

        private void UseItem(int itemId)
        {
            ItemDef def = _config?.Items.Find(i => i.Id == itemId);
            if (def == null)
            {
                this.LogWarning($"Không có vật phẩm {itemId} trong config.");
                return;
            }

            if (string.IsNullOrEmpty(def.ScriptName) || !_environment.HasClass(def.ScriptName))
            {
                this.LogWarning($"{def.Name} không gắn script nào.");
                return;
            }

            // Đưa dữ liệu vật phẩm sang Lua dưới dạng table, không phải userdata: script chỉ cần
            // đọc mấy trường, mà table thì đúng chất Lua hơn và không mở thêm bề mặt API nào.
            Table itemTable = _environment.NewTable();
            itemTable["id"] = def.Id;
            itemTable["name"] = def.Name;
            itemTable["value"] = def.Value;

            DynValue canUse = _environment.CallMethod(def.ScriptName, "OnPreCheckCondition",
                itemTable, _luaPlayer);
            if (canUse.Type == DataType.Boolean && !canUse.Boolean)
            {
                return;
            }

            _environment.CallMethod(def.ScriptName, "OnUse", itemTable, _luaPlayer);
        }
    }
}
```

> Table phải được tạo **từ chính máy ảo sẽ nhận nó** (`_environment.NewTable()`). MoonSharp kiểm tra
> quyền sở hữu khi giá trị đi qua biên giới; `new Table(null)` hoặc table của một `Script` khác sẽ
> bị từ chối ngay lúc gọi.

</details>

**✅ CHECKPOINT 3:** Play. Máu bắt đầu 30/100 (dưới 30%) → bấm `1` → thấy *"Nguy kịch!"* và hồi 100.
Bấm `1` lần nữa → *"Máu đang đầy"*. Bấm `2` vài lần → phần thưởng khác nhau theo tỉ lệ.

Rồi **phép thử thật sự**: thêm vật phẩm thứ ba mà **không mở Visual Studio**.
1. Tạo `LuaScripts/GoldPouch.lua`: `local GoldPouch = Item.GetClass("GoldPouch")` + hàm `OnUse` gọi
   `Player.AddGold(player, item.value)`.
2. Thêm một dòng vào `items` trong `config.lua`: `{ id = 1003, name = "Túi vàng", script = "GoldPouch", value = 500 }`.
3. Bấm `R`, rồi thêm phím `3` gọi `UseItem(1003)`… — à mà không, cả cái đó cũng phải sửa C#. Đó
   chính là bài học: **ranh giới của hot-update nằm ở chỗ bạn vẽ nó.** Bảng vật phẩm và hành vi thì
   đổi được nóng; còn "phím nào gọi hàm gì" là code khung, vẫn phải build. Hệ thật giải chuyện này
   bằng cách để **UI cũng lấy danh sách từ config** — thử đổi `LuaLabRunner` để phím `1..9` ánh xạ
   theo thứ tự trong `_config.Items` xem, rồi vật phẩm mới sẽ tự có phím mà không cần sửa gì thêm.

---

## Bước 4 — Cập nhật từ xa: "không cần build hay up lại game" (75 phút)

Ba bước trên vẫn phải **sửa file trong dự án**. Bây giờ mới tới phần thật: người chơi đã cài game
rồi, script mới đến với họ bằng cách nào.

### 4.1 Ba nguồn script, thứ tự ưu tiên

Script trong một game đã phát hành có thể đến từ ba nơi, và `LuaScriptStore` là chỗ **duy nhất**
biết luật ưu tiên giữa chúng:

| Ưu tiên | Nguồn | Ở đâu | Có trong bản build? |
|---|---|---|---|
| 1 | **Thư mục dev** | `Assets/_Sandbox/LuaLab/LuaScripts/` | ❌ chỉ Editor, bọc `#if UNITY_EDITOR` |
| 2 | **Bản đã tải về** | `Application.persistentDataPath/lua/` | ✅ nếu người chơi đã cập nhật |
| 3 | **Bản đóng gói** | `Resources/LuaBundled/*.lua.txt` | ✅ luôn có, là lưới an toàn cuối |

Ba chi tiết đáng nhớ:

- **`persistentDataPath` là nơi duy nhất ghi được trên mọi nền tảng** và tồn tại qua các lần mở app.
  Đó là lý do bản tải về nằm ở đó chứ không nằm cạnh file game.
- **Bản đóng gói phải có đuôi `.lua.txt`** — Unity không biết `.lua` là gì nên không import; đổi
  thành `.txt` thì nó thành `TextAsset` và `Resources.Load<TextAsset>` đọc được. Đây là mẹo mà mọi
  dự án nhúng Lua vào Unity đều phải dùng.
- **Thư mục dev đứng trên cùng, nhưng phải tắt được.** Không tắt thì Bước 4 sẽ không bao giờ thấy
  bản tải về, vì bản dev luôn thắng. Đặt một `bool` trên runner để bật/tắt.

### 4.2 "CDN" giả lập

Không cần dựng server thật. Tạo thư mục `Assets/_Sandbox/LuaLab/RemoteRoot~/` — Unity **bỏ qua mọi
thư mục có tên kết thúc bằng `~`**, nên nó nằm trong dự án mà không bị import, đúng như một máy chủ
ở xa. Trong đó:

```json
{
  "version": 2,
  "files": [
    { "name": "config.lua" },
    { "name": "HealPotion.lua" },
    { "name": "RandomBox.lua" }
  ]
}
```

Đọc bằng `UnityWebRequest` với URL `file://…`. Không phải để cho phức tạp: **cùng một dòng code sẽ
chạy với `https://cdn.game.com/…`** khi có server thật. Chuyển thư mục thành HTTP chỉ là đổi
`_baseUrl`. Đổi đường dẫn Windows thành URL đúng chuẩn thì dùng `new System.Uri(path).AbsoluteUri` —
tự lo cả dấu `\` lẫn dấu cách trong tên thư mục.

### 4.3 Luồng cập nhật

```
Bấm nút "Kiểm tra cập nhật"
        │
        ├─► tải manifest.json từ CDN
        │        │ lỗi mạng? → dùng bản đang có, KHÔNG báo lỗi to  ← game vẫn phải chơi được
        │        ▼
        ├─► remote.version > local.version ?
        │        │ không → "đã là mới nhất", dừng
        │        ▼
        ├─► tải từng file trong danh sách về persistentDataPath/lua/
        │        │ một file lỗi → BỎ toàn bộ đợt cập nhật này, giữ nguyên bản cũ
        │        ▼                 (nửa cũ nửa mới còn tệ hơn cũ hoàn toàn)
        ├─► ghi version mới xuống local
        └─► Reload() — nạp lại máy ảo từ nguồn mới
```

Dòng quan trọng nhất của cả sơ đồ là **"một file lỗi → bỏ cả đợt"**. Cập nhật phải **nguyên tử**:
`config.lua` v2 nhắc tới `GoldPouch.lua` mà file đó tải hụt thì game hỏng theo kiểu khó hiểu nhất.
Tải hết vào thư mục tạm, đủ mới đổi sang thư mục thật — đó là mẫu chuẩn của mọi hệ cập nhật.

### 4.4 Việc của bạn

- `LuaScriptStore.cs` — ba nguồn, `Read(fileName)`, `WriteCache`, đọc/ghi version local
  (`PlayerPrefs` là đủ cho lab).
- `LuaUpdater.cs` — MonoBehaviour dùng coroutine + `UnityWebRequest`. (Dự án chính dùng UniTask,
  nhưng coroutine giữ cho lab không phụ thuộc gì thêm.)
- Trong `RemoteRoot~/` đặt `manifest.json` version 2 cùng bản `config.lua` sửa số và
  `HealPotion.lua` sửa hành vi.
- Runner: phím `U` để kiểm tra cập nhật, và tắt cờ ưu tiên thư mục dev trước khi thử.

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

**`Scripts/LuaScriptStore.cs`**:

```csharp
using System.IO;
using HungNT;
using UnityEngine;

namespace LuaLab
{
    /// <summary>
    /// Chỗ duy nhất biết một file .lua lấy từ đâu. Ba nguồn theo thứ tự ưu tiên:
    /// thư mục dev (chỉ Editor) → bản đã tải về → bản đóng gói theo build.
    /// </summary>
    public static class LuaScriptStore
    {
        private const string VERSION_KEY = "LuaLab.ScriptVersion";
        private const string RESOURCE_FOLDER = "LuaBundled";

        /// <summary>Bật thì thư mục dev thắng mọi nguồn khác. Tắt đi để thử luồng cập nhật thật.</summary>
        public static bool PreferDevFolder = true;

        /// <summary>Nơi ghi được trên mọi nền tảng và sống qua các lần mở app.</summary>
        public static string CacheDir
        {
            get { return Path.Combine(Application.persistentDataPath, "lua"); }
        }

        /// <summary>Phiên bản bộ script đang có trên máy này.</summary>
        public static int LocalVersion
        {
            get { return PlayerPrefs.GetInt(VERSION_KEY, 0); }
            set
            {
                PlayerPrefs.SetInt(VERSION_KEY, value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Đọc nội dung một script. Trả chuỗi rỗng nếu không nguồn nào có.</summary>
        public static string Read(string fileName)
        {
#if UNITY_EDITOR
            if (PreferDevFolder)
            {
                string devPath = Path.Combine(Application.dataPath, "_Sandbox/LuaLab/LuaScripts", fileName);
                if (File.Exists(devPath))
                {
                    return File.ReadAllText(devPath);
                }
            }
#endif

            string cachePath = Path.Combine(CacheDir, fileName);
            if (File.Exists(cachePath))
            {
                return File.ReadAllText(cachePath);
            }

            // Lưới an toàn cuối: bản đóng gói cùng build. Resources.Load cắt phần đuôi mở rộng,
            // nên file trên đĩa là "config.lua.txt" mà key tra là "config.lua".
            var asset = Resources.Load<TextAsset>($"{RESOURCE_FOLDER}/{fileName}");
            if (asset != null)
            {
                return asset.text;
            }

            DebugEx.LogError($"[LuaScriptStore] Không tìm thấy {fileName} ở bất kỳ nguồn nào.");
            return string.Empty;
        }

        public static void WriteCache(string fileName, string content)
        {
            Directory.CreateDirectory(CacheDir);
            File.WriteAllText(Path.Combine(CacheDir, fileName), content);
        }

        /// <summary>Xoá sạch bản đã tải, quay về bản đóng gói — nút "sửa lỗi" của người chơi.</summary>
        public static void ClearCache()
        {
            if (Directory.Exists(CacheDir))
            {
                Directory.Delete(CacheDir, true);
            }

            LocalVersion = 0;
            DebugEx.Log("[LuaScriptStore] Đã xoá bản tải về, quay lại bản đóng gói.");
        }
    }
}
```

**`Scripts/LuaUpdater.cs`**:

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using HungNT;
using UnityEngine;
using UnityEngine.Networking;

namespace LuaLab
{
    [Serializable]
    public sealed class ScriptManifest
    {
        public int version;
        public ManifestEntry[] files;
    }

    [Serializable]
    public sealed class ManifestEntry
    {
        public string name;
    }

    /// <summary>
    /// Tải bộ script mới từ "CDN" về máy. Thư mục RemoteRoot~ đóng vai máy chủ; đổi sang HTTP thật
    /// chỉ là đổi BaseUrl, phần còn lại giữ nguyên.
    /// </summary>
    public sealed class LuaUpdater : MonoBehaviour
    {
        /// <summary>Gọi khi cập nhật thành công, để bên ngoài nạp lại máy ảo.</summary>
        public event Action OnUpdated;

        private string BaseUrl
        {
            get
            {
                string dir = System.IO.Path.Combine(Application.dataPath, "_Sandbox/LuaLab/RemoteRoot~");
                // Uri lo cả dấu \ của Windows lẫn dấu cách trong tên thư mục -> file:///D:/...
                return new Uri(dir + System.IO.Path.DirectorySeparatorChar).AbsoluteUri;
            }
        }

        public void CheckForUpdate()
        {
            StartCoroutine(CheckRoutine());
        }

        private IEnumerator CheckRoutine()
        {
            this.Log("Đang kiểm tra cập nhật...");

            string manifestText = null;
            yield return Download("manifest.json", text => manifestText = text);

            if (manifestText == null)
            {
                // Không tải được manifest KHÔNG phải lỗi nghiêm trọng: người chơi vẫn chơi được
                // bằng bộ script đang có. Chỉ báo nhẹ rồi thôi.
                this.LogWarning("Không kết nối được máy chủ script, dùng bản đang có.");
                yield break;
            }

            ScriptManifest manifest = JsonUtility.FromJson<ScriptManifest>(manifestText);
            if (manifest == null || manifest.files == null)
            {
                this.LogError("manifest.json sai định dạng.");
                yield break;
            }

            if (manifest.version <= LuaScriptStore.LocalVersion)
            {
                this.Log($"Đã là bản mới nhất (v{LuaScriptStore.LocalVersion}).");
                yield break;
            }

            this.Log($"Có bản mới: v{LuaScriptStore.LocalVersion} → v{manifest.version}, " +
                     $"{manifest.files.Length} file.");

            // Tải hết vào bộ nhớ trước, đủ mới ghi xuống đĩa: cập nhật phải nguyên tử.
            // Nửa cũ nửa mới còn tệ hơn cũ hoàn toàn.
            var downloaded = new Dictionary<string, string>();
            foreach (ManifestEntry entry in manifest.files)
            {
                string content = null;
                yield return Download(entry.name, text => content = text);

                if (content == null)
                {
                    this.LogError($"Tải hụt {entry.name} — huỷ toàn bộ đợt cập nhật, giữ bản cũ.");
                    yield break;
                }

                downloaded[entry.name] = content;
            }

            foreach (KeyValuePair<string, string> file in downloaded)
            {
                LuaScriptStore.WriteCache(file.Key, file.Value);
            }

            LuaScriptStore.LocalVersion = manifest.version;
            this.Log($"Cập nhật xong lên v{manifest.version}. Đang nạp lại...");
            OnUpdated?.Invoke();
        }

        private IEnumerator Download(string fileName, Action<string> onDone)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(BaseUrl + fileName))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    this.LogWarning($"Tải {fileName} lỗi: {request.error}");
                    onDone(null);
                    yield break;
                }

                onDone(request.downloadHandler.text);
            }
        }
    }
}
```

Phần thêm vào `LuaLabRunner`:

```csharp
[SerializeField] private LuaUpdater _updater;
[SerializeField] private bool _preferDevFolder = true;

private void Start()
{
    LuaScriptStore.PreferDevFolder = _preferDevFolder;
    _state = new PlayerState();
    _luaPlayer = new LuaPlayer(_state);

    if (_updater != null)
    {
        _updater.OnUpdated += Reload;
    }

    Reload();
}

private void OnDestroy()
{
    if (_updater != null)
    {
        _updater.OnUpdated -= Reload;
    }
}

// trong Update():
if (Input.GetKeyDown(KeyCode.U))
{
    _updater.CheckForUpdate();
}
else if (Input.GetKeyDown(KeyCode.C))
{
    LuaScriptStore.ClearCache();
    Reload();
}
```

**`RemoteRoot~/manifest.json`** như mục 4.2. **`RemoteRoot~/config.lua`** là bản `version = 2` với
`dropRate` khác và thêm `GoldPouch`. **`RemoteRoot~/HealPotion.lua`** đổi ngưỡng nguy kịch từ `0.3`
thành `0.5` và đổi câu thông báo — để nhìn là biết bản mới đã vào.

</details>

**✅ CHECKPOINT 4:** Đây là checkpoint quan trọng nhất của cả lab, làm đúng thứ tự:

1. Trong Inspector **bỏ tick `Prefer Dev Folder`** (không thì bản dev luôn thắng).
2. Play → bấm `C` xoá cache → Console báo đang chạy bản đóng gói, `config v1`.
3. Bấm `1` → thấy hành vi cũ (ngưỡng nguy kịch 30%).
4. Bấm `U` → *"Có bản mới: v0 → v2"* → tải xong → tự `Reload`.
5. Bấm `1` → **hành vi đã khác**, câu thông báo mới, ngưỡng mới.
6. **Thoát Play mode, Play lại** → vẫn là bản v2, vì nó nằm trong `persistentDataPath` chứ không
   phải trong bộ nhớ.

Bạn vừa đổi luật chơi của một "bản đã phát hành" mà không biên dịch lại dòng nào. Đây chính xác là
điều bạn nghe được về vo-lam-genz, chỉ khác chỗ ngồi: ở đó máy ảo nằm trên GameServer và "người chơi
tải về" thu lại thành "server nạp lại file".

Thử nốt hai tình huống hỏng, vì phần lớn giá trị của hệ này nằm ở lúc hỏng:
- Đổi tên `RemoteRoot~/HealPotion.lua` đi rồi bấm `U` → phải thấy *"huỷ toàn bộ đợt cập nhật"* và
  game vẫn chạy bản cũ nguyên vẹn.
- Sửa `RemoteRoot~/config.lua` cho sai cú pháp (bỏ một dấu `}`), tăng version, bấm `U` → nghĩ trước
  xem chuyện gì xảy ra, rồi so với thực tế. Bước 5 nói về đúng chuyện này.

---

## Bước 5 — Sống chung với script hỏng (45 phút)

Bước 4 để lộ một lỗ hổng: cập nhật thì nguyên tử, nhưng **script tải về đúng nguyên vẹn mà nội dung
sai** thì sao? Bấm `U` xong game trắng bảng, mà bản cũ thì đã bị ghi đè. Ở một game thật, đó là sự
cố toàn server.

Bốn lớp phòng vệ, xếp theo thứ tự đáng làm:

**(1) Nạp thử trước khi tin.** Sau khi tải về, nạp bộ script mới vào **một máy ảo tạm** và kiểm hai
điều: `config.lua` có `return` ra table không, và mọi `script` mà nó nhắc tới có nạp được không.
Đạt thì mới ghi cache và đổi version. Không đạt thì vứt, giữ nguyên bản cũ. Đây là phiên bản rẻ tiền
của "canary deploy", và nó chặn được 90% sự cố.

**(2) Giữ bản trước đó.** Ghi vào `lua/` thì chép bản đang có sang `lua_backup/` trước. Có `backup`
thì mới có nút "quay lại bản cũ" — mà lúc 11 giờ đêm thì nút đó đáng giá hơn mọi log.

**(3) Mọi lời gọi vào Lua đều phải bọc.** `LuaEnvironment.CallMethod` ở Bước 3 đã `try/catch`
`ScriptRuntimeException` và trả `Nil` — nghĩa là một script hỏng chỉ làm **một vật phẩm** không dùng
được, không kéo theo cả hệ. Nguyên tắc: **script không bao giờ được phép ném exception xuyên qua
biên giới vào code khung.**

**(4) Sandbox, và biết giới hạn của nó.** `Preset_SoftSandbox` chặn `io`, `os.execute`, `require`,
`load` — tức là script không đọc/ghi file, không chạy lệnh hệ thống, không nạp thêm mã. Nhưng nó
**không** chặn được `while true do end`: MoonSharp không có bộ đếm lệnh sẵn, một vòng lặp vô hạn
treo luôn cả tiến trình. Cách xử thực dụng: chạy script trên **luồng/hàng đợi riêng có timeout**
thay vì gọi thẳng trên luồng logic chính — và đúng là vo-lam-genz làm vậy, `KTLuaScript` đẩy mọi lời
gọi qua một hàng đợi worker (`Channel` + `ExecuteFunctionAsync`) chứ không gọi trực tiếp.

Và một giới hạn không phải kỹ thuật, quan trọng hơn cả bốn cái trên — **golden rule #2 của dự án**:

> Hot-update **không** làm client đáng tin hơn. Nếu đặt luật tính sát thương vào Lua ở *client*,
> người chơi sửa file đó dễ hơn sửa DLL rất nhiều. Trong game online, Lua thuộc về **server**; ở
> client nó chỉ nên lo những thứ mà người chơi có gian lận cũng chẳng được gì: bố cục UI, hiệu ứng,
> lời thoại, hoạt cảnh.

Việc của bạn: thêm lớp (1) và (2) vào `LuaUpdater`. Lớp (3) đã có từ Bước 3; lớp (4) chỉ cần đọc
hiểu — nhưng hãy thử `while true do end` trong một script rồi bấm `1` để tự thấy Unity treo thật
(chuẩn bị sẵn Task Manager).

<details>
<summary><b>📖 Lời giải — mở sau khi đã tự code</b></summary>

Chèn vào `CheckRoutine`, ngay trước vòng ghi cache:

```csharp
// (1) Nạp thử vào một máy ảo tạm. Sai thì vứt cả đợt, người chơi không biết gì đã xảy ra.
if (!IsHealthy(downloaded))
{
    this.LogError("Bộ script mới không nạp được — giữ nguyên bản đang chạy.");
    yield break;
}

// (2) Sao lưu bản đang có trước khi ghi đè.
LuaScriptStore.BackupCache();
```

```csharp
/// <summary>Nạp thử bộ script mới vào máy ảo dùng một lần để chắc chắn nó sống được.</summary>
private bool IsHealthy(Dictionary<string, string> files)
{
    if (!files.TryGetValue("config.lua", out string configCode))
    {
        this.LogError("Bộ mới thiếu config.lua.");
        return false;
    }

    var probe = new LuaEnvironment();
    GameConfig config = GameConfig.FromLua(probe.Load("config.lua", configCode));
    if (config == null || config.Items.Count == 0)
    {
        return false;
    }

    foreach (ItemDef item in config.Items)
    {
        if (string.IsNullOrEmpty(item.ScriptName) || probe.HasClass(item.ScriptName))
        {
            continue;
        }

        string fileName = $"{item.ScriptName}.lua";
        if (!files.TryGetValue(fileName, out string code))
        {
            // config mới nhắc tới một script không có trong đợt tải: đúng kiểu hỏng mà
            // kiểm tra nguyên tử ở Bước 4 không bắt được, vì mọi file trong manifest đều tải OK.
            this.LogError($"config mới cần {fileName} nhưng manifest không có.");
            return false;
        }

        probe.Load(fileName, code);
        if (!probe.HasClass(item.ScriptName))
        {
            // Nạp mà class không xuất hiện = file sai cú pháp, hoặc quên dòng Item.GetClass.
            this.LogError($"{fileName} nạp xong nhưng không đăng ký class '{item.ScriptName}'.");
            return false;
        }
    }

    return true;
}
```

Thêm vào `LuaScriptStore`:

```csharp
private static string BackupDir
{
    get { return Path.Combine(Application.persistentDataPath, "lua_backup"); }
}

/// <summary>Chép bản đang dùng sang thư mục sao lưu trước khi ghi đè.</summary>
public static void BackupCache()
{
    if (!Directory.Exists(CacheDir))
    {
        return;
    }

    if (Directory.Exists(BackupDir))
    {
        Directory.Delete(BackupDir, true);
    }

    Directory.CreateDirectory(BackupDir);
    foreach (string path in Directory.GetFiles(CacheDir))
    {
        File.Copy(path, Path.Combine(BackupDir, Path.GetFileName(path)));
    }

    PlayerPrefs.SetInt("LuaLab.BackupVersion", LocalVersion);
}
```

</details>

**✅ CHECKPOINT 5:** Làm hỏng `RemoteRoot~/config.lua` (bỏ một dấu `}`), tăng `version` lên 3, bấm
`U` → Console báo *"không nạp được — giữ nguyên bản đang chạy"*, và bấm `1` vẫn dùng được vật phẩm
bằng bản v2. Đó là khác biệt giữa một hệ hot-update và một khẩu súng tự bắn vào chân.

---

## Tự kiểm tra hiểu bài

Tự trả lời từng câu xong mới mở đáp án của câu đó.

**Câu 1.** Sếp nói: *"đưa hết chỉ số vật phẩm sang Lua để sau này chỉnh không cần build"*. Bảng chỉ
số đó chỉ có `id, name, damage, price`. Bạn trả lời thế nào?
<details>
<summary><b>📖 Đáp án câu 1</b></summary>

Đó là **mức 1** trong mục A2 — dữ liệu thuần, không có nhánh nào. Thứ cần là **tải cấu hình từ
server**, không phải nhúng một ngôn ngữ lập trình. Dùng JSON tải về là đạt đúng mục tiêu "chỉnh
không cần build" với chi phí gần bằng không, lại giữ được kiểm tra kiểu và schema.

Nhúng Lua cho việc này là trả cái giá của mức 2 (mất type check, phải sandbox, phải lo script hỏng,
phải giữ hợp đồng API) để mua thứ mà mức 1 đã cho không.

Câu trả lời đầy đủ nên kèm cái mốc để biết khi nào đổi ý: *"khi nào bảng bắt đầu cần điều kiện —
kiểu 'dưới 30% máu thì gấp đôi' — thì lúc đó Lua mới đáng, và hạ tầng tải file của mức 1 dùng lại
được nguyên vẹn để tải script."*

</details>

**Câu 2.** `function RandomBox:OnUse(item, player)` — vì sao C# gọi hàm này phải truyền **ba** tham
số chứ không phải hai, và nếu quên thì lỗi hiện ra như thế nào?
<details>
<summary><b>📖 Đáp án câu 2</b></summary>

Vì dấu `:` chỉ là đường cú pháp: `function T:M(a, b)` biên dịch thành `function T.M(self, a, b)`.
Hàm thật sự có ba tham số, tham số đầu tên `self` và Lua chỉ tự điền nó khi *gọi* cũng bằng dấu `:`
(`obj:M(a, b)`). C# gọi qua `Script.Call` là gọi hàm trần, không có cú pháp `:` nào cả — nên phải tự
tay đưa table class vào vị trí đầu.

Quên thì mọi tham số **lệch một nấc**: `self` nhận `item`, `item` nhận `player`, `player` nhận `nil`.
Lỗi không nổ ở chỗ gọi mà nổ ở dòng nào đó bên trong script — `attempt to index a nil value` khi
script chạm tới `player:GetHp()`. Nhìn thông báo đó thì tưởng `player` truyền vào bị null, đi sửa
đúng chỗ không có lỗi. Đây là lỗi tốn thời gian nhất của người mới nhúng Lua.

</details>

**Câu 3.** Vì sao Lua chỉ thấy `LuaPlayer` chứ không thấy thẳng `PlayerState`, dù `LuaPlayer` chẳng
làm gì ngoài chuyển tiếp?
<details>
<summary><b>📖 Đáp án câu 3</b></summary>

Vì `UserData.RegisterType<T>()` mở **toàn bộ thành viên public** của `T` cho script — không có cách
nào chọn lọc từng cái. Đưa `PlayerState` ra là script viết được `player.Hp = 99999` hoặc
`player.Bag:Clear()`, không qua kiểm tra nào, không có log nào.

`LuaPlayer` biến "mọi thứ public" thành "đúng những gì tôi cho phép", và đặt trường thật sau
`internal` để cùng assembly C# vẫn dùng được còn Lua thì không thấy. Nó cũng là **chỗ neo của hợp
đồng**: đổi tên `PlayerState.Hp` thành `CurrentHp` chỉ cần sửa một dòng trong lớp bọc, 50 file script
đang chạy không hề hấn gì. Không có lớp bọc thì mọi lần đổi tên trong code miền là một lần gãy
script — và trình biên dịch không cảnh báo được.

Đúng lý do vo-lam-genz có `Lua_Player`/`Lua_Item` thay vì đưa `KPlayer` trần.

</details>

**Câu 4.** Bước 4 tải hết vào bộ nhớ rồi mới ghi xuống đĩa. Vì sao không ghi thẳng từng file cho
đơn giản?
<details>
<summary><b>📖 Đáp án câu 4</b></summary>

Vì cập nhật phải **nguyên tử**: hoặc bộ mới vào trọn vẹn, hoặc bộ cũ ở nguyên. Ghi thẳng từng file
thì rớt mạng giữa chừng để lại một trạng thái **không phải bản nào cả** — `config.lua` v2 đã nhắc
tới `GoldPouch` trong khi `GoldPouch.lua` chưa kịp tải. Người chơi mở app ra thấy hỏng, mà log thì
báo "cập nhật thành công một phần", và không có bản nào để quay về.

Nửa cũ nửa mới luôn tệ hơn cũ hoàn toàn: cũ hoàn toàn thì người chơi chỉ *chưa có tính năng mới*;
nửa vời thì game *hỏng*.

Cùng một lý do khiến Bước 5 nạp thử vào máy ảo tạm trước khi tin, và khiến hệ cập nhật thật ghi vào
thư mục tạm rồi mới đổi tên thư mục.

</details>

**Câu 5.** Đề xuất: *"đưa công thức tính sát thương sang Lua ở client để cân bằng game nhanh"*. Vấn
đề nằm ở đâu?
<details>
<summary><b>📖 Đáp án câu 5</b></summary>

Nó phá **golden rule #2 — server là source of truth**. Công thức sát thương là luật chơi; luật chơi
thuộc về server. Đưa xuống client thì:

1. **Người chơi sửa được.** File `.lua` trong `persistentDataPath` là văn bản thuần, ai cũng mở
   được. Sửa `damage * 1.5` thành `damage * 150` dễ hơn sửa DLL rất nhiều — hot-update ở client làm
   game **dễ hack hơn**, không phải khó hơn.
2. **Hai nguồn sự thật.** Nếu server vẫn tính riêng thì client hiển thị một đằng, server chốt một
   nẻo; nếu server tin client thì hết chuyện để nói.
3. **Không đạt được mục tiêu.** "Cân bằng nhanh" đòi mọi người chơi cùng đổi cùng lúc — mà client
   thì mỗi người tải về một thời điểm.

Chỗ đúng: công thức nằm trong Lua **trên server** — đúng như vo-lam-genz. Client hot-update thì chỉ
nên lo thứ mà gian lận cũng vô hại: bố cục UI, hiệu ứng, lời thoại, sự kiện hiển thị.

</details>

**Câu 6.** Sau lab này, muốn đưa Lua vào dự án MMORPG thật thì đặt ở đâu, và mảnh nào của lab dùng
lại được?
<details>
<summary><b>📖 Đáp án câu 6</b></summary>

Đặt ở **`Server/GameServer/`**, thành một thư mục kiểu `LuaSystem/` — vì mọi lý do ở câu 5, và vì
server .NET 8 chạy MoonSharp không vướng gì IL2CPP.

Dùng lại được gần như toàn bộ, chỉ đổi chỗ ngồi:

| Mảnh trong lab | Bản trên GameServer |
|---|---|
| `LuaEnvironment` | y hệt, chỉ đổi `DebugEx` → `Log` của `ServerCore` |
| `LuaPlayer` / `PlayerLib` | bọc quanh `PlayerEntity` thật |
| `LuaScriptStore` | đơn giản hơn: server đọc thẳng thư mục `LuaScripts/`, không cần ba tầng |
| `LuaUpdater` | **bỏ** — server sửa file tại chỗ; thay bằng lệnh console `reload lua` |
| Nạp thử trước khi tin (Bước 5) | **giữ nguyên, còn quan trọng hơn** — script hỏng ở server là sự cố toàn server |

Cái *không* có trong lab mà bản server bắt buộc phải có: **chạy script qua hàng đợi worker có
timeout** thay vì gọi thẳng trên luồng tick. Một `while true` trong lab chỉ treo Editor của bạn; ở
server nó treo cả thế giới.

Và một câu hỏi phải trả lời trước khi viết dòng đầu tiên: **hệ nào của MMORPG xứng đáng có Lua?**
Câu trả lời gần như chắc chắn không phải "di chuyển" hay "chiến đấu" (những thứ chạy mỗi tick, cần
nhanh và cần replay được), mà là những thứ **thưa và hay đổi**: hiệu ứng vật phẩm, hội thoại NPC,
nhiệm vụ, sự kiện theo mùa. Đúng danh sách mà vo-lam-genz đang để trong `LuaScripts/`.

</details>

---

## Dọn lab

Xoá thư mục `Assets/_Sandbox/` (và file `.meta` của nó). Không có gì khác phải hoàn tác: không đụng
`Packages/manifest.json`, không đụng `Assets/packages.config`, không đụng `Assets/Game/`.
Nếu đã thêm scene lab vào Build Settings thì bỏ ra. Dữ liệu tải về nằm ở
`Application.persistentDataPath/lua/` — xoá tay nếu muốn sạch hoàn toàn.

## Đi tiếp

1. **Đọc code thật**: `../vo-lam-genz-server/GameServer/GameServer/KiemThe/LuaSystem/`. Đọc theo thứ
   tự `KTLuaEnvironment.cs` → `KTLuaScript.cs` → một file trong `Logic/KTLuaLib_*.cs` → một file
   `.lua` trong `bin/Debug/LuaScripts/Item/Common/`. Bạn vừa tự dựng lại chính bộ khung đó nên sẽ
   đọc rất nhanh.
2. **Thêm hash vào manifest** (MD5 từng file) để chỉ tải file nào thật sự đổi, và để phát hiện file
   hỏng khi tải. Đây là bước tiếp theo tự nhiên nhất của Bước 4.
3. **Thử xLua** nếu định làm client mobile: cùng ý tưởng, khác ở chỗ nó sinh code binding trước
   thay vì reflection lúc chạy, và có `[Hotfix]` để vá cả hàm C# đã ship.
4. **Sách**: *Programming in Lua* bản 1 đọc miễn phí ở `lua.org/pil`. Chương metatable là chương duy
   nhất cần đọc hai lần. `moonsharp.org` có mục "Compatibility" liệt kê những chỗ nó khác Lua chuẩn —
   đọc trước khi mất nửa buổi vì một hàm không tồn tại.
