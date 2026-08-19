using System.Collections.Generic;
using MMORPG.Shared.Dto.World;
using UnityEngine;

namespace MMORPG.Client.World
{
    /// <summary>
    /// Hiển thị nhân vật của NGƯỜI KHÁC: nhận vị trí rời rạc từ snapshot, vẽ mượt bằng nội suy.
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

        private readonly struct Sample
        {
            public readonly float Time;
            public readonly Vector2 Pos;

            public Sample(float time, Vector2 pos)
            {
                Time = time;
                Pos = pos;
            }
        }

        private readonly List<Sample> _buffer = new();

        /// <summary>Gọi mỗi lần snapshot đến. Mốc thời gian là đồng hồ MÁY MÌNH lúc nhận.</summary>
        public void PushState(Vector2 pos)
        {
            _buffer.Add(new Sample(Time.time, pos));

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
                return;
            }

            // Qua cả mẫu cuối: mạng đang nghẽn, KHÔNG đoán tiếp — đứng ở vị trí chắc chắn cuối cùng.
            // Ngoại suy ở đây là đổi "khựng nhẹ" lấy "lao qua tường rồi bị giật ngược", lỗ vốn.
            transform.position = _buffer[^1].Pos;
        }
    }
}
