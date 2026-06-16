using UnityEngine;

namespace SoulCraft.Core
{
    public class CameraController : MonoBehaviour
    {
        public static CameraController Instance { get; private set; }

        [Header("Follow")]
        [SerializeField] private Transform _target;
        [SerializeField] private float _followSpeed = 8f;
        [SerializeField] private Vector3 _offset = new(0, 0, -10);

        [Header("Bounds")]
        [SerializeField] private bool _useBounds;
        [SerializeField] private Vector2 _boundsMin;
        [SerializeField] private Vector2 _boundsMax;

        // Screen Shake
        private float _shakeTimer;
        private float _shakeIntensity;
        private Vector3 _shakeOffset;

        void Awake()
        {
            Instance = this;
        }

        void LateUpdate()
        {
            if (_target == null) return;

            Vector3 desired = _target.position + _offset;

            if (_useBounds)
            {
                desired.x = Mathf.Clamp(desired.x, _boundsMin.x, _boundsMax.x);
                desired.y = Mathf.Clamp(desired.y, _boundsMin.y, _boundsMax.y);
            }

            transform.position = Vector3.Lerp(transform.position, desired, _followSpeed * Time.deltaTime);

            // Shake
            if (_shakeTimer > 0)
            {
                _shakeTimer -= Time.unscaledDeltaTime;
                _shakeOffset = Random.insideUnitCircle * _shakeIntensity;
                transform.position += (Vector3)_shakeOffset;

                _shakeIntensity = Mathf.Lerp(_shakeIntensity, 0, Time.unscaledDeltaTime * 20f);
                if (_shakeTimer <= 0) _shakeIntensity = 0;
            }
        }

        public void SetTarget(Transform target) => _target = target;

        public void SetBounds(Vector2 min, Vector2 max)
        {
            _useBounds = true;
            _boundsMin = min;
            _boundsMax = max;
        }

        public void Shake(float intensity = 0.3f, float duration = 0.2f)
        {
            _shakeIntensity = intensity;
            _shakeTimer = duration;
        }

        /// <summary>
        /// 보스전 히트 시 강한 흔들림
        /// </summary>
        public void HeavyShake() => Shake(0.6f, 0.35f);
    }
}
