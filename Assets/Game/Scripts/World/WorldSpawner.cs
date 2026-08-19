using System.Collections.Generic;
using HungNT;
using MMORPG.Client.Network.Handlers;
using MMORPG.Shared.Dto.Character;
using MMORPG.Shared.Dto.World;
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

        private WorldApi _worldApi;
        private WorldNetHandler _worldNetHandler;
        private LocalPlayer _localPlayer;

        private GameObject _localPlayerObject;
        private readonly Dictionary<int, RemotePlayerView> _remotes = new();

        [Inject]
        public void Construct(WorldApi worldApi, WorldNetHandler worldNetHandler, LocalPlayer localPlayer)
        {
            _worldApi = worldApi;
            _worldNetHandler = worldNetHandler;
            _localPlayer = localPlayer;
        }

        private void Start()
        {
            _worldNetHandler.OnEntitySpawn += OnEntitySpawn;
            _worldNetHandler.OnEntityDespawn += OnEntityDespawn;
            _worldNetHandler.OnSnapshot += OnSnapshot;
        }

        private void OnDestroy()
        {
            if (_worldNetHandler == null)
                return;

            _worldNetHandler.OnEntitySpawn -= OnEntitySpawn;
            _worldNetHandler.OnEntityDespawn -= OnEntityDespawn;
            _worldNetHandler.OnSnapshot -= OnSnapshot;
        }

        public void SpawnLocalPlayer(EnterWorldResponse response)
        {
            if (_localPlayerObject != null)
                DespawnLocalPlayer();

            _localPlayerObject = Instantiate(_playerPrefab, new Vector3(response.X, response.Y), Quaternion.identity, _entityRoot);
            _localPlayerObject.name = $"Player_{response.EntityId}_{response.Name}";

            // Prefab sinh lúc runtime — VContainer không tự inject. Đưa phụ thuộc vào tay.
            var motor = _localPlayerObject.GetComponent<PlayerMotor>();
            motor.Init(_worldApi, _worldNetHandler, new Vector2(response.X, response.Y));

            _cameraFollow.SetTarget(_localPlayerObject.transform);

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
        }

        private void OnEntitySpawn(EntitySpawnNotice notice)
        {
            // Gói về chính mình (nếu có) hoặc gói lặp — bỏ qua, không nhân bản.
            if (notice.EntityId == _localPlayer.EntityId || _remotes.ContainsKey(notice.EntityId))
                return;

            GameObject remote = Instantiate(_remotePrefab, new Vector3(notice.X, notice.Y, 0f), Quaternion.identity, _entityRoot);
            remote.name = $"Remote_{notice.EntityId}_{notice.Name}";

            var view = remote.GetComponent<RemotePlayerView>();
            view.PushState(new Vector2(notice.X, notice.Y));

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

                view.PushState(new Vector2(state.X, state.Y));
            }
        }

        private void DespawnAllRemotes()
        {
            foreach (RemotePlayerView view in _remotes.Values)
                Destroy(view.gameObject);

            _remotes.Clear();
        }
    }
}
