using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using HungNT;
using MMORPG.Client.Network;
using MMORPG.Client.Network.Handlers;
using MMORPG.Shared.Dto;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace MMORPG.Client.Auth
{
    /// <summary>
    /// Nối UI với mạng. Đây là chỗ duy nhất biết cả hai bên.
    /// </summary>
    public sealed class LoginPresenter : MonoBehaviour
    {
        [SerializeField] private LoginUi _loginUi;

        private const float RESPONSE_TIMEOUT_SECONDS = 8f;

        private NetService _netService;
        private AuthApi _authApi;
        private AuthNetHandler _authNetHandler;
        private NetworkSettings _networkSettings;
        private CancellationTokenSource _responseTimeout;

        [Inject]
        public void Construct(NetService netService, AuthApi authApi, AuthNetHandler authNetHandler, NetworkSettings networkSettings)
        {
            _netService = netService;
            _authApi = authApi;
            _authNetHandler = authNetHandler;
            _networkSettings = networkSettings;
        }

        private void Awake()
        {
            _loginUi.LoginButton.onClick.AddListener(OnClickLogin);
            _loginUi.RegisterButton.onClick.AddListener(OnClickRegister);

            _authNetHandler.OnLoginResult += OnAuthResult;
            _authNetHandler.OnRegisterResult += OnAuthResult;
            _authNetHandler.OnKicked += OnKicked;
        }

        private void OnDestroy()
        {
            CancelResponseTimeout();

            _loginUi.LoginButton.onClick.RemoveListener(OnClickLogin);
            _loginUi.RegisterButton.onClick.RemoveListener(OnClickRegister);

            if (_authNetHandler == null)
                return;

            _authNetHandler.OnLoginResult -= OnAuthResult;
            _authNetHandler.OnRegisterResult -= OnAuthResult;
            _authNetHandler.OnKicked -= OnKicked;
        }

        private void OnClickLogin()
        {
            SubmitAsync(isRegister: false).Forget();
        }

        private void OnClickRegister()
        {
            SubmitAsync(isRegister: true).Forget();
        }

        private async UniTaskVoid SubmitAsync(bool isRegister)
        {
            // Khoá nút ngay: người chơi bấm 5 lần liên tiếp thì server nhận 5 request,
            // và với rate limiter 5 lần/phút thì họ tự khoá chính mình.
            _loginUi.SetInteractable(false);
            _loginUi.ShowMessage("Đang kết nối...", isError: false);

            if (!_netService.IsConnected && !await _netService.ConnectAsync(_networkSettings.Host, _networkSettings.Port))
            {
                this.LogWarning($"Không kết nối được {$"{_networkSettings.Host}:{_networkSettings.Port}".Color("orange")}");
                _loginUi.ShowMessage("Không kết nối được máy chủ.", isError: true);
                _loginUi.SetInteractable(true);
                return;
            }

            _loginUi.ShowMessage(isRegister ? "Đang tạo tài khoản..." : "Đang đăng nhập...", isError: false);

            if (isRegister)
                _authApi.Register(_loginUi.Username, _loginUi.Password);
            else
                _authApi.Login(_loginUi.Username, _loginUi.Password);

            ArmResponseTimeout();
        }

        private void OnAuthResult(AuthResponse response)
        {
            CancelResponseTimeout();
            _loginUi.SetInteractable(true);

            if (!response.Success)
            {
                this.LogWarning($"Server từ chối: {response.Error.ToString().Color("red")}");
                _loginUi.ShowMessage(AuthErrorText.Of(response.Error), isError: true);
                return;
            }

            this.Log($"Đăng nhập thành công: {response.Username.Color("cyan")} — token {response.SessionToken.Length.ToString().Bold()} ký tự");
            _loginUi.ShowMessage($"Xin chào, {response.Username}!", isError: false);

            // Ẩn UI là hết việc của panel này — bước vào world do phần world đảm nhiệm
            // khi nghe cùng sự kiện đăng nhập thành công.
            _loginUi.SetVisible(false);
        }

        private void OnKicked(KickedNotice notice)
        {
            CancelResponseTimeout();
            this.LogWarning($"Bị đá khỏi server: {notice.Reason.Color("orange")}");
            _loginUi.SetVisible(true);
            _loginUi.SetInteractable(true);
            _loginUi.ShowMessage(notice.Reason, isError: true);
        }

        /// <summary>
        /// Server không trả lời thì trả quyền tương tác lại cho người chơi thay vì khoá UI vĩnh viễn.
        /// Response về (OnAuthResult / OnKicked) sẽ huỷ đồng hồ này.
        /// </summary>
        private void ArmResponseTimeout()
        {
            CancelResponseTimeout();
            _responseTimeout = new CancellationTokenSource();
            WatchResponseTimeoutAsync(_responseTimeout.Token).Forget();
        }

        private void CancelResponseTimeout()
        {
            if (_responseTimeout == null)
                return;

            _responseTimeout.Cancel();
            _responseTimeout.Dispose();
            _responseTimeout = null;
        }

        private async UniTaskVoid WatchResponseTimeoutAsync(CancellationToken cancellationToken)
        {
            bool canceled = await UniTask
                .Delay(TimeSpan.FromSeconds(RESPONSE_TIMEOUT_SECONDS), cancellationToken: cancellationToken)
                .SuppressCancellationThrow();

            if (canceled)
                return;

            this.LogWarning($"Không nhận được phản hồi sau {RESPONSE_TIMEOUT_SECONDS} giây");
            _loginUi.ShowMessage("Máy chủ không phản hồi. Thử lại sau giây lát.", isError: true);
            _loginUi.SetInteractable(true);
        }

        [Button]
        public void TestConnect()
        {
            _netService.ConnectAsync(_networkSettings.Host, _networkSettings.Port).Forget();
        }

        [Button]
        public void TestLogout()
        {
            _authApi.Logout();
        }
    }
}