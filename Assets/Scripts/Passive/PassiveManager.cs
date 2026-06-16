using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SoulCraft.Core;
using SoulCraft.Farming;
using SoulCraft.Player;

namespace SoulCraft.Passive
{
    /// <summary>
    /// 패시브 세이브 데이터. SaveData에 포함된다.
    /// </summary>
    [Serializable]
    public class PassiveSaveEntry
    {
        public string passiveId;
        public int level;
    }

    /// <summary>
    /// 패시브 세이브 데이터 컨테이너.
    /// </summary>
    [Serializable]
    public class PassiveSaveData
    {
        public List<PassiveSaveEntry> entries = new();
    }

    /// <summary>
    /// 패시브 스킬 시스템의 핵심 매니저.
    /// 모든 패시브를 로드/관리하고, 해금/강화/보너스 계산을 담당한다.
    /// </summary>
    public class PassiveManager : MonoBehaviour
    {
        public static PassiveManager Instance { get; private set; }

        // ── Events ─────────────────────────────────────────

        /// <summary>
        /// 패시브가 해금 또는 레벨업되었을 때 발생한다. (passiveId, newLevel)
        /// </summary>
        public event Action<string, int> OnPassiveUnlocked;

        // ── Data ───────────────────────────────────────────

        /// <summary>모든 패시브 정의 데이터 (passiveId -> PassiveData)</summary>
        private readonly Dictionary<string, PassiveData> _allPassives = new();

        /// <summary>해금된 패시브와 현재 레벨 (passiveId -> level)</summary>
        private readonly Dictionary<string, int> _unlockedPassives = new();

        /// <summary>JSON으로부터 생성된 런타임 PassiveData 목록 (GC 방지)</summary>
        private readonly List<PassiveData> _runtimeAssets = new();

        // ── Dependencies ───────────────────────────────────

        private PlayerStats _playerStats;

        // ── Properties ─────────────────────────────────────

        /// <summary>등록된 모든 패시브 데이터</summary>
        public IReadOnlyDictionary<string, PassiveData> AllPassives => _allPassives;

        /// <summary>해금된 패시브와 레벨</summary>
        public IReadOnlyDictionary<string, int> UnlockedPassives => _unlockedPassives;

        // ── Lifecycle ──────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void Start()
        {
            LoadPassiveDefinitions();
            FindPlayerStats();
        }

        void OnDestroy()
        {
            // 런타임 생성 에셋 정리
            foreach (var asset in _runtimeAssets)
            {
                if (asset != null) Destroy(asset);
            }
            _runtimeAssets.Clear();

            if (Instance == this) Instance = null;
        }

        // ── Initialization ─────────────────────────────────

        /// <summary>
        /// Resources와 JSON으로부터 패시브 데이터를 로드한다.
        /// </summary>
        private void LoadPassiveDefinitions()
        {
            // 1) Resources 폴더에서 ScriptableObject 로드
            var soPassives = Resources.LoadAll<PassiveData>("Passives");
            foreach (var p in soPassives)
            {
                if (!string.IsNullOrEmpty(p.passiveId))
                    _allPassives[p.passiveId] = p;
            }

            // 2) JSON 정의 파일로부터 런타임 생성
            LoadFromJson();
        }

