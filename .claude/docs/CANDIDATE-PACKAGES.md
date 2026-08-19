# CANDIDATE-PACKAGES — Code có thể tách thành package `com.hungnt.*`

> Sổ ghi chép. Trong lúc làm, gặp đoạn code **hạ tầng, tái dùng được, không dính nghiệp vụ game** thì ghi vào đây.
> Đừng tách package ngay lúc vừa viết xong — API chưa ổn định, tách sớm là tự trói tay mình.
>
> **Tiêu chí đủ điều kiện tách:**
> 1. Dùng được ở một game hoàn toàn khác mà không phải sửa gì.
> 2. Không tham chiếu tới bất kỳ type nào của `MMORPG.*`.
> 3. API đã đứng yên qua ít nhất 2 phase (không phải sửa chữ ký nữa).
> 4. Có test hoặc ít nhất một demo chạy được độc lập.
>
> Quy trình tách: xem skill `hungnt-package-workflow`.

---

## Đang theo dõi

| Ứng viên | Xuất hiện ở | Tiêu chí đạt | Ghi chú |
|----------|-------------|--------------|---------|
| **`com.hungnt.network`** — `ITransport`, `TcpTransport`, `NetDispatcher`, `NetHandlerAttribute` | Phase 1–2 | 1 ✅ · 2 ⬜ (đang dính `NetCmd` của `MMORPG.Shared`) · 3 ⬜ · 4 ⬜ | Ứng viên mạnh nhất. Để tách được phải làm dispatcher generic theo kiểu enum, hoặc dùng `int` ở tầng package. **Kế hoạch: Phase 15.** |
| **Interpolation buffer** cho vị trí entity | Phase 7 | ⬜ | Nếu viết đủ tổng quát (buffer snapshot theo timestamp + nội suy) thì dùng lại được ở mọi game multiplayer |
| **Spatial grid / AOI** | Phase 9 | ⬜ | Chia không gian 2D thành ô + truy vấn hàng xóm — thuần thuật toán, không dính Unity. Có thể là package chung cho cả client và server (netstandard) |
| **Fixed tick loop** server | Phase 6 | ⬜ | Nhỏ, có thể chỉ là 1 file — cân nhắc gộp vào `com.hungnt.network` thay vì package riêng |
| **Motor kinematic 2D** (trọng lực, nhảy, va chạm tile) trong `Shared` | Phase 8 | ⬜ | Thuần toán, netstandard, chạy được cả 2 bên. Nếu viết đủ tổng quát thì là nền cho mọi game platformer có server authoritative |
| **Stat pipeline** (base + điểm cộng + trang bị + buff → recompute) | Phase 12 | ⬜ | Khuôn mẫu này lặp lại ở mọi game có chỉ số. Tách được nếu không hard-code tên chỉ số |

---

## Đã tách

*(chưa có)*

---

## Đã cân nhắc và quyết định KHÔNG tách

| Thứ | Vì sao không |
|-----|--------------|
| `PacketFrame` / `FrameReader` | Nằm ở `Server/Shared` (netstandard2.1) — client Unity đã dùng chung qua DLL rồi, đóng thêm package UPM là trùng lặp |
| `NetPayload` | Như trên, và nó gắn chặt với lựa chọn MemoryPack + LZ4 của dự án này |
