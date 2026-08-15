using System.Collections.Concurrent;
using MMORPG.ServerCore;
using MMORPG.Shared.Dto.Db;

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

            Log.Info($"Spawn {entity.Name.Cyan()} entity {entityId.ToString().Green()} " +
                     $"tại map {entity.MapId} ({entity.X:0.##}, {entity.Y:0.##}) — " +
                     $"{OnlineCount} người trong world");

            return entity;
        }

        public void Despawn(PlayerEntity entity)
        {
            _entities.TryRemove(entity.EntityId, out _);
            _entityIdByAccount.TryRemove(entity.AccountId, out _);

            Log.Info($"Despawn {entity.Name.Cyan()} entity {entity.EntityId} — còn {OnlineCount} người");
        }

        /// <summary>
        /// Tài khoản này đã có entity trong world chưa. Cần vì một tài khoản có thể đăng nhập
        /// ở hai chỗ trong khe thời gian trước khi session cũ kịp bị đá.
        /// </summary>
        public bool TryGetByAccount(long accountId, out PlayerEntity entity)
        {
            entity = null;

            return _entityIdByAccount.TryGetValue(accountId, out int entityId) &&
                   _entities.TryGetValue(entityId, out entity);
        }
    }
}
