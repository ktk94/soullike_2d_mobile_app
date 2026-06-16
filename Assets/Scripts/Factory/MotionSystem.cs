using UnityEngine;
using SoulCraft.Core;
using SoulCraft.Enemy;

namespace SoulCraft.Factory
{
    /// <summary>
    /// 프리팹/애니메이터 없이 코드만으로 걷기/이동 모션을 구현하는 시스템.
    /// SimpleMotion: 플레이어/일반 캐릭터용 기본 이동 모션.
    /// EnemyMotion: 적 타입별 특수 모션.
    /// </summary>

    // ================================================================
    //  SimpleMotion - 기본 이동 모션 컴포넌트
    // ================================================================

    /// <summary>
    /// SpriteRenderer 기반의 코드 모션 시스템.
    /// Rigidbody2D.linearVelocity를 감지하여 걷기 모션(바운스, 기울기, 스쿼시/스트레치)을 적용하고,
    /// 정지 시 idle 호흡 모션을 재생한다. 발밑에 프로시저럴 타원 그림자를 생성한다.
    /// </summary>
    public class SimpleMotion : MonoBehaviour
    {
        // ── 설정: 이동 모션 ────────────────────────────────────
        protected const float BounceAmplitude = 0.06f;
        protected const float BounceFrequency = 8f; // Hz
        protected const float TiltAngle = 5f; // degrees
        protected const float SquashMin = 0.9f;
        protected const float SquashMax = 1.1f;

        // ── 설정: idle 호흡 ────────────────────────────────────
        protected const float IdleBreathAmplitude = 0.02f;
        protected const float IdleBreathFrequency = 0.5f; // Hz

        // ── 설정: 복귀 속도 ────────────────────────────────────
        protected const float ReturnSpeed = 8f;

        // ── 설정: 이동 감지 임계값 ─────────────────────────────
        protected const float MoveThreshold = 0.1f;

        // ── 그림자 설정 ─────────────────────────────────────────
        protected const float ShadowOffsetY = -0.3f;
        protected const int ShadowTexWidth = 16;
        protected const int ShadowTexHeight = 8;

        // ── 런타임 참조 ────────────────────────────────────────
        protected SpriteRenderer spriteRenderer;
        protected Rigidbody2D rb;

        protected Transform shadowTransform;
        protected SpriteRenderer shadowRenderer;

        protected Vector3 baseLocalPosition;
        protected Vector3 baseScale;
        protected float motionTimer;

        // ── 현재 적용 값 (부드러운 복귀용) ─────────────────────
        protected float currentBounceY;
        protected float currentTiltZ;
        protected float currentSquashX;
        protected float currentSquashY;

        // ── Lifecycle ───────────────────────────────────────────

        protected virtual void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            rb = GetComponent<Rigidbody2D>();
        }

        protected virtual void Start()
        {
            baseLocalPosition = Vector3.zero;
            baseScale = transform.localScale;
            CreateShadow();
        }

        protected virtual void Update()
        {
            if (rb == null || spriteRenderer == null) return;

            float speed = rb.linearVelocity.magnitude;
            bool isMoving = speed > MoveThreshold;

            if (isMoving)
            {
                UpdateMovingMotion();
            }
            else
            {
                UpdateIdleMotion();
            }

            ApplyMotion();
            UpdateShadow(isMoving);
        }

        // ── 이동 모션 ──────────────────────────────────────────

