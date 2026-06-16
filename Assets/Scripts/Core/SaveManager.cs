using UnityEngine;
using System.IO;

namespace SoulCraft.Core
{
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        private string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

        void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void Save(SaveData data)
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SavePath, json);
        }

        public SaveData Load()
        {
            if (!File.Exists(SavePath))
                return new SaveData();

            string json = File.ReadAllText(SavePath);
            return JsonUtility.FromJson<SaveData>(json);
        }

        public bool HasSave() => File.Exists(SavePath);

        public void DeleteSave()
        {
            if (File.Exists(SavePath))
                File.Delete(SavePath);
        }
    }

    [System.Serializable]
    public class SaveData
    {
        public int playerLevel = 1;
        public int playerExp;
        public int gold;
        public int highestStageCleared;
        public int totalPlayTime;
        public int totalBossKills;
        public string[] unlockedSkillIds = new string[0];
        public string[] inventoryItemJson = new string[0];
        public string[] equippedItemJson = new string[0];
        public PlayerStatsSave stats = new PlayerStatsSave();
    }

    [System.Serializable]
    public class PlayerStatsSave
    {
        public int maxHp = 100;
        public int attack = 10;
        public int defense = 5;
        public float speed = 5f;
        public float critRate = 0.05f;
        public float critDamage = 1.5f;
    }
}
