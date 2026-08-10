# VOLAMGENZ-REFERENCE — Chắt lọc từ dự án gốc

> Tài liệu này trả lời đúng 1 câu hỏi: **trong `vo-lam-genz`, cái gì đáng bê sang dự án này, cái gì chỉ nên đọc cho hiểu rồi bỏ.**
>
> Bối cảnh cần biết trước: `vo-lam-genz` là codebase **kế thừa** — gốc là một game Trung Quốc (KiemThe/剑侠),
> port sang VN, đi qua nhiều đời dev. Nên nó **trộn nhiều thời kỳ**: phần rất cũ (gói tin dạng chuỗi `a:b:c`,
> `switch` ~1000 case) sống chung với phần rất mới (`[RpcHandler]` + MemoryPack + auto-register).
> Đọc mà không phân biệt được 2 thời kỳ đó thì sẽ bắt chước nhầm.

---

## 1. Bản đồ 3 repo

| Repo | Là gì | Ngôn ngữ |
|------|-------|----------|
| `vo-lam-genz` | Unity client (~1.390 file `.cs`) | C# / Unity 6000.2 |
| `vo-lam-genz-server/GameServer` | Game Server — logic, entity, combat, socket với client | C# .NET 8 |
| `vo-lam-genz-server/GameDBServer` | DB Server — process riêng, sở hữu MySQL | C# .NET |
| `vo-lam-genz-server/LogDBServerG` | Log server riêng (log gameplay, giao dịch) | C# .NET |
| `vo-lam-genz-config-server` | Submodule config XML của server (nằm ở `GameServer/GameServer/bin/Debug`) | XML |

**Luật sắt của cả hệ thống:** client **không bao giờ** nối DB.

```
Client ──TCP (TCPGameServerCmds)──► GameServer ──TCP nội bộ (CMD_DB_*)──► GameDBServer ──► MySQL
```

---

## 2. Luồng chạy end-to-end

```
Play
 └─► HTTP: đọc version (StreamingAssets / local / server) → so sánh 3 nguồn
      ├─ lệch app version   → bắt update app
      └─ lệch resource ver  → tải patch AssetBundle theo MD5 từng file (CDN)
 └─► LoadConfig — nạp XML tĩnh vào Loader.Items / Loader.Maps
 └─► UI Login
      ├─ HTTP: lấy danh sách máy chủ
      ├─ TCP #1 → Login Server: lấy session/token   (UISelectServer TỰ new TCPClient rồi hủy)
      └─ TCP #2 → Game Server: kết nối chính
           ├─ CMD_ROLE_LIST → chọn/tạo nhân vật (UIRoleManager)
           ├─ CMD_INIT_GAME → nhận RoleData
           ├─ load map (2 bundle: map + map_config, scene additive)
           └─ PlayZone.EnterGame → CMD_PLAY_GAME → vòng lặp gameplay
```

**2 kênh mạng, tách bạch rõ ràng — điều này đáng học:**

| Kênh | Dùng cho | Vì sao |
|------|----------|--------|
| **HTTP** | version, patch AssetBundle, danh sách server | Nặng, tĩnh, không cần realtime, cần CDN cache |
| **TCP** | toàn bộ gameplay | Realtime, cần giữ kết nối, thứ tự gói được đảm bảo |

---

## 3. Tầng mạng — phần đáng bê nhất

### 3.1. Framing (đóng khung gói tin)

TCP là **dòng byte liên tục**, không có ranh giới gói. `Send` 3 lần có thể `Receive` về 1 lần (hoặc ngược lại).
Nên phải tự định khung. vo-lam-genz dùng **length-prefix**:

```
┌──────────────┬──────────────┬────────────────────────┐
│ int32 length │ int32 cmdId  │ payload (length-4 byte)│
│ = 4 + payLen │              │                        │
└──────────────┴──────────────┴────────────────────────┘
```

- Gửi: `TCPOutPacket.MakeTCPOutPacket(pool, bytes, offset, len, cmdId)` → `Socket.BeginSend`
- Nhận: `TCPClient.SocketReceived` đọc vào `mReceiveBuffer` → `TCPInPacket.WriteData(...)` **gom byte tới khi đủ 1 gói**
  → bắn `TCPCmdPacketEvent` → `TCPCmdHandler.ProcessServerCmd(cmdId, bytes, size)`

