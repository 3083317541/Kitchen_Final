using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Kitchen.Cookers;
using Kitchen.Core;
using Kitchen.Data;
using Kitchen.Entities;
using Kitchen.Input;
using Kitchen.Orders;
using Kitchen.Runtime;
using Kitchen.Staff;
using Kitchen.View;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Kitchen.EditorTools
{
    /// <summary>
    /// Drives the real runtime through a scripted play session and records the result of every
    /// acceptance criterion.
    ///
    /// It is deliberately not a mock: it builds the same component graph the scene uses, calls the
    /// same <see cref="GameRuntime"/> entry points and pumps the same
    /// <see cref="GameRuntime.AdvanceFrame"/> that the frame loop uses. The only thing it does not
    /// cover is Unity's own play-mode plumbing.
    /// </summary>
    public static class KitchenAcceptanceVerifier
    {
        private class Result
        {
            public string Name;
            public bool Passed;
            public string Detail;
        }

        private static readonly List<Result> Results = new List<Result>();
        private static GameRuntime runtime;
        private static int stepCount;
        private static int bootCookerCount;
        private static int bootStaffCount;
        private static bool bootClearedPreview;

        /// <summary>True when the most recent run contained at least one failure.</summary>
        public static bool LastRunFailed { get; private set; }

        // ------------------------------------------------------------------ entry point

        /// <summary>Runs the whole suite once and writes the report to <paramref name="reportPath"/>.</summary>
        public static void Run(string reportPath)
        {
            Results.Clear();
            GameObject root = null;

            try
            {
                root = CreateHarness();
                if (runtime == null)
                {
                    Record("harness", false, "GameRuntime could not be created.");
                    return;
                }

                RunScenarios();
                RecordSceneWiring();
            }
            catch (Exception exception)
            {
                Record("harness", false, $"Unhandled exception: {exception.GetType().Name}: {exception.Message}");
                Debug.LogException(exception);
            }
            finally
            {
                if (root != null)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }

                runtime = null;
            }

            WriteReport(reportPath);
        }

        private static GameObject CreateHarness()
        {
            GameObject root = new GameObject("~AcceptanceHarness");
            root.hideFlags = HideFlags.HideAndDontSave;

            GameObject world = new GameObject("World");
            world.transform.SetParent(root.transform, false);
            Transform floor = Child(world.transform, "Floor");
            Transform gridRoot = Child(world.transform, "GridRoot");
            Transform cookerRoot = Child(world.transform, "CookerRoot");
            Transform staffRoot = Child(world.transform, "StaffRoot");
            Transform ingredientRoot = Child(world.transform, "IngredientRoot");

            // A preview container is planted before boot so the runtime's cleanup is actually tested.
            Transform cookerPreview = Child(cookerRoot, KitchenScenePreview.PreviewContainerName);
            Child(cookerPreview, "Cooker_PreviewStub");
            Transform staffPreview = Child(staffRoot, KitchenScenePreview.PreviewContainerName);
            Child(staffPreview, "Staff_PreviewStub");

            GameObject cameraObject = new GameObject("HarnessCamera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.position = new Vector3(0f, 30f, 0f);
            cameraObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;

            GameObject systemsObject = new GameObject("Systems");
            systemsObject.transform.SetParent(root.transform, false);
            systemsObject.AddComponent<Kitchen.Grid.GridManager>();
            systemsObject.AddComponent<CookerManager>();
            systemsObject.AddComponent<StaffManager>();
            systemsObject.AddComponent<Kitchen.Work.WorkManager>();
            systemsObject.AddComponent<Kitchen.Inventory.InventoryManager>();
            systemsObject.AddComponent<Kitchen.Recipes.RecipeManager>();
            systemsObject.AddComponent<OrderManager>();
            systemsObject.AddComponent<PlateReturnManager>();

            // The strategies come from the generated assets, so the verification exercises the
            // persisted configuration rather than a code-only copy.
            Kitchen.Work.WorkStrategyHolder holder = systemsObject.AddComponent<Kitchen.Work.WorkStrategyHolder>();
            KitchenSceneBuilder.AssignWorkStrategies(holder, LoadStrategies());

            GameObject runtimeObject = new GameObject("Game_Runtime");
            runtimeObject.transform.SetParent(root.transform, false);
            GameRuntime gameRuntime = runtimeObject.AddComponent<GameRuntime>();
            InputManager inputManager = runtimeObject.AddComponent<InputManager>();
            runtimeObject.AddComponent<Kitchen.UI.UIManager>();
            runtimeObject.AddComponent<Kitchen.Level.LevelManager>();

            // The suite drives a sandbox level rather than a campaign level: campaign levels start with
            // almost nothing on purpose, so testing against one would test the balancing instead of the
            // systems. The saved asset is preferred because its definitions are the persisted ones, which
            // are what carry the prefab and material links.
            LevelConfig level = AssetDatabase.LoadAssetAtPath<LevelConfig>(KitchenDataAssetGenerator.SandboxLevelPath);
            if (level == null)
            {
                Debug.LogWarning("[Acceptance] The sandbox level asset is missing; using in-memory content.");
                level = GameContentFactory.CreateSandboxLevel(GameContentFactory.CreateRuntimeContent());
            }

            SerializedObject so = new SerializedObject(gameRuntime);
            so.FindProperty("levelConfig").objectReferenceValue = level;
            so.FindProperty("loadFromResourcesWhenEmpty").boolValue = false;
            so.FindProperty("worldCamera").objectReferenceValue = camera;
            so.FindProperty("inputManager").objectReferenceValue = inputManager;
            so.FindProperty("uiManager").objectReferenceValue = null;
            so.FindProperty("floorRoot").objectReferenceValue = floor;
            so.FindProperty("gridRoot").objectReferenceValue = gridRoot;
            so.FindProperty("cookerRoot").objectReferenceValue = cookerRoot;
            so.FindProperty("staffRoot").objectReferenceValue = staffRoot;
            so.FindProperty("ingredientRoot").objectReferenceValue = ingredientRoot;
            so.FindProperty("gridManager").objectReferenceValue = systemsObject.GetComponent<Kitchen.Grid.GridManager>();
            so.FindProperty("cookerManager").objectReferenceValue = systemsObject.GetComponent<CookerManager>();
            so.FindProperty("staffManager").objectReferenceValue = systemsObject.GetComponent<StaffManager>();
            so.FindProperty("workManager").objectReferenceValue = systemsObject.GetComponent<Kitchen.Work.WorkManager>();
            so.FindProperty("inventoryManager").objectReferenceValue = systemsObject.GetComponent<Kitchen.Inventory.InventoryManager>();
            so.FindProperty("recipeManager").objectReferenceValue = systemsObject.GetComponent<Kitchen.Recipes.RecipeManager>();
            so.FindProperty("orderManager").objectReferenceValue = systemsObject.GetComponent<OrderManager>();
            so.FindProperty("plateReturnManager").objectReferenceValue = systemsObject.GetComponent<PlateReturnManager>();
            so.ApplyModifiedPropertiesWithoutUndo();

            gameRuntime.Boot();
            gameRuntime.StartLevel();

            // Measured straight after boot, before any scenario builds or removes anything, so this
            // describes the starting state only.
            bootCookerCount = gameRuntime.Cookers.Cookers.Count;
            bootStaffCount = gameRuntime.AllStaff.Count;
            bool previewsCleared = cookerRoot.Find(KitchenScenePreview.PreviewContainerName) == null
                && staffRoot.Find(KitchenScenePreview.PreviewContainerName) == null;
            bootClearedPreview = previewsCleared;

            if (!previewsCleared)
            {
                Debug.LogError("[Acceptance] The runtime did not remove the authored scene preview.");
            }

            if (!gameRuntime.IsInitialized)
            {
                Record("boot", false, "GameRuntime did not initialise.");
                return root;
            }

            // Real devices are muted for the duration so a stray click cannot disturb the run.
            inputManager.enabled = false;

            runtime = gameRuntime;
            Record("boot", true, $"Level '{level.name}' booted with {runtime.AllStaff.Count} staff and {runtime.Cookers.Cookers.Count} cookers.");
            return root;
        }

        private static List<CookerWorkStrategy> LoadStrategies()
        {
            List<CookerWorkStrategy> strategies = new List<CookerWorkStrategy>();
            string[] guids = AssetDatabase.FindAssets("t:CookerWorkStrategy", new[] { KitchenDataAssetGenerator.StrategyFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                CookerWorkStrategy strategy = AssetDatabase.LoadAssetAtPath<CookerWorkStrategy>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (strategy != null)
                {
                    strategies.Add(strategy);
                }
            }

            strategies.Sort((a, b) => a.WorkType.CompareTo(b.WorkType));
            return strategies;
        }

        private static Transform Child(Transform parent, string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        // ------------------------------------------------------------------ scenarios

        private static void RunScenarios()
        {
            ScenarioGrid();
            ScenarioStaff();
            ScenarioBuild();
            ScenarioBuildHighlight();
            ScenarioChopping();
            ScenarioFrying();
            ScenarioAssembly();
            ScenarioWashing();
            ScenarioOrderAndPlates();
            ScenarioUiGuard();
            ScenarioPlayerClicks();
            ScenarioWorldGeometry();
            ScenarioCameraFraming();
            ScenarioUiLayout();
            ScenarioSceneContents();
            ScenarioArtPrefabs();
            ScenarioMaterials();
            ScenarioCampaign();
            ScenarioCookerControls();
            ScenarioScheduling();
            ScenarioOperationPositions();
            ScenarioSceneScriptGuids();
            ScenarioFinalConsistency();
        }

        // ---- 19b. HUD layout -----------------------------------------------------------------

        /// <summary>
        /// Checks the HUD for the two problems a player actually notices: panels drawn on top of one
        /// another, and a label whose text needs more room than its rectangle provides, which is what
        /// makes text run into its neighbour.
        ///
        /// The measurements come from TextMeshPro's own layout pass, so they describe the real text
        /// rather than an estimate from the character count.
        /// </summary>
        private static void ScenarioUiLayout()
        {
            // The HUD is not created in this harness (the acceptance run has no Canvas), so the
            // generated scene is inspected instead.
            if (!File.Exists(KitchenSceneGenerator.ScenePath))
            {
                Record("ui_no_overlap", false, $"{KitchenSceneGenerator.ScenePath} does not exist.");
                return;
            }

            UnityEngine.SceneManagement.Scene scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                KitchenSceneGenerator.ScenePath,
                UnityEditor.SceneManagement.OpenSceneMode.Additive);

            Canvas canvas = null;
            UnityEngine.GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Canvas found = roots[i].GetComponent<Canvas>();
                if (found != null)
                {
                    canvas = found;
                    break;
                }
            }

            if (canvas == null)
            {
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
                Record("ui_no_overlap", false, "the scene has no Canvas.");
                return;
            }

            // Panels are the named containers the HUD is built from.
            string[] panelNames =
            {
                "TopBar", "CookerShopPanel", "OrderPanel", "SelectionPanel",
                "RecipeProgressPanel", "NotificationPanel"
            };

            // The layout groups must be resolved before any rectangle can be measured.
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            ForceLayout(canvasRect);

            List<string> overlaps = new List<string>();

            for (int a = 0; a < panelNames.Length; a++)
            {
                RectTransform first = FindRect(canvas.transform, panelNames[a]);
                if (first == null || !first.gameObject.activeInHierarchy)
                {
                    continue;
                }

                for (int b = a + 1; b < panelNames.Length; b++)
                {
                    RectTransform second = FindRect(canvas.transform, panelNames[b]);
                    if (second == null || !second.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    Rect firstRect = ToCanvasRect(first, canvas);
                    Rect secondRect = ToCanvasRect(second, canvas);

                    if (firstRect.Overlaps(secondRect, true))
                    {
                        overlaps.Add($"{panelNames[a]} {firstRect} overlaps {panelNames[b]} {secondRect}");
                    }
                }
            }

            Expect("ui_no_panel_overlap", overlaps.Count == 0,
                overlaps.Count == 0
                    ? $"the six HUD panels occupy separate areas (canvas scale {canvas.scaleFactor:0.##})."
                    : string.Join("; ", overlaps.ToArray()));

            ExpectPanelsInsideCanvas(panelNames, canvas, canvasRect);
            ScenarioLabelOverlap(canvas);
            ScenarioHudScale(canvas);

            // Text fitting: a label that needs more width than its rectangle allows is clipped or
            // pushed over a neighbour.
            List<string> tooNarrow = new List<string>();
            int measured = 0;
            int skipped = 0;

            TMP_Text[] labels = canvas.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                TMP_Text label = labels[i];
                if (label == null || string.IsNullOrEmpty(label.text))
                {
                    continue;
                }

                float available = label.rectTransform.rect.width;
                if (available <= 1f)
                {
                    // Laid out by a parent group: the group gives it its width, so measure later.
                    skipped++;
                    continue;
                }

                float required;
                try
                {
                    required = label.GetPreferredValues(label.text, 0f, 0f).x;
                }
                catch (Exception)
                {
                    skipped++;
                    continue;
                }

                if (required <= 0.01f)
                {
                    // The font cannot measure the text, which the glyph check already reports.
                    skipped++;
                    continue;
                }

                measured++;

                if (required > available + 1f)
                {
                    tooNarrow.Add($"{GetPath(label.transform)} needs {required:0} but has {available:0} " +
                        $"('{label.text}')");
                }
            }

            UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);

            Expect("ui_text_fits", tooNarrow.Count == 0,
                tooNarrow.Count == 0
                    ? $"{measured} label(s) measured and all fit their rectangle ({skipped} laid out by a group)."
                    : $"{tooNarrow.Count} label(s) overflow: {string.Join("; ", tooNarrow.ToArray())}");
        }

        /// <summary>
        /// Measures a UI rectangle in canvas space by reading its world corners.
        ///
        /// The layout groups must have run for those corners to mean anything, so the caller is
        /// expected to have called <see cref="ForceLayout"/> first. Screen space is normalised against
        /// the canvas pixel rectangle and re-centred on the origin, so two panels can be compared
        /// whatever the window size or canvas scale is.
        /// </summary>
        private static Rect ToCanvasRect(RectTransform rect, Canvas canvas)
        {
            if (rect == null || canvas == null)
            {
                return new Rect();
            }

            // Measured in the canvas root local space. Going through screen pixels made every UI check
            // depend on the editor game view aspect: with a 2.3:1 view the same panel measured a
            // different size and position, which produced overlap failures that only existed on screen.
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            Vector2 min = canvasRect.InverseTransformPoint(corners[0]);
            Vector2 max = canvasRect.InverseTransformPoint(corners[2]);

            return new Rect(
                Mathf.Min(min.x, max.x),
                Mathf.Min(min.y, max.y),
                Mathf.Abs(max.x - min.x),
                Mathf.Abs(max.y - min.y));
        }

        /// <summary>
        /// No two HUD labels may overlap each other.
        ///
        /// The panel check only compares the six container rectangles, so two rows inside one panel can
        /// sit exactly on top of each other and still "pass". That is how the money readout and the
        /// stock readout ended up drawn over one another inside the top bar.
        /// </summary>
        private static void ScenarioLabelOverlap(Canvas canvas)
        {
            ForceLayout(canvas.GetComponent<RectTransform>());

            TMP_Text[] labels = canvas.GetComponentsInChildren<TMP_Text>(false);
            List<string> overlaps = new List<string>();
            int measured = 0;

            for (int a = 0; a < labels.Length; a++)
            {
                RectTransform first = labels[a].rectTransform;
                if (!labels[a].gameObject.activeInHierarchy || first.sizeDelta.sqrMagnitude <= 0.0001f)
                {
                    continue;
                }

                Rect firstRect = ToCanvasRect(first, canvas);
                measured++;

                for (int b = a + 1; b < labels.Length; b++)
                {
                    RectTransform second = labels[b].rectTransform;
                    if (!labels[b].gameObject.activeInHierarchy || second.sizeDelta.sqrMagnitude <= 0.0001f)
                    {
                        continue;
                    }

                    // A label inside another label's rect is a parent/child case, not an overlap.
                    if (second.IsChildOf(first) || first.IsChildOf(second))
                    {
                        continue;
                    }

                    Rect secondRect = ToCanvasRect(second, canvas);
                    if (firstRect.Overlaps(secondRect, true))
                    {
                        overlaps.Add($"{labels[a].name} overlaps {labels[b].name}");
                    }
                }
            }

            Expect("ui_labels_do_not_overlap", overlaps.Count == 0,
                overlaps.Count == 0
                    ? $"{measured} HUD label(s) checked and none sit on top of another."
                    : $"{overlaps.Count} overlap(s): {string.Join("; ", overlaps.GetRange(0, Mathf.Min(4, overlaps.Count)).ToArray())}");
        }

        /// <summary>
        /// Every HUD panel has to be fully inside the canvas.
        ///
        /// The overlap check only compares panels with each other, so a panel that collapsed to zero
        /// height or hung off the top edge passed it: nothing overlapped because nothing was there. That
        /// is how the top bar lost its raw material readout off the top of the screen.
        /// </summary>
        private static void ExpectPanelsInsideCanvas(string[] panelNames, Canvas canvas, RectTransform canvasRect)
        {
            Rect bounds = canvasRect.rect;

            List<string> outside = new List<string>();
            int checkedPanels = 0;

            for (int i = 0; i < panelNames.Length; i++)
            {
                RectTransform panel = FindRect(canvas.transform, panelNames[i]);
                if (panel == null || !panel.gameObject.activeInHierarchy)
                {
                    continue;
                }

                checkedPanels++;
                Rect rect = ToCanvasRect(panel, canvas);

                // A zero or near-zero size cannot show anything, so it counts as outside.
                if (rect.width < 8f || rect.height < 8f)
                {
                    outside.Add($"{panelNames[i]} has no usable size ({rect.width:0.#} x {rect.height:0.#})");
                    continue;
                }

                const float tolerance = 0.5f;
                bool inside = rect.xMin >= bounds.xMin - tolerance
                    && rect.xMax <= bounds.xMax + tolerance
                    && rect.yMin >= bounds.yMin - tolerance
                    && rect.yMax <= bounds.yMax + tolerance;

                if (!inside)
                {
                    outside.Add($"{panelNames[i]} {rect} is outside {bounds}");
                }
            }

            Expect("ui_panels_inside_canvas", outside.Count == 0,
                outside.Count == 0
                    ? $"all {checkedPanels} HUD panel(s) are fully visible inside {bounds.width:0}x{bounds.height:0}."
                    : string.Join("; ", outside.ToArray()));
        }

        /// <summary>Maps a world position onto centred canvas space.</summary>
        private static Vector2 ToCanvasPoint(Vector3 world, Canvas canvas)
        {
            Vector3 screen = RectTransformUtility.WorldToScreenPoint(null, world);
            Rect pixelRect = canvas.pixelRect;
            Vector2 reference = GetReferenceResolution(canvas);

            if (pixelRect.width < 1f || pixelRect.height < 1f)
            {
                return new Vector2(screen.x, screen.y);
            }

            return new Vector2(
                (screen.x - pixelRect.x) / pixelRect.width * reference.x - reference.x * 0.5f,
                (screen.y - pixelRect.y) / pixelRect.height * reference.y - reference.y * 0.5f);
        }

        /// <summary>
        /// Runs the layout system over the HUD, outermost first, so every rectangle holds the size and
        /// position it will have at runtime. Without this the layout groups leave their children at
        /// their authored size, which is zero for anything sized by a group.
        /// </summary>
        private static void ForceLayout(RectTransform root)
        {
            RectTransform[] all = root.GetComponentsInChildren<RectTransform>(true);

            // Sorting by depth puts parents before children, so a parent group sizes itself using the
            // preferred sizes of children that have already been resolved.
            Array.Sort(all, (a, b) => Depth(a).CompareTo(Depth(b)));

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null)
                {
                    continue;
                }

                LayoutRebuilder.ForceRebuildLayoutImmediate(all[i]);
                Canvas.ForceUpdateCanvases();
            }
        }

        private static int Depth(Transform transform)
        {
            int depth = 0;
            Transform current = transform.parent;
            while (current != null)
            {
                depth++;
                current = current.parent;
            }

            return depth;
        }

        /// <summary>The canvas reference resolution, for converting parent sizes into canvas space.</summary>
        private static Vector2 GetReferenceResolution(Canvas canvas)
        {
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            return scaler != null ? scaler.referenceResolution : new Vector2(1920f, 1080f);
        }

        private static RectTransform FindRect(Transform root, string name)
        {
            if (root.name == name && root is RectTransform rect)
            {
                return rect;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                RectTransform found = FindRect(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        // ---- scene contents ------------------------------------------------------------------

        /// <summary>
        /// Confirms the saved scene actually contains the kitchen rather than empty roots.
        ///
        /// Everything in the world is normally created at runtime, which left the Scene view blank.
        /// The preview authored at build time is what the editor shows, so it is counted here with the
        /// same renderer based approach the model checks use.
        /// </summary>
        private static void ScenarioSceneContents()
        {
            if (!File.Exists(KitchenSceneGenerator.ScenePath))
            {
                Record("scene_has_visible_kitchen", false, $"{KitchenSceneGenerator.ScenePath} does not exist.");
                return;
            }

            UnityEngine.SceneManagement.Scene scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                KitchenSceneGenerator.ScenePath,
                UnityEditor.SceneManagement.OpenSceneMode.Additive);

            int cells = 0;
            int floorParts = 0;
            int cookers = 0;
            int cookerParts = 0;
            int staff = 0;
            int staffParts = 0;
            int lights = 0;
            int cameras = 0;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name != "World")
                {
                    continue;
                }

                Transform gridRoot = roots[i].transform.Find("GridRoot");
                Transform cookerRoot = roots[i].transform.Find("CookerRoot");
                Transform staffRoot = roots[i].transform.Find("StaffRoot");

                cells = CountNamed(gridRoot, "Cell_");
                floorParts = CountRenderers(gridRoot) - cells;
                cookers = gridRoot != null ? CountChildren(cookerRoot) : 0;
                cookerParts = CountRenderers(cookerRoot);
                staff = CountChildren(staffRoot);
                staffParts = CountRenderers(staffRoot);
            }

            for (int i = 0; i < roots.Length; i++)
            {
                lights += roots[i].GetComponentsInChildren<Light>(true).Length;
                cameras += roots[i].GetComponentsInChildren<Camera>(true).Length;
            }

            UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);

            // The scene is authored from the first campaign level, while the harness drives the sandbox,
            // so the expected counts come from the level asset the scene was built from.
            LevelConfig sceneLevel = AssetDatabase.LoadAssetAtPath<LevelConfig>(KitchenDataAssetGenerator.LevelAssetPath)
                ?? runtime.Level;

            bool gridOk = cells == sceneLevel.Width * sceneLevel.Height && floorParts >= 1;
            bool cookersOk = cookers == sceneLevel.StartingCookers.Count && cookerParts >= cookers * 5;
            bool staffOk = staff == sceneLevel.StaffCount && staffParts >= staff * 6;
            bool sceneOk = lights >= 2 && cameras >= 1;

            Expect("scene_has_visible_kitchen", gridOk && cookersOk && staffOk && sceneOk,
                $"grid cells = {cells} (expected {sceneLevel.Width * sceneLevel.Height}), floor parts = {floorParts}, " +
                $"cookers = {cookers} built from {cookerParts} parts, chefs = {staff} built from {staffParts} parts, " +
                $"lights = {lights}, cameras = {cameras}.");

            // Booting must replace the authored preview rather than add to it. The count is the one
            // captured immediately after StartLevel, because later scenarios buy and remove cookers.
            Expect("runtime_replaces_preview_cleanly",
                bootCookerCount == runtime.Level.StartingCookers.Count && bootStaffCount == runtime.Level.StaffCount,
                $"right after boot the runtime held {bootCookerCount} cooker(s) and {bootStaffCount} chef(s), " +
                $"and the level defines {runtime.Level.StartingCookers.Count} and {runtime.Level.StaffCount}.");

            Expect("preview_removed_at_runtime", bootClearedPreview,
                bootClearedPreview
                    ? "the runtime deleted the preview container planted before boot."
                    : "the preview container survived boot, which would duplicate every cooker and chef.");
        }

        /// <summary>
        /// Describes a model as its sorted list of relative paths and component type names. Two
        /// objects with the same description were built by the same code, which is how an instance is
        /// matched back to the prefab it came from even though Instantiate leaves no prefab link.
        /// </summary>
        private static string DescribeHierarchy(GameObject root)
        {
            if (root == null)
            {
                return string.Empty;
            }

            List<string> entries = new List<string>();
            Component[] components = root.GetComponentsInChildren<Component>(true);

            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null || component is Transform)
                {
                    continue;
                }

                string path = RelativePath(root.transform, component.transform);
                if (root.GetComponent<CookerView>() != null && path.StartsWith("WorkPositionIndicator/", StringComparison.Ordinal))
                {
                    continue;
                }

                entries.Add(path + ":" + component.GetType().Name);
            }

            entries.Sort(StringComparer.Ordinal);
            return string.Join("|", entries.ToArray());
        }

        private static string RelativePath(Transform root, Transform target)
        {
            if (target == root)
            {
                return ".";
            }

            string path = target.name;
            Transform current = target.parent;
            while (current != null && current != root)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private static int CountNamed(Transform root, string prefix)
        {
            if (root == null)
            {
                return 0;
            }

            int count = 0;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountRenderers(Transform root)
        {
            return root == null ? 0 : root.GetComponentsInChildren<MeshRenderer>(true).Length;
        }

        private static int CountChildren(Transform root)
        {
            if (root == null)
            {
                return 0;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                if (root.GetChild(i).name == KitchenScenePreview.PreviewContainerName)
                {
                    return root.GetChild(i).childCount;
                }
            }

            return root.childCount;
        }

        // ---- art prefabs ---------------------------------------------------------------------

        /// <summary>
        /// Confirms the prefab assets exist and that the definitions point at them.
        ///
        /// Prefabs are how art enters the game, so an asset that exists on disk but is not assigned to
        /// its definition would mean editing it has no effect. Both halves are checked here.
        /// </summary>
        private static void ScenarioArtPrefabs()
        {
            string[] expected =
            {
                KitchenPrefabGenerator.FloorPrefabPath,
                KitchenPrefabGenerator.CellPrefabPath,
                $"{KitchenPrefabGenerator.StaffFolder}/Staff_Chef.prefab"
            };

            List<string> missingAssets = new List<string>();
            for (int i = 0; i < expected.Length; i++)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(expected[i]) == null)
                {
                    missingAssets.Add(expected[i]);
                }
            }

            IReadOnlyList<CookerDefinition> cookers = runtime.Level.Cookers;
            int cookersWithPrefab = 0;
            for (int i = 0; i < cookers.Count; i++)
            {
                if (cookers[i] == null)
                {
                    continue;
                }

                string path = $"{KitchenPrefabGenerator.CookerFolder}/Cooker_{cookers[i].Id}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    missingAssets.Add(path);
                }
                else if (cookers[i].ViewPrefab != null)
                {
                    cookersWithPrefab++;
                }
            }

            IReadOnlyList<ItemDefinition> items = runtime.Level.Items;
            int itemsWithPrefab = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null)
                {
                    continue;
                }

                string path = $"{KitchenPrefabGenerator.IngredientFolder}/Item_{items[i].Id}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    missingAssets.Add(path);
                }
                else if (items[i].ViewPrefab != null)
                {
                    itemsWithPrefab++;
                }
            }

            Expect("art_prefab_assets_exist", missingAssets.Count == 0,
                missingAssets.Count == 0
                    ? $"{expected.Length + cookers.Count + items.Count} prefab asset(s) are present."
                    : $"{missingAssets.Count} missing: {string.Join(", ", missingAssets.ToArray())}");

            bool levelLinked = runtime.Level.StaffPrefab != null && runtime.Level.FloorPrefab != null;
            Expect("art_prefabs_are_assigned",
                cookersWithPrefab == cookers.Count && itemsWithPrefab == items.Count && levelLinked,
                $"cookers linked to a prefab = {cookersWithPrefab}/{cookers.Count}, " +
                $"items linked = {itemsWithPrefab}/{items.Count}, " +
                $"chef prefab = {(runtime.Level.StaffPrefab != null ? runtime.Level.StaffPrefab.name : "<none>")}, " +
                $"floor prefab = {(runtime.Level.FloorPrefab != null ? runtime.Level.FloorPrefab.name : "<none>")}.");

            // A runtime Instantiate deliberately produces a plain clone with no live prefab
            // connection, which is what a shipped game wants: the prefab is a template, not a live
            // link. Comparing the component make-up of the instance with the prefab proves the view
            // was built from the prefab rather than from the primitive fallback.
            int viewsFromPrefabs = 0;
            int viewsFromPrimitives = 0;
            string detail = string.Empty;

            for (int i = 0; i < runtime.Cookers.Cookers.Count; i++)
            {
                CookerRuntime cooker = runtime.Cookers.Cookers[i];
                if (cooker.View == null || cooker.Definition == null)
                {
                    continue;
                }

                GameObject prefab = cooker.Definition.ViewPrefab;
                if (prefab != null && DescribeHierarchy(prefab) == DescribeHierarchy(cooker.View.gameObject))
                {
                    viewsFromPrefabs++;
                }
                else
                {
                    viewsFromPrimitives++;
                    if (detail.Length == 0)
                    {
                        detail = prefab == null
                            ? $"{cooker.Definition.name} has no prefab assigned"
                            : $"instance[{DescribeHierarchy(cooker.View.gameObject)}] prefab[{DescribeHierarchy(prefab)}]";
                    }
                }
            }

            Expect("runtime_views_come_from_prefabs", viewsFromPrimitives == 0 && viewsFromPrefabs > 0,
                viewsFromPrimitives == 0
                    ? $"all {viewsFromPrefabs} cooker view(s) were instantiated from their prefab."
                    : $"{viewsFromPrefabs} from a prefab, {viewsFromPrimitives} from primitives: {detail}");

            if (viewsFromPrimitives > 0 && runtime.Cookers.Cookers.Count > 0)
            {
                CookerRuntime first = runtime.Cookers.Cookers[0];
                if (first.View != null && first.Definition != null && first.Definition.ViewPrefab != null)
                {
                    string instanceText = DescribeHierarchy(first.View.gameObject);
                    string prefabText = DescribeHierarchy(first.Definition.ViewPrefab);
                    File.WriteAllText(
                        KitchenSceneGenerator.ToAbsolutePath("Logs/prefab_compare.txt"),
                        $"INSTANCE\n{instanceText.Replace("|", "\n")}\n\nPREFAB\n{prefabText.Replace("|", "\n")}\n");
                }
            }
        }

        // ---- materials -----------------------------------------------------------------------

        /// <summary>
        /// Confirms every generated view has a real material and that the material library resolved.
        ///
        /// A null material renders with Unity's magenta error shader, so this is the check that would
        /// have caught the prefabs serialising null materials.
        /// </summary>
        private static void ScenarioMaterials()
        {
            string[] folders =
            {
                KitchenPrefabGenerator.CookerFolder,
                KitchenPrefabGenerator.StaffFolder,
                KitchenPrefabGenerator.GridFolder,
                KitchenPrefabGenerator.IngredientFolder
            };

            List<string> missing = new List<string>();
            int checkedPrefabs = 0;
            int checkedRenderers = 0;

            string[] guids = AssetDatabase.FindAssets("t:Prefab", folders);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                checkedPrefabs++;
                MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);

                for (int r = 0; r < renderers.Length; r++)
                {
                    checkedRenderers++;

                    if (renderers[r] == null)
                    {
                        continue;
                    }

                    Material material = renderers[r].sharedMaterial;
                    if (material == null)
                    {
                        missing.Add($"{path}: {GetPath(renderers[r].transform)} has no material");
                        continue;
                    }

                    if (material.shader != null && material.shader.name == "Hidden/InternalErrorShader")
                    {
                        missing.Add($"{path}: {GetPath(renderers[r].transform)} uses the error shader");
                    }
                }
            }

            Expect("prefab_renderers_have_materials", missing.Count == 0,
                missing.Count == 0
                    ? $"{checkedRenderers} renderer(s) across {checkedPrefabs} prefab(s) all have a material."
                    : $"{missing.Count} renderer(s) have no usable material: " +
                      string.Join("; ", missing.GetRange(0, Mathf.Min(5, missing.Count)).ToArray()));

            // The library the runtime resolves materials from must exist and be populated.
            WhiteboxMaterialLibrary library = AssetDatabase.LoadAssetAtPath<WhiteboxMaterialLibrary>(
                KitchenMaterialGenerator.LibraryPath);

            int usable = 0;
            if (library != null)
            {
                for (int i = 0; i < library.Materials.Count; i++)
                {
                    Material material = library.Materials[i];
                    if (material != null && material.shader != null &&
                        material.shader.name != "Hidden/InternalErrorShader")
                    {
                        usable++;
                    }
                }
            }

            Expect("material_library_populated", library != null && usable >= 10,
                library == null
                    ? $"no material library at '{KitchenMaterialGenerator.LibraryPath}'"
                    : $"library holds {library.Materials.Count} material(s), {usable} usable.");

            // The views must resolve to those assets rather than the in-memory fallback.
            int withAssetMaterial = 0;
            for (int i = 0; i < runtime.Cookers.Cookers.Count; i++)
            {
                CookerRuntime cooker = runtime.Cookers.Cookers[i];
                if (cooker.View == null)
                {
                    continue;
                }

                MeshRenderer[] renderers = cooker.View.GetComponentsInChildren<MeshRenderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                {
                    Material material = renderers[r].sharedMaterial;
                    if (material != null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(material)))
                    {
                        withAssetMaterial++;
                        break;
                    }
                }
            }

            Expect("runtime_views_use_material_assets", withAssetMaterial == runtime.Cookers.Cookers.Count,
                $"{withAssetMaterial} of {runtime.Cookers.Cookers.Count} cooker view(s) use an asset material.");
        }

        // ---- campaign ------------------------------------------------------------------------

        /// <summary>
        /// Confirms the campaign exists and behaves: three level assets that chain together, a start
        /// screen registered first in the build settings, a level one that can only afford the two
        /// cookers it teaches, and an order list that ends instead of wrapping.
        /// </summary>
        private static void ScenarioCampaign()
        {
            // 1. Every level asset exists and chains to the next, ending at zero.
            List<LevelConfig> levels = new List<LevelConfig>();
            List<string> missing = new List<string>();

            for (int number = 1; number <= GameContentFactory.LevelCount; number++)
            {
                string path = $"{KitchenDataAssetGenerator.ResourcesRoot}/Level_{number:00}.asset";
                LevelConfig level = AssetDatabase.LoadAssetAtPath<LevelConfig>(path);
                if (level == null)
                {
                    missing.Add(path);
                }
                else
                {
                    levels.Add(level);
                }
            }

            Expect("campaign_levels_exist", missing.Count == 0,
                missing.Count == 0
                    ? $"{levels.Count} level asset(s) present."
                    : $"missing: {string.Join(", ", missing.ToArray())}");

            bool chained = levels.Count == GameContentFactory.LevelCount;
            string chain = string.Empty;
            for (int i = 0; i < levels.Count && chained; i++)
            {
                bool lastLevel = i == levels.Count - 1;
                int expectedNext = lastLevel ? 0 : levels[i].LevelNumber + 1;
                if (levels[i].NextLevelNumber != expectedNext || levels[i].LevelNumber != i + 1)
                {
                    chained = false;
                }

                chain += $"{levels[i].LevelNumber}->{levels[i].NextLevelNumber} ";
            }

            Expect("campaign_levels_chain", chained,
                $"chain = {chain.Trim()} (last level must point at 0).");

            // 2. The start screen exists and is the scene a build launches into.
            bool menuExists = File.Exists(KitchenSceneGenerator.MenuScenePath);
            string firstScene = string.Empty;
            EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
            if (buildScenes.Length > 0)
            {
                firstScene = buildScenes[0].path;
            }

            Expect("start_screen_is_first_scene",
                menuExists && firstScene == KitchenSceneGenerator.MenuScenePath,
                menuExists
                    ? $"first build scene = {firstScene}."
                    : $"{KitchenSceneGenerator.MenuScenePath} does not exist.");

            // 3. Level one's money buys exactly one chopping station and one assembly table.
            LevelConfig first = levels.Count > 0 ? levels[0] : null;
            if (first != null)
            {
                CookerDefinition chopping = first.FindCooker(GameContentFactory.Ids.ChoppingStation);
                CookerDefinition assembly = first.FindCooker(GameContentFactory.Ids.AssemblyTable);
                int exact = (chopping != null ? chopping.Price : 0) + (assembly != null ? assembly.Price : 0);

                Expect("level_one_budget_is_exact", first.StartingMoney == exact,
                    $"starting money = {first.StartingMoney}, chopping {chopping?.Price} + assembly {assembly?.Price} = {exact}.");

                // The first level teaches salads, so it must not offer meat or bread at all.
                bool noMeat = first.FindItem(GameContentFactory.Ids.RawMeat) == null
                    && first.FindItem(GameContentFactory.Ids.Bread) == null;
                bool hasSalad = first.FindItem(GameContentFactory.Ids.Salad) != null;
                int cookerCount = first.Cookers.Count;

                Expect("level_one_is_salad_only", noMeat && hasSalad && cookerCount == 2,
                    $"meat/bread absent = {noMeat}, salad present = {hasSalad}, cookers = {cookerCount} (expected 2).");

                // Nothing is pre-placed in level one: placing the cookers is the lesson.
                Expect("level_one_starts_empty", first.StartingCookers.Count == 0 && first.StaffCount == 1,
                    $"pre-placed cookers = {first.StartingCookers.Count}, staff = {first.StaffCount}.");
            }

            // 4. Later levels grow: the pan arrives in 2, the washer in 3.
            if (levels.Count >= 3)
            {
                bool panInSecond = levels[1].FindCooker(GameContentFactory.Ids.FryingPan) != null;
                bool washerInThird = levels[2].FindCooker(GameContentFactory.Ids.DishWasher) != null;
                bool washerNotInSecond = levels[1].FindCooker(GameContentFactory.Ids.DishWasher) == null;
                bool staffGrows = levels[0].StaffCount < levels[1].StaffCount &&
                                  levels[1].StaffCount < levels[2].StaffCount;

                Expect("levels_unlock_progressively",
                    panInSecond && washerInThird && washerNotInSecond && staffGrows,
                    $"pan in level 2 = {panInSecond}, washer in level 3 = {washerInThird}, " +
                    $"washer absent from level 2 = {washerNotInSecond}, staff grows = {staffGrows} " +
                    $"({levels[0].StaffCount} -> {levels[1].StaffCount} -> {levels[2].StaffCount}).");
            }

            // 5. The order list ends. Serving every remaining order must clear the level exactly once
            //    and leave no order in flight, instead of wrapping around forever.
            if (runtime != null && runtime.Orders != null)
            {
                ItemDefinition cleanPlate = runtime.CleanPlate;
                int guard = 0;

                while (!runtime.Orders.IsLevelCleared && guard++ < 12)
                {
                    OrderManager.OrderStatus active = runtime.Orders.Current;
                    if (active == null || active.Dish == null || cleanPlate == null)
                    {
                        break;
                    }

                    ResetInventory();
                    runtime.GrantItem(active.Dish, active.DishRequired);
                    runtime.GrantItem(cleanPlate, active.PlatesRequired);

                    if (!runtime.TrySubmitOrder())
                    {
                        break;
                    }
                }

                bool cleared = runtime.Orders.IsLevelCleared && runtime.Orders.Current == null;
                bool servedAll = runtime.Orders.CompletedOrders == runtime.Orders.OrderCount;

                Expect("order_list_ends_and_clears_level", cleared && servedAll,
                    $"level cleared = {cleared}, orders in level = {runtime.Orders.OrderCount}, " +
                    $"completed = {runtime.Orders.CompletedOrders}, order still active = {runtime.Orders.Current != null}.");

                // A cleared level must not accept another delivery.
                bool refused = !runtime.TrySubmitOrder();
                Expect("cleared_level_rejects_delivery", refused,
                    $"submitting after the level cleared was refused = {refused}.");
            }
        }

        // ---- 2/21. camera framing ------------------------------------------------------------

        /// <summary>
        /// Confirms the view is a raised 3D perspective top down camera and that the whole map is
        /// inside the frustum.
        ///
        /// It reads the Main Camera that was actually written to Kitchen.unity rather than the
        /// stand-in camera the rest of the harness uses, because the scene camera is the one the
        /// player looks through.
        /// </summary>
        private static void ScenarioCameraFraming()
        {
            if (!File.Exists(KitchenSceneGenerator.ScenePath))
            {
                Record("camera_perspective_3d", false, $"{KitchenSceneGenerator.ScenePath} does not exist.");
                return;
            }

            UnityEngine.SceneManagement.Scene scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                KitchenSceneGenerator.ScenePath,
                UnityEditor.SceneManagement.OpenSceneMode.Additive);

            Camera camera = KitchenSceneBuilder.FindSceneMainCamera(scene);

            // The additive handle can come back without roots when the scene was already open, so the
            // loaded cameras are searched as a fallback instead of reporting a missing camera.
            if (camera == null)
            {
                Camera[] loaded = UnityEngine.Object.FindObjectsOfType<Camera>(true);
                for (int i = 0; i < loaded.Length; i++)
                {
                    if (loaded[i] != null && loaded[i].CompareTag("MainCamera"))
                    {
                        camera = loaded[i];
                        break;
                    }
                }
            }

            bool perspective = false;
            bool raised = false;
            bool aboveGround = false;
            bool looksDown = false;
            float pitchDegrees = 0f;
            float fieldOfView = 0f;
            float projectionAspect = 0f;
            Vector3 position = Vector3.zero;

            if (camera != null)
            {
                perspective = !camera.orthographic && camera.fieldOfView > 1f && camera.fieldOfView < 179f;
                pitchDegrees = camera.transform.rotation.eulerAngles.x;
                fieldOfView = camera.fieldOfView;
                projectionAspect = camera.aspect;
                position = camera.transform.position;
                raised = pitchDegrees > 20f && pitchDegrees < 89f;
                aboveGround = position.y > 1f;
                looksDown = Vector3.Dot(camera.transform.forward, Vector3.down) > 0.2f;
            }

            UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);

            Expect("camera_perspective_3d", camera != null && perspective && raised && aboveGround && looksDown,
                camera == null
                    ? "no Main Camera was found in Kitchen.unity."
                    : $"projection = {(perspective ? "perspective" : "orthographic")}, fieldOfView = {fieldOfView:0.#}, " +
                      $"pitch = {pitchDegrees:0.#} deg, position = {position}, " +
                      $"height above the floor = {position.y - runtime.Level.GroundHeight:0.#}, looking down = {looksDown}.");

            // The projected aspect saved in the scene must match the one the framing was computed for,
            // otherwise the baked distance would not correspond to the projection it was solved for.
            // The camera component reports the live game view aspect, which is an editor window setting
            // rather than a property of the scene, so asserting that it equals the authored framing aspect
            // failed whenever the game view was not 4:3. What matters is that the whole map still fits at
            // the aspect the camera is actually being viewed with.
            bool aspectFits = false;
            float actualAspect = projectionAspect > 0.01f ? projectionAspect : KitchenSceneBuilder.CameraFramingAspect;

            if (camera != null && runtime.Level != null)
            {
                KitchenSceneBuilder.CameraFraming live =
                    KitchenSceneBuilder.ComputeFraming(runtime.Level, KitchenSceneBuilder.DefaultCameraPitch, actualAspect);

                List<KitchenSceneBuilder.CornerProjection> liveCorners =
                    KitchenSceneBuilder.ProjectMapCorners(
                        live, runtime.Level, KitchenSceneBuilder.CameraViewportMargin, actualAspect);

                aspectFits = true;

                for (int c = 0; c < liveCorners.Count; c++)
                {
                    if (!liveCorners[c].InsideViewport(KitchenSceneBuilder.CameraViewportMargin))
                    {
                        aspectFits = false;
                        break;
                    }
                }
            }

            Expect("camera_projection_aspect", aspectFits,
                $"camera aspect in use = {projectionAspect:0.000} " +
                $"(authored framing aspect {KitchenSceneBuilder.CameraFramingAspect:0.000}), " +
                $"the whole map fits at that aspect = {aspectFits}.");

            // Fitting is computed from the same framing maths the generator uses, across the window
            // shapes the game can actually run in.
            float[] aspects = { 21f / 9f, 16f / 9f, 4f / 3f, 1f };
            List<string> failures = new List<string>();

            for (int i = 0; i < aspects.Length; i++)
            {
                KitchenSceneBuilder.CameraFraming framing =
                    KitchenSceneBuilder.ComputeFraming(runtime.Level, KitchenSceneBuilder.DefaultCameraPitch, aspects[i]);

                List<KitchenSceneBuilder.CornerProjection> corners =
                    KitchenSceneBuilder.ProjectMapCorners(
                        framing, runtime.Level, KitchenSceneBuilder.CameraViewportMargin, aspects[i]);

                for (int c = 0; c < corners.Count; c++)
                {
                    if (!corners[c].InsideViewport(KitchenSceneBuilder.CameraViewportMargin))
                    {
                        failures.Add($"aspect {aspects[i]:0.00}: corner {corners[c].World} projects to " +
                            $"({corners[c].Viewport.x:0.000}, {corners[c].Viewport.y:0.000})");
                    }
                }
            }

            Expect("camera_fits_map", failures.Count == 0,
                failures.Count == 0
                    ? $"all four map corners are inside the view frustum at {string.Join(", ", Array.ConvertAll(aspects, a => a.ToString("0.00")))}."
                    : string.Join("; ", failures.ToArray()));
        }

        // ---- 2. grid -------------------------------------------------------------------------

        private static void ScenarioGrid()
        {
            Kitchen.Grid.GridManager grid = runtime.Grid;
            const int expected = 25;
            Expect("grid_5x5", grid.Width == 5 && grid.Height == 5 && grid.CellCount == expected,
                $"{grid.Width}x{grid.Height}, {grid.CellCount} cells (expected 5x5, {expected}).");

            Vector3 origin = grid.GridToWorld(new Vector2Int(0, 0));
            Vector3 far = grid.GridToWorld(new Vector2Int(4, 4));
            Expect("grid_origin_world", Near(origin, new Vector3(-4f, 0f, -4f)) && Near(far, new Vector3(4f, 0f, 4f)),
                $"cell(0,0)={origin} cell(4,4)={far} (expected (-4,0,-4) and (4,0,4)).");

            Vector3 centre = grid.GridToWorld(new Vector2Int(2, 2));
            Expect("grid_centre_aligned", Near(centre, Vector3.zero), $"centre cell world position = {centre}.");

            bool roundTrip = true;
            for (int x = 0; x < 5; x++)
            {
                for (int y = 0; y < 5; y++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (grid.WorldToGrid(grid.GridToWorld(cell)) != cell)
                    {
                        roundTrip = false;
                    }
                }
            }

            Expect("grid_round_trip", roundTrip, "every cell survives GridToWorld -> WorldToGrid.");

            // Even extents cannot put a cell centre on the map centre, because the centre falls on a
            // cell boundary. What must hold is that the map itself is centred: the midpoint of the
            // first and last cell is the map centre, and all cells are evenly spaced around it.
            // The odd case must place a cell exactly on the centre. It is sampled while the default
            // 5x5 grid is still built, before the even grid replaces it below.
            Vector3 oddCentre = grid.GridToWorld(new Vector2Int(2, 2));
            Expect("grid_odd_centre_cell", Near(oddCentre, Vector3.zero),
                $"the middle cell of a 5x5 map sits at {oddCentre}.");

            LevelConfig even = ScriptableObject.CreateInstance<LevelConfig>();
            even.Setup(4, 4, 2f, Vector3.zero, 0f, 0, null, null, 1, null, null, null, null, null);
            grid.BuildGrid(even);
            Vector3 evenFirst = grid.GridToWorld(new Vector2Int(0, 0));
            Vector3 evenLast = grid.GridToWorld(new Vector2Int(3, 3));
            Vector3 evenMidpoint = (evenFirst + evenLast) * 0.5f;
            bool evenCentred = Near(evenMidpoint, Vector3.zero) && Near(evenFirst, new Vector3(-3f, 0f, -3f));
            Expect("grid_even_alignment", evenCentred,
                $"4x4 first cell = {evenFirst}, last cell = {evenLast}, midpoint = {evenMidpoint} (expected (-3,0,-3), (3,0,3), (0,0,0)).");

            UnityEngine.Object.DestroyImmediate(even);

            grid.BuildGrid(runtime.Level);

            // BuildGrid creates empty cells, so the cookers and chefs that were already placed have to
            // be registered on the fresh grid again.
            grid.RebuildOccupancyFromManagers();
            Expect("grid_rebuild", grid.CellCount == expected, $"grid restored to {grid.CellCount} cells.");
        }

        // ---- 3/4/5. staff --------------------------------------------------------------------

        private static void ScenarioStaff()
        {
            Expect("staff_three", runtime.AllStaff.Count == 3, $"{runtime.AllStaff.Count} staff created (expected 3).");

            HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();
            bool unique = true;
            for (int i = 0; i < runtime.AllStaff.Count; i++)
            {
                if (!occupied.Add(runtime.AllStaff[i].GridPosition))
                {
                    unique = false;
                }
            }

            Expect("staff_no_overlap_at_start", unique, "no two staff share a start cell.");

            StaffRuntime staff = runtime.AllStaff[0];
            runtime.HandleStaffClicked(staff);
            bool selected = runtime.Staff.Selected == staff && staff.IsSelected && staff.View != null && staff.View.gameObject.activeSelf;
            Expect("staff_select", selected, $"selected = {(runtime.Staff.Selected != null ? runtime.Staff.Selected.DisplayName : "<none>")}, view present = {staff.View != null}.");

            // The two blocking rules are exercised on deliberately placed chefs so the outcome does
            // not depend on the level's spawn offsets.
            StaffRuntime mover = runtime.AllStaff[0];
            StaffRuntime blocker = runtime.AllStaff[1];
            StaffRuntime spare = runtime.AllStaff[2];

            Relocate(mover, new Vector2Int(2, 2));
            Relocate(blocker, new Vector2Int(2, 3));
            Relocate(spare, new Vector2Int(0, 2));
            runtime.HandleStaffClicked(mover);

            bool blocked = !runtime.TryMoveSelectedStaff(new Vector2Int(0, 1))
                && mover.GridPosition == new Vector2Int(2, 2);
            Expect("staff_blocked_by_staff", blocked,
                $"chef {mover.Id} at {mover.GridPosition} refused to step onto chef {blocker.Id} at {blocker.GridPosition}.");

            bool shifted = runtime.TryMoveSelectedStaff(new Vector2Int(1, 0))
                && mover.GridPosition == new Vector2Int(3, 2)
                && runtime.Grid.GetStaffId(new Vector2Int(3, 2)) == mover.Id;
            Expect("staff_wasd_move", shifted,
                $"a legal step moved chef {mover.Id} to {mover.GridPosition}; the grid reports staff {runtime.Grid.GetStaffId(mover.GridPosition)}.");

            // A second chef must not be able to enter the cell the first one now occupies.
            Relocate(blocker, new Vector2Int(2, 2));
            runtime.HandleStaffClicked(blocker);
            bool followUpBlocked = !runtime.TryMoveSelectedStaff(new Vector2Int(1, 0))
                && blocker.GridPosition == new Vector2Int(2, 2);
            Expect("staff_no_overlap_enforced", followUpBlocked,
                $"chef {blocker.Id} refused to enter {mover.GridPosition}, already taken by chef {mover.Id}.");

            runtime.HandleStaffClicked(mover);

            // Out of bounds is refused: chef 0 is placed on the border for this check.
            Relocate(mover, new Vector2Int(0, 1));
            runtime.HandleStaffClicked(mover);
            bool refusedEdge = !runtime.TryMoveSelectedStaff(new Vector2Int(-1, 0))
                && mover.GridPosition == new Vector2Int(0, 1);
            Expect("staff_out_of_bounds", refusedEdge,
                "a step from (0,1) towards (-1,1) was refused and the chef stayed put.");

            // Move to a free cell so the arrival check below has real travel to perform.
            Relocate(blocker, new Vector2Int(2, 3));
            Relocate(mover, new Vector2Int(2, 2));
            runtime.HandleStaffClicked(mover);
            runtime.TryMoveSelectedStaff(new Vector2Int(0, -1));

            // Smooth movement: the model must reach the centre of its cell.
            Advance(1.5f);
            Vector3 expectedWorld = runtime.Grid.GridToWorld(staff.GridPosition);
            bool arrived = !staff.IsMoving && Near(staff.WorldPosition, expectedWorld);
            Expect("staff_arrives_at_centre", arrived,
                $"staff world {staff.WorldPosition} vs cell centre {expectedWorld}, moving = {staff.IsMoving}.");
        }

        // ---- 6/7/8. building -----------------------------------------------------------------

        private static void ScenarioBuild()
        {
            runtime.Grid.ClearHighlights();

            CookerDefinition chopping = runtime.FindCookerDefinition(GameContentFactory.Ids.ChoppingStation);
            CookerDefinition frying = runtime.FindCookerDefinition(GameContentFactory.Ids.FryingPan);
            CookerDefinition assembly = runtime.FindCookerDefinition(GameContentFactory.Ids.AssemblyTable);
            CookerDefinition washer = runtime.FindCookerDefinition(GameContentFactory.Ids.DishWasher);
            Expect("build_four_cooker_types", chopping != null && frying != null && assembly != null && washer != null,
                "the catalogue exposes chopping station, frying pan, assembly table and dish washer.");

            // Legal placement charges exactly the catalogue price.
            Vector2Int freeCell = FindFreeCookerCell(1, 1);
            int moneyBefore = runtime.Money;
            bool placed = runtime.TryBuildCooker(chopping, freeCell);
            CookerRuntime built = placed ? runtime.Cookers.GetCookerAt(freeCell) : null;
            bool charged = built != null && runtime.Money == moneyBefore - chopping.Price;
            Expect("build_charge", charged,
                $"money {moneyBefore} -> {runtime.Money}, price {chopping.Price}, cooker placed = {built != null}.");

            Expect("build_occupancy", built != null && runtime.Grid.GetCookerId(freeCell) == built.Id,
                $"grid cell {freeCell} now reports cooker {runtime.Grid.GetCookerId(freeCell)}.");

            // Illegal placement: the cell is taken, so nothing may be charged and nothing created.
            int moneyBeforeFail = runtime.Money;
            int cookersBeforeFail = runtime.Cookers.Cookers.Count;
            bool refused = runtime.TryBuildCooker(frying, freeCell);
            bool noCharge = !refused && runtime.Money == moneyBeforeFail && runtime.Cookers.Cookers.Count == cookersBeforeFail;
            Expect("build_refused_occupied", noCharge,
                $"money stayed {runtime.Money}, cookers stayed {runtime.Cookers.Cookers.Count}.");

            // Illegal placement: outside the grid.
            int moneyBeforeOutside = runtime.Money;
            bool outside = runtime.TryBuildCooker(frying, new Vector2Int(-1, 9));
            Expect("build_refused_out_of_bounds", !outside && runtime.Money == moneyBeforeOutside,
                $"money stayed {runtime.Money}.");

            // Illegal placement: not enough money.
            int savedMoney = runtime.Money;
            int cookersBeforeBroke = runtime.Cookers.Cookers.Count;
            SetMoney(0);
            bool broke = runtime.TryBuildCooker(frying, FindFreeCookerCell(3, 3));
            bool refusedBroke = !broke && runtime.Money == 0 && runtime.Cookers.Cookers.Count == cookersBeforeBroke;
            SetMoney(savedMoney);
            Expect("build_refused_no_money", refusedBroke, "a cooker was refused while the balance was 0 and nothing was charged.");

            // Preview legality comes from the same rule the build uses.
            GameObject previewObject = new GameObject("PreviewProbe");
            PlacementPreview preview = previewObject.AddComponent<PlacementPreview>();
            preview.Show(frying, runtime.Grid.GridToWorld(freeCell), freeCell);
            preview.SetValid(runtime.Grid.IsFreeForCooker(freeCell), freeCell);
            bool previewInvalid = preview.IsVisible && !preview.IsValid;
            preview.Show(frying, runtime.Grid.GridToWorld(FindFreeCookerCell(0, 0)), Vector2Int.zero);
            preview.SetValid(true, Vector2Int.zero);
            bool previewValid = preview.IsValid;
            UnityEngine.Object.DestroyImmediate(previewObject);
            Expect("build_preview_validity", previewInvalid && previewValid,
                $"occupied cell showed invalid = {previewInvalid}, free cell showed valid = {previewValid}.");

            // Cancelling build mode costs nothing.
            InputManager input = runtime.transform.GetComponent<InputManager>();
            int moneyBeforeCancel = runtime.Money;
            input.RequestBuildCooker(frying);
            bool entered = input.Mode == Kitchen.Core.InteractionMode.Build;
            input.CancelCurrentInteraction();
            bool cancelled = input.Mode == Kitchen.Core.InteractionMode.Normal && runtime.Money == moneyBeforeCancel && runtime.Cookers.Cookers.Count == cookersBeforeFail;
            Expect("build_cancel_free", entered && cancelled,
                $"entered build = {entered}, cancelled back to {input.Mode}, money unchanged = {runtime.Money == moneyBeforeCancel}.");

            // Moving a cooker keeps grid occupancy consistent.
            CookerRuntime mover = runtime.Cookers.Cookers[0];
            Vector2Int from = mover.GridPosition;
            Vector2Int to = FindFreeCookerCell(2, 4);
            bool freeBefore = runtime.Grid.IsFreeForCooker(to, mover.Id);
            int cookerCountBefore = runtime.Cookers.Cookers.Count;
            bool moveAccepted = runtime.Cookers.TryMoveCooker(mover, to);
            bool moved = moveAccepted && mover.GridPosition == to && runtime.Grid.GetCookerId(to) == mover.Id && !runtime.Grid.HasCooker(from);
            Expect("cooker_move_occupancy", moved,
                $"freeBefore={freeBefore} accepted={moveAccepted} runtime={mover.GridPosition} gridAtTarget={runtime.Grid.GetCookerId(to)} gridAtSource={runtime.Grid.GetCookerId(from)} cookers={cookerCountBefore}.");
            Expect("cooker_move_trace", moved, $"trace: {runtime.Grid.LastMoveTrace}");
        }

        // ---- 9/10. chopping ------------------------------------------------------------------

        /// <summary>
        /// The build highlight must follow the pointer instead of accumulating.
        ///
        /// Moving off a cell, hovering the UI and leaving build mode each have to restore the cells
        /// behind them. A stale green cell reads as a stuck selection, which is exactly the bug this
        /// check exists to prevent from coming back.
        /// </summary>
        private static void ScenarioBuildHighlight()
        {
            if (runtime == null || runtime.Grid == null || runtime.Grid.View == null || runtime.Input == null)
            {
                Record("build_highlight_follows_pointer", false, "the grid view or input manager is missing.");
                return;
            }

            GridView view = runtime.Grid.View;
            CookerDefinition definition = runtime.FindCookerDefinition(GameContentFactory.Ids.ChoppingStation);
            if (definition == null)
            {
                Record("build_highlight_follows_pointer", false, "no chopping station definition.");
                return;
            }

            // Build mode refuses to start when the cooker is unaffordable, so the balance is topped up
            // for the duration of this check and restored afterwards.
            int moneyBefore = runtime.Money;
            if (moneyBefore < definition.Price)
            {
                runtime.AddMoney(definition.Price - moneyBefore);
            }

            runtime.Input.RequestBuildCooker(definition);

            Vector2Int first = new Vector2Int(1, 1);
            Vector2Int second = new Vector2Int(2, 1);

            runtime.Input.UpdateBuildPreview(first);
            bool firstShown = view.IsHighlighted(first) && view.HighlightedCount == 1;

            // Moving to another cell must replace the previous highlight rather than add to it.
            runtime.Input.UpdateBuildPreview(second);
            bool moved = view.IsHighlighted(second) && !view.IsHighlighted(first);
            bool single = view.HighlightedCount == 1;

            // Leaving build mode must clear the last cell, which is the Escape path.
            runtime.Input.ExitBuildMode();
            bool cleared = view.HighlightedCount == 0 && !view.IsHighlighted(second);

            Expect("build_highlight_follows_pointer", firstShown && moved && single && cleared,
                $"first cell shown = {firstShown}, moved to the second = {moved}, " +
                $"one cell at a time = {single}, cleared on exit = {cleared} " +
                $"(highlighted now = {view.HighlightedCount}, mode = {runtime.Input.Mode}, " +
                $"money = {runtime.Money}, price = {definition.Price}).");

            // Leave nothing behind for the scenarios that follow.
            view.ClearHighlights();
            runtime.AddMoney(moneyBefore - runtime.Money);
        }

        /// <summary>
        /// A supported station must keep processing, and the player must be able to choose what it
        /// processes.
        ///
        /// Both were reported from play: a station stopped after one item, and with cabbage and tomato
        /// both in stock the player could not steer it.
        /// </summary>
        private static void ScenarioCookerControls()
        {
            CookerRuntime cooker = FindCooker(GameContentFactory.Ids.ChoppingStation);
            if (cooker == null)
            {
                Record("cooker_keeps_working", false, "no chopping station found.");
                return;
            }

            // --- a station keeps going instead of stopping after one unit -----------------------
            ResetInventory();
            runtime.GrantItem(Item(GameContentFactory.Ids.Cabbage), 2);
            cooker.ClearOutputs();
            cooker.PreferredRecipeId = GameContentFactory.Ids.ChopCabbage;

            MoveStaffTo(0, cooker.GridPosition);

            // 10 cuts at 0.3 s is 3 s per slice; 9 s covers two with margin.
            Advance(9f);

            ItemDefinition sliceItem = Item(GameContentFactory.Ids.CabbageSlice);
            int held = cooker.GetOutputCount(sliceItem);
            int stocked = runtime.Inventory.TotalCount(sliceItem);
            bool keptWorking = ProducedBy(cooker, sliceItem) >= 2;
            Expect("cooker_keeps_working", keptWorking,
                $"after 9 s with 2 cabbages the station produced {held} slice(s) held and {stocked} in stock " +
                $"(expected 2; it used to stop after the first because its output slot was full).");

            // --- the chosen recipe is the one that runs ----------------------------------------
            ResetInventory();
            runtime.GrantItem(Item(GameContentFactory.Ids.Cabbage), 1);
            runtime.GrantItem(Item(GameContentFactory.Ids.Tomato), 1);
            cooker.ClearOutputs();
            cooker.ForgetLock();
            cooker.PreferredRecipeId = GameContentFactory.Ids.ChopTomato;

            Advance(0.5f);

            bool lockedTomato = cooker.LockedRecipe != null &&
                cooker.LockedRecipe.Id == GameContentFactory.Ids.ChopTomato;
            Expect("recipe_choice_is_honoured", lockedTomato,
                $"pinned '{GameContentFactory.Ids.ChopTomato}', locked recipe = " +
                $"{(cooker.LockedRecipe != null ? cooker.LockedRecipe.Id : "<none>")} " +
                $"while cabbage was also available.");

            // --- clearing the choice falls back to automatic selection -------------------------
            cooker.ForgetLock();
            cooker.PreferredRecipeId = null;
            Advance(0.5f);

            bool automatic = cooker.LockedRecipe != null;
            Expect("recipe_choice_can_be_cleared", automatic,
                $"with no pin the station locked {(cooker.LockedRecipe != null ? cooker.LockedRecipe.Id : "<none>")} automatically.");

            cooker.ForgetLock();
            cooker.PreferredRecipeId = null;
            cooker.ClearOutputs();
            ParkAllStaff();
        }

        /// <summary>
        /// The HUD scale control must actually change the canvas reference resolution, and must clamp so
        /// the HUD cannot be scaled into uselessness.
        /// </summary>
        private static void ScenarioHudScale(Canvas canvas)
        {
            if (canvas == null)
            {
                Record("hud_scale_changes_canvas", false, "the scene has no Canvas.");
                return;
            }

            UnityEngine.UI.CanvasScaler scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            if (scaler == null)
            {
                Record("hud_scale_changes_canvas", false, "the canvas has no CanvasScaler.");
                return;
            }

            Vector2 before = scaler.referenceResolution;
            float savedScale = PlayerPrefs.GetFloat(Kitchen.UI.HudScaler.PrefsKey, 1f);

            // The scaler has to be on the canvas in the saved scene. Adding one here would hide a broken
            // wiring, which is exactly how the scale buttons ended up doing nothing in play.
            Kitchen.UI.HudScaler hudScaler = canvas.GetComponent<Kitchen.UI.HudScaler>();
            if (hudScaler == null)
            {
                Record("hud_scale_changes_canvas", false,
                    "the canvas in the scene has no HudScaler component, so the +/- buttons cannot work.");
                return;
            }

            hudScaler.Configure(canvas, before);
            hudScaler.SetScale(1f, false);

            hudScaler.Step(0.2f);
            Vector2 bigger = scaler.referenceResolution;

            hudScaler.Step(-0.4f);
            Vector2 smaller = scaler.referenceResolution;

            // A larger HUD scale means a smaller reference resolution, and vice versa.
            bool changed = bigger.x < before.x && smaller.x > before.x;

            // The clamp has to hold, otherwise the HUD can be scaled into uselessness.
            hudScaler.SetScale(99f, false);
            bool clampedHigh = hudScaler.Scale <= Kitchen.UI.HudScaler.MaxScale + 0.001f;
            hudScaler.SetScale(-99f, false);
            bool clampedLow = hudScaler.Scale >= Kitchen.UI.HudScaler.MinScale - 0.001f;

            Expect("hud_scale_changes_canvas", changed && clampedHigh && clampedLow,
                $"reference {before.x:0}x{before.y:0} -> bigger HUD {bigger.x:0} -> smaller HUD {smaller.x:0}, " +
                $"clamped at both ends = {clampedHigh && clampedLow}.");

            // Restore whatever the player had.
            hudScaler.SetScale(savedScale, false);
            scaler.referenceResolution = before;
            PlayerPrefs.SetFloat(Kitchen.UI.HudScaler.PrefsKey, savedScale);
            PlayerPrefs.Save();
        }

        private static void ScenarioChopping()
        {
            CookerRuntime cooker = FindCooker(GameContentFactory.Ids.ChoppingStation);
            if (cooker == null)
            {
                Record("chop_station_present", false, "no chopping station found.");
                return;
            }

            ResetInventory();
            runtime.GrantItem(Item(GameContentFactory.Ids.Cabbage), 2);
            ParkAllStaff();

            Advance(1.0f);
            int idleTicks = cooker.WorkTicks;
            Expect("cooker_idle_without_staff", !cooker.IsActive && idleTicks == 0 && cooker.LockedRecipe == null,
                $"inactive = {!cooker.IsActive}, ticks = {idleTicks}, recipe = {(cooker.LockedRecipe != null ? cooker.LockedRecipe.name : "<none>")}.");

            MoveStaffTo(0, cooker.GridPosition);
            Advance(0.4f);
            Expect("cooker_active_with_staff", cooker.IsActive && cooker.LockedRecipe != null,
                $"active = {cooker.IsActive}, recipe = {(cooker.LockedRecipe != null ? cooker.LockedRecipe.name : "<none>")}.");

            Advance(1.5f);
            int partialTicks = cooker.WorkTicks;
            float partialProgress = cooker.WorkProgress;
            bool rate = partialTicks >= 1 && partialTicks <= 9;
            Expect("chop_rate_0_3s", rate, $"after about 1.9 s the station had cut {partialTicks} of 10 times (0.3 s per cut).");

            // Leaving must pause the work and keep the partial progress.
            MoveStaffTo(0, FindFreeStaffCell(cooker.GridPosition + new Vector2Int(0, -1)));
            Advance(0.1f);
            bool pausedFlag = !cooker.IsActive;
            Advance(1.5f);
            bool frozen = cooker.WorkTicks == partialTicks;
            Expect("chop_pause_preserves_progress", pausedFlag && frozen,
                $"active = {cooker.IsActive}, ticks stayed {partialTicks} -> {cooker.WorkTicks}, progress kept at {cooker.WorkProgress:0.00} (was {partialProgress:0.00}).");

            // Returning resumes and, after enough time, finishes.
            MoveStaffTo(0, cooker.GridPosition);
            Advance(0.2f);
            bool resumed = cooker.IsActive && cooker.WorkTicks >= partialTicks;
            Advance(3.5f);
            bool produced = ProducedBy(cooker, Item(GameContentFactory.Ids.CabbageSlice)) > 0;
            Expect("chop_resumes_and_completes", resumed && produced,
                $"resumed = {resumed}, output = {Describe(runtime.Cookers.DescribeOutputs(cooker))}.");

            runtime.Cookers.TryCollectCookerOutput(cooker);
            // Produce reaches the kitchen exactly once: collecting again must not duplicate it.
            runtime.Cookers.TryCollectCookerOutput(cooker);
            int slicesInStock = runtime.Inventory.TotalCount(Item(GameContentFactory.Ids.CabbageSlice));

            Expect("chop_collect", slicesInStock == 1,
                $"the kitchen holds {slicesInStock} cabbage slice(s) after one chop and one collect " +
                "(expected exactly 1).");
        }

        // ---- 11. frying ----------------------------------------------------------------------

        private static void ScenarioFrying()
        {
            CookerRuntime cooker = FindCooker(GameContentFactory.Ids.FryingPan);
            if (cooker == null)
            {
                Record("fry_pan_present", false, "no frying pan found.");
                return;
            }

            ResetInventory();
            runtime.GrantItem(Item(GameContentFactory.Ids.RawMeatSlice), 1);
            ParkAllStaff();
            MoveStaffTo(0, cooker.GridPosition);

            Advance(2.5f);
            bool halfway = cooker.WorkProgress > 0.3f && cooker.WorkProgress < 0.8f && ProducedBy(cooker, Item(GameContentFactory.Ids.CookedMeatSlice)) == 0;
            Advance(3.5f);
            bool done = ProducedBy(cooker, Item(GameContentFactory.Ids.CookedMeatSlice)) > 0;
            Expect("fry_5_seconds", halfway && done,
                $"halfway progress = {(halfway ? "about 50%" : "unexpected")}, output = {Describe(runtime.Cookers.DescribeOutputs(cooker))}.");
        }

        // ---- 12. assembly --------------------------------------------------------------------

        private static void ScenarioAssembly()
        {
            CookerRuntime cooker = FindCooker(GameContentFactory.Ids.AssemblyTable);
            if (cooker == null)
            {
                Record("assembly_table_present", false, "no assembly table found.");
                return;
            }

            ItemDefinition cabbageSlice = Item(GameContentFactory.Ids.CabbageSlice);
            ItemDefinition tomatoSlice = Item(GameContentFactory.Ids.TomatoSlice);
            ItemDefinition cookedMeatSlice = Item(GameContentFactory.Ids.CookedMeatSlice);
            ItemDefinition breadSlice = Item(GameContentFactory.Ids.BreadSlice);
            ItemDefinition salad = Item(GameContentFactory.Ids.Salad);
            ItemDefinition meatBread = Item(GameContentFactory.Ids.MeatBread);

            // Recipe 1: exact two item match, 10 seconds.
            ResetInventory();
            runtime.GrantItem(cabbageSlice, 1);
            runtime.GrantItem(tomatoSlice, 1);
            ParkAllStaff();
            MoveStaffTo(0, cooker.GridPosition);
            Advance(5.0f);
            bool half = cooker.WorkProgress > 0.2f && cooker.WorkProgress < 0.85f && ProducedBy(cooker, salad) == 0;
            Advance(6.5f);
            bool saladDone = ProducedBy(cooker, salad) > 0;
            Expect("assemble_salad_10_seconds", half && saladDone,
                $"mid progress observed = {half}, output = {Describe(runtime.Cookers.DescribeOutputs(cooker))}.");

            // Recipe 2: the four item match must win over the two item match.
            ResetInventory();
            runtime.GrantItem(cabbageSlice, 1);
            runtime.GrantItem(tomatoSlice, 1);
            runtime.GrantItem(cookedMeatSlice, 1);
            runtime.GrantItem(breadSlice, 1);
            runtime.Cookers.TryCollectCookerOutput(cooker);
            Advance(0.1f);
            string locked = cooker.LockedRecipe != null ? cooker.LockedRecipe.Id : "<none>";
            Advance(11.0f);
            bool meatBreadDone = ProducedBy(cooker, meatBread) > 0;
            bool noSalad = runtime.Inventory.TotalCount(salad) == 0;
            Expect("assemble_four_item_recipe", meatBreadDone && noSalad,
                $"locked recipe = {locked}, output = {Describe(runtime.Cookers.DescribeOutputs(cooker))}, salad produced = {!noSalad}.");

            // Exact quantity matching. One cabbage slice cannot make the salad (a tomato is missing)
            // and cannot make the sandwich either, so the table must sit idle and leave the slice in
            // the stock: a partial set is never reserved and never consumed.
            ResetInventory();
            runtime.GrantItem(cabbageSlice, 1);
            runtime.Cookers.TryCollectCookerOutput(cooker);
            MoveStaffTo(0, cooker.GridPosition);
            Advance(15.0f);

            bool nothing = ProducedBy(cooker, meatBread) == 0
                && ProducedBy(cooker, salad) == 0
                && runtime.Inventory.TotalCount(meatBread) == 0
                && runtime.Inventory.TotalCount(salad) == 0;
            bool untouched = runtime.Inventory.TotalCount(cabbageSlice) == 1
                && runtime.Inventory.ReservedCount(cabbageSlice) == 0
                && cooker.Slots.Count == 0;
            Expect("assemble_exact_quantities", nothing && untouched,
                $"produced nothing = {nothing}, the incomplete set was left untouched = {untouched} " +
                $"(cabbage in stock = {runtime.Inventory.TotalCount(cabbageSlice)}, " +
                $"reserved = {runtime.Inventory.ReservedCount(cabbageSlice)}, slots = {runtime.Cookers.DescribeSlots(cooker)}), " +
                $"state = {cooker.State}, recipe = {(cooker.LockedRecipe != null ? cooker.LockedRecipe.Id : "<none>")}.");

            // Adding the missing companion must then make the salad, so the wait above was about the
            // missing ingredient and not a stuck station.
            runtime.GrantItem(tomatoSlice, 1);
            Advance(0.2f);
            bool saladLocked = cooker.LockedRecipe != null && cooker.LockedRecipe.Id == "assemble_salad";
            Advance(10.5f);
            bool saladMade = ProducedBy(cooker, salad) > 0 || runtime.Inventory.TotalCount(salad) > 0;
            Expect("assemble_starts_once_complete", saladLocked && saladMade,
                $"salad locked = {saladLocked}, salad produced = {saladMade}, output = {Describe(runtime.Cookers.DescribeOutputs(cooker))}.");

            // A surplus of an unrelated item must not disturb an exact match.
            ResetInventory();
            runtime.GrantItem(cabbageSlice, 3);
            runtime.GrantItem(tomatoSlice, 3);
            runtime.Cookers.TryCollectCookerOutput(cooker);
            MoveStaffTo(0, cooker.GridPosition);
            Advance(1.0f);
            bool exactReservation = cooker.GetReservedCount(cabbageSlice) == 1 && cooker.GetReservedCount(tomatoSlice) == 1;
            Advance(10.0f);
            bool oneSaladOnly = runtime.Inventory.TotalCount(salad) <= 1;
            Expect("assemble_reserves_exact_quantity", exactReservation && oneSaladOnly,
                $"one cabbage and one tomato reserved = {exactReservation}, salads produced = {runtime.Inventory.TotalCount(salad)}.");
        }

        // ---- 13/16. washing ------------------------------------------------------------------

        private static void ScenarioWashing()
        {
            CookerRuntime cooker = FindCooker(GameContentFactory.Ids.DishWasher);
            if (cooker == null)
            {
                Record("dish_washer_present", false, "no dish washer found.");
                return;
            }

            ItemDefinition dirty = Item(GameContentFactory.Ids.DirtyPlate);
            ItemDefinition clean = Item(GameContentFactory.Ids.CleanPlate);

            ResetInventory();
            runtime.GrantItem(dirty, 2);
            ParkAllStaff();

            Advance(1.0f);
            bool stopsWithoutStaff = runtime.Inventory.TotalCount(clean) == 0;
            Expect("wash_needs_staff", stopsWithoutStaff, "no clean plate appeared while nobody was at the washer.");

            // The chef arrives with both dirty plates already in stock, so the washer runs twice.
            MoveStaffTo(0, cooker.GridPosition);
            Advance(5.5f);
            int cleanedOnce = runtime.Inventory.TotalCount(clean);
            bool firstPlate = cleanedOnce >= 1;
            Advance(6.0f);
            int cleanedTwice = runtime.Inventory.TotalCount(clean);
            bool secondPlate = cleanedTwice > cleanedOnce;
            Expect("wash_5_seconds_per_plate", firstPlate && secondPlate,
                $"clean plates after 5.5 s = {cleanedOnce}, after 11.5 s = {cleanedTwice} " +
                $"(dirty left = {runtime.Inventory.TotalCount(dirty)}, reserved = {runtime.Inventory.ReservedCount(dirty)}, " +
                $"state = {cooker.State}, holding = {cooker.TotalOutputCount}).");

            // No dirty plates: the washer must stop on its own.
            ResetInventory();
            ParkAllStaff();
            MoveStaffTo(0, cooker.GridPosition);
            Advance(6.0f);
            Expect("wash_stops_without_dirty_plates", runtime.Inventory.TotalCount(clean) == 0 && ProducedBy(cooker, clean) == 0,
                $"clean plates produced with an empty dirty stock = {runtime.Inventory.TotalCount(clean)}.");
        }

        // ---- 14/15/17. orders and the plate cycle --------------------------------------------

        private static void ScenarioOrderAndPlates()
        {
            ItemDefinition salad = Item(GameContentFactory.Ids.Salad);
            ItemDefinition clean = Item(GameContentFactory.Ids.CleanPlate);
            ItemDefinition dirty = Item(GameContentFactory.Ids.DirtyPlate);

            OrderManager.OrderStatus order = runtime.Orders.Current;
            Expect("order_issued", order != null && order.IsActive,
                order != null ? $"active order '{order.Definition.DisplayName}' with {order.TimeRemaining:0} s left." : "no order was issued.");

            ResetInventory();
            ParkAllStaff();

            // No clean plate: the delivery must be refused and must not consume the dish.
            runtime.GrantItem(salad, 1);
            bool refused = !runtime.TrySubmitOrder();
            bool dishKept = runtime.Inventory.TotalCount(salad) == 1;
            Expect("delivery_needs_clean_plate", refused && dishKept,
                $"delivery refused = {refused}, salad kept = {dishKept}.");

            // No dish: likewise refused.
            ResetInventory();
            runtime.GrantItem(clean, 1);
            bool refused2 = !runtime.TrySubmitOrder();
            bool plateKept = runtime.Inventory.TotalCount(clean) == 1;
            Expect("delivery_needs_dish", refused2 && plateKept,
                $"delivery refused = {refused2}, clean plate kept = {plateKept}.");

            // Both present: the delivery succeeds exactly once and pays the reward.
            ResetInventory();
            runtime.GrantItem(salad, 1);
            runtime.GrantItem(clean, 1);
            int moneyBefore = runtime.Money;
            int completedBefore = runtime.Orders.CompletedOrders;
            int reward = runtime.Orders.Current.Reward;
            int returnsBefore = runtime.PlateReturns.PendingCount;
            bool served = runtime.TrySubmitOrder();
            bool paid = runtime.Money == moneyBefore + reward;
            bool consumed = runtime.Inventory.TotalCount(salad) == 0 && runtime.Inventory.TotalCount(clean) == 0;
            bool ticket = runtime.PlateReturns.PendingCount == returnsBefore + 1;
            Expect("delivery_success", served && paid && consumed && ticket,
                $"served = {served}, money {moneyBefore} -> {runtime.Money} (reward {reward}), consumed = {consumed}, plate tickets = {runtime.PlateReturns.PendingCount}.");

            // The sequence is finite now, so a delivery either issues the next order or, when it was the
            // last one, clears the level. Both are correct; silently wrapping is not.
            bool nextIssued = runtime.Orders.Current != null && runtime.Orders.Current.IsActive;
            bool levelCleared = runtime.Orders.IsLevelCleared;
            Expect("order_completed_once",
                runtime.Orders.CompletedOrders == completedBefore + 1 && (nextIssued || levelCleared),
                $"completed orders = {runtime.Orders.CompletedOrders}, next order issued = {nextIssued}, " +
                $"level cleared = {levelCleared}, orders in level = {runtime.Orders.OrderCount}.");

            // A settled order cannot be paid twice.
            int moneyAfterServe = runtime.Money;
            bool refusedAgain = !runtime.TrySubmitOrder();
            Expect("no_double_reward", refusedAgain && runtime.Money == moneyAfterServe,
                $"second submission refused = {refusedAgain}, money unchanged = {runtime.Money == moneyAfterServe}.");

            // The plate return ticket turns into exactly one dirty plate after 10-20 seconds.
            Advance(10.5f);
            bool earlyOk = runtime.Inventory.TotalCount(dirty) <= 1;
            Advance(10.0f);
            int dirtyCount = runtime.Inventory.TotalCount(dirty);
            bool returned = dirtyCount == 1 && runtime.PlateReturns.PendingCount == 0;
            Expect("plate_return_10_to_20_seconds", returned,
                $"dirty plates after 20.5 s = {dirtyCount} (1 expected, never more than 1 early = {earlyOk}), pending tickets = {runtime.PlateReturns.PendingCount}.");

            // Washing the returned plate puts a clean plate back into circulation.
            CookerRuntime washer = FindCooker(GameContentFactory.Ids.DishWasher);
            if (washer != null)
            {
                // The plate cycle is cleared and the chef is parked before the stock is measured, so
                // no leftover ticket or in-flight wash can add to the count below.
                runtime.PlateReturns.ClearPending();
                ParkAllStaff();
                ResetInventory();
                runtime.GrantItem(dirty, 1);
                MoveStaffTo(0, washer.GridPosition);
                Advance(6.0f);

                int cleanAfter = runtime.Inventory.TotalCount(clean);
                Expect("clean_plate_circulates", cleanAfter == 1,
                    $"clean plates after washing the returned plate = {cleanAfter} " +
                    $"(dirty left = {runtime.Inventory.TotalCount(dirty)}, reserved = {runtime.Inventory.ReservedCount(dirty)}, " +
                    $"pending tickets = {runtime.PlateReturns.PendingCount}).");
            }

            // An expired order fails and is replaced; the reward is never paid.
            ResetInventory();
            OrderManager.OrderStatus active = runtime.Orders.Current;
            int failedBefore = runtime.Orders.FailedOrders;
            int moneyBeforeTimeout = runtime.Money;
            Advance(active.Definition.TimeLimit + 1f);
            bool failed = runtime.Orders.FailedOrders == failedBefore + 1;
            bool replaced = runtime.Orders.Current != null && runtime.Orders.Current.IsActive;
            Expect("order_timeout", failed && replaced && runtime.Money == moneyBeforeTimeout,
                $"failed orders = {runtime.Orders.FailedOrders}, money unchanged = {runtime.Money == moneyBeforeTimeout}, new order active = {replaced}.");
        }

        // ---- 19. UI / world input separation -------------------------------------------------

        private static void ScenarioUiGuard()
        {
            InputManager input = runtime.transform.GetComponent<InputManager>();
            StaffRuntime first = runtime.AllStaff[0];
            StaffRuntime second = runtime.AllStaff[1];

            runtime.HandleStaffClicked(first);
            runtime.Input.enabled = true;

            input.SetPointerOverUiOverride(true, true);
            input.Tick(0.016f);
            bool pointerReported = input.PointerOverUI;
            bool selectionUnchanged = runtime.Staff.Selected == first;

            input.SetPointerOverUiOverride(false, false);
            input.Tick(0.016f);
            bool cleared = !input.PointerOverUI;

            input.enabled = false;
            runtime.HandleStaffClicked(second);
            bool stillSelectableWhenNotOverUi = runtime.Staff.Selected == second;

            Expect("ui_blocks_world_input", pointerReported && selectionUnchanged && cleared && stillSelectableWhenNotOverUi,
                $"pointer over UI reported = {pointerReported}, selection untouched = {selectionUnchanged}, released = {cleared}, world selection still works = {stillSelectableWhenNotOverUi}.");

            // A click while over the UI must not be interpreted as a ground click either.
            Vector2Int before = input.HoveredCell;
            input.SetPointerOverUiOverride(true, true);
            input.Tick(0.016f);
            Vector2Int during = input.HoveredCell;
            input.SetPointerOverUiOverride(false, false);
            input.Tick(0.016f);
            Expect("ui_suppresses_ground_hover", during == before,
                $"hovered cell while over the UI = {during} (unchanged from {before}).");
        }

        // ---- 4/9 via the real click entry points --------------------------------------------

        private static void ScenarioPlayerClicks()
        {
            StaffRuntime staff = runtime.AllStaff[0];
            CookerRuntime cooker = FindCooker(GameContentFactory.Ids.ChoppingStation);
            if (cooker == null)
            {
                return;
            }

            runtime.HandleStaffClicked(staff);
            runtime.HandleCookerClicked(cooker);
            bool cookerSelected = runtime.Cookers.Selected == cooker && runtime.Staff.Selected == staff;
            bool cookerViewSelected = cooker.View != null;
            Expect("click_select_cooker", cookerSelected && cookerViewSelected,
                $"cooker selected = {cookerSelected}, view present = {cookerViewSelected}.");

            runtime.HandleStaffClicked(staff);
            bool staffSelected = runtime.Staff.Selected == staff && runtime.Cookers.Selected == null;
            Expect("click_select_staff", staffSelected, $"staff selected = {staffSelected}, cooker selection cleared = {runtime.Cookers.Selected == null}.");

            // Selection visuals must exist and switch on.
            bool ringOn = staff.View != null && staff.View.gameObject.activeSelf;
            runtime.HandleCookerClicked(cooker);
            bool staffRetained = runtime.Staff.Selected == staff;
            Expect("selection_feedback", ringOn && staffRetained,
                $"selected staff view active = {ringOn}, employee retained for device assignment = {staffRetained}.");

            // Assignment activates an adjacent employee; leaving releases the assignment.
            ResetInventory();
            runtime.GrantItem(Item(GameContentFactory.Ids.Cabbage), 1);
            ParkAllStaff();
            MoveStaffTo(0, cooker.GridPosition);
            Advance(0.3f);
            bool activated = cooker.IsActive && cooker.LockedRecipe != null;
            MoveStaffTo(0, FindFreeStaffCell(cooker.GridPosition + new Vector2Int(0, -1)));
            Advance(0.3f);
            bool deactivated = !cooker.IsActive;
            Expect("cooker_activation_by_grid", activated && deactivated,
                $"activated when staff arrived = {activated}, deactivated when staff left = {deactivated}.");
        }

        // ---- world geometry and scene wiring -------------------------------------------------

        private static void ScenarioScheduling()
        {
            ResetInventory();
            ParkAllStaff();
            CookerRuntime chopping = FindCooker(GameContentFactory.Ids.ChoppingStation);
            CookerRuntime pan = FindCooker(GameContentFactory.Ids.FryingPan);
            StaffRuntime employee = runtime.AllStaff[0];
            Vector2Int choppingPosition = chopping.GridPosition;
            Vector2Int panPosition = pan.GridPosition;
            CookerFacing choppingFacing = chopping.Facing;
            CookerFacing panFacing = pan.Facing;
            string preferredRecipe = chopping.PreferredRecipeId;

            try
            {
                Relocate(runtime.AllStaff[1], new Vector2Int(0, 3));
                Relocate(runtime.AllStaff[2], new Vector2Int(4, 2));
                Relocate(employee, new Vector2Int(2, 2));
                bool arranged = runtime.Cookers.TryMoveCooker(chopping, new Vector2Int(2, 3)) &&
                    runtime.Cookers.TryMoveCooker(pan, new Vector2Int(3, 2));
                arranged &= FaceCooker(chopping, CookerFacing.South) && FaceCooker(pan, CookerFacing.West);
                Expect("scheduling_work_positions", arranged, "chopping and heating share the work position (2,2).");
                if (!arranged)
                {
                    return;
                }

                bool refusedOccupiedMove = !runtime.Grid.TryMoveCookerTraced(chopping.GridPosition, pan.GridPosition, chopping.Id);
                Expect("failed_device_move_preserves_grid", refusedOccupiedMove &&
                    runtime.Grid.GetCookerId(chopping.GridPosition) == chopping.Id &&
                    runtime.Grid.GetCookerId(pan.GridPosition) == pan.Id,
                    "a refused move preserves the source device and the occupied target.");

                chopping.PreferredRecipeId = GameContentFactory.Ids.ChopCabbage;
                runtime.GrantItem(Item(GameContentFactory.Ids.Cabbage), 2);
                runtime.GrantItem(Item(GameContentFactory.Ids.RawMeatSlice), 3);
                runtime.HandleStaffClicked(employee);
                Advance(0.2f);
                Expect("adjacency_requires_assignment", !chopping.IsActive && !pan.IsActive,
                    "standing beside two devices does not activate either without a task.");
                Expect("staff_cannot_enter_device", !runtime.Staff.TryMove(employee, Vector2Int.up),
                    "device cells cannot be entered by employees.");
                Expect("device_cannot_cover_staff", !runtime.Grid.IsFreeForCooker(employee.GridPosition),
                    "a device cannot be placed on a work position occupied by an employee.");

                Relocate(employee, new Vector2Int(2, 1));
                Expect("diagonal_operation_refused", !runtime.Staff.TryAssignCooker(employee, pan),
                    "a diagonal neighbour cannot operate a device.");
                Relocate(employee, new Vector2Int(2, 2));
                runtime.HandleCookerClicked(chopping);
                Advance(0.65f);
                int cuts = chopping.WorkTicks;
                Relocate(runtime.AllStaff[1], new Vector2Int(1, 3));
                Expect("device_has_one_operator", !runtime.Staff.TryAssignCooker(runtime.AllStaff[1], chopping),
                    "a second employee cannot take over an assigned device.");

                runtime.HandleCookerClicked(pan);
                Advance(pan.Definition.PreparationDuration * 0.3f);
                float preparationProgress = pan.WorkProgress;
                runtime.HandleCookerClicked(chopping);
                Advance(0.3f);
                Expect("preparation_requires_staff", pan.State == CookerState.Preparing &&
                    Mathf.Approximately(preparationProgress, pan.WorkProgress),
                    "preparation remains paused while the employee operates another device.");
                cuts = chopping.WorkTicks;
                runtime.HandleCookerClicked(pan);
                Advance(pan.Definition.PreparationDuration + 0.1f);
                Expect("one_employee_one_active_task", pan.IsHeating && !chopping.IsActive &&
                    employee.AssignedCookerId == 0 && chopping.WorkTicks == cuts,
                    "switching to preparation pauses chopping and retains its progress.");
                bool left = runtime.Staff.TryMove(employee, Vector2Int.left);
                Advance(0.05f);
                Expect("no_work_while_moving", left && !runtime.Staff.TryAssignCooker(employee, chopping) && !pan.IsActive,
                    "travelling immediately releases the task and cannot activate a new device.");
                Advance(pan.Definition.Duration + 0.25f);
                ItemDefinition cooked = Item(GameContentFactory.Ids.CookedMeatSlice);
                Expect("heat_continues_unattended", pan.HasAnyOutput && !pan.IsActive,
                    "heating completes while the assigned employee is away.");
                Expect("heated_output_waits_locally", runtime.Inventory.TotalCount(cooked) == 0 &&
                    runtime.Inventory.TotalCount(Item(GameContentFactory.Ids.RawMeatSlice)) == 2,
                    "one batch waits in the pan; it is not credited or restarted remotely.");
                Expect("remote_collection_refused", !runtime.TryCollectCookerOutput(pan) && pan.HasAnyOutput,
                    "a distant selected employee cannot bypass collection time.");
                Expect("dismantle_cannot_collect", !runtime.TrySellCooker(pan) && pan.HasAnyOutput,
                    "dismantling cannot bypass the return visit.");
                Expect("busy_heater_cannot_move", !runtime.Cookers.TryMoveCooker(pan, new Vector2Int(3, 3)) &&
                    pan.GridPosition == new Vector2Int(3, 2),
                    "a pending batch cannot be moved to bypass employee travel.");

                MoveStaffTo(0, pan.GridPosition);
                Expect("collection_requires_operation_time", pan.HasAnyOutput && runtime.Inventory.TotalCount(cooked) == 0,
                    "assigning a collector does not instantly credit the output.");
                Advance(pan.Definition.CollectionDuration + 0.05f);
                Expect("collection_credits_once", !pan.HasAnyOutput && runtime.Inventory.TotalCount(cooked) == 1,
                    "an adjacent employee completes collection and credits exactly one unit.");
                ParkAllStaff();
                Advance(1f);
                Expect("heat_needs_new_visit", pan.LockedRecipe == null && !pan.HasAnyOutput,
                    "a new batch is not started without another employee visit.");

                MoveStaffTo(0, pan.GridPosition);
                Advance(pan.Definition.PreparationDuration + 0.1f);
                ParkAllStaff();
                Advance(pan.Definition.Duration + pan.Definition.BurnGraceDuration + 0.5f);
                Expect("unattended_food_burns", pan.State == CookerState.Burnt && !pan.HasAnyOutput &&
                    runtime.Inventory.TotalCount(cooked) == 1,
                    "uncollected food is lost after its grace period without duplicating inventory.");
                MoveStaffTo(0, pan.GridPosition);
                Advance(pan.Definition.CleaningDuration * 0.5f);
                float cleaningProgress = pan.WorkProgress;
                ParkAllStaff();
                Advance(1f);
                Expect("cleaning_requires_staff", pan.State == CookerState.Cleaning &&
                    Mathf.Approximately(cleaningProgress, pan.WorkProgress),
                    "cleaning pauses when the employee leaves.");
                MoveStaffTo(0, pan.GridPosition);
                Advance(pan.Definition.CleaningDuration * 0.5f + 0.05f);
                Expect("burnt_device_recovers", pan.State == CookerState.Idle && pan.LockedRecipe == null,
                    "finishing cleanup makes the pan usable again.");
            }
            finally
            {
                ResetInventory();
                ParkAllStaff();
                runtime.Cookers.TryMoveCooker(pan, panPosition);
                runtime.Cookers.TryMoveCooker(chopping, choppingPosition);
                FaceCooker(chopping, choppingFacing);
                FaceCooker(pan, panFacing);
                chopping.PreferredRecipeId = preferredRecipe;
            }
        }

        private static void ScenarioOperationPositions()
        {
            ResetInventory();
            ParkAllStaff();
            CookerRuntime chopping = FindCooker(GameContentFactory.Ids.ChoppingStation);
            CookerRuntime pan = FindCooker(GameContentFactory.Ids.FryingPan);
            StaffRuntime employee = runtime.AllStaff[0];
            StaffRuntime helper = runtime.AllStaff[1];
            Vector2Int choppingPosition = chopping.GridPosition;
            Vector2Int panPosition = pan.GridPosition;
            CookerFacing choppingFacing = chopping.Facing;
            CookerFacing panFacing = pan.Facing;
            string preferredRecipe = chopping.PreferredRecipeId;

            try
            {
                Relocate(helper, new Vector2Int(0, 3));
                Relocate(runtime.AllStaff[2], new Vector2Int(4, 2));
                Relocate(employee, new Vector2Int(2, 2));
                bool arranged = runtime.Cookers.TryMoveCooker(chopping, new Vector2Int(2, 3)) &&
                    runtime.Cookers.TryMoveCooker(pan, new Vector2Int(3, 2)) &&
                    FaceCooker(chopping, CookerFacing.South) && FaceCooker(pan, CookerFacing.West);
                Expect("orientation_shared_work_cell", arranged && chopping.WorkPosition == pan.WorkPosition &&
                    runtime.Cookers.CountWorkPositionUsers(new Vector2Int(2, 2)) == 2,
                    "two facing devices may share one employee cell without reserving it twice.");
                if (!arranged)
                {
                    return;
                }

                Relocate(employee, new Vector2Int(1, 3));
                Expect("orientation_side_operation_refused", !runtime.Staff.TryAssignCooker(employee, chopping),
                    "being adjacent on the side is insufficient to operate a device.");
                Relocate(employee, new Vector2Int(2, 4));
                Expect("orientation_back_operation_refused", !runtime.Staff.TryAssignCooker(employee, chopping),
                    "the back of a device cannot activate its work.");
                Relocate(employee, new Vector2Int(2, 2));
                chopping.PreferredRecipeId = GameContentFactory.Ids.ChopCabbage;
                runtime.GrantItem(Item(GameContentFactory.Ids.Cabbage), 3);
                runtime.GrantItem(Item(GameContentFactory.Ids.RawMeatSlice), 2);
                bool assigned = runtime.Staff.TryAssignCooker(employee, chopping);
                Advance(0.65f);
                int cuts = chopping.WorkTicks;
                Expect("orientation_front_operation", assigned && chopping.IsActive && cuts > 0,
                    "the front work position activates an explicitly assigned employee.");
                Expect("orientation_shared_cell_exclusive", !runtime.Grid.IsFreeForStaff(chopping.WorkPosition, helper.Id) &&
                    !runtime.Staff.TryAssignCooker(helper, pan),
                    "a second employee cannot occupy or remotely operate the shared work cell.");

                runtime.HandleStaffClicked(employee);
                runtime.HandleCookerClicked(chopping);
                runtime.Input.RequestRotate();
                Expect("orientation_rotation_releases_task", chopping.Facing == CookerFacing.West &&
                    employee.AssignedCookerId == 0 && !chopping.IsActive && chopping.WorkTicks == cuts,
                    "the rotation input releases attendance while preserving manual work progress.");
                bool fullTurn = runtime.TryRotateCooker(chopping) && runtime.TryRotateCooker(chopping) && runtime.TryRotateCooker(chopping);
                Expect("orientation_full_turn", fullTurn && chopping.Facing == CookerFacing.South &&
                    chopping.WorkPosition == new Vector2Int(2, 2), "four clockwise turns restore the original work position.");
                Transform visual = chopping.View.transform.Find("Visual");
                Expect("orientation_model_and_indicator", chopping.View.HasWorkPositionIndicator && visual != null &&
                    Vector3.Dot(visual.forward, Vector3.back) > 0.99f,
                    "the model and facing marker follow the logical front direction.");

                bool split = FaceCooker(pan, CookerFacing.North);
                Relocate(helper, pan.WorkPosition);
                Expect("orientation_split_allows_parallel_work", split && runtime.Staff.TryAssignCooker(employee, chopping) &&
                    runtime.Staff.TryAssignCooker(helper, pan) && chopping.IsActive && pan.IsActive,
                    "separating the work positions allows two employees to work concurrently.");
                Advance(pan.Definition.PreparationDuration + 0.1f);
                CookerFacing heatingFacing = pan.Facing;
                Expect("orientation_busy_heater_cannot_rotate", pan.IsHeating && !runtime.TryRotateCooker(pan) && pan.Facing == heatingFacing,
                    "an active heating batch cannot be rotated away from its collection position.");

                int money = runtime.Money;
                Expect("orientation_invalid_build_no_charge", !runtime.TryBuildCooker(chopping.Definition,
                    new Vector2Int(1, 4), CookerFacing.North) && runtime.Money == money,
                    "a build with an out-of-bounds front is refused without charging money.");
                Expect("orientation_blocked_build_no_charge", !runtime.TryBuildCooker(chopping.Definition,
                    new Vector2Int(1, 3), CookerFacing.East) && runtime.Money == money,
                    "a build whose front contains another device is refused without charging money.");

                runtime.Input.SetPointerOverUiOverride(true, true);
                CookerFacing beforeUi = chopping.Facing;
                runtime.Input.RequestRotate();
                Expect("orientation_ui_blocks_rotation", chopping.Facing == beforeUi, "UI hover suppresses world rotation input.");
                runtime.Input.SetPointerOverUiOverride(false, false);
                runtime.Input.RequestBuildCooker(chopping.Definition);
                CookerFacing pending = runtime.Input.PendingFacing;
                runtime.Input.RequestRotate();
                Expect("orientation_preview_rotation_free", runtime.Input.PendingFacing == CookerFacingUtility.Clockwise(pending) &&
                    runtime.Money == money && chopping.Facing == beforeUi,
                    "build rotation changes only the preview direction and costs nothing.");
                runtime.Input.CancelCurrentInteraction();

                ResetInventory();
                ParkAllStaff();
                runtime.Cookers.TryMoveCooker(chopping, new Vector2Int(1, 4));
                FaceCooker(chopping, CookerFacing.North);
                bool blocked = !runtime.Cookers.IsWorkPositionOpen(chopping.WorkPosition);
                bool recovered = runtime.TryRotateCooker(chopping) && runtime.Cookers.IsWorkPositionOpen(chopping.WorkPosition);
                Expect("orientation_edge_rotation_recovers", blocked && recovered,
                    "rotation can pass through an invalid front and recover at the next valid direction.");
            }
            finally
            {
                runtime.Input.SetPointerOverUiOverride(false, false);
                runtime.Input.CancelCurrentInteraction();
                ResetInventory();
                ParkAllStaff();
                runtime.Cookers.TryMoveCooker(pan, panPosition);
                runtime.Cookers.TryMoveCooker(chopping, choppingPosition);
                FaceCooker(chopping, choppingFacing);
                FaceCooker(pan, panFacing);
                chopping.PreferredRecipeId = preferredRecipe;
            }
        }

        private static bool FaceCooker(CookerRuntime cooker, CookerFacing facing)
        {
            for (int turn = 0; turn < 4 && cooker.Facing != facing; turn++)
            {
                if (!runtime.TryRotateCooker(cooker))
                {
                    return false;
                }
            }

            return cooker.Facing == facing;
        }

        private static void ScenarioWorldGeometry()
        {
            Kitchen.Grid.GridManager grid = runtime.Grid;
            bool viewBuilt = grid.View != null && grid.View.CellCount == 25;
            bool highlights = false;
            if (grid.View != null)
            {
                grid.View.SetHighlight(new Vector2Int(0, 0), true, true);
                highlights = grid.View.HighlightedCount == 1;
                grid.View.ClearHighlights();
                highlights &= grid.View.HighlightedCount == 0;
            }

            Expect("grid_view", viewBuilt && highlights,
                $"grid view cells = {(grid.View != null ? grid.View.CellCount : -1)}, highlight add/clear works = {highlights}.");

            bool staffViews = true;
            for (int i = 0; i < runtime.AllStaff.Count; i++)
            {
                if (runtime.AllStaff[i].View == null || !runtime.AllStaff[i].View.gameObject.activeSelf)
                {
                    staffViews = false;
                }
            }

            bool cookerViews = true;
            for (int i = 0; i < runtime.Cookers.Cookers.Count; i++)
            {
                if (runtime.Cookers.Cookers[i].View == null || !runtime.Cookers.Cookers[i].View.gameObject.activeSelf)
                {
                    cookerViews = false;
                }
            }

            Expect("views_present", staffViews && cookerViews,
                $"every staff and cooker has an active view (staff = {staffViews}, cookers = {cookerViews}).");

            // A 3D view needs real volume: every cooker and chef must be built from several parts,
            // must cast shadows, and must stay inside the single cell it occupies. The cell bound is
            // what keeps art scale from ever deciding grid occupancy.
            int minCookerParts = int.MaxValue;
            int maxCookerParts = 0;
            bool allCastShadows = true;
            bool allInsideCell = true;
            string boundProblem = string.Empty;
            float cellLimit = runtime.Grid.CellSize;

            for (int i = 0; i < runtime.Cookers.Cookers.Count; i++)
            {
                CookerView view = runtime.Cookers.Cookers[i].View;
                if (view == null)
                {
                    continue;
                }

                int parts = CountSolidParts(view.transform);
                minCookerParts = Mathf.Min(minCookerParts, parts);
                maxCookerParts = Mathf.Max(maxCookerParts, parts);

                if (!HasShadowCaster(view.transform))
                {
                    allCastShadows = false;
                }

                if (view.TryGetWorldBounds(out Bounds bounds) &&
                    (bounds.size.x > cellLimit + 0.01f || bounds.size.z > cellLimit + 0.01f))
                {
                    allInsideCell = false;
                    boundProblem = $"{view.name} spans {bounds.size.x:0.00} x {bounds.size.z:0.00} against a {cellLimit:0.00} cell";
                }
            }

            int staffParts = 0;
            if (runtime.AllStaff.Count > 0 && runtime.AllStaff[0].View != null)
            {
                staffParts = CountSolidParts(runtime.AllStaff[0].View.transform);
            }

            Expect("models_have_3d_volume", minCookerParts >= 5 && staffParts >= 6,
                $"cooker parts per station = {minCookerParts}..{maxCookerParts}, chef parts = {staffParts}.");

            Expect("models_fit_their_cell", allInsideCell,
                allInsideCell
                    ? $"every cooker stays within its {cellLimit:0.00} unit cell."
                    : boundProblem);

            Expect("models_cast_shadows", allCastShadows,
                allCastShadows ? "every cooker model has shadow casting renderers." : "some cooker models do not cast shadows.");
        }

        /// <summary>Counts the mesh renderers under a model, ignoring ground decals.</summary>
        private static int CountSolidParts(Transform root)
        {
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            int count = 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                // A ground decal is a flat quad on the ground plane; it is not part of the model.
                if (renderers[i].sharedMaterial != null &&
                    renderers[i].sharedMaterial.shader != null &&
                    renderers[i].sharedMaterial.shader.name == "Sprites/Default")
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        private static bool HasShadowCaster(Transform root)
        {
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Every script referenced by the saved scene must resolve to a real asset.
        ///
        /// A component whose script guid does not exist silently disappears, which is how the HUD scale
        /// control ended up detached from the canvas: the scene was authored in a copy of the project and
        /// the copied scene carried a guid that the real project never had. This check turns that into a
        /// failure instead of a dead button.
        /// </summary>
        private static void ScenarioSceneScriptGuids()
        {
            if (!File.Exists(KitchenSceneGenerator.ScenePath))
            {
                Record("scene_script_guids_resolve", false, "the scene file does not exist.");
                return;
            }

            string yaml = File.ReadAllText(KitchenSceneGenerator.ScenePath);
            MatchCollection scripts = Regex.Matches(
                yaml, @"m_Script: \{fileID: \d+, guid: ([0-9a-f]{32}), type: 3\}");

            List<string> unresolved = new List<string>();
            int checkedScripts = 0;

            for (int i = 0; i < scripts.Count; i++)
            {
                string guid = scripts[i].Groups[1].Value;
                checkedScripts++;

                if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)) && !unresolved.Contains(guid))
                {
                    unresolved.Add(guid);
                }
            }

            Expect("scene_script_guids_resolve", unresolved.Count == 0 && checkedScripts > 0,
                unresolved.Count == 0
                    ? $"{checkedScripts} script reference(s) in the scene all resolve to an asset."
                    : $"{unresolved.Count} script guid(s) point at nothing: " +
                      string.Join(", ", unresolved.ToArray()));
        }

        private static void ScenarioFinalConsistency()
        {
            int problems = runtime.Grid.ValidateConsistency(out string report);
            Expect("grid_consistent", problems == 0, report);

            // Every placed cooker must be on the cell the grid reports for it.
            bool coherent = true;
            StringBuilder occupancy = new StringBuilder();
            for (int i = 0; i < runtime.Cookers.Cookers.Count; i++)
            {
                CookerRuntime cooker = runtime.Cookers.Cookers[i];
                int gridId = runtime.Grid.GetCookerId(cooker.GridPosition);
                occupancy.Append($"[#{cooker.Id} {cooker.DefinitionId} @{cooker.GridPosition} grid={gridId}] ");
                if (gridId != cooker.Id)
                {
                    coherent = false;
                }
            }

            for (int i = 0; i < runtime.AllStaff.Count; i++)
            {
                StaffRuntime staff = runtime.AllStaff[i];
                int gridId = runtime.Grid.GetStaffId(staff.GridPosition);
                occupancy.Append($"[staff {staff.Id} @{staff.GridPosition} grid={gridId}] ");
                if (gridId != staff.Id)
                {
                    coherent = false;
                }
            }

            Expect("occupancy_coherent", coherent,
                coherent ? "every cooker and staff record matches the grid." : occupancy.ToString());
            Expect("advance_frame_count", stepCount > 0, $"{stepCount} simulation steps were executed during verification.");
        }

        /// <summary>Checks the generated scene for empty required references.</summary>
        private static void RecordSceneWiring()
        {
            if (!File.Exists(KitchenSceneGenerator.ScenePath))
            {
                Record("scene_wiring", false, $"{KitchenSceneGenerator.ScenePath} does not exist.");
                return;
            }

            UnityEngine.SceneManagement.Scene scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                KitchenSceneGenerator.ScenePath,
                UnityEditor.SceneManagement.OpenSceneMode.Additive);

            List<string> missing = new List<string>();
            int found = 0;

            // The HUD font is checked in the same pass: a label with no usable font renders nothing,
            // which is exactly the "the UI has no text" symptom.
            int uiLabels = 0;
            int uiLabelsWithoutFont = 0;
            int uiLabelsWithUnusableFont = 0;
            string uiFontName = "<none>";
            string uiFontProblem = string.Empty;
            TMP_FontAsset uiFont = null;
            HashSet<char> uiCharacters = new HashSet<char>();

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Component[] components = roots[i].GetComponentsInChildren<Component>(true);
                for (int c = 0; c < components.Length; c++)
                {
                    Component component = components[c];
                    if (component == null || component is Transform || component is CanvasRenderer)
                    {
                        continue;
                    }

                    found++;
                    SerializedObject so = new SerializedObject(component);
                    SerializedProperty property = so.GetIterator();
                    while (property.NextVisible(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference)
                        {
                            continue;
                        }

                        if (property.name == "m_Script")
                        {
                            continue;
                        }

                        if (property.objectReferenceValue == null && IsRequiredReference(component, property.name))
                        {
                            missing.Add($"{GetPath(component.transform)}.{property.name}");
                        }
                    }

                    if (component is TMP_Text label)
                    {
                        uiLabels++;

                        if (label.font == null)
                        {
                            uiLabelsWithoutFont++;
                            if (uiFontProblem.Length == 0)
                            {
                                uiFontProblem = $"{GetPath(label.transform)} has no font";
                            }
                        }
                        else
                        {
                            uiFontName = label.font.name;
                            uiFont = label.font;

                            if (!label.font.IsUsable())
                            {
                                uiLabelsWithUnusableFont++;
                                if (uiFontProblem.Length == 0)
                                {
                                    uiFontProblem = $"{GetPath(label.transform)} uses '{label.font.name}' which cannot render";
                                }
                            }

                            // Every character the label starts with must exist in the font, otherwise
                            // the HUD silently shows nothing for those words.
                            string content = label.text;
                            if (!string.IsNullOrEmpty(content))
                            {
                                for (int ch = 0; ch < content.Length; ch++)
                                {
                                    char character = content[ch];
                                    if (!char.IsWhiteSpace(character) && !char.IsControl(character))
                                    {
                                        uiCharacters.Add(character);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);

            Expect("scene_wiring", missing.Count == 0,
                missing.Count == 0
                    ? $"{found} components in Kitchen.unity have no empty required reference."
                    : $"{missing.Count} empty required reference(s): {string.Join(", ", missing.ToArray())}");

            Expect("ui_labels_have_font", uiLabels > 0 && uiLabelsWithoutFont == 0 && uiLabelsWithUnusableFont == 0,
                uiLabels == 0
                    ? "no TMP labels were found in the scene."
                    : $"{uiLabels} labels checked, {uiLabelsWithoutFont} without a font, " +
                      $"{uiLabelsWithUnusableFont} with an unusable font. Font = '{uiFontName}'. {uiFontProblem}");

            // Functional proof that the HUD can render: every character the starting labels show must
            // resolve to a glyph in the font. This is what distinguishes "text is wired" from "text
            // is actually drawn".
            List<char> missingCharacters = new List<char>();
            if (uiFont != null)
            {
                foreach (char character in uiCharacters)
                {
                    if (uiFont.characterLookupTable == null ||
                        !uiFont.characterLookupTable.ContainsKey(character))
                    {
                        missingCharacters.Add(character);
                    }
                }
            }

            Expect("ui_font_has_glyphs",
                uiFont != null && missingCharacters.Count == 0,
                uiFont == null
                    ? "no font asset was found on the HUD labels."
                    : $"{uiCharacters.Count} distinct characters used by the HUD starting labels; " +
                      $"missing glyphs = {missingCharacters.Count}" +
                      (missingCharacters.Count > 0 ? $" ({new string(missingCharacters.ToArray())})" : string.Empty) +
                      $". Font = '{uiFont.name}', atlas = " +
                      $"{(uiFont.atlasTexture != null ? $"{uiFont.atlasTexture.width}x{uiFont.atlasTexture.height}" : "none")}.");
        }

        /// <summary>
        /// References the demo genuinely cannot run without. Optional art hooks are excluded because
        /// leaving those empty is exactly how the whitebox fallback is selected.
        /// </summary>
        private static bool IsRequiredReference(Component component, string fieldName)
        {
            if (component is GameRuntime)
            {
                switch (fieldName)
                {
                    case "levelConfig":
                    case "inputManager":
                    case "uiManager":
                    case "gridManager":
                    case "cookerManager":
                    case "staffManager":
                    case "workManager":
                    case "inventoryManager":
                    case "recipeManager":
                    case "orderManager":
                    case "plateReturnManager":
                    case "floorRoot":
                    case "gridRoot":
                    case "cookerRoot":
                    case "staffRoot":
                    case "ingredientRoot":
                    case "worldCamera":
                        return true;
                    default:
                        return false;
                }
            }

            if (component is Kitchen.UI.UIManager)
            {
                switch (fieldName)
                {
                    case "moneyText":
                    case "modeText":
                    case "timeText":
                    case "orderPanel":
                    case "orderTitleText":
                    case "orderRequirementText":
                    case "orderTimerText":
                    case "submitOrderButton":
                    case "cookerShopPanel":
                    case "selectionPanel":
                    case "progressPanel":
                    case "progressFill":
                    case "notificationPanel":
                    case "notificationText":
                    case "cookerButtons":
                    case "cookerButtonLabels":
                        return true;
                    default:
                        return false;
                }
            }

            if (component is Kitchen.Work.WorkStrategyHolder)
            {
                return fieldName == "strategies";
            }

            if (component is Kitchen.Level.LevelManager)
            {
                return fieldName == "activeLevel" || fieldName == "runtime";
            }

            // Per-instance view fields are optional by design; they select the whitebox fallback.
            return false;
        }

        // ------------------------------------------------------------------ harness utilities

        private static void Advance(float seconds)
        {
            if (runtime == null)
            {
                return;
            }

            const float step = 0.05f;
            int steps = Mathf.Max(1, Mathf.CeilToInt(seconds / step));
            for (int i = 0; i < steps; i++)
            {
                runtime.AdvanceFrame(step, 1f);
                stepCount++;
            }
        }

        private static void ParkAllStaff()
        {
            // The middle row of the default 5x5 map is free of cookers, so each staff member is
            // parked on a distinct cell there. Overlapping them on purpose would break the rule the
            // harness is supposed to be testing.
            Vector2Int[] parking =
            {
                new Vector2Int(1, 2),
                new Vector2Int(2, 2),
                new Vector2Int(3, 2)
            };

            HashSet<Vector2Int> taken = new HashSet<Vector2Int>();
            for (int index = 0; index < runtime.Cookers.Cookers.Count; index++)
            {
                taken.Add(runtime.Cookers.Cookers[index].WorkPosition);
            }

            for (int i = 0; i < runtime.AllStaff.Count; i++)
            {
                StaffRuntime staff = runtime.AllStaff[i];
                Vector2Int preferred = i < parking.Length ? parking[i] : new Vector2Int(0, 2 + i);

                Vector2Int target = preferred;
                if (!runtime.Grid.InBounds(target) || taken.Contains(target) || !runtime.Grid.IsFreeForStaff(target, staff.Id))
                {
                    target = FindFreeCellExcluding(taken, staff.Id);
                }

                taken.Add(target);
                Relocate(staff, target);
            }
        }

        private static void Relocate(StaffRuntime staff, Vector2Int cell)
        {
            runtime.Staff.ReleaseCooker(staff.AssignedCookerId);
            runtime.Grid.TryReleaseStaff(staff.GridPosition, staff.Id);
            if (!runtime.Grid.TryOccupyStaff(cell, staff.Id))
            {
                throw new InvalidOperationException($"Cannot relocate employee {staff.Id} to {cell}.");
            }
            staff.SnapTo(cell, runtime.Grid.GridToWorld(cell));
            if (staff.View != null)
            {
                staff.View.ApplyWorldPosition(staff.WorldPosition);
            }
        }

        private static void MoveStaffTo(int index, Vector2Int cell)
        {
            if (index < 0 || index >= runtime.AllStaff.Count)
            {
                return;
            }

            StaffRuntime staff = runtime.AllStaff[index];
            Vector2Int target = cell;

            CookerRuntime targetCooker = runtime.Cookers.GetCookerAt(cell);
            if (targetCooker != null)
            {
                Vector2Int candidate = targetCooker.WorkPosition;
                if (runtime.Grid.IsFreeForStaff(candidate, staff.Id))
                {
                    Relocate(staff, candidate);
                    if (!runtime.Staff.TryAssignCooker(staff, targetCooker))
                    {
                        throw new InvalidOperationException($"Cannot assign employee {staff.Id} to cooker {targetCooker.Id}.");
                    }

                    return;
                }

                throw new InvalidOperationException($"Cooker {targetCooker.Id} has no available work position.");
            }

            if (!runtime.Grid.InBounds(target) || !runtime.Grid.IsFreeForStaff(target, staff.Id))
            {
                target = runtime.Grid.TryFindNearestFreeCellForStaff(target, staff.Id, out Vector2Int found) ? found : staff.GridPosition;
            }

            Relocate(staff, target);
        }

        private static Vector2Int FindFreeCellExcluding(HashSet<Vector2Int> taken, int staffId)
        {
            for (int y = 0; y < runtime.Grid.Height; y++)
            {
                for (int x = 0; x < runtime.Grid.Width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (!taken.Contains(cell) && runtime.Grid.IsFreeForStaff(cell, staffId))
                    {
                        return cell;
                    }
                }
            }

            return new Vector2Int(0, 0);
        }

        /// <summary>Finds a staff free cell near a preferred one, used to walk a chef away.</summary>
        private static Vector2Int FindFreeStaffCell(Vector2Int preferred)
        {
            return runtime.Grid.TryFindNearestFreeCellForStaff(preferred, 0, out Vector2Int found)
                ? found
                : preferred;
        }

        private static Vector2Int FindFreeCookerCell(int startX, int startY)
        {
            for (int y = startY; y < runtime.Grid.Height; y++)
            {
                for (int x = startX; x < runtime.Grid.Width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (runtime.Grid.IsFreeForCooker(cell))
                    {
                        return cell;
                    }
                }

                startX = 0;
            }

            return new Vector2Int(0, 0);
        }

        /// <summary>
        /// Units of an item the cooker has produced, whether they are still held in its output slot or
        /// already in the kitchen stock. Checks use this so they do not depend on where produce lives.
        /// </summary>
        private static int ProducedBy(CookerRuntime cooker, ItemDefinition item)
        {
            int held = cooker != null ? cooker.GetOutputCount(item) : 0;
            int stock = runtime != null && runtime.Inventory != null
                ? runtime.Inventory.TotalCount(item)
                : 0;

            return held + stock;
        }

        private static CookerRuntime FindCooker(string definitionId)
        {
            for (int i = 0; i < runtime.Cookers.Cookers.Count; i++)
            {
                CookerRuntime cooker = runtime.Cookers.Cookers[i];
                if (cooker.Definition != null && cooker.Definition.Id == definitionId)
                {
                    return cooker;
                }
            }

            return null;
        }

        private static ItemDefinition Item(string id)
        {
            return runtime.FindItem(id);
        }

        private static void ResetInventory()
        {
            Kitchen.Inventory.InventoryManager inventory = runtime.Inventory;
            FieldInfo totalsField = typeof(Kitchen.Inventory.InventoryManager).GetField("totals", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo reservationsField = typeof(Kitchen.Inventory.InventoryManager).GetField("reservations", BindingFlags.Instance | BindingFlags.NonPublic);

            (totalsField?.GetValue(inventory) as Dictionary<ItemDefinition, int>)?.Clear();
            (reservationsField?.GetValue(inventory) as Dictionary<ItemDefinition, int>)?.Clear();

            // A ticket left over from an earlier scenario would drop a dirty plate into the middle of
            // the next measurement, so the plate cycle is reset along with the stock.
            runtime.PlateReturns.ClearPending();

            // Cookers must forget their locks too, otherwise a stale reservation points at stock
            // that no longer exists.
            for (int i = 0; i < runtime.Cookers.Cookers.Count; i++)
            {
                CookerRuntime cooker = runtime.Cookers.Cookers[i];
                cooker.ForgetLock();
                cooker.ClearOutputs();
                cooker.ResetProgress();
                cooker.State = CookerState.Idle;
                runtime.Work.GetStrategy(cooker.Definition.WorkType)?.Reset(cooker);
            }
        }

        private static void SetMoney(int amount)
        {
            PropertyInfo property = typeof(GameRuntime).GetProperty("Money", BindingFlags.Instance | BindingFlags.Public);
            property?.SetValue(runtime, amount);
        }

        private static void SetPrivateProperty(object target, string propertyName, object value)
        {
            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanWrite)
            {
                property.SetValue(target, value);
                return;
            }

            // Fall back to the compiler generated backing field.
            FieldInfo field = target.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            field?.SetValue(target, value);
        }

        private static void Grid(Action<Kitchen.Grid.GridManager> action)
        {
            action?.Invoke(runtime.Grid);
        }

        private static bool Near(Vector3 a, Vector3 b)
        {
            return (a - b).sqrMagnitude < 0.000001f;
        }

        private static string Describe(string text)
        {
            return string.IsNullOrEmpty(text) ? "-" : text;
        }

        private static string GetPath(Transform transform)
        {
            string path = transform.name;
            Transform current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        // ------------------------------------------------------------------ reporting

        private static void Expect(string name, bool passed, string detail)
        {
            Results.Add(new Result { Name = name, Passed = passed, Detail = detail });

            if (passed)
            {
                Debug.Log($"[Acceptance] PASS {name}: {detail}");
            }
            else
            {
                Debug.LogError($"[Acceptance] FAIL {name}: {detail}");
            }
        }

        private static void Record(string name, bool passed, string detail)
        {
            Expect(name, passed, detail);
        }

        private static void WriteReport(string reportPath)
        {
            int passed = 0;
            for (int i = 0; i < Results.Count; i++)
            {
                if (Results[i].Passed)
                {
                    passed++;
                }
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine($"  \"generatedAt\": \"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\",");
            builder.AppendLine($"  \"unityVersion\": \"{Application.unityVersion}\",");
            builder.AppendLine($"  \"simulationSteps\": {stepCount},");
            builder.AppendLine($"  \"total\": {Results.Count},");
            builder.AppendLine($"  \"passed\": {passed},");
            builder.AppendLine($"  \"failed\": {Results.Count - passed},");
            builder.AppendLine("  \"results\": [");

            for (int i = 0; i < Results.Count; i++)
            {
                Result result = Results[i];
                builder.Append("    { \"name\": \"");
                builder.Append(Escape(result.Name));
                builder.Append("\", \"status\": \"");
                builder.Append(result.Passed ? "PASS" : "FAIL");
                builder.Append("\", \"detail\": \"");
                builder.Append(Escape(result.Detail));
                builder.Append("\" }");
                builder.AppendLine(i < Results.Count - 1 ? "," : string.Empty);
            }

            builder.AppendLine("  ]");
            builder.AppendLine("}");

            string absolute = KitchenSceneGenerator.ToAbsolutePath(reportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute));
            File.WriteAllText(absolute, builder.ToString());

            Debug.Log($"[Acceptance] {passed}/{Results.Count} checks passed. Report written to '{absolute}'.");

            LastRunFailed = passed != Results.Count;
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", " ")
                .Replace("\n", " ");
        }
    }
}
