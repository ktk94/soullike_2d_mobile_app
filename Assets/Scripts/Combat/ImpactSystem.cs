using System.Collections;
using UnityEngine;
using SoulCraft.Core;

namespace SoulCraft.Combat
{
    /// <summary>
    /// 종합 타격 피드백 컨트롤러 (싱글톤).
    /// DamageEvent / EnemyDeathEvent를 구독하여 HitStop, ScreenShake, CameraZoomPunch,
    /// SlowMotion, 흰색 플래시(Chromatic Hit Flash)를 자동 실행한다.
    /// HitFeedback을 보완하는 상위 레벨 연출 시스템.
    /// </summary>
    public class ImpactSystem : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────
        public static ImpactSystem Instance { get; private set; }

        // ── Hit Stop ──────────────────────────────────────────
        [Header("Hit Stop")]
        [Tooltip("일반 히트 정지 시간")]
        [SerializeField] private float _normalHitStopDuration = 0.04f;
        [Tooltip("크리티컬 히트 정지 시간")]
        [SerializeField] private float _criticalHitStopDuration = 0.08f;
        [Tooltip("보스 히트 정지 시간")]
        [SerializeField] private float _bossHitStopDuration = 0.12f;
        [Tooltip("히트 정지 중 Time.timeScale")]
        [SerializeField] private float _hitStopTimeScale = 0f;

        // ── Screen Shake ──────────────────────────────────────
        [Header("Screen Shake - Intensity / Duration")]
        [SerializeField] private float _shakeLight = 0.1f;
        [SerializeField] private float _shakeLightDur = 0.08f;
        [SerializeField] private float _shakeMedium = 0.25f;
        [SerializeField] private float _shakeMediumDur = 0.15f;
        [SerializeField] private float _shakeHeavy = 0.45f;
        [SerializeField] private float _shakeHeavyDur = 0.25f;
        [SerializeField] private float _shakeExtreme = 0.7f;
        [SerializeField] private float _shakeExtremeDur = 0.4f;

        // ── Camera Zoom Punch ─────────────────────────────────
        [Header("Camera Zoom Punch")]
        [Tooltip("타격 순간 줌인 량 (orthographicSize 감소분)")]
        [SerializeField] private float _zoomPunchAmount = 0.15f;
        [Tooltip("줌 펀치 지속 시간")]
        [SerializeField] private float _zoomPunchDuration = 0.12f;
        [Tooltip("크리티컬 줌 펀치 배율")]
        [SerializeField] private float _critZoomMultiplier = 2f;

        // ── Slow Motion ───────────────────────────────────────
        [Header("Slow Motion (Kill Cam)")]
        [Tooltip("드라마틱 슬로모션 배속")]
        [SerializeField] private float _slowMoTimeScale = 0.3f;
        [Tooltip("슬로모션 지속 시간 (realtime)")]
        [SerializeField] private float _slowMoDuration = 1.5f;

        // ── Chromatic Hit Flash ───────────────────────────────
        [Header("Hit Flash (White Sprite Flash)")]
        [Tooltip("피격 순간 흰색 플래시 지속 시간")]
        [SerializeField] private float _flashDuration = 0.06f;

        // ── Internal State ────────────────────────────────────
        private Coroutine _hitStopCoroutine;
        private Coroutine _slowMoCoroutine;
        private Coroutine _zoomPunchCoroutine;

        // 씬 내 남은 적 카운트 (마지막 적 처치 판별용)
        private int _remainingEnemies;
        private bool _trackingEnemies;

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

        void OnEnable()
        {
            GameEventSystem.Subscribe<DamageEvent>(OnDamageEvent);
            GameEventSystem.Subscribe<EnemyDeathEvent>(OnEnemyDeath);
        }

