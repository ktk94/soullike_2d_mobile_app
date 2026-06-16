using UnityEngine;
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
            if (_spriteRenderer == null)
                _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (_rb != null)
            {
                _rb.gravityScale = 0f;
                _rb.freezeRotation = true;
            }
        }

        void Start()
        {
            _stats = GetComponent<PlayerStats>();
            _combat = GetComponent<PlayerCombat>();
            _playerAnimator = GetComponent<PlayerAnimator>();
        }

        void Update()
        {
            UpdateTimers();
            ReadKeyboardInput();
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

        // --- Keyboard Input (Editor/PC 테스트용) ---

        private void ReadKeyboardInput()
        {
            if (CurrentState == PlayerState.Dead) return;

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Vector2 kbInput = new Vector2(h, v);
            if (kbInput.sqrMagnitude > 0.01f)
                _moveInput = kbInput.normalized;
            else if (!_externalInputActive)
                _moveInput = Vector2.zero;

            if (Input.GetKeyDown(KeyCode.Space))
                TryDash();
            if (Input.GetKeyDown(KeyCode.Z) || Input.GetMouseButtonDown(0))
                TryAttack();
        }

        private bool _externalInputActive;

        // --- Public API (가상 조이스틱 / MobileInputUI에서 호출) ---

        public void SetMoveInput(Vector2 input)
        {
            _moveInput = input;
            _externalInputActive = input.sqrMagnitude > 0.01f;
        }

        public void TriggerDash() => TryDash();

        public void TriggerAttack() => TryAttack();

        // --- State Management ---

        public void ChangeState(PlayerState newState)
        {
            if (CurrentState == PlayerState.Dead) return;
            CurrentState = newState;
        }

        public void OnAttackEnd()
        {
            if (CurrentState == PlayerState.Attacking)
                ChangeState(_moveInput.sqrMagnitude > 0.01f ? PlayerState.Moving : PlayerState.Idle);
        }

        public void OnHit()
        {
            if (IsInvincible || CurrentState == PlayerState.Dead) return;

            ChangeState(PlayerState.Hit);
            _playerAnimator.PlayHit();

            IsInvincible = true;
            _iFrameTimer = _iFrameDuration;

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
            float speed = _stats != null ? _stats.Speed : 5f;
            if (_moveInput.sqrMagnitude > 0.01f)
            {
                ChangeState(PlayerState.Moving);

                Vector2 moveDir = _moveInput.normalized;
                _rb.linearVelocity = moveDir * (speed * _moveSpeedMultiplier);

                UpdateFacingDirection(moveDir);
                if (_playerAnimator != null)
                    _playerAnimator.SetMovement(moveDir, _rb.linearVelocity.magnitude);
            }
            else
            {
                ChangeState(PlayerState.Idle);
                _rb.linearVelocity = Vector2.zero;
                if (_playerAnimator != null)
                    _playerAnimator.SetMovement(Vector2.zero, 0f);
            }
        }

        private void UpdateFacingDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.01f) return;

            FacingDirection = direction.normalized;

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

            if (_combat != null) _combat.TryAttack();
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
