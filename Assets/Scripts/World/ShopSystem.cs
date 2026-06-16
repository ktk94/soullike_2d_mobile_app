using System;
using System.Collections.Generic;
using UnityEngine;
using SoulCraft.Core;
using SoulCraft.Farming;
using SoulCraft.Player;

namespace SoulCraft.World
{
    /// <summary>
    /// 상점에 진열되는 개별 상품 정보.
    /// </summary>
    [Serializable]
    public class ShopItem
    {
        public ItemData itemData;
        public int price;
        public int quantity;
        public bool isSold;
    }

    /// <summary>
    /// 던전 내 상점 NPC를 관리한다.
    /// 방 진입 시 랜덤 아이템을 진열하고, 플레이어가 골드로 구매할 수 있다.
    /// </summary>
    public class ShopSystem : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────

        [Header("Item Pools")]
        [Tooltip("상점에서 판매 가능한 소비 아이템 풀 (HP 포션 등)")]
        [SerializeField] private ItemData[] consumablePool;

        [Tooltip("상점에서 판매 가능한 장비 아이템 풀")]
        [SerializeField] private ItemData[] equipmentPool;

        [Header("Shop Settings")]
        [SerializeField] private int minItems = 3;
        [SerializeField] private int maxItems = 5;

        [Tooltip("장비 가격 배율 (sellPrice 기준)")]
        [SerializeField] private float equipmentPriceMultiplier = 3f;

        [Tooltip("소비 아이템 가격 배율")]
        [SerializeField] private float consumablePriceMultiplier = 2f;

        [Header("Rarity Weights")]
        [SerializeField] private float commonWeight = 50f;
        [SerializeField] private float uncommonWeight = 30f;
        [SerializeField] private float rareWeight = 15f;
        [SerializeField] private float epicWeight = 4f;
        [SerializeField] private float legendaryWeight = 1f;

        // ── Runtime ──────────────────────────────────────────

        public List<ShopItem> CurrentInventory { get; private set; } = new();
        public bool IsOpen { get; private set; }

        public event Action OnShopOpened;
        public event Action OnShopClosed;
        public event Action<ShopItem> OnItemPurchased;
        public event Action<string> OnPurchaseFailed; // 실패 사유

        // ── Public API ───────────────────────────────────────

        /// <summary>
        /// 상점을 열고 랜덤 상품을 진열한다.
        /// </summary>
        public void OpenShop()
        {
            if (IsOpen) return;

            IsOpen = true;
            GenerateInventory();
            OnShopOpened?.Invoke();
        }

        /// <summary>
        /// 상점을 닫는다.
        /// </summary>
        public void CloseShop()
        {
            if (!IsOpen) return;

            IsOpen = false;
            OnShopClosed?.Invoke();
        }

        /// <summary>
        /// 상품을 구매한다.
        /// </summary>
        public bool TryPurchase(int itemIndex, PlayerStats playerStats)
        {
            if (playerStats == null)
            {
                OnPurchaseFailed?.Invoke("플레이어 정보를 찾을 수 없습니다.");
                return false;
            }

            if (itemIndex < 0 || itemIndex >= CurrentInventory.Count)
            {
                OnPurchaseFailed?.Invoke("유효하지 않은 아이템입니다.");
                return false;
            }

            ShopItem shopItem = CurrentInventory[itemIndex];

            if (shopItem.isSold)
            {
                OnPurchaseFailed?.Invoke("이미 판매된 아이템입니다.");
                return false;
            }

            if (playerStats.Gold < shopItem.price)
            {
                OnPurchaseFailed?.Invoke("골드가 부족합니다.");
                return false;
            }

            // 골드 차감
            playerStats.Gold -= shopItem.price;
            shopItem.isSold = true;

            // 소비 아이템이면 즉시 효과 적용 (HP 포션인 경우)
            if (shopItem.itemData.itemType == ItemType.Consumable)
            {
                ApplyConsumableEffect(shopItem.itemData, playerStats);
            }

            // 아이템 획득 이벤트 발행
            GameEventSystem.Publish(new ItemDropEvent
            {
                ItemId = shopItem.itemData.itemId,
                Position = transform.position,
                Quantity = shopItem.quantity
            });

            OnItemPurchased?.Invoke(shopItem);
            return true;
        }

        /// <summary>
        /// 인벤토리를 강제로 새로고침한다.
        /// </summary>
        public void RefreshInventory()
        {
            GenerateInventory();
        }

        // ── Internal ─────────────────────────────────────────