        protected virtual void UpdateMovingMotion()
        {
            motionTimer += Time.deltaTime;
            float phase = motionTimer * BounceFrequency * Mathf.PI * 2f;

            // 상하 바운스
            float targetBounceY = Mathf.Sin(phase) * BounceAmplitude;
            currentBounceY = Mathf.Lerp(currentBounceY, targetBounceY, Time.deltaTime * 20f);

            // 좌우 기울기 (이동 방향 기반)
            float moveX = rb.linearVelocity.x;
            float targetTilt = -Mathf.Sign(moveX) * TiltAngle *
                               Mathf.Clamp01(Mathf.Abs(moveX) / 2f);
            currentTiltZ = Mathf.Lerp(currentTiltZ, targetTilt, Time.deltaTime * 10f);

            // 스쿼시/스트레치 (바운스와 동기화)
            float squashPhase = Mathf.Sin(phase);
            float targetSquashX = Mathf.Lerp(SquashMin, SquashMax, (squashPhase + 1f) * 0.5f);
            float targetSquashY = Mathf.Lerp(SquashMax, SquashMin, (squashPhase + 1f) * 0.5f);
            currentSquashX = Mathf.Lerp(currentSquashX, targetSquashX, Time.deltaTime * 15f);
            currentSquashY = Mathf.Lerp(currentSquashY, targetSquashY, Time.deltaTime * 15f);
        }

        protected virtual void UpdateIdleMotion()
        {
            motionTimer += Time.deltaTime;
            float phase = motionTimer * IdleBreathFrequency * Mathf.PI * 2f;

            // idle 호흡: scaleY만 미세하게 변화
            float breath = Mathf.Sin(phase) * IdleBreathAmplitude;

            // 바운스/기울기 복귀
            currentBounceY = Mathf.Lerp(currentBounceY, 0f, Time.deltaTime * ReturnSpeed);
            currentTiltZ = Mathf.Lerp(currentTiltZ, 0f, Time.deltaTime * ReturnSpeed);

            // 스쿼시 복귀 + 호흡
            currentSquashX = Mathf.Lerp(currentSquashX, 1f, Time.deltaTime * ReturnSpeed);
            currentSquashY = Mathf.Lerp(currentSquashY, 1f + breath, Time.deltaTime * ReturnSpeed);
        }

        // ── 모션 적용 ──────────────────────────────────────────

        protected virtual void ApplyMotion()
        {
            // SpriteRenderer의 부모 오브젝트에 적용 (transform 자체)
            // localPosition은 바운스용 Y오프셋만
            // 그러나 transform.position은 물리엔진이 관리하므로
            // 스프라이트 오프셋은 자식이 아닌 spriteRenderer 자체에서 처리

            // 스케일 적용
            transform.localScale = new Vector3(
                baseScale.x * currentSquashX,
                baseScale.y * currentSquashY,
                baseScale.z
            );

            // 회전 적용 (Z축 기울기)
            transform.rotation = Quaternion.Euler(0f, 0f, currentTiltZ);

            // 바운스는 스프라이트 위치 오프셋으로 적용
            // Rigidbody가 있으므로 transform.position을 직접 수정하지 않고
            // 시각적 오프셋만 적용
            if (spriteRenderer != null)
            {
                // SpriteRenderer에는 offset이 없으므로 material offset 대신
                // 별도의 비주얼 오프셋을 transform에 적용하되,
                // Rigidbody와 충돌하지 않도록 주의
                // 여기서는 spriteRenderer.transform == this.transform이므로
                // 상하 바운스는 그림자와의 관계로만 시각적 효과
            }
        }

        // ── 그림자 생성 ─────────────────────────────────────────

