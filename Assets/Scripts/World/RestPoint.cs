using System;
using UnityEngine;
using SoulCraft.Core;
using SoulCraft.Player;

namespace SoulCraft.World
{
    /// <summary>
    /// 소울라이크의 화톳불/체크포인트 역할.
    /// HP 전체 회복, 스킬/장비 변경, 자동 저장 기능을 제공한다.
    /// </summary>
    public class RestPoint : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────

        [Header("Rest Point Settings")]
        [SerializeField] private string restPointId;
        [SerializeField] private string restPointName = "화톳불";

        [Header("Visuals")]
        [SerializeField] private SpriteRenderer flameRenderer;
        [SerializeField] private Color activeColor = new Color(1f, 0.6f, 0.1f, 1f);
        [SerializeField] private Color inactiveColor = new Color(0.3f, 0.2f, 0.1f, 1f);

        [Header("Interaction")]
        [SerializeField] private float interactionRange = 1.5f;
        [SerializeField] private KeyCode interactKey = KeyCode.E;
        [SerializeField] private GameObject interactPromptUI;

        // ── Runtime ──────────────────────────────────────────

        public bool IsActivated { get; private set; }
        public bool IsResting { get; private set; }

        public event Action OnRestPointActivated;
        public event Action OnRestStarted;
        public event Action OnRestFinished;
        public event Action OnSkillChangeRequested;
        public event Action OnEquipmentChangeRequested;

        private PlayerStats playerStats;
        private bool playerInRange;

        // ── Lifecycle ────────────────────────────────────────

        private void Update()
        {
            if (!playerInRange || IsResting) return;

            if (Input.GetKeyDown(interactKey))
            {
                if (!IsActivated)
                    Activate();

                StartRest();
            }
        }

        // ── Public API ───────────────────────────────────────

        /// <summary>
        /// 화톳불을 활성화한다 (최초 1회).
        /// </summary>
        public void Activate()
        {
            if (IsActivated) return;

            IsActivated = true;
            UpdateVisuals();
            OnRestPointActivated?.Invoke();

            GameEventSystem.Publish(new RestPointActivatedEvent
            {
                RestPointId = restPointId
            });
        }

        /// <summary>
        /// 휴식을 시작한다. HP 전체 회복 + 자동 저장.
        /// </summary>
        public void StartRest()
        {
            if (IsResting) return;

            IsResting = true;
            OnRestStarted?.Invoke();

            // HP 전체 회복
            if (playerStats != null)
            {
                playerStats.FullHeal();
            }

            // 진행 상황 자동 저장
            AutoSave();

            // 스킬/장비 변경 UI 열기 가능 상태
            ShowRestMenu();
        }

        /// <summary>
        /// 휴식을 종료하고 탐험을 재개한다.
        /// </summary>
        public void EndRest()
        {
            if (!IsResting) return;

            IsResting = false;
            OnRestFinished?.Invoke();
        }

        /// <summary>
        /// 스킬 변경 UI를 요청한다.
        /// </summary>
        public void RequestSkillChange()
        {
            if (!IsResting) return;
            OnSkillChangeRequested?.Invoke();
        }

        /// <summary>
        /// 장비 변경 UI를 요청한다.
        /// </summary>
        public void RequestEquipmentChange()
        {
            if (!IsResting) return;
            OnEquipmentChangeRequested?.Invoke();
        }

        // ── Internal ─────────────────────────────────────────

        /// <summary>
        /// 현재 진행 상황을 SaveManager를 통해 저장한다.
        /// </summary>
        private void AutoSave()
        {
            if (SaveManager.Instance == null || playerStats == null) return;

            SaveData saveData = SaveManager.Instance.Load();

            saveData.playerLevel = playerStats.Level;
            saveData.playerExp = playerStats.Exp;
            saveData.gold = playerStats.Gold;

            if (GameManager.Instance != null)
            {
                saveData.highestStageCleared = Mathf.Max(
                    saveData.highestStageCleared,
                    GameManager.Instance.CurrentStageIndex
                );
            }

            saveData.stats = playerStats.ToSaveData();
            SaveManager.Instance.Save(saveData);

            Debug.Log($"[RestPoint] 자동 저장 완료 - Lv.{playerStats.Level}, Gold: {playerStats.Gold}");
        }

        /// <summary>
        /// 휴식 메뉴 표시 (UI 시스템과 연동).
        /// </summary>
        private void ShowRestMenu()
        {
            // UI 시스템이 구현되면 여기서 휴식 메뉴 UI를 활성화한다.
            // 현재는 이벤트로 외부에 알림.
            Debug.Log("[RestPoint] 휴식 메뉴 활성화 - 스킬/장비 변경 가능");
        }

        /// <summary>
        /// 화톳불 시각 효과를 갱신한다.
        /// </summary>
        private void UpdateVisuals()
        {
            if (flameRenderer != null)
            {
                flameRenderer.color = IsActivated ? activeColor : inactiveColor;
            }
        }

        // ── Trigger Detection ────────────────────────────────

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            playerInRange = true;
            playerStats = other.GetComponent<PlayerStats>();

            if (interactPromptUI != null)
                interactPromptUI.SetActive(true);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            playerInRange = false;
            playerStats = null;

            if (interactPromptUI != null)
                interactPromptUI.SetActive(false);

            // 범위를 벗어나면 자동으로 휴식 종료
            if (IsResting)
                EndRest();
        }

        // ── Gizmos ───────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = IsActivated ? Color.yellow : Color.gray;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }
    }

    // ── Events ───────────────────────────────────────────────

    public struct RestPointActivatedEvent
    {
        public string RestPointId;
    }
}
