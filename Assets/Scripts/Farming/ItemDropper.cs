using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulCraft.Core;

namespace SoulCraft.Farming
{
    /// <summary>
    /// 적 사망 시 아이템 드롭 오브젝트를 생성하고 관리한다.
    /// GameEventSystem의 EnemyDeathEvent를 구독하여 동작한다.
    /// </summary>
    public class ItemDropper : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────

        [Header("Loot Tables")]
        [Tooltip("enemyId -> LootTable 매핑. Inspector에서 설정한다.")]
        [SerializeField] private List<EnemyLootMapping> lootMappings = new();

        [Header("Drop Settings")]
        [SerializeField] private GameObject droppedItemPrefab;
        [SerializeField] private float dropScatterRadius = 0.5f;

        [Header("Pickup Settings")]
        [SerializeField] private float pickupRadius = 1.5f;
        [SerializeField] private float magnetRadius = 3f;
        [SerializeField] private float magnetSpeed = 8f;

        [Header("Glow Colors by Rarity")]
        [SerializeField] private Color commonGlow     = new Color(0.8f, 0.8f, 0.8f, 1f);
        [SerializeField] private Color uncommonGlow   = new Color(0.2f, 0.8f, 0.2f, 1f);
        [SerializeField] private Color rareGlow       = new Color(0.2f, 0.4f, 1f, 1f);
        [SerializeField] private Color epicGlow       = new Color(0.7f, 0.2f, 0.9f, 1f);
        [SerializeField] private Color legendaryGlow  = new Color(1f, 0.84f, 0f, 1f);

        // ── Runtime ──────────────────────────────────────

        private readonly Dictionary<string, LootTable> _lootTableMap = new();
        private Transform _playerTransform;
        private readonly List<DroppedItem> _activeDrops = new();

        // ── Lifecycle ────────────────────────────────────

        void Awake()
        {
            // Inspector 매핑을 Dictionary로 변환
            foreach (var mapping in lootMappings)
            {
                if (!string.IsNullOrEmpty(mapping.enemyId) && mapping.lootTable != null)
                    _lootTableMap[mapping.enemyId] = mapping.lootTable;
            }
        }

        void OnEnable()
        {
            GameEventSystem.Subscribe<EnemyDeathEvent>(OnEnemyDeath);
        }

        void OnDisable()
        {
            GameEventSystem.Unsubscribe<EnemyDeathEvent>(OnEnemyDeath);
        }

        void Start()
        {
            // 플레이어 트랜스폼 캐싱
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                _playerTransform = player.transform;
        }

        void Update()
        {
            if (_playerTransform == null) return;

            Vector2 playerPos = _playerTransform.position;

            // 역순 순회 (삭제 대응)
            for (int i = _activeDrops.Count - 1; i >= 0; i--)
            {
                var drop = _activeDrops[i];
                if (drop == null || drop.gameObject == null)
                {
                    _activeDrops.RemoveAt(i);
                    continue;
                }

                float distance = Vector2.Distance(playerPos, drop.transform.position);

                // 수집 범위 안이면 획득
                if (distance <= pickupRadius)
                {
                    PickupItem(drop);
                    _activeDrops.RemoveAt(i);
                    continue;
                }

                // 자석 범위 안이면 플레이어에게 끌려감
                if (distance <= magnetRadius)
                {
                    Vector2 direction = (playerPos - (Vector2)drop.transform.position).normalized;
                    drop.transform.position += (Vector3)(direction * magnetSpeed * Time.deltaTime);
                }
            }
        }

        // ── Event Handlers ───────────────────────────────

        private void OnEnemyDeath(EnemyDeathEvent evt)
        {
            if (!_lootTableMap.TryGetValue(evt.EnemyId, out var lootTable)) return;

            var drops = lootTable.Roll();
            foreach (var (item, quantity) in drops)
            {
                SpawnDroppedItem(item, quantity, evt.Position);
            }
        }

        // ── Spawning ─────────────────────────────────────

