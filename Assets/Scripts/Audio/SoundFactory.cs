using UnityEngine;
using System.Collections.Generic;
using static SoulCraft.Audio.SoundSynthesizer;

namespace SoulCraft.Audio
{
    /// <summary>
    /// 게임에서 사용하는 모든 사운드를 코드로 합성하여 캐싱하는 팩토리.
    /// Initialize() 호출 시 30종 이상의 SFX와 4종 BGM을 일괄 생성한다.
    /// </summary>
    public static class SoundFactory
    {
        private static readonly Dictionary<string, AudioClip> _cache = new();
        private static bool _initialized;

        private const int SR = 44100; // sample rate

        // ════════════════════════════════════════════════════════
        //  Public API
        // ════════════════════════════════════════════════════════

        public static bool IsInitialized => _initialized;

        /// <summary>모든 사운드를 생성하여 캐시한다. 게임 시작 시 한 번 호출.</summary>
        public static void Initialize()
        {
            if (_initialized) return;

            ResetRng(42);

            // ── Combat ──
            CreateAttack1();
            CreateAttack2();
            CreateAttack3();
            CreateHit();
            CreateCritical();
            CreateCombo();
            CreateKill();

            // ── Skill Elements ──
            CreateFire();
            CreateIce();
            CreateLightning();
            CreateDark();
            CreateHoly();
            CreateWind();

            // ── Player ──
            CreateDash();
            CreateHurt();
            CreateDeath();
            CreateHeal();
            CreateLevelUp();

            // ── UI ──
            CreateBtnClick();
            CreateBtnHover();
            CreateMenuOpen();
            CreateMenuClose();
            CreateItemPickup();
            CreateItemEquip();
            CreateUpgradeSuccess();
            CreateUpgradeFail();

            // ── Boss ──
            CreateBossIntro();
            CreateBossRoar();
            CreateBossPhase();
            CreateBossDeath();

            // ── BGM ──
            CreateBgmMenu();
            CreateBgmStage1();
            CreateBgmStage2();
            CreateBgmBoss();

            _initialized = true;
        }

        /// <summary>캐시된 AudioClip을 키로 가져온다.</summary>
        public static AudioClip GetClip(string key)
        {
            if (!_initialized)
            {
                Debug.LogWarning("[SoundFactory] Not initialized. Call Initialize() first.");
                return null;
            }
            if (_cache.TryGetValue(key, out var clip)) return clip;
            Debug.LogWarning($"[SoundFactory] Clip not found: {key}");
            return null;
        }

        /// <summary>모든 캐시된 키를 반환한다.</summary>
        public static IEnumerable<string> GetAllKeys() => _cache.Keys;

        /// <summary>캐시를 비우고 초기화 상태를 리셋한다.</summary>
        public static void Clear()
        {
            _cache.Clear();
            _initialized = false;
        }

        // ════════════════════════════════════════════════════════
        //  Helper: Register
        // ════════════════════════════════════════════════════════

        private static void Register(string key, float[] samples)
        {
            Normalize(samples);
            SoftClip(samples, 0.9f);
            _cache[key] = CreateClip(key, samples, SR);
        }

        // ════════════════════════════════════════════════════════
        //  COMBAT
        // ════════════════════════════════════════════════════════

        // sfx_attack_1: 가벼운 검격 - 짧은 고주파 스윕 + 화이트노이즈
        private static void CreateAttack1()
        {
            float dur = 0.12f;
            var sweep = GenerateSweep(Sawtooth, 2000f, 800f, dur, SweepMode.Exponential, SR,
                new ADSREnvelope(0.002f, 0.03f, 0.0f, 0.06f));
            var noise = GenerateNoise(dur, SR,
                new ADSREnvelope(0.001f, 0.02f, 0.0f, 0.05f));
            HighPass(noise, 0.25f);

            Register("sfx_attack_1", Mix((sweep, 0.7f), (noise, 0.4f)));
        }

        // sfx_attack_2: 중간 검격 - 약간 더 무거운 톤
        private static void CreateAttack2()
        {
            float dur = 0.18f;
            var sweep = GenerateSweep(Sawtooth, 1500f, 500f, dur, SweepMode.Exponential, SR,
                new ADSREnvelope(0.003f, 0.04f, 0.0f, 0.1f));
            var body = GenerateSweep(Sine, 300f, 100f, dur, SweepMode.Exponential, SR,
                new ADSREnvelope(0.005f, 0.05f, 0.0f, 0.08f));
            var noise = GenerateNoise(dur, SR,
                new ADSREnvelope(0.002f, 0.03f, 0.0f, 0.08f));
            HighPass(noise, 0.2f);

            Register("sfx_attack_2", Mix((sweep, 0.5f), (body, 0.4f), (noise, 0.3f)));
        }

        // sfx_attack_3: 강한 검격 - 낮은 주파수 + 임팩트
        private static void CreateAttack3()
        {
            float dur = 0.25f;
            var impact = GenerateSweep(Sine, 200f, 50f, dur, SweepMode.Exponential, SR,
                new ADSREnvelope(0.003f, 0.08f, 0.0f, 0.12f));
            var sweep = GenerateSweep(Sawtooth, 1200f, 300f, dur, SweepMode.Exponential, SR,
                new ADSREnvelope(0.002f, 0.05f, 0.0f, 0.15f));
            var noise = GenerateNoise(dur * 0.5f, SR,
                new ADSREnvelope(0.001f, 0.03f, 0.0f, 0.06f));

            float[] mixed = Mix((impact, 0.6f), (sweep, 0.4f), (noise, 0.3f));
            SoftClip(mixed, 1.5f);
            Register("sfx_attack_3", mixed);
        }

        // sfx_hit: 타격 착탄음 - 짧은 임팩트, 중저음
        private static void CreateHit()
        {
            float dur = 0.1f;
            var body = GenerateSweep(Sine, 250f, 80f, dur, SweepMode.Exponential, SR,
                new ADSREnvelope(0.001f, 0.04f, 0.0f, 0.05f));
            var click = GenerateNoise(0.02f, SR,
                new ADSREnvelope(0.0005f, 0.005f, 0.0f, 0.01f));

            int totalSamples = (int)(dur * SR);
            float[] result = new float[totalSamples];
            for (int i = 0; i < totalSamples; i++)
            {
                result[i] = body[i];
                if (i < click.Length) result[i] += click[i] * 0.6f;
            }

            Register("sfx_hit", result);
        }

