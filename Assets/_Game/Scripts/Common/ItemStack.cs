using System;
using Kitchen.Data;
using UnityEngine;

namespace Kitchen.Core
{
    /// <summary>
    /// A serializable pair of an item definition and a positive quantity.
    /// Used by recipes, cookers, inventory requests and level configuration.
    /// </summary>
    [Serializable]
    public struct ItemStack
    {
        [SerializeField] private ItemDefinition item;
        [SerializeField, Min(0)] private int count;

        public ItemStack(ItemDefinition item, int count)
        {
            this.item = item;
            this.count = Mathf.Max(0, count);
        }

        public ItemDefinition Item => item;

        public int Count => count;

        public bool IsValid => item != null && count > 0;

        public ItemStack WithCount(int newCount)
        {
            return new ItemStack(item, newCount);
        }

        public override string ToString()
        {
            string name = item != null ? item.DisplayName : "<null item>";
            return $"{count} x {name}";
        }
    }
}
