using System.Collections.Concurrent;
using MemoryPack;
using MMORPG.ServerCore;
using MMORPG.Shared.Dto.Db;
using MMORPG.Shared.Dto.World;
using MMORPG.Shared.Net;
using MMORPG.Shared.World;

namespace MMORPG.GameServer.World
{
    /// <summary>
    /// Sổ đăng ký mọi entity đang sống trong world: cấp entityId, spawn, despawn, tra cứu.
    /// </summary>
    public sealed class WorldService
    {
        public const int DEFAULT_CLASS_ID = 1;

        /// <summary>
        /// Bề ngang một cột tầm nhìn. Tầm nhìn = 3 cột → bán kính bảo đảm 12 unit mỗi bên, rộng hơn
        /// nửa màn hình (~9 unit) một chút.
        ///
        /// Chỉ chia theo trục X: map cao ~11 unit mà một màn hình đã cao 10, nên chia trục Y là tốn
        /// thêm một chiều trong khoá để nhận về một phép lọc gần như không lọc gì.
        /// </summary>
        private const float AOI_COLUMN_WIDTH = 12f;

        // Ba bộ đệm của vòng tick, giữ làm field và Clear() mỗi lần dùng. Cấp phát mới mỗi tick là
        // rác GC đều đặn 20 lần/giây suốt đời server — thứ chạy mỗi tick thì hình dạng bộ nhớ của nó
        // là một phần thiết kế. Chỉ luồng tick chạm vào, nên không cần đồng bộ gì.
        private readonly Dictionary<(int MapId, int Column), List<PlayerEntity>> _columns = new();
        private readonly List<PlayerEntity> _visibleNow = new();

        // Cùng nội dung với _visibleNow nhưng chỉ id, để phép "ai vừa rời tầm nhìn" hỏi trong O(1).
        // Không có nó thì vòng RemoveWhere phải quét cả _visibleNow cho MỖI id cũ — O(n·m) mỗi người
        // mỗi tick, tức O(n²·m) cho cả server, và đó đúng là con số AOI sinh ra để giết.
        private readonly HashSet<int> _visibleIds = new();

        // Hai sổ tra cứu: theo entityId (đường chính) và theo accountId (kiểm "tài khoản này đã có
        // entity chưa"). ConcurrentDictionary vì Spawn/Despawn chạy từ handler của nhiều session song song.
        private readonly ConcurrentDictionary<int, PlayerEntity> _entities = new();
        private readonly ConcurrentDictionary<long, int> _entityIdByAccount = new();

        // ConcurrentQueue vì bên ghi là luồng đọc phím còn bên đọc là luồng tick. Chỉ hàng đợi này
        // đi qua ranh giới luồng; entity thì không ai ngoài tick được chạm vào.
        private readonly ConcurrentQueue<ForcedActionCommand> _forcedActions = new();

        private int _nextEntityId;

        public int OnlineCount => _entities.Count;

        // Sổ tra map, không phải MỘT map. "Đang đứng ở map nào" là dữ liệu của từng người chơi —
        // giữ một MapGrid ở đây là ép cả server chỉ có một map, ngay ở tầng kiểu dữ liệu.
        private readonly MapRegistry _maps;

        public WorldService(MapRegistry maps)
        {
            _maps = maps;
        }

        public PlayerEntity Spawn(CharacterRow row, ClientSession owner)
        {
            // Interlocked.Increment: cộng 1 và đọc kết quả trong MỘT thao tác nguyên tử.
            // `_nextEntityId++` trần là ba bước đọc–cộng–ghi: hai handler chạy song song
            // có thể cùng đọc một giá trị và hai entity nhận trùng id.
            int entityId = Interlocked.Increment(ref _nextEntityId);
            MapGrid map = _maps.ResolveFor(row);
            var entity = new PlayerEntity(entityId, row, owner, map);

            _entities[entityId] = entity;
            _entityIdByAccount[entity.AccountId] = entity.EntityId;

            Log.Info($"Spawn {entity.Name.Cyan()} entity {entityId.ToString().Green()} " +
                     $"tại map {entity.MapId} ({entity.X:0.##}, {entity.Y:0.##}) — {OnlineCount} người trong world");

            // Không thông báo gì ở đây nữa. Ai thấy được người này thì tick kế tiếp sẽ tự phát hiện —
            // "vừa vào world" chỉ là MỘT cách để lọt vào tầm nhìn ai đó, không phải cách duy nhất.
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

            return _entityIdByAccount.TryGetValue(accountId, out int entityId) && _entities.TryGetValue(entityId, out entity);
        }

