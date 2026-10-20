using System;
using System.Collections.Generic;
using Kitchen.Core;
using Kitchen.Data;
using Kitchen.Runtime;
using UnityEngine;

namespace Kitchen.Orders
{
    /// <summary>
    /// Issues orders, validates deliveries and settles rewards.
    ///
    /// A delivery is only accepted when the dish count, the clean plate count and the order state
    /// all agree. The check happens before anything is consumed, so a failed delivery never eats
    /// produce or plates. Rewards are paid exactly once because a settled order leaves the Active
    /// state immediately.
    /// </summary>
    [DisallowMultipleComponent]
    public class OrderManager : GameSystem
    {
        /// <summary>Live status of the current order, or of the last one that was settled.</summary>
        public class OrderStatus
        {
            public OrderDefinition Definition;
            public int SequenceIndex;
            public OrderState State = OrderState.Active;
            public float TimeRemaining;
            public int IssuedReward;
            public float IssuedTimeLimit;

            public ItemDefinition Dish => Definition != null ? Definition.Dish : null;

            public int DishRequired => Definition != null ? Definition.DishCount : 0;

            public int PlatesRequired => Definition != null ? Definition.CleanPlatesRequired : 0;

            public int Reward => IssuedTimeLimit > 0f ? IssuedReward : Definition != null ? Definition.Reward : 0;

            /// <summary>Total time the order allows, in seconds.</summary>
            public float TimeLimit => IssuedTimeLimit > 0f ? IssuedTimeLimit : Definition != null ? Definition.TimeLimit : 0f;

            public bool IsActive => State == OrderState.Active;
        }

        private readonly List<OrderDefinition> sequence = new List<OrderDefinition>();
        private readonly List<OrderStatus> activeOrders = new List<OrderStatus>();
        private int maxConcurrentOrders = 2;

        private OrderStatus current;
        private int sequenceCursor;
        private bool levelCleared;
        private int completedOrders;
        private int failedOrders;
        private int totalRewardEarned;
        private bool issuingStopped;
        private bool IsEndless => Runtime != null && Runtime.Classic != null;

        public event Action<OrderStatus> OrderIssued;
        public event Action<OrderStatus> OrderSettled;

        /// <summary>Raised once, when the last order of the level has been completed.</summary>
        public event Action LevelCleared;

        public IReadOnlyList<OrderDefinition> Sequence => sequence;

        public OrderStatus Current => current;

        public IReadOnlyList<OrderStatus> ActiveOrders => activeOrders;

        public int MaxConcurrentOrders => maxConcurrentOrders;

        public int CompletedOrders => completedOrders;

        public int FailedOrders => failedOrders;

        public int TotalRewardEarned => totalRewardEarned;

        /// <summary>Replaces the order sequence with the one described by the level.</summary>
        public void ApplyLevel(LevelConfig level)
        {
            sequence.Clear();
            activeOrders.Clear();
            current = null;
            if (level == null)
            {
                return;
            }

            maxConcurrentOrders = level.MaxConcurrentOrders;

            IReadOnlyList<OrderDefinition> configured = level.OrderSequence;
            for (int i = 0; i < configured.Count; i++)
            {
                OrderDefinition order = configured[i];
                if (order == null)
                {
                    continue;
                }

                if (!order.HasValidContent())
                {
                    Debug.LogWarning($"[OrderManager] Order '{order.name}' has no dish and was skipped.");
                    continue;
                }

                sequence.Add(order);
            }
        }

        /// <summary>Issues the first order of the sequence.</summary>
        public void StartFirstOrder()
        {
            if (IsEndless && Runtime.Classic.Phase != DayPhase.Service) return;
            sequenceCursor = 0;
            completedOrders = 0;
            failedOrders = 0;
            totalRewardEarned = 0;
            levelCleared = false;
            issuingStopped = false;
            activeOrders.Clear();
            current = null;
            IssueNextOrder();
        }