        void OnDisable()
        {
            GameEventSystem.Unsubscribe<DamageEvent>(OnDamageEvent);
            GameEventSystem.Unsubscribe<EnemyDeathEvent>(OnEnemyDeath);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ============================================================
        //  Public API
        // ============================================================

        /// <summary>
        /// 씬의 적 수를 등록한다. 마지막 적 처치 슬로모션 판별에 사용.
        /// EnemySpawner 등에서 호출할 수 있다.
        /// </summary>
        public void SetRemainingEnemies(int count)
        {
            _remainingEnemies = count;
            _trackingEnemies = true;
        }

        /// <summary>
        /// 외부에서 직접 히트스톱을 트리거한다.
        /// </summary>
        public void TriggerHitStop(float duration)
        {
            if (_hitStopCoroutine != null) StopCoroutine(_hitStopCoroutine);
            _hitStopCoroutine = StartCoroutine(HitStopCoroutine(duration));
        }

        /// <summary>
        /// 외부에서 직접 슬로모션을 트리거한다.
        /// </summary>
        public void TriggerSlowMotion(float duration, float timeScale)
        {
            if (_slowMoCoroutine != null) StopCoroutine(_slowMoCoroutine);
            _slowMoCoroutine = StartCoroutine(SlowMotionCoroutine(duration, timeScale));
        }

        /// <summary>
        /// 외부에서 직접 카메라 줌 펀치를 트리거한다.
        /// </summary>
        public void TriggerZoomPunch(float amount, float duration)
        {
            if (_zoomPunchCoroutine != null) StopCoroutine(_zoomPunchCoroutine);
            _zoomPunchCoroutine = StartCoroutine(ZoomPunchCoroutine(amount, duration));
        }

        /// <summary>
        /// 강도 레벨별 화면 흔들림. 0=약, 1=중, 2=강, 3=극강.
        /// </summary>
        public void TriggerShake(int level)
        {
            switch (level)
            {
                case 0: Shake(_shakeLight, _shakeLightDur); break;
                case 1: Shake(_shakeMedium, _shakeMediumDur); break;
                case 2: Shake(_shakeHeavy, _shakeHeavyDur); break;
                default: Shake(_shakeExtreme, _shakeExtremeDur); break;
            }
        }

        /// <summary>
        /// 대상 SpriteRenderer에 순간 흰색 플래시를 적용한다.
        /// </summary>
        public void TriggerHitFlash(GameObject target)
        {
            if (target == null) return;
            var sr = target.GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
                StartCoroutine(HitFlashCoroutine(sr));
        }

        // ============================================================
        //  Event Handlers
        // ============================================================

        private void OnDamageEvent(DamageEvent evt)
        {
            bool isBossHit = IsBossTarget(evt.Target);

            // --- Hit Stop ---
            float hitStopDur;
            if (isBossHit)
                hitStopDur = _bossHitStopDuration;
            else if (evt.IsCritical)
                hitStopDur = _criticalHitStopDuration;
            else
                hitStopDur = _normalHitStopDuration;

            TriggerHitStop(hitStopDur);

            // --- Screen Shake ---
            int shakeLevel;
            if (isBossHit && evt.IsCritical)
                shakeLevel = 3; // 극강
            else if (isBossHit || evt.IsCritical)
                shakeLevel = 2; // 강
            else if (evt.Damage > 30)
                shakeLevel = 1; // 중
            else
                shakeLevel = 0; // 약

            TriggerShake(shakeLevel);

            // --- Camera Zoom Punch ---
            float zoomAmount = evt.IsCritical
                ? _zoomPunchAmount * _critZoomMultiplier
                : _zoomPunchAmount;
            TriggerZoomPunch(zoomAmount, _zoomPunchDuration);

            // --- Hit Flash (Chromatic Aberration 대체: 흰색 플래시) ---
            TriggerHitFlash(evt.Target);
        }

        private void OnEnemyDeath(EnemyDeathEvent evt)
        {
            bool isBoss = evt.EnemyId != null && evt.EnemyId.StartsWith("boss_");

            // 적 카운트 감소
            if (_trackingEnemies)
                _remainingEnemies = Mathf.Max(0, _remainingEnemies - 1);

            bool isLastEnemy = _trackingEnemies && _remainingEnemies <= 0;

            // 보스 처치 또는 마지막 적 처치 시 드라마틱 슬로모션
            if (isBoss || isLastEnemy)
            {
                TriggerSlowMotion(_slowMoDuration, _slowMoTimeScale);
                TriggerShake(3); // 극강 흔들림
                TriggerZoomPunch(_zoomPunchAmount * 3f, _zoomPunchDuration * 2f);
            }
        }

        // ============================================================
        //  Coroutines
        // ============================================================

        private IEnumerator HitStopCoroutine(float duration)
        {
            float prevTimeScale = Time.timeScale;
            Time.timeScale = _hitStopTimeScale;

            yield return new WaitForSecondsRealtime(duration);

            // 슬로모션 중이면 슬로모션 타임스케일로 복귀, 아니면 원래 값
            Time.timeScale = (_slowMoCoroutine != null) ? _slowMoTimeScale : prevTimeScale;
            _hitStopCoroutine = null;
        }

        private IEnumerator SlowMotionCoroutine(float duration, float timeScale)
        {
            float prevTimeScale = Time.timeScale;
            Time.timeScale = timeScale;
            Time.fixedDeltaTime = 0.02f * timeScale;

            // 슬로모션에서 점진적 복귀 (마지막 30%에서 서서히 원래 속도로)
            float elapsed = 0f;
            float easeStart = duration * 0.7f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;

                if (elapsed > easeStart)
                {
                    float t = (elapsed - easeStart) / (duration - easeStart);
                    float currentScale = Mathf.Lerp(timeScale, 1f, t * t); // ease-in
                    Time.timeScale = currentScale;
                    Time.fixedDeltaTime = 0.02f * currentScale;
                }

                yield return null;
            }

            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
            _slowMoCoroutine = null;
        }

