using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SoulCraft.Core;

namespace SoulCraft.Audio
{
    /// <summary>
    /// 프로시저럴 사운드 시스템의 중앙 관리자.
    /// BGM(루프) + SFX(원샷, 최대 8채널 풀) 관리.
    /// GameEventSystem을 구독하여 게임 이벤트에 자동으로 사운드를 재생한다.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        // ════════════════════════════════════════════════════════
        //  Singleton
        // ════════════════════════════════════════════════════════

        public static AudioManager Instance { get; private set; }

        // ════════════════════════════════════════════════════════
        //  Inspector
        // ════════════════════════════════════════════════════════

        [Header("Volume")]
        [Range(0f, 1f)] [SerializeField] private float masterVolume = 1f;
        [Range(0f, 1f)] [SerializeField] private float bgmVolume = 0.6f;
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 0.8f;

        [Header("SFX Pool")]
        [SerializeField] private int maxSfxChannels = 8;

        // ════════════════════════════════════════════════════════
        //  Runtime
        // ════════════════════════════════════════════════════════

        private AudioSource _bgmSource;
        private readonly List<AudioSource> _sfxPool = new();
        private Coroutine _bgmFadeCoroutine;
        private string _currentBgmKey;

        // 스킬ID → 속성 매핑 (런타임 캐시)
        private readonly Dictionary<string, DamageType> _skillElementCache = new();

