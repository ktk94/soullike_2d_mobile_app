using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SoulCraft.Core;
using SoulCraft.Combat;

namespace SoulCraft.Farming
{
    /// <summary>
    /// 시너지 목록 UI 화면.
    /// 발견/미발견 시너지를 구분하여 표시하고,
    /// 활성 상태, 필요 재료, 보유량, 스킬 장착 기능을 제공한다.
    /// </summary>
    public class SynergyUI : MonoBehaviour
    {
        // ── Inspector ───────────────────────────────────

        [Header("Panel")]
        [SerializeField] private GameObject _synergyPanel;
        [SerializeField] private KeyCode _toggleKey = KeyCode.Y;

        [Header("List")]
        [Tooltip("시너지 슬롯 프리팹")]
        [SerializeField] private GameObject _synergySlotPrefab;
        [SerializeField] private Transform _listContent;

        [Header("Detail View")]
        [SerializeField] private GameObject _detailPanel;
        [SerializeField] private Image _detailIcon;
        [SerializeField] private TMP_Text _detailNameText;
        [SerializeField] private TMP_Text _detailDescriptionText;
        [SerializeField] private TMP_Text _detailTypeText;
        [SerializeField] private TMP_Text _detailBonusText;
        [SerializeField] private TMP_Text _detailStatusText;

        [Header("Ingredient List (Detail)")]
        [SerializeField] private Transform _ingredientListContent;
        [SerializeField] private GameObject _ingredientSlotPrefab;

        [Header("Skill Info (Detail)")]
        [SerializeField] private Image _skillIcon;
        [SerializeField] private TMP_Text _skillNameText;
        [SerializeField] private TMP_Text _skillDescriptionText;
        [SerializeField] private TMP_Text _skillStatsText;
        [SerializeField] private Button _equipButton;
        [SerializeField] private TMP_Text _equipButtonText;

        [Header("Filter Tabs")]
        [SerializeField] private Button _tabAll;
        [SerializeField] private Button _tabOffensive;
        [SerializeField] private Button _tabDefensive;
        [SerializeField] private Button _tabUtility;

        [Header("Notification")]
        [SerializeField] private GameObject _notificationPanel;
        [SerializeField] private TMP_Text _notificationText;
        [SerializeField] private Image _notificationIcon;
        [SerializeField] private CanvasGroup _notificationCanvasGroup;

        [Header("Visual")]
        [SerializeField] private Color _activeColor = new Color(0.2f, 1f, 0.4f, 1f);
        [SerializeField] private Color _inactiveColor = new Color(0.6f, 0.6f, 0.6f, 1f);
        [SerializeField] private Color _undiscoveredColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        [SerializeField] private Color _ingredientMetColor = new Color(0.3f, 1f, 0.5f, 1f);
        [SerializeField] private Color _ingredientUnmetColor = new Color(1f, 0.3f, 0.3f, 1f);

        // ── Runtime ─────────────────────────────────────

        private SynergyData _selectedSynergy;
        private SynergyType? _currentFilter;
        private readonly List<GameObject> _spawnedSlots = new();
        private readonly List<GameObject> _spawnedIngredients = new();

        private float _notificationTimer;
        private bool _isNotificationShowing;

        // ── Lifecycle ───────────────────────────────────

        void Start()
        {
            // 패널 초기 숨김
            if (_synergyPanel != null)
                _synergyPanel.SetActive(false);

            if (_detailPanel != null)
                _detailPanel.SetActive(false);

            if (_notificationPanel != null)
                _notificationPanel.SetActive(false);

            // 필터 탭 버튼 연결
            SetupFilterTabs();

            // 시너지 매니저 이벤트 구독
            if (SynergyManager.Instance != null)
            {
                SynergyManager.Instance.OnSynergyActivated += OnSynergyActivated;
                SynergyManager.Instance.OnSynergyDeactivated += OnSynergyDeactivated;
                SynergyManager.Instance.OnSynergiesChanged += RefreshList;
            }

            // 장착 버튼 연결
            if (_equipButton != null)
                _equipButton.onClick.AddListener(OnEquipButtonClicked);
        }

        void Update()
        {
            // 토글 키
            if (Input.GetKeyDown(_toggleKey))
                TogglePanel();

            // 알림 타이머
            UpdateNotification();
        }

        void OnDestroy()
        {
            if (SynergyManager.Instance != null)
            {
                SynergyManager.Instance.OnSynergyActivated -= OnSynergyActivated;
                SynergyManager.Instance.OnSynergyDeactivated -= OnSynergyDeactivated;
                SynergyManager.Instance.OnSynergiesChanged -= RefreshList;
            }
        }

        // ── Panel Toggle ────────────────────────────────

