using System.Collections.Generic;
using System.IO;
using Kitchen.Cookers;
using Kitchen.Data;
using Kitchen.View;
using UnityEditor;
using UnityEngine;

namespace Kitchen.EditorTools
{
    /// <summary>
    /// Generates real prefab assets for every visual object and assigns them to the definitions.
    ///
    /// The demo does not need prefabs to run: <see cref="ViewFactory"/> builds the same models from
    /// primitives when a prefab field is empty. Prefabs exist so the art can be edited as assets, seen
    /// in the Scene view, and replaced without touching code.
    ///
    /// Each prefab is produced by the runtime view code itself, which is what guarantees the prefab and
    /// the fallback path stay identical.
    /// </summary>
    public static class KitchenPrefabGenerator
    {
        public const string CookerFolder = "Assets/_Game/Prefabs/Cookers";
        public const string StaffFolder = "Assets/_Game/Prefabs/Staff";
        public const string GridFolder = "Assets/_Game/Prefabs/Grid";
        public const string IngredientFolder = "Assets/_Game/Prefabs/Ingredients";

        /// <summary>Folder holding the saved definition assets the prefabs are assigned to.</summary>
        public const string DataFolder = "Assets/_Game/Data";

        /// <summary>Asset path of the generated floor prefab.</summary>
        public const string FloorPrefabPath = GridFolder + "/Grid_Floor.prefab";

        /// <summary>Asset path of the generated grid cell prefab.</summary>
        public const string CellPrefabPath = GridFolder + "/Grid_Cell.prefab";

        /// <summary>Prefabs produced by a generation pass.</summary>
        public class Result
        {
            public readonly List<string> CreatedPaths = new List<string>();
            public GameObject StaffPrefab;
            public GameObject FloorPrefab;
            public GameObject CellPrefab;
        }

        /// <summary>
        /// Builds every prefab asset and assigns it to the matching saved definition asset.
        ///
        /// The definitions are loaded from disk rather than taken from the in-memory content set,
        /// because those authoring objects are destroyed after generation: a reference written to one
        /// of them would never reach the asset the runtime loads.
        /// </summary>
        public static Result Generate(LevelConfig level)
        {
            Result result = new Result();

            EnsureFolder(CookerFolder);
            EnsureFolder(StaffFolder);
            EnsureFolder(GridFolder);
            EnsureFolder(IngredientFolder);

            GenerateCookers(result);
            GenerateStaff(result);
            GenerateGrid(result, level);
            GenerateItems(result);

            AssetDatabase.SaveAssets();
            return result;
        }

        /// <summary>Loads and returns every saved cooker definition.</summary>
        private static List<CookerDefinition> LoadSavedCookers()
        {
            List<CookerDefinition> definitions = new List<CookerDefinition>();
            string[] guids = AssetDatabase.FindAssets("t:CookerDefinition", new[] { DataFolder });

            for (int i = 0; i < guids.Length; i++)
            {
                CookerDefinition definition = AssetDatabase.LoadAssetAtPath<CookerDefinition>(
                    AssetDatabase.GUIDToAssetPath(guids[i]));
                if (definition != null)
                {
                    definitions.Add(definition);
                }
            }

            return definitions;
        }

        /// <summary>Loads and returns every saved item definition.</summary>
        private static List<ItemDefinition> LoadSavedItems()
        {
            List<ItemDefinition> definitions = new List<ItemDefinition>();
            string[] guids = AssetDatabase.FindAssets("t:ItemDefinition", new[] { DataFolder });

            for (int i = 0; i < guids.Length; i++)
            {
                ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(
                    AssetDatabase.GUIDToAssetPath(guids[i]));
                if (definition != null)
                {
                    definitions.Add(definition);
                }
            }

            return definitions;
        }

        // ------------------------------------------------------------------ cookers

        private static void GenerateCookers(Result result)
        {
            List<CookerDefinition> definitions = LoadSavedCookers();

            for (int i = 0; i < definitions.Count; i++)
            {
                CookerDefinition definition = definitions[i];
                if (definition == null)
                {
                    continue;
                }

                string path = $"{CookerFolder}/Cooker_{definition.Id}.prefab";

                // Cleared first so the definition refers to the prefab being written rather than an
                // older one, and so a corrupted prefab can never be resaved as the new source.
                AssignCookerViewPrefab(definition, null);

                // Built with the runtime view code, so the prefab is exactly what the fallback makes.
                GameObject source = ViewFactory.BuildWhiteboxCookerView(definition);
                CookerView view = source.GetComponent<CookerView>();
                if (view != null)
                {
                    view.EnsureBuilt();
                }

                GameObject saved = SavePrefab(source, path);
                if (saved != null)
                {
                    AssignCookerViewPrefab(definition, saved);
                    result.CreatedPaths.Add(path);
                }
            }
        }

