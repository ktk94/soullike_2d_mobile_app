using System;
using System.Collections.Generic;
using UnityEngine;
using SoulCraft.Core;
using SoulCraft.Enemy;
using SoulCraft.Player;
using SoulCraft.Combat;
using SoulCraft.World;

namespace SoulCraft.Endgame
{
    // ── Data Definitions ──────────────────────────────────────

    /// <summary>
    /// 회차별 추가 적 패턴 정보.
    /// </summary>
    [Serializable]
    public class NgPlusEnemyPattern
    {
        public string enemyId;
        public int requiredCycle;
        public string patternName;
        [TextArea(1, 3)]
        public string patternDescription;
    }

    /// <summary>
    /// 회차별 보스 추가 페이즈 정보.
    /// </summary>
    [Serializable]
    public class NgPlusBossPhase
    {
        public string bossId;
        public int requiredCycle;
        [Range(0f, 1f)]
        public float phaseHpThreshold;
        public string phaseDescription;
    }

    /// <summary>
    /// 회차 전용 장비/스킬 해금 정보.
    /// </summary>
    [Serializable]
    public class NgPlusUnlock
    {
        public int requiredCycle;
        public string unlockType; // "equipment" or "skill"
        public string itemId;
        public string displayName;
        [TextArea(1, 3)]
        public string description;
    }

    /// <summary>
    /// 회차별 드롭률 보정 정보.
    /// </summary>
    [Serializable]
    public class NgPlusDropRate
    {
        public int cycle;
        public float commonDropRate;
        public float uncommonDropRate;
        public float rareDropRate;
        public float epicDropRate;
        public float legendaryDropRate;
    }

    [Serializable]
    public class NewGamePlusSaveData
    {
        public int currentCycle;
        public int highestCycle;
        public bool isNgPlusActive;
        public List<string> unlockedNgPlusItems = new();
    }

    // ── Events ────────────────────────────────────────────────

    public struct NewGamePlusStartedEvent
    {
        public int Cycle;
    }

    public struct NewGamePlusCycleCompleteEvent
    {
        public int CompletedCycle;
    }

    /// <summary>
    /// 회차(New Game+) 시스템.
    /// 메인 스토리 클리어 후 2회차 이상의 고난이도 플레이를 제공한다.
    /// 최대 3회차까지 지원.
    /// </summary>
    public class NewGamePlus : MonoBehaviour
    {
        public static NewGamePlus Instance { get; private set; }

        // ── Inspector ─────────────────────────────────────────

        [Header("Cycle Settings")]
        [SerializeField] private int maxCycle = 3;
        [SerializeField] private float statScalePerCycle = 0.5f;

        [Header("Drop Rate by Cycle")]
        [SerializeField] private NgPlusDropRate[] dropRatesByCycle;

        [Header("NG+ Enemy Patterns")]
        [SerializeField] private NgPlusEnemyPattern[] additionalPatterns;

        [Header("NG+ Boss Phases")]
        [SerializeField] private NgPlusBossPhase[] additionalBossPhases;

        [Header("NG+ Exclusive Unlocks")]
        [SerializeField] private NgPlusUnlock[] exclusiveUnlocks;

        // ── Runtime ───────────────────────────────────────────

        /// <summary>현재 회차 (1 = 일반, 2 = 2회차, 3 = 3회차)</summary>
        public int CurrentCycle { get; private set; } = 1;

        /// <summary>도달한 최고 회차</summary>
        public int HighestCycle { get; private set; } = 1;

        /// <summary>현재 NG+ 모드 활성 여부</summary>
        public bool IsNgPlusActive { get; private set; }

        /// <summary>현재 회차의 적 스탯 배율</summary>
        public float CurrentStatMultiplier => 1f + (CurrentCycle - 1) * statScalePerCycle;

        /// <summary>NG+ 시작 가능 여부</summary>
        public bool CanStartNewGamePlus
        {
            get
            {
                if (CurrentCycle >= maxCycle) return false;
                if (SaveManager.Instance == null) return false;
                var save = SaveManager.Instance.Load();
                // 메인 스토리 최종 스테이지 클리어 확인
                return save.highestStageCleared >= 4; // 스테이지 0~4 (5개)
            }
        }

