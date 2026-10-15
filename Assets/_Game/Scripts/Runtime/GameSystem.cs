using UnityEngine;

namespace Kitchen.Runtime
{
    /// <summary>
    /// Base class for every domain system (grid, cookers, staff, work, inventory, recipes, orders,
    /// plate returns).
    ///
    /// They live on the <c>Systems</c> object of the Kitchen scene so their wiring is visible in the
    /// inspector, but they are never entry points: GameRuntime creates them, injects their
    /// dependencies through <see cref="Initialize"/>, and calls <see cref="Tick"/> in a fixed order.
    /// No system looks up a scene object on its own, and none of them reads input devices.
    /// </summary>
    public abstract class GameSystem : MonoBehaviour
    {
        /// <summary>The runtime that owns this system. Assigned by <see cref="Initialize"/>.</summary>
        public GameRuntime Runtime { get; private set; }

        public bool IsInitialized { get; private set; }

        /// <summary>Called once by GameRuntime before the first tick. Dependencies are already wired.</summary>
        public void Initialize(GameRuntime runtime)
        {
            if (IsInitialized)
            {
                return;
            }

            Runtime = runtime;
            OnInitialize();
            IsInitialized = true;
        }

        /// <summary>Override to cache injected dependencies and set up internal state.</summary>
        protected virtual void OnInitialize()
        {
        }

        /// <summary>Per-frame tick, called by GameRuntime in its fixed system order.</summary>
        public virtual void Tick(float deltaTime)
        {
        }

        /// <summary>Called by GameRuntime when the level is restarted or the runtime shuts down.</summary>
        public virtual void Shutdown()
        {
        }
    }
}
