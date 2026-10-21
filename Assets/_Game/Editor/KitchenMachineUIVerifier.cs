using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kitchen.Cookers;
using Kitchen.Core;
using Kitchen.Data;
using Kitchen.Runtime;
using Kitchen.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kitchen.EditorTools
{
    [InitializeOnLoad]
    public static class KitchenMachineUIVerifier
    {
        [Serializable]
        private sealed class Check
        {
            public string name;
            public bool passed;
        }

        [Serializable]
        private sealed class Report
        {
            public List<Check> checks = new List<Check>();
            public int failed;
        }

        private const string RunningKey = "Kitchen.MachineUIVerification";
        private static double started;
        private static int frames;
        private static Report report = new Report();

        static KitchenMachineUIVerifier() { EditorApplication.update += Tick; }

        public static void Run()
        {
            KitchenSceneGenerator.Verify();
            BeginPlay();
        }

        public static void BeginPlay()
        {
            EditorSceneManager.OpenScene(KitchenSceneGenerator.ScenePath);
            GameRuntime runtime = UnityEngine.Object.FindObjectOfType<GameRuntime>();
            SerializedObject settings = new SerializedObject(runtime);
            settings.FindProperty("levelConfig").objectReferenceValue = AssetDatabase.LoadAssetAtPath<LevelConfig>("Assets/_Game/Resources/Kitchen/Level_Sandbox.asset");
            settings.FindProperty("levelManager").objectReferenceValue = null;
            settings.ApplyModifiedPropertiesWithoutUndo();
            SessionState.SetBool(RunningKey, true);
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(RunningKey, false)) return;
            if (started == 0) started = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - started > 150)
            {
                Add("play_timeout", false);
                Finish();
                return;
            }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            GameRuntime runtime = GameRuntime.Instance;
            if (runtime == null || !runtime.IsInitialized || ++frames < 20) return;
            try
            {
                Verify(runtime);
            }
            catch (Exception exception)
            {
                Add("exception_" + exception.Message, false);
                Debug.LogException(exception);
            }
            Finish();
        }

        private static void Verify(GameRuntime runtime)
        {
            runtime.enabled = false;
            KitchenMachineUI ui = UnityEngine.Object.FindObjectOfType<KitchenMachineUI>();
            CookerRuntime cooker = runtime.Cookers.Cookers.First(station => station.Definition.Kind == CookerKind.ChoppingStation);
            runtime.Input.SetPointerOverUiOverride(true, false);
            VerifyParallelOrdersAndHeat(runtime, ui);
            int assigned = runtime.Staff.Staff[0].AssignedCookerId;
            runtime.Input.RequestCookerRecipe(cooker.View.gameObject);
            Add("right_click_intent_opens_wheel_without_assigning_staff", ui.IsOpen && runtime.Input.RecipeMenuOpen && assigned == runtime.Staff.Staff[0].AssignedCookerId);
            Add("all_current_recipes_have_buttons", runtime.Recipes.RecipesFor(cooker.Definition.Kind).All(recipe => ui.transform.GetComponentsInChildren<Button>().Any(button => button.name == recipe.Id)));
            KitchenButtonVerifier.Verify(ui.transform, Add);
            KitchenHudFonts.VerifyLabels(ui.transform, Add);
            Button tomato = ui.GetComponentsInChildren<Button>().First(button => button.name == "chop_tomato");
            tomato.onClick.Invoke();
            Add("choose_tomato_closes_and_releases_input", cooker.PreferredRecipeId == "chop_tomato" && !ui.IsOpen && !runtime.Input.RecipeMenuOpen);
            string previous = cooker.PreferredRecipeId;
            Add("wrong_kind_and_unknown_recipe_rejected", !runtime.TrySetCookerRecipe(cooker, "wash_plate") && !runtime.TrySetCookerRecipe(cooker, "missing") && cooker.PreferredRecipeId == previous);
            Add("foreign_cooker_rejected", !runtime.TrySetCookerRecipe(new CookerRuntime(999, cooker.Definition, cooker.GridPosition), "chop_tomato"));
            runtime.Input.SetPointerOverUiOverride(true, true);
            runtime.Input.RequestCookerRecipe(cooker.View.gameObject);
            Add("ui_pointer_blocks_world_recipe_intent", !ui.IsOpen);
            runtime.Input.SetPointerOverUiOverride(true, false);
            runtime.Input.RequestBuildCooker(cooker.Definition);
            runtime.Input.RequestCookerRecipe(cooker.View.gameObject);
            Add("build_mode_does_not_open_wheel", !ui.IsOpen);
            runtime.Input.CancelCurrentInteraction();
            ui.OpenRecipes(cooker);
            runtime.Input.CancelCurrentInteraction();
            Add("cancel_closes_wheel", !ui.IsOpen && !runtime.Input.RecipeMenuOpen);
            var strategy = ScriptableObject.CreateInstance<ChoppingWorkStrategy>();
            strategy.Tick(cooker, 0.9f);
            RecipeDefinition locked = cooker.LockedRecipe;
            float progress = cooker.WorkProgress;
            int reserved = runtime.Inventory.ReservedCount(runtime.FindItem("tomato"));
            Add("real_work_produces_partial_progress", locked != null && progress > 0f && progress < 1f);
            runtime.TrySetCookerRecipe(cooker, "chop_cabbage");
            Add("recipe_change_preserves_batch_progress_and_reservations", cooker.LockedRecipe == locked && Mathf.Approximately(progress, cooker.WorkProgress) && reserved == runtime.Inventory.ReservedCount(runtime.FindItem("tomato")));
            runtime.Cookers.SelectCooker(cooker);
            runtime.UI.RefreshAll();
            ui.SendMessage("LateUpdate");
            PaperRingGraphic ring = ui.GetComponentsInChildren<PaperRingGraphic>().First(graphic => graphic.name == "Progress");
            Add("floating_ring_reads_actual_work_progress", Mathf.Approximately(ring.Progress, cooker.WorkProgress) && !ring.raycastTarget);
            Add("bottom_progress_panel_hidden", !Find(ui.transform, "RecipeProgressPanel").gameObject.activeSelf);
            RectTransform badge = ring.transform.parent as RectTransform;
            Vector2 before = badge.anchoredPosition;
            cooker.View.transform.position += Vector3.right;
            ui.SendMessage("LateUpdate");
            Add("progress_follows_machine", Vector2.Distance(before, badge.anchoredPosition) > 1f);
            cooker.View.transform.position -= Vector3.right;
            PaperclipReaction clip = ui.GetComponentInChildren<PaperclipReaction>();
            Image body = clip.transform.Find("Visual").GetComponent<Image>();
            Add("paperclip_uses_imported_transparent_sprite", body.sprite != null && AssetDatabase.GetAssetPath(body.sprite).EndsWith("BreadPaperclip.png"));
            clip.OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
            Add("paperclip_click_shows_speech", clip.transform.parent.GetComponentsInChildren<TMP_Text>().Any(label => label.text == "慢慢来，也会很好吃！"));
            Capture(runtime, "Logs/machine_ui_progress.png");
            ui.OpenRecipes(cooker);
            Capture(runtime, "Logs/machine_ui_wheel.png");
            bool fits = true;
            Canvas canvas = ui.GetComponent<Canvas>();
            HudScaler scaler = ui.GetComponent<HudScaler>();
            foreach (float scale in new[] { HudScaler.MinScale, 1f, HudScaler.MaxScale })
            {
                scaler.SetScale(scale, false);
                Canvas.ForceUpdateCanvases();
                ui.SendMessage("LateUpdate");
                RectTransform wheel = Find(ui.transform, "Wheel");
                Vector3[] corners = new Vector3[4];
                wheel.GetWorldCorners(corners);
                RectTransform canvasRect = (RectTransform)canvas.transform;
                fits &= corners.All(corner => canvasRect.rect.Contains(canvasRect.InverseTransformPoint(corner)));
            }
            Add("wheel_fits_at_all_hud_scales", fits);
            scaler.SetScale(1f, false);
            ui.CloseRecipes();
            strategy.Tick(cooker, cooker.Definition.TickInterval * cooker.Definition.RequiredTicks + 0.1f);
            strategy.Tick(cooker, 0.3f);
            Add("new_recipe_runs_after_current_batch", cooker.LockedRecipe != null && cooker.LockedRecipe.Id == "chop_cabbage");
            Add("automatic_selection_supported", runtime.TrySetCookerRecipe(cooker, null) && cooker.PreferredRecipeId == null);
            ui.OpenRecipes(cooker);
            KitchenPauseMenuVerifier.VerifyCancellation(runtime, Add);
            ui.OpenRecipes(cooker);
            cooker.SetPlacement(cooker.GridPosition, cooker.WorldPosition, false);
            ui.SendMessage("LateUpdate");
            Add("removed_machine_closes_wheel_and_hides_badge", !ui.IsOpen && !runtime.Input.RecipeMenuOpen && !badge.gameObject.activeSelf);
            UnityEngine.Object.Destroy(strategy);
        }

        private static void VerifyParallelOrdersAndHeat(GameRuntime runtime, KitchenMachineUI ui)
        {
            ParallelOrderPanel panel = ui.GetComponentInChildren<ParallelOrderPanel>();
            Add("parallel_tickets_visible_in_play", panel != null && runtime.Orders.ActiveOrders.Count == 2 &&
                panel.GetComponentsInChildren<Button>().Count(button => button.name.StartsWith("SubmitTicket_")) == 2);
            if (panel == null || runtime.Orders.ActiveOrders.Count != 2) return;
            var first = runtime.Orders.ActiveOrders[0];
            var second = runtime.Orders.ActiveOrders[1];
            runtime.GrantItem(second.Dish, second.DishRequired);
            runtime.GrantItem(runtime.CleanPlate, second.PlatesRequired);
            panel.Refresh();
            Button submit = panel.GetComponentsInChildren<Button>().First(button => button.name == "SubmitTicket_1");
            Add("parallel_second_submit_enabled", submit.interactable);
            int money = runtime.Money;
            submit.onClick.Invoke();
            Add("parallel_second_button_targets_own_order", second.State == OrderState.Completed && first.IsActive &&
                runtime.Orders.Current == first && runtime.Money == money + second.Reward && runtime.Orders.ActiveOrders.Count == 2);
            Canvas.ForceUpdateCanvases();
            RectTransform bounds = Find(panel.transform, "ParallelOrderContent");
            Rect inclusiveBounds = bounds.rect;
            inclusiveBounds.xMin -= 0.1f;
            inclusiveBounds.yMin -= 0.1f;
            inclusiveBounds.xMax += 0.1f;
            inclusiveBounds.yMax += 0.1f;
            bool fits = true;
            foreach (RectTransform ticket in bounds)
            {
                if (!ticket.gameObject.activeSelf || !ticket.name.StartsWith("OrderTicket_")) continue;
                Vector3[] corners = new Vector3[4];
                ticket.GetWorldCorners(corners);
                fits &= corners.All(corner => inclusiveBounds.Contains(bounds.InverseTransformPoint(corner)));
            }
            Add("parallel_tickets_fit_panel", fits);

            CookerRuntime pan = runtime.Cookers.Cookers.First(station => station.Definition.Kind == CookerKind.FryingPan);
            runtime.Work.GetStrategy(WorkType.Timed).Reset(pan);
            pan.AddOutput(runtime.FindItem("cooked_meat_slice"), 1);
            pan.State = CookerState.ReadyToCollect;
            typeof(CookerRuntime).GetProperty("ReadySeconds").SetValue(pan, pan.Definition.BurnGraceDuration * 0.5f);
            ui.SendMessage("LateUpdate");
            RectTransform badge = Find(ui.transform, "CookerProgress_" + pan.Id);
            PaperRingGraphic normal = badge.Find("Progress").GetComponent<PaperRingGraphic>();
            PaperRingGraphic warning = badge.Find("OvercookProgress").GetComponent<PaperRingGraphic>();
            Add("heat_red_ring_uses_overcook_progress", Mathf.Approximately(normal.Progress, 1f) &&
                Mathf.Approximately(warning.Progress, 0.5f) && warning.color.r > warning.color.g && !warning.raycastTarget);
            KitchenPauseMenuVerifier.Verify(runtime, Add, "working_kitchen", true);
            Capture(runtime, "Logs/parallel_orders_overcook.png");
            typeof(CookerRuntime).GetProperty("HeatStopped").SetValue(pan, true);
            ui.SendMessage("LateUpdate");
            Add("heat_stopped_ring_safe_green", warning.Progress == 0f && normal.color.g > normal.color.r);
            runtime.Work.GetStrategy(WorkType.Timed).Reset(pan);
            pan.State = CookerState.Idle;
        }

        private static RectTransform Find(Transform parent, string name)
        {
            return parent.GetComponentsInChildren<RectTransform>(true).First(rect => rect.name == name);
        }

        private static void Capture(GameRuntime runtime, string path)
        {
            Canvas canvas = UnityEngine.Object.FindObjectOfType<KitchenMachineUI>().GetComponent<Canvas>();
            Camera camera = runtime.WorldCamera;
            RenderTexture target = new RenderTexture(1920, 1080, 24) { antiAliasing = 4 };
            RenderTexture previous = camera.targetTexture;
            RenderTexture active = RenderTexture.active;
            float aspect = camera.aspect;
            camera.targetTexture = target;
            camera.aspect = 1920f / 1080f;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 2f;
            Canvas.ForceUpdateCanvases();
            canvas.GetComponent<PaperHudLayout>().RefreshLayout();
            canvas.GetComponent<KitchenMachineUI>().SendMessage("LateUpdate");
            camera.Render();
            foreach (PaperRingGraphic ring in canvas.GetComponentsInChildren<PaperRingGraphic>())
            {
                if (ring.Progress <= 0f) continue;
                Mesh geometry = ring.canvasRenderer.GetMesh();
                Add("ring_has_visible_geometry_" + Path.GetFileNameWithoutExtension(path) + "_" + ring.name,
                    geometry != null && geometry.vertexCount > 0 && !ring.canvasRenderer.cull && ring.color.a > 0f);
            }
            RenderTexture.active = target;
            Texture2D image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            image.Apply();
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes(path, image.EncodeToPNG());
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            camera.targetTexture = previous;
            camera.aspect = aspect;
            RenderTexture.active = active;
            target.Release();
            UnityEngine.Object.Destroy(target);
            UnityEngine.Object.Destroy(image);
        }

        private static void Add(string name, bool passed)
        {
            report.checks.Add(new Check { name = name, passed = passed });
            if (!passed) report.failed++;
            Debug.Log("[Machine UI] " + (passed ? "PASS " : "FAIL ") + name);
        }

        private static void Finish()
        {
            SessionState.SetBool(RunningKey, false);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/machine_ui_report.json", JsonUtility.ToJson(report, true));
            if (Application.isBatchMode) EditorApplication.Exit(report.failed == 0 ? 0 : 1);
            else EditorApplication.isPlaying = false;
        }
    }
}
