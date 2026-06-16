using System.Collections;
using UnityEngine;
using SoulCraft.Core;

namespace SoulCraft.Combat
{
    /// <summary>
    /// 전투 파티클 이펙트 통합 관리자.
    /// 대시 잔상, 이동 먼지, 스킬 시전 파동, 레벨업 이펙트, 아이템 획득 이펙트를
    /// 모두 ObjectPool 기반으로 관리한다.
    /// 씬에 하나 배치하고, 이벤트 구독 및 외부 API로 이펙트를 트리거한다.
    /// </summary>
    public class CombatParticleManager : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────
        public static CombatParticleManager Instance { get; private set; }

        // ── Pool Keys ─────────────────────────────────────────
        public const string PoolKeyDashGhost     = "FX_DashGhost";
        public const string PoolKeyMoveDust      = "FX_MoveDust";
        public const string PoolKeySkillCast     = "FX_SkillCast";
        public const string PoolKeyLevelUp       = "FX_LevelUp";
        public const string PoolKeyItemPickup    = "FX_ItemPickup";

        // ── Prefabs ───────────────────────────────────────────
        [Header("Dash Ghost (대시 잔상)")]
        [SerializeField] private GameObject _dashGhostPrefab;
        [Tooltip("대시 시 생성할 잔상 수")]
        [SerializeField] private int _dashGhostCount = 4;
        [Tooltip("잔상 간 생성 간격(초)")]
        [SerializeField] private float _dashGhostInterval = 0.04f;
        [Tooltip("잔상 지속 시간")]
        [SerializeField] private float _dashGhostLifetime = 0.3f;
        [Tooltip("잔상 시작 알파")]
        [SerializeField] private float _dashGhostAlpha = 0.5f;

        [Header("Move Dust (이동 먼지)")]
        [SerializeField] private GameObject _moveDustPrefab;
        [Tooltip("먼지 생성 최소 속도")]
        [SerializeField] private float _dustMinSpeed = 4f;
        [Tooltip("먼지 생성 간격(초)")]
        [SerializeField] private float _dustInterval = 0.15f;
        [Tooltip("먼지 지속 시간")]
        [SerializeField] private float _dustLifetime = 0.4f;
        [Tooltip("먼지 오프셋 (발밑)")]
        [SerializeField] private Vector3 _dustOffset = new(0f, -0.3f, 0f);

        [Header("Skill Cast (스킬 시전 이펙트)")]
        [SerializeField] private GameObject _skillCastPrefab;
        [Tooltip("시전 이펙트 지속 시간")]
        [SerializeField] private float _skillCastLifetime = 0.6f;

        [Header("Level Up (레벨업 이펙트)")]
        [SerializeField] private GameObject _levelUpPrefab;
        [Tooltip("레벨업 이펙트 지속 시간")]
        [SerializeField] private float _levelUpLifetime = 2.0f;

        [Header("Item Pickup (아이템 획득 이펙트)")]
        [SerializeField] private GameObject _itemPickupPrefab;
        [Tooltip("아이템 획득 이펙트 지속 시간")]
        [SerializeField] private float _itemPickupLifetime = 0.5f;

        [Header("Pool Settings")]
        [SerializeField] private int _defaultPoolSize = 10;

        // ── Internal State ────────────────────────────────────
        private float _dustTimer;
        private Transform _playerTransform;
        private Rigidbody2D _playerRb;
        private SpriteRenderer _playerSpriteRenderer;

