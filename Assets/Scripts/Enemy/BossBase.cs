using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulCraft.Core;

namespace SoulCraft.Enemy
{
    /// <summary>
    /// 보스 패턴 하나를 정의하는 클래스.
    /// </summary>
    [Serializable]
    public class BossPattern
    {
        public string patternName;
        /// <summary>패턴 실행 코루틴을 반환하는 델리게이트.</summary>
        [NonSerialized] public Func<BossBase, IEnumerator> Execute;
        [Tooltip("가중 확률 (높을수록 자주 선택)")]
        public float weight = 1f;
        [Tooltip("재사용 대기 시간")]
        public float cooldown = 3f;
        /// <summary>런타임 쿨다운 타이머.</summary>
        [NonSerialized] public float cooldownTimer;
    }

    /// <summary>
    /// 보스 페이즈 하나를 정의하는 클래스.
    /// </summary>
    [Serializable]
    public class BossPhase
    {
        [Tooltip("이 페이즈가 시작되는 HP 비율 (1.0 = 100%)")]
        [Range(0f, 1f)]
        public float hpThreshold = 1f;
        public List<BossPattern> patterns = new();
    }

    /// <summary>
    /// EnemyBase를 상속하는 보스 기본 클래스.
    /// 다중 페이즈, 패턴 가중 선택, 분노(Enrage) 메커닉을 지원한다.
    /// </summary>
    public class BossBase : EnemyBase
    {
        // ── Inspector ───────────────────────────────────────
        [Header("Boss Phases")]
        [SerializeField] private List<BossPhase> phases = new();

        [Header("Phase Transition")]
        [SerializeField] private float phaseTransitionInvincibleTime = 1.5f;
        [SerializeField] private Color phaseTransitionFlashColor = Color.yellow;

        [Header("Enrage")]
        [SerializeField] private float enrageAttackMultiplier = 1.5f;
        [SerializeField] private float enrageSpeedMultiplier = 1.3f;
        [SerializeField] private Color enrageColor = new Color(1f, 0.3f, 0.3f, 1f);

        [Header("Pattern")]
        [SerializeField] private float patternInterval = 1f;

        // ── Runtime ─────────────────────────────────────────
        public int CurrentPhaseIndex { get; private set; }
        public bool IsInvincible { get; private set; }
        public bool IsEnraged { get; private set; }
        public float EnrageAttackMultiplier => IsEnraged ? enrageAttackMultiplier : 1f;
        public float EnrageSpeedMultiplier => IsEnraged ? enrageSpeedMultiplier : 1f;

        private Coroutine patternCoroutine;
        private bool isExecutingPattern;

        // ── Lifecycle ───────────────────────────────────────

        public override void InitializeEnemy()
        {
            base.InitializeEnemy();
            CurrentPhaseIndex = 0;
            IsInvincible = false;
            IsEnraged = false;
            isExecutingPattern = false;

            // 페이즈를 HP 비율 내림차순으로 정렬 (100% → 70% → 30%)
            phases.Sort((a, b) => b.hpThreshold.CompareTo(a.hpThreshold));

            // 모든 패턴 쿨다운 초기화
            foreach (var phase in phases)
            {
                foreach (var pattern in phase.patterns)
                    pattern.cooldownTimer = 0f;
            }
        }

        protected override void Start()
        {
            base.Start();
            RegisterPatterns();
            StartPatternLoop();
        }

        /// <summary>
        /// 자식 클래스에서 오버라이드하여 각 BossPattern.Execute를 할당한다.
        /// </summary>
        protected virtual void RegisterPatterns() { }

        // ── Damage Override ─────────────────────────────────

        public override int TakeDamage(int damage, Vector2 hitSource)
        {
            if (IsInvincible) return 0;

            int applied = base.TakeDamage(damage, hitSource);

            // 페이즈 전환 체크
            CheckPhaseTransition();

            return applied;
        }

        // ── Phase System ────────────────────────────────────

        /// <summary>
        /// 현재 HP 비율에 따라 페이즈 전환 여부를 확인한다.
        /// </summary>
        private void CheckPhaseTransition()
        {
            if (currentState == EnemyState.Dead) return;

            float hpRatio = (float)CurrentHp / MaxHp;

            for (int i = CurrentPhaseIndex + 1; i < phases.Count; i++)
            {
                if (hpRatio <= phases[i].hpThreshold)
                {
                    TransitionToPhase(i);
                    break;
                }
            }
        }

        /// <summary>
        /// 지정된 페이즈로 전환한다. 무적 + 연출 + 이벤트 발행.
        /// </summary>
        private void TransitionToPhase(int newPhaseIndex)
        {
            if (patternCoroutine != null)
                StopCoroutine(patternCoroutine);

            isExecutingPattern = false;
            CurrentPhaseIndex = newPhaseIndex;

            StartCoroutine(PhaseTransitionSequence(newPhaseIndex));
        }

