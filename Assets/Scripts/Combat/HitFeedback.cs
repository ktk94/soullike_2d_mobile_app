using System.Collections;
using UnityEngine;
using SoulCraft.Core;

namespace SoulCraft.Combat
{
    /// <summary>
    /// 타격감을 위한 히트 피드백 시스템.
    /// DamageEvent를 구독하여 히트스톱, 화면 흔들림, 슬로모션 효과를 자동 처리한다.
    /// GameManager 또는 메인 카메라 오브젝트에 부착한다.
    /// </summary>
    public class HitFeedback : MonoBehaviour
    {
        [Header("Hit Stop")]
        [SerializeField] private float _hitStopDuration = 0.05f;
        [SerializeField] private float _hitStopTimeScale = 0.1f;

        [Header("Critical Hit")]
        [SerializeField] private float _critHitStopDuration = 0.1f;
        [SerializeField] private float _critShakeIntensity = 0.5f;
        [SerializeField] private float _critShakeDuration = 0.3f;

        [Header("Normal Hit")]
        [SerializeField] private float _normalShakeIntensity = 0.2f;
        [SerializeField] private float _normalShakeDuration = 0.15f;

        [Header("Slow Motion (Boss Kill 등)")]
        [SerializeField] private float _slowMoDuration = 1.0f;
        [SerializeField] private float _slowMoTimeScale = 0.2f;

        private Coroutine _hitStopCoroutine;
        private Coroutine _slowMoCoroutine;

        // --- Unity Lifecycle ---

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

        // --- Event Handlers ---

        private void OnDamageEvent(DamageEvent evt)
        {
            if (evt.IsCritical)
            {
                // 크리티컬: 더 강한 히트스톱 + 강한 화면 흔들림
                DoHitStop(_critHitStopDuration);
                ShakeCamera(_critShakeIntensity, _critShakeDuration);
            }
            else
            {
                // 일반 히트: 짧은 히트스톱 + 가벼운 흔들림
                DoHitStop(_hitStopDuration);
                ShakeCamera(_normalShakeIntensity, _normalShakeDuration);
            }
        }

        private void OnEnemyDeath(EnemyDeathEvent evt)
        {
            // 보스 처치 등 특수 상황에서 슬로모션
            // (보스 여부는 EnemyId 접두사 등으로 판별 가능)
            if (evt.EnemyId != null && evt.EnemyId.StartsWith("boss_"))
            {
                DoSlowMotion(_slowMoDuration, _slowMoTimeScale);
                ShakeCamera(_critShakeIntensity * 1.5f, _critShakeDuration * 1.5f);
            }
        }

        // --- Public API ---

        /// <summary>
        /// HitStop: Time.timeScale을 순간적으로 낮춰 타격감을 준다.
        /// </summary>
        public void DoHitStop(float duration)
        {
            if (_hitStopCoroutine != null)
                StopCoroutine(_hitStopCoroutine);
            _hitStopCoroutine = StartCoroutine(HitStopCoroutine(duration));
        }

        /// <summary>
        /// 슬로모션 효과를 발동한다 (보스 처치 등).
        /// </summary>
        public void DoSlowMotion(float duration, float timeScale)
        {
            if (_slowMoCoroutine != null)
                StopCoroutine(_slowMoCoroutine);
            _slowMoCoroutine = StartCoroutine(SlowMotionCoroutine(duration, timeScale));
        }

        // --- Private ---

        private void ShakeCamera(float intensity, float duration)
        {
            if (CameraController.Instance != null)
                CameraController.Instance.Shake(intensity, duration);
        }

        private IEnumerator HitStopCoroutine(float duration)
        {
            float originalTimeScale = Time.timeScale;
            Time.timeScale = _hitStopTimeScale;

            // unscaledTime 사용 — timeScale이 낮아도 정확한 대기
            yield return new WaitForSecondsRealtime(duration);

            Time.timeScale = originalTimeScale;
            _hitStopCoroutine = null;
        }

        private IEnumerator SlowMotionCoroutine(float duration, float timeScale)
        {
            float originalTimeScale = Time.timeScale;
            Time.timeScale = timeScale;
            Time.fixedDeltaTime = 0.02f * timeScale;

            yield return new WaitForSecondsRealtime(duration);

            Time.timeScale = originalTimeScale;
            Time.fixedDeltaTime = 0.02f;
            _slowMoCoroutine = null;
        }
    }
}
