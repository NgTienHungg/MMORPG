using System.Collections.Concurrent;
using MMORPG.GameServer.Db;
using MMORPG.ServerCore;
using MMORPG.Shared.Db;
using MMORPG.Shared.Dto.Db;
using MMORPG.Shared.Dto.Inventory;
using MMORPG.Shared.Net;
using MMORPG.Shared.World.Item;

namespace MMORPG.GameServer.World
{
    /// <summary>
    /// Toàn bộ nghiệp vụ túi đồ: nạp lúc vào world, gửi snapshot/delta, autosave, lưu lúc rời world.
    ///
    /// Handler không chứa gì ngoài lời gọi vào đây — nếu có ngày một hàm trong
    /// <c>InventoryHandler</c> dài quá mười dòng thì nghiệp vụ đang rò rỉ ra khỏi chỗ này.
    /// </summary>
    public sealed class InventoryService
    {
        /// <summary>
        /// Bao lâu ghi DB một lần nếu túi bẩn. Đây là con số của một ĐÁNH ĐỔI, không phải một hằng
        /// tuỳ ý: nó là số giây tối đa người chơi mất khi server chết đột ngột, đổi lấy số lượt ghi
        /// DB tiết kiệm được. Hạ xuống 5 thì mất ít hơn, ghi nhiều hơn.
        /// </summary>
        private const float AUTOSAVE_SECONDS = 30f;

        private readonly DbClient _dbClient;

        // Bên GHI là luồng đọc phím, bên ĐỌC là luồng tick. Chỉ hàng đợi này đi qua ranh giới luồng;
        // Inventory thì không ai ngoài tick được chạm vào. Cùng khuôn với _forcedActions ở WorldService.
        private readonly ConcurrentQueue<GrantCommand> _grants = new();

        private float _autosaveTimer;

        public InventoryService(DbClient dbClient)
        {
            _dbClient = dbClient;
        }

        /// <summary>Lệnh phát đồ, xếp hàng chờ tick tiêu thụ.</summary>
        private readonly struct GrantCommand
        {
            public readonly int TemplateId;
            public readonly int Quantity;

            public GrantCommand(int templateId, int quantity)
            {
                TemplateId = templateId;
                Quantity = quantity;
            }
        }

        /// <summary>
        /// Xin phát đồ cho TẤT CẢ người trong world. Gọi được từ luồng bất kỳ — lệnh chỉ được xếp
        /// hàng ở đây, và chỉ thật sự có hiệu lực ở đầu tick kế tiếp.
        /// </summary>
        public void EnqueueGrantAll(int templateId, int quantity)
        {
            _grants.Enqueue(new GrantCommand(templateId, quantity));
        }

        /// <summary>
        /// Nạp túi từ DB và gửi snapshot. Gọi từ <c>CharacterService.EnterWorldAsync</c>, SAU khi
        /// entity đã spawn — snapshot là gói đầu tiên client nhận về túi, và nó phải tới sau
        /// EnterWorldResponse để client đã có bảng item mà tra.
        /// </summary>
        public async Task LoadAsync(PlayerEntity entity)
        {
            try
            {
                var response = await _dbClient.CallAsync<InventoryLoadRequest, InventoryLoadResponse>(
                    DbCmd.InventoryLoad, new InventoryLoadRequest { CharacterId = entity.CharacterId });

                entity.Inventory.Load(response.Items);
            }
            catch (DbUnavailableException ex)
            {
                // Vào world với túi RỖNG thì tệ hơn nhiều so với không vào được: người chơi sẽ tưởng
                // mất đồ, và lần autosave kế tiếp sẽ GHI ĐÈ cái túi rỗng ấy xuống DB — mất thật.
                Log.Error($"Không nạp được túi của {entity.Name.Cyan()}: {ex.Message}. Đá khỏi world.");
                entity.Owner?.Kick("Không đọc được dữ liệu nhân vật. Thử lại sau giây lát.");

                return;
            }

            Log.Info($"Túi của {entity.Name.Cyan()}: {entity.Inventory.UsedSlots}/{Inventory.SLOT_COUNT} ô");

            entity.Owner?.SendData(NetCmd.InventorySnapshot, new InventorySnapshotNotice
            {
                Slots = entity.Inventory.ToSnapshot(),
            });
        }