        // sfx_critical: 크리티컬 히트 - 타격음 + 높은 반짝 톤
        private static void CreateCritical()
        {
            float dur = 0.2f;
            // 기본 임팩트
            var impact = GenerateSweep(Sine, 300f, 80f, 0.1f, SweepMode.Exponential, SR,
                new ADSREnvelope(0.001f, 0.04f, 0.0f, 0.05f));
            // 반짝이는 톤
            var sparkle = GenerateSweep(Sine, 2500f, 3500f, dur, SweepMode.Linear, SR,
                new ADSREnvelope(0.01f, 0.05f, 0.3f, 0.1f));
            var sparkle2 = GenerateSweep(Sine, 3800f, 4200f, dur * 0.7f, SweepMode.Linear, SR,
                new ADSREnvelope(0.02f, 0.04f, 0.2f, 0.08f));
            var click = GenerateNoise(0.015f, SR,
                new ADSREnvelope(0.0005f, 0.005f, 0.0f, 0.008f));

            int totalSamples = (int)(dur * SR);
            float[] result = new float[totalSamples];
            for (int i = 0; i < totalSamples; i++)
            {
                if (i < impact.Length) result[i] += impact[i] * 0.6f;
                if (i < sparkle.Length) result[i] += sparkle[i] * 0.35f;
                if (i < sparkle2.Length) result[i] += sparkle2[i] * 0.2f;
                if (i < click.Length) result[i] += click[i] * 0.5f;
            }

            Register("sfx_critical", result);
        }

        // sfx_combo: 콤보 달성 - 상승 화음 아르페지오
        private static void CreateCombo()
        {
            // C5 - E5 - G5 - C6 아르페지오
            float[] freqs = {
                NoteNameToFreq("C5"),
                NoteNameToFreq("E5"),
                NoteNameToFreq("G5"),
                NoteNameToFreq("C6")
            };

            float noteLen = 0.08f;
            float total = noteLen * freqs.Length + 0.1f;
            float[] arp = GenerateArpeggio(freqs, noteLen, total, 0.8f, SR);

            // 약간의 리버브
            Reverb(arp, SR, 0.3f, 0.2f);
            Register("sfx_combo", arp);
        }

        // sfx_kill: 적 처치 - 쿵 + 보석 소리
        private static void CreateKill()
        {
            float dur = 0.4f;
            // 쿵
            var thud = GenerateKick(0.15f, SR);
            // 보석 반짝 (상승 사인파)
            var gem1 = GenerateTone(Sine, NoteNameToFreq("E6"), 0.15f, SR,
                new ADSREnvelope(0.005f, 0.04f, 0.3f, 0.06f));
            var gem2 = GenerateTone(Sine, NoteNameToFreq("G6"), 0.12f, SR,
                new ADSREnvelope(0.005f, 0.03f, 0.25f, 0.05f));

            int totalSamples = (int)(dur * SR);
            float[] result = new float[totalSamples];
            int gem1Start = (int)(0.08f * SR);
            int gem2Start = (int)(0.16f * SR);

            for (int i = 0; i < totalSamples; i++)
            {
                if (i < thud.Length) result[i] += thud[i] * 0.7f;
                int g1i = i - gem1Start;
                if (g1i >= 0 && g1i < gem1.Length) result[i] += gem1[g1i] * 0.5f;
                int g2i = i - gem2Start;
                if (g2i >= 0 && g2i < gem2.Length) result[i] += gem2[g2i] * 0.4f;
            }

            Register("sfx_kill", result);
        }

        // ════════════════════════════════════════════════════════
        //  SKILL ELEMENTS
        // ════════════════════════════════════════════════════════

        // sfx_fire: 화염 - 보글보글 + 휙
        private static void CreateFire()
        {
            float dur = 0.35f;
            int totalSamples = (int)(dur * SR);
            float[] result = new float[totalSamples];

            // 보글보글: 랜덤 주파수 사인 버스트
            var env = new ADSREnvelope(0.01f, 0.1f, 0.3f, 0.15f);
            var baseNoise = GenerateNoise(dur, SR, env);
            LowPass(baseNoise, 0.08f);

            // 상승 스윕 (휙)
            var swoosh = GenerateSweep(Sine, 200f, 600f, dur * 0.6f, SweepMode.Exponential, SR,
                new ADSREnvelope(0.02f, 0.08f, 0.2f, 0.1f));

            // 크래클 (고주파 노이즈 펄스)
            var crackle = GenerateNoise(dur, SR,
                new ADSREnvelope(0.005f, 0.05f, 0.15f, 0.1f));
            HighPass(crackle, 0.2f);

            for (int i = 0; i < totalSamples; i++)
            {
                result[i] = baseNoise[i] * 0.5f + crackle[i] * 0.3f;
                if (i < swoosh.Length) result[i] += swoosh[i] * 0.4f;
            }

            Register("sfx_fire", result);
        }

        // sfx_ice: 얼음 - 파삭 + 차가운 고주파
        private static void CreateIce()
        {
            float dur = 0.3f;
            // 파삭 크래킹
            var crack = GenerateNoise(0.05f, SR,
                new ADSREnvelope(0.001f, 0.01f, 0.0f, 0.03f));
            HighPass(crack, 0.35f);

            // 차가운 고주파 시머
            var shimmer = GenerateSweep(Sine, 3000f, 4500f, dur, SweepMode.Linear, SR,
                new ADSREnvelope(0.02f, 0.08f, 0.25f, 0.15f));
            var shimmer2 = GenerateSweep(Sine, 3500f, 5000f, dur * 0.8f, SweepMode.Linear, SR,
                new ADSREnvelope(0.03f, 0.06f, 0.2f, 0.12f));

            // 글래스 벨
            var bell = GenerateTone(Sine, 2200f, 0.2f, SR,
                new ADSREnvelope(0.001f, 0.05f, 0.1f, 0.1f));

            int totalSamples = (int)(dur * SR);
            float[] result = new float[totalSamples];
            for (int i = 0; i < totalSamples; i++)
            {
                if (i < crack.Length) result[i] += crack[i] * 0.7f;
                if (i < shimmer.Length) result[i] += shimmer[i] * 0.35f;
                if (i < shimmer2.Length) result[i] += shimmer2[i] * 0.2f;
                if (i < bell.Length) result[i] += bell[i] * 0.3f;
            }

            Reverb(result, SR, 0.4f, 0.25f);
            Register("sfx_ice", result);
        }

        // sfx_lightning: 번개 - 찌직 + 크랙
        private static void CreateLightning()
        {
            float dur = 0.25f;
            int totalSamples = (int)(dur * SR);
            float[] result = new float[totalSamples];

            // 메인 크랙: 급격한 노이즈 버스트
            var crack = GenerateNoise(0.04f, SR,
                new ADSREnvelope(0.0005f, 0.008f, 0.0f, 0.025f));

            // 찌직거림: 빠른 구형파 변조 노이즈
            var buzz = GenerateNoise(dur, SR,
                new ADSREnvelope(0.002f, 0.03f, 0.2f, 0.15f));
            var mod = GenerateTone(Square, 120f, dur, SR,
                new ADSREnvelope(0.001f, 0.02f, 0.5f, 0.1f));
            for (int i = 0; i < buzz.Length; i++)
            {
                if (i < mod.Length) buzz[i] *= Mathf.Abs(mod[i]);
            }
            HighPass(buzz, 0.15f);

            // 저주파 임팩트
            var bass = GenerateSweep(Sine, 150f, 40f, 0.08f, SweepMode.Exponential, SR,
                new ADSREnvelope(0.001f, 0.02f, 0.0f, 0.04f));

            for (int i = 0; i < totalSamples; i++)
            {
                if (i < crack.Length) result[i] += crack[i] * 0.8f;
                result[i] += buzz[i] * 0.5f;
                if (i < bass.Length) result[i] += bass[i] * 0.4f;
            }

            Register("sfx_lightning", result);
        }

