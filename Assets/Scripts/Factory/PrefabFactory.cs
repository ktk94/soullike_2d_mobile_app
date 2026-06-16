using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using SoulCraft.Core;
using SoulCraft.Player;
using SoulCraft.Combat;
using SoulCraft.Enemy;
using SoulCraft.Farming;

namespace SoulCraft.Factory
{
    /// <summary>
    /// 모든 게임 오브젝트를 런타임 코드로 조립하는 팩토리.
    /// 에셋 파일(프리팹) 없이 코드만으로 완성된 GameObject를 생성한다.
    /// Dictionary에 캐싱하여 Instantiate 원본으로 재사용할 수 있다.
    /// </summary>
    public static class PrefabFactory
    {
        // ── Prefab Cache ────────────────────────────────────
        private static readonly Dictionary<string, GameObject> _prefabCache = new();

        // ── Layer Names (프로젝트 설정에 맞게 수정 필요) ──────
        private const string LayerPlayer  = "Player";
        private const string LayerEnemy   = "Enemy";
        private const string LayerItem    = "Item";
        private const string LayerDefault = "Default";

        // ── Sorting Layer Names ─────────────────────────────
        private const string SortPlayer  = "Player";
        private const string SortEnemy   = "Enemy";
        private const string SortItem    = "Item";
        private const string SortUI      = "UI";
        private const string SortDefault = "Default";

        // ================================================================
        //  Public API
        // ================================================================

        /// <summary>
        /// 캐시에서 프리팹을 가져온다. 없으면 null.
        /// </summary>
        public static GameObject GetCachedPrefab(string key)
        {
            _prefabCache.TryGetValue(key, out var prefab);
            return prefab;
        }

        /// <summary>
        /// 모든 기본 프리팹을 미리 생성하여 캐시에 등록한다.
        /// 게임 초기화 시 한 번 호출한다.
        /// </summary>
        public static void PrewarmAll()
        {
            CreatePlayer();
            CreateProjectile();
            CreateDoor();
            CreateDamagePopup();
        }

        /// <summary>
        /// 캐시를 전부 비운다.
        /// </summary>
        public static void ClearCache()
        {
            foreach (var kvp in _prefabCache)
            {
                if (kvp.Value != null)
                    Object.Destroy(kvp.Value);
            }
            _prefabCache.Clear();
        }

        // ================================================================
        //  1. CreatePlayer - 플레이어 GameObject
        // ================================================================

        /// <summary>
        /// 플레이어 GameObject를 코드로 조립한다.
        /// SpriteRenderer, Rigidbody2D, BoxCollider2D,
        /// PlayerController, PlayerStats, PlayerCombat, SpriteAnimator,
        /// SkillManager, Equipment, AttackPoint 자식 오브젝트.
        /// </summary>
        public static GameObject CreatePlayer()
        {
            const string cacheKey = "Player";
            if (_prefabCache.TryGetValue(cacheKey, out var cached) && cached != null)
                return cached;

            var go = new GameObject("Player");
            go.tag = "Player";
            SetLayer(go, LayerPlayer);

            // ── SpriteRenderer ──
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.GetSprite("player_idle");
            sr.sortingLayerName = SortPlayer;
            sr.sortingOrder = 0;

            // ── Rigidbody2D ──
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            // ── BoxCollider2D ──
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = false;
            col.size = new Vector2(0.8f, 0.8f);
            col.offset = new Vector2(0f, 0.1f);

            // ── Core Components ──
            // PlayerStats가 먼저 추가되어야 다른 컴포넌트가 GetComponent로 찾을 수 있음
            go.AddComponent<PlayerStats>();
            go.AddComponent<PlayerCombat>();

            // SpriteAnimator (PlayerAnimator 대신 사용 가능)
            var animator = go.AddComponent<SpriteAnimator>();
            animator.SetSpriteRenderer(sr);
            AnimationFactory.SetupPlayerAnimator(animator);

            // PlayerAnimator도 추가 (기존 시스템 호환용 - Animator 컴포넌트가 필요)
            // 기존 PlayerAnimator는 [RequireComponent(typeof(Animator))]를 요구하므로,
            // Animator 컴포넌트를 빈 상태로 추가
            go.AddComponent<Animator>();
            go.AddComponent<PlayerAnimator>();

            // PlayerController (마지막에 추가 - 다른 컴포넌트를 Awake에서 찾음)
            go.AddComponent<PlayerController>();

            // ── Combat & Farming Components ──
            go.AddComponent<SkillManager>();
            go.AddComponent<Equipment>();

            // ── AttackPoint (자식 오브젝트) ──
            var attackPoint = new GameObject("AttackPoint");
            attackPoint.transform.SetParent(go.transform);
            attackPoint.transform.localPosition = new Vector3(0.7f, 0f, 0f);

            // 캐시에 등록 (비활성화하여 템플릿으로 보관)
            go.SetActive(false);
            _prefabCache[cacheKey] = go;

            return go;
        }

