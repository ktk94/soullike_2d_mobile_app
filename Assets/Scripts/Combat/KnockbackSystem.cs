using System.Collections;
using UnityEngine;
using SoulCraft.Core;

namespace SoulCraft.Combat
{
    /// <summary>
    /// 넉백 유형별 거리 설정.
    /// </summary>
    [System.Serializable]
    public struct KnockbackProfile
    {
        [Tooltip("넉백 힘 (Impulse Force)")]
        public float force;
        [Tooltip("넉백 지속 시간")]
        public float duration;
        [Tooltip("에어본(띄우기) 여부")]
        public bool isAirborne;
        [Tooltip("에어본 시 체공 시간")]
        public float airborneDuration;
    }

    /// <summary>
    /// 피격 시 넉백을 전담 처리하는 시스템.
    /// DamageEvent를 구독하여 공격 유형별 넉백 프로파일을 적용한다.
    /// 넉백 중 벽 충돌 시 추가 "벽꿈" 데미지를 부여하며,
    /// 에어본 상태(띄우기)를 지원하여 추가타가 가능하도록 한다.
    /// </summary>
    public class KnockbackSystem : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────
        public static KnockbackSystem Instance { get; private set; }

        // ── Knockback Profiles ────────────────────────────────
        [Header("Knockback Profiles")]
        [SerializeField] private KnockbackProfile _normalAttack = new()
        {
            force = 4f, duration = 0.12f, isAirborne = false, airborneDuration = 0f
        };
        [SerializeField] private KnockbackProfile _skillAttack = new()
        {
            force = 6f, duration = 0.18f, isAirborne = false, airborneDuration = 0f
        };
        [SerializeField] private KnockbackProfile _criticalAttack = new()
        {
            force = 8f, duration = 0.22f, isAirborne = false, airborneDuration = 0f
        };
        [SerializeField] private KnockbackProfile _comboFinisher = new()
        {
            force = 12f, duration = 0.3f, isAirborne = true, airborneDuration = 0.5f
        };

        // ── Wall Slam ─────────────────────────────────────────
        [Header("Wall Slam (벽꿈 데미지)")]
        [Tooltip("벽꿈 시 추가 데미지 (원래 데미지의 배율)")]
        [SerializeField] private float _wallSlamDamageMultiplier = 0.3f;
        [Tooltip("벽꿈 판정용 레이캐스트 거리")]
        [SerializeField] private float _wallCheckDistance = 0.3f;
        [Tooltip("벽 레이어")]
        [SerializeField] private LayerMask _wallLayer;
        [Tooltip("벽꿈 시 화면 흔들림 강도")]
        [SerializeField] private float _wallSlamShakeIntensity = 0.35f;

        // ── Airborne ──────────────────────────────────────────
        [Header("Airborne (에어본)")]
        [Tooltip("에어본 시 Y축 오프셋 (시각적 띄우기)")]
        [SerializeField] private float _airborneHeight = 0.6f;
        [Tooltip("에어본 상태에서 받는 추가 데미지 배율")]
        [SerializeField] private float _airborneDamageMultiplier = 1.3f;
        [Tooltip("에어본 바운스 횟수")]
        [SerializeField] private int _airborneBounces = 1;

        // ── Combo Step Threshold ──────────────────────────────
        [Header("Combo Finisher Detection")]
        [Tooltip("이 콤보 카운트 이상이면 피니셔로 판정")]
        [SerializeField] private int _finisherComboThreshold = 3;

        // ============================================================
        //  Unity Lifecycle
        // ============================================================

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnEnable()
        {
            GameEventSystem.Subscribe<DamageEvent>(OnDamageEvent);
        }