        public event Action<int> OnNewCycleStarted;
        public event Action<int> OnCycleCompleted;

        private readonly HashSet<string> unlockedNgPlusItems = new();

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
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Public API ────────────────────────────────────────

        /// <summary>
        /// 새로운 회차를 시작한다. 기존 진행도 중 일부를 유지한다.
        /// </summary>
        public bool StartNewGamePlus()
        {
            if (!CanStartNewGamePlus)
            {
                Debug.LogWarning("[NewGamePlus] 회차 시작 불가: 조건 미충족.");
                return false;
            }

            int nextCycle = CurrentCycle + 1;
            if (nextCycle > maxCycle)
            {
                Debug.LogWarning($"[NewGamePlus] 최대 회차({maxCycle})에 도달했습니다.");
                return false;
            }

            CurrentCycle = nextCycle;
            IsNgPlusActive = true;

            if (nextCycle > HighestCycle)
                HighestCycle = nextCycle;

            // 회차 전환 처리
            ApplyCycleTransition();

            // 회차 전용 콘텐츠 해금
            UnlockCycleContent(nextCycle);

            SaveProgress();

            GameEventSystem.Publish(new NewGamePlusStartedEvent { Cycle = nextCycle });
            OnNewCycleStarted?.Invoke(nextCycle);

            Debug.Log($"[NewGamePlus] {nextCycle}회차 시작! " +
                      $"적 스탯 배율: {CurrentStatMultiplier:F1}x");

            return true;
        }

        /// <summary>
        /// 현재 회차의 메인 스토리를 클리어했을 때 호출.
        /// </summary>
        public void OnMainStoryClear()
        {
            GameEventSystem.Publish(new NewGamePlusCycleCompleteEvent
            {
                CompletedCycle = CurrentCycle
            });

            OnCycleCompleted?.Invoke(CurrentCycle);

            Debug.Log($"[NewGamePlus] {CurrentCycle}회차 클리어!");
            SaveProgress();
        }

        // ── Stat Scaling ──────────────────────────────────────

        /// <summary>
        /// 현재 회차에 맞게 적 HP를 스케일링한 값을 반환한다.
        /// </summary>
        public int GetScaledEnemyHp(int baseHp)
        {
            if (!IsNgPlusActive) return baseHp;
            return Mathf.RoundToInt(baseHp * CurrentStatMultiplier);
        }

        /// <summary>
        /// 현재 회차에 맞게 적 공격력을 스케일링한 값을 반환한다.
        /// </summary>
        public int GetScaledEnemyAttack(int baseAttack)
        {
            if (!IsNgPlusActive) return baseAttack;
            return Mathf.RoundToInt(baseAttack * CurrentStatMultiplier);
        }

        /// <summary>
        /// 현재 회차에 맞게 적 방어력을 스케일링한 값을 반환한다.
        /// </summary>
        public int GetScaledEnemyDefense(int baseDefense)
        {
            if (!IsNgPlusActive) return baseDefense;
            return Mathf.RoundToInt(baseDefense * (1f + (CurrentCycle - 1) * 0.3f));
        }

        /// <summary>
        /// 현재 회차에 맞게 적 이동속도를 스케일링한 값을 반환한다.
        /// </summary>
        public float GetScaledEnemySpeed(float baseSpeed)
        {
            if (!IsNgPlusActive) return baseSpeed;
            return baseSpeed * (1f + (CurrentCycle - 1) * 0.15f);
        }

        // ── Pattern / Phase Queries ───────────────────────────

        /// <summary>
        /// 특정 적의 현재 회차에서 사용 가능한 추가 패턴 목록을 반환한다.
        /// </summary>
        public List<NgPlusEnemyPattern> GetAdditionalPatterns(string enemyId)
        {
            var result = new List<NgPlusEnemyPattern>();
            if (additionalPatterns == null || !IsNgPlusActive) return result;

            foreach (var pattern in additionalPatterns)
            {
                if (pattern.enemyId == enemyId && CurrentCycle >= pattern.requiredCycle)
                    result.Add(pattern);
            }
            return result;
        }

