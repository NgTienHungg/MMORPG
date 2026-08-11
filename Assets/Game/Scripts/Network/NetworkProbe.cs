using System;
using System.Buffers.Binary;
using System.Diagnostics;
using Cysharp.Threading.Tasks;
using HungNT;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace MMORPG.Game.Scripts.Network
{
    /// <summary>
    /// UI tạm của Phase 1: kết nối, ping, đo RTT. Sẽ bị thay ở Phase 4 bằng UI login thật.
    /// </summary>
    public class NetworkProbe : MonoBehaviour
    {
        private const int CMD_PING = 1;
        private const int CMD_PONG = 2;

        [Header("Network")]
        [SerializeField] private string _host = "127.0.0.1";

        [SerializeField] private int _port = 7778;

        [Header("UI")]
        [SerializeField] private Button _connectButton;

        [SerializeField] private Button _pingButton;
        [SerializeField] private TextMeshProUGUI _statusText;

        [Inject] private NetService _netService;


        private readonly Stopwatch _clock = Stopwatch.StartNew();

        private void Awake()
        {
            _connectButton.onClick.AddListener(() => ConnectAsync().Forget());
            _pingButton.onClick.AddListener(SendPing);

            _netService.OnPacket += OnPacket;
            _netService.OnStateChanged += OnStateChanged;

            SetStatus("Chưa kết nối");
        }

        private void OnDestroy()
        {
            if (_netService == null)
                return;

            _netService.OnPacket -= OnPacket;
            _netService.OnStateChanged -= OnStateChanged;
        }


        private async UniTaskVoid ConnectAsync()
        {
            SetStatus($"Đang kết nối {_host}:{_port}...");
            bool ok = await _netService.ConnectAsync(_host, _port);
            if (!ok) SetStatus("Kết nối thất bại!");
        }

        private void SendPing()
        {
            Span<byte> payload = stackalloc byte[8];
            BinaryPrimitives.WriteInt64LittleEndian(payload, _clock.ElapsedMilliseconds);

            _netService.Send(CMD_PING, payload);
            this.Log("Gửi Ping");
        }

        private void OnPacket(int cmd, byte[] payload)
        {
            if (cmd != CMD_PONG || payload.Length < 8)
                return;

            long sentAt = BinaryPrimitives.ReadInt64LittleEndian(payload);
            long rtt = _clock.ElapsedMilliseconds - sentAt;

            SetStatus($"Đã kết nối - RTT: {rtt}ms");
        }

        private void OnStateChanged(TransportState state) => SetStatus(state.ToString());

        private void SetStatus(string text)
        {
            _statusText.text = text;
            this.Log($"{text}");
        }
    }
}