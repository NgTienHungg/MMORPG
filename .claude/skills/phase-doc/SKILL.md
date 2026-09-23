---
name: phase-doc
description: Viết hoặc sửa tài liệu hướng dẫn phase (.claude/docs/guides/PHASE-N.md) cho dự án MMORPG. Dùng khi owner yêu cầu soạn doc cho một phase mới, cập nhật doc phase đã có, hoặc khi owner báo doc thiếu code / sai tên / làm theo không chạy được.
---

# Viết tài liệu phase

Tài liệu phase là **bản hướng dẫn owner tự gõ code theo**. Owner không đọc codebase trước khi làm —
doc là thứ duy nhất họ có. Một doc thiếu 20% code không phải "doc hoàn thành 80%": nó là doc **không
dùng được**, vì owner sẽ stuck đúng ở chỗ thiếu và không có cách nào tự suy ra.

## Luật tuyệt đối

### 1. Mọi file được nhắc tên trong bảng "Danh sách file" đều PHẢI có code trong doc

Bảng "Danh sách file — tạo gì, sửa gì" là hợp đồng. Mỗi dòng 🆕 phải có **nguyên văn cả file** trong
một foldout lời giải. Mỗi dòng ✏️ phải có **đủ phần sửa để owner dán vào được**: hoặc cả file, hoặc
khối code kèm câu chỉ rõ nó thay cho cái gì ("thay nguyên hàm `Load()`", "thêm vào cuối class").

Trước khi kết thúc doc, **đếm lại**: số dòng trong bảng file phải bằng số khối code có trong doc. Lệch
một dòng là doc hỏng.

Cấm các kiểu viết thay cho code:
- ❌ "làm tương tự như bước trên"
- ❌ "thêm tương ứng cho `Items`"
- ❌ "// ... phần còn lại giữ nguyên" ở giữa một file mới
- ❌ nhắc tên một hàm ở bảng Troubleshooting mà cả doc không có code của hàm đó

Chỗ duy nhất được rút gọn: **lặp lại y hệt một khối đã in đầy đủ ở trên trong cùng doc**, và phải chỉ
rõ "giống hệt khối X ở Bước N, chỉ đổi `A` thành `B`".

### 2. Tên trong doc phải khớp code thật — kiểm bằng grep, không bằng trí nhớ

Trước khi viết một dòng nào, chạy `grep`/`Glob` để lấy **tên thật đang có trong repo**: tên class, tên
file, tên hàm, chữ ký hàm, namespace. Doc cũ là nguồn **không đáng tin** — owner refactor liên tục và
doc không tự đổi theo.

Riêng phần này đã cắn một lần rồi (Phase 12, 2026-09-22): doc viết `WorldRules` / `CharacterProfile` /
`CharacterProfiles.Build()` / `ActionDefine.cs` trong khi code thật là `WorldConfig` /
`CharacterConfig` / `CharacterConfigContainer.Load()` / `CharacterTableData.cs`.

Trong một doc, **một thứ chỉ có một tên**. Phần "hướng làm" ở trên và phần "lời giải" ở dưới phải gọi
cùng một class bằng cùng một chữ, cùng chữ ký hàm, cùng namespace. Viết xong thì grep lại chính doc
vừa viết để bắt tên mồ côi.

### 3. Code trong doc phải biên dịch được

Không phải giả code, không phải phác thảo. Đủ `using`, đủ `namespace`, đủ đóng ngoặc, đúng convention
của `CONVENTIONS.md` (Allman, 4 space, thân hàm luôn có `{ }`, không expression-bodied trừ property
getter thuần, log qua `Log`/`DebugEx` không `Console.WriteLine`).

Khi doc và code thật cùng được làm trong một lần: **viết code trước, build cho pass, rồi chép code đã
build được vào doc**. Đừng viết doc rồi hy vọng code khớp.

### 4. Phần client không bao giờ được thiếu

Lỗi lặp lại nhiều nhất: doc mô tả server rất kỹ rồi dừng lại, phần client chỉ còn một dòng trong bảng
Troubleshooting. Client là nửa còn lại của mọi phase trong dự án này.

