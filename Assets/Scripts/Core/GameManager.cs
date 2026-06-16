using UnityEngine;
using UnityEngine.SceneManagement;

namespace SoulCraft.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Game State")]
        public GameState CurrentState { get; private set; } = GameState.Menu;

        public int CurrentStageIndex { get; private set; }
        public int CurrentFloor { get; private set; }

        public event System.Action<GameState> OnGameStateChanged;

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

        public void ChangeState(GameState newState)
        {
            if (CurrentState == newState) return;
            CurrentState = newState;
            OnGameStateChanged?.Invoke(newState);
        }

        public void StartStage(int stageIndex)
        {
            CurrentStageIndex = stageIndex;
            CurrentFloor = 0;
            ChangeState(GameState.Playing);
            SceneManager.LoadScene("Stage");
        }

        public void AdvanceFloor()
        {
            CurrentFloor++;
        }

        public void ReturnToHub()
        {
            ChangeState(GameState.Hub);
            SceneManager.LoadScene("Hub");
        }

        public void PauseGame()
        {
            if (CurrentState != GameState.Playing) return;
            ChangeState(GameState.Paused);
            Time.timeScale = 0f;
        }

        public void ResumeGame()
        {
            if (CurrentState != GameState.Paused) return;
            ChangeState(GameState.Playing);
            Time.timeScale = 1f;
        }

        public void GameOver()
        {
            ChangeState(GameState.GameOver);
            Time.timeScale = 0f;
        }

        public void StageClear()
        {
            ChangeState(GameState.StageClear);
        }
    }

    public enum GameState
    {
        Menu,
        Hub,
        Playing,
        Paused,
        BossFight,
        GameOver,
        StageClear
    }
}
