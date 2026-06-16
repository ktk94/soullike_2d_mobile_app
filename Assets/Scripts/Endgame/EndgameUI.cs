using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SoulCraft.Core;
using SoulCraft.Enemy;
using SoulCraft.World;
using SoulCraft.Player;
using SoulCraft.Combat;

namespace SoulCraft.Endgame
{
    // ── Statistics Data ───────────────────────────────────────

    [Serializable]
    public class PlayerStatistics
    {
        public int highestDamage;
        public int maxCombo;
        public float fastestStageClear = float.MaxValue;
        public int totalPlayTime;
        public int totalKills;
        public int totalDeaths;
        public int totalGoldEarned;
        public int totalItemsCollected;
        public int bossKills;
    }

    /// <summary>
    /// 엔드게임 메뉴 UI.
    /// 무한 던전, 일일 도전, 2회차, 업적 목록, 기록실을 코드로 생성하고 관리한다.
    /// </summary>
    public class EndgameUI : MonoBehaviour
    {
        public static EndgameUI Instance { get; private set; }

        // ── Inspector: Root ───────────────────────────────────

        [Header("Root")]
        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private GameObject endgamePanel;

        // ── Inspector: Tab Buttons ────────────────────────────

        [Header("Tab Buttons")]
        [SerializeField] private Button tabInfiniteDungeon;
        [SerializeField] private Button tabDailyChallenge;
        [SerializeField] private Button tabNewGamePlus;
        [SerializeField] private Button tabAchievements;
        [SerializeField] private Button tabStatistics;
        [SerializeField] private Button closeButton;

        // ── Inspector: Tab Panels ─────────────────────────────

        [Header("Tab Panels")]
        [SerializeField] private GameObject panelInfiniteDungeon;
        [SerializeField] private GameObject panelDailyChallenge;
        [SerializeField] private GameObject panelNewGamePlus;
        [SerializeField] private GameObject panelAchievements;
        [SerializeField] private GameObject panelStatistics;

        // ── Inspector: Infinite Dungeon Panel ─────────────────

        [Header("Infinite Dungeon")]
        [SerializeField] private TMP_Text dungeonHighestFloorText;
        [SerializeField] private TMP_Text dungeonTotalRunsText;
        [SerializeField] private TMP_Text dungeonEntryRequirementText;
        [SerializeField] private Button dungeonEnterButton;
        [SerializeField] private TMP_Text dungeonEnterButtonText;

        // ── Inspector: Daily Challenge Panel ──────────────────

        [Header("Daily Challenge")]
        [SerializeField] private TMP_Text dailyChallengeNameText;
        [SerializeField] private TMP_Text dailyDateText;
        [SerializeField] private Transform dailyModifierContainer;
        [SerializeField] private GameObject dailyModifierPrefab;
        [SerializeField] private Button dailyChallengeButton;
        [SerializeField] private TMP_Text dailyChallengeButtonText;
        [SerializeField] private TMP_Text dailyCompletionStatusText;
        [SerializeField] private TMP_Text dailyConsecutiveText;
        [SerializeField] private TMP_Text dailyBestGradeText;

        // ── Inspector: New Game Plus Panel ────────────────────

        [Header("New Game Plus")]
        [SerializeField] private TMP_Text ngPlusCycleText;
        [SerializeField] private TMP_Text ngPlusDifficultyText;
        [SerializeField] private Transform ngPlusChangelogContainer;
        [SerializeField] private GameObject ngPlusChangelogItemPrefab;
        [SerializeField] private Button ngPlusStartButton;
        [SerializeField] private TMP_Text ngPlusStartButtonText;
        [SerializeField] private Transform ngPlusUnlockContainer;
        [SerializeField] private GameObject ngPlusUnlockItemPrefab;

        // ── Inspector: Achievements Panel ─────────────────────

        [Header("Achievements")]
        [SerializeField] private TMP_Text achievementProgressText;
        [SerializeField] private Slider achievementProgressBar;
        [SerializeField] private Transform achievementListContainer;
        [SerializeField] private GameObject achievementItemPrefab;
        [SerializeField] private TMP_Dropdown achievementCategoryFilter;

