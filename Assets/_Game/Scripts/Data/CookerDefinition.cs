using Kitchen.Core;
using UnityEngine;

namespace Kitchen.Data
{
    /// <summary>
    /// Static description of a cooker: price, processing timing, visuals and art hooks.
    /// Every price / duration the game uses is read from here, never hard coded in managers.
    /// </summary>
    [CreateAssetMenu(menuName = "Kitchen/Cooker Definition", fileName = "Cooker_New")]
    public class CookerDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id = "cooker_new";
        [SerializeField] private string displayName = "New Cooker";
        [SerializeField] private CookerKind kind = CookerKind.ChoppingStation;

        [Header("Economy")]
        [SerializeField, Min(0)] private int price = 50;

        [Header("Work")]
        [SerializeField] private WorkType workType = WorkType.Timed;

        [Tooltip("Seconds of work for one completed conversion (Timed / Assembly / Washing).")]
        [SerializeField, Min(0.01f)] private float duration = 5f;

        [Tooltip("Seconds between two discrete chop ticks (Chopping only).")]
        [SerializeField, Min(0.01f)] private float tickInterval = 0.3f;

        [Tooltip("Number of chop ticks required to finish one conversion (Chopping only).")]
        [SerializeField, Min(1)] private int requiredTicks = 10;

        [Header("Heating Operations (Timed only)")]
        [SerializeField, Min(0.01f)] private float preparationDuration = 0.75f;
        [SerializeField, Min(0.01f)] private float collectionDuration = 0.5f;
        [SerializeField, Min(0.01f)] private float burnGraceDuration = 10f;
        [SerializeField, Min(0.01f)] private float cleaningDuration = 1.5f;

        [Header("Whitebox Look")]
        [SerializeField] private Color bodyColor = new Color(0.72f, 0.72f, 0.78f);
        [SerializeField] private Color accentColor = new Color(0.35f, 0.62f, 0.85f);
        [SerializeField] private Vector3 bodySize = new Vector3(1.6f, 0.42f, 1.6f);

        [Header("Art Replacement")]
        [Tooltip("Optional artist prefab. It replaces the generated whitebox body entirely.")]
        [SerializeField] private GameObject viewPrefab;
        [SerializeField] private Material bodyMaterial;
        [SerializeField] private Material accentMaterial;

        public string Id => string.IsNullOrEmpty(id) ? name : id;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

        public CookerKind Kind => kind;

        public int Price => price;

        public WorkType WorkType => workType;

        /// <summary>Seconds required for one conversion (Timed / Assembly / Washing).</summary>
        public float Duration => Mathf.Max(0.01f, duration);

        /// <summary>Seconds between two discrete chop ticks (Chopping).</summary>
        public float TickInterval => Mathf.Max(0.01f, tickInterval);

        /// <summary>Chop ticks required for one conversion (Chopping).</summary>
        public int RequiredTicks => Mathf.Max(1, requiredTicks);

        public float PreparationDuration => Mathf.Max(0.01f, preparationDuration);
        public float CollectionDuration => Mathf.Max(0.01f, collectionDuration);
        public float BurnGraceDuration => Mathf.Max(0.01f, burnGraceDuration);
        public float CleaningDuration => Mathf.Max(0.01f, cleaningDuration);

        public bool RequiresInput => workType != WorkType.Washing;

        public Color BodyColor => bodyColor;

        public Color AccentColor => accentColor;

        public Vector3 BodySize => bodySize;

        public GameObject ViewPrefab => viewPrefab;

        public Material BodyMaterial => bodyMaterial;

        public Material AccentMaterial => accentMaterial;

        /// <summary>Edit-time / code-time authoring helper.</summary>
        public void Setup(
            string cookerId,
            string cookerDisplayName,
            CookerKind cookerKind,
            WorkType cookerWorkType,
            int cookerPrice,
            float seconds,
            Color body,
            Color accent,
            Vector3 size)
        {
            id = cookerId;
            displayName = cookerDisplayName;
            kind = cookerKind;
            workType = cookerWorkType;
            price = cookerPrice;
            duration = seconds;
            bodyColor = body;
            accentColor = accent;
            bodySize = size;
        }

        /// <summary>Edit-time / code-time authoring helper for discrete chopping.</summary>
        public void SetupChopping(string cookerId, string cookerDisplayName, int cookerPrice, float interval, int ticks, Color body, Color accent, Vector3 size)
        {
            Setup(cookerId, cookerDisplayName, CookerKind.ChoppingStation, WorkType.Chopping, cookerPrice, interval * ticks, body, accent, size);
            tickInterval = interval;
            requiredTicks = ticks;
        }
    }
}
