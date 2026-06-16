using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SoulCraft.Story
{
    /// <summary>
    /// 화면 중앙에 짧은 내레이션 텍스트를 페이드인/아웃으로 표시한다.
    /// 스테이지 진입 시 분위기 조성에 사용한다.
    /// </summary>
    public class NarratorUI : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────
        public static NarratorUI Instance { get; private set; }

        // ── Settings ─────────────────────────────────────────
        [Header("Animation")]
        [SerializeField] private float _fadeInDuration = 0.8f;
        [SerializeField] private float _fadeOutDuration = 0.8f;
        [SerializeField] private float _defaultHoldDuration = 2f;

        [Header("Visual")]
        [SerializeField] private int _mainFontSize = 36;
        [SerializeField] private int _subFontSize = 24;
        [SerializeField] private Color _mainTextColor = new Color(0.9f, 0.9f, 0.85f);
        [SerializeField] private Color _subTextColor = new Color(0.7f, 0.7f, 0.65f);
        [SerializeField] private Color _overlayColor = new Color(0f, 0f, 0f, 0.5f);

        // ── UI References (코드에서 생성) ─────────────────────
        private Canvas _canvas;
        private GameObject _narrationPanel;
        private CanvasGroup _canvasGroup;
        private TMP_Text _mainText;
        private TMP_Text _subText;
        private Image _overlayImage;

        // ── Runtime ──────────────────────────────────────────
        private Coroutine _narrationCoroutine;
        private Action _onComplete;

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
            Hide();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ============================================================
        //  Public API
        // ============================================================

        /// <summary>
        /// 화면 중앙에 내레이션 텍스트를 표시한다.
        /// </summary>
        /// <param name="text">메인 텍스트.</param>
        /// <param name="duration">유지 시간(초). 0 이하이면 기본값 사용.</param>
        /// <param name="onComplete">완료 콜백. (nullable)</param>
        public void ShowNarration(string text, float duration = 0f, Action onComplete = null)
        {
            ShowNarration(text, null, duration, onComplete);
        }

        /// <summary>
        /// 화면 중앙에 메인 텍스트와 서브 텍스트를 표시한다.
        /// </summary>
        /// <param name="text">메인 텍스트.</param>
        /// <param name="subText">서브 텍스트. (nullable)</param>
        /// <param name="duration">유지 시간(초). 0 이하이면 기본값 사용.</param>
        /// <param name="onComplete">완료 콜백. (nullable)</param>
        public void ShowNarration(string text, string subText, float duration = 0f, Action onComplete = null)
        {
            if (_narrationCoroutine != null)
                StopCoroutine(_narrationCoroutine);

            float hold = duration > 0f ? duration : _defaultHoldDuration;
            _onComplete = onComplete;
            _narrationCoroutine = StartCoroutine(NarrationSequence(text, subText, hold));
        }

        /// <summary>
        /// 현재 내레이션을 즉시 숨긴다.
        /// </summary>
        public void ForceHide()
        {
            if (_narrationCoroutine != null)
                StopCoroutine(_narrationCoroutine);

            Hide();
            _onComplete?.Invoke();
            _onComplete = null;
        }

        // ============================================================
        //  Narration Sequence
        // ============================================================

        private IEnumerator NarrationSequence(string text, string subText, float holdDuration)
        {
            _mainText.text = text ?? "";
            _subText.text = subText ?? "";
            _subText.gameObject.SetActive(!string.IsNullOrEmpty(subText));

            _narrationPanel.SetActive(true);
            _canvasGroup.alpha = 0f;

            // Fade In
            float elapsed = 0f;
            while (elapsed < _fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Clamp01(elapsed / _fadeInDuration);
                yield return null;
            }
            _canvasGroup.alpha = 1f;

            // Hold
            yield return new WaitForSecondsRealtime(holdDuration);

            // Fade Out
            elapsed = 0f;
            while (elapsed < _fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / _fadeOutDuration);
                yield return null;
            }

            Hide();

            _onComplete?.Invoke();
            _onComplete = null;
            _narrationCoroutine = null;
        }

        // ============================================================
        //  Helpers
        // ============================================================

        private void Hide()
        {
            if (_narrationPanel != null)
            {
                _narrationPanel.SetActive(false);
            }

            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;
        }

        // ============================================================
        //  UI Construction
        // ============================================================

        private void BuildUI()
        {
            // ── Canvas ──
            GameObject canvasGo = new GameObject("NarratorCanvas");
            canvasGo.transform.SetParent(transform);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 90;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            // ── Narration Panel (전체화면 오버레이) ──
            _narrationPanel = new GameObject("NarrationPanel");
            _narrationPanel.transform.SetParent(canvasGo.transform, false);

            RectTransform panelRect = _narrationPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            _overlayImage = _narrationPanel.AddComponent<Image>();
            _overlayImage.color = _overlayColor;
            _overlayImage.raycastTarget = false;

            _canvasGroup = _narrationPanel.AddComponent<CanvasGroup>();
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            // ── Main Text (화면 중앙) ──
            GameObject mainTextGo = new GameObject("MainText");
            mainTextGo.transform.SetParent(_narrationPanel.transform, false);
            RectTransform mainRect = mainTextGo.AddComponent<RectTransform>();
            mainRect.anchorMin = new Vector2(0.1f, 0.4f);
            mainRect.anchorMax = new Vector2(0.9f, 0.55f);
            mainRect.offsetMin = Vector2.zero;
            mainRect.offsetMax = Vector2.zero;

            _mainText = mainTextGo.AddComponent<TextMeshProUGUI>();
            _mainText.fontSize = _mainFontSize;
            _mainText.color = _mainTextColor;
            _mainText.alignment = TextAlignmentOptions.Center;
            _mainText.fontStyle = FontStyles.Italic;
            _mainText.enableWordWrapping = true;

            // ── Sub Text (메인 텍스트 아래) ──
            GameObject subTextGo = new GameObject("SubText");
            subTextGo.transform.SetParent(_narrationPanel.transform, false);
            RectTransform subRect = subTextGo.AddComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0.1f, 0.32f);
            subRect.anchorMax = new Vector2(0.9f, 0.4f);
            subRect.offsetMin = Vector2.zero;
            subRect.offsetMax = Vector2.zero;

            _subText = subTextGo.AddComponent<TextMeshProUGUI>();
            _subText.fontSize = _subFontSize;
            _subText.color = _subTextColor;
            _subText.alignment = TextAlignmentOptions.Center;
            _subText.enableWordWrapping = true;
        }
    }
}