        // ── Inspector: Statistics Panel ───────────────────────

        [Header("Statistics")]
        [SerializeField] private TMP_Text statHighestDamageText;
        [SerializeField] private TMP_Text statMaxComboText;
        [SerializeField] private TMP_Text statFastestClearText;
        [SerializeField] private TMP_Text statTotalPlayTimeText;
        [SerializeField] private TMP_Text statTotalKillsText;
        [SerializeField] private TMP_Text statTotalDeathsText;
        [SerializeField] private TMP_Text statTotalGoldText;
        [SerializeField] private TMP_Text statBossKillsText;
        [SerializeField] private TMP_Text statItemsCollectedText;

        // ── Inspector: Colors ─────────────────────────────────

        [Header("UI Colors")]
        [SerializeField] private Color activeTabColor = new Color(0.3f, 0.7f, 1f);
        [SerializeField] private Color inactiveTabColor = new Color(0.5f, 0.5f, 0.5f);
        [SerializeField] private Color unlockedColor = new Color(1f, 0.85f, 0.3f);
        [SerializeField] private Color lockedColor = new Color(0.4f, 0.4f, 0.4f);
        [SerializeField] private Color gradeColorS = new Color(1f, 0.84f, 0f);
        [SerializeField] private Color gradeColorA = new Color(0.8f, 0.4f, 1f);
        [SerializeField] private Color gradeColorB = new Color(0.3f, 0.6f, 1f);
        [SerializeField] private Color gradeColorC = new Color(0.6f, 0.6f, 0.6f);

        // ── Runtime ───────────────────────────────────────────

        private PlayerStatistics statistics = new();
        private AchievementCategory currentFilterCategory = AchievementCategory.Combat;
        private List<GameObject> spawnedUIElements = new();

        // ── Lifecycle ─────────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void Start()
        {
            LoadStatistics();
            SetupButtons();
            SetupCategoryFilter();

            // 초기 상태: 패널 숨김
            if (endgamePanel != null)
                endgamePanel.SetActive(false);
        }

        void OnEnable()
        {
            GameEventSystem.Subscribe<DamageEvent>(OnDamageForStats);
            GameEventSystem.Subscribe<ComboEvent>(OnComboForStats);
            GameEventSystem.Subscribe<EnemyDeathEvent>(OnKillForStats);
            GameEventSystem.Subscribe<StageCompleteEvent>(OnStageClearForStats);
            GameEventSystem.Subscribe<ItemDropEvent>(OnItemForStats);
            GameEventSystem.Subscribe<EnemyRewardEvent>(OnGoldForStats);
        }

        void OnDisable()
        {
            GameEventSystem.Unsubscribe<DamageEvent>(OnDamageForStats);
            GameEventSystem.Unsubscribe<ComboEvent>(OnComboForStats);
            GameEventSystem.Unsubscribe<EnemyDeathEvent>(OnKillForStats);
            GameEventSystem.Unsubscribe<StageCompleteEvent>(OnStageClearForStats);
            GameEventSystem.Unsubscribe<ItemDropEvent>(OnItemForStats);
            GameEventSystem.Unsubscribe<EnemyRewardEvent>(OnGoldForStats);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Button Setup ──────────────────────────────────────

        private void SetupButtons()
        {
            if (tabInfiniteDungeon != null)
                tabInfiniteDungeon.onClick.AddListener(() => ShowTab(0));
            if (tabDailyChallenge != null)
                tabDailyChallenge.onClick.AddListener(() => ShowTab(1));
            if (tabNewGamePlus != null)
                tabNewGamePlus.onClick.AddListener(() => ShowTab(2));
            if (tabAchievements != null)
                tabAchievements.onClick.AddListener(() => ShowTab(3));
            if (tabStatistics != null)
                tabStatistics.onClick.AddListener(() => ShowTab(4));

            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);

            // 기능 버튼
            if (dungeonEnterButton != null)
                dungeonEnterButton.onClick.AddListener(OnDungeonEnterClicked);
            if (dailyChallengeButton != null)
                dailyChallengeButton.onClick.AddListener(OnDailyChallengeClicked);
            if (ngPlusStartButton != null)
                ngPlusStartButton.onClick.AddListener(OnNgPlusStartClicked);
        }

