using System.Collections.Generic;
using MMORPG.Client.Network.Handlers;
using MMORPG.Shared.Dto.World;
using MMORPG.Shared.World;
using UnityEngine;

namespace MMORPG.Client.World
{
    /// <summary>
    /// Di chuyển nhân vật của chính mình: đọc phím, dự đoán tại chỗ bằng luật chung,
    /// gửi ý định lên server, và đối chiếu lại khi server trả trạng thái authoritative.
    /// </summary>
    public sealed class PlayerMotor : MonoBehaviour
    {
        private InputSystem_Actions _inputActions;

        /// <summary>Một bước dự đoán chưa được server xác nhận — nguyên liệu để replay.</summary>
        private readonly struct PendingInput
        {
            public readonly int Seq;

            /// <summary>
            /// Giữ nguyên cả MoveIntent chứ không chỉ hướng chạy. Thiếu cờ Jump ở đây thì mỗi lần
            /// reconciliation replay lại, quỹ đạo tính ra là quỹ đạo KHÔNG có cú nhảy — nhân vật
            /// bị kéo tụt về mặt đất đúng lúc đang bay lên.
            /// </summary>
            public readonly MoveIntent Intent;

            public PendingInput(int seq, MoveIntent intent)
            {
                Seq = seq;
                Intent = intent;
            }
        }

        private WorldApi _worldApi;
        private WorldNetHandler _worldNetHandler;

        private readonly List<PendingInput> _pending = new();
        private int _nextSeq;
        private float _accumulator;

        /// <summary>
        /// Cú bấm nhảy đang chờ tick tới tiêu thụ. Cần vì Update chạy 60–300Hz còn Step chỉ chạy
        /// 20Hz: đọc WasPressedThisFrame bên trong vòng tick là bỏ lỡ phần lớn các cú bấm.
        /// </summary>
        private bool _jumpLatched;

        // Trạng thái MÔ PHỎNG (nhảy bậc 20Hz) tách khỏi vị trí HIỂN THỊ (transform, mượt theo frame).
        private MoveState _simState;

        private void Awake()
        {
            _inputActions = new InputSystem_Actions();
            _inputActions.Player.Enable();
        }

        public void Init(WorldApi worldApi, WorldNetHandler worldNetHandler, Vector2 spawnPos)
        {
            _worldApi = worldApi;
            _worldNetHandler = worldNetHandler;
            _simState = MoveState.AtRest(spawnPos.x, spawnPos.y);

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

            // Chốt cú bấm ngay tại frame nó xảy ra. Xem comment ở khai báo _jumpLatched.
            if (_inputActions.Player.Jump.WasPressedThisFrame())
                _jumpLatched = true;

            // Chỉ còn trục ngang. Kẹp [-1,1] giống hệt server: analog stick cho giá trị lẻ,
            // và bên nào kẹp khác bên kia là bên đó dự đoán lệch.
            float dirX = Mathf.Clamp(_inputActions.Player.Move.ReadValue<Vector2>().x, -1f, 1f);

            // Vòng accumulator y hệt game loop server: dự đoán theo bậc TICK_DT cố định,
            // không theo frame — frame rate không được ảnh hưởng tốc độ chạy hay độ cao nhảy.
            _accumulator += Time.deltaTime;
            while (_accumulator >= MovementRules.TICK_DT)
            {
                _accumulator -= MovementRules.TICK_DT;
                Step(dirX);
            }

            // Hiển thị đuổi theo mô phỏng. Mốc là MAX_FALL_SPEED chứ không phải MOVE_SPEED:
            // lúc rơi, mô phỏng đi nhanh gấp 4 lần lúc chạy, đuổi bằng tốc độ chạy là tụt lại thấy rõ.
            transform.position = Vector3.MoveTowards(
                transform.position, new Vector3(_simState.X, _simState.Y, 0f),
                MovementRules.MAX_FALL_SPEED * 1.5f * Time.deltaTime
            );
        }

        /// <summary>Một bước dự đoán: mô phỏng trước, ghi nợ, gửi lên server. Gửi CẢ khi đứng yên — thả phím cũng là input.</summary>
        private void Step(float dirX)
        {
            int seq = ++_nextSeq;

            var intent = new MoveIntent { DirX = dirX, Jump = _jumpLatched };

            // Tiêu thụ ngay: một lần bấm sinh đúng một MoveIntent có Jump = true.
            _jumpLatched = false;

            _simState = MovementRules.Step(_simState, intent, MovementRules.TICK_DT);

            _pending.Add(new PendingInput(seq, intent));
            _worldApi.Move(seq, intent);
        }

        /// <summary>
        /// Đối chiếu với server: trạng thái server + replay các input server chưa xử = trạng thái "đáng lẽ".
        /// Dự đoán đúng thì kết quả trùng cái đang có; sai thì bị kéo về — đó là cú giật rubber-band.
        /// </summary>
        private void OnMoveStateResult(MoveStateResponse response)
        {
            _pending.RemoveAll(p => p.Seq <= response.LastInputSeq);

            // Lấy TRỌN trạng thái server, không chỉ vị trí. VelY quyết định 15 tick tiếp theo của cú
            // nhảy, hai bộ đếm coyote/buffer quyết định có được nhảy tiếp không — giữ lại bản của
            // mình mà chỉ nhận X/Y của server là trộn hai sự thật. Gán nguyên struct: không có
            // danh sách trường nào để quên chép.
            MoveState state = response.State;

            foreach (PendingInput pending in _pending)
            {
                state = MovementRules.Step(state, pending.Intent, MovementRules.TICK_DT);
            }

            _simState = state;
        }
    }
}
