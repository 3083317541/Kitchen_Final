using Kitchen.Core;
using Kitchen.Data;
using UnityEngine;

namespace Kitchen.Cookers
{
    /// <summary>
    /// Frying pan: one conversion per <c>duration</c> seconds. The input is set aside the moment
    /// work starts, so it cannot be used by another cooker in the meantime, and the pan keeps its
    /// produce until the player collects it.
    /// </summary>
    [CreateAssetMenu(menuName = "Kitchen/Work Strategy/Timed", fileName = "Work_Timed")]
    public class TimedWorkStrategy : RepeatingConversionStrategy
    {
        public override WorkType WorkType => WorkType.Timed;

        public override string DisplayName => "定时加工";

        protected override bool TryBeginUnit(CookerRuntime cooker)
        {
            return CookerWorkHelpers.TryLockSingleRecipe(cooker);
        }

        protected override string WorkingText(CookerRuntime cooker)
        {
            float duration = UnitDuration(cooker);
            return $"煎制中 {cooker.WorkProgress * 100f:0}%（共 {duration:0.#} 秒）";
        }

        protected override string WaitingText(CookerRuntime cooker)
        {
            return cooker.IsActive ? "等待材料" : "空闲（需要员工）";
        }
    }
}