        private void SetupCategoryFilter()
        {
            if (achievementCategoryFilter == null) return;

            achievementCategoryFilter.ClearOptions();
            var options = new List<string>
            {
                "전투", "탐험", "수집", "강화", "성장", "일일", "숨겨진"
            };
            achievementCategoryFilter.AddOptions(options);
            achievementCategoryFilter.onValueChanged.AddListener(OnCategoryFilterChanged);
        }

        // ── Public API ────────────────────────────────────────

        /// <summary>
        /// 엔드게임 메뉴를 표시한다.
        /// </summary>
        public void Show()
        {
            if (endgamePanel != null)
                endgamePanel.SetActive(true);

            ShowTab(0); // 무한 던전 탭부터 시작
            RefreshAllPanels();
        }

        /// <summary>
        /// 엔드게임 메뉴를 숨긴다.
        /// </summary>
        public void Hide()
        {
            if (endgamePanel != null)
                endgamePanel.SetActive(false);
        }

        /// <summary>
        /// 특정 탭으로 전환한다.
        /// </summary>
        public void ShowTab(int tabIndex)
        {
            // 모든 패널 숨기기
            SetPanelActive(panelInfiniteDungeon, false);
            SetPanelActive(panelDailyChallenge, false);
            SetPanelActive(panelNewGamePlus, false);
            SetPanelActive(panelAchievements, false);
            SetPanelActive(panelStatistics, false);

            // 탭 버튼 색상 초기화
            SetTabColor(tabInfiniteDungeon, inactiveTabColor);
            SetTabColor(tabDailyChallenge, inactiveTabColor);
            SetTabColor(tabNewGamePlus, inactiveTabColor);
            SetTabColor(tabAchievements, inactiveTabColor);
            SetTabColor(tabStatistics, inactiveTabColor);

            // 선택된 탭 활성화
            switch (tabIndex)
            {
                case 0:
                    SetPanelActive(panelInfiniteDungeon, true);
                    SetTabColor(tabInfiniteDungeon, activeTabColor);
                    RefreshDungeonPanel();
                    break;
                case 1:
                    SetPanelActive(panelDailyChallenge, true);
                    SetTabColor(tabDailyChallenge, activeTabColor);
                    RefreshDailyChallengePanel();
                    break;
                case 2:
                    SetPanelActive(panelNewGamePlus, true);
                    SetTabColor(tabNewGamePlus, activeTabColor);
                    RefreshNewGamePlusPanel();
                    break;
                case 3:
                    SetPanelActive(panelAchievements, true);
                    SetTabColor(tabAchievements, activeTabColor);
                    RefreshAchievementsPanel();
                    break;
                case 4:
                    SetPanelActive(panelStatistics, true);
                    SetTabColor(tabStatistics, activeTabColor);
                    RefreshStatisticsPanel();
                    break;
            }
        }

        // ── Refresh Panels ────────────────────────────────────

        private void RefreshAllPanels()
        {
            RefreshDungeonPanel();
            RefreshDailyChallengePanel();
            RefreshNewGamePlusPanel();
            RefreshAchievementsPanel();
            RefreshStatisticsPanel();
        }

        // ── Infinite Dungeon Panel ────────────────────────────