        void OnDisable()
        {
            GameEventSystem.Unsubscribe<DamageEvent>(OnDamageEvent);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ============================================================
        //  Public API
        // ============================================================

        /// <summary>
        /// 특정 대상에게 커스텀 넉백을 적용한다.
        /// </summary>
        public void ApplyKnockback(GameObject target, Vector2 direction, KnockbackProfile profile, int baseDamage = 0)
        {
            if (target == null) return;

            var rb = target.GetComponent<Rigidbody2D>();
            if (rb == null) return;

            StartCoroutine(KnockbackCoroutine(target, rb, direction.normalized, profile, baseDamage));
        }

        /// <summary>
        /// 대상이 현재 에어본 상태인지 확인한다.
        /// </summary>
        public bool IsAirborne(GameObject target)
        {
            if (target == null) return false;
            var state = target.GetComponent<AirborneState>();
            return state != null && state.IsAirborne;
        }

        /// <summary>
        /// 에어본 대상에 대한 추가 데미지 배율을 반환한다.
        /// </summary>
        public float GetAirborneDamageMultiplier(GameObject target)
        {
            return IsAirborne(target) ? _airborneDamageMultiplier : 1f;
        }

        // ============================================================
        //  Event Handler
        // ============================================================

        private void OnDamageEvent(DamageEvent evt)
        {
            if (evt.Target == null || evt.Attacker == null) return;

            // 플레이어가 피격자인 경우 별도 처리 (PlayerController가 담당)
            if (evt.Target.CompareTag("Player")) return;

            // 넉백 방향: 공격자 → 피격자
            Vector2 knockDir = ((Vector2)evt.Target.transform.position
                              - (Vector2)evt.Attacker.transform.position).normalized;

            if (knockDir.sqrMagnitude < 0.001f)
                knockDir = Vector2.right;

            // 프로파일 선택
            KnockbackProfile profile = SelectProfile(evt);

            ApplyKnockback(evt.Target, knockDir, profile, evt.Damage);
        }

        // ============================================================
        //  Profile Selection
        // ============================================================

        private KnockbackProfile SelectProfile(DamageEvent evt)
        {
            // 콤보 피니셔 체크 (콤보 이벤트 기반 — ComboEvent에서 카운트 확인)
            // 여기서는 크리티컬 + 높은 데미지를 피니셔 힌트로 사용
            bool isLikelyFinisher = evt.IsCritical && evt.Damage > 50;

            // 스킬 공격인지 판별 (DamageType이 Physical이 아니면 스킬로 간주)
            bool isSkillAttack = evt.Type != DamageType.Physical;

            if (isLikelyFinisher)
                return _comboFinisher;
            if (evt.IsCritical)
                return _criticalAttack;
            if (isSkillAttack)
                return _skillAttack;

            return _normalAttack;
        }

        // ============================================================
        //  Knockback Coroutine
        // ============================================================

        private IEnumerator KnockbackCoroutine(
            GameObject target,
            Rigidbody2D rb,
            Vector2 direction,
            KnockbackProfile profile,
            int baseDamage)
        {
            if (target == null) yield break;

            // 넉백 임펄스 적용
            rb.linearVelocity = Vector2.zero;
            rb.AddForce(direction * profile.force, ForceMode2D.Impulse);

            // 에어본 처리
            if (profile.isAirborne)
            {
                StartCoroutine(AirborneCoroutine(target, profile.airborneDuration));
            }

            // 넉백 중 벽 충돌 체크
            float elapsed = 0f;
            bool wallSlammed = false;

            while (elapsed < profile.duration)
            {
                if (target == null) yield break;

                elapsed += Time.deltaTime;

                // 벽 충돌 체크 (넉백 방향으로 레이캐스트)
                if (!wallSlammed && _wallLayer.value != 0)
                {
                    RaycastHit2D wallHit = Physics2D.Raycast(
                        target.transform.position,
                        direction,
                        _wallCheckDistance,
                        _wallLayer);

                    if (wallHit.collider != null)
                    {
                        wallSlammed = true;
                        OnWallSlam(target, baseDamage, wallHit.point);

                        // 벽에 부딪히면 즉시 정지
                        rb.linearVelocity = Vector2.zero;
                        yield break;
                    }
                }

                yield return null;
            }

            // 넉백 종료 — 속도 감쇠
            if (target != null && rb != null)
            {
                rb.linearVelocity *= 0.2f; // 약간의 관성 잔여
            }
        }

        // ============================================================
        //  Wall Slam (벽꿈)
        // ============================================================

        private void OnWallSlam(GameObject target, int baseDamage, Vector2 wallPoint)
        {
            // 벽꿈 추가 데미지
            int wallDamage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * _wallSlamDamageMultiplier));

            // 벽꿈 데미지를 DamageEvent로 발행 (연쇄 이펙트 트리거)
            GameEventSystem.Publish(new DamageEvent
            {
                Attacker = null, // 환경 데미지
                Target = target,
                Damage = wallDamage,
                IsCritical = false,
                Type = DamageType.Physical,
                HitPoint = wallPoint
            });

            // 화면 흔들림
            if (CameraController.Instance != null)
                CameraController.Instance.Shake(_wallSlamShakeIntensity, 0.15f);

            // 벽꿈 히트스톱
            if (ImpactSystem.Instance != null)
                ImpactSystem.Instance.TriggerHitStop(0.06f);
        }

        // ============================================================
        //  Airborne (에어본)
        // ============================================================

        private IEnumerator AirborneCoroutine(GameObject target, float duration)
        {
            if (target == null) yield break;

            // AirborneState 컴포넌트 부착 (이미 있으면 재사용)
            var state = target.GetComponent<AirborneState>();
            if (state == null)
                state = target.AddComponent<AirborneState>();

            state.IsAirborne = true;

            // 시각적 띄우기 — SpriteRenderer의 자식 또는 shadow 분리
            // 간단히 스프라이트 오프셋으로 처리
            var sr = target.GetComponentInChildren<SpriteRenderer>();
            Transform spriteTransform = sr != null ? sr.transform : null;
            Vector3 originalLocalPos = spriteTransform != null
                ? spriteTransform.localPosition
                : Vector3.zero;

            float elapsed = 0f;
            int bounceCount = 0;

            while (elapsed < duration)
            {
                if (target == null) yield break;

                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // 포물선 높이 (바운스 포함)
                float height;
                if (bounceCount < _airborneBounces && t > 0.6f)
                {
                    // 바운스 구간
                    float bounceT = (t - 0.6f) / 0.4f;
                    height = _airborneHeight * 0.4f * Mathf.Sin(bounceT * Mathf.PI);
                    if (bounceT > 0.5f && bounceCount < _airborneBounces)
                        bounceCount++;
                }
                else
                {
                    // 메인 포물선
                    height = _airborneHeight * Mathf.Sin(Mathf.Min(t / 0.6f, 1f) * Mathf.PI);
                }

                if (spriteTransform != null)
                    spriteTransform.localPosition = originalLocalPos + Vector3.up * height;

                yield return null;
            }

            // 착지 복원
            if (spriteTransform != null)
                spriteTransform.localPosition = originalLocalPos;

            if (state != null)
                state.IsAirborne = false;
        }
    }

    /// <summary>
    /// 에어본 상태를 추적하는 가벼운 컴포넌트.
    /// KnockbackSystem이 동적으로 부착하며, 외부에서 에어본 여부를 조회할 수 있다.
    /// </summary>
    public class AirborneState : MonoBehaviour
    {
        /// <summary>현재 에어본 상태인지 여부</summary>
        public bool IsAirborne { get; set; }
    }
}
