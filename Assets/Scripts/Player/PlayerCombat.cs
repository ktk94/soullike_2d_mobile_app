using UnityEngine;
using SoulCraft.Core;

namespace SoulCraft.Player
{
    [System.Serializable]
    public struct ComboStep
    {
        [Tooltip("공격 데미지 배율")]
        public float damageMultiplier;
        [Tooltip("공격 판정 반지름")]
        public float hitRadius;
        [Tooltip("공격 판정 오프셋 (전방 거리)")]
        public float hitOffset;
        [Tooltip("전진(lunge) 거리")]
        public float lungeDistance;
        [Tooltip("이 단계 공격 지속 시간")]
        public float duration;
    }

    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Combo Steps")]
        [SerializeField] private ComboStep[] _comboSteps = new ComboStep[]
        {
            new() { damageMultiplier = 1.0f, hitRadius = 0.8f, hitOffset = 0.7f, lungeDistance = 0.3f, duration = 0.3f },
            new() { damageMultiplier = 1.2f, hitRadius = 0.9f, hitOffset = 0.8f, lungeDistance = 0.4f, duration = 0.35f },
            new() { damageMultiplier = 1.6f, hitRadius = 1.1f, hitOffset = 1.0f, lungeDistance = 0.6f, duration = 0.45f }
        };

        [Header("Combo Timing")]
        [Tooltip("콤보 다음 단계 입력 허용 시간")]
        [SerializeField] private float _comboWindowDuration = 0.4f;

        [Header("Hit Detection")]
        [SerializeField] private LayerMask _enemyLayer;

        [Header("Hit Stop")]
        [SerializeField] private float _hitStopDuration = 0.05f;

        [Header("Screen Shake")]
        [SerializeField] private float _shakeIntensity = 0.2f;
        [SerializeField] private float _shakeDuration = 0.1f;

        [Header("Skill")]
        [Tooltip("SkillManager (선택)")]
        [SerializeField] private MonoBehaviour _skillManager; // SkillManager 타입이 구현되면 교체

        // Components
        private PlayerController _controller;
        private PlayerStats _stats;
        private PlayerAnimator _playerAnimator;
        private Rigidbody2D _rb;

        // Combo state
        private int _currentComboStep;
        private float _attackTimer;
        private float _comboWindowTimer;
        private bool _hasNextComboInput;
        private bool _isAttacking;
        private bool _hitDetected; // 현재 스텝에서 히트 판정 완료 여부

        // Hit stop
        private float _hitStopTimer;
        private bool _isHitStopped;
        private float _cachedTimeScale;

