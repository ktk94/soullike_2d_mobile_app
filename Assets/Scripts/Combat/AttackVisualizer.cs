using UnityEngine;

namespace SoulCraft.Combat
{
    /// <summary>
    /// 공격 시 화려한 시각적 이펙트를 생성하는 전담 시스템.
    /// 콤보 단계별로 다른 슬래시, 충격파, 스피드 라인, 히트 스파크를 생성한다.
    /// MonoBehaviour, 플레이어에 부착하여 사용.
    /// </summary>
    public class AttackVisualizer : MonoBehaviour
    {
        // ================================================================
        //  Constants
        // ================================================================

        private const int ArcTextureSize = 64;
        private const float SlashDuration = 0.12f;
        private const float ShockwaveDuration = 0.2f;
        private const float SpeedLineDuration = 0.1f;

        // ================================================================
        //  Cached Sprites (코드 생성)
        // ================================================================

        private static Sprite _arcSlash1;   // 콤보 1단: 횡베기 120도 호
        private static Sprite _arcSlash2;   // 콤보 2단: 올려베기 수직 120도
        private static Sprite _arcSlash3;   // 콤보 3단: 전체 원 + 내부 링
        private static Sprite _speedLine;   // 스피드 라인
        private static Sprite _sparkStar;   // 히트 스파크 별 모양
        private static Sprite _shockwaveRing; // 충격파 원형 링
        private static bool _spritesInitialized;

        // ================================================================
        //  Public API
        // ================================================================

        /// <summary>
        /// 콤보 단계에 맞는 공격 이펙트를 표시한다.
        /// PlayerCombat의 ShowSlashEffect에서 호출.
        /// </summary>
        /// <param name="step">콤보 단계 (0, 1, 2)</param>
        /// <param name="direction">공격 방향</param>
        /// <param name="position">이펙트 발생 위치</param>
        public void ShowComboEffect(int step, Vector2 direction, Vector2 position)
        {
            EnsureSpritesInitialized();

            switch (step)
            {
                case 0:
                    ShowCombo1Effect(direction, position);
                    break;
                case 1:
                    ShowCombo2Effect(direction, position);
                    break;
                case 2:
                    ShowCombo3Effect(direction, position);
                    break;
            }

            // 모든 단계에서 스피드 라인 발사
            SpawnSpeedLines(direction, position, step);
        }

        /// <summary>
        /// 적과의 교차점에 히트 스파크를 생성한다.
        /// 외부에서 호출 가능.
        /// </summary>
        public void ShowHitSpark(Vector2 hitPoint)
        {
            EnsureSpritesInitialized();
            SpawnHitSpark(hitPoint);
        }

        // ================================================================
        //  Combo 1: 횡베기 (반원 호 형태 슬래시, 흰색)
        // ================================================================

        private void ShowCombo1Effect(Vector2 direction, Vector2 position)
        {
            var go = CreateEffectObject("Slash_Combo1", position, _arcSlash1);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.color = Color.white;

            // 공격 방향에 따라 회전
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            go.transform.rotation = Quaternion.Euler(0, 0, angle);

            // 스케일 애니메이션
            var anim = go.AddComponent<SlashEffectAnimator>();
            anim.Initialize(
                startScale: 0f,
                endScale: 1.5f,
                startAlpha: 1f,
                endAlpha: 0f,
                duration: SlashDuration,
                color: Color.white
            );
        }

        // ================================================================
        //  Combo 2: 올려베기 (아래->위 수직 슬래시, 하늘색)
        // ================================================================

        private void ShowCombo2Effect(Vector2 direction, Vector2 position)
        {
            var go = CreateEffectObject("Slash_Combo2", position, _arcSlash2);
            var sr = go.GetComponent<SpriteRenderer>();
            Color skyBlue = new Color(0.5f, 0.8f, 1f, 1f);
            sr.color = skyBlue;

            // 공격 방향에 따라 회전 (수직 방향 보정)
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            go.transform.rotation = Quaternion.Euler(0, 0, angle);

            // 1단보다 큰 스케일
            var anim = go.AddComponent<SlashEffectAnimator>();
            anim.Initialize(
                startScale: 0f,
                endScale: 2.0f,
                startAlpha: 1f,
                endAlpha: 0f,
                duration: SlashDuration,
                color: skyBlue
            );
        }

