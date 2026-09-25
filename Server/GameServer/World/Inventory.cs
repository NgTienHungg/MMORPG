using MMORPG.Shared.Dto.Db;
using MMORPG.Shared.Dto.Inventory;
using MMORPG.Shared.World.Item;

namespace MMORPG.GameServer.World
{
    /// <summary>Một ô túi trong RAM server. Struct: 30 ô là một mảng liền kề, không phải 30 object.</summary>
    public struct ItemStack
    {
        /// <summary>Id của một VẬT cụ thể. 0 khi món đồ vừa sinh ra và chưa qua lần lưu nào.</summary>
        public long ItemId;

        /// <summary>0 = ô TRỐNG. Dùng chính trường này làm dấu "trống" nên không cần một cờ riêng.</summary>
        public int TemplateId;

        public int Quantity;

        public bool IsEmpty => TemplateId == 0;
    }

    /// <summary>
    /// Túi đồ của MỘT nhân vật, sống trong RAM server. Nạp từ DB một lần lúc vào world, đổi trong
    /// RAM, ghi xuống DB khi rời world hoặc khi autosave thấy <see cref="IsDirty"/>.
    ///
    /// <b>CHỈ LUỒNG TICK được chạm vào.</b> Không có khoá nào ở đây, và đó là cố ý: mọi lối vào đều
    /// đi qua <see cref="InventoryService"/>, và service ấy xếp lệnh vào hàng đợi để tick tiêu thụ.
    /// Thêm một lối vào từ luồng khác là phải thêm khoá vào tất cả — rẻ hơn nhiều là đừng thêm.
    ///
    /// Mọi thao tác trả về <b>danh sách ô vừa đổi</b>, không phải bool: tầng gửi gói cần đúng thứ đó
    /// để dựng delta, và tầng UI cần đúng thứ đó để vẽ lại. Trả bool rồi để chỗ gọi tự đoán đã đổi ô
    /// nào là mời nó đoán sai.
    /// </summary>
    public sealed class Inventory
    {
        public const int SLOT_COUNT = 30;

        private readonly ItemStack[] _slots = new ItemStack[SLOT_COUNT];

        // Bộ đệm của TryAdd, giữ làm field và Clear() mỗi lần dùng — cùng lý do với ba bộ đệm của
        // vòng tick trong WorldService: thứ chạy thường xuyên thì hình dạng bộ nhớ của nó là thiết kế.
        private readonly List<(int Slot, int Amount)> _scratch = new();

        /// <summary>
        /// Id tạm cho đồ vừa sinh ra, đếm LÙI từ -1. Âm để không bao giờ đụng id thật của DB
        /// (AUTOINCREMENT luôn dương), nên nhìn một con số là biết nó đã qua DB hay chưa.
        /// </summary>
        private long _nextTempId = -1;

        /// <summary>Có thay đổi chưa được ghi xuống DB không. Autosave đọc cờ này.</summary>
        public bool IsDirty { get; private set; }

        public int UsedSlots
        {
            get
            {
                int count = 0;

                for (int i = 0; i < _slots.Length; i++)
                {
                    if (!_slots[i].IsEmpty)
                        count++;
                }

                return count;
            }
        }

        /// <summary>Nạp từ DB. Bỏ qua dòng hỏng thay vì ném — xem ghi chú trong thân hàm.</summary>
        public void Load(InventoryRow[] rows)
        {
            Array.Clear(_slots, 0, _slots.Length);

            foreach (InventoryRow row in rows)
            {
                // Ba loại dòng hỏng, và cả ba đều BỎ QUA chứ không ném: ô ngoài phạm vi (túi từng
                // rộng hơn rồi bị thu lại), template không còn trong bảng (item bị gỡ), số lượng vô
                // nghĩa. Ném ở đây nghĩa là một dòng DB rác chặn hẳn người chơi vào game — và người
                // chơi thì không sửa được dòng đó.
                if (row.Slot < 0 || row.Slot >= SLOT_COUNT)
                    continue;

                if (row.Quantity <= 0 || ItemConfigContainer.Find(row.TemplateId) == null)
                    continue;

                _slots[row.Slot] = new ItemStack
                {
                    ItemId = row.ItemId,
                    TemplateId = row.TemplateId,
                    Quantity = row.Quantity,
                };
            }

            // Vừa nạp từ DB thì RAM và DB đang khớp nhau — nếu để dirty thì autosave đầu tiên ghi lại
            // đúng thứ vừa đọc lên, 30 lượt ghi không có lý do.
            IsDirty = false;
        }

