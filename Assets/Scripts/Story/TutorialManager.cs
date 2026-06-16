using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SoulCraft.Core;
using SoulCraft.Enemy;
using SoulCraft.UI;

namespace SoulCraft.Story
{
    /// <summary>
    /// 튜토리얼 단계 정의.
    /// </summary>
    public enum TutorialStep
    {
        Move,
        Attack,
        Dash,
        Skill,
        Combo,
        ItemPickup,
        DoorExit,
        Complete
    }

    /// <summary>
    /// 첫 플레이 시 단계별 가이드를 제공하는 튜토리얼 매니저.
    /// 각 단계는 안내 텍스트, 하이라이트 화살표, 완료 조건을 가진다.
    /// SaveData에 tutorialCompleted 플래그를 저장하며, 3초 터치 유지로 스킵할 수 있다.
    /// </summary>
    public class TutorialManager : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────
        public static TutorialManager Instance { get; private set; }

        // ── Settings ─────────────────────────────────────────
        [Header("Tutorial Settings")]
        [SerializeField] private float _skipHoldDuration = 3f;
        [SerializeField] private float _arrowBlinkInterval = 0.5f;

        [Header("Visual")]
        [SerializeField] private Color _guideTextColor = new Color(1f, 1f, 0.8f);
        [SerializeField] private Color _highlightColor = new Color(1f, 0.85f, 0.3f, 0.8f);
        [SerializeField] private int _guideFontSize = 30;
        [SerializeField] private int _skipFontSize = 20;

        [Header("Tutorial Enemy")]
        [Tooltip("튜토리얼 적 프리팹. null이면 적 단계를 건너뛴다.")]
        [SerializeField] private GameObject _tutorialEnemyPrefab;

        [Header("Tutorial Item")]
        [Tooltip("튜토리얼 아이템 프리팹. null이면 아이템 단계를 건너뛴다.")]
        [SerializeField] private GameObject _tutorialItemPrefab;

        // ── Runtime ──────────────────────────────────────────
        public TutorialStep CurrentStep { get; private set; } = TutorialStep.Move;
        public bool IsActive { get; private set; }
        public bool IsCompleted { get; private set; }

        private Canvas _canvas;
        private GameObject _guidePanel;
        private TMP_Text _guideText;
        private TMP_Text _skipText;
        private Image _arrowImage;
        private Image _skipProgressImage;
        private CanvasGroup _canvasGroup;

        private float _skipHoldTimer;
        private bool _isHolding;
        private Coroutine _stepCoroutine;
        private Coroutine _arrowBlinkCoroutine;

        // Step completion tracking
        private bool _playerMoved;
        private bool _playerAttacked;
        private bool _playerDashed;
        private bool _playerUsedSkill;
        private bool _playerCombo;
        private bool _playerPickedItem;
        private bool _playerReachedDoor;
        private GameObject _spawnedEnemy;

