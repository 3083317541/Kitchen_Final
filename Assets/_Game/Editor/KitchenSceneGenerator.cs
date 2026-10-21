using System;
using System.Collections.Generic;
using System.IO;
using Kitchen.Cookers;
using Kitchen.Data;
using Kitchen.Runtime;
using Kitchen.View;
using Kitchen.Work;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kitchen.EditorTools
{
    /// <summary>
    /// One click (or one batch flag) entry point that builds the whole playable demo:
    /// the project layers, the content assets and the Kitchen scene, with every reference wired.
    ///
    /// It can also run automatically. Because Unity refuses to open a project twice, a batch mode
    /// invocation is impossible while the editor already has the project open, so the generator
    /// watches for two request files and performs its work inside the running editor instead.
    /// The request files are removed as soon as the work is done, which makes the whole thing a
    /// one-shot operation.
    /// </summary>
    [InitializeOnLoad]
    public static class KitchenSceneGenerator
    {
        public const string ScenePath = KitchenSceneBuilder.ScenePath;

        private const string GenerateRequestFile = "Assets/_Game/Editor/generate_scene.request";
        private const string VerifyRequestFile = "Assets/_Game/Editor/verify_scene.request";
        private const string ReportPath = "Logs/acceptance_report.json";

        private static bool isRunning;

        static KitchenSceneGenerator()
        {
            EditorApplication.delayCall += OnEditorReady;
        }

        /// <summary>Guards the play mode warning so it is logged once per attempt, not every frame.</summary>
        private static bool warnedAboutPlayMode;

        private static void OnEditorReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += OnEditorReady;
                return;
            }

            // Generating while the editor is in play mode is not safe: creating a scene during play
            // throws, and saving assets mid-play leaves the running game pointing at destroyed objects.
            // The request is deliberately left in place so it runs once play mode has stopped.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (!warnedAboutPlayMode)
                {
                    warnedAboutPlayMode = true;
                    Debug.LogWarning(
                        "[Kitchen] A generation request is waiting, but the editor is in play mode. " +
                        "Stop play mode and it will run automatically.");
                }

                EditorApplication.delayCall += OnEditorReady;
                return;
            }

            warnedAboutPlayMode = false;

            if (File.Exists(VerifyRequestFile))
            {
                File.Delete(VerifyRequestFile);
                AssetDatabase.Refresh();
                KitchenAcceptanceVerifier.Run(ReportPath);
                return;
            }

            if (File.Exists(GenerateRequestFile))
            {
                File.Delete(GenerateRequestFile);
                AssetDatabase.Refresh();
                Generate();
                File.WriteAllText(VerifyRequestFile, "verify");
                AssetDatabase.Refresh();
            }
        }

        // ------------------------------------------------------------------ menu entries

        [MenuItem("Kitchen/1. Generate Data Assets", false, 10)]
        public static void GenerateDataAssetsOnly()
        {
            KitchenDataAssetGenerator.Result result = KitchenDataAssetGenerator.Generate();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Kitchen] Generated {result.CreatedPaths.Count} data asset(s). Level: {(result.Level != null ? result.Level.name : "<none>")}.");
        }

        [MenuItem("Kitchen/2. Generate Kitchen Scene", false, 11)]
        public static void GenerateSceneOnly()
        {
            BuildScene(false);
        }

        [MenuItem("Kitchen/3. Generate Everything", false, 12)]
        public static void Generate()
        {
            BuildScene(true);
        }

        [MenuItem("Kitchen/4. Open Kitchen Scene", false, 30)]
        public static void OpenScene()
        {
            if (!File.Exists(ScenePath))
            {
                GenerateSceneOnly();
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("Kitchen/5. Verify Acceptance Criteria", false, 40)]
        public static void Verify()
        {
            KitchenAcceptanceVerifier.Run(ReportPath);
        }

        /// <summary>
        /// Imports TextMeshPro's essential resources, which provide the default font. Only needed when
        /// no font asset could be created, and only ever run deliberately.
        /// </summary>
        [MenuItem("Kitchen/6. Import TMP Essential Resources", false, 50)]
        public static void ImportTmpEssentials()
        {
            const string packagePath =
                "Packages/com.unity.textmeshpro/Package Resources/TMP Essential Resources.unitypackage";

            if (!File.Exists(packagePath))
            {
                Debug.LogWarning($"[Kitchen] '{packagePath}' was not found. Import TMP Essential Resources from Window > TextMeshPro instead.");
                return;
            }

            AssetDatabase.ImportPackage(packagePath, false);
            Debug.Log("[Kitchen] TextMeshPro essential resources imported.");
        }

        // ------------------------------------------------------------------ batch entry points

        /// <summary>
        /// Batch mode entry point. Usage:
        /// <c>Unity.exe -batchmode -nographics -quit -projectPath &lt;path&gt;
        /// -executeMethod Kitchen.EditorTools.KitchenSceneGenerator.GenerateAndVerify</c>
        ///
        /// It generates the assets, builds the scene, runs the acceptance suite and finally opens the
        /// Kitchen scene so the next interactive session starts on it. The process exit code is 0
        /// only when every check passed.
        /// </summary>
        public static void GenerateAndVerify()
        {
            int exitCode = 1;

            try
            {
                ClearRequestFiles();
                BuildScene(true);

                KitchenAcceptanceVerifier.Run(ReportPath);

                // Leave the project pointing at the demo scene.
                if (File.Exists(ScenePath))
                {
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                }

                exitCode = KitchenAcceptanceVerifier.LastRunFailed ? 1 : 0;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Kitchen] Batch generation failed: {exception}");
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }

        /// <summary>Batch entry point that only rebuilds the data assets and the scene.</summary>
        public static void GenerateOnly()
        {
            int exitCode = 1;

            try
            {
                ClearRequestFiles();
                BuildScene(true);
                exitCode = File.Exists(ScenePath) ? 0 : 1;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Kitchen] Batch generation failed: {exception}");
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }

        /// <summary>
        /// Batch entry point that renders the finished demo to a PNG. It must be run with graphics
        /// enabled, so it is invoked separately from the acceptance suite. Usage:
        /// <c>Unity.exe -batchmode -quit -projectPath &lt;path&gt;
        /// -executeMethod Kitchen.EditorTools.KitchenSceneGenerator.CaptureScreenshot</c>
        /// </summary>
        public static void CaptureScreenshot()
        {
            ClearRequestFiles();
            KitchenScreenshotCapture.Capture(KitchenScreenshotCapture.DefaultOutputPath, 3f);
        }

        /// <summary>Asset path of the generated start screen.</summary>
        public static string MenuScenePath => KitchenSceneBuilder.MenuScenePath;

        private static void ClearRequestFiles()
        {
            if (File.Exists(GenerateRequestFile))
            {
                File.Delete(GenerateRequestFile);
            }

            if (File.Exists(VerifyRequestFile))
            {
                File.Delete(VerifyRequestFile);
            }
        }

        // ------------------------------------------------------------------ the build itself

        /// <summary>
        /// Creates the layers, the data assets and the scene. Returns the generated level.
        /// </summary>
        /// <param name="reloadContent">
        /// True when the content was just regenerated and must be reloaded from disk, so the scene
        /// references the persisted assets rather than the in-memory authoring copies.
        /// </param>
        public static LevelConfig BuildScene(bool reloadContent)
        {
            if (isRunning)
            {
                Debug.LogWarning("[Kitchen] A generation pass is already running.");
                return null;
            }

            isRunning = true;
            try
            {
                EnsureLayers();
                EnsureFolders();

                KitchenDataAssetGenerator.Result data = KitchenDataAssetGenerator.Generate();
                AssetDatabase.SaveAssets();

                // Materials are authored first and registered with Whitebox, so the prefabs built
                // below serialise references to real material assets instead of nulls.
                WhiteboxMaterialLibrary materials = KitchenMaterialGenerator.Generate();
                Whitebox.RegisterLibrary(materials);
                AssetDatabase.SaveAssets();


                // Prefabs are generated before the scene so the scene preview can instantiate them and
                // stay connected to them. Editing a prefab afterwards updates the Scene view and the
                // running game, without regenerating anything.
                LevelConfig prefabLevel = AssetDatabase.LoadAssetAtPath<LevelConfig>(
                    KitchenDataAssetGenerator.LevelAssetPath);
                KitchenPrefabGenerator.Result prefabs = KitchenPrefabGenerator.Generate(
                    prefabLevel,
                    data.Content != null ? data.Content.Cookers : new List<CookerDefinition>(),
                    data.Content != null ? data.Content.Items : new List<ItemDefinition>());

                // Every level needs the art links, not just the one the scene was authored from:
                // otherwise level 2 and 3 would silently fall back to the primitive chef and floor.
                if (prefabs != null)
                {
                    AssignArtPrefabsToAllLevels(prefabs.StaffPrefab, prefabs.FloorPrefab);
                }

                AssetDatabase.SaveAssets();
                Debug.Log($"[Kitchen] Generated {prefabs?.CreatedPaths.Count ?? 0} prefab asset(s).");

                TMP_FontAsset font = EnsureFontAsset();
            Debug.Log(font != null
                ? $"[Kitchen] HUD font resolved: '{font.name}' material={(font.material != null ? font.material.name : "none")} atlas={(font.atlasTexture != null ? font.atlasTexture.width + "x" + font.atlasTexture.height : "none")}."
                : "[Kitchen] HUD font resolved: none.");
            if (font == null)
            {
                Debug.LogWarning("[Kitchen] No TextMeshPro font asset is available; the HUD labels will use TextMeshPro's default font. Run Kitchen/6. Import TMP Essential Resources to create one.");
            }

                if (reloadContent)
                {
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                }

                LevelConfig level = AssetDatabase.LoadAssetAtPath<LevelConfig>(KitchenDataAssetGenerator.LevelAssetPath);
                if (level == null)
                {
                    level = data.Level;
                    Debug.LogWarning("[Kitchen] The level asset could not be reloaded; the in-memory level is used for the scene.");
                }

                if (level == null)
                {
                    Debug.LogError("[Kitchen] No level is available at all; the scene cannot be built correctly.");
                }

                List<CookerWorkStrategy> strategies = new List<CookerWorkStrategy>();
                string[] strategyGuids = AssetDatabase.FindAssets("t:CookerWorkStrategy", new[] { KitchenDataAssetGenerator.StrategyFolder });
                for (int i = 0; i < strategyGuids.Length; i++)
                {
                    CookerWorkStrategy strategy = AssetDatabase.LoadAssetAtPath<CookerWorkStrategy>(AssetDatabase.GUIDToAssetPath(strategyGuids[i]));
                    if (strategy != null)
                    {
                        strategies.Add(strategy);
                    }
                }

                strategies.Sort((a, b) => a.WorkType.CompareTo(b.WorkType));

                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                // Creating the empty scene destroys the previous one, which drops the asset references
                // that scene was holding. Both assets are resolved again now that the new scene exists,
                // so the runtime and the camera framing receive live objects rather than nulls.
                level = AssetDatabase.LoadAssetAtPath<LevelConfig>(KitchenDataAssetGenerator.LevelAssetPath) ?? data.Level;
                font = EnsureFontAsset();

                if (level == null)
                {
                    Debug.LogError(
                        "[Kitchen] The level asset could not be resolved after the scene reset, so the " +
                        "camera cannot be framed and the runtime has no map. The scene was not saved.");
                    return null;
                }

                Debug.Log($"[Kitchen] Building the scene with level '{level.name}' " +
                    $"({level.Width}x{level.Height} cells of {level.CellSize}) and font " +
                    $"{(font != null ? font.name : "<default>")}.");

                GameRuntime runtime = KitchenSceneBuilder.Build(level, strategies, font);

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                {
                    Debug.LogError($"[Kitchen] Failed to save '{ScenePath}'.");
                    return level;
                }

                RegisterSceneInBuildSettings();
                AssetDatabase.SaveAssets();

                Debug.Log($"[Kitchen] Kitchen scene generated at '{ScenePath}' with {strategies.Count} work strategies and level '{(level != null ? level.name : "<none>")}'.");
                if (runtime == null)
                {
                    Debug.LogError("[Kitchen] The generated scene has no GameRuntime.");
                }

                // Built last, because creating the start screen scene replaces the open scene and
                // would invalidate the scene references used just above. The kitchen scene is reopened
                // afterwards so the editor is left on the game rather than on the menu.
                BuildMenuScene(font);
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

                return level;
            }
            finally
            {
                isRunning = false;
            }
        }

        // ------------------------------------------------------------------ project setup

        /// <summary>Adds the Interactable and Ground layers used by cooker/staff picking.</summary>
        public static void EnsureLayers()
        {
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            EnsureLayer(layers, "Interactable");
            EnsureLayer(layers, "Ground");
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureLayer(SerializedProperty layers, string layerName)
        {
            for (int i = 0; i < layers.arraySize; i++)
            {
                SerializedProperty element = layers.GetArrayElementAtIndex(i);
                if (element.stringValue == layerName)
                {
                    return;
                }
            }

            // Layers 0-7 are reserved by Unity; the first free slot from 8 upwards is used.
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty element = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(element.stringValue))
                {
                    element.stringValue = layerName;
                    return;
                }
            }

            Debug.LogError($"[Kitchen] No free layer slot for '{layerName}'. Picking will not work correctly.");
        }

        /// <summary>
        /// Writes the generated chef and floor prefabs onto every level asset, including the sandbox,
        /// so no level can end up without the art links.
        /// </summary>
        private static void AssignArtPrefabsToAllLevels(GameObject staffPrefab, GameObject floorPrefab)
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:LevelConfig", new[] { KitchenDataAssetGenerator.ResourcesRoot });

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                LevelConfig level = AssetDatabase.LoadAssetAtPath<LevelConfig>(path);
                if (level == null)
                {
                    continue;
                }

                level.SetArtPrefabs(staffPrefab, floorPrefab);
                EditorUtility.SetDirty(level);
            }

            AssetDatabase.SaveAssets();
        }

        private static void EnsureFolders()
        {
            string[] folders =
            {
                "Assets/_Game/Prefabs/Grid",
                "Assets/_Game/Prefabs/Staff",
                "Assets/_Game/Prefabs/Cookers",
                "Assets/_Game/Prefabs/Ingredients",
                "Assets/_Game/Prefabs/UI",
                "Assets/_Game/Art/Characters",
                "Assets/_Game/Art/Cookers",
                "Assets/_Game/Art/Environment",
                "Assets/_Game/Art/Ingredients",
                "Assets/_Game/Art/Materials",
                "Assets/_Game/Art/Animations",
                "Assets/_Game/Art/Fonts",
                "Assets/_Game/Scenes",
                "Logs"
            };

            for (int i = 0; i < folders.Length; i++)
            {
                if (folders[i].StartsWith("Assets", StringComparison.Ordinal))
                {
                    EnsureAssetFolder(folders[i]);
                }
                else
                {
                    Directory.CreateDirectory(ToAbsolutePath(folders[i]));
                }
            }
        }

        private static void EnsureAssetFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        public static string ToAbsolutePath(string projectRelative)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, projectRelative);
        }

        /// <summary>
        /// Puts the start screen first and the kitchen second. The order matters: index 0 is what a
        /// build launches into, and the game must open on the menu rather than in the kitchen.
        /// </summary>
        private static void RegisterSceneInBuildSettings()
        {
            string menuPath = MenuScenePath;
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            // Drop both entries, then re-insert them in the right order.
            for (int i = scenes.Count - 1; i >= 0; i--)
            {
                if (scenes[i].path == ScenePath || scenes[i].path == menuPath)
                {
                    scenes.RemoveAt(i);
                }
            }

            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            if (File.Exists(menuPath))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(menuPath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>
        /// Builds and saves the start screen scene. Kept separate from the kitchen build so a failure
        /// here cannot leave a half built kitchen, and so it can be regenerated on its own.
        /// </summary>
        private static void BuildMenuScene(TMP_FontAsset font)
        {
            try
            {
                Scene menuScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                KitchenMenuBuilder.Build(font);

                EditorSceneManager.MarkSceneDirty(menuScene);
                if (!EditorSceneManager.SaveScene(menuScene, MenuScenePath))
                {
                    Debug.LogError($"[Kitchen] Failed to save the start screen at '{MenuScenePath}'.");
                    return;
                }

                Debug.Log($"[Kitchen] Start screen generated at '{MenuScenePath}'.");
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[Kitchen] The start screen could not be generated: {exception.Message}");
            }
        }

        // ------------------------------------------------------------------ font

        /// <summary>
        /// Returns a TextMeshPro font asset for the HUD.
        ///
        /// The HUD text is Chinese, so a CJK capable font is required. TextMeshPro's bundled
        /// Liberation Sans has no Chinese glyphs, and asking Unity for an OS font by name produces a
        /// dynamic font wrapper that TextMeshPro cannot build an atlas from. What does work is loading
        /// a real font file and creating a dynamic atlas asset from it, so the first installed CJK
        /// font found on the machine is copied into the project and used as the source. The atlas is
        /// dynamic, which means glyphs are rasterised on demand and the whole 20 000 character font is
        /// never baked.
        /// </summary>
        public static TMP_FontAsset EnsureFontAsset()
        {
            // Touching TMP_Settings before its resources exist makes TextMeshPro pop a package
            // importer window, which is noisy and useless in a batch run. Detect that case instead.
            if (TMP_Settings.instance == null)
            {
                Debug.LogWarning(
                    "[Kitchen] TextMeshPro settings are missing, which means its essential resources " +
                    "were never imported. Run Kitchen/6. Import TMP Essential Resources, then generate " +
                    "the scene again so the HUD gets a font.");
                return null;
            }

            // The committed CJK font comes first. It lives in version control together with its .meta, so
            // every machine resolves the same guid; the machine-local fallback below was how the scene
            // ended up referencing a font that only existed on the machine that generated it.
            TMP_FontAsset committed = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                KitchenHudFont.FontAssetPath);

            if (committed != null && committed.IsUsable())
            {
                return committed;
            }

            // The font the demo generated for itself, if a previous run created one.
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ChineseFontPath);
            if (existing != null && existing.IsUsable())
            {
                return existing;
            }

            TMP_FontAsset chinese = TryCreateChineseFontAsset();
            if (chinese != null)
            {
                return chinese;
            }

            Debug.LogWarning(
                "[Kitchen] No Chinese font could be created on this machine, so the HUD falls back to " +
                "TextMeshPro's Liberation Sans. Chinese labels may show as blank boxes. Install a CJK " +
                "font such as Noto Sans SC or SimHei and generate the scene again.");

            // Search the whole project for a usable font asset so the generator still produces a
            // working scene even when no CJK source font is present.
            string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
            TMP_FontAsset best = null;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                TMP_FontAsset candidate = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (candidate == null || !candidate.IsUsable())
                {
                    continue;
                }

                if (candidate.name == "LiberationSans SDF" || path.IndexOf("Resources", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return candidate;
                }

                if (best == null)
                {
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>
        /// Copies the first available CJK font into the project and builds a dynamic TextMeshPro asset
        /// from it. Returns null when no suitable font is installed.
        /// </summary>
        private static TMP_FontAsset TryCreateChineseFontAsset()
        {
            for (int i = 0; i < ChineseFontCandidates.Length; i++)
            {
                string sourcePath = ChineseFontCandidates[i];
                if (!File.Exists(sourcePath))
                {
                    continue;
                }

                string fileName = Path.GetFileName(sourcePath);
                string projectPath = $"{FontsFolder}/{fileName}";

                try
                {
                    Directory.CreateDirectory(FontsFolderAbsolute);

                    if (!File.Exists(FontsFolderAbsolute + "/" + fileName))
                    {
                        File.Copy(sourcePath, FontsFolderAbsolute + "/" + fileName, true);
                    }

                    AssetDatabase.ImportAsset(projectPath, ImportAssetOptions.ForceSynchronousImport);
                    Font source = AssetDatabase.LoadAssetAtPath<Font>(projectPath);
                    if (source == null)
                    {
                        Debug.LogWarning($"[Kitchen] '{sourcePath}' could not be imported as a font.");
                        continue;
                    }

                    TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(
                        source,
                        90,
                        9,
                        UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                        2048,
                        2048,
                        AtlasPopulationMode.Dynamic,
                        true);

                    if (font == null)
                    {
                        Debug.LogWarning($"[Kitchen] TextMeshPro could not build a font asset from '{fileName}'.");
                        continue;
                    }

                    EnsureAssetFolder(FontsFolder);
                    font.name = "KitchenHudCJK";
                    AssetDatabase.CreateAsset(font, ChineseFontPath);

                    // The atlas texture and material are sub assets, so they must be added explicitly
                    // or they are lost on the next import.
                    if (font.atlasTexture != null)
                    {
                        font.atlasTexture.name = "KitchenHudCJK Atlas";
                        AssetDatabase.AddObjectToAsset(font.atlasTexture, font);
                    }

                    if (font.material != null)
                    {
                        font.material.name = "KitchenHudCJK Material";
                        AssetDatabase.AddObjectToAsset(font.material, font);
                    }

                    EditorUtility.SetDirty(font);
                    AssetDatabase.SaveAssets();

                    TMP_FontAsset saved = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ChineseFontPath);
                    if (saved == null)
                    {
                        continue;
                    }

                    // Prove the asset can actually rasterise Chinese before it is used by the HUD.
                    if (!saved.TryAddCharacters(ChineseFontSample, out string missing) || !string.IsNullOrEmpty(missing))
                    {
                        Debug.LogWarning(
                            $"[Kitchen] The font asset built from '{fileName}' cannot render Chinese " +
                            $"(missing: {missing}). Trying the next candidate.");
                        AssetDatabase.DeleteAsset(ChineseFontPath);
                        continue;
                    }

                    Debug.Log($"[Kitchen] HUD font created from '{fileName}' with a dynamic 2048x2048 atlas.");
                    return saved;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[Kitchen] Building a font asset from '{sourcePath}' failed: {exception.Message}");
                }
            }

            return null;
        }

        private const string FontsFolder = "Assets/_Game/Art/Fonts";
        private const string ChineseFontPath = "Assets/_Game/Art/Fonts/KitchenHudCJK.asset";
        private const string ChineseFontSample = "厨房订单金钱时间模式切菜台煎锅组装洗碗盘干净脏";

        private static string FontsFolderAbsolute =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, FontsFolder);

        /// <summary>
        /// Installed fonts to try, in order of preference. Noto Sans SC and Source Han render Chinese
        /// most cleanly, then the Windows defaults.
        /// </summary>
        private static readonly string[] ChineseFontCandidates =
        {
            "C:/Windows/Fonts/NotoSansSC-Regular.otf",
            "C:/Windows/Fonts/NotoSansSC-VF.otf",
            "C:/Windows/Fonts/msyh.ttc",
            "C:/Windows/Fonts/simhei.ttf",
            "C:/Windows/Fonts/simsun.ttc",
            "/System/Library/Fonts/PingFang.ttc",
            "/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc",
            "/usr/share/fonts/truetype/wqy/wqy-zenhei.ttc"
        };
    }
}
