using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SoulCraft.Core;
using SoulCraft.Enemy;
using SoulCraft.UI;

namespace SoulCraft.Story
{
    /// <summary>
    /// 보스 등장/처치 연출 시스템.
    /// 카메라 이동, 이름 표시, 대사, HP바 등장, 처치 슬로모션 등을 제어한다.
    /// </summary>
    public class BossCutscene : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────
        public static BossCutscene Instance { get; private set; }

        // ── Settings: Intro ──────────────────────────────────
        [Header("Intro Settings")]
        [SerializeField] private float _dimAlpha = 0.6f;
        [SerializeField] private float _dimFadeDuration = 0.5f;
        [SerializeField] private float _cameraMoveSpeed = 5f;
        [SerializeField] private float _titleFadeInDuration = 0.5f;
        [SerializeField] private float _titleHoldDuration = 1.5f;
        [SerializeField] private float _titleFadeOutDuration = 0.5f;

        [Header("Scale Punch")]
        [SerializeField] private float _punchScale = 1.3f;
        [SerializeField] private float _punchDuration = 0.4f;

        [Header("Intro Visual")]
        [SerializeField] private int _titleFontSize = 48;
        [SerializeField] private int _subtitleFontSize = 32;
        [SerializeField] private Color _titleColor = new Color(1f, 0.85f, 0.3f);
        [SerializeField] private Color _subtitleColor = new Color(0.9f, 0.7f, 0.2f);

        // ── Settings: Death ──────────────────────────────────
        [Header("Death Settings")]
        [SerializeField] private float _slowMotionScale = 0.2f;
        [SerializeField] private float _slowMotionDuration = 2f;
        [SerializeField] private float _dissolveSpeed = 0.8f;

        [Header("Death Visual")]
        [SerializeField] private Color _lightPillarColor = new Color(1f, 0.95f, 0.6f, 0.9f);
        [SerializeField] private float _lightPillarHeight = 600f;
        [SerializeField] private float _lightPillarDuration = 1.5f;

        // ── Runtime ──────────────────────────────────────────
        public bool IsCutsceneActive { get; private set; }

        private Canvas _cutsceneCanvas;
        private Image _dimOverlay;
        private CanvasGroup _titleGroup;
        private TMP_Text _bossNameText;
        private TMP_Text _bossTitleText;
        private TMP_Text _soulAbsorbText;
        private Image _lightPillarImage;
        private CanvasGroup _deathUIGroup;

