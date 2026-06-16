using UnityEngine;
using SoulCraft.Core;

namespace SoulCraft.Combat
{
    /// <summary>
    /// 플레이어 스킬 슬롯 관리 및 스킬 발동을 담당한다.
    /// 4개의 스킬 슬롯을 제공하며, 콤보 시스템과 연동된다.
    /// </summary>
    public class SkillManager : MonoBehaviour
    {
        public const int MaxSlots = 4;

        [Header("Skill Slots")]
        [SerializeField] private SkillData[] _equippedSkills = new SkillData[MaxSlots];

        [Header("References")]
        [SerializeField] private ComboSystem _comboSystem;

        // 쿨다운 타이머 (슬롯별)
        private readonly float[] _cooldownTimers = new float[MaxSlots];

        // 외부에서 마나 잔량을 제공하기 위한 델리게이트
        public System.Func<int> GetCurrentMana;
        // 마나 소모를 알리기 위한 델리게이트
        public System.Action<int> ConsumeMana;

        // --- Properties ---

        /// <summary>현재 장착된 스킬 배열 (읽기 전용 복사본)</summary>
        public SkillData[] EquippedSkills
        {
            get
            {
                var copy = new SkillData[MaxSlots];
                System.Array.Copy(_equippedSkills, copy, MaxSlots);
                return copy;
            }
        }

        // --- Unity Lifecycle ---

        void Update()
        {
            TickCooldowns();
        }

        // --- Public API ---

        /// <summary>
        /// 지정된 슬롯에 스킬을 장착한다.
        /// </summary>
        public bool EquipSkill(int slotIndex, SkillData skill)
        {
            if (slotIndex < 0 || slotIndex >= MaxSlots) return false;
            _equippedSkills[slotIndex] = skill;
            _cooldownTimers[slotIndex] = 0f;
            return true;
        }

        /// <summary>
        /// 슬롯의 스킬을 해제한다.
        /// </summary>
        public bool UnequipSkill(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxSlots) return false;
            _equippedSkills[slotIndex] = null;
            _cooldownTimers[slotIndex] = 0f;
            return true;
        }

        /// <summary>
        /// 두 슬롯의 스킬을 교체한다.
        /// </summary>
        public void SwapSkills(int slotA, int slotB)
        {
            if (slotA < 0 || slotA >= MaxSlots) return;
            if (slotB < 0 || slotB >= MaxSlots) return;

            (_equippedSkills[slotA], _equippedSkills[slotB]) =
                (_equippedSkills[slotB], _equippedSkills[slotA]);
            (_cooldownTimers[slotA], _cooldownTimers[slotB]) =
                (_cooldownTimers[slotB], _cooldownTimers[slotA]);
        }

        /// <summary>
        /// 슬롯 인덱스로 스킬을 사용한다.
        /// 쿨다운, 마나를 확인하고 성공 시 true를 반환한다.
        /// </summary>
        public bool UseSkill(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxSlots) return false;

            SkillData skill = _equippedSkills[slotIndex];
            if (skill == null) return false;

            // 쿨다운 체크
            if (_cooldownTimers[slotIndex] > 0f) return false;

            // 마나 체크
            int currentMana = GetCurrentMana?.Invoke() ?? 0;
            if (currentMana < skill.manaCost) return false;

            // 마나 소모
            ConsumeMana?.Invoke(skill.manaCost);

            // 쿨다운 시작
            _cooldownTimers[slotIndex] = skill.cooldown;

            // 스킬 이펙트 스폰
            if (skill.effectPrefab != null)
            {
                Instantiate(skill.effectPrefab, transform.position, transform.rotation);
            }

            // 콤보 시스템에 등록
            if (_comboSystem != null)
            {
                _comboSystem.RegisterSkillUsage(skill);
            }

            // 이벤트 발행
            GameEventSystem.Publish(new SkillUsedEvent
            {
                SkillId = skill.skillId,
                Cooldown = skill.cooldown
            });

            return true;
        }

        /// <summary>
        /// 슬롯의 남은 쿨다운 시간을 반환한다.
        /// </summary>
        public float GetCooldownRemaining(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxSlots) return 0f;
            return _cooldownTimers[slotIndex];
        }

        /// <summary>
        /// 슬롯의 쿨다운 비율 (0 = 준비됨, 1 = 막 사용함)을 반환한다.
        /// </summary>
        public float GetCooldownRatio(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxSlots) return 0f;
            SkillData skill = _equippedSkills[slotIndex];
            if (skill == null || skill.cooldown <= 0f) return 0f;
            return Mathf.Clamp01(_cooldownTimers[slotIndex] / skill.cooldown);
        }

        /// <summary>
        /// 해당 슬롯의 스킬이 사용 가능한지 확인한다.
        /// </summary>
        public bool CanUseSkill(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxSlots) return false;
            SkillData skill = _equippedSkills[slotIndex];
            if (skill == null) return false;
            if (_cooldownTimers[slotIndex] > 0f) return false;
            int currentMana = GetCurrentMana?.Invoke() ?? 0;
            return currentMana >= skill.manaCost;
        }

        // --- Private ---

        private void TickCooldowns()
        {
            for (int i = 0; i < MaxSlots; i++)
            {
                if (_cooldownTimers[i] > 0f)
                    _cooldownTimers[i] = Mathf.Max(0f, _cooldownTimers[i] - Time.deltaTime);
            }
        }
    }
}