        /// <summary>
        /// 랜덤으로 상점 상품을 생성한다.
        /// </summary>
        private void GenerateInventory()
        {
            CurrentInventory.Clear();

            int itemCount = UnityEngine.Random.Range(minItems, maxItems + 1);

            // 최소 1개는 HP 포션 보장
            ShopItem potionItem = CreateConsumableItem();
            if (potionItem != null)
                CurrentInventory.Add(potionItem);

            // 나머지는 장비/소비 랜덤
            for (int i = CurrentInventory.Count; i < itemCount; i++)
            {
                bool isEquipment = UnityEngine.Random.value > 0.4f; // 60% 확률로 장비

                if (isEquipment && equipmentPool != null && equipmentPool.Length > 0)
                {
                    ShopItem equipItem = CreateEquipmentItem();
                    if (equipItem != null)
                        CurrentInventory.Add(equipItem);
                }
                else
                {
                    ShopItem consumable = CreateConsumableItem();
                    if (consumable != null)
                        CurrentInventory.Add(consumable);
                }
            }
        }

        /// <summary>
        /// 소비 아이템 상품을 생성한다.
        /// </summary>
        private ShopItem CreateConsumableItem()
        {
            if (consumablePool == null || consumablePool.Length == 0) return null;

            ItemData item = consumablePool[UnityEngine.Random.Range(0, consumablePool.Length)];
            if (item == null) return null;

            return new ShopItem
            {
                itemData = item,
                price = Mathf.Max(1, Mathf.RoundToInt(item.sellPrice * consumablePriceMultiplier)),
                quantity = 1,
                isSold = false
            };
        }

        /// <summary>
        /// 장비 아이템 상품을 레어리티 가중 랜덤으로 생성한다.
        /// </summary>
        private ShopItem CreateEquipmentItem()
        {
            if (equipmentPool == null || equipmentPool.Length == 0) return null;

            ItemData item = PickEquipmentByRarityWeight();
            if (item == null) return null;

            // 레어리티에 따라 가격 조정
            float rarityMultiplier = GetRarityPriceMultiplier(item.rarity);
            int price = Mathf.Max(1, Mathf.RoundToInt(item.sellPrice * equipmentPriceMultiplier * rarityMultiplier));

            return new ShopItem
            {
                itemData = item,
                price = price,
                quantity = 1,
                isSold = false
            };
        }

        /// <summary>
        /// 레어리티 가중치를 적용해 장비를 선택한다.
        /// </summary>
        private ItemData PickEquipmentByRarityWeight()
        {
            // 레어리티별로 장비를 분류
            var buckets = new Dictionary<Rarity, List<ItemData>>();
            foreach (var item in equipmentPool)
            {
                if (item == null) continue;
                if (!buckets.ContainsKey(item.rarity))
                    buckets[item.rarity] = new List<ItemData>();
                buckets[item.rarity].Add(item);
            }

            // 가중 랜덤으로 레어리티 선택
            float totalWeight = 0f;
            var weightedRarities = new List<(Rarity rarity, float weight)>();

            foreach (var kvp in buckets)
            {
                float w = GetRarityWeight(kvp.Key);
                weightedRarities.Add((kvp.Key, w));
                totalWeight += w;
            }

            if (totalWeight <= 0f || weightedRarities.Count == 0)
            {
                // fallback: 아무 장비나 반환
                return equipmentPool[UnityEngine.Random.Range(0, equipmentPool.Length)];
            }

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float cumulative = 0f;

            foreach (var (rarity, weight) in weightedRarities)
            {
                cumulative += weight;
                if (roll <= cumulative)
                {
                    var list = buckets[rarity];
                    return list[UnityEngine.Random.Range(0, list.Count)];
                }
            }

            return equipmentPool[UnityEngine.Random.Range(0, equipmentPool.Length)];
        }

        /// <summary>
        /// 레어리티에 대응하는 선택 가중치를 반환한다.
        /// </summary>
        private float GetRarityWeight(Rarity rarity)
        {
            return rarity switch
            {
                Rarity.Common    => commonWeight,
                Rarity.Uncommon  => uncommonWeight,
                Rarity.Rare      => rareWeight,
                Rarity.Epic      => epicWeight,
                Rarity.Legendary => legendaryWeight,
                _                => commonWeight
            };
        }

        /// <summary>
        /// 레어리티에 따른 가격 배율을 반환한다.
        /// </summary>
        private float GetRarityPriceMultiplier(Rarity rarity)
        {
            return rarity switch
            {
                Rarity.Common    => 1.0f,
                Rarity.Uncommon  => 1.5f,
                Rarity.Rare      => 2.5f,
                Rarity.Epic      => 4.0f,
                Rarity.Legendary => 8.0f,
                _                => 1.0f
            };
        }

        /// <summary>
        /// 소비 아이템 효과를 즉시 적용한다.
        /// </summary>
        private void ApplyConsumableEffect(ItemData item, PlayerStats stats)
        {
            // HP 포션 처리: bonusHp 필드를 회복량으로 활용
            if (item.bonusHp > 0)
            {
                stats.Heal(item.bonusHp);
            }
        }
    }
}
