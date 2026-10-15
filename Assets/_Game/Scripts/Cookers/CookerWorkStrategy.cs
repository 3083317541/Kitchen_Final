using System.Collections.Generic;
using Kitchen.Core;
using Kitchen.Data;
using Kitchen.Inventory;
using Kitchen.Recipes;
using UnityEngine;

namespace Kitchen.Cookers
{
    /// <summary>
    /// One processing rule set. A strategy only decides *when* a conversion happens and *what*
    /// progress to report; moving material is RecipeManager's job and reporting occupancy is
    /// InventoryManager's job, so the "no duplicate consumption / never lose material" guarantees
    /// live in exactly one place.
    ///
    /// Strategies hold no timers of their own: WorkManager drives them with a delta time, so no
    /// cooker runs an independent timer.
    /// </summary>
    public abstract class CookerWorkStrategy : ScriptableObject
    {
        /// <summary>Which configured work type this strategy implements.</summary>
        public abstract WorkType WorkType { get; }

        /// <summary>Human readable name for logs and the cooker panel.</summary>
        public abstract string DisplayName { get; }

        public abstract void Tick(CookerRuntime cooker, float deltaTime);

        /// <summary>Called when the cooker stops being activated, e.g. the staff member walked away.</summary>
        public virtual void OnDeactivated(CookerRuntime cooker)
        {
        }

        /// <summary>Called when the cooker definition changes. Drops any locked recipe and stock.</summary>
        public virtual void Reset(CookerRuntime cooker)
        {
            ClearLock(cooker);
            cooker.StatusText = "空闲";
            cooker.State = CookerState.Idle;
        }

        /// <summary>
        /// Releases the reserved stock and forgets the locked recipe. Used only when a conversion
        /// must be abandoned; a pause keeps the lock, which is how partial progress survives.
        /// </summary>
        protected static void ClearLock(CookerRuntime cooker)
        {
            if (cooker == null)
            {
                return;
            }

            InventoryManager inventory = cooker.Inventory;
            if (inventory != null && cooker.Slots.Count > 0)
            {
                inventory.Release(cooker.Slots);
            }

            cooker.ForgetLock();
        }

        /// <summary>Stacks of every reserved input, in the order they were reserved.</summary>
        protected static List<ItemStack> SlotsToStacks(CookerRuntime cooker)
        {
            List<ItemStack> stacks = new List<ItemStack>(cooker.Slots.Count);
            for (int i = 0; i < cooker.Slots.Count; i++)
            {
                stacks.Add(cooker.Slots[i]);
            }

            return stacks;
        }
    }

    /// <summary>
    /// Shared implementation for cookers that repeatedly turn one input set into one output set.
    /// Each unit is atomic: input is reserved before the timer starts, and the outputs are produced
    /// before the inputs are consumed.
    /// </summary>
    public abstract class RepeatingConversionStrategy : CookerWorkStrategy
    {
        /// <summary>Attempts to set aside the inputs of one conversion. Returns false when unavailable.</summary>
        protected abstract bool TryBeginUnit(CookerRuntime cooker);

        /// <summary>Seconds required for one conversion.</summary>
        protected virtual float UnitDuration(CookerRuntime cooker)
        {
            return cooker.Definition != null ? cooker.Definition.Duration : 1f;
        }

        protected abstract string WorkingText(CookerRuntime cooker);

        protected abstract string WaitingText(CookerRuntime cooker);

        public override void Tick(CookerRuntime cooker, float deltaTime)
        {
            if (cooker == null || cooker.Definition == null)
            {
                return;
            }

            RecipeManager recipes = cooker.Recipes;
            InventoryManager inventory = cooker.Inventory;
            if (recipes == null || inventory == null)
            {
                return;
            }

            if (cooker.LockedRecipe == null)
            {
                cooker.ResetProgress();

                if (!TryBeginUnit(cooker) || cooker.LockedRecipe == null)
                {
                    cooker.State = cooker.IsActive ? CookerState.WaitingForInput : CookerState.Idle;
                    cooker.StatusText = WaitingText(cooker);
                    return;
                }
            }

            float duration = Mathf.Max(0.01f, UnitDuration(cooker));
            cooker.WorkSeconds += deltaTime;
            cooker.WorkProgress = Mathf.Clamp01(cooker.WorkSeconds / duration);
            cooker.State = CookerState.Working;
            cooker.StatusText = WorkingText(cooker);

            if (cooker.WorkSeconds < duration)
            {
                return;
            }

            RecipeDefinition locked = cooker.LockedRecipe;
            if (locked == null)
            {
                cooker.ResetProgress();
                return;
            }

            // Producing is RecipeManager's job, so the finished units exist exactly once. This call
            // only decides where they are shown to the player.
            if (!recipes.TryConvert(cooker, locked, true, out List<ItemStack> produced))
            {
                ClearLock(cooker);
                cooker.State = CookerState.Idle;
                cooker.StatusText = "加工失败";
                return;
            }

            ProduceOutput(cooker, produced);

            cooker.ForgetLock();
            cooker.ResetProgress();

            // Look for the next unit straight away so a supported cooker keeps working.
            if (!TryBeginUnit(cooker) || cooker.LockedRecipe == null)
            {
                cooker.State = cooker.HasAnyOutput ? CookerState.ReadyToCollect : cooker.State;
                cooker.StatusText = cooker.HasAnyOutput
                    ? "已完成，请收取产出"
                    : WaitingText(cooker);
            }
        }

        /// <summary>
        /// Decides where a finished unit is shown. By default it waits in the cooker's output slot for
        /// the player to collect it. A pass through appliance such as the dish washer overrides this,
        /// because holding the finished item would stop it from processing the next one.
        ///
        /// The item itself was already created by <see cref="RecipeManager.TryConvert"/>; this method
        /// must never add it to the inventory a second time.
        /// </summary>
        protected virtual void ProduceOutput(CookerRuntime cooker, IReadOnlyList<ItemStack> produced)
        {
            if (produced == null)
            {
                return;
            }

            for (int i = 0; i < produced.Count; i++)
            {
                if (produced[i].IsValid)
                {
                    cooker.AddOutput(produced[i].Item, produced[i].Count);
                }
            }
        }

        /// <summary>Snapshot of the reserved inputs as plain stacks.</summary>
        protected static List<ItemStack> SlotsToStacks(CookerRuntime cooker)
        {
            List<ItemStack> stacks = new List<ItemStack>(cooker.Slots.Count);
            for (int i = 0; i < cooker.Slots.Count; i++)
            {
                stacks.Add(cooker.Slots[i]);
            }

            return stacks;
        }
    }
}
