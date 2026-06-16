using System;
using UnityEngine;
using SoulCraft.Player;

namespace SoulCraft.Farming
{
    /// <summary>
    /// 장비 강화 결과.
    /// </summary>
    public struct UpgradeResult
    {
        public bool Success;
        public int NewLevel;
        public int GoldCost;
        public int MaterialCost;
    }

    /// <summary>
    /// 장비 강화 시스템.
    /// +1 ~ +10 단계, 단계별 성공 확률 감소.
    /// 캐주얼 게임이므로 실패해도 파괴 없음.
    /// </summary>
    public class UpgradeSystem : MonoBehaviour
    {
        public static UpgradeSystem Instance { get; private set; }

        // ── Settings ─────────────────────────────────────

        [Header("Upgrade Settings")]
        [SerializeField] private int maxUpgradeLevel = 10;

        [Header("Success Rates per Level (index 0 = +0 -> +1)")]
        [SerializeField] private float[] successRates = new float[]
        {
            1.00f,  // +0 -> +1 : 100%
            0.95f,  // +1 -> +2 : 95%
            0.90f,  // +2 -> +3 : 90%
            0.80f,  // +3 -> +4 : 80%
            0.70f,  // +4 -> +5 : 70%
            0.55f,  // +5 -> +6 : 55%
            0.40f,  // +6 -> +7 : 40%
            0.30f,  // +7 -> +8 : 30%
            0.20f,  // +8 -> +9 : 20%
            0.10f   // +9 -> +10 : 10%
        };

        [Header("Gold Cost per Level (index 0 = +0 -> +1)")]
        [SerializeField] private int[] goldCosts = new int[]
        {
            100, 200, 400, 800, 1500,
            2500, 4000, 6000, 9000, 15000
        };

        [Header("Material Cost per Level")]
        [SerializeField] private int[] materialCosts = new int[]
        {
            1, 1, 2, 2, 3,
            3, 4, 5, 7, 10
        };

        [Header("Stat Bonus per Level (%)")]
        [Tooltip("강화 단계당 기본 스탯 증가율 (10 = 10%)")]
        [SerializeField] private float statBonusPerLevel = 10f;

        [Header("Required Material")]
        [Tooltip("강화에 필요한 재료 아이템")]
        [SerializeField] private ItemData upgradeMaterial;

        // ── Events ───────────────────────────────────────

        /// <summary>
        /// 강화 시도 결과 이벤트.
        /// </summary>
        public event Action<UpgradeResult> OnUpgradeAttempted;

        // ── Runtime ──────────────────────────────────────

        // 아이템별 강화 단계 저장 (itemId -> level)
        private readonly System.Collections.Generic.Dictionary<string, int> _upgradeLevels = new();

        // ── Lifecycle ────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Public API ───────────────────────────────────

        /// <summary>
        /// 현재 아이템의 강화 단계를 반환한다.
        /// </summary>
        public int GetUpgradeLevel(ItemData item)
        {
            if (item == null) return 0;
            return _upgradeLevels.TryGetValue(item.itemId, out int level) ? level : 0;
        }

        /// <summary>
        /// 강화가 가능한지 확인한다 (최대 레벨, 재료, 골드).
        /// </summary>
        public bool CanUpgrade(ItemData item, PlayerStats playerStats)
        {
            if (item == null || item.itemType != ItemType.Equipment) return false;
            if (playerStats == null) return false;

            int currentLevel = GetUpgradeLevel(item);
            if (currentLevel >= maxUpgradeLevel) return false;

            int goldCost = GetGoldCost(currentLevel);
            int matCost = GetMaterialCost(currentLevel);

            if (playerStats.Gold < goldCost) return false;
            if (upgradeMaterial != null && !Inventory.Instance.HasItem(upgradeMaterial, matCost)) return false;

            return true;
        }

        /// <summary>
        /// 장비 강화를 시도한다.
        /// 재료와 골드는 시도 시 소모되며, 실패해도 돌려주지 않는다.
        /// 실패해도 장비가 파괴되지 않는다 (캐주얼).
        /// </summary>
        public UpgradeResult TryUpgrade(ItemData item, PlayerStats playerStats)
        {
            var result = new UpgradeResult();

            if (!CanUpgrade(item, playerStats))
            {
                result.Success = false;
                result.NewLevel = GetUpgradeLevel(item);
                OnUpgradeAttempted?.Invoke(result);
                return result;
            }

            int currentLevel = GetUpgradeLevel(item);
            int goldCost = GetGoldCost(currentLevel);
            int matCost = GetMaterialCost(currentLevel);

            // 재료 소모
            playerStats.Gold -= goldCost;
            if (upgradeMaterial != null)
                Inventory.Instance.RemoveItem(upgradeMaterial, matCost);

            result.GoldCost = goldCost;
            result.MaterialCost = matCost;

            // 성공 판정
            float rate = GetSuccessRate(currentLevel);
            bool success = UnityEngine.Random.value <= rate;

            if (success)
            {
                int newLevel = currentLevel + 1;
                _upgradeLevels[item.itemId] = newLevel;
                result.Success = true;
                result.NewLevel = newLevel;
            }
            else
            {
                // 실패: 아무 일도 없음 (파괴 없음, 캐주얼)
                result.Success = false;
                result.NewLevel = currentLevel;
            }

            OnUpgradeAttempted?.Invoke(result);
            return result;
        }

        /// <summary>
        /// 강화 단계에 따라 적용되는 스탯 보너스 배율을 반환한다.
        /// 예: +5 이면 1.5 (50% 증가)
        /// </summary>
        public float GetStatMultiplier(ItemData item)
        {
            int level = GetUpgradeLevel(item);
            return 1f + (level * statBonusPerLevel / 100f);
        }

        /// <summary>
        /// 강화 보너스가 적용된 아이템의 실제 보너스 스탯을 반환한다.
        /// </summary>
        public BonusStats GetUpgradedStats(ItemData item)
        {
            if (item == null) return BonusStats.Zero;

            float multiplier = GetStatMultiplier(item);

            return new BonusStats
            {
                hp        = Mathf.RoundToInt(item.bonusHp * multiplier),
                atk       = Mathf.RoundToInt(item.bonusAtk * multiplier),
                def       = Mathf.RoundToInt(item.bonusDef * multiplier),
                speed     = item.bonusSpeed * multiplier,
                critRate  = item.bonusCritRate * multiplier,
                critDamage = item.bonusCritDamage * multiplier
            };
        }

        /// <summary>
        /// 강화 단계를 직접 설정한다 (세이브/로드용).
        /// </summary>
        public void SetUpgradeLevel(string itemId, int level)
        {
            _upgradeLevels[itemId] = Mathf.Clamp(level, 0, maxUpgradeLevel);
        }

        // ── Cost / Rate Queries ──────────────────────────

        public int GetGoldCost(int currentLevel)
        {
            if (currentLevel < 0 || currentLevel >= goldCosts.Length) return int.MaxValue;
            return goldCosts[currentLevel];
        }

        public int GetMaterialCost(int currentLevel)
        {
            if (currentLevel < 0 || currentLevel >= materialCosts.Length) return int.MaxValue;
            return materialCosts[currentLevel];
        }

        public float GetSuccessRate(int currentLevel)
        {
            if (currentLevel < 0 || currentLevel >= successRates.Length) return 0f;
            return successRates[currentLevel];
        }
    }
}
