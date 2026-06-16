using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SoulCraft.Core;

namespace SoulCraft.Enemy
{
    /// <summary>
    /// 적 피격 시 시각적 반응 강화.
    /// 스프라이트 플래시, 넉백 흔들림, 크기 펀치, 파티클, 데미지 숫자 팝업.
    /// DamageEvent를 구독하거나 EnemyBase.TakeDamage에서 직접 호출.
    /// </summary>
    public class EnemyHitReaction : MonoBehaviour
    {
        // ── 설정: 스프라이트 플래시 ──────────────────────────
        private const float FlashDuration = 0.1f;
        private static readonly Color FlashColor = Color.white;

        // ── 설정: 넉백 흔들림 ────────────────────────────────
        private const float ShakeAmount = 0.05f;
        private const int ShakeCount = 3;
        private const float ShakeDuration = 0.15f;

        // ── 설정: 크기 펀치 ──────────────────────────────────
        private const float PunchScale = 1.2f;
        private const float PunchDuration = 0.15f;

        // ── 설정: 파티클 ─────────────────────────────────────
        private const int ParticleMinCount = 3;
        private const int ParticleMaxCount = 5;
        private const float ParticleSpeed = 3f;
        private const float ParticleLifetime = 0.4f;
        private const float ParticleSize = 0.06f;

        // ── 설정: 데미지 숫자 ────────────────────────────────
        private const float DmgTextFloatSpeed = 1.8f;
        private const float DmgTextLifetime = 0.8f;
        private const float DmgTextNormalSize = 5f;
        private const float DmgTextCritSize = 7.5f;
        private static readonly Color DmgTextNormalColor = Color.white;
        private static readonly Color DmgTextCritColor = new(1f, 0.9f, 0.1f, 1f);

        // ── 런타임 참조 ────────────────────────────────────────
        private SpriteRenderer _spriteRenderer;
        private Color _originalColor;
        private Vector3 _originalScale;
        private Vector3 _originalLocalPos;

        private Coroutine _flashCoroutine;
        private Coroutine _shakeCoroutine;
        private Coroutine _punchCoroutine;

        // ── 스프라이트 캐시 (파티클/데미지 텍스트용) ────────────
        private static Sprite _whitePixelSprite;

        // ── Lifecycle ─────────────────────────────────────────

        void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _originalScale = transform.localScale;
        }

        void Start()
        {
            if (_spriteRenderer != null)
                _originalColor = _spriteRenderer.color;
            _originalLocalPos = transform.localPosition;

            // DamageEvent 구독
            GameEventSystem.Subscribe<DamageEvent>(OnDamageEvent);
        }

        void OnDestroy()
        {
            GameEventSystem.Unsubscribe<DamageEvent>(OnDamageEvent);
        }

        // ── 이벤트 핸들러 ──────────────────────────────────────

        private void OnDamageEvent(DamageEvent evt)
        {
            // 이 오브젝트가 타겟인 경우만 반응
            if (evt.Target != gameObject) return;
            PlayHitReaction(evt.Damage, evt.IsCritical, evt.HitPoint);
        }

        // ── Public API ────────────────────────────────────────

        /// <summary>
        /// 피격 반응을 실행한다.
        /// EnemyBase.TakeDamage에서 직접 호출하거나, DamageEvent로 트리거된다.
        /// </summary>
        public void PlayHitReaction(int damage, bool isCritical, Vector2 hitPoint)
        {
            if (!gameObject.activeInHierarchy) return;

            // 1. 스프라이트 플래시
            PlayFlash();

            // 2. 넉백 흔들림
            PlayShake(hitPoint);

            // 3. 크기 펀치
            PlayScalePunch();

            // 4. 파티클
            SpawnHitParticles(hitPoint);

            // 5. 데미지 숫자 팝업
            SpawnDamageNumber(damage, isCritical, hitPoint);
        }

        // ── 1. 스프라이트 플래시 ──────────────────────────────

        private void PlayFlash()
        {
            if (_spriteRenderer == null) return;

            if (_flashCoroutine != null)
                StopCoroutine(_flashCoroutine);
            _flashCoroutine = StartCoroutine(FlashCoroutine());
        }

        private IEnumerator FlashCoroutine()
        {
            _spriteRenderer.color = FlashColor;
            yield return new WaitForSeconds(FlashDuration);
            _spriteRenderer.color = _originalColor;
            _flashCoroutine = null;
        }

        // ── 2. 넉백 흔들림 ───────────────────────────────────

        private void PlayShake(Vector2 hitPoint)
        {
            if (_shakeCoroutine != null)
                StopCoroutine(_shakeCoroutine);
            _shakeCoroutine = StartCoroutine(ShakeCoroutine(hitPoint));
        }

        private IEnumerator ShakeCoroutine(Vector2 hitPoint)
        {
            Vector2 knockDir = ((Vector2)transform.position - hitPoint).normalized;
            float elapsed = 0f;
            float interval = ShakeDuration / (ShakeCount * 2f);

            for (int i = 0; i < ShakeCount; i++)
            {
                // 피격 반대 방향으로 이동
                transform.position += (Vector3)(knockDir * ShakeAmount);
                yield return new WaitForSeconds(interval);

                // 원래 위치로 복귀
                transform.position -= (Vector3)(knockDir * ShakeAmount);
                yield return new WaitForSeconds(interval);
            }

            _shakeCoroutine = null;
        }

        // ── 3. 크기 펀치 ─────────────────────────────────────

        private void PlayScalePunch()
        {
            if (_punchCoroutine != null)
                StopCoroutine(_punchCoroutine);
            _punchCoroutine = StartCoroutine(ScalePunchCoroutine());
        }

