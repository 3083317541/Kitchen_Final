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
            Apply(true, true);
        }

        [MenuItem("Kitchen/UI/Apply Main Menu (Menu Only)", false, 2)]
        public static void UpdateMainMenu()
        {
            Apply(false, true);
        }

        [MenuItem("Kitchen/UI/Apply Kitchen HUD (HUD Only)", false, 3)]
        public static void UpdateKitchenHud()
        {
            Apply(true, false);
        }

        [MenuItem("Kitchen/UI/Apply Kitchen Typography (Fonts Only)", false, 4)]
        public static void UpdateKitchenTypography()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before updating UI fonts.");
            for (int index = 0; index < SceneManager.sceneCount; index++)
                if (SceneManager.GetSceneAt(index).isDirty)
                    throw new InvalidOperationException("Save scene changes before updating UI fonts.");
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                TMP_FontAsset font = KitchenHudFonts.Chinese;
                Scene scene = EditorSceneManager.OpenScene(KitchenSceneGenerator.ScenePath, OpenSceneMode.Single);
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    Canvas canvas = root.GetComponent<Canvas>();
                    if (canvas == null) continue;
                    foreach (TMP_Text label in canvas.GetComponentsInChildren<TMP_Text>(true))
                    {
                        label.font = font;
                        label.fontSharedMaterial = font.material;
                        label.fontStyle &= ~FontStyles.Bold;
                        EditorUtility.SetDirty(label);
                    }
                    KitchenMachineUI machine = canvas.GetComponent<KitchenMachineUI>();
                    if (machine != null)
                    {
                        machine.Configure(PaperUITheme.Rounded, font);
                        EditorUtility.SetDirty(machine);
                    }
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log("[Kitchen UI] Updated HUD fonts only; existing layout and bindings preserved.");
            }
            finally
            {
                if (Array.Exists(setup, item => item.isLoaded && item.isActive))
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        private static void Apply(bool kitchen, bool menu)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before updating UI.");
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                if (SceneManager.GetSceneAt(index).isDirty)
                    throw new InvalidOperationException("Save your open scene changes before applying the UI update.");
            }
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            TMP_FontAsset font = kitchen ? KitchenSceneGenerator.EnsureFontAsset() : KitchenMenuFonts.Chinese;
            if (font == null) throw new InvalidOperationException("The Chinese UI font is missing.");
            try
            {
                if (kitchen) UpdateKitchen(font);
                if (menu) UpdateMenu(font);
                AssetDatabase.SaveAssets();
                Directory.CreateDirectory("Logs");
                File.WriteAllText(ResultPath, JsonUtility.ToJson(new Result { success = true,
                    message = kitchen && menu ? "Updated Kitchen and MainMenu UI. Gameplay roots and configuration assets are preserved."
                        : kitchen ? "Updated Kitchen HUD only." : "Updated MainMenu UI only." }, true));
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
