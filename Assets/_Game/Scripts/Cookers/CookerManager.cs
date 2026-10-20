using System.Collections.Generic;
using Kitchen.Core;
using Kitchen.Data;
using Kitchen.Grid;
using Kitchen.Runtime;
using Kitchen.View;
using UnityEngine;

namespace Kitchen.Cookers
{
    /// <summary>
    /// Owns the lifecycle of placed cookers: affordability, creation, placement validation, grid
    /// occupancy, movement and removal. Prices and timings always come from the CookerDefinition,
    /// never from constants in this class.
    /// </summary>
    [DisallowMultipleComponent]
    public class CookerManager : GameSystem
    {
        private readonly List<CookerRuntime> cookers = new List<CookerRuntime>();
        private readonly Dictionary<int, CookerRuntime> byId = new Dictionary<int, CookerRuntime>();
        private readonly List<CookerDefinition> catalogue = new List<CookerDefinition>();

        private Transform cookerRoot;
        private int nextCookerId = 1;
        private CookerRuntime selected;

        public IReadOnlyList<CookerRuntime> Cookers => cookers;

        /// <summary>Ordered catalogue of buildable cookers, taken from the level configuration.</summary>
        public IReadOnlyList<CookerDefinition> Catalogue => catalogue;

        /// <summary>Transform that newly created cooker views are parented to.</summary>
        public Transform CookerRoot => cookerRoot;

        /// <summary>Injected by GameRuntime so cookers find their world parent without searching.</summary>
        public void SetWorldRoots(Transform root)
        {
            cookerRoot = root;
            RemovePreviewObjects();
        }