        public void TogglePanel()
        {
            if (_synergyPanel == null) return;

            bool isActive = _synergyPanel.activeSelf;
            _synergyPanel.SetActive(!isActive);

            if (!isActive)
            {
                RefreshList();
            }
        }

        public void OpenPanel()
        {
            if (_synergyPanel == null) return;
            _synergyPanel.SetActive(true);
            RefreshList();
        }

        public void ClosePanel()
        {
            if (_synergyPanel != null)
                _synergyPanel.SetActive(false);

            if (_detailPanel != null)
                _detailPanel.SetActive(false);
        }

        // ── Filter Tabs ─────────────────────────────────

        private void SetupFilterTabs()
        {
            if (_tabAll != null)
                _tabAll.onClick.AddListener(() => SetFilter(null));
            if (_tabOffensive != null)
                _tabOffensive.onClick.AddListener(() => SetFilter(SynergyType.OffensiveCombo));
            if (_tabDefensive != null)
                _tabDefensive.onClick.AddListener(() => SetFilter(SynergyType.DefensiveCombo));
            if (_tabUtility != null)
                _tabUtility.onClick.AddListener(() => SetFilter(SynergyType.UtilityCombo));
        }

        private void SetFilter(SynergyType? filter)
        {
            _currentFilter = filter;
            RefreshList();
        }

        // ── List Refresh ────────────────────────────────

        /// <summary>
        /// 시너지 목록을 갱신한다.
        /// 발견된 시너지는 아이콘과 이름이 표시되고,
        /// 미발견 시너지는 "???" 로 표시된다.
        /// </summary>
        public void RefreshList()
        {
            if (SynergyManager.Instance == null) return;

            // 기존 슬롯 정리
            ClearSpawnedSlots();

            var allSynergies = SynergyManager.Instance.AllSynergies;

            foreach (var synergy in allSynergies)
            {
                if (synergy == null) continue;

                // 필터 적용
                if (_currentFilter.HasValue && synergy.synergyType != _currentFilter.Value)
                    continue;

                CreateSynergySlot(synergy);
            }
        }

        private void CreateSynergySlot(SynergyData synergy)
        {
            if (_synergySlotPrefab == null || _listContent == null) return;

            var slotObj = Instantiate(_synergySlotPrefab, _listContent);
            _spawnedSlots.Add(slotObj);

            bool isDiscovered = SynergyManager.Instance.IsSynergyDiscovered(synergy.synergyId);
            bool isActive = SynergyManager.Instance.IsSynergyActive(synergy.synergyId);

            // 슬롯 내부 컴포넌트 설정
            var nameText = slotObj.GetComponentInChildren<TMP_Text>();
            var iconImage = slotObj.transform.Find("Icon")?.GetComponent<Image>();
            var bgImage = slotObj.GetComponent<Image>();
            var button = slotObj.GetComponent<Button>();

            // 이름 설정
            if (nameText != null)
            {
                nameText.text = isDiscovered ? synergy.synergyName : "???";
            }

            // 아이콘 설정
            if (iconImage != null)
            {
                if (isDiscovered && synergy.icon != null)
                {
                    iconImage.sprite = synergy.icon;
                    iconImage.color = Color.white;
                }
                else
                {
                    iconImage.sprite = null;
                    iconImage.color = _undiscoveredColor;
                }
            }

            // 배경 색상 (활성/비활성/미발견)
            if (bgImage != null)
            {
                if (isActive)
                    bgImage.color = _activeColor;
                else if (isDiscovered)
                    bgImage.color = _inactiveColor;
                else
                    bgImage.color = _undiscoveredColor;
            }

            // 진행도 표시
            var progressText = slotObj.transform.Find("Progress")?.GetComponent<TMP_Text>();
            if (progressText != null)
            {
                var (fulfilled, total) = SynergyManager.Instance.GetSynergyProgress(synergy);
                progressText.text = $"{fulfilled}/{total}";
            }

            // 상태 아이콘
            var statusIcon = slotObj.transform.Find("StatusIcon")?.GetComponent<Image>();
            if (statusIcon != null)
            {
                statusIcon.color = isActive ? _activeColor : _inactiveColor;
            }

            // 클릭 이벤트
            if (button != null)
            {
                var captured = synergy;
                button.onClick.AddListener(() => SelectSynergy(captured));
            }
        }

        private void ClearSpawnedSlots()
        {
            foreach (var slot in _spawnedSlots)
            {
                if (slot != null) Destroy(slot);
            }
            _spawnedSlots.Clear();
        }

        // ── Detail View ─────────────────────────────────