        protected virtual void CreateShadow()
        {
            var shadowGo = new GameObject("Shadow");
            shadowGo.transform.SetParent(transform);
            shadowGo.transform.localPosition = new Vector3(0f, ShadowOffsetY, 0f);
            shadowGo.transform.localRotation = Quaternion.identity;

            shadowRenderer = shadowGo.AddComponent<SpriteRenderer>();

            // 16x8 타원 텍스처 프로시저럴 생성
            var tex = new Texture2D(ShadowTexWidth, ShadowTexHeight, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            float halfW = ShadowTexWidth * 0.5f;
            float halfH = ShadowTexHeight * 0.5f;

            for (int y = 0; y < ShadowTexHeight; y++)
            {
                for (int x = 0; x < ShadowTexWidth; x++)
                {
                    float dx = (x - halfW) / halfW;
                    float dy = (y - halfH) / halfH;
                    bool inside = (dx * dx + dy * dy) <= 1f;
                    tex.SetPixel(x, y, inside ? new Color(0f, 0f, 0f, 0.3f) : Color.clear);
                }
            }
            tex.Apply();

            shadowRenderer.sprite = Sprite.Create(
                tex,
                new Rect(0, 0, ShadowTexWidth, ShadowTexHeight),
                new Vector2(0.5f, 0.5f),
                16f
            );
            shadowRenderer.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder - 1 : -1;
            shadowRenderer.sortingLayerName = spriteRenderer != null ? spriteRenderer.sortingLayerName : "Default";

            shadowTransform = shadowGo.transform;
        }

        // ── 그림자 갱신 ─────────────────────────────────────────

        protected virtual void UpdateShadow(bool isMoving)
        {
            if (shadowTransform == null) return;

            // 그림자는 항상 발밑에 고정 (바운스 보정)
            // transform의 회전을 상쇄
            shadowTransform.localRotation = Quaternion.Euler(0f, 0f, -currentTiltZ);

            // 그림자 스케일: transform scale의 역수로 일정 크기 유지
            float invScaleX = currentSquashX > 0.01f ? 1f / currentSquashX : 1f;
            float invScaleY = currentSquashY > 0.01f ? 1f / currentSquashY : 1f;
            shadowTransform.localScale = new Vector3(invScaleX, invScaleY, 1f);
        }

        // ── OnDestroy ───────────────────────────────────────────

        protected virtual void OnDestroy()
        {
            if (shadowTransform != null && shadowTransform.gameObject != null)
            {
                Destroy(shadowTransform.gameObject);
            }
        }
    }

    // ================================================================
    //  EnemyMotion - 적 타입별 모션 컴포넌트
    // ================================================================

    /// <summary>
    /// 적 타입별 특수 이동 모션.
    /// gameObject.name에서 적 타입을 자동 파싱하여 해당 모션을 적용한다.
    /// - slime: 뛰어다니는 모션 (주기적 scale bounce)
    /// - skeleton: 걷기 모션 (좌우 흔들림 + 바운스)
    /// - bat: 날개짓 (scaleX 교대 + 부유)
    /// - fire_spirit: 유령 부유 (느린 부유 + 회전 + 크기 맥동)
    /// - ice_golem: 무거운 걸음 (느린 바운스 + 미세 화면 진동)
    /// </summary>
    public class EnemyMotion : MonoBehaviour
    {
        // ── 적 타입 열거 ─────────────────────────────────────────
        private enum EnemyMotionType
        {
            Default,
            Slime,
            Skeleton,
            Bat,
            FireSpirit,
            IceGolem
        }

        // ── 런타임 참조 ────────────────────────────────────────
        private SpriteRenderer spriteRenderer;
        private Rigidbody2D rb;
        private Transform shadowTransform;
        private SpriteRenderer shadowRenderer;
        private EnemyMotionType motionType;

        private Vector3 baseScale;
        private float motionTimer;
        private float slimeJumpTimer;
        private float slimeJumpInterval = 0.5f;
        private int slimeJumpPhase; // 0=idle, 1=squash, 2=stretch, 3=recover

        // ── 이동 감지 임계값 ────────────────────────────────────
        private const float MoveThreshold = 0.1f;

        // ── 그림자 설정 ─────────────────────────────────────────
        private const float ShadowOffsetY = -0.3f;
        private const int ShadowTexWidth = 16;
        private const int ShadowTexHeight = 8;

        // ── Lifecycle ───────────────────────────────────────────

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            rb = GetComponent<Rigidbody2D>();
        }

        private void Start()
        {
            baseScale = transform.localScale;
            motionType = ParseMotionType(gameObject.name);
            CreateShadow();
        }

