using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using SoulCraft.Core;
using SoulCraft.UI;

namespace SoulCraft.Factory
{
    /// <summary>
    /// 모든 UI를 런타임 코드로 생성하는 팩토리.
    /// Canvas, Panel, Button, Slider, Text 등 전체 UI를 프리팹 없이 순수 코드로 구축한다.
    /// </summary>
    public static class UIFactory
    {
        // ================================================================
        //  Color Palette
        // ================================================================

        private static readonly Color ColBgDark        = new(0.05f, 0.05f, 0.08f, 0.85f);
        private static readonly Color ColBgPanel       = new(0.08f, 0.08f, 0.12f, 0.92f);
        private static readonly Color ColBorderWhite   = new(1f, 1f, 1f, 0.6f);
        private static readonly Color ColGold          = new(1f, 0.84f, 0.25f, 1f);
        private static readonly Color ColRed           = new(0.9f, 0.15f, 0.15f, 1f);
        private static readonly Color ColRedDark       = new(0.6f, 0.1f, 0.1f, 1f);
        private static readonly Color ColBlue          = new(0.2f, 0.45f, 0.9f, 1f);
        private static readonly Color ColBlueDark      = new(0.1f, 0.2f, 0.5f, 1f);
        private static readonly Color ColOrange        = new(1f, 0.6f, 0.15f, 0.9f);
        private static readonly Color ColHpGreen       = new(0.2f, 0.85f, 0.3f, 1f);
        private static readonly Color ColTextWhite     = new(0.95f, 0.95f, 0.95f, 1f);
        private static readonly Color ColTextShadow    = new(0f, 0f, 0f, 0.5f);
        private static readonly Color ColCooldownOverlay = new(0f, 0f, 0f, 0.6f);
        private static readonly Color ColBtnNormal     = new(0.15f, 0.15f, 0.2f, 0.9f);
        private static readonly Color ColBtnPressed    = new(0.25f, 0.25f, 0.35f, 0.95f);
        private static readonly Color ColTransparent   = new(0f, 0f, 0f, 0f);

        // ================================================================
        //  Cached References (BuildAll 이후 접근)
        // ================================================================

        // -- HUD --
        public static Slider HpSlider { get; private set; }
        public static Image HpFillImage { get; private set; }
        public static TMP_Text HpText { get; private set; }
        public static Slider ManaSlider { get; private set; }
        public static Image ManaFillImage { get; private set; }

        public static Button AttackButton { get; private set; }
        public static Button DashButton { get; private set; }
        public static Button[] SkillButtons { get; private set; } = new Button[4];
        public static Image[] SkillIcons { get; private set; } = new Image[4];
        public static Image[] SkillCooldownOverlays { get; private set; } = new Image[4];
        public static TMP_Text[] SkillCooldownTexts { get; private set; } = new TMP_Text[4];

        // Joystick
        public static RectTransform JoystickBackground { get; private set; }
        public static RectTransform JoystickKnob { get; private set; }
        public static VirtualJoystick JoystickComponent { get; private set; }

        // Combo
        public static GameObject ComboPanel { get; private set; }
        public static TMP_Text ComboNameText { get; private set; }
        public static TMP_Text ComboCountText { get; private set; }
        public static TMP_Text ComboMultiplierText { get; private set; }

        // Info
        public static TMP_Text GoldText { get; private set; }
        public static TMP_Text FloorText { get; private set; }

        // -- Boss HP Bar --
        public static GameObject BossHPPanel { get; private set; }
        public static Slider BossHpSlider { get; private set; }
        public static Slider BossDelayHpSlider { get; private set; }
        public static Image BossHpFillImage { get; private set; }
        public static Image BossDelayFillImage { get; private set; }
        public static TMP_Text BossNameText { get; private set; }
        public static TMP_Text BossPhaseText { get; private set; }

        // -- Pause Menu --
        public static GameObject PausePanel { get; private set; }
        public static Button PauseResumeBtn { get; private set; }
        public static Button PauseInventoryBtn { get; private set; }
        public static Button PauseSkillBtn { get; private set; }
        public static Button PausePassiveBtn { get; private set; }
        public static Button PauseHubBtn { get; private set; }

        // -- Result Screen --
        public static GameObject ResultPanel { get; private set; }
        public static TMP_Text ResultTitleText { get; private set; }
        public static TMP_Text ResultGoldText { get; private set; }
        public static TMP_Text ResultExpText { get; private set; }
        public static Transform ResultItemListContent { get; private set; }
        public static TMP_Text ResultTimeText { get; private set; }
        public static TMP_Text ResultKillText { get; private set; }
        public static TMP_Text ResultDamageDealtText { get; private set; }
        public static TMP_Text ResultDamageTakenText { get; private set; }
        public static TMP_Text ResultMaxComboText { get; private set; }
        public static Button ResultReturnBtn { get; private set; }
        public static CanvasGroup ResultCanvasGroup { get; private set; }

        // -- Main Menu --
        public static GameObject MainMenuPanel { get; private set; }
        public static Button MenuStartBtn { get; private set; }
        public static Button MenuContinueBtn { get; private set; }
        public static Button MenuSettingsBtn { get; private set; }

