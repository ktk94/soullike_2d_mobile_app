using UnityEngine;
using SoulCraft.Core;

namespace SoulCraft.Combat
{
    /// <summary>
    /// 범용 2D 투사체.
    /// 방향, 속도, 데미지, 관통 여부, 수명을 설정할 수 있다.
    /// Rigidbody2D(Kinematic) + Collider2D(IsTrigger)를 필요로 한다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class Projectile : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float _speed = 10f;

        [Header("Damage")]
        [SerializeField] private int _damage = 10;
        [SerializeField] private float _skillMultiplier = 1f;
        [SerializeField] private DamageType _element = DamageType.Physical;

        [Header("Behavior")]
        [SerializeField] private bool _piercing;
        [SerializeField] private int _maxPierceCount = 3;
        [SerializeField] private float _lifetime = 5f;

        [Header("Visuals")]
        [SerializeField] private GameObject _hitEffectPrefab;

        [Header("Layer")]
        [SerializeField] private LayerMask _targetLayers;

        private Vector2 _direction = Vector2.right;
        private GameObject _owner;
        private int _pierceCount;
        private Rigidbody2D _rb;
        private float _spawnTime;

        // --- Unity Lifecycle ---

        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.gravityScale = 0f;
        }

        void OnEnable()
        {
            _spawnTime = Time.time;
            _pierceCount = 0;
        }

        void Update()
        {
            // 수명 체크
            if (Time.time - _spawnTime >= _lifetime)
            {
                DestroyProjectile();
                return;
            }
        }

        void FixedUpdate()
        {
            _rb.MovePosition(_rb.position + _direction * (_speed * Time.fixedDeltaTime));
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            // 발사자 자신은 무시
            if (_owner != null && other.gameObject == _owner) return;

            // 타겟 레이어 체크
            if ((_targetLayers.value & (1 << other.gameObject.layer)) == 0) return;

            // 데미지 처리
            ApplyDamage(other);

            // 히트 이펙트 스폰
            SpawnHitEffect(other.ClosestPoint(transform.position));

            // 관통 처리
            if (_piercing)
            {
                _pierceCount++;
                if (_pierceCount >= _maxPierceCount)
                    DestroyProjectile();
            }
            else
            {
                DestroyProjectile();
            }
        }

        // --- Public API ---

        /// <summary>
        /// 투사체를 초기화한다. 스폰 직후 호출해야 한다.
        /// </summary>
        public void Initialize(
            Vector2 direction,
            GameObject owner,
            int damage,
            float skillMultiplier = 1f,
            DamageType element = DamageType.Physical)
        {
            _direction = direction.normalized;
            _owner = owner;
            _damage = damage;
            _skillMultiplier = skillMultiplier;
            _element = element;
            _spawnTime = Time.time;
            _pierceCount = 0;

            // 진행 방향으로 회전
            float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        /// <summary>
        /// 투사체의 속도를 설정한다.
        /// </summary>
        public void SetSpeed(float speed) => _speed = speed;

        /// <summary>
        /// 관통 여부를 설정한다.
        /// </summary>
        public void SetPiercing(bool piercing, int maxCount = 3)
        {
            _piercing = piercing;
            _maxPierceCount = maxCount;
        }

        /// <summary>
        /// 수명을 설정한다.
        /// </summary>
        public void SetLifetime(float lifetime) => _lifetime = lifetime;

        /// <summary>
        /// 히트 이펙트 프리팹을 설정한다.
        /// </summary>
        public void SetHitEffect(GameObject prefab) => _hitEffectPrefab = prefab;

        // --- Private ---

        private void ApplyDamage(Collider2D target)
        {
            // DamageEvent 발행 — 실제 데미지 적용은 대상의 Health 컴포넌트가 처리
            Vector2 hitPoint = target.ClosestPoint(transform.position);

            GameEventSystem.Publish(new DamageEvent
            {
                Attacker = _owner,
                Target = target.gameObject,
                Damage = Mathf.Max(1, Mathf.RoundToInt(_damage * _skillMultiplier)),
                IsCritical = false, // 투사체 자체는 크리티컬 판정을 하지 않음 (외부에서 설정 가능)
                Type = _element,
                HitPoint = hitPoint
            });
        }

        private void SpawnHitEffect(Vector2 position)
        {
            if (_hitEffectPrefab != null)
            {
                var fx = Instantiate(_hitEffectPrefab, position, Quaternion.identity);
                Destroy(fx, 2f);
            }
        }

        private void DestroyProjectile()
        {
            Destroy(gameObject);
        }
    }
}