        // ================================================================
        //  Combo 3: 내려찍기 (위->아래 + 충격파, 금색)
        // ================================================================

        private void ShowCombo3Effect(Vector2 direction, Vector2 position)
        {
            Color goldColor = new Color(1f, 0.85f, 0.3f, 1f);

            // 슬래시 이펙트
            var slashGo = CreateEffectObject("Slash_Combo3", position, _arcSlash3);
            var slashSr = slashGo.GetComponent<SpriteRenderer>();
            slashSr.color = goldColor;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            slashGo.transform.rotation = Quaternion.Euler(0, 0, angle);

            var slashAnim = slashGo.AddComponent<SlashEffectAnimator>();
            slashAnim.Initialize(
                startScale: 0f,
                endScale: 2.5f,
                startAlpha: 1f,
                endAlpha: 0f,
                duration: SlashDuration * 1.5f,
                color: goldColor
            );

            // 충격파 원형 링 이펙트
            var shockGo = CreateEffectObject("Shockwave_Combo3", position, _shockwaveRing);
            var shockSr = shockGo.GetComponent<SpriteRenderer>();
            shockSr.color = goldColor;
            shockSr.sortingOrder = 21;

            var shockAnim = shockGo.AddComponent<SlashEffectAnimator>();
            shockAnim.Initialize(
                startScale: 0.5f,
                endScale: 3.0f,
                startAlpha: 1f,
                endAlpha: 0f,
                duration: ShockwaveDuration,
                color: goldColor
            );

            // 화면 흔들림
            if (SoulCraft.Core.CameraController.Instance != null)
            {
                SoulCraft.Core.CameraController.Instance.Shake(0.4f, 0.25f);
            }
        }

        // ================================================================
        //  Speed Lines
        // ================================================================

        /// <summary>
        /// 공격 방향으로 얇은 선 3~5개 빠르게 발사
        /// </summary>
        private void SpawnSpeedLines(Vector2 direction, Vector2 position, int step)
        {
            int lineCount = 3 + step; // 콤보 단계에 따라 3~5개
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            for (int i = 0; i < lineCount; i++)
            {
                float spreadAngle = angle + Random.Range(-20f, 20f);
                float offsetDist = Random.Range(0.2f, 0.5f);

                Vector2 offset = new Vector2(
                    Mathf.Cos(spreadAngle * Mathf.Deg2Rad),
                    Mathf.Sin(spreadAngle * Mathf.Deg2Rad)
                ) * offsetDist;

                Vector2 linePos = position + offset;

                var lineGo = CreateEffectObject($"SpeedLine_{i}", linePos, _speedLine);
                lineGo.transform.rotation = Quaternion.Euler(0, 0, spreadAngle);
                lineGo.transform.localScale = new Vector3(1f + step * 0.3f, 0.3f, 1f);

                var sr = lineGo.GetComponent<SpriteRenderer>();
                sr.color = new Color(1f, 1f, 1f, 0.7f);
                sr.sortingOrder = 19;

                var anim = lineGo.AddComponent<SpeedLineAnimator>();
                anim.Initialize(
                    direction: new Vector2(
                        Mathf.Cos(spreadAngle * Mathf.Deg2Rad),
                        Mathf.Sin(spreadAngle * Mathf.Deg2Rad)
                    ),
                    speed: 15f + step * 5f,
                    duration: SpeedLineDuration
                );
            }
        }

        // ================================================================
        //  Hit Spark
        // ================================================================

