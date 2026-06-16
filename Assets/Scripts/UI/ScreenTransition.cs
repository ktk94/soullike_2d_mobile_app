using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SoulCraft.UI
{
    /// <summary>
    /// 화면 전환 효과 (페이드 인/아웃).
    /// 스테이지 진입, 보스전 시작, 사망 시 사용.
    /// 코루틴 기반 싱글톤.
    /// </summary>
    public class ScreenTransition : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────
        public static ScreenTransition Instance { get; private set; }

        // ── Inspector ────────────────────────────────────────
        [Header("UI References")]
        [SerializeField] private CanvasGroup _fadeCanvasGroup;
        [SerializeField] private Image _fadeImage;

        [Header("Default Settings")]
        [SerializeField] private float _defaultFadeDuration = 0.5f;
        [SerializeField] private Color _defaultFadeColor = Color.black;

        [Header("Boss Intro")]
        [SerializeField] private Color _bossFadeColor = new Color(0.15f, 0f, 0f);
        [SerializeField] private float _bossIntroDuration = 0.8f;

        [Header("Death")]
        [SerializeField] private Color _deathFadeColor = new Color(0.5f, 0f, 0f);
        [SerializeField] private float _deathFadeDuration = 1.2f;

        // ── Runtime ──────────────────────────────────────────
        /// <summary>현재 전환이 진행 중인지 여부.</summary>
        public bool IsTransitioning { get; private set; }

        private Coroutine _currentTransition;

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

            // 시작 시 완전 투명
            SetAlpha(0f);
            SetInteractable(false);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ============================================================
        //  Public API: Basic Fade
        // ============================================================

        /// <summary>
        /// 페이드 아웃 (화면이 어두워짐).
        /// </summary>
        public void FadeOut(float duration = -1f, Color? color = null, Action onComplete = null)
        {
            float dur = duration >= 0f ? duration : _defaultFadeDuration;
            Color col = color ?? _defaultFadeColor;
            StartTransition(FadeCoroutine(0f, 1f, dur, col, onComplete));
        }

        /// <summary>
        /// 페이드 인 (화면이 밝아짐).
        /// </summary>
        public void FadeIn(float duration = -1f, Action onComplete = null)
        {
            float dur = duration >= 0f ? duration : _defaultFadeDuration;
            StartTransition(FadeCoroutine(1f, 0f, dur, _defaultFadeColor, onComplete));
        }

        /// <summary>
        /// 페이드 아웃 -> 중간 콜백 -> 페이드 인 순서로 진행.
        /// 씬 전환이나 스테이지 이동 시 사용.
        /// </summary>
        public void FadeOutIn(Action onMiddle, float fadeDuration = -1f,
            Color? color = null, float holdDuration = 0.2f, Action onComplete = null)
        {
            float dur = fadeDuration >= 0f ? fadeDuration : _defaultFadeDuration;
            Color col = color ?? _defaultFadeColor;
            StartTransition(FadeOutInCoroutine(onMiddle, dur, col, holdDuration, onComplete));
        }

        // ============================================================
        //  Public API: Specialized Transitions
        // ============================================================

        /// <summary>
        /// 스테이지 진입 전환.
        /// </summary>
        public void StageEnterTransition(Action onMiddle = null, Action onComplete = null)
        {
            FadeOutIn(onMiddle, _defaultFadeDuration, _defaultFadeColor, 0.3f, onComplete);
        }

        /// <summary>
        /// 보스전 시작 전환. 붉은 톤의 페이드.
        /// </summary>
        public void BossIntroTransition(Action onMiddle = null, Action onComplete = null)
        {
            FadeOutIn(onMiddle, _bossIntroDuration, _bossFadeColor, 0.5f, onComplete);
        }

        /// <summary>
        /// 플레이어 사망 전환. 느린 페이드 아웃.
        /// </summary>
        public void DeathTransition(Action onComplete = null)
        {
            StartTransition(DeathFadeCoroutine(onComplete));
        }

        // ============================================================
        //  Coroutines
        // ============================================================

        private IEnumerator FadeCoroutine(float fromAlpha, float toAlpha,
            float duration, Color color, Action onComplete)
        {
            IsTransitioning = true;
            SetFadeColor(color);
            SetInteractable(true);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float alpha = Mathf.Lerp(fromAlpha, toAlpha, t);
                SetAlpha(alpha);
                yield return null;
            }

            SetAlpha(toAlpha);

            // 완전 투명이면 입력 차단 해제
            if (Mathf.Approximately(toAlpha, 0f))
                SetInteractable(false);

            IsTransitioning = false;
            onComplete?.Invoke();
        }

        private IEnumerator FadeOutInCoroutine(Action onMiddle, float fadeDuration,
            Color color, float holdDuration, Action onComplete)
        {
            IsTransitioning = true;
            SetFadeColor(color);
            SetInteractable(true);

            // 1. Fade Out
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                SetAlpha(Mathf.Lerp(0f, 1f, t));
                yield return null;
            }
            SetAlpha(1f);

            // 2. Hold (중간 콜백 실행)
            onMiddle?.Invoke();
            yield return new WaitForSecondsRealtime(holdDuration);

            // 3. Fade In
            elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                SetAlpha(Mathf.Lerp(1f, 0f, t));
                yield return null;
            }
            SetAlpha(0f);
            SetInteractable(false);

            IsTransitioning = false;
            onComplete?.Invoke();
        }

        private IEnumerator DeathFadeCoroutine(Action onComplete)
        {
            IsTransitioning = true;
            SetFadeColor(_deathFadeColor);
            SetInteractable(true);

            // 느린 페이드 아웃
            float elapsed = 0f;
            while (elapsed < _deathFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _deathFadeDuration);
                // Ease-in 커브 (처음엔 느리다가 나중에 빨라짐)
                float alpha = t * t;
                SetAlpha(alpha);
                yield return null;
            }
            SetAlpha(1f);

            // 잠시 유지
            yield return new WaitForSecondsRealtime(0.8f);

            IsTransitioning = false;
            onComplete?.Invoke();
        }

        // ============================================================
        //  Helpers
        // ============================================================

        private void StartTransition(IEnumerator coroutine)
        {
            // 이전 전환이 진행 중이면 중단
            if (_currentTransition != null)
                StopCoroutine(_currentTransition);

            _currentTransition = StartCoroutine(coroutine);
        }

        private void SetAlpha(float alpha)
        {
            if (_fadeCanvasGroup != null)
                _fadeCanvasGroup.alpha = alpha;
        }

        private void SetFadeColor(Color color)
        {
            if (_fadeImage != null)
                _fadeImage.color = color;
        }

        /// <summary>
        /// 전환 중에는 raycastTarget을 활성화하여 뒤 UI 입력을 차단한다.
        /// </summary>
        private void SetInteractable(bool block)
        {
            if (_fadeCanvasGroup != null)
            {
                _fadeCanvasGroup.blocksRaycasts = block;
                _fadeCanvasGroup.interactable = false; // 항상 비활성 (클릭 방지)
            }
        }
    }
}
