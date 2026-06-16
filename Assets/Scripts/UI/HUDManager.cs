using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SoulCraft.Core;
using SoulCraft.Player;
using SoulCraft.Combat;

namespace SoulCraft.UI
{
    /// <summary>
    /// 전투 중 HUD 관리.
    /// HP/마나/스태미나 바, 스킬 슬롯, 콤보 카운터, 현재 방 표시, 재화 표시.
    /// GameEventSystem을 구독하여 자동 업데이트한다.
    /// </summary>
    public class HUDManager : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────
        public static HUDManager Instance { get; private set; }

        // ── Inspector: HP / Mana / Stamina ───────────────────
        [Header("Player Bars")]
        [SerializeField] private Slider _hpSlider;
        [SerializeField] private Slider _manaSlider;
        [SerializeField] private Slider _staminaSlider;
        [SerializeField] private TMP_Text _hpText;
        [SerializeField] private Image _hpFillImage;
        [SerializeField] private Color _hpColorHigh = new Color(0.2f, 0.9f, 0.3f);
        [SerializeField] private Color _hpColorMid = new Color(0.95f, 0.85f, 0.1f);
        [SerializeField] private Color _hpColorLow = new Color(0.95f, 0.2f, 0.15f);

        // ── Inspector: Skill Slots ───────────────────────────
        [Header("Skill Slots (4)")]
        [SerializeField] private SkillSlotUI[] _skillSlots = new SkillSlotUI[4];

        // ── Inspector: Combo ─────────────────────────────────
        [Header("Combo Counter")]
        [SerializeField] private GameObject _comboPanel;
        [SerializeField] private TMP_Text _comboNameText;
        [SerializeField] private TMP_Text _comboCountText;
        [SerializeField] private TMP_Text _comboMultiplierText;
        [SerializeField] private float _comboDisplayDuration = 2.5f;

        // ── Inspector: Room / Minimap ────────────────────────
        [Header("Room Indicator")]
        [SerializeField] private TMP_Text _roomText;
        [SerializeField] private TMP_Text _floorText;

        // ── Inspector: Currency ──────────────────────────────
        [Header("Currency")]
        [SerializeField] private TMP_Text _goldText;

        // ── Runtime ──────────────────────────────────────────
        private PlayerStats _playerStats;
        private SkillManager _skillManager;
        private float _comboDisplayTimer;

