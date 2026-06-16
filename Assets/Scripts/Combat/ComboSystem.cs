using System.Collections.Generic;
using UnityEngine;
using SoulCraft.Core;

namespace SoulCraft.Combat
{
    /// <summary>
    /// 콤보 레시피. 일정 시간 내에 정해진 스킬 태그 시퀀스를 입력하면 보너스가 발동된다.
    /// </summary>
    [System.Serializable]
    public class ComboRecipe
    {
        public string comboName;
        [Tooltip("필요한 스킬 태그 시퀀스 (순서대로 매칭)")]
        public string[] requiredTags;
        public float bonusDamageMultiplier = 1.5f;
        [Tooltip("콤보 완성 시 스폰할 특수 효과 프리팹")]
        public GameObject specialEffectPrefab;
        [TextArea(1, 3)]
        public string description;
    }

    /// <summary>
    /// 스킬 사용 기록을 추적하고, 콤보 레시피와 매칭하여 보너스를 발동하는 핵심 시스템.
    /// </summary>
    public class ComboSystem : MonoBehaviour
    {
        [Header("Combo Settings")]
        [SerializeField] private float _comboWindow = 3f;
        [SerializeField] private int _maxChainLength = 10;

        [Header("Combo Recipes")]
        [SerializeField] private List<ComboRecipe> _recipes = new();

        // 현재 콤보 체인: (태그, 등록 시각)
        private readonly List<ComboEntry> _currentChain = new();

        // 마지막으로 완성된 콤보의 보너스 배율 (DamageCalculator가 참조)
        private float _lastComboBonus = 1f;
        private float _comboBonusExpireTime;
        private const float ComboBonusDuration = 2f;

        // 현재 히트 카운트 (연속 공격)
        private int _hitCount;

        // --- Properties ---

        /// <summary>현재 콤보 히트 카운트</summary>
        public int HitCount => _hitCount;

        /// <summary>현재 활성 콤보 보너스 배율 (만료 시 1.0)</summary>
        public float ActiveComboBonus =>
            Time.time < _comboBonusExpireTime ? _lastComboBonus : 1f;

        /// <summary>콤보 체인에 남아 있는 태그 목록 (디버그/UI용)</summary>
        public IReadOnlyList<ComboEntry> CurrentChain => _currentChain;

        // --- Unity Lifecycle ---

        void Awake()
        {
            InitializeDefaultRecipes();
        }

        void Update()
        {
            PruneExpiredEntries();
        }

        // --- Public API ---

        /// <summary>
        /// SkillManager가 스킬을 사용할 때 호출한다.
        /// 스킬이 가진 모든 comboTag를 체인에 등록하고, 레시피 매칭을 시도한다.
        /// </summary>
        public void RegisterSkillUsage(SkillData skill)
        {
            if (skill == null || skill.comboTags == null) return;

            float now = Time.time;
            _hitCount++;

            foreach (string tag in skill.comboTags)
            {
                if (string.IsNullOrEmpty(tag)) continue;

                _currentChain.Add(new ComboEntry
                {
                    Tag = tag,
                    Timestamp = now
                });
            }

            // 체인 길이 제한
            while (_currentChain.Count > _maxChainLength)
                _currentChain.RemoveAt(0);

            // 레시피 매칭 (가장 긴 레시피부터 검사)
            TryMatchRecipes();
        }

        /// <summary>
        /// 콤보 레시피를 런타임에 추가한다.
        /// </summary>
        public void AddRecipe(ComboRecipe recipe)
        {
            if (recipe != null && !_recipes.Contains(recipe))
                _recipes.Add(recipe);
        }

        /// <summary>
        /// 콤보 체인과 히트 카운트를 초기화한다.
        /// </summary>
        public void ResetCombo()
        {
            _currentChain.Clear();
            _hitCount = 0;
            _lastComboBonus = 1f;
        }

        // --- Private ---

