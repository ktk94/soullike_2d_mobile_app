using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SoulCraft.Core;
using SoulCraft.Farming;

namespace SoulCraft.Passive
{
    /// <summary>
    /// 패시브 스킬 트리 UI.
    /// 4개 카테고리 탭, 패시브 노드 표시, 선행 관계 연결선, 상세 패널을 관리한다.
    /// </summary>
    public class PassiveTreeUI : MonoBehaviour
    {
        // ── References ─────────────────────────────────────

        [Header("Category Tabs")]
        [SerializeField] private Button _tabOffense;
        [SerializeField] private Button _tabDefense;
        [SerializeField] private Button _tabUtility;
        [SerializeField] private Button _tabFarming;

        [Header("Tab Colors")]
        [SerializeField] private Color _activeTabColor = new Color(1f, 0.85f, 0.3f, 1f);
        [SerializeField] private Color _inactiveTabColor = new Color(0.6f, 0.6f, 0.6f, 1f);

        [Header("Node Container")]
        [Tooltip("패시브 노드들이 배치되는 부모 Transform")]
        [SerializeField] private RectTransform _nodeContainer;

        [Header("Node Prefab")]
        [SerializeField] private GameObject _nodeSlotPrefab;

        [Header("Connection Line")]
        [Tooltip("노드 간 연결선을 그리는 부모 Transform (노드 아래에 렌더링)")]
        [SerializeField] private RectTransform _lineContainer;
        [SerializeField] private GameObject _linePrefab;

        [Header("Detail Panel")]
        [SerializeField] private GameObject _detailPanel;
        [SerializeField] private Image _detailIcon;
        [SerializeField] private TextMeshProUGUI _detailName;
        [SerializeField] private TextMeshProUGUI _detailDescription;
        [SerializeField] private TextMeshProUGUI _detailCurrentEffect;
        [SerializeField] private TextMeshProUGUI _detailNextEffect;
        [SerializeField] private TextMeshProUGUI _detailCostText;
        [SerializeField] private TextMeshProUGUI _detailGoldCostText;
        [SerializeField] private TextMeshProUGUI _detailFailReasonText;
        [SerializeField] private Button _unlockButton;
        [SerializeField] private TextMeshProUGUI _unlockButtonText;

        [Header("Node Colors")]
        [SerializeField] private Color _lockedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color _unlockedColor = new Color(0.2f, 0.8f, 0.4f, 1f);
        [SerializeField] private Color _maxLevelColor = new Color(1f, 0.85f, 0.1f, 1f);
        [SerializeField] private Color _canUnlockColor = new Color(0.4f, 0.6f, 1f, 1f);

        [Header("Line Colors")]
        [SerializeField] private Color _lineActiveColor = new Color(0.2f, 0.8f, 0.4f, 0.8f);
        [SerializeField] private Color _lineInactiveColor = new Color(0.4f, 0.4f, 0.4f, 0.4f);

        // ── State ──────────────────────────────────────────

        private PassiveCategory _currentCategory = PassiveCategory.Offense;
        private PassiveData _selectedPassive;

        /// <summary>노드 슬롯: passiveId -> (슬롯 오브젝트, RectTransform)</summary>
        private readonly Dictionary<string, PassiveNodeSlot> _nodeSlots = new();

        /// <summary>연결선 오브젝트 목록 (카테고리 전환 시 파괴)</summary>
        private readonly List<GameObject> _lineObjects = new();

        // ── Lifecycle ──────────────────────────────────────

        void OnEnable()
        {
            // 탭 버튼 이벤트
            _tabOffense?.onClick.AddListener(() => SwitchCategory(PassiveCategory.Offense));
            _tabDefense?.onClick.AddListener(() => SwitchCategory(PassiveCategory.Defense));
            _tabUtility?.onClick.AddListener(() => SwitchCategory(PassiveCategory.Utility));
            _tabFarming?.onClick.AddListener(() => SwitchCategory(PassiveCategory.Farming));

            // 해금 버튼
            _unlockButton?.onClick.AddListener(OnUnlockButtonClicked);

            // 패시브 변경 이벤트 구독
            if (PassiveManager.Instance != null)
                PassiveManager.Instance.OnPassiveUnlocked += OnPassiveChanged;

            // 상세 패널 숨김
            if (_detailPanel != null)
                _detailPanel.SetActive(false);

            RefreshUI();
        }

        void OnDisable()
        {
            _tabOffense?.onClick.RemoveAllListeners();
            _tabDefense?.onClick.RemoveAllListeners();
            _tabUtility?.onClick.RemoveAllListeners();
            _tabFarming?.onClick.RemoveAllListeners();
            _unlockButton?.onClick.RemoveAllListeners();

            if (PassiveManager.Instance != null)
                PassiveManager.Instance.OnPassiveUnlocked -= OnPassiveChanged;
        }

