using UnityEngine;
using System;
using SoulCraft.Core;

namespace SoulCraft.Player
{
    public class PlayerStats : MonoBehaviour
    {
        // --- Events ---
        public event Action<int, int> OnHpChanged;   // currentHp, maxHp
        public event Action OnDeath;
        public event Action<int> OnLevelUp;           // newLevel

        // --- Base Stats ---
        [Header("HP")]
        [SerializeField] private int _baseMaxHp = 100;
        [SerializeField] private int _hpPerLevel = 12;

        [Header("Offense")]
        [SerializeField] private int _baseAttack = 10;
        [SerializeField] private int _attackPerLevel = 3;
        [SerializeField] private float _baseCritRate = 0.05f;
        [SerializeField] private float _critRatePerLevel = 0.005f;
        [SerializeField] private float _baseCritDamage = 1.5f;

        [Header("Defense")]
        [SerializeField] private int _baseDefense = 5;
        [SerializeField] private int _defensePerLevel = 2;

        [Header("Movement")]
        [SerializeField] private float _baseSpeed = 5f;
        [SerializeField] private float _speedPerLevel = 0.05f;

        [Header("Leveling")]
        [SerializeField] private int _baseExpToLevel = 100;
        [SerializeField] private float _expScaleFactor = 1.25f;

        // --- Properties ---
        public int Level { get; private set; } = 1;
        public int Exp { get; private set; }
        public int Gold { get; set; }

        public int MaxHp { get; private set; }
        public int CurrentHp { get; private set; }
        public int Attack { get; private set; }
        public int Defense { get; private set; }
        public float Speed { get; private set; }
        public float CritRate { get; private set; }
        public float CritDamage { get; private set; }

        public bool IsDead => CurrentHp <= 0;
        public int ExpToNextLevel => Mathf.RoundToInt(_baseExpToLevel * Mathf.Pow(_expScaleFactor, Level - 1));

        // Equipment / buff bonuses (외부에서 가감)
        [NonSerialized] public int BonusMaxHp;
        [NonSerialized] public int BonusAttack;
        [NonSerialized] public int BonusDefense;
        [NonSerialized] public float BonusSpeed;
        [NonSerialized] public float BonusCritRate;
        [NonSerialized] public float BonusCritDamage;

        private PlayerController _controller;

        // --- Unity Lifecycle ---

        void Awake()
        {
            _controller = GetComponent<PlayerController>();
            RecalculateStats();
            CurrentHp = MaxHp;
        }

        // --- Stat Calculation ---

        public void RecalculateStats()
        {
            int prevMaxHp = MaxHp;

            MaxHp = _baseMaxHp + _hpPerLevel * (Level - 1) + BonusMaxHp;
            Attack = _baseAttack + _attackPerLevel * (Level - 1) + BonusAttack;
            Defense = _baseDefense + _defensePerLevel * (Level - 1) + BonusDefense;
            Speed = _baseSpeed + _speedPerLevel * (Level - 1) + BonusSpeed;
            CritRate = Mathf.Clamp01(_baseCritRate + _critRatePerLevel * (Level - 1) + BonusCritRate);
            CritDamage = _baseCritDamage + BonusCritDamage;

            // MaxHp가 증가하면 그만큼 CurrentHp도 증가
            if (MaxHp > prevMaxHp && prevMaxHp > 0)
            {
                CurrentHp = Mathf.Min(CurrentHp + (MaxHp - prevMaxHp), MaxHp);
                OnHpChanged?.Invoke(CurrentHp, MaxHp);
            }
        }

        // --- Damage ---

        /// <summary>
        /// 데미지를 받는다. 방어력 공식: actual = raw * (100 / (100 + defense))
        /// </summary>
        public int TakeDamage(int rawDamage, DamageType type)
        {
            if (IsDead) return 0;

            // 무적 상태 체크
            if (_controller != null && _controller.IsInvincible) return 0;

            float reduction = 100f / (100f + Defense);
            int actualDamage = Mathf.Max(1, Mathf.RoundToInt(rawDamage * reduction));

            CurrentHp = Mathf.Max(0, CurrentHp - actualDamage);
            OnHpChanged?.Invoke(CurrentHp, MaxHp);

            if (CurrentHp <= 0)
            {
                Die();
            }
            else
            {
                _controller?.OnHit();
            }

            return actualDamage;
        }

        // --- Heal ---

        public void Heal(int amount)
        {
            if (IsDead || amount <= 0) return;

            int prevHp = CurrentHp;
            CurrentHp = Mathf.Min(CurrentHp + amount, MaxHp);

            if (CurrentHp != prevHp)
            {
                OnHpChanged?.Invoke(CurrentHp, MaxHp);

                GameEventSystem.Publish(new PlayerHealEvent
                {
                    Amount = CurrentHp - prevHp,
                    CurrentHp = CurrentHp
                });
            }
        }

        /// <summary>
        /// HP를 최대치로 회복.
        /// </summary>
        public void FullHeal()
        {
            Heal(MaxHp - CurrentHp);
        }

        // --- EXP / Level ---

        public void AddExp(int amount)
        {
            if (IsDead || amount <= 0) return;

            Exp += amount;
            while (Exp >= ExpToNextLevel)
            {
                Exp -= ExpToNextLevel;
                LevelUp();
            }
        }

        private void LevelUp()
        {
            Level++;
            RecalculateStats();

            // 레벨업 시 체력 전부 회복
            CurrentHp = MaxHp;
            OnHpChanged?.Invoke(CurrentHp, MaxHp);
            OnLevelUp?.Invoke(Level);
        }

        // --- Death ---

        private void Die()
        {
            OnDeath?.Invoke();
            _controller?.OnDeath();
        }

        // --- Save / Load ---

        public void LoadFromSave(PlayerStatsSave save, int level, int exp, int gold)
        {
            Level = Mathf.Max(1, level);
            Exp = exp;
            Gold = gold;

            _baseMaxHp = save.maxHp;
            _baseAttack = save.attack;
            _baseDefense = save.defense;
            _baseSpeed = save.speed;
            _baseCritRate = save.critRate;
            _baseCritDamage = save.critDamage;

            RecalculateStats();
            CurrentHp = MaxHp;
        }

        public PlayerStatsSave ToSaveData()
        {
            return new PlayerStatsSave
            {
                maxHp = _baseMaxHp,
                attack = _baseAttack,
                defense = _baseDefense,
                speed = _baseSpeed,
                critRate = _baseCritRate,
                critDamage = _baseCritDamage
            };
        }
    }
}
