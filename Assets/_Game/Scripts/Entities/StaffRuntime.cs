using UnityEngine;

namespace Kitchen.Entities
{
    /// <summary>
    /// Runtime state of one kitchen employee: which cell it stands on, where it is moving to, and
    /// whether it is the currently selected employee. The visual model is a separate
    /// <see cref="Kitchen.View.StaffView"/> so gameplay never depends on art.
    /// </summary>
    public class StaffRuntime
    {
        public StaffRuntime(int staffId, string displayName, Vector2Int gridPosition, Vector3 worldPosition)
        {
            Id = staffId;
            DisplayName = displayName;
            GridPosition = gridPosition;
            TargetGridPosition = gridPosition;
            WorldPosition = worldPosition;
        }

        /// <summary>Stable runtime id. Never zero for a valid employee.</summary>
        public int Id { get; private set; }

        public string DisplayName { get; set; }

        /// <summary>Cell the employee is standing on. Updated the moment a move starts.</summary>
        public Vector2Int GridPosition { get; internal set; }

        /// <summary>Cell the employee is walking towards.</summary>
        public Vector2Int TargetGridPosition { get; internal set; }

        /// <summary>Smoothed world position of the model.</summary>
        public Vector3 WorldPosition { get; internal set; }

        /// <summary>True while the model is still travelling to the centre of its cell.</summary>
        public bool IsMoving { get; internal set; }

        public bool IsSelected { get; internal set; }

        public int AssignedCookerId { get; internal set; }

        public Vector2Int Facing { get; internal set; } = Vector2Int.up;

        public Vector2Int FrontCell => GridPosition + Facing;

        public Cookers.CookerRuntime AssignedCooker =>
            Owner != null && Owner.Cookers != null ? Owner.Cookers.GetCooker(AssignedCookerId) : null;

        /// <summary>The runtime that owns this employee.</summary>
        public Runtime.GameRuntime Owner { get; internal set; }

        /// <summary>Presentation object in the scene, or null when no view was created.</summary>
        public View.StaffView View { get; private set; }

        /// <summary>Cooker on the employee's current cell, or null.</summary>
        public Cookers.CookerRuntime OccupiedCooker =>
            Owner != null && Owner.Cookers != null ? Owner.Cookers.GetCookerAt(GridPosition) : null;

        public void SetView(View.StaffView view)
        {
            View = view;
        }

        /// <summary>Teleports the model to the centre of a cell.</summary>
        public void SnapTo(Vector2Int cell, Vector3 worldPosition)
        {
            GridPosition = cell;
            TargetGridPosition = cell;
            WorldPosition = worldPosition;
            IsMoving = false;
        }

        public override string ToString()
        {
            return $"{DisplayName}#{Id} @{GridPosition}{(IsMoving ? $" -> {TargetGridPosition}" : string.Empty)}";
        }
    }
}