        // ════════════════════════════════════════════════════════
        //  Unity Lifecycle
        // ════════════════════════════════════════════════════════

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeAudioSources();
            SoundFactory.Initialize();
        }

        void OnEnable()
        {
            SubscribeEvents();
        }

        void OnDisable()
        {
            UnsubscribeEvents();
        }

        // ════════════════════════════════════════════════════════
        //  Initialization
        // ════════════════════════════════════════════════════════

        private void InitializeAudioSources()
        {
            // BGM Source
            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.loop = true;
            _bgmSource.playOnAwake = false;
            _bgmSource.volume = bgmVolume * masterVolume;
            _bgmSource.priority = 0;

            // SFX Pool
            for (int i = 0; i < maxSfxChannels; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.loop = false;
                src.playOnAwake = false;
                src.priority = 128;
                _sfxPool.Add(src);
            }
        }

        // ════════════════════════════════════════════════════════
        //  Public API — SFX
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// SFX를 원샷으로 재생한다. 빈 채널을 찾아 재생하며,
        /// 모두 사용 중이면 가장 오래 재생 중인 채널을 빼앗는다.
        /// </summary>
        public void PlaySFX(string key, float volume = 1f, float pitch = 1f)
        {
            AudioClip clip = SoundFactory.GetClip(key);
            if (clip == null) return;

            AudioSource source = GetAvailableSfxSource();
            source.clip = clip;
            source.volume = volume * sfxVolume * masterVolume;
            source.pitch = pitch;
            source.Play();
        }

        /// <summary>SFX를 약간의 랜덤 피치로 재생 (반복 재생 시 단조로움 방지).</summary>
        public void PlaySFXRandomized(string key, float volume = 1f, float pitchMin = 0.9f, float pitchMax = 1.1f)
        {
            float pitch = Random.Range(pitchMin, pitchMax);
            PlaySFX(key, volume, pitch);
        }

        // ════════════════════════════════════════════════════════
        //  Public API — BGM
        // ════════════════════════════════════════════════════════

        /// <summary>BGM을 페이드인으로 시작한다. 이미 재생 중이면 크로스페이드.</summary>
        public void PlayBGM(string key, float fadeTime = 1f)
        {
            if (_currentBgmKey == key && _bgmSource.isPlaying) return;

            AudioClip clip = SoundFactory.GetClip(key);
            if (clip == null) return;

            if (_bgmFadeCoroutine != null)
                StopCoroutine(_bgmFadeCoroutine);

            _bgmFadeCoroutine = StartCoroutine(CrossFadeBGM(clip, key, fadeTime));
        }

        /// <summary>BGM을 페이드아웃으로 정지한다.</summary>
        public void StopBGM(float fadeTime = 1f)
        {
            if (!_bgmSource.isPlaying) return;

            if (_bgmFadeCoroutine != null)
                StopCoroutine(_bgmFadeCoroutine);

            _bgmFadeCoroutine = StartCoroutine(FadeOutBGM(fadeTime));
        }

        // ════════════════════════════════════════════════════════
        //  Public API — Volume
        // ════════════════════════════════════════════════════════

        public float MasterVolume
        {
            get => masterVolume;
            set
            {
                masterVolume = Mathf.Clamp01(value);
                ApplyVolumes();
            }
        }

        public float BgmVolume
        {
            get => bgmVolume;
            set
            {
                bgmVolume = Mathf.Clamp01(value);
                ApplyVolumes();
            }
        }

        public float SfxVolume
        {
            get => sfxVolume;
            set
            {
                sfxVolume = Mathf.Clamp01(value);
                // SFX는 PlaySFX 시점에 volume을 설정하므로 별도 갱신 불필요
            }
        }

        /// <summary>스킬ID와 속성 매핑을 등록한다 (SkillData 로딩 후 호출).</summary>
        public void RegisterSkillElement(string skillId, DamageType element)
        {
            _skillElementCache[skillId] = element;
        }

        // ════════════════════════════════════════════════════════
        //  Event Subscriptions
        // ════════════════════════════════════════════════════════

        private void SubscribeEvents()
        {
            GameEventSystem.Subscribe<DamageEvent>(OnDamage);
            GameEventSystem.Subscribe<EnemyDeathEvent>(OnEnemyDeath);
            GameEventSystem.Subscribe<SkillUsedEvent>(OnSkillUsed);
            GameEventSystem.Subscribe<ComboEvent>(OnCombo);
            GameEventSystem.Subscribe<BossPhaseChangeEvent>(OnBossPhaseChange);
            GameEventSystem.Subscribe<PlayerHealEvent>(OnPlayerHeal);
            GameEventSystem.Subscribe<SoulCraft.Farming.ItemPickupEvent>(OnItemPickup);
        }

        private void UnsubscribeEvents()
        {
            GameEventSystem.Unsubscribe<DamageEvent>(OnDamage);
            GameEventSystem.Unsubscribe<EnemyDeathEvent>(OnEnemyDeath);
            GameEventSystem.Unsubscribe<SkillUsedEvent>(OnSkillUsed);
            GameEventSystem.Unsubscribe<ComboEvent>(OnCombo);
            GameEventSystem.Unsubscribe<BossPhaseChangeEvent>(OnBossPhaseChange);
            GameEventSystem.Unsubscribe<PlayerHealEvent>(OnPlayerHeal);
            GameEventSystem.Unsubscribe<SoulCraft.Farming.ItemPickupEvent>(OnItemPickup);
        }

        // ════════════════════════════════════════════════════════
        //  Event Handlers
        // ════════════════════════════════════════════════════════

        private void OnDamage(DamageEvent evt)
        {
            if (evt.IsCritical)
                PlaySFXRandomized("sfx_critical", 0.9f, 0.95f, 1.05f);
            else
                PlaySFXRandomized("sfx_hit", 0.8f, 0.9f, 1.1f);
        }

        private void OnEnemyDeath(EnemyDeathEvent evt)
        {
            PlaySFX("sfx_kill");
        }

        private void OnSkillUsed(SkillUsedEvent evt)
        {
            // 스킬 속성에 따른 사운드 분기
            if (_skillElementCache.TryGetValue(evt.SkillId, out var element))
            {
                string sfxKey = element switch
                {
                    DamageType.Fire      => "sfx_fire",
                    DamageType.Ice       => "sfx_ice",
                    DamageType.Lightning => "sfx_lightning",
                    DamageType.Dark      => "sfx_dark",
                    DamageType.Holy      => "sfx_holy",
                    _                    => "sfx_attack_1" // Physical
                };
                PlaySFXRandomized(sfxKey, 0.85f);
            }
            else
            {
                // 매핑 없으면 기본 공격음
                PlaySFXRandomized("sfx_attack_1", 0.7f);
            }
        }

        private void OnCombo(ComboEvent evt)
        {
            PlaySFX("sfx_combo");
        }

        private void OnBossPhaseChange(BossPhaseChangeEvent evt)
        {
            PlaySFX("sfx_boss_phase");
        }

        private void OnPlayerHeal(PlayerHealEvent evt)
        {
            PlaySFX("sfx_heal");
        }

        private void OnItemPickup(SoulCraft.Farming.ItemPickupEvent evt)
        {
            PlaySFXRandomized("sfx_item_pickup", 0.7f, 0.95f, 1.1f);
        }

        // ════════════════════════════════════════════════════════
        //  Internal — SFX Pool
        // ════════════════════════════════════════════════════════

        private AudioSource GetAvailableSfxSource()
        {
            // 1. 빈 채널 탐색
            foreach (var src in _sfxPool)
            {
                if (!src.isPlaying) return src;
            }

            // 2. 모두 사용 중이면 가장 오래 재생된 채널 빼앗기
            AudioSource oldest = _sfxPool[0];
            float oldestTime = float.MaxValue;
            foreach (var src in _sfxPool)
            {
                float remaining = 0f;
                if (src.clip != null)
                    remaining = src.clip.length - src.time;
                if (remaining < oldestTime)
                {
                    oldestTime = remaining;
                    oldest = src;
                }
            }

            oldest.Stop();
            return oldest;
        }

        // ════════════════════════════════════════════════════════
        //  Internal — BGM Fading
        // ════════════════════════════════════════════════════════

        private IEnumerator CrossFadeBGM(AudioClip newClip, string key, float fadeTime)
        {
            float targetVol = bgmVolume * masterVolume;

            // 현재 재생 중이면 페이드아웃
            if (_bgmSource.isPlaying && fadeTime > 0f)
            {
                float startVol = _bgmSource.volume;
                float elapsed = 0f;
                float halfFade = fadeTime * 0.5f;

                while (elapsed < halfFade)
                {
                    elapsed += Time.unscaledDeltaTime;
                    _bgmSource.volume = Mathf.Lerp(startVol, 0f, elapsed / halfFade);
                    yield return null;
                }

                _bgmSource.Stop();
            }

            // 새 BGM 시작
            _bgmSource.clip = newClip;
            _bgmSource.volume = 0f;
            _bgmSource.Play();
            _currentBgmKey = key;

            // 페이드인
            if (fadeTime > 0f)
            {
                float elapsed = 0f;
                float halfFade = fadeTime * 0.5f;

                while (elapsed < halfFade)
                {
                    elapsed += Time.unscaledDeltaTime;
                    _bgmSource.volume = Mathf.Lerp(0f, targetVol, elapsed / halfFade);
                    yield return null;
                }
            }

            _bgmSource.volume = targetVol;
            _bgmFadeCoroutine = null;
        }

        private IEnumerator FadeOutBGM(float fadeTime)
        {
            float startVol = _bgmSource.volume;
            float elapsed = 0f;

            while (elapsed < fadeTime)
            {
                elapsed += Time.unscaledDeltaTime;
                _bgmSource.volume = Mathf.Lerp(startVol, 0f, elapsed / fadeTime);
                yield return null;
            }

            _bgmSource.Stop();
            _bgmSource.volume = 0f;
            _currentBgmKey = null;
            _bgmFadeCoroutine = null;
        }

        private void ApplyVolumes()
        {
            if (_bgmSource != null && _bgmSource.isPlaying)
                _bgmSource.volume = bgmVolume * masterVolume;
        }
    }
}