> **Cái bẫy kinh điển**: quên vòng lặp "gom tới khi đủ" → gói to bị vỡ, gói nhỏ bị dính. Đây là bài học Phase 1.

Có thêm `DataHelper.SortBytes(...)` — xáo byte để obfuscate nhẹ. **Không bê**: đây là che giấu, không phải bảo mật,
và làm việc debug packet cực khổ.

### 3.2. Nén payload — `MemoryPackUtility` (bê nguyên ý tưởng)

Trong payload còn 1 lớp header nữa để đánh dấu có nén hay không:

```
┌──────────┬────────────────────────┬───────────────────┐
│ flag 1B  │ rawLen 4B (nếu flag=1) │ MemoryPack bytes  │
└──────────┴────────────────────────┴───────────────────┘
  0x00 = raw · 0x01 = LZ4
```

Logic quyết định trong `CompressRaw`:
- payload ≤ **4KB** → không nén, chỉ prepend `0x00`. Nén gói bé lỗ vốn (tốn CPU, có khi còn to hơn).
- payload > 4KB → thử nén LZ4 `L00_FAST`, **chỉ dùng bản nén nếu nó thực sự nhỏ hơn** bản không nén.
- Dùng `ArrayPool<byte>.Shared` + `ArrayBufferWriterPool` để không rác GC.

📄 `vo-lam-genz-server/GameServer/GameServer/Tools/MemoryPackUtility.cs` — **đọc file này, nó rất sạch.**

### 3.3. Dispatch — 2 thời kỳ, chỉ bê thời kỳ mới

**Thời kỳ cũ (TRÁNH):** `TCPCmdHandler.cs` — 2.056 dòng, một `switch` ~1000 `case`.
Thêm 1 lệnh = sửa file dùng chung = merge conflict + dễ sót. Vi phạm Open/Closed.

**Thời kỳ mới (BÊ):** dispatch table + attribute + auto-register bằng reflection.

Client — `RpcWrapperHandler2.cs`:
```csharp
[RpcHandler(TCPGameServerCmds.CMD_BAGUA_GET_INFO)]
public static void OnGetBaguaInfo(RpcData rpcData)      // signature CỐ ĐỊNH: static void (RpcData)
{
    var data = rpcData.GetData<BaguaAllData>();          // MemoryPack + giải nén
    OnBaguaAllDataReceived?.Invoke(data);                // bắn event → Presenter → UI
}
```
- `RegisterAllFromAttributes()` quét reflection **1 lần lúc boot** (`[RuntimeInitializeOnLoadMethod]`), tự đăng ký.
- `RpcData` **copy** byte ra buffer riêng — vì buffer nhận là buffer dùng lại, giữ reference là hỏng data.
- Handler đã được `await UniTask.SwitchToMainThread()` trước khi gọi → an toàn đụng Unity API.

Server — `CmdWrapperHandler.cs`:
```csharp
[TcpCommandHandler(TCPGameServerCmds.CMD_BAGUA_GET_INFO)]
public static TcpResult GetInfoHandler(TcpRequestData req)
{
    var result = new TcpResult();
    result.SetData(baguaAllData);        // tự MemoryPack + nén
    result.ReturnCode = TCPProcessCmdResults.RESULT_DATA;   // → echo về đúng cmd vừa nhận
    return result;
}
```
- Có cả bản async: `[AsyncTcpCommandHandler]` trả `Task<TcpResult>` — dùng khi phải chờ DB.
- `TcpRequestData.Rpc<T>(cmd, dto)` để **chủ động push** một cmd khác về client (không phải response).

> **Điểm rất hay:** 2 bên đối xứng nhau. Client `[RpcHandler]` ↔ Server `[TcpCommandHandler]`, cùng 1 enum cmd.
> Dự án này bê nguyên ý tưởng đó, đổi tên thành `[NetHandler]` / `[TcpHandler]`.

### 3.4. Ba kiểu payload — chỉ giữ 1