        /// <summary>
        /// Gửi một gói cho mọi entity đang đứng CÙNG MỘT MAP, trừ một người (thường là nguồn tin).
        ///
        /// mapId là THAM SỐ chứ không đọc từ dto: lúc chuyển map, gói EntityDespawn phải gửi cho
        /// người ở map CŨ trong khi entity đã mang id map MỚI rồi.
        /// </summary>
        private void Broadcast<T>(NetCmd cmd, T dto, int mapId, int exceptEntityId) where T : IMemoryPackable<T>
        {
            // Duyệt ConcurrentDictionary trong lúc có thể có Spawn/Despawn song song là hợp lệ:
            // iterator "weakly consistent" — không ném lỗi, chỉ có thể thiếu/thừa đúng entity
            // đang vào/ra tại khoảnh khắc đó. Với gói thông báo thì sai một tick là vô hại.
            foreach (PlayerEntity entity in _entities.Values)
            {
                if (entity.EntityId == exceptEntityId)
                    continue;

                // Người ở map khác mà nhận EntitySpawn thì client dựng ra một bóng ma không bao giờ
                // có snapshot để cập nhật — đứng im tới lúc người kia thoát.
                if (entity.MapId != mapId)
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

                // Hiện ra là đã đúng hướng mặt và đúng tư thế, không quay đầu một nhịp sau.
                FacingLeft = entity.State.FacingLeft,
                Crouching = entity.State.Crouching,
                Action = entity.State.Action,
            };
        }

