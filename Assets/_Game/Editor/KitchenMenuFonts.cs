using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Kitchen.EditorTools
{
    public static class KitchenMenuFonts
    {
        public static TMP_FontAsset Title => Ensure("LilitaOne", "Assets/ThirdParty/GoogleFonts/LilitaOne/LilitaOne-Regular.ttf", false);
        public static TMP_FontAsset Chinese => Ensure("MenuChinese", "Assets/ThirdParty/GoogleFonts/NotoSansSC/NotoSansSC-Bold.otf", true);

        private static TMP_FontAsset Ensure(string name, string sourcePath, bool chinese)
        {
            string path = "Assets/_Game/Art/Fonts/" + name + " SDF.asset";
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            Font source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (source == null) throw new InvalidOperationException("Missing menu font: " + sourcePath);
            string characters = "";
            for (int code = 32; code < 127; code++) characters += (char)code;
            if (chinese) characters += "小小厨房开饭啦。教程经典模式设置返回全局音量厨房界面缩放弹性动效已启用关闭自动保存仅调整游戏内界面不影响主菜单从第一份沙拉开始每日营业与挑战得分进度第关可直接学习站位生产慢慢来也会很好吃今天要好厨师长工！，：·%—";
            else characters += "’";
            if (chinese)
            {
                characters += KitchenMenuBuilder.TutorialIntro + KitchenMenuBuilder.ClassicIntro.Replace("\n", "") + "开始教程开始营业退出游戏";
                foreach (string step in KitchenMenuBuilder.TutorialSteps) characters += step;
                foreach (string step in KitchenMenuBuilder.ClassicSteps) characters += step;
            }
            if (existing != null && existing.HasCharacters(characters, out uint[] missingCharacters)) return existing;
            TMP_FontAsset font = existing != null ? existing : TMP_FontAsset.CreateFontAsset(source, chinese ? 64 : 100, 9,
                GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            font.name = name + " SDF";
            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            if (!font.TryAddCharacters(characters, out string missing))
                throw new InvalidOperationException("Menu font is missing characters: " + missing);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            if (existing == null)
            {
                AssetDatabase.CreateAsset(font, path);
                foreach (Texture2D atlas in font.atlasTextures)
                {
                    atlas.name = name + " Atlas";
                    AssetDatabase.AddObjectToAsset(atlas, font);
                }
                font.material.name = name + " Material";
                AssetDatabase.AddObjectToAsset(font.material, font);
            }
            foreach (Texture2D atlas in font.atlasTextures) EditorUtility.SetDirty(atlas);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            return font;
        }
    }
}
