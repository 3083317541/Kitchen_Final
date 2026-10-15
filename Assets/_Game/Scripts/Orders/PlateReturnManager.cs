using System.Collections.Generic;
using Kitchen.Core;
using Kitchen.Data;
using Kitchen.Runtime;
using UnityEngine;

namespace Kitchen.Orders
{
    /// <summary>
    /// Handles the plate cycle after a delivery: every served dish produces a "plate to return"
    /// ticket that waits a random 10-20 seconds and then turns into one dirty plate.
    ///
    /// All tickets share this one queue and this one timer. No plate owns a timer of its own, and
    /// no coroutine is created per plate.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlateReturnManager : GameSystem
    {
        private readonly Queue<float> pendingReturns = new Queue<float>();

        private float nextReturnIn = -1f;
        private float minDelay = 10f;
        private float maxDelay = 20f;

        /// <summary>Tickets waiting for their delay to elapse.</summary>
        public int PendingCount => pendingReturns.Count;

        /// <summary>Seconds until the next dirty plate appears, or -1 when the queue is empty.</summary>
        public float NextReturnIn => nextReturnIn;

        public void Configure(float minimum, float maximum)
        {
            minDelay = Mathf.Max(0.01f, minimum);
            maxDelay = Mathf.Max(minDelay, maximum);
        }

        public override void Shutdown()
        {
            pendingReturns.Clear();
            nextReturnIn = -1f;
        }

        /// <summary>Queues one plate for return.</summary>
        public void EnqueueReturn()
        {
            pendingReturns.Enqueue(RollDelay());
        }

        /// <summary>Queues several plates at once.</summary>
        public void EnqueueReturns(int count)
        {
            for (int i = 0; i < count; i++)
            {
                EnqueueReturn();
            }
        }

        private float RollDelay()
        {
            return Random.Range(minDelay, maxDelay);
        }

        public override void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            if (pendingReturns.Count == 0)
            {
                nextReturnIn = -1f;
                return;
            }

            if (nextReturnIn < 0f)
            {
                // First tick after the queue woke up: the front ticket is due exactly then.
                nextReturnIn = pendingReturns.Peek();
            }

            // Tickets are consumed strictly in order, so the one at the front of the queue is the
            // only one that can become due in this pass.
            nextReturnIn -= deltaTime;

            if (nextReturnIn > 0f)
            {
                return;
            }

            pendingReturns.Dequeue();

            ItemDefinition dirtyPlate = Runtime.DirtyPlate;
            if (dirtyPlate != null && Runtime.Inventory != null)
            {
                Runtime.Inventory.Add(dirtyPlate, 1);
                Runtime.Notify("有脏盘子送回了厨房。", MessageKind.Info);
            }

            nextReturnIn = pendingReturns.Count > 0 ? pendingReturns.Peek() : -1f;
        }

        /// <summary>Drops every pending ticket without producing plates. Used on restart.</summary>
        public void ClearPending()
        {
            pendingReturns.Clear();
            nextReturnIn = -1f;
        }
    }
}
