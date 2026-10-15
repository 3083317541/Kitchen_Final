using Kitchen.Core;
using Kitchen.Data;
using UnityEngine;

namespace Kitchen.View
{
    /// <summary>
    /// Shows where a cooker would be placed, and whether that cell is legal.
    ///
    /// The preview is presentation only: it never creates a cooker and never charges money. It asks
    /// the caller for a legality answer and renders it, which is why the build rules stay in
    /// CookerManager and GridManager.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlacementPreview : MonoBehaviour
    {
        [Header("Art replacement")]
        [Tooltip("Optional ghost model. When set it replaces the generated box.")]
        [SerializeField] private GameObject ghostPrefab;
        [SerializeField] private Material validMaterial;
        [SerializeField] private Material invalidMaterial;

        [Header("Fallback appearance")]
        [SerializeField] private Color validColor = new Color(0.35f, 0.85f, 0.45f, 0.45f);
        [SerializeField] private Color invalidColor = new Color(0.90f, 0.30f, 0.28f, 0.45f);
        [SerializeField] private float ghostHeight = 0.45f;
        [SerializeField] private float ghostFootprint = 1.75f;

        private Transform ghostRoot;
        private Renderer ghostRenderer;
        private GameObject spawnItemRoot;
        private CookerDefinition currentDefinition;
        private bool visible;

        /// <summary>True when the ghost is currently displayed.</summary>
        public bool IsVisible => visible;

        /// <summary>Cell the ghost is currently over.</summary>
        public Vector2Int Cell { get; private set; }

        /// <summary>Whether the ghost is currently drawn in its legal state.</summary>
        public bool IsValid { get; private set; }

        /// <summary>Shows the ghost for a cooker definition and swaps the model if needed.</summary>
        public void Show(CookerDefinition definition, Vector3 worldPosition, Vector2Int cell)
        {
            if (definition != currentDefinition)
            {
                RebuildGhost(definition);
            }

            currentDefinition = definition;
            visible = true;
            Cell = cell;

            if (ghostRoot != null)
            {
                ghostRoot.gameObject.SetActive(true);
            }

            MoveTo(worldPosition);
        }

        private void RebuildGhost(CookerDefinition definition)
        {
            DestroyGhost();

            ghostRoot = new GameObject("PlacementGhost").transform;
            ghostRoot.SetParent(transform, false);

            float height = ghostHeight;
            float footprint = ghostFootprint;
            Color tint = validColor;

            if (definition != null)
            {
                height = Mathf.Clamp(definition.BodySize.y, 0.15f, 0.9f);
                footprint = Mathf.Min(1.8f, Mathf.Max(0.4f, definition.BodySize.x));
                tint = new Color(definition.AccentColor.r, definition.AccentColor.g, definition.AccentColor.b, validColor.a);
            }

            if (ghostPrefab != null)
            {
                GameObject instance = Instantiate(ghostPrefab, ghostRoot);
                instance.name = "ArtGhost";
                ghostRenderer = instance.GetComponentInChildren<Renderer>();
                return;
            }

            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "GhostBox";
            box.transform.SetParent(ghostRoot, false);
            box.transform.localPosition = new Vector3(0f, height * 0.5f + 0.03f, 0f);
            box.transform.localScale = new Vector3(footprint, height, footprint);
            Object.Destroy(box.GetComponent<Collider>());

            ghostRenderer = box.GetComponent<Renderer>();
            if (ghostRenderer != null)
            {
                ghostRenderer.sharedMaterial = validMaterial != null
                    ? validMaterial
                    : Whitebox.TransparentMaterial(tint, "GhostValid");
            }

            // A footprint outline makes the occupied cell unmistakable.
            GameObject outline = Whitebox.Quad("GhostPad", ghostRoot, new Vector3(0f, 0.025f, 0f), 1f, tint, gameObject.layer);
            outline.transform.localScale = new Vector3(1.86f, 1.86f, 1f);

            spawnItemRoot = new GameObject("GhostItems");
            spawnItemRoot.transform.SetParent(ghostRoot, false);
            spawnItemRoot.transform.localPosition = new Vector3(0f, height + 0.08f, 0f);
        }

        /// <summary>Moves the ghost to a world position.</summary>
        public void MoveTo(Vector3 worldPosition)
        {
            transform.position = new Vector3(worldPosition.x, worldPosition.y, worldPosition.z);
        }

        /// <summary>Switches the ghost between its legal and illegal look.</summary>
        public void SetValid(bool valid, Vector2Int cell)
        {
            IsValid = valid;
            Cell = cell;

            if (ghostRenderer == null)
            {
                return;
            }

            Material material = valid
                ? (validMaterial != null ? validMaterial : Whitebox.TransparentMaterial(validColor, "GhostValid"))
                : (invalidMaterial != null ? invalidMaterial : Whitebox.TransparentMaterial(invalidColor, "GhostInvalid"));

            ghostRenderer.sharedMaterial = material;
        }

        /// <summary>Hides the ghost.</summary>
        public void Hide()
        {
            visible = false;
            if (ghostRoot != null)
            {
                ghostRoot.gameObject.SetActive(false);
            }
        }

        private void DestroyGhost()
        {
            if (ghostRoot == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(ghostRoot.gameObject);
            }
            else
            {
                DestroyImmediate(ghostRoot.gameObject);
            }

            ghostRoot = null;
            ghostRenderer = null;
            spawnItemRoot = null;
        }

        /// <summary>Destroys this preview.</summary>
        public void Dispose()
        {
            DestroyGhost();

            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }
    }
}