        private IEnumerator PhaseTransitionSequence(int newPhaseIndex)
        {
            // 무적 상태
            IsInvincible = true;
            rb.linearVelocity = Vector2.zero;

            // 페이즈 전환 이벤트 발행
            GameEventSystem.Publish(new BossPhaseChangeEvent
            {
                NewPhase = newPhaseIndex,
                HpRatio = (float)CurrentHp / MaxHp
            });

            // 전환 연출: 색상 점멸
            yield return PhaseTransitionEffect();

            // 마지막 페이즈면 분노 발동
            if (newPhaseIndex == phases.Count - 1)
                ActivateEnrage();

            IsInvincible = false;

            // 패턴 루프 재개
            StartPatternLoop();
        }

        /// <summary>
        /// 페이즈 전환 시 시각 연출 (색상 점멸).
        /// </summary>
        private IEnumerator PhaseTransitionEffect()
        {
            float elapsed = 0f;
            float flashInterval = 0.15f;
            bool toggle = false;
            Color original = spriteRenderer.color;

            while (elapsed < phaseTransitionInvincibleTime)
            {
                spriteRenderer.color = toggle ? phaseTransitionFlashColor : original;
                toggle = !toggle;
                yield return new WaitForSeconds(flashInterval);
                elapsed += flashInterval;
            }

            spriteRenderer.color = original;
        }

        // ── Enrage ──────────────────────────────────────────

        /// <summary>
        /// 마지막 페이즈 진입 시 분노 상태를 활성화한다.
        /// </summary>
        private void ActivateEnrage()
        {
            IsEnraged = true;
            spriteRenderer.color = enrageColor;
        }

        // ── Pattern Loop ────────────────────────────────────

        private void StartPatternLoop()
        {
            if (patternCoroutine != null)
                StopCoroutine(patternCoroutine);

            patternCoroutine = StartCoroutine(PatternLoopCoroutine());
        }

        private IEnumerator PatternLoopCoroutine()
        {
            while (!IsDead)
            {
                if (IsInvincible || currentState == EnemyState.Dead)
                {
                    yield return null;
                    continue;
                }

                // 현재 페이즈의 패턴 중 사용 가능한 것을 가중 확률로 선택
                BossPattern selected = SelectPattern();
                if (selected != null)
                {
                    isExecutingPattern = true;
                    if (selected.Execute != null)
                        yield return selected.Execute(this);
                    selected.cooldownTimer = selected.cooldown;
                    isExecutingPattern = false;
                }

                yield return new WaitForSeconds(patternInterval);

                // 쿨다운 갱신
                UpdateCooldowns(patternInterval);
            }
        }

        /// <summary>
        /// 현재 페이즈 패턴 중 쿨다운이 끝난 것을 가중 확률로 선택한다.
        /// </summary>
        private BossPattern SelectPattern()
        {
            if (CurrentPhaseIndex >= phases.Count) return null;

            var available = new List<BossPattern>();
            float totalWeight = 0f;

            foreach (var p in phases[CurrentPhaseIndex].patterns)
            {
                if (p.cooldownTimer <= 0f && p.Execute != null)
                {
                    available.Add(p);
                    totalWeight += p.weight;
                }
            }

            if (available.Count == 0) return null;

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float cumulative = 0f;

            foreach (var p in available)
            {
                cumulative += p.weight;
                if (roll <= cumulative)
                    return p;
            }

            return available[available.Count - 1];
        }

        private void UpdateCooldowns(float delta)
        {
            foreach (var phase in phases)
            {
                foreach (var p in phase.patterns)
                {
                    if (p.cooldownTimer > 0f)
                        p.cooldownTimer -= delta;
                }
            }
        }

        // ── Death Override ──────────────────────────────────

        protected override void Die()
        {
            if (patternCoroutine != null)
                StopCoroutine(patternCoroutine);

            isExecutingPattern = false;
            IsInvincible = false;
            base.Die();
        }

        // ── Public Helpers (패턴에서 사용) ────────────────────

        /// <summary>분노 배율 적용된 최종 공격력.</summary>
        public int GetBossAttack()
        {
            return Mathf.RoundToInt(Data.attack * EnrageAttackMultiplier);
        }

        /// <summary>분노 배율 적용된 최종 이동속도.</summary>
        public float GetBossSpeed()
        {
            return Data.speed * EnrageSpeedMultiplier;
        }

        /// <summary>
        /// 패턴 전용: 잠시 무적 상태를 설정한다.
        /// </summary>
        public void SetTemporaryInvincible(bool value)
        {
            IsInvincible = value;
        }
    }
}