        private void Update()
        {
            if (spriteRenderer == null) return;

            motionTimer += Time.deltaTime;
            bool isMoving = rb != null && rb.linearVelocity.magnitude > MoveThreshold;

            switch (motionType)
            {
                case EnemyMotionType.Slime:
                    UpdateSlimeMotion(isMoving);
                    break;
                case EnemyMotionType.Skeleton:
                    UpdateSkeletonMotion(isMoving);
                    break;
                case EnemyMotionType.Bat:
                    UpdateBatMotion(isMoving);
                    break;
                case EnemyMotionType.FireSpirit:
                    UpdateFireSpiritMotion(isMoving);
                    break;
                case EnemyMotionType.IceGolem:
                    UpdateIceGolemMotion(isMoving);
                    break;
                default:
                    UpdateDefaultMotion(isMoving);
                    break;
            }

            UpdateShadow();
        }

        // ── 타입 파싱 ──────────────────────────────────────────

        /// <summary>
        /// GameObject 이름에서 적 타입을 파싱한다.
        /// 예: "Enemy_slime", "Enemy_fire_spirit_elite" 등
        /// </summary>
        private static EnemyMotionType ParseMotionType(string name)
        {
            string lower = name.ToLower();

            if (lower.Contains("slime")) return EnemyMotionType.Slime;
            if (lower.Contains("skeleton") || lower.Contains("skull")) return EnemyMotionType.Skeleton;
            if (lower.Contains("bat")) return EnemyMotionType.Bat;
            if (lower.Contains("fire_spirit") || lower.Contains("firespirit")) return EnemyMotionType.FireSpirit;
            if (lower.Contains("ice_golem") || lower.Contains("icegolem")) return EnemyMotionType.IceGolem;

            return EnemyMotionType.Default;
        }

        // ================================================================
        //  Slime Motion - 뛰어다니는 모션
        // ================================================================

        /// <summary>
        /// 슬라임: 0.5초 간격으로 scaleY 0.6 -> 1.3 -> 1.0 튕기기.
        /// 점프 시 그림자 작아짐.
        /// </summary>
        private void UpdateSlimeMotion(bool isMoving)
        {
            slimeJumpTimer += Time.deltaTime;

            if (slimeJumpTimer >= slimeJumpInterval)
            {
                slimeJumpTimer -= slimeJumpInterval;
                slimeJumpPhase = 1; // 새 점프 시작
            }

            float scaleX = baseScale.x;
            float scaleY = baseScale.y;
            float shadowScale = 1f;

            float phaseTime = slimeJumpTimer / slimeJumpInterval;

            if (phaseTime < 0.15f)
            {
                // 찌그러짐 (squash) - 0~15%
                float t = phaseTime / 0.15f;
                scaleY = baseScale.y * Mathf.Lerp(1f, 0.6f, t);
                scaleX = baseScale.x * Mathf.Lerp(1f, 1.3f, t);
                shadowScale = 1f;
            }
            else if (phaseTime < 0.45f)
            {
                // 늘어남 (stretch, 점프) - 15~45%
                float t = (phaseTime - 0.15f) / 0.3f;
                scaleY = baseScale.y * Mathf.Lerp(0.6f, 1.3f, t);
                scaleX = baseScale.x * Mathf.Lerp(1.3f, 0.8f, t);
                shadowScale = Mathf.Lerp(1f, 0.5f, t); // 그림자 작아짐
            }
            else if (phaseTime < 0.7f)
            {
                // 착지 복귀 - 45~70%
                float t = (phaseTime - 0.45f) / 0.25f;
                scaleY = baseScale.y * Mathf.Lerp(1.3f, 0.9f, t);
                scaleX = baseScale.x * Mathf.Lerp(0.8f, 1.1f, t);
                shadowScale = Mathf.Lerp(0.5f, 1.1f, t); // 그림자 복귀
            }
            else
            {
                // 안정화 - 70~100%
                float t = (phaseTime - 0.7f) / 0.3f;
                scaleY = baseScale.y * Mathf.Lerp(0.9f, 1f, t);
                scaleX = baseScale.x * Mathf.Lerp(1.1f, 1f, t);
                shadowScale = Mathf.Lerp(1.1f, 1f, t);
            }

            transform.localScale = new Vector3(scaleX, scaleY, baseScale.z);

            // 그림자 크기 조정
            if (shadowTransform != null)
            {
                float invX = scaleX > 0.01f ? baseScale.x / scaleX : 1f;
                float invY = scaleY > 0.01f ? baseScale.y / scaleY : 1f;
                shadowTransform.localScale = new Vector3(
                    invX * shadowScale,
                    invY * shadowScale,
                    1f
                );
            }
        }

