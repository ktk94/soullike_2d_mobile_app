using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulCraft.Core;

namespace SoulCraft.Enemy
{
    /// <summary>
    /// 웨이브 내 개별 적 스폰 정보.
    /// </summary>
    [Serializable]
    public class SpawnEntry
    {
        public string poolKey;
        public EnemyData enemyData;
        public int count = 1;
    }

    /// <summary>
    /// 웨이브 하나를 정의하는 클래스.
    /// </summary>
    [Serializable]
    public class SpawnWave
    {
        public string waveName;
        public List<SpawnEntry> entries = new();
        [Tooltip("이 웨이브 시작 전 대기 시간")]
        public float delayBeforeWave = 1f;
        [Tooltip("적 사이 스폰 간격")]
        public float spawnInterval = 0.3f;
    }

    /// <summary>
    /// 방(Room) 진입 시 적을 웨이브 단위로 스폰하고,
    /// 모든 적 처치 시 클리어 이벤트를 발행하는 스포너.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        // ── Inspector ───────────────────────────────────────
        [Header("Waves")]
        [SerializeField] private List<SpawnWave> waves = new();

        [Header("Spawn Area")]
        [SerializeField] private Transform[] spawnPoints;
        [Tooltip("spawnPoints가 없을 경우 이 반경 안에서 랜덤 스폰")]
        [SerializeField] private float randomSpawnRadius = 4f;

        [Header("Settings")]
        [Tooltip("방 진입 시 자동으로 시작할지 여부")]
        [SerializeField] private bool autoStartOnEnable = true;
        [SerializeField] private int stageIndex;
        [SerializeField] private int floorIndex;

        // ── Runtime ─────────────────────────────────────────
        public bool IsCleared { get; private set; }
        public int CurrentWaveIndex { get; private set; }
        public int AliveEnemyCount => aliveEnemies.Count;

        public event Action OnAllWavesCleared;
        public event Action<int> OnWaveStarted; // wave index

        private readonly List<GameObject> aliveEnemies = new();
        private Coroutine spawnCoroutine;

        // ── Lifecycle ───────────────────────────────────────

        private void OnEnable()
        {
            GameEventSystem.Subscribe<EnemyDeathEvent>(OnEnemyDeath);

            if (autoStartOnEnable)
                StartSpawning();
        }

        private void OnDisable()
        {
            GameEventSystem.Unsubscribe<EnemyDeathEvent>(OnEnemyDeath);

            if (spawnCoroutine != null)
                StopCoroutine(spawnCoroutine);
        }

        // ── Public API ──────────────────────────────────────

        /// <summary>
        /// 웨이브 스폰을 시작한다.
        /// </summary>
        public void StartSpawning()
        {
            if (spawnCoroutine != null)
                StopCoroutine(spawnCoroutine);

            IsCleared = false;
            CurrentWaveIndex = 0;
            aliveEnemies.Clear();
            spawnCoroutine = StartCoroutine(SpawnWavesCoroutine());
        }

        /// <summary>
        /// 모든 스폰을 즉시 중단한다.
        /// </summary>
        public void StopSpawning()
        {
            if (spawnCoroutine != null)
            {
                StopCoroutine(spawnCoroutine);
                spawnCoroutine = null;
            }
        }

        // ── Spawn Logic ─────────────────────────────────────

        private IEnumerator SpawnWavesCoroutine()
        {
            for (int w = 0; w < waves.Count; w++)
            {
                CurrentWaveIndex = w;
                SpawnWave wave = waves[w];

                // 웨이브 시작 전 대기
                yield return new WaitForSeconds(wave.delayBeforeWave);

                OnWaveStarted?.Invoke(w);

                // 웨이브 내 적 스폰
                yield return SpawnWaveEntries(wave);

                // 이 웨이브의 모든 적이 처치될 때까지 대기
                yield return new WaitUntil(() => aliveEnemies.Count == 0);
            }

            // 모든 웨이브 클리어
            IsCleared = true;
            OnAllWavesCleared?.Invoke();

            GameEventSystem.Publish(new StageCompleteEvent
            {
                StageIndex = stageIndex,
                FloorIndex = floorIndex,
                ClearTime = Time.timeSinceLevelLoad
            });
        }

        private IEnumerator SpawnWaveEntries(SpawnWave wave)
        {
            int spawnPointIndex = 0;

            foreach (SpawnEntry entry in wave.entries)
            {
                for (int i = 0; i < entry.count; i++)
                {
                    Vector3 pos = GetSpawnPosition(ref spawnPointIndex);
                    GameObject enemy = SpawnEnemy(entry, pos);

                    if (enemy != null)
                        aliveEnemies.Add(enemy);

                    yield return new WaitForSeconds(wave.spawnInterval);
                }
            }
        }

        /// <summary>
        /// 적 하나를 생성하고 초기화한다.
        /// ObjectPool이 있으면 풀을 사용하고, 없으면 null 반환.
        /// </summary>
        private GameObject SpawnEnemy(SpawnEntry entry, Vector3 position)
        {
            if (ObjectPool.Instance == null)
            {
                Debug.LogWarning("[EnemySpawner] ObjectPool이 없습니다.");
                return null;
            }

            GameObject enemy = ObjectPool.Instance.Spawn(
                entry.poolKey, position, Quaternion.identity);

            if (enemy == null)
            {
                Debug.LogWarning($"[EnemySpawner] 풀 키 '{entry.poolKey}'에서 오브젝트를 가져올 수 없습니다.");
                return null;
            }

            // EnemyBase가 있으면 데이터 재할당 후 초기화
            EnemyBase enemyBase = enemy.GetComponent<EnemyBase>();
            if (enemyBase != null)
                enemyBase.InitializeEnemy();

            return enemy;
        }

        /// <summary>
        /// 스폰 위치를 결정한다. spawnPoints가 있으면 순환, 없으면 랜덤.
        /// </summary>
        private Vector3 GetSpawnPosition(ref int index)
        {
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                Vector3 pos = spawnPoints[index % spawnPoints.Length].position;
                index++;
                return pos;
            }

            // 랜덤 위치
            Vector2 offset = UnityEngine.Random.insideUnitCircle * randomSpawnRadius;
            return transform.position + new Vector3(offset.x, offset.y, 0f);
        }

        // ── Death Tracking ──────────────────────────────────

        private void OnEnemyDeath(EnemyDeathEvent evt)
        {
            aliveEnemies.Remove(evt.Enemy);

            // null 참조 정리
            aliveEnemies.RemoveAll(e => e == null || !e.activeInHierarchy);
        }

        // ── Gizmos ──────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            // 랜덤 스폰 반경
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, randomSpawnRadius);

            // 스폰 포인트
            if (spawnPoints == null) return;
            Gizmos.color = Color.cyan;
            foreach (var point in spawnPoints)
            {
                if (point != null)
                    Gizmos.DrawWireSphere(point.position, 0.3f);
            }
        }
    }
}
