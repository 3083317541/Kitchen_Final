using UnityEngine;

namespace Kitchen.Core
{
    public enum CookerFacing
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3
    }

    public static class CookerFacingUtility
    {
        public static CookerFacing Normalize(CookerFacing facing)
        {
            return (CookerFacing)(((int)facing % 4 + 4) % 4);
        }

        public static CookerFacing Clockwise(CookerFacing facing)
        {
            return Normalize((CookerFacing)((int)facing + 1));
        }

        public static Vector2Int Offset(CookerFacing facing)
        {
            switch (Normalize(facing))
            {
                case CookerFacing.East: return Vector2Int.right;
                case CookerFacing.South: return Vector2Int.down;
                case CookerFacing.West: return Vector2Int.left;
                default: return Vector2Int.up;
            }
        }

        public static string Label(CookerFacing facing)
        {
            switch (Normalize(facing))
            {
                case CookerFacing.East: return "东";
                case CookerFacing.South: return "南";
                case CookerFacing.West: return "西";
                default: return "北";
            }
        }
    }

    /// <summary>Broad category of an item. Used for UI grouping and validation.</summary>
    public enum ItemCategory
    {
        Raw = 0,
        Processed = 1,
        Dish = 2,
        Plate = 3
    }

    /// <summary>
    /// Kind of cooker. Purely a content tag; runtime behaviour is selected by
    /// <see cref="WorkType"/> so new cooker kinds do not require new code paths.
    /// </summary>
    public enum CookerKind
    {
        ChoppingStation = 0,
        FryingPan = 1,
        AssemblyTable = 2,
        DishWasher = 3
    }

    /// <summary>
    /// How a cooker advances its work. <see cref="WorkManager"/> maps this value onto a
    /// processing strategy, so processing rules never live inside art/animation code.
    /// </summary>
    public enum WorkType
    {
        /// <summary>Discrete cuts: one tick every <c>tickInterval</c>, completes after <c>requiredTicks</c>.</summary>
        Chopping = 0,

        /// <summary>Continuous timer of <c>duration</c> seconds per unit produced.</summary>
        Timed = 1,

        /// <summary>Continuous timer of <c>duration</c> seconds, consumes a multi item recipe set at once.</summary>
        Assembly = 2,

        /// <summary>Continuous timer of <c>duration</c> seconds, consumes dirty plates one by one.</summary>
        Washing = 3
    }

    /// <summary>Interaction mode owned by <see cref="Kitchen.Input.InputManager"/>.</summary>
    public enum InteractionMode
    {
        Normal = 0,
        Build = 1,
        SelectIngredient = 2,
        MoveCooker = 3
    }

    /// <summary>Lifecycle of a cooker's current work.</summary>
    public enum CookerState
    {
        /// <summary>No staff on the cell and no locked work.</summary>
        Idle = 0,

        /// <summary>Staff present but the required input items are missing.</summary>
        WaitingForInput = 1,

        /// <summary>Staff present, work in progress.</summary>
        Working = 2,

        /// <summary>Work finished, output sitting in the cooker waiting to be picked up.</summary>
        ReadyToCollect = 3,

        /// <summary>Output slot is occupied; blocked until it is emptied.</summary>
        Blocked = 4,
        Preparing = 5,
        Collecting = 6,
        Burnt = 7,
        Cleaning = 8
    }

    /// <summary>Lifecycle of an order.</summary>
    public enum OrderState
    {
        Active = 0,
        Completed = 1,
        Failed = 2
    }
}