        /// <summary>
        /// 특정 보스의 현재 회차에서 사용 가능한 추가 페이즈 목록을 반환한다.
        /// </summary>
        public List<NgPlusBossPhase> GetAdditionalBossPhases(string bossId)
        {
            var result = new List<NgPlusBossPhase>();
            if (additionalBossPhases == null || !IsNgPlusActive) return result;

            foreach (var phase in additionalBossPhases)
            {
                if (phase.bossId == bossId && CurrentCycle >= phase.requiredCycle)
                    result.Add(phase);
            }
            return result;
        }

        /// <summary>
        /// 적이 NG+ 추가 패턴을 가지고 있는지 확인한다.
        /// </summary>
        public bool HasAdditionalPatterns(string enemyId)
        {
            return GetAdditionalPatterns(enemyId).Count > 0;
        }

        /// <summary>
        /// 보스가 NG+ 추가 페이즈를 가지고 있는지 확인한다.
        /// </summary>
        public bool HasAdditionalBossPhases(string bossId)
        {
            return GetAdditionalBossPhases(bossId).Count > 0;
        }

        // ── Drop Rate ─────────────────────────────────────────

        /// <summary>
        /// 현재 회차의 드롭률 정보를 반환한다.
        /// 회차가 높을수록 레어리티 높은 아이템 확률이 증가한다.
        /// </summary>
        public NgPlusDropRate GetCurrentDropRate()
        {
            if (dropRatesByCycle != null)
            {
                foreach (var rate in dropRatesByCycle)
                {
                    if (rate.cycle == CurrentCycle)
                        return rate;
                }
            }

            // 기본 드롭률 (회차 기반 자동 계산)
            float cycleBonus = (CurrentCycle - 1) * 0.1f;
            return new NgPlusDropRate
            {
                cycle = CurrentCycle,
                commonDropRate = Mathf.Max(0.3f - cycleBonus, 0.1f),
                uncommonDropRate = 0.30f,
                rareDropRate = 0.20f + cycleBonus * 0.5f,
                epicDropRate = 0.12f + cycleBonus,
                legendaryDropRate = 0.03f + cycleBonus * 0.5f
            };
        }

        /// <summary>
        /// 레어리티별 드롭 가중치를 반환한다 (LootTable 연동용).
        /// </summary>
        public float GetRarityDropWeight(Farming.Rarity rarity)
        {
            var rate = GetCurrentDropRate();
            return rarity switch
            {
                Farming.Rarity.Common => rate.commonDropRate,
                Farming.Rarity.Uncommon => rate.uncommonDropRate,
                Farming.Rarity.Rare => rate.rareDropRate,
                Farming.Rarity.Epic => rate.epicDropRate,
                Farming.Rarity.Legendary => rate.legendaryDropRate,
                _ => rate.commonDropRate
            };
        }

        // ── Exclusive Content ─────────────────────────────────

        /// <summary>
        /// 현재 회차에서 해금된 전용 콘텐츠 목록을 반환한다.
        /// </summary>
        public List<NgPlusUnlock> GetUnlockedContent()
        {
            var result = new List<NgPlusUnlock>();
            if (exclusiveUnlocks == null) return result;

            foreach (var unlock in exclusiveUnlocks)
            {
                if (CurrentCycle >= unlock.requiredCycle)
                    result.Add(unlock);
            }
            return result;
        }

