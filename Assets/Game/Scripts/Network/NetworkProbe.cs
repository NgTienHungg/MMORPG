using System;
using Cysharp.Threading.Tasks;
using HungNT;
using MMORPG.Client.Network.Handlers;
using MMORPG.Shared.Dto;
using MMORPG.Shared.Net;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace MMORPG.Client.Network
{
    public class NetworkProbe : MonoBehaviour
    {
        [SerializeField] private string _host = "127.0.0.1";
        [SerializeField] private int _port = 7778;

        [SerializeField] private Button _connectButton;
        [SerializeField] private Button _pingButton;
        [SerializeField] private Button _echoButton;
        [SerializeField] private TMP_InputField _echoInput;
        [SerializeField] private TextMeshProUGUI _statusText;

        private NetService _net;
        private SystemNetHandler _systemHandler;

        [Inject]
        public void Construct(NetService net, SystemNetHandler systemHandler)
        {
            _net = net;
            _systemHandler = systemHandler;
        }

        private void Awake()
        {
            _connectButton.onClick.AddListener(() => ConnectAsync().Forget());
            _pingButton.onClick.AddListener(SendPing);
            _echoButton.onClick.AddListener(SendEcho);

            _systemHandler.OnPong += OnPong;
            _systemHandler.OnEcho += OnEcho;
            _net.OnStateChanged += OnStateChanged;

            SetStatus("Chưa kết nối");
        }

        private void OnDestroy()
        {
            // Construct chưa chạy nếu container build lỗi — đừng để OnDestroy nổ chồng lên lỗi gốc.
            if (_net == null)
                return;

            _systemHandler.OnPong -= OnPong;
            _systemHandler.OnEcho -= OnEcho;

            // NetService là singleton, sống lâu hơn scene này. Không gỡ tay thì lần load scene sau
            // event vẫn gọi vào MonoBehaviour đã destroy.
            _net.OnStateChanged -= OnStateChanged;
        }

        private async UniTaskVoid ConnectAsync()
        {
            SetStatus($"Đang kết nối {_host}:{_port}...");
            if (!await _net.ConnectAsync(_host, _port))
                SetStatus("Kết nối thất bại");
        }

        private void SendPing() =>
            _net.Send(NetCmd.Ping, new PingRequest
                {
                    ClientTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                }
            );

        private void SendEcho() => _net.Send(NetCmd.Echo, new EchoRequest { Message = _echoInput.text });

        private void OnStateChanged(TransportState state) => SetStatus(state.ToString());

        private void OnPong(PingResponse res)
        {
            long rtt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - res.ClientTimeMs;
            SetStatus($"RTT: {rtt} ms · lệch giờ server: {res.ServerTimeMs - res.ClientTimeMs} ms");
        }

        private void OnEcho(EchoResponse res) => SetStatus($"Server vọng lại: \"{res.Message}\"");

        private void SetStatus(string text)
        {
            _statusText.text = text;
            this.Log(text);
        }
    }
}