        // ============================================================
        //  Unity Lifecycle
        // ============================================================

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            BuildUI();
            HideUI();
        }

        void OnDestroy()
        {
            UnsubscribeEvents();
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (!IsActive) return;

            HandleSkipInput();
        }

        // ============================================================
        //  Public API
        // ============================================================

        /// <summary>
        /// 튜토리얼을 시작한다. SaveData에서 이미 완료되었으면 무시한다.
        /// </summary>
        public void StartTutorial()
        {
            // 저장 데이터 확인
            if (SaveManager.Instance != null)
            {
                SaveData save = SaveManager.Instance.Load();
                if (save != null && IsTutorialCompletedInSave(save))
                {
                    IsCompleted = true;
                    return;
                }
            }

            IsActive = true;
            IsCompleted = false;
            CurrentStep = TutorialStep.Move;

            SubscribeEvents();
            ShowUI();
            StartStep(TutorialStep.Move);
        }

        /// <summary>
        /// 튜토리얼을 강제 스킵한다.
        /// </summary>
        public void SkipTutorial()
        {
            CompleteTutorial();
        }

        /// <summary>
        /// 외부에서 특정 조건 달성을 알려줄 때 호출한다.
        /// </summary>
        public void NotifyItemPickedUp()
        {
            _playerPickedItem = true;
        }

        /// <summary>
        /// 외부에서 문 도달을 알려줄 때 호출한다.
        /// </summary>
        public void NotifyDoorReached()
        {
            _playerReachedDoor = true;
        }

        // ============================================================
        //  Step Management
        // ============================================================

        private void StartStep(TutorialStep step)
        {
            if (_stepCoroutine != null)
                StopCoroutine(_stepCoroutine);

            CurrentStep = step;
            _stepCoroutine = StartCoroutine(RunStep(step));
        }

        private IEnumerator RunStep(TutorialStep step)
        {
            switch (step)
            {
                case TutorialStep.Move:
                    yield return RunMoveStep();
                    break;
                case TutorialStep.Attack:
                    yield return RunAttackStep();
                    break;
                case TutorialStep.Dash:
                    yield return RunDashStep();
                    break;
                case TutorialStep.Skill:
                    yield return RunSkillStep();
                    break;
                case TutorialStep.Combo:
                    yield return RunComboStep();
                    break;
                case TutorialStep.ItemPickup:
                    yield return RunItemPickupStep();
                    break;
                case TutorialStep.DoorExit:
                    yield return RunDoorExitStep();
                    break;
            }
        }

        private void AdvanceToNextStep()
        {
            TutorialStep next = CurrentStep + 1;

            if (next >= TutorialStep.Complete)
            {
                CompleteTutorial();
                return;
            }

            StartStep(next);
        }

        // ============================================================
        //  Individual Steps
        // ============================================================

        // --- Step 1: Move ---
        private IEnumerator RunMoveStep()
        {
            SetGuideText("조이스틱을 움직여 이동하세요");
            ShowArrowAtScreenPosition(new Vector2(0.2f, 0.15f)); // 조이스틱 위치 근처

            _playerMoved = false;
            float moveAccumulated = 0f;

            while (moveAccumulated < 2f) // 2초간 이동 감지
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
                    if (rb != null && rb.linearVelocity.sqrMagnitude > 0.5f)
                        moveAccumulated += Time.deltaTime;
                }
                yield return null;
            }

            _playerMoved = true;
            yield return ShowStepComplete("이동 성공!");
            AdvanceToNextStep();
        }

        // --- Step 2: Attack ---
        private IEnumerator RunAttackStep()
        {
            SetGuideText("공격 버튼을 눌러 적을 공격하세요");
            ShowArrowAtScreenPosition(new Vector2(0.85f, 0.2f)); // 공격 버튼 근처

            // 적 1마리 스폰
            SpawnTutorialEnemy();

            _playerAttacked = false;
            while (!_playerAttacked)
                yield return null;

            yield return ShowStepComplete("공격 성공!");
            AdvanceToNextStep();
        }

        // --- Step 3: Dash ---
        private IEnumerator RunDashStep()
        {
            SetGuideText("대시로 적의 공격을 피하세요!");
            ShowArrowAtScreenPosition(new Vector2(0.7f, 0.12f)); // 대시 버튼 근처

            _playerDashed = false;
            while (!_playerDashed)
                yield return null;

            yield return ShowStepComplete("대시 성공!");
            AdvanceToNextStep();
        }

        // --- Step 4: Skill ---
        private IEnumerator RunSkillStep()
        {
            SetGuideText("스킬을 사용해보세요");
            ShowArrowAtScreenPosition(new Vector2(0.75f, 0.3f)); // 스킬 버튼 근처

            _playerUsedSkill = false;
            while (!_playerUsedSkill)
                yield return null;

            yield return ShowStepComplete("스킬 사용 성공!");
            AdvanceToNextStep();
        }

        // --- Step 5: Combo ---
        private IEnumerator RunComboStep()
        {
            SetGuideText("연속으로 스킬을 사용하면 콤보가 발동합니다!");
            HideArrow();

            _playerCombo = false;

            // 콤보를 발동하지 않아도 10초 후 자동 진행
            float timeout = 10f;
            float elapsed = 0f;

            while (!_playerCombo && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (_playerCombo)
                yield return ShowStepComplete("콤보 발동!");
            else
                yield return ShowStepComplete("나중에 다시 시도해보세요!");

            AdvanceToNextStep();
        }

        // --- Step 6: Item Pickup ---
        private IEnumerator RunItemPickupStep()
        {
            SetGuideText("아이템을 획득하세요");
            HideArrow();

            // 아이템 드롭
            SpawnTutorialItem();

            _playerPickedItem = false;

            float timeout = 15f;
            float elapsed = 0f;

            while (!_playerPickedItem && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            yield return ShowStepComplete("아이템 획득!");
            AdvanceToNextStep();
        }

        // --- Step 7: Door Exit ---
        private IEnumerator RunDoorExitStep()
        {
            SetGuideText("문을 통과하여 다음으로 진행하세요");

            // 문 방향 화살표 (화면 우측 상단 방향)
            ShowArrowAtScreenPosition(new Vector2(0.8f, 0.7f));

            _playerReachedDoor = false;
            while (!_playerReachedDoor)
                yield return null;

            yield return ShowStepComplete("튜토리얼 완료!");
            AdvanceToNextStep();
        }

        // ============================================================
        //  Tutorial Completion
        // ============================================================

        private void CompleteTutorial()
        {
            IsActive = false;
            IsCompleted = true;

            if (_stepCoroutine != null)
                StopCoroutine(_stepCoroutine);

            UnsubscribeEvents();
            HideUI();

            // SaveData에 완료 저장
            SaveTutorialCompleted();

            // 완료 대사
            if (DialogueSystem.Instance != null)
            {
                DialogueLine[] completeLines = new[]
                {
                    new DialogueLine
                    {
                        speakerName = "수호 정령",
                        text = "기본적인 전투 기술을 모두 익혔군. 이제 네 앞길은 네가 개척해야 해."
                    },
                    new DialogueLine
                    {
                        speakerName = "수호 정령",
                        text = "행운을 빈다, 영혼의 그릇이여."
                    }
                };
                DialogueSystem.Instance.ShowDialogue(completeLines);
            }
        }

        // ============================================================
        //  Save Integration
        // ============================================================

        private bool IsTutorialCompletedInSave(SaveData save)
        {
            return save.tutorialCompleted;
        }

        private void SaveTutorialCompleted()
        {
            if (SaveManager.Instance == null) return;

            SaveData save = SaveManager.Instance.Load();
            save.tutorialCompleted = true;
            SaveManager.Instance.Save(save);
        }

        // ============================================================
        //  Event Handling
        // ============================================================

        private void SubscribeEvents()
        {
            GameEventSystem.Subscribe<DamageEvent>(OnDamageEvent);
            GameEventSystem.Subscribe<SkillUsedEvent>(OnSkillUsedEvent);
            GameEventSystem.Subscribe<ComboEvent>(OnComboEvent);
            GameEventSystem.Subscribe<EnemyDeathEvent>(OnEnemyDeathEvent);
            GameEventSystem.Subscribe<ItemDropEvent>(OnItemDropEvent);
        }

        private void UnsubscribeEvents()
        {
            GameEventSystem.Unsubscribe<DamageEvent>(OnDamageEvent);
            GameEventSystem.Unsubscribe<SkillUsedEvent>(OnSkillUsedEvent);
            GameEventSystem.Unsubscribe<ComboEvent>(OnComboEvent);
            GameEventSystem.Unsubscribe<EnemyDeathEvent>(OnEnemyDeathEvent);
            GameEventSystem.Unsubscribe<ItemDropEvent>(OnItemDropEvent);
        }

        private void OnDamageEvent(DamageEvent evt)
        {
            // 플레이어가 공격한 경우
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null && evt.Attacker == player)
            {
                _playerAttacked = true;
            }

            // 대시 감지: 플레이어가 대시 상태에서 적의 공격을 회피한 경우는
            // PlayerController의 상태로 판별 (여기서는 간접적으로 대시 이벤트 대체)
        }

        private void OnSkillUsedEvent(SkillUsedEvent evt)
        {
            _playerUsedSkill = true;

            // 대시 감지도 여기서 처리 (Dash가 스킬이 아닌 경우 별도 처리 필요)
            if (!_playerDashed)
                _playerDashed = true;
        }

        private void OnComboEvent(ComboEvent evt)
        {
            _playerCombo = true;
        }

        private void OnEnemyDeathEvent(EnemyDeathEvent evt)
        {
            if (_spawnedEnemy != null && evt.Enemy == _spawnedEnemy)
            {
                _playerAttacked = true;
            }
        }

        private void OnItemDropEvent(ItemDropEvent evt)
        {
            _playerPickedItem = true;
        }

        // ============================================================
        //  Skip Input (3초 유지)
        // ============================================================

        private void HandleSkipInput()
        {
            bool isTouching = Input.GetMouseButton(0) || Input.touchCount > 0;

            if (isTouching)
            {
                _skipHoldTimer += Time.unscaledDeltaTime;

                // 스킵 진행률 표시
                float progress = Mathf.Clamp01(_skipHoldTimer / _skipHoldDuration);
                UpdateSkipProgress(progress);

                if (_skipHoldTimer >= _skipHoldDuration)
                {
                    SkipTutorial();
                }
            }
            else
            {
                _skipHoldTimer = 0f;
                UpdateSkipProgress(0f);
            }
        }

        // ============================================================
        //  Spawning
        // ============================================================

        private void SpawnTutorialEnemy()
        {
            if (_tutorialEnemyPrefab == null) return;

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            Vector3 spawnPos = player.transform.position + new Vector3(3f, 0f, 0f);
            _spawnedEnemy = Instantiate(_tutorialEnemyPrefab, spawnPos, Quaternion.identity);
        }

        private void SpawnTutorialItem()
        {
            if (_tutorialItemPrefab == null) return;

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            Vector3 spawnPos = player.transform.position + new Vector3(2f, 1f, 0f);
            Instantiate(_tutorialItemPrefab, spawnPos, Quaternion.identity);
        }

        // ============================================================
        //  UI Helpers
        // ============================================================

        private void SetGuideText(string text)
        {
            if (_guideText != null)
                _guideText.text = text;
        }

        private void ShowArrowAtScreenPosition(Vector2 normalizedPos)
        {
            if (_arrowImage == null) return;

            _arrowImage.gameObject.SetActive(true);
            RectTransform rt = _arrowImage.GetComponent<RectTransform>();
            rt.anchorMin = normalizedPos;
            rt.anchorMax = normalizedPos;
            rt.anchoredPosition = Vector2.zero;

            // 깜빡임 시작
            if (_arrowBlinkCoroutine != null)
                StopCoroutine(_arrowBlinkCoroutine);
            _arrowBlinkCoroutine = StartCoroutine(ArrowBlinkCoroutine());
        }

        private void HideArrow()
        {
            if (_arrowImage != null)
                _arrowImage.gameObject.SetActive(false);

            if (_arrowBlinkCoroutine != null)
            {
                StopCoroutine(_arrowBlinkCoroutine);
                _arrowBlinkCoroutine = null;
            }
        }

        private IEnumerator ArrowBlinkCoroutine()
        {
            while (_arrowImage != null && _arrowImage.gameObject.activeSelf)
            {
                _arrowImage.color = new Color(_highlightColor.r, _highlightColor.g,
                    _highlightColor.b, 0.3f);
                yield return new WaitForSecondsRealtime(_arrowBlinkInterval);

                _arrowImage.color = _highlightColor;
                yield return new WaitForSecondsRealtime(_arrowBlinkInterval);
            }
        }

        private IEnumerator ShowStepComplete(string message)
        {
            SetGuideText($"<color=#AAFFAA>{message}</color>");
            HideArrow();
            yield return new WaitForSecondsRealtime(1f);
        }

        private void UpdateSkipProgress(float progress)
        {
            if (_skipProgressImage != null)
            {
                _skipProgressImage.fillAmount = progress;
                _skipProgressImage.gameObject.SetActive(progress > 0f);
            }

            if (_skipText != null)
            {
                if (progress > 0f)
                    _skipText.text = $"화면 유지로 스킵... ({progress * 100f:F0}%)";
                else
                    _skipText.text = "화면 길게 터치하여 스킵";
            }
        }

        private void ShowUI()
        {
            if (_guidePanel != null)
                _guidePanel.SetActive(true);
        }

        private void HideUI()
        {
            if (_guidePanel != null)
                _guidePanel.SetActive(false);

            HideArrow();
        }

        // ============================================================
        //  UI Construction
        // ============================================================

        private void BuildUI()
        {
            // ── Canvas ──
            GameObject canvasGo = new GameObject("TutorialCanvas");
            canvasGo.transform.SetParent(transform);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 80;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            // ── Guide Panel ──
            _guidePanel = new GameObject("GuidePanel");
            _guidePanel.transform.SetParent(canvasGo.transform, false);

            RectTransform panelRect = _guidePanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 0.85f);
            panelRect.anchorMax = new Vector2(1f, 0.95f);
            panelRect.offsetMin = new Vector2(30f, 0f);
            panelRect.offsetMax = new Vector2(-30f, 0f);

            Image panelBg = _guidePanel.AddComponent<Image>();
            panelBg.color = new Color(0f, 0f, 0f, 0.7f);
            panelBg.raycastTarget = false;

            _canvasGroup = _guidePanel.AddComponent<CanvasGroup>();
            _canvasGroup.blocksRaycasts = false;

            // ── Guide Text (화면 상단) ──
            GameObject guideTextGo = new GameObject("GuideText");
            guideTextGo.transform.SetParent(_guidePanel.transform, false);
            RectTransform guideRect = guideTextGo.AddComponent<RectTransform>();
            guideRect.anchorMin = Vector2.zero;
            guideRect.anchorMax = Vector2.one;
            guideRect.offsetMin = new Vector2(15f, 5f);
            guideRect.offsetMax = new Vector2(-15f, -5f);

            _guideText = guideTextGo.AddComponent<TextMeshProUGUI>();
            _guideText.fontSize = _guideFontSize;
            _guideText.color = _guideTextColor;
            _guideText.alignment = TextAlignmentOptions.Center;
            _guideText.enableWordWrapping = true;
            _guideText.richText = true;

            // ── Arrow Indicator ──
            GameObject arrowGo = new GameObject("ArrowIndicator");
            arrowGo.transform.SetParent(canvasGo.transform, false);
            RectTransform arrowRect = arrowGo.AddComponent<RectTransform>();
            arrowRect.sizeDelta = new Vector2(60f, 60f);
            arrowRect.anchorMin = new Vector2(0.5f, 0.5f);
            arrowRect.anchorMax = new Vector2(0.5f, 0.5f);

            _arrowImage = arrowGo.AddComponent<Image>();
            _arrowImage.color = _highlightColor;
            _arrowImage.raycastTarget = false;

            // 화살표 대신 원형 하이라이트로 사용 (스프라이트 없이)
            // 실제 게임에서는 화살표 스프라이트를 할당할 수 있다.

            // 화살표 텍스트 (유니코드 화살표)
            GameObject arrowTextGo = new GameObject("ArrowText");
            arrowTextGo.transform.SetParent(arrowGo.transform, false);
            RectTransform arrowTextRect = arrowTextGo.AddComponent<RectTransform>();
            arrowTextRect.anchorMin = Vector2.zero;
            arrowTextRect.anchorMax = Vector2.one;
            arrowTextRect.offsetMin = Vector2.zero;
            arrowTextRect.offsetMax = Vector2.zero;

            TMP_Text arrowTxt = arrowTextGo.AddComponent<TextMeshProUGUI>();
            arrowTxt.text = "\u25BC"; // 하향 삼각형
            arrowTxt.fontSize = 40;
            arrowTxt.color = Color.white;
            arrowTxt.alignment = TextAlignmentOptions.Center;

            // ── Skip Area (하단) ──
            GameObject skipGo = new GameObject("SkipArea");
            skipGo.transform.SetParent(canvasGo.transform, false);
            RectTransform skipRect = skipGo.AddComponent<RectTransform>();
            skipRect.anchorMin = new Vector2(0.3f, 0.02f);
            skipRect.anchorMax = new Vector2(0.7f, 0.06f);
            skipRect.offsetMin = Vector2.zero;
            skipRect.offsetMax = Vector2.zero;

            // Skip Text
            GameObject skipTextGo = new GameObject("SkipText");
            skipTextGo.transform.SetParent(skipGo.transform, false);
            RectTransform skipTextRect = skipTextGo.AddComponent<RectTransform>();
            skipTextRect.anchorMin = Vector2.zero;
            skipTextRect.anchorMax = Vector2.one;
            skipTextRect.offsetMin = Vector2.zero;
            skipTextRect.offsetMax = Vector2.zero;

            _skipText = skipTextGo.AddComponent<TextMeshProUGUI>();
            _skipText.text = "화면 길게 터치하여 스킵";
            _skipText.fontSize = _skipFontSize;
            _skipText.color = new Color(0.6f, 0.6f, 0.6f);
            _skipText.alignment = TextAlignmentOptions.Center;

            // Skip Progress Bar
            GameObject progressGo = new GameObject("SkipProgress");
            progressGo.transform.SetParent(canvasGo.transform, false);
            RectTransform progressRect = progressGo.AddComponent<RectTransform>();
            progressRect.anchorMin = new Vector2(0.3f, 0.01f);
            progressRect.anchorMax = new Vector2(0.7f, 0.02f);
            progressRect.offsetMin = Vector2.zero;
            progressRect.offsetMax = Vector2.zero;

            _skipProgressImage = progressGo.AddComponent<Image>();
            _skipProgressImage.color = new Color(1f, 0.85f, 0.3f, 0.8f);
            _skipProgressImage.type = Image.Type.Filled;
            _skipProgressImage.fillMethod = Image.FillMethod.Horizontal;
            _skipProgressImage.fillAmount = 0f;
            _skipProgressImage.raycastTarget = false;
        }
    }
}