        // Slash visual
        private GameObject _slashVisual;
        private SpriteRenderer _slashRenderer;
        private float _slashVisualTimer;

        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
        }

        void Start()
        {
            _controller = GetComponent<PlayerController>();
            _stats = GetComponent<PlayerStats>();
            _playerAnimator = GetComponent<PlayerAnimator>();
            CreateSlashVisual();
        }

        private void CreateSlashVisual()
        {
            _slashVisual = new GameObject("SlashEffect");
            _slashVisual.transform.SetParent(transform);
            _slashVisual.transform.localPosition = new Vector3(0.5f, 0, 0);
            _slashRenderer = _slashVisual.AddComponent<SpriteRenderer>();
            _slashRenderer.sortingLayerName = "Effect";
            _slashRenderer.sortingOrder = 20;

            // SpriteFactory에서 슬래시 스프라이트 가져오기
            var slashSprite = SoulCraft.Factory.SpriteFactory.GetSprite("fx_slash_1");
            if (slashSprite != null)
                _slashRenderer.sprite = slashSprite;
            else
            {
                // 폴백: 흰색 원호 만들기
                int size = 32;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), new Vector2(size / 2f, size / 2f));
                        bool inArc = dist > 8 && dist < 14 && x > size / 2;
                        tex.SetPixel(x, y, inArc ? Color.white : Color.clear);
                    }
                tex.Apply();
                _slashRenderer.sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16);
            }
            _slashVisual.SetActive(false);
        }

        private void ShowSlashEffect(int step)
        {
            // AttackVisualizer가 있으면 우선 사용
            Vector2 facing = _controller != null ? _controller.FacingDirection : Vector2.right;
            Vector2 effectPos = (Vector2)transform.position + facing.normalized * 0.6f;

            var visualizer = GetComponent<SoulCraft.Combat.AttackVisualizer>();
            if (visualizer != null)
            {
                visualizer.ShowComboEffect(step, facing, effectPos);
            }

            // 기본 슬래시 비주얼도 표시 (폴백 겸 보조 이펙트)
            if (_slashVisual == null) return;
            _slashVisual.SetActive(true);

            // 공격 방향에 따라 회전
            float angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
            _slashVisual.transform.localRotation = Quaternion.Euler(0, 0, angle - 90 + step * 60);
            _slashVisual.transform.localPosition = (Vector3)(facing.normalized * 0.6f);

            // 스케일로 콤보 단계 표현
            float scale = 1f + step * 0.3f;
            _slashVisual.transform.localScale = new Vector3(scale, scale, 1);

            // 색상: 단계별로 점점 밝게
            Color c = step == 0 ? Color.white : step == 1 ? new Color(0.8f, 0.9f, 1f) : new Color(1f, 0.9f, 0.5f);
            _slashRenderer.color = c;

            _slashVisualTimer = 0.15f;
        }

        void Update()
        {
            UpdateHitStop();
            UpdateAttack();
            UpdateComboWindow();
            UpdateSlashVisual();
        }

        // --- Public API ---

        /// <summary>
        /// PlayerController에서 호출. 공격 시도.
        /// </summary>
        public void TryAttack()
        {
            if (_isAttacking)
            {
                // 현재 공격 중이면 다음 콤보 예약
                if (_currentComboStep < _comboSteps.Length - 1)
                    _hasNextComboInput = true;
                return;
            }

            StartAttack(0);
        }

        /// <summary>
        /// 현재 콤보 단계를 반환 (0-based). 공격 중이 아니면 -1.
        /// </summary>
        public int GetCurrentComboStep() => _isAttacking ? _currentComboStep : -1;

        // --- Attack Flow ---

        private void StartAttack(int step)
        {
            _currentComboStep = step;
            _isAttacking = true;
            _hitDetected = false;
            _hasNextComboInput = false;
            _comboWindowTimer = 0f;

            ComboStep data = _comboSteps[step];
            _attackTimer = data.duration;

            if (_controller != null) _controller.ChangeState(PlayerState.Attacking);
            if (_playerAnimator != null) _playerAnimator.PlayAttack(step);
            ShowSlashEffect(step);

            // Lunge (전진)
            ApplyLunge(data.lungeDistance);

            // Hit detection (약간의 딜레이 후 판정 — 즉시 판정)
            PerformHitDetection(data);
        }

        private void UpdateAttack()
        {
            if (!_isAttacking) return;
            if (_isHitStopped) return;

            _attackTimer -= Time.deltaTime;
            if (_attackTimer <= 0f)
            {
                OnAttackStepEnd();
            }
        }

        private void OnAttackStepEnd()
        {
            if (_hasNextComboInput && _currentComboStep < _comboSteps.Length - 1)
            {
                // 다음 콤보 단계로
                StartAttack(_currentComboStep + 1);
            }
            else
            {
                // 콤보 윈도우 오픈 (마지막 단계가 아니면)
                if (_currentComboStep < _comboSteps.Length - 1)
                {
                    _isAttacking = false;
                    _comboWindowTimer = _comboWindowDuration;
                }
                else
                {
                    // 마지막 단계 종료
                    EndCombo();
                }
            }
        }

        private void UpdateComboWindow()
        {
            if (_comboWindowTimer <= 0f) return;

            _comboWindowTimer -= Time.deltaTime;
            if (_comboWindowTimer <= 0f)
            {
                // 콤보 윈도우 만료 — 콤보 리셋
                EndCombo();
            }
        }

        private void EndCombo()
        {
            _isAttacking = false;
            _currentComboStep = 0;
            _hasNextComboInput = false;
            _comboWindowTimer = 0f;
            if (_controller != null) _controller.OnAttackEnd();
        }

        private void UpdateSlashVisual()
        {
            if (_slashVisualTimer > 0)
            {
                _slashVisualTimer -= Time.deltaTime;
                if (_slashVisualTimer <= 0 && _slashVisual != null)
                    _slashVisual.SetActive(false);
            }
        }

        // --- Hit Detection ---

        private void PerformHitDetection(ComboStep data)
        {
            Vector2 facing = _controller != null ? _controller.FacingDirection : Vector2.right;
            Vector2 origin = (Vector2)transform.position + facing * data.hitOffset;

            // LayerMask 없이 전체 검색 후 EnemyBase로 필터링
            Collider2D[] hits = Physics2D.OverlapCircleAll(origin, data.hitRadius);

            bool anyHit = false;

            foreach (var hit in hits)
            {
                if (hit.gameObject == gameObject) continue;

                // EnemyBase가 있는 오브젝트만 타격
                var enemyBase = hit.GetComponent<SoulCraft.Enemy.EnemyBase>();
                if (enemyBase == null) continue;
                if (enemyBase.IsDead) continue;

                int rawDamage = CalculateDamage(data.damageMultiplier, out bool isCritical);

                // EnemyBase에 직접 데미지 적용
                enemyBase.TakeDamage(rawDamage, (Vector2)transform.position);

                // 이벤트 발행
                GameEventSystem.Publish(new DamageEvent
                {
                    Attacker = gameObject,
                    Target = hit.gameObject,
                    Damage = rawDamage,
                    IsCritical = isCritical,
                    Type = DamageType.Physical,
                    HitPoint = hit.ClosestPoint(origin)
                });

                // 히트 스파크 이펙트
                var visualizer = GetComponent<SoulCraft.Combat.AttackVisualizer>();
                if (visualizer != null)
                    visualizer.ShowHitSpark(hit.ClosestPoint(origin));

                anyHit = true;
            }

            if (anyHit)
            {
                _hitDetected = true;
                ApplyHitStop();
                ApplyScreenShake();

                // 콤보 이벤트
                GameEventSystem.Publish(new ComboEvent
                {
                    ComboName = "BasicAttack",
                    ComboCount = _currentComboStep + 1,
                    BonusDamageMultiplier = data.damageMultiplier
                });
            }
        }

        private int CalculateDamage(float multiplier, out bool isCritical)
        {
            float baseDamage = _stats.Attack * multiplier;

            isCritical = Random.value < _stats.CritRate;
            if (isCritical)
                baseDamage *= _stats.CritDamage;

            return Mathf.Max(1, Mathf.RoundToInt(baseDamage));
        }

        // --- Lunge ---

        private void ApplyLunge(float distance)
        {
            if (distance <= 0f) return;

            Vector2 facing = _controller.FacingDirection;
            _rb.MovePosition(_rb.position + facing * distance);
        }

        // --- Hit Stop ---

        private void ApplyHitStop()
        {
            if (_hitStopDuration <= 0f) return;

            _isHitStopped = true;
            _cachedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            _hitStopTimer = _hitStopDuration;
        }

        private void UpdateHitStop()
        {
            if (!_isHitStopped) return;

            // unscaled time 사용 (timeScale이 0이므로)
            _hitStopTimer -= Time.unscaledDeltaTime;
            if (_hitStopTimer <= 0f)
            {
                _isHitStopped = false;
                Time.timeScale = _cachedTimeScale;
            }
        }

        // --- Screen Shake ---

        private void ApplyScreenShake()
        {
            if (CameraController.Instance != null)
                CameraController.Instance.Shake(_shakeIntensity, _shakeDuration);
        }

        // --- Gizmos ---

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (_comboSteps == null || _comboSteps.Length == 0) return;

            Vector2 facing = Application.isPlaying
                ? _controller.FacingDirection
                : Vector2.right;

            foreach (var step in _comboSteps)
            {
                Vector2 origin = (Vector2)transform.position + facing * step.hitOffset;
                Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.3f);
                Gizmos.DrawWireSphere(origin, step.hitRadius);
            }
        }
#endif
    }

}
