using System.Collections.Generic;
using Kitchen.Core;
using UnityEngine;

namespace Kitchen.Data
{
    /// <summary>
    /// Everything GameRuntime needs to build a playable level: map geometry, starting money,
    /// starting stock, the staff roster, the initial orders and the content catalogue
    /// (cookers, recipes, items) that the managers operate on.
    /// </summary>
    [CreateAssetMenu(menuName = "Kitchen/Level Config", fileName = "Level_New")]
    public class LevelConfig : ScriptableObject
    {
        [Header("Map")]
        [SerializeField, Min(1)] private int width = 5;
        [SerializeField, Min(1)] private int height = 5;
        [SerializeField, Min(0.1f)] private float cellSize = 2f;
        [Tooltip("World XZ position that the centre of the map is aligned to.")]
        [SerializeField] private Vector3 mapCenter = Vector3.zero;
        [Tooltip("World Y of the walkable floor.")]
        [SerializeField] private float groundHeight = 0f;

        [Header("Economy")]
        [SerializeField, Min(0)] private int startingMoney = 500;

        [Header("Starting Stock")]
        [SerializeField] private List<ItemStack> startingInventory = new List<ItemStack>();
        [Tooltip("Cookers placed automatically when the level starts.")]
        [SerializeField] private List<CookerPlacement> startingCookers = new List<CookerPlacement>();

        [Header("Staff")]
        [SerializeField, Min(1)] private int staffCount = 3;
        [Tooltip("Cell offsets, relative to the map centre cell, used for the starting staff positions. Extra staff wrap around the list.")]
        [SerializeField] private List<Vector2Int> staffSpawnOffsets = new List<Vector2Int>();

        [Header("Orders")]
        [Tooltip("Orders are issued in this order. The level is cleared once every entry has been completed; the list does not wrap.")]
        [SerializeField] private List<OrderDefinition> orderSequence = new List<OrderDefinition>();

        [Header("Campaign")]
        [Tooltip("1-based level number. Also selects the Resources asset 'Kitchen/Level_0N'.")]
        [SerializeField] private int levelNumber = 1;
        [SerializeField] private string levelTitle = "第一关";
        [Tooltip("Level number loaded after this one is cleared. 0 means this is the last level.")]
        [SerializeField] private int nextLevelNumber = 0;

        [Tooltip("Tutorial hints shown at the start of the level, in order. Display only.")]
        [SerializeField] private List<string> tutorialSteps = new List<string>();

        [Header("Clear Reward")]
        [Tooltip("Money granted when the level is cleared. Carried into the next level through CampaignProgress.")]
        [SerializeField, Min(0)] private int rewardMoney = 0;
        [Tooltip("Extra employees granted when the level is cleared.")]
        [SerializeField, Min(0)] private int rewardStaffCount = 0;
        [Tooltip("Cookers granted when the level is cleared. Displayed on the reward panel.")]
        [SerializeField] private List<string> rewardCookerIds = new List<string>();

        [Header("Art Replacement")]
        [Tooltip("Employee prefab used for every chef. Empty falls back to the generated whitebox chef.")]
        [SerializeField] private GameObject staffPrefab;

        [Tooltip("Optional floor prefab. Empty falls back to the generated floor slab.")]
        [SerializeField] private GameObject floorPrefab;

        [Header("Content Catalogue")]
        [SerializeField] private List<ItemDefinition> items = new List<ItemDefinition>();
        [SerializeField] private List<CookerDefinition> cookers = new List<CookerDefinition>();
        [SerializeField] private List<RecipeDefinition> recipes = new List<RecipeDefinition>();

        public int Width => Mathf.Max(1, width);

        public int Height => Mathf.Max(1, height);

        public float CellSize => Mathf.Max(0.1f, cellSize);

        public Vector3 MapCenter => mapCenter;

        public float GroundHeight => groundHeight;

        public int StartingMoney => Mathf.Max(0, startingMoney);

        public IReadOnlyList<ItemStack> StartingInventory => startingInventory;

        public IReadOnlyList<CookerPlacement> StartingCookers => startingCookers;

        public int StaffCount => Mathf.Max(1, staffCount);

        public IReadOnlyList<Vector2Int> StaffSpawnOffsets => staffSpawnOffsets;

        public IReadOnlyList<OrderDefinition> OrderSequence => orderSequence;

        /// <summary>1-based level number, also the Resources asset suffix.</summary>
        public int LevelNumber => Mathf.Max(1, levelNumber);

        /// <summary>Player facing level name, e.g. "第一关".</summary>
        public string LevelTitle => string.IsNullOrEmpty(levelTitle) ? $"第 {LevelNumber} 关" : levelTitle;

        /// <summary>Level loaded after this one is cleared, or 0 when this is the last level.</summary>
        public int NextLevelNumber => Mathf.Max(0, nextLevelNumber);

        public bool HasNextLevel => NextLevelNumber > 0;

        /// <summary>Ordered tutorial hints for this level.</summary>
        public IReadOnlyList<string> TutorialSteps => tutorialSteps;

        public int RewardMoney => Mathf.Max(0, rewardMoney);

        public int RewardStaffCount => Mathf.Max(0, rewardStaffCount);

        public IReadOnlyList<string> RewardCookerIds => rewardCookerIds;

