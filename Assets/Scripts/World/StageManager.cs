using System;
using System.Collections.Generic;
using UnityEngine;
using SoulCraft.Core;
using SoulCraft.Enemy;

namespace SoulCraft.World
{
    /// <summary>
    /// 방 배치 정보. 한 층(Floor) 내 방 시퀀스를 정의.
    /// </summary>
    [Serializable]
    public class FloorLayout
    {
        public List<RoomType> roomSequence = new();
    }

    /// <summary>
    /// 스테이지 진행을 총괄하는 매니저.
    /// 현재 스테이지/층/방을 관리하고, 층 이동 시 방을 랜덤 배치한다.
    /// </summary>
    public class StageManager : MonoBehaviour
    {
        public static StageManager Instance { get; private set; }

        // ── Inspector ────────────────────────────────────────

        [Header("Stage Database")]
        [SerializeField] private StageData[] allStages;

        [Header("Room Prefabs")]
        [SerializeField] private GameObject combatRoomPrefab;
        [SerializeField] private GameObject treasureRoomPrefab;
        [SerializeField] private GameObject shopRoomPrefab;
        [SerializeField] private GameObject bossRoomPrefab;
        [SerializeField] private GameObject restRoomPrefab;

        [Header("Layout Settings")]
        [SerializeField] private float roomSpacing = 20f;
        [Tooltip("전투방당 적 수 기본값")]
        [SerializeField] private int enemiesPerCombatRoom = 3;
        [Tooltip("보스방 적 수")]
        [SerializeField] private int enemiesPerBossRoom = 1;

        // ── Runtime ──────────────────────────────────────────

        public StageData CurrentStage { get; private set; }
        public int CurrentFloorIndex { get; private set; }
        public int CurrentRoomIndex { get; private set; }
        public Room CurrentRoom => currentRooms != null && CurrentRoomIndex < currentRooms.Count
            ? currentRooms[CurrentRoomIndex] : null;

        public event Action<StageData> OnStageStarted;
        public event Action<int> OnFloorChanged;       // floorIndex
        public event Action<Room> OnRoomChanged;        // room
        public event Action OnStageCleared;

        private readonly List<Room> currentRooms = new();
        private readonly List<GameObject> roomInstances = new();
        private FloorLayout currentFloorLayout;

        // ── Lifecycle ────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            GameEventSystem.Subscribe<RoomClearedEvent>(OnRoomClearedHandler);
        }

        private void OnDisable()
        {
            GameEventSystem.Unsubscribe<RoomClearedEvent>(OnRoomClearedHandler);
        }

        // ── Public API ───────────────────────────────────────

        /// <summary>
        /// 스테이지를 시작한다.
        /// </summary>
        public void StartStage(int stageIndex)
        {
            if (stageIndex < 0 || stageIndex >= allStages.Length)
            {
                Debug.LogError($"[StageManager] 유효하지 않은 스테이지 인덱스: {stageIndex}");
                return;
            }

            CurrentStage = allStages[stageIndex];
            CurrentFloorIndex = 0;
            CurrentRoomIndex = 0;

            OnStageStarted?.Invoke(CurrentStage);
            BuildFloor(CurrentFloorIndex);
        }

        /// <summary>
        /// 다음 층으로 이동한다.
        /// </summary>
        public void AdvanceToNextFloor()
        {
            CurrentFloorIndex++;

            if (CurrentFloorIndex >= CurrentStage.floorCount)
            {
                // 모든 층 클리어 → 스테이지 클리어
                HandleStageClear();
                return;
            }

            CurrentRoomIndex = 0;

            if (GameManager.Instance != null)
                GameManager.Instance.AdvanceFloor();

            OnFloorChanged?.Invoke(CurrentFloorIndex);
            BuildFloor(CurrentFloorIndex);
        }

        /// <summary>
        /// 다음 방으로 이동한다.
        /// </summary>
        public void AdvanceToNextRoom()
        {
            CurrentRoomIndex++;

            if (CurrentRoomIndex >= currentRooms.Count)
            {
                // 현재 층의 모든 방 클리어 → 다음 층
                AdvanceToNextFloor();
                return;
            }

            OnRoomChanged?.Invoke(CurrentRoom);
        }

        /// <summary>
        /// 스테이지 인덱스로 StageData를 반환한다.
        /// </summary>
        public StageData GetStageData(int index)
        {
            if (index < 0 || index >= allStages.Length) return null;
            return allStages[index];
        }

        /// <summary>
        /// 등록된 전체 스테이지 수.
        /// </summary>
        public int StageCount => allStages != null ? allStages.Length : 0;

        // ── Floor Building ───────────────────────────────────

