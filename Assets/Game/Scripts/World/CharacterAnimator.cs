using System;
using System.Collections.Generic;
using MMORPG.Shared.World;
using UnityEngine;

namespace MMORPG.Client.World
{
    /// <summary>
    /// Chiếu đúng clip ứng với trạng thái nhân vật. Không giữ luật, không quyết định gì:
    /// nhận (tư thế, hành động, hướng mặt) rồi phát. Nhờ vậy dùng chung được cho nhân vật của
    /// mình lẫn nhân vật người khác — hai nguồn dữ liệu hoàn toàn khác nhau, cùng một cách vẽ.
    ///
    /// Animator Controller ở đây cố tình KHÔNG có transition nào: bảng chuyển tiếp đã nằm trong
    /// CharacterStates và chạy ở cả hai đầu dây. Dựng thêm một máy trạng thái nữa bên trong Unity
    /// là có hai nguồn sự thật cho cùng một câu hỏi, và cái thứ hai thì server không đọc được.
    /// </summary>
    public sealed class CharacterAnimator : MonoBehaviour
    {
        [Serializable]
        private struct LocomotionClip
        {
            public LocomotionState State;
            public AnimationClip Clip;
        }

        [Serializable]
        private struct ActionClip
        {
            public ActionState Action;
            public AnimationClip Clip;
        }

        [SerializeField] private Animator _animator;
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private LocomotionClip[] _locomotionClips;
        [SerializeField] private ActionClip[] _actionClips;

        // Tên state trong Animator Controller trùng tên clip (mặc định khi kéo clip vào controller).
        // Băm sẵn ra hash một lần: Animator.Play nhận hash, còn StringToHash mỗi frame là phí.
        private readonly Dictionary<LocomotionState, int> _locomotionHashes = new();
        private readonly Dictionary<ActionState, int> _actionHashes = new();
        private readonly Dictionary<ActionState, float> _actionLengths = new();

        /// <summary>Clip đang phát. Chỉ gọi Animator.Play khi giá trị này đổi — gọi mỗi frame thì
        /// hoạt ảnh bị đặt lại về frame 0 liên tục và đứng hình.</summary>
        private int _currentHash;

        /// <summary>
        /// Bộ số của nhân vật đang được vẽ — cần đúng một thứ trong đó: mỗi hành động dài bao nhiêu
        /// tick, để co clip cho vừa. Nhân vật của mình lấy từ ClassId trong EnterWorldResponse,
        /// nhân vật người khác lấy từ ClassId trong EntitySpawnNotice.
        /// </summary>
        private CharacterProfile _profile;

        /// <summary>Gọi ngay sau khi Instantiate, trước lần Apply đầu tiên.</summary>
        public void Init(CharacterProfile profile)
        {
            _profile = profile;
        }

        private void Awake()
        {
            foreach (LocomotionClip entry in _locomotionClips)
                _locomotionHashes[entry.State] = Animator.StringToHash(entry.Clip.name);

            foreach (ActionClip entry in _actionClips)
            {
                _actionHashes[entry.Action] = Animator.StringToHash(entry.Clip.name);
                _actionLengths[entry.Action] = entry.Clip.length;
            }
        }

        /// <summary>
        /// Cập nhật hình theo trạng thái. Gọi mỗi frame; nó tự lọc những lần không có gì đổi.
        /// </summary>
        public void Apply(LocomotionState locomotion, ActionState action, bool facingLeft)
        {
            _spriteRenderer.flipX = facingLeft;

            // Tầng 2 đè tầng 1: đang đánh thì vẽ đánh, dù chân vẫn đang chạy.
            if (action != ActionState.None)
            {
                PlayAction(action);
                return;
            }

            PlayLocomotion(locomotion);
        }

        private void PlayLocomotion(LocomotionState locomotion)
        {
            if (!_locomotionHashes.TryGetValue(locomotion, out int hash) || hash == _currentHash)
                return;

            _currentHash = hash;
            _animator.speed = 1f;
            _animator.Play(hash, 0, 0f);
        }

        private void PlayAction(ActionState action)
        {
            if (!_actionHashes.TryGetValue(action, out int hash) || hash == _currentHash)
                return;

            _currentHash = hash;

            // Co clip cho vừa số tick mà LUẬT quy định, thay vì để độ dài clip quyết định luật.
            // Không co thì clip dài hơn bị cắt ngọn giữa chừng, clip ngắn hơn đứng hình chờ —
            // và cách "sửa" hiển nhiên là đi chỉnh clip, tức là mời người làm hình chỉnh cân bằng game.
            int ticks = _profile.GetAction(action).DurationTicks;
            float wanted = ticks * MovementRules.TICK_DT;

            _animator.speed = wanted > 0f ? _actionLengths[action] / wanted : 1f;
            _animator.Play(hash, 0, 0f);
        }
    }
}