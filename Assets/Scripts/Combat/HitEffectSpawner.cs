using UnityEngine;
using SoulCraft.Core;

namespace SoulCraft.Combat
{
    /// <summary>
    /// DamageEvent를 구독하여 타격 위치에 속성별 히트 이펙트 프리팹을 ObjectPool로 스폰한다.
    /// 크리티컬 시 확대 이펙트 + 추가 파티클을 스폰하며, DamagePopup과 연동한다.
    /// </summary>
    public class HitEffectSpawner : MonoBehaviour
    {
        // ── Pool Keys ─────────────────────────────────────────
        public const string PoolKeyPhysical  = "HitFX_Physical";
        public const string PoolKeyFire      = "HitFX_Fire";
        public const string PoolKeyIce       = "HitFX_Ice";
        public const string PoolKeyLightning = "HitFX_Lightning";
        public const string PoolKeyDark      = "HitFX_Dark";
        public const string PoolKeyHoly      = "HitFX_Holy";
        public const string PoolKeyCritExtra = "HitFX_CritExtra";

        // ── Prefabs ───────────────────────────────────────────
        [Header("Element Hit Effect Prefabs")]
        [Tooltip("물리: 슬래시 이펙트")]
        [SerializeField] private GameObject _physicalHitPrefab;
        [Tooltip("화염: 화염 파티클")]
        [SerializeField] private GameObject _fireHitPrefab;
        [Tooltip("빙결: 얼음 파편")]
        [SerializeField] private GameObject _iceHitPrefab;
        [Tooltip("전기: 전기 스파크")]
        [SerializeField] private GameObject _lightningHitPrefab;
        [Tooltip("암흑: 어둠 파동")]
        [SerializeField] private GameObject _darkHitPrefab;
        [Tooltip("신성: 빛 폭발")]
        [SerializeField] private GameObject _holyHitPrefab;

        [Header("Critical Extra")]
        [Tooltip("크리티컬 시 추가 스폰되는 파티클")]
        [SerializeField] private GameObject _critExtraPrefab;

        [Header("Pool Settings")]
        [SerializeField] private int _initialPoolSize = 8;

        [Header("Effect Settings")]
        [Tooltip("일반 히트 이펙트 스케일")]
        [SerializeField] private float _normalScale = 1f;
        [Tooltip("크리티컬 히트 이펙트 스케일")]
        [SerializeField] private float _criticalScale = 1.6f;
        [Tooltip("이펙트 자동 반환 시간")]
        [SerializeField] private float _effectLifetime = 0.6f;
        [Tooltip("크리티컬 추가 파티클 반환 시간")]
        [SerializeField] private float _critExtraLifetime = 0.8f;

        [Header("Rotation")]
        [Tooltip("히트 이펙트에 공격 방향 기반 회전을 적용")]
        [SerializeField] private bool _rotateToAttackDirection = true;

        // ============================================================
        //  Unity Lifecycle
        // ============================================================

        void Start()
        {
            RegisterPools();
        }

        void OnEnable()
        {
            GameEventSystem.Subscribe<DamageEvent>(OnDamageEvent);
        }

        void OnDisable()
        {
            GameEventSystem.Unsubscribe<DamageEvent>(OnDamageEvent);
        }

        // ============================================================
        //  Pool Registration
        // ============================================================

        private void RegisterPools()
        {
            if (ObjectPool.Instance == null) return;

            TryRegister(PoolKeyPhysical, _physicalHitPrefab);
            TryRegister(PoolKeyFire, _fireHitPrefab);
            TryRegister(PoolKeyIce, _iceHitPrefab);
            TryRegister(PoolKeyLightning, _lightningHitPrefab);
            TryRegister(PoolKeyDark, _darkHitPrefab);
            TryRegister(PoolKeyHoly, _holyHitPrefab);
            TryRegister(PoolKeyCritExtra, _critExtraPrefab);
        }

        private void TryRegister(string key, GameObject prefab)
        {
            if (prefab != null)
                ObjectPool.Instance.RegisterPool(key, prefab, _initialPoolSize);
        }

        // ============================================================
        //  Event Handler
        // ============================================================

        private void OnDamageEvent(DamageEvent evt)
        {
            // 히트 이펙트 스폰
            SpawnHitEffect(evt);

            // 크리티컬이면 추가 파티클
            if (evt.IsCritical)
                SpawnCriticalExtra(evt.HitPoint);
        }

        // ============================================================
        //  Spawning
        // ============================================================

        private void SpawnHitEffect(DamageEvent evt)
        {
            if (ObjectPool.Instance == null) return;

            string poolKey = GetPoolKey(evt.Type);
            Quaternion rotation = GetHitRotation(evt);
            float scale = evt.IsCritical ? _criticalScale : _normalScale;

            GameObject fx = ObjectPool.Instance.Spawn(poolKey, evt.HitPoint, rotation);
            if (fx == null) return;

            // 스케일 적용
            fx.transform.localScale = Vector3.one * scale;

            // 자동 반환
            ObjectPool.Instance.Despawn(poolKey, fx, _effectLifetime);
        }

        private void SpawnCriticalExtra(Vector2 hitPoint)
        {
            if (ObjectPool.Instance == null || _critExtraPrefab == null) return;

            GameObject fx = ObjectPool.Instance.Spawn(
                PoolKeyCritExtra,
                hitPoint,
                Quaternion.identity);

            if (fx == null) return;

            fx.transform.localScale = Vector3.one * _criticalScale;
            ObjectPool.Instance.Despawn(PoolKeyCritExtra, fx, _critExtraLifetime);
        }

        // ============================================================
        //  Helpers
        // ============================================================

        private string GetPoolKey(DamageType type)
        {
            return type switch
            {
                DamageType.Fire      => PoolKeyFire,
                DamageType.Ice       => PoolKeyIce,
                DamageType.Lightning => PoolKeyLightning,
                DamageType.Dark      => PoolKeyDark,
                DamageType.Holy      => PoolKeyHoly,
                _                    => PoolKeyPhysical,
            };
        }

        private Quaternion GetHitRotation(DamageEvent evt)
        {
            if (!_rotateToAttackDirection) return Quaternion.identity;
            if (evt.Attacker == null || evt.Target == null) return Quaternion.identity;

            Vector2 dir = (Vector2)evt.Target.transform.position
                        - (Vector2)evt.Attacker.transform.position;

            if (dir.sqrMagnitude < 0.001f) return Quaternion.identity;

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            // 약간의 랜덤 흔들림
            angle += Random.Range(-15f, 15f);
            return Quaternion.Euler(0f, 0f, angle);
        }
    }
}
