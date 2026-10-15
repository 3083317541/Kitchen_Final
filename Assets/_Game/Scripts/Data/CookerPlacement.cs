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

        public CookerPlacement(CookerDefinition definition, Vector2Int gridPosition)
        {
            this.definition = definition;
            this.gridPosition = gridPosition;
        }

        public CookerDefinition Definition => definition;

        public Vector2Int GridPosition => gridPosition;

        public bool IsValid => definition != null;
    }
}
