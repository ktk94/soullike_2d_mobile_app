using UnityEngine;

namespace SoulCraft.Factory
{
    /// <summary>
    /// 모든 파티클 시스템을 런타임 코드로 생성한다.
    /// ParticleSystem 컴포넌트를 AddComponent로 추가하고 모듈을 설정한다.
    /// 각 메서드는 완성된 ParticleSystem이 붙은 GameObject를 반환한다.
    /// </summary>
    public static class ParticleFactory
    {
        // ================================================================
        //  Shared Helpers
        // ================================================================

        /// <summary>
        /// 기본 파티클 머테리얼을 반환한다 (Sprites-Default).
        /// </summary>
        private static Material GetDefaultParticleMaterial()
        {
            // Sprites-Default는 Unity 빌트인 머테리얼
            return new Material(Shader.Find("Sprites/Default"));
        }

        /// <summary>
        /// 새 GameObject에 ParticleSystem을 추가하고 렌더러를 설정한다.
        /// </summary>
        private static ParticleSystem CreateBaseParticle(string name)
        {
            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();

            // 기본 emission 비활성화 (개별 설정에서 재활성화)
            var emission = ps.emission;
            emission.enabled = false;

            // 렌더러 설정
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = GetDefaultParticleMaterial();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingLayerName = "Effects";
            renderer.sortingOrder = 100;

            // 기본: 재생 후 자동 정지
            var main = ps.main;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            return ps;
        }

        /// <summary>
        /// SpriteFactory에서 텍스처를 가져와 렌더러에 적용한다.
        /// </summary>
        private static void ApplyParticleTexture(ParticleSystem ps, string spriteKey)
        {
            var tex = SpriteFactory.GetTexture(spriteKey);
            if (tex != null)
            {
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer != null && renderer.material != null)
                    renderer.material.mainTexture = tex;
            }
        }

        /// <summary>
        /// 자동 파괴 설정: stopAction = Destroy
        /// </summary>
        private static void SetAutoDestroy(ParticleSystem ps)
        {
            var main = ps.main;
            main.stopAction = ParticleSystemStopAction.Destroy;
        }

        // ================================================================
        //  1. CreateHitParticle - 타격 시 스파크
        // ================================================================

        /// <summary>
        /// 타격 시 스파크 파티클. 짧은 수명(0.3초), 방사형 burst(8~12개),
        /// 빠른 속도 후 감속, 크기 감소.
        /// </summary>
        public static GameObject CreateHitParticle(Color color)
        {
            var ps = CreateBaseParticle("FX_Hit");
            ApplyParticleTexture(ps, "particle_spark");
            SetAutoDestroy(ps);

            // Main
            var main = ps.main;
            main.duration = 0.3f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.15f);
            main.startColor = color;
            main.gravityModifier = 0.5f;
            main.maxParticles = 15;

