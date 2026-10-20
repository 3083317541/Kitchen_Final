using System.Collections.Generic;
using Kitchen.Data;
using UnityEngine;

namespace Kitchen.Runtime
{
    public enum DayPhase { Briefing, Service, Results, Transition }

    public enum KitchenSkill { Practice, QuickSteps, Hospitality, ExtraPlate, HeatControl, Recruitment }

    public sealed class ClassicRunState
    {
        private readonly Dictionary<KitchenSkill, int> skills = new Dictionary<KitchenSkill, int>();
        private readonly Dictionary<string, int> cookerPurchases = new Dictionary<string, int>();
        public int Day { get; private set; } = 1;
        public int TotalScore { get; private set; }
        public int BestDayScore { get; private set; }
        public int Money { get; private set; }
        public bool HasSnapshot { get; private set; }
        public List<CookerPlacement> Layout { get; } = new List<CookerPlacement>();
        public List<string> PreferredRecipes { get; } = new List<string>();

        public int Rank(KitchenSkill skill) => skills.TryGetValue(skill, out int rank) ? rank : 0;
        public int CookerPurchaseCount(string id) => cookerPurchases.TryGetValue(id, out int count) ? count : 0;

        public int CookerPrice(CookerDefinition definition, int increasePercent)
        {
            double price = System.Math.Ceiling(definition.Price *
                (1d + CookerPurchaseCount(definition.Id) * (double)increasePercent / 100d));
            return (int)System.Math.Min(int.MaxValue, price);
        }

        public void RecordCookerPurchase(string id)
        {
            int count = CookerPurchaseCount(id);
            if (count < int.MaxValue) cookerPurchases[id] = count + 1;
        }
        public float WorkMultiplier => 1f + Rank(KitchenSkill.Practice) * 0.15f;
        public float MoveMultiplier => 1f + Rank(KitchenSkill.QuickSteps) * 0.15f;
        public float RewardMultiplier => 1f + Rank(KitchenSkill.Hospitality) * 0.15f;
        public float HeatGraceMultiplier => 1f + Rank(KitchenSkill.HeatControl) * 0.25f;

        public void RecordDay(GameRuntime runtime, int score)
        {
            Money = runtime.Money;
            TotalScore += score;
            BestDayScore = Mathf.Max(BestDayScore, score);
            Layout.Clear();
            PreferredRecipes.Clear();
            foreach (var cooker in runtime.Cookers.Cookers)
            {
                Layout.Add(new CookerPlacement(cooker.Definition, cooker.GridPosition, cooker.Facing));
                PreferredRecipes.Add(cooker.PreferredRecipeId);
            }
            HasSnapshot = true;
        }

        public void AdvanceDay(KitchenSkill skill)
        {
            skills[skill] = Rank(skill) + 1;
            Day++;
        }

        public string DescribeSkills()
        {
            var entries = new List<string>();
            foreach (KitchenSkill skill in System.Enum.GetValues(typeof(KitchenSkill)))
                if (Rank(skill) > 0) entries.Add($"{ClassicDayController.SkillName(skill)} {Rank(skill)}级");
            return entries.Count == 0 ? "暂无技能" : string.Join(" · ", entries);
        }
    }

    public static class ClassicProgress
    {
        public static ClassicRunState Current { get; private set; }
        public static void Begin() { Current = new ClassicRunState(); }
        public static void Leave() { Current = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() { Current = null; }
    }
}
