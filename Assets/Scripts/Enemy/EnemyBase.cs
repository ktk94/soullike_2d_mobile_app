using System.Collections;
using UnityEngine;
using SoulCraft.Core;

namespace SoulCraft.Enemy
{
    /// <summary>
    /// 모든 적 유닛의 기본 클래스.
    /// HP 관리, 피격/넉백/사망 처리, 상태 열거를 담당한다.
    /// </summary>
    public enum EnemyState
    {
        Idle,
        Patrol,
        Chase,
        Attack,
        Hit,
        Dead
    }

    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class EnemyBase : MonoBehaviour
    {
        // ── Inspector ───────────────────────────────────────
        [Header("Data")]
        [SerializeField] protected EnemyData data;

        [Header("Knockback")]
        [SerializeField] private float knockbackForce = 4f;
        [SerializeField] private float knockbackDuration = 0.15f;

        [Header("Hit Flash")]
        [SerializeField] private Color hitFlashColor = Color.white;
        [SerializeField] private float hitFlashDuration = 0.1f;

        // ── Runtime ─────────────────────────────────────────
        public EnemyData Data => data;
        public int CurrentHp { get; protected set; }
        public int MaxHp => data != null ? data.maxHp : 1;
        public bool IsDead => currentState == EnemyState.Dead;
        public EnemyState CurrentState => currentState;

        protected EnemyState currentState = EnemyState.Idle;
        protected Rigidbody2D rb;
        protected SpriteRenderer spriteRenderer;
        private Color originalColor;
        private Coroutine flashCoroutine;

        // ── Lifecycle ───────────────────────────────────────
        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        protected virtual void Start()
        {
            InitializeEnemy();
        }

        /// <summary>
        /// 데이터 기반 초기화. 풀에서 재활용할 때도 호출 가능.
        /// </summary>
        public virtual void InitializeEnemy()
        {
            if (data == null)
            {
                Debug.LogError($"[EnemyBase] EnemyData가 할당되지 않았습니다: {gameObject.name}");
                return;
            }

            CurrentHp = data.maxHp;
            currentState = EnemyState.Idle;
            originalColor = spriteRenderer.color;

            if (data.sprite != null)
                spriteRenderer.sprite = data.sprite;
        }

        // ── State ───────────────────────────────────────────
        public virtual void SetState(EnemyState newState)
        {
            if (currentState == EnemyState.Dead) return;
            currentState = newState;
        }

        // ── Damage / Death ──────────────────────────────────

        /// <summary>
        /// 데미지를 적용하고 피격 반응을 실행한다.
        /// </summary>
        /// <param name="damage">최종 데미지(방어력 적용 전)</param>
        /// <param name="hitSource">공격 원점(넉백 방향 계산용)</param>
        /// <returns>실제 적용된 데미지</returns>
        public virtual int TakeDamage(int damage, Vector2 hitSource)
        {
            if (currentState == EnemyState.Dead) return 0;

            // 방어력 적용
            int effectiveDamage = Mathf.Max(1, damage - data.defense);
            CurrentHp -= effectiveDamage;

            // 피격 플래시
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(HitFlashCoroutine());

            // 넉백
            ApplyKnockback(hitSource);

            if (CurrentHp <= 0)
            {
                CurrentHp = 0;
                Die();
            }
            else
            {
                SetState(EnemyState.Hit);
            }

            return effectiveDamage;
        }

        /// <summary>
        /// 피격 시 넉백을 적용한다.
        /// </summary>
        protected virtual void ApplyKnockback(Vector2 hitSource)
        {
            Vector2 direction = ((Vector2)transform.position - hitSource).normalized;
            rb.linearVelocity = Vector2.zero;
            rb.AddForce(direction * knockbackForce, ForceMode2D.Impulse);
            StartCoroutine(StopKnockbackAfterDelay());
        }

        private IEnumerator StopKnockbackAfterDelay()
        {
            yield return new WaitForSeconds(knockbackDuration);
            if (currentState != EnemyState.Dead)
                rb.linearVelocity = Vector2.zero;
        }

        /// <summary>
        /// 피격 시 스프라이트 색상을 잠시 변경한다.
        /// </summary>
        private IEnumerator HitFlashCoroutine()
        {
            spriteRenderer.color = hitFlashColor;
            yield return new WaitForSeconds(hitFlashDuration);
            spriteRenderer.color = originalColor;
            flashCoroutine = null;
        }

        /// <summary>
        /// 사망 처리: 이벤트 발행, 보상 트리거, 오브젝트 비활성화.
        /// </summary>
        protected virtual void Die()
        {
            SetState(EnemyState.Dead);
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;

            // 사망 이벤트 발행
            GameEventSystem.Publish(new EnemyDeathEvent
            {
                Enemy = gameObject,
                Position = transform.position,
                EnemyId = data.enemyId
            });

            // 아이템 드롭 트리거
            if (!string.IsNullOrEmpty(data.lootTableId))
            {
                GameEventSystem.Publish(new ItemDropEvent
                {
                    ItemId = data.lootTableId,
                    Position = transform.position,
                    Quantity = 1
                });
            }

            // Exp / Gold 보상 — 별도 이벤트 또는 PlayerManager 직접 호출 가능
            // 여기서는 범용 이벤트로 처리
            GameEventSystem.Publish(new EnemyRewardEvent
            {
                Exp = data.expReward,
                Gold = data.goldReward,
                Position = transform.position
            });

            // 사망 연출 후 비활성화
            StartCoroutine(DeathSequence());
        }

        /// <summary>
        /// 사망 연출 코루틴. 자식 클래스에서 오버라이드하여 연출 추가 가능.
        /// </summary>
        protected virtual IEnumerator DeathSequence()
        {
            // 페이드 아웃
            float elapsed = 0f;
            float fadeDuration = 0.5f;
            Color c = spriteRenderer.color;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                spriteRenderer.color = c;
                yield return null;
            }

            gameObject.SetActive(false);
        }

        // ── Utility ─────────────────────────────────────────

        /// <summary>
        /// 오브젝트 풀에서 재활용될 때 호출. 상태를 완전히 초기화한다.
        /// </summary>
        protected virtual void OnEnable()
        {
            if (data != null)
            {
                rb = rb != null ? rb : GetComponent<Rigidbody2D>();
                spriteRenderer = spriteRenderer != null ? spriteRenderer : GetComponent<SpriteRenderer>();
                rb.simulated = true;
                InitializeEnemy();
            }
        }
    }

    // ── Reward Event (EnemyBase 전용) ─────────────────────
    /// <summary>
    /// 적 처치 시 경험치/골드 보상 이벤트.
    /// </summary>
    public struct EnemyRewardEvent
    {
        public int Exp;
        public int Gold;
        public Vector2 Position;
    }
}