        // ================================================================
        //  2. CreateEnemy - 적 GameObject
        // ================================================================

        /// <summary>
        /// 적 GameObject를 코드로 조립한다.
        /// enemyType에 따라 스프라이트와 애니메이션이 달라진다.
        /// </summary>
        public static GameObject CreateEnemy(string enemyType)
        {
            string cacheKey = $"Enemy_{enemyType}";
            if (_prefabCache.TryGetValue(cacheKey, out var cached) && cached != null)
                return cached;

            var go = new GameObject($"Enemy_{enemyType}");
            go.tag = "Enemy";
            SetLayer(go, LayerEnemy);

            // ── SpriteRenderer ──
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.GetSprite($"enemy_{enemyType}_idle");
            sr.sortingLayerName = SortEnemy;
            sr.sortingOrder = 0;

            // ── Rigidbody2D ──
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // ── CircleCollider2D ──
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = false;
            col.radius = 0.4f;

            // ── Enemy Components ──
            go.AddComponent<EnemyBase>();
            go.AddComponent<EnemyAI>();
            go.AddComponent<EnemyStagger>();

            // ── SpriteAnimator ──
            var animator = go.AddComponent<SpriteAnimator>();
            animator.SetSpriteRenderer(sr);
            AnimationFactory.SetupEnemyAnimator(animator, enemyType);

            // 캐시
            go.SetActive(false);
            _prefabCache[cacheKey] = go;

            return go;
        }

        // ================================================================
        //  3. CreateBoss - 보스 GameObject
        // ================================================================

        /// <summary>
        /// 보스 GameObject를 코드로 조립한다.
        /// 일반 적보다 큰 스케일, BossBase, BossPatterns 참조.
        /// </summary>
        public static GameObject CreateBoss(string bossType)
        {
            string cacheKey = $"Boss_{bossType}";
            if (_prefabCache.TryGetValue(cacheKey, out var cached) && cached != null)
                return cached;

            var go = new GameObject($"Boss_{bossType}");
            go.tag = "Enemy";
            SetLayer(go, LayerEnemy);

            // ── SpriteRenderer ──
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.GetSprite($"boss_{bossType}_idle");
            sr.sortingLayerName = SortEnemy;
            sr.sortingOrder = 10; // 보스는 일반 적보다 앞에 렌더링

            // 보스는 더 큰 스케일
            go.transform.localScale = new Vector3(2f, 2f, 1f);

            // ── Rigidbody2D ──
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.mass = 5f; // 보스는 더 무거움
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // ── CircleCollider2D (더 큰 반지름) ──
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = false;
            col.radius = 0.7f;

            // ── Boss Components ──
            // BossBase는 EnemyBase를 상속하므로 별도의 EnemyBase는 불필요
            go.AddComponent<BossBase>();
            go.AddComponent<EnemyAI>();
            go.AddComponent<EnemyStagger>();

            // ── SpriteAnimator ──
            var animator = go.AddComponent<SpriteAnimator>();
            animator.SetSpriteRenderer(sr);
            AnimationFactory.SetupBossAnimator(animator, bossType);

            // 캐시
            go.SetActive(false);
            _prefabCache[cacheKey] = go;

            return go;
        }

        // ================================================================
        //  4. CreateItemDrop - 드롭 아이템 오브젝트
        // ================================================================