        // sfx_dark: 어둠 - 저음 웅웅
        private static void CreateDark()
        {
            float dur = 0.5f;
            // 저주파 드론
            var drone = GenerateTone(Sine, 60f, dur, SR,
                new ADSREnvelope(0.05f, 0.1f, 0.6f, 0.2f));
            var drone2 = GenerateTone(Sine, 63f, dur, SR,
                new ADSREnvelope(0.06f, 0.1f, 0.5f, 0.2f)); // 비트 주파수 효과

            // 저주파 LFO 변조
            var sub = GenerateTone(Sine, 35f, dur, SR,
                new ADSREnvelope(0.08f, 0.15f, 0.7f, 0.2f));

            // 어두운 분위기 노이즈
            var darkNoise = GenerateNoise(dur, SR,
                new ADSREnvelope(0.1f, 0.15f, 0.3f, 0.2f));
            LowPass(darkNoise, 0.03f);

            float[] mixed = Mix((drone, 0.5f), (drone2, 0.4f), (sub, 0.3f), (darkNoise, 0.25f));
            LowPass(mixed, 0.06f);
            Register("sfx_dark", mixed);
        }

        // sfx_holy: 성스러운 - 맑은 종 + 하모닉스
        private static void CreateHoly()
        {
            float dur = 0.6f;
            float bellFreq = NoteNameToFreq("C6");
            // 벨 톤
            var bell = GenerateTone(Sine, bellFreq, dur, SR,
                new ADSREnvelope(0.002f, 0.15f, 0.2f, 0.3f));
            // 하모닉스
            var h2 = GenerateTone(Sine, bellFreq * 2.0f, dur * 0.7f, SR,
                new ADSREnvelope(0.003f, 0.1f, 0.15f, 0.2f));
            var h3 = GenerateTone(Sine, bellFreq * 3.0f, dur * 0.5f, SR,
                new ADSREnvelope(0.005f, 0.08f, 0.1f, 0.15f));
            var h5 = GenerateTone(Sine, bellFreq * 5.0f, dur * 0.3f, SR,
                new ADSREnvelope(0.005f, 0.05f, 0.05f, 0.1f));

            // 5도 위 하모니
            var fifth = GenerateTone(Sine, NoteNameToFreq("G5"), dur * 0.8f, SR,
                new ADSREnvelope(0.01f, 0.12f, 0.15f, 0.25f));

            float[] mixed = Mix(
                (bell, 0.5f), (h2, 0.2f), (h3, 0.1f), (h5, 0.05f), (fifth, 0.3f)
            );
            Reverb(mixed, SR, 0.6f, 0.35f);
            Register("sfx_holy", mixed);
        }

        // sfx_wind: 바람 - 쉬이익 노이즈 스윕
        private static void CreateWind()
        {
            float dur = 0.35f;
            var noise = GenerateNoise(dur, SR,
                new ADSREnvelope(0.02f, 0.08f, 0.4f, 0.15f));

            // 필터 스윕: 시간에 따라 로우패스 주파수 변화 (수동 구현)
            int totalSamples = noise.Length;
            float[] filtered = new float[totalSamples];
            float filterState = 0f;

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / totalSamples;
                float cutoff = Mathf.Lerp(0.02f, 0.15f, t < 0.5f ? t * 2f : 2f - t * 2f);
                float rc = 1f / (cutoff * 2f * Mathf.PI);
                float alpha = 1f / (rc + 1f);
                filterState += alpha * (noise[i] - filterState);
                filtered[i] = filterState;
            }

            // 약간의 톤 추가
            var tone = GenerateSweep(Sine, 400f, 800f, dur, SweepMode.Linear, SR,
                new ADSREnvelope(0.05f, 0.1f, 0.15f, 0.12f));

            Register("sfx_wind", Mix((filtered, 0.7f), (tone, 0.15f)));
        }

        // ════════════════════════════════════════════════════════
        //  PLAYER
        // ════════════════════════════════════════════════════════

        // sfx_dash: 대시 - 빠른 바람 소리
        private static void CreateDash()
        {
            float dur = 0.15f;
            var noise = GenerateNoise(dur, SR,
                new ADSREnvelope(0.005f, 0.04f, 0.0f, 0.08f));

            // 밴드패스 효과: 로우패스 후 하이패스
            LowPass(noise, 0.2f);
            HighPass(noise, 0.08f);

            // 약간의 톤 스윕
            var swoosh = GenerateSweep(Sine, 500f, 200f, dur, SweepMode.Exponential, SR,
                new ADSREnvelope(0.003f, 0.03f, 0.0f, 0.06f));

            Register("sfx_dash", Mix((noise, 0.7f), (swoosh, 0.3f)));
        }

        // sfx_hurt: 피격 - 둔탁한 임팩트 + 작은 신음(하강 톤)
        private static void CreateHurt()
        {
            float dur = 0.2f;
            var impact = GenerateSweep(Sine, 180f, 60f, 0.08f, SweepMode.Exponential, SR,
                new ADSREnvelope(0.001f, 0.03f, 0.0f, 0.04f));
            var noise = GenerateNoise(0.03f, SR,
                new ADSREnvelope(0.001f, 0.008f, 0.0f, 0.015f));

            // 하강 신음 톤
            var groan = GenerateSweep(Sine, 400f, 250f, dur, SweepMode.Linear, SR,
                new ADSREnvelope(0.01f, 0.05f, 0.2f, 0.1f));

            int totalSamples = (int)(dur * SR);
            float[] result = new float[totalSamples];
            for (int i = 0; i < totalSamples; i++)
            {
                if (i < impact.Length) result[i] += impact[i] * 0.6f;
                if (i < noise.Length) result[i] += noise[i] * 0.4f;
                if (i < groan.Length) result[i] += groan[i] * 0.25f;
            }

            Register("sfx_hurt", result);
        }

