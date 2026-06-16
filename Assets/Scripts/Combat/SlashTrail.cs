using System.Collections;
using UnityEngine;
using SoulCraft.Core;

namespace SoulCraft.Combat
{
    /// <summary>
    /// 공격 시 무기 궤적을 따라 잔상 효과를 생성한다.
    /// TrailRenderer 기반의 무기 궤적 + LineRenderer 기반의 콤보 피니셔 궤적.
    /// 콤보 단계에 따라 색상/두께가 변화하며, 피니셔에서 더 화려한 궤적을 연출한다.
    /// PlayerCombat 또는 무기 오브젝트에 부착한다.
    /// </summary>
    [RequireComponent(typeof(TrailRenderer))]
    public class SlashTrail : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────
        [Header("Trail Settings")]
        [Tooltip("트레일이 부착된 무기 팁 트랜스폼 (없으면 자기 자신)")]
        [SerializeField] private Transform _weaponTip;

        [Header("Combo Step Colors")]
        [SerializeField] private Color _combo1Color = new Color(0.9f, 0.9f, 1f, 0.8f);
        [SerializeField] private Color _combo2Color = new Color(0.6f, 0.8f, 1f, 0.9f);
        [SerializeField] private Color _combo3Color = new Color(0.3f, 0.5f, 1f, 1f);
        [SerializeField] private Color _finisherColor = new Color(1f, 0.6f, 0.1f, 1f);

        [Header("Combo Step Widths")]
        [SerializeField] private float _combo1Width = 0.08f;
        [SerializeField] private float _combo2Width = 0.12f;
        [SerializeField] private float _combo3Width = 0.18f;
        [SerializeField] private float _finisherWidth = 0.3f;

        [Header("Trail Time")]
        [Tooltip("일반 궤적 잔상 시간")]
        [SerializeField] private float _normalTrailTime = 0.12f;
        [Tooltip("피니셔 궤적 잔상 시간")]
        [SerializeField] private float _finisherTrailTime = 0.25f;

        [Header("Finisher Arc (LineRenderer)")]
        [Tooltip("피니셔에서 추가로 그려지는 호 궤적용 LineRenderer (선택)")]
        [SerializeField] private LineRenderer _arcRenderer;
        [Tooltip("호 궤적 세그먼트 수")]
        [SerializeField] private int _arcSegments = 20;
        [Tooltip("호 궤적 반지름")]
        [SerializeField] private float _arcRadius = 1.2f;
        [Tooltip("호 궤적 각도 (도)")]
        [SerializeField] private float _arcAngle = 180f;
        [Tooltip("호 궤적 표시 시간")]
        [SerializeField] private float _arcDuration = 0.3f;

        [Header("Finisher Particle")]
        [Tooltip("콤보 피니셔 시 추가 파티클 이펙트 (선택)")]
        [SerializeField] private ParticleSystem _finisherParticle;

        // ── Components ────────────────────────────────────────
        private TrailRenderer _trail;
        private Coroutine _arcCoroutine;

        // ── State ─────────────────────────────────────────────
        private bool _isActive;
        private int _currentComboStep;

        // ============================================================
        //  Unity Lifecycle
        // ============================================================

        void Awake()
        {
            _trail = GetComponent<TrailRenderer>();
            _trail.emitting = false;
            _trail.clear = true;

            if (_weaponTip == null)
                _weaponTip = transform;

            if (_arcRenderer != null)
            {
                _arcRenderer.positionCount = 0;
                _arcRenderer.enabled = false;
            }
        }

        void OnEnable()
        {
            GameEventSystem.Subscribe<ComboEvent>(OnComboEvent);
        }

        void OnDisable()
        {
            GameEventSystem.Unsubscribe<ComboEvent>(OnComboEvent);
            StopTrail();
        }

        // ============================================================
        //  Public API
        // ============================================================

