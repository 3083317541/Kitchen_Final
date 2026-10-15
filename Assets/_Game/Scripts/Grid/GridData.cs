using System.Collections.Generic;
using UnityEngine;

namespace Kitchen.Grid
{
    /// <summary>
    /// Runtime occupancy record of a single grid cell. This is the authoritative source for
    /// "which cooker / which staff is on this cell" and is always updated through GridManager.
    /// </summary>
    public class GridData
    {
        private readonly List<int> staffIds = new List<int>(2);

        public GridData(Vector2Int gridPosition, Vector3 worldPosition)
        {
            this.gridPosition = gridPosition;
            this.worldPosition = worldPosition;
        }

        /// <summary>Grid coordinate of this cell.</summary>
        public readonly Vector2Int gridPosition;

        /// <summary>Centre of this cell in world space at ground height.</summary>
        public readonly Vector3 worldPosition;

        /// <summary>Id of the cooker occupying this cell, or 0 when empty.</summary>
        public int cookerId;

        /// <summary>First staff id standing on this cell, or 0 when empty. Kept for quick reads.</summary>
        public int staffId;

        /// <summary>Every staff id standing on this cell. A cell allows at most one staff member.</summary>
        public IReadOnlyList<int> StaffIds => staffIds;

        /// <summary>Number of staff standing on this cell.</summary>
        public int StaffCount => staffIds.Count;

        /// <summary>Staff id at a position in the list, or 0 when out of range.</summary>
        public int GetStaffIdAt(int index)
        {
            return index >= 0 && index < staffIds.Count ? staffIds[index] : 0;
        }

        public bool HasCooker => cookerId != 0;

        public bool HasStaff => staffIds.Count > 0;

        public bool HasStaffOtherThan(int otherStaffId)
        {
            for (int i = 0; i < staffIds.Count; i++)
            {
                if (staffIds[i] != otherStaffId)
                {
                    return true;
                }
            }

            return false;
        }

        public bool AddStaff(int id)
        {
            if (id == 0 || staffIds.Contains(id))
            {
                return false;
            }

            staffIds.Add(id);
            staffId = staffIds[0];
            return true;
        }

        public bool RemoveStaff(int id)
        {
            bool removed = staffIds.Remove(id);
            if (removed)
            {
                staffId = staffIds.Count > 0 ? staffIds[0] : 0;
            }

            return removed;
        }

        public void ClearStaff()
        {
            staffIds.Clear();
            staffId = 0;
        }

        public override string ToString()
        {
            return $"Grid{gridPosition} cooker={cookerId} staff={string.Join(",", staffIds)}";
        }
    }
}
