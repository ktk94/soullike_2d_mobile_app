using System;
using System.Collections.Generic;
using UnityEngine;
using SoulCraft.Core;

namespace SoulCraft.Endgame
{
    // ── Enums ─────────────────────────────────────────────────

    public enum ChallengeModifierType
    {
        // 적 강화
        EnemyHpDouble,
        EnemyAtkUp,

        // 플레이어 제한
        NoManaRegen,
        SkillLimitTwo,
        DashCooldownDouble,

        // 특수 조건
        FireOnlyEffective,
        TimeLimitThirtySeconds,
        NoDamageTaken
    }

    public enum ChallengeGrade
    {
        C,
        B,
        A,
        S
    }

    // ── Data Definitions ──────────────────────────────────────

    [Serializable]
    public class ChallengeModifier
    {
        public ChallengeModifierType type;
        public string displayName;
        public string description;
        public float value;
    }

    [Serializable]
    public class ChallengeReward
    {
        public ChallengeGrade grade;
        public int goldReward;
        public int specialMaterialCount;
        public string specialMaterialId;
    }

    [Serializable]
    public class DailyChallengeSaveData
    {
        public string lastCompletedDate;
        public int totalCompletions;
        public int consecutiveDays;
        public string lastConsecutiveDate;
        public int bestGradeInt; // ChallengeGrade as int
    }

    /// <summary>
    /// 오늘의 도전 조건을 묶어 전달하는 구조체.
    /// </summary>
    public struct DailyChallengeConfig
    {
        public int Seed;
        public DateTime Date;
        public List<ChallengeModifier> Modifiers;
        public string ChallengeName;
    }

    // ── Events ────────────────────────────────────────────────

    public struct DailyChallengeStartedEvent
    {
        public string ChallengeName;
        public int ModifierCount;
    }

    public struct DailyChallengeCompletedEvent
    {
        public ChallengeGrade Grade;
        public float ClearTime;
        public int DamageTaken;
    }

    /// <summary>
    /// 일일 도전 모드.
    /// 날짜(DateTime) 기반 시드로 매일 다른 도전 조건을 생성한다.
    /// 하루 1회만 도전 가능.
    /// </summary>
    public class DailyChallenge : MonoBehaviour
    {
        public static DailyChallenge Instance { get; private set; }

        // ── Inspector ─────────────────────────────────────────

        [Header("Challenge Settings")]
        [Tooltip("도전에 적용될 동시 조건 수 (최소/최대)")]
        [SerializeField] private int minModifiers = 2;
        [SerializeField] private int maxModifiers = 3;

        [Header("Grade Thresholds")]
        [SerializeField] private float sGradeTimeLimit = 30f;
        [SerializeField] private float aGradeTimeLimit = 60f;
        [SerializeField] private float bGradeTimeLimit = 120f;
        [SerializeField] private int sGradeMaxDamage = 0;
        [SerializeField] private int aGradeMaxDamage = 50;

        [Header("Rewards")]
        [SerializeField] private ChallengeReward[] gradeRewards;

        // ── Runtime ───────────────────────────────────────────

        public DailyChallengeConfig TodayConfig { get; private set; }
        public bool IsRunning { get; private set; }
        public bool HasCompletedToday { get; private set; }
        public int TotalCompletions { get; private set; }
        public int ConsecutiveDays { get; private set; }
        public ChallengeGrade BestGrade { get; private set; } = ChallengeGrade.C;

        public event Action<DailyChallengeConfig> OnChallengeGenerated;
        public event Action OnChallengeStarted;
        public event Action<ChallengeGrade, ChallengeReward> OnChallengeCompleted;
        public event Action OnChallengeFailed;

        // 도전 중 추적
        private float challengeStartTime;
        private int damageTakenDuringChallenge;
        private int skillsUsedCount;
        private HashSet<string> skillsUsed = new();

        // 저장
        private string lastCompletedDate;
        private string lastConsecutiveDate;

