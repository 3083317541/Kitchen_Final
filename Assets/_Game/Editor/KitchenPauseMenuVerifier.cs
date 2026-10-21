using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kitchen.Core;
using Kitchen.Runtime;
using Kitchen.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kitchen.EditorTools
{
    public static class KitchenPauseMenuVerifier
    {
        public static void Verify(GameRuntime runtime, Action<string, bool> check, string context, bool settingsChecks = false)
        {
            Action<string, bool> add = (name, passed) => check("pause_" + context + "_" + name, passed);
            KitchenPauseMenu menu = runtime.UI.PauseMenu;
            add("menu_created_and_closed", menu != null && !menu.Canvas.gameObject.activeSelf && !runtime.IsPaused);
            if (menu == null) return;
            runtime.Input.CancelCurrentInteraction();
            float speed = Time.timeScale;
            string before = Snapshot(runtime);
            runtime.Input.RequestEscape();
            add("escape_opens_modal_and_stops_time", runtime.IsPaused && runtime.Input.PauseMenuOpen &&
                menu.Canvas.gameObject.activeSelf && Time.timeScale == 0f);
            runtime.AdvanceFrame(25f);
            runtime.SendMessage("Update");
            add("all_simulation_state_frozen", before == Snapshot(runtime));
            add("gameplay_actions_blocked", !runtime.CanOperateKitchen && !runtime.CanArrangeKitchen &&
                !runtime.BeginClassicService() && !runtime.BeginTutorialPractice() && !runtime.TrySubmitOrder());
            runtime.Input.RequestBuildCooker(runtime.Cookers.Catalogue[0]);
            add("build_intent_blocked", runtime.Input.Mode == InteractionMode.Normal);
            if (settingsChecks) VerifySettings(runtime, menu, add);
            runtime.Input.RequestEscape();
            add("escape_resumes_and_restores_time", !runtime.IsPaused && !runtime.Input.PauseMenuOpen &&
                !menu.Canvas.gameObject.activeSelf && Mathf.Approximately(Time.timeScale, speed));
            if (runtime.CanOperateKitchen)
            {
                float elapsed = runtime.ElapsedTime;
                runtime.AdvanceFrame(0.02f);
                add("simulation_resumes_without_catching_up", Mathf.Approximately(runtime.ElapsedTime, elapsed + 0.02f));
            }
        }

        private static void VerifySettings(GameRuntime runtime, KitchenPauseMenu menu, Action<string, bool> add)
        {
            bool hasVolume = PlayerPrefs.HasKey(PaperUISettings.VolumeKey);
            bool hasScale = PlayerPrefs.HasKey(HudScaler.PrefsKey);
            bool hasMotion = PlayerPrefs.HasKey(PaperUISettings.MotionKey);
            float volumeSetting = PaperUISettings.Volume;
            float scaleSetting = PlayerPrefs.GetFloat(HudScaler.PrefsKey, 1f);
            bool motionSetting = PaperUISettings.MotionEnabled;
            HudScaler hud = runtime.UI.GetComponentInChildren<HudScaler>();
            if (hud == null) hud = UnityEngine.Object.FindObjectOfType<KitchenMachineUI>().GetComponent<HudScaler>();
            float originalScale = hud.Scale;
            try
            {
                SettleEntrance(menu);
                EventSystem.current.SetSelectedGameObject(null);
                KitchenButtonVerifier.Verify(menu.Canvas.transform, add);
                Capture(runtime, menu, "pause-menu.png", 1920, 1080, add);
                Capture(runtime, menu, "pause-menu-1280x960.png", 1280, 960, add);
                Capture(runtime, menu, "pause-menu-720x1280.png", 720, 1280, add);
                KitchenHudFonts.VerifyLabels(menu.Canvas.transform, add);
                Canvas hudCanvas = hud.GetComponent<Canvas>();
                add("underlying_hud_locked", !hudCanvas.GetComponent<CanvasGroup>().interactable);
                RectTransform layout = menu.Canvas.transform.Find("PauseCard") as RectTransform;
                Vector2 size = layout.rect.size;
                CanvasScaler pauseScaler = menu.Canvas.GetComponent<CanvasScaler>();
                Vector2 reference = pauseScaler.referenceResolution;
                Find(menu, "PauseSettings").onClick.Invoke();
                Slider volume = menu.Canvas.GetComponentsInChildren<Slider>().First(slider => slider.name == "PauseVolume");
                Slider scale = menu.Canvas.GetComponentsInChildren<Slider>().First(slider => slider.name == "PauseHudScale");
                volume.value = 0.37f;
                scale.value = 1.45f;
                Find(menu, "PauseMotion").onClick.Invoke();
                add("settings_apply_and_persist", Mathf.Approximately(AudioListener.volume, 0.37f) &&
                    Mathf.Approximately(PlayerPrefs.GetFloat(PaperUISettings.VolumeKey), 0.37f) &&
                    Mathf.Approximately(hud.Scale, 1.45f) && Mathf.Approximately(PlayerPrefs.GetFloat(HudScaler.PrefsKey), 1.45f) &&
                    PaperUISettings.MotionEnabled != motionSetting);
                Canvas.ForceUpdateCanvases();
                add("hud_scaling_keeps_menu_size_stable", layout.rect.size == size && pauseScaler.referenceResolution == reference &&
                    hudCanvas.GetComponent<CanvasScaler>().referenceResolution != reference);
                Capture(runtime, menu, "pause-settings.png", 1920, 1080, add);
                KitchenHudFonts.VerifyLabels(menu.Canvas.transform, add);
                runtime.Input.RequestEscape();
                add("escape_from_settings_returns_without_resuming", runtime.IsPaused && !menu.SettingsOpen && Find(menu, "ResumeGame").gameObject.activeInHierarchy);
                Find(menu, "PauseSettings").onClick.Invoke();
                add("settings_reopen_retains_values", Mathf.Approximately(volume.value, 0.37f) && Mathf.Approximately(scale.value, 1.45f));
                Find(menu, "PauseSettingsBack").onClick.Invoke();
                Find(menu, "PauseReturnToMenu").onClick.Invoke();
                add("return_requires_confirmation", runtime.IsPaused && Find(menu, "PauseConfirmExit").gameObject.activeInHierarchy);
                Capture(runtime, menu, "pause-confirm.png", 1920, 1080, add);
                KitchenHudFonts.VerifyLabels(menu.Canvas.transform, add);
                runtime.Input.RequestEscape();
                add("escape_cancels_exit_confirmation", runtime.IsPaused && Find(menu, "ResumeGame").gameObject.activeInHierarchy);
                Find(menu, "PauseQuitGame").onClick.Invoke();
                add("quit_requires_confirmation", runtime.IsPaused && Find(menu, "PauseConfirmExit").gameObject.activeInHierarchy &&
                    menu.Canvas.GetComponentsInChildren<TMP_Text>().Any(label => label.text.Contains("收工")));
                Find(menu, "PauseCancelExit").onClick.Invoke();
                PointerEventData pointer = new PointerEventData(EventSystem.current) { position = Vector2.zero };
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, hits);
                add("backdrop_blocks_world_clicks", hits.Count > 0 && hits[0].gameObject.name == "Backdrop");
            }
            finally
            {
                PaperUISettings.SetVolume(volumeSetting);
                PaperUISettings.SetMotion(motionSetting);
                hud.SetScale(originalScale, false);
                if (hasVolume) PlayerPrefs.SetFloat(PaperUISettings.VolumeKey, volumeSetting); else PlayerPrefs.DeleteKey(PaperUISettings.VolumeKey);
                if (hasScale) PlayerPrefs.SetFloat(HudScaler.PrefsKey, scaleSetting); else PlayerPrefs.DeleteKey(HudScaler.PrefsKey);
                if (hasMotion) PlayerPrefs.SetInt(PaperUISettings.MotionKey, motionSetting ? 1 : 0); else PlayerPrefs.DeleteKey(PaperUISettings.MotionKey);
                PlayerPrefs.Save();
            }
        }

        public static void VerifyCancellation(GameRuntime runtime, Action<string, bool> check)
        {
            runtime.Input.RequestBuildCooker(runtime.Cookers.Catalogue[0]);
            runtime.Input.RequestEscape();
            check("escape_cancels_build_before_pausing", !runtime.IsPaused && runtime.Input.Mode == InteractionMode.Normal);
            KitchenMachineUI machine = UnityEngine.Object.FindObjectOfType<KitchenMachineUI>();
            var cooker = runtime.Cookers.Cookers[0];
            runtime.Input.SetPointerOverUiOverride(true, false);
            runtime.Input.RequestCookerRecipe(cooker.View.gameObject);
            check("wheel_is_open_before_escape", machine.IsOpen);
            runtime.Input.RequestEscape();
            check("escape_cancels_wheel_before_pausing", !runtime.IsPaused && !runtime.Input.RecipeMenuOpen && !machine.IsOpen);
            runtime.SetTimeScale(0.6f);
            runtime.Input.RequestEscape();
            Find(runtime.UI.PauseMenu, "ResumeGame").onClick.Invoke();
            check("resume_button_restores_custom_time_scale", !runtime.IsPaused && Mathf.Approximately(Time.timeScale, 0.6f));
            runtime.SetTimeScale(1f);
        }

        public static void ReturnViaMenu(GameRuntime runtime)
        {
            runtime.Input.CancelCurrentInteraction();
            runtime.Input.RequestEscape();
            Find(runtime.UI.PauseMenu, "PauseReturnToMenu").onClick.Invoke();
            Find(runtime.UI.PauseMenu, "PauseConfirmExit").onClick.Invoke();
        }

        private static string Snapshot(GameRuntime runtime)
        {
            return runtime.ElapsedTime + "|" + runtime.Money + "|" + runtime.Classic?.RemainingSeconds + "|" +
                runtime.PlateReturns.NextReturnIn + "|" + runtime.PlateReturns.PendingCount + "|" +
                string.Join(";", runtime.Orders.ActiveOrders.Select(order => order.SequenceIndex + ":" + order.TimeRemaining + ":" + order.State)) + "|" +
                string.Join(";", runtime.AllStaff.Select(staff => staff.GridPosition + ":" + staff.WorldPosition + ":" + staff.IsMoving)) + "|" +
                string.Join(";", runtime.Cookers.Cookers.Select(cooker => cooker.Id + ":" + cooker.State + ":" + cooker.WorkTicks + ":" +
                    cooker.WorkSeconds + ":" + cooker.OperationSeconds + ":" + cooker.ReadySeconds + ":" + cooker.TotalOutputCount)) + "|" +
                string.Join(";", runtime.Level.Items.Select(item => item.Id + ":" + runtime.Inventory.TotalCount(item)));
        }

        private static Button Find(KitchenPauseMenu menu, string name) => menu.Canvas.GetComponentsInChildren<Button>(true).First(button => button.name == name);

        private static void Capture(GameRuntime runtime, KitchenPauseMenu menu, string name, int width, int height, Action<string, bool> add)
        {
            Camera camera = runtime.WorldCamera;
            Canvas[] canvases = { UnityEngine.Object.FindObjectOfType<KitchenMachineUI>().GetComponent<Canvas>(), menu.Canvas };
            RenderTexture target = new RenderTexture(width, height, 24) { antiAliasing = 4 };
            RenderTexture previous = camera.targetTexture;
            RenderTexture active = RenderTexture.active;
            float aspect = camera.aspect;
            camera.targetTexture = target;
            camera.aspect = (float)width / height;
            foreach (Canvas canvas in canvases)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 2f;
                canvas.GetComponent<CanvasScaler>().SendMessage("Update");
            }
            Canvas.ForceUpdateCanvases();
            canvases[0].GetComponent<PaperHudLayout>().RefreshLayout();
            SettleEntrance(menu);
            camera.Render();
            RenderTexture.active = target;
            Texture2D screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
            screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            screenshot.Apply();
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes("Logs/" + name, screenshot.EncodeToPNG());
            var corners = new Vector3[4];
            bool contained = true;
            foreach (Button button in menu.Canvas.GetComponentsInChildren<Button>())
            {
                button.GetComponent<RectTransform>().GetWorldCorners(corners);
                contained &= corners.All(corner =>
                {
                    Vector3 point = camera.WorldToViewportPoint(corner);
                    return point.x >= 0f && point.x <= 1f && point.y >= 0f && point.y <= 1f;
                });
            }
            add(name + "_buttons_fit_viewport", contained);
            foreach (Canvas canvas in canvases)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
            }
            camera.targetTexture = previous;
            camera.aspect = aspect;
            RenderTexture.active = active;
            target.Release();
            UnityEngine.Object.Destroy(target);
            UnityEngine.Object.Destroy(screenshot);
        }

        private static void SettleEntrance(KitchenPauseMenu menu)
        {
            PaperMenuPageMotion entrance = menu.Canvas.transform.Find("PauseCard").GetComponent<PaperMenuPageMotion>();
            for (int frame = 0; frame < Mathf.CeilToInt(0.3f / Mathf.Max(0.0001f, Time.unscaledDeltaTime)); frame++) entrance.SendMessage("Update");
        }
    }
}