        // ================================================================
        //  Skeleton Motion - 걷기 모션
        // ================================================================

        /// <summary>
        /// 해골: 좌우 흔들림 + 바운스 (사람이 걷는 느낌).
        /// </summary>
        private void UpdateSkeletonMotion(bool isMoving)
        {
            if (isMoving)
            {
                float phase = motionTimer * 6f * Mathf.PI * 2f; // 6Hz 걷기

                // 좌우 흔들림
                float swayX = Mathf.Sin(phase) * 0.04f;
                // 바운스 (한 걸음마다 위아래)
                float bounceY = Mathf.Abs(Mathf.Sin(phase)) * 0.05f;

                // 기울기 (좌우 발걸음에 따라)
                float tilt = Mathf.Sin(phase) * 3f;

                transform.localScale = new Vector3(
                    baseScale.x * (1f + Mathf.Sin(phase * 2f) * 0.03f),
                    baseScale.y * (1f + bounceY * 0.5f),
                    baseScale.z
                );
                transform.rotation = Quaternion.Euler(0f, 0f, tilt);
            }
            else
            {
                // idle: 약간의 호흡
                float breath = Mathf.Sin(motionTimer * 0.5f * Mathf.PI * 2f) * 0.01f;
                transform.localScale = new Vector3(
                    baseScale.x,
                    baseScale.y * (1f + breath),
                    baseScale.z
                );
                transform.rotation = Quaternion.Lerp(
                    transform.rotation,
                    Quaternion.identity,
                    Time.deltaTime * 8f
                );
            }
        }

        // ================================================================
        //  Bat Motion - 날개짓
        // ================================================================

        /// <summary>
        /// 박쥐: scaleX를 0.7~1.3으로 빠르게 교대 (날개 펄럭),
        /// 위아래 부유 (sin, 진폭 0.15, 2Hz).
        /// </summary>
        private void UpdateBatMotion(bool isMoving)
        {
            // 날개짓: scaleX 빠르게 교대 (12Hz)
            float wingPhase = motionTimer * 12f * Mathf.PI * 2f;
            float wingScale = Mathf.Lerp(0.7f, 1.3f, (Mathf.Sin(wingPhase) + 1f) * 0.5f);

            // 부유: 위아래 sin (2Hz, 진폭 0.15)
            float floatPhase = motionTimer * 2f * Mathf.PI * 2f;
            float floatY = Mathf.Sin(floatPhase) * 0.15f;

            transform.localScale = new Vector3(
                baseScale.x * wingScale,
                baseScale.y,
                baseScale.z
            );

            // 부유 오프셋은 그림자 위치로 표현 (Rigidbody 영향 회피)
            // 그림자를 아래로 더 내려 부유 효과 표현
            if (shadowTransform != null)
            {
                float invWing = wingScale > 0.01f ? 1f / wingScale : 1f;
                shadowTransform.localPosition = new Vector3(
                    0f,
                    ShadowOffsetY - floatY,
                    0f
                );
                shadowTransform.localScale = new Vector3(
                    invWing * (1f - Mathf.Abs(floatY) * 2f), // 높이 올라갈수록 그림자 작아짐
                    1f,
                    1f
                );
            }

            // 약간의 기울기 흔들림
            float tilt = Mathf.Sin(motionTimer * 1.5f * Mathf.PI * 2f) * 8f;
            transform.rotation = Quaternion.Euler(0f, 0f, tilt);
        }