        // 모든 가능한 조건
        private static readonly ChallengeModifier[] AllModifiers = new ChallengeModifier[]
        {
            new ChallengeModifier
            {
                type = ChallengeModifierType.EnemyHpDouble,
                displayName = "적 HP 2배",
                description = "모든 적의 HP가 2배가 됩니다.",
                value = 2f
            },
            new ChallengeModifier
            {
                type = ChallengeModifierType.EnemyAtkUp,
                displayName = "적 공격력 1.5배",
                description = "모든 적의 공격력이 1.5배가 됩니다.",
                value = 1.5f
            },
            new ChallengeModifier
            {
                type = ChallengeModifierType.NoManaRegen,
                displayName = "마나 회복 없음",
                description = "도전 중 마나가 자연 회복되지 않습니다.",
                value = 0f
            },
            new ChallengeModifier
            {
                type = ChallengeModifierType.SkillLimitTwo,
                displayName = "스킬 2개만 사용",
                description = "스킬 슬롯 2개만 사용할 수 있습니다.",
                value = 2f
            },
            new ChallengeModifier
            {
                type = ChallengeModifierType.DashCooldownDouble,
                displayName = "대시 쿨다운 2배",
                description = "대시 쿨다운이 2배로 증가합니다.",
                value = 2f
            },
            new ChallengeModifier
            {
                type = ChallengeModifierType.FireOnlyEffective,
                displayName = "화염 속성만 유효",
                description = "화염 속성 공격만 적에게 데미지를 줄 수 있습니다.",
                value = 1f
            },
            new ChallengeModifier
            {
                type = ChallengeModifierType.TimeLimitThirtySeconds,
                displayName = "30초 내 클리어",
                description = "30초 안에 모든 적을 처치해야 합니다.",
                value = 30f
            },
            new ChallengeModifier
            {
                type = ChallengeModifierType.NoDamageTaken,
                displayName = "피격 금지",
                description = "단 한 번이라도 피격되면 실패합니다.",
                value = 0f
            }
        };

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
            LoadProgress();
            GenerateTodayChallenge();
        }

        void Update()
        {
            if (!IsRunning) return;

            // 시간 제한 조건 체크
            if (HasModifier(ChallengeModifierType.TimeLimitThirtySeconds))
            {
                float elapsed = Time.time - challengeStartTime;
                ChallengeModifier timeMod = GetModifier(ChallengeModifierType.TimeLimitThirtySeconds);
                if (timeMod != null && elapsed >= timeMod.value)
                {
                    FailChallenge("시간 초과!");
                }
            }
        }

        void OnEnable()
        {
            GameEventSystem.Subscribe<DamageEvent>(OnDamageReceived);
            GameEventSystem.Subscribe<SkillUsedEvent>(OnSkillUsed);
            GameEventSystem.Subscribe<EnemyDeathEvent>(OnEnemyDeathInChallenge);
        }

