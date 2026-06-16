using UnityEngine;
using SoulCraft.Core;
using SoulCraft.Combat;
using SoulCraft.Player;
using SoulCraft.Farming;

namespace SoulCraft.Passive
{
    /// <summary>
    /// 특수 패시브 효과를 실시간으로 처리하는 프로세서.
    /// GameEventSystem을 통해 전투/파밍 이벤트를 구독하고,
    /// 패시브 보너스를 적용한다.
    /// </summary>
    public class PassiveEffectProcessor : MonoBehaviour
    {
        public static PassiveEffectProcessor Instance { get; private set; }

        // ── Dependencies ───────────────────────────────────

        private PlayerStats _playerStats;
        private ComboSystem _comboSystem;

        // ── Cached Passive Values (성능 최적화) ────────────

        private float _lifeStealPercent;
        private float _damageReductionPercent;
        private float _expBonusPercent;
        private float _goldBonusPercent;
        private float _elementalDamageBonusPercent;
        private float _comboWindowExtendSeconds;
        private float _staggerDamageBonusPercent;
        private float _skillCooldownReductionPercent;
        private float _dodgeCooldownReductionPercent;

        // ── Lifecycle ──────────────────────────────────────

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
            FindDependencies();
            CachePassiveValues();
            SubscribeEvents();
        }

        void OnDestroy()
        {
            UnsubscribeEvents();
            if (Instance == this) Instance = null;
        }

        // ── Initialization ─────────────────────────────────