        // -- Screen Transition --
        public static CanvasGroup TransitionCanvasGroup { get; private set; }
        public static Image TransitionImage { get; private set; }

        // -- Canvas --
        public static Canvas MainCanvas { get; private set; }
        public static Canvas OverlayCanvas { get; private set; }

        // -- MobileInputUI reference --
        public static MobileInputUI MobileInputComponent { get; private set; }

        // ================================================================
        //  BuildAll - 전체 UI 생성 진입점
        // ================================================================

        /// <summary>
        /// 모든 UI를 생성하고 캐시된 참조를 설정한다.
        /// </summary>
        public static void BuildAll()
        {
            EnsureEventSystem();
            CreateMainCanvas();
            CreateOverlayCanvas();

            BuildHUD();
            BuildBossHPBar();
            BuildComboDisplay();
            BuildPauseMenu();
            BuildResultScreen();
            BuildMainMenu();
            BuildScreenTransition();
            BuildMobileInput();

            Debug.Log("[UIFactory] 전체 UI 생성 완료.");
        }

        // ================================================================
        //  EventSystem
        // ================================================================

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;

            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();
        }

        // ================================================================
        //  Canvas Creation
        // ================================================================

        private static void CreateMainCanvas()
        {
            var go = new GameObject("Canvas_Main");
            MainCanvas = go.AddComponent<Canvas>();
            MainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            MainCanvas.sortingOrder = 0;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
        }

        private static void CreateOverlayCanvas()
        {
            var go = new GameObject("Canvas_Overlay");
            OverlayCanvas = go.AddComponent<Canvas>();
            OverlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            OverlayCanvas.sortingOrder = 100;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
        }

        // ================================================================
        //  HUD (전투 중 상시 표시)
        // ================================================================

        private static void BuildHUD()
        {
            var hudRoot = CreatePanel("HUD_Root", MainCanvas.transform, Vector2.zero, Vector2.zero,
                ColTransparent);
            var hudRect = hudRoot.GetComponent<RectTransform>();
            hudRect.anchorMin = Vector2.zero;
            hudRect.anchorMax = Vector2.one;
            hudRect.offsetMin = Vector2.zero;
            hudRect.offsetMax = Vector2.zero;

            // ── 현재 층 표시 (좌상단, HP 위) ──
            FloorText = CreateText("FloorText", hudRoot.transform,
                new Vector2(30, -20), new Vector2(400, 40),
                "잊혀진 숲 - 1층", 22, ColGold, TextAlignmentOptions.Left);
            SetAnchors(FloorText.rectTransform, new Vector2(0, 1), new Vector2(0, 1));
            FloorText.rectTransform.pivot = new Vector2(0, 1);

            // ── HP 바 (좌상단) ──
            var hpGroup = CreatePanel("HP_Group", hudRoot.transform,
                new Vector2(30, -65), new Vector2(320, 32),
                ColTransparent);
            SetAnchors(hpGroup.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1));
            hpGroup.GetComponent<RectTransform>().pivot = new Vector2(0, 1);

            // HP 바 배경 (흰 테두리)
            CreateImage("HP_Border", hpGroup.transform, Vector2.zero, new Vector2(320, 32),
                ColBorderWhite);
            CreateImage("HP_BG", hpGroup.transform, Vector2.zero, new Vector2(316, 28),
                new Color(0.15f, 0.05f, 0.05f, 0.9f));

            Image hpFill;
            HpSlider = CreateSlider("HP_Slider", hpGroup.transform,
                Vector2.zero, new Vector2(312, 24), ColRed, out hpFill);
            HpFillImage = hpFill;
            HpSlider.value = 1f;

            HpText = CreateText("HP_Text", hpGroup.transform,
                Vector2.zero, new Vector2(312, 24),
                "100 / 100", 16, ColTextWhite, TextAlignmentOptions.Center);

