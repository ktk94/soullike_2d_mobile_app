using UnityEngine;

namespace SoulCraft.Enemy
{
    /// <summary>
    /// 적 유닛의 기본 데이터를 정의하는 ScriptableObject.
    /// Inspector에서 에셋으로 생성하여 EnemyBase에 할당한다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewEnemyData", menuName = "SoulCraft/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        [Header("Identity")]
        public string enemyId;
        public string enemyName;
        public bool isBoss;

        [Header("Stats")]
        public int maxHp = 100;
        public int attack = 10;
        public int defense = 3;
        public float speed = 2f;

        [Header("Detection")]
        public float detectionRange = 6f;
        public float attackRange = 1.5f;

        [Header("Rewards")]
        public int expReward = 20;
        public int goldReward = 10;
        public string lootTableId;

        [Header("Visuals")]
        public Sprite sprite;
    }
}
