using UnityEngine;
using SoulCraft.Core;

namespace SoulCraft.Combat
{
    /// <summary>
    /// 데미지 계산 유틸리티.
    /// 공식: (공격력 * 스킬배율 - 방어력*0.5) * 속성보너스 * 크리티컬 * 콤보보너스
    /// 최소 데미지 1 보장.
    /// </summary>
    public static class DamageCalculator
    {
        // --- 속성 상성 배율 ---
        private const float StrongMultiplier = 1.5f;
        private const float WeakMultiplier = 0.5f;
        private const float NeutralMultiplier = 1.0f;
        private const float CounterMultiplier = 2.0f; // Dark <-> Holy 상극

        /// <summary>
        /// 최종 데미지를 계산한다.
        /// </summary>
        /// <param name="attackPower">공격자의 공격력</param>
        /// <param name="skillMultiplier">스킬 데미지 배율</param>
        /// <param name="defense">대상의 방어력</param>
        /// <param name="attackElement">공격 속성</param>
        /// <param name="targetElement">대상 속성</param>
        /// <param name="critRate">크리티컬 확률 (0~1)</param>
        /// <param name="critDamage">크리티컬 데미지 배율 (예: 1.5)</param>
        /// <param name="comboBonus">콤보 보너스 배율</param>
        /// <returns>계산된 데미지 결과</returns>
        public static DamageResult Calculate(
            int attackPower,
            float skillMultiplier,
            int defense,
            DamageType attackElement,
            DamageType targetElement,
            float critRate,
            float critDamage,
            float comboBonus = 1f)
        {
            // 기본 데미지 = 공격력 * 스킬배율 - 방어력 * 0.5
            float baseDamage = attackPower * skillMultiplier - defense * 0.5f;

            // 속성 보너스
            float elementBonus = GetElementMultiplier(attackElement, targetElement);

            // 크리티컬 판정
            bool isCritical = RollCritical(critRate);
            float critMultiplier = isCritical ? critDamage : 1f;

            // 최종 계산
            float finalDamage = baseDamage * elementBonus * critMultiplier * comboBonus;

            // 최소 데미지 보장
            int damage = Mathf.Max(1, Mathf.RoundToInt(finalDamage));

            return new DamageResult
            {
                Damage = damage,
                IsCritical = isCritical,
                ElementMultiplier = elementBonus,
                ComboMultiplier = comboBonus,
                AttackElement = attackElement
            };
        }

        /// <summary>
        /// 속성 상성 배율을 반환한다.
        /// Fire > Ice > Lightning > Fire (삼각 상성)
        /// Dark <-> Holy (상극)
        /// </summary>
        public static float GetElementMultiplier(DamageType attack, DamageType target)
        {
            if (attack == target) return NeutralMultiplier;
            if (attack == DamageType.Physical || target == DamageType.Physical)
                return NeutralMultiplier;

            // 삼각 상성: Fire > Ice > Lightning > Fire
            if (attack == DamageType.Fire && target == DamageType.Ice) return StrongMultiplier;
            if (attack == DamageType.Ice && target == DamageType.Lightning) return StrongMultiplier;
            if (attack == DamageType.Lightning && target == DamageType.Fire) return StrongMultiplier;

            // 역상성
            if (attack == DamageType.Ice && target == DamageType.Fire) return WeakMultiplier;
            if (attack == DamageType.Lightning && target == DamageType.Ice) return WeakMultiplier;
            if (attack == DamageType.Fire && target == DamageType.Lightning) return WeakMultiplier;

            // Dark <-> Holy 상극 (서로에게 강함)
            if (attack == DamageType.Dark && target == DamageType.Holy) return CounterMultiplier;
            if (attack == DamageType.Holy && target == DamageType.Dark) return CounterMultiplier;

            return NeutralMultiplier;
        }

        /// <summary>
        /// 크리티컬 판정을 수행한다.
        /// </summary>
        public static bool RollCritical(float critRate)
        {
            return Random.value < critRate;
        }
    }

    /// <summary>
    /// 데미지 계산 결과.
    /// </summary>
    public struct DamageResult
    {
        public int Damage;
        public bool IsCritical;
        public float ElementMultiplier;
        public float ComboMultiplier;
        public DamageType AttackElement;
    }
}
