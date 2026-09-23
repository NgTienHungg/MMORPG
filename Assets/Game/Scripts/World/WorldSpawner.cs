using System.Collections.Generic;
using HungNT;
using MMORPG.Client.Config;
using MMORPG.Client.Network.Handlers;
using MMORPG.Shared.Dto.Character;
using MMORPG.Shared.Dto.World;
using MMORPG.Shared.World.Character;
using MMORPG.Shared.World.Map;
using UnityEngine;
using VContainer;

namespace MMORPG.Client.World
{
    /// <summary>
    /// Dựng và gỡ biểu diễn hình ảnh (GameObject) cho nhân vật của chính mình, trỏ camera bám theo.
    /// </summary>
    public class WorldSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject _playerPrefab;
        [SerializeField] private GameObject _remotePrefab;
        [SerializeField] private Transform _entityRoot;
        [SerializeField] private CameraFollow _cameraFollow;
        [SerializeField] private MapView _mapView;

        private WorldApi _worldApi;
        private WorldNetHandler _worldNetHandler;
        private LocalPlayer _localPlayer;
        private MapService _mapService;
        private ConfigService _configService;

        private GameObject _localPlayerObject;
        private readonly Dictionary<int, RemotePlayerView> _remotes = new();

        [Inject]
        public void Construct(WorldApi worldApi, WorldNetHandler worldNetHandler, LocalPlayer localPlayer,
            MapService mapService, Config.ConfigService configService)
        {
            _worldApi = worldApi;
            _worldNetHandler = worldNetHandler;
            _localPlayer = localPlayer;
            _mapService = mapService;
            _configService = configService;
        }

        private void Start()
        {
            _worldNetHandler.OnEntitySpawn += OnEntitySpawn;
            _worldNetHandler.OnEntityDespawn += OnEntityDespawn;
            _worldNetHandler.OnSnapshot += OnSnapshot;
            _worldNetHandler.OnMapChanged += OnMapChanged;
        }

        private void OnDestroy()
        {
            if (_worldNetHandler == null)
                return;

            _worldNetHandler.OnEntitySpawn -= OnEntitySpawn;
            _worldNetHandler.OnEntityDespawn -= OnEntityDespawn;
            _worldNetHandler.OnSnapshot -= OnSnapshot;
            _worldNetHandler.OnMapChanged -= OnMapChanged;
        }

        public void SpawnLocalPlayer(EnterWorldResponse response)
        {
            if (_localPlayerObject != null)
                DespawnLocalPlayer();

            // Nạp LUẬT trước, dựng HÌNH sau, rồi mới Init motor: motor cần lưới va chạm ngay từ tick
            // dự đoán đầu tiên.
            MapGrid map = _mapService.Load(response.MapId);

            _mapView.Show(map);

            _localPlayerObject = Instantiate(_playerPrefab, new Vector3(response.X, response.Y), Quaternion.identity, _entityRoot);
            _localPlayerObject.name = $"Player_{response.EntityId}_{response.Name}";

            // Prefab sinh lúc runtime — VContainer không tự inject. Đưa phụ thuộc vào tay.
            //
            // Bảng tra được CHÍNH Ở ĐÂY chứ không truyền CharacterConfig từ ngoài vào: container đã
            // nạp xong ở WorldPresenter một nhịp trước, và tra tại chỗ dùng thì không có đường nào để
            // một chỗ gọi khác đưa vào bộ số của lớp nhân vật khác.
            var motor = _localPlayerObject.GetComponent<PlayerMotor>();
            motor.Init(_worldApi, _worldNetHandler, new Vector2(response.X, response.Y),
                CharacterConfigContainer.Get(response.ClassId), _configService.World, map);

            _cameraFollow.SetTarget(_localPlayerObject.transform);

            // Bám mượt là để đuổi theo người đang chạy; ở đây chưa có gì để đuổi, nên nhảy thẳng tới
            // nơi — không có dòng này thì frame đầu của world là cảnh camera bay từ gốc toạ độ tới.
            _cameraFollow.SnapToTarget();

            this.Log($"Vào map {response.MapId} tại {response.X:0.##}:{response.Y:0.##} - entity {response.EntityId}");
        }

