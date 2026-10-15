using System.Collections.Generic;
using Kitchen.Core;
using Kitchen.Data;
using Kitchen.Inventory;
using Kitchen.Recipes;
using UnityEngine;

namespace Kitchen.Cookers
{
    /// <summary>
    /// Chopping station: one cut every <c>tickInterval</c> seconds, finished after
    /// <c>requiredTicks</c> cuts. Partial progress is preserved while the station is inactive, so a
    /// staff member who walks away pauses the work and resuming continues where it stopped.
    /// </summary>
    [CreateAssetMenu(menuName = "Kitchen/Work Strategy/Chopping", fileName = "Work_Chopping")]
    public class ChoppingWorkStrategy : CookerWorkStrategy
    {
        public override WorkType WorkType => WorkType.Chopping;

        public override string DisplayName => "切菜";

        public override void Tick(CookerRuntime cooker, float deltaTime)
        {
            if (cooker == null || cooker.Definition == null)
            {
                return;
            }

            CookerDefinition definition = cooker.Definition;
            RecipeManager recipes = cooker.Recipes;
            InventoryManager inventory = cooker.Inventory;

            if (recipes == null || inventory == null)
            {
                return;
            }

            // A locked recipe means a conversion is mid-flight. Keep cutting even though the raw
            // item has left the free stock, because the material is set aside for this station.
            if (cooker.LockedRecipe == null && !TryLockNextItem(cooker, recipes))
            {
                cooker.WorkSeconds = 0f;
                cooker.WorkProgress = 0f;
                cooker.State = cooker.IsActive ? CookerState.WaitingForInput : CookerState.Idle;
                cooker.StatusText = cooker.IsActive ? "等待材料" : "空闲（需要员工）";
                return;
            }

            int required = definition.RequiredTicks;
            float interval = definition.TickInterval;

            cooker.WorkSeconds += deltaTime;

            int guard = 0;
            while (cooker.WorkSeconds >= interval && cooker.WorkTicks < required && guard < 100000)
            {
                guard++;
                cooker.WorkSeconds -= interval;
                cooker.WorkTicks++;
            }

            cooker.WorkProgress = required > 0 ? Mathf.Clamp01((float)cooker.WorkTicks / required) : 0f;
            cooker.State = CookerState.Working;
            cooker.StatusText = $"切菜中 {cooker.WorkTicks}/{required}";

            if (cooker.WorkTicks < required)
            {
                return;
            }

            RecipeDefinition recipe = cooker.LockedRecipe;
            if (recipe == null)
            {
                cooker.ResetProgress();
                return;
            }

            // The raw item is reserved for this station, so it is consumed straight from the
            // reservation. Free stock is zero by definition while the work is in flight.
            // RecipeManager creates the finished slice exactly once; the station then holds it.
            if (!recipes.TryConvert(cooker, recipe, true, out List<ItemStack> produced))
            {
                ClearLock(cooker);
                cooker.State = CookerState.Idle;
                cooker.StatusText = "加工失败";
                return;
            }

            for (int i = 0; i < produced.Count; i++)
            {
                if (produced[i].IsValid)
                {
                    cooker.AddOutput(produced[i].Item, produced[i].Count);
                }
            }

            cooker.ForgetLock();
            cooker.ResetProgress();
            cooker.WorkSeconds = 0f;
            cooker.State = cooker.HasAnyOutput ? CookerState.ReadyToCollect : CookerState.Idle;

            if (TryLockNextItem(cooker, recipes))
            {
                cooker.State = CookerState.Working;
            }
            else
            {
                cooker.StatusText = cooker.HasAnyOutput
                    ? "已完成，请收取产出"
                    : "等待材料";
            }
        }

        /// <summary>Reserves one raw item and locks the matching recipe. Returns false when none is available.</summary>
        private static bool TryLockNextItem(CookerRuntime cooker, RecipeManager recipes)
        {
            if (cooker.HasAnyOutput)
            {
                // The station holds on to finished produce; the player must take it out first.
                return false;
            }

            if (cooker.Definition == null)
            {
                return false;
            }

            InventoryManager inventory = cooker.Inventory;
            RecipeDefinition usable = recipes.FindUsableRecipe(cooker.Definition.Kind);
            if (usable == null || inventory == null)
            {
                return false;
            }

            IReadOnlyList<RecipeInput> inputs = usable.Inputs;
            for (int i = 0; i < inputs.Count; i++)
            {
                if (!inventory.TryReserve(inputs[i].Item, inputs[i].Count))
                {
                    return false;
                }

                cooker.AddReserved(inputs[i].Item, inputs[i].Count);
            }

            cooker.LockedRecipe = usable;
            cooker.WorkTicks = 0;
            cooker.WorkSeconds = 0f;
            cooker.WorkProgress = 0f;
            return true;
        }
    }
}
