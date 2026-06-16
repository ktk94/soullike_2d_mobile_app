using System.Collections;
using UnityEngine;
using SoulCraft.Core;

namespace SoulCraft.Combat
{
    /// <summary>
    /// 콤보 단계별 전진 거리 프로파일.
    /// </summary>
    [System.Serializable]
    public struct LungeProfile
    {
        [Tooltip("전진 거리")]
        public float distance;
        [Tooltip("전진 시간")]
        public float duration;
        [Tooltip("전진 후 관성 감속 시간")]
        public float inertiaDuration;
    }

    /// <summary>
    /// 공격 시 플레이어가 적 방향으로 미끄러지듯 전진하는 시스템.
    /// 콤보 단계별 전진 거리가 다르며, 전진 중 적과의 충돌을 무시(통과)한다.
    /// 전진 종료 시 약간의 관성 느낌을 부여한다.
    /// PlayerCombat 또는 Player 오브젝트에 부착한다.
    /// </summary>
    public class AttackLunge : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────
        [Header("Combo Lunge Profiles")]
        [Tooltip("콤보 1단계 (짧은 전진)")]
        [SerializeField] private LungeProfile _combo1 = new()
        {
            distance = 0.5f, duration = 0.08f, inertiaDuration = 0.05f
        };
        [Tooltip("콤보 2단계 (중간 전진)")]
        [SerializeField] private LungeProfile _combo2 = new()
        {
            distance = 0.8f, duration = 0.10f, inertiaDuration = 0.06f
        };
        [Tooltip("콤보 3단계 (긴 전진)")]
        [SerializeField] private LungeProfile _combo3 = new()
        {
            distance = 1.3f, duration = 0.14f, inertiaDuration = 0.08f
        };

        [Header("Skill Lunge")]
        [Tooltip("스킬별 커스텀 전진 거리 (0이면 기본값 사용)")]
        [SerializeField] private float _defaultSkillLungeDistance = 1.0f;
        [SerializeField] private float _skillLungeDuration = 0.12f;
        [SerializeField] private float _skillInertiaDuration = 0.06f;

        [Header("Collision")]
        [Tooltip("전진 중 무시할 레이어 (적 레이어)")]
        [SerializeField] private LayerMask _ignoreLayerDuringLunge;

        [Header("Easing")]
        [Tooltip("전진 이징 커브 (기본: 빠르게 시작, 천천히 정지)")]
        [SerializeField] private AnimationCurve _lungeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Target Snapping")]
        [Tooltip("근처 적을 향해 자동 보정 (0이면 비활성)")]
        [SerializeField] private float _autoAimRadius = 2f;
        [Tooltip("적 감지 레이어")]
        [SerializeField] private LayerMask _enemyLayer;

        // ── Components ────────────────────────────────────────
        private Rigidbody2D _rb;
        private Collider2D _collider;
        private Coroutine _lungeCoroutine;

        // ── State ─────────────────────────────────────────────
        private bool _isLunging;
        private int _originalLayer;

        /// <summary>현재 전진(런지) 중인지 여부</summary>
        public bool IsLunging => _isLunging;

