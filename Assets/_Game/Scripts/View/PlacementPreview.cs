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
        private Renderer[] ghostRenderers;
        private float visualHeight;
        private CookerDefinition currentDefinition;
        private bool visible;
        private WorkPositionIndicator workPositionIndicator;

        public CookerFacing Facing { get; private set; }

        public Vector2Int WorkCell => Cell + CookerFacingUtility.Offset(Facing);

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

            Transform cookerVisual = definition != null && definition.ViewPrefab != null
                ? definition.ViewPrefab.transform.Find("Visual") : null;
            GameObject model = ghostPrefab != null ? ghostPrefab : cookerVisual != null ? cookerVisual.gameObject : null;
            if (model != null)
            {
                GameObject instance = Instantiate(model, ghostRoot);
                instance.name = "ArtGhost";
                instance.SetActive(true);
                foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                {
                    collider.enabled = false;
                    if (Application.isPlaying) Destroy(collider);
                    else DestroyImmediate(collider);
                }
                foreach (var behaviour in instance.GetComponentsInChildren<Behaviour>(true))
                {
                    behaviour.enabled = false;
                }
                foreach (var body in instance.GetComponentsInChildren<Rigidbody>(true))
                {
                    body.isKinematic = true;
                    body.detectCollisions = false;
                }
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    height = Mathf.Max(height, renderer.bounds.max.y - ghostRoot.position.y);
                }
            }
            else
            {
                GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = "GhostBox";
                box.transform.SetParent(ghostRoot, false);
                box.transform.localPosition = new Vector3(0f, height * 0.5f + 0.03f, 0f);
                box.transform.localScale = new Vector3(footprint, height, footprint);
                var collider = box.GetComponent<Collider>();
                collider.enabled = false;
                if (Application.isPlaying) Destroy(collider);
                else DestroyImmediate(collider);
            }
            visualHeight = height;

            // A footprint outline makes the occupied cell unmistakable.
            GameObject outline = Whitebox.Quad("GhostPad", ghostRoot, new Vector3(0f, 0.025f, 0f), 1f, tint, gameObject.layer);
            outline.transform.localScale = new Vector3(1.86f, 1.86f, 1f);

            ghostRenderers = ghostRoot.GetComponentsInChildren<Renderer>(true);
            SetValid(true, Cell);
            GameLayers.SetLayerRecursively(ghostRoot.gameObject, GameLayers.Ground);
        }

        /// <summary>Moves the ghost to a world position.</summary>
        public void MoveTo(Vector3 worldPosition)
        {
            transform.position = new Vector3(worldPosition.x, worldPosition.y, worldPosition.z);
        }

        public void ShowWorkPosition(CookerFacing facing, float cellSize, bool open, bool occupied)
        {
            Facing = CookerFacingUtility.Normalize(facing);
            if (ghostRoot != null)
            {
                ghostRoot.localRotation = Quaternion.Euler(0f, (int)Facing * 90f, 0f);
            }

            if (workPositionIndicator == null)
            {
                workPositionIndicator = new WorkPositionIndicator(transform, cellSize, visualHeight + 0.18f);
            }
            Transform arrow = transform.Find("WorkPositionIndicator/FacingArrow");
            if (arrow != null)
            {
                Vector3 position = arrow.localPosition;
                position.y = visualHeight + 0.18f;
                arrow.localPosition = position;
            }

            workPositionIndicator.SetVisible(true);
            workPositionIndicator.Refresh(Facing, open, occupied, true);
        }

        /// <summary>Switches the ghost between its legal and illegal look.</summary>
        public void SetValid(bool valid, Vector2Int cell)
        {
            IsValid = valid;
            Cell = cell;

            if (ghostRenderers == null)
            {
                return;
            }

            Material material = valid
                ? (validMaterial != null ? validMaterial : Whitebox.TransparentMaterial(validColor, "GhostValid"))
                : (invalidMaterial != null ? invalidMaterial : Whitebox.TransparentMaterial(invalidColor, "GhostInvalid"));

            foreach (var renderer in ghostRenderers)
            {
                var materials = renderer.sharedMaterials;
                for (int index = 0; index < materials.Length; index++) materials[index] = material;
                renderer.sharedMaterials = materials;
            }
        }

        /// <summary>Hides the ghost.</summary>
        public void Hide()
        {
            visible = false;
            workPositionIndicator?.SetVisible(false);
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
                ghostRoot.name = "RetiredPlacementGhost";
                ghostRoot.gameObject.SetActive(false);
                Destroy(ghostRoot.gameObject);
            }
            else
            {
                DestroyImmediate(ghostRoot.gameObject);
            }

            ghostRoot = null;
            ghostRenderers = null;
        }

        /// <summary>Destroys this preview.</summary>
        public void Dispose()
        {
            Hide();
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
