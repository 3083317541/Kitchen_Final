using System;
using System.IO;
using Kitchen.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kitchen.EditorTools
{
    [InitializeOnLoad]
    public static class KitchenUIUpdater
    {
        private const string RequestPath = "Library/kitchen_ui_update.request";
        private const string ResultPath = "Logs/ui_update_result.json";
        private static double nextCheck;

        static KitchenUIUpdater() { EditorApplication.update += Poll; }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + 1f;
            if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.isPlaying = false;
                return;
            }
            File.Delete(RequestPath);
            try { UpdateScenes(); }
            catch (Exception exception)
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText(ResultPath, JsonUtility.ToJson(new Result { success = false, message = exception.ToString() }, true));
                Debug.LogException(exception);
            }
        }

        [MenuItem("Kitchen/UI/Apply Paper UI (UI Only)", false, 1)]
        public static void UpdateScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before updating UI.");
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                if (SceneManager.GetSceneAt(index).isDirty)
                    throw new InvalidOperationException("Save your open scene changes before applying the UI update.");
            }
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            TMP_FontAsset font = KitchenSceneGenerator.EnsureFontAsset();
            if (font == null) throw new InvalidOperationException("The Chinese UI font is missing.");
            try
            {
                UpdateKitchen(font);
                UpdateMenu(font);
                AssetDatabase.SaveAssets();
                Directory.CreateDirectory("Logs");
                File.WriteAllText(ResultPath, JsonUtility.ToJson(new Result { success = true,
                    message = "Updated Kitchen and MainMenu UI. Gameplay roots and configuration assets are preserved." }, true));
                Debug.Log("[Kitchen UI] Paper UI applied. Only Canvas objects and UI bindings were updated.");
            }
            finally
            {
                if (Array.Exists(setup, item => item.isLoaded && item.isActive))
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.OpenScene(KitchenSceneGenerator.ScenePath, OpenSceneMode.Single);
            }
        }

        private static void UpdateKitchen(TMP_FontAsset font)
        {
            Scene scene = EditorSceneManager.OpenScene(KitchenSceneGenerator.ScenePath, OpenSceneMode.Single);
            UIManager manager = null;
            GameObject oldCanvas = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                UIManager found = root.GetComponentInChildren<UIManager>(true);
                if (found != null) manager = found;
                if (root.GetComponent<Canvas>() != null && root.name == "Canvas") oldCanvas = root;
            }
            if (manager == null || oldCanvas == null) throw new InvalidOperationException("Expected Kitchen UIManager and Canvas were not found.");
            GameObject canvas = KitchenUIBuilder.BuildCanvas(font);
            KitchenUIBuilder.WireUI(manager, canvas, font);
            UnityEngine.Object.DestroyImmediate(oldCanvas);
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void UpdateMenu(TMP_FontAsset font)
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/_Game/Scenes/MainMenu.unity", OpenSceneMode.Single);
            MainMenuController controller = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                MainMenuController found = root.GetComponentInChildren<MainMenuController>(true);
                if (found != null) controller = found;
            }
            if (controller == null) throw new InvalidOperationException("MainMenuController was not found.");
            KitchenMenuBuilder.UpdateUI(controller, font);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        [Serializable]
        private sealed class Result
        {
            public bool success;
            public string message;
        }
    }
}