        private void SpawnDroppedItem(ItemData item, int quantity, Vector2 position)
        {
            // 약간 흩뿌리기
            Vector2 scatter = Random.insideUnitCircle * dropScatterRadius;
            Vector3 spawnPos = new Vector3(position.x + scatter.x, position.y + scatter.y, 0f);

            GameObject obj;
            if (droppedItemPrefab != null)
            {
                obj = Instantiate(droppedItemPrefab, spawnPos, Quaternion.identity);
            }
            else
            {
                // 프리팹이 없으면 임시 오브젝트 생성
                obj = new GameObject($"Drop_{item.itemName}");
                obj.transform.position = spawnPos;
                var sr = obj.AddComponent<SpriteRenderer>();
                sr.sprite = item.icon;
                sr.sortingOrder = 5;
            }

            // DroppedItem 컴포넌트 부착
            var droppedItem = obj.GetComponent<DroppedItem>();
            if (droppedItem == null)
                droppedItem = obj.AddComponent<DroppedItem>();

            droppedItem.Initialize(item, quantity);

            // 레어리티별 글로우 효과
            ApplyGlow(obj, item.rarity);

            // 드롭 애니메이션: 바닥에 떨어지는 연출
            StartCoroutine(DropBounceAnimation(obj.transform, spawnPos));

            _activeDrops.Add(droppedItem);
        }

        /// <summary>
        /// 드롭 시 위로 살짝 튀었다가 내려오는 연출.
        /// </summary>
        private IEnumerator DropBounceAnimation(Transform target, Vector3 finalPos)
        {
            if (target == null) yield break;

            float duration = 0.4f;
            float bounceHeight = 0.6f;
            float elapsed = 0f;
            Vector3 startPos = finalPos + Vector3.up * bounceHeight;

            target.position = startPos;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // 바운스 커브: 위에서 아래로 내려오며 감쇄
                float yOffset = bounceHeight * (1f - t) * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f));
                target.position = finalPos + Vector3.up * yOffset;

                yield return null;
            }

            if (target != null)
                target.position = finalPos;
        }

        // ── Glow ─────────────────────────────────────────

        private void ApplyGlow(GameObject obj, Rarity rarity)
        {
            Color glowColor = rarity switch
            {
                Rarity.Common    => commonGlow,
                Rarity.Uncommon  => uncommonGlow,
                Rarity.Rare      => rareGlow,
                Rarity.Epic      => epicGlow,
                Rarity.Legendary => legendaryGlow,
                _                => commonGlow
            };

            // SpriteRenderer에 색상 적용
            var sr = obj.GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.color = glowColor;

            // 자식에 글로우 파티클 또는 Light2D가 있으면 색상 전달
            var light = obj.GetComponentInChildren<UnityEngine.Rendering.Universal.Light2D>();
            if (light != null)
            {
                light.color = glowColor;
                light.intensity = rarity >= Rarity.Epic ? 2f : 1f;
            }
        }

        // ── Pickup ───────────────────────────────────────

        private void PickupItem(DroppedItem drop)
        {
            if (Inventory.Instance == null) return;

            int added = Inventory.Instance.AddItem(drop.Item, drop.Quantity);
            if (added > 0)
            {
                // 획득 이벤트 발행
                GameEventSystem.Publish(new ItemPickupEvent
                {
                    Item = drop.Item,
                    Quantity = added
                });
            }

            Destroy(drop.gameObject);
        }
    }

    // ── Supporting Types ─────────────────────────────────

    /// <summary>
    /// 바닥에 떨어진 아이템 오브젝트에 부착되는 컴포넌트.
    /// </summary>
    public class DroppedItem : MonoBehaviour
    {
        public ItemData Item { get; private set; }
        public int Quantity { get; private set; }

        public void Initialize(ItemData item, int quantity)
        {
            Item = item;
            Quantity = quantity;
        }
    }

    /// <summary>
    /// enemyId와 LootTable의 매핑. Inspector에서 설정한다.
    /// </summary>
    [System.Serializable]
    public class EnemyLootMapping
    {
        public string enemyId;
        public LootTable lootTable;
    }

    public struct ItemPickupEvent
    {
        public ItemData Item;
        public int Quantity;
    }
}