        // ============================================================
        //  Unity Lifecycle
        // ============================================================

        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
        }

        void OnDisable()
        {
            if (_isLunging)
                CancelLunge();
        }

        // ============================================================
        //  Public API
        // ============================================================

        /// <summary>
        /// 콤보 단계에 맞는 전진을 실행한다.
        /// </summary>
        /// <param name="comboStep">콤보 단계 (0-based)</param>
        /// <param name="direction">전진 방향 (자동 보정이 활성화되면 적 방향으로 수정될 수 있다)</param>
        public void PerformComboLunge(int comboStep, Vector2 direction)
        {
            LungeProfile profile = GetComboProfile(comboStep);
            Vector2 finalDir = TryAutoAim(direction);
            ExecuteLunge(finalDir, profile);
        }

        /// <summary>
        /// 스킬에 맞는 커스텀 전진을 실행한다.
        /// </summary>
        /// <param name="direction">전진 방향</param>
        /// <param name="customDistance">커스텀 거리 (0이면 기본값 사용)</param>
        public void PerformSkillLunge(Vector2 direction, float customDistance = 0f)
        {
            float dist = customDistance > 0f ? customDistance : _defaultSkillLungeDistance;
            var profile = new LungeProfile
            {
                distance = dist,
                duration = _skillLungeDuration,
                inertiaDuration = _skillInertiaDuration
            };

            Vector2 finalDir = TryAutoAim(direction);
            ExecuteLunge(finalDir, profile);
        }

        /// <summary>
        /// 현재 진행 중인 전진을 취소한다.
        /// </summary>
        public void CancelLunge()
        {
            if (_lungeCoroutine != null)
            {
                StopCoroutine(_lungeCoroutine);
                _lungeCoroutine = null;
            }

            RestoreCollision();
            _isLunging = false;

            if (_rb != null)
                _rb.linearVelocity = Vector2.zero;
        }

        // ============================================================
        //  Execution
        // ============================================================

        private void ExecuteLunge(Vector2 direction, LungeProfile profile)
        {
            if (_rb == null || profile.distance <= 0f) return;
            if (direction.sqrMagnitude < 0.001f) return;

            if (_lungeCoroutine != null)
                StopCoroutine(_lungeCoroutine);

            _lungeCoroutine = StartCoroutine(LungeCoroutine(direction.normalized, profile));
        }

        private IEnumerator LungeCoroutine(Vector2 direction, LungeProfile profile)
        {
            _isLunging = true;

            // 적 레이어와의 충돌 무시
            SetCollisionIgnore(true);

            Vector2 startPos = _rb.position;
            Vector2 targetPos = startPos + direction * profile.distance;
            float elapsed = 0f;

            // 메인 전진 (커브 기반 보간)
            while (elapsed < profile.duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / profile.duration);
                float curvedT = _lungeCurve.Evaluate(t);

                Vector2 newPos = Vector2.Lerp(startPos, targetPos, curvedT);
                _rb.MovePosition(newPos);

                yield return new WaitForFixedUpdate();
            }

            // 최종 위치 보정
            _rb.MovePosition(targetPos);

            // 충돌 복원
            SetCollisionIgnore(false);

            // 관성 감속 (전진 방향으로 약간 미끄러짐)
            if (profile.inertiaDuration > 0f)
            {
                float inertiaSpeed = (profile.distance / profile.duration) * 0.3f;
                _rb.linearVelocity = direction * inertiaSpeed;

                float inertiaElapsed = 0f;
                while (inertiaElapsed < profile.inertiaDuration)
                {
                    inertiaElapsed += Time.deltaTime;
                    float decay = 1f - (inertiaElapsed / profile.inertiaDuration);
                    _rb.linearVelocity = direction * inertiaSpeed * decay;
                    yield return null;
                }
            }

            _rb.linearVelocity = Vector2.zero;
            _isLunging = false;
            _lungeCoroutine = null;
        }

        // ============================================================
        //  Collision Control
        // ============================================================

        private void SetCollisionIgnore(bool ignore)
        {
            if (_ignoreLayerDuringLunge.value == 0) return;

            // _ignoreLayerDuringLunge에 포함된 모든 레이어와의 충돌을 제어
            int playerLayer = gameObject.layer;

            for (int i = 0; i < 32; i++)
            {
                if ((_ignoreLayerDuringLunge.value & (1 << i)) != 0)
                {
                    Physics2D.IgnoreLayerCollision(playerLayer, i, ignore);
                }
            }
        }

        private void RestoreCollision()
        {
            SetCollisionIgnore(false);
        }

        // ============================================================
        //  Auto Aim
        // ============================================================

        /// <summary>
        /// 근처에 적이 있으면 그 방향으로 전진 방향을 보정한다.
        /// </summary>
        private Vector2 TryAutoAim(Vector2 originalDirection)
        {
            if (_autoAimRadius <= 0f || _enemyLayer.value == 0)
                return originalDirection;

            // 전진 방향 부채꼴(90도) 내에서 가장 가까운 적 탐색
            Collider2D[] candidates = Physics2D.OverlapCircleAll(
                transform.position,
                _autoAimRadius,
                _enemyLayer);

            if (candidates.Length == 0) return originalDirection;

            float bestDot = -1f;
            Vector2 bestDir = originalDirection;
            Vector2 normalizedInput = originalDirection.normalized;

            foreach (var candidate in candidates)
            {
                if (candidate.gameObject == gameObject) continue;

                Vector2 toEnemy = ((Vector2)candidate.transform.position
                                 - (Vector2)transform.position);

                if (toEnemy.sqrMagnitude < 0.01f) continue;

                Vector2 toEnemyNorm = toEnemy.normalized;
                float dot = Vector2.Dot(normalizedInput, toEnemyNorm);

                // 전방 90도 이내 (dot > 0)에서 가장 정면에 가까운 적
                if (dot > 0.3f && dot > bestDot)
                {
                    bestDot = dot;
                    bestDir = toEnemyNorm;
                }
            }

            return bestDir;
        }

        // ============================================================
        //  Helpers
        // ============================================================

        private LungeProfile GetComboProfile(int step)
        {
            return step switch
            {
                0 => _combo1,
                1 => _combo2,
                2 => _combo3,
                _ => _combo3, // 3단계 이상은 최대 프로파일 사용
            };
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (_autoAimRadius > 0f)
            {
                Gizmos.color = new Color(0f, 1f, 0.5f, 0.2f);
                Gizmos.DrawWireSphere(transform.position, _autoAimRadius);
            }
        }
#endif
    }
}
