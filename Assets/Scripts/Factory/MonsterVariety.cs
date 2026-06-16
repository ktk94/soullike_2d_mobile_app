using UnityEngine;
using SoulCraft.Core;
using SoulCraft.Enemy;

namespace SoulCraft.Factory
{
    /// <summary>
    /// 몬스터 다양화 시스템.
    /// 크기/색상 변이, 엘리트 몬스터, 속성 변이, 프로시저럴 파티클 부착을 처리한다.
    /// </summary>
    public static class MonsterVariety
    {
        // ================================================================
        //  Constants
        // ================================================================

        // 크기 변이 범위
        private const float SizeVariationMin = 0.85f;
        private const float SizeVariationMax = 1.15f;

        // 색상 변이 범위
        private const float HueVariation = 0.10f;
        private const float SaturationVariation = 0.10f;

        // 엘리트 확률: roomIndex에 따라 5%~15%
        private const float EliteBaseChance = 0.05f;
        private const float EliteChancePerRoom = 0.02f;
        private const float EliteMaxChance = 0.15f;

        // 엘리트 배율
        private const float EliteSizeMultiplier = 1.3f;
        private const float EliteHpMultiplier = 1.5f;
        private const float EliteAtkMultiplier = 1.5f;
        private const float EliteRewardMultiplier = 2f;

        // 속성 부여 확률
        private const float ElementChance = 0.2f;

        // ================================================================
        //  Main API
        // ================================================================

        /// <summary>
        /// 몬스터에 다양화를 적용한다.
        /// 크기 변이, 색상 변이, 엘리트 여부, 속성 부여를 한 번에 처리.
        /// </summary>
        /// <param name="enemy">대상 적 GameObject</param>
        /// <param name="type">적 타입 ("slime", "skeleton", "bat", "fire_spirit", "ice_golem")</param>
        /// <param name="roomIndex">현재 방 인덱스 (0-based, 엘리트 확률에 영향)</param>
        public static void ApplyVariety(GameObject enemy, string type, int roomIndex)
        {
            if (enemy == null) return;

            var sr = enemy.GetComponent<SpriteRenderer>();
            if (sr == null) return;

            // 1. 크기 변이
            ApplySizeVariation(enemy);

            // 2. 색상 변이
            ApplyColorVariation(sr);

            // 3. 엘리트 판정
            bool isElite = TryMakeElite(enemy, type, roomIndex);

            // 4. 속성 변이 (엘리트가 아닌 경우에도 적용 가능)
            TryApplyElement(enemy, type, sr);

            // 5. EnemyMotion의 baseScale 갱신
            var motion = enemy.GetComponent<EnemyMotion>();
            if (motion != null)
            {
                motion.RefreshBaseScale();
            }

            // 6. EnemyHitReaction의 원래 색상/스케일 갱신
            var hitReaction = enemy.GetComponent<EnemyHitReaction>();
            if (hitReaction != null)
            {
                hitReaction.RefreshOriginalColor();
                hitReaction.RefreshOriginalScale();
            }
        }

        // ================================================================
        //  Size Variation
        // ================================================================

        /// <summary>
        /// 기본 크기에 +/-15% 랜덤 변이를 적용한다.
        /// </summary>
        private static void ApplySizeVariation(GameObject enemy)
        {
            float sizeMultiplier = Random.Range(SizeVariationMin, SizeVariationMax);
            enemy.transform.localScale *= sizeMultiplier;
        }

        // ================================================================
        //  Color Variation
        // ================================================================

        /// <summary>
        /// Hue +/-10%, Saturation +/-10% 랜덤 색상 변이를 적용한다.
        /// </summary>
        private static void ApplyColorVariation(SpriteRenderer sr)
        {
            Color originalColor = sr.color;
            Color.RGBToHSV(originalColor, out float h, out float s, out float v);

            h += Random.Range(-HueVariation, HueVariation);
            s += Random.Range(-SaturationVariation, SaturationVariation);

            // Hue는 0~1 래핑
            h = Mathf.Repeat(h, 1f);
            s = Mathf.Clamp01(s);
            v = Mathf.Clamp01(v);

            Color newColor = Color.HSVToRGB(h, s, v);
            newColor.a = originalColor.a;
            sr.color = newColor;
        }

        // ================================================================
        //  Elite Monster
        // ================================================================

