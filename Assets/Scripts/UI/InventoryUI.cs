using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SoulCraft.Core;
using SoulCraft.Farming;

namespace SoulCraft.UI
{
    // ================================================================
    //  InventoryItem  (런타임 인벤토리 슬롯 데이터)
    // ================================================================

    /// <summary>
    /// 인벤토리에 보관 중인 아이템 한 칸의 런타임 데이터.
    /// </summary>
    [System.Serializable]
    public class InventoryItem
    {
        public ItemData Data;
        public int Quantity;

        public InventoryItem(ItemData data, int quantity)
        {
            Data = data;
            Quantity = Mathf.Clamp(quantity, 1, data != null ? data.maxStack : 99);
        }
    }

    // ================================================================
    //  ItemSlotUI  (그리드 한 칸의 UI 요소)
    // ================================================================

    /// <summary>
    /// 인벤토리 그리드의 개별 슬롯 UI.
    /// 아이콘, 수량 텍스트, 레어리티 테두리 색상을 표시한다.
    /// </summary>
    public class ItemSlotUI : MonoBehaviour
    {
        [SerializeField] private Image _iconImage;
        [SerializeField] private TMP_Text _quantityText;
        [SerializeField] private Image _borderImage;
        [SerializeField] private Button _button;

        private InventoryItem _item;
        private InventoryUI _parentUI;
        private int _slotIndex;

        // ── Rarity Colors ────────────────────────────────────
        private static readonly Dictionary<Rarity, Color> RarityColors = new()
        {
            { Rarity.Common,    new Color(0.7f, 0.7f, 0.7f) },
            { Rarity.Uncommon,  new Color(0.3f, 0.85f, 0.3f) },
            { Rarity.Rare,      new Color(0.3f, 0.5f, 1f) },
            { Rarity.Epic,      new Color(0.7f, 0.3f, 0.95f) },
            { Rarity.Legendary, new Color(1f, 0.7f, 0.1f) },
        };

        public void Initialize(InventoryUI parentUI, int slotIndex)
        {
            _parentUI = parentUI;
            _slotIndex = slotIndex;

            if (_button != null)
                _button.onClick.AddListener(OnSlotClicked);
        }

        /// <summary>
        /// 슬롯에 아이템 데이터를 표시한다.
        /// </summary>
        public void SetItem(InventoryItem item)
        {
            _item = item;

            if (item == null || item.Data == null)
            {
                ClearSlot();
                return;
            }

            // 아이콘
            if (_iconImage != null)
            {
                _iconImage.sprite = item.Data.icon;
                _iconImage.enabled = item.Data.icon != null;
                _iconImage.color = Color.white;
            }

            // 수량
            if (_quantityText != null)
            {
                bool showQuantity = item.Data.stackable && item.Quantity > 1;
                _quantityText.text = showQuantity ? item.Quantity.ToString() : "";
                _quantityText.enabled = showQuantity;
            }

            // 레어리티 테두리
            if (_borderImage != null)
            {
                _borderImage.color = RarityColors.TryGetValue(item.Data.rarity, out var c)
                    ? c
                    : Color.gray;
                _borderImage.enabled = true;
            }
        }

        /// <summary>
        /// 슬롯을 비운다.
        /// </summary>
        public void ClearSlot()
        {
            _item = null;

            if (_iconImage != null)
            {
                _iconImage.sprite = null;
                _iconImage.enabled = false;
            }

            if (_quantityText != null)
                _quantityText.enabled = false;

            if (_borderImage != null)
            {
                _borderImage.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
                _borderImage.enabled = true;
            }
        }

        private void OnSlotClicked()
        {
            if (_item != null && _parentUI != null)
                _parentUI.ShowItemDetail(_item, _slotIndex);
        }

