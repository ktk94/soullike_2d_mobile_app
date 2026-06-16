using System;
using System.Collections.Generic;
using UnityEngine;
using SoulCraft.Core;
using SoulCraft.Enemy;

namespace SoulCraft.World
{
    // ── Enums ────────────────────────────────────────────────

    public enum RoomType
    {
        Combat,
        Treasure,
        Shop,
        Boss,
        Rest
    }

    /// <summary>
    /// 던전 내 개별 방(Room)을 관리한다.
    /// 적 스폰, 클리어 판정, 출입구 제어, 오브젝트 배치를 담당.
    /// </summary>
    public class Room : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────

        [Header("Room Settings")]
        [SerializeField] private RoomType roomType = RoomType.Combat;

        [Header("Doors")]
        [SerializeField] private GameObject[] doors;
        [Tooltip("방 클리어 전에는 출구 문이 잠겨 있다")]
        [SerializeField] private bool lockDoorsOnEnter = true;

        [Header("Spawn Points")]
        [SerializeField] private Transform[] enemySpawnPoints;
        [SerializeField] private Transform[] obstacleSpawnPoints;
        [SerializeField] private Transform[] trapSpawnPoints;

        [Header("Prefabs")]
        [SerializeField] private GameObject treasureChestPrefab;
        [SerializeField] private Transform treasureSpawnPoint;

        // ── Runtime ──────────────────────────────────────────

        public RoomType Type => roomType;
        public bool IsCleared { get; private set; }
        public bool IsPlayerInside { get; private set; }

        public event Action OnRoomEntered;
        public event Action OnRoomCleared;

        private readonly List<GameObject> aliveEnemies = new();
        private bool isInitialized;

        // ── Public API ───────────────────────────────────────

        /// <summary>
        /// 방을 초기화한다. StageManager에서 방 생성 시 호출.
        /// </summary>
        public void Initialize(RoomType type, EnemyData[] enemyPool = null, int enemyCount = 0)
        {
            roomType = type;
            IsCleared = false;
            isInitialized = true;
            aliveEnemies.Clear();

            SetDoorsLocked(false); // 입장 전에는 열린 상태

            switch (roomType)
            {
                case RoomType.Combat:
                case RoomType.Boss:
                    PrepareEnemySpawns(enemyPool, enemyCount);
                    break;
                case RoomType.Treasure:
                    SpawnTreasure();
                    break;
                case RoomType.Shop:
                case RoomType.Rest:
                    // Shop과 Rest는 별도 컴포넌트(ShopSystem, RestPoint)가 처리
                    break;
            }

            isInitialized = true;
        }

        /// <summary>
        /// 플레이어가 방에 진입했을 때 호출.
        /// </summary>
        public void OnPlayerEnter()
        {
            if (IsPlayerInside) return;
            IsPlayerInside = true;

            OnRoomEntered?.Invoke();

            if (lockDoorsOnEnter && !IsCleared &&
                (roomType == RoomType.Combat || roomType == RoomType.Boss))
            {
                SetDoorsLocked(true);
                SpawnEnemies();
            }

            // 보스방 진입 시 GameState를 BossFight로 변경
            if (roomType == RoomType.Boss && GameManager.Instance != null)
            {
                GameManager.Instance.ChangeState(GameState.BossFight);
            }
        }

        /// <summary>
        /// 플레이어가 방을 떠났을 때 호출.
        /// </summary>
        public void OnPlayerExit()
        {
            IsPlayerInside = false;
        }

        /// <summary>
        /// 적이 죽었을 때 호출. 남은 적이 없으면 클리어 처리.
        /// </summary>
        public void OnEnemyDefeated(GameObject enemy)
        {
            aliveEnemies.Remove(enemy);
            aliveEnemies.RemoveAll(e => e == null || !e.activeInHierarchy);

            if (aliveEnemies.Count == 0 && !IsCleared)
            {
                ClearRoom();
            }
        }

        // ── Internal ─────────────────────────────────────────

        private void OnEnable()
        {
            GameEventSystem.Subscribe<EnemyDeathEvent>(HandleEnemyDeath);
        }

        private void OnDisable()
        {
            GameEventSystem.Unsubscribe<EnemyDeathEvent>(HandleEnemyDeath);
        }