        // sfx_death: 사망 - 느린 하강 + 리버브
        private static void CreateDeath()
        {
            float dur = 0.8f;
            var descend = GenerateSweep(Sine, 300f, 40f, dur, SweepMode.Exponential, SR,
                new ADSREnvelope(0.01f, 0.15f, 0.3f, 0.4f));
            var descend2 = GenerateSweep(Triangle, 200f, 30f, dur, SweepMode.Exponential, SR,
                new ADSREnvelope(0.02f, 0.1f, 0.2f, 0.35f));

            // 어두운 노이즈
            var noise = GenerateNoise(dur, SR,
                new ADSREnvelope(0.05f, 0.2f, 0.15f, 0.4f));
            LowPass(noise, 0.04f);

            float[] mixed = Mix((descend, 0.5f), (descend2, 0.3f), (noise, 0.25f));
            Reverb(mixed, SR, 0.7f, 0.4f);
            Register("sfx_death", mixed);
        }

        // sfx_heal: 회복 - 맑은 상승 톤
        private static void CreateHeal()
        {
            float dur = 0.4f;
            float[] freqs = {
                NoteNameToFreq("C5"), NoteNameToFreq("E5"),
                NoteNameToFreq("G5"), NoteNameToFreq("C6")
            };
            float noteLen = 0.1f;
            float[] arp = GenerateArpeggio(freqs, noteLen, dur, 0.7f, SR);

            // 부드러운 시머
            var shimmer = GenerateTone(Sine, NoteNameToFreq("C6"), dur * 0.5f, SR,
                new ADSREnvelope(0.05f, 0.1f, 0.2f, 0.15f));

            int totalSamples = (int)(dur * SR);
            float[] result = new float[totalSamples];
            int shimmerStart = (int)(dur * 0.4f * SR);
            for (int i = 0; i < totalSamples; i++)
            {
                result[i] = arp[i] * 0.7f;
                int si = i - shimmerStart;
                if (si >= 0 && si < shimmer.Length) result[i] += shimmer[si] * 0.3f;
            }

            Reverb(result, SR, 0.4f, 0.25f);
            Register("sfx_heal", result);
        }

        // sfx_levelup: 레벨업 - 팡파레 짧은 버전
        private static void CreateLevelUp()
        {
            float dur = 0.8f;
            int totalSamples = (int)(dur * SR);

            // 짧은 팡파레: C5-E5-G5 동시 + 옥타브 상승
            float[] chord1Freqs = {
                NoteNameToFreq("C5"), NoteNameToFreq("E5"), NoteNameToFreq("G5")
            };
            float[] chord1 = GenerateChord(chord1Freqs, 0.3f, 0.7f, SR);

            float[] chord2Freqs = {
                NoteNameToFreq("C6"), NoteNameToFreq("E6"), NoteNameToFreq("G6")
            };
            float[] chord2 = GenerateChord(chord2Freqs, 0.5f, 0.6f, SR);
            int chord2Start = (int)(0.3f * SR);

            // 스파클 효과
            var sparkle = GenerateSweep(Sine, 3000f, 5000f, 0.3f, SweepMode.Linear, SR,
                new ADSREnvelope(0.02f, 0.08f, 0.15f, 0.15f));
            int sparkleStart = (int)(0.35f * SR);

            float[] result = new float[totalSamples];
            for (int i = 0; i < totalSamples; i++)
            {
                if (i < chord1.Length) result[i] += chord1[i];
                int c2i = i - chord2Start;
                if (c2i >= 0 && c2i < chord2.Length) result[i] += chord2[c2i];
                int si = i - sparkleStart;
                if (si >= 0 && si < sparkle.Length) result[i] += sparkle[si] * 0.25f;
            }

            Reverb(result, SR, 0.4f, 0.2f);
            Register("sfx_levelup", result);
        }

        // ════════════════════════════════════════════════════════
        //  UI
        // ════════════════════════════════════════════════════════

        // sfx_btn_click: 짧은 틱
        private static void CreateBtnClick()
        {
            float dur = 0.04f;
            var tone = GenerateTone(Sine, 1800f, dur, SR,
                new ADSREnvelope(0.001f, 0.01f, 0.0f, 0.02f));
            var click = GenerateNoise(0.008f, SR,
                new ADSREnvelope(0.0003f, 0.002f, 0.0f, 0.004f));

            int totalSamples = (int)(dur * SR);
            float[] result = new float[totalSamples];
            for (int i = 0; i < totalSamples; i++)
            {
                result[i] = tone[i] * 0.6f;
                if (i < click.Length) result[i] += click[i] * 0.4f;
            }

            Register("sfx_btn_click", result);
        }

        // sfx_btn_hover: 부드러운 틱
        private static void CreateBtnHover()
        {
            float dur = 0.03f;
            var tone = GenerateTone(Sine, 2200f, dur, SR,
                new ADSREnvelope(0.002f, 0.01f, 0.0f, 0.015f));
            Register("sfx_btn_hover", tone);
        }

        // sfx_menu_open: 스으읍 (상승)
        private static void CreateMenuOpen()
        {
            float dur = 0.15f;
            var sweep = GenerateSweep(Sine, 400f, 1200f, dur, SweepMode.Exponential, SR,
                new ADSREnvelope(0.005f, 0.04f, 0.3f, 0.06f));
            var noise = GenerateNoise(dur, SR,
                new ADSREnvelope(0.003f, 0.03f, 0.1f, 0.05f));
            LowPass(noise, 0.15f);

            Register("sfx_menu_open", Mix((sweep, 0.6f), (noise, 0.2f)));
        }

        // sfx_menu_close: 반대 방향 (하강)
        private static void CreateMenuClose()
        {
            float dur = 0.12f;
            var sweep = GenerateSweep(Sine, 1200f, 400f, dur, SweepMode.Exponential, SR,
                new ADSREnvelope(0.003f, 0.03f, 0.2f, 0.05f));
            var noise = GenerateNoise(dur, SR,
                new ADSREnvelope(0.002f, 0.02f, 0.08f, 0.04f));
            LowPass(noise, 0.12f);

            Register("sfx_menu_close", Mix((sweep, 0.6f), (noise, 0.15f)));
        }

        // sfx_item_pickup: 동전 소리
        private static void CreateItemPickup()
        {
            float dur = 0.15f;
            // 두 개의 빠른 톤 (ting-ting)
            var tone1 = GenerateTone(Sine, NoteNameToFreq("E6"), 0.07f, SR,
                new ADSREnvelope(0.001f, 0.02f, 0.1f, 0.03f));
            var tone2 = GenerateTone(Sine, NoteNameToFreq("A6"), 0.07f, SR,
                new ADSREnvelope(0.001f, 0.02f, 0.1f, 0.03f));

            int totalSamples = (int)(dur * SR);
            float[] result = new float[totalSamples];
            int t2Start = (int)(0.05f * SR);
            for (int i = 0; i < totalSamples; i++)
            {
                if (i < tone1.Length) result[i] += tone1[i] * 0.6f;
                int t2i = i - t2Start;
                if (t2i >= 0 && t2i < tone2.Length) result[i] += tone2[t2i] * 0.6f;
            }

            Register("sfx_item_pickup", result);
        }

