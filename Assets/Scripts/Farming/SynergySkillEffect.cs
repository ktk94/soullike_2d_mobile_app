using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SoulCraft.Core;
using SoulCraft.Combat;

namespace SoulCraft.Farming
{
    /// <summary>
    /// 연계 스킬 발동 시의 특수 효과(컷인 연출, 카메라 줌, 슬로모션)를 처리한다.
    /// 일반 스킬보다 화려한 이펙트를 제공한다.
    /// </summary>
    public class SynergySkillEffect : MonoBehaviour
    {
        // ── Singleton ───────────────────────────────────
        public static SynergySkillEffect Instance { get; private set; }

        // ── Inspector: Cut-In ───────────────────────────

        [Header("Cut-In")]
        [Tooltip("컷인 연출 캔버스 오브젝트")]
        [SerializeField] private GameObject _cutInPanel;

        [Tooltip("스킬 이름 표시 텍스트")]
        [SerializeField] private TMP_Text _cutInNameText;

        [Tooltip("스킬 타입 표시 텍스트 (예: 공격형 연계)")]
        [SerializeField] private TMP_Text _cutInTypeText;

        [Tooltip("스킬 아이콘")]
        [SerializeField] private Image _cutInIcon;

        [Tooltip("컷인 배경 이미지")]
        [SerializeField] private Image _cutInBackground;

        [Tooltip("컷인 연출 총 지속 시간")]
        [SerializeField] private float _cutInDuration = 0.8f;

        [Tooltip("컷인 텍스트 표시까지의 딜레이")]
        [SerializeField] private float _cutInTextDelay = 0.15f;

        // ── Inspector: Camera ───────────────────────────

        [Header("Camera Zoom")]
        [Tooltip("줌 시 카메라 orthographicSize 감소량")]
        [SerializeField] private float _zoomAmount = 1.5f;

        [Tooltip("줌 인 소요 시간")]
        [SerializeField] private float _zoomInDuration = 0.15f;

        [Tooltip("줌 유지 시간")]
        [SerializeField] private float _zoomHoldDuration = 0.3f;

        [Tooltip("줌 복귀 소요 시간")]
        [SerializeField] private float _zoomOutDuration = 0.3f;

        // ── Inspector: Slow Motion ──────────────────────

        [Header("Slow Motion")]
        [Tooltip("슬로모션 시 Time.timeScale 값")]
        [SerializeField] private float _slowMotionScale = 0.3f;

        [Tooltip("슬로모션 지속 시간 (비스케일)")]
        [SerializeField] private float _slowMotionDuration = 0.5f;

        [Tooltip("슬로모션 복귀 보간 시간")]
        [SerializeField] private float _slowMotionRecoverDuration = 0.2f;

        // ── Inspector: Effects ──────────────────────────

        [Header("Effects")]
        [Tooltip("연계 스킬 전용 파티클 이펙트 프리팹")]
        [SerializeField] private GameObject _synergyBurstEffectPrefab;

        [Tooltip("이펙트 스케일 배율 (일반 스킬 대비)")]
        [SerializeField] private float _effectScaleMultiplier = 1.5f;

        [Tooltip("화면 플래시 이미지 (전체 화면)")]
        [SerializeField] private Image _flashImage;

        [Tooltip("플래시 지속 시간")]
        [SerializeField] private float _flashDuration = 0.15f;

        [Header("Screen Shake")]
        [Tooltip("연계 스킬 전용 화면 흔들림 강도")]
        [SerializeField] private float _shakeIntensity = 0.5f;

        [Tooltip("화면 흔들림 지속 시간")]
        [SerializeField] private float _shakeDuration = 0.3f;

        // ── Runtime ─────────────────────────────────────

        private Coroutine _currentEffectRoutine;
        private float _originalTimeScale = 1f;
        private float _originalCameraSize;
        private bool _isPerformingEffect;

        // ── Lifecycle ───────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // 초기 숨김
            if (_cutInPanel != null)
                _cutInPanel.SetActive(false);

            if (_flashImage != null)
            {
                var c = _flashImage.color;
                c.a = 0f;
                _flashImage.color = c;
            }
        }

        void OnEnable()
        {
            // 연계 스킬 사용 이벤트 구독
            GameEventSystem.Subscribe<SkillUsedEvent>(OnSkillUsed);
        }

