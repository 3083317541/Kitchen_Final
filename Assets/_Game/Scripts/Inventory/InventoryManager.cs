using System.Collections.Generic;
using Kitchen.Core;
using Kitchen.Data;
using Kitchen.Runtime;
using UnityEngine;

namespace Kitchen.Inventory
{
    /// <summary>
    /// The only place item counts may change. Raw materials, processed materials, dishes, clean
    /// plates and dirty plates all live here, and every stock mutation goes through this API.
    /// Items can be reserved by a cooker while it works so two cookers never consume the same unit.
    /// </summary>
    [DisallowMultipleComponent]
    public class InventoryManager : GameSystem
    {
        private readonly Dictionary<ItemDefinition, int> totals = new Dictionary<ItemDefinition, int>();
        private readonly Dictionary<ItemDefinition, int> reservations = new Dictionary<ItemDefinition, int>();
        private readonly List<ItemDefinition> knownItems = new List<ItemDefinition>();
        private bool unlimitedRaw;

        public bool IsUnlimited(ItemDefinition item) => unlimitedRaw && item != null && item.Category == ItemCategory.Raw;

        public IReadOnlyList<ItemDefinition> KnownItems => knownItems;

        /// <summary>Seeds the inventory from the level configuration.</summary>
        public void ApplyStartingInventory(LevelConfig level)
        {
            unlimitedRaw = level != null && level.IsClassicMode;
            totals.Clear();
            reservations.Clear();
            knownItems.Clear();

            if (level == null)
            {
                Debug.LogWarning("[InventoryManager] ApplyStartingInventory received a null LevelConfig.");
                return;
            }

            IReadOnlyList<ItemStack> stock = level.StartingInventory;
            for (int i = 0; i < stock.Count; i++)
            {
                ItemStack stack = stock[i];
                if (stack.IsValid)
                {
                    Add(stack.Item, stack.Count);
                }
            }

            // Every configured item is addressable from the UI even when the count is zero.
            IReadOnlyList<ItemDefinition> all = level.Items;
            for (int i = 0; i < all.Count; i++)
            {
                EnsureKnown(all[i]);
            }
        }

        /// <summary>Registers an item so it appears in the UI even at zero stock.</summary>
        public void EnsureKnown(ItemDefinition item)
        {
            if (item != null && !knownItems.Contains(item))
            {
                knownItems.Add(item);
            }
        }

        // ------------------------------------------------------------------ queries

        /// <summary>Total units held, ignoring reservations.</summary>
        public int TotalCount(ItemDefinition item)
        {
            if (IsUnlimited(item)) return int.MaxValue;
            if (item == null)
            {
                return 0;
            }

            return totals.TryGetValue(item, out int count) ? count : 0;
        }

        /// <summary>Units that are not reserved by a cooker and may therefore be moved elsewhere.</summary>
        public int AvailableCount(ItemDefinition item)
        {
            if (IsUnlimited(item)) return int.MaxValue;
            if (item == null)
            {
                return 0;
            }

            int total = TotalCount(item);
            int reserved = reservations.TryGetValue(item, out int value) ? value : 0;
            return Mathf.Max(0, total - reserved);
        }

        /// <summary>Units currently held by cookers.</summary>
        public int ReservedCount(ItemDefinition item)
        {
            if (item == null)
            {
                return 0;
            }

            return reservations.TryGetValue(item, out int value) ? value : 0;
        }

        public bool Has(ItemDefinition item, int count)
        {
            return count <= 0 || AvailableCount(item) >= count;
        }

        public bool HasAll(IReadOnlyList<ItemStack> stacks)
        {
            if (stacks == null)
            {
                return true;
            }

            for (int i = 0; i < stacks.Count; i++)
            {
                if (!Has(stacks[i].Item, stacks[i].Count))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Human readable snapshot used by the UI and by automated tests.</summary>
        public string Describe()
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int i = 0; i < knownItems.Count; i++)
            {
                ItemDefinition item = knownItems[i];
                if (IsUnlimited(item))
                {
                    builder.Append($"{item.DisplayName} ∞ | ");
                    continue;
                }
                int total = TotalCount(item);
                int reserved = ReservedCount(item);
                if (total == 0 && reserved == 0 && item != null && item.Category == ItemCategory.Raw)
                {
                    // Keep raw materials visible even at zero so the player sees what is missing.
                    builder.Append($"{item.DisplayName} 0 | ");
                    continue;
                }

                if (item == null)
                {
                    continue;
                }

                builder.Append(reserved > 0
                    ? $"{item.DisplayName} {total} ({reserved} in use) | "
                    : $"{item.DisplayName} {total} | ");
            }

            return builder.Length > 0 ? builder.ToString(0, builder.Length - 3) : "-";
        }

