using System;
using System.Collections.Generic;
using System.IO;
using Kitchen.Cookers;
using Kitchen.Core;
using Kitchen.Data;
using UnityEditor;
using UnityEngine;

namespace Kitchen.EditorTools
{
    /// <summary>
    /// Writes the demo content to real ScriptableObject assets under Assets/_Game/Resources/Kitchen.
    ///
    /// Assets are authored layer by layer (items, then cookers, then recipes and orders, then the
    /// level) and each layer is remapped afterwards, so every persisted reference points at another
    /// persisted asset. That is what lets the values be edited in the inspector and still load at
    /// runtime through <c>Resources.Load</c>.
    /// </summary>
    public static class KitchenDataAssetGenerator
    {
        public const string ResourcesRoot = "Assets/_Game/Resources/Kitchen";
        public const string DataFolder = "Assets/_Game/Data";
        public const string LevelAssetPath = ResourcesRoot + "/Level_01.asset";

        /// <summary>Sandbox level the acceptance suite drives.</summary>
        public const string SandboxLevelPath = ResourcesRoot + "/Level_Sandbox.asset";
        public const string StrategyFolder = ResourcesRoot + "/WorkStrategies";

        /// <summary>Result of a generation pass: the level asset plus the work strategy assets.</summary>
        public class Result
        {
            /// <summary>First campaign level. Kept for callers that predate the level list.</summary>
            public LevelConfig Level;

            /// <summary>Every campaign level, in order.</summary>
            public readonly List<LevelConfig> Levels = new List<LevelConfig>();

            /// <summary>
            /// Sandbox level used by the acceptance suite. Saved as an asset so every definition it
            /// references is the persisted one, which is what carries the prefab and material links.
            /// </summary>
            public LevelConfig Sandbox;

            /// <summary>
            /// The content set that was authored. The prefab generator needs it to know which cookers
            /// and items exist, and the Remap step has already pointed its references at the saved
            /// assets by the time generation finishes.
            /// </summary>
            public GameContentFactory.Content Content;

            public List<CookerWorkStrategy> Strategies = new List<CookerWorkStrategy>();
            public readonly List<string> CreatedPaths = new List<string>();
        }

        /// <summary>Generates (or regenerates) every data asset and returns the wired result.</summary>
        public static Result Generate()
        {
            Result result = new Result();

            GameContentFactory.Content content = GameContentFactory.CreateRuntimeContent();
            Dictionary<ScriptableObject, ScriptableObject> mapping = new Dictionary<ScriptableObject, ScriptableObject>();

            // Note: assets are created one at a time, not inside StartAssetEditing. LoadAssetAtPath
            // returns null while asset editing is deferred, and the generator reads every asset back
            // right after writing it so the next layer can reference it.
            try
            {
                RecursiveDelete(ResourcesRoot);
                EnsureFolder(ResourcesRoot);
                EnsureFolder(StrategyFolder);

                SaveStrategies(result, mapping);

                SaveItems(content, mapping, result);
                RemapInto(content, mapping);
                SaveCookers(content, mapping, result);
                RemapInto(content, mapping);
                SaveRecipes(content, mapping, result);
                SaveOrders(content, mapping, result);
                RemapInto(content, mapping);
                SaveLevels(content, mapping, result);
                RemapInto(content, mapping);

                result.Level = result.Levels.Count > 0 ? result.Levels[0] : content.Level;
                result.Content = content;
            }
            finally
            {
                AssetDatabase.Refresh();
            }

            return result;
        }

        // ------------------------------------------------------------------ layers

        private static void SaveStrategies(Result result, Dictionary<ScriptableObject, ScriptableObject> mapping)
        {
            CreateAsset(Strategy(ChoppingWorkStrategy.CreateInstance<ChoppingWorkStrategy>(), "Work_Chopping"), $"{StrategyFolder}/Work_Chopping.asset", mapping, result);
            CreateAsset(Strategy(TimedWorkStrategy.CreateInstance<TimedWorkStrategy>(), "Work_Timed"), $"{StrategyFolder}/Work_Timed.asset", mapping, result);
            CreateAsset(Strategy(AssemblyWorkStrategy.CreateInstance<AssemblyWorkStrategy>(), "Work_Assembly"), $"{StrategyFolder}/Work_Assembly.asset", mapping, result);
            CreateAsset(Strategy(WashingWorkStrategy.CreateInstance<WashingWorkStrategy>(), "Work_Washing"), $"{StrategyFolder}/Work_Washing.asset", mapping, result);

            foreach (KeyValuePair<ScriptableObject, ScriptableObject> pair in mapping)
            {
                if (pair.Value is CookerWorkStrategy strategy)
                {
                    result.Strategies.Add(strategy);
                }
            }

            // Deterministic order: one strategy per work type, in enum order.
            result.Strategies.Sort((a, b) => a.WorkType.CompareTo(b.WorkType));
        }