        void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(OnSlotClicked);
        }
    }

    // ================================================================
    //  InventoryUI  (메인 인벤토리 화면)
    // ================================================================

    /// <summary>
    /// 그리드 레이아웃의 인벤토리 화면.
    /// 탭 필터(전체/장비/재료/소비), 아이템 상세 정보 팝업, 장착/사용 버튼.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        // ── Inspector: Grid ──────────────────────────────────
        [Header("Grid")]
        [SerializeField] private Transform _gridParent;
        [SerializeField] private GameObject _slotPrefab;
        [SerializeField] private int _maxSlots = 40;

        // ── Inspector: Tab Filter ────────────────────────────
        [Header("Tab Buttons")]
        [SerializeField] private Button _tabAll;
        [SerializeField] private Button _tabEquipment;
        [SerializeField] private Button _tabMaterial;
        [SerializeField] private Button _tabConsumable;
        [SerializeField] private Color _tabActiveColor = Color.white;
        [SerializeField] private Color _tabInactiveColor = new Color(0.6f, 0.6f, 0.6f);

        // ── Inspector: Detail Popup ──────────────────────────
        [Header("Item Detail Popup")]
        [SerializeField] private GameObject _detailPanel;
        [SerializeField] private Image _detailIcon;
        [SerializeField] private TMP_Text _detailNameText;
        [SerializeField] private TMP_Text _detailDescText;
        [SerializeField] private TMP_Text _detailStatsText;
        [SerializeField] private TMP_Text _detailRarityText;
        [SerializeField] private Button _equipButton;
        [SerializeField] private Button _useButton;
        [SerializeField] private Button _closeDetailButton;
        [SerializeField] private TMP_Text _equipButtonText;

        // ── Runtime ──────────────────────────────────────────

        /// <summary>
        /// 인벤토리 아이템 목록. 외부 시스템(InventoryManager)에서 설정.
        /// </summary>
        private List<InventoryItem> _allItems = new();
        private List<ItemSlotUI> _slotUIs = new();
        private ItemFilterTab _currentTab = ItemFilterTab.All;
        private InventoryItem _selectedItem;
        private int _selectedSlotIndex = -1;

        // 외부에서 장착/사용 처리를 위임받는 콜백
        public System.Action<InventoryItem, int> OnEquipRequested;
        public System.Action<InventoryItem, int> OnUseRequested;

        private enum ItemFilterTab
        {
            All,
            Equipment,
            Material,
            Consumable
        }

        // ============================================================
        //  Unity Lifecycle
        // ============================================================

        void Awake()
        {
            CreateSlots();
            SetupTabButtons();
            SetupDetailButtons();

            if (_detailPanel != null)
                _detailPanel.SetActive(false);
        }

        void OnEnable()
        {
            RefreshGrid();
        }

        // ============================================================
        //  Slot Creation
        // ============================================================

        private void CreateSlots()
        {
            if (_gridParent == null || _slotPrefab == null) return;

            for (int i = 0; i < _maxSlots; i++)
            {
                GameObject slotObj = Instantiate(_slotPrefab, _gridParent);
                var slotUI = slotObj.GetComponent<ItemSlotUI>();
                if (slotUI != null)
                {
                    slotUI.Initialize(this, i);
                    _slotUIs.Add(slotUI);
                }
            }
        }

        // ============================================================
        //  Tab Filter
        // ============================================================

        private void SetupTabButtons()
        {
            if (_tabAll != null)
                _tabAll.onClick.AddListener(() => SetFilter(ItemFilterTab.All));
            if (_tabEquipment != null)
                _tabEquipment.onClick.AddListener(() => SetFilter(ItemFilterTab.Equipment));
            if (_tabMaterial != null)
                _tabMaterial.onClick.AddListener(() => SetFilter(ItemFilterTab.Material));
            if (_tabConsumable != null)
                _tabConsumable.onClick.AddListener(() => SetFilter(ItemFilterTab.Consumable));
        }

        private void SetFilter(ItemFilterTab tab)
        {
            _currentTab = tab;
            UpdateTabVisuals();
            RefreshGrid();
        }

        private void UpdateTabVisuals()
        {
            SetTabColor(_tabAll, _currentTab == ItemFilterTab.All);
            SetTabColor(_tabEquipment, _currentTab == ItemFilterTab.Equipment);
            SetTabColor(_tabMaterial, _currentTab == ItemFilterTab.Material);
            SetTabColor(_tabConsumable, _currentTab == ItemFilterTab.Consumable);
        }

        private void SetTabColor(Button tab, bool active)
        {
            if (tab == null) return;
            var img = tab.GetComponent<Image>();
            if (img != null)
                img.color = active ? _tabActiveColor : _tabInactiveColor;
        }

        // ============================================================
        //  Grid Refresh
        // ============================================================

        /// <summary>
        /// 외부에서 아이템 목록을 설정한 후 그리드를 갱신한다.
        /// </summary>
        public void SetItems(List<InventoryItem> items)
        {
            _allItems = items ?? new List<InventoryItem>();
            RefreshGrid();
        }

        /// <summary>
        /// 현재 필터에 맞는 아이템으로 그리드를 갱신한다.
        /// </summary>
        public void RefreshGrid()
        {
            List<InventoryItem> filtered = GetFilteredItems();

            for (int i = 0; i < _slotUIs.Count; i++)
            {
                if (i < filtered.Count)
                    _slotUIs[i].SetItem(filtered[i]);
                else
                    _slotUIs[i].ClearSlot();
            }
        }

        private List<InventoryItem> GetFilteredItems()
        {
            if (_currentTab == ItemFilterTab.All)
                return _allItems;

            var filtered = new List<InventoryItem>();
            foreach (var item in _allItems)
            {
                if (item?.Data == null) continue;

                bool match = _currentTab switch
                {
                    ItemFilterTab.Equipment => item.Data.itemType == ItemType.Equipment,
                    ItemFilterTab.Material => item.Data.itemType == ItemType.Material,
                    ItemFilterTab.Consumable => item.Data.itemType == ItemType.Consumable,
                    _ => true
                };

                if (match) filtered.Add(item);
            }

            return filtered;
        }

        // ============================================================
        //  Item Detail Popup
        // ============================================================

        /// <summary>
        /// 아이템 상세 팝업을 표시한다.
        /// </summary>
        public void ShowItemDetail(InventoryItem item, int slotIndex)
        {
            if (item == null || item.Data == null || _detailPanel == null) return;

            _selectedItem = item;
            _selectedSlotIndex = slotIndex;
            _detailPanel.SetActive(true);

            var data = item.Data;

            // 아이콘
            if (_detailIcon != null)
            {
                _detailIcon.sprite = data.icon;
                _detailIcon.enabled = data.icon != null;
            }

            // 이름 (레어리티 색상)
            if (_detailNameText != null)
            {
                _detailNameText.text = data.itemName;
                _detailNameText.color = GetRarityTextColor(data.rarity);
            }

            // 레어리티
            if (_detailRarityText != null)
            {
                _detailRarityText.text = $"[{data.rarity}]";
                _detailRarityText.color = GetRarityTextColor(data.rarity);
            }

            // 설명
            if (_detailDescText != null)
                _detailDescText.text = data.description;

            // 스탯 (장비일 때)
            if (_detailStatsText != null)
            {
                if (data.itemType == ItemType.Equipment)
                    _detailStatsText.text = BuildEquipmentStatsString(data);
                else
                    _detailStatsText.text = $"판매가: {data.sellPrice}G";
            }

            // 버튼 표시 제어
            if (_equipButton != null)
            {
                bool isEquipment = data.itemType == ItemType.Equipment;
                _equipButton.gameObject.SetActive(isEquipment);
                if (_equipButtonText != null)
                    _equipButtonText.text = "장착";
            }

            if (_useButton != null)
            {
                bool isConsumable = data.itemType == ItemType.Consumable;
                _useButton.gameObject.SetActive(isConsumable);
            }
        }

        /// <summary>
        /// 상세 팝업을 닫는다.
        /// </summary>
        public void HideItemDetail()
        {
            _selectedItem = null;
            _selectedSlotIndex = -1;

            if (_detailPanel != null)
                _detailPanel.SetActive(false);
        }

        private void SetupDetailButtons()
        {
            if (_equipButton != null)
                _equipButton.onClick.AddListener(OnEquipButtonClicked);
            if (_useButton != null)
                _useButton.onClick.AddListener(OnUseButtonClicked);
            if (_closeDetailButton != null)
                _closeDetailButton.onClick.AddListener(HideItemDetail);
        }

        private void OnEquipButtonClicked()
        {
            if (_selectedItem != null)
            {
                OnEquipRequested?.Invoke(_selectedItem, _selectedSlotIndex);
                HideItemDetail();
            }
        }

        private void OnUseButtonClicked()
        {
            if (_selectedItem != null)
            {
                OnUseRequested?.Invoke(_selectedItem, _selectedSlotIndex);
                RefreshGrid();
                HideItemDetail();
            }
        }

        // ============================================================
        //  Helpers
        // ============================================================

        private string BuildEquipmentStatsString(ItemData data)
        {
            var sb = new System.Text.StringBuilder();

            if (data.bonusHp != 0) sb.AppendLine($"HP  +{data.bonusHp}");
            if (data.bonusAtk != 0) sb.AppendLine($"ATK +{data.bonusAtk}");
            if (data.bonusDef != 0) sb.AppendLine($"DEF +{data.bonusDef}");
            if (data.bonusSpeed != 0f) sb.AppendLine($"SPD +{data.bonusSpeed:F1}");
            if (data.bonusCritRate != 0f) sb.AppendLine($"CRIT +{data.bonusCritRate * 100f:F1}%");
            if (data.bonusCritDamage != 0f) sb.AppendLine($"CDMG +{data.bonusCritDamage * 100f:F0}%");
            if (data.element != DamageType.Physical) sb.AppendLine($"Element: {data.element}");

            sb.AppendLine($"판매가: {data.sellPrice}G");

            return sb.ToString().TrimEnd();
        }

        private Color GetRarityTextColor(Rarity rarity)
        {
            return rarity switch
            {
                Rarity.Common    => new Color(0.8f, 0.8f, 0.8f),
                Rarity.Uncommon  => new Color(0.3f, 0.9f, 0.3f),
                Rarity.Rare      => new Color(0.3f, 0.55f, 1f),
                Rarity.Epic      => new Color(0.75f, 0.35f, 1f),
                Rarity.Legendary => new Color(1f, 0.75f, 0.15f),
                _ => Color.white,
            };
        }

        void OnDestroy()
        {
            // 탭 버튼 리스너 해제
            if (_tabAll != null) _tabAll.onClick.RemoveAllListeners();
            if (_tabEquipment != null) _tabEquipment.onClick.RemoveAllListeners();
            if (_tabMaterial != null) _tabMaterial.onClick.RemoveAllListeners();
            if (_tabConsumable != null) _tabConsumable.onClick.RemoveAllListeners();

            // 상세 버튼 리스너 해제
            if (_equipButton != null) _equipButton.onClick.RemoveAllListeners();
            if (_useButton != null) _useButton.onClick.RemoveAllListeners();
            if (_closeDetailButton != null) _closeDetailButton.onClick.RemoveAllListeners();
        }
    }
}