        /// <summary>
        /// 지정된 층의 방 시퀀스를 생성하고 인스턴스화한다.
        /// 기본 패턴: 전투 → 전투 → 보물 → 전투 → 보스 (마지막 층)
        ///            전투 → 전투 → 보물 → 전투 → 휴식 (중간 층)
        /// </summary>
        private void BuildFloor(int floorIndex)
        {
            ClearCurrentRooms();

            currentFloorLayout = GenerateFloorLayout(floorIndex);

            for (int i = 0; i < currentFloorLayout.roomSequence.Count; i++)
            {
                RoomType type = currentFloorLayout.roomSequence[i];
                Vector3 position = new Vector3(i * roomSpacing, 0f, 0f);

                GameObject prefab = GetRoomPrefab(type);
                if (prefab == null)
                {
                    Debug.LogWarning($"[StageManager] RoomType.{type}에 대한 프리팹이 없습니다.");
                    continue;
                }

                GameObject roomObj = Instantiate(prefab, position, Quaternion.identity, transform);
                roomObj.name = $"Floor{floorIndex}_Room{i}_{type}";
                roomInstances.Add(roomObj);

                Room room = roomObj.GetComponent<Room>();
                if (room == null)
                    room = roomObj.AddComponent<Room>();

                InitializeRoom(room, type);
                currentRooms.Add(room);

                // 방 클리어 시 다음 방으로 자동 이동 연결
                int capturedIndex = i;
                room.OnRoomCleared += () => OnIndividualRoomCleared(capturedIndex);
            }

            OnFloorChanged?.Invoke(floorIndex);

            if (currentRooms.Count > 0)
                OnRoomChanged?.Invoke(currentRooms[0]);
        }

        /// <summary>
        /// 층 인덱스에 따라 방 시퀀스를 생성한다.
        /// </summary>
        private FloorLayout GenerateFloorLayout(int floorIndex)
        {
            var layout = new FloorLayout();
            bool isLastFloor = (floorIndex == CurrentStage.floorCount - 1);

            // 기본 시퀀스: 전투 → 전투 → 보물 → 전투
            layout.roomSequence.Add(RoomType.Combat);
            layout.roomSequence.Add(RoomType.Combat);

            // 중간에 보물 또는 상점을 랜덤 배치
            if (UnityEngine.Random.value > 0.5f)
                layout.roomSequence.Add(RoomType.Treasure);
            else
                layout.roomSequence.Add(RoomType.Shop);

            layout.roomSequence.Add(RoomType.Combat);

            // 마지막 층이면 보스방, 아니면 휴식방
            if (isLastFloor)
                layout.roomSequence.Add(RoomType.Boss);
            else
                layout.roomSequence.Add(RoomType.Rest);

            return layout;
        }

        /// <summary>
        /// Room 컴포넌트를 타입에 맞게 초기화한다.
        /// </summary>
        private void InitializeRoom(Room room, RoomType type)
        {
            switch (type)
            {
                case RoomType.Combat:
                    room.Initialize(type, CurrentStage.normalEnemyPool, enemiesPerCombatRoom);
                    break;

                case RoomType.Boss:
                    // 보스용 EnemyData 검색 (normalEnemyPool에서 bossId와 일치하는 것)
                    EnemyData[] bossPool = GetBossPool();
                    room.Initialize(type, bossPool, enemiesPerBossRoom);
                    break;

                case RoomType.Treasure:
                case RoomType.Shop:
                case RoomType.Rest:
                    room.Initialize(type);
                    break;
            }
        }

        /// <summary>
        /// 보스 EnemyData 배열을 구성한다.
        /// </summary>
        private EnemyData[] GetBossPool()
        {
            if (CurrentStage.normalEnemyPool == null) return null;

            foreach (var enemy in CurrentStage.normalEnemyPool)
            {
                if (enemy != null && enemy.enemyId == CurrentStage.bossId)
                    return new EnemyData[] { enemy };
            }

            // normalEnemyPool에 보스가 없으면 빈 배열 반환
            // (보스는 별도 프리팹/풀로 관리될 수 있음)
            Debug.LogWarning($"[StageManager] 보스 ID '{CurrentStage.bossId}'를 찾을 수 없습니다.");
            return null;
        }

        /// <summary>
        /// RoomType에 맞는 프리팹을 반환한다.
        /// </summary>
        private GameObject GetRoomPrefab(RoomType type)
        {
            return type switch
            {
                RoomType.Combat   => combatRoomPrefab,
                RoomType.Treasure => treasureRoomPrefab,
                RoomType.Shop     => shopRoomPrefab,
                RoomType.Boss     => bossRoomPrefab,
                RoomType.Rest     => restRoomPrefab,
                _                 => combatRoomPrefab
            };
        }

        // ── Event Handlers ───────────────────────────────────

        private void OnRoomClearedHandler(RoomClearedEvent evt)
        {
            // 이벤트 버스 경유 처리 (필요 시 UI 갱신 등)
        }

        private void OnIndividualRoomCleared(int roomIndex)
        {
            Debug.Log($"[StageManager] Room {roomIndex} ({currentFloorLayout.roomSequence[roomIndex]}) 클리어!");

            // 마지막 방이면 다음 층으로 자동 이동하지 않음 (플레이어 선택 대기)
            // 플레이어가 문을 통과하면 AdvanceToNextRoom()이 호출됨
        }

        /// <summary>
        /// 스테이지 클리어 처리.
        /// </summary>
        private void HandleStageClear()
        {
            OnStageCleared?.Invoke();

            if (GameManager.Instance != null)
                GameManager.Instance.StageClear();

            GameEventSystem.Publish(new StageCompleteEvent
            {
                StageIndex = GameManager.Instance != null ? GameManager.Instance.CurrentStageIndex : 0,
                FloorIndex = CurrentFloorIndex,
                ClearTime = Time.timeSinceLevelLoad
            });
        }

        /// <summary>
        /// 현재 층의 모든 방 인스턴스를 정리한다.
        /// </summary>
        private void ClearCurrentRooms()
        {
            foreach (var obj in roomInstances)
            {
                if (obj != null)
                    Destroy(obj);
            }

            roomInstances.Clear();
            currentRooms.Clear();
        }
    }
}
