using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SoulCraft.Core;
using SoulCraft.Farming;

namespace SoulCraft.UI
{
    // ================================================================
    //  StageResultData  (결과 화면에 표시할 데이터)
    // ================================================================

    /// <summary>
    /// 스테이지 결과 화면에 표시할 통계 및 보상 데이터.
    /// </summary>
    [System.Serializable]
    public class StageResultData
    {
        public bool IsVictory;

        // 보상
        public int GoldEarned;
        public int ExpEarned;
        public List<RewardItemEntry> ItemsEarned = new();

        // 통계
        public float ClearTime;
        public int TotalDamageDealt;
        public int TotalDamageTaken;
        public int EnemiesDefeated;
        public int SkillsUsed;
        public int MaxComboHits;
        public string BestComboName;
    }

    /// <summary>
    /// 보상 아이템 하나의 데이터.
    /// </summary>
    [System.Serializable]
    public struct RewardItemEntry
    {
        public ItemData ItemData;
        public int Quantity;
    }

    // ================================================================
    //  RewardItemSlotUI  (보상 아이템 슬롯)
    // ================================================================

    /// <summary>
    /// 결과 화면에서 획득 아이템을 표시하는 슬롯 UI.
    /// </summary>
    public class RewardItemSlotUI : MonoBehaviour
    {
        [SerializeField] private Image _iconImage;
        [SerializeField] private TMP_Text _quantityText;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private Image _borderImage;

        public void SetReward(ItemData data, int quantity)
        {
            if (data == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            if (_iconImage != null)
            {
                _iconImage.sprite = data.icon;
                _iconImage.enabled = data.icon != null;
            }

            if (_quantityText != null)
                _quantityText.text = quantity > 1 ? $"x{quantity}" : "";

            if (_nameText != null)
                _nameText.text = data.itemName;

            if (_borderImage != null)
            {
                _borderImage.color = data.rarity switch
                {
                    Rarity.Common    => new Color(0.7f, 0.7f, 0.7f),
                    Rarity.Uncommon  => new Color(0.3f, 0.85f, 0.3f),
                    Rarity.Rare      => new Color(0.3f, 0.5f, 1f),
                    Rarity.Epic      => new Color(0.7f, 0.3f, 0.95f),
                    Rarity.Legendary => new Color(1f, 0.7f, 0.1f),
                    _ => Color.gray,
                };
            }
        }
    }

    // ================================================================
    //  ResultScreenUI  (스테이지 결과 화면)
    // ================================================================

    /// <summary>
    /// 스테이지 클리어 / 게임오버 결과 화면.
    /// 획득 아이템, 골드, 경험치, 전투 통계를 표시한다.
    /// </summary>
    public class ResultScreenUI : MonoBehaviour
    {
        // ── Inspector: Header ────────────────────────────────
        [Header("Header")]
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private Color _victoryColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] private Color _defeatColor = new Color(0.85f, 0.15f, 0.15f);

        // ── Inspector: Rewards ───────────────────────────────
        [Header("Rewards")]
        [SerializeField] private TMP_Text _goldText;
        [SerializeField] private TMP_Text _expText;
        [SerializeField] private Transform _rewardItemsParent;
        [SerializeField] private GameObject _rewardItemSlotPrefab;

        // ── Inspector: Statistics ────────────────────────────
        [Header("Statistics")]
        [SerializeField] private TMP_Text _clearTimeText;
        [SerializeField] private TMP_Text _damageDealtText;
        [SerializeField] private TMP_Text _damageTakenText;
        [SerializeField] private TMP_Text _enemiesDefeatedText;
        [SerializeField] private TMP_Text _skillsUsedText;
        [SerializeField] private TMP_Text _maxComboText;

        // ── Inspector: Buttons ───────────────────────────────
        [Header("Buttons")]
        [SerializeField] private Button _returnToHubButton;
        [SerializeField] private Button _retryButton;

        // ── Inspector: Animation ─────────────────────────────
        [Header("Animation")]
        [SerializeField] private float _statRevealDelay = 0.15f;
        [SerializeField] private float _itemRevealDelay = 0.1f;
        [SerializeField] private CanvasGroup _canvasGroup;

        // ── Runtime ──────────────────────────────────────────
        private StageResultData _resultData;
        private readonly List<GameObject> _spawnedItemSlots = new();

        // ============================================================
        //  Unity Lifecycle
        // ============================================================

        void Awake()
        {
            SetupButtons();
            gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (_returnToHubButton != null) _returnToHubButton.onClick.RemoveAllListeners();
            if (_retryButton != null) _retryButton.onClick.RemoveAllListeners();
        }

        // ============================================================
        //  Public API
        // ============================================================

        /// <summary>
        /// 결과 화면을 표시한다.
        /// </summary>
        public void Show(StageResultData data)
        {
            if (data == null) return;

            _resultData = data;
            gameObject.SetActive(true);

            // 일시정지 해제 (GameOver에서 timeScale=0일 수 있음)
            // 결과 화면은 unscaledTime으로 동작해야 함

            SetupHeader();
            SetupRewards();
            SetupStatistics();
            SetupButtonVisibility();

            StartCoroutine(RevealAnimation());
        }

        /// <summary>
        /// 결과 화면을 숨긴다.
        /// </summary>
        public void Hide()
        {
            ClearSpawnedSlots();
            gameObject.SetActive(false);
        }