        // ------------------------------------------------------------------ mutation

        /// <summary>Adds stock. Negative or zero amounts are ignored.</summary>
        public void Add(ItemDefinition item, int count)
        {
            if (item == null || count <= 0)
            {
                return;
            }

            EnsureKnown(item);
            if (IsUnlimited(item)) return;
            totals[item] = TotalCount(item) + count;
        }

        /// <summary>Adds a batch of stacks.</summary>
        public void Add(IReadOnlyList<ItemStack> stacks)
        {
            if (stacks == null)
            {
                return;
            }

            for (int i = 0; i < stacks.Count; i++)
            {
                Add(stacks[i].Item, stacks[i].Count);
            }
        }

        /// <summary>All-or-nothing removal of unreserved stock.</summary>
        public bool TryConsume(ItemDefinition item, int count)
        {
            if (item == null || count <= 0)
            {
                return false;
            }

            if (AvailableCount(item) < count)
            {
                return false;
            }

            if (!IsUnlimited(item)) totals[item] = TotalCount(item) - count;
            return true;
        }

        /// <summary>All-or-nothing removal of a batch of unreserved stock.</summary>
        public bool TryConsume(IReadOnlyList<ItemStack> stacks)
        {
            if (stacks == null)
            {
                return true;
            }

            if (!HasAll(stacks))
            {
                return false;
            }

            for (int i = 0; i < stacks.Count; i++)
            {
                TryConsume(stacks[i].Item, stacks[i].Count);
            }

            return true;
        }

        /// <summary>
        /// Reserves unreserved stock for a cooker. Reserved units stop counting as available,
        /// which is what stops two cookers from consuming the same unit.
        /// </summary>
        public bool TryReserve(ItemDefinition item, int count)
        {
            if (item == null || count <= 0)
            {
                return false;
            }

            if (AvailableCount(item) < count)
            {
                return false;
            }

            EnsureKnown(item);
            reservations[item] = ReservedCount(item) + count;
            return true;
        }

        /// <summary>Reserves a batch atomically: either every stack is reserved or none is.</summary>
        public bool TryReserve(IReadOnlyList<ItemStack> stacks)
        {
            if (stacks == null || stacks.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < stacks.Count; i++)
            {
                if (AvailableCount(stacks[i].Item) < stacks[i].Count)
                {
                    return false;
                }
            }

            for (int i = 0; i < stacks.Count; i++)
            {
                TryReserve(stacks[i].Item, stacks[i].Count);
            }

            return true;
        }

        /// <summary>Returns reserved units to the available pool without consuming them.</summary>
        public void Release(ItemDefinition item, int count)
        {
            if (item == null || count <= 0)
            {
                return;
            }

            int reserved = ReservedCount(item);
            if (reserved <= 0)
            {
                return;
            }

            reservations[item] = Mathf.Max(0, reserved - count);
        }

        public void Release(IReadOnlyList<ItemStack> stacks)
        {
            if (stacks == null)
            {
                return;
            }

            for (int i = 0; i < stacks.Count; i++)
            {
                Release(stacks[i].Item, stacks[i].Count);
            }
        }

        /// <summary>
        /// Consumes previously reserved units. This never fails for a valid reservation because the
        /// stock was already set aside, which is what keeps a finished conversion from losing material.
        /// </summary>
        public bool TryConsumeReserved(ItemDefinition item, int count)
        {
            if (item == null || count <= 0)
            {
                return false;
            }

            int reserved = ReservedCount(item);
            if (reserved < count)
            {
                return false;
            }

            int total = TotalCount(item);
            if (total < count)
            {
                // Defensive: keep the two counters from drifting apart.
                reservations[item] = Mathf.Max(0, reserved - count);
                return false;
            }

            reservations[item] = reserved - count;
            if (!IsUnlimited(item)) totals[item] = total - count;
            return true;
        }

        public bool TryConsumeReserved(IReadOnlyList<ItemStack> stacks)
        {
            if (stacks == null || stacks.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < stacks.Count; i++)
            {
                if (ReservedCount(stacks[i].Item) < stacks[i].Count)
                {
                    return false;
                }
            }

            for (int i = 0; i < stacks.Count; i++)
            {
                TryConsumeReserved(stacks[i].Item, stacks[i].Count);
            }

            return true;
        }

        /// <summary>Snapshot used by tests and by the UI.</summary>
        public List<ItemStack> Snapshot()
        {
            List<ItemStack> snapshot = new List<ItemStack>(knownItems.Count);
            for (int i = 0; i < knownItems.Count; i++)
            {
                ItemDefinition item = knownItems[i];
                int total = TotalCount(item);
                if (total > 0)
                {
                    snapshot.Add(new ItemStack(item, total));
                }
            }

            return snapshot;
        }
    }
}
