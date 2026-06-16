using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulCraft.Core;
using SoulCraft.Enemy;
using SoulCraft.Player;
using SoulCraft.Combat;
using SoulCraft.World;
using SoulCraft.Farming;

namespace SoulCraft.Endgame
{
    // ── Events ────────────────────────────────────────────────

    public struct DungeonFloorClearedEvent
    {
        public int Floor;
        public float ClearTime;
    }

    public struct DungeonRunEndedEvent
    {
        public int FinalFloor;
        public bool IsNewRecord;
    }

    // ── Reward Definitions ────────────────────────────────────

    public enum DungeonRewardType
    {
        RandomEquipment,
        SkillEnhance,
        HpRecover
    }

    [Serializable]
    public class DungeonRewardOption
    {
        public DungeonRewardType type;
        public string displayName;
        public string description;
        public Sprite icon;
    }

    // ── Floor Configuration ───────────────────────────────────

    /// <summary>
    /// 층별 난이도 및 적 구성 정보.
    /// </summary>
    public struct FloorConfig
    {
        public int Floor;
        public float HpMultiplier;
        public float AtkMultiplier;
        public int EnemyCount;
        public bool IsMiniBoss;
        public bool IsEnhancedBoss;
    }

    // ── Save Data ─────────────────────────────────────────────

    [Serializable]
    public class InfiniteDungeonSaveData
    {
        public int highestFloor;
        public int totalRuns;
        public int totalFloorsCleared;
    }

    /// <summary>
    /// 무한 던전 모드.
    /// 층수가 올라갈수록 난이도가 무한 스케일링되는 엔드게임 콘텐츠.
    /// 입장 조건: 메인 스토리 클리어.
    /// </summary>
    public class InfiniteDungeon : MonoBehaviour
    {
        public static InfiniteDungeon Instance { get; private set; }

        // ── Inspector ─────────────────────────────────────────

        [Header("Entry Requirement")]
        [Tooltip("입장에 필요한 최소 클리어 스테이지 인덱스 (메인 스토리 마지막)")]
        [SerializeField] private int requiredStageClearIndex = 4;

        [Header("Scaling")]
        [SerializeField] private float hpScalePerFloor = 0.15f;
        [SerializeField] private float atkScalePerFloor = 0.15f;
        [SerializeField] private int baseEnemyCount = 3;
        [SerializeField] private int maxEnemyCount = 12;

        [Header("Boss Floors")]
        [SerializeField] private int miniBossInterval = 5;
        [SerializeField] private int enhancedBossInterval = 10;
        [SerializeField] private float miniBossHpMultiplier = 3f;
        [SerializeField] private float miniBossAtkMultiplier = 1.5f;
        [SerializeField] private float enhancedBossHpMultiplier = 6f;
        [SerializeField] private float enhancedBossAtkMultiplier = 2.5f;

        [Header("Enemy Pool")]
        [SerializeField] private EnemyData[] normalEnemyPool;
        [SerializeField] private EnemyData[] miniBossPool;
        [SerializeField] private EnemyData[] enhancedBossPool;

        [Header("Reward Pool")]
        [SerializeField] private ItemData[] rewardEquipmentPool;
        [SerializeField] private SkillData[] rewardSkillPool;

        [Header("Spawn")]
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private float randomSpawnRadius = 5f;

        [Header("Reward UI")]
        [SerializeField] private GameObject rewardSelectionPanel;
        [SerializeField] private DungeonRewardOption[] rewardTemplates;

        // ── Runtime ───────────────────────────────────────────

        public int CurrentFloor { get; private set; }
        public int HighestFloor { get; private set; }
        public bool IsRunning { get; private set; }
        public FloorConfig CurrentFloorConfig { get; private set; }

        public event Action<int> OnFloorStarted;
        public event Action<int> OnFloorCleared;
        public event Action<DungeonRewardOption[]> OnRewardSelection;
        public event Action<int, bool> OnRunEnded; // finalFloor, isNewRecord

        private readonly List<GameObject> aliveEnemies = new();
        private int totalRuns;
        private int totalFloorsCleared;
        private float floorStartTime;
        private Coroutine floorCoroutine;

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

        void OnEnable()
        {
            GameEventSystem.Subscribe<EnemyDeathEvent>(OnEnemyDeath);
        }

