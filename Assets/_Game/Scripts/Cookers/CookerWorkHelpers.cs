using System.Collections.Generic;
using Kitchen.Core;
using Kitchen.Data;
using Kitchen.Inventory;
using Kitchen.Recipes;

namespace Kitchen.Cookers
{
    /// <summary>
    /// Shared input locking used by the ready-made strategies. Keeping it in one place means every
    /// cooker reserves stock through <see cref="InventoryManager"/> and none of them can consume the
    /// same unit twice.
    /// </summary>
    public static class CookerWorkHelpers
    {
        /// <summary>
        /// Locks the highest priority usable recipe for a single item set, such as one raw meat
        /// slice on a frying pan. Returns false when nothing is available or the output slot is full.
        /// </summary>
        public static bool TryLockSingleRecipe(CookerRuntime cooker)
        {
            if (cooker == null || cooker.Definition == null)
            {
                return false;
            }

            if (cooker.HasAnyOutput)
            {
                return false;
            }

            RecipeManager recipes = cooker.Recipes;
            InventoryManager inventory = cooker.Inventory;
            if (recipes == null || inventory == null)
            {
                return false;
            }

            RecipeDefinition usable = recipes.FindUsableRecipe(cooker.Definition.Kind);
            if (usable == null)
            {
                return false;
            }

            List<ItemStack> required = new List<ItemStack>();
            IReadOnlyList<RecipeInput> inputs = usable.Inputs;
            for (int i = 0; i < inputs.Count; i++)
            {
                required.Add(inputs[i].ToStack());
            }

            return TryLock(cooker, usable, required);
        }

        /// <summary>
        /// Locks the best recipe the cooker can currently satisfy, taking already reserved
        /// ingredients into account. Used by the assembly table, which accepts ingredients one at a
        /// time until a full recipe set is present.
        /// </summary>
        public static bool TryLockAssemblyRecipe(CookerRuntime cooker)
        {
            if (cooker == null || cooker.Definition == null)
            {
                return false;
            }

            if (cooker.HasAnyOutput)
            {
                return false;
            }

            RecipeManager recipes = cooker.Recipes;
            if (recipes == null)
            {
                return false;
            }

            if (!recipes.TryFindRecipeVariant(cooker, out RecipeDefinition recipe, out List<ItemStack> required))
            {
                return false;
            }

            if (required.Count == 0)
            {
                // The slots already match a recipe exactly; start the conversion with what is there.
                if (!recipes.MatchesReservedExactly(cooker, recipe))
                {
                    return false;
                }

                cooker.LockedRecipe = recipe;
                cooker.WorkSeconds = 0f;
                cooker.WorkProgress = 0f;
                return true;
            }

            return TryLock(cooker, recipe, required);
        }

        private static bool TryLock(CookerRuntime cooker, RecipeDefinition recipe, IReadOnlyList<ItemStack> required)
        {
            if (recipe == null || required == null)
            {
                return false;
            }

            InventoryManager inventory = cooker.Inventory;
            if (inventory == null)
            {
                return false;
            }

            // Reserve everything first so a partial failure leaves no half held material behind.
            if (!inventory.TryReserve(required))
            {
                return false;
            }

            for (int i = 0; i < required.Count; i++)
            {
                cooker.AddReserved(required[i].Item, required[i].Count);
            }

            cooker.LockedRecipe = recipe;
            cooker.WorkSeconds = 0f;
            cooker.WorkProgress = 0f;
            return true;
        }
    }
}
