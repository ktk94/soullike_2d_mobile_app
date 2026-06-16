using System.Collections.Generic;
using UnityEngine;
using SoulCraft.Core;
using SoulCraft.Player;
using SoulCraft.Combat;
using SoulCraft.Farming;
using SoulCraft.UI;
using SoulCraft.World;

namespace SoulCraft.Factory
{
    /// <summary>
    /// 씬에 이 하나만 배치하면 게임 전체가 자동으로 구성되는 부트스트래퍼.
    /// Awake에서 모든 핵심 시스템을 초기화하고, Start에서 테스트 스테이지를 생성한다.
    /// </summary>
    public class SceneBootstrapper : MonoBehaviour
    {
        // ================================================================
        //  Auto-run option (Awake 또는 RuntimeInitialize)
        // ================================================================

        [Header("Settings")]
        [Tooltip("true이면 Awake 실행 시 자동 부트스트랩. false이면 수동 호출.")]
        [SerializeField] private bool _autoBootstrap = true;

        [Header("Camera")]
        [SerializeField] private float _cameraSize = 5.5f;
        [SerializeField] private Color _cameraBgColor = new(0.05f, 0.05f, 0.08f, 1f);

        [Header("Test Stage")]
        [SerializeField] private bool _generateTestStage = true;

        // ── Runtime References ──
        private GameObject _playerGo;
        private PlayerController _playerController;
        private CameraController _cameraController;

        // ================================================================
        //  RuntimeInitializeOnLoadMethod (선택적 자동 실행)
        // ================================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnBeforeSceneLoad()
        {
            // SceneBootstrapper가 씬에 이미 있으면 Awake가 알아서 처리.
            // 없으면 여기서 생성.
            // (씬에 수동 배치된 경우와 충돌 방지를 위해 지연 체크)
        }

        // ================================================================
        //  Awake - 핵심 시스템 초기화
        // ================================================================

        void Awake()
        {
            if (!_autoBootstrap) return;

            Bootstrap();
        }

        /// <summary>
        /// 전체 시스템 부트스트랩을 실행한다. 외부에서 수동 호출도 가능.
        /// </summary>
        public void Bootstrap()
        {
            Debug.Log("[SceneBootstrapper] === 부트스트랩 시작 ===");

            // 1. SpriteFactory 초기화 (존재하는 경우)
            InitializeSpriteFactory();

            // 2. 핵심 매니저 생성
            CreateCoreManagers();

            // 3. 카메라 설정
            SetupCamera();

            // 4. PrefabFactory 초기화 (존재하는 경우)
            InitializePrefabFactory();

            // 5. ParticleFactory 초기화 (존재하는 경우)
            InitializeParticleFactory();

            // 6. UIFactory로 전체 UI 생성
            UIFactory.BuildAll();

            // 7. UI 컴포넌트 와이어링
            WireUIComponents();

            // 8. 전투 피드백 시스템 생성
            CreateCombatFeedbackSystems();

            // 9. 콤보 시스템, DamagePopupSpawner 등 생성
            CreateCombatSupportSystems();

            Debug.Log("[SceneBootstrapper] === 부트스트랩 완료 ===");
        }

        // ================================================================
        //  Start - 테스트 스테이지 생성
        // ================================================================

        void Start()
        {
            if (!_generateTestStage) return;

            Debug.Log("[SceneBootstrapper] 테스트 스테이지 생성 시작...");

            // 1. 테스트 스테이지 생성
            var stage = SimpleRoom.GenerateTestStage(
                out Vector2 playerSpawnPos,
                out List<Vector2> enemySpawnPositions,
                out List<Vector2> doorPositions);

            // 2. 플레이어 배치
            SpawnPlayer(playerSpawnPos);

            // 3. 적 배치
            SpawnEnemies(enemySpawnPositions);

            // 4. ObjectPool에 투사체, 이펙트, 데미지 팝업 등록
            RegisterObjectPools();

            // 5. 카메라 타겟 설정
            if (_cameraController != null && _playerGo != null)
            {
                _cameraController.SetTarget(_playerGo.transform);
            }

            // 6. MobileInputUI -> PlayerController 연결
            ConnectMobileInput();

            // 7. GameManager 상태를 Playing으로
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ChangeState(GameState.Playing);
            }

            // 8. HUD 초기 갱신
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.RefreshAllUI();
            }

