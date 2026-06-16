using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SoulCraft.Core;
using SoulCraft.Enemy;

namespace SoulCraft.UI
{
    /// <summary>
    /// 보스전 시 화면 상단에 표시되는 보스 HP바.
    /// 실제 HP 바 + 딜레이(트레일) HP 바 이중 구조로 자연스러운 감소 연출.
    /// 페이즈별 색상 변화, BossPhaseChangeEvent 구독.
    /// </summary>
    public class BossHPBar : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────
        [Header("UI References")]
        [SerializeField] private GameObject _bossHPPanel;
        [SerializeField] private Slider _hpSlider;
        [SerializeField] private Slider _delayHpSlider;
        [SerializeField] private Image _hpFillImage;
        [SerializeField] private Image _delayFillImage;
        [SerializeField] private TMP_Text _bossNameText;
        [SerializeField] private TMP_Text _phaseText;

        [Header("Phase Colors")]
        [SerializeField] private Color _phaseColor1 = new Color(0.2f, 0.9f, 0.3f);   // Phase 1: 초록
        [SerializeField] private Color _phaseColor2 = new Color(0.95f, 0.85f, 0.1f);  // Phase 2: 노랑
        [SerializeField] private Color _phaseColor3 = new Color(0.95f, 0.2f, 0.15f);  // Phase 3: 빨강
        [SerializeField] private Color _delayBarColor = new Color(1f, 0.6f, 0.2f, 0.8f);

        [Header("Animation")]
        [SerializeField] private float _delaySpeed = 0.4f;
        [SerializeField] private float _delayWait = 0.5f;
        [SerializeField] private float _showAnimDuration = 0.5f;

        // ── Runtime ──────────────────────────────────────────
        private BossBase _currentBoss;
        private float _targetHpRatio;
        private float _delayHpRatio;
        private float _delayWaitTimer;
        private int _currentPhase;
        private bool _isVisible;

        private CanvasGroup _canvasGroup;

        // ============================================================
        //  Unity Lifecycle
        // ============================================================