| Kiểu | Trong vo-lam-genz | Dự án này |
|------|-------------------|-----------|
| Chuỗi `a:b:c` + `e.fields[i]` | Legacy (login, move, role list) | ❌ **Không dùng.** Sai index = bug runtime, không có type-safety |
| ProtoBuf | Feature cũ + kênh GS↔DBS | ❌ Không dùng (giữ 1 loại cho gọn) |
| **MemoryPack** | Feature mới | ✅ **Dùng duy nhất cái này** |

Ba kiểu song song là gánh nặng nhận thức thật sự — mỗi lần đọc code phải đoán "cái này thời kỳ nào".

---

## 4. GameServer — cấu trúc & mô hình chạy

### 4.1. Layout
```
GameServer/GameServer/
├── Server/          TCPManager, TCPCmdHandler, CmdWrapperHandler, TCPClientPool, SendCmdManager
├── TCPSOCKET/       SocketListener, TMSKSocket, BufferManager, SocketAsyncEventArgsPool  ← tầng socket thô
├── KiemThe/
│   ├── Core/        Entity: KPlayer, Monster, Item, Skill... (component-based)
│   ├── Logic/       Manager: PlayerManager, MonsterManager, ItemManager, SkillTree, BuffTree
│   ├── Network/     KT_TCPHandler_*.cs (legacy, chia theo mảng) + *TcpHandler.cs (mới, attribute)
│   └── Entities/
├── Services/        BackgroundService: MainDispatcherWorker, ClientsWorker, DBCommandWorker,
│                    DBWriterWorker, SpriteDBWorker, ChatMsgWorker, SocketCheckWorker
├── Configurations/  appsettings.json + appsettings.{Development,Staging,Production}.json
└── Program.cs       Generic Host, đăng ký worker + web API (Swagger) + Prometheus metrics
```

### 4.2. Mô hình chạy: nhiều `BackgroundService` song song

Không phải 1 vòng lặp game duy nhất. Mỗi loại việc có 1 worker riêng, mỗi worker là 1 `BackgroundService`
với vòng `while (!token.IsCancellationRequested) { … await Task.Delay(sleepMs) }`:

| Worker | Việc |
|--------|------|
| `MainDispatcherWorker` | Nhịp chính, khởi tạo TCP, timer tổng |
| `ClientsWorker` | Xử lý gói tin đến từ client |
| `DBCommandWorker` / `DBWriterWorker` | Gửi lệnh sang DBServer, ghi định kỳ |
| `SpriteDBWorker` | Lưu state nhân vật xuống DB theo chu kỳ |
| `ChatMsgWorker` | Hàng đợi chat |
| `SocketCheckWorker` | Dọn socket chết, timeout |

Mẹo đáng học trong vòng lặp: `sleepMs = Max(5, interval - (endTicks - startTicks))` —
**trừ đi thời gian đã xử lý** để nhịp đều, thay vì `Delay(interval)` cứng làm tick trôi dần.

> ⚠️ Nhược điểm: state game là `static` toàn cục (`Global`, `GameManager`) và nhiều worker cùng đụng vào →
> phải khoá tay, dễ race. Dự án này đi hướng khác: **1 game loop tick cố định, logic chạy 1 luồng**, IO ở luồng khác.

---

## 5. GameServer ↔ GameDBServer — phần ít người để ý nhưng quan trọng nhất

Đây là lý do 3-tier tồn tại. Có **2 kiểu gọi DB**, dùng sai kiểu là chết server:

### Kiểu 1 — Đọc đồng bộ (chặn)
```csharp
string[] dbFields = Global.ExecuteDBCmd((int)TCPGameServerCmds.CMD_DB_GETFUBENSEQID, "0", serverId);
```
Gửi lệnh sang DBServer rồi **đứng chờ** phản hồi. Dùng cho việc bắt buộc phải có kết quả mới đi tiếp
(vd load nhân vật lúc login). **Không được gọi trong vòng lặp gameplay** — 1 truy vấn chậm là cả server khựng.

### Kiểu 2 — Ghi bất đồng bộ (hàng đợi)
```csharp
GameManager.DBCmdMgr.AddDBCmd((int)TCPGameServerCmds.CMD_DB_ADDGIVETokenITEM, "1:2:3");
```
Đẩy lệnh vào hàng đợi, `DBCommandWorker` gửi đi sau. Không chờ, không biết kết quả.
Dùng cho mọi thao tác ghi thường xuyên (tăng exp, thêm item, log).