        // ============================================================
        //  Setup
        // ============================================================

        private void SetupHeader()
        {
            if (_titleText == null) return;

            if (_resultData.IsVictory)
            {
                _titleText.text = "STAGE CLEAR";
                _titleText.color = _victoryColor;
            }
            else
            {
                _titleText.text = "GAME OVER";
                _titleText.color = _defeatColor;
            }
        }

        private void SetupRewards()
        {
            // 골드
            if (_goldText != null)
                _goldText.text = $"{_resultData.GoldEarned:N0} G";

            // 경험치
            if (_expText != null)
                _expText.text = $"{_resultData.ExpEarned:N0} EXP";

            // 아이템 목록
            ClearSpawnedSlots();

            if (_rewardItemsParent != null && _rewardItemSlotPrefab != null)
            {
                foreach (var entry in _resultData.ItemsEarned)
                {
                    GameObject slotObj = Instantiate(_rewardItemSlotPrefab, _rewardItemsParent);
                    var slotUI = slotObj.GetComponent<RewardItemSlotUI>();
                    if (slotUI != null)
                        slotUI.SetReward(entry.ItemData, entry.Quantity);

                    // 초기에 숨김 (애니메이션에서 순차 표시)
                    slotObj.SetActive(false);
                    _spawnedItemSlots.Add(slotObj);
                }
            }
        }

        private void SetupStatistics()
        {
            // 클리어 타임
            if (_clearTimeText != null)
            {
                int minutes = Mathf.FloorToInt(_resultData.ClearTime / 60f);
                int seconds = Mathf.FloorToInt(_resultData.ClearTime % 60f);
                int ms = Mathf.FloorToInt((_resultData.ClearTime * 100f) % 100f);
                _clearTimeText.text = $"{minutes:D2}:{seconds:D2}.{ms:D2}";
            }

            // 기타 통계
            if (_damageDealtText != null)
                _damageDealtText.text = _resultData.TotalDamageDealt.ToString("N0");

            if (_damageTakenText != null)
                _damageTakenText.text = _resultData.TotalDamageTaken.ToString("N0");

            if (_enemiesDefeatedText != null)
                _enemiesDefeatedText.text = _resultData.EnemiesDefeated.ToString();

            if (_skillsUsedText != null)
                _skillsUsedText.text = _resultData.SkillsUsed.ToString();

            if (_maxComboText != null)
            {
                string comboText = _resultData.MaxComboHits.ToString();
                if (!string.IsNullOrEmpty(_resultData.BestComboName))
                    comboText += $" ({_resultData.BestComboName})";
                _maxComboText.text = comboText;
            }
        }

        private void SetupButtonVisibility()
        {
            // 재시도 버튼은 게임오버 시에만 표시
            if (_retryButton != null)
                _retryButton.gameObject.SetActive(!_resultData.IsVictory);
        }

        // ============================================================
        //  Buttons
        // ============================================================

        private void SetupButtons()
        {
            if (_returnToHubButton != null)
                _returnToHubButton.onClick.AddListener(OnReturnToHubClicked);

            if (_retryButton != null)
                _retryButton.onClick.AddListener(OnRetryClicked);
        }

        private void OnReturnToHubClicked()
        {
            Time.timeScale = 1f;
            Hide();

            if (ScreenTransition.Instance != null)
            {
                ScreenTransition.Instance.FadeOutIn(
                    onMiddle: () =>
                    {
                        if (GameManager.Instance != null)
                            GameManager.Instance.ReturnToHub();
                    }
                );
            }
            else
            {
                if (GameManager.Instance != null)
                    GameManager.Instance.ReturnToHub();
            }
        }

        private void OnRetryClicked()
        {
            Time.timeScale = 1f;
            Hide();

            if (ScreenTransition.Instance != null)
            {
                ScreenTransition.Instance.FadeOutIn(
                    onMiddle: () =>
                    {
                        if (GameManager.Instance != null)
                            GameManager.Instance.StartStage(GameManager.Instance.CurrentStageIndex);
                    }
                );
            }
            else
            {
                if (GameManager.Instance != null)
                    GameManager.Instance.StartStage(GameManager.Instance.CurrentStageIndex);
            }
        }

        // ============================================================
        //  Reveal Animation
        // ============================================================

        private IEnumerator RevealAnimation()
        {
            // 전체 패널 페이드 인
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                float fadeIn = 0.3f;
                float elapsed = 0f;
                while (elapsed < fadeIn)
                {
                    elapsed += Time.unscaledDeltaTime;
                    _canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeIn);
                    yield return null;
                }
                _canvasGroup.alpha = 1f;
            }

            // 통계 항목 순차 표시는 이미 세팅됨.
            // 아이템 순차 등장
            yield return new WaitForSecondsRealtime(_statRevealDelay * 6f);

            foreach (var slot in _spawnedItemSlots)
            {
                if (slot != null)
                {
                    slot.SetActive(true);
                    yield return new WaitForSecondsRealtime(_itemRevealDelay);
                }
            }
        }

        // ============================================================
        //  Cleanup
        // ============================================================

        private void ClearSpawnedSlots()
        {
            foreach (var slot in _spawnedItemSlots)
            {
                if (slot != null)
                    Destroy(slot);
            }
            _spawnedItemSlots.Clear();
        }
    }
}
