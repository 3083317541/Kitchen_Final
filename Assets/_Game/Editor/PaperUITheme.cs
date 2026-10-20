using System.IO;
using Kitchen.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Kitchen.EditorTools
{
    public static class PaperUITheme
    {
        public static readonly Color Ink = new Color32(27, 27, 27, 255);
        public static readonly Color Muted = new Color32(96, 96, 91, 255);
        public static readonly Color Yellow = new Color32(246, 207, 124, 255);
        public static readonly Color TitleInk = new Color32(116, 89, 31, 255);
        private const string SpritePath = "Assets/_Game/Art/UI/PaperRounded.png";
        private static Sprite rounded;

        public static Sprite Rounded
        {
            get
            {
                if (rounded != null) return rounded;
                rounded = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
                if (rounded != null) return rounded;
                Directory.CreateDirectory(Path.GetDirectoryName(SpritePath));
                const int size = 128;
                const float radius = 32f;
                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int row = 0; row < size; row++)
                {
                    for (int column = 0; column < size; column++)
                    {
                        float horizontal = Mathf.Max(Mathf.Abs(column + 0.5f - size / 2f) - (size / 2f - radius), 0f);
                        float vertical = Mathf.Max(Mathf.Abs(row + 0.5f - size / 2f) - (size / 2f - radius), 0f);
                        float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(horizontal * horizontal + vertical * vertical) + 0.5f);
                        texture.SetPixel(column, row, new Color(1f, 1f, 1f, alpha));
                    }
                }
                texture.Apply();
                File.WriteAllBytes(SpritePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(SpritePath);
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteBorder = new Vector4(32, 32, 32, 32);
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
                rounded = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
                return rounded;
            }
        }

        public static Image Surface(GameObject target, Color color)
        {
            Image image = target.GetComponent<Image>();
            if (image == null) image = target.AddComponent<Image>();
            image.sprite = Rounded;
            image.type = Image.Type.Sliced;
            image.color = color;
            return image;
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 size, Vector2 position)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        public static TMP_Text Label(Transform parent, string name, string content, TMP_FontAsset font,
            float size, Vector2 bounds, Vector2 position)
        {
            RectTransform rect = Rect(parent, name, bounds, position);
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = content;
            label.fontSize = size;
            label.color = Ink;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.enableAutoSizing = true;
            label.fontSizeMin = Mathf.Min(18f, size);
            label.fontSizeMax = size;
            label.raycastTarget = false;
            return label;
        }

        public static void Title(Transform parent, string caption, TMP_FontAsset font, float width = 210f)
        {
            RectTransform tab = Rect(parent, "TitleTab", new Vector2(width, 58f), new Vector2(-8f, 25f));
            Surface(tab.gameObject, Yellow);
            TMP_Text label = Label(tab, "TitleLabel", caption, font, 29f, new Vector2(width - 32f, 48f), new Vector2(16f, -5f));
            label.color = TitleInk;
            label.fontStyle = FontStyles.Bold;
        }

        public static void StyleButton(Button button)
        {
            RectTransform root = button.GetComponent<RectTransform>();
            Image hit = button.GetComponent<Image>();
            hit.color = Color.clear;
            RectTransform visual = Rect(root, "Visual", Vector2.zero, Vector2.zero);
            visual.anchorMin = Vector2.zero;
            visual.anchorMax = Vector2.one;
            visual.offsetMin = Vector2.zero;
            visual.offsetMax = Vector2.zero;
            visual.pivot = new Vector2(0.5f, 0.5f);
            Image tray = Surface(visual.gameObject, Yellow);
            tray.raycastTarget = false;
            RectTransform face = Rect(visual, "Face", Vector2.zero, Vector2.zero);
            face.anchorMin = Vector2.zero;
            face.anchorMax = Vector2.one;
            face.offsetMin = new Vector2(5f, 5f);
            face.offsetMax = new Vector2(-5f, -5f);
            Surface(face.gameObject, Color.white).raycastTarget = false;
            for (int index = root.childCount - 1; index >= 0; index--)
            {
                Transform child = root.GetChild(index);
                if (child != visual) child.SetParent(visual, false);
            }
            foreach (TMP_Text label in visual.GetComponentsInChildren<TMP_Text>(true))
            {
                label.color = Ink;
                label.enableAutoSizing = true;
                label.fontSizeMin = 18f;
                label.fontSizeMax = label.fontSize;
            }
            button.targetGraphic = tray;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.96f, 0.83f);
            colors.pressedColor = new Color(0.93f, 0.86f, 0.68f);
            colors.disabledColor = new Color(0.77f, 0.77f, 0.77f, 0.6f);
            button.colors = colors;
            button.gameObject.AddComponent<PaperButtonMotion>().Configure(visual);
        }

        public static void StyleSolidButton(Button button, Color color)
        {
            StyleButton(button);
            PaperButtonStyle.Apply(button, color);
        }

        public static void AddPaperclip(Transform panel, TMP_FontAsset font)
        {
            RectTransform hit = Rect(panel, "BreadPaperclip", new Vector2(82f, 132f), new Vector2(-112f, 65f));
            hit.anchorMin = hit.anchorMax = new Vector2(1f, 1f);
            Image hitImage = hit.gameObject.AddComponent<Image>();
            hitImage.color = Color.clear;
            RectTransform visual = Rect(hit, "Visual", new Vector2(82f, 132f), new Vector2(41f, -56f));
            visual.pivot = new Vector2(0.5f, 0.57576f);
            Image body = visual.gameObject.AddComponent<Image>();
            body.sprite = ImportPaperclip("BreadPaperclip");
            body.raycastTarget = false;
            RectTransform eyes = Rect(visual, "Eyes", new Vector2(41f, 15.4f), new Vector2(41f, -35.1f));
            eyes.pivot = new Vector2(0.5f, 0.5f);
            Image eyeImage = eyes.gameObject.AddComponent<Image>();
            eyeImage.sprite = ImportPaperclip("BreadPaperclipEyes");
            eyeImage.raycastTarget = false;
            RectTransform bubble = Rect(panel, "PaperclipBubble", new Vector2(330f, 54f), new Vector2(-346f, -12f));
            Surface(bubble.gameObject, Yellow).raycastTarget = false;
            TMP_Text speech = Label(bubble, "Speech", "开工啦，厨师长！", font, 22f, new Vector2(302f, 46f), new Vector2(14f, -4f));
            hit.gameObject.AddComponent<PaperclipReaction>().Configure(visual, eyes, speech);
            bubble.gameObject.SetActive(false);
        }

        private static Sprite ImportPaperclip(string name)
        {
            string path = "Assets/_Game/Art/UI/" + name + ".png";
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new FileNotFoundException("Paperclip PNG is missing.", path);
            if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled ||
                importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
