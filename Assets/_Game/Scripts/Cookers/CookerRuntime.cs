using System.Collections.Generic;
using Kitchen.Core;
using Kitchen.Data;
using Kitchen.Inventory;
using Kitchen.Recipes;
using Kitchen.Runtime;
using UnityEngine;

namespace Kitchen.Cookers
{
    /// <summary>
    /// Runtime state of one placed cooker: which cell it occupies, whether staff are activating it,
    /// which recipe inputs it holds, how far the current conversion has progressed and what it has
    /// produced. Progress lives here so a cooker that loses its staff pauses and later resumes.
    /// </summary>
    public class CookerRuntime
    {
        private readonly List<ItemStack> slots = new List<ItemStack>();
        private readonly List<ItemStack> outputs = new List<ItemStack>();

        public CookerRuntime(int cookerId, CookerDefinition definition, Vector2Int gridPosition)
        {
            Id = cookerId;
            Definition = definition;
            GridPosition = gridPosition;
        }

        /// <summary>
        /// The runtime that owns this cooker. Assigned by CookerManager at creation time so a cooker
        /// never has to search the scene for its dependencies.
        /// </summary>
        public GameRuntime Owner { get; internal set; }

        /// <summary>Shared stock. Every material movement goes through it.</summary>
        public InventoryManager Inventory => Owner != null ? Owner.Inventory : null;

        /// <summary>Shared recipe catalogue.</summary>
        public RecipeManager Recipes => Owner != null ? Owner.Recipes : null;

        /// <summary>Stable runtime id. Never zero for a valid cooker.</summary>
        public int Id { get; private set; }

        /// <summary>Configuration this cooker was created from.</summary>
        public CookerDefinition Definition { get; private set; }

        public Vector2Int GridPosition { get; internal set; }

        /// <summary>Grid cell centre in world space.</summary>
        public Vector3 WorldPosition { get; internal set; }

        /// <summary>True when the cooker exists in the world and occupies a grid cell.</summary>
        public bool IsPlaced { get; internal set; }

        /// <summary>True while a staff member stands on the cooker's cell.</summary>
        public bool IsActive { get; internal set; }

        /// <summary>Staff id currently activating the cooker, or -1.</summary>
        public int ActiveStaffId { get; internal set; } = -1;

        /// <summary>Recipe currently locked in for conversion, or null.</summary>
        public RecipeDefinition LockedRecipe { get; internal set; }

        /// <summary>Discrete progress steps already completed (chop ticks).</summary>
        public int WorkTicks { get; internal set; }

        /// <summary>Seconds accumulated towards the current duration based conversion.</summary>
        public float WorkSeconds { get; internal set; }

        /// <summary>Normalised progress of the current conversion, in the range 0..1.</summary>
        public float WorkProgress { get; internal set; }

        /// <summary>State reported to the UI.</summary>
        public CookerState State { get; set; } = CookerState.Idle;

        /// <summary>Short explanation of what the cooker is doing, shown in the cooker panel.</summary>
        public string StatusText { get; internal set; } = "Idle";

        /// <summary>Inputs currently set aside for this cooker.</summary>
        public IReadOnlyList<ItemStack> Slots => slots;

        /// <summary>Finished produce waiting to be collected by the player.</summary>
        public IReadOnlyList<ItemStack> Outputs => outputs;

        /// <summary>Definition id, handy for logs and tests.</summary>
        public string DefinitionId => Definition != null ? Definition.Id : "<none>";

        /// <summary>Presentation object in the scene, or null when no view was created.</summary>
        public View.CookerView View { get; private set; }

        /// <summary>Called by CookerManager right after the view is instantiated.</summary>
        public void SetView(View.CookerView view)
        {
            View = view;
        }

        // ------------------------------------------------------------------ reserved input

        public int GetReservedCount(ItemDefinition item)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].Item == item)
                {
                    return slots[i].Count;
                }
            }

            return 0;
        }

        public void AddReserved(ItemDefinition item, int count)
        {
            if (item == null || count <= 0)
            {
                return;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].Item == item)
                {
                    slots[i] = slots[i].WithCount(slots[i].Count + count);
                    return;
                }
            }

            slots.Add(new ItemStack(item, count));
        }

        /// <summary>Empties the reserved slots. The caller is responsible for releasing stock.</summary>
        public void ClearSlots()
        {
            slots.Clear();
        }

        /// <summary>
        /// Drops the locked recipe and the reserved slots without touching the inventory. Only used
        /// right after a successful conversion, where RecipeManager has already consumed the
        /// reserved units.
        /// </summary>
        public void ForgetLock()
        {
            slots.Clear();
            LockedRecipe = null;
        }

        // ------------------------------------------------------------------ produced output

        public int GetOutputCount(ItemDefinition item)
        {
            for (int i = 0; i < outputs.Count; i++)
            {
                if (outputs[i].Item == item)
                {
                    return outputs[i].Count;
                }
            }

            return 0;
        }

        public void AddOutput(ItemDefinition item, int count)
        {
            if (item == null || count <= 0)
            {
                return;
            }

            for (int i = 0; i < outputs.Count; i++)
            {
                if (outputs[i].Item == item)
                {
                    outputs[i] = outputs[i].WithCount(outputs[i].Count + count);
                    return;
                }
            }

            outputs.Add(new ItemStack(item, count));
        }

        public void ClearOutputs()
        {
            outputs.Clear();
        }

        /// <summary>
        /// Moves finished produce from the cooker's output slot into the shared inventory.
        /// Returns false when there is nothing to move.
        /// </summary>
        public bool CollectAllOutput()
        {
            InventoryManager inventory = Inventory;
            if (inventory == null || outputs.Count == 0)
            {
                return false;
            }

            List<ItemStack> batch = new List<ItemStack>(outputs.Count);
            for (int i = 0; i < outputs.Count; i++)
            {
                if (outputs[i].IsValid)
                {
                    batch.Add(outputs[i]);
                }
            }

            if (batch.Count == 0)
            {
                outputs.Clear();
                return false;
            }

            inventory.Add(batch);
            outputs.Clear();
            return true;
        }

        public bool HasAnyOutput
        {
            get
            {
                for (int i = 0; i < outputs.Count; i++)
                {
                    if (outputs[i].Count > 0)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>Total number of finished units sitting in the output slot.</summary>
        public int TotalOutputCount
        {
            get
            {
                int total = 0;
                for (int i = 0; i < outputs.Count; i++)
                {
                    total += outputs[i].Count;
                }

                return total;
            }
        }

        // ------------------------------------------------------------------ progress helpers

        public void ResetProgress()
        {
            WorkTicks = 0;
            WorkSeconds = 0f;
            WorkProgress = 0f;
        }

        /// <summary>Applies a grid position change and refreshes the cached world position.</summary>
        public void SetPlacement(Vector2Int gridPosition, Vector3 worldPosition, bool placed)
        {
            GridPosition = gridPosition;
            WorldPosition = worldPosition;
            IsPlaced = placed;
        }

        /// <summary>Replaces the configuration. Only used while authoring a prefab in the editor.</summary>
        public void SetDefinition(CookerDefinition definition)
        {
            Definition = definition;
        }

        /// <summary>Replaces the runtime id. Only used while authoring a prefab in the editor.</summary>
        public void SetId(int id)
        {
            Id = id;
        }

        public override string ToString()
        {
            return $"Cooker#{Id}({DefinitionId}) @{GridPosition} {State} progress={WorkProgress:0.00}";
        }
    }
}