        /// <summary>
        /// Ghi túi xuống DB nếu bẩn. Gọi từ <c>CharacterService.LeaveWorldAsync</c> TRƯỚC khi
        /// <c>Despawn</c> — sau đó thì entity đã ra khỏi sổ và không ai còn cầm cái túi nữa.
        /// </summary>
        public async Task SaveAsync(PlayerEntity entity)
        {
            if (!entity.Inventory.IsDirty)
                return;

            try
            {
                await _dbClient.CallAsync<InventorySaveRequest, DbOkResponse>(
                    DbCmd.InventorySave, new InventorySaveRequest
                    {
                        CharacterId = entity.CharacterId,
                        Items = entity.Inventory.ToRows(),
                    });

                entity.Inventory.MarkSaved();
            }
            catch (DbUnavailableException ex)
            {
                // Cùng lý do với SavePosition ở Phase 5: mất một lần lưu thì khó chịu, nhưng làm sập
                // đường ngắt kết nối thì tệ hơn — session không dọn được, entity treo lại mãi mãi.
                Log.Warn($"Không lưu được túi của {entity.Name.Cyan()}: {ex.Message}");
            }
        }

        //--------------------------------------------------------------------------------------------
        // Ba thao tác do client xin. Mọi phép kiểm biên đã làm trong Inventory — ở đây chỉ còn việc
        // gửi delta khi có gì đó thật sự đổi.
        //--------------------------------------------------------------------------------------------

        public void Use(PlayerEntity entity, int slot)
        {
            SendDelta(entity, entity.Inventory.TryUse(slot));
        }

        public void Drop(PlayerEntity entity, int slot, int quantity)
        {
            IReadOnlyList<int> changed = entity.Inventory.TryRemove(slot, quantity);

            if (changed.Count > 0)
                Log.Debug($"{entity.Name} vứt {quantity} ở ô {slot}");

            SendDelta(entity, changed);
        }

        public void Move(PlayerEntity entity, int from, int to)
        {
            SendDelta(entity, entity.Inventory.TryMove(from, to));
        }

        /// <summary>
        /// Gửi những ô vừa đổi. Danh sách RỖNG thì KHÔNG gửi gì — một gói delta không có ô nào bắt
        /// client vẽ lại vì không có lý do, và tệ hơn là nó nói dối rằng có chuyện vừa xảy ra.
        /// </summary>
        private static void SendDelta(PlayerEntity entity, IReadOnlyList<int> changedSlots)
        {
            if (changedSlots.Count == 0)
                return;

            var slots = new InventorySlotDto[changedSlots.Count];

            for (int i = 0; i < changedSlots.Count; i++)
                slots[i] = entity.Inventory.ToDto(changedSlots[i]);

            entity.Owner?.SendData(NetCmd.InventoryDelta, new InventoryDeltaNotice { Slots = slots });
        }

        //--------------------------------------------------------------------------------------------
        // Vòng tick
        //--------------------------------------------------------------------------------------------

        /// <summary>
        /// Gọi mỗi tick từ <c>WorldService.Tick</c>: tiêu thụ lệnh phát đồ rồi đếm giờ autosave.
        ///
        /// Nhận cả danh sách entity thay vì tự giữ một sổ riêng: "ai đang trong world" đã có đúng một
        /// nguồn là <c>WorldService</c>, và sổ thứ hai thì sớm muộn lệch với sổ thứ nhất.
        ///
        /// ICollection chứ không IReadOnlyCollection: <c>ConcurrentDictionary.Values</c> trả về
        /// <c>ICollection</c>, và hai interface ấy KHÔNG kế thừa nhau trong .NET.
        /// </summary>
        public void Tick(float dt, ICollection<PlayerEntity> entities)
        {
            while (_grants.TryDequeue(out GrantCommand command))
                GrantAll(command, entities);

            _autosaveTimer += dt;

            if (_autosaveTimer < AUTOSAVE_SECONDS)
                return;

            _autosaveTimer = 0f;

            foreach (PlayerEntity entity in entities)
            {
                if (!entity.Inventory.IsDirty)
                    continue;

                // KHÔNG await trong vòng tick: một lượt đi-về DBServer là vài ms, nhân với số người
                // online là cả nhịp tim server đứng lại. Bắn đi rồi quên — SaveAsync tự log khi hỏng.
                _ = SaveAsync(entity);
            }
        }

        private static void GrantAll(GrantCommand command, ICollection<PlayerEntity> entities)
        {
            ItemConfig config = ItemConfigContainer.Find(command.TemplateId);

            if (config == null)
            {
                Log.Warn($"Phát đồ: không có template {command.TemplateId.ToString().Red()} trong bảng.");
                return;
            }

            foreach (PlayerEntity entity in entities)
            {
                IReadOnlyList<int> changed = entity.Inventory.TryAdd(command.TemplateId, command.Quantity);

                if (changed.Count == 0)
                {
                    Log.Warn($"Túi của {entity.Name.Cyan()} đầy, không nhận {config.Name}");
                    continue;
                }

                Log.Info($"+{command.Quantity} {config.Name.Cyan()} → ô {string.Join(", ", changed)} " +
                         $"({entity.Name})");

                SendDelta(entity, changed);
            }
        }
    }
}
