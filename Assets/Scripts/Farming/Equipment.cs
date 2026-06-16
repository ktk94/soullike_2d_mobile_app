using System;
using UnityEngine;
using SoulCraft.Player;

namespace SoulCraft.Farming
{
    /// <summary>
    /// 장비 보너스 스탯을 묶어 전달하기 위한 구조체.
    /// </summary>
    [Serializable]
    public struct BonusStats
    {
        public int hp;
        public int atk;
        public int def;
        public float speed;
        public float critRate;
        public float critDamage;

        public static BonusStats operator +(BonusStats a, BonusStats b)
        {
            return new BonusStats
            {
                hp        = a.hp + b.hp,
                atk       = a.atk + b.atk,
                def       = a.def + b.def,
                speed     = a.speed + b.speed,
                critRate  = a.critRate + b.critRate,
                critDamage = a.critDamage + b.critDamage
            };
        }

        public static BonusStats Zero => new();
    }

    /// <summary>
    /// 장비 관리 시스템.
    /// 4개의 장착 슬롯(Weapon, Armor, Accessory1, Accessory2)을 관리하고,
    /// 장착 변경 시 PlayerStats에 보너스를 반영한다.
    /// </summary>
    public class Equipment : MonoBehaviour
    {
        // ── Events ───────────────────────────────────────

        /// <summary>
        /// 장비가 변경되었을 때 발생한다. (변경된 슬롯 이름, 새 아이템 또는 null)
        /// </summary>
        public event Action<string, ItemData> OnEquipmentChanged;

        // ── Slots ────────────────────────────────────────

        public ItemData Weapon     { get; private set; }
        public ItemData Armor      { get; private set; }
        public ItemData Accessory1 { get; private set; }
        public ItemData Accessory2 { get; private set; }

        // ── Dependencies ─────────────────────────────────

        private PlayerStats _playerStats;

        void Awake()
        {
            _playerStats = GetComponent<PlayerStats>();
        }

        // ── Equip / Unequip ──────────────────────────────

        /// <summary>
        /// 아이템을 장착한다.
        /// Equipment 타입이 아니면 무시한다. Accessory는 빈 슬롯 우선, 둘 다 차있으면 Accessory1 교체.
        /// 기존 장비가 있으면 인벤토리로 돌아간다.
        /// </summary>
        /// <returns>장착 성공 여부</returns>
        public bool Equip(ItemData item)
        {
            if (item == null || item.itemType != ItemType.Equipment) return false;

            string slotName;

            switch (item.equipSlot)
            {
                case EquipSlot.Weapon:
                    UnequipToInventory(Weapon);
                    Weapon = item;
                    slotName = "Weapon";
                    break;

                case EquipSlot.Armor:
                    UnequipToInventory(Armor);
                    Armor = item;
                    slotName = "Armor";
                    break;

                case EquipSlot.Accessory:
                    if (Accessory1 == null)
                    {
                        Accessory1 = item;
                        slotName = "Accessory1";
                    }
                    else if (Accessory2 == null)
                    {
                        Accessory2 = item;
                        slotName = "Accessory2";
                    }
                    else
                    {
                        // 둘 다 차있으면 Accessory1 교체
                        UnequipToInventory(Accessory1);
                        Accessory1 = item;
                        slotName = "Accessory1";
                    }
                    break;

                default:
                    return false;
            }

            // 인벤토리에서 장착한 아이템 1개 제거
            Inventory.Instance?.RemoveItem(item, 1);

            ApplyBonusToStats();
            OnEquipmentChanged?.Invoke(slotName, item);
            return true;
        }

        /// <summary>
        /// 특정 슬롯의 장비를 해제하고 인벤토리로 돌려보낸다.
        /// </summary>
        public bool Unequip(string slotName)
        {
            ItemData removed = null;

            switch (slotName)
            {
                case "Weapon":
                    removed = Weapon;
                    Weapon = null;
                    break;
                case "Armor":
                    removed = Armor;
                    Armor = null;
                    break;
                case "Accessory1":
                    removed = Accessory1;
                    Accessory1 = null;
                    break;
                case "Accessory2":
                    removed = Accessory2;
                    Accessory2 = null;
                    break;
                default:
                    return false;
            }

            if (removed == null) return false;

            Inventory.Instance?.AddItem(removed, 1);
            ApplyBonusToStats();
            OnEquipmentChanged?.Invoke(slotName, null);
            return true;
        }

        // ── Stat Calculation ─────────────────────────────

        /// <summary>
        /// 장착된 모든 장비의 보너스 합산을 반환한다.
        /// </summary>
        public BonusStats GetTotalBonusStats()
        {
            BonusStats total = BonusStats.Zero;
            total = total + GetItemBonus(Weapon);
            total = total + GetItemBonus(Armor);
            total = total + GetItemBonus(Accessory1);
            total = total + GetItemBonus(Accessory2);
            return total;
        }

        /// <summary>
        /// 개별 아이템의 보너스 스탯을 반환한다.
        /// </summary>
        private BonusStats GetItemBonus(ItemData item)
        {
            if (item == null) return BonusStats.Zero;

            return new BonusStats
            {
                hp        = item.bonusHp,
                atk       = item.bonusAtk,
                def       = item.bonusDef,
                speed     = item.bonusSpeed,
                critRate  = item.bonusCritRate,
                critDamage = item.bonusCritDamage
            };
        }

        /// <summary>
        /// PlayerStats의 Bonus 필드에 장비 보너스를 반영하고 스탯을 재계산한다.
        /// </summary>
        private void ApplyBonusToStats()
        {
            if (_playerStats == null) return;

            BonusStats total = GetTotalBonusStats();

            _playerStats.BonusMaxHp     = total.hp;
            _playerStats.BonusAttack    = total.atk;
            _playerStats.BonusDefense   = total.def;
            _playerStats.BonusSpeed     = total.speed;
            _playerStats.BonusCritRate  = total.critRate;
            _playerStats.BonusCritDamage = total.critDamage;

            _playerStats.RecalculateStats();
        }

        // ── Helper ───────────────────────────────────────

        /// <summary>
        /// 기존 장비를 인벤토리로 되돌린다 (교체 시 사용).
        /// </summary>
        private void UnequipToInventory(ItemData item)
        {
            if (item == null) return;
            Inventory.Instance?.AddItem(item, 1);
        }
    }
}