        // ================================================================
        //  Fire Spirit Motion - 유령 부유
        // ================================================================

        /// <summary>
        /// 화염정령: 느린 위아래 부유 + 약간의 회전 + 크기 맥동(1.0~1.15).
        /// </summary>
        private void UpdateFireSpiritMotion(bool isMoving)
        {
            // 느린 부유 (1Hz)
            float floatPhase = motionTimer * 1f * Mathf.PI * 2f;
            float floatY = Mathf.Sin(floatPhase) * 0.1f;

            // 크기 맥동 (0.8Hz, 1.0~1.15)
            float pulsePhase = motionTimer * 0.8f * Mathf.PI * 2f;
            float pulse = Mathf.Lerp(1.0f, 1.15f, (Mathf.Sin(pulsePhase) + 1f) * 0.5f);

            // 느린 회전 (0.3Hz, ±10도)
            float rotPhase = motionTimer * 0.3f * Mathf.PI * 2f;
            float rotation = Mathf.Sin(rotPhase) * 10f;

            transform.localScale = new Vector3(
                baseScale.x * pulse,
                baseScale.y * pulse,
                baseScale.z
            );
            transform.rotation = Quaternion.Euler(0f, 0f, rotation);

            // 그림자: 부유에 따라 위치/크기 조정
            if (shadowTransform != null)
            {
                float invPulse = pulse > 0.01f ? 1f / pulse : 1f;
                shadowTransform.localPosition = new Vector3(
                    0f,
                    ShadowOffsetY - floatY,
                    0f
                );
                shadowTransform.localScale = new Vector3(
                    invPulse * (1f - Mathf.Abs(floatY) * 1.5f),
                    invPulse * (1f - Mathf.Abs(floatY) * 1.5f),
                    1f
                );
                shadowTransform.localRotation = Quaternion.Euler(0f, 0f, -rotation);
            }
        }

        // ================================================================
        //  Ice Golem Motion - 무거운 걸음
        // ================================================================