        /// <summary>
        /// 드롭 아이템 오브젝트를 코드로 조립한다.
        /// SpriteRenderer, CircleCollider2D(trigger), 바운스 애니메이션.
        /// </summary>
        public static GameObject CreateItemDrop(string itemId)
        {
            string cacheKey = $"ItemDrop_{itemId}";
            if (_prefabCache.TryGetValue(cacheKey, out var cached) && cached != null)
                return cached;

            var go = new GameObject($"ItemDrop_{itemId}");
            go.tag = "Item";
            SetLayer(go, LayerItem);

            // ── SpriteRenderer ──
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.GetSprite($"item_{itemId}");
            sr.sortingLayerName = SortItem;
            sr.sortingOrder = 0;

            // ── CircleCollider2D (트리거) ──
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.3f;

            // ── Rigidbody2D (Kinematic — 물리 안 받음) ──
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;

            // ── 바운스 애니메이션 ──
            go.AddComponent<ItemDropBounce>();

            // 캐시
            go.SetActive(false);
            _prefabCache[cacheKey] = go;

            return go;
        }

        // ================================================================
        //  5. CreateProjectile - 투사체
        // ================================================================

        /// <summary>
        /// 범용 투사체 오브젝트를 코드로 조립한다.
        /// SpriteRenderer, Rigidbody2D(Kinematic), CircleCollider2D(trigger), Projectile.
        /// </summary>
        public static GameObject CreateProjectile()
        {
            const string cacheKey = "Projectile";
            if (_prefabCache.TryGetValue(cacheKey, out var cached) && cached != null)
                return cached;

            var go = new GameObject("Projectile");
            SetLayer(go, LayerDefault);

            // ── SpriteRenderer ──
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.GetSprite("particle_circle");
            sr.sortingLayerName = SortDefault;
            sr.sortingOrder = 50;

            // ── Rigidbody2D (Kinematic) ──
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;

            // ── CircleCollider2D (트리거) ──
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.15f;

            // ── Projectile Component ──
            go.AddComponent<Projectile>();

            // 캐시
            go.SetActive(false);
            _prefabCache[cacheKey] = go;

            return go;
        }

        // ================================================================
        //  6. CreateDoor - 문 오브젝트
        // ================================================================

        /// <summary>
        /// 문 오브젝트를 코드로 조립한다.
        /// SpriteRenderer, BoxCollider2D, DoorController.
        /// </summary>
        public static GameObject CreateDoor()
        {
            const string cacheKey = "Door";
            if (_prefabCache.TryGetValue(cacheKey, out var cached) && cached != null)
                return cached;

            var go = new GameObject("Door");
            SetLayer(go, LayerDefault);

            // ── SpriteRenderer ──
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.GetSprite("door_closed");
            sr.sortingLayerName = SortDefault;
            sr.sortingOrder = -1;

            // ── BoxCollider2D ──
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = false;
            col.size = new Vector2(1f, 1f);

            // ── DoorController ──
            go.AddComponent<DoorController>();

            // 캐시
            go.SetActive(false);
            _prefabCache[cacheKey] = go;

            return go;
        }

        // ================================================================
        //  7. CreateDamagePopup - 데미지 숫자 팝업
        // ================================================================

        /// <summary>
        /// 데미지 숫자 팝업을 코드로 조립한다.
        /// Canvas(World Space) + TextMeshPro + DamagePopup 컴포넌트.
        /// </summary>
        public static GameObject CreateDamagePopup()
        {
            const string cacheKey = "DamagePopup";
            if (_prefabCache.TryGetValue(cacheKey, out var cached) && cached != null)
                return cached;

            var go = new GameObject("DamagePopup");

            // ── Canvas (World Space) ──
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingLayerName = SortUI;
            canvas.sortingOrder = 1000;

            // Canvas RectTransform 설정
            var canvasRect = go.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(2f, 1f);
            canvasRect.localScale = new Vector3(0.02f, 0.02f, 0.02f); // 월드 스페이스에서 적절한 크기

            // ── CanvasGroup (페이드용) ──
            var canvasGroup = go.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;

            // ── TextMeshPro 텍스트 오브젝트 ──
            var textGo = new GameObject("DamageText");
            textGo.transform.SetParent(go.transform, false);

            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = "0";
            tmp.fontSize = 6f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.fontStyle = FontStyles.Bold;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            // 기본 TMP 폰트 사용 (TMP Essentials 임포트 필요)

            // ── DamagePopup Component ──
            // DamagePopup 스크립트가 SerializeField로 참조를 받으므로
            // 리플렉션으로 할당하거나, DamagePopup이 Awake에서 자동 탐색하도록 해야 함
            var popup = go.AddComponent<SoulCraft.UI.DamagePopup>();

            // SerializeField 할당 (리플렉션)
            SetPrivateField(popup, "_damageText", tmp);
            SetPrivateField(popup, "_canvasGroup", canvasGroup);

            // 캐시
            go.SetActive(false);
            _prefabCache[cacheKey] = go;

            return go;
        }

