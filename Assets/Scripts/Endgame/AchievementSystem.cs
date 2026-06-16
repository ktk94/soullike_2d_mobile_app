using System;
using System.Collections.Generic;
using UnityEngine;
using SoulCraft.Core;

namespace SoulCraft.Endgame
{
    // ── Enums ─────────────────────────────────────────────────

    public enum AchievementCategory
    {
        Combat,
        Exploration,
        Collection,
        Enhancement,
        Growth,
        Daily,
        Hidden
    }

    public enum ConditionType
    {
        /// <summary>누적형: 현재값이 목표값 이상이면 달성</summary>
        Cumulative,
        /// <summary>단발형: 특정 조건을 한 번 만족하면 달성</summary>
        OneShot
    }

    public enum AchievementRewardType
    {
        Gold,
        Title,
        UniqueEquipment,
        SpecialMaterial
    }

    // ── Data Definitions ──────────────────────────────────────

    [Serializable]
    public class AchievementCondition
    {
        public ConditionType conditionType;
        public float targetValue;
        public float currentValue;

        public bool IsMet => currentValue >= targetValue;
        public float Progress => targetValue > 0 ? Mathf.Clamp01(currentValue / targetValue) : 0f;
    }

    [Serializable]
    public class AchievementReward
    {
        public AchievementRewardType type;
        public int goldAmount;
        public string titleName;
        public string itemId;
    }

    [Serializable]
    public class Achievement
    {
        public string id;
        public string name;
        public string description;
        public AchievementCategory category;
        public AchievementCondition condition;
        public AchievementReward reward;
        public bool isUnlocked;
        public bool isHidden;

        /// <summary>보상 수령 완료 여부</summary>
        public bool isRewardClaimed;
    }

    [Serializable]
    public class AchievementSaveData
    {
        public List<AchievementSaveEntry> entries = new();
    }

    [Serializable]
    public class AchievementSaveEntry
    {
        public string id;
        public float currentValue;
        public bool isUnlocked;
        public bool isRewardClaimed;
    }

    // ── Events ────────────────────────────────────────────────

    public struct AchievementUnlockedEvent
    {
        public string AchievementId;
        public string AchievementName;
        public AchievementCategory Category;
    }

    /// <summary>
    /// 업적 시스템.
    /// GameEventSystem의 다양한 이벤트를 구독하여 업적 진행도를 자동 추적하고,
    /// 달성 시 보상을 지급한다.
    /// </summary>
    public class AchievementSystem : MonoBehaviour
    {
        public static AchievementSystem Instance { get; private set; }

        // ── Inspector ─────────────────────────────────────────

        [Header("UI")]
        [SerializeField] private GameObject achievementPopupPrefab;
        [SerializeField] private Transform popupParent;
        [SerializeField] private float popupDuration = 3f;

        // ── Runtime ───────────────────────────────────────────

        private readonly Dictionary<string, Achievement> achievements = new();
        private readonly List<Achievement> achievementList = new();

        public IReadOnlyList<Achievement> AllAchievements => achievementList;
        public event Action<Achievement> OnAchievementUnlocked;

        // 추적 카운터
        private int totalKills;
        private int maxCombo;
        private bool bossNoHitCleared;
        private HashSet<string> usedElements = new();
        private HashSet<string> discoveredSynergies = new();

        // ── Lifecycle ─────────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializeAchievements();
        }

        void OnEnable()
        {
            // GameEventSystem 구독
            GameEventSystem.Subscribe<EnemyDeathEvent>(OnEnemyDeath);
            GameEventSystem.Subscribe<DamageEvent>(OnDamage);
            GameEventSystem.Subscribe<ComboEvent>(OnCombo);
            GameEventSystem.Subscribe<SkillUsedEvent>(OnSkillUsed);
            GameEventSystem.Subscribe<StageCompleteEvent>(OnStageComplete);
            GameEventSystem.Subscribe<ItemDropEvent>(OnItemDrop);
            GameEventSystem.Subscribe<SynergyActivatedEvent>(OnSynergyActivated);
            GameEventSystem.Subscribe<PassiveUnlockedEvent>(OnPassiveUnlocked);
            GameEventSystem.Subscribe<DungeonFloorClearedEvent>(OnDungeonFloorCleared);
            GameEventSystem.Subscribe<DailyChallengeCompletedEvent>(OnDailyChallengeCompleted);
        }

