using UnityEngine;
using SoulCraft.Core;

namespace SoulCraft.Player
{
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimator : MonoBehaviour
    {
        // --- Animator Parameter Hashes ---
        private static readonly int HashMoveSpeed = Animator.StringToHash("MoveSpeed");
        private static readonly int HashMoveX = Animator.StringToHash("MoveX");
        private static readonly int HashMoveY = Animator.StringToHash("MoveY");
        private static readonly int HashIsDashing = Animator.StringToHash("IsDashing");
        private static readonly int HashIsAttacking = Animator.StringToHash("IsAttacking");
        private static readonly int HashAttackCombo = Animator.StringToHash("AttackCombo");
        private static readonly int HashIsHit = Animator.StringToHash("IsHit");
        private static readonly int HashIsDead = Animator.StringToHash("IsDead");
        private static readonly int HashTriggerAttack = Animator.StringToHash("TriggerAttack");
        private static readonly int HashTriggerDash = Animator.StringToHash("TriggerDash");
        private static readonly int HashTriggerHit = Animator.StringToHash("TriggerHit");
        private static readonly int HashTriggerDeath = Animator.StringToHash("TriggerDeath");

        private Animator _animator;
        private PlayerController _controller;

        // 마지막으로 설정된 방향 (방향 블렌드용)
        private Vector2 _lastNonZeroDirection = Vector2.down;

        void Awake()
        {
            _animator = GetComponent<Animator>();
            _controller = GetComponent<PlayerController>();
        }

        void LateUpdate()
        {
            if (_controller == null) return;

            // 상태 기반 bool 파라미터 동기화
            _animator.SetBool(HashIsDashing, _controller.CurrentState == PlayerState.Dashing);
            _animator.SetBool(HashIsAttacking, _controller.CurrentState == PlayerState.Attacking);
            _animator.SetBool(HashIsHit, _controller.CurrentState == PlayerState.Hit);
            _animator.SetBool(HashIsDead, _controller.CurrentState == PlayerState.Dead);
        }

        // --- Movement ---

        /// <summary>
        /// 이동 방향 및 속도를 Animator에 전달.
        /// </summary>
        public void SetMovement(Vector2 direction, float speed)
        {
            if (direction.sqrMagnitude > 0.01f)
                _lastNonZeroDirection = direction.normalized;

            _animator.SetFloat(HashMoveSpeed, speed);
            _animator.SetFloat(HashMoveX, _lastNonZeroDirection.x);
            _animator.SetFloat(HashMoveY, _lastNonZeroDirection.y);
        }

        // --- Attack ---

        /// <summary>
        /// 콤보 단계별 공격 애니메이션 트리거.
        /// </summary>
        public void PlayAttack(int comboStep)
        {
            _animator.SetInteger(HashAttackCombo, comboStep);
            _animator.SetTrigger(HashTriggerAttack);
        }

        // --- Dash ---

        public void PlayDash()
        {
            _animator.SetTrigger(HashTriggerDash);
        }

        // --- Hit ---

        public void PlayHit()
        {
            _animator.SetTrigger(HashTriggerHit);
        }

        // --- Death ---

        public void PlayDeath()
        {
            _animator.SetTrigger(HashTriggerDeath);
        }

        // --- Utility ---

        /// <summary>
        /// 현재 바라보는 방향을 4방향(상하좌우) 인덱스로 반환.
        /// 0 = 하, 1 = 좌, 2 = 상, 3 = 우
        /// </summary>
        public int GetDirectionIndex()
        {
            float absX = Mathf.Abs(_lastNonZeroDirection.x);
            float absY = Mathf.Abs(_lastNonZeroDirection.y);

            if (absY >= absX)
                return _lastNonZeroDirection.y >= 0 ? 2 : 0; // 상 : 하
            else
                return _lastNonZeroDirection.x >= 0 ? 3 : 1; // 우 : 좌
        }

        /// <summary>
        /// 현재 블렌드 방향 벡터.
        /// </summary>
        public Vector2 GetFacingDirection() => _lastNonZeroDirection;

        /// <summary>
        /// Animation Event에서 호출 — 공격 판정 타이밍 알림용.
        /// </summary>
        public void AnimEvent_AttackHit()
        {
            // 필요 시 PlayerCombat에서 딜레이 판정으로 전환할 때 사용
        }

        /// <summary>
        /// Animation Event에서 호출 — 공격 종료 알림.
        /// </summary>
        public void AnimEvent_AttackEnd()
        {
            if (_controller != null)
                _controller.OnAttackEnd();
        }
    }
}