        private void RefreshDungeonPanel()
        {
            var dungeon = InfiniteDungeon.Instance;

            if (dungeonHighestFloorText != null)
            {
                int highest = dungeon != null ? dungeon.HighestFloor : 0;
                dungeonHighestFloorText.text = $"최고 기록: {highest}층";
            }

            if (dungeonTotalRunsText != null)
            {
                // 총 도전 횟수는 SaveData에서 가져옴
                dungeonTotalRunsText.text = dungeon != null
                    ? $"총 도전: {dungeon.ToSaveData().totalRuns}회"
                    : "총 도전: 0회";
            }

            bool canEnter = dungeon != null && dungeon.CanEnter();

            if (dungeonEntryRequirementText != null)
            {
                dungeonEntryRequirementText.text = canEnter
                    ? "<color=#00FF00>입장 가능</color>"
                    : "<color=#FF4444>메인 스토리 클리어 필요</color>";
            }

            if (dungeonEnterButton != null)
                dungeonEnterButton.interactable = canEnter;

            if (dungeonEnterButtonText != null)
                dungeonEnterButtonText.text = canEnter ? "입장" : "잠김";
        }

        // ── Daily Challenge Panel ─────────────────────────────

        private void RefreshDailyChallengePanel()
        {
            var daily = DailyChallenge.Instance;

            if (dailyDateText != null)
                dailyDateText.text = DateTime.Today.ToString("yyyy년 MM월 dd일");

            if (daily == null)
            {
                if (dailyChallengeNameText != null)
                    dailyChallengeNameText.text = "도전 정보 없음";
                return;
            }

            var config = daily.TodayConfig;

            if (dailyChallengeNameText != null)
                dailyChallengeNameText.text = config.ChallengeName;

            // 조건 목록 표시
            RefreshDailyModifiers(config);

            // 도전 상태
            bool completed = daily.HasCompletedToday;

            if (dailyCompletionStatusText != null)
            {
                dailyCompletionStatusText.text = completed
                    ? "<color=#00FF00>오늘의 도전 완료!</color>"
                    : "<color=#FFAA00>도전 가능</color>";
            }

            if (dailyChallengeButton != null)
                dailyChallengeButton.interactable = !completed;

            if (dailyChallengeButtonText != null)
                dailyChallengeButtonText.text = completed ? "완료됨" : "도전 시작";

            if (dailyConsecutiveText != null)
                dailyConsecutiveText.text = $"연속 {daily.ConsecutiveDays}일";

            if (dailyBestGradeText != null)
            {
                dailyBestGradeText.text = $"최고 등급: {daily.BestGrade}";
                dailyBestGradeText.color = GetGradeColor(daily.BestGrade);
            }
        }

        private void RefreshDailyModifiers(DailyChallengeConfig config)
        {
            if (dailyModifierContainer == null || dailyModifierPrefab == null) return;

            // 기존 제거
            ClearContainer(dailyModifierContainer);

            if (config.Modifiers == null) return;

            foreach (var mod in config.Modifiers)
            {
                GameObject item = Instantiate(dailyModifierPrefab, dailyModifierContainer);
                spawnedUIElements.Add(item);

                var nameText = item.transform.Find("Name")?.GetComponent<TMP_Text>();
                var descText = item.transform.Find("Description")?.GetComponent<TMP_Text>();

                if (nameText != null) nameText.text = mod.displayName;
                if (descText != null) descText.text = mod.description;
            }
        }

        // ── New Game Plus Panel ───────────────────────────────

        private void RefreshNewGamePlusPanel()
        {
            var ngPlus = NewGamePlus.Instance;

            if (ngPlus == null)
            {
                if (ngPlusCycleText != null) ngPlusCycleText.text = "회차 정보 없음";
                return;
            }

            if (ngPlusCycleText != null)
            {
                string cycleDisplay = ngPlus.GetCycleDisplayText();
                ngPlusCycleText.text = string.IsNullOrEmpty(cycleDisplay)
                    ? "현재: 1회차 (일반)"
                    : $"현재: {cycleDisplay}";
            }

            if (ngPlusDifficultyText != null)
                ngPlusDifficultyText.text = ngPlus.GetCycleDifficultyText();

            // 변경 사항 목록
            RefreshNgPlusChangelog(ngPlus);

            // NG+ 전용 해금 목록
            RefreshNgPlusUnlocks(ngPlus);

            // 시작 버튼
            bool canStart = ngPlus.CanStartNewGamePlus;

            if (ngPlusStartButton != null)
                ngPlusStartButton.interactable = canStart;

            if (ngPlusStartButtonText != null)
            {
                if (ngPlus.CurrentCycle >= 3)
                    ngPlusStartButtonText.text = "최대 회차 도달";
                else if (canStart)
                    ngPlusStartButtonText.text = $"{ngPlus.CurrentCycle + 1}회차 시작";
                else
                    ngPlusStartButtonText.text = "메인 스토리 클리어 필요";
            }
        }

