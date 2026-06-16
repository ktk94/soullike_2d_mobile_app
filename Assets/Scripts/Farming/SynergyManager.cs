using System;
using System.Collections.Generic;
using UnityEngine;
using SoulCraft.Core;
using SoulCraft.Combat;

namespace SoulCraft.Farming
{
    // ── Manager ─────────────────────────────────────────

    /// <summary>
    /// 시너지(연계 스킬) 시스템의 핵심 매니저.
    /// 인벤토리 변경을 감지하여 시너지 조합 활성/비활성을 자동 관리한다.
    /// </summary>
    public class SynergyManager : MonoBehaviour
    {
        // ── Singleton ───────────────────────────────────
        public static SynergyManager Instance { get; private set; }

        // ── Inspector ───────────────────────────────────
        [Header("Settings")]
        [Tooltip("Resources 폴더 내 SynergyData 에셋 경로")]
        [SerializeField] private string _synergyResourcePath = "Data/Synergies";

        [Tooltip("시너지 해금 알림 지속 시간 (초)")]
        [SerializeField] private float _notificationDuration = 3f;

        // ── Data ────────────────────────────────────────

        /// <summary>모든 시너지 레시피 정의</summary>
        private readonly List<SynergyData> _allSynergies = new();

        /// <summary>현재 활성화된 시너지 ID 집합</summary>
        private readonly HashSet<string> _activeSynergyIds = new();

        /// <summary>현재 활성 시너지 데이터 목록</summary>
        private readonly List<SynergyData> _activeSynergies = new();

        /// <summary>플레이어가 한 번이라도 발견(해금)한 시너지 ID 집합</summary>
        private readonly HashSet<string> _discoveredSynergyIds = new();

        // ── Events ──────────────────────────────────────

        /// <summary>시너지가 활성화될 때 발생 (SynergyData)</summary>
        public event Action<SynergyData> OnSynergyActivated;

        /// <summary>시너지가 비활성화될 때 발생 (SynergyData)</summary>
        public event Action<SynergyData> OnSynergyDeactivated;

        /// <summary>시너지 목록이 변경될 때 발생</summary>
        public event Action OnSynergiesChanged;

        // ── Properties ──────────────────────────────────

        /// <summary>현재 활성 시너지 목록 (읽기 전용)</summary>
        public IReadOnlyList<SynergyData> ActiveSynergies => _activeSynergies;

        /// <summary>전체 시너지 레시피 목록 (읽기 전용)</summary>
        public IReadOnlyList<SynergyData> AllSynergies => _allSynergies;

        /// <summary>알림 지속 시간</summary>
        public float NotificationDuration => _notificationDuration;

        // ── Lifecycle ───────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            LoadSynergyDefinitions();
        }

        void OnEnable()
        {
            // 인벤토리 변경 이벤트 구독
            if (Inventory.Instance != null)
            {
                Inventory.Instance.OnInventoryChanged += OnInventoryChanged;
            }
        }

        void Start()
        {
            // OnEnable에서 아직 Inventory가 초기화되지 않았을 수 있으므로 Start에서 재구독
            if (Inventory.Instance != null)
            {
                Inventory.Instance.OnInventoryChanged -= OnInventoryChanged;
                Inventory.Instance.OnInventoryChanged += OnInventoryChanged;
            }

            // 초기 시너지 검사
            CheckSynergies();
        }

