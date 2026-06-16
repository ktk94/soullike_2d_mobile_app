using System.Collections;
using UnityEngine;

namespace SoulCraft.Enemy
{
    /// <summary>
    /// 상태 머신 기반 적 AI.
    /// EnemyBase와 함께 사용하며, 플레이어 감지/추적/공격을 담당한다.
    /// </summary>
    [RequireComponent(typeof(EnemyBase))]
    public class EnemyAI : MonoBehaviour
    {
        // ── Inspector ───────────────────────────────────────
        [Header("Patrol")]
        [SerializeField] private float patrolMoveTime = 2f;
        [SerializeField] private float idleWaitMin = 1f;
        [SerializeField] private float idleWaitMax = 3f;
        [SerializeField] private float idleToPatrolChance = 0.5f;

        [Header("Attack")]
        [SerializeField] private float attackCooldown = 1.2f;
        [SerializeField] private int attackDamage = -1; // -1이면 data.attack 사용

        [Header("Hit Stun")]
        [SerializeField] private float hitStunDuration = 0.3f;

        [Header("Chase")]
        [SerializeField] private float chaseLoseRange = 10f;

        [Header("Player Layer")]
        [SerializeField] private LayerMask playerLayer;
        [SerializeField] private string playerTag = "Player";

        // ── Runtime ─────────────────────────────────────────
        private EnemyBase enemyBase;
        private EnemyData data;
        private Rigidbody2D rb;
        private SpriteRenderer spriteRenderer;

        private Transform playerTransform;
        private Vector2 patrolDirection;
        private float stateTimer;
        private float attackTimer;
        private bool isProcessingState;

