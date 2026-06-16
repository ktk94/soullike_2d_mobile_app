using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SoulCraft.Core;

namespace SoulCraft.Player
{
    /// <summary>
    /// 적 처치 시 EXP 획득 및 레벨업 시스템.
    /// EnemyDeathEvent / EnemyRewardEvent를 구독하여 경험치를 자동 획득하고,
    /// 레벨업 시 스탯 증가 + 이펙트 + HUD EXP 바 갱신을 처리한다.
    /// </summary>
    public class LevelUpSystem : MonoBehaviour
    {
        // ── EXP 보상 테이블 ──────────────────────────────────
        private static readonly Dictionary<string, int> ExpRewardTable = new()
        {
            { "slime", 15 },
            { "skeleton", 25 },
            { "bat", 20 },
            { "boss_elder_grove", 300 },
            { "boss_frost_knight", 400 },
            { "boss_shadow_lord", 500 },
            { "boss", 200 }
        };

        // ── 레벨업 스탯 증가량 ───────────────────────────────
        private const int HpPerLevel = 8;
        private const int AttackPerLevel = 2;
        private const int DefensePerLevel = 1;

        // ── 레벨업 이펙트 설정 ───────────────────────────────
        private const float LevelUpEffectDuration = 1.5f;
        private const float LevelUpPillarWidth = 0.6f;
        private const float LevelUpPillarHeight = 4f;
        private static readonly Color ColGoldLight = new(1f, 0.85f, 0.2f, 0.7f);
        private static readonly Color ColGoldBright = new(1f, 0.95f, 0.5f, 0.9f);

        // ── 레벨업 텍스트 설정 ───────────────────────────────
        private const float LevelUpTextDuration = 1.5f;
        private static readonly Color ColLevelUpText = new(1f, 0.9f, 0.1f, 1f);

        // ── 런타임 참조 ────────────────────────────────────────
        private PlayerStats _stats;
        private int _previousLevel;

        // ── HUD EXP 바 ──────────────────────────────────────
        private GameObject _expBarRoot;
        private RectTransform _expFillRect;
        private Image _expFillImage;
        private TMP_Text _levelText;
        private TMP_Text _expText;

        // ── Lifecycle ─────────────────────────────────────────

        void Awake()
        {
            _stats = GetComponent<PlayerStats>();
        }

        void Start()
        {
            if (_stats == null)
            {
                Debug.LogError("[LevelUpSystem] PlayerStats not found on this GameObject.");
                enabled = false;
                return;
            }

            _previousLevel = _stats.Level;

            // 이벤트 구독
            GameEventSystem.Subscribe<EnemyDeathEvent>(OnEnemyDeath);
            GameEventSystem.Subscribe<EnemyRewardEvent>(OnEnemyReward);

            // PlayerStats의 레벨업 이벤트 구독
            _stats.OnLevelUp += OnLevelUp;

            // HUD에 EXP 바 생성
            CreateExpBarUI();

            // 초기 갱신
            UpdateExpBar();
        }

        void OnDestroy()
        {
            GameEventSystem.Unsubscribe<EnemyDeathEvent>(OnEnemyDeath);
            GameEventSystem.Unsubscribe<EnemyRewardEvent>(OnEnemyReward);

            if (_stats != null)
                _stats.OnLevelUp -= OnLevelUp;

            if (_expBarRoot != null)
                Destroy(_expBarRoot);
        }

        void Update()
        {
            UpdateExpBar();
        }

        // ── 이벤트 핸들러 ──────────────────────────────────────

        private void OnEnemyDeath(EnemyDeathEvent evt)
        {
            // EnemyId에서 기본 타입 추출하여 보상 결정
            string enemyId = evt.EnemyId;
            if (string.IsNullOrEmpty(enemyId)) return;

            int expReward = GetExpReward(enemyId);
            if (expReward > 0 && _stats != null)
            {
                _stats.AddExp(expReward);
            }
        }

