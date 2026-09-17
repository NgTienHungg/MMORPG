using UnityEngine;

namespace MMORPG.Client.World
{
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] private float _smoothTime = 0.15f;
        [SerializeField] private Vector3 _offset = new(0, 0, -10f);

        private Transform _target;
        private Vector3 _velocity;

        public void SetTarget(Transform target)
        {
            _target = target;
        }

        /// <summary>
        /// Nhảy thẳng tới mục tiêu, không bám mượt. Dùng cho những cú DỊCH CHUYỂN — vào world, sang
        /// map — chứ không phải cho di chuyển thường.
        ///
        /// Không có nó thì SmoothDamp coi cú nhảy sang map như một cú chạy: camera lướt qua cả bản đồ
        /// mất nửa giây, và thứ nó lướt qua là map MỚI vừa dựng xong ở chỗ khác hẳn.
        ///
        /// Xoá _velocity là bắt buộc: nó là vận tốc tích luỹ của đoạn bám trước đó, giữ lại thì camera
        /// tới nơi rồi còn trôi thêm một đoạn theo quán tính cũ.
        /// </summary>
        public void SnapToTarget()
        {
            if (_target == null)
                return;

            transform.position = _target.position + _offset;
            _velocity = Vector3.zero;
        }

        // LateUpdate chứ không phải Update: nhân vật phải di chuyển xong rồi camera mới bám theo.
        // Làm ngược lại thì camera luôn trễ một frame và hình bị rung nhẹ.
        private void LateUpdate()
        {
            if (_target == null)
                return;

            // SmoothDamp cần một biến vận tốc do NÓ tự quản giữa các frame — truyền bằng `ref`
            // để nó đọc/ghi thẳng vào field; code của mình không bao giờ tự sửa _velocity.
            transform.position = Vector3.SmoothDamp(
                transform.position, _target.position + _offset, ref _velocity, _smoothTime);
        }
    }
}
