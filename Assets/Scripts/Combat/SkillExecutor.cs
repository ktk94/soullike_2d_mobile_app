using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulCraft.Core;
using SoulCraft.Player;

namespace SoulCraft.Combat
{
    /// <summary>
    /// 스킬 타입별 실행 로직을 처리하는 시스템.
    /// SkillManager.UseSkill() 성공 후 호출되어 실제 판정/투사체/버프를 수행한다.
    /// </summary>
    public class SkillExecutor : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────
        public static SkillExecutor Instance { get; private set; }

        // ── Inspector ────────────────────────────────────────
        [Header("Melee Settings")]
        [Tooltip("근접 스킬 부채꼴 판정 각도 (도)")]
        [SerializeField] private float _meleeArcAngle = 120f;

        [Header("Projectile Settings")]
        [Tooltip("기본 투사체 프리팹 (SkillData에 개별 프리팹이 없을 때 사용)")]
        [SerializeField] private GameObject _defaultProjectilePrefab;
        [SerializeField] private float _defaultProjectileSpeed = 12f;

        [Header("AoE Settings")]
        [Tooltip("기본 AoE 이펙트 프리팹")]
        [SerializeField] private GameObject _defaultAoeEffectPrefab;

        [Header("Buff Settings")]
        [Tooltip("버프 적용 시 표시할 기본 파티클")]
        [SerializeField] private GameObject _buffActivateEffectPrefab;

        [Header("References")]
        [SerializeField] private AttackLunge _attackLunge;
        [SerializeField] private LayerMask _enemyLayer;
        [SerializeField] private LayerMask _wallLayer;

        // ── Runtime ──────────────────────────────────────────
        private PlayerStats _playerStats;
        private BuffSystem _buffSystem;
        private Transform _playerTransform;

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

