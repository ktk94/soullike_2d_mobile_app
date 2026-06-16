using System;
using System.Collections.Generic;
using UnityEngine;
using SoulCraft.Core;
using SoulCraft.Player;

namespace SoulCraft.Combat
{
    // ================================================================
    //  Enums & Data Structures
    // ================================================================

    /// <summary>
    /// 스탯 종류.
    /// </summary>
    public enum StatType
    {
        Attack,
        Defense,
        Speed,
        AttackSpeed,
        CritRate,
        CritDamage,
        MaxHp,
        LifeSteal
    }

    /// <summary>
    /// 수치 변경 방식.
    /// </summary>
    public enum ModifierType
    {
        /// <summary>고정 수치 가산</summary>
        Flat,
        /// <summary>비율 가산 (0.5 = +50%)</summary>
        Percent
    }

    /// <summary>
    /// 개별 스탯 변경 항목.
    /// </summary>
    [Serializable]
    public class StatModifier
    {
        public StatType StatType;
        public ModifierType ModType;
        public float Value;
    }

    /// <summary>
    /// 버프/디버프 정의.
    /// </summary>
    [Serializable]
    public class Buff
    {
        public string Id;
        public string Name;
        public float Duration;
        public StatModifier[] StatModifiers;
        public bool IsDebuff;
        public Sprite Icon;
        public bool Stackable;

        /// <summary>틱 기반 효과 간격 (0이면 틱 없음)</summary>
        public float TickInterval;
        /// <summary>틱당 HP 변화량 (양수=회복, 음수=데미지)</summary>
        public int TickHpChange;
        /// <summary>틱 데미지 속성</summary>
        public DamageType TickElement;
    }

    /// <summary>
    /// 현재 활성화된 버프 인스턴스.
    /// </summary>
    public class ActiveBuff
    {
        public Buff BuffData;
        public float RemainingTime;
        public float TickTimer;
        public int StackCount;

        /// <summary>이 인스턴스가 PlayerStats에 실제로 적용한 Flat 수치 (해제 시 차감용)</summary>
        public float[] AppliedFlatValues;
        /// <summary>이 인스턴스가 PlayerStats에 실제로 적용한 Percent 수치</summary>
        public float[] AppliedPercentValues;

        public ActiveBuff(Buff buff)
        {
            BuffData = buff;
            RemainingTime = buff.Duration;
            TickTimer = 0f;
            StackCount = 1;

            int modCount = buff.StatModifiers != null ? buff.StatModifiers.Length : 0;
            AppliedFlatValues = new float[modCount];
            AppliedPercentValues = new float[modCount];
        }
    }

    // ================================================================
    //  BuffSystem
    // ================================================================

    /// <summary>
    /// 버프/디버프 관리 시스템.
    /// PlayerStats와 연동하여 스탯 보너스를 적용/해제하며,
    /// 틱 기반 효과(DoT/HoT)를 처리한다.
    /// </summary>
    public class BuffSystem : MonoBehaviour
    {
        // ── Events ───────────────────────────────────────────
        public event Action<ActiveBuff> OnBuffApplied;
        public event Action<ActiveBuff> OnBuffRemoved;
        public event Action<ActiveBuff> OnBuffRefreshed;
        public event Action<ActiveBuff, int> OnBuffTick; // activeBuff, tickHpChange

        // ── Inspector ────────────────────────────────────────
        [Header("Settings")]
        [Tooltip("최대 동시 활성 버프 수")]
        [SerializeField] private int _maxActiveBuffs = 16;

        [Header("References")]
        [SerializeField] private PlayerStats _playerStats;

        // ── Runtime ──────────────────────────────────────────
        private readonly List<ActiveBuff> _activeBuffs = new();
        private readonly List<ActiveBuff> _buffsToRemove = new();

        // 버프에 의한 총 스탯 보너스 캐시
        private int _totalBonusAttack;
        private int _totalBonusDefense;
        private float _totalBonusSpeed;
        private float _totalBonusCritRate;
        private float _totalBonusCritDamage;
        private int _totalBonusMaxHp;
        private float _totalLifeSteal;
        private float _totalAttackSpeedBonus;

        // Percent 기반 보너스 (별도 추적)
        private float _percentAttack;
        private float _percentDefense;
        private float _percentSpeed;

        // ── Properties ───────────────────────────────────────

        /// <summary>현재 활성 버프 목록 (읽기 전용).</summary>
        public IReadOnlyList<ActiveBuff> ActiveBuffs => _activeBuffs;