        // ── Lifecycle ───────────────────────────────────────
        private void Awake()
        {
            enemyBase = GetComponent<EnemyBase>();
            rb = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Start()
        {
            data = enemyBase.Data;
            if (data == null)
            {
                Debug.LogError($"[EnemyAI] EnemyData가 없습니다: {gameObject.name}");
                enabled = false;
                return;
            }
            attackTimer = 0f;
            StartCoroutine(StateMachineCoroutine());
        }

        private void OnEnable()
        {
            attackTimer = 0f;
            isProcessingState = false;
        }

        // ── State Machine ───────────────────────────────────

        /// <summary>
        /// 메인 상태 머신 루프. 각 상태에서 적절한 행동을 수행한다.
        /// </summary>
        private IEnumerator StateMachineCoroutine()
        {
            // 데이터 로드 대기
            yield return null;

            while (!enemyBase.IsDead)
            {
                switch (enemyBase.CurrentState)
                {
                    case EnemyState.Idle:
                        yield return HandleIdle();
                        break;
                    case EnemyState.Patrol:
                        yield return HandlePatrol();
                        break;
                    case EnemyState.Chase:
                        yield return HandleChase();
                        break;
                    case EnemyState.Attack:
                        yield return HandleAttack();
                        break;
                    case EnemyState.Hit:
                        yield return HandleHit();
                        break;
                    default:
                        yield return null;
                        break;
                }
            }
        }

        // ── Idle ────────────────────────────────────────────
        private IEnumerator HandleIdle()
        {
            rb.linearVelocity = Vector2.zero;
            float waitTime = Random.Range(idleWaitMin, idleWaitMax);
            float elapsed = 0f;

            while (elapsed < waitTime)
            {
                if (enemyBase.CurrentState != EnemyState.Idle) yield break;

                if (DetectPlayer())
                {
                    enemyBase.SetState(EnemyState.Chase);
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // 일정 확률로 Patrol 전환
            if (Random.value < idleToPatrolChance)
                enemyBase.SetState(EnemyState.Patrol);

            yield return null;
        }

        // ── Patrol ──────────────────────────────────────────
        private IEnumerator HandlePatrol()
        {
            // 랜덤 방향 결정
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            patrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;

            float elapsed = 0f;

            while (elapsed < patrolMoveTime)
            {
                if (enemyBase.CurrentState != EnemyState.Patrol) yield break;

                if (DetectPlayer())
                {
                    enemyBase.SetState(EnemyState.Chase);
                    yield break;
                }

                rb.linearVelocity = patrolDirection * (data.speed * 0.5f);
                FlipSprite(patrolDirection.x);

                elapsed += Time.deltaTime;
                yield return null;
            }

            rb.linearVelocity = Vector2.zero;
            enemyBase.SetState(EnemyState.Idle);
        }

        // ── Chase ───────────────────────────────────────────
        private IEnumerator HandleChase()
        {
            while (enemyBase.CurrentState == EnemyState.Chase)
            {
                if (playerTransform == null || !playerTransform.gameObject.activeInHierarchy)
                {
                    enemyBase.SetState(EnemyState.Idle);
                    yield break;
                }

                Vector2 toPlayer = (Vector2)playerTransform.position - (Vector2)transform.position;
                float distance = toPlayer.magnitude;

                // 공격 범위 진입
                if (distance <= data.attackRange && attackTimer <= 0f)
                {
                    enemyBase.SetState(EnemyState.Attack);
                    yield break;
                }

                // 추적 범위 이탈
                if (distance > chaseLoseRange)
                {
                    playerTransform = null;
                    enemyBase.SetState(EnemyState.Idle);
                    yield break;
                }

                // 플레이어 방향으로 이동
                Vector2 dir = toPlayer.normalized;
                rb.linearVelocity = dir * data.speed;
                FlipSprite(dir.x);

                yield return null;
            }
        }

        // ── Attack ──────────────────────────────────────────
        private IEnumerator HandleAttack()
        {
            rb.linearVelocity = Vector2.zero;

            // 공격 실행
            PerformAttack();

            // 공격 쿨다운 대기
            attackTimer = attackCooldown;
            float elapsed = 0f;
            while (elapsed < attackCooldown)
            {
                if (enemyBase.CurrentState != EnemyState.Attack) yield break;
                elapsed += Time.deltaTime;
                attackTimer = attackCooldown - elapsed;
                yield return null;
            }

            attackTimer = 0f;

            // 플레이어가 아직 범위 안이면 다시 공격, 아니면 Chase
            if (playerTransform != null)
            {
                float dist = Vector2.Distance(transform.position, playerTransform.position);
                if (dist <= data.attackRange)
                    enemyBase.SetState(EnemyState.Attack);
                else
                    enemyBase.SetState(EnemyState.Chase);
            }
            else
            {
                enemyBase.SetState(EnemyState.Idle);
            }
        }

        /// <summary>
        /// 실제 공격 판정. 범위 내 플레이어에게 데미지를 전달한다.
        /// </summary>
        private void PerformAttack()
        {
            int dmg = attackDamage > 0 ? attackDamage : data.attack;

            Collider2D[] hits = Physics2D.OverlapCircleAll(
                transform.position, data.attackRange, playerLayer);

            foreach (var hit in hits)
            {
                if (hit.CompareTag(playerTag))
                {
                    // DamageEvent를 통해 플레이어에게 데미지 전달
                    SoulCraft.Core.GameEventSystem.Publish(new SoulCraft.Core.DamageEvent
                    {
                        Attacker = gameObject,
                        Target = hit.gameObject,
                        Damage = dmg,
                        IsCritical = false,
                        Type = SoulCraft.Core.DamageType.Physical,
                        HitPoint = transform.position
                    });
                }
            }
        }

        // ── Hit ─────────────────────────────────────────────
        private IEnumerator HandleHit()
        {
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(hitStunDuration);

            // 경직 후 플레이어를 알고 있으면 Chase, 아니면 Idle
            if (playerTransform != null && playerTransform.gameObject.activeInHierarchy)
                enemyBase.SetState(EnemyState.Chase);
            else
                enemyBase.SetState(EnemyState.Idle);
        }

        // ── Detection ───────────────────────────────────────

        /// <summary>
        /// Physics2D.OverlapCircle로 플레이어를 감지한다.
        /// </summary>
        private bool DetectPlayer()
        {
            Collider2D col = Physics2D.OverlapCircle(
                transform.position, data.detectionRange, playerLayer);

            if (col != null && col.CompareTag(playerTag))
            {
                playerTransform = col.transform;
                return true;
            }

            return false;
        }

        // ── Utility ─────────────────────────────────────────

        private void FlipSprite(float xDirection)
        {
            if (Mathf.Abs(xDirection) < 0.01f) return;
            spriteRenderer.flipX = xDirection < 0f;
        }

        private void OnDrawGizmosSelected()
        {
            if (data == null) return;

            // 감지 범위 (노란색)
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, data.detectionRange);

            // 공격 범위 (빨간색)
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, data.attackRange);
        }
    }
}