        /// <summary>
        /// 적과 교차점에 별 모양 스파크 + 작은 파편을 생성한다.
        /// </summary>
        private void SpawnHitSpark(Vector2 hitPoint)
        {
            // 메인 스파크 (별 모양)
            var sparkGo = CreateEffectObject("HitSpark", hitPoint, _sparkStar);
            var sr = sparkGo.GetComponent<SpriteRenderer>();
            sr.color = new Color(1f, 0.95f, 0.7f, 1f);
            sr.sortingOrder = 25;
            sparkGo.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

            var sparkAnim = sparkGo.AddComponent<SlashEffectAnimator>();
            sparkAnim.Initialize(
                startScale: 0.3f,
                endScale: 1.2f,
                startAlpha: 1f,
                endAlpha: 0f,
                duration: 0.15f,
                color: new Color(1f, 0.95f, 0.7f, 1f)
            );

            // SpriteFactory의 fx_hit 사용 (파편)
            var fxHitSprite = SoulCraft.Factory.SpriteFactory.GetSprite("fx_hit");
            if (fxHitSprite != null)
            {
                int debrisCount = Random.Range(3, 6);
                for (int i = 0; i < debrisCount; i++)
                {
                    var debrisGo = new GameObject($"HitDebris_{i}");
                    debrisGo.transform.position = (Vector3)hitPoint;

                    var debrisSr = debrisGo.AddComponent<SpriteRenderer>();
                    debrisSr.sprite = fxHitSprite;
                    debrisSr.sortingLayerName = "Effect";
                    debrisSr.sortingOrder = 24;
                    debrisSr.color = new Color(1f, 0.9f, 0.5f, 1f);

                    debrisGo.transform.localScale = Vector3.one * Random.Range(0.2f, 0.5f);

                    var debrisAnim = debrisGo.AddComponent<DebrisAnimator>();
                    debrisAnim.Initialize(
                        direction: Random.insideUnitCircle.normalized,
                        speed: Random.Range(3f, 8f),
                        duration: Random.Range(0.1f, 0.2f)
                    );
                }
            }
        }

        // ================================================================
        //  Effect Object Factory
        // ================================================================

        private GameObject CreateEffectObject(string name, Vector2 position, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.position = (Vector3)position;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = "Effect";
            sr.sortingOrder = 20;

            return go;
        }

        // ================================================================
        //  Sprite Generation (코드로 Texture2D 생성)
        // ================================================================

        private static void EnsureSpritesInitialized()
        {
            if (_spritesInitialized) return;
            _spritesInitialized = true;

            _arcSlash1 = CreateArcSprite("ArcSlash1", -60f, 60f);       // 120도 호 (횡베기)
            _arcSlash2 = CreateArcSprite("ArcSlash2", -30f, 90f);       // 120도 수직 (올려베기)
            _arcSlash3 = CreateFullRingSprite("ArcSlash3");              // 전체 원 + 내부 링 (내려찍기)
            _speedLine = CreateSpeedLineSprite();
            _sparkStar = CreateSparkStarSprite();
            _shockwaveRing = CreateShockwaveRingSprite();
        }

        /// <summary>
        /// 호 형태 스프라이트 생성 (코드로).
        /// 중심에서의 거리와 각도를 계산하여 innerRadius~outerRadius,
        /// startAngle~endAngle 범위 내의 픽셀을 흰색으로 채운다.
        /// </summary>
        private static Sprite CreateArcSprite(string name, float startAngleDeg, float endAngleDeg)
        {
            int size = ArcTextureSize;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;

            float center = size * 0.5f;
            float innerRadius = size * 0.28f;
            float outerRadius = size * 0.45f;

            // 각도를 라디안으로
            float startAngle = startAngleDeg * Mathf.Deg2Rad;
            float endAngle = endAngleDeg * Mathf.Deg2Rad;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx);

                    bool inRadius = dist >= innerRadius && dist <= outerRadius;
                    bool inAngle = angle >= startAngle && angle <= endAngle;