        /// <summary>
        /// PassiveDefinitions.json을 읽어 패시브 데이터를 런타임 생성한다.
        /// </summary>
        private void LoadFromJson()
        {
            var textAsset = Resources.Load<TextAsset>("Data/PassiveDefinitions");
            if (textAsset == null) return;

            var root = JsonUtility.FromJson<PassiveDefinitionRoot>(textAsset.text);
            if (root == null || root.passives == null) return;

            // JSON으로부터 PassiveData ScriptableObject를 런타임 생성
            // 선행 패시브 연결을 위해 먼저 모든 패시브를 생성한 뒤, 두 번째 패스로 연결한다.

            var jsonDataMap = new Dictionary<string, PassiveDefinitionJson>();
            var createdMap = new Dictionary<string, PassiveData>();

            foreach (var def in root.passives)
            {
                if (string.IsNullOrEmpty(def.passiveId)) continue;
                if (_allPassives.ContainsKey(def.passiveId)) continue; // SO가 이미 존재

                var data = ScriptableObject.CreateInstance<PassiveData>();
                data.passiveId = def.passiveId;
                data.passiveName = def.passiveName;
                data.description = def.description;
                data.maxLevel = def.maxLevel;
                data.category = ParseCategory(def.category);

                // effectPerLevel
                if (def.effects != null)
                {
                    data.effectPerLevel = new PassiveEffect[def.effects.Length];
                    for (int i = 0; i < def.effects.Length; i++)
                    {
                        data.effectPerLevel[i] = new PassiveEffect
                        {
                            statType = ParseStatType(def.effects[i].statType),
                            value = def.effects[i].value,
                            isPercentage = def.effects[i].isPercentage
                        };
                    }
                }

                // goldCost
                data.goldCost = def.goldCost;

                _runtimeAssets.Add(data);
                _allPassives[def.passiveId] = data;
                createdMap[def.passiveId] = data;
                jsonDataMap[def.passiveId] = def;
            }

            // 두 번째 패스: 선행 패시브 연결
            foreach (var kvp in createdMap)
            {
                var def = jsonDataMap[kvp.Key];
                if (def.prerequisites != null && def.prerequisites.Length > 0)
                {
                    var prereqList = new List<PassiveData>();
                    var prereqLevelList = new List<int>();

                    for (int i = 0; i < def.prerequisites.Length; i++)
                    {
                        string prereqId = def.prerequisites[i].passiveId;
                        int reqLevel = def.prerequisites[i].requiredLevel;

                        if (_allPassives.TryGetValue(prereqId, out var prereqData))
                        {
                            prereqList.Add(prereqData);
                            prereqLevelList.Add(reqLevel);
                        }
                    }

                    kvp.Value.prerequisites = prereqList.ToArray();
                    kvp.Value.prerequisiteLevels = prereqLevelList.ToArray();
                }
            }
        }

        /// <summary>
        /// PlayerStats 참조를 찾는다.
        /// </summary>
        private void FindPlayerStats()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                _playerStats = player.GetComponent<PlayerStats>();
        }

        /// <summary>
        /// 외부에서 PlayerStats를 설정한다 (씬 전환 등에서 사용).
        /// </summary>
        public void SetPlayerStats(PlayerStats stats)
        {
            _playerStats = stats;
            ApplyPassives();
        }

        // ── Unlock / Level Up ──────────────────────────────

        /// <summary>
        /// 패시브를 해금하거나 레벨업한다.
        /// 처음 해금 시 Lv1, 이미 있으면 +1 레벨업.
        /// </summary>
        /// <returns>성공 여부</returns>
        public bool UnlockPassive(string passiveId)
        {
            if (!CanUnlock(passiveId)) return false;

            if (!_allPassives.TryGetValue(passiveId, out var data)) return false;

            int currentLevel = GetPassiveLevel(passiveId);
            int targetLevel = currentLevel + 1;

            // 비용 소모
            if (!ConsumeResources(data, targetLevel)) return false;

            // 레벨 적용
            _unlockedPassives[passiveId] = targetLevel;

            // 패시브 보너스 재적용
            ApplyPassives();

            // 이벤트 발행
            OnPassiveUnlocked?.Invoke(passiveId, targetLevel);

            GameEventSystem.Publish(new PassiveUnlockedEvent
            {
                PassiveId = passiveId,
                NewLevel = targetLevel
            });

            Debug.Log($"[PassiveManager] 패시브 해금/강화: {data.passiveName} Lv{targetLevel}");
            return true;
        }

        /// <summary>
        /// 패시브를 해금/레벨업할 수 있는지 확인한다.
        /// </summary>
        public bool CanUnlock(string passiveId)
        {
            if (!_allPassives.TryGetValue(passiveId, out var data)) return false;

            int currentLevel = GetPassiveLevel(passiveId);

            // 최대 레벨 체크
            if (currentLevel >= data.maxLevel) return false;

            int targetLevel = currentLevel + 1;

            // 선행 패시브 체크
            if (!CheckPrerequisites(data)) return false;

            // 재료 체크
            if (!HasResources(data, targetLevel)) return false;

            return true;
        }