        private void RefreshNgPlusChangelog(NewGamePlus ngPlus)
        {
            if (ngPlusChangelogContainer == null || ngPlusChangelogItemPrefab == null) return;

            ClearContainer(ngPlusChangelogContainer);

            int nextCycle = Mathf.Min(ngPlus.CurrentCycle + 1, 3);
            var changes = ngPlus.GetCycleChangelog(nextCycle);

            foreach (var change in changes)
            {
                GameObject item = Instantiate(ngPlusChangelogItemPrefab, ngPlusChangelogContainer);
                spawnedUIElements.Add(item);

                var text = item.GetComponentInChildren<TMP_Text>();
                if (text != null) text.text = $"- {change}";
            }
        }

        private void RefreshNgPlusUnlocks(NewGamePlus ngPlus)
        {
            if (ngPlusUnlockContainer == null || ngPlusUnlockItemPrefab == null) return;

            ClearContainer(ngPlusUnlockContainer);

            var unlocks = ngPlus.GetUnlockedContent();
            foreach (var unlock in unlocks)
            {
                GameObject item = Instantiate(ngPlusUnlockItemPrefab, ngPlusUnlockContainer);
                spawnedUIElements.Add(item);

                var nameText = item.transform.Find("Name")?.GetComponent<TMP_Text>();
                var descText = item.transform.Find("Description")?.GetComponent<TMP_Text>();
                var typeText = item.transform.Find("Type")?.GetComponent<TMP_Text>();

                if (nameText != null) nameText.text = unlock.displayName;
                if (descText != null) descText.text = unlock.description;
                if (typeText != null)
                    typeText.text = unlock.unlockType == "equipment" ? "장비" : "스킬";
            }
        }

        // ── Achievements Panel ────────────────────────────────

        private void RefreshAchievementsPanel()
        {
            var system = AchievementSystem.Instance;
            if (system == null) return;

            // 전체 진행률
            float completion = system.GetCompletionRate();
            int unlocked = system.GetUnlockedCount();
            int total = system.AllAchievements.Count;

            if (achievementProgressText != null)
                achievementProgressText.text = $"달성률: {unlocked}/{total} ({completion * 100f:F0}%)";

            if (achievementProgressBar != null)
                achievementProgressBar.value = completion;

            // 카테고리별 필터링
            RefreshAchievementList();
        }

