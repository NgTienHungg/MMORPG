using HungNT;
using MMORPG.Client.Network.Handlers;
using MMORPG.Shared.Dto;
using MMORPG.Shared.Dto.Character;
using UnityEngine;
using VContainer;

namespace MMORPG.Client.World
{
    /// <summary>
    /// Nối auth với world: đăng nhập xong tự gửi EnterWorld, nhận response thì spawn.
    /// Không có UI riêng — phase này client vào thẳng game.
    /// </summary>
    public sealed class WorldPresenter : MonoBehaviour
    {
        [SerializeField] private WorldSpawner _worldSpawner;

        private WorldApi _worldApi;
        private WorldNetHandler _worldNetHandler;
        private AuthNetHandler _authNetHandler;
        private LocalPlayer _localPlayer;

        [Inject]
        public void Construct(WorldApi worldApi, WorldNetHandler worldNetHandler,
            AuthNetHandler authNetHandler, LocalPlayer localPlayer)
        {
            _worldApi = worldApi;
            _worldNetHandler = worldNetHandler;
            _authNetHandler = authNetHandler;
            _localPlayer = localPlayer;
        }

        private void Start()
        {
            _authNetHandler.OnLoginResult += OnLoggedIn;
            _authNetHandler.OnLogoutResult += OnLoggedOut;
            _authNetHandler.OnKicked += OnKicked;
            _worldNetHandler.OnEnteredWorld += OnEnteredWorld;
        }

        private void OnDestroy()
        {
            if (_authNetHandler == null)
                return;

            _authNetHandler.OnLoginResult -= OnLoggedIn;
            _authNetHandler.OnLogoutResult -= OnLoggedOut;
            _authNetHandler.OnKicked -= OnKicked;
            _worldNetHandler.OnEnteredWorld -= OnEnteredWorld;
        }

        private void OnLoggedIn(AuthResponse response)
        {
            if (!response.Success)
                return;

            _worldApi.EnterWorld();
        }

        private void OnEnteredWorld(EnterWorldResponse response)
        {
            if (!response.Success)
            {
                this.LogWarning($"EnterWorld thất bại: {response.Error}");
                return;
            }

            _localPlayer.Apply(response);
            _worldSpawner.SpawnLocalPlayer(response);
        }

        private void OnLoggedOut(AuthResponse response)
        {
            _worldSpawner.DespawnLocalPlayer();
            _localPlayer.Clear();
        }

        private void OnKicked(KickedNotice notice)
        {
            _worldSpawner.DespawnLocalPlayer();
            _localPlayer.Clear();
        }
    }
}
