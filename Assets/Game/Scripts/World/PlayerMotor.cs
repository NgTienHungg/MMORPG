using System.Collections.Generic;
using HungNT;
using MMORPG.Client.Network.Handlers;
using MMORPG.Shared.Dto.World;
using MMORPG.Shared.World;
using UnityEngine;

namespace MMORPG.Client.World
{
    /// <summary>
    /// Di chuyển nhân vật của chính mình: đọc phím, dự đoán tại chỗ bằng công thức chung,
    /// gửi ý định lên server, và đối chiếu lại khi server trả vị trí authoritative.
    /// </summary>
    public sealed class PlayerMotor : MonoBehaviour
    {
        private InputSystem_Actions _inputActions;

        /// <summary>Một bước dự đoán chưa được server xác nhận — nguyên liệu để replay.</summary>
        private readonly struct PendingInput
        {
            public readonly int Seq;
            public readonly Vector2 Dir;

            public PendingInput(int seq, Vector2 dir)
            {
                Seq = seq;
                Dir = dir;
            }
        }

        private WorldApi _worldApi;
        private WorldNetHandler _worldNetHandler;

        private readonly List<PendingInput> _pending = new();
        private int _nextSeq;
        private float _accumulator;

        // Vị trí MÔ PHỎNG (nhảy bậc 20Hz) tách khỏi vị trí HIỂN THỊ (transform, mượt theo frame).
        private Vector2 _simPos;

        private void Awake()
        {
            _inputActions = new InputSystem_Actions();
            _inputActions.Player.Enable();
        }

        public void Init(WorldApi worldApi, WorldNetHandler worldNetHandler, Vector2 spawnPos)
        {
            _worldApi = worldApi;
            _worldNetHandler = worldNetHandler;
            _simPos = spawnPos;

            _worldNetHandler.OnMoveStateResult += OnMoveStateResult;
        }

        private void OnDestroy()
        {
            // Wrapper giữ một InputActionAsset runtime — không Dispose thì asset và các
            // callback của nó sống sót qua cả lần đổi scene.
            _inputActions?.Dispose();

            if (_worldNetHandler != null)
                _worldNetHandler.OnMoveStateResult -= OnMoveStateResult;
        }

        private void Update()
        {
            if (_worldApi == null)
                return;

            var dir = _inputActions.Player.Move.ReadValue<Vector2>();

            // Đi chéo là (1,1) — dài √2. Không chuẩn hoá thì đi chéo nhanh hơn đi thẳng,
            // và server (cũng chuẩn hoá) sẽ không đồng ý với dự đoán của ta.
            if (dir.sqrMagnitude > 1f)
                dir.Normalize();

            // Vòng accumulator y hệt game loop server: dự đoán theo bậc TICK_DT cố định,
            // không theo frame — frame rate không được ảnh hưởng tốc độ chạy.
            _accumulator += Time.deltaTime;
            while (_accumulator >= MovementRules.TICK_DT)
            {
                _accumulator -= MovementRules.TICK_DT;
                Step(dir);
            }

            // Hiển thị đuổi theo mô phỏng, hơi nhanh hơn tốc độ chạy để không bao giờ tụt lại xa.
            transform.position = Vector3.MoveTowards(
                transform.position, new Vector3(_simPos.x, _simPos.y, 0f),
                MovementRules.MOVE_SPEED * 1.5f * Time.deltaTime
            );
        }

        /// <summary>Một bước dự đoán: mô phỏng trước, ghi nợ, gửi lên server. Gửi CẢ khi dir = (0,0) — thả phím cũng là input.</summary>
        private void Step(Vector2 dir)
        {
            int seq = ++_nextSeq;

            (_simPos.x, _simPos.y) = MovementRules.Step(_simPos.x, _simPos.y, dir.x, dir.y, MovementRules.TICK_DT);

            _pending.Add(new PendingInput(seq, dir));
            _worldApi.Move(seq, dir.x, dir.y);
        }

        /// <summary>
        /// Đối chiếu với server: vị trí server + replay các input server chưa xử = vị trí "đáng lẽ".
        /// Dự đoán đúng thì kết quả trùng cái đang có; sai thì bị kéo về — đó là cú giật rubber-band.
        /// </summary>
        private void OnMoveStateResult(MoveStateResponse state)
        {
            _pending.RemoveAll(p => p.Seq <= state.LastInputSeq);

            var pos = new Vector2(state.X, state.Y);
            foreach (PendingInput pending in _pending)
            {
                (pos.x, pos.y) = MovementRules.Step(pos.x, pos.y, pending.Dir.x, pending.Dir.y, MovementRules.TICK_DT);
            }

            _simPos = pos;
        }
    }
}