        /// <summary>
        /// 확률적으로 엘리트 몬스터로 승격한다.
        /// 엘리트: 크기 1.3배, 금색 외곽 글로우, HP/ATK 1.5배, 이름에 "강화된" 접두사, 드롭 보상 2배.
        /// </summary>
        /// <returns>엘리트가 되었으면 true</returns>
        private static bool TryMakeElite(GameObject enemy, string type, int roomIndex)
        {
            float eliteChance = Mathf.Clamp(
                EliteBaseChance + roomIndex * EliteChancePerRoom,
                EliteBaseChance,
                EliteMaxChance
            );

            if (Random.value > eliteChance) return false;

            // --- 엘리트 승격 ---

            // 크기 증가
            enemy.transform.localScale *= EliteSizeMultiplier;

            // 이름에 "강화된" 접두사
            if (!enemy.name.StartsWith("강화된"))
            {
                enemy.name = $"강화된_{enemy.name}";
            }

            // EnemyBase 스탯 증폭
            var enemyBase = enemy.GetComponent<EnemyBase>();
            if (enemyBase != null && enemyBase.Data != null)
            {
                var data = enemyBase.Data;
                data.maxHp = Mathf.RoundToInt(data.maxHp * EliteHpMultiplier);
                data.attack = Mathf.RoundToInt(data.attack * EliteAtkMultiplier);
                data.expReward = Mathf.RoundToInt(data.expReward * EliteRewardMultiplier);
                data.goldReward = Mathf.RoundToInt(data.goldReward * EliteRewardMultiplier);

                // HP 재초기화
                enemyBase.InitializeEnemy();
            }

            // 금색 외곽 글로우 효과 (별도 자식 SpriteRenderer로 구현)
            CreateEliteGlow(enemy);

            Debug.Log($"[MonsterVariety] 엘리트 몬스터 생성: {enemy.name}");

            return true;
        }

        /// <summary>
        /// 엘리트 금색 외곽 글로우를 생성한다.
        /// 약간 크고 금색 반투명인 자식 스프라이트.
        /// </summary>
        private static void CreateEliteGlow(GameObject enemy)
        {
            var sr = enemy.GetComponent<SpriteRenderer>();
            if (sr == null) return;

            var glowGo = new GameObject("EliteGlow");
            glowGo.transform.SetParent(enemy.transform);
            glowGo.transform.localPosition = Vector3.zero;
            glowGo.transform.localRotation = Quaternion.identity;
            glowGo.transform.localScale = Vector3.one * 1.2f; // 원본보다 20% 크게

            var glowSr = glowGo.AddComponent<SpriteRenderer>();
            glowSr.sprite = sr.sprite;
            glowSr.color = new Color(1f, 0.85f, 0.2f, 0.35f); // 금색 반투명
            glowSr.sortingOrder = sr.sortingOrder - 1;
            glowSr.sortingLayerName = sr.sortingLayerName;

            // 글로우 맥동 효과
            var pulsator = glowGo.AddComponent<EliteGlowPulsator>();
        }

        // ================================================================
        //  Element Variation
        // ================================================================

        /// <summary>
        /// 확률적으로 속성을 부여한다.
        /// 속성별 색상 틴트와 파티클을 추가하고, 속성 데미지를 부여한다.
        /// </summary>
        private static void TryApplyElement(GameObject enemy, string type, SpriteRenderer sr)
        {
            if (Random.value > ElementChance) return;

            // 적 타입에 따른 속성 매칭
            ElementType element = GetRandomElement(type);

            switch (element)
            {
                case ElementType.Fire:
                    ApplyFireElement(enemy, sr);
                    break;
                case ElementType.Ice:
                    ApplyIceElement(enemy, sr);
                    break;
                case ElementType.Lightning:
                    ApplyLightningElement(enemy, sr);
                    break;
            }
        }

        private enum ElementType
        {
            Fire,
            Ice,
            Lightning
        }

        /// <summary>
        /// 적 타입에 따라 자연스러운 속성을 선택한다.
        /// </summary>
        private static ElementType GetRandomElement(string type)
        {
            string lower = type.ToLower();

            // 특정 타입은 특정 속성과 어울림 (가중치 부여)
            if (lower.Contains("slime"))
            {
                // 슬라임은 불 속성이 잘 어울림
                float roll = Random.value;
                if (roll < 0.5f) return ElementType.Fire;
                if (roll < 0.8f) return ElementType.Ice;
                return ElementType.Lightning;
            }
            if (lower.Contains("skeleton"))
            {
                // 해골은 얼음 속성이 잘 어울림
                float roll = Random.value;
                if (roll < 0.5f) return ElementType.Ice;
                if (roll < 0.8f) return ElementType.Fire;
                return ElementType.Lightning;
            }
            if (lower.Contains("bat"))
            {
                // 박쥐는 전기 속성이 잘 어울림
                float roll = Random.value;
                if (roll < 0.5f) return ElementType.Lightning;
                if (roll < 0.8f) return ElementType.Fire;
                return ElementType.Ice;
            }

            // 기본: 균등 확률
            int idx = Random.Range(0, 3);
            return (ElementType)idx;
        }

