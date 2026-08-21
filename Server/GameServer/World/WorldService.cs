using System.Collections.Concurrent;
using MemoryPack;
using MMORPG.ServerCore;
using MMORPG.Shared.Dto.Db;
using MMORPG.Shared.Dto.World;
using MMORPG.Shared.Net;

namespace MMORPG.GameServer.World
{
    /// <summary>
    /// Sổ đăng ký mọi entity đang sống trong world: cấp entityId, spawn, despawn, tra cứu.
    /// </summary>
    public sealed class WorldService
    {
        public const int DEFAULT_CLASS_ID = 1;
        public const int DEFAULT_MAP_ID = 1;
        public const int SPAWN_X = 0;
        public const int SPAWN_Y = 0;

        // Hai sổ tra cứu: theo entityId (đường chính) và theo accountId (kiểm "tài khoản này đã có
        // entity chưa"). ConcurrentDictionary vì Spawn/Despawn chạy từ handler của nhiều session song song.
        private readonly ConcurrentDictionary<int, PlayerEntity> _entities = new();
        private readonly ConcurrentDictionary<long, int> _entityIdByAccount = new();

        private int _nextEntityId;

        public int OnlineCount => _entities.Count;

        public PlayerEntity Spawn(CharacterRow row, ClientSession owner)
        {
            // Interlocked.Increment: cộng 1 và đọc kết quả trong MỘT thao tác nguyên tử.
            // `_nextEntityId++` trần là ba bước đọc–cộng–ghi: hai handler chạy song song
            // có thể cùng đọc một giá trị và hai entity nhận trùng id.
            int entityId = Interlocked.Increment(ref _nextEntityId);
            var entity = new PlayerEntity(entityId, row, owner);

            _entities[entityId] = entity;
            _entityIdByAccount[entity.AccountId] = entity.EntityId;

            Log.Info($"Spawn {entity.Name.Cyan()} entity {entityId.ToString().Green()} " + $"tại map {entity.MapId} ({entity.X:0.##}, {entity.Y:0.##}) — " + $"{OnlineCount} người trong world");

            // Người mới cần biết ai đang có mặt — gửi một loạt EntitySpawn về từng người cũ.
            foreach (PlayerEntity other in _entities.Values)
            {
                if (other.EntityId == entity.EntityId)
                    continue;

                owner.SendData(NetCmd.EntitySpawn, ToSpawnNotice(other));
            }

            // Và người cũ cần biết có người mới.
            Broadcast(NetCmd.EntitySpawn, ToSpawnNotice(entity), exceptEntityId: entity.EntityId);

            return entity;
        }

        public void Despawn(PlayerEntity entity)
        {
            _entities.TryRemove(entity.EntityId, out _);
            _entityIdByAccount.TryRemove(entity.AccountId, out _);

            Broadcast(
                NetCmd.EntityDespawn,
                new EntityDespawnNotice { EntityId = entity.EntityId },
                exceptEntityId: entity.EntityId
            );

            Log.Info($"Despawn {entity.Name.Cyan()} entity {entity.EntityId} — còn {OnlineCount} người");
        }

        /// <summary>
        /// Tài khoản này đã có entity trong world chưa. Cần vì một tài khoản có thể đăng nhập
        /// ở hai chỗ trong khe thời gian trước khi session cũ kịp bị đá.
        /// </summary>
        public bool TryGetByAccount(long accountId, out PlayerEntity entity)
        {
            entity = null;

            return _entityIdByAccount.TryGetValue(accountId, out int entityId) && _entities.TryGetValue(entityId, out entity);
        }

        /// <summary>Gửi một gói cho mọi entity đang trong world, trừ một người (thường là nguồn tin).</summary>
        private void Broadcast<T>(NetCmd cmd, T dto, int exceptEntityId) where T : IMemoryPackable<T>
        {
            // Duyệt ConcurrentDictionary trong lúc có thể có Spawn/Despawn song song là hợp lệ:
            // iterator "weakly consistent" — không ném lỗi, chỉ có thể thiếu/thừa đúng entity
            // đang vào/ra tại khoảnh khắc đó. Với gói thông báo thì sai một tick là vô hại.
            foreach (PlayerEntity entity in _entities.Values)
            {
                if (entity.EntityId == exceptEntityId)
                    continue;

                entity.Owner?.SendData(cmd, dto);
            }
        }

        private static EntitySpawnNotice ToSpawnNotice(PlayerEntity entity)
        {
            return new EntitySpawnNotice
            {
                EntityId = entity.EntityId,
                Name = entity.Name,
                ClassId = entity.ClassId,
                X = entity.X,
                Y = entity.Y,
            };
        }

        /// <summary>Game loop gọi mỗi tick: mô phỏng mọi entity rồi báo vị trí cho chính chủ.</summary>
        public void Tick(float dt)
        {
            // Vòng 1: tích phân TẤT CẢ trước. Trộn tích phân với gửi thì người gửi trước
            // mang vị trí cũ của người tích phân sau — hai client nhìn cùng tick ra hai bức tranh.
            foreach (PlayerEntity entity in _entities.Values)
                entity.Integrate(dt);

            // Vòng 2: gửi. MoveState cho chính chủ (đường reconciliation),
            // WorldSnapshot về những người còn lại (đường interpolation).
            foreach (PlayerEntity entity in _entities.Values)
            {
                if (entity.Owner == null)
                    continue;

                // Gửi nguyên State: thêm trường vào MoveState sau này là tự động lên dây,
                // không phải nhớ quay lại đây chép thêm một dòng.
                entity.Owner.SendData(NetCmd.MoveState, new MoveStateResponse
                    {
                        LastInputSeq = entity.LastInputSeq,
                        State = entity.State,
                    }
                );

                entity.Owner.SendData(NetCmd.WorldSnapshot, BuildSnapshotFor(entity));
            }
        }

        /// <summary>Mọi entity trừ chính người nhận. O(n²) mỗi tick — chấp nhận cho tới khi có AOI.</summary>
        private WorldSnapshotNotice BuildSnapshotFor(PlayerEntity viewer)
        {
            var states = new List<EntityState>(_entities.Count - 1);

            foreach (PlayerEntity entity in _entities.Values)
            {
                if (entity.EntityId == viewer.EntityId)
                    continue;

                states.Add(new EntityState { EntityId = entity.EntityId, X = entity.X, Y = entity.Y });
            }

            return new WorldSnapshotNotice { States = states.ToArray() };
        }
    }
}
