using System.Collections.Generic;
using HungNT;
using MMORPG.Shared.World;
using UnityEngine;

namespace MMORPG.Client.World
{
    /// <summary>
    /// Hiển thị nhân vật của NGƯỜI KHÁC: nhận vị trí rời rạc từ snapshot, vẽ mượt bằng nội suy,
    /// và suy tư thế từ chính chuỗi vị trí đó.
    /// Luôn vẽ trễ INTERP_DELAY so với gói mới nhất — đổi độ trễ lấy sự chắc chắn không phải đoán.
    /// </summary>
    public class RemotePlayerView : MonoBehaviour
    {
        /// <summary>
        /// Độ trễ nội suy: phải ≥ 2–3 khoảng tick (50ms) + jitter thì mẫu "bên phải" mới kịp đến
        /// trước lúc cần vẽ. Nhỏ quá → hết mẫu, nhân vật giật; to quá → người khác càng "cũ".
        /// </summary>
        private const float INTERP_DELAY = 0.15f;

        /// <summary>Giữ mẫu trong chừng này rồi cắt — quá khứ xa hơn không bao giờ cần vẽ lại.</summary>
        private const float BUFFER_KEEP = 1f;

        /// <summary>
        /// Ngưỡng coi hai mẫu là "không dịch chuyển". Không so == 0 vì hai mẫu có thể cách nhau
        /// hơn một tick khi mạng dồn gói; nhưng để rất nhỏ, vì trên sàn phẳng Y được gán thẳng
        /// bằng GROUND_Y nên hai tick đứng yên cho đúng cùng một số float.
        /// </summary>
        private const float EPS = 0.0001f;

        [SerializeField] private CharacterAnimator _characterAnimator;

        private readonly struct Sample
        {
            public readonly float Time;
            public readonly Vector2 Pos;
            public readonly bool FacingLeft;
            public readonly bool Crouching;
            public readonly ActionState Action;

            public Sample(float time, Vector2 pos, bool facingLeft, bool crouching, ActionState action)
            {
                Time = time;
                Pos = pos;
                FacingLeft = facingLeft;
                Crouching = crouching;
                Action = action;
            }
        }

        private readonly List<Sample> _buffer = new();

        /// <summary>
        /// Bảng số của LỚP NHÂN VẬT KIA — cần để co clip cho vừa thời lượng hành động của họ.
        /// Gọi ngay sau Instantiate, trước mẫu đầu tiên.
        /// </summary>
        public void Init(CharacterProfile profile)
        {
            _characterAnimator.Init(profile);
        }

        /// <summary>Gọi mỗi lần snapshot đến. Mốc thời gian là đồng hồ MÁY MÌNH lúc nhận.</summary>
        public void PushState(Vector2 pos, bool facingLeft, bool crouching, ActionState action)
        {
            _buffer.Add(new Sample(Time.time, pos, facingLeft, crouching, action));

            // Cắt đầu buffer: chỉ cần giữ đủ để nội suy, không phải lịch sử cả trận.
            while (_buffer.Count > 2 && _buffer[0].Time < Time.time - BUFFER_KEEP)
                _buffer.RemoveAt(0);
        }

        private void Update()
        {
            if (_buffer.Count == 0)
                return;

            // Vẽ tại thời điểm quá khứ: mọi thứ cần biết về khoảnh khắc này đã nằm sẵn trong buffer.
            float renderTime = Time.time - INTERP_DELAY;

            // Trước mẫu đầu (mới xuất hiện) → đứng ở mẫu đầu.
            if (renderTime <= _buffer[0].Time)
            {
                transform.position = _buffer[0].Pos;
                Draw(_buffer[0], _buffer[0], 1f);
                return;
            }

            // Tìm hai mẫu kẹp renderTime rồi nội suy theo tỉ lệ thời gian giữa chúng.
            for (int i = 0; i < _buffer.Count - 1; i++)
            {
                Sample a = _buffer[i];
                Sample b = _buffer[i + 1];

                if (renderTime > b.Time)
                    continue;

                float t = (renderTime - a.Time) / (b.Time - a.Time);
                transform.position = Vector2.Lerp(a.Pos, b.Pos, t);
                Draw(a, b, b.Time - a.Time);
                return;
            }

            // Qua cả mẫu cuối: mạng đang nghẽn, KHÔNG đoán tiếp — đứng ở vị trí chắc chắn cuối cùng.
            // Ngoại suy ở đây là đổi "khựng nhẹ" lấy "lao qua tường rồi bị giật ngược", lỗ vốn.
            Sample last = _buffer[^1];
            transform.position = last.Pos;
            Draw(last, last, 1f);
        }

        /// <summary>
        /// Suy tư thế từ hai mẫu ĐANG được nội suy rồi giao cho animator.
        ///
        /// Dựng một MoveState chỉ điền 4 field trông xấu, nhưng đó là cái giá để định nghĩa
        /// "tư thế là gì" vẫn chỉ có MỘT bản, ở Shared: thêm một LocomotionState mới thì người
        /// khác tự đúng theo, không ai phải nhớ đi sửa chỗ thứ hai.
        ///
        /// Tư thế và hành động lấy từ mẫu a — mẫu đang được vẽ. Lấy mẫu mới nhất là để hoạt ảnh
        /// chạy trước vị trí 0.15 giây: nhân vật vung tay ở chỗ nó chưa đứng tới.
        /// </summary>
        private void Draw(in Sample a, in Sample b, float dt)
        {
            Vector2 delta = b.Pos - a.Pos;

            var sampled = new MoveState
            {
                VelX = delta.x / dt,
                VelY = delta.y / dt,

                // Trên sàn phẳng, hai tick liên tiếp cho đúng cùng một Y. Trên không thì trọng lực
                // bảo đảm Y đổi mỗi tick. Suy sai ở đây chỉ hỏng MỘT frame hoạt ảnh của người khác —
                // biểu diễn được phép đoán, mô phỏng thì không.
                Grounded = Mathf.Abs(delta.y) < EPS,

                Crouching = a.Crouching,
            };

            _characterAnimator.Apply(CharacterStates.Derive(sampled), a.Action, a.FacingLeft);
        }
    }
}
