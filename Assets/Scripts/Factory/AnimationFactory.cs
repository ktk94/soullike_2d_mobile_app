using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoulCraft.Factory
{
    // ================================================================
    //  SpriteAnimationData
    //  하나의 애니메이션 클립에 해당하는 데이터 (프레임 배열, fps, 루프 여부, 프레임 이벤트)
    // ================================================================

    [Serializable]
    public class SpriteAnimationData
    {
        public string name;
        public Sprite[] frames;
        public float fps = 8f;
        public bool loop = true;

        /// <summary>
        /// 특정 프레임 인덱스에서 발동할 콜백.
        /// Key = 프레임 인덱스, Value = 콜백
        /// </summary>
        public Dictionary<int, Action> frameEvents = new();

        /// <summary>애니메이션 재생 완료 시 콜백 (비루프 전용).</summary>
        public Action onComplete;

        public float FrameDuration => fps > 0f ? 1f / fps : 1f;
        public float TotalDuration => frames != null ? frames.Length * FrameDuration : 0f;
    }

    // ================================================================
    //  SpriteAnimator
    //  SpriteRenderer에 프레임 배열을 순환 재생하는 코루틴 기반 컴포넌트.
    // ================================================================

    public class SpriteAnimator : MonoBehaviour
    {
        // ── References ──────────────────────────────────────
        private SpriteRenderer _spriteRenderer;

        // ── Animation Registry ──────────────────────────────
        private readonly Dictionary<string, SpriteAnimationData> _animations = new();

        // ── Playback State ──────────────────────────────────
        private SpriteAnimationData _currentAnim;
        private int _currentFrameIndex;
        private Coroutine _playbackCoroutine;
        private bool _isPaused;

        /// <summary>현재 재생 중인 애니메이션 이름. 없으면 null.</summary>
        public string CurrentAnimation => _currentAnim?.name;

        /// <summary>현재 프레임 인덱스.</summary>
        public int CurrentFrame => _currentFrameIndex;

        /// <summary>재생 중인지 여부.</summary>
        public bool IsPlaying => _playbackCoroutine != null && !_isPaused;

        // ── Callbacks ───────────────────────────────────────

        /// <summary>애니메이션 재생이 완료되었을 때 호출 (비루프 전용).</summary>
        public event Action<string> OnAnimationComplete;

        /// <summary>특정 프레임에 도달했을 때 호출. (animName, frameIndex)</summary>
        public event Action<string, int> OnFrameEvent;

        // ============================================================
        //  Unity Lifecycle
        // ============================================================

        void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer == null)
                _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        void OnDisable()
        {
            StopPlayback();
        }

        // ============================================================
        //  Public API
        // ============================================================

        /// <summary>
        /// 애니메이션 데이터를 등록한다.
        /// </summary>
        public void AddAnimation(SpriteAnimationData data)
        {
            if (data == null || string.IsNullOrEmpty(data.name)) return;
            _animations[data.name] = data;
        }

        /// <summary>
        /// 복수의 애니메이션을 한 번에 등록한다.
        /// </summary>
        public void AddAnimations(IEnumerable<SpriteAnimationData> dataList)
        {
            foreach (var data in dataList)
                AddAnimation(data);
        }

        /// <summary>
        /// 등록된 애니메이션 데이터를 반환한다.
        /// </summary>
        public SpriteAnimationData GetAnimationData(string animName)
        {
            _animations.TryGetValue(animName, out var data);
            return data;
        }

        /// <summary>
        /// 지정된 애니메이션을 재생한다.
        /// 이미 같은 애니메이션이 재생 중이면 무시한다 (force=false 시).
        /// </summary>
        public void PlayAnimation(string animName, bool force = false)
        {
            if (!_animations.TryGetValue(animName, out var data))
            {
                Debug.LogWarning($"[SpriteAnimator] Animation not found: {animName}");
                return;
            }

            // 같은 애니메이션이 이미 재생 중이면 무시
            if (!force && _currentAnim != null && _currentAnim.name == animName && _playbackCoroutine != null)
                return;

            StopPlayback();
            _currentAnim = data;
            _currentFrameIndex = 0;
            _isPaused = false;
            _playbackCoroutine = StartCoroutine(PlaybackCoroutine(data));
        }

        /// <summary>
        /// 현재 재생을 일시정지한다.
        /// </summary>
        public void Pause()
        {
            _isPaused = true;
        }

        /// <summary>
        /// 일시정지를 해제한다.
        /// </summary>
        public void Resume()
        {
            _isPaused = false;
        }

        /// <summary>
        /// 현재 재생을 즉시 중단한다.
        /// </summary>
        public void Stop()
        {
            StopPlayback();
        }

        /// <summary>
        /// SpriteRenderer 참조를 수동으로 설정한다 (자식 오브젝트 등).
        /// </summary>
        public void SetSpriteRenderer(SpriteRenderer sr)
        {
            _spriteRenderer = sr;
        }

        // ============================================================
        //  Playback Coroutine
        // ============================================================

        private IEnumerator PlaybackCoroutine(SpriteAnimationData data)
        {
            if (data.frames == null || data.frames.Length == 0)
            {
                _playbackCoroutine = null;
                yield break;
            }

            float frameDuration = data.FrameDuration;
            int totalFrames = data.frames.Length;

            // 단일 프레임이면 스프라이트만 설정하고 끝
            if (totalFrames == 1)
            {
                SetSprite(data.frames[0]);
                _currentFrameIndex = 0;
                FireFrameEvents(data, 0);

                // 비루프인 경우 onComplete 콜백 호출
                if (!data.loop)
                {
                    data.onComplete?.Invoke();
                    OnAnimationComplete?.Invoke(data.name);
                }

                _playbackCoroutine = null;
                yield break;
            }

            // 멀티 프레임 재생
            while (true)
            {
                for (int i = 0; i < totalFrames; i++)
                {
                    // 일시정지 대기
                    while (_isPaused)
                        yield return null;

                    _currentFrameIndex = i;
                    SetSprite(data.frames[i]);

                    // 프레임 이벤트 발동
                    FireFrameEvents(data, i);

                    yield return new WaitForSeconds(frameDuration);
                }

                // 루프가 아니면 재생 완료
                if (!data.loop)
                {
                    data.onComplete?.Invoke();
                    OnAnimationComplete?.Invoke(data.name);
                    _playbackCoroutine = null;
                    yield break;
                }
            }
        }

        private void FireFrameEvents(SpriteAnimationData data, int frameIndex)
        {
            if (data.frameEvents != null && data.frameEvents.TryGetValue(frameIndex, out var callback))
                callback?.Invoke();

            OnFrameEvent?.Invoke(data.name, frameIndex);
        }

        private void SetSprite(Sprite sprite)
        {
            if (_spriteRenderer != null && sprite != null)
                _spriteRenderer.sprite = sprite;
        }

        private void StopPlayback()
        {
            if (_playbackCoroutine != null)
            {
                StopCoroutine(_playbackCoroutine);
                _playbackCoroutine = null;
            }
            _currentAnim = null;
            _currentFrameIndex = 0;
            _isPaused = false;
        }
    }

    // ================================================================
    //  AnimationFactory
    //  SpriteFactory를 이용해 SpriteAnimationData 세트를 생성한다.
    // ================================================================

    public static class AnimationFactory
    {
        // ============================================================
        //  Player Animations
        // ============================================================

        /// <summary>
        /// 플레이어 애니메이션 세트를 생성하여 SpriteAnimator에 등록한다.
        /// 반환된 리스트를 직접 활용할 수도 있다.
        /// </summary>
        public static List<SpriteAnimationData> CreatePlayerAnimations()
        {
            var animations = new List<SpriteAnimationData>();

            // ── idle: 1프레임, 정적 ──
            animations.Add(new SpriteAnimationData
            {
                name = "idle",
                frames = new[] { SpriteFactory.GetSprite("player_idle") },
                fps = 1f,
                loop = true
            });

            // ── walk: 2프레임, 8fps, 루프 ──
            animations.Add(new SpriteAnimationData
            {
                name = "walk",
                frames = new[]
                {
                    SpriteFactory.GetSprite("player_walk_1"),
                    SpriteFactory.GetSprite("player_walk_2")
                },
                fps = 8f,
                loop = true
            });

            // ── attack_1: 12fps, 비루프, 프레임 2에서 히트 콜백 ──
            var attack1 = new SpriteAnimationData
            {
                name = "attack_1",
                frames = new[]
                {
                    SpriteFactory.GetSprite("player_idle"),      // 준비 자세
                    SpriteFactory.GetSprite("player_attack_1"),  // 스윙
                    SpriteFactory.GetSprite("player_attack_1"),  // 히트 프레임
                    SpriteFactory.GetSprite("player_idle")       // 복귀
                },
                fps = 12f,
                loop = false
            };
            // 프레임 2에서 히트 판정 콜백 — 외부에서 등록
            // attack1.frameEvents[2] = () => { /* hit callback */ };
            animations.Add(attack1);

            // ── attack_2: 12fps, 비루프 ──
            animations.Add(new SpriteAnimationData
            {
                name = "attack_2",
                frames = new[]
                {
                    SpriteFactory.GetSprite("player_idle"),
                    SpriteFactory.GetSprite("player_attack_2"),
                    SpriteFactory.GetSprite("player_attack_2"),
                    SpriteFactory.GetSprite("player_idle")
                },
                fps = 12f,
                loop = false
            });

            // ── attack_3: 10fps, 비루프 ──
            animations.Add(new SpriteAnimationData
            {
                name = "attack_3",
                frames = new[]
                {
                    SpriteFactory.GetSprite("player_idle"),
                    SpriteFactory.GetSprite("player_attack_3"),
                    SpriteFactory.GetSprite("player_attack_3"),
                    SpriteFactory.GetSprite("player_attack_3"),
                    SpriteFactory.GetSprite("player_idle")
                },
                fps = 10f,
                loop = false
            });

            // ── dash: 1프레임 ──
            animations.Add(new SpriteAnimationData
            {
                name = "dash",
                frames = new[] { SpriteFactory.GetSprite("player_dash") },
                fps = 1f,
                loop = false
            });

            // ── hit: 1프레임, 0.3초 후 idle 복귀 ──
            var hitAnim = new SpriteAnimationData
            {
                name = "hit",
                frames = new[] { SpriteFactory.GetSprite("player_hit") },
                fps = 1f,
                loop = false
            };
            // onComplete는 외부에서 설정 (예: SpriteAnimator에서 idle로 전환)
            animations.Add(hitAnim);

            return animations;
        }

        /// <summary>
        /// 플레이어 SpriteAnimator를 완전히 세팅한다.
        /// attack_1의 프레임 2 히트 콜백과 hit 완료 시 idle 복귀를 자동 연결한다.
        /// </summary>
        public static void SetupPlayerAnimator(SpriteAnimator animator, Action onAttackHit = null)
        {
            var anims = CreatePlayerAnimations();

            // attack_1 프레임 2에 히트 콜백 등록
            var attack1 = anims.Find(a => a.name == "attack_1");
            if (attack1 != null && onAttackHit != null)
            {
                attack1.frameEvents[2] = onAttackHit;
            }

            // hit 완료 시 0.3초 후 idle로 복귀
            var hitAnim = anims.Find(a => a.name == "hit");
            if (hitAnim != null)
            {
                hitAnim.onComplete = () =>
                {
                    // MonoBehaviour의 Invoke를 직접 쓸 수 없으므로 코루틴 우회
                    animator.StartCoroutine(DelayedIdleReturn(animator, 0.3f));
                };
            }

            // 각 공격 애니메이션 완료 시 idle로 복귀
            foreach (var anim in anims)
            {
                if (anim.name.StartsWith("attack_"))
                {
                    var capturedAnim = anim;
                    var existingOnComplete = capturedAnim.onComplete;
                    capturedAnim.onComplete = () =>
                    {
                        existingOnComplete?.Invoke();
                        animator.PlayAnimation("idle");
                    };
                }
            }

            animator.AddAnimations(anims);
            animator.PlayAnimation("idle");
        }

        private static IEnumerator DelayedIdleReturn(SpriteAnimator animator, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (animator != null && animator.gameObject.activeInHierarchy)
                animator.PlayAnimation("idle");
        }

        // ============================================================
        //  Enemy Animations
        // ============================================================

        /// <summary>
        /// 적 타입에 맞는 애니메이션 세트를 생성한다.
        /// SpriteFactory에서 "enemy_{enemyType}_idle", "enemy_{enemyType}_move" 키로 스프라이트를 가져온다.
        /// </summary>
        public static List<SpriteAnimationData> CreateEnemyAnimations(string enemyType)
        {
            var animations = new List<SpriteAnimationData>();

            string idleKey = $"enemy_{enemyType}_idle";
            string moveKey = $"enemy_{enemyType}_move";

            Sprite idleSprite = SpriteFactory.GetSprite(idleKey);
            Sprite moveSprite = SpriteFactory.GetSprite(moveKey);

            // idle: 1프레임
            animations.Add(new SpriteAnimationData
            {
                name = "idle",
                frames = new[] { idleSprite },
                fps = 1f,
                loop = true
            });

            // move: 2프레임 (idle + move), 6fps, 루프
            animations.Add(new SpriteAnimationData
            {
                name = "move",
                frames = new[] { idleSprite, moveSprite },
                fps = 6f,
                loop = true
            });

            // hit: 1프레임 (idle 스프라이트 재활용)
            animations.Add(new SpriteAnimationData
            {
                name = "hit",
                frames = new[] { idleSprite },
                fps = 1f,
                loop = false
            });

            return animations;
        }

        /// <summary>
        /// 적 SpriteAnimator를 완전히 세팅한다.
        /// </summary>
        public static void SetupEnemyAnimator(SpriteAnimator animator, string enemyType)
        {
            var anims = CreateEnemyAnimations(enemyType);

            // hit 완료 시 idle로 복귀
            var hitAnim = anims.Find(a => a.name == "hit");
            if (hitAnim != null)
            {
                hitAnim.onComplete = () =>
                {
                    if (animator != null && animator.gameObject.activeInHierarchy)
                        animator.PlayAnimation("idle");
                };
            }

            animator.AddAnimations(anims);
            animator.PlayAnimation("idle");
        }

        // ============================================================
        //  Boss Animations
        // ============================================================

        /// <summary>
        /// 보스 타입에 맞는 애니메이션 세트를 생성한다.
        /// </summary>
        public static List<SpriteAnimationData> CreateBossAnimations(string bossType)
        {
            var animations = new List<SpriteAnimationData>();

            string idleKey = $"boss_{bossType}_idle";
            string moveKey = $"boss_{bossType}_move";

            Sprite idleSprite = SpriteFactory.GetSprite(idleKey);
            Sprite moveSprite = SpriteFactory.GetSprite(moveKey);

            animations.Add(new SpriteAnimationData
            {
                name = "idle",
                frames = new[] { idleSprite },
                fps = 1f,
                loop = true
            });

            animations.Add(new SpriteAnimationData
            {
                name = "move",
                frames = new[] { idleSprite, moveSprite },
                fps = 4f,
                loop = true
            });

            animations.Add(new SpriteAnimationData
            {
                name = "hit",
                frames = new[] { idleSprite },
                fps = 1f,
                loop = false
            });

            return animations;
        }

        /// <summary>
        /// 보스 SpriteAnimator를 완전히 세팅한다.
        /// </summary>
        public static void SetupBossAnimator(SpriteAnimator animator, string bossType)
        {
            var anims = CreateBossAnimations(bossType);

            var hitAnim = anims.Find(a => a.name == "hit");
            if (hitAnim != null)
            {
                hitAnim.onComplete = () =>
                {
                    if (animator != null && animator.gameObject.activeInHierarchy)
                        animator.PlayAnimation("idle");
                };
            }

            animator.AddAnimations(anims);
            animator.PlayAnimation("idle");
        }
    }
}
