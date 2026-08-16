using System;
using Cysharp.Threading.Tasks;
using HungNT;
using MMORPG.Client.Network.Handlers;
using MMORPG.Shared.Dto;
using MMORPG.Shared.Dto.World;
using MMORPG.Shared.Net;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace MMORPG.Client.Network
{
    public class NetworkProbe : MonoBehaviour
    {
        [SerializeField] private Button _connectButton;
        [SerializeField] private Button _pingButton;
        [SerializeField] private Button _echoButton;
        [SerializeField] private Button _serverInfoButton;
        [SerializeField] private TMP_InputField _echoInput;
        [SerializeField] private TextMeshProUGUI _statusText;

        private NetService _netService;
        private SystemNetHandler _systemNetHandler;
        private NetworkSettings _networkSettings;

        [Inject]
        public void Construct(NetService netService, SystemNetHandler systemNetHandler, NetworkSettings networkSettings)
        {
            _netService = netService;
            _systemNetHandler = systemNetHandler;
            _networkSettings = networkSettings;
        }

        private void Awake()
        {
            _connectButton.onClick.AddListener(OnClickConnect);
            _pingButton.onClick.AddListener(SendPing);
            _echoButton.onClick.AddListener(SendEcho);
            _serverInfoButton.onClick.AddListener(GetServerInfo);

            _systemNetHandler.OnPong += OnPong;
            _systemNetHandler.OnEcho += OnEcho;
            _systemNetHandler.OnServerInfo += OnServerInfo;
            _netService.OnStateChanged += OnStateChanged;

            SetStatus("Chưa kết nối");
        }

        private void OnDestroy()
        {
            _connectButton.onClick.RemoveListener(OnClickConnect);
            _pingButton.onClick.RemoveListener(SendPing);
            _echoButton.onClick.RemoveListener(SendEcho);
            _serverInfoButton.onClick.RemoveListener(GetServerInfo);

            // Construct chưa chạy nếu container build lỗi — đừng để OnDestroy nổ chồng lên lỗi gốc.
            if (_netService == null)
                return;

            _systemNetHandler.OnPong -= OnPong;
            _systemNetHandler.OnEcho -= OnEcho;

            // NetService là singleton, sống lâu hơn scene này. Không gỡ tay thì lần load scene sau
            // event vẫn gọi vào MonoBehaviour đã destroy.
            _netService.OnStateChanged -= OnStateChanged;
        }

        private void OnClickConnect()
        {
            ConnectAsync().Forget();
        }

        private async UniTaskVoid ConnectAsync()
        {
            SetStatus($"Đang kết nối {_networkSettings.Host}:{_networkSettings.Port}...");
            if (!await _netService.ConnectAsync(_networkSettings.Host, _networkSettings.Port))
                SetStatus("Kết nối thất bại");
        }

        private void SendPing()
        {
            _netService.Send(NetCmd.Ping, new PingRequest
                {
                    ClientTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                }
            );
        }

        private void SendEcho()
        {
            _netService.Send(NetCmd.Echo, new EchoRequest { Message = _echoInput.text });
        }

        private void GetServerInfo()
        {
            _netService.Send(NetCmd.ServerInfo, new EmptyRequest());
        }

        private void OnStateChanged(TransportState state)
        {
            SetStatus(state.ToString());
        }

        private void OnPong(PingResponse response)
        {
            long rtt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - response.ClientTimeMs;
            SetStatus($"RTT: {rtt} ms · lệch giờ server: {response.ServerTimeMs - response.ClientTimeMs} ms");
        }

        private void OnEcho(EchoResponse response)
        {
            SetStatus($"Server vọng lại: \"{response.Message}\"");
        }

        private void OnServerInfo(ServerInfoResponse response)
        {
            SetStatus($"Server name: {response.ServerName}");
        }

        private void SetStatus(string text)
        {
            _statusText.text = text;
            this.Log(text); 
        }

        [Button]
        public void SendMoveInput(int seq, float dirX, float dirY)
        {
            _netService.Send(NetCmd.MoveInput, new MoveInputRequest { Seq = seq, DirX = dirX, DirY = dirY });
        }
    }
}