using System.Collections;
using UnityEngine;
using SoulCraft.Core;

namespace SoulCraft.Combat
{
    /// <summary>
    /// 경직(Stagger) 프로파일. 적 종류별로 다른 임계치/지속 시간을 설정한다.
    /// </summary>
    [System.Serializable]
    public struct StaggerProfile
    {
        [Tooltip("경직 게이지 최대치 (이 값을 초과하면 경직 진입)")]
        public float threshold;
        [Tooltip("경직 상태 지속 시간")]
        public float staggerDuration;
        [Tooltip("경직 후 게이지 리셋까지의 쿨다운")]
        public float recoveryCooldown;
        [Tooltip("경직 상태에서 받는 데미지 배율")]
        public float damageMultiplier;
    }

    /// <summary>
    /// 적 경직(Stagger) 시스템.
    /// 누적 데미지가 StaggerGauge 임계치를 초과하면 경직 상태에 진입한다.
    /// 경직 상태: 일정 시간 행동 불가 + 받는 데미지 배율 증가.
    /// 보스도 경직 가능하지만 임계치가 훨씬 높다.
    /// 경직 시 흔들림 애니메이션과 이펙트를 재생한다.
    /// 적 오브젝트에 직접 부착하여 사용한다.
    /// </summary>
    public class EnemyStagger : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────
        [Header("Stagger Profile")]
        [SerializeField] private StaggerProfile _profile = new()
        {
            threshold = 50f,
            staggerDuration = 1.5f,
            recoveryCooldown = 3f,
            damageMultiplier = 1.5f
        };

        [Header("Boss Override")]
        [Tooltip("보스인 경우 사용할 프로파일 (isBoss가 true이면 자동 적용)")]
        [SerializeField] private StaggerProfile _bossProfile = new()
        {
            threshold = 200f,
            staggerDuration = 1.0f,
            recoveryCooldown = 8f,
            damageMultiplier = 1.3f
        };

        [Header("Stagger Visuals")]
        [Tooltip("경직 시 흔들림 강도")]
        [SerializeField] private float _shakeIntensity = 0.08f;
        [Tooltip("경직 시 흔들림 속도 (진동 주파수)")]
        [SerializeField] private float _shakeFrequency = 30f;
        [Tooltip("경직 진입 시 스폰할 이펙트 프리팹 풀 키")]
        [SerializeField] private string _staggerEffectPoolKey = "StaggerFX";
        [Tooltip("경직 시 스프라이트 틴트")]
        [SerializeField] private Color _staggerTint = new Color(1f, 0.7f, 0.7f, 1f);

        [Header("Gauge Decay")]
        [Tooltip("초당 경직 게이지 자연 감소량")]
        [SerializeField] private float _gaugeDecayRate = 5f;
        [Tooltip("피격 후 게이지 감소가 시작되기까지의 대기 시간")]
        [SerializeField] private float _decayDelay = 1.5f;

        // ── Runtime State ─────────────────────────────────────
        private StaggerProfile _activeProfile;
        private float _currentGauge;
        private bool _isStaggered;
        private bool _isOnCooldown;
        private float _lastHitTime;

        // Components
        private SpriteRenderer _spriteRenderer;
        private Color _originalColor;
        private Vector3 _originalLocalPos;
        private Coroutine _staggerCoroutine;
        private Coroutine _shakeCoroutine;

        // ── Properties ────────────────────────────────────────
        /// <summary>현재 경직 상태인지 여부</summary>
        public bool IsStaggered => _isStaggered;

        /// <summary>현재 경직 게이지 (0 ~ threshold)</summary>
        public float CurrentGauge => _currentGauge;

        /// <summary>경직 게이지 비율 (0 ~ 1)</summary>
        public float GaugeRatio => _activeProfile.threshold > 0
            ? Mathf.Clamp01(_currentGauge / _activeProfile.threshold)
            : 0f;

        /// <summary>현재 데미지 배율 (경직 시 증가)</summary>
        public float CurrentDamageMultiplier => _isStaggered ? _activeProfile.damageMultiplier : 1f;

        // ============================================================
        //  Unity Lifecycle
        // ============================================================

        void Awake()
        {
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            _activeProfile = _profile;
        }

        void Start()
        {
            // EnemyData에서 보스 여부 확인
            var enemyBase = GetComponent<SoulCraft.Enemy.EnemyBase>();
            if (enemyBase != null && enemyBase.Data != null && enemyBase.Data.isBoss)
            {
                _activeProfile = _bossProfile;
            }

            if (_spriteRenderer != null)
                _originalColor = _spriteRenderer.color;

            _originalLocalPos = _spriteRenderer != null
                ? _spriteRenderer.transform.localPosition
                : Vector3.zero;
        }

        void OnEnable()
        {
            GameEventSystem.Subscribe<DamageEvent>(OnDamageEvent);
            ResetGauge();
        }

        void OnDisable()
        {
            GameEventSystem.Unsubscribe<DamageEvent>(OnDamageEvent);
            StopAllStaggerEffects();
        }

        void Update()
        {
            // 게이지 자연 감소 (피격 후 일정 시간 경과 후)
            if (!_isStaggered && _currentGauge > 0f)
            {
                if (Time.time - _lastHitTime > _decayDelay)
                {
                    _currentGauge = Mathf.Max(0f, _currentGauge - _gaugeDecayRate * Time.deltaTime);
                }
            }
        }

        // ============================================================
        //  Public API
        // ============================================================

