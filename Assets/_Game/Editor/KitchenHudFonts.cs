using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Kitchen.EditorTools
{
    public static class KitchenHudFonts
    {
        public const string SourcePath = "Assets/ThirdParty/GoogleFonts/ZCOOLKuaiLe/ZCOOLKuaiLe-Regular.ttf";
        private const string FallbackSource = "Assets/ThirdParty/GoogleFonts/NotoSansSC/NotoSansSC-Bold.otf";

        public static TMP_FontAsset Chinese
        {
            get
            {
                string characters = Characters();
                TMP_FontAsset fallback = Ensure("KitchenHudBoldFallback", FallbackSource, characters);
                TMP_FontAsset font = Ensure("KitchenHudCartoon", SourcePath, characters);
                font.material.SetFloat(ShaderUtilities.ID_FaceDilate, 0.12f);
                EditorUtility.SetDirty(font.material);
                if (font.fallbackFontAssetTable == null) font.fallbackFontAssetTable = new List<TMP_FontAsset>();
                if (!font.fallbackFontAssetTable.Contains(fallback))
                {
                    font.fallbackFontAssetTable.Add(fallback);
                    EditorUtility.SetDirty(font);
                }
                string unsupported = new string(characters.Where(character => !font.HasCharacter(character, true)).ToArray());
                if (unsupported.Length > 0)
                    throw new InvalidOperationException("HUD fonts cannot render: " + unsupported);
                AssetDatabase.SaveAssets();
                return font;
            }
        }

        private static string Characters()
        {
            var characters = new SortedSet<char>();
            for (int code = 32; code < 127; code++) characters.Add((char)code);
            foreach (char character in "→×−…：，。！（）·％、；？【】《》“”‘’—") characters.Add(character);
            foreach (string path in Directory.GetFiles("Assets/_Game", "*", SearchOption.AllDirectories))
            {
                if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) &&
                    !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) continue;
                if (path.Replace('\\', '/').Contains("/Art/")) continue;
                foreach (char character in File.ReadAllText(path))
                    if (character >= '\u4e00' && character <= '\u9fff') characters.Add(character);
            }
            return new string(characters.ToArray());
        }

        private static TMP_FontAsset Ensure(string name, string sourcePath, string characters)
        {
            string path = "Assets/_Game/Art/Fonts/" + name + " SDF.asset";
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font != null && font.HasCharacters(characters, out uint[] missing, true)) return font;
            Font source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (source == null) throw new InvalidOperationException("Missing bundled HUD font: " + sourcePath);
            bool created = font == null;
            if (created)
                font = TMP_FontAsset.CreateFontAsset(source, 64, 9, GlyphRenderMode.SDFAA, 2048, 2048,
                    AtlasPopulationMode.Dynamic, true);
            font.name = name + " SDF";
            font.TryAddCharacters(characters, out string unsupported);
            if (created) AssetDatabase.CreateAsset(font, path);
            foreach (Texture2D atlas in font.atlasTextures)
            {
                if (created || !AssetDatabase.Contains(atlas))
                {
                    atlas.name = name + " Atlas";
                    AssetDatabase.AddObjectToAsset(atlas, font);
                }
                EditorUtility.SetDirty(atlas);
            }
            if (created || !AssetDatabase.Contains(font.material))
            {
                font.material.name = name + " Material";
                AssetDatabase.AddObjectToAsset(font.material, font);
            }
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            return font;
        }

        public static void VerifyLabels(Transform root, Action<string, bool> check)
        {
            foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
            {
                bool bundled = label.font != null && label.font.name == "KitchenHudCartoon SDF" &&
                    label.fontSharedMaterial == label.font.material;
                check("cartoon_font_" + label.name, bundled);
                if (!bundled || !label.gameObject.activeInHierarchy) continue;
                label.ForceMeshUpdate();
                string characters = new string(label.GetParsedText().Where(character => !char.IsControl(character)).ToArray());
                bool complete = label.font.HasCharacters(characters, out uint[] missing, true);
                check("glyphs_" + label.name, complete);
                if (!complete) Debug.LogError("Missing HUD glyphs: " + string.Concat(missing.Select(code => char.ConvertFromUtf32((int)code))));
                if (label.GetComponentInParent<UnityEngine.UI.Button>() != null || label.name == "TitleLabel")
                    check("text_fits_" + label.transform.parent.name + "_" + label.name, !label.isTextOverflowing);
            }
        }
    }
}
