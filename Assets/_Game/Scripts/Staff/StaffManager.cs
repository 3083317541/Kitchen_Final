using System.Collections.Generic;
using Kitchen.Data;
using Kitchen.Entities;
using Kitchen.Runtime;
using Kitchen.View;
using UnityEngine;

namespace Kitchen.Staff
{
    /// <summary>
    /// Owns the employees: creation, selection, discrete grid movement and the smoothing that
    /// walks a model to the centre of its destination cell. Employees may never overlap each other
    /// but may share a cell with a cooker, which is how cookers are activated.
    /// </summary>
    [DisallowMultipleComponent]
    public class StaffManager : GameSystem
    {
        private readonly List<StaffRuntime> staff = new List<StaffRuntime>();
        private readonly Dictionary<int, StaffRuntime> byId = new Dictionary<int, StaffRuntime>();

        private Transform staffRoot;
        private GameObject staffPrefab;
        private int nextStaffId = 1;
        private StaffRuntime selected;

        public IReadOnlyList<StaffRuntime> Staff => staff;

        public Transform StaffRoot => staffRoot;

        /// <summary>Seconds needed to walk one cell. Derived from the speed field.</summary>
        private float moveSpeed = 6f;

        public StaffRuntime Selected => selected;

        /// <summary>Injected by GameRuntime so employees find their world parent without searching.</summary>
        public void SetWorldRoots(Transform root)
        {
            staffRoot = root;
            RemovePreviewObjects();
        }

        /// <summary>
        /// Deletes the preview chefs authored into the saved scene, so the runtime does not end up
        /// with two of everybody.
        /// </summary>
        private void RemovePreviewObjects()
        {
            if (staffRoot == null)
            {
                return;
            }

            Transform preview = staffRoot.Find(PreviewContainerName);
            if (preview == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(preview.gameObject);
            }
            else
            {
                DestroyImmediate(preview.gameObject);
            }
        }

        /// <summary>Name of the container the scene preview is authored under.</summary>
        private const string PreviewContainerName = "Preview";

        public void SetMoveSpeed(float speed)
        {
            moveSpeed = Mathf.Max(0.5f, speed);
        }

        // ------------------------------------------------------------------ creation

        /// <summary>Creates the roster described by the level configuration.</summary>
        public void SpawnLevelStaff(LevelConfig level)
        {
            if (level == null)
            {
                return;
            }

            staffPrefab = level.StaffPrefab;

            int count = level.StaffCount;
            IReadOnlyList<Vector2Int> offsets = level.StaffSpawnOffsets;
            Vector2Int fallbackBase = new Vector2Int(Runtime.Grid.Width / 2, Runtime.Grid.Height / 2);

            for (int i = 0; i < count; i++)
            {
                Vector2Int preferred = i < offsets.Count ? fallbackBase + offsets[i] : fallbackBase;

                if (!Runtime.Grid.TryFindNearestFreeCellForStaff(preferred, 0, out Vector2Int cell))
                {
                    Debug.LogWarning($"[StaffManager] No free cell for employee {i + 1}; it was skipped.");
                    continue;
                }

                CreateStaff($"Chef {i + 1}", cell);
            }

            Debug.Log($"[StaffManager] Created {staff.Count} employee(s).");
        }

        /// <summary>Creates a single employee. Used for level setup and tests.</summary>
        public StaffRuntime CreateStaff(string displayName, Vector2Int cell)
        {
            if (!Runtime.Grid.IsFreeForStaff(cell, 0))
            {
                if (!Runtime.Grid.TryFindNearestFreeCellForStaff(cell, 0, out cell))
                {
                    return null;
                }
            }

            int id = nextStaffId++;
            Vector3 world = Runtime.Grid.GridToWorld(cell);
            StaffRuntime runtime = new StaffRuntime(id, displayName, cell, world)
            {
                Owner = Runtime
            };

            if (!Runtime.Grid.TryOccupyStaff(cell, id))
            {
                return null;
            }

            staff.Add(runtime);
            byId.Add(id, runtime);

            CreateView(runtime);
            return runtime;
        }

        private void CreateView(StaffRuntime runtime)
        {
            GameObject instance = ViewFactory.CreateStaffView(staffPrefab);
            if (instance == null)
            {
                return;
            }

            instance.transform.SetParent(staffRoot, false);
            instance.transform.position = runtime.WorldPosition;
            instance.name = $"Staff_{runtime.Id}_{runtime.DisplayName.Replace(' ', '_')}";

            StaffView view = instance.GetComponent<StaffView>();
            if (view != null)
            {
                view.Bind(runtime);
                runtime.SetView(view);
            }

            instance.SetActive(true);
        }

