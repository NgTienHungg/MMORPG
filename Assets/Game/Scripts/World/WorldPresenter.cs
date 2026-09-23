using HungNT;
using MMORPG.Client.Config;
using MMORPG.Client.Network.Handlers;
using MMORPG.Shared.Dto.Auth;
using MMORPG.Shared.Dto.Character;
using UnityEngine;
using VContainer;

namespace MMORPG.Client.World
{
    /// <summary>
    /// Nối auth với world: đăng nhập xong tự gửi EnterWorld, nhận response thì nạp config rồi spawn.
    /// Không có UI riêng — phase này client vào thẳng game.
    /// </summary>
    public sealed class WorldPresenter : MonoBehaviour
    {
        [SerializeField] private WorldSpawner _worldSpawner;

        private WorldApi _worldApi;
        private WorldNetHandler _worldNetHandler;
        private AuthNetHandler _authNetHandler;
        private LocalPlayer _localPlayer;
        private ConfigService _configService;

        [Inject]
        public void Construct(WorldApi worldApi, WorldNetHandler worldNetHandler,
            AuthNetHandler authNetHandler, LocalPlayer localPlayer, ConfigService configService)
        {
            _worldApi = worldApi;
            _worldNetHandler = worldNetHandler;
            _authNetHandler = authNetHandler;
            _localPlayer = localPlayer;
            _configService = configService;
        }

        private void Start()
        {
            _authNetHandler.OnLoginResult += OnLoggedIn;
            _authNetHandler.OnLogoutResult += OnLoggedOut;
            _authNetHandler.OnKicked += OnKicked;
            _worldNetHandler.OnEnterWorldResult += OnEnterWorldResult;
        }

        private void OnDestroy()
        {
            if (_authNetHandler == null)
                return;

            _authNetHandler.OnLoginResult -= OnLoggedIn;
            _authNetHandler.OnLogoutResult -= OnLoggedOut;
            _authNetHandler.OnKicked -= OnKicked;
            _worldNetHandler.OnEnterWorldResult -= OnEnterWorldResult;
        }

        private void OnLoggedIn(AuthResponse response)
        {
            if (!response.Success)
                return;

            _worldApi.EnterWorld();
        }

        private void OnEnterWorldResult(EnterWorldResponse response)
        {
            if (!response.Success)
            {
                this.LogWarning($"EnterWorld thất bại: {response.Error}");
                return;
            }

            // Hai lý do: (1) WorldSpawner gọi PlayerMotor.Init, mà Init tra CharacterConfigContainer
            // ngay dòng đầu — bảng hỏng thì container ném và triệu chứng là "vào world xong không
            // có nhân vật nào"; (2) Apply trả false khi bảng lệch server, và lúc đó KHÔNG được vào
            // world: chơi bằng bộ số khác server là rubber-band không có tên.
            if (!_configService.Apply(response))
                return;

            _localPlayer.Apply(response);
            _worldSpawner.SpawnLocalPlayer(response);
        }

        private void OnLoggedOut(AuthResponse response)
        {
            _worldSpawner.DespawnLocalPlayer();
            _localPlayer.Clear();
            _configService.Clear();
        }

        private void OnKicked(KickedNotice notice)
        {
            _worldSpawner.DespawnLocalPlayer();
            _localPlayer.Clear();
            _configService.Clear();
        }
    }
}
