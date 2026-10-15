using System.Collections.Generic;
using Kitchen.Core;
using UnityEngine;

namespace Kitchen.Data
{
    /// <summary>
    /// Single source of truth for the demo's content values (items, cookers, recipes, orders, level).
    /// The editor generator persists exactly these values as ScriptableObject assets; the runtime
    /// falls back to them when no assets are assigned, so the demo is always playable.
    /// </summary>
    public static class GameContentFactory
    {
        // ------------------------------------------------------------------ tuning constants

        public const int DefaultMapWidth = 5;
        public const int DefaultMapHeight = 5;
        public const float DefaultCellSize = 2f;
        public const float DefaultGroundHeight = 0f;
        public const int DefaultStartingMoney = 500;
        public const int DefaultStaffCount = 3;

        public const int ChoppingPrice = 50;
        public const int FryingPanPrice = 80;
        public const int AssemblyPrice = 120;
        public const int DishWasherPrice = 60;

        public const float ChopIntervalSeconds = 0.3f;
        public const int ChopTicksRequired = 10;
        public const float FryingSeconds = 5f;
        public const float AssemblySeconds = 10f;
        public const float WashingSeconds = 5f;

        public const float DefaultPlateReturnMinSeconds = 10f;
        public const float DefaultPlateReturnMaxSeconds = 20f;

        /// <summary>Id constants so scenes, assets and tests can reference content without magic strings.</summary>
        public static class Ids
        {
            public const string Bread = "bread";
            public const string BreadSlice = "bread_slice";
            public const string Cabbage = "cabbage";
            public const string CabbageSlice = "cabbage_slice";
            public const string Tomato = "tomato";
            public const string TomatoSlice = "tomato_slice";
            public const string RawMeat = "raw_meat";
            public const string RawMeatSlice = "raw_meat_slice";
            public const string CookedMeat = "cooked_meat";
            public const string CookedMeatSlice = "cooked_meat_slice";
            public const string Salad = "salad";
            public const string MeatBread = "meat_bread";
            public const string CleanPlate = "clean_plate";
            public const string DirtyPlate = "dirty_plate";

            public const string ChoppingStation = "chopping_station";
            public const string FryingPan = "frying_pan";
            public const string AssemblyTable = "assembly_table";
            public const string DishWasher = "dish_washer";
        }

        /// <summary>All content produced by <see cref="CreateRuntimeContent"/>.</summary>
        public class Content
        {
            public readonly List<ItemDefinition> Items = new List<ItemDefinition>();
            public readonly List<CookerDefinition> Cookers = new List<CookerDefinition>();
            public readonly List<RecipeDefinition> Recipes = new List<RecipeDefinition>();
            public readonly List<OrderDefinition> Orders = new List<OrderDefinition>();
            public LevelConfig Level;

            public ItemDefinition Item(string id)
            {
                for (int i = 0; i < Items.Count; i++)
                {
                    if (Items[i] != null && Items[i].Id == id)
                    {
                        return Items[i];
                    }
                }

                return null;
            }