        /// <summary>현재 흡혈 비율 (0~1).</summary>
        public float LifeStealRatio => _totalLifeSteal;

        /// <summary>현재 공격속도 보너스 비율.</summary>
        public float AttackSpeedBonus => _totalAttackSpeedBonus;

        // ============================================================
        //  Unity Lifecycle
        // ============================================================

        void Start()
        {
            if (_playerStats == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                    _playerStats = player.GetComponent<PlayerStats>();
            }

            // 흡혈 이벤트 구독
            GameEventSystem.Subscribe<DamageEvent>(OnDamageForLifeSteal);
        }

        void OnDestroy()
        {
            GameEventSystem.Unsubscribe<DamageEvent>(OnDamageForLifeSteal);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _buffsToRemove.Clear();

            for (int i = _activeBuffs.Count - 1; i >= 0; i--)
            {
                ActiveBuff active = _activeBuffs[i];
                active.RemainingTime -= dt;

                // 틱 처리
                if (active.BuffData.TickInterval > 0f)
                {
                    active.TickTimer += dt;
                    if (active.TickTimer >= active.BuffData.TickInterval)
                    {
                        active.TickTimer -= active.BuffData.TickInterval;
                        ProcessTick(active);
                    }
                }

                // 만료 체크
                if (active.RemainingTime <= 0f)
                {
                    _buffsToRemove.Add(active);
                }
            }

            // 만료된 버프 제거
            foreach (var expired in _buffsToRemove)
            {
                RemoveBuffInternal(expired);
            }
        }

        // ============================================================
        //  Public API
        // ============================================================

        /// <summary>
        /// 버프를 적용한다.
        /// 같은 ID의 버프가 이미 있으면 Stackable 여부에 따라 갱신/중첩한다.
        /// </summary>
        public void ApplyBuff(Buff buff)
        {
            if (buff == null) return;

            // 기존 동일 버프 검색
            ActiveBuff existing = FindActiveBuff(buff.Id);

            if (existing != null)
            {
                if (buff.Stackable)
                {
                    // 중첩 가능: 스택 증가 + 스탯 재적용
                    existing.StackCount++;
                    existing.RemainingTime = buff.Duration; // 지속시간 갱신
                    UnapplyStatModifiers(existing);
                    ApplyStatModifiers(existing);
                    OnBuffRefreshed?.Invoke(existing);
                }
                else
                {
                    // 중첩 불가: 지속시간만 갱신
                    existing.RemainingTime = buff.Duration;
                    existing.TickTimer = 0f;
                    OnBuffRefreshed?.Invoke(existing);
                }
                return;
            }

            // 최대 버프 수 체크
            if (_activeBuffs.Count >= _maxActiveBuffs)
            {
                Debug.LogWarning($"[BuffSystem] 최대 버프 수({_maxActiveBuffs})에 도달. '{buff.Name}' 적용 실패.");
                return;
            }

            // 새 버프 생성 및 적용
            ActiveBuff newBuff = new ActiveBuff(buff);
            _activeBuffs.Add(newBuff);
            ApplyStatModifiers(newBuff);

            OnBuffApplied?.Invoke(newBuff);

            Debug.Log($"[BuffSystem] 버프 적용: {buff.Name} ({buff.Duration}초)");
        }

        /// <summary>
        /// 특정 ID의 버프를 즉시 제거한다.
        /// </summary>
        public void RemoveBuff(string buffId)
        {
            ActiveBuff target = FindActiveBuff(buffId);
            if (target != null)
            {
                RemoveBuffInternal(target);
            }
        }

        /// <summary>
        /// 모든 버프를 제거한다.
        /// </summary>
        public void RemoveAllBuffs()
        {
            for (int i = _activeBuffs.Count - 1; i >= 0; i--)
            {
                RemoveBuffInternal(_activeBuffs[i]);
            }
        }

        /// <summary>
        /// 모든 디버프만 제거한다.
        /// </summary>
        public void RemoveAllDebuffs()
        {
            for (int i = _activeBuffs.Count - 1; i >= 0; i--)
            {
                if (_activeBuffs[i].BuffData.IsDebuff)
                    RemoveBuffInternal(_activeBuffs[i]);
            }
        }

        /// <summary>
        /// 특정 ID의 버프가 활성 중인지 확인한다.
        /// </summary>
        public bool HasBuff(string buffId)
        {
            return FindActiveBuff(buffId) != null;
        }