        private void HandleEnemyDeath(EnemyDeathEvent evt)
        {
            if (!IsPlayerInside) return;
            OnEnemyDefeated(evt.Enemy);
        }

        /// <summary>
        /// 전투방/보스방 적 스폰 데이터를 준비한다 (아직 실제 스폰은 하지 않음).
        /// </summary>
        private void PrepareEnemySpawns(EnemyData[] enemyPool, int enemyCount)
        {
            // 스폰 데이터만 캐싱. 실제 스폰은 OnPlayerEnter에서 수행.
            cachedEnemyPool = enemyPool;
            cachedEnemyCount = enemyCount;
        }

        private EnemyData[] cachedEnemyPool;
        private int cachedEnemyCount;

        /// <summary>
        /// 캐싱된 데이터를 기반으로 적을 실제 스폰한다.
        /// </summary>
        private void SpawnEnemies()
        {
            if (cachedEnemyPool == null || cachedEnemyPool.Length == 0) return;

            int count = cachedEnemyCount > 0 ? cachedEnemyCount : 3;

            for (int i = 0; i < count; i++)
            {
                EnemyData data = cachedEnemyPool[UnityEngine.Random.Range(0, cachedEnemyPool.Length)];
                Vector3 pos = GetEnemySpawnPosition(i);

                GameObject enemy = SpawnEnemyFromPool(data, pos);
                if (enemy != null)
                    aliveEnemies.Add(enemy);
            }
        }

        /// <summary>
        /// ObjectPool을 사용하여 적 오브젝트를 스폰한다.
        /// </summary>
        private GameObject SpawnEnemyFromPool(EnemyData data, Vector3 position)
        {
            if (data == null) return null;

            if (ObjectPool.Instance != null)
            {
                GameObject enemy = ObjectPool.Instance.Spawn(data.enemyId, position, Quaternion.identity);
                if (enemy != null)
                {
                    EnemyBase enemyBase = enemy.GetComponent<EnemyBase>();
                    if (enemyBase != null)
                        enemyBase.InitializeEnemy();
                }
                return enemy;
            }

            Debug.LogWarning("[Room] ObjectPool이 없습니다. 적을 스폰할 수 없습니다.");
            return null;
        }

        /// <summary>
        /// 적 스폰 위치를 결정한다.
        /// </summary>
        private Vector3 GetEnemySpawnPosition(int index)
        {
            if (enemySpawnPoints != null && enemySpawnPoints.Length > 0)
                return enemySpawnPoints[index % enemySpawnPoints.Length].position;

            // 스폰 포인트가 없으면 방 중심 주변 랜덤 배치
            Vector2 offset = UnityEngine.Random.insideUnitCircle * 3f;
            return transform.position + new Vector3(offset.x, offset.y, 0f);
        }

        /// <summary>
        /// 보물방에 보물 상자를 스폰한다.
        /// </summary>
        private void SpawnTreasure()
        {
            if (treasureChestPrefab == null) return;

            Vector3 pos = treasureSpawnPoint != null
                ? treasureSpawnPoint.position
                : transform.position;

            Instantiate(treasureChestPrefab, pos, Quaternion.identity, transform);
        }

        /// <summary>
        /// 방을 클리어 처리한다.
        /// </summary>
        private void ClearRoom()
        {
            IsCleared = true;
            SetDoorsLocked(false);
            OnRoomCleared?.Invoke();

            // 보스방 클리어 시 Playing으로 복귀
            if (roomType == RoomType.Boss && GameManager.Instance != null)
            {
                GameManager.Instance.ChangeState(GameState.Playing);
            }

            GameEventSystem.Publish(new RoomClearedEvent
            {
                RoomType = roomType
            });
        }

        /// <summary>
        /// 문(출구)의 잠금 상태를 설정한다.
        /// </summary>
        private void SetDoorsLocked(bool locked)
        {
            if (doors == null) return;

            foreach (var door in doors)
            {
                if (door != null)
                    door.SetActive(!locked); // active = 통과 가능
            }
        }

        // ── Trigger ──────────────────────────────────────────

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
                OnPlayerEnter();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
                OnPlayerExit();
        }
    }

    // ── Events ───────────────────────────────────────────────

    public struct RoomClearedEvent
    {
        public RoomType RoomType;
    }
}