        /// <summary>Game loop gọi mỗi tick: mô phỏng mọi entity rồi báo vị trí cho chính chủ.</summary>
        public void Tick(float dt)
        {
            // Vòng 0: tiêu thụ lệnh đến từ ngoài. Đặt trước vòng tích phân để trạng thái vừa bị áp
            // đặt được chính tick này diễn tiến (đếm ngược, khoá di chuyển), thay vì trễ một nhịp.
            while (_forcedActions.TryDequeue(out ForcedActionCommand command))
            {
                foreach (PlayerEntity entity in _entities.Values)
                {
                    if (command.BypassRules)
                        entity.Revive();
                    else
                        entity.ForceAction(command.Action);
                }
            }

            // Vòng 1: tích phân TẤT CẢ trước. Trộn tích phân với gửi thì người gửi trước
            // mang vị trí cũ của người tích phân sau — hai client nhìn cùng tick ra hai bức tranh.
            foreach (PlayerEntity entity in _entities.Values)
                entity.Integrate(dt);

            // Vòng 1b: cổng. SAU tích phân vì vị trí phải chốt xong mới biết có bước vào cổng không;
            // TRƯỚC dựng chỉ mục vì chính tick này chỉ mục phải thấy họ đã ở map mới — nhờ vậy phép
            // diff ở vòng 3 báo tin ngay, không trễ thêm một nhịp.
            foreach (PlayerEntity entity in _entities.Values)
                TryTakePortal(entity);

            // Vòng 2: dựng lại chỉ mục cột từ đầu. O(n), và không có trạng thái nào sống qua tick nên
            // không tồn tại lớp bug "chỉ mục lệch thực tế" (quên gỡ cột cũ, entity chết còn nằm lại...).
            _columns.Clear();

            foreach (PlayerEntity entity in _entities.Values)
            {
                (int, int) key = ColumnOf(entity);

                if (!_columns.TryGetValue(key, out List<PlayerEntity> column))
                {
                    column = new List<PlayerEntity>();
                    _columns[key] = column;
                }

                column.Add(entity);
            }

            // Vòng 3: với từng người — tầm nhìn mới, so với tầm nhìn cũ, phát spawn/despawn, gửi trạng thái.
            foreach (PlayerEntity viewer in _entities.Values)
            {
                if (viewer.Owner == null)
                    continue;

                CollectVisible(viewer);

                // (1) Ai mới lọt vào tầm nhìn → giới thiệu họ với viewer.
                foreach (PlayerEntity seen in _visibleNow)
                {
                    if (!viewer.Visible.Contains(seen.EntityId))
                        viewer.Owner.SendData(NetCmd.EntitySpawn, ToSpawnNotice(seen));
                }

                // (2) Ai vừa rời tầm nhìn → báo biến mất. PHẢI làm trước khi ghi đè tập Visible;
                //     đảo thứ tự thì tập cũ mất trước khi kịp so, và không ai despawn bao giờ.
                viewer.Visible.RemoveWhere(id =>
                {
                    if (_visibleIds.Contains(id))
                        return false;

                    viewer.Owner.SendData(NetCmd.EntityDespawn, new EntityDespawnNotice { EntityId = id });

                    return true;
                });

                // (3) Chốt tập mới.
                foreach (PlayerEntity seen in _visibleNow)
                    viewer.Visible.Add(seen.EntityId);

                // Vị trí của chính mình vẫn đi đường riêng — đường reconciliation, không dính AOI:
                // bạn luôn nhìn thấy chính mình.
                viewer.Owner.SendData(NetCmd.MoveState, new MoveStateResponse
                    {
                        LastInputSeq = viewer.LastInputSeq,
                        State = viewer.State,
                    }
                );

                viewer.Owner.SendData(NetCmd.WorldSnapshot, BuildSnapshot());
            }
        }



        private static (int MapId, int Column) ColumnOf(PlayerEntity entity)
        {
            // Floor chứ không phải cast: toạ độ X âm (nửa trái của map) phải rơi về cột bên trái,
            // không gom hết về cột 0 — cast cắt về phía 0 nên -5 và +5 sẽ cùng ra cột 0.
            return (entity.MapId, (int)MathF.Floor(entity.State.X / AOI_COLUMN_WIDTH));
        }

        /// <summary>
        /// Đổ vào <see cref="_visibleNow"/> mọi entity trong 3 cột quanh viewer, cùng map, trừ chính
        /// viewer.
        ///
        /// Lọc MapId là ranh giới CỨNG: hai người ở hai map khác nhau không bao giờ thấy nhau dù toạ
        /// độ X của họ bằng nhau. Hiện chỉ có một map nên nó chưa lọc gì — nhưng viết bây giờ rẻ hơn
        /// nhiều so với đi tìm lý do người ở hang động nhìn thấy người ở đồng cỏ.
        /// </summary>
        private void CollectVisible(PlayerEntity viewer)
        {
            _visibleNow.Clear();
            _visibleIds.Clear();

            (int mapId, int column) = ColumnOf(viewer);

            for (int offset = -1; offset <= 1; offset++)
            {
                if (!_columns.TryGetValue((mapId, column + offset), out List<PlayerEntity> cell))
                    continue;

                foreach (PlayerEntity entity in cell)
                {
                    if (entity.EntityId == viewer.EntityId)
                        continue;

                    _visibleNow.Add(entity);
                    _visibleIds.Add(entity.EntityId);
                }
            }
        }

