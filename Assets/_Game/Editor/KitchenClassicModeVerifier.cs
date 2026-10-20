using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kitchen.Core;
using Kitchen.Cookers;
using Kitchen.Entities;
using Kitchen.Runtime;
using Kitchen.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Kitchen.EditorTools
{
    [InitializeOnLoad]
    public static class KitchenClassicModeVerifier
    {
        [Serializable] private sealed class Check { public string name; public bool passed; }
        [Serializable] private sealed class Report { public List<Check> checks = new List<Check>(); public int failed; }
        private const string RunningKey = "Kitchen.ClassicVerification";
        private static Report report = new Report();
        private static double started;
        private static int frames;
        private static int stage;
        private static int settledScore;
        private static int settledMoney;

        static KitchenClassicModeVerifier()
        {
            EditorApplication.update += Tick;
            SceneManager.sceneLoaded += (scene, mode) => frames = 0;
        }

        public static void BeginPlay()
        {
            EditorSceneManager.OpenScene("Assets/_Game/Scenes/MainMenu.unity");
            SessionState.SetBool(RunningKey, true);
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(RunningKey, false)) return;
            if (started == 0) started = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - started > 240) { Add("play_timeout", false); Finish(); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || ++frames < 20) return;
            try
            {
                if (stage == 0)
                {
                    Add("menu_has_two_modes", FindButton("ClassicModeButton") != null && FindButton("StartButton") != null);
                    FindButton("StartButton").onClick.Invoke();
                    if (UnityEngine.Object.FindObjectOfType<MainMenuDetails>() != null)
                        FindButton("TutorialConfirmButton").onClick.Invoke();
                }
                else if (stage <= 3) VerifyTutorial(stage);
                else if (stage == 4) VerifyFirstDay();
                else if (stage == 5) VerifySecondDay();
                else if (stage == 6)
                {
                    GameRuntime runtime = GameRuntime.Instance;
                    Add("second_skill_survives_scene_load", runtime.Classic.Run.Day == 3 &&
                        runtime.Classic.Run.Rank(KitchenSkill.Practice) == 1 && runtime.Classic.Run.Rank(KitchenSkill.QuickSteps) == 1);
                    ClassicProgress.Begin();
                    foreach (KitchenSkill skill in Enum.GetValues(typeof(KitchenSkill))) ClassicProgress.Current.AdvanceDay(skill);
                    ClassicProgress.Current.AdvanceDay(KitchenSkill.Recruitment);
                    SceneManager.LoadScene("Kitchen");
                }
                else if (stage == 7) VerifySkills();
                else if (stage == 8)
                {
                    Add("return_menu_ends_classic_run", ClassicProgress.Current == null);
                    FindButton("ClassicModeButton").onClick.Invoke();
                    if (UnityEngine.Object.FindObjectOfType<MainMenuDetails>() != null)
                        FindButton("ClassicConfirmButton").onClick.Invoke();
                }
                else
                {
                    GameRuntime runtime = GameRuntime.Instance;
                    Add("fresh_classic_resets_days_and_upgrades", runtime.Classic.Run.Day == 1 &&
                        runtime.Classic.Run.TotalScore == 0 && runtime.Classic.Run.Rank(KitchenSkill.Practice) == 0 &&
                        runtime.Cookers.Cookers.Count == 0 && runtime.AllStaff.Count == 2 && runtime.Money == 400);
                    Add("fresh_classic_resets_purchase_prices", runtime.Cookers.Catalogue.All(definition =>
                        runtime.Classic.Run.CookerPurchaseCount(definition.Id) == 0 && runtime.GetCookerPrice(definition) == definition.Price));
                    Finish();
                    return;
                }
                stage++;
                frames = 0;
            }
            catch (Exception exception)
            {
                Add("exception_" + exception.Message, false);
                Debug.LogException(exception);
                Finish();
            }
        }

        private static void VerifyTutorial(int number)
        {
            GameRuntime runtime = GameRuntime.Instance;
            runtime.enabled = false;
            Add("tutorial_" + number + "_briefing", runtime.Classic == null && runtime.Level.LevelNumber == number &&
                runtime.Level.LevelTitle.StartsWith("教程") && runtime.TutorialBriefingOpen && runtime.Orders.ActiveOrders.Count == 0);
            runtime.AdvanceFrame(5f);
            Add("tutorial_" + number + "_briefing_pauses_clock", runtime.ElapsedTime == 0f);
            FindButton("BeginService").onClick.Invoke();
            Add("tutorial_" + number + "_keeps_base_prices", runtime.Cookers.Catalogue.All(definition =>
                runtime.GetCookerPrice(definition) == definition.Price));
            Add("tutorial_" + number + "_order_slots", runtime.Orders.ActiveOrders.Count == (number == 1 ? 1 : 2));
            if (number == 3) Add("tutorial_three_requires_plate_cycle", runtime.Inventory.TotalCount(runtime.CleanPlate) == 2 &&
                runtime.Orders.OrderCount > 2);
            int limit = 10;
            while (runtime.Orders.ActiveOrders.Count > 0 && limit-- > 0) Serve(runtime, 0);
            Add("tutorial_" + number + "_finite_clear", runtime.Orders.IsLevelCleared && runtime.UI.LevelClearPanel.activeSelf);
            runtime.UI.LevelClearNextButton.onClick.Invoke();
        }

        private static void VerifyFirstDay()
        {
            GameRuntime runtime = GameRuntime.Instance;
            runtime.enabled = false;
            ClassicDayController day = runtime.Classic;
            Add("tutorial_completion_enters_classic", day != null && day.Run.Day == 1 && day.Phase == DayPhase.Briefing);
            Add("classic_initial_staff_and_plates", runtime.AllStaff.Count == 2 && runtime.Inventory.TotalCount(runtime.CleanPlate) == 2);
            Add("classic_starts_empty_with_purchase_budget", runtime.Cookers.Cookers.Count == 0 && runtime.Money == 400 &&
                runtime.Level.StartingCookers.Count == 0);
            runtime.AdvanceFrame(10f);
            Add("briefing_has_no_production_orders_or_timer", runtime.ElapsedTime == 0f && day.RemainingSeconds == 180f &&
                runtime.Orders.ActiveOrders.Count == 0 && runtime.Cookers.Cookers.All(cooker => cooker.LockedRecipe == null));
            var raw = runtime.FindItem("tomato");
            Add("unlimited_raw_reservations_consume_safely", runtime.Inventory.IsUnlimited(raw) &&
                runtime.Inventory.TryReserve(raw, 1000) && runtime.Inventory.TryConsumeReserved(raw, 1000) &&
                runtime.Inventory.TryConsume(raw, 1000) && runtime.Inventory.AvailableCount(raw) == int.MaxValue &&
                runtime.Inventory.ReservedCount(raw) == 0);
            Add("plates_and_processed_food_stay_finite", !runtime.Inventory.IsUnlimited(runtime.CleanPlate) &&
                !runtime.Inventory.IsUnlimited(runtime.FindItem("tomato_slice")) && !runtime.Inventory.TryConsume(runtime.CleanPlate, 3));
            KitchenButtonVerifier.Verify(UnityEngine.Object.FindObjectOfType<KitchenMachineUI>().transform, Add);
            Capture(runtime, "classic_briefing.png");
            FindButton("ArrangeKitchen").onClick.Invoke();
            Add("briefing_can_be_dismissed_for_building", !FindRect(runtime, "DayFlowOverlay").gameObject.activeSelf &&
                FindButton("OpenDailyBriefing").gameObject.activeSelf);
            VerifyPurchases(runtime);
            var chopping = runtime.Cookers.Cookers.First(cooker => cooker.GridPosition == new Vector2Int(0, 4));
            Add("arrange_moves_preserves_recipe", runtime.TryMoveCooker(chopping, Vector2Int.right) &&
                runtime.TrySetCookerRecipe(chopping, "chop_tomato"));
            FindButton("OpenDailyBriefing").onClick.Invoke();
            FindButton("BeginService").onClick.Invoke();
            Add("service_starts_once", day.Phase == DayPhase.Service && runtime.Orders.ActiveOrders.Count == 2 && !runtime.BeginClassicService());
            var served = runtime.Orders.ActiveOrders[1];
            Serve(runtime, 1);
            Add("score_reward_and_duplicate_delivery", day.Score == served.Reward && !runtime.TrySubmitOrder(served));
            for (int index = 0; index < 7; index++) Serve(runtime, index % 2);
            Add("orders_continue_beyond_original_list", runtime.Orders.CompletedOrders == 8 && runtime.Orders.ActiveOrders.Count == 2 &&
                !runtime.Orders.IsLevelCleared && !runtime.UI.LevelClearPanel.activeSelf);
            var expired = runtime.Orders.ActiveOrders[0];
            var other = runtime.Orders.ActiveOrders[1];
            expired.TimeRemaining = 0.05f;
            runtime.AdvanceFrame(0.1f);
            Add("timeout_leaves_and_replaces_without_retry", expired.State == OrderState.Failed && day.MissedOrders == 1 &&
                runtime.Orders.ActiveOrders.Contains(other) && runtime.Orders.ActiveOrders.All(order => order.SequenceIndex != expired.SequenceIndex));
            var pending = runtime.Orders.ActiveOrders[0];
            runtime.Input.RequestBuildCooker(chopping.Definition);
            runtime.AdvanceFrame(1000f);
            Add("closing_clamps_time_and_clears_orders", day.Phase == DayPhase.Results && day.RemainingSeconds == 0f &&
                Mathf.Approximately(runtime.ElapsedTime, 180f) && runtime.Orders.ActiveOrders.Count == 0 &&
                runtime.Input.Mode == Kitchen.Core.InteractionMode.Normal);
            settledScore = day.Score;
            settledMoney = runtime.Money;
            Add("settlement_counts_once_and_offers_three_skills", day.Run.TotalScore == day.Score && day.Choices.Count == 3 &&
                day.Choices.Distinct().Count() == 3 && day.UnservedOrders == 2);
            runtime.AdvanceFrame(500f);
            runtime.Orders.StartFirstOrder();
            Add("closed_service_blocks_mutations", !runtime.TrySubmitOrder(pending) &&
                !runtime.TryBuildCooker(chopping.Definition, new Vector2Int(2, 3)) &&
                day.Run.TotalScore == settledScore && runtime.Orders.ActiveOrders.Count == 0);
            Add("invalid_skill_rejected", !day.ChooseSkill(KitchenSkill.Recruitment) && day.Run.Day == 1);
            Capture(runtime, "classic_results.png");
            FindButton("SkillChoice_0").onClick.Invoke();
        }

        private static void VerifySecondDay()
        {
            GameRuntime runtime = GameRuntime.Instance;
            runtime.enabled = false;
            ClassicDayController day = runtime.Classic;
            Add("next_day_starts_with_briefing", day.Run.Day == 2 && day.Phase == DayPhase.Briefing && day.Score == 0 &&
                day.RemainingSeconds == 180f && runtime.Orders.ActiveOrders.Count == 0);
            Add("money_score_and_skill_survive", runtime.Money == settledMoney && day.Run.TotalScore == settledScore &&
                day.Run.BestDayScore == settledScore && day.Run.Rank(KitchenSkill.Practice) == 1);
            Add("layout_and_recipe_survive", runtime.Cookers.Cookers.Count == 5 && runtime.Cookers.Cookers.Any(cooker =>
                cooker.GridPosition == new Vector2Int(1, 4) && cooker.PreferredRecipeId == "chop_tomato"));
            var choppingDefinition = runtime.Cookers.Catalogue.First(definition => definition.Kind == CookerKind.ChoppingStation);
            Add("purchase_count_and_price_survive_free_layout_restore", day.Run.CookerPurchaseCount(choppingDefinition.Id) == 3 &&
                runtime.GetCookerPrice(choppingDefinition) == 88 && runtime.Cookers.Catalogue.Where(definition =>
                    definition != choppingDefinition).All(definition => day.Run.CookerPurchaseCount(definition.Id) == 1));
            Add("food_batches_and_plate_loans_reset", runtime.Inventory.TotalCount(runtime.CleanPlate) == 2 &&
                runtime.Inventory.TotalCount(runtime.FindItem("salad")) == 0 && runtime.PlateReturns.PendingCount == 0 &&
                runtime.Cookers.Cookers.All(cooker => !cooker.HasAnyOutput && cooker.LockedRecipe == null));
            runtime.BeginClassicService();
            var chopping = runtime.Cookers.Cookers.First(cooker => cooker.PreferredRecipeId == "chop_tomato");
            PlaceEmployee(runtime, runtime.AllStaff[0], chopping.WorkPosition);
            runtime.Staff.TryAssignCooker(runtime.AllStaff[0], chopping);
            runtime.AdvanceFrame(chopping.Definition.TickInterval / day.Run.WorkMultiplier + 0.001f);
            Add("practice_skill_changes_actual_work_speed", chopping.WorkTicks == 1);
            Add("daily_pressure_increases_without_asset_mutation", runtime.Orders.ActiveOrders[0].TimeLimit <
                runtime.Orders.ActiveOrders[0].Definition.TimeLimit * 0.6f);
            runtime.AdvanceFrame(1000f);
            FindButton("SkillChoice_0").onClick.Invoke();
        }

        private static void VerifySkills()
        {
            GameRuntime runtime = GameRuntime.Instance;
            runtime.enabled = false;
            ClassicDayController day = runtime.Classic;
            Add("plate_and_recruitment_skills_change_roster", runtime.Inventory.TotalCount(runtime.CleanPlate) == 3 && runtime.AllStaff.Count == 4);
            StaffRuntime employee = runtime.AllStaff[0];
            Vector3 before = employee.WorldPosition;
            runtime.Staff.Select(employee);
            bool moved = runtime.TryMoveSelectedStaff(Vector2Int.up);
            runtime.AdvanceFrame(0.1f);
            Add("movement_skill_changes_actual_travel", moved && Mathf.Abs(Vector3.Distance(before, employee.WorldPosition) - 0.69f) < 0.01f);
            runtime.AdvanceFrame(1f);
            var panDefinition = runtime.Cookers.Catalogue.First(definition => definition.Kind == CookerKind.FryingPan);
            Add("skills_run_still_buys_own_equipment", runtime.TryBuildCooker(panDefinition, new Vector2Int(4, 4), CookerFacing.South));
            int moneyBeforeService = runtime.Money;
            runtime.BeginClassicService();
            var order = runtime.Orders.ActiveOrders[0];
            Serve(runtime, 0);
            Add("hospitality_changes_money_and_score", day.Score == Mathf.RoundToInt(order.Definition.Reward * 1.15f) &&
                runtime.Money == moneyBeforeService + day.Score);
            CookerRuntime pan = runtime.Cookers.Cookers.First(cooker => cooker.Definition.Kind == CookerKind.FryingPan);
            pan.AddOutput(runtime.FindItem("cooked_meat_slice"), 1);
            pan.State = CookerState.ReadyToCollect;
            runtime.AdvanceFrame(pan.Definition.BurnGraceDuration * 1.1f);
            Add("heat_control_extends_real_overcook_window", pan.HasAnyOutput && pan.BurnProgress > 0.8f && pan.BurnProgress < 1f);
            runtime.AdvanceFrame(pan.Definition.BurnGraceDuration * 0.2f);
            Add("burnt_batches_recorded", pan.State == CookerState.Burnt && day.BurntBatches == 1);
            runtime.Work.GetStrategy(WorkType.Timed).Reset(pan);
            pan.State = CookerState.Idle;
            PlaceEmployee(runtime, employee, pan.WorkPosition);
            runtime.Staff.TryAssignCooker(employee, pan);
            runtime.GrantItem(runtime.FindItem("raw_meat_slice"), 1);
            runtime.AdvanceFrame(pan.Definition.PreparationDuration / day.Run.WorkMultiplier + 0.001f);
            Add("practice_speeds_manual_pan_preparation", pan.State == CookerState.Working && pan.IsHeating);
            runtime.AdvanceFrame(1000f);
            Add("capped_recruitment_not_offered", !day.Choices.Contains(KitchenSkill.Recruitment));
            FindButton("ReturnToMenu").onClick.Invoke();
        }

        private static void VerifyPurchases(GameRuntime runtime)
        {
            var definitions = runtime.Cookers.Catalogue;
            var chopping = definitions.First(definition => definition.Kind == CookerKind.ChoppingStation);
            Add("first_purchase_costs_base_and_raises_only_same_type", runtime.TryBuildCooker(chopping, new Vector2Int(0, 4), CookerFacing.South) &&
                runtime.Money == 350 && runtime.GetCookerPrice(chopping) == 63 && definitions.Where(definition => definition != chopping)
                    .All(definition => runtime.GetCookerPrice(definition) == definition.Price));
            Add("shop_displays_next_purchase_price", UnityEngine.Object.FindObjectsOfType<TMP_Text>().Any(label =>
                label.text == $"{chopping.DisplayName}　63 金币"));
            int moneyBeforeFailure = runtime.Money;
            Add("invalid_placement_does_not_charge_or_raise_price", !runtime.TryBuildCooker(chopping, new Vector2Int(0, 4)) &&
                !runtime.TryBuildCooker(chopping, new Vector2Int(-1, 4)) &&
                !runtime.TryBuildCooker(chopping, new Vector2Int(3, 4), CookerFacing.North) &&
                !runtime.TryBuildCooker(chopping, new Vector2Int(0, 3), CookerFacing.North) &&
                runtime.Money == moneyBeforeFailure && runtime.GetCookerPrice(chopping) == 63);
            var pan = definitions.First(definition => definition.Kind == CookerKind.FryingPan);
            var assembly = definitions.First(definition => definition.Kind == CookerKind.AssemblyTable);
            var washer = definitions.First(definition => definition.Kind == CookerKind.DishWasher);
            Add("starting_budget_buys_complete_kitchen", runtime.TryBuildCooker(pan, new Vector2Int(4, 4), CookerFacing.South) &&
                runtime.TryBuildCooker(assembly, new Vector2Int(0, 0)) && runtime.TryBuildCooker(washer, new Vector2Int(4, 0)) &&
                runtime.Money == 90 && runtime.Cookers.Cookers.Count == 4);
            runtime.Input.RequestBuildCooker(chopping);
            runtime.SpendMoney(40);
            runtime.Input.UpdateBuildPreview(new Vector2Int(2, 3));
            Add("preview_rejects_balance_that_only_covers_old_price", !runtime.Input.HoveredCellLegal);
            runtime.Input.CancelCurrentInteraction();
            runtime.Input.RequestBuildCooker(chopping);
            Add("build_request_uses_increased_price", runtime.Input.Mode == Kitchen.Core.InteractionMode.Normal);
            runtime.UI.RequestCookerPurchase(definitions.ToList().IndexOf(chopping));
            Add("shop_request_uses_increased_price", runtime.Input.Mode == Kitchen.Core.InteractionMode.Normal);
            Add("insufficient_funds_and_cancel_do_not_raise_price", !runtime.TryBuildCooker(chopping, new Vector2Int(2, 4), CookerFacing.South) &&
                runtime.Money == 50 && runtime.GetCookerPrice(chopping) == 63);
            runtime.AddMoney(40);
            Add("second_purchase_charges_rounded_increased_price", runtime.TryBuildCooker(chopping, new Vector2Int(2, 4), CookerFacing.South) &&
                runtime.Money == 27 && runtime.GetCookerPrice(chopping) == 75);
            var extra = runtime.Cookers.Cookers.First(cooker => cooker.GridPosition == new Vector2Int(2, 4));
            Add("demolition_does_not_reset_purchase_count_or_refund", runtime.TrySellCooker(extra) &&
                runtime.Money == 27 && runtime.GetCookerPrice(chopping) == 75);
            runtime.AddMoney(75);
            Add("third_purchase_keeps_accumulated_increase", runtime.TryBuildCooker(chopping, new Vector2Int(2, 4), CookerFacing.South) &&
                runtime.Money == 27 && runtime.GetCookerPrice(chopping) == 88 && runtime.Classic.Run.CookerPurchaseCount(chopping.Id) == 3);
            var duplicate = UnityEngine.Object.Instantiate(chopping);
            Add("pricing_uses_stable_definition_id", runtime.GetCookerPrice(duplicate) == 88 && chopping.Price == 50);
            UnityEngine.Object.Destroy(duplicate);
        }

        private static void Serve(GameRuntime runtime, int slot)
        {
            var order = runtime.Orders.ActiveOrders[slot];
            runtime.GrantItem(order.Dish, order.DishRequired);
            runtime.GrantItem(runtime.CleanPlate, order.PlatesRequired);
            runtime.UI.RefreshValues();
            FindButton("SubmitTicket_" + slot).onClick.Invoke();
        }

        private static void PlaceEmployee(GameRuntime runtime, StaffRuntime employee, Vector2Int cell)
        {
            runtime.Staff.ReleaseCooker(employee.AssignedCookerId);
            runtime.Grid.TryReleaseStaff(employee.GridPosition, employee.Id);
            typeof(StaffRuntime).GetProperty("GridPosition").SetValue(employee, cell);
            typeof(StaffRuntime).GetProperty("TargetGridPosition").SetValue(employee, cell);
            typeof(StaffRuntime).GetProperty("WorldPosition").SetValue(employee, runtime.Grid.GridToWorld(cell));
            typeof(StaffRuntime).GetProperty("IsMoving").SetValue(employee, false);
            runtime.Grid.TryOccupyStaff(cell, employee.Id);
            employee.View.ApplyWorldPosition(employee.WorldPosition);
        }

        private static Button FindButton(string name) => UnityEngine.Object.FindObjectsOfType<Button>(true).First(button => button.name == name);
        private static RectTransform FindRect(GameRuntime runtime, string name) => UnityEngine.Object.FindObjectOfType<KitchenDayPanel>().GetComponentsInChildren<RectTransform>(true).First(rect => rect.name == name);

        private static void Capture(GameRuntime runtime, string name)
        {
            Canvas canvas = UnityEngine.Object.FindObjectOfType<KitchenDayPanel>().GetComponent<Canvas>();
            Camera camera = runtime.WorldCamera;
            RenderTexture target = new RenderTexture(1920, 1080, 24);
            RenderTexture previous = camera.targetTexture;
            RenderTexture active = RenderTexture.active;
            float aspect = camera.aspect;
            camera.targetTexture = target;
            camera.aspect = 16f / 9f;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 2f;
            Canvas.ForceUpdateCanvases();
            canvas.GetComponent<PaperHudLayout>().RefreshLayout();
            canvas.GetComponent<KitchenDayPanel>().SendMessage("Update");
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            Texture2D screenshot = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            screenshot.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            screenshot.Apply();
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes("Logs/" + name, screenshot.EncodeToPNG());
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            camera.targetTexture = previous;
            camera.aspect = aspect;
            RenderTexture.active = active;
            target.Release();
            UnityEngine.Object.Destroy(target);
            UnityEngine.Object.Destroy(screenshot);
        }

        private static void Add(string name, bool passed)
        {
            report.checks.Add(new Check { name = name, passed = passed });
            if (!passed) report.failed++;
            Debug.Log("[Classic] " + (passed ? "PASS " : "FAIL ") + name);
        }

        private static void Finish()
        {
            SessionState.SetBool(RunningKey, false);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/classic_mode_report.json", JsonUtility.ToJson(report, true));
            if (Application.isBatchMode) EditorApplication.Exit(report.failed == 0 ? 0 : 1);
            else EditorApplication.isPlaying = false;
        }
    }
}
