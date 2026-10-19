using System.Collections.Generic;
using Kitchen.Cookers;
using Kitchen.Core;
using Kitchen.Runtime;
using UnityEngine;

namespace Kitchen.Work
{
    /// <summary>
    /// Advances every cooker exactly once per frame, in a single loop.
    ///
    /// Manual work pauses without an assigned employee. Timed heating also advances unattended
    /// phases through its strategy. No cooker owns a timer of its own.
    /// </summary>
    [DisallowMultipleComponent]
    public class WorkManager : GameSystem
    {
        private readonly List<CookerWorkStrategy> strategies = new List<CookerWorkStrategy>();

        private readonly Dictionary<WorkType, CookerWorkStrategy> byWorkType = new Dictionary<WorkType, CookerWorkStrategy>();

        /// <summary>Strategies used by this runtime, taken from the WorkStrategyHolder on the same object.</summary>
        public IReadOnlyList<CookerWorkStrategy> Strategies => strategies;

        public void AddStrategy(CookerWorkStrategy strategy)
        {
            if (strategy == null || strategies.Contains(strategy))
            {
                return;
            }

            strategies.Add(strategy);
            byWorkType[strategy.WorkType] = strategy;
        }

        protected override void OnInitialize()
        {
            strategies.Clear();
            byWorkType.Clear();

            // The strategy assets sit on a holder component next to this one, so no scene search is
            // involved: a component may always inspect its own game object.
            WorkStrategyHolder holder = GetComponent<WorkStrategyHolder>();
            if (holder != null)
            {
                holder.CopyTo(strategies);
            }

            for (int i = 0; i < strategies.Count; i++)
            {
                CookerWorkStrategy strategy = strategies[i];
                if (strategy == null)
                {
                    continue;
                }

                byWorkType[strategy.WorkType] = strategy;
            }

            if (byWorkType.Count == 0)
            {
                Debug.LogError("[WorkManager] No work strategies configured; no cooker can process anything.");
            }
        }

        public CookerWorkStrategy GetStrategy(WorkType workType)
        {
            return byWorkType.TryGetValue(workType, out CookerWorkStrategy strategy) ? strategy : null;
        }

        public override void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            CookerManager cookers = Runtime.Cookers;
            if (cookers == null)
            {
                return;
            }

            IReadOnlyList<CookerRuntime> list = cookers.Cookers;
            cookers.Tick(0f);
            for (int i = 0; i < list.Count; i++)
            {
                CookerRuntime cooker = list[i];
                if (cooker == null || !cooker.IsPlaced || cooker.Definition == null)
                {
                    continue;
                }

                CookerWorkStrategy strategy = GetStrategy(cooker.Definition.WorkType);
                if (strategy == null)
                {
                    cooker.State = CookerState.Idle;
                    cooker.StatusText = $"缺少 {cooker.Definition.WorkType} 的加工策略";
                    continue;
                }

                if (!cooker.IsActive && cooker.Definition.WorkType != WorkType.Timed)
                {
                    // Paused: no progress is applied and no material is touched.
                    strategy.OnDeactivated(cooker);
                    if (!cooker.HasAnyOutput)
                    {
                        cooker.StatusText = cooker.LockedRecipe != null
                            ? "已暂停，等待指派相邻员工"
                            : "空闲（需要指派相邻员工）";
                    }
                    else
                    {
                        cooker.StatusText = "已完成，请收取产出";
                    }

                    continue;
                }

                strategy.Tick(cooker, deltaTime);
            }
        }

        /// <summary>Drops all locked work and returns reserved stock. Used on restart.</summary>
        public override void Shutdown()
        {
            if (Runtime != null && Runtime.Cookers != null)
            {
                IReadOnlyList<CookerRuntime> list = Runtime.Cookers.Cookers;
                for (int i = 0; i < list.Count; i++)
                {
                    CookerWorkStrategy strategy = GetStrategy(list[i].Definition != null ? list[i].Definition.WorkType : WorkType.Timed);
                    strategy?.Reset(list[i]);
                }
            }

            byWorkType.Clear();
        }
    }
}