        // ------------------------------------------------------------------ selection

        /// <summary>Selects an employee. Selecting null clears the selection.</summary>
        public void Select(StaffRuntime runtime)
        {
            if (selected == runtime)
            {
                return;
            }

            if (selected != null)
            {
                selected.IsSelected = false;
                selected.View?.SetSelected(false);
            }

            selected = runtime;

            if (selected != null)
            {
                selected.IsSelected = true;
                selected.View?.SetSelected(true);
            }
        }

        /// <summary>Selects an employee that is standing on a cell.</summary>
        public bool SelectAt(Vector2Int cell)
        {
            int id = Runtime.Grid.GetStaffId(cell);
            if (id == 0)
            {
                return false;
            }

            StaffRuntime runtime = GetStaff(id);
            if (runtime == null)
            {
                return false;
            }

            Select(runtime);
            return true;
        }

        public StaffRuntime GetStaff(int staffId)
        {
            return byId.TryGetValue(staffId, out StaffRuntime runtime) ? runtime : null;
        }

        public StaffRuntime GetStaffAt(Vector2Int cell)
        {
            return GetStaff(Runtime.Grid.GetStaffId(cell));
        }

        // ------------------------------------------------------------------ movement

        /// <summary>
        /// Requests a discrete move of the selected employee by one cell. The employee is allowed to
        /// start a new step before the previous one finished, which keeps WASD responsive.
        /// </summary>
        public bool TryMoveSelected(Vector2Int direction)
        {
            if (selected == null)
            {
                Runtime.Notify("请先用左键选中一名厨师。", MessageKind.Warning);
                return false;
            }

            return TryMove(selected, direction);
        }

        public bool TryMove(StaffRuntime runtime, Vector2Int direction)
        {
            if (runtime == null)
            {
                return false;
            }

            if (direction == Vector2Int.zero)
            {
                return false;
            }

            // Movement is strictly to an adjacent cell: no path finding in this version.
            if (Mathf.Abs(direction.x) + Mathf.Abs(direction.y) != 1)
            {
                Runtime.Notify("只能移动到相邻格子。", MessageKind.Warning);
                return false;
            }

            Vector2Int target = runtime.GridPosition + direction;
            if (!Runtime.Grid.InBounds(target))
            {
                Runtime.Notify("已经到厨房边缘了。", MessageKind.Warning);
                return false;
            }

            if (!Runtime.Grid.IsFreeForStaff(target, runtime.Id))
            {
                Runtime.Notify("那个格子上已经站着别的厨师了。", MessageKind.Warning);
                return false;
            }

            Vector2Int from = runtime.GridPosition;
            if (!Runtime.Grid.TryMoveStaff(from, target, runtime.Id))
            {
                Runtime.Notify("无法进入该格子。", MessageKind.Warning);
                return false;
            }

            Vector3 world = Runtime.Grid.GridToWorld(target);
            runtime.GridPosition = target;
            runtime.TargetGridPosition = target;
            runtime.IsMoving = true;

            // The model keeps its current position and walks to the new cell centre.
            return true;
        }

        /// <summary>Legality test used by the UI to grey out impossible moves.</summary>
        public bool CanMoveSelected(Vector2Int direction)
        {
            if (selected == null)
            {
                return false;
            }

            Vector2Int target = selected.GridPosition + direction;
            return Runtime.Grid.InBounds(target) && Runtime.Grid.IsFreeForStaff(target, selected.Id);
        }

        // ------------------------------------------------------------------ ticking

        public override void Tick(float deltaTime)
        {
            float speed = Mathf.Max(0.5f, moveSpeed);

            for (int i = 0; i < staff.Count; i++)
            {
                StaffRuntime runtime = staff[i];
                if (!runtime.IsMoving)
                {
                    continue;
                }

                Vector3 target = Runtime.Grid.GridToWorld(runtime.TargetGridPosition);
                Vector3 current = runtime.WorldPosition;

                // Only horizontal travel matters; the model always rests at ground height.
                Vector3 flatTarget = new Vector3(target.x, current.y, target.z);
                Vector3 next = Vector3.MoveTowards(current, flatTarget, speed * deltaTime);

                if ((next - flatTarget).sqrMagnitude <= 0.000001f)
                {
                    next = flatTarget;
                    runtime.IsMoving = false;
                }

                runtime.WorldPosition = next;

                if (runtime.View != null)
                {
                    runtime.View.ApplyWorldPosition(next);
                }
            }
        }
    }
}