        /// <summary>
        /// 기본 콤보 레시피를 등록한다.
        /// Inspector에서 레시피가 비어 있을 경우 기본값을 채운다.
        /// </summary>
        private void InitializeDefaultRecipes()
        {
            if (_recipes.Count > 0) return;

            // 화염베기 -> 바람가르기 = "화염 폭풍"
            _recipes.Add(new ComboRecipe
            {
                comboName = "화염 폭풍",
                requiredTags = new[] { "Fire", "Wind" },
                bonusDamageMultiplier = 1.8f,
                description = "화염베기 후 바람가르기로 화염 폭풍을 일으킨다."
            });

            // 빙결창 -> 번개타격 = "서리 번개"
            _recipes.Add(new ComboRecipe
            {
                comboName = "서리 번개",
                requiredTags = new[] { "Ice", "Lightning" },
                bonusDamageMultiplier = 2.0f,
                description = "빙결창 후 번개타격으로 서리 번개를 소환한다."
            });

            // 기본공격 3히트 -> 강공격 = "피니셔"
            _recipes.Add(new ComboRecipe
            {
                comboName = "피니셔",
                requiredTags = new[] { "BasicAttack", "BasicAttack", "BasicAttack", "HeavyAttack" },
                bonusDamageMultiplier = 2.5f,
                description = "기본공격 3연타 후 강공격으로 피니셔를 날린다."
            });
        }

        /// <summary>
        /// 콤보 윈도우를 초과한 오래된 엔트리를 제거한다.
        /// </summary>
        private void PruneExpiredEntries()
        {
            float cutoff = Time.time - _comboWindow;
            _currentChain.RemoveAll(e => e.Timestamp < cutoff);

            // 체인이 비면 히트 카운트도 리셋
            if (_currentChain.Count == 0)
                _hitCount = 0;
        }

        /// <summary>
        /// 현재 체인의 끝부분에서 레시피와 일치하는 시퀀스를 찾는다.
        /// 여러 레시피가 동시에 매칭되면 가장 긴(가장 구체적인) 것을 우선한다.
        /// </summary>
        private void TryMatchRecipes()
        {
            ComboRecipe bestMatch = null;
            int bestLength = 0;

            foreach (var recipe in _recipes)
            {
                if (recipe.requiredTags == null || recipe.requiredTags.Length == 0)
                    continue;

                int reqLen = recipe.requiredTags.Length;
                if (reqLen > _currentChain.Count) continue;

                // 체인의 끝에서 reqLen 개를 비교
                bool matched = true;
                int startIdx = _currentChain.Count - reqLen;
                for (int i = 0; i < reqLen; i++)
                {
                    if (_currentChain[startIdx + i].Tag != recipe.requiredTags[i])
                    {
                        matched = false;
                        break;
                    }
                }

                if (matched && reqLen > bestLength)
                {
                    bestMatch = recipe;
                    bestLength = reqLen;
                }
            }

            if (bestMatch != null)
                ActivateCombo(bestMatch);
        }

        /// <summary>
        /// 콤보를 발동한다: 보너스 설정, 이펙트 스폰, 이벤트 발행, 체인 초기화.
        /// </summary>
        private void ActivateCombo(ComboRecipe recipe)
        {
            _lastComboBonus = recipe.bonusDamageMultiplier;
            _comboBonusExpireTime = Time.time + ComboBonusDuration;

            // 특수 효과 스폰
            if (recipe.specialEffectPrefab != null)
            {
                Instantiate(recipe.specialEffectPrefab, transform.position, Quaternion.identity);
            }

            // 이벤트 발행
            GameEventSystem.Publish(new ComboEvent
            {
                ComboName = recipe.comboName,
                ComboCount = _hitCount,
                BonusDamageMultiplier = recipe.bonusDamageMultiplier
            });

            Debug.Log($"[ComboSystem] 콤보 발동: {recipe.comboName} (x{recipe.bonusDamageMultiplier})");

            // 매칭된 시퀀스를 체인에서 제거하여 중복 발동 방지
            int removeCount = recipe.requiredTags.Length;
            int removeStart = _currentChain.Count - removeCount;
            _currentChain.RemoveRange(removeStart, removeCount);
        }

        // --- Nested Types ---

        [System.Serializable]
        public struct ComboEntry
        {
            public string Tag;
            public float Timestamp;
        }
    }
}
