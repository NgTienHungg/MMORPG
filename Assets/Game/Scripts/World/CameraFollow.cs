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