        // ── Public API ─────────────────────────────────────

        /// <summary>
        /// 전체 UI를 새로고침한다.
        /// </summary>
        public void RefreshUI()
        {
            UpdateTabVisuals();
            RebuildNodes();
            RebuildLines();
            RefreshDetailPanel();
        }

        /// <summary>
        /// 카테고리를 전환한다.
        /// </summary>
        public void SwitchCategory(PassiveCategory category)
        {
            _currentCategory = category;
            _selectedPassive = null;
            RefreshUI();
        }

        // ── Tab Visuals ────────────────────────────────────

        private void UpdateTabVisuals()
        {
            SetTabColor(_tabOffense, _currentCategory == PassiveCategory.Offense);
            SetTabColor(_tabDefense, _currentCategory == PassiveCategory.Defense);
            SetTabColor(_tabUtility, _currentCategory == PassiveCategory.Utility);
            SetTabColor(_tabFarming, _currentCategory == PassiveCategory.Farming);
        }

        private void SetTabColor(Button tab, bool active)
        {
            if (tab == null) return;
            var img = tab.GetComponent<Image>();
            if (img != null)
                img.color = active ? _activeTabColor : _inactiveTabColor;
        }

        // ── Node Building ──────────────────────────────────

        /// <summary>
        /// 현재 카테고리의 패시브 노드를 생성/갱신한다.
        /// </summary>
        private void RebuildNodes()
        {
            // 기존 노드 제거
            ClearNodes();

            if (PassiveManager.Instance == null) return;

            var passives = PassiveManager.Instance.GetPassivesByCategory(_currentCategory);

            for (int i = 0; i < passives.Count; i++)
            {
                var data = passives[i];
                CreateNodeSlot(data, i);
            }
        }

        /// <summary>
        /// 패시브 노드 슬롯 하나를 생성한다.
        /// </summary>
        private void CreateNodeSlot(PassiveData data, int index)
        {
            if (_nodeSlotPrefab == null || _nodeContainer == null) return;

            GameObject slotObj = Instantiate(_nodeSlotPrefab, _nodeContainer);
            slotObj.name = $"Node_{data.passiveId}";

            var slot = new PassiveNodeSlot
            {
                GameObject = slotObj,
                RectTransform = slotObj.GetComponent<RectTransform>(),
                PassiveData = data
            };

            // 아이콘 설정
            var iconImg = slotObj.transform.Find("Icon")?.GetComponent<Image>();
            if (iconImg != null && data.icon != null)
                iconImg.sprite = data.icon;

            // 레벨 텍스트
            var levelText = slotObj.transform.Find("LevelText")?.GetComponent<TextMeshProUGUI>();
            int currentLevel = PassiveManager.Instance.GetPassiveLevel(data.passiveId);
            if (levelText != null)
                levelText.text = $"{currentLevel}/{data.maxLevel}";

            // 이름 텍스트
            var nameText = slotObj.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            if (nameText != null)
                nameText.text = data.passiveName;

            // 배경색 (해금 상태에 따라)
            var bgImg = slotObj.GetComponent<Image>();
            if (bgImg == null)
                bgImg = slotObj.transform.Find("Background")?.GetComponent<Image>();

            if (bgImg != null)
            {
                if (currentLevel >= data.maxLevel)
                    bgImg.color = _maxLevelColor;
                else if (currentLevel > 0)
                    bgImg.color = _unlockedColor;
                else if (PassiveManager.Instance.CanUnlock(data.passiveId))
                    bgImg.color = _canUnlockColor;
                else
                    bgImg.color = _lockedColor;
            }

            // 클릭 이벤트
            var button = slotObj.GetComponent<Button>();
            if (button == null)
                button = slotObj.AddComponent<Button>();

            var capturedData = data;
            button.onClick.AddListener(() => OnNodeClicked(capturedData));

            _nodeSlots[data.passiveId] = slot;
        }

        /// <summary>
        /// 모든 노드 슬롯을 제거한다.
        /// </summary>
        private void ClearNodes()
        {
            foreach (var kvp in _nodeSlots)
            {
                if (kvp.Value.GameObject != null)
                    Destroy(kvp.Value.GameObject);
            }
            _nodeSlots.Clear();
        }

        // ── Connection Lines ───────────────────────────────

        /// <summary>
        /// 선행 관계 연결선을 다시 그린다.
        /// </summary>
        private void RebuildLines()
        {
            ClearLines();

            if (PassiveManager.Instance == null || _lineContainer == null || _linePrefab == null)
                return;

            foreach (var kvp in _nodeSlots)
            {
                var data = kvp.Value.PassiveData;
                if (data.prerequisites == null) continue;

                foreach (var prereq in data.prerequisites)
                {
                    if (prereq == null) continue;
                    if (!_nodeSlots.TryGetValue(prereq.passiveId, out var fromSlot)) continue;

                    var toSlot = kvp.Value;
                    CreateConnectionLine(fromSlot, toSlot, prereq.passiveId);
                }
            }
        }

