using System.Collections.Generic;
using Kitchen.Core;
using Kitchen.Data;
using Kitchen.Cookers;
using Kitchen.Inventory;
using Kitchen.Runtime;
using UnityEngine;

namespace Kitchen.Recipes
{
    /// <summary>
    /// Owns recipe matching and the input lock. It decides whether a cooker may run a recipe,
    /// reserves the inputs so two cookers can never consume the same unit, and performs the final
    /// conversion. Processing strategies ask this system instead of duplicating the rules.
    /// </summary>
    [DisallowMultipleComponent]
    public class RecipeManager : GameSystem
    {
        private readonly List<RecipeDefinition> recipes = new List<RecipeDefinition>();

        public IReadOnlyList<RecipeDefinition> Recipes => recipes;

        /// <summary>Replaces the recipe catalogue with the one described by the level.</summary>
        public void ApplyLevel(LevelConfig level)
        {
            recipes.Clear();
            if (level == null)
            {
                return;
            }

            IReadOnlyList<RecipeDefinition> configured = level.Recipes;
            for (int i = 0; i < configured.Count; i++)
            {
                RecipeDefinition recipe = configured[i];
                if (recipe == null)
                {
                    continue;
                }

                if (!recipe.HasValidContent())
                {
                    Debug.LogWarning($"[RecipeManager] Recipe '{recipe.name}' has no valid inputs or outputs and was skipped.");
                    continue;
                }

                recipes.Add(recipe);
            }

            recipes.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        /// <summary>True when at least one recipe belongs to this cooker kind.</summary>
        public bool HasRecipeFor(CookerKind kind)
        {
            for (int i = 0; i < recipes.Count; i++)
            {
                if (recipes[i].CookerKind == kind)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Finds the highest priority recipe of <paramref name="kind"/> whose inputs are entirely
        /// covered by the unreserved inventory.
        /// </summary>
        public RecipeDefinition FindUsableRecipe(CookerKind kind)
        {
            InventoryManager inventory = Runtime.Inventory;
            for (int i = 0; i < recipes.Count; i++)
            {
                RecipeDefinition recipe = recipes[i];
                if (recipe.CookerKind != kind)
                {
                    continue;
                }

                if (HasIngredients(recipe))
                {
                    return recipe;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds a recipe that can be started by adding ingredients on top of what is already
        /// reserved for this cooker. Used by the assembly table, which fills its slots over time.
        /// </summary>
        public bool TryFindRecipeVariant(CookerRuntime cooker, out RecipeDefinition recipe, out List<ItemStack> required)
        {
            recipe = null;
            required = null;

            if (cooker == null || cooker.Definition == null)
            {
                return false;
            }

            InventoryManager inventory = Runtime.Inventory;
            for (int i = 0; i < recipes.Count; i++)
            {
                RecipeDefinition candidate = recipes[i];
                if (candidate.CookerKind != cooker.Definition.Kind)
                {
                    continue;
                }

                List<ItemStack> needed = new List<ItemStack>();
                bool possible = true;

                IReadOnlyList<RecipeInput> inputs = candidate.Inputs;
                for (int j = 0; j < inputs.Count; j++)
                {
                    ItemStack stack = inputs[j].ToStack();
                    int already = cooker.GetReservedCount(stack.Item);
                    int extra = stack.Count - already;
                    if (extra < 0)
                    {
                        // The slots hold more of this item than the candidate needs.
                        possible = false;
                        break;
                    }

                    if (extra == 0)
                    {
                        continue;
                    }

                    if (!inventory.Has(stack.Item, extra))
                    {
                        possible = false;
                        break;
                    }

                    needed.Add(new ItemStack(stack.Item, extra));
                }

                if (!possible)
                {
                    continue;
                }

                recipe = candidate;
                required = needed;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Reserves the additional inputs of a recipe variant. Can only fail if the stock changed
        /// between matching and reserving, in which case nothing is reserved and nothing is lost.
        /// </summary>
        public bool TryReserveAdditional(IReadOnlyList<ItemStack> additional)
        {
            if (additional == null || additional.Count == 0)
            {
                return true;
            }

            InventoryManager inventory = Runtime.Inventory;
            for (int i = 0; i < additional.Count; i++)
            {
                if (!inventory.Has(additional[i].Item, additional[i].Count))
                {
                    return false;
                }
            }

            for (int i = 0; i < additional.Count; i++)
            {
                inventory.TryReserve(additional[i].Item, additional[i].Count);
            }

            return true;
        }

        /// <summary>True when the whole input set of a recipe is currently unreserved.</summary>
        public bool HasIngredients(RecipeDefinition recipe)
        {
            if (recipe == null)
            {
                return false;
            }

            InventoryManager inventory = Runtime.Inventory;
            IReadOnlyList<RecipeInput> inputs = recipe.Inputs;
            for (int i = 0; i < inputs.Count; i++)
            {
                if (!inventory.Has(inputs[i].Item, inputs[i].Count))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Performs one conversion and reports the produced stacks.
        ///
        /// The outputs are produced first and the inputs are consumed afterwards, so a failed
        /// conversion can never lose material. Reserved units are spent directly, because a cooker
        /// that reserved its input has already removed it from the free stock.
        ///
        /// This method is the only place that creates finished goods; deciding where a finished unit
        /// is displayed is the caller's job.
        /// </summary>
        public bool TryConvert(
            CookerRuntime cooker,
            RecipeDefinition recipe,
            bool consumeReserved,
            out List<ItemStack> producedStacks)
        {
            producedStacks = null;

            if (cooker == null || recipe == null)
            {
                return false;
            }

            InventoryManager inventory = Runtime.Inventory;
            if (inventory == null)
            {
                return false;
            }

            // A reserved input that is no longer held means the cooker's slot bookkeeping is stale.
            // Refusing here keeps the "never lose material, never duplicate output" guarantee.
            if (consumeReserved && !CanConsumeReserved(recipe))
            {
                Debug.LogWarning($"[RecipeManager] Recipe '{recipe.name}' cannot be converted: its reserved inputs are no longer held.");
                return false;
            }

            List<ItemStack> produced = new List<ItemStack>(recipe.Outputs.Count);
            IReadOnlyList<ItemStack> outputs = recipe.Outputs;
            for (int i = 0; i < outputs.Count; i++)
            {
                if (!outputs[i].IsValid)
                {
                    continue;
                }

                inventory.Add(outputs[i].Item, outputs[i].Count);
                produced.Add(outputs[i]);
            }

            if (produced.Count == 0)
            {
                Debug.LogWarning($"[RecipeManager] Recipe '{recipe.name}' produced nothing; conversion aborted.");
                return false;
            }

            IReadOnlyList<RecipeInput> inputs = recipe.Inputs;
            for (int i = 0; i < inputs.Count; i++)
            {
                if (consumeReserved)
                {
                    // The cooker set these units aside before it started, so they are spent directly.
                    inventory.TryConsumeReserved(inputs[i].Item, inputs[i].Count);
                }
                else
                {
                    inventory.TryConsume(inputs[i].Item, inputs[i].Count);
                }
            }

            producedStacks = produced;
            return true;
        }

        /// <summary>Convenience overload for callers that do not need the produced stacks.</summary>
        public bool TryConvert(CookerRuntime cooker, RecipeDefinition recipe, bool consumeReserved)
        {
            return TryConvert(cooker, recipe, consumeReserved, out _);
        }

        /// <summary>
        /// True when every reserved unit a recipe needs is actually held by a cooker. Reserved stock
        /// is deliberately excluded from the free counts, so this is the only valid pre-flight check
        /// for a conversion that is already in flight.
        /// </summary>
        public bool CanConsumeReserved(RecipeDefinition recipe)
        {
            if (recipe == null)
            {
                return false;
            }

            InventoryManager inventory = Runtime.Inventory;
            if (inventory == null)
            {
                return false;
            }

            IReadOnlyList<RecipeInput> inputs = recipe.Inputs;
            for (int i = 0; i < inputs.Count; i++)
            {
                if (inventory.ReservedCount(inputs[i].Item) < inputs[i].Count)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// True when the cooker's reserved slots exactly match the recipe inputs.
        /// Exact matching is what stops an order from being fulfilled by the wrong dish.
        /// </summary>
        public bool MatchesReservedExactly(CookerRuntime cooker, RecipeDefinition recipe)
        {
            if (cooker == null || recipe == null)
            {
                return false;
            }

            IReadOnlyList<RecipeInput> inputs = recipe.Inputs;
            for (int i = 0; i < inputs.Count; i++)
            {
                if (cooker.GetReservedCount(inputs[i].Item) != inputs[i].Count)
                {
                    return false;
                }
            }

            // No item kind outside the recipe may sit in the slots.
            IReadOnlyList<ItemStack> slots = cooker.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                bool known = false;
                for (int j = 0; j < inputs.Count; j++)
                {
                    if (inputs[j].Item == slots[i].Item)
                    {
                        known = true;
                        break;
                    }
                }

                if (!known)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
