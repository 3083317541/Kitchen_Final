using Kitchen.Data;
using Kitchen.UI;
using Kitchen.View;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kitchen.EditorTools
{
    /// <summary>
    /// Authors the start screen: one Canvas with a title, a progress line, a start button and a quit
    /// button, plus the <see cref="MainMenuController"/> that drives them.
    ///
    /// The scene is generated rather than hand made for the same reason as the kitchen: the demo has
    /// no art pipeline, so every screen is reproducible from code and stays consistent with the HUD.
    /// </summary>
    public static class KitchenMenuBuilder
    {
        /// <summary>Name of the generated start scene.</summary>
        public const string SceneName = "MainMenu";

        private static readonly Color BackgroundColor = new Color(0.05f, 0.06f, 0.09f, 1f);
        private static readonly Color CardColor = new Color(0.10f, 0.12f, 0.16f, 0.98f);
        private static readonly Color TitleColor = new Color(0.97f, 0.98f, 1f);
        private static readonly Color BodyColor = new Color(0.72f, 0.76f, 0.84f);
        private static readonly Color StartButtonColor = new Color(0.18f, 0.46f, 0.26f, 1f);
        private static readonly Color QuitButtonColor = new Color(0.28f, 0.30f, 0.36f, 1f);

        /// <summary>Builds the menu objects and returns the controller.</summary>
        public static MainMenuController Build(TMP_FontAsset fontAsset)
        {
            GameObject root = new GameObject("MainMenu");
            MainMenuController controller = root.AddComponent<MainMenuController>();

            BuildCamera(root.transform);
            GameObject canvas = BuildCanvas(root.transform, fontAsset);
            BuildContent(canvas.transform, controller, fontAsset);

            return controller;
        }

        private static void BuildCamera(Transform parent)
        {
            GameObject cameraGo = new GameObject("Main Camera");
            cameraGo.transform.SetParent(parent, false);
            cameraGo.transform.position = new Vector3(0f, 11f, -9f);
            cameraGo.transform.rotation = Quaternion.Euler(28f, 0f, 0f);

            Camera camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
            camera.fieldOfView = 45f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 200f;

            // The menu is canvas only, so there is nothing for a light to illuminate.
            cameraGo.tag = "MainCamera";
        }

        private static GameObject BuildCanvas(Transform parent, TMP_FontAsset fontAsset)
        {
            GameObject canvasGo = new GameObject("MenuCanvas");
            canvasGo.transform.SetParent(parent, false);

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.transform.SetParent(parent, false);
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<StandaloneInputModule>();
            }

            return canvasGo;
        }

        private static void BuildContent(Transform canvas, MainMenuController controller, TMP_FontAsset fontAsset)
        {
            // Full screen backdrop that also blocks any stray raycast.
            GameObject backdrop = CreateRect(canvas, "Backdrop");
            RectTransform backdropRect = backdrop.GetComponent<RectTransform>();
            Stretch(backdropRect);
            Image backImage = backdrop.AddComponent<Image>();
            backImage.color = BackgroundColor;

            RectTransform card = CreateRect(canvas, "MenuCard").GetComponent<RectTransform>();
            card.anchorMin = new Vector2(0.5f, 0.5f);
            card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(900f, 620f);
            Image cardImage = card.gameObject.AddComponent<Image>();
            cardImage.color = CardColor;

            RectTransform column = Column(card, "MenuColumn", 26f, 40f);

            TMP_Text title = Label(column, "TitleText", "厨房 Demo", 78f, fontAsset, TitleColor, 90f);
            Label(column, "SubtitleText", "俯视角厨房自动化 Demo", 30f, fontAsset, BodyColor, 46f);
            TMP_Text progress = Label(column, "ProgressText", "点击开始进入第一关", 26f, fontAsset, BodyColor, 40f);
            Label(column, "HintText", "左键选人 · WASD 移动 · 1-4 购买厨具 · Esc 取消", 24f, fontAsset, BodyColor, 36f);

            Button startButton = MakeButton(column, "StartButton", "StartButtonLabel", "开始游戏",
                StartButtonColor, fontAsset, 96f);
            Button quitButton = MakeButton(column, "QuitButton", "QuitButtonLabel", "退出游戏",
                QuitButtonColor, fontAsset, 76f);

            // Wire through the serialised fields: assigning to public properties would not be saved with
            // the scene, and the controller reads these fields at runtime.
            SerializedObject serialized = new SerializedObject(controller);
            SetObject(serialized, "startButton", startButton);
            SetObject(serialized, "quitButton", quitButton);
            SetObject(serialized, "progressText", progress);
            SetObject(serialized, "titleText", title);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(controller);
        }

        // ------------------------------------------------------------------ primitives

        private static GameObject CreateRect(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.GetComponent<RectTransform>().SetParent(parent, false);
            return go;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static RectTransform Column(Transform parent, string name, float spacing, float padding)
        {
            RectTransform rect = CreateRect(parent, name).GetComponent<RectTransform>();
            Stretch(rect);

            VerticalLayoutGroup group = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = spacing;
            group.childAlignment = TextAnchor.MiddleCenter;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            group.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);

            return rect;
        }

        private static TMP_Text Label(
            Transform parent,
            string name,
            string content,
            float fontSize,
            TMP_FontAsset fontAsset,
            Color color,
            float height)
        {
            GameObject go = CreateRect(parent, name);
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();

            if (fontAsset != null)
            {
                text.font = fontAsset;
            }

            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;

            LayoutElement element = go.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleWidth = 1f;

            return text;
        }

        private static Button MakeButton(
            Transform parent,
            string objectName,
            string labelName,
            string label,
            Color background,
            TMP_FontAsset fontAsset,
            float height)
        {
            GameObject go = CreateRect(parent, objectName);
            Image image = go.AddComponent<Image>();
            image.color = background;

            LayoutElement element = go.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleWidth = 1f;

            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.84f, 0.84f, 0.84f, 1f);
            button.colors = colors;

            Label(go.transform, labelName, label, 40f, fontAsset, TitleColor, height - 8f);
            return button;
        }

        private static void SetObject(SerializedObject serialized, string field, UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(field);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }
    }
}
