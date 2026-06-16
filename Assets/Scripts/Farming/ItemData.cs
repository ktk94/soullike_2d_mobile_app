using UnityEngine;
using SoulCraft.Core;

namespace SoulCraft.Farming
{
    // ── Enums ────────────────────────────────────────────

    public enum ItemType
    {
        Material,
        Equipment,
        Consumable,
        KeyItem
    }

    public enum Rarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    public enum EquipSlot
    {
        Weapon,
        Armor,
        Accessory
    }

    // ── ScriptableObject ─────────────────────────────────

    /// <summary>
    /// 아이템 기본 데이터. Inspector에서 에셋으로 생성한다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewItemData", menuName = "SoulCraft/Item Data")]
    public class ItemData : ScriptableObject
    {
        [Header("Identity")]
        public string itemId;
        public string itemName;
        [TextArea(2, 4)]
        public string description;
        public Sprite icon;

        [Header("Classification")]
        public ItemType itemType;
        public Rarity rarity;

        [Header("Stacking")]
        public bool stackable = true;
        [Min(1)]
        public int maxStack = 99;

        [Header("Economy")]
        [Min(0)]
        public int sellPrice;

        // ── Equipment-Only Fields ────────────────────────

        [Header("Equipment (Only for ItemType.Equipment)")]
        public EquipSlot equipSlot;
        public int bonusHp;
        public int bonusAtk;
        public int bonusDef;
        public float bonusSpeed;
        [Range(0f, 1f)]
        public float bonusCritRate;
        public float bonusCritDamage;
        public DamageType element = DamageType.Physical;
    }
}