        void OnDisable()
        {
            GameEventSystem.Unsubscribe<DamageEvent>(OnDamageReceived);
            GameEventSystem.Unsubscribe<SkillUsedEvent>(OnSkillUsed);
            GameEventSystem.Unsubscribe<EnemyDeathEvent>(OnEnemyDeathInChallenge);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Challenge Generation ──────────────────────────────

        /// <summary>
        /// 오늘 날짜 기반으로 도전 조건을 생성한다.
        /// 같은 날에는 항상 같은 조건이 생성된다.
        /// </summary>
        public void GenerateTodayChallenge()
        {
            DateTime today = DateTime.Today;
            int seed = today.Year * 10000 + today.Month * 100 + today.Day;
            System.Random rng = new System.Random(seed);

            // 조건 수 결정
            int modifierCount = rng.Next(minModifiers, maxModifiers + 1);

            // 사용 가능한 조건에서 시드 기반 랜덤 선택 (중복 없이)
            var available = new List<ChallengeModifier>(AllModifiers);
            var selected = new List<ChallengeModifier>();

            for (int i = 0; i < modifierCount && available.Count > 0; i++)
            {
                int idx = rng.Next(0, available.Count);
                selected.Add(available[idx]);
                available.RemoveAt(idx);
            }

            // 도전 이름 생성
            string challengeName = GenerateChallengeName(selected, rng);

            TodayConfig = new DailyChallengeConfig
            {
                Seed = seed,
                Date = today,
                Modifiers = selected,
                ChallengeName = challengeName
            };

            // 오늘 이미 클리어했는지 확인
            CheckTodayCompletion();

            OnChallengeGenerated?.Invoke(TodayConfig);

            Debug.Log($"[DailyChallenge] 오늘의 도전: {challengeName}");
            foreach (var mod in selected)
            {
                Debug.Log($"  - {mod.displayName}: {mod.description}");
            }
        }

        /// <summary>
        /// 조건에 기반한 도전 이름을 생성한다.
        /// </summary>
        private string GenerateChallengeName(List<ChallengeModifier> modifiers, System.Random rng)
        {
            string[] prefixes = { "극한의", "지옥의", "시련의", "불굴의", "광기의", "절망의" };
            string[] suffixes = { "시련", "각성", "투기장", "격전", "전장", "도전" };

            string prefix = prefixes[rng.Next(0, prefixes.Length)];
            string suffix = suffixes[rng.Next(0, suffixes.Length)];

            return $"{prefix} {suffix}";
        }

        // ── Challenge Start / End ─────────────────────────────

        /// <summary>
        /// 오늘의 도전을 시작한다.
        /// </summary>
        public bool StartChallenge()
        {
            if (HasCompletedToday)
            {
                Debug.LogWarning("[DailyChallenge] 오늘은 이미 도전을 완료했습니다.");
                return false;
            }

            if (IsRunning)
            {
                Debug.LogWarning("[DailyChallenge] 이미 도전 중입니다.");
                return false;
            }

            IsRunning = true;
            challengeStartTime = Time.time;
            damageTakenDuringChallenge = 0;
            skillsUsedCount = 0;
            skillsUsed.Clear();

            GameEventSystem.Publish(new DailyChallengeStartedEvent
            {
                ChallengeName = TodayConfig.ChallengeName,
                ModifierCount = TodayConfig.Modifiers.Count
            });

            OnChallengeStarted?.Invoke();

            Debug.Log($"[DailyChallenge] 도전 시작: {TodayConfig.ChallengeName}");
            return true;
        }

        /// <summary>
        /// 도전 성공 처리. 모든 적 처치 시 외부에서 호출.
        /// </summary>
        public void CompleteChallenge()
        {
            if (!IsRunning) return;

            IsRunning = false;
            float clearTime = Time.time - challengeStartTime;

            // 등급 산정
            ChallengeGrade grade = CalculateGrade(clearTime, damageTakenDuringChallenge);

            // 보상 지급
            ChallengeReward reward = GetRewardForGrade(grade);
            ApplyReward(reward);

            // 기록 저장
            HasCompletedToday = true;
            TotalCompletions++;
            lastCompletedDate = DateTime.Today.ToString("yyyy-MM-dd");

            // 연속 출석 체크
            UpdateConsecutiveDays();

            // 최고 등급 갱신
            if ((int)grade > (int)BestGrade)
                BestGrade = grade;

            SaveProgress();

            GameEventSystem.Publish(new DailyChallengeCompletedEvent
            {
                Grade = grade,
                ClearTime = clearTime,
                DamageTaken = damageTakenDuringChallenge
            });

            OnChallengeCompleted?.Invoke(grade, reward);

            Debug.Log($"[DailyChallenge] 도전 완료! 등급: {grade}, 시간: {clearTime:F1}초, " +
                      $"피격 데미지: {damageTakenDuringChallenge}");
        }

        /// <summary>
        /// 도전 실패 처리.
        /// </summary>
        public void FailChallenge(string reason = "")
        {
            if (!IsRunning) return;

            IsRunning = false;
            OnChallengeFailed?.Invoke();
            Debug.Log($"[DailyChallenge] 도전 실패: {reason}");
        }

        // ── Grade Calculation ─────────────────────────────────

        /// <summary>
        /// 클리어 시간과 받은 데미지를 기반으로 등급을 산정한다.
        /// </summary>
        private ChallengeGrade CalculateGrade(float clearTime, int damageTaken)
        {
            // S등급: 시간 제한 내 + 무피격
            if (clearTime <= sGradeTimeLimit && damageTaken <= sGradeMaxDamage)
                return ChallengeGrade.S;

            // A등급: 시간 제한 내 + 소피격
            if (clearTime <= aGradeTimeLimit && damageTaken <= aGradeMaxDamage)
                return ChallengeGrade.A;

            // B등급: 적당한 시간
            if (clearTime <= bGradeTimeLimit)
                return ChallengeGrade.B;

            // C등급: 클리어만 함
            return ChallengeGrade.C;
        }

        /// <summary>
        /// 등급에 맞는 보상을 반환한다.
        /// </summary>
        private ChallengeReward GetRewardForGrade(ChallengeGrade grade)
        {
            if (gradeRewards != null)
            {
                foreach (var reward in gradeRewards)
                {
                    if (reward.grade == grade)
                        return reward;
                }
            }

            // 기본 보상
            return new ChallengeReward
            {
                grade = grade,
                goldReward = GetDefaultGoldReward(grade),
                specialMaterialCount = grade == ChallengeGrade.S ? 1 : 0,
                specialMaterialId = "mat_challenge_special"
            };
        }

        private int GetDefaultGoldReward(ChallengeGrade grade)
        {
            return grade switch
            {
                ChallengeGrade.S => 5000,
                ChallengeGrade.A => 3000,
                ChallengeGrade.B => 1500,
                ChallengeGrade.C => 500,
                _ => 500
            };
        }

        private void ApplyReward(ChallengeReward reward)
        {
            if (reward == null) return;

            // 골드 지급
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                var stats = player.GetComponent<Player.PlayerStats>();
                if (stats != null)
                    stats.Gold += reward.goldReward;
            }

            Debug.Log($"[DailyChallenge] 보상 지급 - 골드: {reward.goldReward}, " +
                      $"특별 재료: {reward.specialMaterialCount}개");
        }