        /// <summary>
        /// 특정 아이템이 NG+ 전용인지 확인한다.
        /// </summary>
        public bool IsNgPlusExclusive(string itemId)
        {
            if (exclusiveUnlocks == null) return false;

            foreach (var unlock in exclusiveUnlocks)
            {
                if (unlock.itemId == itemId)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// NG+ 전용 아이템이 현재 사용 가능한지 확인한다.
        /// </summary>
        public bool IsNgPlusItemAvailable(string itemId)
        {
            return unlockedNgPlusItems.Contains(itemId);
        }

        // ── Cycle Transition ──────────────────────────────────

        /// <summary>
        /// 회차 전환 시 데이터를 처리한다.
        /// - 레벨, 장비, 스킬은 유지
        /// - 스테이지 진행도는 초기화
        /// - NG+ 표식 적용
        /// </summary>
        private void ApplyCycleTransition()
        {
            if (SaveManager.Instance == null) return;

            var save = SaveManager.Instance.Load();

            // 스테이지 진행도 초기화 (레벨/장비/스킬은 유지)
            save.highestStageCleared = 0;

            // 골드 보너스 (회차 보상)
            int goldBonus = 1000 * CurrentCycle;
            save.gold += goldBonus;

            SaveManager.Instance.Save(save);

            Debug.Log($"[NewGamePlus] 회차 전환 완료 - " +
                      $"스테이지 초기화, 골드 보너스: +{goldBonus}");
        }

        /// <summary>
        /// 회차에 따른 전용 콘텐츠를 해금한다.
        /// </summary>
        private void UnlockCycleContent(int cycle)
        {
            if (exclusiveUnlocks == null) return;

            foreach (var unlock in exclusiveUnlocks)
            {
                if (unlock.requiredCycle <= cycle && !unlockedNgPlusItems.Contains(unlock.itemId))
                {
                    unlockedNgPlusItems.Add(unlock.itemId);
                    Debug.Log($"[NewGamePlus] NG+ 전용 해금: {unlock.displayName} ({unlock.unlockType})");
                }
            }
        }

        // ── Difficulty Description ────────────────────────────

        /// <summary>
        /// 현재 회차의 난이도 설명 텍스트를 반환한다.
        /// </summary>
        public string GetCycleDifficultyText()
        {
            return CurrentCycle switch
            {
                1 => "일반 난이도",
                2 => "2회차: 적 스탯 1.5배, 새로운 패턴 추가, 에픽+ 드롭 증가",
                3 => "3회차: 적 스탯 2배, 보스 추가 페이즈, 레전더리 드롭 대폭 증가",
                _ => $"{CurrentCycle}회차: 적 스탯 {CurrentStatMultiplier:F1}배"
            };
        }

        /// <summary>
        /// 회차 표기 텍스트를 반환한다.
        /// </summary>
        public string GetCycleDisplayText()
        {
            if (CurrentCycle <= 1) return "";
            return $"NG+{CurrentCycle - 1}";
        }

        /// <summary>
        /// 특정 회차의 주요 변경 사항 목록을 반환한다.
        /// </summary>
        public List<string> GetCycleChangelog(int cycle)
        {
            var changes = new List<string>();

            if (cycle >= 2)
            {
                changes.Add($"적 HP/ATK: 기본값 x{1f + (cycle - 1) * statScalePerCycle:F1}");
                changes.Add("적에게 새로운 공격 패턴 추가");
                changes.Add("높은 레어리티 드롭률 증가");
            }

            if (cycle >= 2)
            {
                changes.Add("2회차 전용 장비 해금");
                changes.Add("2회차 전용 스킬 해금");
            }

            if (cycle >= 3)
            {
                changes.Add("보스에 추가 페이즈 등장");
                changes.Add("3회차 전용 최강 장비 해금");
                changes.Add("레전더리 드롭률 대폭 증가");
            }

            return changes;
        }

        // ── Save / Load ──────────────────────────────────────

        public NewGamePlusSaveData ToSaveData()
        {
            return new NewGamePlusSaveData
            {
                currentCycle = CurrentCycle,
                highestCycle = HighestCycle,
                isNgPlusActive = IsNgPlusActive,
                unlockedNgPlusItems = new List<string>(unlockedNgPlusItems)
            };
        }

        public void LoadFromSave(NewGamePlusSaveData data)
        {
            if (data == null) return;
            CurrentCycle = Mathf.Max(1, data.currentCycle);
            HighestCycle = Mathf.Max(CurrentCycle, data.highestCycle);
            IsNgPlusActive = data.isNgPlusActive;

            unlockedNgPlusItems.Clear();
            if (data.unlockedNgPlusItems != null)
            {
                foreach (var id in data.unlockedNgPlusItems)
                    unlockedNgPlusItems.Add(id);
            }
        }

        private void SaveProgress()
        {
            string json = JsonUtility.ToJson(ToSaveData());
            PlayerPrefs.SetString("NewGamePlus_Save", json);
            PlayerPrefs.Save();
        }

        private void LoadProgress()
        {
            string json = PlayerPrefs.GetString("NewGamePlus_Save", "");
            if (!string.IsNullOrEmpty(json))
            {
                var data = JsonUtility.FromJson<NewGamePlusSaveData>(json);
                LoadFromSave(data);
            }
        }
    }
}
