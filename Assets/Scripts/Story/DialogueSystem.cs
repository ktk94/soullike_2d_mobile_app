using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SoulCraft.Story
{
    /// <summary>
    /// 대화 한 줄의 데이터.
    /// </summary>
    [Serializable]
    public class DialogueLine
    {
        public string speakerName;
        [TextArea(2, 5)]
        public string text;
        public Sprite speakerSprite;
    }

    /// <summary>
    /// 화면 하단에 대화창을 표시하고 텍스트 타이핑 효과를 지원하는 대화 시스템.
    /// UI를 코드에서 직접 생성하며, 대화 중 게임을 일시정지한다.
    /// ShowDialogue(DialogueLine[]) 호출로 사용한다.
    /// </summary>
    public class DialogueSystem : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────
        public static DialogueSystem Instance { get; private set; }

        // ── Settings ─────────────────────────────────────────
        [Header("Typing")]
        [SerializeField] private float _typingSpeed = 0.04f;
        [SerializeField] private float _fastTypingSpeed = 0.01f;

        [Header("Visual")]
        [SerializeField] private Color _panelColor = new Color(0f, 0f, 0f, 0.85f);
        [SerializeField] private Color _nameColor = new Color(1f, 0.85f, 0.4f);
        [SerializeField] private int _fontSize = 28;
        [SerializeField] private int _nameFontSize = 32;

        // ── Runtime State ────────────────────────────────────
        public bool IsDialogueActive { get; private set; }

        private DialogueLine[] _currentLines;
        private int _currentLineIndex;
        private bool _isTyping;
        private bool _skipRequested;
        private Coroutine _typingCoroutine;
        private Action _onDialogueComplete;
        private float _previousTimeScale;

        // ── UI References (코드에서 생성) ─────────────────────
        private Canvas _canvas;
        private GameObject _dialoguePanel;
        private TMP_Text _nameText;
        private TMP_Text _bodyText;
        private Image _speakerImage;
        private Image _panelImage;
        private TMP_Text _continueIndicator;
        private CanvasGroup _canvasGroup;

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
            DontDestroyOnLoad(gameObject);

            BuildUI();
            HideDialogue();
        }

        void Update()
        {
            if (!IsDialogueActive) return;

            // 탭/클릭 감지
            if (Input.GetMouseButtonDown(0) || Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            {
                OnTap();
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ============================================================
        //  Public API
        // ============================================================

        /// <summary>
        /// 대화를 시작한다. 게임을 일시정지하고 대화창을 표시한다.
        /// </summary>
        /// <param name="lines">표시할 대사 배열.</param>
        /// <param name="onComplete">대화 완료 시 콜백. (nullable)</param>
        public void ShowDialogue(DialogueLine[] lines, Action onComplete = null)
        {
            if (lines == null || lines.Length == 0)
            {
                onComplete?.Invoke();
                return;
            }

            _currentLines = lines;
            _currentLineIndex = 0;
            _onDialogueComplete = onComplete;
            IsDialogueActive = true;

            // 게임 일시정지
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            // UI 표시
            _dialoguePanel.SetActive(true);
            StartCoroutine(FadeCanvasGroup(0f, 1f, 0.15f));

            // 첫 대사 표시
            DisplayCurrentLine();
        }

        /// <summary>
        /// 현재 진행 중인 대화를 즉시 종료한다.
        /// </summary>
        public void ForceEndDialogue()
        {
            if (!IsDialogueActive) return;
            EndDialogue();
        }

        // ============================================================
        //  Input Handling
        // ============================================================

        private void OnTap()
        {
            if (_isTyping)
            {
                // 타이핑 중이면 즉시 전체 표시
                _skipRequested = true;
            }
            else
            {
                // 타이핑 완료 상태면 다음 대사로
                AdvanceDialogue();
            }
        }

        private void AdvanceDialogue()
        {
            _currentLineIndex++;

            if (_currentLineIndex < _currentLines.Length)
            {
                DisplayCurrentLine();
            }
            else
            {
                EndDialogue();
            }
        }

        // ============================================================
        //  Line Display
        // ============================================================

        private void DisplayCurrentLine()
        {
            if (_typingCoroutine != null)
                StopCoroutine(_typingCoroutine);

            DialogueLine line = _currentLines[_currentLineIndex];

            // 화자 이름
            _nameText.text = line.speakerName ?? "";
            _nameText.gameObject.SetActive(!string.IsNullOrEmpty(line.speakerName));

            // 화자 초상화
            if (line.speakerSprite != null)
            {
                _speakerImage.sprite = line.speakerSprite;
                _speakerImage.gameObject.SetActive(true);
            }
            else
            {
                _speakerImage.gameObject.SetActive(false);
            }

            // 타이핑 시작
            _continueIndicator.gameObject.SetActive(false);
            _typingCoroutine = StartCoroutine(TypeText(line.text));
        }

        private IEnumerator TypeText(string fullText)
        {
            _isTyping = true;
            _skipRequested = false;
            _bodyText.text = "";

            for (int i = 0; i < fullText.Length; i++)
            {
                if (_skipRequested)
                {
                    // 즉시 전체 표시
                    _bodyText.text = fullText;
                    break;
                }

                _bodyText.text += fullText[i];

                // Rich text 태그 내부는 건너뛰기
                if (fullText[i] == '<')
                {
                    int closeIndex = fullText.IndexOf('>', i);
                    if (closeIndex > i)
                    {
                        _bodyText.text = fullText.Substring(0, closeIndex + 1);
                        i = closeIndex;
                        continue;
                    }
                }

                float delay = _skipRequested ? 0f : _typingSpeed;
                yield return new WaitForSecondsRealtime(delay);
            }

            _isTyping = false;
            _skipRequested = false;

            // 계속 표시 인디케이터
            _continueIndicator.gameObject.SetActive(true);
            StartCoroutine(BlinkContinueIndicator());
        }

        private IEnumerator BlinkContinueIndicator()
        {
            while (_continueIndicator.gameObject.activeSelf)
            {
                _continueIndicator.alpha = _continueIndicator.alpha > 0.5f ? 0.3f : 1f;
                yield return new WaitForSecondsRealtime(0.5f);
            }
        }

        // ============================================================
        //  End Dialogue
        // ============================================================

        private void EndDialogue()
        {
            if (_typingCoroutine != null)
                StopCoroutine(_typingCoroutine);

            IsDialogueActive = false;
            _isTyping = false;

            // 시간 복원
            Time.timeScale = _previousTimeScale;

            // UI 숨김
            StartCoroutine(FadeAndHide());

            // 콜백 호출
            _onDialogueComplete?.Invoke();
            _onDialogueComplete = null;
        }

        private IEnumerator FadeAndHide()
        {
            yield return StartCoroutine(FadeCanvasGroup(1f, 0f, 0.15f));
            HideDialogue();
        }

        private void HideDialogue()
        {
            if (_dialoguePanel != null)
                _dialoguePanel.SetActive(false);
        }

        // ============================================================
        //  Canvas Group Fade (unscaledTime)
        // ============================================================

        private IEnumerator FadeCanvasGroup(float from, float to, float duration)
        {
            if (_canvasGroup == null) yield break;

            float elapsed = 0f;
            _canvasGroup.alpha = from;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            _canvasGroup.alpha = to;
        }

        // ============================================================
        //  UI Construction (코드에서 생성)
        // ============================================================

        private void BuildUI()
        {
            // ── Canvas ──
            GameObject canvasGo = new GameObject("DialogueCanvas");
            canvasGo.transform.SetParent(transform);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            // ── Dialogue Panel (화면 하단) ──
            _dialoguePanel = new GameObject("DialoguePanel");
            _dialoguePanel.transform.SetParent(canvasGo.transform, false);

            RectTransform panelRect = _dialoguePanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(1f, 0.3f);
            panelRect.offsetMin = new Vector2(20f, 20f);
            panelRect.offsetMax = new Vector2(-20f, 0f);

            _panelImage = _dialoguePanel.AddComponent<Image>();
            _panelImage.color = _panelColor;

            _canvasGroup = _dialoguePanel.AddComponent<CanvasGroup>();

            // 패널에 VerticalLayoutGroup 대신 수동 배치

            // ── Speaker Image (좌측) ──
            GameObject speakerGo = new GameObject("SpeakerImage");
            speakerGo.transform.SetParent(_dialoguePanel.transform, false);
            RectTransform speakerRect = speakerGo.AddComponent<RectTransform>();
            speakerRect.anchorMin = new Vector2(0f, 0f);
            speakerRect.anchorMax = new Vector2(0f, 1f);
            speakerRect.pivot = new Vector2(0f, 0.5f);
            speakerRect.anchoredPosition = new Vector2(15f, 0f);
            speakerRect.sizeDelta = new Vector2(100f, 100f);

            _speakerImage = speakerGo.AddComponent<Image>();
            _speakerImage.preserveAspect = true;
            _speakerImage.color = Color.white;

            // ── Name Text ──
            GameObject nameGo = new GameObject("NameText");
            nameGo.transform.SetParent(_dialoguePanel.transform, false);
            RectTransform nameRect = nameGo.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0f, 1f);
            nameRect.anchoredPosition = new Vector2(130f, -10f);
            nameRect.sizeDelta = new Vector2(-150f, 40f);

            _nameText = nameGo.AddComponent<TextMeshProUGUI>();
            _nameText.fontSize = _nameFontSize;
            _nameText.color = _nameColor;
            _nameText.fontStyle = FontStyles.Bold;
            _nameText.alignment = TextAlignmentOptions.TopLeft;
            _nameText.enableWordWrapping = false;
            _nameText.overflowMode = TextOverflowModes.Ellipsis;

            // ── Body Text ──
            GameObject bodyGo = new GameObject("BodyText");
            bodyGo.transform.SetParent(_dialoguePanel.transform, false);
            RectTransform bodyRect = bodyGo.AddComponent<RectTransform>();
            bodyRect.anchorMin = new Vector2(0f, 0f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.offsetMin = new Vector2(130f, 20f);
            bodyRect.offsetMax = new Vector2(-20f, -55f);

            _bodyText = bodyGo.AddComponent<TextMeshProUGUI>();
            _bodyText.fontSize = _fontSize;
            _bodyText.color = Color.white;
            _bodyText.alignment = TextAlignmentOptions.TopLeft;
            _bodyText.enableWordWrapping = true;
            _bodyText.overflowMode = TextOverflowModes.Overflow;

            // ── Continue Indicator (우하단 삼각형 표시) ──
            GameObject continueGo = new GameObject("ContinueIndicator");
            continueGo.transform.SetParent(_dialoguePanel.transform, false);
            RectTransform continueRect = continueGo.AddComponent<RectTransform>();
            continueRect.anchorMin = new Vector2(1f, 0f);
            continueRect.anchorMax = new Vector2(1f, 0f);
            continueRect.pivot = new Vector2(1f, 0f);
            continueRect.anchoredPosition = new Vector2(-20f, 10f);
            continueRect.sizeDelta = new Vector2(40f, 30f);

            _continueIndicator = continueGo.AddComponent<TextMeshProUGUI>();
            _continueIndicator.text = "\u25BC"; // down triangle
            _continueIndicator.fontSize = 24;
            _continueIndicator.color = Color.white;
            _continueIndicator.alignment = TextAlignmentOptions.BottomRight;
        }
    }
}