        /// <summary>
        /// 얼음골렘: 느린 바운스(진폭 0.03, 3Hz) + 이동 시 화면 미세 진동.
        /// </summary>
        private void UpdateIceGolemMotion(bool isMoving)
        {
            if (isMoving)
            {
                float phase = motionTimer * 3f * Mathf.PI * 2f;

                // 느린 바운스
                float bounceY = Mathf.Abs(Mathf.Sin(phase)) * 0.03f;

                transform.localScale = new Vector3(
                    baseScale.x * (1f + Mathf.Sin(phase * 0.5f) * 0.02f),
                    baseScale.y * (1f + bounceY),
                    baseScale.z
                );

                // 걸음 착지 시 화면 미세 진동
                float sinVal = Mathf.Sin(phase);
                float prevSinVal = Mathf.Sin((motionTimer - Time.deltaTime) * 3f * Mathf.PI * 2f);

                // sin 값이 양에서 음으로 바뀔 때 = 착지 순간
                if (prevSinVal > 0f && sinVal <= 0f)
                {
                    if (CameraController.Instance != null)
                    {
                        CameraController.Instance.Shake(0.08f, 0.1f);
                    }
                }
            }
            else
            {
                // idle: 거의 움직이지 않음 (무거움 표현)
                float breath = Mathf.Sin(motionTimer * 0.3f * Mathf.PI * 2f) * 0.005f;
                transform.localScale = new Vector3(
                    baseScale.x,
                    baseScale.y * (1f + breath),
                    baseScale.z
                );
            }

            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                Quaternion.identity,
                Time.deltaTime * 5f
            );
        }

        // ================================================================
        //  Default Motion - 기본 적 모션
        // ================================================================

        /// <summary>
        /// 타입을 알 수 없는 적: 기본 바운스 + 기울기.
        /// </summary>
        private void UpdateDefaultMotion(bool isMoving)
        {
            if (isMoving)
            {
                float phase = motionTimer * 6f * Mathf.PI * 2f;

                float bounceY = Mathf.Sin(phase) * 0.05f;
                float moveX = rb != null ? rb.linearVelocity.x : 0f;
                float tilt = -Mathf.Sign(moveX) * 3f * Mathf.Clamp01(Mathf.Abs(moveX) / 2f);

                float squashX = Mathf.Lerp(0.92f, 1.08f, (Mathf.Sin(phase) + 1f) * 0.5f);
                float squashY = Mathf.Lerp(1.08f, 0.92f, (Mathf.Sin(phase) + 1f) * 0.5f);

                transform.localScale = new Vector3(
                    baseScale.x * squashX,
                    baseScale.y * squashY,
                    baseScale.z
                );
                transform.rotation = Quaternion.Euler(0f, 0f, tilt);
            }
            else
            {
                float breath = Mathf.Sin(motionTimer * 0.5f * Mathf.PI * 2f) * 0.015f;
                transform.localScale = new Vector3(
                    baseScale.x,
                    baseScale.y * (1f + breath),
                    baseScale.z
                );
                transform.rotation = Quaternion.Lerp(
                    transform.rotation,
                    Quaternion.identity,
                    Time.deltaTime * 8f
                );
            }
        }

        // ================================================================
        //  Shadow
        // ================================================================

        private void CreateShadow()
        {
            var shadowGo = new GameObject("Shadow");
            shadowGo.transform.SetParent(transform);
            shadowGo.transform.localPosition = new Vector3(0f, ShadowOffsetY, 0f);
            shadowGo.transform.localRotation = Quaternion.identity;

            shadowRenderer = shadowGo.AddComponent<SpriteRenderer>();

            // 16x8 타원 텍스처 프로시저럴 생성
            var tex = new Texture2D(ShadowTexWidth, ShadowTexHeight, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            float halfW = ShadowTexWidth * 0.5f;
            float halfH = ShadowTexHeight * 0.5f;

            for (int y = 0; y < ShadowTexHeight; y++)
            {
                for (int x = 0; x < ShadowTexWidth; x++)
                {
                    float dx = (x - halfW) / halfW;
                    float dy = (y - halfH) / halfH;
                    bool inside = (dx * dx + dy * dy) <= 1f;
                    tex.SetPixel(x, y, inside ? new Color(0f, 0f, 0f, 0.3f) : Color.clear);
                }
            }
            tex.Apply();

            shadowRenderer.sprite = Sprite.Create(
                tex,
                new Rect(0, 0, ShadowTexWidth, ShadowTexHeight),
                new Vector2(0.5f, 0.5f),
                16f
            );
            shadowRenderer.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder - 1 : -1;
            shadowRenderer.sortingLayerName = spriteRenderer != null ? spriteRenderer.sortingLayerName : "Default";

            shadowTransform = shadowGo.transform;
        }

        private void UpdateShadow()
        {
            if (shadowTransform == null) return;

            // 그림자 회전 상쇄 (항상 수평 유지)
            shadowTransform.rotation = Quaternion.identity;
        }

        private void OnDestroy()
        {
            if (shadowTransform != null && shadowTransform.gameObject != null)
            {
                Destroy(shadowTransform.gameObject);
            }
        }

        // ================================================================
        //  Public API
        // ================================================================

        /// <summary>
        /// 기본 스케일을 갱신한다. MonsterVariety에서 크기 변경 후 호출.
        /// </summary>
        public void RefreshBaseScale()
        {
            baseScale = transform.localScale;
        }
    }
}