        private void OnEnemyReward(EnemyRewardEvent evt)
        {
            // EnemyRewardEvent에서 직접 EXP가 올 경우에도 처리
            // (EnemyBase.Die에서 발행하는 보상 이벤트)
            // 중복 방지: EnemyDeathEvent에서 이미 처리했으므로 여기서는 골드만 처리
            if (_stats != null && evt.Gold > 0)
            {
                _stats.Gold += evt.Gold;

                // HUD 골드 갱신
                var hud = SoulCraft.UI.HUDManager.Instance;
                if (hud != null)
                    hud.UpdateGoldDisplay(_stats.Gold);
            }
        }

        private void OnLevelUp(int newLevel)
        {
            // 스탯 증가 (PlayerStats 자체 RecalculateStats 외 추가 보너스)
            _stats.BonusMaxHp += HpPerLevel;
            _stats.BonusAttack += AttackPerLevel;
            _stats.BonusDefense += DefensePerLevel;
            _stats.RecalculateStats();

            // HP 전체 회복
            _stats.FullHeal();

            // 레벨업 이펙트
            StartCoroutine(LevelUpEffectCoroutine());

            // 레벨업 텍스트
            StartCoroutine(LevelUpTextCoroutine());

            // 사운드 재생 시도
            TryPlayLevelUpSound();

            // 세이브
            TrySave();

            _previousLevel = newLevel;

            Debug.Log($"[LevelUpSystem] LEVEL UP! Lv.{newLevel}");
        }

        // ── EXP 보상 계산 ──────────────────────────────────────

        private int GetExpReward(string enemyId)
        {
            // 정확한 매칭 먼저 시도
            if (ExpRewardTable.TryGetValue(enemyId, out int reward))
                return reward;

            // 부분 매칭 (slime_0, skeleton_2 등)
            string lower = enemyId.ToLower();
            foreach (var kvp in ExpRewardTable)
            {
                if (lower.Contains(kvp.Key))
                    return kvp.Value;
            }

            // 기본값
            return 10;
        }

        // ── 레벨업 이펙트 (금색 빛 기둥) ────────────────────────

        private IEnumerator LevelUpEffectCoroutine()
        {
            // 빛 기둥 오브젝트 생성
            var pillarGo = new GameObject("LevelUpPillar");
            pillarGo.transform.position = transform.position;

            var sr = pillarGo.AddComponent<SpriteRenderer>();
            sr.sprite = GetWhitePixelSprite();
            sr.color = ColGoldLight;
            sr.sortingOrder = 150;
            pillarGo.transform.localScale = new Vector3(LevelUpPillarWidth, LevelUpPillarHeight, 1f);

            // 외부 글로우 (약간 큰 반투명)
            var glowGo = new GameObject("LevelUpGlow");
            glowGo.transform.SetParent(pillarGo.transform, false);
            glowGo.transform.localScale = new Vector3(2f, 1f, 1f);
            var glowSr = glowGo.AddComponent<SpriteRenderer>();
            glowSr.sprite = GetWhitePixelSprite();
            glowSr.color = new Color(1f, 0.9f, 0.3f, 0.3f);
            glowSr.sortingOrder = 149;

            // 파티클 (금색 입자 상승)
            for (int i = 0; i < 12; i++)
            {
                StartCoroutine(LevelUpParticleCoroutine(transform.position));
            }

            // 페이드인 → 유지 → 페이드아웃
            float elapsed = 0f;
            float fadeIn = LevelUpEffectDuration * 0.15f;
            float hold = LevelUpEffectDuration * 0.5f;
            float fadeOut = LevelUpEffectDuration * 0.35f;

            // 페이드인
            while (elapsed < fadeIn)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeIn;
                Color c = ColGoldBright;
                c.a = Mathf.Lerp(0f, 0.9f, t);
                sr.color = c;
                pillarGo.transform.position = transform.position;
                yield return null;
            }