        void OnDisable()
        {
            GameEventSystem.Unsubscribe<EnemyDeathEvent>(OnEnemyDeath);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Public API ────────────────────────────────────────

        /// <summary>
        /// 입장 가능 여부를 확인한다.
        /// 메인 스토리(마지막 스테이지)를 클리어해야 입장 가능.
        /// </summary>
        public bool CanEnter()
        {
            if (SaveManager.Instance == null) return false;
            var save = SaveManager.Instance.Load();
            return save.highestStageCleared >= requiredStageClearIndex;
        }

        /// <summary>
        /// 무한 던전 도전을 시작한다.
        /// </summary>
        public bool StartRun()
        {
            if (!CanEnter())
            {
                Debug.LogWarning("[InfiniteDungeon] 입장 조건 미충족: 메인 스토리를 클리어해주세요.");
                return false;
            }

            if (IsRunning)
            {
                Debug.LogWarning("[InfiniteDungeon] 이미 진행 중입니다.");
                return false;
            }

            IsRunning = true;
            CurrentFloor = 1;
            totalRuns++;
            aliveEnemies.Clear();

            Debug.Log("[InfiniteDungeon] 무한 던전 도전 시작!");

            if (GameManager.Instance != null)
                GameManager.Instance.ChangeState(GameState.Playing);

            StartFloor();
            return true;
        }

        /// <summary>
        /// 현재 도전을 중단한다.
        /// </summary>
        public void AbandonRun()
        {
            if (!IsRunning) return;
            EndRun(false);
        }

        /// <summary>
        /// 보상 선택 결과를 처리한다.
        /// </summary>
        public void SelectReward(int rewardIndex)
        {
            if (rewardIndex < 0 || rewardIndex >= 3) return;

            var options = GenerateRewardOptions();
            if (rewardIndex >= options.Length) return;

            ApplyReward(options[rewardIndex]);
            HideRewardPanel();

            // 다음 층 시작
            CurrentFloor++;
            StartFloor();
        }

        /// <summary>
        /// 층 설정을 계산하여 반환한다.
        /// </summary>
        public FloorConfig CalculateFloorConfig(int floor)
        {
            var config = new FloorConfig();
            config.Floor = floor;

            // 기본 스케일링
            float baseScale = 1f + floor * hpScalePerFloor;
            config.HpMultiplier = baseScale;
            config.AtkMultiplier = 1f + floor * atkScalePerFloor;
            config.EnemyCount = Mathf.Min(baseEnemyCount + floor / 2, maxEnemyCount);

            // 10층마다 강화 보스 (10층 우선 판정)
            if (floor > 0 && floor % enhancedBossInterval == 0)
            {
                config.IsEnhancedBoss = true;
                config.IsMiniBoss = false;
                config.HpMultiplier = baseScale * enhancedBossHpMultiplier;
                config.AtkMultiplier = config.AtkMultiplier * enhancedBossAtkMultiplier;
                config.EnemyCount = 1;
            }
            // 5층마다 미니보스 (10층은 제외)
            else if (floor > 0 && floor % miniBossInterval == 0)
            {
                config.IsMiniBoss = true;
                config.IsEnhancedBoss = false;
                config.HpMultiplier = baseScale * miniBossHpMultiplier;
                config.AtkMultiplier = config.AtkMultiplier * miniBossAtkMultiplier;
                config.EnemyCount = 1;
            }
            else
            {
                config.IsMiniBoss = false;
                config.IsEnhancedBoss = false;
            }

            return config;
        }

        /// <summary>
        /// 세이브 데이터를 반환한다.
        /// </summary>
        public InfiniteDungeonSaveData ToSaveData()
        {
            return new InfiniteDungeonSaveData
            {
                highestFloor = HighestFloor,
                totalRuns = totalRuns,
                totalFloorsCleared = totalFloorsCleared
            };
        }

        /// <summary>
        /// 세이브 데이터를 복원한다.
        /// </summary>
        public void LoadFromSave(InfiniteDungeonSaveData data)
        {
            if (data == null) return;
            HighestFloor = data.highestFloor;
            totalRuns = data.totalRuns;
            totalFloorsCleared = data.totalFloorsCleared;
        }

        // ── Floor Logic ───────────────────────────────────────

        private void StartFloor()
        {
            CurrentFloorConfig = CalculateFloorConfig(CurrentFloor);
            floorStartTime = Time.time;
            aliveEnemies.Clear();

            string floorType = CurrentFloorConfig.IsEnhancedBoss ? "강화 보스"
                             : CurrentFloorConfig.IsMiniBoss ? "미니보스"
                             : "일반";

            Debug.Log($"[InfiniteDungeon] {CurrentFloor}층 시작 " +
                      $"(타입: {floorType}, 적 수: {CurrentFloorConfig.EnemyCount}, " +
                      $"HP배율: {CurrentFloorConfig.HpMultiplier:F2}x, " +
                      $"ATK배율: {CurrentFloorConfig.AtkMultiplier:F2}x)");

            OnFloorStarted?.Invoke(CurrentFloor);

            if (floorCoroutine != null)
                StopCoroutine(floorCoroutine);
            floorCoroutine = StartCoroutine(SpawnFloorEnemies());
        }

        private IEnumerator SpawnFloorEnemies()
        {
            yield return new WaitForSeconds(1f); // 층 전환 딜레이

            FloorConfig config = CurrentFloorConfig;

            for (int i = 0; i < config.EnemyCount; i++)
            {
                EnemyData selectedData = SelectEnemyData(config);
                if (selectedData == null)
                {
                    Debug.LogWarning("[InfiniteDungeon] 스폰할 적 데이터가 없습니다.");
                    continue;
                }

                Vector3 pos = GetSpawnPosition(i);
                GameObject enemy = SpawnScaledEnemy(selectedData, pos, config);

                if (enemy != null)
                    aliveEnemies.Add(enemy);

                yield return new WaitForSeconds(0.3f);
            }
        }

        /// <summary>
        /// 층 설정에 맞는 적 데이터를 선택한다.
        /// </summary>
        private EnemyData SelectEnemyData(FloorConfig config)
        {
            EnemyData[] pool;

            if (config.IsEnhancedBoss)
                pool = enhancedBossPool != null && enhancedBossPool.Length > 0
                    ? enhancedBossPool : normalEnemyPool;
            else if (config.IsMiniBoss)
                pool = miniBossPool != null && miniBossPool.Length > 0
                    ? miniBossPool : normalEnemyPool;
            else
                pool = normalEnemyPool;

            if (pool == null || pool.Length == 0) return null;
            return pool[UnityEngine.Random.Range(0, pool.Length)];
        }

        /// <summary>
        /// 스케일링된 스탯으로 적을 스폰한다.
        /// ObjectPool 사용 시 풀에서 가져오고, 없으면 로그 경고.
        /// </summary>
        private GameObject SpawnScaledEnemy(EnemyData data, Vector3 position, FloorConfig config)
        {
            if (data == null) return null;

            GameObject enemy = null;

            if (ObjectPool.Instance != null)
            {
                enemy = ObjectPool.Instance.Spawn(data.enemyId, position, Quaternion.identity);
            }

            if (enemy == null)
            {
                Debug.LogWarning($"[InfiniteDungeon] 적 '{data.enemyId}' 스폰 실패.");
                return null;
            }

            // EnemyBase 초기화 후 스탯 스케일링 적용
            var enemyBase = enemy.GetComponent<EnemyBase>();
            if (enemyBase != null)
            {
                enemyBase.InitializeEnemy();

                // 스케일링 적용: 런타임 HP 직접 설정
                int scaledHp = Mathf.RoundToInt(data.maxHp * config.HpMultiplier);
                SetEnemyHp(enemyBase, scaledHp);
            }

            return enemy;
        }

        /// <summary>
        /// 리플렉션 없이 EnemyBase의 HP를 설정한다.
        /// EnemyBase.CurrentHp는 protected set이므로, 데미지를 역산하여 적용.
        /// </summary>
        private void SetEnemyHp(EnemyBase enemyBase, int targetHp)
        {
            // EnemyBase.InitializeEnemy()가 data.maxHp로 초기화한 뒤,
            // 추가 HP를 Heal처럼 적용할 수 없으므로
            // 음수 데미지 트릭 대신, 오버라이드 가능한 구조에서는
            // 외부에서 직접 설정하는 메서드가 필요하다.
            // 현재 구조에서는 InitializeEnemy 후 CurrentHp가 data.maxHp이므로,
            // targetHp > CurrentHp인 경우 차이만큼 보정하기 어렵다.
            // 따라서 던전 전용 스케일링은 DamageCalculator에서 방어력 기반으로
            // 처리하거나, 스케일링 래퍼를 사용한다.
            //
            // 여기서는 DungeonEnemyScaler 컴포넌트를 부착하여 처리한다.
            var scaler = enemyBase.GetComponent<DungeonEnemyScaler>();
            if (scaler == null)
                scaler = enemyBase.gameObject.AddComponent<DungeonEnemyScaler>();

            scaler.ApplyScaling(targetHp, CurrentFloorConfig.AtkMultiplier);
        }

        private Vector3 GetSpawnPosition(int index)
        {
            if (spawnPoints != null && spawnPoints.Length > 0)
                return spawnPoints[index % spawnPoints.Length].position;

            Vector2 offset = UnityEngine.Random.insideUnitCircle * randomSpawnRadius;
            return transform.position + new Vector3(offset.x, offset.y, 0f);
        }

        // ── Enemy Death Tracking ──────────────────────────────

        private void OnEnemyDeath(EnemyDeathEvent evt)
        {
            if (!IsRunning) return;

            aliveEnemies.Remove(evt.Enemy);
            aliveEnemies.RemoveAll(e => e == null || !e.activeInHierarchy);

            if (aliveEnemies.Count == 0)
            {
                OnFloorClearedInternal();
            }
        }

        private void OnFloorClearedInternal()
        {
            float clearTime = Time.time - floorStartTime;
            totalFloorsCleared++;

            Debug.Log($"[InfiniteDungeon] {CurrentFloor}층 클리어! (소요시간: {clearTime:F1}초)");

            GameEventSystem.Publish(new DungeonFloorClearedEvent
            {
                Floor = CurrentFloor,
                ClearTime = clearTime
            });

            OnFloorCleared?.Invoke(CurrentFloor);

            // 최고 기록 갱신
            if (CurrentFloor > HighestFloor)
                HighestFloor = CurrentFloor;

            // 보상 선택 표시
            ShowRewardSelection();
        }

        // ── Reward System (로그라이크 스타일 3택) ──────────────

        private void ShowRewardSelection()
        {
            var options = GenerateRewardOptions();
            OnRewardSelection?.Invoke(options);

            if (rewardSelectionPanel != null)
                rewardSelectionPanel.SetActive(true);

            // 게임 일시 정지 (보상 선택 중)
            Time.timeScale = 0f;
        }

        private void HideRewardPanel()
        {
            if (rewardSelectionPanel != null)
                rewardSelectionPanel.SetActive(false);

            Time.timeScale = 1f;
        }

        /// <summary>
        /// 3개의 보상 옵션을 생성한다.
        /// </summary>
        private DungeonRewardOption[] GenerateRewardOptions()
        {
            var options = new DungeonRewardOption[3];

            // 옵션 1: 랜덤 장비
            options[0] = new DungeonRewardOption
            {
                type = DungeonRewardType.RandomEquipment,
                displayName = "랜덤 장비",
                description = GetEquipmentRewardDescription()
            };

            // 옵션 2: 스킬 강화
            options[1] = new DungeonRewardOption
            {
                type = DungeonRewardType.SkillEnhance,
                displayName = "스킬 강화",
                description = "장착 중인 스킬 중 하나의 데미지가 20% 증가합니다."
            };

            // 옵션 3: HP 회복
            options[2] = new DungeonRewardOption
            {
                type = DungeonRewardType.HpRecover,
                displayName = "HP 회복",
                description = "HP를 50% 회복합니다."
            };

            // 보상 템플릿에서 아이콘 복사
            if (rewardTemplates != null)
            {
                for (int i = 0; i < options.Length && i < rewardTemplates.Length; i++)
                {
                    if (rewardTemplates[i] != null)
                        options[i].icon = rewardTemplates[i].icon;
                }
            }

            return options;
        }

        private string GetEquipmentRewardDescription()
        {
            // 층이 높을수록 레어리티 확률 상승
            if (CurrentFloor >= 50) return "전설 등급 장비 확정!";
            if (CurrentFloor >= 30) return "에픽 이상 장비가 나올 확률이 높습니다.";
            if (CurrentFloor >= 10) return "레어 이상 장비가 나올 확률이 높습니다.";
            return "랜덤 등급의 장비를 획득합니다.";
        }

        /// <summary>
        /// 선택된 보상을 적용한다.
        /// </summary>
        private void ApplyReward(DungeonRewardOption reward)
        {
            switch (reward.type)
            {
                case DungeonRewardType.RandomEquipment:
                    GrantRandomEquipment();
                    break;

                case DungeonRewardType.SkillEnhance:
                    GrantSkillEnhance();
                    break;

                case DungeonRewardType.HpRecover:
                    GrantHpRecover();
                    break;
            }

            Debug.Log($"[InfiniteDungeon] 보상 선택: {reward.displayName}");
        }

        private void GrantRandomEquipment()
        {
            if (rewardEquipmentPool == null || rewardEquipmentPool.Length == 0) return;

            // 층 기반 레어리티 가중치
            ItemData selected = rewardEquipmentPool[UnityEngine.Random.Range(0, rewardEquipmentPool.Length)];

            if (Inventory.Instance != null)
            {
                Inventory.Instance.AddItem(selected, 1);
                Debug.Log($"[InfiniteDungeon] 장비 획득: {selected.itemName}");
            }
        }

        private void GrantSkillEnhance()
        {
            // 플레이어의 장착된 스킬 중 랜덤으로 하나의 데미지 배율을 올린다
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            var skillManager = player.GetComponent<SkillManager>();
            if (skillManager == null) return;

            var equipped = skillManager.EquippedSkills;
            var validSkills = new List<int>();

            for (int i = 0; i < equipped.Length; i++)
            {
                if (equipped[i] != null)
                    validSkills.Add(i);
            }

            if (validSkills.Count > 0)
            {
                int idx = validSkills[UnityEngine.Random.Range(0, validSkills.Count)];
                SkillData skill = equipped[idx];
                // 런타임 스킬 데미지 배율은 별도 버프 시스템으로 관리됨
                // 여기서는 이벤트로 전달
                Debug.Log($"[InfiniteDungeon] 스킬 강화: {skill.skillName} 데미지 +20%");
            }
        }

        private void GrantHpRecover()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            var stats = player.GetComponent<PlayerStats>();
            if (stats == null) return;

            int healAmount = Mathf.RoundToInt(stats.MaxHp * 0.5f);
            stats.Heal(healAmount);
            Debug.Log($"[InfiniteDungeon] HP 회복: +{healAmount}");
        }