        // sfx_item_equip: 금속 찰칵
        private static void CreateItemEquip()
        {
            float dur = 0.12f;
            // 금속 클랭
            var metal = GenerateTone(Sine, 3500f, dur, SR,
                new ADSREnvelope(0.001f, 0.03f, 0.05f, 0.06f));
            var metal2 = GenerateTone(Sine, 4200f, dur * 0.7f, SR,
                new ADSREnvelope(0.001f, 0.02f, 0.03f, 0.04f));
            var click = GenerateNoise(0.015f, SR,
                new ADSREnvelope(0.0005f, 0.003f, 0.0f, 0.008f));

            int totalSamples = (int)(dur * SR);
            float[] result = new float[totalSamples];
            for (int i = 0; i < totalSamples; i++)
            {
                result[i] = metal[i] * 0.5f;
                if (i < metal2.Length) result[i] += metal2[i] * 0.3f;
                if (i < click.Length) result[i] += click[i] * 0.5f;
            }

            Register("sfx_item_equip", result);
        }

        // sfx_upgrade_success: 반짝 + 상승
        private static void CreateUpgradeSuccess()
        {
            float dur = 0.35f;
            float[] freqs = {
                NoteNameToFreq("C5"), NoteNameToFreq("E5"), NoteNameToFreq("G5")
            };
            float noteLen = 0.1f;
            float[] arp = GenerateArpeggio(freqs, noteLen, 0.3f, 0.8f, SR);

            var sparkle = GenerateSweep(Sine, 2000f, 4000f, 0.2f, SweepMode.Linear, SR,
                new ADSREnvelope(0.01f, 0.05f, 0.2f, 0.1f));

            int totalSamples = (int)(dur * SR);
            float[] result = new float[totalSamples];
            int sparkleStart = (int)(0.15f * SR);
            for (int i = 0; i < totalSamples; i++)
            {
                if (i < arp.Length) result[i] += arp[i] * 0.7f;
                int si = i - sparkleStart;
                if (si >= 0 && si < sparkle.Length) result[i] += sparkle[si] * 0.3f;
            }

            Reverb(result, SR, 0.3f, 0.15f);
            Register("sfx_upgrade_success", result);
        }

        // sfx_upgrade_fail: 둔탁한 하강
        private static void CreateUpgradeFail()
        {
            float dur = 0.3f;
            var descend = GenerateSweep(Sine, 400f, 150f, dur, SweepMode.Exponential, SR,
                new ADSREnvelope(0.005f, 0.08f, 0.2f, 0.15f));
            var thud = GenerateSweep(Sine, 120f, 50f, 0.1f, SweepMode.Exponential, SR,
                new ADSREnvelope(0.002f, 0.03f, 0.0f, 0.05f));

            // 불협화음
            var dissonance = GenerateTone(Sine, 155f, dur * 0.6f, SR,
                new ADSREnvelope(0.01f, 0.06f, 0.15f, 0.12f));

            int totalSamples = (int)(dur * SR);
            float[] result = new float[totalSamples];
            for (int i = 0; i < totalSamples; i++)
            {
                result[i] = descend[i] * 0.5f;
                if (i < thud.Length) result[i] += thud[i] * 0.4f;
                if (i < dissonance.Length) result[i] += dissonance[i] * 0.2f;
            }

            Register("sfx_upgrade_fail", result);
        }

        // ════════════════════════════════════════════════════════
        //  BOSS
        // ════════════════════════════════════════════════════════

        // sfx_boss_intro: 보스 등장 - 저음 드럼 + 긴장감
        private static void CreateBossIntro()
        {
            float dur = 1.2f;
            int totalSamples = (int)(dur * SR);
            float[] result = new float[totalSamples];

            // 큰 킥 드럼
            var kick = GenerateKick(0.4f, SR);

            // 긴장감 있는 저음 드론 (천천히 상승)
            var drone = GenerateSweep(Sine, 40f, 80f, dur, SweepMode.Linear, SR,
                new ADSREnvelope(0.1f, 0.2f, 0.6f, 0.3f));
            var drone2 = GenerateSweep(Sine, 42f, 82f, dur, SweepMode.Linear, SR,
                new ADSREnvelope(0.12f, 0.2f, 0.5f, 0.3f));

            // 불안한 트라이톤 간격
            var tension = GenerateSweep(Sine, 120f, 170f, dur * 0.7f, SweepMode.Linear, SR,
                new ADSREnvelope(0.2f, 0.15f, 0.4f, 0.3f));

            // 깊은 노이즈 럼블
            var rumble = GenerateNoise(dur, SR,
                new ADSREnvelope(0.15f, 0.3f, 0.4f, 0.3f));
            LowPass(rumble, 0.025f);

            for (int i = 0; i < totalSamples; i++)
            {
                if (i < kick.Length) result[i] += kick[i] * 0.7f;
                result[i] += drone[i] * 0.4f;
                result[i] += drone2[i] * 0.3f;
                if (i < tension.Length) result[i] += tension[i] * 0.25f;
                result[i] += rumble[i] * 0.3f;
            }

            Reverb(result, SR, 0.6f, 0.3f);
            Register("sfx_boss_intro", result);
        }

        // sfx_boss_roar: 보스 포효 - 저주파 진동 + 노이즈
        private static void CreateBossRoar()
        {
            float dur = 0.8f;
            // 저주파 진동
            var sub = GenerateSweep(Sine, 50f, 30f, dur, SweepMode.Exponential, SR,
                new ADSREnvelope(0.02f, 0.15f, 0.5f, 0.3f));
            // 그로울 (복잡한 파형)
            var growl = GenerateSweep(Sawtooth, 80f, 50f, dur, SweepMode.Exponential, SR,
                new ADSREnvelope(0.03f, 0.1f, 0.6f, 0.25f));
            LowPass(growl, 0.04f);

            // 노이즈 레이어
            var noise = GenerateNoise(dur, SR,
                new ADSREnvelope(0.05f, 0.15f, 0.35f, 0.3f));
            LowPass(noise, 0.05f);

            float[] mixed = Mix((sub, 0.5f), (growl, 0.4f), (noise, 0.3f));
            SoftClip(mixed, 1.5f);
            Reverb(mixed, SR, 0.5f, 0.3f);
            Register("sfx_boss_roar", mixed);
        }