        /// <summary>
        /// Advances to the next order, or ends the level when the sequence is exhausted.
        ///
        /// The sequence does not wrap: a level is a finite list of orders, which is what gives the
        /// campaign a point at which to clear the level and hand out its reward.
        /// </summary>
        public void IssueNextOrder()
        {
            if (issuingStopped || sequence.Count == 0) return;
            while (activeOrders.Count < maxConcurrentOrders && (IsEndless || sequenceCursor < sequence.Count))
            {
                IssueOrder(sequenceCursor++);
            }

            current = activeOrders.Count > 0 ? activeOrders[0] : null;
            if (!IsEndless && sequence.Count > 0 && completedOrders == sequence.Count && !levelCleared)
            {
                levelCleared = true;
                LevelCleared?.Invoke();
            }
        }

        /// <summary>True once every order in the sequence has been completed.</summary>
        public bool IsLevelCleared => levelCleared;

        /// <summary>Orders still to be completed, counting the one in flight.</summary>
        public int OrdersRemaining => Mathf.Max(0, sequence.Count - completedOrders);

        /// <summary>Total orders in this level.</summary>
        public int OrderCount => sequence.Count;

        /// <summary>Position of the current order in the sequence, 1-based, or 0 when none is active.</summary>
        public int CurrentOrderNumber => current != null ? current.SequenceIndex + 1 : 0;

        private void IssueOrder(int index, int slot = -1)
        {
            OrderDefinition definition = sequence[index % sequence.Count];
            float timeLimit = Runtime.Classic != null ? Runtime.Classic.OrderTimeLimit(definition.TimeLimit) : definition.TimeLimit;

            OrderStatus issued = new OrderStatus
            {
                Definition = definition,
                SequenceIndex = index,
                State = OrderState.Active,
                TimeRemaining = timeLimit,
                IssuedTimeLimit = timeLimit,
                IssuedReward = Runtime.Classic != null ? Runtime.Classic.OrderReward(definition.Reward) : definition.Reward
            };

            if (slot < 0) activeOrders.Add(issued);
            else activeOrders.Insert(slot, issued);
            current = activeOrders[0];
            OrderIssued?.Invoke(issued);
            Runtime.Notify($"新订单：{definition.DisplayName}。", MessageKind.Info);
        }

        public override void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || issuingStopped)
            {
                return;
            }

