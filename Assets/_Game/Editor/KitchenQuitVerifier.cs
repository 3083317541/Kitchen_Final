using System.IO;
using System.Linq;
using Kitchen.Runtime;
using Kitchen.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Kitchen.EditorTools
{
    [InitializeOnLoad]
    public static class KitchenQuitVerifier
    {
        private const string ModeKey = "Kitchen.QuitVerificationMode";
        private const string RequestedKey = "Kitchen.QuitVerificationRequested";
        private static int frames;
        private static double started;

        static KitchenQuitVerifier()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public static void RunMenu() { Begin("menu"); }
        public static void RunKitchen() { Begin("kitchen"); }

        private static void Begin(string mode)
        {
            SessionState.SetString(ModeKey, mode);
            SessionState.SetBool(RequestedKey, false);
            EditorSceneManager.OpenScene(mode == "menu" ? KitchenSceneGenerator.MenuScenePath : KitchenSceneGenerator.ScenePath);
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            string mode = SessionState.GetString(ModeKey, "");
            if (string.IsNullOrEmpty(mode)) return;
            if (started == 0) started = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - started > 120) { Finish(mode, false); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || ++frames < 20 || SessionState.GetBool(RequestedKey, false)) return;
            SessionState.SetBool(RequestedKey, true);
            if (mode == "menu")
                Object.FindObjectsOfType<Button>().First(button => button.name == "QuitButton").onClick.Invoke();
            else
            {
                GameRuntime runtime = GameRuntime.Instance;
                runtime.Input.RequestEscape();
                KitchenPauseMenu menu = runtime.UI.PauseMenu;
                menu.Canvas.GetComponentsInChildren<Button>(true).First(button => button.name == "PauseQuitGame").onClick.Invoke();
                menu.Canvas.GetComponentsInChildren<Button>(true).First(button => button.name == "PauseConfirmExit").onClick.Invoke();
            }
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            string mode = SessionState.GetString(ModeKey, "");
            if (state == PlayModeStateChange.EnteredEditMode && !string.IsNullOrEmpty(mode))
                Finish(mode, SessionState.GetBool(RequestedKey, false) && !EditorApplication.isPlaying && Time.timeScale > 0f);
        }

        private static void Finish(string mode, bool passed)
        {
            SessionState.SetString(ModeKey, "");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/quit_" + mode + "_report.json", "{\"quit_stops_editor_play_and_restores_time\":" + (passed ? "true" : "false") + "}");
            if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
            else EditorApplication.isPlaying = false;
        }
    }
}
