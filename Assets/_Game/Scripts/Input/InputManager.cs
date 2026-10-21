using System;
using Kitchen.Cookers;
using Kitchen.Core;
using Kitchen.Data;
using Kitchen.Runtime;
using Kitchen.View;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Kitchen.Input
{
    /// <summary>
    /// The only place mouse and keyboard are read.
    ///
    /// It maintains the interaction mode, samples world and UI hits, and turns them into intents
    /// passed on as events. Managers never touch <c>Input</c> or <c>EventSystem</c> themselves.
    /// UI hits always win over world hits, so a button press can never also place a cooker or select
    /// an employee.
    /// </summary>
    [DisallowMultipleComponent]
    public class InputManager : MonoBehaviour
    {
        [Header("Layers")]
        [SerializeField] private LayerMask interactableMask;
        [SerializeField] private LayerMask groundMask;
        [SerializeField] private bool layersInitialized;

        [Header("Keys")]
        [SerializeField] private KeyCode cancelKey = KeyCode.Escape;
        [SerializeField] private KeyCode cancelMouseButton = KeyCode.Mouse1;

        [Header("Behaviour")]
        [Tooltip("Keyboard repeat delay while a movement key is held. 0 = discrete presses only.")]
        [SerializeField] private float moveRepeatDelay = 0.22f;

        // ------------------------------------------------------------------ mode state

        private InteractionMode mode = InteractionMode.Normal;
        private CookerDefinition pendingCooker;
        private CookerFacing pendingFacing;
        private CookerRuntime pendingMoveCooker;
        private ItemDefinition selectedIngredient;
        private PlacementPreview preview;
        private Camera worldCamera;

        /// <summary>
        /// Runtime this manager drives. Injected by GameRuntime so the pointer code never depends on
        /// which runtime happens to be the static instance.
        /// </summary>
        private Runtime.GameRuntime runtime;
        private float moveRepeatTimer;
        private readonly Vector2Int[] lastDirections = new Vector2Int[4];

        // ------------------------------------------------------------------ events

        /// <summary>Raised when the interaction mode changes, so the UI can update its hints.</summary>
        public event Action<InteractionMode> ModeChanged;

        /// <summary>Raised when the player confirmed a cooker purchase, before placement.</summary>
        public event Action<CookerDefinition> BuildRequested;

        /// <summary>Raised when the player cancelled an interaction. No money was spent.</summary>
        public event Action CancelRequested;
        public event Action PauseRequested;

        public bool PauseMenuOpen { get; private set; }
        private int pauseCloseFrame = -1;

        public void SetPauseMenuOpen(bool open)
        {
            if (PauseMenuOpen && !open) pauseCloseFrame = Time.frameCount;
            PauseMenuOpen = open;
            moveRepeatTimer = 0f;
            if (open) { ClearCellHighlights(); preview?.Hide(); }
        }

        public void RequestEscape()
        {
            if (!PauseMenuOpen && (mode != InteractionMode.Normal || RecipeMenuOpen))
                CancelCurrentInteraction();
            else
                PauseRequested?.Invoke();
        }

        /// <summary>Raised when the selected cooker should step one cell in a direction.</summary>
        public event Action<CookerRuntime, Vector2Int> MoveCookerRequested;

        /// <summary>Raised when the selected employee should step one cell.</summary>
        public event Action<Vector2Int> MoveStaffRequested;

        /// <summary>Raised when the player wants the cooker details panel opened.</summary>
        public event Action<CookerRuntime> CookerContextRequested;

        public event Action<CookerRuntime> CookerRecipeRequested;

        public bool RecipeMenuOpen { get; private set; }
        private int recipeCloseFrame = -1;

        public void SetRecipeMenuOpen(bool open)
        {
            if (RecipeMenuOpen && !open) recipeCloseFrame = Time.frameCount;
            RecipeMenuOpen = open;
            moveRepeatTimer = 0f;
            if (open) { ClearCellHighlights(); preview?.Hide(); }
        }

        public void RequestCookerRecipe(GameObject hitObject)
        {
            if (PauseMenuOpen || mode != InteractionMode.Normal || PointerOverUI || RecipeMenuOpen) return;
            CookerView view = hitObject != null ? hitObject.GetComponentInParent<CookerView>() : null;
            if (view != null && view.Runtime != null && view.Runtime.IsPlaced)
                CookerRecipeRequested?.Invoke(view.Runtime);
        }

        public event Action<CookerRuntime> RotateCookerRequested;

        /// <summary>Raised when the player picked an ingredient in the selection mode.</summary>
        public event Action<ItemDefinition> IngredientSelected;

        /// <summary>
        /// Raised when a number key asked for a cooker from the catalogue by index.
        /// InputManager only reports the intent; whoever listens decides what to do with it.
        /// </summary>
        public event Action<int> CookerPurchaseRequested;

        // ------------------------------------------------------------------ read-only state

        public InteractionMode Mode => mode;

        public CookerDefinition PendingCooker => pendingCooker;

        public CookerFacing PendingFacing => pendingFacing;

        public CookerRuntime PendingMoveCooker => pendingMoveCooker;

        public ItemDefinition SelectedIngredient => selectedIngredient;

        /// <summary>Cell under the pointer this frame, including out-of-bounds positions.</summary>
        public Vector2Int HoveredCell { get; private set; }

        /// <summary>Selectable object under the pointer this frame, or null.</summary>
        public GameObject HoveredObject { get; private set; }

        /// <summary>True when the pointer is over UI this frame. World input is suppressed while true.</summary>
        public bool PointerOverUI { get; private set; }

        /// <summary>
        /// Test seam: when true the UI hit test is replaced by <see cref="forcedPointerOverUi"/>.
        /// Production code never sets this, so the real EventSystem answer is used at runtime.
        /// </summary>
        private bool pointerOverUiOverride;
        private bool forcedPointerOverUi;

        /// <summary>
        /// Forces the pointer-over-UI answer for automated checks. Also assigns the property directly
        /// so a caller can assert the state without waiting for a frame. Not used by the game.
        /// </summary>
        public void SetPointerOverUiOverride(bool enabled, bool value)
        {
            pointerOverUiOverride = enabled;
            forcedPointerOverUi = value;
            PointerOverUI = enabled && value;
        }

        /// <summary>Legality of the hovered cell in build mode. Meaningless in other modes.</summary>
        public bool HoveredCellLegal { get; private set; }

        // ------------------------------------------------------------------ setup

        /// <summary>
        /// The runtime this manager acts on: the injected one, or the static instance as a fallback for
        /// a scene that was not booted through GameRuntime.
        /// </summary>
        private Runtime.GameRuntime ActiveRuntime => runtime != null ? runtime : Runtime.GameRuntime.Instance;

        /// <summary>Injects the camera and resolves the layers. Called by GameRuntime before the first tick.</summary>
        public void Initialize(Camera camera)
        {
            Initialize(camera, null);
        }

        /// <summary>
        /// Injects the camera and the runtime the input should act on. Passing the runtime explicitly
        /// keeps the manager independent of the static instance, which is what makes it testable.
        /// </summary>
        public void Initialize(Camera camera, Runtime.GameRuntime owner)
        {
            runtime = owner;
            worldCamera = camera != null ? camera : Camera.main;

            if (!layersInitialized)
            {
                interactableMask = GameLayers.InteractableMask;
                groundMask = GameLayers.GroundMask;
                layersInitialized = true;
            }

            SetMode(InteractionMode.Normal);
        }

        /// <summary>Lets an outside system override the sampled layers, e.g. after editing the project.</summary>
        public void SetLayerMasks(LayerMask interactables, LayerMask ground)
        {
            interactableMask = interactables;
            groundMask = ground;
            layersInitialized = true;
        }

        private void OnDisable()
        {
            ExitMoveCookerMode();
            ExitBuildMode();
        }

        // ------------------------------------------------------------------ mode transitions

        private void SetMode(InteractionMode next)
        {
            if (mode == next)
            {
                return;
            }

            mode = next;
            ModeChanged?.Invoke(mode);
        }

        /// <summary>Enters build mode for a cooker definition. The purchase is charged on placement.</summary>
        public void RequestBuildCooker(CookerDefinition definition)
        {
            if (PauseMenuOpen || definition == null)
            {
                return;
            }

            ExitMoveCookerMode();
            pendingCooker = definition;
            pendingFacing = CookerFacing.North;
            SetMode(InteractionMode.Build);

            if (preview == null)
            {
                preview = ViewFactory.CreatePlacementPreview(transform.parent);
            }

            BuildRequested?.Invoke(definition);
        }

        /// <summary>Leaves build mode without charging anything.</summary>
        public void ExitBuildMode()
        {
            pendingCooker = null;

            if (preview != null)
            {
                preview.Hide();
            }

            // Cancelling with Escape takes this path, so the highlight is cleared here too.
            ClearCellHighlights();

            if (mode == InteractionMode.Build)
            {
                SetMode(InteractionMode.Normal);
            }
        }

        /// <summary>Enters cooker relocation mode for a cooker.</summary>
        public void EnterMoveCookerMode(CookerRuntime cooker)
        {
            if (PauseMenuOpen || cooker == null)
            {
                return;
            }

            ExitBuildMode();
            pendingMoveCooker = cooker;
            SetMode(InteractionMode.MoveCooker);
        }

        /// <summary>Leaves cooker relocation mode.</summary>
        public void ExitMoveCookerMode()
        {
            pendingMoveCooker = null;

            if (mode == InteractionMode.MoveCooker)
            {
                SetMode(InteractionMode.Normal);
            }
        }

        /// <summary>Enters the ingredient selection mode for a cooker.</summary>
        public void EnterSelectIngredientMode(ItemDefinition item)
        {
            if (PauseMenuOpen) return;
            selectedIngredient = item;
            SetMode(item != null ? InteractionMode.SelectIngredient : InteractionMode.Normal);
        }

        /// <summary>Cancels whatever interaction is active. Safe to call from anywhere.</summary>
        public void CancelCurrentInteraction()
        {
            bool hadSomething = mode != InteractionMode.Normal || RecipeMenuOpen;
            RecipeMenuOpen = false;

            ExitBuildMode();
            ExitMoveCookerMode();
            selectedIngredient = null;

            if (mode != InteractionMode.Normal)
            {
                SetMode(InteractionMode.Normal);
            }

            if (hadSomething)
            {
                CancelRequested?.Invoke();
            }
        }

        // ------------------------------------------------------------------ per frame sampling

        /// <summary>
        /// Samples input once per frame, resolves what the pointer is over, then turns the result
        /// into intents. GameRuntime calls this before any manager ticks.
        /// </summary>
        public void Tick(float deltaTime, bool allowWorldInput = true)
        {
            if (UnityEngine.Input.GetKeyDown(cancelKey))
            {
                RequestEscape();
                return;
            }
            if (PauseMenuOpen || !allowWorldInput || pauseCloseFrame == Time.frameCount || recipeCloseFrame == Time.frameCount) return;
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            if (worldCamera == null)
            {
                return;
            }

            PointerOverUI = IsPointerOverUIThisFrame();

            // 1. Cancellation always wins, in every mode.
            if (UnityEngine.Input.GetKeyDown(cancelMouseButton) && (mode != InteractionMode.Normal || RecipeMenuOpen))
            {
                CancelCurrentInteraction();
                return;
            }

            // 2. Resolve what the pointer is over. UI hits suppress every world action below.
            SamplePointer(out Vector2Int cell, out GameObject hitObject);

            HoveredCell = cell;
            HoveredObject = hitObject;

            if (RecipeMenuOpen) return;
            if (UnityEngine.Input.GetKeyDown(cancelMouseButton))
            {
                RequestCookerRecipe(hitObject);
                return;
            }

            // 3. Keyboard movement, routed by the current mode.
            HandleMovementKeys(deltaTime);
            HandleCatalogueKeys();
            if (!PointerOverUI && UnityEngine.Input.GetKeyDown(KeyCode.R))
            {
                RequestRotate();
            }

            if (PointerOverUI)
            {
                HoveredCellLegal = false;

                // The pointer left the world, so the build highlight has to go with it. Returning early
                // without clearing left the last hovered cell green for as long as the UI was under the
                // cursor.
                ClearCellHighlights();
                preview?.Hide();
                return;
            }

            // 4. Click handling for the current mode.
            switch (mode)
            {
                case InteractionMode.Build:
                    UpdateBuildPreview(cell);
                    if (UnityEngine.Input.GetMouseButtonDown(0))
                    {
                        ConfirmBuild(cell);
                    }

                    break;

                case InteractionMode.SelectIngredient:
                    if (UnityEngine.Input.GetMouseButtonDown(0))
                    {
                        ConfirmIngredient();
                    }

                    break;

                case InteractionMode.MoveCooker:
                    if (UnityEngine.Input.GetMouseButtonDown(0))
                    {
                        ConfirmCookerMove(cell);
                    }

                    break;

                default:
                    if (UnityEngine.Input.GetMouseButtonDown(0))
                    {
                        HandleWorldSelection(hitObject, cell);
                    }

                    break;
            }
        }

        private bool IsPointerOverUIThisFrame()
        {
            return pointerOverUiOverride ? forcedPointerOverUi : IsPointerOverUi();
        }

        private static bool IsPointerOverUi()
        {
            EventSystem current = EventSystem.current;
            return current != null && current.IsPointerOverGameObject();
        }

        private void SamplePointer(out Vector2Int cell, out GameObject hitObject)
        {
            cell = new Vector2Int(-1, -1);
            hitObject = null;

            if (PointerOverUI)
            {
                return;
            }

            Ray ray = worldCamera.ScreenPointToRay(UnityEngine.Input.mousePosition);

            // Selectable objects are tested first so a click on a cooker selects the cooker even
            // though the cooker also stands on a ground cell.
            if (Physics.Raycast(ray, out RaycastHit selectableHit, 400f, interactableMask, QueryTriggerInteraction.Ignore))
            {
                hitObject = selectableHit.collider.gameObject;
            }

            cell = ResolvePointerCell(ray);
        }

        private Vector2Int ResolvePointerCell(Ray ray)
        {
            Runtime.GameRuntime runtime = ActiveRuntime;
            if (runtime != null && runtime.Grid != null)
            {
                Plane ground = new Plane(Vector3.up, new Vector3(0f, runtime.Grid.Math.GroundY, 0f));
                return ground.Raycast(ray, out float distance)
                    ? runtime.Grid.WorldToGrid(ray.GetPoint(distance)) : new Vector2Int(-1, -1);
            }

            return Physics.Raycast(ray, out RaycastHit hit, 400f, groundMask, QueryTriggerInteraction.Ignore)
                ? WorldToGrid(hit.point) : new Vector2Int(-1, -1);
        }

        private Vector2Int WorldToGrid(Vector3 world)
        {
            Runtime.GameRuntime runtime = ActiveRuntime;
            return runtime != null && runtime.Grid != null ? runtime.Grid.WorldToGrid(world) : Vector2Int.zero;
        }

        // ------------------------------------------------------------------ mode actions

        /// <summary>
        /// Shows the build ghost and the cell highlight under the pointer.
        ///
        /// The highlight is cleared before the new one is applied. Setting only the new cell left every
        /// cell the pointer had crossed still green, which read as a stuck selection.
        /// </summary>
        public void UpdateBuildPreview(Vector2Int cell)
        {
            Runtime.GameRuntime runtime = ActiveRuntime;
            if (runtime == null || runtime.Grid == null)
            {
                return;
            }

            bool inBounds = runtime.Grid.InBounds(cell);
            bool free = inBounds && runtime.Grid.IsFreeForCooker(cell);

            // A missing pending definition means the highlight is being driven on its own; the cell is
            // still legal in that case, so the highlight logic does not depend on the purchase state.
            bool affordable = pendingCooker == null || runtime.CanAfford(runtime.GetCookerPrice(pendingCooker));
            bool legal = inBounds && free && affordable && runtime.Cookers.CanPlaceCooker(cell, pendingFacing);

            HoveredCellLegal = legal;

            // The ghost is optional: a scene without a placement preview still gets the highlight, and
            // the highlight is the part the player reads as "this cell can take a cooker".
            if (preview != null && pendingCooker != null)
            {
                Vector3 world = runtime.Grid.GridToWorld(
                    inBounds ? cell : runtime.Grid.WorldToGridClamped(worldCamera.transform.position));
                preview.Show(pendingCooker, world, cell);
                preview.SetValid(legal, cell);
                Vector2Int workCell = cell + CookerFacingUtility.Offset(pendingFacing);
                preview.ShowWorkPosition(pendingFacing, runtime.Grid.CellSize,
                    runtime.Cookers.IsWorkPositionOpen(workCell), runtime.Grid.HasStaff(workCell));
            }

            ClearCellHighlights();

            if (runtime.Grid.View != null)
            {
                runtime.Grid.View.SetHighlight(cell, true, legal);
            }
        }

        /// <summary>
        /// Restores every highlighted cell to its normal look. The build preview owns the highlight, so
        /// it is dropped whenever the pointer moves to another cell, hovers the UI, or build mode ends.
        /// </summary>
        public void ClearCellHighlights()
        {
            Runtime.GameRuntime runtime = ActiveRuntime;
            if (runtime != null && runtime.Grid != null && runtime.Grid.View != null)
            {
                runtime.Grid.View.ClearHighlights();
            }
        }

        private void ConfirmBuild(Vector2Int cell)
        {
            Runtime.GameRuntime runtime = ActiveRuntime;
            if (runtime == null || pendingCooker == null)
            {
                return;
            }

            CookerDefinition definition = pendingCooker;
            bool placed = runtime.TryBuildCooker(definition, cell, pendingFacing);

            if (runtime.Grid != null && runtime.Grid.View != null)
            {
                runtime.Grid.View.ClearHighlights();
            }

            if (placed)
            {
                // TryBuildCooker already exited build mode; make sure the ghost is gone too.
                ExitBuildMode();
            }
        }

        private void ConfirmCookerMove(Vector2Int cell)
        {
            Runtime.GameRuntime runtime = ActiveRuntime;
            if (runtime == null || pendingMoveCooker == null)
            {
                return;
            }

            Vector2Int from = pendingMoveCooker.GridPosition;
            Vector2Int delta = cell - from;

            if (delta == Vector2Int.zero)
            {
                runtime.Notify("Pick a different cell for the cooker.", MessageKind.Warning);
                return;
            }

            if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) != 1)
            {
                // Relocation is one adjacent cell at a time so occupancy stays easy to reason about.
                runtime.Notify("A cooker can only move to an adjacent cell.", MessageKind.Warning);
                return;
            }

            runtime.TryMoveCooker(pendingMoveCooker, delta);
        }

        private void ConfirmIngredient()
        {
            if (selectedIngredient == null)
            {
                return;
            }

            IngredientSelected?.Invoke(selectedIngredient);
            ExitSelectIngredientMode();
        }

        /// <summary>Leaves the ingredient selection mode.</summary>
        public void ExitSelectIngredientMode()
        {
            selectedIngredient = null;
            if (mode == InteractionMode.SelectIngredient)
            {
                SetMode(InteractionMode.Normal);
            }
        }

        private void HandleWorldSelection(GameObject hitObject, Vector2Int cell)
        {
            Runtime.GameRuntime runtime = ActiveRuntime;
            if (runtime == null)
            {
                return;
            }

            if (hitObject != null)
            {
                CookerView cookerView = hitObject.GetComponentInParent<CookerView>();
                if (cookerView != null && cookerView.Runtime != null)
                {
                    CookerContextRequested?.Invoke(cookerView.Runtime);
                    return;
                }

                StaffView staffView = hitObject.GetComponentInParent<StaffView>();
                if (staffView != null && staffView.Runtime != null)
                {
                    runtime.HandleStaffClicked(staffView.Runtime);
                    return;
                }
            }

            runtime.HandleEmptyCellClicked(cell);
        }

        // ------------------------------------------------------------------ keyboard

        public void RequestRotate()
        {
            if (PointerOverUI)
            {
                return;
            }

            if (mode == InteractionMode.Build)
            {
                pendingFacing = CookerFacingUtility.Clockwise(pendingFacing);
                UpdateBuildPreview(HoveredCell);
                return;
            }

            if (mode == InteractionMode.Normal || mode == InteractionMode.MoveCooker)
            {
                CookerRuntime cooker = mode == InteractionMode.MoveCooker ? pendingMoveCooker : ActiveRuntime?.Cookers.Selected;
                if (cooker != null)
                {
                    RotateCookerRequested?.Invoke(cooker);
                }
            }
        }

        private void HandleMovementKeys(float deltaTime)
        {
            Vector2Int direction = ReadDirection();

            if (direction == Vector2Int.zero)
            {
                moveRepeatTimer = 0f;
                ClearHeldDirections();
                return;
            }

            bool held = IsHeld(direction);
            if (held && moveRepeatTimer > 0f)
            {
                moveRepeatTimer -= deltaTime;
                return;
            }

            moveRepeatTimer = moveRepeatDelay;
            DispatchMove(direction);
        }

        private Vector2Int ReadDirection()
        {
            if (UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.UpArrow))
            {
                return new Vector2Int(0, 1);
            }

            if (UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.DownArrow))
            {
                return new Vector2Int(0, -1);
            }

            if (UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow))
            {
                return new Vector2Int(-1, 0);
            }

            if (UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow))
            {
                return new Vector2Int(1, 0);
            }

            return Vector2Int.zero;
        }

        private bool IsHeld(Vector2Int direction)
        {
            // A direction counts as held when the key was already down before this frame.
            if (direction == new Vector2Int(0, 1))
            {
                return !UnityEngine.Input.GetKeyDown(KeyCode.W) && !UnityEngine.Input.GetKeyDown(KeyCode.UpArrow);
            }

            if (direction == new Vector2Int(0, -1))
            {
                return !UnityEngine.Input.GetKeyDown(KeyCode.S) && !UnityEngine.Input.GetKeyDown(KeyCode.DownArrow);
            }

            if (direction == new Vector2Int(-1, 0))
            {
                return !UnityEngine.Input.GetKeyDown(KeyCode.A) && !UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow);
            }

            return !UnityEngine.Input.GetKeyDown(KeyCode.D) && !UnityEngine.Input.GetKeyDown(KeyCode.RightArrow);
        }

        private void ClearHeldDirections()
        {
            for (int i = 0; i < lastDirections.Length; i++)
            {
                lastDirections[i] = Vector2Int.zero;
            }
        }

        /// <summary>Number keys 1-4 request the matching cooker from the catalogue.</summary>
        private void HandleCatalogueKeys()
        {
            if (PointerOverUI)
            {
                return;
            }

            KeyCode[] keys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4 };
            for (int i = 0; i < keys.Length; i++)
            {
                if (UnityEngine.Input.GetKeyDown(keys[i]))
                {
                    CookerPurchaseRequested?.Invoke(i);
                    return;
                }
            }
        }

        private void DispatchMove(Vector2Int direction)
        {
            if (mode == InteractionMode.MoveCooker)
            {
                if (pendingMoveCooker != null)
                {
                    MoveCookerRequested?.Invoke(pendingMoveCooker, direction);
                }

                return;
            }

            if (mode == InteractionMode.Build || mode == InteractionMode.SelectIngredient)
            {
                return;
            }

            MoveStaffRequested?.Invoke(direction);
        }
    }
}
