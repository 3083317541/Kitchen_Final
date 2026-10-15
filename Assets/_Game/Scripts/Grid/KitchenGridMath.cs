using UnityEngine;

namespace Kitchen.Grid
{
    /// <summary>
    /// Pure grid arithmetic: cell index to world position and back.
    ///
    /// It is deliberately a plain struct rather than part of <see cref="GridManager"/>, so the same
    /// conversions can be used by the editor when it authors the visible scene preview. Keeping one
    /// implementation is what guarantees the preview sits exactly where the runtime places things.
    /// </summary>
    public struct KitchenGridMath
    {
        private readonly int width;
        private readonly int height;
        private readonly float cellSize;
        private readonly Vector3 mapCenter;
        private readonly float groundHeight;

        public KitchenGridMath(int width, int height, float cellSize, Vector3 mapCenter, float groundHeight)
        {
            this.width = Mathf.Max(1, width);
            this.height = Mathf.Max(1, height);
            this.cellSize = Mathf.Max(0.1f, cellSize);
            this.mapCenter = mapCenter;
            this.groundHeight = groundHeight;
        }

        /// <summary>Builds the arithmetic for a level.</summary>
        public static KitchenGridMath FromLevel(Data.LevelConfig level)
        {
            return level == null
                ? new KitchenGridMath(5, 5, 2f, Vector3.zero, 0f)
                : new KitchenGridMath(level.Width, level.Height, level.CellSize, level.MapCenter, level.GroundHeight);
        }

        public int Width => width;

        public int Height => height;

        public float CellSize => cellSize;

        /// <summary>World X of grid column 0.</summary>
        public float OriginX => mapCenter.x - width * cellSize * 0.5f + cellSize * 0.5f;

        /// <summary>World Z of grid row 0.</summary>
        public float OriginZ => mapCenter.z - height * cellSize * 0.5f + cellSize * 0.5f;

        /// <summary>Ground level in world space.</summary>
        public float GroundY => groundHeight + mapCenter.y;

        /// <summary>Centre of a cell in world space at ground height.</summary>
        public Vector3 GridToWorld(Vector2Int cell)
        {
            return new Vector3(
                OriginX + cell.x * cellSize,
                GroundY,
                OriginZ + cell.y * cellSize);
        }

        /// <summary>Centre of a cell when the grid is offset by whole cells from a reference cell.</summary>
        public Vector3 OffsetToWorld(Vector2Int reference, Vector2Int offset)
        {
            return GridToWorld(reference + offset);
        }

        /// <summary>Cell containing a world position.</summary>
        public Vector2Int WorldToGrid(Vector3 world)
        {
            return new Vector2Int(
                Mathf.RoundToInt((world.x - OriginX) / cellSize),
                Mathf.RoundToInt((world.z - OriginZ) / cellSize));
        }

        /// <summary>Cell at the middle of the map. Odd extents land on a cell, even extents between two.</summary>
        public Vector2Int CentreCell => new Vector2Int(width / 2, height / 2);
    }
}
