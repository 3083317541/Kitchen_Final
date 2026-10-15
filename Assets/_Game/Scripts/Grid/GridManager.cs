using System.Collections.Generic;
using Kitchen.Data;
using Kitchen.Runtime;
using Kitchen.View;
using UnityEngine;

namespace Kitchen.Grid
{
    /// <summary>
    /// Owns the logical grid: cell lookup, coordinate conversion, occupancy bookkeeping and the
    /// legality rules for building and walking. It never creates or destroys gameplay objects;
    /// CookerManager and StaffManager ask it for permission and then report the result back.
    /// </summary>
    [DisallowMultipleComponent]
    public class GridManager : GameSystem
    {
        private readonly Dictionary<Vector2Int, GridData> cells = new Dictionary<Vector2Int, GridData>();
        private readonly List<Vector2Int> cellKeys = new List<Vector2Int>();

        private int width = 5;
        private int height = 5;
        private float cellSize = 2f;
        private float groundHeight;
        private Vector3 mapCenter = Vector3.zero;
        private Transform gridRoot;
        private GridView view;

        public int Width => width;

        public int Height => height;

        public float CellSize => cellSize;

        public float GroundHeight => groundHeight;

        public Vector3 MapCenter => mapCenter;

        /// <summary>Presentation object showing the cells, or null before it is built.</summary>
        public GridView View => view;

        /// <summary>Injected by GameRuntime so the grid view is parented without a scene search.</summary>
        public void SetWorldRoots(Transform root)
        {
            gridRoot = root;
        }

        /// <summary>Number of generated cells. Used by validation code and tests.</summary>
        public int CellCount => cells.Count;

        /// <summary>Read-only view of every cell key, for iteration without allocating.</summary>
        public IReadOnlyList<Vector2Int> CellKeys => cellKeys;

