using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoulCraft.Farming
{
    /// <summary>
    /// 드롭 테이블 하나의 항목.
    /// </summary>
    [Serializable]
    public class LootEntry
    {
        public ItemData item;
        [Range(0f, 1f)]
        public float dropRate = 0.5f;
        [Min(1)]
        public int minQuantity = 1;
        [Min(1)]
        public int maxQuantity = 1;
    }

    /// <summary>
    /// 적/보스/상자 등에 할당하는 루트 테이블.
    /// Roll()을 호출하면 확률 판정 후 드롭 결과를 반환한다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewLootTable", menuName = "SoulCraft/Loot Table")]
    public class LootTable : ScriptableObject
    {
        [Header("Entries")]
        [SerializeField] private List<LootEntry> entries = new();

        [Header("Rarity Drop Rate Multipliers")]
        [Tooltip("Common 아이템의 드롭 확률 보정 배율")]
        [SerializeField] private float commonMultiplier = 1.0f;
        [SerializeField] private float uncommonMultiplier = 0.8f;
        [SerializeField] private float rareMultiplier = 0.5f;
        [SerializeField] private float epicMultiplier = 0.2f;
        [SerializeField] private float legendaryMultiplier = 0.05f;

        /// <summary>
        /// 드롭 판정을 수행하고 결과 리스트를 반환한다.
        /// </summary>
        public List<(ItemData item, int quantity)> Roll()
        {
            var results = new List<(ItemData, int)>();

            foreach (var entry in entries)
            {
                if (entry.item == null) continue;

                float adjustedRate = entry.dropRate * GetRarityMultiplier(entry.item.rarity);
                float roll = UnityEngine.Random.value;

                if (roll <= adjustedRate)
                {
                    int quantity = UnityEngine.Random.Range(entry.minQuantity, entry.maxQuantity + 1);
                    results.Add((entry.item, quantity));
                }
            }

            return results;
        }

        /// <summary>
        /// 레어리티별 드롭 확률 보정 배율을 반환한다.
        /// Legendary는 매우 낮은 배율을 적용하여 희귀성을 보장한다.
        /// </summary>
        private float GetRarityMultiplier(Rarity rarity)
        {
            return rarity switch
            {
                Rarity.Common    => commonMultiplier,
                Rarity.Uncommon  => uncommonMultiplier,
                Rarity.Rare      => rareMultiplier,
                Rarity.Epic      => epicMultiplier,
                Rarity.Legendary => legendaryMultiplier,
                _                => 1f
            };
        }
    }
}