### Ý tưởng cốt lõi cần hiểu
> **State game sống trong RAM của GameServer, DB chỉ là bản lưu.**
> Đánh quái không phải là `UPDATE character SET exp = exp + 10`. Nó là: sửa số trong RAM ngay lập tức
> (client thấy phản hồi tức thì), rồi *thỉnh thoảng* mới ghi xuống DB (dirty flag + worker định kỳ).
> DB không bao giờ nằm trên đường đi của gameplay realtime.

Dải `CMD_DB_*` bắt đầu từ `CMD_DB_START_CMD = 908` — **client không bao giờ gửi những lệnh này.**

---

## 6. Bảng tổng: BÊ gì / TRÁNH gì

### ✅ Đáng bê (tư duy, không phải code)

| # | Thứ | Vì sao |
|---|-----|--------|
| 1 | Tách 2 kênh HTTP (tĩnh/nặng) và TCP (realtime) | Đúng chuẩn, ai cũng làm vậy |
| 2 | Framing length-prefix | Giải đúng bài toán "TCP là stream" |
| 3 | Dispatch table + attribute + auto-register | Thêm handler không đụng file dùng chung |
| 4 | 2 bên đối xứng: cùng enum cmd, cùng DTO | Không bao giờ lệch contract |
| 5 | Nén có ngưỡng + chỉ nén nếu thật sự nhỏ hơn | Tối ưu đúng chỗ |
| 6 | Kỷ luật main-thread (`SwitchToMainThread`) | Không thì crash ngẫu nhiên |
| 7 | Server là source of truth | Nền tảng chống gian lận |
| 8 | State trong RAM, DB là bản lưu (dirty flag + worker) | Bài học kiến trúc lớn nhất của MMO |
| 9 | Patch AssetBundle theo MD5 từng file | Update nhanh, tiết kiệm băng thông |
| 10 | Packet pool / ArrayPool để tránh GC | Server chạy 24/7 không được rác |
| 11 | Trừ thời gian xử lý khi tính sleep của vòng lặp | Tick không trôi |

### ❌ Nên tránh

| # | Anti-pattern | Bằng chứng | Hậu quả |
|---|--------------|------------|---------|
| 1 | God class / mega file | `TCPCmdHandler.cs` 2.056 dòng · `TCPGameServerCmds.cs` ~3.785 dòng enum · `PlayZone` + 48 file partial | Không đọc nổi, merge conflict liên miên |
| 2 | `switch` ~1000 case thay dispatch table | `PlayZone_Network_Switch.cs` | Thêm lệnh phải sửa file chung |
| 3 | Global mutable static rải rác | `Global`, `GameManager`, `GameInstance.Game`, `Super` | Coupling ẩn, thứ tự init ngầm, không test được |
| 4 | 3 kiểu serialize song song | chuỗi / ProtoBuf / MemoryPack | Mỗi lần đọc code phải đoán thời kỳ |
| 5 | Gói tin dạng chuỗi + `e.fields[index]` | Legacy move/login | Sai index không báo lỗi compile |
| 6 | Magic string / number | `RootParams["serverip"]`, port `"4502"` hard-code | Gõ sai không ai biết |
| 7 | `catch (Exception) { }` nuốt lỗi | `LoadVersion.cs`, `DownloadResource.cs` | Lỗi biến mất, debug địa ngục |
| 8 | Hard-code khoá mã hoá trong source | `MainGame.cs` — `KTResourceCrypto.SetKey("eabb22…")` | Ai đọc source cũng thấy |
| 9 | Network dính chặt UI | `UISelectServer` tự `new TCPClient`, tự build packet trong callback UI | Không tái dùng, không test |
| 10 | Không có abstraction transport | Không có `ITransport` / `IPacketCodec` | Không mock được, đổi sang WebSocket/KCP phải sửa khắp nơi |
| 11 | Comment trộn Việt/Anh/Trung | rải khắp | Nhiễu |
| 12 | Xáo byte gói tin coi là bảo mật | `DataHelper.SortBytes` | Không chặn được ai, chỉ làm khổ chính mình khi debug |

---

