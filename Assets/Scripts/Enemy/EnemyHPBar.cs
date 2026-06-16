using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SoulCraft.Enemy
{
    /// <summary>
    /// 각 적 머리 위에 표시되는 월드 스페이스 HP 바.
    /// 코드로 Canvas + Image(배경/HP바/딜레이바)를 직접 생성한다.
    /// 보스는 별도 BossHPBar UI를 사용하므로 제외.
    /// </summary>
    public class EnemyHPBar : MonoBehaviour
    {
        // ── 설정 ──────────────────────────────────────────────
        private const float BarOffsetY = 0.8f;
        private const float BarWidth = 0.6f;
        private const float BarHeight = 0.08f;
        private const float DelaySpeed = 2f;
        private const float ShowDuration = 3f;

        // ── 색상 ──────────────────────────────────────────────
        private static readonly Color ColBackground = new(0f, 0f, 0f, 0.8f);
        private static readonly Color ColHpHigh = new(0.2f, 0.85f, 0.3f, 1f);
        private static readonly Color ColHpLow = new(0.9f, 0.15f, 0.15f, 1f);
        private static readonly Color ColDelay = new(1f, 0.6f, 0.15f, 0.9f);

        // ── 런타임 참조 ────────────────────────────────────────
        private EnemyBase _enemyBase;
        private Canvas _canvas;
        private RectTransform _hpFillRect;
        private RectTransform _delayFillRect;
        private Image _hpFillImage;
        private Image _delayFillImage;
        private GameObject _barRoot;

        private float _displayedHpRatio = 1f;
        private float _delayHpRatio = 1f;
        private float _showTimer;
        private bool _isBoss;
        private int _lastHp;

        // ── Lifecycle ─────────────────────────────────────────

        void Awake()
        {
            _enemyBase = GetComponent<EnemyBase>();
        }

        void Start()
        {
            if (_enemyBase == null)
            {
                enabled = false;
                return;
            }

            // 보스인 경우 비활성화
            if (_enemyBase.Data != null && _enemyBase.Data.isBoss)
            {
                _isBoss = true;
                enabled = false;
                return;
            }

            _lastHp = _enemyBase.CurrentHp;
            CreateHPBarUI();
            HideBar();
        }

        void LateUpdate()
        {
            if (_isBoss || _enemyBase == null || _barRoot == null) return;

            // HP 변경 감지
            int currentHp = _enemyBase.CurrentHp;
            int maxHp = _enemyBase.MaxHp;

            if (currentHp != _lastHp)
            {
                _lastHp = currentHp;
                _showTimer = ShowDuration;

                float targetRatio = maxHp > 0 ? (float)currentHp / maxHp : 0f;
                _displayedHpRatio = targetRatio;
                UpdateHpFill(_displayedHpRatio);

                if (currentHp < maxHp)
                    ShowBar();
            }

            // 딜레이 바 부드러운 감소
            if (_delayHpRatio > _displayedHpRatio)
            {
                _delayHpRatio -= DelaySpeed * Time.deltaTime;
                if (_delayHpRatio < _displayedHpRatio)
                    _delayHpRatio = _displayedHpRatio;
                UpdateDelayFill(_delayHpRatio);
            }

            // 풀HP이거나 타이머 만료 시 숨김
            if (_showTimer > 0f)
            {
                _showTimer -= Time.deltaTime;
                if (_showTimer <= 0f && _displayedHpRatio >= 1f)
                    HideBar();
            }

            // 사망 시 숨김
            if (_enemyBase.IsDead)
                HideBar();

            // 위치 갱신 (적 머리 위)
            UpdatePosition();
        }

        void OnDestroy()
        {
            if (_barRoot != null)
                Destroy(_barRoot);
        }

        // ── UI 생성 ──────────────────────────────────────────

        private void CreateHPBarUI()
        {
            // Canvas 루트
            _barRoot = new GameObject("EnemyHPBar_Canvas");
            _canvas = _barRoot.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 50;

            var canvasRect = _barRoot.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(BarWidth, BarHeight);
            canvasRect.localScale = new Vector3(1f, 1f, 1f);

            // CanvasScaler 불필요 (월드 스페이스)

            // 배경 (검정)
            var bgGo = new GameObject("BG", typeof(RectTransform));
            bgGo.transform.SetParent(_barRoot.transform, false);
            var bgRect = bgGo.GetComponent<RectTransform>();
            StretchFull(bgRect);
            var bgImage = bgGo.AddComponent<Image>();
            bgImage.color = ColBackground;
            bgImage.raycastTarget = false;

            // 딜레이 바 (주황) - 뒤에 배치
            var delayGo = new GameObject("DelayFill", typeof(RectTransform));
            delayGo.transform.SetParent(_barRoot.transform, false);
            _delayFillRect = delayGo.GetComponent<RectTransform>();
            SetFillAnchors(_delayFillRect, 1f);
            _delayFillImage = delayGo.AddComponent<Image>();
            _delayFillImage.color = ColDelay;
            _delayFillImage.raycastTarget = false;

            // HP 바 (빨강/녹색) - 앞에 배치
            var hpGo = new GameObject("HpFill", typeof(RectTransform));
            hpGo.transform.SetParent(_barRoot.transform, false);
            _hpFillRect = hpGo.GetComponent<RectTransform>();
            SetFillAnchors(_hpFillRect, 1f);
            _hpFillImage = hpGo.AddComponent<Image>();
            _hpFillImage.color = ColHpHigh;
            _hpFillImage.raycastTarget = false;

            UpdatePosition();
        }

        // ── UI 갱신 ──────────────────────────────────────────

        private void UpdateHpFill(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            SetFillAnchors(_hpFillRect, ratio);

            // 색상 보간: 녹색(풀HP) → 빨강(죽기 직전)
            _hpFillImage.color = Color.Lerp(ColHpLow, ColHpHigh, ratio);
        }

        private void UpdateDelayFill(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            SetFillAnchors(_delayFillRect, ratio);
        }

        private void SetFillAnchors(RectTransform rt, float fillRatio)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(fillRatio, 1f);
            rt.offsetMin = new Vector2(1f, 1f);
            rt.offsetMax = new Vector2(-1f, -1f);
        }

        private void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void UpdatePosition()
        {
            if (_barRoot == null || _enemyBase == null) return;
            _barRoot.transform.position = transform.position + new Vector3(0f, BarOffsetY, 0f);
            // 항상 카메라를 향하도록 (월드 스페이스이므로 회전 고정)
            _barRoot.transform.rotation = Quaternion.identity;
        }

        // ── 표시/숨김 ────────────────────────────────────────

        private void ShowBar()
        {
            if (_barRoot != null)
                _barRoot.SetActive(true);
        }

        private void HideBar()
        {
            if (_barRoot != null)
                _barRoot.SetActive(false);
        }

        // ── Public ────────────────────────────────────────────

        /// <summary>
        /// 외부에서 HP 변경을 즉시 반영시킬 때 호출.
        /// </summary>
        public void ForceUpdate()
        {
            if (_enemyBase == null) return;

            int currentHp = _enemyBase.CurrentHp;
            int maxHp = _enemyBase.MaxHp;
            float ratio = maxHp > 0 ? (float)currentHp / maxHp : 0f;

            _displayedHpRatio = ratio;
            _delayHpRatio = ratio;
            _lastHp = currentHp;

            UpdateHpFill(ratio);
            UpdateDelayFill(ratio);

            if (currentHp < maxHp)
            {
                _showTimer = ShowDuration;
                ShowBar();
            }
        }

        /// <summary>
        /// 오브젝트 풀 재활용 시 리셋.
        /// </summary>
        void OnEnable()
        {
            _displayedHpRatio = 1f;
            _delayHpRatio = 1f;
            _showTimer = 0f;

            if (_enemyBase != null)
                _lastHp = _enemyBase.CurrentHp;

            if (_barRoot != null)
            {
                UpdateHpFill(1f);
                UpdateDelayFill(1f);
                HideBar();
            }
        }
    }
}
