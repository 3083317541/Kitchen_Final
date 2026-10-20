using System.Collections.Generic;
using Kitchen.Data;
using Kitchen.Grid;
using UnityEngine;

namespace Kitchen.View
{
    /// <summary>
    /// Draws the floor and one quad per grid cell, and owns the cell highlights.
    ///
    /// It is deliberately unable to make gameplay decisions: it renders whatever this component is
    /// told to render, so a highlight can never be mistaken for a rule. Cell size, origin and the
    /// map extent all come from <see cref="GridManager"/>, never from a model's scale.
    /// </summary>
    [DisallowMultipleComponent]
    public class GridView : MonoBehaviour
    {
        [Header("Art replacement")]
        [Tooltip("Optional floor prefab. When set it replaces the generated floor slab.")]
        [SerializeField] private GameObject floorPrefab;
        [Tooltip("Optional prefab used for every grid cell, so the cell look can be edited as art.")]
        [SerializeField] private GameObject cellPrefab;

        [Tooltip("Optional materials. When empty the shared whitebox materials are used.")]
        [SerializeField] private Material floorMaterial;
        [SerializeField] private Material cellMaterial;
        [SerializeField] private Material validHighlightMaterial;
        [SerializeField] private Material invalidHighlightMaterial;

        [Header("Fallback appearance")]
        [SerializeField] private Color floorColor = new Color(0.30f, 0.32f, 0.36f);
        [SerializeField] private Color cellColor = new Color(0.40f, 0.43f, 0.48f, 0.55f);
        [SerializeField] private Color validColor = new Color(0.35f, 0.85f, 0.45f, 0.40f);
        [SerializeField] private Color invalidColor = new Color(0.90f, 0.30f, 0.28f, 0.40f);
        [SerializeField] private float cellYOffset = 0.02f;

        private readonly Dictionary<Vector2Int, GameObject> cells = new Dictionary<Vector2Int, GameObject>();
        private readonly HashSet<Vector2Int> highlighted = new HashSet<Vector2Int>();

        private Transform cellRoot;
        private Transform floorRoot;
        private int width;
        private int height;
        private float cellSize = 2f;
        private Vector3 origin;
        private float groundHeight;
        private Material artCellMaterial;

        /// <summary>Number of cell quads currently drawn.</summary>
        public int CellCount => cells.Count;

        /// <summary>Number of cells currently highlighted.</summary>
        public int HighlightedCount => highlighted.Count;

        /// <summary>True when this cell currently carries a highlight. Used by the acceptance suite.</summary>
        public bool IsHighlighted(Vector2Int cell)
        {
            return highlighted.Contains(cell);
        }

        /// <summary>
        /// Builds the cells and floor straight from a level, without a manager.
        ///
        /// The editor uses this to author a visible scene preview, so what the Scene view shows is
        /// exactly what the runtime builds. Normal play still goes through
        /// <see cref="Kitchen.Grid.GridManager"/>.
        /// </summary>
        public static GridView CreateForLevel(LevelConfig level, Transform parent)
        {
            if (level == null)
            {
                return null;
            }

            GameObject go = new GameObject("GridView");
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            GridView view = go.AddComponent<GridView>();
            GameLayers.SetLayerRecursively(go, GameLayers.Ground);
            view.BuildFromLevel(level);
            return view;
        }