        /// <summary>
        /// 특정 ID의 활성 버프를 반환한다.
        /// </summary>
        public ActiveBuff GetActiveBuff(string buffId)
        {
            return FindActiveBuff(buffId);
        }

        /// <summary>
        /// 특정 버프의 남은 시간을 반환한다 (없으면 0).
        /// </summary>
        public float GetBuffRemainingTime(string buffId)
        {
            ActiveBuff active = FindActiveBuff(buffId);
            return active?.RemainingTime ?? 0f;
        }

        /// <summary>
        /// 특정 버프의 지속시간 비율을 반환한다 (0=만료, 1=방금 시작).
        /// </summary>
        public float GetBuffDurationRatio(string buffId)
        {
            ActiveBuff active = FindActiveBuff(buffId);
            if (active == null) return 0f;
            if (active.BuffData.Duration <= 0f) return 0f;
            return Mathf.Clamp01(active.RemainingTime / active.BuffData.Duration);
        }

        /// <summary>
        /// 특정 버프의 지속시간을 연장한다.
        /// </summary>
        public void ExtendBuffDuration(string buffId, float additionalTime)
        {
            ActiveBuff active = FindActiveBuff(buffId);
            if (active != null)
            {
                active.RemainingTime += additionalTime;
            }
        }

        // ============================================================
        //  Stat Modifier Application
        // ============================================================

        /// <summary>
        /// 버프의 스탯 변경을 PlayerStats에 적용한다.
        /// </summary>
        private void ApplyStatModifiers(ActiveBuff active)
        {
            if (_playerStats == null || active.BuffData.StatModifiers == null) return;

            for (int i = 0; i < active.BuffData.StatModifiers.Length; i++)
            {
                StatModifier mod = active.BuffData.StatModifiers[i];
                float value = mod.Value * active.StackCount;

                switch (mod.StatType)
                {
                    case StatType.Attack:
                        if (mod.ModType == ModifierType.Flat)
                        {
                            int flatVal = Mathf.RoundToInt(value);
                            _playerStats.BonusAttack += flatVal;
                            active.AppliedFlatValues[i] = flatVal;
                        }
                        else
                        {
                            int percentVal = Mathf.RoundToInt(_playerStats.Attack * value);
                            _playerStats.BonusAttack += percentVal;
                            active.AppliedPercentValues[i] = percentVal;
                            _percentAttack += value;
                        }
                        break;

                    case StatType.Defense:
                        if (mod.ModType == ModifierType.Flat)
                        {
                            int flatVal = Mathf.RoundToInt(value);
                            _playerStats.BonusDefense += flatVal;
                            active.AppliedFlatValues[i] = flatVal;
                        }
                        else
                        {
                            int percentVal = Mathf.RoundToInt(_playerStats.Defense * value);
                            _playerStats.BonusDefense += percentVal;
                            active.AppliedPercentValues[i] = percentVal;
                            _percentDefense += value;
                        }
                        break;

                    case StatType.Speed:
                        if (mod.ModType == ModifierType.Flat)
                        {
                            _playerStats.BonusSpeed += value;
                            active.AppliedFlatValues[i] = value;
                        }
                        else
                        {
                            float percentVal = _playerStats.Speed * value;
                            _playerStats.BonusSpeed += percentVal;
                            active.AppliedPercentValues[i] = percentVal;
                            _percentSpeed += value;
                        }
                        break;

                    case StatType.CritRate:
                        _playerStats.BonusCritRate += value;
                        active.AppliedFlatValues[i] = value;
                        break;

                    case StatType.CritDamage:
                        _playerStats.BonusCritDamage += value;
                        active.AppliedFlatValues[i] = value;
                        break;

                    case StatType.MaxHp:
                        if (mod.ModType == ModifierType.Flat)
                        {
                            int flatVal = Mathf.RoundToInt(value);
                            _playerStats.BonusMaxHp += flatVal;
                            active.AppliedFlatValues[i] = flatVal;
                        }
                        else
                        {
                            int percentVal = Mathf.RoundToInt(_playerStats.MaxHp * value);
                            _playerStats.BonusMaxHp += percentVal;
                            active.AppliedPercentValues[i] = percentVal;
                        }
                        break;

                    case StatType.LifeSteal:
                        _totalLifeSteal += value;
                        active.AppliedFlatValues[i] = value;
                        break;

                    case StatType.AttackSpeed:
                        _totalAttackSpeedBonus += value;
                        active.AppliedFlatValues[i] = value;
                        break;
                }
            }

            _playerStats.RecalculateStats();
        }