        // sfx_boss_phase: 페이즈 전환 - 경고음 + 웅장한 임팩트
        private static void CreateBossPhase()
        {
            float dur = 1.0f;
            int totalSamples = (int)(dur * SR);
            float[] result = new float[totalSamples];

            // 경고음 (반복 비프)
            float beepDur = 0.1f;
            float beepInterval = 0.15f;
            for (int b = 0; b < 4; b++)
            {
                int start = (int)(b * beepInterval * SR);
                var beep = GenerateTone(Square, 880f, beepDur, SR,
                    new ADSREnvelope(0.002f, 0.02f, 0.5f, 0.02f));
                for (int i = 0; i < beep.Length; i++)
                {
                    int idx = start + i;
                    if (idx < totalSamples) result[idx] += beep[i] * 0.3f;
                }
            }

            // 웅장한 임팩트 (경고 후)
            int impactStart = (int)(0.6f * SR);
            var impact = GenerateKick(0.35f, SR);
            var boom = GenerateSweep(Sine, 100f, 30f, 0.4f, SweepMode.Exponential, SR,
                new ADSREnvelope(0.005f, 0.1f, 0.3f, 0.2f));
            var crashNoise = GenerateNoise(0.3f, SR,
                new ADSREnvelope(0.002f, 0.05f, 0.2f, 0.15f));
            LowPass(crashNoise, 0.08f);

            for (int i = 0; i < totalSamples; i++)
            {
                int rel = i - impactStart;
                if (rel >= 0)
                {
                    if (rel < impact.Length) result[i] += impact[rel] * 0.7f;
                    if (rel < boom.Length) result[i] += boom[rel] * 0.5f;
                    if (rel < crashNoise.Length) result[i] += crashNoise[rel] * 0.3f;
                }
            }

            Reverb(result, SR, 0.5f, 0.25f);
            Register("sfx_boss_phase", result);
        }

        // sfx_boss_death: 보스 처치 - 긴 폭발 + 승리 팡파레
        private static void CreateBossDeath()
        {
            float dur = 2.0f;
            int totalSamples = (int)(dur * SR);
            float[] result = new float[totalSamples];

            // 폭발
            var explosion = GenerateKick(0.5f, SR);
            var expNoise = GenerateNoise(0.8f, SR,
                new ADSREnvelope(0.005f, 0.2f, 0.3f, 0.4f));
            LowPass(expNoise, 0.06f);

            for (int i = 0; i < totalSamples; i++)
            {
                if (i < explosion.Length) result[i] += explosion[i] * 0.6f;
                if (i < expNoise.Length) result[i] += expNoise[i] * 0.35f;
            }

            // 승리 팡파레 (폭발 후)
            int fanfareStart = (int)(0.7f * SR);
            // C-E-G 코드 → G-B-D → C 옥타브
            float[][] chords = {
                new[] { NoteNameToFreq("C5"), NoteNameToFreq("E5"), NoteNameToFreq("G5") },
                new[] { NoteNameToFreq("G5"), NoteNameToFreq("B5"), NoteNameToFreq("D6") },
                new[] { NoteNameToFreq("C6"), NoteNameToFreq("E6"), NoteNameToFreq("G6") }
            };

            float chordDur = 0.35f;
            for (int c = 0; c < chords.Length; c++)
            {
                int cStart = fanfareStart + (int)(c * chordDur * SR);
                float[] chord = GenerateChord(chords[c], chordDur, 0.7f, SR);
                for (int i = 0; i < chord.Length; i++)
                {
                    int idx = cStart + i;
                    if (idx < totalSamples) result[idx] += chord[i];
                }
            }

            Reverb(result, SR, 0.5f, 0.25f);
            Register("sfx_boss_death", result);
        }

        // ════════════════════════════════════════════════════════
        //  BGM
        // ════════════════════════════════════════════════════════

        // bgm_menu: 메인 메뉴 - Am-F-C-G 피아노 아르페지오, 8마디 루프
        private static void CreateBgmMenu()
        {
            float bpm = 90f;
            float beatDur = 60f / bpm;
            float barDur = beatDur * 4f;
            float totalDur = barDur * 8f; // 8마디
            int totalSamples = (int)(totalDur * SR);
            float[] result = new float[totalSamples];

            // Am-F-C-G 코드 진행 (2마디씩)
            float[][] chordNotes = {
                // Am: A3-C4-E4
                new[] { NoteNameToFreq("A3"), NoteNameToFreq("C4"), NoteNameToFreq("E4") },
                // F: F3-A3-C4
                new[] { NoteNameToFreq("F3"), NoteNameToFreq("A3"), NoteNameToFreq("C4") },
                // C: C3-E3-G3
                new[] { NoteNameToFreq("C3"), NoteNameToFreq("E3"), NoteNameToFreq("G3") },
                // G: G3-B3-D4
                new[] { NoteNameToFreq("G3"), NoteNameToFreq("B3"), NoteNameToFreq("D4") }
            };

            // 각 코드당 2마디, 아르페지오 패턴
            for (int chord = 0; chord < 4; chord++)
            {
                float chordStart = chord * barDur * 2f;
                float[] notes = chordNotes[chord];

                // 8비트 아르페지오 패턴: 1-3-5-3-1-5-3-5 (옥타브 위 포함)
                int[] pattern = { 0, 1, 2, 1, 0, 2, 1, 2 };
                float[] octaves = { 1f, 1f, 1f, 1f, 1f, 2f, 2f, 1f };

                for (int bar = 0; bar < 2; bar++)
                {
                    for (int beat = 0; beat < 8; beat++)
                    {
                        float noteStart = chordStart + bar * barDur + beat * (beatDur / 2f);
                        int sampleStart = (int)(noteStart * SR);
                        float freq = notes[pattern[beat]] * octaves[beat];
                        float noteDur = beatDur * 0.8f;

                        float[] note = GeneratePianoNote(freq, noteDur, 0.6f, SR);
                        for (int i = 0; i < note.Length; i++)
                        {
                            int idx = sampleStart + i;
                            if (idx < totalSamples) result[idx] += note[i] * 0.4f;
                        }
                    }
                }

                // 저음 베이스 (코드 루트, 느린 노트)
                float bassFreq = notes[0] * 0.5f;
                for (int bar = 0; bar < 2; bar++)
                {
                    float bassStart = chordStart + bar * barDur;
                    int bassSampleStart = (int)(bassStart * SR);
                    float[] bass = GenerateTone(Sine, bassFreq, barDur * 0.9f, SR,
                        new ADSREnvelope(0.01f, 0.1f, 0.4f, 0.3f));
                    for (int i = 0; i < bass.Length; i++)
                    {
                        int idx = bassSampleStart + i;
                        if (idx < totalSamples) result[idx] += bass[i] * 0.2f;
                    }
                }
            }

            // 부드러운 패드 (전체 배경)
            var pad = GenerateTone(Sine, NoteNameToFreq("A2"), totalDur, SR,
                new ADSREnvelope(0.5f, 1f, 0.3f, 1f));
            var pad2 = GenerateTone(Sine, NoteNameToFreq("E3"), totalDur, SR,
                new ADSREnvelope(0.6f, 1f, 0.25f, 1f));
            for (int i = 0; i < totalSamples; i++)
            {
                result[i] += pad[i] * 0.08f;
                if (i < pad2.Length) result[i] += pad2[i] * 0.06f;
            }

            Reverb(result, SR, 0.5f, 0.2f);
            FadeInOut(result, (int)(0.5f * SR), (int)(1.0f * SR));
            Normalize(result);
            _cache["bgm_menu"] = CreateClip("bgm_menu", result, SR);
        }

