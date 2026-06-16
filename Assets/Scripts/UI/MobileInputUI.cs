using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using SoulCraft.Player;
using SoulCraft.Combat;

namespace SoulCraft.UI
{
    // ================================================================
    //  VirtualJoystick  (이동용 가상 조이스틱)
    // ================================================================

    /// <summary>
    /// 드래그로 방향을 입력하는 가상 조이스틱.
    /// 터치 시작 시 조이스틱 배경이 나타나고, 드래그하면 노브가 따라다닌다.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("References")]
        [SerializeField] private RectTransform _background;
        [SerializeField] private RectTransform _knob;

        [Header("Settings")]
        [SerializeField] private float _maxRadius = 80f;
        [SerializeField] private bool _dynamicPosition = true;

        // ── Output ───────────────────────────────────────────
        /// <summary>정규화된 입력 벡터 (-1 ~ 1).</summary>
        public Vector2 InputDirection { get; private set; }
        public bool IsActive { get; private set; }

        private Vector2 _origin;
        private Canvas _parentCanvas;
        private Camera _uiCamera;

        void Awake()
        {
            _parentCanvas = GetComponentInParent<Canvas>();
            if (_parentCanvas != null && _parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                _uiCamera = _parentCanvas.worldCamera;
        }

        void Start()
        {
            if (_background != null)
                _origin = _background.anchoredPosition;

            ResetKnob();
        }

        // ── Pointer Events ───────────────────────────────────

        public void OnPointerDown(PointerEventData eventData)
        {
            IsActive = true;

            if (_dynamicPosition && _background != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _background.parent as RectTransform,
                    eventData.position,
                    _uiCamera,
                    out Vector2 localPoint
                );
                _background.anchoredPosition = localPoint;
            }

            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_background == null || _knob == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _background,
                eventData.position,
                _uiCamera,
                out Vector2 localPoint
            );

            // 최대 반경으로 클램프
            Vector2 clamped = Vector2.ClampMagnitude(localPoint, _maxRadius);
            _knob.anchoredPosition = clamped;

            // 정규화 (-1 ~ 1)
            InputDirection = clamped / _maxRadius;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            IsActive = false;
            InputDirection = Vector2.zero;
            ResetKnob();

            // 동적 위치 모드면 원래 자리로 복귀
            if (_dynamicPosition && _background != null)
                _background.anchoredPosition = _origin;
        }

        private void ResetKnob()
        {
            if (_knob != null)
                _knob.anchoredPosition = Vector2.zero;
        }
    }

    // ================================================================
    //  MobileInputUI  (모바일 전용 터치 입력 UI)
    // ================================================================

    /// <summary>
    /// 모바일 전용 터치 입력 UI.
    /// 가상 조이스틱(이동), 공격/대시 버튼, 스킬 버튼 4개.
    /// PlayerController와 SkillManager에 입력을 전달한다.
    /// </summary>
    public class MobileInputUI : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────
        [Header("Joystick")]
        [SerializeField] private VirtualJoystick _joystick;

        [Header("Action Buttons")]
        [SerializeField] private Button _attackButton;
        [SerializeField] private Button _dashButton;

        [Header("Skill Buttons (4)")]
        [SerializeField] private Button[] _skillButtons = new Button[4];
        [SerializeField] private Image[] _skillCooldownOverlays = new Image[4];

        [Header("Auto Detection")]
        [Tooltip("false로 설정하면 항상 표시. true면 모바일에서만 표시.")]
        [SerializeField] private bool _mobileOnly = true;

        // ── Runtime ──────────────────────────────────────────
        private PlayerController _playerController;
        private SkillManager _skillManager;
        private bool _isInitialized;

        // ============================================================
        //  Unity Lifecycle
        // ============================================================

        void Start()
        {
            // 모바일이 아닌 환경에서는 비활성화
            if (_mobileOnly && !IsMobilePlatform())
            {
                gameObject.SetActive(false);
                return;
            }

            FindPlayerReferences();
            SetupButtons();
            _isInitialized = true;
        }

        void Update()
        {
            if (!_isInitialized) return;

            // 플레이어 참조가 없으면 다시 찾기
            if (_playerController == null)
            {
                FindPlayerReferences();
                if (_playerController == null) return;
            }

            UpdateJoystickInput();
            UpdateSkillCooldownOverlays();
        }

        void OnDestroy()
        {
            CleanupButtons();
        }

        // ============================================================
        //  Initialization
        // ============================================================

        private void FindPlayerReferences()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            _playerController = player.GetComponent<PlayerController>();
            _skillManager = player.GetComponent<SkillManager>();
        }

        private void SetupButtons()
        {
            if (_attackButton != null)
                _attackButton.onClick.AddListener(OnAttackButtonPressed);

            if (_dashButton != null)
                _dashButton.onClick.AddListener(OnDashButtonPressed);

            for (int i = 0; i < _skillButtons.Length; i++)
            {
                if (_skillButtons[i] != null)
                {
                    int slotIndex = i; // 클로저용 로컬 복사
                    _skillButtons[i].onClick.AddListener(() => OnSkillButtonPressed(slotIndex));
                }
            }
        }

        private void CleanupButtons()
        {
            if (_attackButton != null)
                _attackButton.onClick.RemoveListener(OnAttackButtonPressed);

            if (_dashButton != null)
                _dashButton.onClick.RemoveListener(OnDashButtonPressed);

            for (int i = 0; i < _skillButtons.Length; i++)
            {
                if (_skillButtons[i] != null)
                    _skillButtons[i].onClick.RemoveAllListeners();
            }
        }

        // ============================================================
        //  Joystick -> PlayerController
        // ============================================================

        private void UpdateJoystickInput()
        {
            if (_joystick == null || _playerController == null) return;

            _playerController.SetMoveInput(_joystick.InputDirection);
        }

        // ============================================================
        //  Button Callbacks
        // ============================================================

        private void OnAttackButtonPressed()
        {
            if (_playerController != null)
                _playerController.TriggerAttack();
        }

        private void OnDashButtonPressed()
        {
            if (_playerController != null)
                _playerController.TriggerDash();
        }

        private void OnSkillButtonPressed(int slotIndex)
        {
            if (_skillManager != null)
                _skillManager.UseSkill(slotIndex);
        }

        // ============================================================
        //  Skill Cooldown Overlays
        // ============================================================

        private void UpdateSkillCooldownOverlays()
        {
            if (_skillManager == null) return;

            for (int i = 0; i < _skillCooldownOverlays.Length; i++)
            {
                if (_skillCooldownOverlays[i] == null) continue;

                float ratio = _skillManager.GetCooldownRatio(i);
                _skillCooldownOverlays[i].fillAmount = ratio;
                _skillCooldownOverlays[i].enabled = ratio > 0f;
            }
        }

        // ============================================================
        //  Platform Detection
        // ============================================================

        private bool IsMobilePlatform()
        {
#if UNITY_ANDROID || UNITY_IOS
            return true;
#else
            // 에디터에서 모바일 시뮬레이션 시에도 표시
            return Application.isMobilePlatform;
#endif
        }
    }
}
