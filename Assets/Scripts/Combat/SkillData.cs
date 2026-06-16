using UnityEngine;
using SoulCraft.Core;

namespace SoulCraft.Combat
{
    public enum SkillType
    {
        Melee,
        Ranged,
        AoE,
        Buff
    }

    [CreateAssetMenu(fileName = "NewSkill", menuName = "SoulCraft/Skill Data")]
    public class SkillData : ScriptableObject
    {
        [Header("Basic Info")]
        public string skillId;
        public string skillName;
        [TextArea(2, 4)]
        public string description;
        public Sprite icon;

        [Header("Stat")]
        public float damageMultiplier = 1f;
        public float cooldown = 1f;
        public int manaCost;
        public float range = 1.5f;
        public float aoeRadius;

        [Header("Duration / Buff")]
        [Tooltip("버프/장판 등 지속 시간 (초)")]
        public float duration;

        [Header("Type")]
        public DamageType element = DamageType.Physical;
        public SkillType skillType = SkillType.Melee;

        [Header("Visuals")]
        public GameObject effectPrefab;
        public GameObject hitEffectPrefab;

        [Header("Combo")]
        [Tooltip("이 스킬이 속하는 콤보 태그 (예: Fire, Wind, BasicAttack, HeavyAttack)")]
        public string[] comboTags;
    }
}