        /// <summary>
        /// 해금 불가 사유를 반환한다.
        /// </summary>
        public string GetUnlockFailReason(string passiveId)
        {
            if (!_allPassives.TryGetValue(passiveId, out var data))
                return "알 수 없는 패시브입니다.";

            int currentLevel = GetPassiveLevel(passiveId);

            if (currentLevel >= data.maxLevel)
                return "이미 최대 레벨입니다.";

            int targetLevel = currentLevel + 1;

            // 선행 패시브 체크
            if (data.prerequisites != null)
            {
                for (int i = 0; i < data.prerequisites.Length; i++)
                {
                    var prereq = data.prerequisites[i];
                    if (prereq == null) continue;

                    int reqLevel = data.GetPrerequisiteLevel(i);
                    int prereqLevel = GetPassiveLevel(prereq.passiveId);

                    if (prereqLevel < reqLevel)
                    {
                        return $"선행 패시브 필요: {prereq.passiveName} Lv{reqLevel} (현재 Lv{prereqLevel})";
                    }
                }
            }

            // 골드 체크
            if (_playerStats != null)
            {
                int goldNeeded = data.GetGoldCost(targetLevel);
                if (_playerStats.Gold < goldNeeded)
                    return $"골드 부족: {goldNeeded}G 필요 (보유 {_playerStats.Gold}G)";
            }

            // 재료 체크
            if (data.unlockCost != null && targetLevel - 1 < data.unlockCost.Length)
            {
                var cost = data.unlockCost[targetLevel - 1];
                if (cost.item != null)
                {
                    int have = Inventory.Instance != null ? Inventory.Instance.GetItemCount(cost.item) : 0;
                    if (have < cost.quantity)
                        return $"재료 부족: {cost.item.itemName} {cost.quantity}개 필요 (보유 {have}개)";
                }
            }

            return "해금 가능";
        }

        // ── Stat Queries ───────────────────────────────────

        /// <summary>
        /// 특정 패시브의 현재 레벨을 반환한다. 해금되지 않았으면 0.
        /// </summary>
        public int GetPassiveLevel(string passiveId)
        {
            return _unlockedPassives.TryGetValue(passiveId, out int level) ? level : 0;
        }

        /// <summary>
        /// 지정 스탯 타입에 대한 모든 패시브 보너스를 합산하여 반환한다.
        /// 퍼센트 보너스는 소수로 반환 (예: 15% = 0.15).
        /// </summary>
        public float GetTotalPassiveBonus(PassiveStatType statType)
        {
            float total = 0f;

            foreach (var kvp in _unlockedPassives)
            {
                if (!_allPassives.TryGetValue(kvp.Key, out var data)) continue;

                int level = kvp.Value;
                if (data.effectPerLevel == null) continue;

                // 모든 레벨의 누적이 아닌, 해당 레벨의 효과값을 사용
                // (effectPerLevel[level-1]이 해당 레벨의 총 효과를 나타냄)
                for (int i = 0; i < data.effectPerLevel.Length; i++)
                {
                    // 이 패시브의 레벨에 해당하는 효과만 적용
                    // effectPerLevel 인덱스가 level-1과 같은 것만 적용
                    // (하나의 패시브가 하나의 statType만 영향)
                    if (i == level - 1 && data.effectPerLevel[i].statType == statType)
                    {
                        total += data.effectPerLevel[i].value;
                    }
                }
            }

            return total;
        }

        /// <summary>
        /// 특정 스탯 타입에 대한 퍼센트 보너스만 합산하여 반환한다.
        /// </summary>
        public float GetPercentageBonus(PassiveStatType statType)
        {
            float total = 0f;

            foreach (var kvp in _unlockedPassives)
            {
                if (!_allPassives.TryGetValue(kvp.Key, out var data)) continue;

                int level = kvp.Value;
                if (data.effectPerLevel == null || level <= 0) continue;

                int idx = Mathf.Clamp(level - 1, 0, data.effectPerLevel.Length - 1);
                var effect = data.effectPerLevel[idx];

                if (effect.statType == statType && effect.isPercentage)
                    total += effect.value;
            }

            return total;
        }

        /// <summary>
        /// 특정 스탯 타입에 대한 고정값 보너스만 합산하여 반환한다.
        /// </summary>
        public float GetFlatBonus(PassiveStatType statType)
        {
            float total = 0f;

            foreach (var kvp in _unlockedPassives)
            {
                if (!_allPassives.TryGetValue(kvp.Key, out var data)) continue;

                int level = kvp.Value;
                if (data.effectPerLevel == null || level <= 0) continue;

                int idx = Mathf.Clamp(level - 1, 0, data.effectPerLevel.Length - 1);
                var effect = data.effectPerLevel[idx];

                if (effect.statType == statType && !effect.isPercentage)
                    total += effect.value;
            }

            return total;
        }

        /// <summary>
        /// 카테고리별 패시브 목록을 반환한다.
        /// </summary>
        public List<PassiveData> GetPassivesByCategory(PassiveCategory category)
        {
            return _allPassives.Values
                .Where(p => p.category == category)
                .OrderBy(p => p.passiveId)
                .ToList();
        }

        // ── Apply to PlayerStats ───────────────────────────