        void OnDisable()
        {
            GameEventSystem.Unsubscribe<EnemyDeathEvent>(OnEnemyDeath);
            GameEventSystem.Unsubscribe<DamageEvent>(OnDamage);
            GameEventSystem.Unsubscribe<ComboEvent>(OnCombo);
            GameEventSystem.Unsubscribe<SkillUsedEvent>(OnSkillUsed);
            GameEventSystem.Unsubscribe<StageCompleteEvent>(OnStageComplete);
            GameEventSystem.Unsubscribe<ItemDropEvent>(OnItemDrop);
            GameEventSystem.Unsubscribe<SynergyActivatedEvent>(OnSynergyActivated);
            GameEventSystem.Unsubscribe<PassiveUnlockedEvent>(OnPassiveUnlocked);
            GameEventSystem.Unsubscribe<DungeonFloorClearedEvent>(OnDungeonFloorCleared);
            GameEventSystem.Unsubscribe<DailyChallengeCompletedEvent>(OnDailyChallengeCompleted);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Achievement Definitions (30개) ────────────────────

        private void InitializeAchievements()
        {
            achievements.Clear();
            achievementList.Clear();

            // ======== 전투 (Combat) - 6개 ========

            Register(new Achievement
            {
                id = "combat_first_kill",
                name = "첫 번째 사냥",
                description = "적을 처음으로 처치하세요.",
                category = AchievementCategory.Combat,
                condition = new AchievementCondition { conditionType = ConditionType.Cumulative, targetValue = 1 },
                reward = new AchievementReward { type = AchievementRewardType.Gold, goldAmount = 100 }
            });

            Register(new Achievement
            {
                id = "combat_100_kills",
                name = "백인참",
                description = "적을 100마리 처치하세요.",
                category = AchievementCategory.Combat,
                condition = new AchievementCondition { conditionType = ConditionType.Cumulative, targetValue = 100 },
                reward = new AchievementReward { type = AchievementRewardType.Gold, goldAmount = 1000 }
            });

            Register(new Achievement
            {
                id = "combat_1000_kills",
                name = "천인참",
                description = "적을 1000마리 처치하세요.",
                category = AchievementCategory.Combat,
                condition = new AchievementCondition { conditionType = ConditionType.Cumulative, targetValue = 1000 },
                reward = new AchievementReward { type = AchievementRewardType.Title, titleName = "학살자" }
            });

            Register(new Achievement
            {
                id = "combat_boss_no_hit",
                name = "완벽한 회피",
                description = "보스를 무피격으로 클리어하세요.",
                category = AchievementCategory.Combat,
                condition = new AchievementCondition { conditionType = ConditionType.OneShot, targetValue = 1 },
                reward = new AchievementReward { type = AchievementRewardType.UniqueEquipment, itemId = "eq_perfect_ring" }
            });

            Register(new Achievement
            {
                id = "combat_10_combo",
                name = "연격의 달인",
                description = "10콤보를 달성하세요.",
                category = AchievementCategory.Combat,
                condition = new AchievementCondition { conditionType = ConditionType.OneShot, targetValue = 10 },
                reward = new AchievementReward { type = AchievementRewardType.Gold, goldAmount = 500 }
            });

            Register(new Achievement
            {
                id = "combat_all_elements",
                name = "만능 속성사",
                description = "모든 속성 스킬을 사용하세요.",
                category = AchievementCategory.Combat,
                condition = new AchievementCondition { conditionType = ConditionType.Cumulative, targetValue = 6 },
                reward = new AchievementReward { type = AchievementRewardType.Title, titleName = "원소 지배자" }
            });

            // ======== 탐험 (Exploration) - 5개 ========

            Register(new Achievement
            {
                id = "explore_stage1_clear",
                name = "첫 발걸음",
                description = "1스테이지를 클리어하세요.",
                category = AchievementCategory.Exploration,
                condition = new AchievementCondition { conditionType = ConditionType.OneShot, targetValue = 1 },
                reward = new AchievementReward { type = AchievementRewardType.Gold, goldAmount = 300 }
            });

            Register(new Achievement
            {
                id = "explore_all_stages",
                name = "세계의 끝",
                description = "모든 스테이지를 클리어하세요.",
                category = AchievementCategory.Exploration,
                condition = new AchievementCondition { conditionType = ConditionType.Cumulative, targetValue = 5 },
                reward = new AchievementReward { type = AchievementRewardType.Title, titleName = "정복자" }
            });

            Register(new Achievement
            {
                id = "explore_dungeon_10",
                name = "심연의 시작",
                description = "무한 던전 10층을 돌파하세요.",
                category = AchievementCategory.Exploration,
                condition = new AchievementCondition { conditionType = ConditionType.OneShot, targetValue = 10 },
                reward = new AchievementReward { type = AchievementRewardType.Gold, goldAmount = 2000 }
            });

            Register(new Achievement
            {
                id = "explore_dungeon_50",
                name = "심연의 탐험가",
                description = "무한 던전 50층을 돌파하세요.",
                category = AchievementCategory.Exploration,
                condition = new AchievementCondition { conditionType = ConditionType.OneShot, targetValue = 50 },
                reward = new AchievementReward { type = AchievementRewardType.UniqueEquipment, itemId = "eq_abyss_blade" }
            });

            Register(new Achievement
            {
                id = "explore_dungeon_100",
                name = "심연의 지배자",
                description = "무한 던전 100층을 돌파하세요.",
                category = AchievementCategory.Exploration,
                condition = new AchievementCondition { conditionType = ConditionType.OneShot, targetValue = 100 },
                reward = new AchievementReward { type = AchievementRewardType.Title, titleName = "심연의 왕" }
            });

            // ======== 수집 (Collection) - 4개 ========

            Register(new Achievement
            {
                id = "collect_100_items",
                name = "수집가",
                description = "아이템을 100개 수집하세요.",
                category = AchievementCategory.Collection,
                condition = new AchievementCondition { conditionType = ConditionType.Cumulative, targetValue = 100 },
                reward = new AchievementReward { type = AchievementRewardType.Gold, goldAmount = 800 }
            });

            Register(new Achievement
            {
                id = "collect_legendary",
                name = "전설의 빛",
                description = "레전더리 등급 아이템을 획득하세요.",
                category = AchievementCategory.Collection,
                condition = new AchievementCondition { conditionType = ConditionType.OneShot, targetValue = 1 },
                reward = new AchievementReward { type = AchievementRewardType.Gold, goldAmount = 3000 }
            });

            Register(new Achievement
            {
                id = "collect_all_synergies",
                name = "연금술사",
                description = "모든 시너지를 발견하세요.",
                category = AchievementCategory.Collection,
                condition = new AchievementCondition { conditionType = ConditionType.Cumulative, targetValue = 10 },
                reward = new AchievementReward { type = AchievementRewardType.Title, titleName = "연금술의 대가" }
            });

            Register(new Achievement
            {
                id = "collect_full_epic_gear",
                name = "완전 무장",
                description = "모든 장비 슬롯을 에픽 이상으로 채우세요.",
                category = AchievementCategory.Collection,
                condition = new AchievementCondition { conditionType = ConditionType.OneShot, targetValue = 1 },
                reward = new AchievementReward { type = AchievementRewardType.UniqueEquipment, itemId = "eq_champion_crown" }
            });

            // ======== 강화 (Enhancement) - 3개 ========

            Register(new Achievement
            {
                id = "enhance_first",
                name = "강화의 시작",
                description = "장비를 처음으로 강화하세요.",
                category = AchievementCategory.Enhancement,
                condition = new AchievementCondition { conditionType = ConditionType.OneShot, targetValue = 1 },
                reward = new AchievementReward { type = AchievementRewardType.Gold, goldAmount = 200 }
            });

            Register(new Achievement
            {
                id = "enhance_plus10",
                name = "+10 달성",
                description = "장비를 +10까지 강화하세요.",
                category = AchievementCategory.Enhancement,
                condition = new AchievementCondition { conditionType = ConditionType.OneShot, targetValue = 1 },
                reward = new AchievementReward { type = AchievementRewardType.Title, titleName = "강화의 달인" }
            });

            Register(new Achievement
            {
                id = "enhance_fail_10",
                name = "불굴의 의지",
                description = "강화에 10회 실패하세요. (...힘내세요)",
                category = AchievementCategory.Enhancement,
                condition = new AchievementCondition { conditionType = ConditionType.Cumulative, targetValue = 10 },
                reward = new AchievementReward { type = AchievementRewardType.Gold, goldAmount = 5000 }
            });

            // ======== 성장 (Growth) - 5개 ========

            Register(new Achievement
            {
                id = "growth_level_10",
                name = "성장의 발판",
                description = "레벨 10에 도달하세요.",
                category = AchievementCategory.Growth,
                condition = new AchievementCondition { conditionType = ConditionType.OneShot, targetValue = 10 },
                reward = new AchievementReward { type = AchievementRewardType.Gold, goldAmount = 500 }
            });

            Register(new Achievement
            {
                id = "growth_level_25",
                name = "숙련 전사",
                description = "레벨 25에 도달하세요.",
                category = AchievementCategory.Growth,
                condition = new AchievementCondition { conditionType = ConditionType.OneShot, targetValue = 25 },
                reward = new AchievementReward { type = AchievementRewardType.Gold, goldAmount = 1500 }
            });

            Register(new Achievement
            {
                id = "growth_level_50",
                name = "전설의 시작",
                description = "레벨 50에 도달하세요.",
                category = AchievementCategory.Growth,
                condition = new AchievementCondition { conditionType = ConditionType.OneShot, targetValue = 50 },
                reward = new AchievementReward { type = AchievementRewardType.Title, titleName = "전설의 전사" }
            });

            Register(new Achievement
            {
                id = "growth_all_passives",
                name = "만능인",
                description = "모든 패시브 스킬을 해금하세요.",
                category = AchievementCategory.Growth,
                condition = new AchievementCondition { conditionType = ConditionType.OneShot, targetValue = 1 },
                reward = new AchievementReward { type = AchievementRewardType.Title, titleName = "패시브 마스터" }
            });

            Register(new Achievement
            {
                id = "growth_all_skills",
                name = "기술의 정점",
                description = "모든 스킬을 획득하세요.",
                category = AchievementCategory.Growth,
                condition = new AchievementCondition { conditionType = ConditionType.OneShot, targetValue = 1 },
                reward = new AchievementReward { type = AchievementRewardType.Title, titleName = "스킬 마스터" }
            });

            // ======== 일일 (Daily) - 3개 ========

            Register(new Achievement
            {
                id = "daily_first_clear",
                name = "일일 도전 입문",
                description = "일일 도전을 처음으로 클리어하세요.",
                category = AchievementCategory.Daily,
                condition = new AchievementCondition { conditionType = ConditionType.OneShot, targetValue = 1 },
                reward = new AchievementReward { type = AchievementRewardType.Gold, goldAmount = 500 }
            });

            Register(new Achievement
            {
                id = "daily_7_consecutive",
                name = "꾸준한 도전자",
                description = "일일 도전을 7일 연속 클리어하세요.",
                category = AchievementCategory.Daily,
                condition = new AchievementCondition { conditionType = ConditionType.Cumulative, targetValue = 7 },
                reward = new AchievementReward { type = AchievementRewardType.UniqueEquipment, itemId = "eq_daily_amulet" }
            });

            Register(new Achievement
            {
                id = "daily_s_grade",
                name = "완벽한 하루",
                description = "일일 도전에서 S등급을 달성하세요.",
                category = AchievementCategory.Daily,
                condition = new AchievementCondition { conditionType = ConditionType.OneShot, targetValue = 1 },
                reward = new AchievementReward { type = AchievementRewardType.Title, titleName = "완벽주의자" }
            });

            // ======== 숨겨진 (Hidden) - 4개 ========

            Register(new Achievement
            {
                id = "hidden_true_vessel",
                name = "그릇의 진정한 주인",
                description = "???",
                category = AchievementCategory.Hidden,
                condition = new AchievementCondition { conditionType = ConditionType.OneShot, targetValue = 1 },
                reward = new AchievementReward { type = AchievementRewardType.UniqueEquipment, itemId = "eq_malrok_soul" },
                isHidden = true
            });

            Register(new Achievement
            {
                id = "hidden_speedrun",
                name = "빛보다 빠르게",
                description = "???",
                category = AchievementCategory.Hidden,
                condition = new AchievementCondition { conditionType = ConditionType.OneShot, targetValue = 1 },
                reward = new AchievementReward { type = AchievementRewardType.Title, titleName = "번개" },
                isHidden = true
            });

            Register(new Achievement
            {
                id = "hidden_pacifist_floor",
                name = "무저항의 길",
                description = "???",
                category = AchievementCategory.Hidden,
                condition = new AchievementCondition { conditionType = ConditionType.OneShot, targetValue = 1 },
                reward = new AchievementReward { type = AchievementRewardType.Gold, goldAmount = 10000 },
                isHidden = true
            });

            Register(new Achievement
            {
                id = "hidden_eternal_warrior",
                name = "영원한 전사",
                description = "???",
                category = AchievementCategory.Hidden,
                condition = new AchievementCondition { conditionType = ConditionType.OneShot, targetValue = 1 },
                reward = new AchievementReward { type = AchievementRewardType.Title, titleName = "영원한 전사" },
                isHidden = true
            });
        }

        private void Register(Achievement achievement)
        {
            achievements[achievement.id] = achievement;
            achievementList.Add(achievement);
        }

        // ── Public API ────────────────────────────────────────

        /// <summary>
        /// 업적의 진행도를 증가시킨다.
        /// </summary>
        public void AddProgress(string achievementId, float amount = 1f)
        {
            if (!achievements.TryGetValue(achievementId, out var achievement)) return;
            if (achievement.isUnlocked) return;

            achievement.condition.currentValue += amount;

            if (achievement.condition.IsMet)
            {
                UnlockAchievement(achievement);
            }
        }

        /// <summary>
        /// 업적의 진행도를 특정 값으로 설정한다.
        /// </summary>
        public void SetProgress(string achievementId, float value)
        {
            if (!achievements.TryGetValue(achievementId, out var achievement)) return;
            if (achievement.isUnlocked) return;

            achievement.condition.currentValue = value;

            if (achievement.condition.IsMet)
            {
                UnlockAchievement(achievement);
            }
        }

        /// <summary>
        /// 업적을 직접 해금한다 (OneShot 타입용).
        /// </summary>
        public void TriggerAchievement(string achievementId)
        {
            if (!achievements.TryGetValue(achievementId, out var achievement)) return;
            if (achievement.isUnlocked) return;

            achievement.condition.currentValue = achievement.condition.targetValue;
            UnlockAchievement(achievement);
        }

        /// <summary>
        /// 업적 보상을 수령한다.
        /// </summary>
        public bool ClaimReward(string achievementId)
        {
            if (!achievements.TryGetValue(achievementId, out var achievement)) return false;
            if (!achievement.isUnlocked || achievement.isRewardClaimed) return false;

            achievement.isRewardClaimed = true;
            ApplyReward(achievement.reward);
            SaveProgress();

            Debug.Log($"[AchievementSystem] 보상 수령: {achievement.name}");
            return true;
        }

        /// <summary>
        /// 카테고리별 업적 목록을 반환한다.
        /// </summary>
        public List<Achievement> GetByCategory(AchievementCategory category)
        {
            var result = new List<Achievement>();
            foreach (var a in achievementList)
            {
                if (a.category == category)
                    result.Add(a);
            }
            return result;
        }

        /// <summary>
        /// 전체 달성률을 반환한다 (0.0~1.0).
        /// </summary>
        public float GetCompletionRate()
        {
            if (achievementList.Count == 0) return 0f;

            int unlocked = 0;
            foreach (var a in achievementList)
            {
                if (a.isUnlocked) unlocked++;
            }
            return (float)unlocked / achievementList.Count;
        }

        /// <summary>
        /// 해금된 업적 수를 반환한다.
        /// </summary>
        public int GetUnlockedCount()
        {
            int count = 0;
            foreach (var a in achievementList)
            {
                if (a.isUnlocked) count++;
            }
            return count;
        }

        /// <summary>
        /// 미수령 보상이 있는 업적 수를 반환한다.
        /// </summary>
        public int GetUnclaimedRewardCount()
        {
            int count = 0;
            foreach (var a in achievementList)
            {
                if (a.isUnlocked && !a.isRewardClaimed) count++;
            }
            return count;
        }

        // ── Unlock Logic ──────────────────────────────────────

        private void UnlockAchievement(Achievement achievement)
        {
            if (achievement.isUnlocked) return;

            achievement.isUnlocked = true;

            // 팝업 알림
            ShowUnlockPopup(achievement);

            // 이벤트 발행
            GameEventSystem.Publish(new AchievementUnlockedEvent
            {
                AchievementId = achievement.id,
                AchievementName = achievement.name,
                Category = achievement.category
            });

            OnAchievementUnlocked?.Invoke(achievement);
            SaveProgress();

            Debug.Log($"[AchievementSystem] 업적 달성! {achievement.name}: {achievement.description}");
        }

        /// <summary>
        /// 화면 상단에 업적 달성 알림 팝업을 표시한다.
        /// </summary>
        private void ShowUnlockPopup(Achievement achievement)
        {
            if (achievementPopupPrefab == null) return;

            Transform parent = popupParent;
            if (parent == null)
            {
                // Canvas를 찾아서 상단에 배치
                Canvas canvas = FindFirstObjectByType<Canvas>();
                if (canvas != null)
                    parent = canvas.transform;
            }

            if (parent == null) return;

            GameObject popup = Instantiate(achievementPopupPrefab, parent);

            // 팝업에 AchievementPopup 컴포넌트가 있으면 초기화
            var popupComp = popup.GetComponent<AchievementPopup>();
            if (popupComp != null)
            {
                string displayName = achievement.isHidden
                    ? $"숨겨진 업적: {achievement.name}"
                    : $"업적 달성: {achievement.name}";
                popupComp.Show(displayName, achievement.description, popupDuration);
            }
            else
            {
                // 팝업 컴포넌트가 없으면 일정 시간 후 파괴
                Destroy(popup, popupDuration);
            }
        }

        // ── Reward Application ────────────────────────────────

        private void ApplyReward(AchievementReward reward)
        {
            if (reward == null) return;

            switch (reward.type)
            {
                case AchievementRewardType.Gold:
                    var player = GameObject.FindGameObjectWithTag("Player");
                    if (player != null)
                    {
                        var stats = player.GetComponent<Player.PlayerStats>();
                        if (stats != null)
                            stats.Gold += reward.goldAmount;
                    }
                    Debug.Log($"[AchievementSystem] 골드 보상: +{reward.goldAmount}");
                    break;

                case AchievementRewardType.Title:
                    // 칭호는 별도 칭호 시스템에서 관리
                    Debug.Log($"[AchievementSystem] 칭호 획득: {reward.titleName}");
                    break;

                case AchievementRewardType.UniqueEquipment:
                    // 고유 장비는 아이템 시스템에서 생성
                    Debug.Log($"[AchievementSystem] 고유 장비 획득: {reward.itemId}");
                    break;

                case AchievementRewardType.SpecialMaterial:
                    Debug.Log($"[AchievementSystem] 특별 재료 획득: {reward.itemId}");
                    break;
            }
        }

        // ── Event Handlers ────────────────────────────────────

        private void OnEnemyDeath(EnemyDeathEvent evt)
        {
            totalKills++;

            // 전투: 첫 킬, 100킬, 1000킬
            AddProgress("combat_first_kill", 1);
            SetProgress("combat_100_kills", totalKills);
            SetProgress("combat_1000_kills", totalKills);
        }

        private void OnDamage(DamageEvent evt)
        {
            // 보스 무피격 추적은 별도 로직 (BossFight 상태에서 피격 체크)
        }

        private void OnCombo(ComboEvent evt)
        {
            // 10콤보 달성
            if (evt.ComboCount >= 10)
            {
                TriggerAchievement("combat_10_combo");
            }

            if (evt.ComboCount > maxCombo)
                maxCombo = evt.ComboCount;
        }

        private void OnSkillUsed(SkillUsedEvent evt)
        {
            // 모든 속성 스킬 사용 추적
            // 스킬 ID에서 속성 판별은 SkillData를 참조해야 하지만,
            // 이벤트에는 SkillId만 있으므로 별도 매핑이 필요.
            // 간단하게 ID에 속성 접두사가 포함된 경우 추적.
            string id = evt.SkillId.ToLower();
            if (id.Contains("fire") || id.Contains("flame"))
                usedElements.Add("Fire");
            if (id.Contains("ice") || id.Contains("frost"))
                usedElements.Add("Ice");
            if (id.Contains("lightning") || id.Contains("thunder"))
                usedElements.Add("Lightning");
            if (id.Contains("dark") || id.Contains("shadow"))
                usedElements.Add("Dark");
            if (id.Contains("holy") || id.Contains("light"))
                usedElements.Add("Holy");
            if (id.Contains("physical") || id.Contains("basic") || id.Contains("slash"))
                usedElements.Add("Physical");

            SetProgress("combat_all_elements", usedElements.Count);
        }

        private void OnStageComplete(StageCompleteEvent evt)
        {
            // 1스테이지 클리어
            if (evt.StageIndex == 0)
                TriggerAchievement("explore_stage1_clear");

            // 전 스테이지 클리어
            AddProgress("explore_all_stages", 1);
        }

        private void OnItemDrop(ItemDropEvent evt)
        {
            // 아이템 수집 추적
            AddProgress("collect_100_items", evt.Quantity);
        }

        private void OnSynergyActivated(SynergyActivatedEvent evt)
        {
            discoveredSynergies.Add(evt.SynergyId);
            SetProgress("collect_all_synergies", discoveredSynergies.Count);
        }

        private void OnPassiveUnlocked(PassiveUnlockedEvent evt)
        {
            // 패시브 해금 추적은 PassiveManager의 전체 해금 상태를 확인해야 함
        }

        private void OnDungeonFloorCleared(DungeonFloorClearedEvent evt)
        {
            // 무한 던전 층수 업적
            if (evt.Floor >= 10) TriggerAchievement("explore_dungeon_10");
            if (evt.Floor >= 50) TriggerAchievement("explore_dungeon_50");
            if (evt.Floor >= 100) TriggerAchievement("explore_dungeon_100");
        }

        private void OnDailyChallengeCompleted(DailyChallengeCompletedEvent evt)
        {
            // 일일 도전 업적
            TriggerAchievement("daily_first_clear");

            if (evt.Grade == ChallengeGrade.S)
                TriggerAchievement("daily_s_grade");

            // 연속 클리어는 DailyChallenge에서 ConsecutiveDays를 조회
            if (DailyChallenge.Instance != null)
            {
                SetProgress("daily_7_consecutive", DailyChallenge.Instance.ConsecutiveDays);
            }
        }

        // ── External Triggers ─────────────────────────────────

        /// <summary>
        /// 보스 무피격 클리어 시 외부에서 호출.
        /// </summary>
        public void OnBossNoHitClear()
        {
            TriggerAchievement("combat_boss_no_hit");
        }

        /// <summary>
        /// 레전더리 아이템 획득 시 외부에서 호출.
        /// </summary>
        public void OnLegendaryItemObtained()
        {
            TriggerAchievement("collect_legendary");
        }

        /// <summary>
        /// 전 장비 슬롯 에픽 이상 달성 시 외부에서 호출.
        /// </summary>
        public void OnFullEpicGear()
        {
            TriggerAchievement("collect_full_epic_gear");
        }

        /// <summary>
        /// 강화 시도 시 외부에서 호출.
        /// </summary>
        public void OnEnhanceAttempt(bool success, int newLevel)
        {
            if (success)
            {
                TriggerAchievement("enhance_first");
                if (newLevel >= 10)
                    TriggerAchievement("enhance_plus10");
            }
            else
            {
                AddProgress("enhance_fail_10", 1);
            }
        }

        /// <summary>
        /// 레벨업 시 외부에서 호출.
        /// </summary>
        public void OnPlayerLevelUp(int newLevel)
        {
            if (newLevel >= 10) TriggerAchievement("growth_level_10");
            if (newLevel >= 25) TriggerAchievement("growth_level_25");
            if (newLevel >= 50) TriggerAchievement("growth_level_50");
        }

        /// <summary>
        /// 모든 패시브 해금 시 외부에서 호출.
        /// </summary>
        public void OnAllPassivesUnlocked()
        {
            TriggerAchievement("growth_all_passives");
        }

        /// <summary>
        /// 모든 스킬 획득 시 외부에서 호출.
        /// </summary>
        public void OnAllSkillsObtained()
        {
            TriggerAchievement("growth_all_skills");
        }

        /// <summary>
        /// 말로크 처치 후 특수 조건 충족 시 외부에서 호출.
        /// </summary>
        public void OnMalrokSpecialCondition()
        {
            TriggerAchievement("hidden_true_vessel");
        }

        // ── Save / Load ──────────────────────────────────────

        public AchievementSaveData ToSaveData()
        {
            var save = new AchievementSaveData();
            foreach (var a in achievementList)
            {
                save.entries.Add(new AchievementSaveEntry
                {
                    id = a.id,
                    currentValue = a.condition.currentValue,
                    isUnlocked = a.isUnlocked,
                    isRewardClaimed = a.isRewardClaimed
                });
            }
            return save;
        }

        public void LoadFromSave(AchievementSaveData save)
        {
            if (save == null || save.entries == null) return;

            foreach (var entry in save.entries)
            {
                if (achievements.TryGetValue(entry.id, out var achievement))
                {
                    achievement.condition.currentValue = entry.currentValue;
                    achievement.isUnlocked = entry.isUnlocked;
                    achievement.isRewardClaimed = entry.isRewardClaimed;
                }
            }

            // totalKills 복원
            if (achievements.TryGetValue("combat_1000_kills", out var killAchv))
                totalKills = (int)killAchv.condition.currentValue;
        }

        private void SaveProgress()
        {
            string json = JsonUtility.ToJson(ToSaveData());
            PlayerPrefs.SetString("Achievement_Save", json);
            PlayerPrefs.Save();
        }

        public void LoadProgress()
        {
            string json = PlayerPrefs.GetString("Achievement_Save", "");
            if (!string.IsNullOrEmpty(json))
            {
                var data = JsonUtility.FromJson<AchievementSaveData>(json);
                LoadFromSave(data);
            }
        }
    }