        /// <summary>Snapshot dựng từ tập vừa gom — không còn duyệt toàn bộ world như Phase 7.</summary>
        private WorldSnapshotNotice BuildSnapshot()
        {
            var states = new EntityState[_visibleNow.Count];

            for (int i = 0; i < _visibleNow.Count; i++)
            {
                PlayerEntity entity = _visibleNow[i];

                states[i] = new EntityState
                {
                    EntityId = entity.EntityId,
                    X = entity.X,
                    Y = entity.Y,
                    FacingLeft = entity.State.FacingLeft,
                    Crouching = entity.State.Crouching,
                    Action = entity.State.Action,
                };
            }

            return new WorldSnapshotNotice { States = states };
        }

         /// <summary>
        /// Đưa entity qua cổng nếu nó vừa bước vào một cái. CHỈ GỌI TỪ LUỒNG TICK.
        ///
        /// Chú ý cái KHÔNG có ở đây: không gửi EntityDespawn cho ai, không gửi EntitySpawn cho ai,
        /// không dọn danh sách nào cả. Đổi MapId là đổi khoá chỉ mục, và phép diff tầm nhìn ở vòng 3 tự
        /// sinh ra đủ bốn chiều thông báo. Đó là toàn bộ lý do bước này nằm SAU Bước 1.
        /// </summary>
        private void TryTakePortal(PlayerEntity entity)
        {
            Portal portal = entity.TakePortal();

            if (portal == null)
                return;

            if (!_maps.TryGet(portal.ToMapId, out MapGrid target))
            {
                // Dữ liệu hỏng chứ không phải người chơi làm sai — đứng yên còn hơn ném họ đi đâu đó.
                Log.Warn($"Cổng ở map {entity.MapId} trỏ tới map {portal.ToMapId} không có trong registry.");
                return;
            }

            SpawnPoint spawn = target.FindSpawn(portal.ToSpawnId);
            int fromMapId = entity.MapId;

            entity.MoveToMap(target, spawn.X, spawn.Y);

            // Gói DUY NHẤT phải gửi tay ở đây — và nó không nói với ai ngoài chính người đi.
            entity.Owner?.SendData(NetCmd.MapChanged, new MapChangedNotice
            {
                MapId = entity.MapId,
                State = entity.State,
            });

            Log.Info($"{entity.Name.Cyan()} map {fromMapId} → {entity.MapId.ToString().Green()} " +
                     $"tại \"{portal.ToSpawnId}\" ({entity.X:0.##}, {entity.Y:0.##})");
        }

        /// <summary>
        /// Xin gây trạng thái cho TẤT CẢ entity trong world. Gọi được từ luồng bất kỳ — lệnh chỉ
        /// được xếp hàng ở đây, và chỉ thật sự có hiệu lực ở đầu tick kế tiếp.
        ///
        /// Vì sao không sửa thẳng entity tại đây: MoveState là struct hơn 40 byte, ghi nó trong lúc
        /// luồng tick đang đọc thì người đọc có thể thấy nửa cũ nửa mới. Không exception, không log,
        /// chỉ là một tick mang toạ độ vô nghĩa — loại lỗi đắt nhất để tìm.
        /// </summary>
        public void EnqueueForceAll(ActionState action)
        {
            _forcedActions.Enqueue(new ForcedActionCommand(action, bypassRules: false));
        }

        public void EnqueueReviveAll()
        {
            _forcedActions.Enqueue(new ForcedActionCommand(ActionState.None, bypassRules: true));
        }

        /// <summary>
        /// Một lệnh đổi trạng thái đến từ NGOÀI luồng tick. Hiện chỉ có nút thử trên console phát ra;
        /// từ Phase 14 thì sát thương của quái và của người chơi khác cũng đi đường này.
        /// </summary>
        private readonly struct ForcedActionCommand
        {
            public readonly ActionState Action;

            /// <summary>Bỏ qua bảng chuyển tiếp — chỉ dùng cho hồi sinh, vì Die không có lối ra hợp lệ.</summary>
            public readonly bool BypassRules;

            public ForcedActionCommand(ActionState action, bool bypassRules)
            {
                Action = action;
                BypassRules = bypassRules;
            }
        }
    }
}