        public void DespawnLocalPlayer()
        {
            if (_localPlayerObject == null)
                return;

            Destroy(_localPlayerObject);
            _localPlayerObject = null;
            _cameraFollow.SetTarget(null);

            // xoá hết tất cả các player khác trong session này
            DespawnAllRemotes();
            _mapView.Clear();
        }

        private void OnEntitySpawn(EntitySpawnNotice notice)
        {
            // Gói về chính mình (nếu có) hoặc gói lặp — bỏ qua, không nhân bản.
            if (notice.EntityId == _localPlayer.EntityId || _remotes.ContainsKey(notice.EntityId))
                return;

            GameObject remote = Instantiate(_remotePrefab, new Vector3(notice.X, notice.Y, 0f), Quaternion.identity, _entityRoot);
            remote.name = $"Remote_{notice.EntityId}_{notice.Name}";

            var view = remote.GetComponent<RemotePlayerView>();

            // Bảng số tra từ ClassId của NGƯỜI KIA, không phải của mình: hai lớp nhân vật có thời
            // lượng hành động khác nhau, và người xem phải co clip theo bảng của người bị xem.
            view.Init(CharacterConfigContainer.Get(notice.ClassId));
            view.PushState(new Vector2(notice.X, notice.Y), notice.FacingLeft, notice.Crouching, notice.Action);

            _remotes[notice.EntityId] = view;
        }

        private void OnEntityDespawn(EntityDespawnNotice notice)
        {
            if (!_remotes.TryGetValue(notice.EntityId, out RemotePlayerView view))
                return;

            Destroy(view.gameObject);
            _remotes.Remove(notice.EntityId);
        }

        private void OnSnapshot(WorldSnapshotNotice snapshot)
        {
            foreach (EntityState state in snapshot.States)
            {
                // Vị trí của mình đi đường MoveState — snapshot chỉ dành cho người khác.
                if (state.EntityId == _localPlayer.EntityId)
                    continue;

                // Id lạ: snapshot của tick này vượt mặt gói EntitySpawn (hai luồng server cùng
                // enqueue, thứ tự không bảo đảm). Bỏ qua — EntitySpawn sẽ đến trong vài chục ms.
                if (!_remotes.TryGetValue(state.EntityId, out RemotePlayerView view))
                    continue;

                view.PushState(new Vector2(state.X, state.Y), state.FacingLeft, state.Crouching, state.Action);
            }
        }

        private void DespawnAllRemotes()
        {
            foreach (RemotePlayerView view in _remotes.Values)
                Destroy(view.gameObject);

            _remotes.Clear();
        }

        private void OnMapChanged(MapChangedNotice notice)
        {
            if (_localPlayerObject == null)
                return;

            // Cùng ba dòng như lúc vào world — nạp LUẬT, dựng HÌNH, rồi mới đặt lại motor.
            MapGrid map = _mapService.Load(notice.MapId);

            _mapView.Show(map);

            _localPlayerObject.GetComponent<PlayerMotor>().SetMap(map, notice.State);

            // Cùng lý do như lúc vào world: sang map là một cú DỊCH CHUYỂN. Để camera bám mượt thì nó
            // lướt qua cả bản đồ mới trong nửa giây trước khi dừng đúng chỗ.
            _cameraFollow.SnapToTarget();

            // KHÔNG gọi DespawnAllRemotes(). Server đã gửi EntityDespawn cho từng người ở map cũ ngay
            // tick sau — dọn tay ở đây là đường thứ hai làm cùng một việc, và hai đường thì sớm muộn
            // lệch nhau. Chịu một tick ma đứng im, đổi lại một đường duy nhất cho mọi lý do biến mất.
            this.Log($"Sang map {notice.MapId} tại {notice.State.X:0.##}:{notice.State.Y:0.##}");
        }
    }
}