            public CookerDefinition Cooker(string id)
            {
                for (int i = 0; i < Cookers.Count; i++)
                {
                    if (Cookers[i] != null && Cookers[i].Id == id)
                    {
                        return Cookers[i];
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// Rewrites every definition reference in a content set.
        ///
        /// The editor generator authors the content in memory first, saves it as assets one layer at
        /// a time, and uses this to point the still in-memory objects at the saved assets. Without it
        /// a persisted recipe would reference an in-memory item and would not survive a reload.
        /// </summary>
        public static void Remap(Content content, System.Func<ScriptableObject, ScriptableObject> resolve)
        {
            if (content == null || resolve == null)
            {
                return;
            }

            // Items are referenced by cookers, recipes, orders and the level, so they are remapped
            // in place first; everything else is remapped after its own layer is saved.
            for (int i = 0; i < content.Items.Count; i++)
            {
                content.Items[i] = RemapObject(content.Items[i], resolve);
            }

            for (int i = 0; i < content.Cookers.Count; i++)
            {
                content.Cookers[i] = RemapObject(content.Cookers[i], resolve);
            }

            for (int i = 0; i < content.Recipes.Count; i++)
            {
                content.Recipes[i] = RemapObject(content.Recipes[i], resolve);
            }

            for (int i = 0; i < content.Orders.Count; i++)
            {
                content.Orders[i] = RemapObject(content.Orders[i], resolve);
            }

            content.Level = RemapObject(content.Level, resolve);
        }

        private static T RemapObject<T>(T original, System.Func<ScriptableObject, ScriptableObject> resolve) where T : ScriptableObject
        {
            if (original == null)
            {
                return null;
            }

            ScriptableObject mapped = resolve(original);
            return mapped != null ? mapped as T : original;
        }

        // ------------------------------------------------------------------ public entry points

        /// <summary>
        /// Builds the full content set in memory. Used by the editor generator to persist assets and
        /// by the runtime as a fallback when no prepared <see cref="LevelConfig"/> is assigned.
        /// </summary>
        public static Content CreateRuntimeContent()
        {
            Content content = new Content();

            CreateItems(content);
            CreateCookers(content);
            CreateRecipes(content);
            CreateOrders(content);
            content.Level = CreateLevel(content);

            return content;
        }

        /// <summary>
        /// Default level layout used by both the generator and the runtime fallback.
        /// The asset name is fixed so the level can also be loaded with
        /// <c>Resources.Load&lt;LevelConfig&gt;("Kitchen/Level_01")</c>.
        /// </summary>
        public static LevelConfig CreateLevel(Content content)
        {
            if (content == null)
            {
                content = CreateRuntimeContent();
            }

            LevelConfig level = ScriptableObject.CreateInstance<LevelConfig>();
            level.name = "Level_01";

            ItemDefinition bread = content.Item(Ids.Bread);
            ItemDefinition cabbage = content.Item(Ids.Cabbage);
            ItemDefinition tomato = content.Item(Ids.Tomato);
            ItemDefinition rawMeat = content.Item(Ids.RawMeat);
            ItemDefinition rawMeatSlice = content.Item(Ids.RawMeatSlice);
            ItemDefinition cookedMeatSlice = content.Item(Ids.CookedMeatSlice);
            ItemDefinition cleanPlate = content.Item(Ids.CleanPlate);

            List<ItemStack> stock = new List<ItemStack>
            {
                new ItemStack(bread, 3),
                new ItemStack(cabbage, 4),
                new ItemStack(tomato, 4),
                new ItemStack(rawMeat, 4),
                new ItemStack(rawMeatSlice, 2),
                new ItemStack(cookedMeatSlice, 2),
                new ItemStack(cleanPlate, 3)
            };

            // Map is 5x5, centre cell is (2,2). Cookers form a ring so the middle row stays walkable.
            List<CookerPlacement> placements = new List<CookerPlacement>
            {
                new CookerPlacement(content.Cooker(Ids.ChoppingStation), new Vector2Int(0, 4)),
                new CookerPlacement(content.Cooker(Ids.FryingPan), new Vector2Int(4, 4)),
                new CookerPlacement(content.Cooker(Ids.AssemblyTable), new Vector2Int(0, 0)),
                new CookerPlacement(content.Cooker(Ids.DishWasher), new Vector2Int(4, 0))
            };

            List<Vector2Int> spawnOffsets = new List<Vector2Int>
            {
                new Vector2Int(-1, -1),
                new Vector2Int(0, -1),
                new Vector2Int(1, -1)
            };

            level.Setup(
                DefaultMapWidth,
                DefaultMapHeight,
                DefaultCellSize,
                Vector3.zero,
                DefaultGroundHeight,
                DefaultStartingMoney,
                stock,
                placements,
                DefaultStaffCount,
                spawnOffsets,
                content.Orders,
                content.Items,
                content.Cookers,
                content.Recipes);

            return level;
        }

        // ------------------------------------------------------------------ items

        private static void CreateItems(Content content)
        {
            content.Items.Add(Item(Ids.Bread, "面包", ItemCategory.Raw, new Color(0.78f, 0.58f, 0.32f), 0.30f));
            content.Items.Add(Item(Ids.BreadSlice, "面包片", ItemCategory.Processed, new Color(0.90f, 0.76f, 0.52f), 0.20f));
            content.Items.Add(Item(Ids.Cabbage, "包菜", ItemCategory.Raw, new Color(0.44f, 0.74f, 0.38f), 0.30f));
            content.Items.Add(Item(Ids.CabbageSlice, "菜片", ItemCategory.Processed, new Color(0.60f, 0.88f, 0.50f), 0.20f));
            content.Items.Add(Item(Ids.Tomato, "番茄", ItemCategory.Raw, new Color(0.86f, 0.25f, 0.20f), 0.28f));
            content.Items.Add(Item(Ids.TomatoSlice, "番茄片", ItemCategory.Processed, new Color(0.95f, 0.45f, 0.36f), 0.20f));
            content.Items.Add(Item(Ids.RawMeat, "生肉", ItemCategory.Raw, new Color(0.86f, 0.45f, 0.48f), 0.30f));
            content.Items.Add(Item(Ids.RawMeatSlice, "生肉片", ItemCategory.Processed, new Color(0.92f, 0.56f, 0.58f), 0.20f));
            content.Items.Add(Item(Ids.CookedMeat, "熟肉", ItemCategory.Processed, new Color(0.60f, 0.34f, 0.22f), 0.30f));
            content.Items.Add(Item(Ids.CookedMeatSlice, "熟肉片", ItemCategory.Processed, new Color(0.70f, 0.42f, 0.26f), 0.20f));
            content.Items.Add(Item(Ids.Salad, "沙拉", ItemCategory.Dish, new Color(0.52f, 0.82f, 0.44f), 0.34f));
            content.Items.Add(Item(Ids.MeatBread, "生菜番茄肉面包", ItemCategory.Dish, new Color(0.82f, 0.62f, 0.36f), 0.36f));
            content.Items.Add(Item(Ids.CleanPlate, "干净盘子", ItemCategory.Plate, new Color(0.93f, 0.95f, 0.97f), 0.34f));
            content.Items.Add(Item(Ids.DirtyPlate, "脏盘子", ItemCategory.Plate, new Color(0.48f, 0.44f, 0.38f), 0.34f));
        }

        private static ItemDefinition Item(string id, string displayName, ItemCategory category, Color color, float scale)
        {
            ItemDefinition definition = ScriptableObject.CreateInstance<ItemDefinition>();
            definition.name = "Item_" + id;
            definition.Setup(id, displayName, category, color, scale);
            return definition;
        }

        // ------------------------------------------------------------------ cookers

        private static void CreateCookers(Content content)
        {
            CookerDefinition chopping = ScriptableObject.CreateInstance<CookerDefinition>();
            chopping.name = "Cooker_ChoppingStation";
            chopping.SetupChopping(
                Ids.ChoppingStation,
                "切菜台",
                ChoppingPrice,
                ChopIntervalSeconds,
                ChopTicksRequired,
                new Color(0.74f, 0.70f, 0.64f),
                new Color(0.42f, 0.66f, 0.90f),
                new Vector3(1.6f, 0.42f, 1.6f));
            content.Cookers.Add(chopping);

            CookerDefinition frying = ScriptableObject.CreateInstance<CookerDefinition>();
            frying.name = "Cooker_FryingPan";
            frying.Setup(
                Ids.FryingPan,
                "煎锅",
                CookerKind.FryingPan,
                WorkType.Timed,
                FryingPanPrice,
                FryingSeconds,
                new Color(0.30f, 0.32f, 0.36f),
                new Color(0.92f, 0.42f, 0.20f),
                new Vector3(1.5f, 0.35f, 1.5f));
            content.Cookers.Add(frying);

            CookerDefinition assembly = ScriptableObject.CreateInstance<CookerDefinition>();
            assembly.name = "Cooker_AssemblyTable";
            assembly.Setup(
                Ids.AssemblyTable,
                "组装台",
                CookerKind.AssemblyTable,
                WorkType.Assembly,
                AssemblyPrice,
                AssemblySeconds,
                new Color(0.80f, 0.78f, 0.72f),
                new Color(0.55f, 0.45f, 0.80f),
                new Vector3(1.7f, 0.46f, 1.7f));
            content.Cookers.Add(assembly);

            CookerDefinition washer = ScriptableObject.CreateInstance<CookerDefinition>();
            washer.name = "Cooker_DishWasher";
            washer.Setup(
                Ids.DishWasher,
                "洗碗台",
                CookerKind.DishWasher,
                WorkType.Washing,
                DishWasherPrice,
                WashingSeconds,
                new Color(0.62f, 0.68f, 0.74f),
                new Color(0.34f, 0.72f, 0.78f),
                new Vector3(1.6f, 0.60f, 1.6f));
            content.Cookers.Add(washer);
        }

        // ------------------------------------------------------------------ recipes

        private static void CreateRecipes(Content content)
        {
            ItemDefinition bread = content.Item(Ids.Bread);
            ItemDefinition breadSlice = content.Item(Ids.BreadSlice);
            ItemDefinition cabbage = content.Item(Ids.Cabbage);
            ItemDefinition cabbageSlice = content.Item(Ids.CabbageSlice);
            ItemDefinition tomato = content.Item(Ids.Tomato);
            ItemDefinition tomatoSlice = content.Item(Ids.TomatoSlice);
            ItemDefinition rawMeat = content.Item(Ids.RawMeat);
            ItemDefinition rawMeatSlice = content.Item(Ids.RawMeatSlice);
            ItemDefinition cookedMeat = content.Item(Ids.CookedMeat);
            ItemDefinition cookedMeatSlice = content.Item(Ids.CookedMeatSlice);
            ItemDefinition salad = content.Item(Ids.Salad);
            ItemDefinition meatBread = content.Item(Ids.MeatBread);
            ItemDefinition cleanPlate = content.Item(Ids.CleanPlate);
            ItemDefinition dirtyPlate = content.Item(Ids.DirtyPlate);

            // Chopping station: one raw item -> one sliced item, 10 cuts at 0.3s.
            content.Recipes.Add(RecipeDefinition.Create(
                "chop_bread", "切面包", CookerKind.ChoppingStation,
                new[] { new RecipeInput(bread, 1) },
                new[] { new ItemStack(breadSlice, 1) }));

            content.Recipes.Add(RecipeDefinition.Create(
                "chop_cabbage", "切包菜", CookerKind.ChoppingStation,
                new[] { new RecipeInput(cabbage, 1) },
                new[] { new ItemStack(cabbageSlice, 1) }));

            content.Recipes.Add(RecipeDefinition.Create(
                "chop_tomato", "切番茄", CookerKind.ChoppingStation,
                new[] { new RecipeInput(tomato, 1) },
                new[] { new ItemStack(tomatoSlice, 1) }));

            content.Recipes.Add(RecipeDefinition.Create(
                "chop_raw_meat", "切生肉", CookerKind.ChoppingStation,
                new[] { new RecipeInput(rawMeat, 1) },
                new[] { new ItemStack(rawMeatSlice, 1) }));

            content.Recipes.Add(RecipeDefinition.Create(
                "chop_cooked_meat", "切熟肉", CookerKind.ChoppingStation,
                new[] { new RecipeInput(cookedMeat, 1) },
                new[] { new ItemStack(cookedMeatSlice, 1) }));

            // Frying pan: 5s per portion.
            content.Recipes.Add(RecipeDefinition.Create(
                "fry_raw_meat_slice", "煎生肉片", CookerKind.FryingPan,
                new[] { new RecipeInput(rawMeatSlice, 1) },
                new[] { new ItemStack(cookedMeatSlice, 1) }));

            content.Recipes.Add(RecipeDefinition.Create(
                "fry_raw_meat", "煎生肉", CookerKind.FryingPan,
                new[] { new RecipeInput(rawMeat, 1) },
                new[] { new ItemStack(cookedMeat, 1) }));

            // Assembly table: 10s per portion, exact quantities.
            // The larger recipe is listed first because it is the more specific match.
            content.Recipes.Add(RecipeDefinition.Create(
                "assemble_meat_bread", "组装生菜番茄肉面包", CookerKind.AssemblyTable,
                new[]
                {
                    new RecipeInput(cabbageSlice, 1),
                    new RecipeInput(tomatoSlice, 1),
                    new RecipeInput(cookedMeatSlice, 1),
                    new RecipeInput(breadSlice, 1)
                },
                new[] { new ItemStack(meatBread, 1) },
                10));

            content.Recipes.Add(RecipeDefinition.Create(
                "assemble_salad", "组装沙拉", CookerKind.AssemblyTable,
                new[]
                {
                    new RecipeInput(cabbageSlice, 1),
                    new RecipeInput(tomatoSlice, 1)
                },
                new[] { new ItemStack(salad, 1) }));

            // Dish washer: 5s per plate. The plate is both consumed and produced, which is why
            // input and output are allowed to reference the same item kind here.
            content.Recipes.Add(RecipeDefinition.Create(
                "wash_plate", "洗盘子", CookerKind.DishWasher,
                new[] { new RecipeInput(dirtyPlate, 1) },
                new[] { new ItemStack(cleanPlate, 1) }));
        }

        // ------------------------------------------------------------------ orders

        private static void CreateOrders(Content content)
        {
            ItemDefinition salad = content.Item(Ids.Salad);
            ItemDefinition meatBread = content.Item(Ids.MeatBread);

            content.Orders.Add(OrderDefinition.Create(
                "order_salad",
                "沙拉订单",
                salad,
                1,
                1,
                120,
                120f));

            content.Orders.Add(OrderDefinition.Create(
                "order_meat_bread",
                "生菜番茄肉面包订单",
                meatBread,
                1,
                1,
                220,
                240f));
        }
    }
}