            for (int index = activeOrders.Count - 1; index >= 0; index--)
            {
                OrderStatus order = activeOrders[index];
                order.TimeRemaining = Mathf.Max(0f, order.TimeRemaining - deltaTime);
                if (order.TimeRemaining <= 0f) FailOrder(order);
            }
        }

        // ------------------------------------------------------------------ delivery

        /// <summary>Renders an order state for the player facing message.</summary>
        private static string DescribeState(OrderState state)
        {
            switch (state)
            {
                case OrderState.Completed:
                    return "完成";
                case OrderState.Failed:
                    return "失败";
                default:
                    return "进行中";
            }
        }

        /// <summary>True when the player currently holds everything the order needs.</summary>
        public bool CanSubmit()
        {
            return CanSubmit(current);
        }

        public bool CanSubmit(OrderStatus order)
        {
            if (!Runtime.CanOperateKitchen || issuingStopped || order == null || !order.IsActive || !activeOrders.Contains(order))
            {
                return false;
            }

            Inventory.InventoryManager inventory = Runtime.Inventory;
            if (inventory == null)
            {
                return false;
            }

            ItemDefinition dish = order.Dish;
            if (dish == null)
            {
                return false;
            }

            if (!inventory.Has(dish, order.DishRequired) || order.TimeRemaining <= 0f)
            {
                return false;
            }

            ItemDefinition cleanPlate = Runtime.CleanPlate;
            if (cleanPlate == null)
            {
                return false;
            }

            return inventory.Has(cleanPlate, order.PlatesRequired);
        }

        /// <summary>
        /// Serves the current order. Validates the dish count, the clean plate count and the order
        /// state before consuming anything; on success it pays the reward, spawns the plate return
        /// ticket and issues the next order.
        /// </summary>
        public bool TrySubmitCurrentOrder()
        {
            return TrySubmitOrder(current);
        }

        public bool TrySubmitOrder(OrderStatus order)
        {
            if (!Runtime.CanOperateKitchen || issuingStopped) return false;
            if (order == null || !activeOrders.Contains(order))
            {
                Runtime.Notify("当前没有进行中的订单。", MessageKind.Warning);
                return false;
            }

            if (!order.IsActive)
            {
                Runtime.Notify($"订单「{order.Definition.DisplayName}」已经{DescribeState(order.State)}。", MessageKind.Warning);
                return false;
            }

            if (order.TimeRemaining <= 0f)
            {
                FailOrder(order);
                return false;
            }

            Inventory.InventoryManager inventory = Runtime.Inventory;
            ItemDefinition dish = order.Dish;
            ItemDefinition cleanPlate = Runtime.CleanPlate;

            if (dish == null || cleanPlate == null)
            {
                Runtime.Notify("订单配置不完整。", MessageKind.Warning);
                return false;
            }

            int dishRequired = order.DishRequired;
            int platesRequired = order.PlatesRequired;

            // Both requirements are checked before anything is taken, so a failed delivery is free.
            if (!inventory.Has(dish, dishRequired))
            {
                Runtime.Notify($"{dish.DisplayName} 不足：订单需要 {dishRequired} 份。", MessageKind.Warning);
                return false;
            }

            if (!inventory.Has(cleanPlate, platesRequired))
            {
                Runtime.Notify($"干净盘子不足：订单需要 {platesRequired} 个。", MessageKind.Warning);
                return false;
            }

            if (!inventory.TryConsume(dish, dishRequired) || !inventory.TryConsume(cleanPlate, platesRequired))
            {
                Runtime.Notify("上菜失败。", MessageKind.Warning);
                return false;
            }

            OrderStatus settled = order;
            settled.State = OrderState.Completed;
            activeOrders.Remove(settled);
            current = activeOrders.Count > 0 ? activeOrders[0] : null;
            completedOrders++;
            totalRewardEarned += settled.Reward;

            Runtime.AddMoney(settled.Reward);
            Runtime.Classic?.RecordOrder(true, settled.Reward);
            Runtime.Notify($"订单完成！获得 {settled.Reward} 金钱。", MessageKind.Success);

            // Each served dish sends one plate to the return queue instead of a per-dish timer.
            if (Runtime.PlateReturns != null)
            {
                for (int i = 0; i < platesRequired; i++)
                {
                    Runtime.PlateReturns.EnqueueReturn();
                }
            }

            OrderSettled?.Invoke(settled);
            IssueNextOrder();
            return true;
        }

        /// <summary>Marks the current order as failed. Safe to call repeatedly.</summary>
        public void FailCurrentOrder()
        {
            FailOrder(current);
        }

        private void FailOrder(OrderStatus order)
        {
            int slot = activeOrders.IndexOf(order);
            if (slot < 0 || !order.IsActive)
            {
                return;
            }

            OrderStatus settled = order;
            settled.State = OrderState.Failed;
            activeOrders.RemoveAt(slot);
            current = activeOrders.Count > 0 ? activeOrders[0] : null;
            failedOrders++;
            Runtime.Classic?.RecordOrder(false, 0);

            Runtime.Notify(IsEndless ? $"顾客离开：{settled.Definition.DisplayName} 已超时。" :
                $"订单失败：{settled.Definition.DisplayName} 已超时，重新再来一次。", MessageKind.Error);

            OrderSettled?.Invoke(settled);

            // A failed order is retried instead of skipped, so a mistake cannot make the level
            // impossible to finish. The sequence cursor stays where it is.
            if (IsEndless) IssueNextOrder();
            else IssueOrder(settled.SequenceIndex, slot);
        }

        public void StopIssuing()
        {
            issuingStopped = true;
            foreach (OrderStatus order in activeOrders) order.State = OrderState.Failed;
            activeOrders.Clear();
            current = null;
        }

        public override void Shutdown()
        {
            current = null;
            activeOrders.Clear();
            sequence.Clear();
            sequenceCursor = 0;
            levelCleared = false;
        }
    }
}