        /// <summary>
        /// 두 노드 사이의 연결선을 생성한다.
        /// </summary>
        private void CreateConnectionLine(PassiveNodeSlot from, PassiveNodeSlot to, string prereqId)
        {
            if (_linePrefab == null || _lineContainer == null) return;

            GameObject lineObj = Instantiate(_linePrefab, _lineContainer);
            lineObj.name = $"Line_{prereqId}_to_{to.PassiveData.passiveId}";

            var lineRect = lineObj.GetComponent<RectTransform>();
            if (lineRect == null) return;

            // 두 노드의 위치로부터 선의 위치와 크기를 계산
            Vector2 fromPos = from.RectTransform.anchoredPosition;
            Vector2 toPos = to.RectTransform.anchoredPosition;
            Vector2 direction = toPos - fromPos;
            float distance = direction.magnitude;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            // 선의 중심을 두 노드의 중간에 배치
            lineRect.anchoredPosition = fromPos + direction * 0.5f;
            lineRect.sizeDelta = new Vector2(distance, 4f); // 4px 두께
            lineRect.localRotation = Quaternion.Euler(0f, 0f, angle);

            // 색상 설정 (선행 패시브가 해금되었으면 활성 색상)
            var lineImg = lineObj.GetComponent<Image>();
            if (lineImg != null)
            {
                int prereqLevel = PassiveManager.Instance.GetPassiveLevel(prereqId);
                lineImg.color = prereqLevel > 0 ? _lineActiveColor : _lineInactiveColor;
            }

            _lineObjects.Add(lineObj);
        }

        /// <summary>
        /// 모든 연결선을 제거한다.
        /// </summary>
        private void ClearLines()
        {
            foreach (var line in _lineObjects)
            {
                if (line != null) Destroy(line);
            }
            _lineObjects.Clear();
        }

        // ── Node Click ─────────────────────────────────────

        /// <summary>
        /// 패시브 노드를 클릭했을 때 상세 패널을 표시한다.
        /// </summary>
        private void OnNodeClicked(PassiveData data)
        {
            _selectedPassive = data;
            RefreshDetailPanel();
        }

        // ── Detail Panel ───────────────────────────────────

        /// <summary>
        /// 상세 패널을 갱신한다.
        /// </summary>
        private void RefreshDetailPanel()
        {
            if (_detailPanel == null) return;

            if (_selectedPassive == null)
            {
                _detailPanel.SetActive(false);
                return;
            }

            _detailPanel.SetActive(true);

            var data = _selectedPassive;
            int currentLevel = PassiveManager.Instance != null
                ? PassiveManager.Instance.GetPassiveLevel(data.passiveId)
                : 0;

            // 아이콘
            if (_detailIcon != null)
                _detailIcon.sprite = data.icon;

            // 이름
            if (_detailName != null)
                _detailName.text = $"{data.passiveName} (Lv {currentLevel}/{data.maxLevel})";

            // 설명
            if (_detailDescription != null)
                _detailDescription.text = data.description;

            // 현재 효과
            if (_detailCurrentEffect != null)
            {
                if (currentLevel > 0)
                {
                    var effect = data.GetEffect(currentLevel);
                    _detailCurrentEffect.text = $"현재 효과: {FormatEffect(effect)}";
                }
                else
                {
                    _detailCurrentEffect.text = "현재 효과: 없음";
                }
            }

            // 다음 레벨 효과
            if (_detailNextEffect != null)
            {
                if (currentLevel < data.maxLevel)
                {
                    var nextEffect = data.GetEffect(currentLevel + 1);
                    _detailNextEffect.text = $"다음 레벨 효과: {FormatEffect(nextEffect)}";
                }
                else
                {
                    _detailNextEffect.text = "최대 레벨 달성!";
                }
            }

            // 필요 재료
            if (_detailCostText != null)
            {
                if (currentLevel < data.maxLevel)
                {
                    var cost = data.GetUnlockCost(currentLevel + 1);
                    if (cost.item != null)
                    {
                        int have = Inventory.Instance != null ? Inventory.Instance.GetItemCount(cost.item) : 0;
                        string colorTag = have >= cost.quantity ? "<color=#80FF80>" : "<color=#FF4040>";
                        _detailCostText.text = $"재료: {cost.item.itemName} {colorTag}{have}/{cost.quantity}</color>";
                    }
                    else
                    {
                        _detailCostText.text = "재료: 없음";
                    }
                }
                else
                {
                    _detailCostText.text = "";
                }
            }

            // 골드 비용
            if (_detailGoldCostText != null)
            {
                if (currentLevel < data.maxLevel)
                {
                    int goldNeeded = data.GetGoldCost(currentLevel + 1);
                    int playerGold = 0;
                    var player = GameObject.FindGameObjectWithTag("Player");
                    if (player != null)
                    {
                        var stats = player.GetComponent<SoulCraft.Player.PlayerStats>();
                        if (stats != null) playerGold = stats.Gold;
                    }
                    string colorTag = playerGold >= goldNeeded ? "<color=#FFD700>" : "<color=#FF4040>";
                    _detailGoldCostText.text = $"골드: {colorTag}{goldNeeded}G</color> (보유: {playerGold}G)";
                }
                else
                {
                    _detailGoldCostText.text = "";
                }
            }

            // 해금 불가 사유
            if (_detailFailReasonText != null)
            {
                if (PassiveManager.Instance != null && currentLevel < data.maxLevel)
                {
                    bool canUnlock = PassiveManager.Instance.CanUnlock(data.passiveId);
                    if (!canUnlock)
                    {
                        string reason = PassiveManager.Instance.GetUnlockFailReason(data.passiveId);
                        _detailFailReasonText.text = $"<color=#FF4040>{reason}</color>";
                        _detailFailReasonText.gameObject.SetActive(true);
                    }
                    else
                    {
                        _detailFailReasonText.gameObject.SetActive(false);
                    }
                }
                else
                {
                    _detailFailReasonText.gameObject.SetActive(false);
                }
            }

            // 해금 버튼
            if (_unlockButton != null)
            {
                bool canUnlock = PassiveManager.Instance != null &&
                                 PassiveManager.Instance.CanUnlock(data.passiveId);

                _unlockButton.interactable = canUnlock;

                if (_unlockButtonText != null)
                {
                    if (currentLevel >= data.maxLevel)
                        _unlockButtonText.text = "최대 레벨";
                    else if (currentLevel == 0)
                        _unlockButtonText.text = "해금";
                    else
                        _unlockButtonText.text = "강화";
                }
            }
        }