        /// <summary>
        /// 경직 게이지에 값을 추가한다. 임계치 초과 시 경직 진입.
        /// </summary>
        public void AddStaggerGauge(float amount)
        {
            if (_isStaggered || _isOnCooldown) return;

            _currentGauge += amount;
            _lastHitTime = Time.time;

            if (_currentGauge >= _activeProfile.threshold)
            {
                EnterStagger();
            }
        }

        /// <summary>
        /// 즉시 경직 상태에 진입시킨다 (보스 패턴 등에서 사용).
        /// </summary>
        public void ForceStagger()
        {
            if (_isStaggered) return;
            EnterStagger();
        }

        /// <summary>
        /// 게이지를 초기화한다.
        /// </summary>
        public void ResetGauge()
        {
            _currentGauge = 0f;
            _isStaggered = false;
            _isOnCooldown = false;
        }

        // ============================================================
        //  Event Handler
        // ============================================================

        private void OnDamageEvent(DamageEvent evt)
        {
            // 이 오브젝트가 타겟인 경우에만 처리
            if (evt.Target != gameObject) return;

            // 경직 게이지 추가 (데미지량 기반)
            float staggerAmount = evt.Damage;

            // 크리티컬이면 경직 기여량 증가
            if (evt.IsCritical)
                staggerAmount *= 1.5f;

            AddStaggerGauge(staggerAmount);
        }

        // ============================================================
        //  Stagger Flow
        // ============================================================

        private void EnterStagger()
        {
            _isStaggered = true;
            _currentGauge = 0f;

            // 적 상태를 Hit로 변경 (행동 불가)
            var enemyBase = GetComponent<SoulCraft.Enemy.EnemyBase>();
            if (enemyBase != null)
                enemyBase.SetState(SoulCraft.Enemy.EnemyState.Hit);

            // 경직 연출 시작
            if (_staggerCoroutine != null) StopCoroutine(_staggerCoroutine);
            _staggerCoroutine = StartCoroutine(StaggerCoroutine());

            // 흔들림 연출
            if (_shakeCoroutine != null) StopCoroutine(_shakeCoroutine);
            _shakeCoroutine = StartCoroutine(StaggerShakeCoroutine(_activeProfile.staggerDuration));

            // 경직 이펙트 스폰
            SpawnStaggerEffect();

            // 스프라이트 틴트
            if (_spriteRenderer != null)
                _spriteRenderer.color = _staggerTint;
        }

        private IEnumerator StaggerCoroutine()
        {
            yield return new WaitForSeconds(_activeProfile.staggerDuration);

            ExitStagger();
        }

        private void ExitStagger()
        {
            _isStaggered = false;

            // 스프라이트 색상 복원
            if (_spriteRenderer != null)
                _spriteRenderer.color = _originalColor;

            // 적 상태 복원
            var enemyBase = GetComponent<SoulCraft.Enemy.EnemyBase>();
            if (enemyBase != null && enemyBase.CurrentState != SoulCraft.Enemy.EnemyState.Dead)
                enemyBase.SetState(SoulCraft.Enemy.EnemyState.Idle);

            // 쿨다운 시작
            _isOnCooldown = true;
            StartCoroutine(CooldownCoroutine());

            _staggerCoroutine = null;
        }

        private IEnumerator CooldownCoroutine()
        {
            yield return new WaitForSeconds(_activeProfile.recoveryCooldown);
            _isOnCooldown = false;
        }

        // ============================================================
        //  Visual Effects
        // ============================================================

        /// <summary>
        /// 경직 상태에서 스프라이트를 빠르게 좌우로 흔든다.
        /// </summary>
        private IEnumerator StaggerShakeCoroutine(float duration)
        {
            if (_spriteRenderer == null) yield break;

            Transform spriteTransform = _spriteRenderer.transform;
            float elapsed = 0f;

            while (elapsed < duration && _isStaggered)
            {
                elapsed += Time.deltaTime;

                // 고주파 사인파 흔들림
                float offsetX = Mathf.Sin(elapsed * _shakeFrequency) * _shakeIntensity;
                // 시간이 지남에 따라 강도 감소
                float decay = 1f - (elapsed / duration) * 0.5f;
                spriteTransform.localPosition = _originalLocalPos
                    + new Vector3(offsetX * decay, 0f, 0f);

                yield return null;
            }

            // 위치 복원
            spriteTransform.localPosition = _originalLocalPos;
            _shakeCoroutine = null;
        }

        private void SpawnStaggerEffect()
        {
            if (ObjectPool.Instance == null) return;
            if (string.IsNullOrEmpty(_staggerEffectPoolKey)) return;

            GameObject fx = ObjectPool.Instance.Spawn(
                _staggerEffectPoolKey,
                transform.position + Vector3.up * 0.5f,
                Quaternion.identity);

            if (fx != null)
                ObjectPool.Instance.Despawn(_staggerEffectPoolKey, fx, _activeProfile.staggerDuration);
        }

        private void StopAllStaggerEffects()
        {
            if (_staggerCoroutine != null)
            {
                StopCoroutine(_staggerCoroutine);
                _staggerCoroutine = null;
            }
            if (_shakeCoroutine != null)
            {
                StopCoroutine(_shakeCoroutine);
                _shakeCoroutine = null;
            }

            // 스프라이트 복원
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = _originalColor;
                _spriteRenderer.transform.localPosition = _originalLocalPos;
            }

            _isStaggered = false;
        }
    }
}