            // 유지
            elapsed = 0f;
            while (elapsed < hold)
            {
                elapsed += Time.deltaTime;
                pillarGo.transform.position = transform.position;

                // 약간의 펄스
                float pulse = 1f + Mathf.Sin(elapsed * 10f) * 0.05f;
                pillarGo.transform.localScale = new Vector3(
                    LevelUpPillarWidth * pulse,
                    LevelUpPillarHeight,
                    1f);

                yield return null;
            }

            // 페이드아웃
            elapsed = 0f;
            while (elapsed < fadeOut)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeOut;
                Color c = sr.color;
                c.a = Mathf.Lerp(0.9f, 0f, t);
                sr.color = c;

                Color gc = glowSr.color;
                gc.a = Mathf.Lerp(0.3f, 0f, t);
                glowSr.color = gc;

                pillarGo.transform.position = transform.position;
                yield return null;
            }

            Destroy(pillarGo);
        }

        private IEnumerator LevelUpParticleCoroutine(Vector2 origin)
        {
            // 지연 시작
            yield return new WaitForSeconds(Random.Range(0f, 0.5f));

            var particleGo = new GameObject("LvUpParticle");
            float xOffset = Random.Range(-0.4f, 0.4f);
            particleGo.transform.position = new Vector3(origin.x + xOffset, origin.y - 0.5f, 0f);

            var sr = particleGo.AddComponent<SpriteRenderer>();
            sr.sprite = GetWhitePixelSprite();
            sr.color = ColGoldBright;
            sr.sortingOrder = 151;
            particleGo.transform.localScale = Vector3.one * Random.Range(0.03f, 0.06f);

            float speed = Random.Range(2f, 4f);
            float lifetime = Random.Range(0.5f, 1.0f);
            float elapsed = 0f;

            while (elapsed < lifetime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / lifetime;

                particleGo.transform.position += Vector3.up * speed * Time.deltaTime;

                Color c = sr.color;
                c.a = Mathf.Lerp(1f, 0f, t);
                sr.color = c;

                float s = Mathf.Lerp(particleGo.transform.localScale.x, 0f, t);
                particleGo.transform.localScale = Vector3.one * s;

                yield return null;
            }

            Destroy(particleGo);
        }

        // ── 레벨업 텍스트 (화면 중앙) ────────────────────────

        private IEnumerator LevelUpTextCoroutine()
        {
            // 오버레이 Canvas 생성
            var canvasGo = new GameObject("LevelUpText_Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            // CanvasGroup (페이드용)
            var canvasGroup = canvasGo.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            // 텍스트
            var textGo = new GameObject("LevelUpText", typeof(RectTransform));
            textGo.transform.SetParent(canvasGo.transform, false);

            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = new Vector2(0, 50);
            textRect.sizeDelta = new Vector2(600, 120);

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = $"LEVEL UP!\nLv.{_stats.Level}";
            tmp.fontSize = 48;
            tmp.color = ColLevelUpText;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            tmp.raycastTarget = false;

            // 부제 (스탯 증가 표시)
            var subTextGo = new GameObject("LevelUpSubText", typeof(RectTransform));
            subTextGo.transform.SetParent(canvasGo.transform, false);

            var subRect = subTextGo.GetComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0.5f, 0.5f);
            subRect.anchorMax = new Vector2(0.5f, 0.5f);
            subRect.pivot = new Vector2(0.5f, 0.5f);
            subRect.anchoredPosition = new Vector2(0, -30);
            subRect.sizeDelta = new Vector2(600, 60);

            var subTmp = subTextGo.AddComponent<TextMeshProUGUI>();
            subTmp.text = $"HP+{HpPerLevel}  ATK+{AttackPerLevel}  DEF+{DefensePerLevel}";
            subTmp.fontSize = 24;
            subTmp.color = new Color(0.9f, 0.9f, 0.9f, 0.9f);
            subTmp.alignment = TextAlignmentOptions.Center;
            subTmp.enableWordWrapping = false;
            subTmp.raycastTarget = false;

            // 애니메이션
            float elapsed = 0f;
            float fadeIn = 0.3f;
            float hold = 0.8f;
            float fadeOut = 0.4f;
            float totalDuration = fadeIn + hold + fadeOut;

            while (elapsed < totalDuration)
            {
                elapsed += Time.deltaTime;

                if (elapsed < fadeIn)
                {
                    // 페이드인 + 스케일 업
                    float t = elapsed / fadeIn;
                    canvasGroup.alpha = t;
                    float s = Mathf.Lerp(0.5f, 1f, t);
                    textRect.localScale = Vector3.one * s;
                }
                else if (elapsed < fadeIn + hold)
                {
                    // 유지
                    canvasGroup.alpha = 1f;
                    textRect.localScale = Vector3.one;
                }
                else
                {
                    // 페이드아웃 + 위로 이동
                    float t = (elapsed - fadeIn - hold) / fadeOut;
                    canvasGroup.alpha = 1f - t;
                    textRect.anchoredPosition = new Vector2(0, 50 + t * 30f);
                }

                yield return null;
            }

            Destroy(canvasGo);
        }

        // ── 사운드 ────────────────────────────────────────────

        private void TryPlayLevelUpSound()
        {
            var audioMgr = SoulCraft.Audio.AudioManager.Instance;
            if (audioMgr != null)
            {
                audioMgr.PlaySFX("sfx_level_up");
            }
        }

        // ── 세이브 연동 ──────────────────────────────────────

        private void TrySave()
        {
            if (SaveManager.Instance == null || _stats == null) return;

            SaveData data = SaveManager.Instance.Load();
            data.playerLevel = _stats.Level;
            data.playerExp = _stats.Exp;
            data.gold = _stats.Gold;
            data.stats = _stats.ToSaveData();
            SaveManager.Instance.Save(data);
        }

        // ── HUD EXP 바 생성 ──────────────────────────────────

        private void CreateExpBarUI()
        {
            // HUDManager가 있는 Canvas를 찾아서 HP바 아래에 EXP 바 추가
            var mainCanvas = FindMainCanvas();
            if (mainCanvas == null) return;

            // HUD 루트 찾기
            var hudRoot = mainCanvas.transform.Find("HUD_Root");
            if (hudRoot == null)
            {
                hudRoot = mainCanvas.transform;
            }

            // EXP 바 그룹 (HP 바 아래, 마나 바 아래)
            _expBarRoot = new GameObject("EXP_Group", typeof(RectTransform));
            _expBarRoot.transform.SetParent(hudRoot, false);

            var groupRect = _expBarRoot.GetComponent<RectTransform>();
            groupRect.anchorMin = new Vector2(0, 1);
            groupRect.anchorMax = new Vector2(0, 1);
            groupRect.pivot = new Vector2(0, 1);
            groupRect.anchoredPosition = new Vector2(30, -128);
            groupRect.sizeDelta = new Vector2(240, 18);

            // 배경 (테두리)
            var borderGo = new GameObject("EXP_Border", typeof(RectTransform));
            borderGo.transform.SetParent(_expBarRoot.transform, false);
            var borderRect = borderGo.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = Vector2.zero;
            var borderImg = borderGo.AddComponent<Image>();
            borderImg.color = new Color(1f, 1f, 1f, 0.6f);
            borderImg.raycastTarget = false;

            // 배경 (내부)
            var bgGo = new GameObject("EXP_BG", typeof(RectTransform));
            bgGo.transform.SetParent(_expBarRoot.transform, false);
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = new Vector2(1, 1);
            bgRect.offsetMax = new Vector2(-1, -1);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.05f, 0.15f, 0.9f);
            bgImg.raycastTarget = false;

            // EXP 바 채움
            var fillGo = new GameObject("EXP_Fill", typeof(RectTransform));
            fillGo.transform.SetParent(_expBarRoot.transform, false);
            _expFillRect = fillGo.GetComponent<RectTransform>();
            _expFillRect.anchorMin = Vector2.zero;
            _expFillRect.anchorMax = new Vector2(0f, 1f);
            _expFillRect.offsetMin = new Vector2(2, 2);
            _expFillRect.offsetMax = new Vector2(-2, -2);
            _expFillImage = fillGo.AddComponent<Image>();
            _expFillImage.color = new Color(0.4f, 0.3f, 0.9f, 1f);
            _expFillImage.raycastTarget = false;

            // 레벨 텍스트
            var levelTextGo = new GameObject("LevelText", typeof(RectTransform));
            levelTextGo.transform.SetParent(_expBarRoot.transform, false);
            var levelTextRect = levelTextGo.GetComponent<RectTransform>();
            levelTextRect.anchorMin = new Vector2(0, 0);
            levelTextRect.anchorMax = new Vector2(0, 1);
            levelTextRect.pivot = new Vector2(1, 0.5f);
            levelTextRect.anchoredPosition = new Vector2(-4, 0);
            levelTextRect.sizeDelta = new Vector2(60, 0);

            _levelText = levelTextGo.AddComponent<TextMeshProUGUI>();
            _levelText.text = $"Lv.{_stats.Level}";
            _levelText.fontSize = 12;
            _levelText.color = new Color(1f, 0.85f, 0.25f, 1f);
            _levelText.alignment = TextAlignmentOptions.Right;
            _levelText.enableWordWrapping = false;
            _levelText.raycastTarget = false;

            // EXP 숫자 텍스트
            var expTextGo = new GameObject("ExpText", typeof(RectTransform));
            expTextGo.transform.SetParent(_expBarRoot.transform, false);
            var expTextRect = expTextGo.GetComponent<RectTransform>();
            expTextRect.anchorMin = Vector2.zero;
            expTextRect.anchorMax = Vector2.one;
            expTextRect.offsetMin = Vector2.zero;
            expTextRect.offsetMax = Vector2.zero;

            _expText = expTextGo.AddComponent<TextMeshProUGUI>();
            _expText.text = "";
            _expText.fontSize = 10;
            _expText.color = new Color(0.9f, 0.9f, 0.95f, 0.9f);
            _expText.alignment = TextAlignmentOptions.Center;
            _expText.enableWordWrapping = false;
            _expText.raycastTarget = false;
        }

        // ── HUD EXP 바 갱신 ──────────────────────────────────

        private void UpdateExpBar()
        {
            if (_stats == null || _expFillRect == null) return;

            int expToNext = _stats.ExpToNextLevel;
            float ratio = expToNext > 0 ? (float)_stats.Exp / expToNext : 0f;
            ratio = Mathf.Clamp01(ratio);

            _expFillRect.anchorMax = new Vector2(ratio, 1f);

            if (_levelText != null)
                _levelText.text = $"Lv.{_stats.Level}";

            if (_expText != null)
                _expText.text = $"{_stats.Exp}/{expToNext}";
        }

        // ── 유틸리티 ──────────────────────────────────────────

        private Canvas FindMainCanvas()
        {
            // UIFactory에서 생성한 Canvas_Main 찾기
            var go = GameObject.Find("Canvas_Main");
            if (go != null)
                return go.GetComponent<Canvas>();

            // 못 찾으면 아무 ScreenSpaceOverlay Canvas 사용
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    return canvas;
            }

            return null;
        }

        private static Sprite _whitePixelSprite;

        private static Sprite GetWhitePixelSprite()
        {
            if (_whitePixelSprite != null) return _whitePixelSprite;

            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            var pixels = new Color[16];
            for (int i = 0; i < 16; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();

            _whitePixelSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            _whitePixelSprite.name = "WhitePixel";

            return _whitePixelSprite;
        }
    }
}