        private IEnumerator ZoomPunchCoroutine(float amount, float duration)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                _zoomPunchCoroutine = null;
                yield break;
            }

            float originalSize = cam.orthographicSize;
            float targetSize = originalSize - amount;
            float halfDuration = duration * 0.5f;

            // 줌인 (빠르게)
            float elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / halfDuration;
                cam.orthographicSize = Mathf.Lerp(originalSize, targetSize, t);
                yield return null;
            }

            // 줌아웃 (부드럽게)
            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / halfDuration;
                // ease-out
                float eased = 1f - (1f - t) * (1f - t);
                cam.orthographicSize = Mathf.Lerp(targetSize, originalSize, eased);
                yield return null;
            }

            cam.orthographicSize = originalSize;
            _zoomPunchCoroutine = null;
        }

        private IEnumerator HitFlashCoroutine(SpriteRenderer sr)
        {
            if (sr == null) yield break;

            Color original = sr.color;
            sr.color = Color.white;

            yield return new WaitForSecondsRealtime(_flashDuration);

            // SpriteRenderer가 아직 유효한지 확인
            if (sr != null)
                sr.color = original;
        }

        // ============================================================
        //  Helpers
        // ============================================================

        private void Shake(float intensity, float duration)
        {
            if (CameraController.Instance != null)
                CameraController.Instance.Shake(intensity, duration);
        }

        /// <summary>
        /// 대상이 보스인지 판별한다. EnemyData.isBoss 또는 EnemyId 접두사로 판별.
        /// </summary>
        private bool IsBossTarget(GameObject target)
        {
            if (target == null) return false;

            // Enemy 네임스페이스의 EnemyBase 체크
            var enemyBase = target.GetComponent<SoulCraft.Enemy.EnemyBase>();
            if (enemyBase != null && enemyBase.Data != null)
                return enemyBase.Data.isBoss;

            return false;
        }
    }
}