            // Emission: burst
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 8, 12)
            });

            // Shape: sphere (방사형)
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;

            // Size over Lifetime: 줄어듦
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            // Velocity over Lifetime: 감속 (Drag 대신 속도 감소)
            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.speedModifier = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.1f));

            // Color over Lifetime: 페이드 아웃
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            return ps.gameObject;
        }

        // ================================================================
        //  2. CreateSlashParticle - 검격 파티클
        // ================================================================

        /// <summary>
        /// 검격 파티클. 호 형태, 흰색, 짧은 수명.
        /// </summary>
        public static GameObject CreateSlashParticle()
        {
            var ps = CreateBaseParticle("FX_Slash");
            ApplyParticleTexture(ps, "particle_slash");
            SetAutoDestroy(ps);

            var main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.startLifetime = 0.15f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
            main.startColor = new Color(1f, 1f, 1f, 0.9f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-30f * Mathf.Deg2Rad, 30f * Mathf.Deg2Rad);
            main.maxParticles = 3;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 1, 3)
            });

            var shape = ps.shape;
            shape.enabled = false;

            // Size over Lifetime: 빠르게 커졌다가 유지
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.3f),
                    new Keyframe(0.3f, 1f),
                    new Keyframe(1f, 1f)
                ));

            // Color over Lifetime: 빠른 페이드 아웃
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) }
            );
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            return ps.gameObject;
        }

        // ================================================================
        //  3. CreateFireParticle - 화염 파티클
        // ================================================================

        /// <summary>
        /// 화염 파티클. 주황->빨강 색상, 위로 올라감, 크기 감소, 연속 발생.
        /// </summary>
        public static GameObject CreateFireParticle()
        {
            var ps = CreateBaseParticle("FX_Fire");
            ApplyParticleTexture(ps, "particle_circle");
            SetAutoDestroy(ps);

            var main = ps.main;
            main.duration = 2f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            main.startColor = new Color(1f, 0.6f, 0.1f);
            main.gravityModifier = -0.3f; // 위로 올라감
            main.maxParticles = 50;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 25f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15f;
            shape.radius = 0.2f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // 위로 방출

            // Color over Lifetime: 주황 → 빨강 → 투명
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.8f, 0.2f), 0f),
                    new GradientColorKey(new Color(1f, 0.3f, 0.05f), 0.5f),
                    new GradientColorKey(new Color(0.5f, 0.1f, 0.05f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.8f, 0f),
                    new GradientAlphaKey(0.6f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            // Size over Lifetime: 줄어듦
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));

            // Velocity over Lifetime: 약간의 좌우 흔들림
            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.x = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);

            return ps.gameObject;
        }

        // ================================================================
        //  4. CreateIceParticle - 얼음 파티클
        // ================================================================

        /// <summary>
        /// 얼음 파티클. 파랑/하늘색, 느리게 떨어짐, 반짝임.
        /// </summary>
        public static GameObject CreateIceParticle()
        {
            var ps = CreateBaseParticle("FX_Ice");
            ApplyParticleTexture(ps, "particle_diamond");
            SetAutoDestroy(ps);

            var main = ps.main;
            main.duration = 2f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.5f, 0.8f, 1f),
                new Color(0.7f, 0.9f, 1f)
            );
            main.gravityModifier = 0.2f; // 느리게 떨어짐
            main.maxParticles = 40;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 15f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;

            // Color over Lifetime: 반짝임 (알파 진동)
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.6f, 0.85f, 1f), 0f),
                    new GradientColorKey(new Color(0.4f, 0.7f, 1f), 0.5f),
                    new GradientColorKey(new Color(0.5f, 0.8f, 1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.8f, 0f),
                    new GradientAlphaKey(1f, 0.25f),
                    new GradientAlphaKey(0.5f, 0.5f),
                    new GradientAlphaKey(1f, 0.75f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            // Size over Lifetime
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

            // Rotation over Lifetime: 천천히 회전
            var rol = ps.rotationOverLifetime;
            rol.enabled = true;
            rol.z = new ParticleSystem.MinMaxCurve(-90f * Mathf.Deg2Rad, 90f * Mathf.Deg2Rad);

            return ps.gameObject;
        }

        // ================================================================
        //  5. CreateLightningParticle - 번개 파티클
        // ================================================================

        /// <summary>
        /// 번개 파티클. 노란색, 빠르게 번쩍, 짧은 수명.
        /// </summary>
        public static GameObject CreateLightningParticle()
        {
            var ps = CreateBaseParticle("FX_Lightning");
            ApplyParticleTexture(ps, "particle_spark");
            SetAutoDestroy(ps);

            var main = ps.main;
            main.duration = 0.3f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 10f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.1f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 1f, 0.5f),
                new Color(1f, 0.95f, 0.3f)
            );
            main.gravityModifier = 0f;
            main.maxParticles = 30;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 15, 25),
                new ParticleSystem.Burst(0.05f, 5, 10)
            });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            // Color over Lifetime: 빠른 번쩍 후 소멸
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(1f, 1f, 0.3f), 0.3f),
                    new GradientColorKey(new Color(0.8f, 0.6f, 0.1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.8f, 0.2f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            // Size over Lifetime
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            return ps.gameObject;
        }

        // ================================================================
        //  6. CreateDarkParticle - 어둠 파티클
        // ================================================================

        /// <summary>
        /// 어둠 파티클. 보라/검정, 소용돌이, 안으로 모임.
        /// </summary>
        public static GameObject CreateDarkParticle()
        {
            var ps = CreateBaseParticle("FX_Dark");
            ApplyParticleTexture(ps, "particle_circle");
            SetAutoDestroy(ps);

            var main = ps.main;
            main.duration = 2f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.3f, 0.0f, 0.4f),
                new Color(0.15f, 0.0f, 0.2f)
            );
            main.gravityModifier = 0f;
            main.maxParticles = 40;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 20f;

            // Shape: 바깥 원에서 시작
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.0f;
            shape.radiusThickness = 0f; // 엣지에서만 방출

            // Velocity over Lifetime: 중심을 향해 모이는 소용돌이
            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            // 안쪽으로 끌리는 속도 (orbital)
            vol.orbitalZ = new ParticleSystem.MinMaxCurve(2f, 4f);
            vol.radial = new ParticleSystem.MinMaxCurve(-2f, -1f); // 안으로 모임

            // Color over Lifetime
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.4f, 0.0f, 0.6f), 0f),
                    new GradientColorKey(new Color(0.1f, 0.0f, 0.15f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.7f, 0.3f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            // Size over Lifetime: 줄어듦 (중심에서 소멸)
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            return ps.gameObject;
        }

        // ================================================================
        //  7. CreateHolyParticle - 성스러운 파티클
        // ================================================================

        /// <summary>
        /// 성스러운 파티클. 흰/금, 위로 올라감, 빛남.
        /// </summary>
        public static GameObject CreateHolyParticle()
        {
            var ps = CreateBaseParticle("FX_Holy");
            ApplyParticleTexture(ps, "particle_star");
            SetAutoDestroy(ps);

            var main = ps.main;
            main.duration = 2f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.15f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.95f, 0.7f),
                new Color(1f, 1f, 1f)
            );
            main.gravityModifier = -0.5f; // 위로 올라감
            main.maxParticles = 40;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 15f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.3f;

            // Color over Lifetime: 빛나는 효과
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 1f, 0.85f), 0f),
                    new GradientColorKey(new Color(1f, 0.9f, 0.5f), 0.5f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.9f, 0.2f),
                    new GradientAlphaKey(0.6f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            // Size over Lifetime: 약간 커졌다가 줄어듦
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.5f),
                    new Keyframe(0.3f, 1f),
                    new Keyframe(1f, 0f)
                ));

            // Rotation over Lifetime
            var rol = ps.rotationOverLifetime;
            rol.enabled = true;
            rol.z = new ParticleSystem.MinMaxCurve(45f * Mathf.Deg2Rad, 180f * Mathf.Deg2Rad);

            return ps.gameObject;
        }

        // ================================================================
        //  8. CreateDashGhostParticle - 대시 잔상
        // ================================================================

        /// <summary>
        /// 대시 잔상 파티클. 반투명 복제, 페이드 아웃.
        /// 플레이어 스프라이트를 텍스처로 사용.
        /// </summary>
        public static GameObject CreateDashGhostParticle()
        {
            var ps = CreateBaseParticle("FX_DashGhost");
            ApplyParticleTexture(ps, "player_idle");
            SetAutoDestroy(ps);

            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = 0.3f;
            main.startSpeed = 0f;
            main.startSize = 1f;
            main.startColor = new Color(0.5f, 0.7f, 1f, 0.5f);
            main.maxParticles = 5;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 1),
                new ParticleSystem.Burst(0.05f, 1),
                new ParticleSystem.Burst(0.1f, 1)
            });

            var shape = ps.shape;
            shape.enabled = false;

            // Color over Lifetime: 페이드 아웃
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.5f, 0.7f, 1f), 0f),
                    new GradientColorKey(new Color(0.3f, 0.5f, 0.8f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.5f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            // 렌더러: billboard이 아닌 Mesh 또는 Stretch
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            return ps.gameObject;
        }

        // ================================================================
        //  9. CreateDustParticle - 이동 먼지
        // ================================================================

        /// <summary>
        /// 이동 먼지 파티클. 갈색/회색, 작은 크기, 바닥에서 올라옴.
        /// </summary>
        public static GameObject CreateDustParticle()
        {
            var ps = CreateBaseParticle("FX_Dust");
            ApplyParticleTexture(ps, "particle_circle");
            SetAutoDestroy(ps);

            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.6f, 0.5f, 0.35f, 0.6f),
                new Color(0.5f, 0.5f, 0.5f, 0.4f)
            );
            main.gravityModifier = -0.2f; // 약간 위로
            main.maxParticles = 10;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 3, 6)
            });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.15f;

            // Color over Lifetime: 페이드 아웃
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.6f, 0.5f, 0.35f), 0f),
                    new GradientColorKey(new Color(0.5f, 0.5f, 0.5f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.6f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            // Size over Lifetime
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

            return ps.gameObject;
        }

        // ================================================================
        //  10. CreateLevelUpParticle - 레벨업 기둥
        // ================================================================

        /// <summary>
        /// 레벨업 기둥 파티클. 금색, 아래에서 위로 기둥 형태.
        /// </summary>
        public static GameObject CreateLevelUpParticle()
        {
            var ps = CreateBaseParticle("FX_LevelUp");
            ApplyParticleTexture(ps, "particle_star");
            SetAutoDestroy(ps);

            var main = ps.main;
            main.duration = 1.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.85f, 0.2f),
                new Color(1f, 0.95f, 0.6f)
            );
            main.gravityModifier = 0f;
            main.maxParticles = 80;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 30, 50),
                new ParticleSystem.Burst(0.2f, 10, 20)
            });

            // Shape: 얇은 원 (기둥 형태로 위로 방출)
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 5f; // 좁은 각도 -> 기둥
            shape.radius = 0.3f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // 위로 방출

            // Color over Lifetime: 금색 유지 후 페이드 아웃
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.9f, 0.3f), 0f),
                    new GradientColorKey(new Color(1f, 1f, 0.7f), 0.5f),
                    new GradientColorKey(new Color(1f, 0.85f, 0.2f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.8f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            // Size over Lifetime
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.5f),
                    new Keyframe(0.2f, 1f),
                    new Keyframe(1f, 0f)
                ));

            // Rotation over Lifetime
            var rol = ps.rotationOverLifetime;
            rol.enabled = true;
            rol.z = new ParticleSystem.MinMaxCurve(90f * Mathf.Deg2Rad, 360f * Mathf.Deg2Rad);

            return ps.gameObject;
        }

        // ================================================================
        //  11. CreateItemPickupParticle - 아이템 획득 반짝임
        // ================================================================

        /// <summary>
        /// 아이템 획득 반짝임. 흰색 별, burst.
        /// </summary>
        public static GameObject CreateItemPickupParticle()
        {
            var ps = CreateBaseParticle("FX_ItemPickup");
            ApplyParticleTexture(ps, "particle_star");
            SetAutoDestroy(ps);

            var main = ps.main;
            main.duration = 0.4f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startColor = new Color(1f, 1f, 1f, 0.9f);
            main.gravityModifier = -0.2f;
            main.maxParticles = 15;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 8, 12)
            });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;

            // Color over Lifetime
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(1f, 1f, 0.8f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            // Size over Lifetime
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            // Rotation over Lifetime
            var rol = ps.rotationOverLifetime;
            rol.enabled = true;
            rol.z = new ParticleSystem.MinMaxCurve(180f * Mathf.Deg2Rad, 540f * Mathf.Deg2Rad);

            return ps.gameObject;
        }

        // ================================================================
        //  12. CreateDeathParticle - 적 사망 파편
        // ================================================================

        /// <summary>
        /// 적 사망 파티클. 해당 색상 파편이 흩어짐.
        /// </summary>
        public static GameObject CreateDeathParticle(Color baseColor)
        {
            var ps = CreateBaseParticle("FX_Death");
            ApplyParticleTexture(ps, "particle_circle");
            SetAutoDestroy(ps);

            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                baseColor,
                Color.Lerp(baseColor, Color.black, 0.3f)
            );
            main.gravityModifier = 1.5f; // 중력으로 떨어짐
            main.maxParticles = 25;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 12, 20)
            });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;

            // Color over Lifetime: 기본 색 → 어둡게 + 페이드 아웃
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(baseColor, 0f),
                    new GradientColorKey(Color.Lerp(baseColor, Color.black, 0.5f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.5f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            // Size over Lifetime
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            // Velocity over Lifetime: 감속
            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.speedModifier = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

            return ps.gameObject;
        }

        // ================================================================
        //  13. CreateBossEnrageParticle - 보스 분노 오라
        // ================================================================

        /// <summary>
        /// 보스 분노 파티클. 빨간 오라, 크고 지속적.
        /// </summary>
        public static GameObject CreateBossEnrageParticle()
        {
            var ps = CreateBaseParticle("FX_BossEnrage");
            ApplyParticleTexture(ps, "particle_circle");
            // 분노 파티클은 자동 파괴하지 않음 (지속적)

            var main = ps.main;
            main.duration = 0f; // 무한 (loop)
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.1f, 0.1f, 0.7f),
                new Color(0.8f, 0.0f, 0.0f, 0.5f)
            );
            main.gravityModifier = -0.3f;
            main.maxParticles = 60;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 30f;

            // Shape: 보스 주변 원형
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.0f;
            shape.radiusThickness = 1f; // 전체 영역

            // Color over Lifetime: 빨강 → 어두운 빨강 + 페이드 아웃
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.2f, 0.1f), 0f),
                    new GradientColorKey(new Color(0.6f, 0.0f, 0.0f), 0.7f),
                    new GradientColorKey(new Color(0.3f, 0.0f, 0.0f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.6f, 0.2f),
                    new GradientAlphaKey(0.4f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            // Size over Lifetime: 커졌다가 줄어듦
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.3f),
                    new Keyframe(0.3f, 1f),
                    new Keyframe(1f, 0.5f)
                ));

            // Velocity over Lifetime: 위로 올라가며 약간 소용돌이
            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.y = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            vol.orbitalY = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);

            return ps.gameObject;
        }

        // ================================================================
        //  14. CreateHealParticle - 힐 파티클
        // ================================================================

        /// <summary>
        /// 힐 파티클. 초록색 + 모양, 위로 올라감.
        /// </summary>
        public static GameObject CreateHealParticle()
        {
            var ps = CreateBaseParticle("FX_Heal");
            ApplyParticleTexture(ps, "particle_cross");
            SetAutoDestroy(ps);

            var main = ps.main;
            main.duration = 1.0f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.15f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.2f, 1f, 0.4f),
                new Color(0.4f, 1f, 0.6f)
            );
            main.gravityModifier = -0.5f; // 위로
            main.maxParticles = 30;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 8, 15),
                new ParticleSystem.Burst(0.2f, 5, 10)
            });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.4f;

            // Color over Lifetime: 초록 유지 후 페이드 아웃
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.3f, 1f, 0.5f), 0f),
                    new GradientColorKey(new Color(0.5f, 1f, 0.7f), 0.5f),
                    new GradientColorKey(new Color(0.2f, 0.8f, 0.4f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.9f, 0.15f),
                    new GradientAlphaKey(0.7f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            // Size over Lifetime
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.5f),
                    new Keyframe(0.2f, 1f),
                    new Keyframe(1f, 0.3f)
                ));

            // Rotation over Lifetime: 느리게 회전
            var rol = ps.rotationOverLifetime;
            rol.enabled = true;
            rol.z = new ParticleSystem.MinMaxCurve(-45f * Mathf.Deg2Rad, 45f * Mathf.Deg2Rad);

            // Velocity over Lifetime: 약간의 좌우 흔들림
            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);

            return ps.gameObject;
        }
    }
}