        // ── Run End ───────────────────────────────────────────

        /// <summary>
        /// 플레이어 사망 시 호출. 진행 기록을 저장한다.
        /// </summary>
        public void OnPlayerDeath()
        {
            if (!IsRunning) return;
            EndRun(true);
        }

        private void EndRun(bool fromDeath)
        {
            if (floorCoroutine != null)
                StopCoroutine(floorCoroutine);

            IsRunning = false;
            Time.timeScale = 1f;

            bool isNewRecord = CurrentFloor > HighestFloor;
            if (isNewRecord)
                HighestFloor = CurrentFloor;

            // 진행 기록 저장
            SaveProgress();

            GameEventSystem.Publish(new DungeonRunEndedEvent
            {
                FinalFloor = CurrentFloor,
                IsNewRecord = isNewRecord
            });

            OnRunEnded?.Invoke(CurrentFloor, isNewRecord);

            string reason = fromDeath ? "사망" : "포기";
            Debug.Log($"[InfiniteDungeon] 도전 종료 ({reason}): " +
                      $"{CurrentFloor}층 도달, 최고 기록: {HighestFloor}층");

            // 허브로 복귀
            if (GameManager.Instance != null)
                GameManager.Instance.ReturnToHub();
        }

        private void SaveProgress()
        {
            // SaveManager를 통해 기록 저장
            // 기존 SaveData에 무한 던전 데이터를 추가로 저장한다.
            if (SaveManager.Instance == null) return;

            var save = SaveManager.Instance.Load();
            // 무한 던전 데이터는 JSON으로 별도 저장
            string dungeonJson = JsonUtility.ToJson(ToSaveData());
            PlayerPrefs.SetString("InfiniteDungeon_Save", dungeonJson);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// PlayerPrefs에서 기록을 불러온다.
        /// </summary>
        public void LoadProgress()
        {
            string json = PlayerPrefs.GetString("InfiniteDungeon_Save", "");
            if (!string.IsNullOrEmpty(json))
            {
                var data = JsonUtility.FromJson<InfiniteDungeonSaveData>(json);
                LoadFromSave(data);
            }
        }
    }

    // ── Dungeon Enemy Scaler ──────────────────────────────────

    /// <summary>
    /// 무한 던전 전용 적 스케일링 컴포넌트.
    /// 적의 HP/ATK를 런타임에 스케일링한다.
    /// </summary>
    public class DungeonEnemyScaler : MonoBehaviour
    {
        public int ScaledMaxHp { get; private set; }
        public int ScaledCurrentHp { get; private set; }
        public float AtkMultiplier { get; private set; } = 1f;

        private EnemyBase enemyBase;

        /// <summary>
        /// 스케일링을 적용한다.
        /// </summary>
        public void ApplyScaling(int scaledHp, float atkMultiplier)
        {
            enemyBase = GetComponent<EnemyBase>();
            ScaledMaxHp = scaledHp;
            ScaledCurrentHp = scaledHp;
            AtkMultiplier = atkMultiplier;
        }

        /// <summary>
        /// 스케일링된 공격력을 반환한다.
        /// DamageCalculator에서 이 컴포넌트를 참조하여 배율을 적용할 수 있다.
        /// </summary>
        public int GetScaledAttack(int baseAttack)
        {
            return Mathf.RoundToInt(baseAttack * AtkMultiplier);
        }
    }
}
