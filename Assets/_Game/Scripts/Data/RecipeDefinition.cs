using System.Collections.Generic;
using Kitchen.Core;
using UnityEngine;

namespace Kitchen.Data
{
    /// <summary>
    /// A single input item of a recipe together with its exact required quantity.
    /// Quantities must match exactly when the recipe is evaluated.
    /// </summary>
    [System.Serializable]
    public struct RecipeInput
    {
        [SerializeField] private ItemDefinition item;
        [SerializeField, Min(1)] private int count;

        public RecipeInput(ItemDefinition item, int count)
        {
            this.item = item;
            this.count = Mathf.Max(1, count);
        }

        public ItemDefinition Item => item;

        public int Count => Mathf.Max(0, count);

        public bool IsValid => item != null && count > 0;

        public ItemStack ToStack()
        {
            return new ItemStack(item, Count);
        }
    }

    /// <summary>
    /// Data driven conversion rule: exact inputs -> exact outputs, restricted to a cooker kind.
    /// Recipes contain no runtime state; RecipeManager owns matching and locking.
    /// </summary>
    [CreateAssetMenu(menuName = "Kitchen/Recipe Definition", fileName = "Recipe_New")]
    public class RecipeDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id = "recipe_new";
        [SerializeField] private string displayName = "New Recipe";

        [Header("Restriction")]
        [Tooltip("Only a cooker of this kind may run the recipe.")]
        [SerializeField] private CookerKind cookerKind = CookerKind.ChoppingStation;

        [Header("Conversion")]
        [SerializeField] private List<RecipeInput> inputs = new List<RecipeInput>();
        [SerializeField] private List<ItemStack> outputs = new List<ItemStack>();

        [Tooltip("Lower values are evaluated first when several recipes match the same cooker.")]
        [SerializeField] private int priority = 0;

        public string Id => string.IsNullOrEmpty(id) ? name : id;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

        public CookerKind CookerKind => cookerKind;

        public IReadOnlyList<RecipeInput> Inputs => inputs;

        public IReadOnlyList<ItemStack> Outputs => outputs;

        public int Priority => priority;

        public static RecipeDefinition Create(
            string recipeId,
            string recipeDisplayName,
            CookerKind kind,
            IList<RecipeInput> recipeInputs,
            IList<ItemStack> recipeOutputs,
            int recipePriority = 0)
        {
            RecipeDefinition recipe = CreateInstance<RecipeDefinition>();
            recipe.name = recipeId;
            recipe.id = recipeId;
            recipe.displayName = recipeDisplayName;
            recipe.cookerKind = kind;
            recipe.inputs = new List<RecipeInput>(recipeInputs);
            recipe.outputs = new List<ItemStack>(recipeOutputs);
            recipe.priority = recipePriority;
            return recipe;
        }

        public bool HasValidContent()
        {
            if (inputs == null || inputs.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < inputs.Count; i++)
            {
                if (!inputs[i].IsValid)
                {
                    return false;
                }
            }

            if (outputs == null || outputs.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < outputs.Count; i++)
            {
                if (!outputs[i].IsValid)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
