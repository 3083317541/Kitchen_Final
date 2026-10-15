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

        /// <summary>Staff id standing on the cooker's cell, or -1 when the cooker is not activated.</summary>
        public int TryGetWorkingStaffId(CookerRuntime cooker)
        {
            if (cooker == null || !cooker.IsPlaced)
            {
                return -1;
            }

            int staffId = Runtime.Grid.GetStaffId(cooker.GridPosition);
            return staffId != 0 ? staffId : -1;
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

                CreateCooker(placement.Definition, placement.GridPosition, true);
            }
        }

        /// <summary>Creates a cooker without charging for it. Used for level setup and tests.</summary>
        public CookerRuntime CreateCooker(CookerDefinition definition, Vector2Int gridPosition, bool free)
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

            if (!free)
            {
                if (!Runtime.CanAfford(definition.Price))
                {
                    Runtime.Notify($"金钱不足，无法购买 {definition.DisplayName}（{definition.Price}）。", MessageKind.Warning);
                    return null;
                }

                Runtime.SpendMoney(definition.Price);
            }

            int id = nextCookerId++;
            Vector3 world = Runtime.Grid.GridToWorld(gridPosition);
            CookerRuntime cooker = new CookerRuntime(id, definition, gridPosition)
            {
                Owner = Runtime
            };

            if (!Runtime.Grid.TryOccupyCooker(gridPosition, id))
            {
                if (!free)
                {
                    Runtime.AddMoney(definition.Price);
                }

                return null;
            }

            cooker.SetPlacement(gridPosition, world, true);
            cookers.Add(cooker);
            byId.Add(id, cooker);

            CreateView(cooker);

            Runtime.Notify($"已放置 {definition.DisplayName}，花费 {definition.Price}。", MessageKind.Info);
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
        public CookerRuntime TryBuildCooker(CookerDefinition definition, Vector2Int gridPosition)
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

            if (!Runtime.CanAfford(definition.Price))
            {
                Runtime.Notify($"金钱不足：{definition.DisplayName} 需要 {definition.Price}。", MessageKind.Warning);
                return null;
            }

            // CreateCooker re-checks affordability and performs the charge, so a failure here can
            // never leave a half built cooker or a deducted balance behind.
            return CreateCooker(definition, gridPosition, false);
        }

        // ------------------------------------------------------------------ movement

        /// <summary>Moves a cooker to another cell, updating grid occupancy and the view.</summary>
        public bool TryMoveCooker(CookerRuntime cooker, Vector2Int target)
        {
            if (cooker == null || !cooker.IsPlaced)
            {
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

            if (cooker.View != null)
            {
                cooker.View.SnapTo(world);
            }

            // Occupancy changed, so the newly covering staff member re-activates the cooker next tick.
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

            if (cooker.Slots.Count > 0 && Runtime.Inventory != null)
            {
                Runtime.Inventory.Release(cooker.Slots);
            }

            cooker.ForgetLock();

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
            // Cooker activation is refreshed every tick from the grid, so it is impossible for a
            // cooker to keep working after its staff member has walked away.
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

            bool collected = cooker.CollectAllOutput();
            Runtime.Notify(
                collected ? "产出已收入厨房库存。" : "产出收取失败。",
                collected ? MessageKind.Success : MessageKind.Warning);

            return collected;
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