        void Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _playerStats = player.GetComponent<PlayerStats>();
                _playerTransform = player.transform;
                if (_attackLunge == null)
                    _attackLunge = player.GetComponent<AttackLunge>();
            }

            _buffSystem = GetComponent<BuffSystem>();
            if (_buffSystem == null)
                _buffSystem = gameObject.AddComponent<BuffSystem>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ============================================================
        //  Public API
        // ============================================================

        /// <summary>
        /// 스킬을 실행한다. 스킬 타입에 따라 적절한 로직을 분기한다.
        /// </summary>
        /// <param name="skill">실행할 스킬 데이터</param>
        /// <param name="playerPos">플레이어 현재 위치</param>
        /// <param name="aimDir">조준 방향 (정규화)</param>
        public void ExecuteSkill(SkillData skill, Vector2 playerPos, Vector2 aimDir)
        {
            if (skill == null) return;

            Vector2 direction = aimDir.sqrMagnitude > 0.001f ? aimDir.normalized : Vector2.right;

            switch (skill.skillType)
            {
                case SkillType.Melee:
                    ExecuteMelee(skill, playerPos, direction);
                    break;
                case SkillType.Ranged:
                    ExecuteRanged(skill, playerPos, direction);
                    break;
                case SkillType.AoE:
                    ExecuteAoE(skill, playerPos, direction);
                    break;
                case SkillType.Buff:
                    ExecuteBuff(skill);
                    break;
            }
        }

        // ============================================================
        //  Melee Execution
        // ============================================================

        private void ExecuteMelee(SkillData skill, Vector2 origin, Vector2 direction)
        {
            // AttackLunge 연동
            if (_attackLunge != null)
            {
                float lungeDistance = skill.range * 0.4f;
                _attackLunge.PerformSkillLunge(direction, lungeDistance);
            }

            // 특수 스킬 분기
            string skillId = skill.skillId;

            if (skillId == "melee_chain_slash")
            {
                StartCoroutine(ChainSlashCoroutine(skill, origin, direction));
                return;
            }

            if (skillId == "melee_counter")
            {
                StartCoroutine(CounterCoroutine(skill, origin, direction));
                return;
            }

            // 부채꼴 또는 360도 판정
            float arcAngle = HasComboTag(skill, "Spin") ? 360f : _meleeArcAngle;
            List<Collider2D> hitTargets = DetectEnemiesInArc(origin, direction, skill.range, arcAngle);

            foreach (var target in hitTargets)
            {
                int damage = CalculateSkillDamage(skill, target.gameObject, out bool isCritical);

                // 특수 효과: 처형
                if (skillId == "melee_execute")
                {
                    damage = ApplyExecuteBonus(damage, target.gameObject);
                }

                // 특수 효과: 올려베기 — 에어본
                if (skillId == "melee_uppercut")
                {
                    ApplyAirborne(target.gameObject, 0.8f);
                }

                // 특수 효과: 양손 내려찍기 — 경직 게이지 추가
                float staggerMultiplier = (skillId == "melee_overhead_smash") ? 2f : 1f;

                // 대지 진동 — 경직 3배 (AoE지만 분류 참조용)
                PublishDamageEvent(skill, target.gameObject, origin, damage, isCritical);
                ApplyStaggerBonus(target.gameObject, damage, staggerMultiplier);
            }

            SpawnSkillEffect(skill, origin, direction);
        }

        /// <summary>
        /// 연환 참: 5연속 빠른 베기 코루틴.
        /// </summary>
        private IEnumerator ChainSlashCoroutine(SkillData skill, Vector2 origin, Vector2 direction)
        {
            const int hitCount = 5;
            const float interval = 0.12f;

            for (int i = 0; i < hitCount; i++)
            {
                Vector2 currentPos = _playerTransform != null
                    ? (Vector2)_playerTransform.position
                    : origin;

                List<Collider2D> hits = DetectEnemiesInArc(currentPos, direction, skill.range, _meleeArcAngle);

                foreach (var target in hits)
                {
                    int damage = CalculateSkillDamage(skill, target.gameObject, out bool isCritical);

                    // 마지막 타격: 넉백 2배
                    bool isLastHit = (i == hitCount - 1);
                    PublishDamageEvent(skill, target.gameObject, currentPos, damage, isCritical);

                    if (isLastHit)
                    {
                        ApplyKnockbackToTarget(target.gameObject, direction, 2f);
                    }
                }

                if (i < hitCount - 1)
                    yield return new WaitForSeconds(interval);
            }
        }

        /// <summary>
        /// 카운터: 짧은 파리 판정 후 반격.
        /// </summary>
        private IEnumerator CounterCoroutine(SkillData skill, Vector2 origin, Vector2 direction)
        {
            float parryWindow = skill.duration > 0f ? skill.duration : 0.4f;
            bool parrySuccess = false;

            // 파리 판정 대기
            float elapsed = 0f;
            while (elapsed < parryWindow)
            {
                // 플레이어 전방 부채꼴 내에 적 투사체나 공격 판정이 있는지 체크
                Vector2 currentPos = _playerTransform != null
                    ? (Vector2)_playerTransform.position
                    : origin;

                Collider2D[] nearby = Physics2D.OverlapCircleAll(currentPos, skill.range, _enemyLayer);
                foreach (var col in nearby)
                {
                    // 공격 중인 적 감지 (Hit 상태가 아닌 Attack 상태의 적)
                    var enemyBase = col.GetComponent<SoulCraft.Enemy.EnemyBase>();
                    if (enemyBase != null && enemyBase.CurrentState == SoulCraft.Enemy.EnemyState.Attack)
                    {
                        Vector2 toEnemy = ((Vector2)col.transform.position - currentPos).normalized;
                        float dot = Vector2.Dot(direction, toEnemy);
                        if (dot > 0.3f)
                        {
                            parrySuccess = true;
                            break;
                        }
                    }
                }

                if (parrySuccess) break;
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (parrySuccess)
            {
                // 반격: 크리티컬 확정
                Vector2 currentPos = _playerTransform != null
                    ? (Vector2)_playerTransform.position
                    : origin;

                List<Collider2D> hits = DetectEnemiesInArc(currentPos, direction, skill.range * 1.2f, _meleeArcAngle);

                foreach (var target in hits)
                {
                    int damage = CalculateSkillDamage(skill, target.gameObject, out _);
                    // 크리티컬 확정 적용
                    float critDamage = _playerStats != null ? _playerStats.CritDamage : 1.5f;
                    damage = Mathf.RoundToInt(damage * critDamage);

                    GameEventSystem.Publish(new DamageEvent
                    {
                        Attacker = _playerTransform != null ? _playerTransform.gameObject : null,
                        Target = target.gameObject,
                        Damage = damage,
                        IsCritical = true,
                        Type = ConvertElement(skill.element),
                        HitPoint = target.ClosestPoint(currentPos)
                    });
                }

                // 카운터 성공 연출
                if (ImpactSystem.Instance != null)
                    ImpactSystem.Instance.TriggerHitStop(0.1f);
                if (CameraController.Instance != null)
                    CameraController.Instance.Shake(0.4f, 0.15f);
            }
        }

        // ============================================================
        //  Ranged Execution
        // ============================================================

        private void ExecuteRanged(SkillData skill, Vector2 origin, Vector2 direction)
        {
            string skillId = skill.skillId;

            // 성스러운 빛: 즉발 히트스캔
            if (skillId == "ranged_holy_light")
            {
                ExecuteHitscan(skill, origin, direction);
                return;
            }

            // 투사체 생성
            GameObject prefab = skill.effectPrefab != null ? skill.effectPrefab : _defaultProjectilePrefab;
            if (prefab == null) return;

            GameObject projObj = Instantiate(prefab, origin, Quaternion.identity);
            Projectile proj = projObj.GetComponent<Projectile>();

            if (proj == null)
            {
                Destroy(projObj);
                return;
            }

            int baseDamage = _playerStats != null ? _playerStats.Attack : 10;
            DamageType element = ConvertElement(skill.element);

            proj.Initialize(direction, _playerTransform != null ? _playerTransform.gameObject : null,
                baseDamage, skill.damageMultiplier, element);
            proj.SetSpeed(_defaultProjectileSpeed);
            proj.SetLifetime(skill.range / _defaultProjectileSpeed + 1f);

            // 스킬별 투사체 특성
            if (skillId == "ranged_ice_arrow")
            {
                proj.SetPiercing(true, 3);
                // 빙결 감속은 OnHit에서 BuffSystem을 통해 적용
            }

            if (skillId == "ranged_wind_blade")
            {
                // 부메랑 투사체: 별도 코루틴으로 복귀 처리
                StartCoroutine(BoomerangCoroutine(projObj, origin, direction, skill));
            }

            if (skillId == "ranged_dark_orb")
            {
                proj.SetSpeed(4f); // 느린 투사체
                StartCoroutine(DarkOrbTrailCoroutine(projObj, skill));
            }

            if (skillId == "ranged_lightning_spear")
            {
                proj.SetSpeed(18f); // 빠른 투사체
                // 연쇄 번개는 적중 이벤트 구독으로 처리
            }

            // 히트 이펙트
            if (skill.hitEffectPrefab != null)
                proj.SetHitEffect(skill.hitEffectPrefab);

            // 화염구: 착탄 시 폭발은 Projectile OnDestroy에서 AoE 트리거
            if (skillId == "ranged_fireball")
            {
                // Projectile에 폭발 컴포넌트 부착
                var explosion = projObj.AddComponent<ProjectileExplosion>();
                explosion.Initialize(skill.aoeRadius, baseDamage, skill.damageMultiplier,
                    element, _enemyLayer, _playerTransform != null ? _playerTransform.gameObject : null);
            }
        }

        /// <summary>
        /// 히트스캔 방식 (성스러운 빛).
        /// </summary>
        private void ExecuteHitscan(SkillData skill, Vector2 origin, Vector2 direction)
        {
            RaycastHit2D hit = Physics2D.Raycast(origin, direction, skill.range, _enemyLayer);
            if (hit.collider != null)
            {
                int damage = CalculateSkillDamage(skill, hit.collider.gameObject, out bool isCritical);
                PublishDamageEvent(skill, hit.collider.gameObject, origin, damage, isCritical);
            }

            SpawnSkillEffect(skill, origin, direction);
        }

        /// <summary>
        /// 부메랑 투사체 (바람 칼날).
        /// </summary>
        private IEnumerator BoomerangCoroutine(GameObject projectile, Vector2 origin, Vector2 direction, SkillData skill)
        {
            if (projectile == null) yield break;

            float travelTime = skill.range / _defaultProjectileSpeed;
            yield return new WaitForSeconds(travelTime);

            if (projectile == null) yield break;

            // 방향 반전 — 플레이어 위치로 복귀
            Vector2 currentPos = projectile.transform.position;
            Vector2 returnTarget = _playerTransform != null
                ? (Vector2)_playerTransform.position
                : origin;
            Vector2 returnDir = (returnTarget - currentPos).normalized;

            var proj = projectile.GetComponent<Projectile>();
            if (proj != null)
            {
                int baseDamage = _playerStats != null ? _playerStats.Attack : 10;
                proj.Initialize(returnDir, _playerTransform != null ? _playerTransform.gameObject : null,
                    baseDamage, skill.damageMultiplier, ConvertElement(skill.element));
            }

            // 복귀 후 자동 파괴
            yield return new WaitForSeconds(travelTime + 0.5f);
            if (projectile != null)
                Destroy(projectile);
        }

        /// <summary>
        /// 어둠 구체 이동 경로 장판 생성.
        /// </summary>
        private IEnumerator DarkOrbTrailCoroutine(GameObject projectile, SkillData skill)
        {
            float interval = 0.3f;
            float elapsed = 0f;
            float maxDuration = skill.duration > 0f ? skill.duration : 4f;
            int baseDamage = _playerStats != null ? _playerStats.Attack : 10;

            while (elapsed < maxDuration && projectile != null)
            {
                Vector2 pos = projectile.transform.position;

                // 장판 생성
                StartCoroutine(DamageZoneCoroutine(
                    pos, skill.aoeRadius, 4f, 0.5f,
                    Mathf.RoundToInt(baseDamage * 0.15f),
                    ConvertElement(skill.element)));

                elapsed += interval;
                yield return new WaitForSeconds(interval);
            }
        }

        // ============================================================
        //  AoE Execution
        // ============================================================

        private void ExecuteAoE(SkillData skill, Vector2 origin, Vector2 direction)
        {
            string skillId = skill.skillId;

            // 지정 위치 계산: range > 0 이면 전방 range 위치, 아니면 자기 위치
            Vector2 center = skill.range > 0f
                ? origin + direction * skill.range * 0.5f
                : origin;

            // 어둠 폭발: 흡입 후 폭발
            if (skillId == "aoe_dark_implosion")
            {
                StartCoroutine(DarkImplosionCoroutine(skill, center));
                return;
            }

            // 번개 폭풍: 랜덤 낙뢰
            if (skillId == "aoe_lightning_storm")
            {
                StartCoroutine(LightningStormCoroutine(skill, center));
                return;
            }

            // 치유의 원: 지속 회복
            if (skillId == "aoe_healing_circle")
            {
                StartCoroutine(HealingCircleCoroutine(skill, center));
                SpawnSkillEffect(skill, center, direction);
                return;
            }

            // 즉발 범위 공격
            Collider2D[] targets = Physics2D.OverlapCircleAll(center, skill.aoeRadius, _enemyLayer);

            foreach (var target in targets)
            {
                int damage = CalculateSkillDamage(skill, target.gameObject, out bool isCritical);
                PublishDamageEvent(skill, target.gameObject, center, damage, isCritical);

                // 대지 진동: 경직 3배, 이속 감소
                if (skillId == "aoe_earth_tremor")
                {
                    ApplyStaggerBonus(target.gameObject, damage, 3f);
                    ApplySlowDebuff(target.gameObject, 0.3f, 2f);
                }
            }

            // 지속 효과
            if (skillId == "aoe_flame_burst")
            {
                // 화상 도트
                foreach (var target in targets)
                {
                    ApplyBurnDoT(target.gameObject, 4f, 0.25f);
                }
            }

            if (skillId == "aoe_ice_field")
            {
                // 빙결 장판: 지속 데미지 + 감속
                int baseDamage = _playerStats != null ? _playerStats.Attack : 10;
                StartCoroutine(DamageZoneCoroutine(
                    center, skill.aoeRadius, skill.duration, 0.5f,
                    Mathf.RoundToInt(baseDamage * 0.4f),
                    DamageType.Ice));

                StartCoroutine(SlowZoneCoroutine(center, skill.aoeRadius, skill.duration, 0.5f));
            }

            SpawnSkillEffect(skill, center, direction);
        }

        /// <summary>
        /// 어둠 폭발: 흡입 후 폭발.
        /// </summary>
        private IEnumerator DarkImplosionCoroutine(SkillData skill, Vector2 center)
        {
            float pullDuration = 0.5f;
            float pullForce = 8f;

            // 흡입 페이즈
            float elapsed = 0f;
            while (elapsed < pullDuration)
            {
                Collider2D[] inRange = Physics2D.OverlapCircleAll(center, skill.aoeRadius, _enemyLayer);
                foreach (var col in inRange)
                {
                    var rb = col.GetComponent<Rigidbody2D>();
                    if (rb != null)
                    {
                        Vector2 pullDir = (center - (Vector2)col.transform.position).normalized;
                        rb.AddForce(pullDir * pullForce, ForceMode2D.Force);
                    }
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // 폭발 페이즈
            Collider2D[] targets = Physics2D.OverlapCircleAll(center, skill.aoeRadius, _enemyLayer);
            foreach (var target in targets)
            {
                int damage = CalculateSkillDamage(skill, target.gameObject, out bool isCritical);
                PublishDamageEvent(skill, target.gameObject, center, damage, isCritical);
            }

            // 폭발 이펙트
            SpawnSkillEffect(skill, center, Vector2.up);

            if (CameraController.Instance != null)
                CameraController.Instance.Shake(0.5f, 0.2f);
        }

        /// <summary>
        /// 번개 폭풍: 0.5초 간격 낙뢰 3회.
        /// </summary>
        private IEnumerator LightningStormCoroutine(SkillData skill, Vector2 center)
        {
            const int strikes = 3;
            const float interval = 0.5f;
            float strikeRadius = 1.5f;

            for (int i = 0; i < strikes; i++)
            {
                // 범위 내 랜덤 위치
                Vector2 strikePos = center + Random.insideUnitCircle * skill.aoeRadius * 0.8f;

                Collider2D[] targets = Physics2D.OverlapCircleAll(strikePos, strikeRadius, _enemyLayer);
                foreach (var target in targets)
                {
                    int damage = CalculateSkillDamage(skill, target.gameObject, out bool isCritical);
                    PublishDamageEvent(skill, target.gameObject, strikePos, damage, isCritical);
                }

                // 낙뢰 이펙트
                SpawnSkillEffect(skill, strikePos, Vector2.up);

                if (CameraController.Instance != null)
                    CameraController.Instance.Shake(0.3f, 0.1f);

                yield return new WaitForSeconds(interval);
            }
        }

        /// <summary>
        /// 치유의 원: 6초간 매 0.5초마다 최대HP의 3% 회복.
        /// </summary>
        private IEnumerator HealingCircleCoroutine(SkillData skill, Vector2 center)
        {
            float duration = skill.duration > 0f ? skill.duration : 6f;
            float interval = 0.5f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (_playerStats != null && _playerTransform != null)
                {
                    float dist = Vector2.Distance(center, _playerTransform.position);
                    if (dist <= skill.aoeRadius)
                    {
                        int healAmount = Mathf.Max(1, Mathf.RoundToInt(_playerStats.MaxHp * 0.03f));
                        _playerStats.Heal(healAmount);
                    }
                }

                elapsed += interval;
                yield return new WaitForSeconds(interval);
            }
        }

        // ============================================================
        //  Buff Execution
        // ============================================================

        private void ExecuteBuff(SkillData skill)
        {
            if (_buffSystem == null || _playerStats == null) return;

            string skillId = skill.skillId;
            DamageType element = ConvertElement(skill.element);

            Buff buff = new Buff
            {
                Id = skillId,
                Name = skill.skillName,
                Duration = skill.duration,
                IsDebuff = false,
                Stackable = false,
                Icon = skill.icon
            };

            switch (skillId)
            {
                case "buff_berserker_rage":
                    buff.StatModifiers = new StatModifier[]
                    {
                        new StatModifier { StatType = StatType.Attack, ModType = ModifierType.Percent, Value = 0.5f },
                        new StatModifier { StatType = StatType.Defense, ModType = ModifierType.Percent, Value = -0.3f }
                    };
                    break;

                case "buff_iron_skin":
                    buff.StatModifiers = new StatModifier[]
                    {
                        new StatModifier { StatType = StatType.Defense, ModType = ModifierType.Percent, Value = 1.0f }
                    };
                    break;

                case "buff_haste":
                    buff.StatModifiers = new StatModifier[]
                    {
                        new StatModifier { StatType = StatType.Speed, ModType = ModifierType.Percent, Value = 0.3f },
                        new StatModifier { StatType = StatType.AttackSpeed, ModType = ModifierType.Percent, Value = 0.3f }
                    };
                    break;

                case "buff_focus":
                    buff.StatModifiers = new StatModifier[]
                    {
                        new StatModifier { StatType = StatType.CritRate, ModType = ModifierType.Flat, Value = 0.2f },
                        new StatModifier { StatType = StatType.CritDamage, ModType = ModifierType.Flat, Value = 0.3f }
                    };
                    break;

                case "buff_soul_drain":
                    buff.StatModifiers = new StatModifier[]
                    {
                        new StatModifier { StatType = StatType.LifeSteal, ModType = ModifierType.Flat, Value = 0.1f }
                    };
                    break;

                default:
                    Debug.LogWarning($"[SkillExecutor] 알 수 없는 버프 스킬: {skillId}");
                    return;
            }

            _buffSystem.ApplyBuff(buff);

            // 버프 이펙트
            if (_buffActivateEffectPrefab != null && _playerTransform != null)
            {
                var fx = Instantiate(_buffActivateEffectPrefab, _playerTransform.position, Quaternion.identity);
                fx.transform.SetParent(_playerTransform);
                Destroy(fx, skill.duration);
            }

            // 이벤트 발행
            GameEventSystem.Publish(new BuffAppliedEvent
            {
                BuffId = skillId,
                BuffName = skill.skillName,
                Duration = skill.duration,
                IsDebuff = false
            });
        }

        // ============================================================
        //  Special Effects
        // ============================================================

        /// <summary>
        /// 화상 도트를 적용한다.
        /// </summary>
        private void ApplyBurnDoT(GameObject target, float duration, float damageRatio)
        {
            if (target == null || _playerStats == null) return;

            int tickDamage = Mathf.Max(1, Mathf.RoundToInt(_playerStats.Attack * damageRatio));
            StartCoroutine(DoTCoroutine(target, duration, 1f, tickDamage, DamageType.Fire));
        }

        /// <summary>
        /// DoT (Damage over Time) 코루틴.
        /// </summary>
        private IEnumerator DoTCoroutine(GameObject target, float duration, float tickInterval,
            int tickDamage, DamageType element)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (target == null) yield break;

                var damageable = target.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(tickDamage, element);
                }

                GameEventSystem.Publish(new DamageEvent
                {
                    Attacker = _playerTransform != null ? _playerTransform.gameObject : null,
                    Target = target,
                    Damage = tickDamage,
                    IsCritical = false,
                    Type = element,
                    HitPoint = target.transform.position
                });

                elapsed += tickInterval;
                yield return new WaitForSeconds(tickInterval);
            }
        }

        /// <summary>
        /// 지속 데미지 구역 코루틴.
        /// </summary>
        private IEnumerator DamageZoneCoroutine(Vector2 center, float radius, float duration,
            float tickInterval, int tickDamage, DamageType element)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                Collider2D[] inZone = Physics2D.OverlapCircleAll(center, radius, _enemyLayer);
                foreach (var col in inZone)
                {
                    var damageable = col.GetComponent<IDamageable>();
                    if (damageable != null)
                        damageable.TakeDamage(tickDamage, element);

                    GameEventSystem.Publish(new DamageEvent
                    {
                        Attacker = _playerTransform != null ? _playerTransform.gameObject : null,
                        Target = col.gameObject,
                        Damage = tickDamage,
                        IsCritical = false,
                        Type = element,
                        HitPoint = col.transform.position
                    });
                }

                elapsed += tickInterval;
                yield return new WaitForSeconds(tickInterval);
            }
        }

        /// <summary>
        /// 감속 구역 코루틴 (빙결 장판).
        /// </summary>
        private IEnumerator SlowZoneCoroutine(Vector2 center, float radius, float duration, float tickInterval)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                Collider2D[] inZone = Physics2D.OverlapCircleAll(center, radius, _enemyLayer);
                foreach (var col in inZone)
                {
                    ApplySlowDebuff(col.gameObject, 0.5f, tickInterval + 0.2f);
                }

                elapsed += tickInterval;
                yield return new WaitForSeconds(tickInterval);
            }
        }

        /// <summary>
        /// 에어본을 적용한다.
        /// </summary>
        private void ApplyAirborne(GameObject target, float duration)
        {
            if (target == null) return;

            if (KnockbackSystem.Instance != null)
            {
                var profile = new KnockbackProfile
                {
                    force = 2f,
                    duration = 0.1f,
                    isAirborne = true,
                    airborneDuration = duration
                };

                Vector2 upDir = Vector2.up;
                KnockbackSystem.Instance.ApplyKnockback(target, upDir, profile);
            }
        }

        /// <summary>
        /// 감속 디버프를 적용한다.
        /// </summary>
        private void ApplySlowDebuff(GameObject target, float slowPercent, float duration)
        {
            if (target == null || _buffSystem == null) return;

            // 적에게 BuffSystem이 없으면 간단한 속도 감소 처리
            var enemyBase = target.GetComponent<SoulCraft.Enemy.EnemyBase>();
            if (enemyBase != null)
            {
                StartCoroutine(SlowEnemyCoroutine(target, slowPercent, duration));
            }
        }

        private IEnumerator SlowEnemyCoroutine(GameObject target, float slowPercent, float duration)
        {
            if (target == null) yield break;

            var rb = target.GetComponent<Rigidbody2D>();
            if (rb == null) yield break;

            // 속도 감소 적용 (linearDamping 증가로 간접 구현)
            float originalDrag = rb.linearDamping;
            rb.linearDamping = originalDrag + slowPercent * 20f;

            yield return new WaitForSeconds(duration);

            if (target != null && rb != null)
                rb.linearDamping = originalDrag;
        }

        /// <summary>
        /// 넉백을 적용한다.
        /// </summary>
        private void ApplyKnockbackToTarget(GameObject target, Vector2 direction, float multiplier)
        {
            if (target == null || KnockbackSystem.Instance == null) return;

            var profile = new KnockbackProfile
            {
                force = 8f * multiplier,
                duration = 0.2f,
                isAirborne = false,
                airborneDuration = 0f
            };

            KnockbackSystem.Instance.ApplyKnockback(target, direction, profile);
        }

        /// <summary>
        /// 처형 보너스 데미지를 적용한다.
        /// </summary>
        private int ApplyExecuteBonus(int baseDamage, GameObject target)
        {
            var enemyBase = target.GetComponent<SoulCraft.Enemy.EnemyBase>();
            if (enemyBase != null)
            {
                float hpRatio = (float)enemyBase.CurrentHp / enemyBase.MaxHp;
                if (hpRatio <= 0.3f)
                {
                    return Mathf.RoundToInt(baseDamage * 3f);
                }
            }
            return baseDamage;
        }

        /// <summary>
        /// 경직 게이지 보너스를 적용한다.
        /// </summary>
        private void ApplyStaggerBonus(GameObject target, int damage, float multiplier)
        {
            if (multiplier <= 1f) return;

            var stagger = target.GetComponent<EnemyStagger>();
            if (stagger != null)
            {
                float bonusStagger = damage * (multiplier - 1f);
                stagger.AddStaggerGauge(bonusStagger);
            }
        }

        // ============================================================
        //  번개 연쇄 (LightningChain) — 이벤트 기반
        // ============================================================

        void OnEnable()
        {
            GameEventSystem.Subscribe<DamageEvent>(OnDamageForChainLightning);
        }

        void OnDisable()
        {
            GameEventSystem.Unsubscribe<DamageEvent>(OnDamageForChainLightning);
        }

        private void OnDamageForChainLightning(DamageEvent evt)
        {
            // 번개 속성 데미지가 들어올 때 연쇄 처리
            if (evt.Type != DamageType.Lightning) return;
            if (evt.Attacker == null || evt.Target == null) return;
            if (!evt.Attacker.CompareTag("Player")) return;

            // 이미 연쇄된 데미지인지 체크 (연쇄 데미지는 약한 데미지)
            // 간단한 임계치로 무한 루프 방지
            if (evt.Damage < 5) return;

            StartCoroutine(ChainLightningCoroutine(evt.Target, evt.Damage));
        }

        private IEnumerator ChainLightningCoroutine(GameObject firstTarget, int baseDamage)
        {
            yield return new WaitForSeconds(0.1f);

            if (firstTarget == null) yield break;

            float chainRadius = 3f;
            int maxChains = 2;
            float chainDamageRatio = 0.6f;

            HashSet<GameObject> alreadyHit = new HashSet<GameObject> { firstTarget };
            Vector2 lastPos = firstTarget.transform.position;

            for (int i = 0; i < maxChains; i++)
            {
                Collider2D[] nearby = Physics2D.OverlapCircleAll(lastPos, chainRadius, _enemyLayer);
                Collider2D bestTarget = null;
                float bestDist = float.MaxValue;

                foreach (var col in nearby)
                {
                    if (alreadyHit.Contains(col.gameObject)) continue;
                    float dist = Vector2.Distance(lastPos, col.transform.position);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestTarget = col;
                    }
                }

                if (bestTarget == null) break;

                int chainDamage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * chainDamageRatio));
                alreadyHit.Add(bestTarget.gameObject);

                var damageable = bestTarget.GetComponent<IDamageable>();
                if (damageable != null)
                    damageable.TakeDamage(chainDamage, DamageType.Lightning);

                lastPos = bestTarget.transform.position;
                chainDamageRatio *= 0.7f; // 연쇄마다 데미지 감소

                yield return new WaitForSeconds(0.08f);
            }
        }

        // ============================================================
        //  Utility
        // ============================================================

        /// <summary>
        /// 부채꼴 범위 내 적을 감지한다.
        /// </summary>
        private List<Collider2D> DetectEnemiesInArc(Vector2 origin, Vector2 direction, float range, float arcAngle)
        {
            List<Collider2D> result = new List<Collider2D>();
            Collider2D[] candidates = Physics2D.OverlapCircleAll(origin, range, _enemyLayer);

            float halfArc = arcAngle * 0.5f;

            foreach (var col in candidates)
            {
                if (arcAngle >= 360f)
                {
                    result.Add(col);
                    continue;
                }

                Vector2 toTarget = ((Vector2)col.transform.position - origin).normalized;
                float angle = Vector2.Angle(direction, toTarget);

                if (angle <= halfArc)
                    result.Add(col);
            }

            return result;
        }

        /// <summary>
        /// 스킬 데미지를 계산한다.
        /// </summary>
        private int CalculateSkillDamage(SkillData skill, GameObject target, out bool isCritical)
        {
            int attackPower = _playerStats != null ? _playerStats.Attack : 10;
            float critRate = _playerStats != null ? _playerStats.CritRate : 0.05f;
            float critDamage = _playerStats != null ? _playerStats.CritDamage : 1.5f;

            int defense = 0;
            DamageType targetElement = DamageType.Physical;

            var enemyBase = target.GetComponent<SoulCraft.Enemy.EnemyBase>();
            if (enemyBase != null && enemyBase.Data != null)
            {
                defense = enemyBase.Data.defense;
            }

            DamageType attackElement = ConvertElement(skill.element);

            DamageResult result = DamageCalculator.Calculate(
                attackPower, skill.damageMultiplier, defense,
                attackElement, targetElement,
                critRate, critDamage);

            isCritical = result.IsCritical;
            return result.Damage;
        }

        /// <summary>
        /// DamageEvent를 발행한다.
        /// </summary>
        private void PublishDamageEvent(SkillData skill, GameObject target, Vector2 origin,
            int damage, bool isCritical)
        {
            var damageable = target.GetComponent<IDamageable>();
            if (damageable != null)
                damageable.TakeDamage(damage, ConvertElement(skill.element));

            GameEventSystem.Publish(new DamageEvent
            {
                Attacker = _playerTransform != null ? _playerTransform.gameObject : null,
                Target = target,
                Damage = damage,
                IsCritical = isCritical,
                Type = ConvertElement(skill.element),
                HitPoint = target.transform.position
            });
        }

        /// <summary>
        /// 스킬 이펙트를 스폰한다.
        /// </summary>
        private void SpawnSkillEffect(SkillData skill, Vector2 position, Vector2 direction)
        {
            GameObject prefab = skill.effectPrefab;
            if (prefab == null)
            {
                if (skill.skillType == SkillType.AoE && _defaultAoeEffectPrefab != null)
                    prefab = _defaultAoeEffectPrefab;
                else
                    return;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            var fx = Instantiate(prefab, position, Quaternion.Euler(0, 0, angle));

            float lifetime = skill.duration > 0f ? skill.duration : 2f;
            Destroy(fx, lifetime);
        }

        /// <summary>
        /// 문자열 element를 DamageType으로 변환한다.
        /// </summary>
        private DamageType ConvertElement(DamageType element)
        {
            return element;
        }

        /// <summary>
        /// 콤보 태그 보유 여부를 확인한다.
        /// </summary>
        private bool HasComboTag(SkillData skill, string tag)
        {
            if (skill.comboTags == null) return false;
            foreach (var t in skill.comboTags)
            {
                if (t == tag) return true;
            }
            return false;
        }
    }

    // ================================================================
    //  투사체 착탄 폭발 컴포넌트 (화염구 등)
    // ================================================================

    /// <summary>
    /// Projectile에 부착하여 파괴 시 AoE 폭발을 발생시킨다.
    /// </summary>
    public class ProjectileExplosion : MonoBehaviour
    {
        private float _radius;
        private int _baseDamage;
        private float _multiplier;
        private DamageType _element;
        private LayerMask _enemyLayer;
        private GameObject _owner;
        private bool _initialized;

        public void Initialize(float radius, int baseDamage, float multiplier,
            DamageType element, LayerMask enemyLayer, GameObject owner)
        {
            _radius = radius;
            _baseDamage = baseDamage;
            _multiplier = multiplier;
            _element = element;
            _enemyLayer = enemyLayer;
            _owner = owner;
            _initialized = true;
        }

        void OnDestroy()
        {
            if (!_initialized) return;

            Collider2D[] targets = Physics2D.OverlapCircleAll(transform.position, _radius, _enemyLayer);
            foreach (var target in targets)
            {
                int damage = Mathf.Max(1, Mathf.RoundToInt(_baseDamage * _multiplier));

                var damageable = target.GetComponent<IDamageable>();
                if (damageable != null)
                    damageable.TakeDamage(damage, _element);

                GameEventSystem.Publish(new DamageEvent
                {
                    Attacker = _owner,
                    Target = target.gameObject,
                    Damage = damage,
                    IsCritical = false,
                    Type = _element,
                    HitPoint = target.ClosestPoint(transform.position)
                });
            }
        }
    }

    // ================================================================
    //  Buff 이벤트
    // ================================================================

    public struct BuffAppliedEvent
    {
        public string BuffId;
        public string BuffName;
        public float Duration;
        public bool IsDebuff;
    }

    public struct BuffRemovedEvent
    {
        public string BuffId;
        public string BuffName;
    }
}