        // ============================================================
        //  Unity Lifecycle
        // ============================================================

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            // 플레이어 참조 확보
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _playerStats = player.GetComponent<PlayerStats>();
                _skillManager = player.GetComponent<SkillManager>();
            }

            // 이벤트 구독
            SubscribeEvents();

            // 초기 UI 갱신
            RefreshAllUI();

            // 콤보 패널 초기 숨김
            if (_comboPanel != null)
                _comboPanel.SetActive(false);
        }

        void Update()
        {
            UpdateSkillSlotCooldowns();
            UpdateComboDisplayTimer();
            UpdateBars();
        }

        void OnDestroy()
        {
            UnsubscribeEvents();
            if (Instance == this) Instance = null;
        }

        // ============================================================
        //  Event Subscribe / Unsubscribe
        // ============================================================

        private void SubscribeEvents()
        {
            GameEventSystem.Subscribe<DamageEvent>(OnDamageEvent);
            GameEventSystem.Subscribe<PlayerHealEvent>(OnPlayerHealEvent);
            GameEventSystem.Subscribe<ComboEvent>(OnComboEvent);
            GameEventSystem.Subscribe<SkillUsedEvent>(OnSkillUsedEvent);
            GameEventSystem.Subscribe<StageCompleteEvent>(OnStageCompleteEvent);

            if (_playerStats != null)
                _playerStats.OnHpChanged += OnHpChanged;
        }

        private void UnsubscribeEvents()
        {
            GameEventSystem.Unsubscribe<DamageEvent>(OnDamageEvent);
            GameEventSystem.Unsubscribe<PlayerHealEvent>(OnPlayerHealEvent);
            GameEventSystem.Unsubscribe<ComboEvent>(OnComboEvent);
            GameEventSystem.Unsubscribe<SkillUsedEvent>(OnSkillUsedEvent);
            GameEventSystem.Unsubscribe<StageCompleteEvent>(OnStageCompleteEvent);

            if (_playerStats != null)
                _playerStats.OnHpChanged -= OnHpChanged;
        }

        // ============================================================
        //  Refresh All
        // ============================================================

        public void RefreshAllUI()
        {
            if (_playerStats != null)
            {
                UpdateHPBar(_playerStats.CurrentHp, _playerStats.MaxHp);
                UpdateGoldDisplay(_playerStats.Gold);
            }

            UpdateRoomIndicator();
            RefreshSkillSlotIcons();
        }

        // ============================================================
        //  HP Bar
        // ============================================================

        private void OnHpChanged(int current, int max)
        {
            UpdateHPBar(current, max);
        }

        private void UpdateHPBar(int current, int max)
        {
            if (_hpSlider == null) return;

            float ratio = max > 0 ? (float)current / max : 0f;
            _hpSlider.value = ratio;

            if (_hpText != null)
                _hpText.text = $"{current} / {max}";

            if (_hpFillImage != null)
                _hpFillImage.color = GetHpColor(ratio);
        }

        private Color GetHpColor(float ratio)
        {
            if (ratio > 0.5f)
                return Color.Lerp(_hpColorMid, _hpColorHigh, (ratio - 0.5f) * 2f);
            else
                return Color.Lerp(_hpColorLow, _hpColorMid, ratio * 2f);
        }

        // ============================================================
        //  Mana / Stamina (Placeholder update each frame)
        // ============================================================

        private void UpdateBars()
        {
            // Mana/Stamina 바는 PlayerStats에 해당 시스템이 추가되면 연동.
            // 현재는 슬라이더가 할당되어 있으면 값 유지.
        }

        // ============================================================
        //  Skill Slot Cooldowns
        // ============================================================

        private void RefreshSkillSlotIcons()
        {
            if (_skillManager == null) return;

            SkillData[] equipped = _skillManager.EquippedSkills;
            for (int i = 0; i < _skillSlots.Length; i++)
            {
                if (_skillSlots[i] == null) continue;

                if (i < equipped.Length && equipped[i] != null)
                    _skillSlots[i].SetSkill(equipped[i].icon, equipped[i].skillName);
                else
                    _skillSlots[i].ClearSlot();
            }
        }

        private void UpdateSkillSlotCooldowns()
        {
            if (_skillManager == null) return;

            for (int i = 0; i < _skillSlots.Length; i++)
            {
                if (_skillSlots[i] == null) continue;
                float ratio = _skillManager.GetCooldownRatio(i);
                _skillSlots[i].SetCooldownOverlay(ratio);
            }
        }

        // ============================================================
        //  Combo Counter
        // ============================================================

        private void OnComboEvent(ComboEvent evt)
        {
            if (_comboPanel == null) return;

            _comboPanel.SetActive(true);
            _comboDisplayTimer = _comboDisplayDuration;

            if (_comboNameText != null)
                _comboNameText.text = evt.ComboName;

            if (_comboCountText != null)
                _comboCountText.text = $"{evt.ComboCount} HIT";

            if (_comboMultiplierText != null)
                _comboMultiplierText.text = $"x{evt.BonusDamageMultiplier:F1}";
        }

        private void UpdateComboDisplayTimer()
        {
            if (_comboPanel == null || !_comboPanel.activeSelf) return;

            _comboDisplayTimer -= Time.deltaTime;
            if (_comboDisplayTimer <= 0f)
                _comboPanel.SetActive(false);
        }

        // ============================================================
        //  Room / Floor Indicator
        // ============================================================

        private void UpdateRoomIndicator()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            if (_floorText != null)
                _floorText.text = $"B{gm.CurrentFloor + 1}F";

            if (_roomText != null)
                _roomText.text = $"Stage {gm.CurrentStageIndex + 1}";
        }

        // ============================================================
        //  Currency
        // ============================================================

        public void UpdateGoldDisplay(int gold)
        {
            if (_goldText != null)
                _goldText.text = gold.ToString("N0");
        }

        // ============================================================
        //  Event Handlers
        // ============================================================

        private void OnDamageEvent(DamageEvent evt)
        {
            // 플레이어가 타겟일 때 HP바 즉시 갱신
            if (_playerStats != null && evt.Target == _playerStats.gameObject)
                UpdateHPBar(_playerStats.CurrentHp, _playerStats.MaxHp);
        }

        private void OnPlayerHealEvent(PlayerHealEvent evt)
        {
            if (_playerStats != null)
                UpdateHPBar(_playerStats.CurrentHp, _playerStats.MaxHp);
        }

        private void OnSkillUsedEvent(SkillUsedEvent evt)
        {
            // 스킬 사용 시 쿨다운 UI는 Update에서 자동 처리됨.
        }

        private void OnStageCompleteEvent(StageCompleteEvent evt)
        {
            UpdateRoomIndicator();
        }

        // ============================================================
        //  Public Setters (외부 시스템 연동용)
        // ============================================================

        /// <summary>
        /// 마나 바 갱신. PlayerStats에 마나 시스템이 추가되면 사용.
        /// </summary>
        public void SetManaBar(float current, float max)
        {
            if (_manaSlider != null)
                _manaSlider.value = max > 0f ? current / max : 0f;
        }

        /// <summary>
        /// 스태미나 바 갱신.
        /// </summary>
        public void SetStaminaBar(float current, float max)
        {
            if (_staminaSlider != null)
                _staminaSlider.value = max > 0f ? current / max : 0f;
        }
    }

    // ================================================================
    //  SkillSlotUI  (HUDManager 내부에서 사용하는 개별 스킬 슬롯)
    // ================================================================

    /// <summary>
    /// 스킬 슬롯 하나의 UI 요소를 관리한다.
    /// 아이콘 표시 + 쿨다운 오버레이 (fillAmount 방식).
    /// </summary>
    [System.Serializable]
    public class SkillSlotUI
    {
        [SerializeField] private Image _iconImage;
        [SerializeField] private Image _cooldownOverlay;
        [SerializeField] private TMP_Text _cooldownText;

        /// <summary>
        /// 스킬 아이콘과 이름을 설정한다.
        /// </summary>
        public void SetSkill(Sprite icon, string skillName)
        {
            if (_iconImage != null)
            {
                _iconImage.sprite = icon;
                _iconImage.enabled = icon != null;
            }
        }

        /// <summary>
        /// 슬롯을 비운다.
        /// </summary>
        public void ClearSlot()
        {
            if (_iconImage != null)
            {
                _iconImage.sprite = null;
                _iconImage.enabled = false;
            }
            SetCooldownOverlay(0f);
        }

        /// <summary>
        /// 쿨다운 오버레이를 설정한다.
        /// ratio: 0 = 준비됨, 1 = 막 사용함.
        /// </summary>
        public void SetCooldownOverlay(float ratio)
        {
            if (_cooldownOverlay != null)
            {
                _cooldownOverlay.fillAmount = ratio;
                _cooldownOverlay.enabled = ratio > 0f;
            }

            if (_cooldownText != null)
            {
                if (ratio > 0f)
                {
                    _cooldownText.enabled = true;
                    // 쿨다운 텍스트를 실제 초로 표시하려면 외부에서 설정해야 함.
                    // 여기서는 비율만 표시.
                }
                else
                {
                    _cooldownText.enabled = false;
                }
            }
        }
    }
}