        /// <summary>
        /// Deletes the preview cookers that were authored into the saved scene. The runtime creates
        /// its own views, so keeping the previews would duplicate every cooker.
        /// </summary>
        private void RemovePreviewObjects()
        {
            if (cookerRoot == null)
            {
                return;
            }

            Transform preview = cookerRoot.Find(PreviewContainerName);
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

        /// <summary>Replaces the buildable catalogue with the one described by the level.</summary>
        public void ApplyLevel(LevelConfig level)
        {
            catalogue.Clear();
            if (level == null)
            {
                return;
            }

            IReadOnlyList<CookerDefinition> configured = level.Cookers;
            for (int i = 0; i < configured.Count; i++)
            {
                if (configured[i] != null)
                {
                    catalogue.Add(configured[i]);
                }
            }
        }

        public CookerDefinition GetCatalogueEntry(int index)
        {
            if (index < 0 || index >= catalogue.Count)
            {
                return null;
            }

            return catalogue[index];
        }

        // ------------------------------------------------------------------ queries

        public CookerRuntime GetCooker(int cookerId)
        {
            return byId.TryGetValue(cookerId, out CookerRuntime runtime) ? runtime : null;
        }

        public CookerRuntime GetCookerAt(Vector2Int gridPosition)
        {
            return GetCooker(Runtime.Grid.GetCookerId(gridPosition));
        }

        /// <summary>True when a cell already holds a cooker and therefore cannot take another.</summary>
        public bool IsCellOccupied(Vector2Int gridPosition)
        {
            return Runtime.Grid.HasCooker(gridPosition);
        }

        /// <summary>Currently selected cooker, or null.</summary>
        public CookerRuntime Selected => selected;

        /// <summary>Selects a cooker. Passing null clears the selection.</summary>
        public void SelectCooker(CookerRuntime cooker)
        {
            if (selected == cooker)
            {
                return;
            }

            if (selected != null && selected.View != null)
            {
                selected.View.SetSelected(false);
            }

            selected = cooker;

            if (selected != null && selected.View != null)
            {
                selected.View.SetSelected(true);
            }
        }

        /// <summary>Staff id assigned to operate this cooker from an adjacent cell, or -1.</summary>
        public int TryGetWorkingStaffId(CookerRuntime cooker)
        {
            if (cooker == null || !cooker.IsPlaced)
            {
                return -1;
            }

            IReadOnlyList<Entities.StaffRuntime> employees = Runtime.Staff.Staff;
            for (int index = 0; index < employees.Count; index++)
            {
                Entities.StaffRuntime employee = employees[index];
                if (!employee.IsMoving && employee.AssignedCookerId == cooker.Id &&
                    employee.GridPosition == cooker.WorkPosition)
                {
                    return employee.Id;
                }
            }

            return -1;
        }

        // ------------------------------------------------------------------ creation

        /// <summary>Places the cookers listed in the level configuration.</summary>
        public void SpawnLevelCookers(LevelConfig level)
        {
            if (level == null)
            {
                return;
            }

            IReadOnlyList<CookerPlacement> placements = level.StartingCookers;
            for (int i = 0; i < placements.Count; i++)
            {
                CookerPlacement placement = placements[i];
                if (!placement.IsValid)
                {
                    continue;
                }

                if (Runtime.Grid.HasCooker(placement.GridPosition))
                {
                    Debug.LogWarning($"[CookerManager] Starting cooker '{placement.Definition.Id}' skipped: cell {placement.GridPosition} is taken.");
                    continue;
                }

                CreateCooker(placement.Definition, placement.GridPosition, true, placement.Facing);
            }

            for (int index = 0; index < cookers.Count; index++)
            {
                CookerRuntime cooker = cookers[index];
                for (int attempt = 0; attempt < 4 && !IsWorkPositionOpen(cooker.WorkPosition) &&
                    !(Runtime.Classic != null && Runtime.Classic.Run.HasSnapshot); attempt++)
                {
                    cooker.Facing = CookerFacingUtility.Clockwise(cooker.Facing);
                }

                cooker.View?.ApplyState();
            }
        }

        /// <summary>Creates a cooker without charging for it. Used for level setup and tests.</summary>
        public CookerRuntime CreateCooker(CookerDefinition definition, Vector2Int gridPosition, bool free, CookerFacing facing = CookerFacing.North)
        {
            if (definition == null)
            {
                Debug.LogError("[CookerManager] CreateCooker called with a null definition.");
                return null;
            }

            if (!Runtime.Grid.InBounds(gridPosition))
            {
                return null;
            }

            if (!Runtime.Grid.IsFreeForCooker(gridPosition))
            {
                return null;
            }

            int price = free ? 0 : Runtime.GetCookerPrice(definition);
            if (!free)
            {
                if (!Runtime.CanAfford(price))
                {
                    Runtime.Notify($"金钱不足，无法购买 {definition.DisplayName}（{price}）。", MessageKind.Warning);
                    return null;
                }

                Runtime.SpendMoney(price);
            }

            int id = nextCookerId++;
            Vector3 world = Runtime.Grid.GridToWorld(gridPosition);
            CookerRuntime cooker = new CookerRuntime(id, definition, gridPosition, facing)
            {
                Owner = Runtime
            };

            if (!Runtime.Grid.TryOccupyCooker(gridPosition, id))
            {
                if (!free)
                {
                    Runtime.AddMoney(price);
                }

                return null;
            }

            cooker.SetPlacement(gridPosition, world, true);
            cookers.Add(cooker);
            byId.Add(id, cooker);

            CreateView(cooker);

            if (!free) Runtime.Classic?.Run.RecordCookerPurchase(definition.Id);
            Runtime.Notify($"已放置 {definition.DisplayName}，花费 {price}。", MessageKind.Info);
            return cooker;
        }

        private void CreateView(CookerRuntime cooker)
        {
            GameObject instance = ViewFactory.CreateCookerView(cooker.Definition);
            if (instance == null)
            {
                return;
            }

            instance.transform.SetParent(cookerRoot, false);
            instance.transform.position = cooker.WorldPosition;
            instance.name = $"Cooker_{cooker.Id}_{cooker.DefinitionId}";

            CookerView view = instance.GetComponent<CookerView>();
            if (view != null)
            {
                view.Bind(cooker);
                cooker.SetView(view);
            }

            instance.SetActive(true);
        }

        /// <summary>Player facing build request: the full affordability + placement + charge flow.</summary>
        public CookerRuntime TryBuildCooker(CookerDefinition definition, Vector2Int gridPosition, CookerFacing facing = CookerFacing.North)
        {
            if (definition == null)
            {
                Runtime.Notify("请先选中一个厨具。", MessageKind.Warning);
                return null;
            }

            if (!Runtime.Grid.InBounds(gridPosition))
            {
                Runtime.Notify("不能建在厨房范围之外。", MessageKind.Warning);
                return null;
            }

            if (!Runtime.Grid.IsFreeForCooker(gridPosition))
            {
                Runtime.Notify("该格子已经有厨具了。", MessageKind.Warning);
                return null;
            }

            int price = Runtime.GetCookerPrice(definition);
            if (!Runtime.CanAfford(price))
            {
                Runtime.Notify($"金钱不足：{definition.DisplayName} 需要 {price}。", MessageKind.Warning);
                return null;
            }

            if (!IsWorkPositionOpen(gridPosition + CookerFacingUtility.Offset(facing)))
            {
                Runtime.Notify("正面操作格在厨房外或被设备挡住，请按 R 旋转或换个位置。", MessageKind.Warning);
                return null;
            }

            // CreateCooker re-checks affordability and performs the charge, so a failure here can
            // never leave a half built cooker or a deducted balance behind.
            return CreateCooker(definition, gridPosition, false, facing);
        }

        public bool IsWorkPositionOpen(Vector2Int cell, int ignoreCookerId = 0)
        {
            return Runtime.Grid.InBounds(cell) &&
                (Runtime.Grid.GetCookerId(cell) == 0 || Runtime.Grid.GetCookerId(cell) == ignoreCookerId);
        }

        public bool CanPlaceCooker(Vector2Int cell, CookerFacing facing)
        {
            return Runtime.Grid.IsFreeForCooker(cell) &&
                IsWorkPositionOpen(cell + CookerFacingUtility.Offset(facing));
        }

        public int CountWorkPositionUsers(Vector2Int cell)
        {
            int count = 0;
            for (int index = 0; index < cookers.Count; index++)
            {
                if (cookers[index].IsPlaced && cookers[index].WorkPosition == cell)
                {
                    count++;
                }
            }

            return count;
        }

        public string DescribeWorkPosition(CookerRuntime cooker)
        {
            if (!IsWorkPositionOpen(cooker.WorkPosition))
            {
                return "操作格受阻";
            }

            int staffId = Runtime.Grid.GetStaffId(cooker.WorkPosition);
            string status = staffId == 0 ? "空闲" :
                TryGetWorkingStaffId(cooker) == staffId ? "在岗" : "有人";
            int count = CountWorkPositionUsers(cooker.WorkPosition);
            return count > 1 ? $"{status} · 共用{count}台" : status;
        }

        public bool TryRotateCooker(CookerRuntime cooker)
        {
            if (cooker == null || cooker.Owner != Runtime || !cooker.IsPlaced)
            {
                return false;
            }

            if (cooker.Definition.WorkType == WorkType.Timed &&
                (cooker.LockedRecipe != null || cooker.HasAnyOutput ||
                 cooker.State == CookerState.Burnt || cooker.State == CookerState.Cleaning))
            {
                Runtime.Notify("请先完成加热、取出和清理，再旋转设备。", MessageKind.Warning);
                return false;
            }

            cooker.Facing = CookerFacingUtility.Clockwise(cooker.Facing);
            Runtime.Staff.ReleaseCooker(cooker.Id);
            Tick(0f);
            bool open = IsWorkPositionOpen(cooker.WorkPosition);
            Runtime.Notify(open ? $"设备朝{CookerFacingUtility.Label(cooker.Facing)}，请从正面操作。" :
                "正面操作格被挡或越界，请继续旋转或移动设备。", open ? MessageKind.Info : MessageKind.Warning);
            return true;
        }

        // ------------------------------------------------------------------ movement

        /// <summary>Moves a cooker to another cell, updating grid occupancy and the view.</summary>
        public bool TryMoveCooker(CookerRuntime cooker, Vector2Int target)
        {
            if (cooker == null || !cooker.IsPlaced)
            {
                return false;
            }

            if (cooker.Definition.WorkType == WorkType.Timed &&
                (cooker.LockedRecipe != null || cooker.HasAnyOutput ||
                 cooker.State == CookerState.Burnt || cooker.State == CookerState.Cleaning))
            {
                Runtime.Notify("请先完成加热、取出和清理，再移动设备。", MessageKind.Warning);
                return false;
            }

            if (!Runtime.Grid.InBounds(target))
            {
                Runtime.Notify("不能把厨具移到厨房之外。", MessageKind.Warning);
                return false;
            }

            if (target == cooker.GridPosition)
            {
                Runtime.Notify("厨具已经在该位置。", MessageKind.Info);
                return true;
            }

            if (!Runtime.Grid.IsFreeForCooker(target, cooker.Id))
            {
                Runtime.Notify("该格子已经有厨具了。", MessageKind.Warning);
                return false;
            }

            Vector2Int from = cooker.GridPosition;
            bool moved = Runtime.Grid.TryMoveCookerTraced(from, target, cooker.Id);
            if (!moved)
            {
                Runtime.Notify($"厨具无法移动。{Runtime.Grid.LastMoveTrace}", MessageKind.Warning);
                return false;
            }
            Vector3 world = Runtime.Grid.GridToWorld(target);
            cooker.SetPlacement(target, world, true);
            Runtime.Staff.ReleaseCooker(cooker.Id);

            if (cooker.View != null)
            {
                cooker.View.SnapTo(world);
            }

            // Moving releases the assignment; the employee must choose a new adjacent task.
            cooker.IsActive = false;
            cooker.ActiveStaffId = -1;

            Runtime.Notify($"{cooker.Definition.DisplayName} 已移动。", MessageKind.Info);
            return true;
        }

        // ------------------------------------------------------------------ removal

        /// <summary>Removes a cooker and frees its cell. Reserved stock is returned to the inventory.</summary>
        public bool TryRemoveCooker(CookerRuntime cooker)
        {
            if (cooker == null || !cooker.IsPlaced)
            {
                return false;
            }

            if (cooker.Definition.WorkType == WorkType.Timed &&
                (cooker.LockedRecipe != null || cooker.HasAnyOutput ||
                 cooker.State == CookerState.Burnt || cooker.State == CookerState.Cleaning))
            {
                Runtime.Notify("请先让员工完成取出或清理，再拆除加热设备。", MessageKind.Warning);
                return false;
            }

            if (cooker.Slots.Count > 0 && Runtime.Inventory != null)
            {
                Runtime.Inventory.Release(cooker.Slots);
            }

            cooker.ForgetLock();
            Runtime.Staff.ReleaseCooker(cooker.Id);

            if (cooker.HasAnyOutput && Runtime.Inventory != null)
            {
                cooker.CollectAllOutput();
            }

            Runtime.Grid.TryReleaseCooker(cooker.GridPosition, cooker.Id);

            if (cooker.View != null)
            {
                cooker.View.Dispose();
                cooker.SetView(null);
            }

            cookers.Remove(cooker);
            byId.Remove(cooker.Id);
            cooker.SetPlacement(cooker.GridPosition, cooker.WorldPosition, false);

            Runtime.Notify($"{cooker.Definition.DisplayName} 已拆除。", MessageKind.Info);
            return true;
        }

        // ------------------------------------------------------------------ ticking

        public override void Tick(float deltaTime)
        {
            // Attendance is refreshed from explicit tasks and arrived adjacent employees.
            for (int i = 0; i < cookers.Count; i++)
            {
                CookerRuntime cooker = cookers[i];
                if (!cooker.IsPlaced)
                {
                    continue;
                }

                int staffId = TryGetWorkingStaffId(cooker);
                bool active = staffId > 0;

                if (active != cooker.IsActive)
                {
                    cooker.IsActive = active;
                    cooker.ActiveStaffId = staffId;

                    if (!active)
                    {
                        // The work strategy keeps its progress; only the activation flag changes.
                        cooker.View?.SetActiveState(false);
                    }
                    else
                    {
                        cooker.View?.SetActiveState(true);
                    }
                }
                else if (active)
                {
                    cooker.ActiveStaffId = staffId;
                }

                cooker.View?.ApplyState();
            }
        }

        /// <summary>Formats the reserved inputs of a cooker for the UI.</summary>
        public string DescribeSlots(CookerRuntime cooker)
        {
            if (cooker == null || cooker.Slots.Count == 0)
            {
                return "-";
            }

            return Whitebox.Stacks(cooker.Slots);
        }

        /// <summary>
        /// Moves a cooker's finished produce into the shared kitchen stock.
        /// Routed through the manager so the UI never mutates a cooker directly.
        /// </summary>
        public bool TryCollectCookerOutput(CookerRuntime cooker)
        {
            if (cooker == null)
            {
                return false;
            }

            if (!cooker.HasAnyOutput)
            {
                Runtime.Notify("该厨具没有可收取的产出。", MessageKind.Info);
                return false;
            }

            bool assigned = Runtime.Staff.TryAssignCooker(Runtime.Staff.Selected, cooker);
            Runtime.Notify(assigned ? "员工正在处理设备产出。" : "请选择站在正面操作格的员工来处理产出。",
                assigned ? MessageKind.Info : MessageKind.Warning);
            return assigned;
        }

        /// <summary>Formats the produced output of a cooker for the UI.</summary>
        public string DescribeOutputs(CookerRuntime cooker)
        {
            if (cooker == null || cooker.Outputs.Count == 0)
            {
                return "-";
            }

            return Whitebox.Stacks(cooker.Outputs);
        }
    }
}