        // ── Modifier Queries ──────────────────────────────────

        /// <summary>
        /// 현재 도전에 특정 조건이 포함되어 있는지 확인한다.
        /// </summary>
        public bool HasModifier(ChallengeModifierType type)
        {
            if (TodayConfig.Modifiers == null) return false;
            foreach (var mod in TodayConfig.Modifiers)
            {
                if (mod.type == type) return true;
            }
            return false;
        }

        /// <summary>
        /// 특정 조건의 상세 정보를 반환한다.
        /// </summary>
        public ChallengeModifier GetModifier(ChallengeModifierType type)
        {
            if (TodayConfig.Modifiers == null) return null;
            foreach (var mod in TodayConfig.Modifiers)
            {
                if (mod.type == type) return mod;
            }
            return null;
        }

        /// <summary>
        /// 적 HP 배율을 반환한다 (일일 도전 조건 반영).
        /// </summary>
        public float GetEnemyHpMultiplier()
        {
            if (!IsRunning) return 1f;
            return HasModifier(ChallengeModifierType.EnemyHpDouble) ? 2f : 1f;
        }

        /// <summary>
        /// 적 공격력 배율을 반환한다 (일일 도전 조건 반영).
        /// </summary>
        public float GetEnemyAtkMultiplier()
        {
            if (!IsRunning) return 1f;
            return HasModifier(ChallengeModifierType.EnemyAtkUp) ? 1.5f : 1f;
        }

