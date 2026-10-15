using Kitchen.Cookers;
using UnityEngine;

namespace Kitchen.Work
{
    /// <summary>
    /// Holds the work strategy assets for the kitchen.
    ///
    /// Strategies are ScriptableObject assets, so their references are kept on this component
    /// (which lives next to WorkManager on the Systems object) and injected into WorkManager during
    /// initialisation. That keeps the wiring visible in the inspector without giving WorkManager a
    /// scene search.
    /// </summary>
    [DisallowMultipleComponent]
    public class WorkStrategyHolder : MonoBehaviour
    {
        [Tooltip("One strategy per WorkType. Missing entries are reported as errors at startup.")]
        [SerializeField] private CookerWorkStrategy[] strategies = new CookerWorkStrategy[0];

        public CookerWorkStrategy[] Strategies => strategies;

        /// <summary>Collects every non-null strategy. Never allocates more than the configured count.</summary>
        public int CopyTo(System.Collections.Generic.List<CookerWorkStrategy> destination)
        {
            if (destination == null || strategies == null)
            {
                return 0;
            }

            int added = 0;
            for (int i = 0; i < strategies.Length; i++)
            {
                if (strategies[i] == null || destination.Contains(strategies[i]))
                {
                    continue;
                }

                destination.Add(strategies[i]);
                added++;
            }

            return added;
        }
    }
}