        /// <summary>Human readable reward summary, or an empty string when the level grants nothing.</summary>
        public string DescribeReward()
        {
            List<string> parts = new List<string>();

            if (RewardStaffCount > 0)
            {
                parts.Add($"员工 ×{RewardStaffCount}");
            }

            for (int i = 0; i < rewardCookerIds.Count; i++)
            {
                if (!string.IsNullOrEmpty(rewardCookerIds[i]))
                {
                    CookerDefinition definition = FindCooker(rewardCookerIds[i]);
                    parts.Add(definition != null ? definition.DisplayName : rewardCookerIds[i]);
                }
            }

            if (RewardMoney > 0)
            {
                parts.Add($"金钱 {RewardMoney}");
            }

            return parts.Count > 0 ? string.Join("、", parts.ToArray()) : string.Empty;
        }

        /// <summary>Fills the campaign fields. Used by the editor generator.</summary>
        public void SetCampaign(
            int number,
            string title,
            int nextNumber,
            IList<string> tutorial,
            int moneyReward,
            int staffReward,
            IList<string> cookerRewards)
        {
            levelNumber = Mathf.Max(1, number);
            levelTitle = title;
            nextLevelNumber = Mathf.Max(0, nextNumber);
            tutorialSteps = tutorial != null ? new List<string>(tutorial) : new List<string>();
            rewardMoney = Mathf.Max(0, moneyReward);
            rewardStaffCount = Mathf.Max(0, staffReward);
            rewardCookerIds = cookerRewards != null ? new List<string>(cookerRewards) : new List<string>();
        }

        public IReadOnlyList<ItemDefinition> Items => items;

        /// <summary>Employee art prefab, or null to use the generated whitebox chef.</summary>
        public GameObject StaffPrefab => staffPrefab;

        /// <summary>Floor art prefab, or null to use the generated floor slab.</summary>
        public GameObject FloorPrefab => floorPrefab;

        /// <summary>Assigns the art prefabs. Used by the editor generator.</summary>
        public void SetArtPrefabs(GameObject staff, GameObject floor)
        {
            staffPrefab = staff;
            floorPrefab = floor;
        }

        public IReadOnlyList<CookerDefinition> Cookers => cookers;

        public IReadOnlyList<RecipeDefinition> Recipes => recipes;

        /// <summary>Resolves a cooker definition by its id. Returns null when unknown.</summary>
        public CookerDefinition FindCooker(string id)
        {
            if (string.IsNullOrEmpty(id) || cookers == null)
            {
                return null;
            }

            for (int i = 0; i < cookers.Count; i++)
            {
                CookerDefinition definition = cookers[i];
                if (definition != null && definition.Id == id)
                {
                    return definition;
                }
            }

            return null;
        }

        /// <summary>Resolves an item definition by its id. Returns null when unknown.</summary>
        public ItemDefinition FindItem(string id)
        {
            if (string.IsNullOrEmpty(id) || items == null)
            {
                return null;
            }

            for (int i = 0; i < items.Count; i++)
            {
                ItemDefinition definition = items[i];
                if (definition != null && definition.Id == id)
                {
                    return definition;
                }
            }

            return null;
        }

        public OrderDefinition GetOrderForIndex(int index)
        {
            if (orderSequence == null || orderSequence.Count == 0)
            {
                return null;
            }

            int clamped = Mathf.Clamp(index, 0, orderSequence.Count - 1);
            return orderSequence[clamped];
        }

        /// <summary>
        /// Fills every field. Also repairs null collections so that a freshly created asset is
        /// always safe to use even before it is first serialized.
        /// </summary>
        public void Setup(
            int mapWidth,
            int mapHeight,
            float mapCellSize,
            Vector3 center,
            float floorHeight,
            int money,
            IList<ItemStack> stock,
            IList<CookerPlacement> placedCookers,
            int staff,
            IList<Vector2Int> spawnOffsets,
            IList<OrderDefinition> orders,
            IList<ItemDefinition> allItems,
            IList<CookerDefinition> allCookers,
            IList<RecipeDefinition> allRecipes)
        {
            width = Mathf.Max(1, mapWidth);
            height = Mathf.Max(1, mapHeight);
            cellSize = Mathf.Max(0.1f, mapCellSize);
            mapCenter = center;
            groundHeight = floorHeight;
            startingMoney = Mathf.Max(0, money);
            startingInventory = stock != null ? new List<ItemStack>(stock) : new List<ItemStack>();
            startingCookers = placedCookers != null ? new List<CookerPlacement>(placedCookers) : new List<CookerPlacement>();
            staffCount = Mathf.Max(1, staff);
            staffSpawnOffsets = spawnOffsets != null ? new List<Vector2Int>(spawnOffsets) : new List<Vector2Int>();
            orderSequence = orders != null ? new List<OrderDefinition>(orders) : new List<OrderDefinition>();
            items = allItems != null ? new List<ItemDefinition>(allItems) : new List<ItemDefinition>();
            cookers = allCookers != null ? new List<CookerDefinition>(allCookers) : new List<CookerDefinition>();
            recipes = allRecipes != null ? new List<RecipeDefinition>(allRecipes) : new List<RecipeDefinition>();
        }
    }
}