        // ── 불 속성 ─────────────────────────────────────────────

        /// <summary>
        /// 불 속성: 빨간 틴트 + 화염 파티클 + 화염 데미지.
        /// </summary>
        private static void ApplyFireElement(GameObject enemy, SpriteRenderer sr)
        {
            // 색상 틴트: 빨강으로
            Color baseColor = sr.color;
            sr.color = Color.Lerp(baseColor, new Color(1f, 0.3f, 0.1f, baseColor.a), 0.4f);

            // 이름에 속성 표시
            if (!enemy.name.Contains("불타는"))
            {
                enemy.name = $"불타는_{enemy.name}";
            }

            // 프로시저럴 화염 파티클 생성
            CreateFireParticleOnEnemy(enemy);

            // 속성 데미지 (EnemyBase의 data에 반영)
            var enemyBase = enemy.GetComponent<EnemyBase>();
            if (enemyBase != null && enemyBase.Data != null)
            {
                // 공격력 10% 증가 (화염 추가 데미지 표현)
                enemyBase.Data.attack = Mathf.RoundToInt(enemyBase.Data.attack * 1.1f);
            }

            Debug.Log($"[MonsterVariety] 불 속성 부여: {enemy.name}");
        }

        // ── 얼음 속성 ───────────────────────────────────────────

        /// <summary>
        /// 얼음 속성: 파란 틴트 + 눈 파티클.
        /// </summary>
        private static void ApplyIceElement(GameObject enemy, SpriteRenderer sr)
        {
            Color baseColor = sr.color;
            sr.color = Color.Lerp(baseColor, new Color(0.3f, 0.6f, 1f, baseColor.a), 0.4f);

            if (!enemy.name.Contains("얼어붙은"))
            {
                enemy.name = $"얼어붙은_{enemy.name}";
            }

            CreateIceParticleOnEnemy(enemy);

            var enemyBase = enemy.GetComponent<EnemyBase>();
            if (enemyBase != null && enemyBase.Data != null)
            {
                // 방어력 증가 (얼음 갑옷 표현)
                enemyBase.Data.defense = Mathf.RoundToInt(enemyBase.Data.defense * 1.3f);
            }

            Debug.Log($"[MonsterVariety] 얼음 속성 부여: {enemy.name}");
        }

        // ── 전기 속성 ───────────────────────────────────────────

        /// <summary>
        /// 전기 속성: 노란 틴트 + 스파크 파티클.
        /// </summary>
        private static void ApplyLightningElement(GameObject enemy, SpriteRenderer sr)
        {
            Color baseColor = sr.color;
            sr.color = Color.Lerp(baseColor, new Color(1f, 1f, 0.3f, baseColor.a), 0.4f);

            if (!enemy.name.Contains("전기"))
            {
                enemy.name = $"전기_{enemy.name}";
            }

            CreateLightningParticleOnEnemy(enemy);

            var enemyBase = enemy.GetComponent<EnemyBase>();
            if (enemyBase != null && enemyBase.Data != null)
            {
                // 이동 속도 증가 (전기로 빠르게)
                enemyBase.Data.speed *= 1.2f;
            }

            Debug.Log($"[MonsterVariety] 전기 속성 부여: {enemy.name}");
        }

        // ================================================================
        //  Procedural Particle Creation
        // ================================================================

        /// <summary>
        /// 적에게 화염 파티클을 부착한다.
        /// 주황/빨강 불꽃이 위로 올라가는 ParticleSystem을 코드로 생성.
        /// </summary>
        private static void CreateFireParticleOnEnemy(GameObject enemy)
        {
            var psGo = new GameObject("FX_FireAura");
            psGo.transform.SetParent(enemy.transform);
            psGo.transform.localPosition = new Vector3(0f, -0.1f, 0f);
            psGo.transform.localRotation = Quaternion.identity;
            psGo.transform.localScale = Vector3.one;

            var ps = psGo.AddComponent<ParticleSystem>();

            // Main module
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.4f, 0.05f, 0.8f),
                new Color(1f, 0.7f, 0.1f, 0.9f)
            );
            main.gravityModifier = -0.8f; // 위로 올라감
            main.maxParticles = 20;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            // Emission
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 12f;