    // ── Achievement Popup Component ───────────────────────────

    /// <summary>
    /// 업적 달성 팝업 UI 컴포넌트.
    /// Prefab에 부착하여 사용한다.
    /// </summary>
    public class AchievementPopup : MonoBehaviour
    {
        [SerializeField] private TMPro.TMP_Text titleText;
        [SerializeField] private TMPro.TMP_Text descriptionText;
        [SerializeField] private UnityEngine.UI.Image iconImage;
        [SerializeField] private CanvasGroup canvasGroup;

        private float duration;
        private float elapsed;
        private float fadeOutStart;

        public void Show(string title, string description, float displayDuration)
        {
            duration = displayDuration;
            fadeOutStart = duration * 0.7f;
            elapsed = 0f;

            if (titleText != null) titleText.text = title;
            if (descriptionText != null) descriptionText.text = description;
            if (canvasGroup != null) canvasGroup.alpha = 1f;
        }

        void Update()
        {
            elapsed += Time.unscaledDeltaTime;

            // 페이드 아웃
            if (elapsed >= fadeOutStart && canvasGroup != null)
            {
                float fadeProgress = (elapsed - fadeOutStart) / (duration - fadeOutStart);
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, fadeProgress);
            }

            if (elapsed >= duration)
            {
                Destroy(gameObject);
            }
        }
    }
}