        /// <summary>
        /// Pre-generates the grid described by <paramref name="config"/>.
        /// Supports odd and even extents because positions are derived from the map centre.
        /// </summary>
        public void BuildGrid(LevelConfig config)
        {
            if (config == null)
            {
                Debug.LogError("[GridManager] BuildGrid received a null LevelConfig.");
                return;
            }

            width = config.Width;
            height = config.Height;
            cellSize = config.CellSize;
            groundHeight = config.GroundHeight;
            mapCenter = config.MapCenter;

            cells.Clear();
            cellKeys.Clear();

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Vector2Int key = new Vector2Int(x, y);
                    cells.Add(key, new GridData(key, GridToWorld(key)));
                    cellKeys.Add(key);
                }
            }
        }

        // ------------------------------------------------------------------ presentation

        /// <summary>
        /// Creates the cell highlights under <paramref name="root"/>. The grid view is purely
        /// cosmetic: it never decides legality and never touches occupancy.
        /// </summary>
        public void BuildGridVisuals(LevelConfig config, Transform root)
        {
            if (config == null || root == null)
            {
                return;
            }

            if (view != null)
            {
                view.Dispose();
                view = null;            }

            view = ViewFactory.CreateGridView();
            if (view == null)
            {
                Debug.LogWarning("[GridManager] No grid view could be created.");
                return;
            }

            view.transform.SetParent(root, false);
            view.name = "Grid_Cells";
            view.Build(this, config);
        }

        /// <summary>
        /// Clears every cell highlight. Exposed here so callers never have to reach into the view.
        /// </summary>
        public void ClearHighlights()
        {
            view?.ClearHighlights();
        }

        /// <summary>
        /// Re-registers every placed cooker and every staff member on the grid from the managers that
        /// own them. <see cref="BuildGrid"/> creates empty cells, so anything that was already in the
        /// world must be registered again afterwards. The same call repairs the grid if an occupancy
        /// record is ever lost.
        /// </summary>
        public void RebuildOccupancyFromManagers()
        {
            if (Runtime == null)
            {
                return;
            }

            for (int i = 0; i < cellKeys.Count; i++)
            {
                GridData data = cells[cellKeys[i]];
                data.cookerId = 0;
                data.ClearStaff();
            }

            if (Runtime.Cookers != null)
            {
                IReadOnlyList<Cookers.CookerRuntime> cookers = Runtime.Cookers.Cookers;
                for (int i = 0; i < cookers.Count; i++)
                {
                    Cookers.CookerRuntime cooker = cookers[i];
                    if (cooker.IsPlaced && InBounds(cooker.GridPosition))
                    {
                        TryOccupyCooker(cooker.GridPosition, cooker.Id);
                    }
                }
            }

            if (Runtime.Staff != null)
            {
                IReadOnlyList<Entities.StaffRuntime> staff = Runtime.Staff.Staff;
                for (int i = 0; i < staff.Count; i++)
                {
                    Entities.StaffRuntime member = staff[i];
                    if (InBounds(member.GridPosition))
                    {
                        TryOccupyStaff(member.GridPosition, member.Id);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ conversion

        /// <summary>
        /// Centre of a cell in world space at ground height.
        ///
        /// The origin is offset by half a cell so that the <em>map</em> is centred on the configured
        /// map centre. That keeps both extents correct: a 5x5 map puts its middle cell exactly on the
        /// centre, and a 4x4 map straddles it, because the centre then falls on a cell boundary.
        /// </summary>
        public Vector3 GridToWorld(Vector2Int gridPosition)
        {
            return Math.GridToWorld(gridPosition);
        }

        /// <summary>World X of grid column 0.</summary>
        public float OriginX => Math.OriginX;

        /// <summary>World Z of grid row 0.</summary>
        public float OriginZ => Math.OriginZ;

        /// <summary>
        /// The shared grid arithmetic for the current level. The editor uses the same struct when it
        /// authors the visible scene preview, which is what keeps the two exactly aligned.
        /// </summary>
        public KitchenGridMath Math => new KitchenGridMath(width, height, cellSize, mapCenter, groundHeight);

        /// <summary>Cell containing a world position. Rounding handles both odd and even extents.</summary>
        public Vector2Int WorldToGrid(Vector3 worldPosition)
        {
            return Math.WorldToGrid(worldPosition);
        }

        /// <summary>Cell containing a world position, clamped into the grid.</summary>
        public Vector2Int WorldToGridClamped(Vector3 worldPosition)
        {
            Vector2Int cell = WorldToGrid(worldPosition);
            return new Vector2Int(Mathf.Clamp(cell.x, 0, width - 1), Mathf.Clamp(cell.y, 0, height - 1));
        }

        // ------------------------------------------------------------------ queries

        public bool InBounds(Vector2Int gridPosition)
        {
            return gridPosition.x >= 0 && gridPosition.x < width && gridPosition.y >= 0 && gridPosition.y < height;
        }

        public GridData GetGridData(Vector2Int gridPosition)
        {
            return cells.TryGetValue(gridPosition, out GridData data) ? data : null;
        }

        public bool HasCooker(Vector2Int gridPosition)
        {
            GridData data = GetGridData(gridPosition);
            return data != null && data.HasCooker;
        }

        public int GetCookerId(Vector2Int gridPosition)
        {
            GridData data = GetGridData(gridPosition);
            return data != null ? data.cookerId : 0;
        }

        public bool HasStaff(Vector2Int gridPosition)
        {
            GridData data = GetGridData(gridPosition);
            return data != null && data.HasStaff;
        }

        public int GetStaffId(Vector2Int gridPosition)
        {
            GridData data = GetGridData(gridPosition);
            return data != null ? data.staffId : 0;
        }

        /// <summary>Finds the cell a cooker occupies, or (-1,-1) when unknown.</summary>
        public Vector2Int FindCookerCell(int cookerId)
        {
            if (cookerId == 0)
            {
                return new Vector2Int(-1, -1);
            }

            for (int i = 0; i < cellKeys.Count; i++)
            {
                GridData data = cells[cellKeys[i]];
                if (data.HasCooker && data.cookerId == cookerId)
                {
                    return data.gridPosition;
                }
            }

            return new Vector2Int(-1, -1);
        }

        /// <summary>Finds the cell a staff member stands on, or (-1,-1) when unknown.</summary>
        public Vector2Int FindStaffCell(int staffId)
        {
            if (staffId == 0)
            {
                return new Vector2Int(-1, -1);
            }

            for (int i = 0; i < cellKeys.Count; i++)
            {
                GridData data = cells[cellKeys[i]];
                if (data.staffId == staffId)
                {
                    return data.gridPosition;
                }
            }

            return new Vector2Int(-1, -1);
        }

        // ------------------------------------------------------------------ validation

        /// <summary>A cell accepts a cooker when it is inside the grid and holds no other cooker.</summary>
        public bool IsFreeForCooker(Vector2Int gridPosition, int ignoreCookerId = 0)
        {
            GridData data = GetGridData(gridPosition);
            if (data == null)
            {
                return false;
            }

            return !data.HasCooker || data.cookerId == ignoreCookerId;
        }

        /// <summary>
        /// A cell accepts a staff member when it is inside the grid and holds no other staff member.
        /// Staff may share a cell with a cooker, which is how cookers get activated.
        /// </summary>
        public bool IsFreeForStaff(Vector2Int gridPosition, int staffId = 0)
        {
            GridData data = GetGridData(gridPosition);
            if (data == null)
            {
                return false;
            }

            return !data.HasStaffOtherThan(staffId);
        }

        /// <summary>Finds the closest free cell to <paramref name="preferred"/> using a widening search.</summary>
        public bool TryFindNearestFreeCellForStaff(Vector2Int preferred, int staffId, out Vector2Int result)
        {
            result = preferred;
            if (IsFreeForStaff(preferred, staffId))
            {
                return true;
            }

            for (int radius = 1; radius <= width + height; radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius)
                        {
                            continue;
                        }

                        Vector2Int candidate = new Vector2Int(preferred.x + dx, preferred.y + dy);
                        if (IsFreeForStaff(candidate, staffId))
                        {
                            result = candidate;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>Finds the closest free cell to <paramref name="preferred"/> for a cooker.</summary>
        public bool TryFindNearestFreeCellForCooker(Vector2Int preferred, int ignoreCookerId, out Vector2Int result)
        {
            result = preferred;
            if (IsFreeForCooker(preferred, ignoreCookerId))
            {
                return true;
            }

            for (int radius = 1; radius <= width + height; radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius)
                        {
                            continue;
                        }

                        Vector2Int candidate = new Vector2Int(preferred.x + dx, preferred.y + dy);
                        if (IsFreeForCooker(candidate, ignoreCookerId))
                        {
                            result = candidate;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        // ------------------------------------------------------------------ occupancy mutation

        public bool TryOccupyCooker(Vector2Int gridPosition, int cookerId)
        {
            GridData data = GetGridData(gridPosition);
            if (data == null || (data.HasCooker && data.cookerId != cookerId))
            {
                return false;
            }

            data.cookerId = cookerId;
            return true;
        }

        public bool TryReleaseCooker(Vector2Int gridPosition, int cookerId)
        {
            GridData data = GetGridData(gridPosition);
            if (data == null || data.cookerId != cookerId)
            {
                return false;
            }

            data.cookerId = 0;
            return true;
        }

        public bool TryOccupyStaff(Vector2Int gridPosition, int staffId)
        {
            GridData data = GetGridData(gridPosition);
            if (data == null || data.HasStaffOtherThan(staffId))
            {
                return false;
            }

            return data.AddStaff(staffId);
        }

        public bool TryReleaseStaff(Vector2Int gridPosition, int staffId)
        {
            GridData data = GetGridData(gridPosition);
            return data != null && data.RemoveStaff(staffId);
        }

        /// <summary>Moves a cooker record from one cell to another, leaving both cells consistent.</summary>
        public bool TryMoveCooker(Vector2Int from, Vector2Int to, int cookerId)
        {
            if (from == to)
            {
                return true;
            }

            if (!IsFreeForCooker(to, cookerId))
            {
                return false;
            }

            if (!TryReleaseCooker(from, cookerId))
            {
                return false;
            }

            if (!TryOccupyCooker(to, cookerId))
            {
                // Restore the previous state so the grid never loses a cooker record.
                TryOccupyCooker(from, cookerId);
                return false;
            }

            return true;
        }

        /// <summary>Diagnostic string describing the occupancy of one cell.</summary>
        public string DescribeCell(Vector2Int cell)
        {
            GridData data = GetGridData(cell);
            return data == null ? $"{cell}:out-of-bounds" : data.ToString();
        }

        /// <summary>
        /// Trace of the last cocked move, kept for diagnostics and automated checks.
        /// It is plain state rather than a log so nothing is written to the console in normal play.
        /// </summary>
        public string LastMoveTrace { get; private set; } = string.Empty;

        /// <summary>
        /// Moves a cooker between cells and additionally records a trace that shows exactly which
        /// check rejected the move. Callers that only need the outcome use <see cref="TryMoveCooker"/>.
        /// </summary>
        public bool TryMoveCookerTraced(Vector2Int from, Vector2Int to, int cookerId)
        {
            bool freeForCooker = IsFreeForCooker(to, cookerId);
            bool freeForStaff = IsFreeForStaff(to, cookerId);
            bool releaseFrom = TryReleaseCooker(from, cookerId);
            bool occupyTo = TryOccupyCooker(to, cookerId);

            if (!freeForCooker)
            {
                // Restore the original state.
                TryOccupyCooker(from, cookerId);
            }

            LastMoveTrace =
                $"from={DescribeCell(from)} to={DescribeCell(to)} free={freeForCooker} " +
                $"freeForStaff={freeForStaff} released={releaseFrom} occupied={occupyTo}";

            return freeForCooker && occupyTo;
        }

        /// <summary>Moves a staff record from one cell to another, leaving both cells consistent.</summary>
        public bool TryMoveStaff(Vector2Int from, Vector2Int to, int staffId)
        {
            if (from == to)
            {
                return true;
            }

            if (!IsFreeForStaff(to, staffId))
            {
                return false;
            }

            if (!TryReleaseStaff(from, staffId))
            {
                return false;
            }

            if (!TryOccupyStaff(to, staffId))
            {
                TryOccupyStaff(from, staffId);
                return false;
            }

            return true;
        }

        /// <summary>Drops every staff record for a staff member. Used when a staff is removed.</summary>
        public void ReleaseStaffEverywhere(int staffId)
        {
            for (int i = 0; i < cellKeys.Count; i++)
            {
                cells[cellKeys[i]].RemoveStaff(staffId);
            }
        }

        /// <summary>Repairs any inconsistent staff bookkeeping. Returns the number of cells fixed.</summary>
        public int ValidateConsistency(out string report)
        {
            int problems = 0;
            System.Text.StringBuilder builder = new System.Text.StringBuilder();

            for (int i = 0; i < cellKeys.Count; i++)
            {
                GridData data = cells[cellKeys[i]];
                if (data.StaffCount > 1)
                {
                    problems++;
                    builder.AppendLine($"cell {data.gridPosition} holds {data.StaffCount} staff");
                }

                if (data.staffId != data.GetStaffIdAt(0))
                {
                    problems++;
                    builder.AppendLine($"cell {data.gridPosition} staffId cache out of sync");
                }
            }

            report = problems == 0
                ? $"Grid consistent ({cells.Count} cells)."
                : $"{problems} grid problem(s):\n{builder}";

            return problems;
        }
    }
}