        void OnDisable()
        {
            if (Inventory.Instance != null)
            {
                Inventory.Instance.OnInventoryChanged -= OnInventoryChanged;
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Data Loading ────────────────────────────────

        /// <summary>
        /// Resources 폴더에서 모든 SynergyData 에셋을 로드한다.
        /// </summary>
        private void LoadSynergyDefinitions()
        {
            _allSynergies.Clear();

            var loaded = Resources.LoadAll<SynergyData>(_synergyResourcePath);
            if (loaded != null && loaded.Length > 0)
            {
                _allSynergies.AddRange(loaded);
                Debug.Log($"[SynergyManager] {loaded.Length}개의 시너지 정의 로드 완료.");
            }
            else
            {
                Debug.LogWarning(
                    $"[SynergyManager] Resources/{_synergyResourcePath} 경로에서 " +
                    "SynergyData를 찾을 수 없습니다.");
            }
        }

        /// <summary>
        /// 런타임에 시너지 데이터를 외부에서 추가한다 (JSON 로더 등).
        /// </summary>
        public void RegisterSynergy(SynergyData data)
        {
            if (data == null) return;

            // 중복 방지
            foreach (var s in _allSynergies)
            {
                if (s.synergyId == data.synergyId) return;
            }

            _allSynergies.Add(data);
        }

        // ── Core: Synergy Check ─────────────────────────

        /// <summary>
        /// 인벤토리의 현재 아이템을 검사하여 활성화 가능한 시너지를 판별한다.
        /// 새로 활성화되거나 비활성화된 시너지가 있으면 이벤트를 발행한다.
        /// </summary>
        public void CheckSynergies()
        {
            var inventory = Inventory.Instance;
            if (inventory == null) return;

            bool changed = false;

            foreach (var synergy in _allSynergies)
            {
                if (synergy == null) continue;

                bool meetsRequirements = synergy.CheckRequirements(inventory);
                bool isCurrentlyActive = _activeSynergyIds.Contains(synergy.synergyId);

                if (meetsRequirements && !isCurrentlyActive)
                {
                    // 새로 활성화
                    ActivateSynergy(synergy);
                    changed = true;
                }
                else if (!meetsRequirements && isCurrentlyActive)
                {
                    // 비활성화
                    DeactivateSynergy(synergy);
                    changed = true;
                }
            }

            if (changed)
            {
                OnSynergiesChanged?.Invoke();
            }
        }

        // ── Activate / Deactivate ───────────────────────

        private void ActivateSynergy(SynergyData synergy)
        {
            _activeSynergyIds.Add(synergy.synergyId);
            _activeSynergies.Add(synergy);

            // 발견 기록 추가
            _discoveredSynergyIds.Add(synergy.synergyId);

            // 연계 스킬을 플레이어 스킬 목록에 자동 추가는 하지 않고,
            // 사용 가능 목록에만 추가한다. 장착은 플레이어가 직접 한다.

            // 이벤트 발행 (로컬)
            OnSynergyActivated?.Invoke(synergy);

            // 이벤트 발행 (글로벌)
            GameEventSystem.Publish(new SynergyActivatedEvent
            {
                SynergyId = synergy.synergyId,
                SynergyName = synergy.synergyName,
                UnlockMessage = synergy.unlockMessage,
                SkillId = synergy.resultSkill != null ? synergy.resultSkill.skillId : ""
            });

            Debug.Log($"[SynergyManager] 시너지 활성화: {synergy.synergyName}");
        }

        private void DeactivateSynergy(SynergyData synergy)
        {
            _activeSynergyIds.Remove(synergy.synergyId);
            _activeSynergies.Remove(synergy);

            // 장착된 스킬 슬롯에서 해당 연계 스킬을 자동 제거
            RemoveSynergySkillFromSlots(synergy);

            // 이벤트 발행 (로컬)
            OnSynergyDeactivated?.Invoke(synergy);

            // 이벤트 발행 (글로벌)
            GameEventSystem.Publish(new SynergyDeactivatedEvent
            {
                SynergyId = synergy.synergyId,
                SynergyName = synergy.synergyName,
                SkillId = synergy.resultSkill != null ? synergy.resultSkill.skillId : ""
            });

            Debug.Log($"[SynergyManager] 시너지 비활성화: {synergy.synergyName}");
        }

        /// <summary>
        /// 비활성화된 시너지의 연계 스킬을 장착 슬롯에서 제거한다.
        /// </summary>
        private void RemoveSynergySkillFromSlots(SynergyData synergy)
        {
            if (synergy.resultSkill == null) return;

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            var skillManager = player.GetComponent<SkillManager>();
            if (skillManager == null) return;

            var equipped = skillManager.EquippedSkills;
            for (int i = 0; i < equipped.Length; i++)
            {
                if (equipped[i] != null && equipped[i].skillId == synergy.resultSkill.skillId)
                {
                    skillManager.UnequipSkill(i);
                }
            }
        }

        // ── Public Queries ──────────────────────────────

        /// <summary>
        /// 현재 사용 가능한 연계 스킬 목록을 반환한다.
        /// </summary>
        public List<SkillData> GetActiveSynergySkills()
        {
            var skills = new List<SkillData>();

            foreach (var synergy in _activeSynergies)
            {
                if (synergy.resultSkill != null)
                    skills.Add(synergy.resultSkill);
            }

            return skills;
        }

        /// <summary>
        /// 특정 시너지가 현재 활성 상태인지 확인한다.
        /// </summary>
        public bool IsSynergyActive(string synergyId)
        {
            return _activeSynergyIds.Contains(synergyId);
        }

        /// <summary>
        /// 특정 시너지가 발견된 적이 있는지 확인한다.
        /// </summary>
        public bool IsSynergyDiscovered(string synergyId)
        {
            return _discoveredSynergyIds.Contains(synergyId);
        }

        /// <summary>
        /// 특정 시너지의 재료 충족 진행도를 반환한다.
        /// 반환값: (충족된 재료 종류 수, 전체 재료 종류 수)
        /// </summary>
        public (int fulfilled, int total) GetSynergyProgress(SynergyData synergy)
        {
            if (synergy == null || synergy.requiredItems == null)
                return (0, 0);

            var inventory = Inventory.Instance;
            if (inventory == null)
                return (0, synergy.requiredItems.Length);

            int fulfilled = 0;
            int total = synergy.requiredItems.Length;

            foreach (var ingredient in synergy.requiredItems)
            {
                if (ingredient.item == null) continue;

                if (inventory.HasItem(ingredient.item, ingredient.requiredQuantity))
                    fulfilled++;
            }

            return (fulfilled, total);
        }

        /// <summary>
        /// 특정 스킬이 연계 스킬인지 확인한다.
        /// </summary>
        public bool IsSynergySkill(SkillData skill)
        {
            if (skill == null) return false;

            foreach (var synergy in _allSynergies)
            {
                if (synergy.resultSkill != null &&
                    synergy.resultSkill.skillId == skill.skillId)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 특정 스킬에 해당하는 시너지 데이터를 반환한다.
        /// </summary>
        public SynergyData GetSynergyBySkill(SkillData skill)
        {
            if (skill == null) return null;

            foreach (var synergy in _allSynergies)
            {
                if (synergy.resultSkill != null &&
                    synergy.resultSkill.skillId == skill.skillId)
                    return synergy;
            }

            return null;
        }

        /// <summary>
        /// 시너지 ID로 시너지 데이터를 검색한다.
        /// </summary>
        public SynergyData GetSynergyById(string synergyId)
        {
            foreach (var synergy in _allSynergies)
            {
                if (synergy.synergyId == synergyId)
                    return synergy;
            }

            return null;
        }

        /// <summary>
        /// 특정 아이템이 재료로 사용되는 모든 시너지를 반환한다.
        /// </summary>
        public List<SynergyData> GetSynergiesUsingItem(ItemData item)
        {
            var result = new List<SynergyData>();

            foreach (var synergy in _allSynergies)
            {
                if (synergy.ContainsIngredient(item))
                    result.Add(synergy);
            }

            return result;
        }

        /// <summary>
        /// 타입별 활성 시너지 목록을 반환한다.
        /// </summary>
        public List<SynergyData> GetActiveSynergiesByType(SynergyType type)
        {
            var result = new List<SynergyData>();

            foreach (var synergy in _activeSynergies)
            {
                if (synergy.synergyType == type)
                    result.Add(synergy);
            }

            return result;
        }

        /// <summary>
        /// 발견 기록을 외부에서 복원한다 (세이브 로드 등).
        /// </summary>
        public void RestoreDiscoveredSynergies(IEnumerable<string> synergyIds)
        {
            if (synergyIds == null) return;

            foreach (var id in synergyIds)
            {
                _discoveredSynergyIds.Add(id);
            }
        }

        /// <summary>
        /// 현재 발견된 시너지 ID 목록을 반환한다 (세이브용).
        /// </summary>
        public List<string> GetDiscoveredSynergyIds()
        {
            return new List<string>(_discoveredSynergyIds);
        }

        // ── Inventory Callback ──────────────────────────

        private void OnInventoryChanged()
        {
            CheckSynergies();
        }
    }
}
