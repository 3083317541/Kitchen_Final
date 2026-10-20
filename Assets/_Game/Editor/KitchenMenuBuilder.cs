using Kitchen.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kitchen.EditorTools
{
    public static class KitchenMenuBuilder
    {
        public const string SceneName = "MainMenu";
        public const string TutorialIntro = "从第一份沙拉开始，熟悉你的小厨房。";
        public const string ClassicIntro = "安排厨师，搭建厨房。\n挑战每日营业，争取更高得分。";
        public static readonly string[] TutorialSteps = { "选择厨师，走到机器正面", "右键机器，选择加工配方", "备齐食材，完成第一份订单" };
        public static readonly string[] ClassicSteps = { "开门前调整设备与厨师站位", "限时营业，完成顾客订单", "打烊结算，选择技能迎接明天" };
        private static readonly Color BackgroundColor = new Color32(18, 18, 18, 255);

        public static MainMenuController Build(TMP_FontAsset fontAsset)
        {
            GameObject root = new GameObject("MainMenu");
            MainMenuController controller = root.AddComponent<MainMenuController>();
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(root.transform, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
            cameraObject.tag = "MainCamera";
            UpdateUI(controller, fontAsset);
            return controller;
        }

        public static void UpdateUI(MainMenuController controller, TMP_FontAsset fontAsset)
        {
            Transform oldCanvas = controller.transform.Find("MenuCanvas");
            if (oldCanvas != null) Object.DestroyImmediate(oldCanvas.gameObject);
            RectTransform canvas = new GameObject("MenuCanvas", typeof(RectTransform)).GetComponent<RectTransform>();
            canvas.SetParent(controller.transform, false);
            canvas.gameObject.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                GameObject events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                events.transform.SetParent(controller.transform, false);
            }
            BuildContent(canvas, controller);
        }

        private static void BuildContent(RectTransform canvas, MainMenuController controller)
        {
            TMP_FontAsset chinese = KitchenMenuFonts.Chinese;
            TMP_FontAsset english = KitchenMenuFonts.Title;
            RectTransform background = Rect(canvas, "Backdrop", Vector2.zero, Vector2.zero);
            Stretch(background);
            background.gameObject.AddComponent<Image>().color = BackgroundColor;
            RectTransform dots = Rect(background, "Dots", Vector2.zero, Vector2.zero);
            Stretch(dots);
            PaperDotBackground pattern = dots.gameObject.AddComponent<PaperDotBackground>();
            pattern.color = new Color(1f, 1f, 1f, 0.08f);
            pattern.raycastTarget = false;

            RectTransform composition = Rect(canvas, "Composition", new Vector2(1460f, 740f), new Vector2(0f, 20f));
            composition.anchorMin = composition.anchorMax = composition.pivot = new Vector2(0.5f, 0.5f);
            RectTransform brand = Rect(composition, "Brand", new Vector2(720f, 470f), new Vector2(0f, -130f));
            RectTransform brandVisual = Rect(brand, "Visual", brand.sizeDelta, Vector2.zero);
            TMP_Text kitchen = Text(brandVisual, "TitleText", "KITCHEN", english, 174f, new Vector2(720f, 195f), new Vector2(0f, -15f), Color.white);
            kitchen.characterSpacing = 1.5f;
            kitchen.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 3f);
            TMP_Text club = Text(brandVisual, "ClubText", "CLUB", english, 210f, new Vector2(650f, 230f), new Vector2(10f, -185f), PaperUITheme.Yellow);
            club.characterSpacing = 6f;
            club.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -2f);
            Text(brandVisual, "Tagline", "小小厨房，开饭啦。", chinese, 29f, new Vector2(650f, 58f), new Vector2(14f, -415f), new Color32(201, 195, 185, 255)).characterSpacing = 5f;
            Icon(brandVisual, "ChefHat", PaperMenuIcon.Shape.Hat, new Vector2(104f, 104f), new Vector2(623f, 50f), Color.white).transform.localRotation = Quaternion.Euler(0f, 0f, -16f);
            brand.gameObject.AddComponent<PaperMenuEntrance>().Configure(brandVisual, 0f);

            RectTransform card = Rect(composition, "MenuCard", new Vector2(620f, 620f), new Vector2(820f, -80f));
            RectTransform cardVisual = Rect(card, "Visual", card.sizeDelta, Vector2.zero);
            cardVisual.localRotation = Quaternion.Euler(0f, 0f, -1f);
            PaperUITheme.Surface(cardVisual.gameObject, Color.white);
            RectTransform tab = Rect(cardVisual, "TitleTab", new Vector2(335f, 88f), new Vector2(-12f, 39f));
            PaperUITheme.Surface(tab.gameObject, PaperUITheme.Yellow);
            tab.localRotation = Quaternion.Euler(0f, 0f, 2f);
            Text(tab, "TitleLabel", "LET’S COOK!", english, 44f, new Vector2(290f, 70f), new Vector2(25f, -8f), PaperUITheme.TitleInk);
            PaperUITheme.AddPaperclip(cardVisual, chinese);
            RectTransform bubble = cardVisual.Find("PaperclipBubble") as RectTransform;
            bubble.anchorMin = bubble.anchorMax = new Vector2(1f, 1f);
            bubble.anchoredPosition = new Vector2(-346f, 142f);

            RectTransform home = Rect(cardVisual, "Home", card.sizeDelta, Vector2.zero);
            Button tutorial = MenuButton(home, "StartButton", "教程", "TUTORIAL", PaperMenuIcon.Shape.Book, 98f, false, chinese, english);
            Button classic = MenuButton(home, "ClassicModeButton", "经典模式", "CLASSIC MODE", PaperMenuIcon.Shape.Hat, 254f, true, chinese, english);
            Button settings = MenuButton(home, "SettingsButton", "设置", "SETTINGS", PaperMenuIcon.Shape.Settings, 410f, false, chinese, english);
            TMP_Text progress = Text(home, "ProgressText", "从第一份沙拉开始", chinese, 21f, new Vector2(530f, 40f), new Vector2(45f, -562f), PaperUITheme.Muted);
            progress.alignment = TextAlignmentOptions.Center;
            BuildSettings(cardVisual, home, settings, chinese, english);
            RectTransform tutorialPage = BuildDetails(cardVisual, "Tutorial", "教程", TutorialIntro, TutorialSteps,
                "开始教程", chinese, out Button tutorialBack, out Button tutorialConfirm);
            RectTransform classicPage = BuildDetails(cardVisual, "Classic", "经典模式", ClassicIntro, ClassicSteps,
                "开始营业", chinese, out Button classicBack, out Button classicConfirm);
            cardVisual.gameObject.AddComponent<MainMenuDetails>().Configure(home.gameObject, tutorialPage.gameObject,
                classicPage.gameObject, tutorial, classic, tutorialBack, classicBack);
            card.gameObject.AddComponent<PaperMenuEntrance>().Configure(cardVisual, 0.1f);
            canvas.gameObject.AddComponent<PaperMenuLayout>().Configure(composition, brand, card);

            TMP_Text footer = Text(canvas, "Footer", "MADE WITH A LITTLE LOVE", english, 19f, new Vector2(550f, 42f), new Vector2(44f, 23f), new Color32(142, 139, 130, 255));
            footer.rectTransform.anchorMin = footer.rectTransform.anchorMax = Vector2.zero;
            footer.rectTransform.pivot = Vector2.zero;
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("startButton").objectReferenceValue = tutorialConfirm;
            serialized.FindProperty("classicButton").objectReferenceValue = classicConfirm;
            serialized.FindProperty("quitButton").objectReferenceValue = null;
            serialized.FindProperty("progressText").objectReferenceValue = progress;
            serialized.FindProperty("titleText").objectReferenceValue = kitchen;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        private static void BuildSettings(RectTransform card, RectTransform home, Button open, TMP_FontAsset chinese, TMP_FontAsset english)
        {
            RectTransform panel = Rect(card, "Settings", card.sizeDelta, Vector2.zero);
            Text(panel, "Heading", "设置", chinese, 41f, new Vector2(530f, 60f), new Vector2(45f, -90f), PaperUITheme.Ink);
            Slider volume = SliderRow(panel, "Volume", "全局音量", 185f, 0f, 1f, chinese, out TMP_Text volumeValue);
            Slider scale = SliderRow(panel, "HudScale", "厨房界面缩放", 299f, HudScaler.MinScale, HudScaler.MaxScale, chinese, out TMP_Text scaleValue);
            Text(panel, "MotionLabel", "弹性动效", chinese, 28f, new Vector2(350f, 48f), new Vector2(45f, -413f), PaperUITheme.Ink);
            RectTransform toggleRoot = Rect(panel, "MotionToggle", new Vector2(85f, 46f), new Vector2(490f, -414f));
            Image track = PaperUITheme.Surface(toggleRoot.gameObject, new Color32(235, 232, 224, 255));
            Toggle toggle = toggleRoot.gameObject.AddComponent<Toggle>();
            toggle.toggleTransition = Toggle.ToggleTransition.None;
            toggle.targetGraphic = track;
            RectTransform mark = Rect(toggleRoot, "Enabled", new Vector2(75f, 36f), new Vector2(5f, -5f));
            toggle.graphic = PaperUITheme.Surface(mark.gameObject, PaperUITheme.Yellow);
            Text(mark, "On", "ON", english, 23f, mark.sizeDelta, Vector2.zero, PaperUITheme.TitleInk).alignment = TextAlignmentOptions.Center;
            Button back = SmallButton(panel, "BackButton", "返回", chinese, new Vector2(530f, 76f), new Vector2(45f, -492f));
            Text(panel, "SavedHint", "自动保存 · 缩放仅调整游戏内界面", chinese, 19f, new Vector2(530f, 32f), new Vector2(45f, -576f), PaperUITheme.Muted).alignment = TextAlignmentOptions.Center;
            card.gameObject.AddComponent<MainMenuSettings>().Configure(home.gameObject, panel.gameObject, open, back, volume, scale, toggle, volumeValue, scaleValue);
            panel.gameObject.SetActive(false);
        }

        private static RectTransform BuildDetails(RectTransform card, string name, string heading, string intro,
            string[] steps, string confirmation, TMP_FontAsset font, out Button back, out Button confirm)
        {
            RectTransform panel = Rect(card, name + "Details", card.sizeDelta, Vector2.zero);
            RectTransform visual = Rect(panel, "Visual", card.sizeDelta, Vector2.zero);
            back = SmallButton(visual, name + "BackButton", "", font, new Vector2(64f, 64f), new Vector2(45f, -92f));
            Image backTray = back.targetGraphic as Image;
            backTray.color = new Color32(246, 243, 237, 255);
            ColorBlock backColors = back.colors;
            backColors.highlightedColor = Color.white;
            back.colors = backColors;
            back.transform.Find("Visual/Face").GetComponent<Image>().color = new Color32(246, 243, 237, 255);
            PaperMenuIcon backIcon = Icon(back.transform.Find("Visual"), "Arrow", PaperMenuIcon.Shape.Arrow,
                new Vector2(28f, 28f), new Vector2(18f, -18f), PaperUITheme.TitleInk);
            backIcon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            backIcon.rectTransform.anchoredPosition = new Vector2(32f, -32f);
            backIcon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 135f);
            Text(visual, name + "Heading", heading, font, 39f, new Vector2(445f, 64f), new Vector2(131f, -92f), PaperUITheme.Ink);
            TMP_Text description = Text(visual, name + "Intro", intro, font, 25f, new Vector2(530f, 88f),
                new Vector2(45f, -188f), PaperUITheme.Muted);
            description.enableWordWrapping = true;
            description.alignment = TextAlignmentOptions.TopLeft;
            for (int index = 0; index < steps.Length; index++)
            {
                float top = 292f + index * 66f;
                RectTransform badge = Rect(visual, name + "Step" + index, new Vector2(44f, 44f), new Vector2(45f, -top));
                Image badgeImage = PaperUITheme.Surface(badge.gameObject, new Color32(255, 243, 214, 255));
                badgeImage.pixelsPerUnitMultiplier = 1.6f;
                badgeImage.raycastTarget = false;
                Text(badge, "Number", (index + 1).ToString(), font, 25f, badge.sizeDelta, Vector2.zero, PaperUITheme.TitleInk).alignment = TextAlignmentOptions.Center;
                Text(visual, name + "StepText" + index, steps[index], font, 25f, new Vector2(466f, 48f),
                    new Vector2(107f, -top), PaperUITheme.Muted);
            }
            confirm = SmallButton(visual, name + "ConfirmButton", confirmation, font,
                new Vector2(530f, 96f), new Vector2(45f, -492f));
            confirm.transform.Find("Visual/Face").GetComponent<Image>().color = PaperUITheme.Yellow;
            TMP_Text confirmLabel = confirm.transform.Find("Visual/Label").GetComponent<TMP_Text>();
            confirmLabel.alignment = TextAlignmentOptions.MidlineLeft;
            confirmLabel.rectTransform.sizeDelta = new Vector2(395f, 96f);
            confirmLabel.rectTransform.anchoredPosition = new Vector2(32f, 0f);
            confirmLabel.fontSize = confirmLabel.fontSizeMax = 32f;
            PaperMenuIcon arrow = Icon(confirm.transform.Find("Visual"), "Arrow", PaperMenuIcon.Shape.Arrow,
                new Vector2(32f, 32f), new Vector2(461f, -32f), PaperUITheme.TitleInk);
            arrow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            arrow.rectTransform.anchoredPosition = new Vector2(477f, -48f);
            arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
            panel.gameObject.AddComponent<PaperMenuPageMotion>().Configure(visual);
            panel.gameObject.SetActive(false);
            return panel;
        }

        private static Slider SliderRow(Transform parent, string name, string caption, float top, float minimum, float maximum, TMP_FontAsset font, out TMP_Text value)
        {
            Text(parent, name + "Label", caption, font, 27f, new Vector2(390f, 43f), new Vector2(45f, -top), PaperUITheme.Ink);
            value = Text(parent, name + "Value", "100%", font, 23f, new Vector2(130f, 43f), new Vector2(445f, -top), PaperUITheme.Muted);
            value.alignment = TextAlignmentOptions.Right;
            RectTransform root = Rect(parent, name, new Vector2(530f, 44f), new Vector2(45f, -top - 48f));
            root.gameObject.AddComponent<Image>().color = Color.clear;
            Slider slider = root.gameObject.AddComponent<Slider>();
            slider.minValue = minimum;
            slider.maxValue = maximum;
            RectTransform track = Rect(root, "Track", new Vector2(530f, 12f), new Vector2(0f, -16f));
            PaperUITheme.Surface(track.gameObject, new Color32(237, 234, 228, 255)).raycastTarget = false;
            RectTransform area = Rect(root, "HandleArea", new Vector2(498f, 32f), new Vector2(16f, -6f));
            RectTransform handle = Rect(area, "Handle", new Vector2(32f, 0f), Vector2.zero);
            handle.anchorMin = handle.anchorMax = handle.pivot = new Vector2(0.5f, 0.5f);
            Image handleImage = PaperUITheme.Surface(handle.gameObject, PaperUITheme.Yellow);
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            return slider;
        }

        private static Button MenuButton(Transform parent, string name, string caption, string subtitle, PaperMenuIcon.Shape icon, float top, bool primary, TMP_FontAsset chinese, TMP_FontAsset english)
        {
            Button button = SmallButton(parent, name, "", chinese, new Vector2(530f, 132f), new Vector2(45f, -top));
            Transform visual = button.transform.Find("Visual");
            if (primary) visual.Find("Face").GetComponent<Image>().color = PaperUITheme.Yellow;
            TMP_Text label = visual.Find("Label").GetComponent<TMP_Text>();
            label.text = caption;
            label.fontSize = label.fontSizeMax = 37f;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0f, 1f);
            label.rectTransform.pivot = new Vector2(0f, 1f);
            label.rectTransform.sizeDelta = new Vector2(320f, 55f);
            label.rectTransform.anchoredPosition = new Vector2(106f, -24f);
            Text(visual, "English", subtitle, english, 20f, new Vector2(330f, 31f), new Vector2(108f, -76f), PaperUITheme.TitleInk).characterSpacing = 2f;
            Icon(visual, "Icon", icon, new Vector2(46f, 46f), new Vector2(33f, -43f), PaperUITheme.TitleInk);
            Icon(visual, "Arrow", PaperMenuIcon.Shape.Arrow, new Vector2(34f, 34f), new Vector2(456f, -49f), PaperUITheme.TitleInk);
            return button;
        }

        private static Button SmallButton(Transform parent, string name, string caption, TMP_FontAsset font, Vector2 size, Vector2 position)
        {
            RectTransform root = Rect(parent, name, size, position);
            root.gameObject.AddComponent<Image>();
            Button button = root.gameObject.AddComponent<Button>();
            Text(root, "Label", caption, font, 30f, size, Vector2.zero, PaperUITheme.Ink).alignment = TextAlignmentOptions.Center;
            PaperUITheme.StyleButton(button);
            return button;
        }

        private static TMP_Text Text(Transform parent, string name, string content, TMP_FontAsset font, float size, Vector2 bounds, Vector2 position, Color color)
        {
            TMP_Text label = PaperUITheme.Label(parent, name, content, font, size, bounds, position);
            label.color = color;
            label.enableWordWrapping = false;
            return label;
        }

        private static PaperMenuIcon Icon(Transform parent, string name, PaperMenuIcon.Shape shape, Vector2 size, Vector2 position, Color color)
        {
            PaperMenuIcon icon = Rect(parent, name, size, position).gameObject.AddComponent<PaperMenuIcon>();
            icon.Configure(shape);
            icon.color = color;
            icon.raycastTarget = false;
            return icon;
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 size, Vector2 position) => PaperUITheme.Rect(parent, name, size, position);

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
