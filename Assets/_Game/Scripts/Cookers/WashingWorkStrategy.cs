using System.Collections.Generic;
using Kitchen.Core;
using Kitchen.Data;
using Kitchen.Inventory;
using UnityEngine;

namespace Kitchen.Cookers
{
    /// <summary>
    /// Dish washer: cleans one dirty plate per <c>duration</c> seconds and yields a clean plate.
    /// It stops on its own when there is no staff on the cell or no dirty plate in stock.
    /// </summary>
    [CreateAssetMenu(menuName = "Kitchen/Work Strategy/Washing", fileName = "Work_Washing")]
    public class WashingWorkStrategy : RepeatingConversionStrategy
    {
        public override WorkType WorkType => WorkType.Washing;

        public override string DisplayName => "清洗";

        protected override bool TryBeginUnit(CookerRuntime cooker)
        {
            return CookerWorkHelpers.TryLockSingleRecipe(cooker);
        }

        protected override string WorkingText(CookerRuntime cooker)
        {
            float duration = UnitDuration(cooker);
            return $"清洗中 {cooker.WorkProgress * 100f:0}%（共 {duration:0.#} 秒）";
        }

        /// <summary>
        /// A clean plate needs no second step: the conversion already put it into the kitchen stock.
        /// Keeping it in the washer's own output slot would stop the washer from cleaning the next
        /// dirty plate, which is the whole point of the station.
        /// </summary>
        protected override void ProduceOutput(CookerRuntime cooker, IReadOnlyList<ItemStack> produced)
        {
            // Intentionally empty: the plate is already in the shared inventory.
        }

        protected override string WaitingText(CookerRuntime cooker)
        {
            if (!cooker.IsActive)
            {
                return "空闲（需要员工）";
            }

            return cooker.HasAnyOutput ? "干净盘子待收取" : "等待脏盘子";
        }
    }
}
