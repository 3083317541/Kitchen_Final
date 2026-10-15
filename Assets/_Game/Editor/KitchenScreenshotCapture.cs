using System.IO;
using Kitchen.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kitchen.EditorTools
{
    /// <summary>
    /// Enters play mode on the generated scene, waits for the games to settle, and writes a PNG of
    /// the game view.
    ///
    /// This exists because geometry and reference checks cannot prove that the HUD text is legible or
    /// that the scene reads as 3D. Only a rendered frame can. It must run with graphics enabled
    /// (without <c>-nographics</c>), because a null graphics device renders nothing.
    /// </summary>
    public static class KitchenScreenshotCapture
    {
        public const string DefaultOutputPath = "Logs/kitchen_screenshot.png";

        private static string outputPath;
        private static double deadline;
        private static bool finished;

        /// <summary>
        /// Schedule a capture. The window closes itself and the editor exits when the file is written,
        /// which is what makes this usable from a headless batch run.
        /// </summary>
        public static void Capture(string path, float delaySeconds)
        {
            outputPath = path;
            finished = false;
            deadline = EditorApplication.timeSinceStartup + delaySeconds;

            KitchenSceneGenerator.BuildScene(true);
            EditorSceneManager.OpenScene(KitchenSceneGenerator.ScenePath, OpenSceneMode.Single);

            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
            EditorApplication.EnterPlaymode();
        }

        private static void OnUpdate()
        {
            if (finished || EditorApplication.timeSinceStartup < deadline)
            {
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                return;
            }

            finished = true;
            EditorApplication.update -= OnUpdate;

            string absolute = KitchenSceneGenerator.ToAbsolutePath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute));

            if (File.Exists(absolute))
            {
                File.Delete(absolute);
            }

            ScreenCapture.CaptureScreenshot(absolute);
            EditorApplication.delayCall += FinishWhenWritten;
        }

        private static void FinishWhenWritten()
        {
            string absolute = KitchenSceneGenerator.ToAbsolutePath(outputPath);
            if (!File.Exists(absolute))
            {
                // Give the capture one more editor frame to land on disk.
                EditorApplication.delayCall += FinishWhenWritten;
                return;
            }

            Debug.Log($"[Kitchen] Screenshot written to '{absolute}'.");

            GameRuntime runtime = Object.FindObjectOfType<GameRuntime>();
            if (runtime != null)
            {
                Debug.Log($"[Kitchen] Screenshot scene state: money={runtime.Money}, staff={runtime.AllStaff.Count}, " +
                    $"cookers={runtime.Cookers.Cookers.Count}, order={(runtime.Orders.Current != null ? runtime.Orders.Current.Definition.DisplayName : "<none>")}.");
            }

            EditorApplication.Exit(0);
        }
    }
}