        /// <summary>Kết xuất để ghi DB. Chỉ ô có đồ — ô trống không cần một dòng để nói rằng nó trống.</summary>
        public InventoryRow[] ToRows()
        {
            var rows = new List<InventoryRow>(UsedSlots);

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
                });
            }

            return rows.ToArray();
        }

        /// <summary>Toàn bộ túi cho gói snapshot. Chỉ ô có đồ, cùng lý do với <see cref="ToRows"/>.</summary>
        public InventorySlotDto[] ToSnapshot()
        {
            var slots = new List<InventorySlotDto>(UsedSlots);

            for (int slot = 0; slot < _slots.Length; slot++)
            {
                if (_slots[slot].IsEmpty)
                    continue;

                slots.Add(ToDto(slot));
            }

            return slots.ToArray();
        }

        /// <summary>Một ô dưới dạng gói tin. Ô trống ra <c>TemplateId = 0</c> — client hiểu đó là "xoá ô".</summary>
        public InventorySlotDto ToDto(int slot)
        {
            return new InventorySlotDto
            {
                Slot = slot,
                TemplateId = _slots[slot].TemplateId,
                Quantity = _slots[slot].Quantity,
            };
        }

        public void MarkSaved()
        {
            IsDirty = false;
        }

        //--------------------------------------------------------------------------------------------
        // Bốn thao tác. Tất cả trả về danh sách ô vừa đổi; RỖNG = không làm gì cả.
        //--------------------------------------------------------------------------------------------

        /// <summary>
        /// Nhận đồ vào túi. Trả danh sách ô vừa đổi; RỖNG nghĩa là không nhận được gì.
        ///
        /// Hoặc nhận TRỌN, hoặc từ chối TRỌN. Nhận một phần nghĩa là phần còn lại bốc hơi mà không có
        /// gì báo — và người chơi sẽ không bao giờ tha thứ cho điều đó, kể cả khi nó chỉ là một bình
        /// máu. Phần thừa đi đâu là quyết định của chỗ GỌI, không phải của cái túi.
        /// </summary>
        public IReadOnlyList<int> TryAdd(int templateId, int quantity)
        {
            ItemConfig config = ItemConfigContainer.Find(templateId);

            if (config == null || quantity <= 0)
                return Array.Empty<int>();

            // THỬ trên bản nháp trước, chỉ ghi vào thật khi chắc chắn đủ chỗ. Cách còn lại — ghi dần
            // rồi hoàn tác khi hết chỗ — là tự viết một transaction bằng tay, và hoàn tác sai thì
            // người chơi mất đồ.
            _scratch.Clear();
            int remaining = quantity;

            // Vòng 1: dồn vào chồng đã có cùng loại, còn chỗ. Trước vòng 2 có chủ đích — dồn xong mới
            // mở ô mới, nếu không thì mỗi lần nhặt một cái là chiếm thêm một ô trong khi ô cũ còn chỗ.
            for (int slot = 0; slot < _slots.Length && remaining > 0; slot++)
            {
                if (_slots[slot].TemplateId != templateId || _slots[slot].Quantity >= config.MaxStack)
                    continue;

                int take = Math.Min(remaining, config.MaxStack - _slots[slot].Quantity);

                _scratch.Add((slot, take));
                remaining -= take;
            }

            // Vòng 2: ô trống.
            for (int slot = 0; slot < _slots.Length && remaining > 0; slot++)
            {
                if (!_slots[slot].IsEmpty)
                    continue;

                int take = Math.Min(remaining, config.MaxStack);

                _scratch.Add((slot, take));
                remaining -= take;
            }

            if (remaining > 0)
                return Array.Empty<int>();

            var changed = new List<int>(_scratch.Count);

            foreach ((int slot, int amount) in _scratch)
            {
                // Ô trống thì đây là lúc món đồ RA ĐỜI — và ra đời nghĩa là nhận một itemId mới.
                // Id âm tạm thời do server cấp; DB cấp id thật ở lần lưu kế tiếp. Client không bao
                // giờ đọc con số này, nó chỉ cần số ô.
                if (_slots[slot].IsEmpty)
                {
                    _slots[slot].TemplateId = templateId;
                    _slots[slot].ItemId = _nextTempId--;
                    _slots[slot].Quantity = 0;
                }

                _slots[slot].Quantity += amount;
                changed.Add(slot);
            }

            IsDirty = true;

            return changed;
        }

        /// <summary>
        /// Dùng một món ở ô này: giảm đúng một cái. Chỉ đồ <see cref="ItemKind.Consumable"/> —
        /// tác dụng thật (hồi máu) là việc của Phase 14, ở đây mới chỉ có phép trừ.
        /// </summary>
        public IReadOnlyList<int> TryUse(int slot)
        {
            if (!IsValidSlot(slot) || _slots[slot].IsEmpty)
                return Array.Empty<int>();

            ItemConfig config = ItemConfigContainer.Find(_slots[slot].TemplateId);

            if (config == null || config.Kind != ItemKind.Consumable)
                return Array.Empty<int>();

            return TryRemove(slot, 1);
        }

        /// <summary>Bỏ bớt số lượng ở một ô. Về 0 thì ô thành trống.</summary>
        public IReadOnlyList<int> TryRemove(int slot, int quantity)
        {
            if (!IsValidSlot(slot) || _slots[slot].IsEmpty || quantity <= 0)
                return Array.Empty<int>();

            // Bỏ nhiều hơn số đang có: từ chối TRỌN chứ không bỏ hết những gì có. Cùng luật
            // all-or-nothing của TryAdd — một yêu cầu vô nghĩa không được biến thành một hành động
            // gần đúng.
            if (quantity > _slots[slot].Quantity)
                return Array.Empty<int>();

            _slots[slot].Quantity -= quantity;

            if (_slots[slot].Quantity == 0)
                _slots[slot] = default;

            IsDirty = true;

            return new[] { slot };
        }

        /// <summary>
        /// Đổi chỗ hai ô. Không dồn chồng — kéo chồng này lên chồng kia cùng loại thì hai ô ĐỔI CHỖ,
        /// không cộng vào nhau.
        ///
        /// Vì sao không dồn: "dồn" và "đổi chỗ" là hai ý định khác nhau của người chơi, và một thao
        /// tác kéo-thả không nói được họ muốn cái nào. Đoán sai thì người chơi mất bố cục túi mà
        /// không hoàn tác được. Nút "sắp xếp túi" là chỗ của phép dồn — xem "Để dành".
        /// </summary>
        public IReadOnlyList<int> TryMove(int from, int to)
        {
            if (!IsValidSlot(from) || !IsValidSlot(to) || from == to)
                return Array.Empty<int>();

            // Kéo một ô trống đi đâu cũng là không làm gì. Trả rỗng thì không có gói nào được gửi.
            if (_slots[from].IsEmpty)
                return Array.Empty<int>();

            (_slots[from], _slots[to]) = (_slots[to], _slots[from]);

            IsDirty = true;

            return new[] { from, to };
        }

        private static bool IsValidSlot(int slot)
        {
            return slot >= 0 && slot < SLOT_COUNT;
        }
    }
}