        // ── Unlock Action ──────────────────────────────────

        /// <summary>
        /// 해금/강화 버튼 클릭 시 호출.
        /// </summary>
        private void OnUnlockButtonClicked()
        {
            if (_selectedPassive == null || PassiveManager.Instance == null) return;

            bool success = PassiveManager.Instance.UnlockPassive(_selectedPassive.passiveId);

            if (success)
            {
                RefreshUI();
            }
        }

        /// <summary>
        /// 패시브 변경 이벤트 핸들러.
        /// </summary>
        private void OnPassiveChanged(string passiveId, int newLevel)
        {
            RefreshUI();
        }

        // ── Formatting Helpers ─────────────────────────────

        /// <summary>
        /// PassiveEffect를 사람이 읽을 수 있는 문자열로 변환한다.
        /// </summary>
        private string FormatEffect(PassiveEffect effect)
        {
            string statName = GetStatDisplayName(effect.statType);
            string valueStr;

            if (effect.isPercentage)
            {
                float displayValue = effect.value * 100f;
                string sign = displayValue >= 0 ? "+" : "";
                valueStr = $"{sign}{displayValue:F0}%";
            }
            else
            {
                string sign = effect.value >= 0 ? "+" : "";
                valueStr = $"{sign}{effect.value:F1}";
            }

            return $"{statName} {valueStr}";
        }

        /// <summary>
        /// PassiveStatType을 표시용 한글 이름으로 변환한다.
        /// </summary>
        private string GetStatDisplayName(PassiveStatType statType)
        {
            return statType switch
            {
                PassiveStatType.MaxHp => "최대 HP",
                PassiveStatType.Attack => "공격력",
                PassiveStatType.Defense => "방어력",
                PassiveStatType.Speed => "이동속도",
                PassiveStatType.CritRate => "치명타 확률",
                PassiveStatType.CritDamage => "치명타 데미지",
                PassiveStatType.DodgeCooldown => "회피 쿨다운",
                PassiveStatType.SkillCooldownReduction => "스킬 쿨다운",
                PassiveStatType.LifeSteal => "흡혈",
                PassiveStatType.DamageReduction => "피해 감소",
                PassiveStatType.ExpBonus => "경험치 획득",
                PassiveStatType.GoldBonus => "골드 획득",
                PassiveStatType.ElementalDamageBonus => "속성 데미지",
                PassiveStatType.ComboWindowExtend => "콤보 윈도우",
                PassiveStatType.StaggerDamageBonus => "경직 추가 데미지",
                _ => statType.ToString()
            };
        }

        // ── Nested Types ───────────────────────────────────

        /// <summary>
        /// 패시브 노드 슬롯 데이터.
        /// </summary>
        private class PassiveNodeSlot
        {
            public GameObject GameObject;
            public RectTransform RectTransform;
            public PassiveData PassiveData;
        }
    }
}