        private Transform _playerTransform;
        private Coroutine _currentCutscene;

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
            HideAll();
        }

        void Start()
        {
            FindPlayer();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ============================================================
        //  Public API: Boss Intro
        // ============================================================

        /// <summary>
        /// 보스 등장 연출을 시작한다.
        /// 연출 완료 후 onComplete 콜백이 호출된다.
        /// </summary>
        public void TriggerBossIntro(BossBase boss, Action onComplete = null)
        {
            if (boss == null || IsCutsceneActive) return;

            if (_currentCutscene != null)
                StopCoroutine(_currentCutscene);

            FindPlayer();
            _currentCutscene = StartCoroutine(BossIntroSequence(boss, onComplete));
        }

        /// <summary>
        /// 보스 처치 연출을 시작한다.
        /// </summary>
        public void TriggerBossDefeat(BossBase boss, string rewardSkillName = null, Action onComplete = null)
        {
            if (boss == null || IsCutsceneActive) return;

            if (_currentCutscene != null)
                StopCoroutine(_currentCutscene);

            _currentCutscene = StartCoroutine(BossDefeatSequence(boss, rewardSkillName, onComplete));
        }

        /// <summary>
        /// 보스 ID를 사용하여 StoryData에서 자동으로 데이터를 가져와 인트로를 실행한다.
        /// </summary>
        public void TriggerBossIntroById(BossBase boss, string bossId, Action onComplete = null)
        {
            if (boss == null || IsCutsceneActive) return;

            if (_currentCutscene != null)
                StopCoroutine(_currentCutscene);

            FindPlayer();
            _currentCutscene = StartCoroutine(BossIntroSequenceById(boss, bossId, onComplete));
        }

        // ============================================================
        //  Boss Intro Sequence
        // ============================================================

        private IEnumerator BossIntroSequence(BossBase boss, Action onComplete)
        {
            IsCutsceneActive = true;
            float prevTimeScale = Time.timeScale;

            // 플레이어 입력 비활성화는 호출 측에서 처리한다고 가정

            BossDialogueSet dialogueData = null;
            if (boss.Data != null)
                dialogueData = StoryData.GetBossDialogue(boss.Data.enemyId);

            string displayName = dialogueData?.BossDisplayName ?? (boss.Data != null ? boss.Data.enemyName : "BOSS");
            string title = dialogueData?.BossTitle ?? "";
            string introLine = dialogueData?.IntroLine ?? "";

            yield return BossIntroCore(boss, displayName, title, introLine, prevTimeScale, onComplete);
        }

        private IEnumerator BossIntroSequenceById(BossBase boss, string bossId, Action onComplete)
        {
            IsCutsceneActive = true;
            float prevTimeScale = Time.timeScale;

            BossDialogueSet dialogueData = StoryData.GetBossDialogue(bossId);
            string displayName = dialogueData.BossDisplayName;
            string title = dialogueData.BossTitle;
            string introLine = dialogueData.IntroLine;

            yield return BossIntroCore(boss, displayName, title, introLine, prevTimeScale, onComplete);
        }

        private IEnumerator BossIntroCore(BossBase boss, string displayName, string title,
            string introLine, float prevTimeScale, Action onComplete)
        {
            // 1. 화면 어두워짐
            yield return FadeOverlay(0f, _dimAlpha, _dimFadeDuration);

            // 2. 카메라 보스에게 이동
            CameraController cam = CameraController.Instance;
            Transform bossTransform = boss.transform;

            if (cam != null)
            {
                cam.SetTarget(bossTransform);
                yield return new WaitForSecondsRealtime(0.8f);
            }

            // 3. 보스 이름 + 칭호 표시
            _bossTitleText.text = title;
            _bossNameText.text = displayName;
            _titleGroup.gameObject.SetActive(true);

            // 텍스트 페이드인
            yield return FadeGroup(_titleGroup, 0f, 1f, _titleFadeInDuration);

            // 유지
            yield return new WaitForSecondsRealtime(_titleHoldDuration);

            // 텍스트 페이드아웃
            yield return FadeGroup(_titleGroup, 1f, 0f, _titleFadeOutDuration);
            _titleGroup.gameObject.SetActive(false);

            // 4. 보스 포효/등장 애니메이션 (스케일 펀치)
            yield return ScalePunch(bossTransform, _punchScale, _punchDuration);

            // 5. 짧은 대사
            if (!string.IsNullOrEmpty(introLine))
            {
                string bossName = displayName;
                DialogueLine[] lines = new[]
                {
                    new DialogueLine { speakerName = bossName, text = introLine }
                };

                bool dialogueFinished = false;
                if (DialogueSystem.Instance != null)
                {
                    // 대화 시스템이 Time.timeScale=0 으로 설정하므로 그대로 활용
                    DialogueSystem.Instance.ShowDialogue(lines, () => dialogueFinished = true);
                    while (!dialogueFinished)
                        yield return null;
                }
                else
                {
                    // 대화 시스템이 없으면 내레이션으로 대체
                    if (NarratorUI.Instance != null)
                    {
                        bool narrationDone = false;
                        NarratorUI.Instance.ShowNarration($"\"{introLine}\"", 2f, () => narrationDone = true);
                        while (!narrationDone)
                            yield return null;
                    }
                }
            }

            // 6. 카메라 플레이어에게 복귀
            if (cam != null && _playerTransform != null)
            {
                cam.SetTarget(_playerTransform);
                yield return new WaitForSecondsRealtime(0.5f);
            }

            // 7. 어두운 오버레이 제거
            yield return FadeOverlay(_dimAlpha, 0f, _dimFadeDuration);

            // 8. 보스 HP바 등장
            BossHPBar bossHPBar = FindAnyObjectByType<BossHPBar>();
            if (bossHPBar != null)
                bossHPBar.ShowBossHP(boss);

            // 9. 전투 시작
            Time.timeScale = prevTimeScale;
            IsCutsceneActive = false;
            _currentCutscene = null;

            GameManager gm = GameManager.Instance;
            if (gm != null)
                gm.ChangeState(GameState.BossFight);

            onComplete?.Invoke();
        }

        // ============================================================
        //  Boss Defeat Sequence
        // ============================================================

        private IEnumerator BossDefeatSequence(BossBase boss, string rewardSkillName, Action onComplete)
        {
            IsCutsceneActive = true;

            BossDialogueSet dialogueData = null;
            if (boss.Data != null)
                dialogueData = StoryData.GetBossDialogue(boss.Data.enemyId);

            string deathLine = dialogueData?.DeathLine ?? "";
            string skillName = rewardSkillName ?? dialogueData?.SkillRewardName ?? "";

            // 1. 슬로모션
            Time.timeScale = _slowMotionScale;
            float slowElapsed = 0f;
            while (slowElapsed < _slowMotionDuration)
            {
                slowElapsed += Time.unscaledDeltaTime;

                // 시간 경과에 따라 서서히 정상 속도로 복귀
                float t = slowElapsed / _slowMotionDuration;
                Time.timeScale = Mathf.Lerp(_slowMotionScale, 1f, t * t);

                yield return null;
            }
            Time.timeScale = 1f;

            // 카메라 강한 흔들림
            if (CameraController.Instance != null)
                CameraController.Instance.HeavyShake();

            // 2. 보스 디졸브 이펙트
            yield return DissolveEffect(boss);

            // 3. 보상 드롭 연출 (빛 기둥)
            Vector3 bossPos = boss.transform.position;
            yield return LightPillarEffect(bossPos);

            // 4. 보스 처치 대사 (짧게 내레이션으로 표시)
            if (!string.IsNullOrEmpty(deathLine))
            {
                if (NarratorUI.Instance != null)
                {
                    bool narrationDone = false;
                    NarratorUI.Instance.ShowNarration($"\"{deathLine}\"", 2f, () => narrationDone = true);
                    while (!narrationDone)
                        yield return null;
                }
            }

            // 5. "영혼 흡수" 텍스트 + 새 스킬 획득 알림
            _deathUIGroup.gameObject.SetActive(true);
            _soulAbsorbText.text = "영혼 흡수";
            yield return FadeGroup(_deathUIGroup, 0f, 1f, 0.5f);
            yield return new WaitForSecondsRealtime(1f);

            if (!string.IsNullOrEmpty(skillName))
            {
                _soulAbsorbText.text = $"영혼 흡수\n<size=70%><color=#AAffAA>새 스킬 획득: {skillName}</color></size>";
                yield return new WaitForSecondsRealtime(1.5f);
            }

            yield return FadeGroup(_deathUIGroup, 1f, 0f, 0.5f);
            _deathUIGroup.gameObject.SetActive(false);

            // 보스 HP바 숨김
            BossHPBar bossHPBar = FindAnyObjectByType<BossHPBar>();
            if (bossHPBar != null)
                bossHPBar.HideBossHP();

            IsCutsceneActive = false;
            _currentCutscene = null;

            onComplete?.Invoke();
        }

        // ============================================================
        //  Visual Effects
        // ============================================================

        /// <summary>스케일 펀치: 원래 크기 -> punchScale -> 원래 크기.</summary>
        private IEnumerator ScalePunch(Transform target, float punchScale, float duration)
        {
            if (target == null) yield break;

            Vector3 originalScale = target.localScale;
            Vector3 punchTarget = originalScale * punchScale;
            float half = duration * 0.5f;

            // Scale Up
            float elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / half;
                target.localScale = Vector3.Lerp(originalScale, punchTarget, t);
                yield return null;
            }

            // Scale Down
            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / half;
                target.localScale = Vector3.Lerp(punchTarget, originalScale, t);
                yield return null;
            }

            target.localScale = originalScale;
        }

        /// <summary>보스 디졸브: 스프라이트 알파를 서서히 0으로 + 스케일 축소.</summary>
        private IEnumerator DissolveEffect(BossBase boss)
        {
            if (boss == null) yield break;

            SpriteRenderer sr = boss.GetComponent<SpriteRenderer>();
            if (sr == null) yield break;

            Color startColor = sr.color;
            Vector3 startScale = boss.transform.localScale;
            float elapsed = 0f;
            float dissolveDuration = 1f / Mathf.Max(_dissolveSpeed, 0.1f);

            while (elapsed < dissolveDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dissolveDuration);

                // 알파 감소
                Color c = startColor;
                c.a = Mathf.Lerp(1f, 0f, t);
                sr.color = c;

                // 스케일 축소
                boss.transform.localScale = Vector3.Lerp(startScale, startScale * 0.5f, t);

                // 흰색으로 변해가기
                sr.color = Color.Lerp(new Color(c.r, c.g, c.b, c.a),
                    new Color(1f, 1f, 1f, c.a), t * 0.5f);

                yield return null;
            }

            sr.color = new Color(1f, 1f, 1f, 0f);
        }

        /// <summary>빛 기둥 연출.</summary>
        private IEnumerator LightPillarEffect(Vector3 worldPos)
        {
            _lightPillarImage.gameObject.SetActive(true);

            // 빛 기둥의 위치를 월드 좌표에서 스크린 좌표로 변환하여 배치
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                Vector2 screenPos = mainCam.WorldToScreenPoint(worldPos);
                RectTransform rt = _lightPillarImage.GetComponent<RectTransform>();

                // 스크린 좌표를 Canvas 로컬 좌표로 변환
                RectTransform canvasRect = _cutsceneCanvas.GetComponent<RectTransform>();
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, screenPos, null, out Vector2 localPos);
                rt.anchoredPosition = new Vector2(localPos.x, 0f);
            }

            // 페이드인
            Color col = _lightPillarColor;
            float elapsed = 0f;
            float fadeIn = 0.3f;
            while (elapsed < fadeIn)
            {
                elapsed += Time.unscaledDeltaTime;
                float a = Mathf.Lerp(0f, col.a, elapsed / fadeIn);
                _lightPillarImage.color = new Color(col.r, col.g, col.b, a);
                yield return null;
            }

            // 유지
            yield return new WaitForSecondsRealtime(_lightPillarDuration);

            // 페이드아웃
            elapsed = 0f;
            float fadeOut = 0.5f;
            while (elapsed < fadeOut)
            {
                elapsed += Time.unscaledDeltaTime;
                float a = Mathf.Lerp(col.a, 0f, elapsed / fadeOut);
                _lightPillarImage.color = new Color(col.r, col.g, col.b, a);
                yield return null;
            }

            _lightPillarImage.gameObject.SetActive(false);
        }

        // ============================================================
        //  Overlay / Fade Helpers
        // ============================================================

        private IEnumerator FadeOverlay(float fromAlpha, float toAlpha, float duration)
        {
            if (_dimOverlay == null) yield break;

            _dimOverlay.gameObject.SetActive(true);
            Color c = _dimOverlay.color;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                c.a = Mathf.Lerp(fromAlpha, toAlpha, t);
                _dimOverlay.color = c;
                yield return null;
            }

            c.a = toAlpha;
            _dimOverlay.color = c;

            if (Mathf.Approximately(toAlpha, 0f))
                _dimOverlay.gameObject.SetActive(false);
        }

        private IEnumerator FadeGroup(CanvasGroup group, float from, float to, float duration)
        {
            if (group == null) yield break;

            float elapsed = 0f;
            group.alpha = from;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            group.alpha = to;
        }

        // ============================================================
        //  Helpers
        // ============================================================

        private void FindPlayer()
        {
            if (_playerTransform != null) return;

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                _playerTransform = player.transform;
        }

        private void HideAll()
        {
            if (_dimOverlay != null)
            {
                Color c = _dimOverlay.color;
                c.a = 0f;
                _dimOverlay.color = c;
                _dimOverlay.gameObject.SetActive(false);
            }

            if (_titleGroup != null)
            {
                _titleGroup.alpha = 0f;
                _titleGroup.gameObject.SetActive(false);
            }

            if (_deathUIGroup != null)
            {
                _deathUIGroup.alpha = 0f;
                _deathUIGroup.gameObject.SetActive(false);
            }

            if (_lightPillarImage != null)
                _lightPillarImage.gameObject.SetActive(false);
        }

        // ============================================================
        //  UI Construction
        // ============================================================

        private void BuildUI()
        {
            // ── Canvas ──
            GameObject canvasGo = new GameObject("BossCutsceneCanvas");
            canvasGo.transform.SetParent(transform);
            _cutsceneCanvas = canvasGo.AddComponent<Canvas>();
            _cutsceneCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _cutsceneCanvas.sortingOrder = 95;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();
            RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();

            // ── Dim Overlay (전체화면 어두운 오버레이) ──
            GameObject dimGo = new GameObject("DimOverlay");
            dimGo.transform.SetParent(canvasGo.transform, false);
            RectTransform dimRect = dimGo.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            _dimOverlay = dimGo.AddComponent<Image>();
            _dimOverlay.color = new Color(0f, 0f, 0f, 0f);
            _dimOverlay.raycastTarget = true;

            // ── Title Group (보스 이름 + 칭호) ──
            GameObject titleGroupGo = new GameObject("TitleGroup");
            titleGroupGo.transform.SetParent(canvasGo.transform, false);
            RectTransform titleGroupRect = titleGroupGo.AddComponent<RectTransform>();
            titleGroupRect.anchorMin = new Vector2(0.1f, 0.4f);
            titleGroupRect.anchorMax = new Vector2(0.9f, 0.6f);
            titleGroupRect.offsetMin = Vector2.zero;
            titleGroupRect.offsetMax = Vector2.zero;
            _titleGroup = titleGroupGo.AddComponent<CanvasGroup>();

            // 칭호 텍스트 (위)
            GameObject titleTextGo = new GameObject("BossTitleText");
            titleTextGo.transform.SetParent(titleGroupGo.transform, false);
            RectTransform titleTextRect = titleTextGo.AddComponent<RectTransform>();
            titleTextRect.anchorMin = new Vector2(0f, 0.5f);
            titleTextRect.anchorMax = new Vector2(1f, 1f);
            titleTextRect.offsetMin = Vector2.zero;
            titleTextRect.offsetMax = Vector2.zero;

            _bossTitleText = titleTextGo.AddComponent<TextMeshProUGUI>();
            _bossTitleText.fontSize = _subtitleFontSize;
            _bossTitleText.color = _subtitleColor;
            _bossTitleText.alignment = TextAlignmentOptions.Center;
            _bossTitleText.fontStyle = FontStyles.Italic;

            // 보스 이름 텍스트 (아래)
            GameObject nameTextGo = new GameObject("BossNameText");
            nameTextGo.transform.SetParent(titleGroupGo.transform, false);
            RectTransform nameTextRect = nameTextGo.AddComponent<RectTransform>();
            nameTextRect.anchorMin = new Vector2(0f, 0f);
            nameTextRect.anchorMax = new Vector2(1f, 0.5f);
            nameTextRect.offsetMin = Vector2.zero;
            nameTextRect.offsetMax = Vector2.zero;

            _bossNameText = nameTextGo.AddComponent<TextMeshProUGUI>();
            _bossNameText.fontSize = _titleFontSize;
            _bossNameText.color = _titleColor;
            _bossNameText.alignment = TextAlignmentOptions.Center;
            _bossNameText.fontStyle = FontStyles.Bold;

            // ── Light Pillar (빛 기둥) ──
            GameObject pillarGo = new GameObject("LightPillar");
            pillarGo.transform.SetParent(canvasGo.transform, false);
            RectTransform pillarRect = pillarGo.AddComponent<RectTransform>();
            pillarRect.anchorMin = new Vector2(0.5f, 0f);
            pillarRect.anchorMax = new Vector2(0.5f, 1f);
            pillarRect.pivot = new Vector2(0.5f, 0f);
            pillarRect.sizeDelta = new Vector2(80f, _lightPillarHeight);
            pillarRect.anchoredPosition = Vector2.zero;

            _lightPillarImage = pillarGo.AddComponent<Image>();
            _lightPillarImage.color = _lightPillarColor;
            _lightPillarImage.raycastTarget = false;

            // ── Death UI Group (영혼 흡수 텍스트) ──
            GameObject deathGroupGo = new GameObject("DeathUIGroup");
            deathGroupGo.transform.SetParent(canvasGo.transform, false);
            RectTransform deathGroupRect = deathGroupGo.AddComponent<RectTransform>();
            deathGroupRect.anchorMin = new Vector2(0.1f, 0.35f);
            deathGroupRect.anchorMax = new Vector2(0.9f, 0.65f);
            deathGroupRect.offsetMin = Vector2.zero;
            deathGroupRect.offsetMax = Vector2.zero;
            _deathUIGroup = deathGroupGo.AddComponent<CanvasGroup>();

            // 배경 패널
            Image deathBg = deathGroupGo.AddComponent<Image>();
            deathBg.color = new Color(0f, 0f, 0f, 0.7f);
            deathBg.raycastTarget = false;

            // 영혼 흡수 텍스트
            GameObject absorbTextGo = new GameObject("SoulAbsorbText");
            absorbTextGo.transform.SetParent(deathGroupGo.transform, false);
            RectTransform absorbRect = absorbTextGo.AddComponent<RectTransform>();
            absorbRect.anchorMin = Vector2.zero;
            absorbRect.anchorMax = Vector2.one;
            absorbRect.offsetMin = new Vector2(20f, 20f);
            absorbRect.offsetMax = new Vector2(-20f, -20f);

            _soulAbsorbText = absorbTextGo.AddComponent<TextMeshProUGUI>();
            _soulAbsorbText.fontSize = 42;
            _soulAbsorbText.color = new Color(0.8f, 0.6f, 1f);
            _soulAbsorbText.alignment = TextAlignmentOptions.Center;
            _soulAbsorbText.fontStyle = FontStyles.Bold;
            _soulAbsorbText.enableWordWrapping = true;
            _soulAbsorbText.richText = true;
        }
    }
}
