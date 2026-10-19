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

            // Recipe ids the player can pin on a station.
            public const string ChopCabbage = "chop_cabbage";
            public const string ChopTomato = "chop_tomato";
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

            /// <summary>Resolves an order definition by id. Returns null when unknown.</summary>
            public OrderDefinition Order(string id)
            {
                for (int i = 0; i < Orders.Count; i++)
                {
                    if (Orders[i] != null && Orders[i].Id == id)
                    {
                        return Orders[i];
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

        /// <summary>Number of campaign levels the factory can build.</summary>
        public const int LevelCount = 3;

        /// <summary>
        /// The first level, kept as a one argument shorthand for callers that predate the campaign.
        /// </summary>
        public static LevelConfig CreateLevel(Content content)
        {
            return CreateLevel(content, 1);
        }

        /// <summary>
        /// Builds the level with the given 1-based number, or the first level when it is unknown.
        /// The asset name is fixed as "Level_0N" so it can be loaded with
        /// <c>Resources.Load&lt;LevelConfig&gt;("Kitchen/Level_0N")</c>.
        /// </summary>
        public static LevelConfig CreateLevel(Content content, int levelNumber)
        {
            if (content == null)
            {
                content = CreateRuntimeContent();
            }

            switch (levelNumber)
            {
                case 2:
                    return BuildLevel(content, Level2Spec(content));
                case 3:
                    return BuildLevel(content, Level3Spec(content));
                default:
                    return BuildLevel(content, Level1Spec(content));
            }
        }

        /// <summary>Every campaign level, in order.</summary>
        public static List<LevelConfig> CreateAllLevels(Content content)
        {
            List<LevelConfig> levels = new List<LevelConfig>(LevelCount);
            for (int i = 1; i <= LevelCount; i++)
            {
                levels.Add(CreateLevel(content, i));
            }

            return levels;
        }

        /// <summary>What distinguishes one level from another.</summary>
        private class LevelSpec
        {
            public int Number;
            public string Title = string.Empty;
            public int Next;
            public int Money;
            public int Staff;
            public List<Vector2Int> SpawnOffsets = new List<Vector2Int>();
            public List<ItemStack> Stock = new List<ItemStack>();
            public List<CookerPlacement> Placements = new List<CookerPlacement>();
            public List<ItemDefinition> Items = new List<ItemDefinition>();
            public List<CookerDefinition> Cookers = new List<CookerDefinition>();
            public List<OrderDefinition> Orders = new List<OrderDefinition>();
            public List<RecipeDefinition> Recipes = new List<RecipeDefinition>();
            public List<string> Tutorial = new List<string>();
            public int RewardMoney;
            public int RewardStaff;
            public List<string> RewardCookers = new List<string>();
        }

        /// <summary>
        /// Level 1: salad only, chopping station and assembly table, one chef.
        /// Starting money buys exactly one chopping station and one assembly table, so both have to be
        /// placed before any salad can be made.
        /// </summary>
        private static LevelSpec Level1Spec(Content content)
        {
            LevelSpec spec = new LevelSpec
            {
                Number = 1,
                Title = "第一关 · 沙拉",
                Next = 2,
                Money = ChoppingPrice + AssemblyPrice,
                Staff = 1
            };

            // A single chef, one row below the centre so the middle of the map stays free.
            spec.SpawnOffsets.Add(new Vector2Int(0, -1));

            AddItem(spec, content, Ids.Cabbage, 4);
            AddItem(spec, content, Ids.Tomato, 4);
            AddItem(spec, content, Ids.CleanPlate, 3);

            // Nothing is pre-placed: placing the cookers is what this level teaches.
            spec.Cookers.Add(content.Cooker(Ids.ChoppingStation));
            spec.Cookers.Add(content.Cooker(Ids.AssemblyTable));

            AddOrder(spec, content, "order_salad");
            AddOrder(spec, content, "order_salad_plus");

            spec.Tutorial.Add("第一步：用鼠标左键选中厨师，再点击地面上的空格子把他放下。");
            spec.Tutorial.Add("第二步：在左侧商店购买切菜台，然后点击一个空格子放置。");
            spec.Tutorial.Add("第三步：同样方式购买并放置组装台。");
            spec.Tutorial.Add("第四步：让厨师站在厨具所在的格子上，厨具才会开始工作。");

            spec.RewardStaff = 1;
            spec.RewardCookers.Add(Ids.FryingPan);

            return spec;
        }

        /// <summary>
        /// Level 2: adds the frying pan and a second chef. The pan needs a chef standing on it for the
        /// whole five seconds, which is the lesson this level teaches.
        /// </summary>
        private static LevelSpec Level2Spec(Content content)
        {
            LevelSpec spec = new LevelSpec
            {
                Number = 2,
                Title = "第二关 · 沙拉与汉堡",
                Next = 3,
                Money = 250,
                Staff = 2
            };

            spec.SpawnOffsets.Add(new Vector2Int(-1, -1));
            spec.SpawnOffsets.Add(new Vector2Int(1, -1));

            AddItem(spec, content, Ids.Cabbage, 4);
            AddItem(spec, content, Ids.Tomato, 4);
            AddItem(spec, content, Ids.Bread, 4);
            AddItem(spec, content, Ids.RawMeat, 4);
            AddItem(spec, content, Ids.CleanPlate, 3);

            // The frying pan is last level's reward, so it is already standing in the kitchen.
            spec.Placements.Add(new CookerPlacement(content.Cooker(Ids.FryingPan), new Vector2Int(4, 4)));

            spec.Cookers.Add(content.Cooker(Ids.ChoppingStation));
            spec.Cookers.Add(content.Cooker(Ids.FryingPan));
            spec.Cookers.Add(content.Cooker(Ids.AssemblyTable));

            AddOrder(spec, content, "order_salad_plus");
            AddOrder(spec, content, "order_meat_bread");

            spec.Tutorial.Add("煎锅是有火的厨具：厨师必须一直站在上面，走开就会暂停加工。");
            spec.Tutorial.Add("生肉要先在切菜台切成生肉片，再放进煎锅煎成熟肉片。");
            spec.Tutorial.Add("汉堡需要 菜片 + 番茄片 + 熟肉片 + 面包片，四样缺一不可。");

            spec.RewardStaff = 1;
            spec.RewardCookers.Add(Ids.DishWasher);

            return spec;
        }

        /// <summary>
        /// Level 3: adds the dish washer so the plate loop can be closed. The kitchen starts with both
        /// the pan and the washer, and stock that already contains dirty plates.
        /// </summary>
        private static LevelSpec Level3Spec(Content content)
        {
            LevelSpec spec = new LevelSpec
            {
                Number = 3,
                Title = "第三关 · 盘子循环",
                Next = 0,
                Money = 300,
                Staff = 3
            };

            spec.SpawnOffsets.Add(new Vector2Int(-1, -1));
            spec.SpawnOffsets.Add(new Vector2Int(0, -1));
            spec.SpawnOffsets.Add(new Vector2Int(1, -1));

            AddItem(spec, content, Ids.Cabbage, 4);
            AddItem(spec, content, Ids.Tomato, 4);
            AddItem(spec, content, Ids.Bread, 4);
            AddItem(spec, content, Ids.RawMeat, 4);
            AddItem(spec, content, Ids.CleanPlate, 3);
            AddItem(spec, content, Ids.DirtyPlate, 2);

            spec.Placements.Add(new CookerPlacement(content.Cooker(Ids.FryingPan), new Vector2Int(4, 4)));
            spec.Placements.Add(new CookerPlacement(content.Cooker(Ids.DishWasher), new Vector2Int(4, 0)));

            spec.Cookers.Add(content.Cooker(Ids.ChoppingStation));
            spec.Cookers.Add(content.Cooker(Ids.FryingPan));
            spec.Cookers.Add(content.Cooker(Ids.AssemblyTable));
            spec.Cookers.Add(content.Cooker(Ids.DishWasher));

            AddOrder(spec, content, "order_salad_plus");
            AddOrder(spec, content, "order_meat_bread");
            AddOrder(spec, content, "order_meat_bread");

            spec.Tutorial.Add("上菜会消耗干净盘子；盘子过一会儿会脏着回来，要用洗碗台洗干净。");

            return spec;
        }

        /// <summary>
        /// A sandbox level that uses everything: all four cookers already placed, every item, every
        /// recipe and three chefs.
        ///
        /// The acceptance suite drives this instead of a campaign level. Campaign levels are balanced
        /// for teaching and start with almost nothing, so testing against them would mean testing the
        /// balancing rather than the systems.
        /// </summary>
        public static LevelConfig CreateSandboxLevel(Content content)
        {
            if (content == null)
            {
                content = CreateRuntimeContent();
            }

            LevelSpec spec = new LevelSpec
            {
                Number = 1,
                Title = "Sandbox",
                Next = 0,
                Money = DefaultStartingMoney,
                Staff = DefaultStaffCount
            };

            spec.SpawnOffsets.Add(new Vector2Int(-1, -1));
            spec.SpawnOffsets.Add(new Vector2Int(0, -1));
            spec.SpawnOffsets.Add(new Vector2Int(1, -1));

            AddItem(spec, content, Ids.Bread, 3);
            AddItem(spec, content, Ids.Cabbage, 4);
            AddItem(spec, content, Ids.Tomato, 4);
            AddItem(spec, content, Ids.RawMeat, 4);
            AddItem(spec, content, Ids.RawMeatSlice, 2);
            AddItem(spec, content, Ids.CookedMeatSlice, 2);
            AddItem(spec, content, Ids.CleanPlate, 3);
            AddItem(spec, content, Ids.DirtyPlate, 2);

            // A ring of cookers keeps the middle row walkable.
            spec.Placements.Add(new CookerPlacement(content.Cooker(Ids.ChoppingStation), new Vector2Int(0, 4)));
            spec.Placements.Add(new CookerPlacement(content.Cooker(Ids.FryingPan), new Vector2Int(4, 4)));
            spec.Placements.Add(new CookerPlacement(content.Cooker(Ids.AssemblyTable), new Vector2Int(0, 0)));
            spec.Placements.Add(new CookerPlacement(content.Cooker(Ids.DishWasher), new Vector2Int(4, 0)));

            spec.Cookers.Add(content.Cooker(Ids.ChoppingStation));
            spec.Cookers.Add(content.Cooker(Ids.FryingPan));
            spec.Cookers.Add(content.Cooker(Ids.AssemblyTable));
            spec.Cookers.Add(content.Cooker(Ids.DishWasher));

            AddOrder(spec, content, "order_salad");
            AddOrder(spec, content, "order_salad_plus");
            AddOrder(spec, content, "order_meat_bread");

            return BuildLevel(content, spec);
        }

        private static void AddItem(LevelSpec spec, Content content, string itemId, int count)
        {
            ItemDefinition item = content.Item(itemId);
            if (item == null)
            {
                return;
            }

            spec.Stock.Add(new ItemStack(item, count));
            if (!spec.Items.Contains(item))
            {
                spec.Items.Add(item);
            }
        }

        private static void AddOrder(LevelSpec spec, Content content, string orderId)
        {
            OrderDefinition order = content.Order(orderId);
            if (order != null)
            {
                spec.Orders.Add(order);
            }
        }

        /// <summary>
        /// Turns a spec into a level asset. The item set is grown to cover every queued order and every
        /// surviving recipe, then the recipes are filtered again so a level never references a recipe
        /// whose ingredients it does not have.
        /// </summary>
        private static LevelConfig BuildLevel(Content content, LevelSpec spec)
        {
            LevelConfig level = ScriptableObject.CreateInstance<LevelConfig>();
            level.name = $"Level_{spec.Number:00}";

            for (int i = 0; i < spec.Orders.Count; i++)
            {
                ItemDefinition dish = spec.Orders[i] != null ? spec.Orders[i].Dish : null;
                AddIfMissing(spec.Items, dish);
            }

            // Every level that serves something takes and returns plates.
            AddIfMissing(spec.Items, content.Item(Ids.CleanPlate));
            AddIfMissing(spec.Items, content.Item(Ids.DirtyPlate));

            ResolveContent(content, spec);

            level.Setup(
                DefaultMapWidth,
                DefaultMapHeight,
                DefaultCellSize,
                Vector3.zero,
                DefaultGroundHeight,
                spec.Money,
                spec.Stock,
                spec.Placements,
                spec.Staff,
                spec.SpawnOffsets,
                spec.Orders,
                spec.Items,
                spec.Cookers,
                spec.Recipes);

            level.SetCampaign(
                spec.Number,
                spec.Title,
                spec.Next,
                spec.Tutorial,
                spec.RewardMoney,
                spec.RewardStaff,
                spec.RewardCookers);

            return level;
        }

        /// <summary>
        /// Computes which items and recipes a level actually has.
        ///
        /// The starting stock and the queued orders seed the item set; then every recipe belonging to a
        /// buildable cooker whose inputs are all present contributes its outputs, repeated until nothing
        /// changes. Doing it this way matters: filtering recipes first and growing from the survivors
        /// removes every recipe whose output is not in the seed set, which is all of them, so a level
        /// would end up with no recipes at all.
        /// </summary>
        private static void ResolveContent(Content content, LevelSpec spec)
        {
            List<RecipeDefinition> candidates = new List<RecipeDefinition>();
            for (int i = 0; i < content.Recipes.Count; i++)
            {
                RecipeDefinition recipe = content.Recipes[i];
                if (recipe == null)
                {
                    continue;
                }

                if (spec.Cookers.Contains(content.Cooker(CookerIdFor(recipe.CookerKind))))
                {
                    candidates.Add(recipe);
                }
            }

            bool grew = true;
            int guard = 0;

            while (grew && guard++ < 32)
            {
                grew = false;

                for (int i = 0; i < candidates.Count; i++)
                {
                    RecipeDefinition recipe = candidates[i];
                    if (!InputsPresent(recipe, spec.Items))
                    {
                        continue;
                    }

                    for (int k = 0; k < recipe.Outputs.Count; k++)
                    {
                        grew |= AddIfMissing(spec.Items, recipe.Outputs[k].Item);
                    }
                }
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                RecipeDefinition recipe = candidates[i];

                // A recipe is part of the level only when everything it touches exists here.
                if (InputsPresent(recipe, spec.Items) && OutputsPresent(recipe, spec.Items))
                {
                    spec.Recipes.Add(recipe);
                }
            }
        }

        private static bool InputsPresent(RecipeDefinition recipe, List<ItemDefinition> items)
        {
            for (int i = 0; i < recipe.Inputs.Count; i++)
            {
                if (!items.Contains(recipe.Inputs[i].Item))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool OutputsPresent(RecipeDefinition recipe, List<ItemDefinition> items)
        {
            for (int i = 0; i < recipe.Outputs.Count; i++)
            {
                if (!items.Contains(recipe.Outputs[i].Item))
                {
                    return false;
                }
            }

            return true;
        }

        private static string CookerIdFor(CookerKind kind)
        {
            switch (kind)
            {
                case CookerKind.ChoppingStation:
                    return Ids.ChoppingStation;
                case CookerKind.FryingPan:
                    return Ids.FryingPan;
                case CookerKind.AssemblyTable:
                    return Ids.AssemblyTable;
                case CookerKind.DishWasher:
                    return Ids.DishWasher;
                default:
                    return string.Empty;
            }
        }

        private static bool AddIfMissing(List<ItemDefinition> items, ItemDefinition item)
        {
            if (item == null || items.Contains(item))
            {
                return false;
            }

            items.Add(item);
            return true;
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

            // Opening order. Generous time and a small reward, used to teach the salad chain.
            content.Orders.Add(OrderDefinition.Create(
                "order_salad",
                "沙拉订单",
                salad,
                1,
                1,
                100,
                180f));

            // A tighter salad order that later levels reuse.
            content.Orders.Add(OrderDefinition.Create(
                "order_salad_plus",
                "沙拉加急订单",
                salad,
                1,
                1,
                140,
                150f));

            // The larger dish, introduced when the frying pan arrives.
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