            // Shape: 작은 원 (적 몸체 주변)
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.15f;

            // Color over Lifetime: 주황 -> 빨강 -> 소멸
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.7f, 0.1f), 0f),
                    new GradientColorKey(new Color(1f, 0.2f, 0.05f), 0.6f),
                    new GradientColorKey(new Color(0.5f, 0.1f, 0.05f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.8f, 0f),
                    new GradientAlphaKey(0.5f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            // Size over Lifetime: 줄어듦
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));

            // Renderer
            var renderer = psGo.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingLayerName = "Enemy";
            renderer.sortingOrder = 6;
        }

        /// <summary>
        /// 적에게 눈/얼음 파티클을 부착한다.
        /// 하얀/하늘색 눈이 느리게 떨어지는 ParticleSystem을 코드로 생성.
        /// </summary>
        private static void CreateIceParticleOnEnemy(GameObject enemy)
        {
            var psGo = new GameObject("FX_IceAura");
            psGo.transform.SetParent(enemy.transform);
            psGo.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            psGo.transform.localRotation = Quaternion.identity;
            psGo.transform.localScale = Vector3.one;

            var ps = psGo.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.7f, 0.9f, 1f, 0.7f),
                new Color(1f, 1f, 1f, 0.8f)
            );
            main.gravityModifier = 0.3f; // 느리게 떨어짐
            main.maxParticles = 15;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 8f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;

            // Color over Lifetime: 반짝이다 사라짐
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.8f, 0.95f, 1f), 0f),
                    new GradientColorKey(new Color(0.6f, 0.85f, 1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.8f, 0f),
                    new GradientAlphaKey(0.5f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            // Size over Lifetime
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.4f));

            // Rotation over Lifetime: 천천히 회전
            var rol = ps.rotationOverLifetime;
            rol.enabled = true;
            rol.z = new ParticleSystem.MinMaxCurve(-90f * Mathf.Deg2Rad, 90f * Mathf.Deg2Rad);

            var renderer = psGo.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingLayerName = "Enemy";
            renderer.sortingOrder = 6;
        }

        /// <summary>
        /// 적에게 전기 스파크 파티클을 부착한다.
        /// 노란색 스파크가 랜덤 방향으로 빠르게 튀는 ParticleSystem을 코드로 생성.
        /// </summary>
        private static void CreateLightningParticleOnEnemy(GameObject enemy)
        {
            var psGo = new GameObject("FX_LightningAura");
            psGo.transform.SetParent(enemy.transform);
            psGo.transform.localPosition = Vector3.zero;
            psGo.transform.localRotation = Quaternion.identity;
            psGo.transform.localScale = Vector3.one;

            var ps = psGo.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 1f, 0.3f, 0.9f),
                new Color(1f, 0.95f, 0.5f, 1f)
            );
            main.gravityModifier = 0f;
            main.maxParticles = 15;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            // 간헐적 버스트 (번개 느낌)
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 3, 6, 5, 0.2f) // 0.2초 간격 반복
            });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;

            // Color over Lifetime: 밝은 노랑 -> 빠르게 소멸
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
                    new GradientAlphaKey(0.6f, 0.3f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            // Size over Lifetime: 빠르게 줄어듦
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var renderer = psGo.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingLayerName = "Enemy";
            renderer.sortingOrder = 6;
        }
    }

    // ================================================================
    //  EliteGlowPulsator - 엘리트 글로우 맥동 컴포넌트
    // ================================================================

    /// <summary>
    /// 엘리트 몬스터의 금색 글로우가 맥동하는 효과.
    /// 크기와 알파를 sin 파형으로 변화시킨다.
    /// </summary>
    public class EliteGlowPulsator : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private float _timer;

        private const float PulseFrequency = 1.5f; // Hz
        private const float ScaleMin = 1.15f;
        private const float ScaleMax = 1.3f;
        private const float AlphaMin = 0.2f;
        private const float AlphaMax = 0.45f;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            if (_sr == null) return;

            _timer += Time.deltaTime;
            float phase = Mathf.Sin(_timer * PulseFrequency * Mathf.PI * 2f);
            float t = (phase + 1f) * 0.5f; // 0~1

            // 크기 맥동
            float scale = Mathf.Lerp(ScaleMin, ScaleMax, t);
            transform.localScale = Vector3.one * scale;

            // 알파 맥동
            Color c = _sr.color;
            c.a = Mathf.Lerp(AlphaMin, AlphaMax, t);
            _sr.color = c;
        }
    }
}