        // bgm_stage_1: 잊혀진 숲 - 미스터리한 분위기, 마이너 키, 느린 템포
        private static void CreateBgmStage1()
        {
            float bpm = 70f;
            float beatDur = 60f / bpm;
            float barDur = beatDur * 4f;
            float totalDur = barDur * 8f;
            int totalSamples = (int)(totalDur * SR);
            float[] result = new float[totalSamples];

            // Dm-Bb-Gm-A 코드 진행 (다크/미스터리)
            float[][] chordNotes = {
                new[] { NoteNameToFreq("D3"), NoteNameToFreq("F3"), NoteNameToFreq("A3") },  // Dm
                new[] { NoteNameToFreq("A#2"), NoteNameToFreq("D3"), NoteNameToFreq("F3") }, // Bb
                new[] { NoteNameToFreq("G3"), NoteNameToFreq("A#3"), NoteNameToFreq("D4") }, // Gm
                new[] { NoteNameToFreq("A3"), NoteNameToFreq("C#4"), NoteNameToFreq("E4") }  // A
            };

            // 느린 아르페지오
            for (int chord = 0; chord < 4; chord++)
            {
                float chordStart = chord * barDur * 2f;
                float[] notes = chordNotes[chord];

                for (int bar = 0; bar < 2; bar++)
                {
                    for (int beat = 0; beat < 4; beat++)
                    {
                        float noteStart = chordStart + bar * barDur + beat * beatDur;
                        int sampleStart = (int)(noteStart * SR);
                        int noteIdx = beat % notes.Length;
                        float freq = notes[noteIdx] * (beat >= 2 ? 2f : 1f);
                        float noteDur = beatDur * 1.5f; // 겹치는 노트

                        float[] note = GeneratePianoNote(freq, noteDur, 0.45f, SR);
                        for (int i = 0; i < note.Length; i++)
                        {
                            int idx = sampleStart + i;
                            if (idx < totalSamples) result[idx] += note[i] * 0.35f;
                        }
                    }
                }
            }

            // 미스터리한 패드 드론
            var droneD = GenerateTone(Sine, NoteNameToFreq("D2"), totalDur, SR,
                new ADSREnvelope(1f, 2f, 0.35f, 2f));
            var droneA = GenerateTone(Sine, NoteNameToFreq("A2"), totalDur, SR,
                new ADSREnvelope(1.5f, 2f, 0.25f, 2f));

            // 숲 분위기: 필터드 노이즈
            var ambient = GenerateNoise(totalDur, SR,
                new ADSREnvelope(2f, 2f, 0.15f, 2f));
            LowPass(ambient, 0.02f);

            for (int i = 0; i < totalSamples; i++)
            {
                result[i] += droneD[i] * 0.12f;
                if (i < droneA.Length) result[i] += droneA[i] * 0.08f;
                result[i] += ambient[i] * 0.06f;
            }

            Reverb(result, SR, 0.7f, 0.3f);
            FadeInOut(result, (int)(1f * SR), (int)(1.5f * SR));
            Normalize(result);
            _cache["bgm_stage_1"] = CreateClip("bgm_stage_1", result, SR);
        }

        // bgm_stage_2: 붉은 광산 - 긴장감, 빠른 비트, 타악기 느낌
        private static void CreateBgmStage2()
        {
            float bpm = 130f;
            float beatDur = 60f / bpm;
            float barDur = beatDur * 4f;
            float totalDur = barDur * 8f;
            int totalSamples = (int)(totalDur * SR);
            float[] result = new float[totalSamples];

            // 드럼 패턴: 킥-하이햇-스네어-하이햇 반복
            int totalBeats = (int)(totalDur / beatDur);
            for (int beat = 0; beat < totalBeats; beat++)
            {
                int sampleStart = (int)(beat * beatDur * SR);

                // 킥: 매 4비트
                if (beat % 4 == 0)
                {
                    float[] kick = GenerateKick(0.2f, SR);
                    for (int i = 0; i < kick.Length; i++)
                    {
                        int idx = sampleStart + i;
                        if (idx < totalSamples) result[idx] += kick[i] * 0.5f;
                    }
                }

                // 스네어: 매 4비트 오프셋 2
                if (beat % 4 == 2)
                {
                    float[] snare = GenerateSnare(0.15f, SR);
                    for (int i = 0; i < snare.Length; i++)
                    {
                        int idx = sampleStart + i;
                        if (idx < totalSamples) result[idx] += snare[i] * 0.35f;
                    }
                }

                // 하이햇: 매 비트
                {
                    bool open = beat % 4 == 3;
                    float[] hat = GenerateHiHat(0.05f, open, SR);
                    for (int i = 0; i < hat.Length; i++)
                    {
                        int idx = sampleStart + i;
                        if (idx < totalSamples) result[idx] += hat[i] * 0.2f;
                    }
                }
            }

            // Em-C-D-B 코드 (긴장감 있는 마이너)
            float[][] chordNotes = {
                new[] { NoteNameToFreq("E3"), NoteNameToFreq("G3"), NoteNameToFreq("B3") },
                new[] { NoteNameToFreq("C3"), NoteNameToFreq("E3"), NoteNameToFreq("G3") },
                new[] { NoteNameToFreq("D3"), NoteNameToFreq("F#3"), NoteNameToFreq("A3") },
                new[] { NoteNameToFreq("B2"), NoteNameToFreq("D#3"), NoteNameToFreq("F#3") }
            };

            // 구형파 베이스라인
            for (int chord = 0; chord < 4; chord++)
            {
                float chordStart = chord * barDur * 2f;
                float bassFreq = chordNotes[chord][0] * 0.5f;

                // 8분음표 베이스라인
                for (int beat = 0; beat < 16; beat++)
                {
                    float noteStart = chordStart + beat * (beatDur / 2f);
                    int sampleStart = (int)(noteStart * SR);
                    float noteDur = beatDur * 0.4f;
                    float freq = (beat % 3 == 0) ? bassFreq : bassFreq * 1.5f;

                    float[] note = GenerateTone(Square, freq, noteDur, SR,
                        new ADSREnvelope(0.005f, 0.02f, 0.5f, 0.03f));
                    LowPass(note, 0.08f);

                    for (int i = 0; i < note.Length; i++)
                    {
                        int idx = sampleStart + i;
                        if (idx < totalSamples) result[idx] += note[i] * 0.15f;
                    }
                }
            }

            // 긴장감 있는 상위 톤 (간헐적)
            var tensionEnv = new ADSREnvelope(0.1f, 0.2f, 0.3f, 0.3f);
            for (int bar = 0; bar < 8; bar += 2)
            {
                float barStart = bar * barDur;
                int sampleStart = (int)(barStart * SR);
                float freq = (bar % 4 == 0) ? NoteNameToFreq("B4") : NoteNameToFreq("C5");
                float[] tone = GenerateTone(Sawtooth, freq, barDur, SR, tensionEnv);
                LowPass(tone, 0.1f);
                for (int i = 0; i < tone.Length; i++)
                {
                    int idx = sampleStart + i;
                    if (idx < totalSamples) result[idx] += tone[i] * 0.1f;
                }
            }

            FadeInOut(result, (int)(0.3f * SR), (int)(1f * SR));
            Normalize(result);
            _cache["bgm_stage_2"] = CreateClip("bgm_stage_2", result, SR);
        }