            Debug.Log("[SceneBootstrapper] 테스트 스테이지 생성 완료.");
        }

        // ================================================================
        //  Step 1: SpriteFactory
        // ================================================================

        private void InitializeSpriteFactory()
        {
            // SpriteFactory는 static 클래스, GetSprite 호출 시 자동 lazy-init.
            // 미리 자주 쓰는 키 몇 개를 요청하여 캐시를 워밍한다.
            SpriteFactory.GetSprite("player_idle_down_0");
            SpriteFactory.GetSprite("ui_btn_normal");
            SpriteFactory.GetSprite("ui_btn_pressed");
            Debug.Log("[SceneBootstrapper] SpriteFactory 캐시 워밍 완료.");
        }

        // ================================================================
        //  Step 2: Core Managers
        // ================================================================

        private void CreateCoreManagers()
        {
            // GameManager
            if (GameManager.Instance == null)
            {
                var go = new GameObject("GameManager");
                go.AddComponent<GameManager>();
                DontDestroyOnLoad(go);
                Debug.Log("[SceneBootstrapper] GameManager 생성.");
            }

            // SaveManager
            if (SaveManager.Instance == null)
            {
                var go = new GameObject("SaveManager");
                go.AddComponent<SaveManager>();
                DontDestroyOnLoad(go);
                Debug.Log("[SceneBootstrapper] SaveManager 생성.");
            }

            // ObjectPool
            if (ObjectPool.Instance == null)
            {
                var go = new GameObject("ObjectPool");
                go.AddComponent<ObjectPool>();
                Debug.Log("[SceneBootstrapper] ObjectPool 생성.");
            }

            // Inventory
            if (Inventory.Instance == null)
            {
                var go = new GameObject("Inventory");
                go.AddComponent<Inventory>();
                Debug.Log("[SceneBootstrapper] Inventory 생성.");
            }
        }

        // ================================================================
        //  Step 3: Camera Setup
        // ================================================================

        private void SetupCamera()
        {
            var mainCam = Camera.main;
            if (mainCam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                mainCam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
                Debug.Log("[SceneBootstrapper] Main Camera 생성.");
            }

            // Camera 설정
            mainCam.orthographic = true;
            mainCam.orthographicSize = _cameraSize;
            mainCam.backgroundColor = _cameraBgColor;
            mainCam.clearFlags = CameraClearFlags.SolidColor;

            // CameraController 추가
            _cameraController = mainCam.GetComponent<CameraController>();
            if (_cameraController == null)
            {
                _cameraController = mainCam.gameObject.AddComponent<CameraController>();
                Debug.Log("[SceneBootstrapper] CameraController 추가.");
            }
        }

        // ================================================================
        //  Step 4: PrefabFactory
        // ================================================================

        private void InitializePrefabFactory()
        {
            // PrefabFactory.PrewarmAll()로 모든 기본 프리팹을 미리 캐싱한다.
            PrefabFactory.PrewarmAll();
            Debug.Log("[SceneBootstrapper] PrefabFactory 프리워밍 완료.");
        }

        // ================================================================
        //  Step 5: ParticleFactory
        // ================================================================

        private void InitializeParticleFactory()
        {
            // ParticleFactory는 static 클래스, 개별 메서드 호출 시 생성.
            // 별도 초기화 불필요. 존재 확인만 한다.
            Debug.Log("[SceneBootstrapper] ParticleFactory 준비 완료 (lazy-init).");
        }

        // ================================================================
        //  Step 7: UI Component Wiring
        // ================================================================

        private void WireUIComponents()
        {
            // HUDManager 와이어링
            UIFactory.WireHUDManager();
            Debug.Log("[SceneBootstrapper] HUDManager 와이어링 완료.");

            // BossHPBar 와이어링
            UIFactory.WireBossHPBar();
            Debug.Log("[SceneBootstrapper] BossHPBar 와이어링 완료.");

            // ResultScreenUI 와이어링
            UIFactory.WireResultScreen();
            Debug.Log("[SceneBootstrapper] ResultScreenUI 와이어링 완료.");
        }

        // ================================================================
        //  Step 8: Combat Feedback Systems
        // ================================================================

        private void CreateCombatFeedbackSystems()
        {
            // HitFeedback
            var hitFeedbackGo = new GameObject("HitFeedback");
            hitFeedbackGo.AddComponent<HitFeedback>();
            Debug.Log("[SceneBootstrapper] HitFeedback 생성.");

            // ImpactSystem
            if (ImpactSystem.Instance == null)
            {
                var impactGo = new GameObject("ImpactSystem");
                impactGo.AddComponent<ImpactSystem>();
                Debug.Log("[SceneBootstrapper] ImpactSystem 생성.");
            }

            // KnockbackSystem
            if (KnockbackSystem.Instance == null)
            {
                var knockbackGo = new GameObject("KnockbackSystem");
                knockbackGo.AddComponent<KnockbackSystem>();
                Debug.Log("[SceneBootstrapper] KnockbackSystem 생성.");
            }
        }

        // ================================================================
        //  Step 9: Combat Support Systems
        // ================================================================

        private void CreateCombatSupportSystems()
        {
            // DamagePopupSpawner (프리팹 없이는 제한적이지만 컴포넌트는 준비)
            var popupSpawnerGo = new GameObject("DamagePopupSpawner");
            popupSpawnerGo.AddComponent<DamagePopupSpawner>();
            Debug.Log("[SceneBootstrapper] DamagePopupSpawner 생성.");

            // HitEffectSpawner
            var hitEffectGo = new GameObject("HitEffectSpawner");
            hitEffectGo.AddComponent<HitEffectSpawner>();
            Debug.Log("[SceneBootstrapper] HitEffectSpawner 생성.");

            // CombatParticleManager
            if (CombatParticleManager.Instance == null)
            {
                var particleMgrGo = new GameObject("CombatParticleManager");
                particleMgrGo.AddComponent<CombatParticleManager>();
                Debug.Log("[SceneBootstrapper] CombatParticleManager 생성.");
            }
        }

        // ================================================================
        //  Player Spawn
        // ================================================================

        private void SpawnPlayer(Vector2 position)
        {
            _playerGo = SimpleRoom.CreateSimplePlayer(position);

            // PlayerStats
            var stats = _playerGo.AddComponent<PlayerStats>();

            // PlayerCombat
            var combat = _playerGo.AddComponent<PlayerCombat>();

            // PlayerAnimator
            var playerAnim = _playerGo.AddComponent<PlayerAnimator>();

            // PlayerController
            _playerController = _playerGo.AddComponent<PlayerController>();

            // SpriteRenderer 참조를 PlayerController에 설정
            var sr = _playerGo.GetComponent<SpriteRenderer>();
            SetPrivateField(_playerController, "_spriteRenderer", sr);

            // SkillManager
            var skillMgr = _playerGo.AddComponent<SkillManager>();

            // ComboSystem
            var comboSystem = _playerGo.AddComponent<ComboSystem>();
            SetPrivateField(skillMgr, "_comboSystem", comboSystem);

            // Equipment
            _playerGo.AddComponent<Equipment>();

            Debug.Log($"[SceneBootstrapper] 플레이어 생성: {position}");
        }

        // ================================================================
        //  Enemy Spawn
        // ================================================================

        private void SpawnEnemies(List<Vector2> positions)
        {
            string[] enemyTypes = { "slime", "skeleton" };

            for (int i = 0; i < positions.Count; i++)
            {
                string type = enemyTypes[Random.Range(0, enemyTypes.Length)];
                var enemyGo = SimpleRoom.CreateSimpleEnemy(type, positions[i]);

                // EnemyData를 런타임 ScriptableObject로 생성
                var enemyData = ScriptableObject.CreateInstance<EnemyData>();
                enemyData.enemyId = $"{type}_{i}";
                enemyData.enemyName = type == "slime" ? "슬라임" : "해골 전사";
                enemyData.isBoss = false;
                enemyData.maxHp = type == "slime" ? 30 : 50;
                enemyData.attack = type == "slime" ? 5 : 10;
                enemyData.defense = type == "slime" ? 1 : 3;
                enemyData.speed = type == "slime" ? 1.5f : 2.5f;
                enemyData.detectionRange = 5f;
                enemyData.attackRange = 1.2f;
                enemyData.expReward = type == "slime" ? 10 : 20;
                enemyData.goldReward = type == "slime" ? 5 : 12;

                // EnemyBase 추가 및 데이터 할당
                var enemyBase = enemyGo.AddComponent<EnemyBase>();
                SetPrivateField(enemyBase, "data", enemyData);

                // EnemyAI 추가
                var aiType = System.Type.GetType("SoulCraft.Enemy.EnemyAI");
                if (aiType != null)
                {
                    enemyGo.AddComponent(aiType);
                }

                Debug.Log($"[SceneBootstrapper] 적 생성: {type} at {positions[i]}");
            }
        }

        // ================================================================
        //  Object Pool Registration
        // ================================================================

        private void RegisterObjectPools()
        {
            if (ObjectPool.Instance == null) return;

            // DamagePopup 프리팹 - PrefabFactory가 이미 캐싱함
            var popupPrefab = PrefabFactory.CreateDamagePopup();
            ObjectPool.Instance.RegisterPool(DamagePopupSpawner.PoolKey, popupPrefab, 20);

            // DamagePopupSpawner에 프리팹 설정
            var spawner = FindAnyObjectByType<DamagePopupSpawner>();
            if (spawner != null)
            {
                SetPrivateField(spawner, "_popupPrefab", popupPrefab);
            }

            // 투사체 프리팹 등록
            var projectilePrefab = PrefabFactory.CreateProjectile();
            if (projectilePrefab != null)
            {
                ObjectPool.Instance.RegisterPool("Projectile", projectilePrefab, 15);
            }

            Debug.Log("[SceneBootstrapper] ObjectPool 등록 완료.");
        }

        // ================================================================
        //  Mobile Input Connection
        // ================================================================

        private void ConnectMobileInput()
        {
            if (UIFactory.MobileInputComponent == null || _playerController == null) return;

            // MobileInputUI는 Start에서 FindGameObjectWithTag("Player")로 자동 연결.
            // 여기서는 mobileOnly를 false로 설정 (이미 BuildAll에서 설정됨).

            Debug.Log("[SceneBootstrapper] MobileInput 연결 완료 (자동 탐색).");
        }

        // ================================================================
        //  Reflection Helper
        // ================================================================

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            if (target == null) return;

            var type = target.GetType();
            while (type != null)
            {
                var field = type.GetField(fieldName,
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }
                type = type.BaseType;
            }

            Debug.LogWarning($"[SceneBootstrapper] Field '{fieldName}' not found on {target.GetType().Name}");
        }
    }
}