        /// <summary>
        /// 버프의 스탯 변경을 PlayerStats에서 해제한다.
        /// </summary>
        private void UnapplyStatModifiers(ActiveBuff active)
        {
            if (_playerStats == null || active.BuffData.StatModifiers == null) return;

            for (int i = 0; i < active.BuffData.StatModifiers.Length; i++)
            {
                StatModifier mod = active.BuffData.StatModifiers[i];

                switch (mod.StatType)
                {
                    case StatType.Attack:
                        if (mod.ModType == ModifierType.Flat)
                            _playerStats.BonusAttack -= Mathf.RoundToInt(active.AppliedFlatValues[i]);
                        else
                        {
                            _playerStats.BonusAttack -= Mathf.RoundToInt(active.AppliedPercentValues[i]);
                            _percentAttack -= mod.Value * active.StackCount;
                        }
                        break;

                    case StatType.Defense:
                        if (mod.ModType == ModifierType.Flat)
                            _playerStats.BonusDefense -= Mathf.RoundToInt(active.AppliedFlatValues[i]);
                        else
                        {
                            _playerStats.BonusDefense -= Mathf.RoundToInt(active.AppliedPercentValues[i]);
                            _percentDefense -= mod.Value * active.StackCount;
                        }
                        break;

                    case StatType.Speed:
                        if (mod.ModType == ModifierType.Flat)
                            _playerStats.BonusSpeed -= active.AppliedFlatValues[i];
                        else
                        {
                            _playerStats.BonusSpeed -= active.AppliedPercentValues[i];
                            _percentSpeed -= mod.Value * active.StackCount;
                        }
                        break;

                    case StatType.CritRate:
                        _playerStats.BonusCritRate -= active.AppliedFlatValues[i];
                        break;

                    case StatType.CritDamage:
                        _playerStats.BonusCritDamage -= active.AppliedFlatValues[i];
                        break;

                    case StatType.MaxHp:
                        if (mod.ModType == ModifierType.Flat)
                            _playerStats.BonusMaxHp -= Mathf.RoundToInt(active.AppliedFlatValues[i]);
                        else
                            _playerStats.BonusMaxHp -= Mathf.RoundToInt(active.AppliedPercentValues[i]);
                        break;

                    case StatType.LifeSteal:
                        _totalLifeSteal -= active.AppliedFlatValues[i];
                        break;

                    case StatType.AttackSpeed:
                        _totalAttackSpeedBonus -= active.AppliedFlatValues[i];
                        break;
                }
            }

            _playerStats.RecalculateStats();
        }

        // ============================================================
        //  Tick Processing
        // ============================================================

        /// <summary>
        /// 틱 효과를 처리한다 (DoT/HoT).
        /// </summary>
        private void ProcessTick(ActiveBuff active)
        {
            if (active.BuffData.TickHpChange == 0) return;
            if (_playerStats == null) return;

            int hpChange = active.BuffData.TickHpChange * active.StackCount;

            if (hpChange > 0)
            {
                // HoT (Heal over Time)
                _playerStats.Heal(hpChange);
            }
            else
            {
                // DoT (Damage over Time) — 플레이어에 대한 디버프
                _playerStats.TakeDamage(Mathf.Abs(hpChange), active.BuffData.TickElement);
            }

            OnBuffTick?.Invoke(active, hpChange);
        }

        // ============================================================
        //  Life Steal
        // ============================================================

        /// <summary>
        /// 데미지 이벤트를 구독하여 흡혈 효과를 처리한다.
        /// </summary>
        private void OnDamageForLifeSteal(DamageEvent evt)
        {
            if (_totalLifeSteal <= 0f) return;
            if (_playerStats == null) return;
            if (evt.Attacker == null) return;
            if (!evt.Attacker.CompareTag("Player")) return;

            // 처치 판정: 적이 죽었으면 보너스 흡혈
            float stealRatio = _totalLifeSteal;

            // 적 사망 체크 (EnemyBase 기반)
            if (evt.Target != null)
            {
                var enemyBase = evt.Target.GetComponent<SoulCraft.Enemy.EnemyBase>();
                if (enemyBase != null && enemyBase.CurrentHp <= 0)
                {
                    stealRatio *= 1.5f; // 처치 시 50% 보너스 흡혈
                }
            }

            int healAmount = Mathf.Max(1, Mathf.RoundToInt(evt.Damage * stealRatio));
            _playerStats.Heal(healAmount);
        }

