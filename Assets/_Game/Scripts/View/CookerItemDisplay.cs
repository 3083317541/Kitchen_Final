using Kitchen.Core;
using Kitchen.Data;
using Kitchen.Cookers;
using Kitchen.Recipes;
using UnityEngine;

namespace Kitchen.View
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CookerView))]
    public sealed class CookerItemDisplay : MonoBehaviour
    {
        [SerializeField] private Transform inputAnchor;
        [SerializeField] private Transform outputAnchor;
        [SerializeField] private float completionDuration = 0.9f;

        private CookerView cookerView;
        private ItemDefinition inputItem;
        private ItemDefinition outputItem;
        private GameObject inputVisual;
        private GameObject outputVisual;
        private RecipeManager recipes;
        private ItemDefinition completedItem;
        private float completionUntil;

        public ItemDefinition DisplayedInput => inputItem;
        public ItemDefinition DisplayedOutput => outputItem;

        public bool IsDisplayTransform(Transform candidate)
        {
            return (inputAnchor != null && candidate != inputAnchor && candidate.IsChildOf(inputAnchor)) ||
                (outputAnchor != null && candidate != outputAnchor && candidate.IsChildOf(outputAnchor));
        }

        public void Refresh()
        {
            if (cookerView == null)
            {
                cookerView = GetComponent<CookerView>();
            }

            var cooker = cookerView.Runtime;
            if (cooker == null)
            {
                return;
            }

            if (recipes != cooker.Recipes)
            {
                if (recipes != null)
                {
                    recipes.Converted -= OnConverted;
                }
                recipes = cooker.Recipes;
                if (recipes != null)
                {
                    recipes.Converted += OnConverted;
                }
            }

            ItemDefinition nextInput = null;
            foreach (ItemStack stack in cooker.Slots)
            {
                if (stack.IsValid)
                {
                    nextInput = stack.Item;
                    break;
                }
            }

            ItemDefinition nextOutput = null;
            foreach (ItemStack stack in cooker.Outputs)
            {
                if (stack.IsValid)
                {
                    nextOutput = stack.Item;
                    break;
                }
            }

            if (nextOutput == null && Time.unscaledTime < completionUntil)
            {
                nextOutput = completedItem;
            }

            ReplaceVisual(ref inputItem, ref inputVisual, nextInput, inputAnchor, "InputItem");
            ReplaceVisual(ref outputItem, ref outputVisual, nextOutput, outputAnchor, "OutputItem");
        }

        private void OnConverted(CookerRuntime cooker, RecipeDefinition recipe)
        {
            if (cookerView != null && cooker == cookerView.Runtime &&
                cooker.Definition.Kind == CookerKind.ChoppingStation && recipe.Outputs.Count > 0)
            {
                completedItem = recipe.Outputs[0].Item;
                completionUntil = Time.unscaledTime + completionDuration;
            }
        }

        private void LateUpdate()
        {
            if (completedItem != null && Time.unscaledTime >= completionUntil)
            {
                completedItem = null;
                Refresh();
            }
        }

        private void OnDestroy()
        {
            if (recipes != null)
            {
                recipes.Converted -= OnConverted;
            }
        }

        private void ReplaceVisual(ref ItemDefinition current, ref GameObject visual, ItemDefinition next, Transform anchor, string objectName)
        {
            if (current == next)
            {
                return;
            }

            if (visual != null)
            {
                visual.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(visual);
                }
                else
                {
                    DestroyImmediate(visual);
                }
            }

            current = next;
            visual = next != null && anchor != null
                ? ViewFactory.CreateItemToken(next, anchor, objectName)
                : null;
        }
    }
}