        /// <summary>Writes the prefab reference onto the cooker definition so the runtime uses it.</summary>
        private static void AssignCookerViewPrefab(CookerDefinition definition, GameObject prefab)
        {
            SerializedObject so = new SerializedObject(definition);
            SerializedProperty property = so.FindProperty("viewPrefab");
            if (property == null)
            {
                Debug.LogWarning($"[KitchenPrefabGenerator] '{definition.name}' has no viewPrefab field.");
                return;
            }

            property.objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        // ------------------------------------------------------------------ staff

        private static void GenerateStaff(Result result)
        {
            const string path = StaffFolder + "/Staff_Chef.prefab";

            GameObject source = ViewFactory.BuildWhiteboxStaffView();
            StaffView view = source.GetComponent<StaffView>();
            if (view != null)
            {
                view.EnsureBuilt();
            }

            GameObject saved = SavePrefab(source, path);
            if (saved != null)
            {
                result.StaffPrefab = saved;
                result.CreatedPaths.Add(path);
            }
        }

        // ------------------------------------------------------------------ grid

        private static void GenerateGrid(Result result, LevelConfig level)
        {
            float width = level != null ? level.Width * level.CellSize + 0.6f : 10.6f;
            float depth = level != null ? level.Height * level.CellSize + 0.6f : 10.6f;

            // Floor slab: a real 3D volume so the kitchen reads as solid ground.
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor_Kitchen";
            floor.transform.localScale = new Vector3(width, 0.3f, depth);
            Collider floorCollider = floor.GetComponent<Collider>();
            if (floorCollider != null)
            {
                Object.DestroyImmediate(floorCollider);
            }

            Renderer floorRenderer = floor.GetComponent<Renderer>();
            if (floorRenderer != null)
            {
                // Resolves to the authored material asset, so the saved prefab keeps a valid material
                // instead of serialising null and rendering as magenta.
                floorRenderer.sharedMaterial = Whitebox.SolidMaterial(Whitebox.FloorColor, "Floor");
                floorRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                floorRenderer.receiveShadows = true;
            }

            GameLayers.SetLayerRecursively(floor, GameLayers.Ground);

            const string floorPath = FloorPrefabPath;
            GameObject savedFloor = SavePrefab(floor, floorPath);
            if (savedFloor != null)
            {
                result.FloorPrefab = savedFloor;
                result.CreatedPaths.Add(floorPath);
            }

            // Cell decal: a flat overlay quad the grid repeats once per cell.
            GameObject cell = Whitebox.Decal("Cell", null, Vector3.zero, 1f, Whitebox.GridCellColor, GameLayers.Ground);
            cell.name = "Grid_Cell";

            const string cellPath = CellPrefabPath;
            GameObject savedCell = SavePrefab(cell, cellPath);
            if (savedCell != null)
            {
                result.CellPrefab = savedCell;
                result.CreatedPaths.Add(cellPath);
            }

            AssignGridViewPrefabs(savedFloor, savedCell);
        }

        /// <summary>Points the GridView component in the scene at the generated floor and cell prefabs.</summary>
        public static void AssignGridViewPrefabs(GameObject floor, GameObject cell)
        {
            GridView[] views = Object.FindObjectsOfType<GridView>(true);
            for (int i = 0; i < views.Length; i++)
            {
                SerializedObject so = new SerializedObject(views[i]);
                SerializedProperty floorProperty = so.FindProperty("floorPrefab");
                SerializedProperty cellProperty = so.FindProperty("cellPrefab");

                if (floorProperty != null)
                {
                    floorProperty.objectReferenceValue = floor;
                }

                if (cellProperty != null)
                {
                    cellProperty.objectReferenceValue = cell;
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(views[i]);
            }
        }

        // ------------------------------------------------------------------ items

        private static void GenerateItems(Result result)
        {
            List<ItemDefinition> definitions = LoadSavedItems();

            for (int i = 0; i < definitions.Count; i++)
            {
                ItemDefinition item = definitions[i];
                if (item == null)
                {
                    continue;
                }

                string path = $"{IngredientFolder}/Item_{item.Id}.prefab";

                // Built without the item reference on purpose: a saved prefab must not carry a
                // reference to the temporary in-memory definition used during generation.
                GameObject token = Whitebox.BuildItemToken(item, null, $"Item_{item.Id}", GameLayers.Ground);
                DetachItemDefinition(token);

                GameObject saved = SavePrefab(token, path);
                if (saved != null)
                {
                    AssignItemViewPrefab(item, saved);
                    result.CreatedPaths.Add(path);
                }
            }
        }

        /// <summary>
        /// Clears any reference to a runtime ItemDefinition inside a generated item prefab. The
        /// definition passed to the builder is a throwaway authoring object, so keeping it would leave
        /// a dangling reference in the saved asset.
        /// </summary>
        private static void DetachItemDefinition(GameObject token)
        {
            if (token == null)
            {
                return;
            }

            Component[] components = token.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                {
                    continue;
                }

                SerializedObject so = new SerializedObject(component);
                SerializedProperty property = so.GetIterator();
                bool changed = false;

                while (property.NextVisible(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference)
                    {
                        continue;
                    }

                    if (property.objectReferenceValue is ItemDefinition)
                    {
                        property.objectReferenceValue = null;
                        changed = true;
                    }
                }

                if (changed)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        private static void AssignItemViewPrefab(ItemDefinition item, GameObject prefab)
        {
            SerializedObject so = new SerializedObject(item);
            SerializedProperty property = so.FindProperty("viewPrefab");
            if (property == null)
            {
                return;
            }

            property.objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// Saves a temporary object as a prefab asset and cleans up the temporary object.
        ///
        /// Materials are carried over explicitly: a material created in memory serialises as null in a
        /// prefab, which is what made every cooker draw magenta. Each material is copied from the
        /// source hierarchy, and any material that is not already an asset is embedded into the prefab
        /// as a sub asset so the reference stays valid.
        /// </summary>
        public static GameObject SavePrefab(GameObject source, string path)
        {
            if (source == null)
            {
                return null;
            }

            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));

            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            // The intended materials are captured before saving, because SaveAsPrefabAsset can drop
            // a reference to a material that does not exist as an asset yet. Keying by hierarchy path
            // makes the pairing exact regardless of ordering.
            Dictionary<string, Material> intended = CollectMaterialMap(source);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(source, path);
            Object.DestroyImmediate(source);

            if (saved == null)
            {
                Debug.LogError($"[KitchenPrefabGenerator] Failed to save '{path}'.");
                return null;
            }

            ApplyMaterials(saved, intended, path);
            AssetDatabase.SaveAssets();

            WarnAboutMissingMaterials(saved, path);
            return saved;
        }

        /// <summary>Maps each renderer's hierarchy path to the material it should have.</summary>
        private static Dictionary<string, Material> CollectMaterialMap(GameObject root)
        {
            Dictionary<string, Material> map = new Dictionary<string, Material>();
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer renderer = renderers[i];
                if (renderer == null || renderer.sharedMaterial == null)
                {
                    continue;
                }

                map[RelativePath(root.transform, renderer.transform)] = renderer.sharedMaterial;
            }

            return map;
        }

        /// <summary>
        /// Re-applies the intended materials to a saved prefab. Materials that are not already assets
        /// are embedded into the prefab as sub assets, so the reference serialises instead of becoming
        /// null and the part never falls back to Unity's magenta error shader.
        /// </summary>
        private static void ApplyMaterials(GameObject saved, Dictionary<string, Material> intended, string path)
        {
            if (saved == null || intended.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<string, Material> pair in intended)
            {
                Material material = pair.Value;
                if (material != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(material)))
                {
                    AssetDatabase.AddObjectToAsset(material, saved);
                }
            }

            MeshRenderer[] renderers = saved.GetComponentsInChildren<MeshRenderer>(true);
            int assigned = 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (intended.TryGetValue(RelativePath(saved.transform, renderer.transform), out Material material) &&
                    material != null)
                {
                    renderer.sharedMaterial = material;
                    assigned++;
                }
            }

            if (assigned > 0)
            {
                EditorUtility.SetDirty(saved);
            }
        }

        /// <summary>Path of a transform relative to a root, with the root itself as ".".</summary>
        private static string RelativePath(Transform root, Transform target)
        {
            if (target == root)
            {
                return ".";
            }

            string path = target.name;
            Transform current = target.parent;
            while (current != null && current != root)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        /// <summary>
        /// Reports renderers in a saved prefab that ended up without a material. A null material draws
        /// with Unity's magenta error shader, so this turns a silent visual bug into a log line.
        /// </summary>
        private static void WarnAboutMissingMaterials(GameObject prefab, string path)
        {
            MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            int missing = 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || renderers[i].sharedMaterial == null)
                {
                    missing++;
                }
            }

            if (missing > 0)
            {
                Debug.LogError(
                    $"[KitchenPrefabGenerator] '{path}' has {missing} renderer(s) with no material; " +
                    "they will draw as magenta. Check that the material library was generated first.");
            }
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = Path.GetFileName(folder);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