        void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null && _bossHPPanel != null)
                _canvasGroup = _bossHPPanel.GetComponent<CanvasGroup>();
        }

        void Start()
        {
            // 초기 숨김
            SetVisible(false);

            // 이벤트 구독
            GameEventSystem.Subscribe<BossPhaseChangeEvent>(OnBossPhaseChanged);
            GameEventSystem.Subscribe<DamageEvent>(OnDamageEvent);
            GameEventSystem.Subscribe<EnemyDeathEvent>(OnEnemyDeath);
        }

        void Update()
        {
            if (!_isVisible || _currentBoss == null) return;

            UpdateHpRatio();
            AnimateDelayBar();
        }

        void OnDestroy()
        {
            GameEventSystem.Unsubscribe<BossPhaseChangeEvent>(OnBossPhaseChanged);
            GameEventSystem.Unsubscribe<DamageEvent>(OnDamageEvent);
            GameEventSystem.Unsubscribe<EnemyDeathEvent>(OnEnemyDeath);
        }

        // ============================================================
        //  Public API
        // ============================================================

        /// <summary>
        /// 보스전 시작 시 호출. 보스 HP바를 표시하고 추적을 시작한다.
        /// </summary>
        public void ShowBossHP(BossBase boss)
        {
            if (boss == null) return;

            _currentBoss = boss;
            _currentPhase = 0;
            _targetHpRatio = 1f;
            _delayHpRatio = 1f;
            _delayWaitTimer = 0f;

            // 보스 이름 표시
            if (_bossNameText != null)
            {
                string bossName = boss.Data != null ? boss.Data.enemyName : "BOSS";
                _bossNameText.text = bossName;
            }

            // 슬라이더 초기화
            if (_hpSlider != null) _hpSlider.value = 1f;
            if (_delayHpSlider != null) _delayHpSlider.value = 1f;

            // 페이즈 색상 초기화
            ApplyPhaseColor(0);

            // 딜레이 바 색상
            if (_delayFillImage != null)
                _delayFillImage.color = _delayBarColor;

            SetVisible(true);
        }

        /// <summary>
        /// 보스 HP바를 숨긴다.
        /// </summary>
        public void HideBossHP()
        {
            _currentBoss = null;
            SetVisible(false);
        }

        // ============================================================
        //  HP Tracking
        // ============================================================

        private void UpdateHpRatio()
        {
            if (_currentBoss == null || _currentBoss.IsDead)
            {
                _targetHpRatio = 0f;
            }
            else
            {
                int maxHp = _currentBoss.MaxHp;
                _targetHpRatio = maxHp > 0 ? (float)_currentBoss.CurrentHp / maxHp : 0f;
            }

            // 실제 HP바는 즉시 반영
            if (_hpSlider != null)
                _hpSlider.value = _targetHpRatio;
        }

        // ============================================================
        //  Delay Bar Animation
        // ============================================================

        private void AnimateDelayBar()
        {
            if (_delayHpSlider == null) return;

            // 딜레이 바가 실제 HP보다 높으면 대기 후 천천히 감소
            if (_delayHpRatio > _targetHpRatio)
            {
                if (_delayWaitTimer > 0f)
                {
                    _delayWaitTimer -= Time.deltaTime;
                }
                else
                {
                    _delayHpRatio = Mathf.MoveTowards(
                        _delayHpRatio,
                        _targetHpRatio,
                        _delaySpeed * Time.deltaTime
                    );
                }
            }
            else
            {
                // 힐 등으로 실제 HP가 올라가면 즉시 맞춤
                _delayHpRatio = _targetHpRatio;
            }

            _delayHpSlider.value = _delayHpRatio;
        }

        // ============================================================
        //  Phase Color
        // ============================================================

        private void ApplyPhaseColor(int phase)
        {
            _currentPhase = phase;

            Color color = phase switch
            {
                0 => _phaseColor1,
                1 => _phaseColor2,
                _ => _phaseColor3
            };

            if (_hpFillImage != null)
                _hpFillImage.color = color;

            if (_phaseText != null)
                _phaseText.text = $"Phase {phase + 1}";
        }

        /// <summary>
        /// HP 비율에 따라 색상을 자동으로 결정한다.
        /// </summary>
        private Color GetColorByHpRatio(float ratio)
        {
            if (ratio > 0.6f)
                return _phaseColor1;
            else if (ratio > 0.3f)
                return Color.Lerp(_phaseColor2, _phaseColor1, (ratio - 0.3f) / 0.3f);
            else
                return Color.Lerp(_phaseColor3, _phaseColor2, ratio / 0.3f);
        }

        // ============================================================
        //  Visibility
        // ============================================================

        private void SetVisible(bool visible)
        {
            _isVisible = visible;

            if (_bossHPPanel != null)
                _bossHPPanel.SetActive(visible);

            if (_canvasGroup != null)
                _canvasGroup.alpha = visible ? 1f : 0f;
        }

        // ============================================================
        //  Event Handlers
        // ============================================================

        private void OnBossPhaseChanged(BossPhaseChangeEvent evt)
        {
            ApplyPhaseColor(evt.NewPhase);

            // 페이즈 전환 시 딜레이 바 대기 리셋
            _delayWaitTimer = _delayWait;
        }

        private void OnDamageEvent(DamageEvent evt)
        {
            if (_currentBoss == null) return;

            // 보스가 피격당한 경우 딜레이 대기 시작
            if (evt.Target == _currentBoss.gameObject)
            {
                _delayWaitTimer = _delayWait;
            }
        }

        private void OnEnemyDeath(EnemyDeathEvent evt)
        {
            if (_currentBoss == null) return;

            if (evt.Enemy == _currentBoss.gameObject)
            {
                // 보스 사망 시: HP바를 0으로 설정 후 일정 시간 후 숨김
                _targetHpRatio = 0f;
                if (_hpSlider != null) _hpSlider.value = 0f;
                _delayWaitTimer = 0f;

                Invoke(nameof(HideBossHP), 2f);
            }
        }
    }
}
