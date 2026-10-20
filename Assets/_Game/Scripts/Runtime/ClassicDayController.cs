using System.Collections.Generic;
using Kitchen.Core;
using Kitchen.Data;
using UnityEngine;

namespace Kitchen.Runtime
{
    public sealed class ClassicDayController
    {
        private readonly GameRuntime runtime;
        private readonly List<KitchenSkill> choices = new List<KitchenSkill>();
        public ClassicRunState Run { get; }
        public DayPhase Phase { get; private set; } = DayPhase.Briefing;
        public float RemainingSeconds { get; private set; }
        public int Score { get; private set; }
        public int CompletedOrders { get; private set; }
        public int MissedOrders { get; private set; }
        public int BurntBatches { get; private set; }
        public int UnservedOrders { get; private set; }
        public int Benchmark => 500 + (Run.Day - 1) * 150;
        public IReadOnlyList<KitchenSkill> Choices => choices;
        public bool CanArrange => Phase == DayPhase.Briefing || Phase == DayPhase.Service;

        public ClassicDayController(GameRuntime owner, ClassicRunState run, float duration)
        {
            runtime = owner;
            Run = run;
            RemainingSeconds = duration;
        }

        public LevelConfig PrepareLevel(LevelConfig source)
        {
            LevelConfig level = Object.Instantiate(source);
            level.name = source.name + "_Day" + Run.Day;
            var stock = new List<ItemStack> { new ItemStack(source.FindItem(GameContentFactory.Ids.CleanPlate),
                2 + Run.Rank(KitchenSkill.ExtraPlate)) };
            level.Setup(source.Width, source.Height, source.CellSize, source.MapCenter, source.GroundHeight,
                Run.HasSnapshot ? Run.Money : source.StartingMoney, stock,
                Run.HasSnapshot ? Run.Layout : new List<CookerPlacement>(),
                source.StaffCount + Run.Rank(KitchenSkill.Recruitment), new List<Vector2Int>(source.StaffSpawnOffsets),
                new List<OrderDefinition>(source.OrderSequence), new List<ItemDefinition>(source.Items),
                new List<CookerDefinition>(source.Cookers), new List<RecipeDefinition>(source.Recipes));
            return level;
        }

        public bool BeginService()
        {
            if (Phase != DayPhase.Briefing) return false;
            Phase = DayPhase.Service;
            runtime.Orders.StartFirstOrder();
            runtime.Notify($"第 {Run.Day} 天开门营业！在倒计时结束前尽量多上菜。", MessageKind.Success);
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (Phase != DayPhase.Service) return;
            RemainingSeconds = Mathf.Max(0f, RemainingSeconds - deltaTime);
            if (RemainingSeconds > 0f) return;
            Phase = DayPhase.Results;
            UnservedOrders = runtime.Orders.ActiveOrders.Count;
            runtime.Input?.CancelCurrentInteraction();
            runtime.Orders.StopIssuing();
            Run.RecordDay(runtime, Score);
            var pool = new List<KitchenSkill>();
            foreach (KitchenSkill skill in System.Enum.GetValues(typeof(KitchenSkill)))
                if (skill != KitchenSkill.Recruitment || Run.Rank(skill) < 2) pool.Add(skill);
            int offset = (Run.Day - 1) % pool.Count;
            for (int index = 0; index < 3; index++) choices.Add(pool[(offset + index) % pool.Count]);
            runtime.UI?.RefreshAll();
        }

        public int OrderReward(int baseReward) => Mathf.RoundToInt(baseReward * Run.RewardMultiplier);

        public float OrderTimeLimit(float baseLimit) => Mathf.Max(35f, baseLimit * 0.6f /
            (1f + Mathf.Min(Run.Day - 1, 10) * 0.08f));

        public void RecordOrder(bool completed, int reward)
        {
            if (Phase != DayPhase.Service) return;
            if (completed) { Score += reward; CompletedOrders++; }
            else MissedOrders++;
        }

        public void RecordBurntBatch()
        {
            if (Phase == DayPhase.Service) BurntBatches++;
        }

        public bool ChooseSkill(KitchenSkill skill)
        {
            if (Phase != DayPhase.Results || !choices.Contains(skill)) return false;
            Phase = DayPhase.Transition;
            Run.AdvanceDay(skill);
            return true;
        }

        public static string SkillName(KitchenSkill skill)
        {
            switch (skill)
            {
                case KitchenSkill.Practice: return "熟能生巧";
                case KitchenSkill.QuickSteps: return "健步如飞";
                case KitchenSkill.Hospitality: return "好评加成";
                case KitchenSkill.ExtraPlate: return "添置餐具";
                case KitchenSkill.HeatControl: return "温控心得";
                default: return "招聘帮手";
            }
        }

        public static string SkillDescription(KitchenSkill skill)
        {
            switch (skill)
            {
                case KitchenSkill.Practice: return "人工加工、装料、取出与清理速度 +15%";
                case KitchenSkill.QuickSteps: return "员工移动速度 +15%";
                case KitchenSkill.Hospitality: return "订单得分与金币奖励 +15%";
                case KitchenSkill.ExtraPlate: return "每天可用盘子 +1，仍需回收清洗";
                case KitchenSkill.HeatControl: return "煎锅红色过火阶段的宽限时间 +25%";
                default: return "下一天员工 +1，最多招聘两位";
            }
        }
    }
}