                    if (inRadius && inAngle)
                    {
                        // 가장자리 부드럽게 (안티앨리어싱)
                        float edgeSoftness = 1f;
                        float innerEdge = Mathf.Clamp01((dist - innerRadius) / edgeSoftness);
                        float outerEdge = Mathf.Clamp01((outerRadius - dist) / edgeSoftness);
                        float alpha = Mathf.Min(innerEdge, outerEdge);

                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }

            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 16f);
            sprite.name = name;
            return sprite;
        }

        /// <summary>
        /// 전체 원(360도) + 내부 링 스프라이트 (콤보 3단용).
        /// </summary>
        private static Sprite CreateFullRingSprite(string name)
        {
            int size = ArcTextureSize;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;

            float center = size * 0.5f;
            float outerRadius = size * 0.45f;
            float innerRadius = size * 0.32f;
            float innerRing2 = size * 0.18f;
            float innerRing2Outer = size * 0.25f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    bool inOuterRing = dist >= innerRadius && dist <= outerRadius;
                    bool inInnerRing = dist >= innerRing2 && dist <= innerRing2Outer;

                    if (inOuterRing || inInnerRing)
                    {
                        float edgeSoftness = 1.5f;
                        float alpha;

                        if (inOuterRing)
                        {
                            float innerEdge = Mathf.Clamp01((dist - innerRadius) / edgeSoftness);
                            float outerEdge = Mathf.Clamp01((outerRadius - dist) / edgeSoftness);
                            alpha = Mathf.Min(innerEdge, outerEdge);
                        }
                        else
                        {
                            float innerEdge = Mathf.Clamp01((dist - innerRing2) / edgeSoftness);
                            float outerEdge = Mathf.Clamp01((innerRing2Outer - dist) / edgeSoftness);
                            alpha = Mathf.Min(innerEdge, outerEdge) * 0.6f;
                        }

                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }

            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 16f);
            sprite.name = name;
            return sprite;
        }

        /// <summary>
        /// 충격파 원형 링 스프라이트 (3단 충격파용).
        /// </summary>
        private static Sprite CreateShockwaveRingSprite()
        {
            int size = ArcTextureSize;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;

            float center = size * 0.5f;
            float innerRadius = size * 0.35f;
            float outerRadius = size * 0.45f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist >= innerRadius && dist <= outerRadius)
                    {
                        float edgeSoftness = 2f;
                        float innerEdge = Mathf.Clamp01((dist - innerRadius) / edgeSoftness);
                        float outerEdge = Mathf.Clamp01((outerRadius - dist) / edgeSoftness);
                        float alpha = Mathf.Min(innerEdge, outerEdge);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }

            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 16f);
            sprite.name = "ShockwaveRing";
            return sprite;
        }

        /// <summary>
        /// 스피드 라인 스프라이트 (얇은 수평 선).
        /// </summary>
        private static Sprite CreateSpeedLineSprite()
        {
            int width = 32;
            int height = 4;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // 좌측에서 우측으로 갈수록 알파 감소 (꼬리 형태)
                    float tX = (float)x / width;
                    float alpha = 1f - tX * tX; // 끝으로 갈수록 페이드

                    // 중앙 라인이 더 밝음
                    float tY = Mathf.Abs(y - height * 0.5f) / (height * 0.5f);
                    alpha *= 1f - tY * 0.5f;

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, width, height),
                new Vector2(0f, 0.5f), 16f);
            sprite.name = "SpeedLine";
            return sprite;
        }

        /// <summary>
        /// 별 모양 스파크 스프라이트 (히트 이펙트용).
        /// </summary>
        private static Sprite CreateSparkStarSprite()
        {
            int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;

            float center = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx);

                    // 별 모양: 각도에 따라 반지름이 변동
                    float starRadius = size * 0.15f + size * 0.2f *
                        Mathf.Abs(Mathf.Cos(angle * 4f)); // 8각 별

                    if (dist <= starRadius)
                    {
                        float alpha = 1f - (dist / starRadius) * 0.5f;
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                    else
                    {
                        // 광선 (얇은 십자)
                        bool onCross = (Mathf.Abs(dx) < 1.5f || Mathf.Abs(dy) < 1.5f)
                                        && dist < size * 0.45f;
                        if (onCross)
                        {
                            float alpha = 1f - dist / (size * 0.45f);
                            tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * 0.5f));
                        }
                        else
                        {
                            tex.SetPixel(x, y, Color.clear);
                        }
                    }
                }
            }

            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 16f);
            sprite.name = "SparkStar";
            return sprite;
        }
    }

    // ================================================================
    //  SlashEffectAnimator - 슬래시/충격파 애니메이션
    // ================================================================

    /// <summary>
    /// 스케일 확대 + 알파 페이드 아웃 후 자동 파괴되는 이펙트 애니메이터.
    /// Instantiate + Destroy(delay) 패턴 사용.
    /// </summary>
    public class SlashEffectAnimator : MonoBehaviour
    {
        private float _startScale;
        private float _endScale;
        private float _startAlpha;
        private float _endAlpha;
        private float _duration;
        private Color _baseColor;

        private float _elapsed;
        private SpriteRenderer _sr;
        private bool _initialized;

        public void Initialize(float startScale, float endScale,
            float startAlpha, float endAlpha, float duration, Color color)
        {
            _startScale = startScale;
            _endScale = endScale;
            _startAlpha = startAlpha;
            _endAlpha = endAlpha;
            _duration = duration;
            _baseColor = color;
            _elapsed = 0f;
            _initialized = true;

            _sr = GetComponent<SpriteRenderer>();

            // 초기 상태
            transform.localScale = Vector3.one * _startScale;
            if (_sr != null)
            {
                _baseColor.a = _startAlpha;
                _sr.color = _baseColor;
            }

            // 자동 파괴 예약
            Destroy(gameObject, _duration + 0.05f);
        }

        private void Update()
        {
            if (!_initialized) return;

            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);

            // 스케일 보간
            float scale = Mathf.Lerp(_startScale, _endScale, t);
            transform.localScale = Vector3.one * scale;

            // 알파 보간
            if (_sr != null)
            {
                float alpha = Mathf.Lerp(_startAlpha, _endAlpha, t);
                Color c = _baseColor;
                c.a = alpha;
                _sr.color = c;
            }
        }
    }

    // ================================================================
    //  SpeedLineAnimator - 스피드 라인 애니메이션
    // ================================================================

    /// <summary>
    /// 지정 방향으로 빠르게 이동하며 페이드아웃 후 자동 파괴되는 스피드 라인.
    /// </summary>
    public class SpeedLineAnimator : MonoBehaviour
    {
        private Vector2 _direction;
        private float _speed;
        private float _duration;
        private float _elapsed;
        private SpriteRenderer _sr;
        private bool _initialized;

        public void Initialize(Vector2 direction, float speed, float duration)
        {
            _direction = direction.normalized;
            _speed = speed;
            _duration = duration;
            _elapsed = 0f;
            _initialized = true;

            _sr = GetComponent<SpriteRenderer>();

            Destroy(gameObject, _duration + 0.05f);
        }

        private void Update()
        {
            if (!_initialized) return;

            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);

            // 이동
            transform.position += (Vector3)(_direction * _speed * Time.deltaTime);

            // 페이드아웃
            if (_sr != null)
            {
                Color c = _sr.color;
                c.a = Mathf.Lerp(0.7f, 0f, t);
                _sr.color = c;
            }
        }
    }

    // ================================================================
    //  DebrisAnimator - 히트 파편 애니메이션
    // ================================================================

    /// <summary>
    /// 히트 시 발생하는 작은 파편이 퍼져나간 뒤 사라지는 애니메이터.
    /// </summary>
    public class DebrisAnimator : MonoBehaviour
    {
        private Vector2 _direction;
        private float _speed;
        private float _duration;
        private float _elapsed;
        private SpriteRenderer _sr;
        private bool _initialized;

        public void Initialize(Vector2 direction, float speed, float duration)
        {
            _direction = direction.normalized;
            _speed = speed;
            _duration = duration;
            _elapsed = 0f;
            _initialized = true;

            _sr = GetComponent<SpriteRenderer>();

            Destroy(gameObject, _duration + 0.05f);
        }

        private void Update()
        {
            if (!_initialized) return;

            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);

            // 이동 (감속)
            float currentSpeed = _speed * (1f - t);
            transform.position += (Vector3)(_direction * currentSpeed * Time.deltaTime);

            // 스케일 축소 + 페이드아웃
            float scale = Mathf.Lerp(1f, 0.1f, t);
            transform.localScale = Vector3.one * scale * 0.3f;

            if (_sr != null)
            {
                Color c = _sr.color;
                c.a = Mathf.Lerp(1f, 0f, t);
                _sr.color = c;
            }
        }
    }
}