        /// <summary>
        /// 시너지를 선택하여 상세 정보를 표시한다.
        /// </summary>
        public void SelectSynergy(SynergyData synergy)
        {
            if (synergy == null) return;

            _selectedSynergy = synergy;

            if (_detailPanel != null)
                _detailPanel.SetActive(true);

            bool isDiscovered = SynergyManager.Instance.IsSynergyDiscovered(synergy.synergyId);
            bool isActive = SynergyManager.Instance.IsSynergyActive(synergy.synergyId);

            // 아이콘
            if (_detailIcon != null)
            {
                _detailIcon.sprite = isDiscovered ? synergy.icon : null;
                _detailIcon.color = isDiscovered ? Color.white : _undiscoveredColor;
            }

            // 이름
            if (_detailNameText != null)
                _detailNameText.text = isDiscovered ? synergy.synergyName : "???";

            // 설명
            if (_detailDescriptionText != null)
                _detailDescriptionText.text = isDiscovered ? synergy.description : "아직 발견되지 않은 시너지입니다.";

            // 타입
            if (_detailTypeText != null)
                _detailTypeText.text = GetSynergyTypeDisplayName(synergy.synergyType);

            // 보너스 효과
            if (_detailBonusText != null)
                _detailBonusText.text = isDiscovered ? synergy.bonusEffect : "???";

            // 상태
            if (_detailStatusText != null)
            {
                if (isActive)
                    _detailStatusText.text = "<color=#33FF77>활성</color>";
                else if (isDiscovered)
                    _detailStatusText.text = "<color=#FFAA33>비활성 (재료 부족)</color>";
                else
                    _detailStatusText.text = "<color=#888888>미발견</color>";
            }

            // 재료 목록 갱신
            RefreshIngredientList(synergy, isDiscovered);

            // 스킬 정보 갱신
            RefreshSkillInfo(synergy, isDiscovered, isActive);
        }

        /// <summary>
        /// 필요 재료 목록 및 현재 보유량을 표시한다.
        /// </summary>
        private void RefreshIngredientList(SynergyData synergy, bool isDiscovered)
        {
            // 기존 슬롯 정리
            ClearSpawnedIngredients();

            if (synergy.requiredItems == null || _ingredientSlotPrefab == null ||
                _ingredientListContent == null)
                return;

            var inventory = Inventory.Instance;

            foreach (var ingredient in synergy.requiredItems)
            {
                if (ingredient.item == null) continue;

                var obj = Instantiate(_ingredientSlotPrefab, _ingredientListContent);
                _spawnedIngredients.Add(obj);

                var nameText = obj.GetComponentInChildren<TMP_Text>();
                var iconImage = obj.transform.Find("Icon")?.GetComponent<Image>();
                var quantityText = obj.transform.Find("Quantity")?.GetComponent<TMP_Text>();

                // 아이콘
                if (iconImage != null && ingredient.item.icon != null)
                {
                    iconImage.sprite = ingredient.item.icon;
                }

                // 아이템 이름 (미발견 시너지는 재료명도 가림)
                if (nameText != null)
                {
                    nameText.text = isDiscovered ? ingredient.item.itemName : "???";
                }

                // 보유량 / 필요량
                if (quantityText != null)
                {
                    int owned = inventory != null ? inventory.GetItemCount(ingredient.item) : 0;
                    int required = ingredient.requiredQuantity;

                    quantityText.text = isDiscovered
                        ? $"{owned} / {required}"
                        : "? / ?";

                    quantityText.color = owned >= required
                        ? _ingredientMetColor
                        : _ingredientUnmetColor;
                }
            }
        }

        /// <summary>
        /// 연계 스킬 정보를 표시한다.
        /// </summary>
        private void RefreshSkillInfo(SynergyData synergy, bool isDiscovered, bool isActive)
        {
            var skill = synergy.resultSkill;

            if (_skillIcon != null)
            {
                if (isDiscovered && skill != null && skill.icon != null)
                {
                    _skillIcon.sprite = skill.icon;
                    _skillIcon.color = Color.white;
                }
                else
                {
                    _skillIcon.sprite = null;
                    _skillIcon.color = _undiscoveredColor;
                }
            }

            if (_skillNameText != null)
                _skillNameText.text = isDiscovered && skill != null ? skill.skillName : "???";

            if (_skillDescriptionText != null)
            {
                _skillDescriptionText.text = isDiscovered && skill != null
                    ? skill.description
                    : "시너지를 발견하면 스킬 정보가 공개됩니다.";
            }

            if (_skillStatsText != null)
            {
                if (isDiscovered && skill != null)
                {
                    _skillStatsText.text =
                        $"배율: x{skill.damageMultiplier:F1}  |  " +
                        $"쿨다운: {skill.cooldown:F1}초  |  " +
                        $"마나: {skill.manaCost}  |  " +
                        $"사거리: {skill.range:F1}";
                }
                else
                {
                    _skillStatsText.text = "???";
                }
            }

            // 장착 버튼
            if (_equipButton != null)
            {
                _equipButton.interactable = isActive;
            }

            if (_equipButtonText != null)
            {
                if (isActive)
                    _equipButtonText.text = "스킬 장착";
                else if (isDiscovered)
                    _equipButtonText.text = "재료 부족";
                else
                    _equipButtonText.text = "미발견";
            }
        }

