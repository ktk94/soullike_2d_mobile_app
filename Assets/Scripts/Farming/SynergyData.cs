using System;
using UnityEngine;
using SoulCraft.Core;
using SoulCraft.Combat;

namespace SoulCraft.Farming
{
    // ── Enums ────────────────────────────────────────────

    /// <summary>
    /// 시너지 조합의 카테고리.
    /// </summary>
    public enum SynergyType
    {
        OffensiveCombo,   // 공격형 연계
        DefensiveCombo,   // 방어형 연계
        UtilityCombo      // 유틸리티형 연계
    }

    // ── Ingredient ──────────────────────────────────────

    /// <summary>
    /// 시너지 조합에 필요한 재료 하나를 정의한다.
    /// </summary>
    [Serializable]
    public class SynergyIngredient
    {
        [Tooltip("필요한 아이템 데이터 참조")]
        public ItemData item;

        [Tooltip("필요 수량")]
        [Min(1)]
        public int requiredQuantity = 1;
    }

    // ── ScriptableObject ────────────────────────────────

    /// <summary>
    /// 시너지(연계 스킬) 레시피 데이터.
    /// 특정 아이템 조합을 보유하면 연계 스킬이 해금된다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSynergyData", menuName = "SoulCraft/Synergy Data")]
    public class SynergyData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("시너지 고유 ID")]
        public string synergyId;

        [Tooltip("시너지 이름 (UI 표시용)")]
        public string synergyName;

        [TextArea(2, 5)]
        [Tooltip("시너지 설명")]
        public string description;

        [Tooltip("시너지 아이콘")]
        public Sprite icon;

        [Header("Recipe")]
        [Tooltip("시너지 조합에 필요한 재료 목록")]
        public SynergyIngredient[] requiredItems;

        [Header("Result")]
        [Tooltip("해금되는 연계 스킬")]
        public SkillData resultSkill;

        [Tooltip("시너지 분류")]
        public SynergyType synergyType = SynergyType.OffensiveCombo;

        [Header("Bonus")]
        [TextArea(1, 3)]
        [Tooltip("추가 효과 설명")]
        public string bonusEffect;

        [Tooltip("해금 시 표시할 알림 메시지")]
        public string unlockMessage;

        [Header("Visuals")]
        [Tooltip("시너지 스킬 발동 시 추가 이펙트 프리팹")]
        public GameObject synergyEffectPrefab;

        [Tooltip("컷인 연출에 표시할 배경 색상")]
        public Color cutInColor = new Color(1f, 0.6f, 0.1f, 1f);

        // ── Runtime Helpers ─────────────────────────────

        /// <summary>
        /// 필요 재료 수를 반환한다.
        /// </summary>
        public int IngredientCount =>
            requiredItems != null ? requiredItems.Length : 0;

        /// <summary>
        /// 인벤토리에 모든 재료가 충분한지 검사한다.
        /// 재료를 소모하지 않는다 (보유 여부만 확인).
        /// </summary>
        public bool CheckRequirements(Inventory inventory)
        {
            if (inventory == null || requiredItems == null) return false;

            foreach (var ingredient in requiredItems)
            {
                if (ingredient.item == null) continue;

                if (!inventory.HasItem(ingredient.item, ingredient.requiredQuantity))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 특정 아이템이 이 시너지의 재료인지 확인한다.
        /// </summary>
        public bool ContainsIngredient(ItemData item)
        {
            if (requiredItems == null || item == null) return false;

            foreach (var ingredient in requiredItems)
            {
                if (ingredient.item == item) return true;
            }

            return false;
        }
    }
}
