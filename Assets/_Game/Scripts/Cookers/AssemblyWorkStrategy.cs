using Kitchen.Core;
using Kitchen.Data;
using UnityEngine;

namespace Kitchen.Cookers
{
    /// <summary>
    /// Assembly table: one conversion per <c>duration</c> seconds. Unlike the frying pan it fills
    /// its slots over time, so it accepts extra ingredients until the exact input set of a recipe is
    /// present. Because a conversion is atomic, an incomplete set is never consumed.
    /// </summary>
    [CreateAssetMenu(menuName = "Kitchen/Work Strategy/Assembly", fileName = "Work_Assembly")]
    public class AssemblyWorkStrategy : RepeatingConversionStrategy
    {
        public override WorkType WorkType => WorkType.Assembly;

        public override string DisplayName => "组装";

        protected override bool TryBeginUnit(CookerRuntime cooker)
        {
            return CookerWorkHelpers.TryLockAssemblyRecipe(cooker);
        }

        protected override string WorkingText(CookerRuntime cooker)
        {
            float duration = UnitDuration(cooker);
            return $"组装中 {cooker.WorkProgress * 100f:0}%（共 {duration:0.#} 秒）";
        }

        protected override string WaitingText(CookerRuntime cooker)
        {
            if (!cooker.IsActive)
            {
                return "空闲（需要员工）";
            }

            return cooker.Slots.Count > 0 ? "等待剩余材料" : "等待材料";
        }
    }
}
