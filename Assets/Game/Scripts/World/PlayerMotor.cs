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
        /// <summary>
        /// Tốc độ tan của sai lệch hiển thị, đơn vị 1/giây. 12 ≈ tan 90% trong ~0.2s: đủ nhanh để
        /// không thấy nhân vật trôi lệch, đủ chậm để cú sửa không thành một cú giật mới.
        /// </summary>
        private const float CORRECTION_DECAY = 12f;

        /// <summary>Lệch hơn mức này thì kéo mượt là dối người chơi — cắt thẳng về vị trí đúng.</summary>
        private const float SNAP_DISTANCE = 2f;

        private InputSystem_Actions _inputActions;

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

        /// <summary>Trạng thái ở tick TRƯỚC — đầu trái của đoạn nội suy đang vẽ.</summary>
        private MoveState _prevSimState;

        /// <summary>
        /// Phần bù thị giác: chênh lệch giữa chỗ đang vẽ và chỗ mô phỏng nói, sinh ra mỗi lần
        /// reconciliation kéo trạng thái về. Cộng vào lúc vẽ rồi cho tiêu dần, nhờ đó cú sửa trải
        /// ra thành một đoạn trượt ngắn thay vì một bước nhảy cóc.
        /// </summary>
        private Vector2 _renderOffset;

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
            _prevSimState = _simState;

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

                // Chốt tick vừa rời đi TRƯỚC khi Step ghi đè _simState — nó là đầu trái của đoạn
                // nội suy mà các frame sắp tới sẽ đi dọc theo.
                _prevSimState = _simState;
                Step(dirX);
            }

            // Suy giảm theo hàm mũ để sai lệch tan sau cùng một khoảng THỜI GIAN ở mọi frame rate,
            // thay vì sau cùng một số FRAME. Nhân trực tiếp: không cần nhớ giá trị ban đầu.
            _renderOffset *= Mathf.Exp(-CORRECTION_DECAY * Time.deltaTime);

            transform.position = InterpolatedPosition() + _renderOffset;
        }

        /// <summary>
        /// Vị trí vẽ ở frame này: điểm giữa hai tick gần nhất, tỉ lệ theo phần thời gian đã trôi
        /// kể từ tick cuối. Hiển thị vì thế luôn trễ mô phỏng đúng một tick (50ms) — cái giá để có
        /// đường đi liền mạch thay vì 20 bậc thang mỗi giây.
        /// </summary>
        private Vector2 InterpolatedPosition()
        {
            float alpha = _accumulator / MovementRules.TICK_DT;

            return Vector2.Lerp(
                new Vector2(_prevSimState.X, _prevSimState.Y),
                new Vector2(_simState.X, _simState.Y),
                alpha);
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
            // Chỗ ĐANG vẽ, ghi lại trước khi trạng thái bị thay: mọi phép bù bên dưới đo từ đây.
            Vector2 renderedBefore = InterpolatedPosition() + _renderOffset;

            _pending.RemoveAll(p => p.Seq <= response.LastInputSeq);

            MoveState state = response.State;

            // Đầu trái của đoạn nội suy sau khi replay = trạng thái ngay TRƯỚC bước replay cuối.
            // Khởi tạo bằng giá trị cũ để lúc không còn input nào để replay thì giữ nguyên đầu trái:
            // cho hai đầu mút trùng nhau là hiển thị đứng hình tới hết tick — chuyện xảy ra liên tục
            // khi test cùng máy, lúc server gần như ack tức thì.
            MoveState previous = _prevSimState;

            foreach (PendingInput pending in _pending)
            {
                previous = state;
                state = MovementRules.Step(state, pending.Intent, MovementRules.TICK_DT);
            }

            _prevSimState = previous;
            _simState = state;

            // Giữ nguyên chỗ đang vẽ, đẩy toàn bộ chênh lệch vào phần bù rồi cho nó tan dần.
            Vector2 offset = renderedBefore - InterpolatedPosition();

            // Lệch quá xa (mất gói kéo dài, server dịch chuyển nhân vật) thì trượt mượt chỉ làm
            // nhân vật đi xuyên địa hình trên đường về — cắt thẳng, thà giật một cái còn hơn.
            if (offset.sqrMagnitude > SNAP_DISTANCE * SNAP_DISTANCE)
                offset = Vector2.zero;

            _renderOffset = offset;
        }

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
    }
}