        // ============================================================
        //  Internal Helpers
        // ============================================================

        /// <summary>
        /// ID로 활성 버프를 찾는다.
        /// </summary>
        private ActiveBuff FindActiveBuff(string buffId)
        {
            for (int i = 0; i < _activeBuffs.Count; i++)
            {
                if (_activeBuffs[i].BuffData.Id == buffId)
                    return _activeBuffs[i];
            }
            return null;
        }

        /// <summary>
        /// 버프를 내부적으로 제거한다.
        /// </summary>
        private void RemoveBuffInternal(ActiveBuff active)
        {
            UnapplyStatModifiers(active);
            _activeBuffs.Remove(active);

            OnBuffRemoved?.Invoke(active);

            // 전역 이벤트 발행
            GameEventSystem.Publish(new BuffRemovedEvent
            {
                BuffId = active.BuffData.Id,
                BuffName = active.BuffData.Name
            });

            Debug.Log($"[BuffSystem] 버프 해제: {active.BuffData.Name}");
        }

        // ============================================================
        //  Convenience: Debuff Factory Methods
        // ============================================================

        /// <summary>
        /// 화상 디버프를 생성하여 적용한다 (플레이어용 — 적 화염 공격 등).
        /// </summary>
        public void ApplyBurnDebuff(float duration, int tickDamage, float tickInterval = 1f)
        {
            ApplyBuff(new Buff
            {
                Id = "debuff_burn",
                Name = "화상",
                Duration = duration,
                IsDebuff = true,
                Stackable = false,
                TickInterval = tickInterval,
                TickHpChange = -tickDamage,
                TickElement = DamageType.Fire,
                StatModifiers = Array.Empty<StatModifier>()
            });
        }

        /// <summary>
        /// 빙결(감속) 디버프를 생성하여 적용한다.
        /// </summary>
        public void ApplyFrostDebuff(float duration, float slowPercent)
        {
            ApplyBuff(new Buff
            {
                Id = "debuff_frost",
                Name = "빙결",
                Duration = duration,
                IsDebuff = true,
                Stackable = false,
                StatModifiers = new StatModifier[]
                {
                    new StatModifier
                    {
                        StatType = StatType.Speed,
                        ModType = ModifierType.Percent,
                        Value = -slowPercent
                    }
                }
            });
        }

        /// <summary>
        /// 독 디버프를 생성하여 적용한다.
        /// </summary>
        public void ApplyPoisonDebuff(float duration, int tickDamage, float tickInterval = 1f)
        {
            ApplyBuff(new Buff
            {
                Id = "debuff_poison",
                Name = "중독",
                Duration = duration,
                IsDebuff = true,
                Stackable = true,
                TickInterval = tickInterval,
                TickHpChange = -tickDamage,
                TickElement = DamageType.Dark,
                StatModifiers = Array.Empty<StatModifier>()
            });
        }

        /// <summary>
        /// 재생(HoT) 버프를 생성하여 적용한다.
        /// </summary>
        public void ApplyRegenBuff(float duration, int tickHeal, float tickInterval = 1f)
        {
            ApplyBuff(new Buff
            {
                Id = "buff_regen",
                Name = "재생",
                Duration = duration,
                IsDebuff = false,
                Stackable = false,
                TickInterval = tickInterval,
                TickHpChange = tickHeal,
                TickElement = DamageType.Holy,
                StatModifiers = Array.Empty<StatModifier>()
            });
        }

        // ============================================================
        //  광전사 분노 — 처치 시 지속시간 연장 연동
        // ============================================================

        void OnEnable()
        {
            GameEventSystem.Subscribe<EnemyDeathEvent>(OnEnemyDeath);
        }

        void OnDisable()
        {
            GameEventSystem.Unsubscribe<EnemyDeathEvent>(OnEnemyDeath);
        }

        private void OnEnemyDeath(EnemyDeathEvent evt)
        {
            // 광전사의 분노: 처치 시 2초 연장
            if (HasBuff("buff_berserker_rage"))
            {
                ExtendBuffDuration("buff_berserker_rage", 2f);
                Debug.Log("[BuffSystem] 광전사의 분노 지속시간 2초 연장!");
            }
        }
    }
}
