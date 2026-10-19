using Kitchen.Core;
using UnityEngine;

namespace Kitchen.Data
{
    /// <summary>
    /// One pre-placed cooker entry of a <see cref="LevelConfig"/>.
    /// The position is an absolute grid coordinate.
    /// </summary>
    [System.Serializable]
    public struct CookerPlacement
    {
        [SerializeField] private CookerDefinition definition;
        [SerializeField] private Vector2Int gridPosition;
        [SerializeField] private CookerFacing facing;

        public CookerPlacement(CookerDefinition definition, Vector2Int gridPosition, CookerFacing facing = CookerFacing.North)
        {
            this.definition = definition;
            this.gridPosition = gridPosition;
            this.facing = CookerFacingUtility.Normalize(facing);
        }

        public CookerDefinition Definition => definition;

        public Vector2Int GridPosition => gridPosition;

        public CookerFacing Facing => CookerFacingUtility.Normalize(facing);

        public bool IsValid => definition != null;
    }
}