        private IEnumerator ScalePunchCoroutine()
        {
            float elapsed = 0f;
            float halfDuration = PunchDuration * 0.5f;

            // 확대
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / halfDuration;
                float scale = Mathf.Lerp(1f, PunchScale, t);
                transform.localScale = _originalScale * scale;
                yield return null;
            }

            // 복귀
            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / halfDuration;
                float scale = Mathf.Lerp(PunchScale, 1f, t);
                transform.localScale = _originalScale * scale;
                yield return null;
            }

            transform.localScale = _originalScale;
            _punchCoroutine = null;
        }

        // ── 4. 파티클 (피격 지점에서 파편) ─────────────────────

        private void SpawnHitParticles(Vector2 hitPoint)
        {
            Color particleColor = _spriteRenderer != null ? _originalColor : Color.red;
            int count = Random.Range(ParticleMinCount, ParticleMaxCount + 1);

            for (int i = 0; i < count; i++)
            {
                StartCoroutine(ParticleCoroutine(hitPoint, particleColor));
            }
        }

        private IEnumerator ParticleCoroutine(Vector2 origin, Color color)
        {
            var particleGo = new GameObject("HitParticle");
            particleGo.transform.position = (Vector3)origin;

            var sr = particleGo.AddComponent<SpriteRenderer>();
            sr.sprite = GetWhitePixelSprite();
            sr.color = color;
            sr.sortingOrder = 100;
            particleGo.transform.localScale = Vector3.one * ParticleSize;

            // 랜덤 방향
            Vector2 dir = Random.insideUnitCircle.normalized;
            float speed = ParticleSpeed * Random.Range(0.7f, 1.3f);

            float elapsed = 0f;
            while (elapsed < ParticleLifetime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / ParticleLifetime;

                // 이동
                particleGo.transform.position += (Vector3)(dir * speed * Time.deltaTime);

                // 크기 감소
                float s = Mathf.Lerp(ParticleSize, 0f, t);
                particleGo.transform.localScale = Vector3.one * s;

                // 페이드아웃
                Color c = sr.color;
                c.a = Mathf.Lerp(1f, 0f, t);
                sr.color = c;

                yield return null;
            }

            Destroy(particleGo);
        }

        // ── 5. 데미지 숫자 팝업 ──────────────────────────────

        private void SpawnDamageNumber(int damage, bool isCritical, Vector2 hitPoint)
        {
            StartCoroutine(DamageNumberCoroutine(damage, isCritical, hitPoint));
        }

        private IEnumerator DamageNumberCoroutine(int damage, bool isCritical, Vector2 hitPoint)
        {
            // Canvas 생성 (월드 스페이스)
            var canvasGo = new GameObject("DmgNumber_Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 200;

            var canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(2f, 0.5f);
            canvasRect.localScale = Vector3.one * 0.01f;
            canvasGo.transform.position = new Vector3(hitPoint.x, hitPoint.y + 0.3f, 0f);

            // TextMeshPro 생성
            var textGo = new GameObject("DmgText", typeof(RectTransform));
            textGo.transform.SetParent(canvasGo.transform, false);

            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            tmp.raycastTarget = false;

            if (isCritical)
            {
                tmp.text = $"{damage}!";
                tmp.fontSize = DmgTextCritSize;
                tmp.color = DmgTextCritColor;
                canvasRect.localScale = Vector3.one * 0.015f;
            }
            else
            {
                tmp.text = damage.ToString();
                tmp.fontSize = DmgTextNormalSize;
                tmp.color = DmgTextNormalColor;
            }

            // CanvasGroup (페이드아웃용)
            var canvasGroup = canvasGo.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;

            // 랜덤 X 오프셋
            float randomX = Random.Range(-0.3f, 0.3f);

            // 애니메이션
            float elapsed = 0f;
            Vector3 startPos = canvasGo.transform.position;

            while (elapsed < DmgTextLifetime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / DmgTextLifetime;

                // 위로 떠오르기
                float yOffset = DmgTextFloatSpeed * elapsed;
                float xOffset = randomX * t;
                canvasGo.transform.position = startPos + new Vector3(xOffset, yOffset, 0f);

                // 페이드아웃 (후반 40%)
                if (t > 0.6f)
                {
                    canvasGroup.alpha = 1f - (t - 0.6f) / 0.4f;
                }

                // 크리티컬 스케일 펀치
                if (isCritical && t < 0.2f)
                {
                    float scalePunch = Mathf.Lerp(1.5f, 1f, t / 0.2f);
                    canvasRect.localScale = Vector3.one * 0.015f * scalePunch;
                }

                yield return null;
            }

            Destroy(canvasGo);
        }

        // ── 유틸리티 ──────────────────────────────────────────

        private static Sprite GetWhitePixelSprite()
        {
            if (_whitePixelSprite != null) return _whitePixelSprite;

            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            var pixels = new Color[16];
            for (int i = 0; i < 16; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();

            _whitePixelSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            _whitePixelSprite.name = "WhitePixel";

            return _whitePixelSprite;
        }

        /// <summary>
        /// 원래 색상을 갱신한다. InitializeEnemy 이후 호출.
        /// </summary>
        public void RefreshOriginalColor()
        {
            if (_spriteRenderer != null)
                _originalColor = _spriteRenderer.color;
        }

        /// <summary>
        /// 원래 스케일을 갱신한다.
        /// </summary>
        public void RefreshOriginalScale()
        {
            _originalScale = transform.localScale;
        }

        void OnEnable()
        {
            if (_spriteRenderer != null)
                _originalColor = _spriteRenderer.color;
            _originalScale = transform.localScale;
        }
    }
}