        private void FindDependencies()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _playerStats = player.GetComponent<PlayerStats>();
                _comboSystem = player.GetComponent<ComboSystem>();
            }
        }

        /// <summary>
        /// 외부에서 종속성을 설정한다 (씬 전환 등).
        /// </summary>
        public void SetDependencies(PlayerStats stats, ComboSystem comboSystem)
        {
            _playerStats = stats;
            _comboSystem = comboSystem;
            CachePassiveValues();
        }

        // ── Event Subscription ─────────────────────────────

        private void SubscribeEvents()
        {
            GameEventSystem.Subscribe<DamageEvent>(OnDamageEvent);
            GameEventSystem.Subscribe<EnemyDeathEvent>(OnEnemyDeath);

            // 패시브 변경 시 캐시 갱신
            if (PassiveManager.Instance != null)
                PassiveManager.Instance.OnPassiveUnlocked += OnPassiveChanged;
        }

        private void UnsubscribeEvents()
        {
            GameEventSystem.Unsubscribe<DamageEvent>(OnDamageEvent);
            GameEventSystem.Unsubscribe<EnemyDeathEvent>(OnEnemyDeath);

            if (PassiveManager.Instance != null)
                PassiveManager.Instance.OnPassiveUnlocked -= OnPassiveChanged;
        }

        // ── Cache Update ───────────────────────────────────

        /// <summary>
        /// 패시브 매니저로부터 특수 효과 수치를 캐싱한다.
        /// 매 프레임 Dictionary 조회를 피하기 위해 이벤트 기반으로 갱신.
        /// </summary>
        public void CachePassiveValues()
        {
            if (PassiveManager.Instance == null) return;

            _lifeStealPercent = PassiveManager.Instance.GetPercentageBonus(PassiveStatType.LifeSteal);
            _damageReductionPercent = PassiveManager.Instance.GetPercentageBonus(PassiveStatType.DamageReduction);
            _expBonusPercent = PassiveManager.Instance.GetPercentageBonus(PassiveStatType.ExpBonus);
            _goldBonusPercent = PassiveManager.Instance.GetPercentageBonus(PassiveStatType.GoldBonus);
            _elementalDamageBonusPercent = PassiveManager.Instance.GetPercentageBonus(PassiveStatType.ElementalDamageBonus);
            _comboWindowExtendSeconds = PassiveManager.Instance.GetFlatBonus(PassiveStatType.ComboWindowExtend);
            _staggerDamageBonusPercent = PassiveManager.Instance.GetPercentageBonus(PassiveStatType.StaggerDamageBonus);
            _skillCooldownReductionPercent = PassiveManager.Instance.GetPercentageBonus(PassiveStatType.SkillCooldownReduction);
            _dodgeCooldownReductionPercent = PassiveManager.Instance.GetPercentageBonus(PassiveStatType.DodgeCooldown);
        }

        private void OnPassiveChanged(string passiveId, int newLevel)
        {
            CachePassiveValues();
        }

        // ── Life Steal (흡혈) ──────────────────────────────

        /// <summary>
        /// DamageEvent를 구독하여, 플레이어가 적에게 데미지를 입혔을 때
        /// 흡혈 패시브에 따라 HP를 회복한다.
        /// </summary>
        private void OnDamageEvent(DamageEvent evt)
        {
            if (_playerStats == null || _playerStats.IsDead) return;

            // 플레이어가 공격자일 때만 흡혈 적용
            if (evt.Attacker == null || _playerStats.gameObject != evt.Attacker) return;

            // 흡혈 처리
            if (_lifeStealPercent > 0f)
            {
                int healAmount = Mathf.Max(1, Mathf.RoundToInt(evt.Damage * _lifeStealPercent));
                _playerStats.Heal(healAmount);
            }
        }

        // ── Damage Reduction (데미지 감소) ─────────────────

        /// <summary>
        /// 피격 데미지에 데미지 감소 패시브를 적용한다.
        /// PlayerStats.TakeDamage 호출 전에 외부에서 이 메서드를 호출하여 데미지를 줄인다.
        /// </summary>
        /// <param name="rawDamage">원래 데미지</param>
        /// <returns>감소된 데미지</returns>
        public int ApplyDamageReduction(int rawDamage)
        {
            if (_damageReductionPercent <= 0f) return rawDamage;

            float reduced = rawDamage * (1f - _damageReductionPercent);
            return Mathf.Max(1, Mathf.RoundToInt(reduced));
        }

        // ── Exp Bonus (경험치 보너스) ─────────────────────

        /// <summary>
        /// 경험치 획득량에 패시브 보너스를 적용한다.
        /// </summary>
        /// <param name="baseExp">기본 경험치</param>
        /// <returns>보너스 적용 후 경험치</returns>
        public int ApplyExpBonus(int baseExp)
        {
            if (_expBonusPercent <= 0f) return baseExp;

            float bonus = baseExp * (1f + _expBonusPercent);
            return Mathf.Max(1, Mathf.RoundToInt(bonus));
        }

        // ── Gold Bonus (골드 보너스) ──────────────────────

        /// <summary>
        /// 골드 획득량에 패시브 보너스를 적용한다.
        /// </summary>
        /// <param name="baseGold">기본 골드</param>
        /// <returns>보너스 적용 후 골드</returns>
        public int ApplyGoldBonus(int baseGold)
        {
            if (_goldBonusPercent <= 0f) return baseGold;

            float bonus = baseGold * (1f + _goldBonusPercent);
            return Mathf.Max(1, Mathf.RoundToInt(bonus));
        }

        // ── Elemental Damage Bonus (속성 데미지 보너스) ───

        /// <summary>
        /// 속성 데미지에 패시브 보너스를 적용한다.
        /// DamageCalculator에서 참조한다.
        /// </summary>
        /// <param name="baseDamage">기본 데미지</param>
        /// <param name="attackElement">공격 속성</param>
        /// <returns>보너스 적용 후 데미지</returns>
        public int ApplyElementalDamageBonus(int baseDamage, DamageType attackElement)
        {
            // Physical이 아닌 속성 공격에만 보너스 적용
            if (attackElement == DamageType.Physical) return baseDamage;
            if (_elementalDamageBonusPercent <= 0f) return baseDamage;

            float bonus = baseDamage * (1f + _elementalDamageBonusPercent);
            return Mathf.Max(1, Mathf.RoundToInt(bonus));
        }

        /// <summary>
        /// 속성 데미지 보너스 배율을 반환한다 (1.0 = 보너스 없음).
        /// DamageCalculator에서 직접 배율을 곱할 때 사용.
        /// </summary>
        public float GetElementalDamageMultiplier(DamageType attackElement)
        {
            if (attackElement == DamageType.Physical) return 1f;
            return 1f + _elementalDamageBonusPercent;
        }

        // ── Combo Window Extend (콤보 윈도우 연장) ────────

        /// <summary>
        /// 콤보 윈도우 연장 시간(초)을 반환한다.
        /// ComboSystem에서 콤보 윈도우 계산 시 이 값을 더한다.
        /// </summary>
        public float GetComboWindowExtension()
        {
            return _comboWindowExtendSeconds;
        }

        // ── Stagger Damage Bonus (경직 데미지 보너스) ─────

        /// <summary>
        /// 경직 상태 적에게 추가 데미지 배율을 반환한다 (1.0 = 보너스 없음).
        /// </summary>
        public float GetStaggerDamageMultiplier()
        {
            return 1f + _staggerDamageBonusPercent;
        }

        /// <summary>
        /// 경직 상태 적에게의 데미지에 보너스를 적용한다.
        /// </summary>
        /// <param name="baseDamage">기본 데미지</param>
        /// <param name="isTargetStaggered">대상이 경직 상태인지</param>
        /// <returns>보너스 적용 후 데미지</returns>
        public int ApplyStaggerDamageBonus(int baseDamage, bool isTargetStaggered)
        {
            if (!isTargetStaggered || _staggerDamageBonusPercent <= 0f)
                return baseDamage;

            float bonus = baseDamage * (1f + _staggerDamageBonusPercent);
            return Mathf.Max(1, Mathf.RoundToInt(bonus));
        }

        // ── Skill Cooldown Reduction (스킬 쿨다운 감소) ──

        /// <summary>
        /// 스킬 쿨다운에 감소 패시브를 적용한다.
        /// </summary>
        /// <param name="baseCooldown">기본 쿨다운 (초)</param>
        /// <returns>감소 적용 후 쿨다운</returns>
        public float ApplySkillCooldownReduction(float baseCooldown)
        {
            if (_skillCooldownReductionPercent <= 0f) return baseCooldown;

            float reduced = baseCooldown * (1f - _skillCooldownReductionPercent);
            return Mathf.Max(0.1f, reduced); // 최소 0.1초
        }

        // ── Dodge Cooldown Reduction (대시 쿨다운 감소) ───

        /// <summary>
        /// 대시 쿨다운에 감소 패시브를 적용한다.
        /// </summary>
        /// <param name="baseCooldown">기본 쿨다운 (초)</param>
        /// <returns>감소 적용 후 쿨다운</returns>
        public float ApplyDodgeCooldownReduction(float baseCooldown)
        {
            if (_dodgeCooldownReductionPercent <= 0f) return baseCooldown;

            float reduced = baseCooldown * (1f - _dodgeCooldownReductionPercent);
            return Mathf.Max(0.1f, reduced); // 최소 0.1초
        }

        // ── Enemy Death Processing (적 처치 시 보너스) ────

        /// <summary>
        /// 적 처치 시 골드/경험치 보너스를 자동 적용한다.
        /// ItemDropper 등 외부 시스템에서 직접 ApplyGoldBonus/ApplyExpBonus를 호출해도 된다.
        /// </summary>
        private void OnEnemyDeath(EnemyDeathEvent evt)
        {
            // 적 처치 이벤트를 통해 추가 처리가 필요한 경우 여기에 구현
            // (예: 골드/경험치는 ItemDropper에서 처리하므로 여기서는 추가 로직만)
        }

        // ── Query API (외부 시스템 참조용) ─────────────────

        /// <summary>현재 흡혈 퍼센트</summary>
        public float LifeStealPercent => _lifeStealPercent;

        /// <summary>현재 데미지 감소 퍼센트</summary>
        public float DamageReductionPercent => _damageReductionPercent;

        /// <summary>현재 경험치 보너스 퍼센트</summary>
        public float ExpBonusPercent => _expBonusPercent;

        /// <summary>현재 골드 보너스 퍼센트</summary>
        public float GoldBonusPercent => _goldBonusPercent;

        /// <summary>현재 속성 데미지 보너스 퍼센트</summary>
        public float ElementalDamageBonusPercent => _elementalDamageBonusPercent;

        /// <summary>현재 콤보 윈도우 연장 시간 (초)</summary>
        public float ComboWindowExtendSeconds => _comboWindowExtendSeconds;

        /// <summary>현재 경직 추가 데미지 퍼센트</summary>
        public float StaggerDamageBonusPercent => _staggerDamageBonusPercent;

        /// <summary>현재 스킬 쿨다운 감소 퍼센트</summary>
        public float SkillCooldownReductionPercent => _skillCooldownReductionPercent;

        /// <summary>현재 대시 쿨다운 감소 퍼센트</summary>
        public float DodgeCooldownReductionPercent => _dodgeCooldownReductionPercent;
    }
}
