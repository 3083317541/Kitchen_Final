using Kitchen.Data;
using Kitchen.Orders;
using Kitchen.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kitchen.Level
{
    /// <summary>
    /// Owns the campaign flow around a level: which level the kitchen builds, what happens when it is
    /// cleared, and how the campaign moves on.
    ///
    /// It is a thin coordinator, not a second entry point: <see cref="GameRuntime"/> still builds and
    /// drives the kitchen. This component only listens for the level-clear signal and decides whether
    /// to show the reward or reload.
    /// </summary>
    [DisallowMultipleComponent]
    public class LevelManager : MonoBehaviour
    {
        /// <summary>Scene loaded after a cleared level.</summary>
        public const string KitchenSceneName = "Kitchen";

        /// <summary>Scene loaded for the start screen.</summary>
        public const string MenuSceneName = "MainMenu";

        [Header("Levels")]
        [Tooltip("Fallback level used when the campaign level cannot be loaded from Resources.")]
        [SerializeField] private LevelConfig activeLevel;

        [Tooltip("All campaign levels, in order. Index 0 is level 1.")]
        [SerializeField] private LevelConfig[] availableLevels = new LevelConfig[0];

        [Tooltip("Load the level selected by CampaignProgress instead of activeLevel.")]
        [SerializeField] private bool useCampaignProgress = true;

        [Header("Runtime")]
        [SerializeField] private GameRuntime runtime;

        private bool subscribed;

        /// <summary>Level currently in use, once the runtime has resolved it.</summary>
        public LevelConfig ActiveLevel => activeLevel;

        /// <summary>All selectable levels.</summary>
        public LevelConfig[] AvailableLevels => availableLevels;

        /// <summary>Number of selectable levels.</summary>
        public int LevelCount => availableLevels != null ? availableLevels.Length : 0;

        /// <summary>True once the level has been cleared and the reward is showing.</summary>
        public bool IsLevelCleared { get; private set; }

        /// <summary>Number of the level being played, 1-based.</summary>
        public int CurrentLevelNumber => activeLevel != null ? activeLevel.LevelNumber : CampaignProgress.LevelNumber;

        /// <summary>Next level to load, or 0 when this is the last level.</summary>
        public int NextLevelNumber => activeLevel != null ? activeLevel.NextLevelNumber : 0;

        public bool HasNextLevel => NextLevelNumber > 0;

        /// <summary>Injects the runtime so the level can be applied without a scene search.</summary>
        public void Initialize(GameRuntime gameRuntime)
        {
            runtime = gameRuntime;
            Subscribe();
        }

        /// <summary>Returns a level by index, or null when the index is out of range.</summary>
        public LevelConfig GetLevel(int index)
        {
            if (availableLevels == null || index < 0 || index >= availableLevels.Length)
            {
                return null;
            }

            return availableLevels[index];
        }

        /// <summary>
        /// Resolves the level the kitchen should build. Honours the campaign position first so the menu
        /// can start any level, then falls back to the assigned asset.
        /// </summary>
        public LevelConfig ResolveLevel()
        {
            if (useCampaignProgress)
            {
                LevelConfig byProgress = Resources.Load<LevelConfig>(
                    CampaignProgress.LevelResourcesPath(CampaignProgress.LevelNumber));
                if (byProgress != null)
                {
                    activeLevel = byProgress;
                    return byProgress;
                }

                // Before any level asset exists, fall back to the catalogue by index.
                LevelConfig byIndex = GetLevel(CampaignProgress.LevelNumber - 1);
                if (byIndex != null)
                {
                    activeLevel = byIndex;
                    return byIndex;
                }
            }

            return activeLevel;
        }

        private void Awake()
        {
            if (runtime == null)
            {
                runtime = GetComponent<GameRuntime>();
            }

            Subscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (subscribed || runtime == null || runtime.Orders == null)
            {
                return;
            }

            runtime.Orders.LevelCleared += OnLevelCleared;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            if (runtime != null && runtime.Orders != null)
            {
                runtime.Orders.LevelCleared -= OnLevelCleared;
            }

            subscribed = false;
        }

        /// <summary>
        /// Called once when the last order of the level is completed. Records the reward, tells the UI
        /// to show the clear panel, and leaves advancing to the player's button.
        /// </summary>
        private void OnLevelCleared()
        {
            IsLevelCleared = true;

            LevelConfig level = activeLevel != null ? activeLevel : (runtime != null ? runtime.Level : null);
            CampaignProgress.ApplyClearReward(level);

            string reward = level != null ? level.DescribeReward() : string.Empty;
            runtime?.Notify(
                string.IsNullOrEmpty(reward)
                    ? $"{level?.LevelTitle ?? "本关"} 完成！"
                    : $"{level?.LevelTitle ?? "本关"} 完成！奖励：{reward}",
                MessageKind.Success);

            runtime?.UI?.ShowLevelClear(level, reward);
        }

        /// <summary>Loads the next level, or the menu when the campaign is finished.</summary>
        public void GoToNextLevel()
        {
            int next = NextLevelNumber;
            if (next <= 0)
            {
                ClassicProgress.Begin();
                LoadKitchen();
                return;
            }

            CampaignProgress.JumpTo(next);
            LoadKitchen();
        }

        /// <summary>Reloads the current level from the start.</summary>
        public void RestartLevel()
        {
            CampaignProgress.JumpTo(CurrentLevelNumber);
            LoadKitchen();
        }

        /// <summary>Returns to the start screen and resets the campaign.</summary>
        public void ReturnToMenu()
        {
            ClassicProgress.Leave();
            CampaignProgress.JumpTo(1);
            SceneManager.LoadScene(MenuSceneName);
        }

        private void LoadKitchen()
        {
            // Reloading the same scene is enough: the kitchen rebuilds every object from the level
            // asset, so there is no state to carry across.
            SceneManager.LoadScene(KitchenSceneName);
        }
    }
}
