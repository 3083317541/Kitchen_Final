using UnityEngine;

namespace Kitchen.Data
{
    /// <summary>
    /// Static description of an order: which dish, how many, how many clean plates it needs,
    /// the reward and the time limit.
    /// </summary>
    [CreateAssetMenu(menuName = "Kitchen/Order Definition", fileName = "Order_New")]
    public class OrderDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id = "order_new";
        [SerializeField] private string displayName = "New Order";

        [Header("Requirements")]
        [Tooltip("The dish the customer wants. Must also exist as a recipe output.")]
        [SerializeField] private ItemDefinition dish;
        [SerializeField, Min(1)] private int dishCount = 1;
        [SerializeField, Min(1)] private int cleanPlatesRequired = 1;

        [Header("Reward & Time")]
        [SerializeField, Min(0)] private int reward = 100;
        [Tooltip("Seconds allowed before the order fails.")]
        [SerializeField, Min(1f)] private float timeLimit = 120f;

        public string Id => string.IsNullOrEmpty(id) ? name : id;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

        public ItemDefinition Dish => dish;

        public int DishCount => Mathf.Max(1, dishCount);

        public int CleanPlatesRequired => Mathf.Max(1, cleanPlatesRequired);

        public int Reward => reward;

        public float TimeLimit => Mathf.Max(1f, timeLimit);

        public static OrderDefinition Create(
            string orderId,
            string orderDisplayName,
            ItemDefinition dish,
            int dishCount,
            int cleanPlates,
            int reward,
            float timeLimit)
        {
            OrderDefinition order = CreateInstance<OrderDefinition>();
            order.name = orderId;
            order.id = orderId;
            order.displayName = orderDisplayName;
            order.dish = dish;
            order.dishCount = Mathf.Max(1, dishCount);
            order.cleanPlatesRequired = Mathf.Max(1, cleanPlates);
            order.reward = reward;
            order.timeLimit = Mathf.Max(1f, timeLimit);
            return order;
        }

        public bool HasValidContent()
        {
            return dish != null && dishCount > 0 && cleanPlatesRequired > 0;
        }
    }
}