        private void RefreshAchievementList()
        {
            if (achievementListContainer == null || achievementItemPrefab == null) return;

            ClearContainer(achievementListContainer);

            var system = AchievementSystem.Instance;
            if (system == null) return;

            var filtered = system.GetByCategory(currentFilterCategory);

            foreach (var achievement in filtered)
            {
                GameObject item = Instantiate(achievementItemPrefab, achievementListContainer);
                spawnedUIElements.Add(item);

                var nameText = item.transform.Find("Name")?.GetComponent<TMP_Text>();
                var descText = item.transform.Find("Description")?.GetComponent<TMP_Text>();
                var progressBar = item.transform.Find("ProgressBar")?.GetComponent<Slider>();
                var progressText = item.transform.Find("ProgressText")?.GetComponent<TMP_Text>();
                var statusIcon = item.transform.Find("StatusIcon")?.GetComponent<Image>();
                var claimButton = item.transform.Find("ClaimButton")?.GetComponent<Button>();

                // 숨겨진 업적: 미달성이면 내용 숨김
                bool showContent = !achievement.isHidden || achievement.isUnlocked;

                if (nameText != null)
                    nameText.text = showContent ? achievement.name : "???";

                if (descText != null)
                    descText.text = showContent ? achievement.description : "숨겨진 업적";

                if (progressBar != null)
                {
                    progressBar.value = achievement.condition.Progress;
                    progressBar.gameObject.SetActive(
                        achievement.condition.conditionType == ConditionType.Cumulative
                        && !achievement.isUnlocked);
                }

                if (progressText != null)
                {
                    if (achievement.isUnlocked)
                    {
                        progressText.text = "달성!";
                        progressText.color = unlockedColor;
                    }
                    else if (achievement.condition.conditionType == ConditionType.Cumulative)
                    {
                        progressText.text = $"{achievement.condition.currentValue:F0}" +
                                            $"/{achievement.condition.targetValue:F0}";
                        progressText.color = lockedColor;
                    }
                    else
                    {
                        progressText.text = "미달성";
                        progressText.color = lockedColor;
                    }
                }

                if (statusIcon != null)
                {
                    statusIcon.color = achievement.isUnlocked ? unlockedColor : lockedColor;
                }

                // 보상 수령 버튼
                if (claimButton != null)
                {
                    bool canClaim = achievement.isUnlocked && !achievement.isRewardClaimed;
                    claimButton.gameObject.SetActive(canClaim);

                    if (canClaim)
                    {
                        string achievementId = achievement.id;
                        claimButton.onClick.AddListener(() =>
                        {
                            AchievementSystem.Instance?.ClaimReward(achievementId);
                            RefreshAchievementsPanel();
                        });
                    }
                }
            }
        }

        private void OnCategoryFilterChanged(int index)
        {
            currentFilterCategory = (AchievementCategory)index;
            RefreshAchievementList();
        }

        // ── Statistics Panel ──────────────────────────────────

        private void RefreshStatisticsPanel()
        {
            if (statHighestDamageText != null)
                statHighestDamageText.text = $"최고 데미지: {statistics.highestDamage:N0}";

            if (statMaxComboText != null)
                statMaxComboText.text = $"최다 콤보: {statistics.maxCombo}";

            if (statFastestClearText != null)
            {
                string time = statistics.fastestStageClear < float.MaxValue
                    ? FormatTime(statistics.fastestStageClear)
                    : "--:--";
                statFastestClearText.text = $"최단 클리어: {time}";
            }

            if (statTotalPlayTimeText != null)
                statTotalPlayTimeText.text = $"총 플레이 시간: {FormatTime(statistics.totalPlayTime)}";

            if (statTotalKillsText != null)
                statTotalKillsText.text = $"총 처치 수: {statistics.totalKills:N0}";

            if (statTotalDeathsText != null)
                statTotalDeathsText.text = $"총 사망 수: {statistics.totalDeaths:N0}";

            if (statTotalGoldText != null)
                statTotalGoldText.text = $"총 획득 골드: {statistics.totalGoldEarned:N0}";

            if (statBossKillsText != null)
                statBossKillsText.text = $"보스 처치: {statistics.bossKills:N0}";

            if (statItemsCollectedText != null)
                statItemsCollectedText.text = $"아이템 수집: {statistics.totalItemsCollected:N0}";
        }

        // ── Button Actions ────────────────────────────────────

        private void OnDungeonEnterClicked()
        {
            var dungeon = InfiniteDungeon.Instance;
            if (dungeon == null)
            {
                Debug.LogWarning("[EndgameUI] InfiniteDungeon 인스턴스가 없습니다.");
                return;
            }

            if (dungeon.StartRun())
            {
                Hide();
            }
        }

        private void OnDailyChallengeClicked()
        {
            var daily = DailyChallenge.Instance;
            if (daily == null)
            {
                Debug.LogWarning("[EndgameUI] DailyChallenge 인스턴스가 없습니다.");
                return;
            }

            if (daily.StartChallenge())
            {
                Hide();
            }
        }