## 7. Bảng ánh xạ: vo-lam-genz → dự án này

| vo-lam-genz | Dự án MMORPG này | Ghi chú |
|-------------|------------------|---------|
| `TCPClient` (socket thô, dính DLL `HSGameEngine`) | `ITransport` + `TcpTransport` | Tách hẳn khỏi game, mock được |
| Framing lẫn trong `BatchSend()` | `IPacketCodec` + `LengthPrefixCodec` | Framing là 1 việc riêng |
| `TCPCmdHandler` switch 1000 case | `NetDispatcher` + `[NetHandler(cmd)]` | Dispatch table |
| `CmdWrapperHandler` + `[TcpCommandHandler]` | `TcpDispatcher` + `[TcpHandler(cmd)]` | **Bê gần như nguyên** — phần này đã sạch |
| `TCPGame` (mọi action trong 1 class) | Tách theo feature: `AuthApi`, `WorldApi`, `InventoryApi` | Mỗi cái nhỏ, chỉ build & gửi DTO |
| `GameInstance.Game` static | `INetService` resolve qua VContainer | Không global mutable |
| `TCPGameServerCmds` enum 3.785 dòng | `NetCmd` enum có quy hoạch dải | Xem `ROADMAP.md` §2 |
| DTO rải trong client + server, đồng bộ thủ công | Project `Server/Shared` → build ra 1 DLL cho cả 2 bên | **Khác biệt lớn nhất** |
| `MemoryPackUtility` (server) + bản copy bên client | 1 bản duy nhất trong `Shared` | Không copy code |
| `Global.ExecuteDBCmd` (chuỗi, chặn) | `IDbClient.RequestAsync<TReq,TRes>` | Type-safe, async |
| `GameManager.DBCmdMgr.AddDBCmd` | `IDbClient.Fire(cmd, dto)` | Giữ ý tưởng hàng đợi ghi |
| `Global` / `GameManager` static | Service inject qua DI (server dùng Generic Host DI) | |
| `MainGame.QueueOnMainThread` | `await UniTask.SwitchToMainThread()` trong dispatcher | Ý tưởng như nhau, code gọn hơn |
| `catch {}` | `DebugEx.LogError` + ném rõ ràng | Không nuốt lỗi |

---

## 8. File nên đọc trong vo-lam-genz (theo thứ tự)

Đọc để **hiểu**, không phải để chép.

| Muốn hiểu | Đọc file |
|-----------|----------|
| Toàn cảnh client | `vo-lam-genz/.claude/docs/PROJECT_OVERVIEW.md` |
| Boot / CDN / TCP — chi tiết nhất | `vo-lam-genz/.claude/docs/BOOT-CDN-TCP-FLOW.md` |
| Quy trình thêm feature C/S | `vo-lam-genz/.claude/docs/FEATURE_DEVELOPMENT_GUIDE.md` |
| Socket client cấp thấp | `Assets/Scripts/FSPlay/GameEngine/Network/TCPClient.cs` |
| Dispatch client (bản sạch) | `Assets/Scripts/FSPlay/KiemVu/Logic/PlayZone/RpcWrapperHandler2.cs` |
| Handler feature mẫu (bản sạch) | `Assets/Scripts/FSPlay/KiemVu/Network/BaguaTcpHandler.cs` |
| Dispatch server (bản sạch) | `GameServer/GameServer/Server/CmdWrapperHandler.cs` |
| Nén + serialize | `GameServer/GameServer/Tools/MemoryPackUtility.cs` |
| Contract dùng chung | `GameServer/MemoryPackSerializerLib/` (+ `BuildDataTut.md`) |
| Worker/tick server | `GameServer/GameServer/Services/MainDispatcherWorker.cs` |
| Cách một feature được làm từ đầu đến cuối | `vo-lam-genz/.claude/docs/bagua-guides/PHASE-1.md` → `PHASE-12.md` |
| AOI / streaming object | `vo-lam-genz/.claude/docs/OBJECT-STREAMING-AOI.md` |
| Load & render map 2D | `vo-lam-genz/.claude/docs/MAP-SYSTEM.md` |

⚠️ Repo server local **có thể đã cũ** so với server thật đang chạy. Dùng để hiểu kiến trúc, đừng coi là chân lý.