        // ============================================================
        //  Unity Lifecycle
        // ============================================================

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
            RegisterPools();
            FindPlayer();
        }

        void OnEnable()
        {
            GameEventSystem.Subscribe<SkillUsedEvent>(OnSkillUsed);
            GameEventSystem.Subscribe<ItemDropEvent>(OnItemPickup);
        }

        void OnDisable()
        {
            GameEventSystem.Unsubscribe<SkillUsedEvent>(OnSkillUsed);
            GameEventSystem.Unsubscribe<ItemDropEvent>(OnItemPickup);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            UpdateMoveDust();
        }

        // ============================================================
        //  Pool Registration
        // ============================================================

        private void RegisterPools()
        {
            if (ObjectPool.Instance == null) return;

            TryRegister(PoolKeyDashGhost, _dashGhostPrefab);
            TryRegister(PoolKeyMoveDust, _moveDustPrefab);
            TryRegister(PoolKeySkillCast, _skillCastPrefab);
            TryRegister(PoolKeyLevelUp, _levelUpPrefab);
            TryRegister(PoolKeyItemPickup, _itemPickupPrefab);
        }

        private void TryRegister(string key, GameObject prefab)
        {
            if (prefab != null)
                ObjectPool.Instance.RegisterPool(key, prefab, _defaultPoolSize);
        }

        // ============================================================
        //  Player Reference
        // ============================================================

        private void FindPlayer()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _playerTransform = player.transform;
                _playerRb = player.GetComponent<Rigidbody2D>();
                _playerSpriteRenderer = player.GetComponentInChildren<SpriteRenderer>();

                // 레벨업 이벤트 구독 (PlayerStats C# 이벤트)
                var stats = player.GetComponent<SoulCraft.Player.PlayerStats>();
                if (stats != null)
                    stats.OnLevelUp += OnLevelUp;
            }
        }

        // ============================================================
        //  1. Dash Ghost (대시 잔상)
        // ============================================================

        /// <summary>
        /// 대시 시 호출. 반투명 분신을 N개 생성한다.
        /// PlayerController.TryDash() 등에서 호출 가능.
        /// </summary>
        public void SpawnDashGhosts()
        {
            if (_playerTransform == null || _dashGhostPrefab == null) return;
            StartCoroutine(DashGhostCoroutine());
        }

        private IEnumerator DashGhostCoroutine()
        {
            for (int i = 0; i < _dashGhostCount; i++)
            {
                SpawnSingleGhost();
                yield return new WaitForSeconds(_dashGhostInterval);
            }
        }

        private void SpawnSingleGhost()
        {
            if (ObjectPool.Instance == null || _playerTransform == null) return;

            GameObject ghost = ObjectPool.Instance.Spawn(
                PoolKeyDashGhost,
                _playerTransform.position,
                Quaternion.identity);

            if (ghost == null) return;

            // 잔상 스프라이트 복사
            var ghostSr = ghost.GetComponentInChildren<SpriteRenderer>();
            if (ghostSr != null && _playerSpriteRenderer != null)
            {
                ghostSr.sprite = _playerSpriteRenderer.sprite;
                ghostSr.flipX = _playerSpriteRenderer.flipX;

                Color c = _playerSpriteRenderer.color;
                c.a = _dashGhostAlpha;
                ghostSr.color = c;
            }

            // 페이드 아웃 코루틴
            StartCoroutine(FadeAndDespawn(ghost, ghostSr, _dashGhostLifetime, PoolKeyDashGhost));
        }

        private IEnumerator FadeAndDespawn(
            GameObject obj,
            SpriteRenderer sr,
            float lifetime,
            string poolKey)
        {
            if (sr == null)
            {
                ObjectPool.Instance?.Despawn(poolKey, obj, lifetime);
                yield break;
            }

            float elapsed = 0f;
            Color startColor = sr.color;

            while (elapsed < lifetime)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(startColor.a, 0f, elapsed / lifetime);
                Color c = startColor;
                c.a = alpha;
                if (sr != null) sr.color = c;
                yield return null;
            }

            if (ObjectPool.Instance != null && obj != null)
                ObjectPool.Instance.Despawn(poolKey, obj);
        }

        // ============================================================
        //  2. Move Dust (이동 먼지)
        // ============================================================

        private void UpdateMoveDust()
        {
            if (_playerRb == null || _moveDustPrefab == null) return;
            if (ObjectPool.Instance == null) return;

            float speed = _playerRb.linearVelocity.magnitude;
            if (speed < _dustMinSpeed) return;

            _dustTimer -= Time.deltaTime;
            if (_dustTimer > 0f) return;

            _dustTimer = _dustInterval;

            Vector3 spawnPos = _playerTransform.position + _dustOffset;
            GameObject dust = ObjectPool.Instance.Spawn(
                PoolKeyMoveDust,
                spawnPos,
                Quaternion.identity);

            if (dust != null)
                ObjectPool.Instance.Despawn(PoolKeyMoveDust, dust, _dustLifetime);
        }

        // ============================================================
        //  3. Skill Cast (스킬 시전 이펙트)
        // ============================================================

        /// <summary>
        /// 스킬 시전 시 캐릭터 주변에 원형 파동 이펙트를 스폰한다.
        /// </summary>
        public void SpawnSkillCastEffect(Vector3 position)
        {
            if (_skillCastPrefab == null || ObjectPool.Instance == null) return;

            GameObject fx = ObjectPool.Instance.Spawn(
                PoolKeySkillCast,
                position,
                Quaternion.identity);

            if (fx != null)
                ObjectPool.Instance.Despawn(PoolKeySkillCast, fx, _skillCastLifetime);
        }

        private void OnSkillUsed(SkillUsedEvent evt)
        {
            if (_playerTransform != null)
                SpawnSkillCastEffect(_playerTransform.position);
        }

        // ============================================================
        //  4. Level Up (레벨업 이펙트)
        // ============================================================

        /// <summary>
        /// 레벨업 시 기둥 모양 빛 이펙트를 스폰한다.
        /// </summary>
        public void SpawnLevelUpEffect(Vector3 position)
        {
            if (_levelUpPrefab == null || ObjectPool.Instance == null) return;

            GameObject fx = ObjectPool.Instance.Spawn(
                PoolKeyLevelUp,
                position,
                Quaternion.identity);

            if (fx != null)
                ObjectPool.Instance.Despawn(PoolKeyLevelUp, fx, _levelUpLifetime);
        }

        private void OnLevelUp(int newLevel)
        {
            if (_playerTransform != null)
                SpawnLevelUpEffect(_playerTransform.position);

            // 레벨업 시 카메라 줌 펀치 연동
            if (ImpactSystem.Instance != null)
                ImpactSystem.Instance.TriggerZoomPunch(0.2f, 0.3f);
        }

        // ============================================================
        //  5. Item Pickup (아이템 획득 이펙트)
        // ============================================================

        /// <summary>
        /// 아이템 획득 위치에 반짝임 이펙트를 스폰한다.
        /// </summary>
        public void SpawnItemPickupEffect(Vector3 position)
        {
            if (_itemPickupPrefab == null || ObjectPool.Instance == null) return;

            GameObject fx = ObjectPool.Instance.Spawn(
                PoolKeyItemPickup,
                position,
                Quaternion.identity);

            if (fx != null)
                ObjectPool.Instance.Despawn(PoolKeyItemPickup, fx, _itemPickupLifetime);
        }

        private void OnItemPickup(ItemDropEvent evt)
        {
            SpawnItemPickupEffect(evt.Position);
        }

        // ============================================================
        //  Combo / Generic API
        // ============================================================

        /// <summary>
        /// 임의의 풀 키로 파티클을 스폰한다.
        /// 외부 시스템이 커스텀 이펙트를 요청할 때 사용.
        /// </summary>
        public GameObject SpawnEffect(string poolKey, Vector3 position, float lifetime)
        {
            if (ObjectPool.Instance == null) return null;

            GameObject fx = ObjectPool.Instance.Spawn(poolKey, position, Quaternion.identity);
            if (fx != null && lifetime > 0f)
                ObjectPool.Instance.Despawn(poolKey, fx, lifetime);

            return fx;
        }
    }
}