        /// <summary>
        /// 공격 시작 시 호출. 콤보 단계(0-based)에 맞는 궤적을 활성화한다.
        /// </summary>
        public void StartTrail(int comboStep, bool isFinisher = false)
        {
            _currentComboStep = comboStep;
            _isActive = true;

            // 색상/두께 결정
            Color color;
            float width;
            float trailTime;

            if (isFinisher)
            {
                color = _finisherColor;
                width = _finisherWidth;
                trailTime = _finisherTrailTime;
            }
            else
            {
                GetComboVisuals(comboStep, out color, out width);
                trailTime = _normalTrailTime;
            }

            // TrailRenderer 설정
            _trail.time = trailTime;
            _trail.startWidth = width;
            _trail.endWidth = width * 0.2f;

            // Gradient 설정 (시작: 불투명 → 끝: 투명)
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new(color, 0f),
                    new(color * 1.2f, 0.3f),
                    new(color * 0.6f, 1f)
                },
                new GradientAlphaKey[]
                {
                    new(color.a, 0f),
                    new(color.a * 0.8f, 0.5f),
                    new(0f, 1f)
                }
            );
            _trail.colorGradient = gradient;

            _trail.Clear();
            _trail.emitting = true;

            // 피니셔: 호 궤적 + 파티클
            if (isFinisher)
            {
                if (_arcCoroutine != null) StopCoroutine(_arcCoroutine);
                _arcCoroutine = StartCoroutine(DrawFinisherArc(color));

                if (_finisherParticle != null)
                {
                    var main = _finisherParticle.main;
                    main.startColor = color;
                    _finisherParticle.Play();
                }
            }
        }

        /// <summary>
        /// 공격 종료 시 호출. 궤적 발광을 중지한다.
        /// </summary>
        public void StopTrail()
        {
            _isActive = false;
            _trail.emitting = false;
        }

        // ============================================================
        //  Event Handler
        // ============================================================

        /// <summary>
        /// ComboEvent를 받아 피니셔 콤보 시 자동으로 궤적 강화를 트리거.
        /// </summary>
        private void OnComboEvent(ComboEvent evt)
        {
            if (evt.ComboName == "피니셔" || evt.BonusDamageMultiplier >= 2.0f)
            {
                StartTrail(_currentComboStep, isFinisher: true);
            }
        }

        // ============================================================
        //  Finisher Arc
        // ============================================================

        private IEnumerator DrawFinisherArc(Color color)
        {
            if (_arcRenderer == null) yield break;

            _arcRenderer.enabled = true;
            _arcRenderer.positionCount = _arcSegments;
            _arcRenderer.startWidth = _finisherWidth * 1.5f;
            _arcRenderer.endWidth = _finisherWidth * 0.3f;

            // Arc color
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new(color, 0f),
                    new(Color.white, 0.5f),
                    new(color, 1f)
                },
                new GradientAlphaKey[]
                {
                    new(1f, 0f),
                    new(0.8f, 0.5f),
                    new(0f, 1f)
                }
            );
            _arcRenderer.colorGradient = gradient;

            // 호 궤적 계산 (무기 팁 기준)
            Vector3 center = _weaponTip.position;
            float baseAngle = Mathf.Atan2(
                _weaponTip.up.y,
                _weaponTip.up.x) * Mathf.Rad2Deg;

            float startAngle = baseAngle - _arcAngle * 0.5f;
            float angleStep = _arcAngle / (_arcSegments - 1);

            for (int i = 0; i < _arcSegments; i++)
            {
                float angle = (startAngle + angleStep * i) * Mathf.Deg2Rad;
                Vector3 point = center + new Vector3(
                    Mathf.Cos(angle) * _arcRadius,
                    Mathf.Sin(angle) * _arcRadius,
                    0f);
                _arcRenderer.SetPosition(i, point);
            }

            // 페이드 아웃
            float elapsed = 0f;
            Color startColor = color;

            while (elapsed < _arcDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha = 1f - (elapsed / _arcDuration);

                var fadeGradient = new Gradient();
                fadeGradient.SetKeys(
                    gradient.colorKeys,
                    new GradientAlphaKey[]
                    {
                        new(alpha, 0f),
                        new(alpha * 0.8f, 0.5f),
                        new(0f, 1f)
                    }
                );
                _arcRenderer.colorGradient = fadeGradient;

                yield return null;
            }

            _arcRenderer.enabled = false;
            _arcRenderer.positionCount = 0;
            _arcCoroutine = null;
        }

        // ============================================================
        //  Helpers
        // ============================================================

        private void GetComboVisuals(int step, out Color color, out float width)
        {
            switch (step)
            {
                case 0:
                    color = _combo1Color;
                    width = _combo1Width;
                    break;
                case 1:
                    color = _combo2Color;
                    width = _combo2Width;
                    break;
                case 2:
                    color = _combo3Color;
                    width = _combo3Width;
                    break;
                default:
                    color = _combo3Color;
                    width = _combo3Width;
                    break;
            }
        }
    }
}