        // bgm_boss: 보스전 - 강렬한 비트, 빠른 템포, 저음 드럼 + 긴장 멜로디
        private static void CreateBgmBoss()
        {
            float bpm = 150f;
            float beatDur = 60f / bpm;
            float barDur = beatDur * 4f;
            float totalDur = barDur * 8f;
            int totalSamples = (int)(totalDur * SR);
            float[] result = new float[totalSamples];

            int totalBeats = (int)(totalDur / beatDur);

            // 헤비 드럼 패턴
            for (int beat = 0; beat < totalBeats; beat++)
            {
                int sampleStart = (int)(beat * beatDur * SR);

                // 더블 킥: 매 비트
                if (beat % 2 == 0)
                {
                    float[] kick = GenerateKick(0.15f, SR);
                    for (int i = 0; i < kick.Length; i++)
                    {
                        int idx = sampleStart + i;
                        if (idx < totalSamples) result[idx] += kick[i] * 0.55f;
                    }
                }

                // 스네어: 매 4비트 오프셋 2
                if (beat % 4 == 2)
                {
                    float[] snare = GenerateSnare(0.12f, SR);
                    for (int i = 0; i < snare.Length; i++)
                    {
                        int idx = sampleStart + i;
                        if (idx < totalSamples) result[idx] += snare[i] * 0.4f;
                    }
                }

                // 하이햇: 16분음표
                for (int sub = 0; sub < 2; sub++)
                {
                    int subStart = sampleStart + (int)(sub * beatDur * 0.5f * SR);
                    float[] hat = GenerateHiHat(0.03f, false, SR);
                    for (int i = 0; i < hat.Length; i++)
                    {
                        int idx = subStart + i;
                        if (idx < totalSamples) result[idx] += hat[i] * 0.15f;
                    }
                }
            }

            // Am-E-F-G 보스 코드 진행
            float[][] chordNotes = {
                new[] { NoteNameToFreq("A2"), NoteNameToFreq("C3"), NoteNameToFreq("E3") },
                new[] { NoteNameToFreq("E2"), NoteNameToFreq("G#2"), NoteNameToFreq("B2") },
                new[] { NoteNameToFreq("F2"), NoteNameToFreq("A2"), NoteNameToFreq("C3") },
                new[] { NoteNameToFreq("G2"), NoteNameToFreq("B2"), NoteNameToFreq("D3") }
            };

            // 파워 코드 베이스
            for (int chord = 0; chord < 4; chord++)
            {
                float chordStart = chord * barDur * 2f;
                float[] notes = chordNotes[chord];

                // 8분음표 리듬의 파워코드
                for (int beat = 0; beat < 16; beat++)
                {
                    float noteStart = chordStart + beat * (beatDur / 2f);
                    int sampleStart = (int)(noteStart * SR);
                    float noteDur = beatDur * 0.35f;

                    // 루트 + 5도
                    var root = GenerateTone(Sawtooth, notes[0], noteDur, SR,
                        new ADSREnvelope(0.003f, 0.015f, 0.6f, 0.02f));
                    var fifth = GenerateTone(Sawtooth, notes[2], noteDur, SR,
                        new ADSREnvelope(0.003f, 0.015f, 0.5f, 0.02f));
                    LowPass(root, 0.06f);
                    LowPass(fifth, 0.06f);

                    for (int i = 0; i < root.Length; i++)
                    {
                        int idx = sampleStart + i;
                        if (idx < totalSamples)
                        {
                            result[idx] += root[i] * 0.15f;
                            if (i < fifth.Length) result[idx] += fifth[i] * 0.1f;
                        }
                    }
                }
            }

            // 긴장 멜로디 (마이너 스케일 기반)
            float[] melodyNotes = {
                NoteNameToFreq("A4"), NoteNameToFreq("C5"), NoteNameToFreq("B4"),
                NoteNameToFreq("A4"), NoteNameToFreq("E5"), NoteNameToFreq("D5"),
                NoteNameToFreq("C5"), NoteNameToFreq("B4"), NoteNameToFreq("A4"),
                NoteNameToFreq("G4"), NoteNameToFreq("A4"), NoteNameToFreq("C5"),
                NoteNameToFreq("E5"), NoteNameToFreq("D5"), NoteNameToFreq("C5"),
                NoteNameToFreq("A4")
            };

            float melodyNoteDur = beatDur;
            for (int n = 0; n < melodyNotes.Length; n++)
            {
                float noteStart = n * melodyNoteDur;
                int sampleStart = (int)(noteStart * SR);
                float[] note = GenerateTone(Square, melodyNotes[n], melodyNoteDur * 0.8f, SR,
                    new ADSREnvelope(0.005f, 0.03f, 0.4f, 0.05f));
                LowPass(note, 0.12f);

                for (int i = 0; i < note.Length; i++)
                {
                    int idx = sampleStart + i;
                    if (idx < totalSamples) result[idx] += note[i] * 0.12f;
                }
            }

            // 반복 멜로디 (후반부)
            int melodyRepeatStart = (int)(barDur * 4f * SR);
            for (int n = 0; n < melodyNotes.Length; n++)
            {
                float noteStart = n * melodyNoteDur;
                int sampleStart = melodyRepeatStart + (int)(noteStart * SR);
                float freq = melodyNotes[n] * 1.0f; // 같은 옥타브
                float[] note = GenerateTone(Square, freq, melodyNoteDur * 0.8f, SR,
                    new ADSREnvelope(0.005f, 0.03f, 0.45f, 0.05f));
                LowPass(note, 0.12f);

                for (int i = 0; i < note.Length; i++)
                {
                    int idx = sampleStart + i;
                    if (idx < totalSamples) result[idx] += note[i] * 0.13f;
                }
            }

            FadeInOut(result, (int)(0.2f * SR), (int)(0.8f * SR));
            Normalize(result);
            _cache["bgm_boss"] = CreateClip("bgm_boss", result, SR);
        }
    }
}
