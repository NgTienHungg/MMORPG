using HungNT;
using MMORPG.Client.Network.Handlers;
using MMORPG.Shared.Dto.Character;
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
        [SerializeField] private Transform _entityRoot;
        [SerializeField] private CameraFollow _cameraFollow;

        private WorldApi _worldApi;
        private WorldNetHandler _worldNetHandler;

        private GameObject _localPlayerObject;
        
        [Inject]
        public void Construct(WorldApi worldApi, WorldNetHandler worldNetHandler)
        {
            _worldApi = worldApi;
            _worldNetHandler = worldNetHandler;
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
        }
    }
}