        void OnDisable()
        {
            GameEventSystem.Unsubscribe<SkillUsedEvent>(OnSkillUsed);

            // 이펙트 중 비활성화되면 타임스케일 복원
            RestoreTimeScale();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Event Handler ───────────────────────────────

        /// <summary>
        /// 스킬 사용 이벤트를 감지하여, 연계 스킬이면 특수 연출을 실행한다.
        /// </summary>
        private void OnSkillUsed(SkillUsedEvent evt)
        {
            if (SynergyManager.Instance == null) return;

            // 현재 활성 시너지 중에서 사용된 스킬 찾기
            foreach (var synergy in SynergyManager.Instance.ActiveSynergies)
            {
                if (synergy.resultSkill != null &&
                    synergy.resultSkill.skillId == evt.SkillId)
                {
                    PlaySynergyEffect(synergy);
                    break;
                }
            }
        }

        // ── Public API ──────────────────────────────────

        /// <summary>
        /// 연계 스킬의 특수 연출을 재생한다.
        /// 외부에서 직접 호출할 수도 있다.
        /// </summary>
        public void PlaySynergyEffect(SynergyData synergy)
        {
            if (synergy == null || _isPerformingEffect) return;

            if (_currentEffectRoutine != null)
                StopCoroutine(_currentEffectRoutine);

            _currentEffectRoutine = StartCoroutine(SynergyEffectSequence(synergy));
        }

        /// <summary>
        /// 연출이 진행 중인지 확인한다.
        /// </summary>
        public bool IsPerformingEffect => _isPerformingEffect;

        // ── Effect Sequence ─────────────────────────────

        /// <summary>
        /// 연계 스킬 발동 시 전체 연출 시퀀스:
        /// 1. 슬로모션 시작
        /// 2. 카메라 줌 인
        /// 3. 컷인 연출 (스킬 이름 표시)
        /// 4. 화면 플래시
        /// 5. 파티클 이펙트 + 화면 흔들림
        /// 6. 슬로모션/카메라 복귀
        /// </summary>
        private IEnumerator SynergyEffectSequence(SynergyData synergy)
        {
            _isPerformingEffect = true;

            // ─── Phase 1: 슬로모션 & 줌 인 ───
            StartSlowMotion();
            StartCoroutine(CameraZoomIn());

            // ─── Phase 2: 컷인 연출 ───
            yield return StartCoroutine(ShowCutIn(synergy));

            // ─── Phase 3: 플래시 ───
            StartCoroutine(ScreenFlash(synergy.cutInColor));

            // ─── Phase 4: 이펙트 스폰 & 화면 흔들림 ───
            SpawnSynergyEffect(synergy);
            ShakeCamera();

            // ─── Phase 5: 슬로모션 복귀 ───
            yield return StartCoroutine(RecoverSlowMotion());

            // ─── Phase 6: 줌 복귀 ───
            yield return StartCoroutine(CameraZoomOut());

            _isPerformingEffect = false;
            _currentEffectRoutine = null;
        }

        // ── Cut-In ──────────────────────────────────────

        /// <summary>
        /// 스킬 이름을 화면 중앙에 컷인 연출로 표시한다.
        /// </summary>
        private IEnumerator ShowCutIn(SynergyData synergy)
        {
            if (_cutInPanel == null)
            {
                yield return new WaitForSecondsRealtime(_cutInDuration);
                yield break;
            }

            _cutInPanel.SetActive(true);

            // 배경 색상
            if (_cutInBackground != null)
                _cutInBackground.color = synergy.cutInColor;

            // 아이콘
            if (_cutInIcon != null)
            {
                _cutInIcon.sprite = synergy.icon;
                _cutInIcon.enabled = synergy.icon != null;
            }

            // 텍스트는 살짝 딜레이 후 표시 (슬라이드 인 느낌)
            if (_cutInNameText != null)
            {
                _cutInNameText.text = "";
                _cutInNameText.color = Color.clear;
            }

            if (_cutInTypeText != null)
            {
                _cutInTypeText.text = GetSynergyTypeLabel(synergy.synergyType);
                _cutInTypeText.color = Color.clear;
            }

            // 약간의 딜레이 후 텍스트 페이드 인
            yield return new WaitForSecondsRealtime(_cutInTextDelay);

            if (_cutInNameText != null)
            {
                _cutInNameText.text = synergy.resultSkill != null
                    ? synergy.resultSkill.skillName
                    : synergy.synergyName;
                _cutInNameText.color = Color.white;
            }

            if (_cutInTypeText != null)
                _cutInTypeText.color = new Color(1f, 1f, 1f, 0.7f);

            // 컷인 유지
            yield return new WaitForSecondsRealtime(_cutInDuration - _cutInTextDelay);

            // 페이드 아웃
            float fadeTime = 0.15f;
            float elapsed = 0f;
            CanvasGroup cg = _cutInPanel.GetComponent<CanvasGroup>();

            if (cg != null)
            {
                while (elapsed < fadeTime)
                {
                    elapsed += Time.unscaledDeltaTime;
                    cg.alpha = 1f - (elapsed / fadeTime);
                    yield return null;
                }
                cg.alpha = 1f; // 다음 사용을 위해 복원
            }

            _cutInPanel.SetActive(false);
        }

        // ── Slow Motion ─────────────────────────────────

        private void StartSlowMotion()
        {
            _originalTimeScale = Time.timeScale;
            Time.timeScale = _slowMotionScale;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
        }

        private IEnumerator RecoverSlowMotion()
        {
            float elapsed = 0f;
            float startScale = Time.timeScale;

            while (elapsed < _slowMotionRecoverDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / _slowMotionRecoverDuration;
                Time.timeScale = Mathf.Lerp(startScale, _originalTimeScale, t);
                Time.fixedDeltaTime = 0.02f * Time.timeScale;
                yield return null;
            }

            RestoreTimeScale();
        }

        private void RestoreTimeScale()
        {
            Time.timeScale = _originalTimeScale;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
        }

        // ── Camera Zoom ─────────────────────────────────

        private IEnumerator CameraZoomIn()
        {
            var cam = Camera.main;
            if (cam == null || !cam.orthographic) yield break;

            _originalCameraSize = cam.orthographicSize;
            float targetSize = _originalCameraSize - _zoomAmount;
            float elapsed = 0f;

            while (elapsed < _zoomInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = EaseOutQuad(elapsed / _zoomInDuration);
                cam.orthographicSize = Mathf.Lerp(_originalCameraSize, targetSize, t);
                yield return null;
            }

            cam.orthographicSize = targetSize;

            // 줌 유지
            yield return new WaitForSecondsRealtime(_zoomHoldDuration);
        }

        private IEnumerator CameraZoomOut()
        {
            var cam = Camera.main;
            if (cam == null || !cam.orthographic) yield break;

            float currentSize = cam.orthographicSize;
            float elapsed = 0f;

            while (elapsed < _zoomOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = EaseInOutQuad(elapsed / _zoomOutDuration);
                cam.orthographicSize = Mathf.Lerp(currentSize, _originalCameraSize, t);
                yield return null;
            }

            cam.orthographicSize = _originalCameraSize;
        }

        // ── Screen Flash ────────────────────────────────

        private IEnumerator ScreenFlash(Color flashColor)
        {
            if (_flashImage == null) yield break;

            // 강한 플래시
            flashColor.a = 0.6f;
            _flashImage.color = flashColor;

            float elapsed = 0f;
            while (elapsed < _flashDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / _flashDuration;
                var c = flashColor;
                c.a = Mathf.Lerp(0.6f, 0f, t);
                _flashImage.color = c;
                yield return null;
            }

            var clear = flashColor;
            clear.a = 0f;
            _flashImage.color = clear;
        }

        // ── Particle Effect ─────────────────────────────

        /// <summary>
        /// 연계 스킬 전용 파티클 이펙트를 스폰한다.
        /// 일반 스킬보다 크고 화려하다.
        /// </summary>
        private void SpawnSynergyEffect(SynergyData synergy)
        {
            // 시너지 전용 이펙트가 있으면 우선 사용
            GameObject prefab = synergy.synergyEffectPrefab;

            // 없으면 범용 버스트 이펙트
            if (prefab == null)
                prefab = _synergyBurstEffectPrefab;

            if (prefab == null) return;

            var player = GameObject.FindGameObjectWithTag("Player");
            Vector3 spawnPos = player != null ? player.transform.position : transform.position;

            var instance = Instantiate(prefab, spawnPos, Quaternion.identity);

            // 스케일 증가 (일반 스킬 대비 화려하게)
            instance.transform.localScale *= _effectScaleMultiplier;

            // 자동 제거
            Destroy(instance, 3f);
        }

        // ── Screen Shake ────────────────────────────────

        private void ShakeCamera()
        {
            if (CameraController.Instance != null)
            {
                CameraController.Instance.Shake(_shakeIntensity, _shakeDuration);
            }
        }

        // ── Easing Functions ────────────────────────────

        private static float EaseOutQuad(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }

        private static float EaseInOutQuad(float t)
        {
            return t < 0.5f
                ? 2f * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
        }

        // ── Helpers ─────────────────────────────────────

        private string GetSynergyTypeLabel(SynergyType type)
        {
            return type switch
            {
                SynergyType.OffensiveCombo => "OFFENSIVE COMBO",
                SynergyType.DefensiveCombo => "DEFENSIVE COMBO",
                SynergyType.UtilityCombo => "UTILITY COMBO",
                _ => "SYNERGY"
            };
        }
    }
}