        /// <summary>Builds the floor and every cell quad from a level configuration.</summary>
        public void BuildFromLevel(LevelConfig level)
        {
            if (level == null)
            {
                return;
            }

            KitchenGridMath math = KitchenGridMath.FromLevel(level);

            Dispose();

            width = math.Width;
            height = math.Height;
            cellSize = math.CellSize;
            groundHeight = math.GroundY;
            origin = math.GridToWorld(Vector2Int.zero);

            floorRoot = new GameObject("FloorMesh").transform;
            floorRoot.SetParent(transform, false);
            BuildFloor(level);

            cellRoot = new GameObject("Cells").transform;
            cellRoot.SetParent(transform, false);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Vector2Int key = new Vector2Int(x, y);
                    Vector3 world = math.GridToWorld(key);

                    GameObject quad = cellPrefab != null
                        ? Instantiate(cellPrefab, cellRoot)
                        : Whitebox.Decal($"Cell_{x}_{y}", cellRoot, Vector3.zero, 1f, cellColor, GameLayers.Ground);

                    quad.name = $"Cell_{x}_{y}";

                    quad.transform.position = new Vector3(world.x, groundHeight + cellYOffset, world.z);
                    quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    quad.transform.localScale = new Vector3(cellSize * 0.94f, cellSize * 0.94f, 1f);

                    ApplyMaterial(quad, cellMaterial != null ? cellMaterial : CellDefaultMaterial());
                    cells[key] = quad;
                }
            }
        }

        /// <summary>Builds the floor and every cell quad. Old geometry is removed first.</summary>
        public void Build(GridManager grid, LevelConfig config)
        {
            if (grid == null || config == null)
            {
                return;
            }

            Dispose();

            width = grid.Width;
            height = grid.Height;
            cellSize = grid.CellSize;
            groundHeight = grid.Math.GroundY;
            origin = grid.GridToWorld(Vector2Int.zero);

            floorRoot = new GameObject("FloorMesh").transform;
            floorRoot.SetParent(transform, false);
            BuildFloor(config);

            cellRoot = new GameObject("Cells").transform;
            cellRoot.SetParent(transform, false);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Vector2Int key = new Vector2Int(x, y);
                    Vector3 world = grid.GridToWorld(key);

                    GameObject quad = cellPrefab != null
                        ? Instantiate(cellPrefab, cellRoot)
                        : Whitebox.Quad($"Cell_{x}_{y}", cellRoot, Vector3.zero, 1f, cellColor, GameLayers.Ground);

                    quad.name = $"Cell_{x}_{y}";

                    // The quad's own scale decides its world size, and that size is the grid's.
                    quad.transform.position = new Vector3(world.x, groundHeight + cellYOffset, world.z);
                    quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    quad.transform.localScale = new Vector3(cellSize * 0.94f, cellSize * 0.94f, 1f);

                    ApplyMaterial(quad, cellMaterial != null ? cellMaterial : CellDefaultMaterial());
                    cells[key] = quad;
                }
            }
        }

        private Material CellDefaultMaterial()
        {
            return artCellMaterial != null ? artCellMaterial : Whitebox.TransparentMaterial(cellColor, "Cell");
        }

        private void BuildFloor(LevelConfig config)
        {
            int layer = GameLayers.Ground;
            artCellMaterial = null;

            // The floor spans the whole map, so its centre is the midpoint of the first and last cell.
            Vector3 first = GridToWorldLocal(0, 0);
            Vector3 last = GridToWorldLocal(width - 1, height - 1);
            Vector3 center = new Vector3(
                (first.x + last.x) * 0.5f,
                groundHeight,
                (first.z + last.z) * 0.5f);

            GameObject selectedFloor = config.FloorPrefab != null ? config.FloorPrefab : floorPrefab;
            if (selectedFloor != null)
            {
                GameObject instance = Instantiate(selectedFloor, floorRoot);
                instance.name = "ArtFloor";
                instance.transform.position = center;
                var layout = instance.GetComponent<KitchenFloorView>();
                if (layout != null)
                {
                    layout.Build(width, height, cellSize);
                    artCellMaterial = layout.CellOverlayMaterial;
                }
                GameLayers.SetLayerRecursively(instance, layer);
                return;
            }

            Material material = floorMaterial != null ? floorMaterial : Whitebox.SolidMaterial(floorColor, "Floor");

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "FloorSlab";
            floor.transform.SetParent(floorRoot, false);
            floor.transform.position = new Vector3(center.x, groundHeight - 0.15f, center.z);
            floor.transform.localScale = new Vector3(width * cellSize + 0.6f, 0.3f, height * cellSize + 0.6f);
            Object.Destroy(floor.GetComponent<Collider>());

            Renderer renderer = floor.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            GameLayers.SetLayerRecursively(floor, layer);
        }

        /// <summary>Bounds of the whole map in world space, used to frame the camera.</summary>
        public bool TryGetBounds(out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            Vector3 first = GridToWorldLocal(0, 0);
            Vector3 last = GridToWorldLocal(width - 1, height - 1);
            Vector3 center = new Vector3(
                (first.x + last.x) * 0.5f,
                groundHeight,
                (first.z + last.z) * 0.5f);

            bounds = new Bounds(center, new Vector3(width * cellSize, 0.1f, height * cellSize));
            return true;
        }

        /// <summary>Recomputes a cell centre from the cached grid metrics.</summary>
        private Vector3 GridToWorldLocal(int x, int y)
        {
            return new Vector3(
                origin.x + x * cellSize,
                groundHeight,
                origin.z + y * cellSize);
        }

        /// <summary>Shows a cell highlight, green when the cell is a legal target and red when not.</summary>
        public void SetHighlight(Vector2Int cell, bool visible, bool valid)
        {
            if (!cells.TryGetValue(cell, out GameObject quad) || quad == null)
            {
                return;
            }

            if (!visible)
            {
                ClearHighlight(cell);
                return;
            }

            ApplyMaterial(quad, valid ? ValidMaterial() : InvalidMaterial());
            highlighted.Add(cell);
        }

        /// <summary>Restores a single cell to its normal look.</summary>
        public void ClearHighlight(Vector2Int cell)
        {
            if (!highlighted.Remove(cell))
            {
                return;
            }

            if (cells.TryGetValue(cell, out GameObject quad) && quad != null)
            {
                ApplyMaterial(quad, cellMaterial != null ? cellMaterial : CellDefaultMaterial());
            }
        }

        /// <summary>Clears every highlight.</summary>
        public void ClearHighlights()
        {
            foreach (Vector2Int cell in highlighted)
            {
                if (cells.TryGetValue(cell, out GameObject quad) && quad != null)
                {
                    ApplyMaterial(quad, cellMaterial != null ? cellMaterial : CellDefaultMaterial());
                }
            }

            highlighted.Clear();
        }

        private Material ValidMaterial()
        {
            return validHighlightMaterial != null ? validHighlightMaterial : Whitebox.TransparentMaterial(validColor, "Valid");
        }

        private Material InvalidMaterial()
        {
            return invalidHighlightMaterial != null ? invalidHighlightMaterial : Whitebox.TransparentMaterial(invalidColor, "Invalid");
        }

        private static void ApplyMaterial(GameObject target, Material material)
        {
            if (target == null || material == null)
            {
                return;
            }

            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        /// <summary>Removes all generated geometry.</summary>
        public void Dispose()
        {
            foreach (KeyValuePair<Vector2Int, GameObject> pair in cells)
            {
                DestroyObject(pair.Value);
            }

            cells.Clear();
            highlighted.Clear();

            DestroyObject(cellRoot != null ? cellRoot.gameObject : null);
            DestroyObject(floorRoot != null ? floorRoot.gameObject : null);

            cellRoot = null;
            floorRoot = null;
        }

        private void OnDestroy()
        {
            cells.Clear();
            highlighted.Clear();
        }

        private static void DestroyObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                target.SetActive(false);
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