        private void ClearSpawnedIngredients()
        {
            foreach (var obj in _spawnedIngredients)
            {
                if (obj != null) Destroy(obj);
            }
            _spawnedIngredients.Clear();
        }

        // ── Equip ───────────────────────────────────────

        /// <summary>
        /// 연계 스킬 장착 버튼 클릭 핸들러.
        /// 비어 있는 스킬 슬롯에 연계 스킬을 자동 장착한다.
        /// </summary>
        private void OnEquipButtonClicked()
        {
            if (_selectedSynergy == null || _selectedSynergy.resultSkill == null) return;

            if (!SynergyManager.Instance.IsSynergyActive(_selectedSynergy.synergyId))
            {
                Debug.LogWarning("[SynergyUI] 시너지가 활성 상태가 아닙니다.");
                return;
            }

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            var skillManager = player.GetComponent<SkillManager>();
            if (skillManager == null) return;

            // 이미 장착되어 있는지 확인
            var equipped = skillManager.EquippedSkills;
            for (int i = 0; i < equipped.Length; i++)
            {
                if (equipped[i] != null &&
                    equipped[i].skillId == _selectedSynergy.resultSkill.skillId)
                {
                    // 이미 장착됨 - 해제
                    skillManager.UnequipSkill(i);
                    RefreshSkillInfo(_selectedSynergy, true, true);
                    Debug.Log($"[SynergyUI] 연계 스킬 해제: {_selectedSynergy.resultSkill.skillName}");
                    return;
                }
            }

            // 빈 슬롯 찾아서 장착
            for (int i = 0; i < equipped.Length; i++)
            {
                if (equipped[i] == null)
                {
                    skillManager.EquipSkill(i, _selectedSynergy.resultSkill);
                    RefreshSkillInfo(_selectedSynergy, true, true);
                    Debug.Log($"[SynergyUI] 연계 스킬 장착: {_selectedSynergy.resultSkill.skillName} → 슬롯 {i}");
                    return;
                }
            }

            Debug.LogWarning("[SynergyUI] 빈 스킬 슬롯이 없습니다.");
        }

        // ── Notification ────────────────────────────────

        private void OnSynergyActivated(SynergyData synergy)
        {
            ShowNotification(
                synergy.icon,
                synergy.unlockMessage ?? $"시너지 해금: {synergy.synergyName}!"
            );

            // 목록이 열려 있으면 갱신
            if (_synergyPanel != null && _synergyPanel.activeSelf)
                RefreshList();
        }

        private void OnSynergyDeactivated(SynergyData synergy)
        {
            ShowNotification(
                synergy.icon,
                $"시너지 해제: {synergy.synergyName}"
            );

            if (_synergyPanel != null && _synergyPanel.activeSelf)
                RefreshList();
        }

        /// <summary>
        /// 화면 상단에 시너지 해금/해제 알림을 표시한다.
        /// </summary>
        private void ShowNotification(Sprite icon, string message)
        {
            if (_notificationPanel == null) return;

            _notificationPanel.SetActive(true);
            _isNotificationShowing = true;

            float duration = SynergyManager.Instance != null
                ? SynergyManager.Instance.NotificationDuration
                : 3f;
            _notificationTimer = duration;

            if (_notificationText != null)
                _notificationText.text = message;

            if (_notificationIcon != null && icon != null)
            {
                _notificationIcon.sprite = icon;
                _notificationIcon.enabled = true;
            }

            if (_notificationCanvasGroup != null)
                _notificationCanvasGroup.alpha = 1f;
        }

        private void UpdateNotification()
        {
            if (!_isNotificationShowing) return;

            _notificationTimer -= Time.unscaledDeltaTime;

            // 페이드 아웃 (마지막 0.5초)
            if (_notificationCanvasGroup != null && _notificationTimer < 0.5f)
            {
                _notificationCanvasGroup.alpha = Mathf.Max(0f, _notificationTimer / 0.5f);
            }

            if (_notificationTimer <= 0f)
            {
                _isNotificationShowing = false;
                if (_notificationPanel != null)
                    _notificationPanel.SetActive(false);
            }
        }

        // ── Helpers ─────────────────────────────────────

        private string GetSynergyTypeDisplayName(SynergyType type)
        {
            return type switch
            {
                SynergyType.OffensiveCombo => "공격형 연계",
                SynergyType.DefensiveCombo => "방어형 연계",
                SynergyType.UtilityCombo => "유틸리티 연계",
                _ => "알 수 없음"
            };
        }
    }
}