        // ================================================================
        //  Utility: Instantiate helpers
        // ================================================================

        /// <summary>
        /// 캐시된 프리팹을 인스턴스화하고 위치를 지정한다.
        /// </summary>
        public static GameObject Instantiate(string cacheKey, Vector3 position, Quaternion rotation)
        {
            if (!_prefabCache.TryGetValue(cacheKey, out var prefab) || prefab == null)
            {
                Debug.LogWarning($"[PrefabFactory] Prefab not found in cache: {cacheKey}");
                return null;
            }

            var instance = Object.Instantiate(prefab, position, rotation);
            instance.SetActive(true);
            return instance;
        }

        /// <summary>
        /// 적 프리팹을 인스턴스화하고 EnemyData를 할당한다.
        /// </summary>
        public static GameObject InstantiateEnemy(string enemyType, Vector3 position, EnemyData data = null)
        {
            string cacheKey = $"Enemy_{enemyType}";

            // 캐시에 없으면 먼저 생성
            if (!_prefabCache.ContainsKey(cacheKey))
                CreateEnemy(enemyType);

            var instance = Instantiate(cacheKey, position, Quaternion.identity);

            if (instance != null && data != null)
            {
                var enemyBase = instance.GetComponent<EnemyBase>();
                if (enemyBase != null)
                {
                    // EnemyData는 SerializeField이므로 리플렉션으로 할당
                    SetPrivateField(enemyBase, "data", data);
                    enemyBase.InitializeEnemy();
                }
            }

            return instance;
        }

        /// <summary>
        /// 보스 프리팹을 인스턴스화하고 EnemyData를 할당한다.
        /// </summary>
        public static GameObject InstantiateBoss(string bossType, Vector3 position, EnemyData data = null)
        {
            string cacheKey = $"Boss_{bossType}";

            if (!_prefabCache.ContainsKey(cacheKey))
                CreateBoss(bossType);

            var instance = Instantiate(cacheKey, position, Quaternion.identity);

            if (instance != null && data != null)
            {
                var bossBase = instance.GetComponent<BossBase>();
                if (bossBase != null)
                {
                    SetPrivateField(bossBase, "data", data);
                    bossBase.InitializeEnemy();
                }
            }

            return instance;
        }

        /// <summary>
        /// 아이템 드롭을 인스턴스화한다.
        /// </summary>
        public static GameObject InstantiateItemDrop(string itemId, Vector3 position)
        {
            string cacheKey = $"ItemDrop_{itemId}";

            if (!_prefabCache.ContainsKey(cacheKey))
                CreateItemDrop(itemId);

            return Instantiate(cacheKey, position, Quaternion.identity);
        }

        // ================================================================
        //  Private Helpers
        // ================================================================

        /// <summary>
        /// GameObject에 레이어를 이름으로 설정한다. 존재하지 않으면 Default.
        /// </summary>
        private static void SetLayer(GameObject go, string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            go.layer = layer >= 0 ? layer : 0;
        }

        /// <summary>
        /// 리플렉션을 이용하여 private/SerializeField를 할당한다.
        /// </summary>
        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var type = target.GetType();
            var field = type.GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public);

