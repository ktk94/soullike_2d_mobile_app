using UnityEngine;
using UnityEngine.InputSystem;
using SoulCraft.Core;

namespace SoulCraft.Player
{
    public enum PlayerState
    {
        Idle,
        Moving,
        Dashing,
        Attacking,
        Hit,
        Dead
    }

    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(PlayerStats))]
    [RequireComponent(typeof(PlayerCombat))]
    [RequireComponent(typeof(PlayerAnimator))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float _moveSpeedMultiplier = 1f;

        [Header("Dash")]
        [SerializeField] private float _dashSpeed = 18f;
        [SerializeField] private float _dashDuration = 0.2f;
        [SerializeField] private float _dashCooldown = 0.8f;
        [SerializeField] private float _iFrameDuration = 0.15f;

        [Header("References")]
        [SerializeField] private SpriteRenderer _spriteRenderer;

        // Components
        private Rigidbody2D _rb;
        private PlayerStats _stats;
        private PlayerCombat _combat;
        private PlayerAnimator _playerAnimator;

        // Input
        private PlayerInputActions _inputActions;
        private Vector2 _moveInput;

        // State
        public PlayerState CurrentState { get; private set; } = PlayerState.Idle;
        public Vector2 FacingDirection { get; private set; } = Vector2.down;
        public bool IsInvincible { get; private set; }

        // Dash
        private Vector2 _dashDirection;
        private float _dashTimer;
        private float _dashCooldownTimer;
        private float _iFrameTimer;

        // --- Unity Lifecycle ---

        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _stats = GetComponent<PlayerStats>();
            _combat = GetComponent<PlayerCombat>();
            _playerAnimator = GetComponent<PlayerAnimator>();

            if (_spriteRenderer == null)
                _spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;

            _inputActions = new PlayerInputActions();
        }

        void OnEnable()
        {
            _inputActions.Enable();
            _inputActions.Gameplay.Move.performed += OnMovePerformed;
            _inputActions.Gameplay.Move.canceled += OnMoveCanceled;
            _inputActions.Gameplay.Dash.performed += OnDashPerformed;
            _inputActions.Gameplay.Attack.performed += OnAttackPerformed;
        }

        void OnDisable()
        {
            _inputActions.Gameplay.Move.performed -= OnMovePerformed;
            _inputActions.Gameplay.Move.canceled -= OnMoveCanceled;
            _inputActions.Gameplay.Dash.performed -= OnDashPerformed;
            _inputActions.Gameplay.Attack.performed -= OnAttackPerformed;
            _inputActions.Disable();
        }

        void Update()
        {
            UpdateTimers();
        }

        void FixedUpdate()
        {
            switch (CurrentState)
            {
                case PlayerState.Idle:
                case PlayerState.Moving:
                    HandleMovement();
                    break;
                case PlayerState.Dashing:
                    HandleDash();
                    break;
                case PlayerState.Attacking:
                case PlayerState.Hit:
                case PlayerState.Dead:
                    _rb.linearVelocity = Vector2.zero;
                    break;
            }
        }

        // --- Input Callbacks ---

        private void OnMovePerformed(InputAction.CallbackContext ctx)
        {
            _moveInput = ctx.ReadValue<Vector2>();
        }

        private void OnMoveCanceled(InputAction.CallbackContext ctx)
        {
            _moveInput = Vector2.zero;
        }

        private void OnDashPerformed(InputAction.CallbackContext ctx)
        {
            TryDash();
        }

        private void OnAttackPerformed(InputAction.CallbackContext ctx)
        {
            TryAttack();
        }

        // --- Public API (for virtual joystick) ---

        /// <summary>
        /// 외부(가상 조이스틱 등)에서 이동 입력을 전달할 때 사용.
        /// </summary>
        public void SetMoveInput(Vector2 input)
        {
            _moveInput = input;
        }

        /// <summary>
        /// 외부에서 대시 트리거.
        /// </summary>
        public void TriggerDash() => TryDash();

        /// <summary>
        /// 외부에서 공격 트리거.
        /// </summary>
        public void TriggerAttack() => TryAttack();

        // --- State Management ---

        public void ChangeState(PlayerState newState)
        {
            if (CurrentState == PlayerState.Dead) return;
            CurrentState = newState;
        }

        /// <summary>
        /// PlayerCombat에서 공격 종료 시 호출.
        /// </summary>
        public void OnAttackEnd()
        {
            if (CurrentState == PlayerState.Attacking)
                ChangeState(_moveInput.sqrMagnitude > 0.01f ? PlayerState.Moving : PlayerState.Idle);
        }

        /// <summary>
        /// 피격 처리. PlayerStats.TakeDamage에서 호출.
        /// </summary>
        public void OnHit()
        {
            if (IsInvincible || CurrentState == PlayerState.Dead) return;

            ChangeState(PlayerState.Hit);
            _playerAnimator.PlayHit();

            // 피격 후 짧은 무적
            IsInvincible = true;
            _iFrameTimer = _iFrameDuration;

            // 일정 시간 후 복귀 (Animator Event 또는 타이머)
            Invoke(nameof(RecoverFromHit), 0.3f);
        }

        public void OnDeath()
        {
            ChangeState(PlayerState.Dead);
            _playerAnimator.PlayDeath();
            _rb.linearVelocity = Vector2.zero;
            _rb.simulated = false;
        }

        // --- Movement ---

        private void HandleMovement()
        {
            if (_moveInput.sqrMagnitude > 0.01f)
            {
                ChangeState(PlayerState.Moving);

                Vector2 moveDir = _moveInput.normalized;
                _rb.linearVelocity = moveDir * (_stats.Speed * _moveSpeedMultiplier);

                UpdateFacingDirection(moveDir);
                _playerAnimator.SetMovement(moveDir, _rb.linearVelocity.magnitude);
            }
            else
            {
                ChangeState(PlayerState.Idle);
                _rb.linearVelocity = Vector2.zero;
                _playerAnimator.SetMovement(Vector2.zero, 0f);
            }
        }

        private void UpdateFacingDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.01f) return;

            FacingDirection = direction.normalized;

            // 좌우 방향 전환
            if (_spriteRenderer != null)
            {
                if (direction.x < -0.01f)
                    _spriteRenderer.flipX = true;
                else if (direction.x > 0.01f)
                    _spriteRenderer.flipX = false;
            }
        }

        // --- Dash ---

        private void TryDash()
        {
            if (CurrentState == PlayerState.Dead ||
                CurrentState == PlayerState.Hit ||
                CurrentState == PlayerState.Dashing)
                return;

            if (_dashCooldownTimer > 0f) return;

            _dashDirection = _moveInput.sqrMagnitude > 0.01f
                ? _moveInput.normalized
                : FacingDirection;

            ChangeState(PlayerState.Dashing);
            _dashTimer = _dashDuration;
            _dashCooldownTimer = _dashCooldown;

            // 대시 중 무적
            IsInvincible = true;
            _iFrameTimer = _iFrameDuration;

            _playerAnimator.PlayDash();
        }

        private void HandleDash()
        {
            if (_dashTimer > 0f)
            {
                _rb.linearVelocity = _dashDirection * _dashSpeed;
                _dashTimer -= Time.fixedDeltaTime;
            }
            else
            {
                // 대시 종료
                _rb.linearVelocity = Vector2.zero;
                ChangeState(_moveInput.sqrMagnitude > 0.01f ? PlayerState.Moving : PlayerState.Idle);
            }
        }

        // --- Attack ---

        private void TryAttack()
        {
            if (CurrentState == PlayerState.Dead ||
                CurrentState == PlayerState.Dashing ||
                CurrentState == PlayerState.Hit)
                return;

            _combat.TryAttack();
        }

        // --- Timers ---

        private void UpdateTimers()
        {
            if (_dashCooldownTimer > 0f)
                _dashCooldownTimer -= Time.deltaTime;

            if (_iFrameTimer > 0f)
            {
                _iFrameTimer -= Time.deltaTime;
                if (_iFrameTimer <= 0f)
                    IsInvincible = false;
            }
        }

        private void RecoverFromHit()
        {
            if (CurrentState == PlayerState.Hit)
                ChangeState(_moveInput.sqrMagnitude > 0.01f ? PlayerState.Moving : PlayerState.Idle);
        }
    }
}