            // ── 마나/스태미나 바 (HP 아래) ──
            var manaGroup = CreatePanel("Mana_Group", hudRoot.transform,
                new Vector2(30, -102), new Vector2(240, 22),
                ColTransparent);
            SetAnchors(manaGroup.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1));
            manaGroup.GetComponent<RectTransform>().pivot = new Vector2(0, 1);

            CreateImage("Mana_Border", manaGroup.transform, Vector2.zero, new Vector2(240, 22),
                ColBorderWhite);
            CreateImage("Mana_BG", manaGroup.transform, Vector2.zero, new Vector2(236, 18),
                new Color(0.05f, 0.05f, 0.15f, 0.9f));

            Image manaFill;
            ManaSlider = CreateSlider("Mana_Slider", manaGroup.transform,
                Vector2.zero, new Vector2(232, 16), ColBlue, out manaFill);
            ManaFillImage = manaFill;
            ManaSlider.value = 1f;

            // ── 골드 표시 (우상단) ──
            var goldGroup = CreatePanel("Gold_Group", hudRoot.transform,
                new Vector2(-30, -30), new Vector2(200, 40),
                new Color(0.1f, 0.1f, 0.1f, 0.7f));
            SetAnchors(goldGroup.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1));
            goldGroup.GetComponent<RectTransform>().pivot = new Vector2(1, 1);

            // 금화 아이콘 (원형 이미지)
            var goldIcon = CreateImage("GoldIcon", goldGroup.transform,
                new Vector2(-75, 0), new Vector2(28, 28), ColGold);
            MakeCircle(goldIcon);

            GoldText = CreateText("GoldText", goldGroup.transform,
                new Vector2(10, 0), new Vector2(140, 36),
                "0", 24, ColGold, TextAlignmentOptions.Right);

            // ── 스킬 슬롯 4개 (우하단) ──
            float skillStartX = -80;
            float skillY = 100;
            float skillSpacing = 80;

            for (int i = 3; i >= 0; i--)
            {
                float x = skillStartX - (3 - i) * skillSpacing;
                CreateSkillSlot(hudRoot.transform, i, new Vector2(x, skillY));
            }

            // ── 공격 버튼 (스킬 슬롯 왼쪽, 큰 원형 빨간색) ──
            float attackX = skillStartX - 4 * skillSpacing - 30;
            var attackBtnGo = CreateCircleButton("AttackButton", hudRoot.transform,
                new Vector2(attackX, skillY), 90, ColRed, ColRedDark);
            SetAnchors(attackBtnGo.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0));
            attackBtnGo.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            AttackButton = attackBtnGo.GetComponent<Button>();

            var atkLabel = CreateText("AtkLabel", attackBtnGo.transform,
                Vector2.zero, new Vector2(80, 40),
                "ATK", 20, ColTextWhite, TextAlignmentOptions.Center);

            // ── 대시 버튼 (공격 버튼 왼쪽, 작은 원형 파란색) ──
            float dashX = attackX - 100;
            var dashBtnGo = CreateCircleButton("DashButton", hudRoot.transform,
                new Vector2(dashX, skillY), 64, ColBlue, ColBlueDark);
            SetAnchors(dashBtnGo.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0));
            dashBtnGo.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            DashButton = dashBtnGo.GetComponent<Button>();

            var dashLabel = CreateText("DashLabel", dashBtnGo.transform,
                Vector2.zero, new Vector2(60, 30),
                "DASH", 14, ColTextWhite, TextAlignmentOptions.Center);

            // ── 가상 조이스틱 (좌하단) ──
            BuildJoystick(hudRoot.transform);
        }

        private static void CreateSkillSlot(Transform parent, int index, Vector2 position)
        {
            var slotGo = CreatePanel($"SkillSlot_{index}", parent,
                position, new Vector2(68, 68), ColBtnNormal);
            var slotRect = slotGo.GetComponent<RectTransform>();
            SetAnchors(slotRect, new Vector2(1, 0), new Vector2(1, 0));
            slotRect.pivot = new Vector2(0.5f, 0.5f);

            // 테두리
            CreateImage($"SkillBorder_{index}", slotGo.transform,
                Vector2.zero, new Vector2(68, 68), ColBorderWhite);

            // 아이콘
            var iconImg = CreateImage($"SkillIcon_{index}", slotGo.transform,
                Vector2.zero, new Vector2(56, 56), ColTextWhite);
            iconImg.enabled = false;
            SkillIcons[index] = iconImg;

            // 쿨다운 오버레이 (fillAmount)
            var cdOverlay = CreateImage($"SkillCD_{index}", slotGo.transform,
                Vector2.zero, new Vector2(64, 64), ColCooldownOverlay);
            cdOverlay.type = Image.Type.Filled;
            cdOverlay.fillMethod = Image.FillMethod.Radial360;
            cdOverlay.fillOrigin = (int)Image.Origin360.Top;
            cdOverlay.fillClockwise = false;
            cdOverlay.fillAmount = 0f;
            cdOverlay.enabled = false;
            SkillCooldownOverlays[index] = cdOverlay;

            // 쿨다운 텍스트
            var cdText = CreateText($"SkillCDText_{index}", slotGo.transform,
                Vector2.zero, new Vector2(64, 64),
                "", 14, ColTextWhite, TextAlignmentOptions.Center);
            cdText.enabled = false;
            SkillCooldownTexts[index] = cdText;

            // 버튼
            var btn = slotGo.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = ColBtnNormal;
            colors.pressedColor = ColBtnPressed;
            colors.highlightedColor = ColBtnNormal;
            btn.colors = colors;
            SkillButtons[index] = btn;
        }

        private static void BuildJoystick(Transform parent)
        {
            // 조이스틱 영역 (좌하단)
            var joystickArea = CreatePanel("JoystickArea", parent,
                new Vector2(180, 180), new Vector2(300, 300), ColTransparent);
            var areaRect = joystickArea.GetComponent<RectTransform>();
            SetAnchors(areaRect, new Vector2(0, 0), new Vector2(0, 0));
            areaRect.pivot = new Vector2(0.5f, 0.5f);

            // 배경 (큰 원)
            var bgGo = CreateImage("JoystickBG", joystickArea.transform,
                Vector2.zero, new Vector2(200, 200),
                new Color(1f, 1f, 1f, 0.15f));
            MakeCircle(bgGo);
            JoystickBackground = bgGo.rectTransform;

            // 핸들 (작은 원)
            var knobGo = CreateImage("JoystickKnob", bgGo.transform,
                Vector2.zero, new Vector2(80, 80),
                new Color(1f, 1f, 1f, 0.5f));
            MakeCircle(knobGo);
            JoystickKnob = knobGo.rectTransform;

            // VirtualJoystick 컴포넌트 부착
            JoystickComponent = joystickArea.AddComponent<VirtualJoystick>();

            // VirtualJoystick의 SerializeField를 reflection으로 설정
            SetPrivateField(JoystickComponent, "_background", JoystickBackground);
            SetPrivateField(JoystickComponent, "_knob", JoystickKnob);
            SetPrivateField(JoystickComponent, "_maxRadius", 80f);
            SetPrivateField(JoystickComponent, "_dynamicPosition", false);
        }

        // ================================================================
        //  Combo Display (화면 중앙 상단)
        // ================================================================

        private static void BuildComboDisplay()
        {
            ComboPanel = CreatePanel("ComboPanel", MainCanvas.transform,
                new Vector2(0, -100), new Vector2(400, 120), ColTransparent);
            var comboRect = ComboPanel.GetComponent<RectTransform>();
            SetAnchors(comboRect, new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            comboRect.pivot = new Vector2(0.5f, 1);

            ComboNameText = CreateText("ComboName", ComboPanel.transform,
                new Vector2(0, -10), new Vector2(380, 40),
                "", 28, ColGold, TextAlignmentOptions.Center);

            ComboCountText = CreateText("ComboCount", ComboPanel.transform,
                new Vector2(0, -50), new Vector2(380, 36),
                "", 22, ColTextWhite, TextAlignmentOptions.Center);

            ComboMultiplierText = CreateText("ComboMultiplier", ComboPanel.transform,
                new Vector2(0, -85), new Vector2(380, 30),
                "", 20, ColRed, TextAlignmentOptions.Center);

            ComboPanel.SetActive(false);
        }

        // ================================================================
        //  Boss HP Bar (화면 상단)
        // ================================================================

        private static void BuildBossHPBar()
        {
            BossHPPanel = CreatePanel("BossHP_Panel", MainCanvas.transform,
                new Vector2(0, -50), new Vector2(0, 80),
                ColTransparent);
            var bossRect = BossHPPanel.GetComponent<RectTransform>();
            // 상단 중앙, 너비 80%
            SetAnchors(bossRect, new Vector2(0.1f, 1), new Vector2(0.9f, 1));
            bossRect.pivot = new Vector2(0.5f, 1);
            bossRect.offsetMin = new Vector2(bossRect.offsetMin.x, bossRect.offsetMin.y);
            bossRect.offsetMax = new Vector2(bossRect.offsetMax.x, bossRect.offsetMax.y);
            bossRect.sizeDelta = new Vector2(0, 80);
            bossRect.anchoredPosition = new Vector2(0, -50);

            // 보스 이름 (중앙)
            BossNameText = CreateText("BossName", BossHPPanel.transform,
                new Vector2(0, 8), new Vector2(0, 30),
                "BOSS", 22, ColRed, TextAlignmentOptions.Center);
            var bnRect = BossNameText.rectTransform;
            SetAnchors(bnRect, new Vector2(0, 1), new Vector2(1, 1));
            bnRect.pivot = new Vector2(0.5f, 1);
            bnRect.sizeDelta = new Vector2(0, 30);
            bnRect.anchoredPosition = new Vector2(0, 0);

            // 슬라이더 영역
            var barArea = CreatePanel("BossBar_Area", BossHPPanel.transform,
                Vector2.zero, Vector2.zero, ColTransparent);
            var barRect = barArea.GetComponent<RectTransform>();
            SetAnchors(barRect, new Vector2(0, 0), new Vector2(1, 1));
            barRect.offsetMin = new Vector2(10, 8);
            barRect.offsetMax = new Vector2(-10, -34);

            // 바 배경
            CreateImage("BossBar_BG", barArea.transform,
                Vector2.zero, Vector2.zero,
                new Color(0.1f, 0.1f, 0.1f, 0.9f),
                true);

            // 테두리
            CreateImage("BossBar_Border", barArea.transform,
                Vector2.zero, Vector2.zero,
                ColBorderWhite, true);

            // 딜레이 HP 슬라이더 (주황, 뒤쪽)
            Image delayFill;
            BossDelayHpSlider = CreateSlider("BossDelayHP", barArea.transform,
                Vector2.zero, Vector2.zero, ColOrange, out delayFill, true);
            BossDelayFillImage = delayFill;
            BossDelayHpSlider.value = 1f;

            // 실제 HP 슬라이더 (빨강, 앞쪽)
            Image bossFill;
            BossHpSlider = CreateSlider("BossHP", barArea.transform,
                Vector2.zero, Vector2.zero, ColRed, out bossFill, true);
            BossHpFillImage = bossFill;
            BossHpSlider.value = 1f;

            // 페이즈 표시
            BossPhaseText = CreateText("BossPhase", BossHPPanel.transform,
                new Vector2(0, 0), new Vector2(120, 20),
                "Phase 1", 14, ColGold, TextAlignmentOptions.Right);
            var phRect = BossPhaseText.rectTransform;
            SetAnchors(phRect, new Vector2(1, 0), new Vector2(1, 0));
            phRect.pivot = new Vector2(1, 0);
            phRect.anchoredPosition = new Vector2(-10, 2);

            // CanvasGroup 추가
            BossHPPanel.AddComponent<CanvasGroup>();
            BossHPPanel.SetActive(false);
        }

        // ================================================================
        //  Pause Menu
        // ================================================================

        private static void BuildPauseMenu()
        {
            // 반투명 검은 오버레이
            PausePanel = CreatePanel("PauseMenu", OverlayCanvas.transform,
                Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.7f));
            var pauseRect = PausePanel.GetComponent<RectTransform>();
            SetAnchors(pauseRect, Vector2.zero, Vector2.one);
            pauseRect.offsetMin = Vector2.zero;
            pauseRect.offsetMax = Vector2.zero;

            // 중앙 패널
            var centerPanel = CreatePanel("PauseCenter", PausePanel.transform,
                Vector2.zero, new Vector2(500, 600), ColBgPanel);
            SetAnchors(centerPanel.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            // 테두리
            CreateImage("PauseBorder", centerPanel.transform,
                Vector2.zero, new Vector2(504, 604), ColBorderWhite);

            // 타이틀
            CreateText("PauseTitle", centerPanel.transform,
                new Vector2(0, 240), new Vector2(400, 60),
                "일시정지", 36, ColGold, TextAlignmentOptions.Center);

            // 버튼들
            float btnY = 130;
            float btnSpacing = -85;

            PauseResumeBtn = CreateMenuButton("계속하기", centerPanel.transform,
                new Vector2(0, btnY), new Vector2(360, 65));
            btnY += btnSpacing;

            PauseInventoryBtn = CreateMenuButton("인벤토리", centerPanel.transform,
                new Vector2(0, btnY), new Vector2(360, 65));
            btnY += btnSpacing;

            PauseSkillBtn = CreateMenuButton("스킬", centerPanel.transform,
                new Vector2(0, btnY), new Vector2(360, 65));
            btnY += btnSpacing;

            PausePassiveBtn = CreateMenuButton("패시브", centerPanel.transform,
                new Vector2(0, btnY), new Vector2(360, 65));
            btnY += btnSpacing;

            PauseHubBtn = CreateMenuButton("허브로 돌아가기", centerPanel.transform,
                new Vector2(0, btnY), new Vector2(360, 65));

            // 이벤트 연결
            PauseResumeBtn.onClick.AddListener(() =>
            {
                PausePanel.SetActive(false);
                if (GameManager.Instance != null) GameManager.Instance.ResumeGame();
            });
            PauseHubBtn.onClick.AddListener(() =>
            {
                PausePanel.SetActive(false);
                Time.timeScale = 1f;
                if (GameManager.Instance != null) GameManager.Instance.ReturnToHub();
            });

            PausePanel.SetActive(false);
        }

        // ================================================================
        //  Result Screen
        // ================================================================

        private static void BuildResultScreen()
        {
            ResultPanel = CreatePanel("ResultScreen", OverlayCanvas.transform,
                Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.8f));
            var rRect = ResultPanel.GetComponent<RectTransform>();
            SetAnchors(rRect, Vector2.zero, Vector2.one);
            rRect.offsetMin = Vector2.zero;
            rRect.offsetMax = Vector2.zero;
            ResultCanvasGroup = ResultPanel.AddComponent<CanvasGroup>();

            // 중앙 패널
            var centerPanel = CreatePanel("ResultCenter", ResultPanel.transform,
                Vector2.zero, new Vector2(600, 900), ColBgPanel);
            SetAnchors(centerPanel.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            CreateImage("ResultBorder", centerPanel.transform,
                Vector2.zero, new Vector2(604, 904), ColBorderWhite);

            // 타이틀
            ResultTitleText = CreateText("ResultTitle", centerPanel.transform,
                new Vector2(0, 390), new Vector2(500, 60),
                "STAGE CLEAR", 40, ColGold, TextAlignmentOptions.Center);

            // 보상 섹션
            CreateText("RewardLabel", centerPanel.transform,
                new Vector2(0, 310), new Vector2(500, 30),
                "-- 보상 --", 20, ColBorderWhite, TextAlignmentOptions.Center);

            ResultGoldText = CreateText("ResultGold", centerPanel.transform,
                new Vector2(-100, 270), new Vector2(200, 30),
                "0 G", 22, ColGold, TextAlignmentOptions.Left);

            ResultExpText = CreateText("ResultExp", centerPanel.transform,
                new Vector2(100, 270), new Vector2(200, 30),
                "0 EXP", 22, ColBlue, TextAlignmentOptions.Left);

            // 획득 아이템 스크롤 영역
            var scrollGo = new GameObject("ItemScroll");
            scrollGo.transform.SetParent(centerPanel.transform, false);
            var scrollRect = scrollGo.AddComponent<RectTransform>();
            scrollRect.anchoredPosition = new Vector2(0, 140);
            scrollRect.sizeDelta = new Vector2(520, 200);

            var scrollView = scrollGo.AddComponent<ScrollRect>();
            scrollView.horizontal = false;
            scrollView.vertical = true;

            var viewport = CreatePanel("Viewport", scrollGo.transform,
                Vector2.zero, Vector2.zero, ColTransparent);
            var vpRect = viewport.GetComponent<RectTransform>();
            SetAnchors(vpRect, Vector2.zero, Vector2.one);
            vpRect.offsetMin = Vector2.zero;
            vpRect.offsetMax = Vector2.zero;
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            var vpImage = viewport.GetComponent<Image>();
            if (vpImage == null) vpImage = viewport.AddComponent<Image>();
            vpImage.color = ColTransparent;

            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(viewport.transform, false);
            var contentRect = contentGo.AddComponent<RectTransform>();
            SetAnchors(contentRect, new Vector2(0, 1), new Vector2(1, 1));
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0, 0);
            var contentSize = contentGo.AddComponent<ContentSizeFitter>();
            contentSize.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 5;
            vlg.padding = new RectOffset(5, 5, 5, 5);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            ResultItemListContent = contentGo.transform;
            scrollView.viewport = vpRect;
            scrollView.content = contentRect;

            // 통계 섹션
            CreateText("StatsLabel", centerPanel.transform,
                new Vector2(0, 20), new Vector2(500, 30),
                "-- 통계 --", 20, ColBorderWhite, TextAlignmentOptions.Center);

            float statY = -15;
            float statSpacing = -32;

            ResultTimeText = CreateStatRow("클리어 시간", "00:00.00",
                centerPanel.transform, ref statY, statSpacing);
            ResultKillText = CreateStatRow("처치 수", "0",
                centerPanel.transform, ref statY, statSpacing);
            ResultDamageDealtText = CreateStatRow("가한 데미지", "0",
                centerPanel.transform, ref statY, statSpacing);
            ResultDamageTakenText = CreateStatRow("받은 데미지", "0",
                centerPanel.transform, ref statY, statSpacing);
            ResultMaxComboText = CreateStatRow("최대 콤보", "0",
                centerPanel.transform, ref statY, statSpacing);

            // 돌아가기 버튼
            ResultReturnBtn = CreateMenuButton("돌아가기", centerPanel.transform,
                new Vector2(0, -370), new Vector2(360, 65));

            ResultPanel.SetActive(false);
        }

        private static TMP_Text CreateStatRow(string label, string defaultValue,
            Transform parent, ref float y, float spacing)
        {
            CreateText($"Stat_{label}_Label", parent,
                new Vector2(-120, y), new Vector2(200, 28),
                label, 18, ColBorderWhite, TextAlignmentOptions.Left);

            var valueText = CreateText($"Stat_{label}_Value", parent,
                new Vector2(120, y), new Vector2(200, 28),
                defaultValue, 18, ColTextWhite, TextAlignmentOptions.Right);

            y += spacing;
            return valueText;
        }

        // ================================================================
        //  Main Menu
        // ================================================================

        private static void BuildMainMenu()
        {
            MainMenuPanel = CreatePanel("MainMenu", OverlayCanvas.transform,
                Vector2.zero, Vector2.zero, new Color(0.02f, 0.02f, 0.05f, 0.95f));
            var mmRect = MainMenuPanel.GetComponent<RectTransform>();
            SetAnchors(mmRect, Vector2.zero, Vector2.one);
            mmRect.offsetMin = Vector2.zero;
            mmRect.offsetMax = Vector2.zero;

            // 게임 타이틀
            CreateText("GameTitle", MainMenuPanel.transform,
                new Vector2(0, 250), new Vector2(800, 120),
                "SoulCraft", 72, ColGold, TextAlignmentOptions.Center);

            // 부제
            CreateText("Subtitle", MainMenuPanel.transform,
                new Vector2(0, 170), new Vector2(600, 50),
                "영혼을 담는 그릇의 여정", 24, ColBorderWhite, TextAlignmentOptions.Center);

            // 버튼
            MenuStartBtn = CreateMenuButton("게임 시작", MainMenuPanel.transform,
                new Vector2(0, -20), new Vector2(400, 70));

            MenuContinueBtn = CreateMenuButton("계속하기", MainMenuPanel.transform,
                new Vector2(0, -110), new Vector2(400, 70));

            MenuSettingsBtn = CreateMenuButton("설정", MainMenuPanel.transform,
                new Vector2(0, -200), new Vector2(400, 70));

            // 세이브가 없으면 계속하기 비활성화
            if (SaveManager.Instance != null && !SaveManager.Instance.HasSave())
            {
                MenuContinueBtn.interactable = false;
                var btnImg = MenuContinueBtn.GetComponent<Image>();
                if (btnImg != null) btnImg.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            }

            MainMenuPanel.SetActive(false);
        }

        // ================================================================
        //  Screen Transition
        // ================================================================

        private static void BuildScreenTransition()
        {
            var transGo = CreatePanel("ScreenTransition", OverlayCanvas.transform,
                Vector2.zero, Vector2.zero, Color.black);
            var tRect = transGo.GetComponent<RectTransform>();
            SetAnchors(tRect, Vector2.zero, Vector2.one);
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;

            TransitionImage = transGo.GetComponent<Image>();
            TransitionCanvasGroup = transGo.AddComponent<CanvasGroup>();
            TransitionCanvasGroup.alpha = 0f;
            TransitionCanvasGroup.blocksRaycasts = false;
            TransitionCanvasGroup.interactable = false;

            // ScreenTransition 컴포넌트 부착
            var st = transGo.AddComponent<ScreenTransition>();
            SetPrivateField(st, "_fadeCanvasGroup", TransitionCanvasGroup);
            SetPrivateField(st, "_fadeImage", TransitionImage);
        }

        // ================================================================
        //  Mobile Input (HUD 연동)
        // ================================================================

        private static void BuildMobileInput()
        {
            var mobileGo = new GameObject("MobileInputUI");
            mobileGo.transform.SetParent(MainCanvas.transform, false);
            MobileInputComponent = mobileGo.AddComponent<MobileInputUI>();

            // SerializeField 설정
            SetPrivateField(MobileInputComponent, "_joystick", JoystickComponent);
            SetPrivateField(MobileInputComponent, "_attackButton", AttackButton);
            SetPrivateField(MobileInputComponent, "_dashButton", DashButton);
            SetPrivateField(MobileInputComponent, "_skillButtons", SkillButtons);
            SetPrivateField(MobileInputComponent, "_skillCooldownOverlays", SkillCooldownOverlays);
            SetPrivateField(MobileInputComponent, "_mobileOnly", false);
        }

        // ================================================================
        //  HUD Manager Wiring
        // ================================================================

        /// <summary>
        /// HUDManager 컴포넌트를 생성하고 UI 참조를 연결한다.
        /// </summary>
        public static HUDManager WireHUDManager()
        {
            var hudGo = new GameObject("HUDManager");
            hudGo.transform.SetParent(MainCanvas.transform, false);
            var hud = hudGo.AddComponent<HUDManager>();

            SetPrivateField(hud, "_hpSlider", HpSlider);
            SetPrivateField(hud, "_manaSlider", ManaSlider);
            SetPrivateField(hud, "_hpText", HpText);
            SetPrivateField(hud, "_hpFillImage", HpFillImage);
            SetPrivateField(hud, "_comboPanel", ComboPanel);
            SetPrivateField(hud, "_comboNameText", ComboNameText);
            SetPrivateField(hud, "_comboCountText", ComboCountText);
            SetPrivateField(hud, "_comboMultiplierText", ComboMultiplierText);
            SetPrivateField(hud, "_goldText", GoldText);
            SetPrivateField(hud, "_floorText", FloorText);

            // SkillSlotUI 배열 생성 및 연결
            var skillSlots = new SkillSlotUI[4];
            for (int i = 0; i < 4; i++)
            {
                skillSlots[i] = new SkillSlotUI();
                SetPrivateField(skillSlots[i], "_iconImage", SkillIcons[i]);
                SetPrivateField(skillSlots[i], "_cooldownOverlay", SkillCooldownOverlays[i]);
                SetPrivateField(skillSlots[i], "_cooldownText", SkillCooldownTexts[i]);
            }
            SetPrivateField(hud, "_skillSlots", skillSlots);

            return hud;
        }

        /// <summary>
        /// BossHPBar 컴포넌트를 생성하고 UI 참조를 연결한다.
        /// </summary>
        public static BossHPBar WireBossHPBar()
        {
            var bossBar = BossHPPanel.AddComponent<BossHPBar>();

            SetPrivateField(bossBar, "_bossHPPanel", BossHPPanel);
            SetPrivateField(bossBar, "_hpSlider", BossHpSlider);
            SetPrivateField(bossBar, "_delayHpSlider", BossDelayHpSlider);
            SetPrivateField(bossBar, "_hpFillImage", BossHpFillImage);
            SetPrivateField(bossBar, "_delayFillImage", BossDelayFillImage);
            SetPrivateField(bossBar, "_bossNameText", BossNameText);
            SetPrivateField(bossBar, "_phaseText", BossPhaseText);

            return bossBar;
        }

        /// <summary>
        /// ResultScreenUI 컴포넌트를 생성하고 UI 참조를 연결한다.
        /// </summary>
        public static ResultScreenUI WireResultScreen()
        {
            var resultUI = ResultPanel.AddComponent<ResultScreenUI>();

            SetPrivateField(resultUI, "_titleText", ResultTitleText);
            SetPrivateField(resultUI, "_goldText", ResultGoldText);
            SetPrivateField(resultUI, "_expText", ResultExpText);
            SetPrivateField(resultUI, "_clearTimeText", ResultTimeText);
            SetPrivateField(resultUI, "_enemiesDefeatedText", ResultKillText);
            SetPrivateField(resultUI, "_damageDealtText", ResultDamageDealtText);
            SetPrivateField(resultUI, "_damageTakenText", ResultDamageTakenText);
            SetPrivateField(resultUI, "_maxComboText", ResultMaxComboText);
            SetPrivateField(resultUI, "_returnToHubButton", ResultReturnBtn);
            SetPrivateField(resultUI, "_canvasGroup", ResultCanvasGroup);
            SetPrivateField(resultUI, "_rewardItemsParent", ResultItemListContent);

            return resultUI;
        }

        // ================================================================
        //  Utility: UI Element Builders
        // ================================================================

        /// <summary>
        /// Panel(Image가 있는 RectTransform) 생성.
        /// </summary>
        private static GameObject CreatePanel(string name, Transform parent,
            Vector2 anchoredPos, Vector2 sizeDelta, Color bgColor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = sizeDelta;

            var img = go.AddComponent<Image>();
            img.color = bgColor;
            img.raycastTarget = false;

            return go;
        }

        /// <summary>
        /// Image 생성.
        /// </summary>
        private static Image CreateImage(string name, Transform parent,
            Vector2 anchoredPos, Vector2 sizeDelta, Color color, bool stretch = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();

            if (stretch)
            {
                SetAnchors(rect, Vector2.zero, Vector2.one);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            else
            {
                rect.anchoredPosition = anchoredPos;
                rect.sizeDelta = sizeDelta;
            }

            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;

            return img;
        }

        /// <summary>
        /// TMP_Text 생성.
        /// </summary>
        private static TMP_Text CreateText(string name, Transform parent,
            Vector2 anchoredPos, Vector2 sizeDelta,
            string text, float fontSize, Color color, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = sizeDelta;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;

            return tmp;
        }

        /// <summary>
        /// Slider 생성. fillImage를 out으로 반환.
        /// </summary>
        private static Slider CreateSlider(string name, Transform parent,
            Vector2 anchoredPos, Vector2 sizeDelta, Color fillColor,
            out Image fillImage, bool stretch = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            if (stretch)
            {
                SetAnchors(rect, Vector2.zero, Vector2.one);
                rect.offsetMin = new Vector2(2, 2);
                rect.offsetMax = new Vector2(-2, -2);
            }
            else
            {
                rect.anchoredPosition = anchoredPos;
                rect.sizeDelta = sizeDelta;
            }

            // Fill Area
            var fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaGo.transform.SetParent(go.transform, false);
            var fillAreaRect = fillAreaGo.GetComponent<RectTransform>();
            SetAnchors(fillAreaRect, Vector2.zero, Vector2.one);
            fillAreaRect.offsetMin = Vector2.zero;
            fillAreaRect.offsetMax = Vector2.zero;

            // Fill
            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(fillAreaGo.transform, false);
            var fillRect = fillGo.GetComponent<RectTransform>();
            SetAnchors(fillRect, Vector2.zero, Vector2.one);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            fillImage = fillGo.AddComponent<Image>();
            fillImage.color = fillColor;
            fillImage.raycastTarget = false;

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.targetGraphic = fillImage;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.interactable = false; // HP바 등은 상호작용 불가
            slider.transition = Selectable.Transition.None;

            return slider;
        }

        /// <summary>
        /// 원형 버튼 생성.
        /// </summary>
        private static GameObject CreateCircleButton(string name, Transform parent,
            Vector2 anchoredPos, float diameter, Color normalColor, Color pressedColor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(diameter, diameter);

            var img = go.AddComponent<Image>();
            img.color = normalColor;
            img.raycastTarget = true;

            // 원형으로 만들기 위해 기본 스프라이트 사용
            MakeCircle(img);

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = normalColor;
            colors.pressedColor = pressedColor;
            colors.highlightedColor = normalColor;
            colors.selectedColor = normalColor;
            btn.colors = colors;

            return go;
        }

        /// <summary>
        /// 메뉴 스타일 버튼 생성.
        /// </summary>
        private static Button CreateMenuButton(string label, Transform parent,
            Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject($"Btn_{label}", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = sizeDelta;

            // 배경
            var img = go.AddComponent<Image>();
            img.color = ColBtnNormal;
            img.raycastTarget = true;

            // 테두리
            CreateImage($"Border_{label}", go.transform,
                Vector2.zero, sizeDelta + new Vector2(4, 4), ColBorderWhite);

            // 텍스트
            CreateText($"Text_{label}", go.transform,
                Vector2.zero, sizeDelta,
                label, 24, ColTextWhite, TextAlignmentOptions.Center);

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = ColBtnNormal;
            colors.pressedColor = ColBtnPressed;
            colors.highlightedColor = new Color(0.2f, 0.2f, 0.28f, 0.95f);
            colors.selectedColor = ColBtnNormal;
            btn.colors = colors;
            btn.targetGraphic = img;

            return btn;
        }

        // ================================================================
        //  Utility Helpers
        // ================================================================

        private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
        }

        /// <summary>
        /// Image를 원형으로 만든다. (Knob 스프라이트를 사용)
        /// </summary>
        private static Sprite _circleSprite;
        private static void MakeCircle(Image img)
        {
            if (_circleSprite == null)
            {
                int size = 64;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Bilinear;
                float center = size / 2f;
                float radius = center - 1;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                        tex.SetPixel(x, y, dist <= radius ? Color.white : Color.clear);
                    }
                tex.Apply();
                _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            }
            img.sprite = _circleSprite;
        }

        /// <summary>
        /// 리플렉션을 통해 private/SerializeField에 값을 설정한다.
        /// </summary>
        private static void SetPrivateField(object target, string fieldName, object value)
        {
            if (target == null) return;

            var type = target.GetType();
            var field = type.GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public);

            if (field != null)
            {
                field.SetValue(target, value);
            }
            else
            {
                // 부모 클래스에서도 검색
                var baseType = type.BaseType;
                while (baseType != null)
                {
                    field = baseType.GetField(fieldName,
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.Public);
                    if (field != null)
                    {
                        field.SetValue(target, value);
                        return;
                    }
                    baseType = baseType.BaseType;
                }

                Debug.LogWarning($"[UIFactory] Field '{fieldName}' not found on {type.Name}");
            }
        }
    }
}
