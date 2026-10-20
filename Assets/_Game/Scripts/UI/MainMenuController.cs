using Kitchen.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kitchen.UI
{
    /// <summary>
    /// Drives the start screen.
    ///
    /// It only resets the campaign, remembers which level to start and loads the kitchen scene; it does
    /// not build anything. Keeping it that small means the start screen cannot drift out of sync with
    /// the game, because the game still starts the same way it always did.
    /// </summary>
    [DisallowMultipleComponent]
    public class MainMenuController : MonoBehaviour
    {
        /// <summary>Scene the start button loads.</summary>
        public const string KitchenSceneName = "Kitchen";

        [Header("Buttons")]
        [SerializeField] private UnityEngine.UI.Button startButton;
        [SerializeField] private UnityEngine.UI.Button quitButton;
        [SerializeField] private UnityEngine.UI.Button classicButton;

        [Header("Labels")]
        [Tooltip("Shown under the title. Uses the highest level reached as a progress hint.")]
        [SerializeField] private TMPro.TMP_Text progressText;

        [Tooltip("Title shown at the top of the screen.")]
        [SerializeField] private TMPro.TMP_Text titleText;

        private void Awake()
        {
            if (startButton != null)
            {
                if (classicButton == null)
                {
                    TMPro.TMP_Text tutorialLabel = startButton.GetComponentInChildren<TMPro.TMP_Text>(true);
                    if (tutorialLabel != null) tutorialLabel.text = "教程模式 · 三关入门";
                    classicButton = Instantiate(startButton, startButton.transform.parent);
                    classicButton.name = "ClassicModeButton";
                    classicButton.transform.SetSiblingIndex(startButton.transform.GetSiblingIndex() + 1);
                    classicButton.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
                    classicButton.GetComponentInChildren<TMPro.TMP_Text>(true).text = "经典模式 · 每日营业";
                    RectTransform card = startButton.transform.parent.parent as RectTransform;
                    if (card != null) card.sizeDelta = new Vector2(card.sizeDelta.x, 800f);
                }
                classicButton.onClick.RemoveListener(StartClassicGame);
                classicButton.onClick.AddListener(StartClassicGame);
            }
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(StartGame);
                startButton.onClick.AddListener(StartGame);
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(QuitGame);
                quitButton.onClick.AddListener(QuitGame);
            }
        }

        private void Start()
        {
            Refresh();
        }

        /// <summary>Starts a fresh campaign from level one and loads the kitchen.</summary>
        public void StartGame()
        {
            ClassicProgress.Leave();
            CampaignProgress.Reset();
            SceneManager.LoadScene(KitchenSceneName);
        }

        /// <summary>Continues from the level the campaign is already on. Used when returning to the menu.</summary>
        public void ContinueGame()
        {
            ClassicProgress.Leave();
            if (CampaignProgress.LevelNumber <= 0)
            {
                CampaignProgress.JumpTo(1);
            }

            SceneManager.LoadScene(KitchenSceneName);
        }

        public void StartClassicGame()
        {
            ClassicProgress.Begin();
            SceneManager.LoadScene(KitchenSceneName);
        }

        /// <summary>Quits the player on a build, and stops play mode in the editor.</summary>
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>Refreshes the labels from the campaign state.</summary>
        public void Refresh()
        {
            if (progressText != null)
            {
                progressText.text = CampaignProgress.HighestReached > 1
                    ? $"教程进度：第 {CampaignProgress.HighestReached} 关"
                    : "从第一份沙拉开始";
            }
        }
    }
}