        /// <summary>
        /// 대시 쿨다운 배율을 반환한다.
        /// </summary>
        public float GetDashCooldownMultiplier()
        {
            if (!IsRunning) return 1f;
            return HasModifier(ChallengeModifierType.DashCooldownDouble) ? 2f : 1f;
        }

        /// <summary>
        /// 사용 가능한 스킬 슬롯 수를 반환한다.
        /// </summary>
        public int GetAvailableSkillSlots()
        {
            if (!IsRunning) return 4;
            return HasModifier(ChallengeModifierType.SkillLimitTwo) ? 2 : 4;
        }

        /// <summary>
        /// 마나 자연 회복이 가능한지 반환한다.
        /// </summary>
        public bool IsManaRegenAllowed()
        {
            if (!IsRunning) return true;
            return !HasModifier(ChallengeModifierType.NoManaRegen);
        }

        /// <summary>
        /// 특정 데미지 타입이 유효한지 반환한다.
        /// </summary>
        public bool IsDamageTypeEffective(DamageType type)
        {
            if (!IsRunning) return true;
            if (!HasModifier(ChallengeModifierType.FireOnlyEffective)) return true;
            return type == DamageType.Fire;
        }

        // ── Event Handlers ────────────────────────────────────

        private void OnDamageReceived(DamageEvent evt)
        {
            if (!IsRunning) return;

            // 플레이어가 피격당한 경우만 추적
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null || evt.Target != player) return;

            damageTakenDuringChallenge += evt.Damage;

            // 피격 금지 조건 체크
            if (HasModifier(ChallengeModifierType.NoDamageTaken))
            {
                FailChallenge("피격 금지 조건 위반!");
            }
        }

        private void OnSkillUsed(SkillUsedEvent evt)
        {
            if (!IsRunning) return;
            skillsUsedCount++;
            skillsUsed.Add(evt.SkillId);
        }

        private void OnEnemyDeathInChallenge(EnemyDeathEvent evt)
        {
            // 도전 중 적 처치 추적 (외부 시스템에서 전체 클리어 판정)
        }

        // ── Consecutive Days ──────────────────────────────────

        private void UpdateConsecutiveDays()
        {
            string today = DateTime.Today.ToString("yyyy-MM-dd");
            string yesterday = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd");

            if (lastConsecutiveDate == yesterday)
            {
                ConsecutiveDays++;
            }
            else if (lastConsecutiveDate != today)
            {
                ConsecutiveDays = 1;
            }

            lastConsecutiveDate = today;
        }

        private void CheckTodayCompletion()
        {
            string today = DateTime.Today.ToString("yyyy-MM-dd");
            HasCompletedToday = (lastCompletedDate == today);
        }

        // ── Save / Load ──────────────────────────────────────

        public DailyChallengeSaveData ToSaveData()
        {
            return new DailyChallengeSaveData
            {
                lastCompletedDate = lastCompletedDate ?? "",
                totalCompletions = TotalCompletions,
                consecutiveDays = ConsecutiveDays,
                lastConsecutiveDate = lastConsecutiveDate ?? "",
                bestGradeInt = (int)BestGrade
            };
        }

        public void LoadFromSave(DailyChallengeSaveData data)
        {
            if (data == null) return;
            lastCompletedDate = data.lastCompletedDate;
            TotalCompletions = data.totalCompletions;
            ConsecutiveDays = data.consecutiveDays;
            lastConsecutiveDate = data.lastConsecutiveDate;
            BestGrade = (ChallengeGrade)data.bestGradeInt;
            CheckTodayCompletion();
        }

        private void SaveProgress()
        {
            string json = JsonUtility.ToJson(ToSaveData());
            PlayerPrefs.SetString("DailyChallenge_Save", json);
            PlayerPrefs.Save();
        }

        private void LoadProgress()
        {
            string json = PlayerPrefs.GetString("DailyChallenge_Save", "");
            if (!string.IsNullOrEmpty(json))
            {
                var data = JsonUtility.FromJson<DailyChallengeSaveData>(json);
                LoadFromSave(data);
            }
        }
    }
}
