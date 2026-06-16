using System;
using UnityEngine;
using SoulCraft.Farming;

namespace SoulCraft.Passive
{
    // ── Enums ────────────────────────────────────────────

    public enum PassiveStatType
    {
        MaxHp,
        Attack,
        Defense,
        Speed,
        CritRate,
        CritDamage,
        DodgeCooldown,
        SkillCooldownReduction,
        LifeSteal,
        DamageReduction,
        ExpBonus,
        GoldBonus,
        ElementalDamageBonus,
        ComboWindowExtend,
        StaggerDamageBonus
    }

    public enum PassiveCategory
    {
        Offense,
        Defense,
        Utility,
        Farming
    }

    // ── Structs ──────────────────────────────────────────

    /// <summary>
    /// 패시브 효과 하나. 스탯 타입, 수치, 퍼센트 여부를 정의한다.
    /// </summary>
    [Serializable]
    public struct PassiveEffect
    {
        [Tooltip("영향을 줄 스탯 타입")]
        public PassiveStatType statType;

        [Tooltip("효과 수치 (퍼센트일 경우 0.05 = 5%)")]
        public float value;

        [Tooltip("true면 퍼센트 보너스, false면 고정값 보너스")]
        public bool isPercentage;
    }

    /// <summary>
    /// 패시브 해금/강화에 필요한 재료 하나.
    /// </summary>
    [Serializable]
    public struct PassiveCost
    {
        [Tooltip("필요한 아이템")]
        public ItemData item;

        [Tooltip("필요 수량")]
        public int quantity;
    }

    // ── ScriptableObject ─────────────────────────────────

    /// <summary>
    /// 패시브 스킬 정의 데이터.
    /// Inspector에서 에셋으로 생성하거나, PassiveManager가 JSON으로부터 런타임 생성한다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewPassive", menuName = "SoulCraft/Passive Data")]
    public class PassiveData : ScriptableObject
    {
        [Header("Identity")]
        public string passiveId;
        public string passiveName;
        [TextArea(2, 4)]
        public string description;
        public Sprite icon;

        [Header("Level")]
        [Range(1, 5)]
        public int maxLevel = 5;

        [Header("Effects per Level")]
        [Tooltip("인덱스 0 = Lv1 효과, 인덱스 1 = Lv2 효과, ...")]
        public PassiveEffect[] effectPerLevel;

        [Header("Unlock Cost per Level")]
        [Tooltip("인덱스 0 = Lv1 해금 비용, 인덱스 1 = Lv2 비용, ...")]
        public PassiveCost[] unlockCost;

        [Header("Gold Cost per Level")]
        [Tooltip("인덱스 0 = Lv1 골드 비용, 인덱스 1 = Lv2 비용, ...")]
        public int[] goldCost;

        [Header("Prerequisites")]
        [Tooltip("이 패시브를 해금하려면 먼저 해금해야 하는 패시브 목록")]
        public PassiveData[] prerequisites;

        [Header("Prerequisite Levels")]
        [Tooltip("prerequisites 배열과 1:1 대응. 각 선행 패시브에 필요한 최소 레벨")]
        public int[] prerequisiteLevels;

        [Header("Category")]
        public PassiveCategory category;

        // ── Helper Methods ──────────────────────────────

        /// <summary>
        /// 지정 레벨의 효과를 반환한다. level은 1-based.
        /// </summary>
        public PassiveEffect GetEffect(int level)
        {
            int index = Mathf.Clamp(level - 1, 0, effectPerLevel.Length - 1);
            return effectPerLevel[index];
        }

        /// <summary>
        /// 지정 레벨의 해금 비용(재료)을 반환한다. level은 1-based.
        /// </summary>
        public PassiveCost GetUnlockCost(int level)
        {
            int index = Mathf.Clamp(level - 1, 0, unlockCost != null ? unlockCost.Length - 1 : 0);
            return unlockCost != null && unlockCost.Length > 0 ? unlockCost[index] : default;
        }

        /// <summary>
        /// 지정 레벨의 골드 비용을 반환한다. level은 1-based.
        /// </summary>
        public int GetGoldCost(int level)
        {
            int index = Mathf.Clamp(level - 1, 0, goldCost != null ? goldCost.Length - 1 : 0);
            return goldCost != null && goldCost.Length > 0 ? goldCost[index] : 0;
        }

        /// <summary>
        /// 선행 패시브의 필요 최소 레벨을 반환한다.
        /// prerequisiteLevels 배열이 없거나 짧으면 기본값 1을 반환한다.
        /// </summary>
        public int GetPrerequisiteLevel(int index)
        {
            if (prerequisiteLevels != null && index >= 0 && index < prerequisiteLevels.Length)
                return prerequisiteLevels[index];
            return 1;
        }
    }
}
