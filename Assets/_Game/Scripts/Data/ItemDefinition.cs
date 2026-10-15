using Kitchen.Core;
using UnityEngine;

namespace Kitchen.Data
{
    /// <summary>
    /// Static description of an ingredient, processed material, dish or plate.
    /// Holds no runtime state; counts live in InventoryManager / CookerRuntime.
    /// </summary>
    [CreateAssetMenu(menuName = "Kitchen/Item Definition", fileName = "Item_New")]
    public class ItemDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id = "item_new";
        [SerializeField] private string displayName = "New Item";
        [SerializeField] private ItemCategory category = ItemCategory.Raw;

        [Header("Whitebox Look")]
        [Tooltip("Tint used by the generated whitebox representation.")]
        [SerializeField] private Color viewColor = Color.white;

        [Tooltip("Uniform scale applied to the generated whitebox representation.")]
        [SerializeField] private float viewScale = 0.3f;

        [Header("Art Replacement")]
        [Tooltip("Optional artist prefab. When set, the generated whitebox mesh is skipped.")]
        [SerializeField] private GameObject viewPrefab;
        [SerializeField] private Material viewMaterial;
        [Tooltip("Optional icon used by the UI. When empty the UI shows a coloured swatch.")]
        [SerializeField] private Sprite icon;

        public string Id => string.IsNullOrEmpty(id) ? name : id;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

        public ItemCategory Category => category;

        public Color ViewColor => viewColor;

        public float ViewScale => viewScale;

        public GameObject ViewPrefab => viewPrefab;

        public Material ViewMaterial => viewMaterial;

        public Sprite Icon => icon;

        /// <summary>Edit-time / code-time authoring helper.</summary>
        public void Setup(string itemId, string itemDisplayName, ItemCategory itemCategory, Color color, float scale = 0.3f)
        {
            id = itemId;
            displayName = itemDisplayName;
            category = itemCategory;
            viewColor = color;
            viewScale = scale;
        }
    }
}