            if (field != null)
            {
                field.SetValue(target, value);
            }
            else
            {
                // 부모 클래스에서 찾기
                var baseType = type.BaseType;
                while (baseType != null)
                {
                    field = baseType.GetField(fieldName,
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.Public);

                    if (field != null)
                    {
                        field.SetValue(target, value);
                        return;
                    }
                    baseType = baseType.BaseType;
                }

                Debug.LogWarning($"[PrefabFactory] Field '{fieldName}' not found on {type.Name}");
            }
        }
    }

    // ================================================================
    //  ItemDropBounce - 아이템 드롭 바운스 애니메이션
    // ================================================================

    /// <summary>
    /// 아이템 드롭 시 위로 튀었다가 바닥에 안착하는 바운스 애니메이션.
    /// 안착 후 위아래로 부드럽게 흔들리는 idle 모션.
    /// </summary>
    public class ItemDropBounce : MonoBehaviour
    {
        [Header("Bounce")]
        [SerializeField] private float _bounceHeight = 0.6f;
        [SerializeField] private float _bounceDuration = 0.4f;

        [Header("Idle Bob")]
        [SerializeField] private float _bobAmplitude = 0.08f;
        [SerializeField] private float _bobSpeed = 2.5f;

        private Vector3 _basePosition;
        private bool _bounceComplete;
        private float _bobTimer;

        void OnEnable()
        {
            _bounceComplete = false;
            _bobTimer = 0f;
            StartCoroutine(BounceCoroutine());
        }

        void Update()
        {
            if (!_bounceComplete) return;

            // 부드러운 위아래 흔들림
            _bobTimer += Time.deltaTime * _bobSpeed;
            float yOffset = Mathf.Sin(_bobTimer) * _bobAmplitude;
            transform.position = _basePosition + Vector3.up * yOffset;
        }

        private IEnumerator BounceCoroutine()
        {
            Vector3 startPos = transform.position;
            Vector3 peakPos = startPos + Vector3.up * _bounceHeight;
            float halfDuration = _bounceDuration * 0.5f;

            // 위로 올라감
            float elapsed = 0f;
            while (elapsed < halfDuration)
            {
                float t = elapsed / halfDuration;
                float easedT = 1f - (1f - t) * (1f - t); // Ease Out Quad
                transform.position = Vector3.Lerp(startPos, peakPos, easedT);
                elapsed += Time.deltaTime;
                yield return null;
            }

            // 아래로 내려옴
            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                float t = elapsed / halfDuration;
                float easedT = t * t; // Ease In Quad
                transform.position = Vector3.Lerp(peakPos, startPos, easedT);
                elapsed += Time.deltaTime;
                yield return null;
            }

            transform.position = startPos;
            _basePosition = startPos;
            _bounceComplete = true;
        }
    }

    // ================================================================
    //  DoorController - 문 열림/닫힘 제어
    // ================================================================

    /// <summary>
    /// 문의 열림/닫힘 상태를 관리한다.
    /// SpriteFactory에서 door_closed / door_open 스프라이트를 전환한다.
    /// </summary>
    public class DoorController : MonoBehaviour
    {
        private SpriteRenderer _spriteRenderer;
        private BoxCollider2D _collider;
        private bool _isOpen;

        /// <summary>문의 열림 상태.</summary>
        public bool IsOpen => _isOpen;

        void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _collider = GetComponent<BoxCollider2D>();
        }

        /// <summary>
        /// 문을 연다. 스프라이트 전환 + 콜라이더 비활성화.
        /// </summary>
        public void Open()
        {
            if (_isOpen) return;
            _isOpen = true;

            if (_spriteRenderer != null)
                _spriteRenderer.sprite = SpriteFactory.GetSprite("door_open");

            if (_collider != null)
                _collider.enabled = false;
        }

        /// <summary>
        /// 문을 닫는다. 스프라이트 전환 + 콜라이더 활성화.
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;

            if (_spriteRenderer != null)
                _spriteRenderer.sprite = SpriteFactory.GetSprite("door_closed");

            if (_collider != null)
                _collider.enabled = true;
        }

        /// <summary>
        /// 토글.
        /// </summary>
        public void Toggle()
        {
            if (_isOpen) Close();
            else Open();
        }
    }
}
