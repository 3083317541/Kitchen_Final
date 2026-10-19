using Kitchen.Core;
using Kitchen.Data;
using Kitchen.Runtime;
using UnityEngine;

namespace Kitchen.Cookers
{
    /// <summary>
    /// Heating requires attended preparation and collection, with autonomous heating in between.
    /// Completed food remains in the device until collected or burnt.
    /// </summary>
    [CreateAssetMenu(menuName = "Kitchen/Work Strategy/Timed", fileName = "Work_Timed")]
    public class TimedWorkStrategy : CookerWorkStrategy
    {
        public override WorkType WorkType => WorkType.Timed;

        public override string DisplayName => "定时加工";

        public override void Tick(CookerRuntime cooker, float deltaTime)
        {
            if (cooker == null || cooker.Definition == null || cooker.Recipes == null || deltaTime <= 0f)
            {
                return;
            }

            CookerDefinition definition = cooker.Definition;
            if (cooker.State == CookerState.Burnt || cooker.State == CookerState.Cleaning)
            {
                if (Operate(cooker, deltaTime, definition.CleaningDuration, CookerState.Cleaning, "清理焦糊"))
                {
                    cooker.OperationSeconds = 0f;
                    cooker.ResetProgress();
                    cooker.State = CookerState.Idle;
                    cooker.StatusText = "已清理，等待装料";
                    cooker.Owner?.Staff?.ReleaseCooker(cooker.Id);
                }

                return;
            }

            if (cooker.HasAnyOutput)
            {
                cooker.ReadySeconds += deltaTime;
                if (cooker.ReadySeconds >= definition.BurnGraceDuration)
                {
                    cooker.ClearOutputs();
                    cooker.OperationSeconds = 0f;
                    cooker.ResetProgress();
                    cooker.State = CookerState.Burnt;
                    cooker.StatusText = "食物烧焦，需要员工清理";
                    cooker.Owner?.Notify("食物烧焦了，请让员工到正面操作格清理设备。", MessageKind.Warning);
                    return;
                }

                if (Operate(cooker, deltaTime, definition.CollectionDuration, CookerState.Collecting,
                    $"取出成品（距烧焦 {definition.BurnGraceDuration - cooker.ReadySeconds:0.0} 秒）"))
                {
                    cooker.CollectAllOutput();
                    cooker.OperationSeconds = 0f;
                    cooker.ReadySeconds = 0f;
                    cooker.ResetProgress();
                    cooker.State = CookerState.Idle;
                    cooker.StatusText = "成品已收入库存";
                    cooker.Owner?.Staff?.ReleaseCooker(cooker.Id);
                }

                return;
            }

            if (cooker.LockedRecipe == null)
            {
                if (!cooker.IsActive)
                {
                    cooker.State = CookerState.Idle;
                    cooker.StatusText = "等待员工装料";
                    return;
                }

                if (!CookerWorkHelpers.TryLockSingleRecipe(cooker))
                {
                    cooker.State = CookerState.WaitingForInput;
                    cooker.StatusText = "等待材料";
                    return;
                }

                cooker.OperationSeconds = 0f;
                cooker.State = CookerState.Preparing;
            }

            if (cooker.State == CookerState.Preparing)
            {
                if (Operate(cooker, deltaTime, definition.PreparationDuration, CookerState.Preparing, "装料启动"))
                {
                    cooker.OperationSeconds = 0f;
                    cooker.WorkSeconds = 0f;
                    cooker.WorkProgress = 0f;
                    cooker.State = CookerState.Working;
                    cooker.StatusText = "自动加热中，员工可以离开";
                    cooker.Owner?.Staff?.ReleaseCooker(cooker.Id);
                }

                return;
            }

            cooker.WorkSeconds += deltaTime;
            cooker.WorkProgress = Mathf.Clamp01(cooker.WorkSeconds / definition.Duration);
            cooker.State = CookerState.Working;
            cooker.StatusText = $"自动加热 {cooker.WorkProgress * 100f:0}%（无需驻守）";
            if (cooker.WorkSeconds < definition.Duration)
            {
                return;
            }

            if (!cooker.Recipes.TryConvert(cooker, cooker.LockedRecipe, true, out var produced))
            {
                Reset(cooker);
                cooker.StatusText = "加工失败，材料已释放";
                return;
            }

            for (int index = 0; index < produced.Count; index++)
            {
                if (produced[index].IsValid)
                {
                    cooker.AddOutput(produced[index].Item, produced[index].Count);
                }
            }

            cooker.ForgetLock();
            cooker.ReadySeconds = 0f;
            cooker.OperationSeconds = 0f;
            cooker.State = CookerState.ReadyToCollect;
            cooker.StatusText = "加热完成，等待员工取出";
        }

        private static bool Operate(CookerRuntime cooker, float deltaTime, float duration, CookerState state, string label)
        {
            if (!cooker.IsActive)
            {
                cooker.StatusText = $"{label}（等待员工）";
                return false;
            }

            cooker.State = state;
            cooker.OperationSeconds += deltaTime;
            cooker.WorkProgress = Mathf.Clamp01(cooker.OperationSeconds / duration);
            cooker.StatusText = $"{label} {cooker.WorkProgress * 100f:0}%";
            return cooker.OperationSeconds >= duration;
        }

        public override void Reset(CookerRuntime cooker)
        {
            base.Reset(cooker);
            cooker.ClearOutputs();
            cooker.OperationSeconds = 0f;
            cooker.ReadySeconds = 0f;
            cooker.ResetProgress();
        }
    }
}
