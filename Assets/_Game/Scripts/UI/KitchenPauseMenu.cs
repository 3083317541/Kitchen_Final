using Kitchen.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kitchen.UI
{
    public sealed class KitchenPauseMenu : MonoBehaviour
    {
        private GameRuntime runtime;
        private HudScaler hudScaler;
        private TMP_FontAsset font;
        private Sprite rounded;
        private Button buttonTemplate;
        private CanvasGroup hudGroup;
        private bool hudInteractable;
        private GameObject overlay;
        private GameObject home;
        private GameObject settings;
        private GameObject confirmation;
        private GameObject previousSelection;
        private Button resume;
        private Button settingsBack;
        private Button confirmBack;
        private TMP_Text title;
        private TMP_Text confirmationText;
        private TMP_Text motionLabel;
        private TMP_Text volumeValue;
        private TMP_Text scaleValue;
        private Slider volume;
        private Slider scale;
        private bool quitting;

        public bool SettingsOpen => settings != null && settings.activeSelf;
        public Canvas Canvas { get; private set; }

        public void Initialize(GameRuntime owner, TMP_Text fontTemplate, Button template, Image surface, HudScaler scaler)
        {
            runtime = owner;
            hudScaler = scaler;
            font = fontTemplate.font;
            rounded = surface.sprite;
            buttonTemplate = template;
            Canvas hudCanvas = surface.GetComponentInParent<Canvas>();
            hudGroup = hudCanvas.GetComponent<CanvasGroup>();
            if (hudGroup == null) hudGroup = hudCanvas.gameObject.AddComponent<CanvasGroup>();
            overlay = new GameObject("PauseCanvas", typeof(RectTransform));
            Canvas = overlay.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 200;
            CanvasScaler canvasScaler = overlay.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasScaler.matchWidthOrHeight = 0.5f;
            overlay.AddComponent<GraphicRaycaster>();
            RectTransform shade = Rect(overlay.transform, "Backdrop", Vector2.zero, Vector2.zero);
            Stretch(shade);
            shade.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.06f, 0.08f, 0.64f);
            RectTransform layout = Rect(overlay.transform, "PauseCard", new Vector2(740f, 700f), Vector2.zero);
            layout.anchorMin = layout.anchorMax = layout.pivot = new Vector2(0.5f, 0.5f);
            RectTransform visual = Rect(layout, "Visual", layout.sizeDelta, Vector2.zero);
            visual.anchorMin = visual.anchorMax = visual.pivot = new Vector2(0.5f, 0.5f);
            Surface(visual, Color.white);
            RectTransform tab = Rect(visual, "TitleTab", new Vector2(310f, 76f), new Vector2(-10f, 28f));
            Surface(tab, new Color32(249, 213, 128, 255));
            title = Label(tab, "TitleLabel", "休息一下", 36f, new Vector2(270f, 68f), new Vector2(24f, -4f));
            home = Rect(visual, "PauseHome", visual.sizeDelta, Vector2.zero).gameObject;
            Label(home.transform, "Heading", "厨房暂停中", 34f, new Vector2(644f, 56f), new Vector2(48f, -66f));
            resume = Button(home.transform, "ResumeGame", "继续游戏", 158f, PaperButtonStyle.Positive);
            resume.onClick.AddListener(() => runtime.SetPaused(false));
            Button open = Button(home.transform, "PauseSettings", "设置", 268f, PaperButtonStyle.Primary);
            open.onClick.AddListener(OpenSettings);
            Button menu = Button(home.transform, "PauseReturnToMenu", "返回主界面", 378f, PaperButtonStyle.Neutral);
            menu.onClick.AddListener(() => OpenConfirmation(false));
            Button quit = Button(home.transform, "PauseQuitGame", "退出游戏", 488f, PaperButtonStyle.Danger);
            quit.onClick.AddListener(() => OpenConfirmation(true));
            Label(home.transform, "Hint", "Esc 继续 · 厨房和订单计时已暂停", 23f,
                new Vector2(644f, 44f), new Vector2(48f, -620f)).alignment = TextAlignmentOptions.Center;
            BuildSettings(visual);
            confirmation = Rect(visual, "PauseConfirmation", visual.sizeDelta, Vector2.zero).gameObject;
            confirmationText = Label(confirmation.transform, "ConfirmationText", "", 31f,
                new Vector2(644f, 220f), new Vector2(48f, -120f));
            confirmationText.alignment = TextAlignmentOptions.Center;
            Button confirm = Button(confirmation.transform, "PauseConfirmExit", "确认离开", 390f, PaperButtonStyle.Danger);
            confirm.onClick.AddListener(() =>
            {
                if (quitting) runtime.QuitGame();
                else runtime.ReturnToMainMenu();
            });
            confirmBack = Button(confirmation.transform, "PauseCancelExit", "留下继续", 500f, PaperButtonStyle.Neutral);
            confirmBack.onClick.AddListener(ShowHome);
            layout.gameObject.AddComponent<PaperMenuPageMotion>().Configure(visual);
            overlay.SetActive(false);
        }

        public void HandleEscape()
        {
            if (!runtime.IsPaused) runtime.SetPaused(true);
            else if (!home.activeSelf) ShowHome();
            else runtime.SetPaused(false);
        }

        public void SetVisible(bool visible)
        {
            if (visible)
            {
                hudInteractable = hudGroup.interactable;
                hudGroup.interactable = false;
                previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                overlay.SetActive(true);
                ShowHome();
            }
            else
            {
                overlay.SetActive(false);
                hudGroup.interactable = hudInteractable;
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
                previousSelection = null;
            }
        }

        private void ShowHome()
        {
            home.SetActive(true);
            settings.SetActive(false);
            confirmation.SetActive(false);
            title.text = "休息一下";
            Select(resume);
        }

        private void OpenSettings()
        {
            home.SetActive(false);
            settings.SetActive(true);
            title.text = "设置";
            volume.SetValueWithoutNotify(PaperUISettings.Volume);
            scale.SetValueWithoutNotify(hudScaler != null ? hudScaler.Scale : PlayerPrefs.GetFloat(HudScaler.PrefsKey, 1f));
            RefreshValues();
            Select(settingsBack);
        }

        private void OpenConfirmation(bool quit)
        {
            quitting = quit;
            home.SetActive(false);
            confirmation.SetActive(true);
            title.text = quit ? "退出游戏" : "返回主界面";
            confirmationText.text = (quit ? "准备收工了吗？" : "要回到主界面吗？") + "\n\n本轮进度不会保存。";
            Select(confirmBack);
        }

        private void BuildSettings(Transform parent)
        {
            settings = Rect(parent, "PauseSettingsPage", new Vector2(740f, 700f), Vector2.zero).gameObject;
            volume = Slider(settings.transform, "PauseVolume", "全局音量", 110f, 0f, 1f, out volumeValue);
            scale = Slider(settings.transform, "PauseHudScale", "厨房界面缩放", 260f, HudScaler.MinScale, HudScaler.MaxScale, out scaleValue);
            volume.onValueChanged.AddListener(value => { PaperUISettings.SetVolume(value); RefreshValues(); });
            scale.onValueChanged.AddListener(value =>
            {
                if (hudScaler != null) hudScaler.SetScale(value);
                else { PlayerPrefs.SetFloat(HudScaler.PrefsKey, value); PlayerPrefs.Save(); }
                RefreshValues();
            });
            Button motion = Button(settings.transform, "PauseMotion", "", 410f, PaperButtonStyle.Choice);
            motionLabel = motion.GetComponentInChildren<TMP_Text>(true);
            motion.onClick.AddListener(() => { PaperUISettings.SetMotion(!PaperUISettings.MotionEnabled); RefreshValues(); });
            settingsBack = Button(settings.transform, "PauseSettingsBack", "返回暂停菜单", 520f, PaperButtonStyle.Neutral);
            settingsBack.onClick.AddListener(ShowHome);
            Label(settings.transform, "SavedHint", "自动保存 · Esc 返回暂停菜单", 23f,
                new Vector2(644f, 44f), new Vector2(48f, -628f)).alignment = TextAlignmentOptions.Center;
        }

        private void RefreshValues()
        {
            volumeValue.text = Mathf.RoundToInt(volume.value * 100f) + "%";
            scaleValue.text = Mathf.RoundToInt(scale.value * 100f) + "%";
            motionLabel.text = "弹性动效 · " + (PaperUISettings.MotionEnabled ? "已开启" : "已关闭");
        }

        private Slider Slider(Transform parent, string name, string caption, float top, float minimum, float maximum, out TMP_Text value)
        {
            Label(parent, name + "Label", caption, 30f, new Vector2(480f, 48f), new Vector2(48f, -top));
            value = Label(parent, name + "Value", "", 27f, new Vector2(130f, 48f), new Vector2(562f, -top));
            value.alignment = TextAlignmentOptions.Right;
            RectTransform root = Rect(parent, name, new Vector2(644f, 58f), new Vector2(48f, -top - 62f));
            root.gameObject.AddComponent<Image>().color = Color.clear;
            Slider slider = root.gameObject.AddComponent<Slider>();
            slider.minValue = minimum;
            slider.maxValue = maximum;
            RectTransform track = Rect(root, "Track", new Vector2(644f, 14f), new Vector2(0f, -22f));
            Surface(track, PaperButtonStyle.Neutral);
            RectTransform area = Rect(root, "HandleArea", new Vector2(608f, 36f), new Vector2(18f, -11f));
            RectTransform handle = Rect(area, "Handle", new Vector2(36f, 0f), Vector2.zero);
            handle.anchorMin = handle.anchorMax = handle.pivot = new Vector2(0.5f, 0.5f);
            Image handleImage = Surface(handle, PaperButtonStyle.Primary);
            slider.targetGraphic = handleImage;
            slider.handleRect = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            ColorBlock colors = slider.colors;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.78f, 0.83f, 0.9f);
            slider.colors = colors;
            return slider;
        }

        private Button Button(Transform parent, string name, string caption, float top, Color color)
        {
            Button button = Instantiate(buttonTemplate, parent);
            button.name = name;
            button.gameObject.SetActive(true);
            button.interactable = true;
            button.onClick = new Button.ButtonClickedEvent();
            RectTransform rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(48f, -top);
            rect.sizeDelta = new Vector2(644f, 88f);
            PaperButtonStyle.Apply(button, color);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            label.text = caption;
            label.font = font;
            label.fontSharedMaterial = font.material;
            label.fontSize = label.fontSizeMax = 32f;
            label.fontSizeMin = 24f;
            label.alignment = TextAlignmentOptions.Center;
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(18f, 8f);
            label.rectTransform.offsetMax = new Vector2(-18f, -8f);
            return button;
        }

        private TMP_Text Label(Transform parent, string name, string text, float size, Vector2 bounds, Vector2 position)
        {
            TMP_Text label = Rect(parent, name, bounds, position).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSharedMaterial = font.material;
            label.text = text;
            label.fontSize = size;
            label.color = PaperButtonStyle.Ink;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;
            return label;
        }

        private Image Surface(RectTransform rect, Color color)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 size, Vector2 position)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void Select(Button button)
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        private void OnDestroy()
        {
            if (overlay != null) Destroy(overlay);
        }
    }
}