        /// <summary>
        /// 모든 패시브 보너스를 PlayerStats에 일괄 적용한다.
        /// Equipment 시스템과 함께 동작: 장비 보너스 위에 패시브 보너스를 추가한다.
        /// </summary>
        public void ApplyPassives()
        {
            if (_playerStats == null)
            {
                FindPlayerStats();
                if (_playerStats == null) return;
            }

            // 퍼센트 보너스 (기존 BonusXxx 필드에 가산)
            float hpBonus = GetPercentageBonus(PassiveStatType.MaxHp);
            float atkBonus = GetPercentageBonus(PassiveStatType.Attack);
            float defBonus = GetPercentageBonus(PassiveStatType.Defense);
            float spdBonus = GetPercentageBonus(PassiveStatType.Speed);
            float critRateBonus = GetPercentageBonus(PassiveStatType.CritRate);
            float critDmgBonus = GetPercentageBonus(PassiveStatType.CritDamage);

            // 고정값 보너스
            float flatHp = GetFlatBonus(PassiveStatType.MaxHp);
            float flatAtk = GetFlatBonus(PassiveStatType.Attack);
            float flatDef = GetFlatBonus(PassiveStatType.Defense);
            float flatSpd = GetFlatBonus(PassiveStatType.Speed);
            float flatCritRate = GetFlatBonus(PassiveStatType.CritRate);
            float flatCritDmg = GetFlatBonus(PassiveStatType.CritDamage);

            // Equipment의 보너스를 가져와 합산
            var equipment = _playerStats.GetComponent<Equipment>();
            BonusStats equipBonus = equipment != null ? equipment.GetTotalBonusStats() : BonusStats.Zero;

            // PlayerStats의 Bonus 필드에 장비 + 패시브 보너스를 합산하여 설정
            // 퍼센트 보너스: 기본 스탯 기준 계산은 RecalculateStats에서 처리하지 않으므로,
            // 여기서 퍼센트를 고정값으로 변환하여 적용한다.
            // (기본 MaxHp 100 기준, 25% = +25)
            _playerStats.BonusMaxHp = equipBonus.hp + Mathf.RoundToInt(flatHp + _playerStats.MaxHp * hpBonus);
            _playerStats.BonusAttack = equipBonus.atk + Mathf.RoundToInt(flatAtk + _playerStats.Attack * atkBonus);
            _playerStats.BonusDefense = equipBonus.def + Mathf.RoundToInt(flatDef + _playerStats.Defense * defBonus);
            _playerStats.BonusSpeed = equipBonus.speed + flatSpd + _playerStats.Speed * spdBonus;
            _playerStats.BonusCritRate = equipBonus.critRate + flatCritRate + critRateBonus;
            _playerStats.BonusCritDamage = equipBonus.critDamage + flatCritDmg + critDmgBonus;

            _playerStats.RecalculateStats();
        }

        // ── Prerequisites Check ────────────────────────────

        /// <summary>
        /// 선행 패시브 조건을 모두 충족하는지 확인한다.
        /// </summary>
        private bool CheckPrerequisites(PassiveData data)
        {
            if (data.prerequisites == null || data.prerequisites.Length == 0)
                return true;

            for (int i = 0; i < data.prerequisites.Length; i++)
            {
                var prereq = data.prerequisites[i];
                if (prereq == null) continue;

                int requiredLevel = data.GetPrerequisiteLevel(i);
                int currentLevel = GetPassiveLevel(prereq.passiveId);

                if (currentLevel < requiredLevel)
                    return false;
            }

            return true;
        }

        // ── Resource Check & Consume ───────────────────────