        private void OnNgPlusStartClicked()
        {
            var ngPlus = NewGamePlus.Instance;
            if (ngPlus == null)
            {
                Debug.LogWarning("[EndgameUI] NewGamePlus 인스턴스가 없습니다.");
                return;
            }

            // 확인 팝업 없이 바로 시작 (확인 UI는 별도 구현 가능)
            if (ngPlus.StartNewGamePlus())
            {
                RefreshNewGamePlusPanel();
            }
        }

        // ── Statistics Event Handlers ─────────────────────────

        private void OnDamageForStats(DamageEvent evt)
        {
            // 플레이어가 가한 데미지 추적
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null && evt.Attacker == player)
            {
                if (evt.Damage > statistics.highestDamage)
                {
                    statistics.highestDamage = evt.Damage;
                    SaveStatistics();
                }
            }

            // 플레이어 사망 추적
            if (player != null && evt.Target == player)
            {
                var stats = player.GetComponent<PlayerStats>();
                if (stats != null && stats.CurrentHp <= 0)
                {
                    statistics.totalDeaths++;
                    SaveStatistics();
                }
            }
        }

        private void OnComboForStats(ComboEvent evt)
        {
            if (evt.ComboCount > statistics.maxCombo)
            {
                statistics.maxCombo = evt.ComboCount;
                SaveStatistics();
            }
        }

        private void OnKillForStats(EnemyDeathEvent evt)
        {
            statistics.totalKills++;

            // 보스 킬 확인 (EnemyId에 boss 포함 여부)
            if (evt.EnemyId != null && evt.EnemyId.ToLower().Contains("boss"))
            {
                statistics.bossKills++;
            }

            SaveStatistics();
        }

        private void OnStageClearForStats(StageCompleteEvent evt)
        {
            if (evt.ClearTime < statistics.fastestStageClear)
            {
                statistics.fastestStageClear = evt.ClearTime;
                SaveStatistics();
            }
        }

        private void OnItemForStats(ItemDropEvent evt)
        {
            statistics.totalItemsCollected += evt.Quantity;
            SaveStatistics();
        }

        private void OnGoldForStats(EnemyRewardEvent evt)
        {
            statistics.totalGoldEarned += evt.Gold;
        }

        // ── Utility ───────────────────────────────────────────

        private void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null)
                panel.SetActive(active);
        }

        private void SetTabColor(Button tab, Color color)
        {
            if (tab == null) return;
            var colors = tab.colors;
            colors.normalColor = color;
            tab.colors = colors;
        }

        private Color GetGradeColor(ChallengeGrade grade)
        {
            return grade switch
            {
                ChallengeGrade.S => gradeColorS,
                ChallengeGrade.A => gradeColorA,
                ChallengeGrade.B => gradeColorB,
                ChallengeGrade.C => gradeColorC,
                _ => gradeColorC
            };
        }

        private void ClearContainer(Transform container)
        {
            if (container == null) return;

            // spawnedUIElements 목록에서 해당 컨테이너의 자식들을 제거
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                var child = container.GetChild(i).gameObject;
                spawnedUIElements.Remove(child);
                Destroy(child);
            }
        }

        private string FormatTime(float seconds)
        {
            if (seconds <= 0f || seconds >= float.MaxValue) return "--:--";

            int totalSec = Mathf.RoundToInt(seconds);
            int hours = totalSec / 3600;
            int minutes = (totalSec % 3600) / 60;
            int secs = totalSec % 60;

            if (hours > 0)
                return $"{hours}:{minutes:D2}:{secs:D2}";
            return $"{minutes}:{secs:D2}";
        }

        // ── Save / Load Statistics ────────────────────────────

        private void SaveStatistics()
        {
            string json = JsonUtility.ToJson(statistics);
            PlayerPrefs.SetString("PlayerStatistics", json);
            PlayerPrefs.Save();
        }

        private void LoadStatistics()
        {
            string json = PlayerPrefs.GetString("PlayerStatistics", "");
            if (!string.IsNullOrEmpty(json))
            {
                statistics = JsonUtility.FromJson<PlayerStatistics>(json);
            }
        }
    }
}
