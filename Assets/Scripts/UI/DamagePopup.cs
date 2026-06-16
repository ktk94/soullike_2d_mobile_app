using UnityEngine;
using TMPro;
using SoulCraft.Core;

namespace SoulCraft.UI
{
    /// <summary>
    /// 데미지 숫자 팝업 (월드 스페이스 Canvas).
    /// 위로 떠오르며 페이드 아웃한다.
    /// 크리티컬: 더 큰 폰트, 다른 색상.
    /// 속성별 색상: Fire=빨강, Ice=파랑, Lightning=노랑, Dark=보라, Holy=흰색.
    /// 힐: 초록색. ObjectPool 사용.
    /// DamageEvent / PlayerHealEvent 구독하여 자동 생성.
    /// </summary>
    public class DamagePopup : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────
        [Header("References")]
        [SerializeField] private TMP_Text _damageText;
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("Movement")]
        [SerializeField] private float _floatSpeed = 1.5f;
        [SerializeField] private float _floatSpreadX = 0.3f;
        [SerializeField] private float _lifetime = 0.8f;

        [Header("Scale")]
        [SerializeField] private float _normalFontSize = 6f;
        [SerializeField] private float _criticalFontSize = 9f;
        [SerializeField] private float _healFontSize = 5f;
        [SerializeField] private float _critScalePunch = 1.4f;

        [Header("Colors")]
        [SerializeField] private Color _physicalColor = Color.white;
        [SerializeField] private Color _fireColor = new Color(1f, 0.3f, 0.1f);
        [SerializeField] private Color _iceColor = new Color(0.3f, 0.7f, 1f);
        [SerializeField] private Color _lightningColor = new Color(1f, 0.95f, 0.3f);
        [SerializeField] private Color _darkColor = new Color(0.6f, 0.2f, 0.9f);
        [SerializeField] private Color _holyColor = new Color(1f, 1f, 0.85f);
        [SerializeField] private Color _criticalColor = new Color(1f, 0.85f, 0f);
        [SerializeField] private Color _healColor = new Color(0.2f, 1f, 0.4f);

        // ── Runtime ──────────────────────────────────────────
        private float _timer;
        private float _randomOffsetX;
        private Vector3 _startScale;
        private bool _isCritical;

        // ============================================================
        //  Unity Lifecycle
        // ============================================================

        void OnEnable()
        {
            _timer = 0f;
            _randomOffsetX = Random.Range(-_floatSpreadX, _floatSpreadX);
            _startScale = transform.localScale;

            if (_canvasGroup != null)
                _canvasGroup.alpha = 1f;
        }

        void Update()
        {
            _timer += Time.deltaTime;
            float progress = _timer / _lifetime;

            // 위로 떠오르기 + 좌우 약간 흔들림
            Vector3 move = new Vector3(_randomOffsetX, _floatSpeed, 0f) * Time.deltaTime;
            transform.position += move;

            // 페이드 아웃 (마지막 40%에서 시작)
            if (_canvasGroup != null)
            {
                float fadeStart = 0.6f;
                if (progress > fadeStart)
                    _canvasGroup.alpha = 1f - (progress - fadeStart) / (1f - fadeStart);
            }

            // 크리티컬 스케일 펀치 (처음 20%에서 확대 후 복귀)
            if (_isCritical)
            {
                float scalePhase = Mathf.Clamp01(progress / 0.2f);
                float scaleMul = Mathf.Lerp(_critScalePunch, 1f, scalePhase);
                transform.localScale = _startScale * scaleMul;
            }

            // 수명 종료 시 풀로 반환
            if (_timer >= _lifetime)
            {
                ReturnToPool();
            }
        }

        // ============================================================
        //  Setup Methods
        // ============================================================

        /// <summary>
        /// 데미지 팝업을 초기화한다.
        /// </summary>
        public void Setup(int damage, bool isCritical, DamageType damageType)
        {
            _isCritical = isCritical;

            if (_damageText == null) return;

            _damageText.text = damage.ToString();
            _damageText.fontSize = isCritical ? _criticalFontSize : _normalFontSize;

            // 색상 결정: 크리티컬이면 크리티컬 색, 아니면 속성별 색
            _damageText.color = isCritical ? _criticalColor : GetElementColor(damageType);

            // 크리티컬 접두사
            if (isCritical)
                _damageText.text = $"CRIT! {damage}";
        }

        /// <summary>
        /// 힐 팝업을 초기화한다.
        /// </summary>
        public void SetupHeal(int amount)
        {
            _isCritical = false;

            if (_damageText == null) return;

            _damageText.text = $"+{amount}";
            _damageText.fontSize = _healFontSize;
            _damageText.color = _healColor;
        }

        // ============================================================
        //  Color Helpers
        // ============================================================

        private Color GetElementColor(DamageType type)
        {
            return type switch
            {
                DamageType.Fire => _fireColor,
                DamageType.Ice => _iceColor,
                DamageType.Lightning => _lightningColor,
                DamageType.Dark => _darkColor,
                DamageType.Holy => _holyColor,
                _ => _physicalColor,
            };
        }

        // ============================================================
        //  Pool
        // ============================================================

        private void ReturnToPool()
        {
            if (ObjectPool.Instance != null)
                ObjectPool.Instance.Despawn(DamagePopupSpawner.PoolKey, gameObject);
            else
                gameObject.SetActive(false);
        }
    }

    // ================================================================
    //  DamagePopupSpawner
    //  DamageEvent / PlayerHealEvent를 구독하여 DamagePopup을 자동 생성.
    // ================================================================

    /// <summary>
    /// DamageEvent와 PlayerHealEvent를 구독하여 DamagePopup을 ObjectPool로 생성한다.
    /// 씬에 하나 배치한다.
    /// </summary>
    public class DamagePopupSpawner : MonoBehaviour
    {
        public const string PoolKey = "DamagePopup";

        [Header("Setup")]
        [SerializeField] private GameObject _popupPrefab;
        [SerializeField] private int _initialPoolSize = 20;
        [SerializeField] private Vector3 _spawnOffset = new Vector3(0f, 0.5f, 0f);

        void Start()
        {
            // 풀 등록
            if (ObjectPool.Instance != null && _popupPrefab != null)
                ObjectPool.Instance.RegisterPool(PoolKey, _popupPrefab, _initialPoolSize);

            // 이벤트 구독
            GameEventSystem.Subscribe<DamageEvent>(OnDamageEvent);
            GameEventSystem.Subscribe<PlayerHealEvent>(OnPlayerHealEvent);
        }

        void OnDestroy()
        {
            GameEventSystem.Unsubscribe<DamageEvent>(OnDamageEvent);
            GameEventSystem.Unsubscribe<PlayerHealEvent>(OnPlayerHealEvent);
        }

        private void OnDamageEvent(DamageEvent evt)
        {
            Vector3 spawnPos = (Vector3)evt.HitPoint + _spawnOffset;
            SpawnDamagePopup(spawnPos, evt.Damage, evt.IsCritical, evt.Type);
        }

        private void OnPlayerHealEvent(PlayerHealEvent evt)
        {
            // 힐 팝업은 플레이어 위치에 생성
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            Vector3 spawnPos = player.transform.position + _spawnOffset;
            SpawnHealPopup(spawnPos, evt.Amount);
        }

        private void SpawnDamagePopup(Vector3 position, int damage, bool isCritical, DamageType type)
        {
            if (ObjectPool.Instance == null) return;

            GameObject obj = ObjectPool.Instance.Spawn(PoolKey, position, Quaternion.identity);
            if (obj == null) return;

            var popup = obj.GetComponent<DamagePopup>();
            if (popup != null)
                popup.Setup(damage, isCritical, type);
        }

        private void SpawnHealPopup(Vector3 position, int amount)
        {
            if (ObjectPool.Instance == null || amount <= 0) return;

            GameObject obj = ObjectPool.Instance.Spawn(PoolKey, position, Quaternion.identity);
            if (obj == null) return;

            var popup = obj.GetComponent<DamagePopup>();
            if (popup != null)
                popup.SetupHeal(amount);
        }
    }
}