        /// <summary>
        /// 해금에 필요한 재료와 골드가 충분한지 확인한다.
        /// </summary>
        private bool HasResources(PassiveData data, int targetLevel)
        {
            // 골드 체크
            if (_playerStats != null)
            {
                int goldNeeded = data.GetGoldCost(targetLevel);
                if (_playerStats.Gold < goldNeeded) return false;
            }

            // 재료 체크
            if (data.unlockCost != null && targetLevel - 1 < data.unlockCost.Length)
            {
                var cost = data.unlockCost[targetLevel - 1];
                if (cost.item != null && cost.quantity > 0)
                {
                    if (Inventory.Instance == null) return false;
                    if (!Inventory.Instance.HasItem(cost.item, cost.quantity)) return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 재료와 골드를 소모한다.
        /// </summary>
        private bool ConsumeResources(PassiveData data, int targetLevel)
        {
            // 골드 소모
            if (_playerStats != null)
            {
                int goldNeeded = data.GetGoldCost(targetLevel);
                if (_playerStats.Gold < goldNeeded) return false;
                _playerStats.Gold -= goldNeeded;
            }

            // 재료 소모
            if (data.unlockCost != null && targetLevel - 1 < data.unlockCost.Length)
            {
                var cost = data.unlockCost[targetLevel - 1];
                if (cost.item != null && cost.quantity > 0)
                {
                    if (Inventory.Instance == null) return false;
                    int removed = Inventory.Instance.RemoveItem(cost.item, cost.quantity);
                    if (removed < cost.quantity) return false;
                }
            }

            return true;
        }

        // ── Save / Load ────────────────────────────────────

        /// <summary>
        /// 패시브 데이터를 세이브 형태로 변환한다.
        /// </summary>
        public PassiveSaveData ToSaveData()
        {
            var save = new PassiveSaveData();
            foreach (var kvp in _unlockedPassives)
            {
                save.entries.Add(new PassiveSaveEntry
                {
                    passiveId = kvp.Key,
                    level = kvp.Value
                });
            }
            return save;
        }

        /// <summary>
        /// 세이브 데이터로부터 패시브 상태를 복원한다.
        /// </summary>
        public void LoadFromSave(PassiveSaveData save)
        {
            _unlockedPassives.Clear();

            if (save == null || save.entries == null) return;

            foreach (var entry in save.entries)
            {
                if (string.IsNullOrEmpty(entry.passiveId)) continue;
                _unlockedPassives[entry.passiveId] = Mathf.Max(0, entry.level);
            }

            ApplyPassives();
        }

        /// <summary>
        /// JSON 문자열로부터 패시브 상태를 복원한다 (간편 버전).
        /// </summary>
        public void LoadFromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            var save = JsonUtility.FromJson<PassiveSaveData>(json);
            LoadFromSave(save);
        }

        /// <summary>
        /// 패시브 상태를 JSON 문자열로 직렬화한다 (간편 버전).
        /// </summary>
        public string SaveToJson()
        {
            return JsonUtility.ToJson(ToSaveData(), true);
        }

        /// <summary>
        /// 모든 패시브를 초기화한다 (리셋).
        /// </summary>
        public void ResetAllPassives()
        {
            _unlockedPassives.Clear();
            ApplyPassives();
        }

        // ── JSON Parsing Helpers ───────────────────────────

        private static PassiveCategory ParseCategory(string category)
        {
            return category?.ToLower() switch
            {
                "offense" => PassiveCategory.Offense,
                "defense" => PassiveCategory.Defense,
                "utility" => PassiveCategory.Utility,
                "farming" => PassiveCategory.Farming,
                _ => PassiveCategory.Offense
            };
        }

        private static PassiveStatType ParseStatType(string statType)
        {
            return statType switch
            {
                "MaxHp" => PassiveStatType.MaxHp,
                "Attack" => PassiveStatType.Attack,
                "Defense" => PassiveStatType.Defense,
                "Speed" => PassiveStatType.Speed,
                "CritRate" => PassiveStatType.CritRate,
                "CritDamage" => PassiveStatType.CritDamage,
                "DodgeCooldown" => PassiveStatType.DodgeCooldown,
                "SkillCooldownReduction" => PassiveStatType.SkillCooldownReduction,
                "LifeSteal" => PassiveStatType.LifeSteal,
                "DamageReduction" => PassiveStatType.DamageReduction,
                "ExpBonus" => PassiveStatType.ExpBonus,
                "GoldBonus" => PassiveStatType.GoldBonus,
                "ElementalDamageBonus" => PassiveStatType.ElementalDamageBonus,
                "ComboWindowExtend" => PassiveStatType.ComboWindowExtend,
                "StaggerDamageBonus" => PassiveStatType.StaggerDamageBonus,
                _ => PassiveStatType.Attack
            };
        }

        // ── JSON Data Classes ──────────────────────────────

        [Serializable]
        private class PassiveDefinitionRoot
        {
            public PassiveDefinitionJson[] passives;
        }

        [Serializable]
        private class PassiveDefinitionJson
        {
            public string passiveId;
            public string passiveName;
            public string description;
            public string category;
            public int maxLevel;
            public PassiveEffectJson[] effects;
            public int[] goldCost;
            public PrerequisiteJson[] prerequisites;
        }

        [Serializable]
        private class PassiveEffectJson
        {
            public string statType;
            public float value;
            public bool isPercentage;
        }

        [Serializable]
        private class PrerequisiteJson
        {
            public string passiveId;
            public int requiredLevel;
        }
    }

}
