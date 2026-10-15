using System;
using System.Collections.Generic;
using Kitchen.Runtime;
using Kitchen.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kitchen.EditorTools
{
    /// <summary>
    /// Builds the HUD: TopBar, OrderPanel, CookerShopPanel, SelectionPanel, RecipeProgressPanel and
    /// NotificationPanel, then binds every reference on <see cref="UIManager"/>.
    ///
    /// The layout is built from Unity layout groups rather than hand placed rectangles. That is what
    /// keeps the labels from overlapping: a group measures its text children and spaces them out, so
    /// a long label can never land on top of its neighbour. The panels are anchored to the four edges
    /// of the screen so they stay put at any window size, and the centre of the screen is left clear
    /// for the kitchen.
    /// </summary>
    public static class KitchenUIBuilder
    {
        public const float CanvasWidth = 1920f;
        public const float CanvasHeight = 1080f;

        // Panel palette
        private static readonly Color PanelColor = new Color(0.07f, 0.08f, 0.11f, 0.93f);
        private static readonly Color CardColor = new Color(0.13f, 0.15f, 0.19f, 0.95f);
        private static readonly Color ButtonColor = new Color(0.20f, 0.33f, 0.50f, 1f);
        private static readonly Color ButtonColorServe = new Color(0.18f, 0.46f, 0.26f, 1f);
        private static readonly Color ButtonColorNeutral = new Color(0.28f, 0.30f, 0.36f, 1f);
        private static readonly Color ButtonColorDanger = new Color(0.46f, 0.21f, 0.21f, 1f);
        private static readonly Color TextColor = new Color(0.93f, 0.95f, 0.98f, 1f);
        private static readonly Color TextMuted = new Color(0.70f, 0.74f, 0.82f, 1f);
        private static readonly Color TextMoney = new Color(0.65f, 0.95f, 0.70f);
        private static readonly Color ProgressFillColor = new Color(0.36f, 0.78f, 0.48f, 1f);

        // Font sizes, tuned for Chinese glyphs which read smaller than Latin at the same size.
        private const float FontTitle = 30f;
        private const float FontBody = 24f;
        private const float FontSmall = 20f;
        private const float FontHuge = 40f;

        /// <summary>Font applied to every label. Resolved once by SetDefaultFont.</summary>
        private static TMP_FontAsset defaultFont;

        // ------------------------------------------------------------------ entry point

        /// <summary>
        /// Creates the Canvas and every panel. <paramref name="fontAsset"/> is applied to each label
        /// as it is created, because TMP resolves its font in Awake.
        /// </summary>
        public static GameObject BuildCanvas(TMP_FontAsset fontAsset)
        {
            SetDefaultFont(fontAsset);

            GameObject canvasGo = new GameObject("Canvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(CanvasWidth, CanvasHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<StandaloneInputModule>();
            }

            BuildTopBar(canvasGo.transform);
            BuildCookerShopPanel(canvasGo.transform);
            BuildOrderPanel(canvasGo.transform);
            BuildSelectionPanel(canvasGo.transform);
            BuildProgressPanel(canvasGo.transform);
            BuildNotificationPanel(canvasGo.transform);

            // Second pass: the font is written through the serialised field so it is guaranteed to
            // be the one saved with the scene. Doing this while each label is created is not enough,
            // because TextMeshPro may replace the reference as it initialises.
            ApplyFontToAllLabels(canvasGo.transform, fontAsset);

            return canvasGo;
        }

        // ------------------------------------------------------------------ panels

        private static void BuildTopBar(Transform canvas)
        {
            GameObject panelObject = Panel(canvas, "TopBar", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -96f), PanelColor);
            RectTransform bar = panelObject.GetComponent<RectTransform>();
            bar.offsetMin = new Vector2(24f, 0f);
            bar.offsetMax = new Vector2(-24f, 0f);

            // Two groups side by side: the left one holds live status, the right one holds the clock
            // and the kitchen stock. Separation is by layout, never by guessing widths.
            RectTransform left = HorizontalGroup(bar, "StatusGroup", 26f, TextAlignmentOptions.MidlineLeft);
            RectTransform right = HorizontalGroup(bar, "InfoGroup", 26f, TextAlignmentOptions.MidlineRight);

            LayoutElement leftElement = left.gameObject.AddComponent<LayoutElement>();
            leftElement.flexibleWidth = 0f;
            leftElement.minWidth = 640f;

            LayoutElement rightElement = right.gameObject.AddComponent<LayoutElement>();
            rightElement.flexibleWidth = 1f;

            Text(left, "MoneyText", "金钱 0", FontHuge, TextMoney, TextAlignmentOptions.MidlineLeft, 220f);
            Text(left, "ModeText", "普通", FontBody, TextColor, TextAlignmentOptions.MidlineLeft, 420f);

            Text(right, "InventoryText", "-", FontSmall, TextMuted, TextAlignmentOptions.MidlineRight, 900f);
            Text(right, "OrderCountText", "完成 0 / 失败 0", FontSmall, TextMuted, TextAlignmentOptions.MidlineRight, 300f);
            Text(right, "TimeText", "00:00", FontBody, TextColor, TextAlignmentOptions.MidlineRight, 140f);
        }

        private static void BuildCookerShopPanel(Transform canvas)
        {
            // Bottom left corner, inset from the edge.
            RectTransform panel = Panel(canvas, "CookerShopPanel", new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(24f, 340f), PanelColor).GetComponent<RectTransform>();
            panel.pivot = new Vector2(0f, 0f);
            panel.sizeDelta = new Vector2(620f, 600f);

            RectTransform content = VerticalGroup(panel, "ShopContent", 12f, TextAlignmentOptions.TopLeft, 24f);

            Text(content, "ShopTitleText", "厨具商店", FontTitle, TextColor, TextAlignmentOptions.MidlineLeft, 0f, 46f);

            for (int i = 0; i < 4; i++)
            {
                string name = $"CookerButton{i}";
                Button(content, name, $"{name}Label", "-", new Vector2(0f, 88f), ButtonColor, FontBody);
            }

            Text(content, "BuildHintText", "点击按钮购买厨具，再点击空地放置", FontSmall, TextMuted,
                TextAlignmentOptions.TopLeft, 0f, 68f, true);
        }

        private static void BuildOrderPanel(Transform canvas)
        {
            // Top right corner, below the top bar.
            RectTransform panel = Panel(canvas, "OrderPanel", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -120f), PanelColor).GetComponent<RectTransform>();
            panel.pivot = new Vector2(1f, 1f);
            panel.sizeDelta = new Vector2(680f, 430f);

            RectTransform content = VerticalGroup(panel, "OrderContent", 10f, TextAlignmentOptions.TopLeft, 22f);

            Text(content, "OrderTitleText", "订单", FontTitle, TextColor, TextAlignmentOptions.MidlineLeft, 0f, 44f);
            Text(content, "OrderRequirementText", "-", FontBody, TextColor, TextAlignmentOptions.TopLeft, 0f, 96f);
            Text(content, "OrderTimerText", "-", FontBody, new Color(1f, 0.85f, 0.5f), TextAlignmentOptions.MidlineLeft, 0f, 40f);
            Text(content, "OrderStateText", "-", FontBody, TextMuted, TextAlignmentOptions.MidlineLeft, 0f, 38f);

            Button(content, "SubmitOrderButton", "SubmitOrderButtonLabel", "上菜", new Vector2(0f, 76f), ButtonColorServe, FontTitle);
            Text(content, "OrderHintText", "上菜需要成品与干净盘子各一份，成功后两者都会被扣除", FontSmall, TextMuted,
                TextAlignmentOptions.TopLeft, 0f, 84f, true);
        }

        private static void BuildSelectionPanel(Transform canvas)
        {
            // Right side, under the order panel.
            RectTransform panel = Panel(canvas, "SelectionPanel", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -560f), PanelColor).GetComponent<RectTransform>();
            panel.pivot = new Vector2(1f, 1f);
            panel.sizeDelta = new Vector2(680f, 250f);

            RectTransform content = VerticalGroup(panel, "SelectionContent", 10f, TextAlignmentOptions.TopLeft, 22f);

            Text(content, "SelectionTitleText", "选中信息", FontBody, TextMuted, TextAlignmentOptions.MidlineLeft, 0f, 34f);

            RectTransform staff = Card(content, "StaffSection", 96f);
            Text(staff, "StaffNameText", "厨师", FontBody, TextColor, TextAlignmentOptions.MidlineLeft, 0f, 34f);
            Text(staff, "StaffPositionText", "-", FontSmall, TextMuted, TextAlignmentOptions.MidlineLeft, 0f, 28f);
            Text(staff, "StaffHintText", "-", FontSmall, TextMuted, TextAlignmentOptions.TopLeft, 0f, 30f);

            RectTransform cooker = Card(content, "CookerSection", 74f);
            Text(cooker, "CookerNameText", "厨具", FontBody, TextColor, TextAlignmentOptions.MidlineLeft, 0f, 32f);
            Text(cooker, "CookerStateText", "-", FontSmall, TextMuted, TextAlignmentOptions.MidlineLeft, 0f, 26f);
            Text(cooker, "CookerPositionText", "-", FontSmall, TextMuted, TextAlignmentOptions.MidlineLeft, 0f, 26f);
        }

        private static void BuildProgressPanel(Transform canvas)
        {
            // Full width strip along the bottom, above the notification line.
            RectTransform panel = Panel(canvas, "RecipeProgressPanel", new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 168f), PanelColor).GetComponent<RectTransform>();
            panel.pivot = new Vector2(0.5f, 0f);
            panel.offsetMin = new Vector2(24f, 0f);
            panel.offsetMax = new Vector2(-24f, 0f);
            panel.sizeDelta = new Vector2(-48f, 150f);

            RectTransform content = VerticalGroup(panel, "ProgressContent", 6f, TextAlignmentOptions.TopLeft, 22f);

            Text(content, "ProgressTitleText", "未选中厨具", FontBody, TextColor, TextAlignmentOptions.MidlineLeft, 0f, 34f);
            Text(content, "ProgressDetailText", "左键点击厨具查看加工进度", FontSmall, TextMuted, TextAlignmentOptions.MidlineLeft, 0f, 30f, true);
            ProgressBar(content, "ProgressBar", 22f, ProgressFillColor, "ProgressFill");
        }

        private static void BuildNotificationPanel(Transform canvas)
        {
            RectTransform panel = Panel(canvas, "NotificationPanel", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 92f), new Color(0.05f, 0.06f, 0.09f, 0.95f)).GetComponent<RectTransform>();
            panel.pivot = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(1000f, 68f);

            RectTransform content = HorizontalGroup(panel, "NotificationContent", 0f, TextAlignmentOptions.Center);
            Text(content, "NotificationText", "-", FontBody, TextColor, TextAlignmentOptions.Center, 940f, 56f);
            panel.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ primitives

        /// <summary>
        /// A panel anchored between two points, positioned and sized by the caller. Panels are laid
        /// out on the screen edges so the centre of the view stays clear for the kitchen.
        /// </summary>
        private static GameObject Panel(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = Vector2.zero;

            Image image = go.AddComponent<Image>();
            image.color = color;
            return go;
        }

        private static RectTransform HorizontalGroup(Transform parent, string name, float spacing, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            HorizontalLayoutGroup group = go.AddComponent<HorizontalLayoutGroup>();
            group.spacing = spacing;
            group.childAlignment = TextAnchor.MiddleLeft;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = true;
            group.padding = new RectOffset(0, 0, 0, 0);

            return rect;
        }

        private static RectTransform VerticalGroup(Transform parent, string name, float spacing, TextAlignmentOptions alignment, float padding)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);

            VerticalLayoutGroup group = go.AddComponent<VerticalLayoutGroup>();
            group.spacing = spacing;
            group.childAlignment = TextAnchor.UpperLeft;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            group.padding = new RectOffset(0, 0, 0, 0);

            return rect;
        }

        /// <summary>A bordered row used for grouped readouts inside a vertical group.</summary>
        private static RectTransform Card(Transform parent, string name, float height)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            Image image = go.AddComponent<Image>();
            image.color = CardColor;

            LayoutElement element = go.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;

            HorizontalLayoutGroup group = go.AddComponent<HorizontalLayoutGroup>();
            group.spacing = 10f;
            group.childAlignment = TextAnchor.MiddleLeft;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            group.padding = new RectOffset(12, 12, 6, 6);

            return rect;
        }

        private static TMP_Text Text(
            Transform parent,
            string name,
            string content,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment,
            float preferredWidth = 0f,
            float height = 0f,
            bool wrap = false)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            // The component must be added before its font can be set, and the font must be set
            // before the component's own Awake picks up a default that may not exist.
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            ApplyFont(text);
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.enableWordWrapping = wrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;

            LayoutElement element = go.AddComponent<LayoutElement>();
            if (wrap)
            {
                // A wrapping label takes whatever width its group gives it, so no width is demanded
                // and the text can never be wider than its rectangle.
                element.flexibleWidth = 1f;
                element.minWidth = 0f;
            }
            else if (preferredWidth > 0f)
            {
                element.minWidth = preferredWidth;
                element.preferredWidth = preferredWidth;
            }
            else
            {
                element.flexibleWidth = 1f;
            }

            if (height > 0f)
            {
                element.minHeight = height;
                element.preferredHeight = height;
            }

            return text;
        }

        private static Button Button(
            Transform parent,
            string objectName,
            string labelName,
            string label,
            Vector2 size,
            Color background,
            float fontSize)
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            Image image = go.AddComponent<Image>();
            image.color = background;

            LayoutElement element = go.AddComponent<LayoutElement>();
            element.minHeight = size.y;
            element.preferredHeight = size.y;
            element.flexibleWidth = 1f;

            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
            button.colors = colors;

            RectTransform labelRect = VerticalGroup(rect, $"{labelName}Holder", 0f, TextAlignmentOptions.Center, 6);
            Text(labelRect, labelName, label, fontSize, new Color(0.97f, 0.98f, 1f), TextAlignmentOptions.Center);
            return button;
        }

        private static Image ProgressBar(Transform parent, string name, float height, Color fillColor, string fillName)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            Image track = go.AddComponent<Image>();
            track.color = new Color(0.18f, 0.20f, 0.24f);
            track.raycastTarget = false;

            LayoutElement element = go.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;

            GameObject fillGo = new GameObject(fillName, typeof(RectTransform));
            RectTransform fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.SetParent(rect, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(3f, 3f);
            fillRect.offsetMax = new Vector2(-3f, -3f);

            Image fill = fillGo.AddComponent<Image>();
            fill.color = fillColor;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 0f;
            fill.raycastTarget = false;

            return fill;
        }

        // ------------------------------------------------------------------ font

        /// <summary>
        /// Assigns the resolved font, falling back to whichever font the component already found so
        /// the HUD never ends up with an unusable asset reference.
        /// </summary>
        private static void ApplyFont(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            if (defaultFont == null && text.font != null)
            {
                defaultFont = text.font;
                return;
            }

            if (defaultFont != null && text.font != defaultFont)
            {
                text.font = defaultFont;
            }
        }

        /// <summary>Restores the labels' font when a canvas is rebuilt or re-wired.</summary>
        private static void SetDefaultFont(TMP_FontAsset fontAsset)
        {
            if (fontAsset != null)
            {
                defaultFont = fontAsset;
            }
        }

        /// <summary>
        /// Writes the resolved font onto every label through the serialised <c>m_fontAsset</c> field,
        /// and gives each label the font's own shared material. This is the step that makes Chinese
        /// text actually render, because a label left on a Latin-only font silently drops the glyphs.
        /// </summary>
        private static void ApplyFontToAllLabels(Transform root, TMP_FontAsset font)
        {
            if (font == null)
            {
                Debug.LogError(
                    "[Kitchen] The HUD font is null, so the labels keep TextMeshPro's Latin-only default " +
                    "and Chinese text will render as blank boxes.\n" +
                    UnityEngine.StackTraceUtility.ExtractStackTrace());
                return;
            }

            TMP_Text[] labels = root.GetComponentsInChildren<TMP_Text>(true);
            int applied = 0;
            int failed = 0;

            for (int i = 0; i < labels.Length; i++)
            {
                TMP_Text label = labels[i];
                if (label == null)
                {
                    continue;
                }

                try
                {
                    SerializedObject so = new SerializedObject(label);
                    SerializedProperty property = so.FindProperty("m_fontAsset");
                    if (property != null)
                    {
                        property.objectReferenceValue = font;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }

                    // Assigning through the property setter as well covers the case where the
                    // serialised field name ever changes in a future TextMeshPro version.
                    if (label.font != font)
                    {
                        label.font = font;
                    }

                    // The material must come from the font, otherwise the label samples the wrong atlas.
                    if (font.material != null && label.fontSharedMaterial != font.material)
                    {
                        label.fontSharedMaterial = font.material;
                    }

                    label.ForceMeshUpdate();
                    EditorUtility.SetDirty(label);
                    applied++;
                }
                catch (Exception exception)
                {
                    failed++;
                    Debug.LogWarning($"[Kitchen] Could not set the font on '{label.name}': {exception.Message}");
                }
            }

            Debug.Log($"[Kitchen] Applied font '{font.name}' to {applied} HUD label(s), {failed} failure(s), " +
                $"material = {(font.material != null ? font.material.name : "none")}.");
        }

        // ------------------------------------------------------------------ binding

        /// <summary>Assigns every UIManager reference from objects created by <see cref="BuildCanvas"/>.</summary>
        public static void WireUI(UIManager ui, GameObject canvas, TMP_FontAsset fontAsset)
        {
            SetDefaultFont(fontAsset);

            if (ui == null || canvas == null)
            {
                return;
            }

            SerializedObject so = new SerializedObject(ui);

            Set(so, "moneyText", Find<TMP_Text>(canvas, "MoneyText"));
            Set(so, "modeText", Find<TMP_Text>(canvas, "ModeText"));
            Set(so, "timeText", Find<TMP_Text>(canvas, "TimeText"));
            Set(so, "orderCountText", Find<TMP_Text>(canvas, "OrderCountText"));
            Set(so, "inventoryText", Find<TMP_Text>(canvas, "InventoryText"));

            Set(so, "orderPanel", FindPanel(canvas, "OrderPanel"));
            Set(so, "orderTitleText", Find<TMP_Text>(canvas, "OrderTitleText"));
            Set(so, "orderRequirementText", Find<TMP_Text>(canvas, "OrderRequirementText"));
            Set(so, "orderTimerText", Find<TMP_Text>(canvas, "OrderTimerText"));
            Set(so, "orderStateText", Find<TMP_Text>(canvas, "OrderStateText"));
            Set(so, "submitOrderButton", Find<Button>(canvas, "SubmitOrderButton"));
            Set(so, "submitOrderButtonLabel", Find<TMP_Text>(canvas, "SubmitOrderButtonLabel"));

            Set(so, "cookerShopPanel", FindPanel(canvas, "CookerShopPanel"));
            Set(so, "buildHintText", Find<TMP_Text>(canvas, "BuildHintText"));

            List<Button> shopButtons = new List<Button>();
            List<TMP_Text> shopLabels = new List<TMP_Text>();
            for (int i = 0; i < 4; i++)
            {
                string buttonName = $"CookerButton{i}";
                Button button = Find<Button>(canvas, buttonName);
                if (button == null)
                {
                    continue;
                }

                shopButtons.Add(button);
                shopLabels.Add(Find<TMP_Text>(button.gameObject, $"{buttonName}Label"));
            }

            SetButtonArray(so, "cookerButtons", shopButtons);
            SetTextArray(so, "cookerButtonLabels", shopLabels);

            Set(so, "selectionPanel", FindPanel(canvas, "SelectionPanel"));
            Set(so, "staffSection", FindPanel(canvas, "StaffSection"));
            Set(so, "staffNameText", Find<TMP_Text>(canvas, "StaffNameText"));
            Set(so, "staffPositionText", Find<TMP_Text>(canvas, "StaffPositionText"));
            Set(so, "staffHintText", Find<TMP_Text>(canvas, "StaffHintText"));
            Set(so, "cookerSection", FindPanel(canvas, "CookerSection"));
            Set(so, "cookerNameText", Find<TMP_Text>(canvas, "CookerNameText"));
            Set(so, "cookerStateText", Find<TMP_Text>(canvas, "CookerStateText"));
            Set(so, "cookerPositionText", Find<TMP_Text>(canvas, "CookerPositionText"));
            Set(so, "cookerInputText", Find<TMP_Text>(canvas, "CookerInputText"));
            Set(so, "cookerOutputText", Find<TMP_Text>(canvas, "CookerOutputText"));
            Set(so, "moveCookerButton", Find<Button>(canvas, "MoveCookerButton"));
            Set(so, "collectButton", Find<Button>(canvas, "CollectButton"));
            Set(so, "sellCookerButton", Find<Button>(canvas, "SellCookerButton"));

            Set(so, "progressPanel", FindPanel(canvas, "RecipeProgressPanel"));
            Set(so, "progressTitleText", Find<TMP_Text>(canvas, "ProgressTitleText"));
            Set(so, "progressDetailText", Find<TMP_Text>(canvas, "ProgressDetailText"));
            Set(so, "progressFill", Find<Image>(canvas, "ProgressFill"));

            Set(so, "notificationPanel", FindPanel(canvas, "NotificationPanel"));
            Set(so, "notificationText", Find<TMP_Text>(canvas, "NotificationText"));

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ helper lookups

        private static T Find<T>(GameObject root, string name) where T : Component
        {
            Transform match = FindTransform(root.transform, name);
            return match != null ? match.GetComponent<T>() : null;
        }

        /// <summary>Finds a GameObject by name. Used for panel references rather than components.</summary>
        private static GameObject FindPanel(GameObject root, string name)
        {
            Transform match = FindTransform(root.transform, name);
            return match != null ? match.gameObject : null;
        }

        private static Transform FindTransform(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindTransform(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void Set(SerializedObject so, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning($"[KitchenUIBuilder] UIManager has no serialized field '{propertyName}'.");
                return;
            }

            property.objectReferenceValue = value;
        }

        private static void SetButtonArray(SerializedObject so, string propertyName, List<Button> buttons)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            property.arraySize = buttons.Count;
            for (int i = 0; i < buttons.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
            }
        }

        private static void SetTextArray(SerializedObject so, string propertyName, List<TMP_Text> texts)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            property.arraySize = texts.Count;
            for (int i = 0; i < texts.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = texts[i];
            }
        }
    }
}
