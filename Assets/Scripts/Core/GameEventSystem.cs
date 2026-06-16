using System;
using System.Collections.Generic;

namespace SoulCraft.Core
{
    /// <summary>
    /// 타입 기반 글로벌 이벤트 시스템. 커플링 없이 시스템 간 통신.
    /// </summary>
    public static class GameEventSystem
    {
        private static readonly Dictionary<Type, Delegate> _events = new();

        public static void Subscribe<T>(Action<T> handler) where T : struct
        {
            var type = typeof(T);
            if (_events.TryGetValue(type, out var existing))
                _events[type] = Delegate.Combine(existing, handler);
            else
                _events[type] = handler;
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            var type = typeof(T);
            if (_events.TryGetValue(type, out var existing))
            {
                var result = Delegate.Remove(existing, handler);
                if (result == null)
                    _events.Remove(type);
                else
                    _events[type] = result;
            }
        }

        public static void Publish<T>(T evt) where T : struct
        {
            if (_events.TryGetValue(typeof(T), out var handler))
                ((Action<T>)handler)?.Invoke(evt);
        }

        public static void Clear() => _events.Clear();
    }

    // --- Game Events ---

    public struct DamageEvent
    {
        public UnityEngine.GameObject Attacker;
        public UnityEngine.GameObject Target;
        public int Damage;
        public bool IsCritical;
        public DamageType Type;
        public UnityEngine.Vector2 HitPoint;
    }

    public struct EnemyDeathEvent
    {
        public UnityEngine.GameObject Enemy;
        public UnityEngine.Vector2 Position;
        public string EnemyId;
    }

    public struct BossPhaseChangeEvent
    {
        public int NewPhase;
        public float HpRatio;
    }

    public struct ItemDropEvent
    {
        public string ItemId;
        public UnityEngine.Vector2 Position;
        public int Quantity;
    }

    public struct PlayerHealEvent
    {
        public int Amount;
        public int CurrentHp;
    }

    public struct SkillUsedEvent
    {
        public string SkillId;
        public float Cooldown;
    }

    public struct ComboEvent
    {
        public string ComboName;
        public int ComboCount;
        public float BonusDamageMultiplier;
    }

    public struct StageCompleteEvent
    {
        public int StageIndex;
        public int FloorIndex;
        public float ClearTime;
    }

    public struct EnemyRewardEvent
    {
        public int Exp;
        public int Gold;
        public UnityEngine.Vector2 Position;
    }

    public struct SynergyActivatedEvent
    {
        public string SynergyId;
        public string SynergyName;
        public string UnlockMessage;
        public string SkillId;
    }

    public struct SynergyDeactivatedEvent
    {
        public string SynergyId;
        public string SynergyName;
        public string SkillId;
    }

    public struct PassiveUnlockedEvent
    {
        public string PassiveId;
        public int NewLevel;
    }

    public enum DamageType
    {
        Physical,
        Fire,
        Ice,
        Lightning,
        Dark,
        Holy
    }
}
