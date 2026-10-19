using System.Collections.Generic;
using System.IO;
using Kitchen.Data;
using Kitchen.View;
using UnityEditor;
using UnityEngine;

namespace Kitchen.EditorTools
{
    /// <summary>
    /// Authors the whitebox materials as real assets.
    ///
    /// This exists because a material created in memory with <see cref="HideFlags.DontSave"/> serialises
    /// as a null reference inside a saved prefab. Every instanced cooker then rendered with Unity's
    /// magenta missing-material shader. Materials are therefore written to
    /// <c>Assets/_Game/Art/Materials</c> and collected in one library asset under Resources, which the
    /// runtime loads and resolves by name.
    /// </summary>
    public static class KitchenMaterialGenerator
    {
        public const string MaterialFolder = "Assets/_Game/Art/Materials";
        public const string LibraryPath = "Assets/_Game/Resources/Kitchen/WhiteboxMaterials.asset";

        /// <summary>Creates the materials, the library, and returns it.</summary>
        public static WhiteboxMaterialLibrary Generate()
        {
            EnsureFolder(MaterialFolder);
            EnsureFolder(Path.GetDirectoryName(LibraryPath).Replace('\\', '/'));

            WhiteboxMaterialLibrary library = LoadOrCreateLibrary();
            library.Clear();

            // Every colour the demo uses is declared here, so the set of assets is deterministic and
            // a missing asset can never come from a colour somebody forgot to list.
            Add(library, Whitebox.FloorColor);
            Add(library, Whitebox.FloorEdgeColor);
            Add(library, Whitebox.MetalColor);
            Add(library, Whitebox.DarkMetalColor);
            Add(library, Whitebox.InactiveAccentColor);
            Add(library, Whitebox.StaffBodyColor);
            Add(library, Whitebox.StaffHeadColor);
            Add(library, Whitebox.DetailBoardColor);
            Add(library, Whitebox.DetailGlassColor);

            // Cooker body and accent colours come from the cooker definitions.
            string[] guids = AssetDatabase.FindAssets("t:CookerDefinition", new[] { "Assets/_Game/Data" });
            for (int i = 0; i < guids.Length; i++)
            {
                CookerDefinition definition = AssetDatabase.LoadAssetAtPath<CookerDefinition>(
                    AssetDatabase.GUIDToAssetPath(guids[i]));
                if (definition == null)
                {
                    continue;
                }

                Add(library, definition.BodyColor);
                Add(library, definition.AccentColor);
            }

            // Every item colour, so a item token also references a real asset.
            string[] itemGuids = AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/_Game/Data" });
            for (int i = 0; i < itemGuids.Length; i++)
            {
                ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(
                    AssetDatabase.GUIDToAssetPath(itemGuids[i]));
                if (item != null)
                {
                    Add(library, item.ViewColor);

                    // Dish tokens draw a second part in a derived topping colour, so that colour needs
                    // an asset as well or the topping serialises as a null material.
                    Add(library, Whitebox.ToppingColor(item));
                }
            }

            // Overlays: grid, highlights and the build ghost.
            Add(library, Whitebox.GridCellColor);
            Add(library, Whitebox.HighlightValidColor);
            Add(library, Whitebox.HighlightInvalidColor);
            Add(library, Whitebox.HighlightSelectColor);
            Add(library, Whitebox.StaffSelectedColor);

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            Debug.Log($"[KitchenMaterialGenerator] Authored {library.Materials.Count} material asset(s).");
            return library;
        }

        private static WhiteboxMaterialLibrary LoadOrCreateLibrary()
        {
            WhiteboxMaterialLibrary library = AssetDatabase.LoadAssetAtPath<WhiteboxMaterialLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<WhiteboxMaterialLibrary>();
                library.name = "WhiteboxMaterials";
                AssetDatabase.CreateAsset(library, LibraryPath);
                library = AssetDatabase.LoadAssetAtPath<WhiteboxMaterialLibrary>(LibraryPath);
            }

            return library;
        }

        /// <summary>
        /// Creates or reuses the asset for a colour and registers it in the library. The public
        /// Whitebox factories are used so the shader, properties and naming cannot drift from the
        /// runtime fallback.
        /// </summary>
        private static void Add(WhiteboxMaterialLibrary library, Color color)
        {
            AddMaterial(library, Whitebox.SolidMaterial(color, "Solid"), 0, color);
            AddMaterial(library, Whitebox.OverlayMaterial(color, "Overlay"), 1, color);
        }

        private static void AddMaterial(WhiteboxMaterialLibrary library, Material template, int prefix, Color color)
        {
            if (template == null)
            {
                return;
            }

            string assetName = Whitebox.MaterialName(prefix, color);
            string path = $"{MaterialFolder}/{assetName}.mat";

            Material stored = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (stored == null)
            {
                // Copied rather than reused: a material already referenced by a prefab must not be
                // overwritten, so a dedicated asset is created from the runtime template.
                stored = new Material(template)
                {
                    name = assetName
                };

                AssetDatabase.CreateAsset(stored, path);
                stored = AssetDatabase.LoadAssetAtPath<Material>(path);
            }

            if (stored != null)
            {
                library.Add(stored);
            }
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
