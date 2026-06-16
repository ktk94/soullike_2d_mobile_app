using UnityEngine;
using SoulCraft.Enemy;
using SoulCraft.Farming;

namespace SoulCraft.World
{
    /// <summary>
    /// 스테이지 하나의 정적 데이터를 정의하는 ScriptableObject.
    /// Inspector에서 에셋으로 생성하여 StageManager에 등록한다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewStageData", menuName = "SoulCraft/Stage Data")]
    public class StageData : ScriptableObject
    {
        // ── Identity ──────────────────────────────────────────

        [Header("Identity")]
        public string stageId;
        public string stageName;
        [TextArea(2, 5)]
        public string stageDescription;

        [Header("Difficulty")]
        [Range(1, 10)]
        public int difficulty = 1;
        public int recommendedLevel = 1;

        // ── Structure ─────────────────────────────────────────

        [Header("Dungeon Structure")]
        [Tooltip("스테이지 내 총 층(Floor) 수")]
        [Min(1)]
        public int floorCount = 3;

        // ── Enemies ───────────────────────────────────────────

        [Header("Enemy Configuration")]
        [Tooltip("이 스테이지에서 출현하는 일반 적 풀")]
        public EnemyData[] normalEnemyPool;

        [Tooltip("보스 ID (EnemyData.enemyId와 매칭)")]
        public string bossId;

        // ── Unlock ────────────────────────────────────────────

        [Header("Unlock Condition")]
        [Tooltip("이 스테이지를 열기 위해 클리어해야 하는 이전 스테이지 ID. 비어 있으면 처음부터 해금.")]
        public string unlockConditionStageId;

        // ── Rewards ───────────────────────────────────────────

        [Header("Rewards")]
        [Tooltip("스테이지 클리어 시 추가 보너스 보상 테이블")]
        public LootTable clearBonusLootTable;

        // ── Visuals / Audio ───────────────────────────────────

        [Header("Visuals & Audio")]
        public Sprite backgroundSprite;
        public string bgmClipName;

        // ── Helpers ───────────────────────────────────────────

        /// <summary>
        /// 해금 조건이 없으면 true. 조건이 있으면 highestClearedStageId와 비교.
        /// </summary>
        public bool IsUnlocked(string highestClearedStageId)
        {
            if (string.IsNullOrEmpty(unlockConditionStageId))
                return true;

            return unlockConditionStageId == highestClearedStageId;
        }

        /// <summary>
        /// normalEnemyPool에서 랜덤으로 EnemyData 하나를 반환한다.
        /// </summary>
        public EnemyData GetRandomNormalEnemy()
        {
            if (normalEnemyPool == null || normalEnemyPool.Length == 0)
                return null;

            return normalEnemyPool[Random.Range(0, normalEnemyPool.Length)];
        }
    }
}
