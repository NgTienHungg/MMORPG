using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using HungNT;
using MMORPG.Client.Network;
using MMORPG.Client.Network.Handlers;
using MMORPG.Shared.Dto;
using MMORPG.Shared.Dto.Auth;
using MMORPG.Shared.Net;
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
        private SystemNetHandler _systemNetHandler;
        private NetworkSettings _networkSettings;
        private SavedLoginStore _savedLoginStore;
        private CancellationTokenSource _responseTimeout;

        /// <summary>
        /// Kết quả kiểm contract của lần nối hiện tại. null = chưa hỏi hoặc chưa về.
        ///
        /// Đặt ở đây chứ không ở NetService: nó là trạng thái của một LẦN NỐI, và màn hình login là
        /// thứ duy nhất phải phản ứng với nó. Ngày có màn hình khác cần biết thì đẩy xuống NetService.
        /// </summary>
        private bool? _contractOk;

        [Inject]
        public void Construct(NetService netService, AuthApi authApi, AuthNetHandler authNetHandler,
            SystemNetHandler systemNetHandler, NetworkSettings networkSettings, SavedLoginStore savedLoginStore)
        {
            _netService = netService;
            _authApi = authApi;
            _authNetHandler = authNetHandler;
            _systemNetHandler = systemNetHandler;
            _networkSettings = networkSettings;
            _savedLoginStore = savedLoginStore;
        }

        private void Awake()
        {
            _loginUi.LoginButton.onClick.AddListener(OnClickLogin);
            _loginUi.RegisterButton.onClick.AddListener(OnClickRegister);

            _authNetHandler.OnLoginResult += OnAuthResult;
            _authNetHandler.OnRegisterResult += OnAuthResult;
            _authNetHandler.OnKicked += OnKicked;
            _systemNetHandler.OnVersionCheck += OnVersionCheck;
        }

        /// <summary>
        /// Điền lại tài khoản đã lưu. Để ở Start chứ không ở Awake vì tới đây thì Awake của
        /// <see cref="LoginUi"/> chắc chắn đã chạy xong — ô mật khẩu đã ở chế độ che ký tự.
        /// </summary>
        private void Start()
        {
            if (!_savedLoginStore.HasSavedLogin)
                return;

            this.Log($"Điền sẵn tài khoản đã ghi nhớ: {_savedLoginStore.Username.Color("cyan")}");

            // Chỉ điền hộ, không tự bấm đăng nhập: người chơi vẫn phải chủ động bấm để còn kịp
            // đổi sang tài khoản khác.
            _loginUi.Prefill(_savedLoginStore.Username, _savedLoginStore.Password, remember: true);
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
            _systemNetHandler.OnVersionCheck -= OnVersionCheck;
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

            // Kiểm phiên bản TRƯỚC khi gửi bất cứ lệnh nào khác. Server đặt Register/Login ở
            // MinState = Verified, nên bỏ qua bước này là mọi lệnh bị từ chối bằng NotAuthenticated —
            // một thông điệp chỉ sai hướng hoàn toàn.
            if (!await EnsureContractAsync())
            {
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

            // Chỉ ghi nhớ sau khi server xác nhận, và ghi tên server trả về (đã chuẩn hoá chữ thường)
            // chứ không phải chuỗi người chơi gõ — lần sau gửi đi đúng thứ server đang lưu.
            if (_loginUi.RememberMe)
                _savedLoginStore.Save(response.Username, _loginUi.Password);
            else
                _savedLoginStore.Clear();

            this.Log($"Đăng nhập thành công: {response.Username.Color("cyan")} — token {response.SessionToken.Length.ToString().Bold()} ký tự");
            _loginUi.ShowMessage($"Xin chào, {response.Username}!", isError: false);

            // Ẩn UI là hết việc của panel này — bước vào world do phần world đảm nhiệm
            // khi nghe cùng sự kiện đăng nhập thành công.
            _loginUi.SetVisible(false);
        }

        /// <summary>
        /// Gửi <c>VersionCheck</c> nếu chưa hỏi lần nào cho kết nối này, rồi chờ kết quả.
        ///
        /// Chờ bằng cách đợi <see cref="_contractOk"/> đổi khỏi null thay vì bằng một TaskCompletionSource:
        /// handler chạy ở main thread (NetDispatcher bảo đảm), nên vòng UniTask.WaitUntil ở main thread
        /// nhìn thấy nó ngay khi gói về, và không có chuyện hai luồng cùng đụng một biến.
        /// </summary>
        private async UniTask<bool> EnsureContractAsync()
        {
            if (_contractOk == true)
                return true;

            if (_contractOk == null)
            {
                _loginUi.ShowMessage("Đang kiểm phiên bản...", isError: false);
                _netService.Send(NetCmd.VersionCheck, new VersionCheckRequest { ContractHash = Contract.Hash });

                bool timedOut = await UniTask
                    .WaitUntil(() => _contractOk != null)
                    .Timeout(TimeSpan.FromSeconds(RESPONSE_TIMEOUT_SECONDS))
                    .SuppressCancellationThrow();

                if (timedOut)
                {
                    _loginUi.ShowMessage("Máy chủ không phản hồi. Thử lại sau giây lát.", isError: true);
                    return false;
                }
            }

            if (_contractOk == false)
            {
                // Không cho gõ tiếp: mọi lệnh sau đây đều sẽ bị server từ chối, và để người chơi thử
                // đi thử lại một việc không bao giờ thành công là tệ hơn một thông báo dứt khoát.
                _loginUi.ShowMessage("Phiên bản game không khớp máy chủ. Cập nhật lại bản mới.", isError: true);
                return false;
            }

            return true;
        }

        private void OnVersionCheck(VersionCheckResponse response)
        {
            _contractOk = response.Ok;
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