Mỗi phase động tới mạng phải trả lời đủ, **bằng code**:
- DTO đổi gì (nguyên văn file trong `Server/Shared/Dto/`)
- Server gắn dữ liệu vào response ở đâu (nguyên văn hàm)
- Client **nhận** ở handler nào, và **đưa dữ liệu đi đâu** (nguyên văn)
- Class mới nào phải đăng ký vào `GameLifetimeScope.Configure` (nguyên văn dòng `builder.Register<...>`)
- MonoBehaviour nào cần `RegisterComponentInHierarchy`

### 5. Thiết kế trước, rồi mới hướng dẫn

Trước khi viết bước nào, trả lời: **thêm cái thứ hai cùng loại tốn bao nhiêu dòng?** Nếu câu trả lời là
"chép lại cả hàm rồi sửa tên" thì thiết kế sai — sửa thiết kế trước, đừng hướng dẫn owner chép.

Đây là lý do `ConfigService` phải generic, không phải `LoadCharacters()` + `LoadItems()` + `LoadMonsters()`.
Owner phát hiện ra lỗi này khi làm bảng item, tức là sau khi đã gõ theo doc — muộn hơn nhiều so với lúc
viết doc.

## Format bắt buộc

Giữ đúng khuôn của PHASE-8 → PHASE-12 (đã chốt, owner quen rồi):

```
# PHASE N — <tên>

> **Kết quả cuối Phase N:** <mô tả thứ chạy được, quan sát được bằng mắt>
> **Điều kiện:** xong PHASE-(N-1)
> **Bài học chính:** (1)... (2)... (3)...

Format như trước: **hướng làm** hiện sẵn, **📖 Lời giải** trong foldout.

## <phần bối cảnh / vì sao>

## Danh sách file — tạo gì, sửa gì
   <bảng theo từng bước, cột File | Việc, có 🆕 / ✏️ / ❌>

## Bước 1 — <tên>
### Hướng làm
   <mô tả bằng lời: làm gì, vì sao vậy, bẫy ở đâu. KHÔNG có code lớn ở đây>
### ✅ CHECKPOINT A
   <danh sách đánh số, mỗi dòng là một thứ quan sát được>
<details>
<summary><b>📖 Lời giải — <code>TênFile</code></b></summary>
   <nguyên văn code>
</details>

## Bước 2 ... Bước 3 ...

## Ba thử nghiệm bắt buộc
## Troubleshooting        <bảng: Triệu chứng | Nguyên nhân | Chỗ sửa>
## Tự kiểm tra hiểu bài   <câu hỏi + <details> đáp án>
## Để dành (ghi lại, chưa làm)
```

Chi tiết chống lỗi render của foldout: xem memory `phase-doc-foldout-format`.

- Tiêu đề foldout: `<summary><b>📖 Lời giải — <code>TênFile.cs</code></b></summary>`
- **Dòng trống sau `<summary>`** và **trước `</details>`**, nếu không markdown bên trong không render.
- Mỗi file một foldout riêng khi file dài; gộp 2–3 file nhỏ có liên quan thì ghi rõ cả hai tên ở summary.

## Quy trình

1. Đọc `CLAUDE.md` + `ROADMAP.md` + doc phase trước.
2. **Quét code thật** — lấy tên, chữ ký, namespace hiện hành. Ghi ra một danh sách tên để tra khi viết.
3. Thiết kế: cái gì ở `Shared`, cái gì chỉ server, client nhận thế nào, thêm cái thứ hai tốn mấy dòng.
4. Nếu lần này cũng viết code: **code trước, build pass, rồi chép vào doc**.
5. Viết doc theo khuôn trên.
6. **Soát cuối** — ba phép đếm, làm thật, không ước lượng:
   - đếm dòng bảng file vs đếm khối code → phải bằng nhau
   - grep từng tên class/hàm trong doc → phải tồn tại trong repo
   - đọc lại phần client → có đủ handler + đăng ký container không
