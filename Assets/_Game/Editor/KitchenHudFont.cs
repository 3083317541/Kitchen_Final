using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Kitchen.EditorTools
{
    /// <summary>
    /// Repairs HUD text that renders as empty boxes.
    ///
    /// The kitchen scene referenced a font asset by guid that exists on nobody else's machine: it was
    /// generated locally from a system font and that asset is excluded from version control, so the
    /// committed scene pointed at a font that is not in the repository. TextMeshPro then fell back to
    /// Liberation Sans, which has no Chinese glyphs, and every Chinese label drew as a box.
    ///
    /// The fix builds one CJK font asset from the free Noto Sans SC that ships with the project, keeps it
    /// in version control with its .meta so the guid is the same on every machine, and repoints the scenes
    /// at it. A dynamic atlas is used so glyphs are rasterised on demand and no character can be missing.
    ///
    /// This deliberately touches only font assets and font references: it never rebuilds the scenes,
    /// prefabs or art.
    /// </summary>
    public static class KitchenHudFont
    {
        /// <summary>Font asset every HUD label should use. Committed, so its guid is stable.</summary>
        public const string FontAssetPath = "Assets/_Game/Art/Fonts/HudCJK SDF.asset";

        /// <summary>Free, redistributable CJK source font that ships with the project (SIL OFL).</summary>
        public const string SourceFontPath = "Assets/ThirdParty/GoogleFonts/NotoSansSC/NotoSansSC-Bold.otf";

        /// <summary>Scenes whose font references are repaired.</summary>
        private static readonly string[] ScenePaths =
        {
            "Assets/_Game/Scenes/Kitchen.unity",
            "Assets/_Game/Scenes/MainMenu.unity",
            "Assets/_Game/Scenes/ArtShowcase.unity",
        };

        /// <summary>Characters that must render, taken from the Chinese the HUD actually shows.</summary>
        private const string SampleCharacters = "厨房订单金钱时间模式上菜材料不足完成失败剩余奖励普通左侧商店购买厨具放置取消选中信息厨师格子激活加工进度沙拉干净盘子脏碗切菜台煎锅组装洗员工名当班就绪新订单超时已拆除移动不能该已经个";

        [MenuItem("Kitchen/Art/Rebuild HUD Font (CJK Only)", false, 300)]
        public static void RepairAll()
        {
            if (!BuildFont())
            {
                Debug.LogError("[KitchenHudFont] 字体资产创建失败，场景未改动。");
                return;
            }

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            string guid = AssetDatabase.AssetPathToGUID(FontAssetPath);

            if (font == null || string.IsNullOrEmpty(guid))
            {
                Debug.LogError("[KitchenHudFont] 字体资产创建后仍无法加载，场景未改动。");
                return;
            }

            Debug.Log($"[KitchenHudFont] 字体资产就绪：{FontAssetPath} guid={guid}");

            RepointScenes(guid);
            ReportDeadFontReferences();
        }

        /// <summary>Creates or refreshes the CJK font asset. Returns false when it could not be built.</summary>
        public static bool BuildFont()
        {
            Font source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (source == null)
            {
                Debug.LogError($"[KitchenHudFont] 找不到源字体：{SourceFontPath}");
                return false;
            }

            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null && existing.IsUsable())
            {
                // Already built and usable; only make sure the glyph machinery works.
                return VerifyGlyphs(existing);
            }

            TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(
                source,
                90,
                9,
                GlyphRenderMode.SDFAA,
                2048,
                2048,
                AtlasPopulationMode.Dynamic,
                true);

            if (font == null)
            {
                Debug.LogError("[KitchenHudFont] TextMeshPro 无法从源字体创建字体资产。");
                return false;
            }

            EnsureFolder(Path.GetDirectoryName(FontAssetPath).Replace('\\', '/'));

            if (existing != null)
            {
                AssetDatabase.DeleteAsset(FontAssetPath);
            }

            font.name = "HudCJK SDF";
            AssetDatabase.CreateAsset(font, FontAssetPath);

            // The atlas texture and material are sub assets; without this they are lost on reimport.
            if (font.atlasTexture != null)
            {
                font.atlasTexture.name = "HudCJK Atlas";
                AssetDatabase.AddObjectToAsset(font.atlasTexture, font);
            }

            if (font.material != null)
            {
                font.material.name = "HudCJK Material";
                AssetDatabase.AddObjectToAsset(font.material, font);
            }

            // A second atlas page, so a long session with many new characters cannot run out of room.
            if (font.atlasTextures != null && font.atlasTextures.Length > 1 && font.atlasTextures[1] != null)
            {
                font.atlasTextures[1].name = "HudCJK Atlas 2";
                AssetDatabase.AddObjectToAsset(font.atlasTextures[1], font);
            }

            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(FontAssetPath, ImportAssetOptions.ForceSynchronousImport);

            TMP_FontAsset saved = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (saved == null)
            {
                Debug.LogError("[KitchenHudFont] 字体资产保存后无法重新加载。");
                return false;
            }

            return VerifyGlyphs(saved);
        }

        /// <summary>Rasterises the sample Chinese to prove the asset can actually draw it.</summary>
        private static bool VerifyGlyphs(TMP_FontAsset font)
        {
            if (font == null)
            {
                return false;
            }

            if (font.atlasPopulationMode == AtlasPopulationMode.Dynamic)
            {
                bool added = font.TryAddCharacters(SampleCharacters, out string missing);
                if (!added || !string.IsNullOrEmpty(missing))
                {
                    Debug.LogError(
                        $"[KitchenHudFont] 动态字体仍缺少字形：'{missing}'。" +
                        "请检查源字体是否包含这些汉字。");
                    return false;
                }

                EditorUtility.SetDirty(font);
                AssetDatabase.SaveAssets();
                return true;
            }

            // A static asset can only draw the glyphs it already contains.
            int missingCount = 0;
            for (int i = 0; i < SampleCharacters.Length; i++)
            {
                if (!font.HasCharacter(SampleCharacters[i]))
                {
                    missingCount++;
                }
            }

            if (missingCount > 0)
            {
                Debug.LogError(
                    $"[KitchenHudFont] 字体 '{font.name}' 是静态图集，缺少 {missingCount} 个字形，" +
                    "会把汉字画成方框。需要改用动态图集。");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Points every label in the scenes at the repaired font.
        ///
        /// Scenes are edited as text on purpose: re-authoring them would overwrite the art and layout work
        /// that has been done by hand since they were generated.
        /// </summary>
        private static void RepointScenes(string guid)
        {
            int totalChanged = 0;

            for (int i = 0; i < ScenePaths.Length; i++)
            {
                string path = ScenePaths[i];
                if (!File.Exists(path))
                {
                    continue;
                }

                string text = File.ReadAllText(path);

                // Any font reference is replaced, not just the dead one, so the whole HUD shares one
                // font that is guaranteed to be present in the repository.
                int replaced = 0;
                string updated = Regex.Replace(
                    text,
                    @"m_fontAsset: \{fileID: (-?\d+), guid: ([0-9a-f]{32}), type: 2\}",
                    match =>
                    {
                        if (match.Groups[2].Value == guid)
                        {
                            return match.Value;
                        }

                        replaced++;
                        return $"m_fontAsset: {{fileID: {match.Groups[1].Value}, guid: {guid}, type: 2}}";
                    });

                if (replaced == 0)
                {
                    continue;
                }

                File.WriteAllText(path, updated);
                totalChanged += replaced;
                Debug.Log($"[KitchenHudFont] {path}: 重新指向 {replaced} 处字体引用。");
            }

            if (totalChanged > 0)
            {
                AssetDatabase.Refresh();
            }

            Debug.Log($"[KitchenHudFont] 共修正 {totalChanged} 处字体引用。");
        }

        /// <summary>
        /// Lists font references in the scenes that do not resolve to an asset. This is the diagnostic that
        /// would have caught the original bug.
        /// </summary>
        [MenuItem("Kitchen/Art/Report Dead Font References", false, 301)]
        public static void ReportDeadFontReferences()
        {
            List<string> dead = new List<string>();

            for (int i = 0; i < ScenePaths.Length; i++)
            {
                string path = ScenePaths[i];
                if (!File.Exists(path))
                {
                    continue;
                }

                string text = File.ReadAllText(path);
                MatchCollection matches = Regex.Matches(
                    text, @"m_fontAsset: \{fileID: -?\d+, guid: ([0-9a-f]{32}), type: 2\}");

                for (int m = 0; m < matches.Count; m++)
                {
                    string guid = matches[m].Groups[1].Value;
                    if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)))
                    {
                        string entry = $"{Path.GetFileName(path)} -> {guid}";
                        if (!dead.Contains(entry))
                        {
                            dead.Add(entry);
                        }
                    }
                }
            }

            if (dead.Count == 0)
            {
                Debug.Log("[KitchenHudFont] 场景里的字体引用全部能解析到资产。");
                return;
            }

            Debug.LogError("[KitchenHudFont] 以下字体引用找不到资产，文字会显示为方框：\n" + string.Join("\n", dead.ToArray()));
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
