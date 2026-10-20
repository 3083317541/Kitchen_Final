using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kitchen.Runtime;
using Kitchen.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Kitchen.EditorTools
{
    [InitializeOnLoad]
    public static class KitchenMainMenuVerifier
    {
        [Serializable]
        private sealed class Check { public string name; public bool passed; }
        [Serializable]
        private sealed class Report { public List<Check> checks = new List<Check>(); public int failed; }
        private const string RunningKey = "Kitchen.MainMenuVerification";
        private static Report report = new Report();
        private static int stage;
        private static double started;
        private static double next;
        private static string oldSettings;
        private static ClassicRunState previousRun;

        static KitchenMainMenuVerifier() { EditorApplication.update += Tick; }

        public static void Run()
        {
            EditorSceneManager.OpenScene(KitchenSceneGenerator.MenuScenePath);
            SessionState.SetBool(RunningKey, true);
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(RunningKey, false)) return;
            if (started == 0) started = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - started > 120)
            {
                Add("play_timeout", false);
                Finish();
                return;
            }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (next == 0) next = EditorApplication.timeSinceStartup + 1.2;
            if (EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + 0.3;
            try
            {
                switch (stage)
                {
                    case 0:
                        VerifyMenu();
                        break;
                    case 1:
                        Enter(FindButton("StartButton").gameObject);
                        break;
                    case 2:
                        VerifyButtonHover();
                        Exit(FindButton("StartButton").gameObject);
                        break;
                    case 3:
                        VerifyButtonExit();
                        Enter(UnityEngine.Object.FindObjectOfType<PaperclipReaction>().gameObject);
                        break;
                    case 4:
                        VerifyClipHover();
                        Exit(UnityEngine.Object.FindObjectOfType<PaperclipReaction>().gameObject);
                        break;
                    case 5:
                        VerifyClipExit();
                        CampaignProgress.JumpTo(2);
                        ClassicProgress.Begin();
                        previousRun = ClassicProgress.Current;
                        Click("StartButton");
                        Add("tutorial_first_click_shows_description_without_starting", OnMenu() && Active("TutorialConfirmButton") && !Active("StartButton") && CampaignProgress.LevelNumber == 2 && ClassicProgress.Current == previousRun);
                        break;
                    case 6:
                        Add("tutorial_has_three_current_instructions", UnityEngine.Object.FindObjectsOfType<TMP_Text>().Count(label => label.name.StartsWith("TutorialStepText")) == 3);
                        Capture("main-menu-tutorial.png", 1920, 1080);
                        Capture("main-menu-tutorial-1280x960.png", 1280, 960);
                        Capture("main-menu-tutorial-720x1280.png", 720, 1280);
                        ExecuteEvents.Execute(FindButton("TutorialBackButton").gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.cancelHandler);
                        Add("tutorial_cancel_returns_without_resetting_progress", Active("StartButton") && !Active("TutorialConfirmButton") && CampaignProgress.LevelNumber == 2 && ClassicProgress.Current == previousRun);
                        break;
                    case 7:
                        Click("ClassicModeButton");
                        Add("classic_first_click_shows_description_without_starting", OnMenu() && Active("ClassicConfirmButton") && !Active("ClassicModeButton") && ClassicProgress.Current == previousRun);
                        break;
                    case 8:
                        Add("classic_description_matches_daily_mode", UnityEngine.Object.FindObjectsOfType<TMP_Text>().Any(label => label.name == "ClassicIntro" && label.text.Contains("每日营业")));
                        Capture("main-menu-classic.png", 1920, 1080);
                        Capture("main-menu-classic-1280x960.png", 1280, 960);
                        Capture("main-menu-classic-720x1280.png", 720, 1280);
                        Click("ClassicBackButton");
                        Add("classic_back_keeps_existing_run", Active("ClassicModeButton") && ClassicProgress.Current == previousRun);
                        break;
                    case 9:
                        Click("StartButton");
                        break;
                    case 10:
                        Add("tutorial_description_reopens", Active("TutorialConfirmButton") && !Active("ClassicConfirmButton"));
                        Button confirm = FindButton("TutorialConfirmButton");
                        PointerEventData pointer = Pointer(confirm.gameObject);
                        ExecuteEvents.Execute(confirm.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                        ExecuteEvents.Execute(confirm.gameObject, pointer, ExecuteEvents.pointerExitHandler);
                        ExecuteEvents.Execute(confirm.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                        Add("dragging_off_confirmation_does_not_start_game", OnMenu() && ClassicProgress.Current == previousRun);
                        Click("TutorialConfirmButton");
                        break;
                    case 11:
                        GameRuntime tutorial = GameRuntime.Instance;
                        if (tutorial == null || !tutorial.IsInitialized) return;
                        Add("tutorial_opens_level_one_without_classic_state", ClassicProgress.Current == null && CampaignProgress.LevelNumber == 1);
                        Add("kitchen_applies_saved_hud_scale", Mathf.Approximately(UnityEngine.Object.FindObjectOfType<HudScaler>().Scale, 1.2f));
                        SceneManager.LoadScene("MainMenu");
                        next += 1f;
                        break;
                    case 12:
                        Click("ClassicModeButton");
                        Add("classic_entry_still_requires_confirmation_after_tutorial", OnMenu() && Active("ClassicConfirmButton") && ClassicProgress.Current == null);
                        break;
                    case 13:
                        Click("ClassicConfirmButton");
                        break;
                    case 14:
                        GameRuntime classic = GameRuntime.Instance;
                        if (classic == null || !classic.IsInitialized) return;
                        Add("classic_opens_day_one_briefing", ClassicProgress.Current != null && ClassicProgress.Current.Day == 1 && classic.Classic != null && classic.Classic.Phase == DayPhase.Briefing);
                        SceneManager.LoadScene("MainMenu");
                        next += 1f;
                        break;
                    case 15:
                        Add("returning_menu_retains_three_entries", UnityEngine.Object.FindObjectsOfType<Button>().Count(button => button.name == "StartButton" || button.name == "ClassicModeButton" || button.name == "SettingsButton") == 3);
                        Finish();
                        break;
                }
                stage++;
            }
            catch (Exception exception)
            {
                Add("exception_" + exception.Message, false);
                Debug.LogException(exception);
                Finish();
            }
        }

        private static void VerifyMenu()
        {
            oldSettings = JsonUtility.ToJson(new SavedSettings
            {
                volume = PlayerPrefs.GetFloat(PaperUISettings.VolumeKey, 0.8f),
                motion = PlayerPrefs.GetInt(PaperUISettings.MotionKey, 1),
                scale = PlayerPrefs.GetFloat(HudScaler.PrefsKey, 1f),
                hasVolume = PlayerPrefs.HasKey(PaperUISettings.VolumeKey),
                hasMotion = PlayerPrefs.HasKey(PaperUISettings.MotionKey),
                hasScale = PlayerPrefs.HasKey(HudScaler.PrefsKey)
            });
            PaperUISettings.SetMotion(true);
            MainMenuController controller = UnityEngine.Object.FindObjectOfType<MainMenuController>();
            Add("controller_and_expected_buttons_present", controller != null && FindButton("StartButton") != null && FindButton("ClassicModeButton") != null && FindButton("SettingsButton") != null);
            TMP_Text title = UnityEngine.Object.FindObjectsOfType<TMP_Text>().First(label => label.name == "TitleText");
            Add("english_title_survives_controller_refresh", title.text == "KITCHEN" && title.font.name.Contains("Lilita"));
            Add("all_menu_glyphs_available", UnityEngine.Object.FindObjectsOfType<TMP_Text>(true).All(label => label.font.HasCharacters(label.text, out uint[] missing, false, false)));
            Capture("main-menu-1920x1080.png", 1920, 1080);
            Capture("main-menu-1280x960.png", 1280, 960);
            Capture("main-menu-720x1280.png", 720, 1280);
            Click("SettingsButton");
            MainMenuSettings settings = UnityEngine.Object.FindObjectOfType<MainMenuSettings>();
            Add("settings_opens_and_hides_home", FindButton("BackButton").gameObject.activeInHierarchy && !FindButton("StartButton").gameObject.activeInHierarchy);
            Slider volume = settings.GetComponentsInChildren<Slider>().First(slider => slider.name == "Volume");
            Slider scale = settings.GetComponentsInChildren<Slider>().First(slider => slider.name == "HudScale");
            Toggle motion = settings.GetComponentInChildren<Toggle>();
            volume.value = 0.37f;
            scale.value = 1.2f;
            motion.isOn = false;
            Add("volume_applies_and_persists", Mathf.Approximately(AudioListener.volume, 0.37f) && Mathf.Approximately(PlayerPrefs.GetFloat(PaperUISettings.VolumeKey), 0.37f));
            Add("hud_scale_persists_for_kitchen", Mathf.Approximately(PlayerPrefs.GetFloat(HudScaler.PrefsKey), 1.2f));
            Add("motion_preference_persists", !PaperUISettings.MotionEnabled);
            Add("slider_handle_remains_round", Mathf.Abs(volume.handleRect.rect.width - volume.handleRect.rect.height) < 0.1f);
            Capture("main-menu-settings.png", 1920, 1080);
            Click("BackButton");
            Button tutorial = FindButton("StartButton");
            RectTransform buttonRoot = tutorial.GetComponent<RectTransform>();
            Vector2 originalPosition = buttonRoot.anchoredPosition;
            PointerEventData pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            PaperButtonMotion animation = tutorial.GetComponent<PaperButtonMotion>();
            EventSystem.current.SetSelectedGameObject(null);
            Image face = tutorial.transform.Find("Visual/Face").GetComponent<Image>();
            Color originalFace = face.color;
            animation.OnPointerEnter(pointer);
            animation.SendMessage("Update");
            Add("reduced_motion_retains_hover_color_feedback", face.color != originalFace && tutorial.transform.Find("Visual").localScale == Vector3.one);
            animation.OnPointerDown(pointer);
            Add("reduced_motion_keeps_button_visual_still", tutorial.transform.Find("Visual").localScale == Vector3.one);
            PaperUISettings.SetMotion(true);
            animation.OnPointerDown(pointer);
            Add("button_press_animates_visual_without_moving_layout", tutorial.transform.Find("Visual").localScale.y < 1f && buttonRoot.anchoredPosition == originalPosition);
            animation.OnPointerUp(pointer);
            animation.OnPointerExit(pointer);
            PaperclipReaction clip = UnityEngine.Object.FindObjectOfType<PaperclipReaction>();
            clip.OnPointerClick(pointer);
            Add("paperclip_click_shows_speech", clip.transform.parent.Find("PaperclipBubble").gameObject.activeSelf);
            clip.gameObject.SetActive(false);
            clip.gameObject.SetActive(true);
            settings.Open();
            Add("settings_reopen_retains_values", Mathf.Approximately(volume.value, 0.37f) && Mathf.Approximately(scale.value, 1.2f) && motion.isOn);
            ExecuteEvents.Execute(FindButton("BackButton").gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.cancelHandler);
            Add("cancel_returns_to_home", tutorial.gameObject.activeInHierarchy && !FindButton("BackButton").gameObject.activeInHierarchy);
            EventSystem.current.SetSelectedGameObject(null);
        }

        private static bool OnMenu() => SceneManager.GetActiveScene().name == "MainMenu";
        private static bool Active(string name) => FindButton(name).gameObject.activeInHierarchy;

        private static PointerEventData Pointer(GameObject target)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform rect = target.GetComponent<RectTransform>();
            Canvas canvas = target.GetComponentInParent<Canvas>();
            Vector2 position = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, rect.TransformPoint(rect.rect.center));
            return new PointerEventData(EventSystem.current) { position = position, button = PointerEventData.InputButton.Left };
        }

        private static void Enter(GameObject target)
        {
            PointerEventData pointer = Pointer(target);
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Add(target.name + "_receives_pointer_raycast", hits.Count > 0 && (hits[0].gameObject == target || hits[0].gameObject.transform.IsChildOf(target.transform)));
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerEnterHandler);
        }

        private static void Exit(GameObject target) => ExecuteEvents.Execute(target, Pointer(target), ExecuteEvents.pointerExitHandler);

        private static void Click(string name)
        {
            GameObject target = FindButton(name).gameObject;
            Enter(target);
            PointerEventData pointer = Pointer(target);
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static void VerifyButtonHover()
        {
            Button button = FindButton("StartButton");
            RectTransform visual = button.transform.Find("Visual") as RectTransform;
            Add("button_hover_lifts_scales_and_tilts_visual", visual.localScale.x > 1.015f && visual.anchoredPosition.y > 2f && Quaternion.Angle(visual.localRotation, Quaternion.identity) > 0.3f);
            Add("button_hover_changes_face_color", visual.Find("Face").GetComponent<Image>().color.b < 0.98f);
            Capture("main-menu-button-hover.png", 1920, 1080);
        }

        private static void VerifyButtonExit()
        {
            RectTransform visual = FindButton("StartButton").transform.Find("Visual") as RectTransform;
            Add("button_pointer_exit_restores_visual", Mathf.Abs(visual.localScale.x - 1f) < 0.006f && Mathf.Abs(visual.anchoredPosition.y) < 0.3f);
        }

        private static void VerifyClipHover()
        {
            PaperclipReaction clip = UnityEngine.Object.FindObjectOfType<PaperclipReaction>();
            Add("paperclip_hover_rotates_visual", Quaternion.Angle(clip.transform.Find("Visual").localRotation, Quaternion.identity) > 5f);
            Capture("main-menu-paperclip-hover.png", 1920, 1080);
        }

        private static void VerifyClipExit()
        {
            PaperclipReaction clip = UnityEngine.Object.FindObjectOfType<PaperclipReaction>();
            Add("paperclip_pointer_exit_restores_rotation", Quaternion.Angle(clip.transform.Find("Visual").localRotation, Quaternion.identity) < 0.5f);
        }

        private static Button FindButton(string name) => UnityEngine.Object.FindObjectsOfType<Button>(true).First(button => button.name == name);

        private static void Capture(string name, int width, int height)
        {
            Canvas canvas = UnityEngine.Object.FindObjectOfType<MainMenuController>().GetComponentInChildren<Canvas>();
            Camera camera = Camera.main;
            RenderTexture target = new RenderTexture(width, height, 24);
            RenderTexture previous = camera.targetTexture;
            RenderTexture active = RenderTexture.active;
            float aspect = camera.aspect;
            camera.targetTexture = target;
            camera.aspect = (float)width / height;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 2f;
            Canvas.ForceUpdateCanvases();
            canvas.GetComponent<PaperMenuLayout>().RefreshLayout();
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            Texture2D screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
            screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            screenshot.Apply();
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes("Logs/" + name, screenshot.EncodeToPNG());
            Vector3[] corners = new Vector3[4];
            bool contained = true;
            foreach (Button button in canvas.GetComponentsInChildren<Button>())
            {
                button.GetComponent<RectTransform>().GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector3 screen = camera.WorldToViewportPoint(corner);
                    contained &= screen.x >= 0f && screen.x <= 1f && screen.y >= 0f && screen.y <= 1f;
                }
            }
            Add(name + "_buttons_inside_viewport", contained);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            camera.targetTexture = previous;
            camera.aspect = aspect;
            RenderTexture.active = active;
            target.Release();
            UnityEngine.Object.Destroy(target);
            UnityEngine.Object.Destroy(screenshot);
            Canvas.ForceUpdateCanvases();
            canvas.GetComponent<PaperMenuLayout>().RefreshLayout();
        }

        [Serializable]
        private sealed class SavedSettings
        {
            public float volume;
            public float scale;
            public int motion;
            public bool hasVolume;
            public bool hasScale;
            public bool hasMotion;
        }

        private static void RestoreSettings()
        {
            if (string.IsNullOrEmpty(oldSettings)) return;
            SavedSettings saved = JsonUtility.FromJson<SavedSettings>(oldSettings);
            if (saved.hasVolume) PlayerPrefs.SetFloat(PaperUISettings.VolumeKey, saved.volume); else PlayerPrefs.DeleteKey(PaperUISettings.VolumeKey);
            if (saved.hasScale) PlayerPrefs.SetFloat(HudScaler.PrefsKey, saved.scale); else PlayerPrefs.DeleteKey(HudScaler.PrefsKey);
            if (saved.hasMotion) PlayerPrefs.SetInt(PaperUISettings.MotionKey, saved.motion); else PlayerPrefs.DeleteKey(PaperUISettings.MotionKey);
            AudioListener.volume = saved.volume;
            PlayerPrefs.Save();
        }

        private static void Add(string name, bool passed)
        {
            report.checks.Add(new Check { name = name, passed = passed });
            if (!passed) report.failed++;
            Debug.Log("[MainMenu] " + (passed ? "PASS " : "FAIL ") + name);
        }

        private static void Finish()
        {
            RestoreSettings();
            SessionState.SetBool(RunningKey, false);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/main_menu_report.json", JsonUtility.ToJson(report, true));
            if (Application.isBatchMode) EditorApplication.Exit(report.failed == 0 ? 0 : 1);
            else EditorApplication.isPlaying = false;
        }
    }
}
