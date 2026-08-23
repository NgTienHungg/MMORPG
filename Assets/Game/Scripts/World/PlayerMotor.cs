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
        
        [SerializeField] private CharacterAnimator _characterAnimator;

        private InputSystem_Actions _inputActions;

        private WorldApi _worldApi;
        private WorldNetHandler _worldNetHandler;
        
        /// <summary>
        /// Bộ số của lớp nhân vật mình đang chơi. Client PHẢI dự đoán bằng đúng bảng server dùng —
        /// lệch một con số là lệch quỹ đạo, và reconciliation sẽ kéo giật liên tục mà không rõ vì sao.
        /// </summary>
        private CharacterProfile _profile;

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
        
        /// <summary>
        /// Cú bấm đánh đang chờ tick tới tiêu thụ. Cùng lý do như _jumpLatched: Update chạy 60–300Hz
        /// còn Step chỉ 20Hz, đọc WasPressedThisFrame bên trong vòng tick là bỏ lỡ phần lớn cú bấm.
        /// </summary>
        private bool _attackLatched;

        private void Awake()
        {
            _inputActions = new InputSystem_Actions();
            _inputActions.Player.Enable();
        }

        public void Init(WorldApi worldApi, WorldNetHandler worldNetHandler, Vector2 spawnPos, int classId)
        {
            _worldApi = worldApi;
            _worldNetHandler = worldNetHandler;
            _profile = CharacterProfiles.Get(classId);

            _simState = MoveState.AtRest(spawnPos.x, spawnPos.y);
            _prevSimState = _simState;

            // Animator cần cùng bảng đó, nhưng chỉ để co clip cho vừa thời lượng.
            _characterAnimator.Init(_profile);

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

            // Hai nút dạng CẠNH: chốt ngay tại frame chúng xảy ra.
            if (_inputActions.Player.Jump.WasPressedThisFrame())
                _jumpLatched = true;

            if (_inputActions.Player.Attack.WasPressedThisFrame())
                _attackLatched = true;

            float dirX = Mathf.Clamp(_inputActions.Player.Move.ReadValue<Vector2>().x, -1f, 1f);

            // Trục GIỮ: lấy MỨC tại lúc dựng tick, không chốt và không gộp. Ngồi là một tư thế kéo
            // dài — thả phím ra là phải đứng dậy ngay tick sau.
            bool crouch = _inputActions.Player.Crouch.IsPressed();

            _accumulator += Time.deltaTime;
            while (_accumulator >= MovementRules.TICK_DT)
            {
                _accumulator -= MovementRules.TICK_DT;

                _prevSimState = _simState;
                Step(dirX, crouch);
            }

            _renderOffset *= Mathf.Exp(-CORRECTION_DECAY * Time.deltaTime);

            transform.position = InterpolatedPosition() + _renderOffset;

            // Hình chạy theo FRAME, không theo tick — gọi ở đây chứ không trong Step.
            // Đọc thẳng _simState (tick mới nhất) chứ không nội suy: VỊ TRÍ thì nội suy cho mượt,
            // còn TRẠNG THÁI thì không có "một nửa giữa idle và walk" để nội suy.
            _characterAnimator.Apply(
                CharacterStates.Derive(_simState), _simState.Action, _simState.FacingLeft);
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
        private void Step(float dirX, bool crouch)
        {
            int seq = ++_nextSeq;

            var intent = new MoveIntent
            {
                DirX = dirX,
                Jump = _jumpLatched,
                Crouch = crouch,
                Action = _attackLatched ? ActionRequest.Attack : ActionRequest.None,
            };

            // Tiêu thụ ngay: một lần bấm sinh đúng một MoveIntent mang nó.
            _jumpLatched = false;
            _attackLatched = false;

            _simState = MovementRules.Step(_simState, intent, MovementRules.TICK_DT, _profile);

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
                state = MovementRules.Step(state, pending.Intent, MovementRules.TICK_DT, _profile);
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