        private static ScriptableObject Strategy(CookerWorkStrategy instance, string assetName)
        {
            instance.name = assetName;
            return instance;
        }

        private static void SaveItems(GameContentFactory.Content content, Dictionary<ScriptableObject, ScriptableObject> mapping, Result result)
        {
            EnsureFolder($"{DataFolder}/Ingredients");
            for (int i = 0; i < content.Items.Count; i++)
            {
                ItemDefinition item = content.Items[i];
                CreateAsset(item, $"{DataFolder}/Ingredients/Item_{item.Id}.asset", mapping, result);
            }
        }

        private static void SaveCookers(GameContentFactory.Content content, Dictionary<ScriptableObject, ScriptableObject> mapping, Result result)
        {
            EnsureFolder($"{DataFolder}/Cookers");
            for (int i = 0; i < content.Cookers.Count; i++)
            {
                CookerDefinition cooker = content.Cookers[i];
                CreateAsset(cooker, $"{DataFolder}/Cookers/Cooker_{cooker.Id}.asset", mapping, result);
            }
        }

        private static void SaveRecipes(GameContentFactory.Content content, Dictionary<ScriptableObject, ScriptableObject> mapping, Result result)
        {
            EnsureFolder($"{DataFolder}/Recipes");
            for (int i = 0; i < content.Recipes.Count; i++)
            {
                RecipeDefinition recipe = content.Recipes[i];
                CreateAsset(recipe, $"{DataFolder}/Recipes/Recipe_{recipe.Id}.asset", mapping, result);
            }
        }

        private static void SaveOrders(GameContentFactory.Content content, Dictionary<ScriptableObject, ScriptableObject> mapping, Result result)
        {
            EnsureFolder($"{DataFolder}/Orders");
            for (int i = 0; i < content.Orders.Count; i++)
            {
                OrderDefinition order = content.Orders[i];
                CreateAsset(order, $"{DataFolder}/Orders/Order_{order.Id}.asset", mapping, result);
            }
        }

        /// <summary>
        /// Writes every campaign level as its own Resources asset, so the kitchen scene can load
        /// "Kitchen/Level_0N" without a scene reference.
        ///
        /// The levels are created from the shared catalogue, which means one item/cooker/recipe set is
        /// authored once and each level only decides what it uses. A duplicate copy under Data/Levels
        /// is deliberately not written: two assets with different guids invite edits that silently do
        /// nothing, and the Resources asset is the one the game loads.
        /// </summary>
        private static void SaveLevels(GameContentFactory.Content content, Dictionary<ScriptableObject, ScriptableObject> mapping, Result result)
        {
            RemoveStaleLevelCopies();

            for (int number = 1; number <= GameContentFactory.LevelCount; number++)
            {
                LevelConfig level = GameContentFactory.CreateLevel(content, number);
                string path = $"{ResourcesRoot}/Level_{number:00}.asset";
                CreateAsset(level, path, mapping, result);

                if (mapping.TryGetValue(level, out ScriptableObject saved) && saved is LevelConfig savedLevel)
                {
                    result.Levels.Add(savedLevel);
                }
            }

            // Saved last so it references the already saved cookers, items, recipes and orders.
            LevelConfig sandbox = GameContentFactory.CreateSandboxLevel(content);
            CreateAsset(sandbox, SandboxLevelPath, mapping, result);
            if (mapping.TryGetValue(sandbox, out ScriptableObject savedSandbox) && savedSandbox is LevelConfig sandboxLevel)
            {
                result.Sandbox = sandboxLevel;
            }
        }

        /// <summary>Deletes the old duplicated level assets that used to live under Data/Levels.</summary>
        private static void RemoveStaleLevelCopies()
        {
            const string legacyFolder = DataFolder + "/Levels";
            if (AssetDatabase.IsValidFolder(legacyFolder))
            {
                AssetDatabase.DeleteAsset(legacyFolder);
                Debug.Log("[KitchenDataAssetGenerator] Removed the duplicated assets under Data/Levels.");
            }
        }

        // ------------------------------------------------------------------ helpers

        private static void CreateAsset(
            ScriptableObject instance,
            string path,
            Dictionary<ScriptableObject, ScriptableObject> mapping,
            Result result)
        {
            if (instance == null)
            {
                return;
            }

            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));

            ScriptableObject existing = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.CreateAsset(instance, path);
            ScriptableObject saved = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (saved == null)
            {
                Debug.LogError($"[KitchenDataAssetGenerator] Failed to create '{path}'.");
                return;
            }

            mapping[instance] = saved;
            result.CreatedPaths.Add(path);
        }

        private static void RemapInto(GameContentFactory.Content content, Dictionary<ScriptableObject, ScriptableObject> mapping)
        {
            GameContentFactory.Remap(content, source => mapping.TryGetValue(source, out ScriptableObject target) ? target : source);
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

        private static void RecursiveDelete(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
