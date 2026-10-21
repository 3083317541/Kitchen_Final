using System.Collections.Generic;
using Kitchen.Cookers;
using Kitchen.Core;
using Kitchen.Data;
using Kitchen.Entities;
using Kitchen.Grid;
using Kitchen.Input;
using Kitchen.Inventory;
using Kitchen.Orders;
using Kitchen.Recipes;
using Kitchen.Staff;
using Kitchen.UI;
using Kitchen.View;
using Kitchen.Work;
using UnityEngine;

namespace Kitchen.Runtime
{
    /// <summary>
    /// Severity of a player facing message.
    /// </summary>
    public enum MessageKind
    {
        Info = 0,
        Success = 1,
        Warning = 2,
        Error = 3
    }

    /// <summary>
    /// The single entry point of the game flow.
    ///
    /// It initialises every system, loads the level, creates the grid / staff / cookers / stock /
    /// orders, receives player intent from <see cref="InputManager"/> and dispatches it to the right
    /// manager, then advances work, plate returns, orders and UI in a fixed order.
    ///
    /// Managers never search the scene: every reference is either assigned in the inspector or
    /// injected here before the first tick.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameRuntime : MonoBehaviour
    {
        /// <summary>
        /// The active runtime. InputManager uses it to translate a pointer ray into a grid cell.
        /// Only one runtime may be active at a time.
        /// </summary>
        public static GameRuntime Instance { get; private set; }

        // ------------------------------------------------------------------ inspector wiring

        [Header("Configuration")]
        [Tooltip("Level to load. When empty the built-in default content is used, so the demo always runs.")]
        [SerializeField] private LevelConfig levelConfig;
        [SerializeField] private bool loadFromResourcesWhenEmpty = true;
        [Tooltip("Resources path used when no level asset is assigned.")]
        [SerializeField] private string resourcesLevelPath = "Kitchen/Level_01";

        [Header("Slots")]
        [SerializeField] private float initialTimeScale = 1f;
        [SerializeField] private float staffMoveSpeed = 6f;
        [SerializeField] private float plateReturnMinSeconds = 10f;
        [SerializeField] private float plateReturnMaxSeconds = 20f;

        [Header("Scene references")]
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Transform floorRoot;
        [SerializeField] private Transform gridRoot;
        [SerializeField] private Transform cookerRoot;
        [SerializeField] private Transform staffRoot;
        [SerializeField] private Transform ingredientRoot;

        [Header("Input")]
        [SerializeField] private InputManager inputManager;

        [Header("UI")]
        [SerializeField] private UIManager uiManager;

        [Header("Level flow")]
        [Tooltip("Owns which level is built and what happens when it is cleared.")]
        [SerializeField] private Kitchen.Level.LevelManager levelManager;

        [Header("Systems")]
        [SerializeField] private GridManager gridManager;
        [SerializeField] private CookerManager cookerManager;
        [SerializeField] private StaffManager staffManager;
        [SerializeField] private WorkManager workManager;
        [SerializeField] private InventoryManager inventoryManager;
        [SerializeField] private RecipeManager recipeManager;
        [SerializeField] private OrderManager orderManager;
        [SerializeField] private PlateReturnManager plateReturnManager;

        // ------------------------------------------------------------------ runtime state

        private bool initialized;
        private bool levelStarted;
        private float resumeTimeScale = 1f;
        private LevelConfig classicLevelInstance;
        public ClassicDayController Classic { get; private set; }
        public bool TutorialBriefingOpen { get; private set; }
        public bool IsPaused { get; private set; }
        public bool CanArrangeKitchen => !IsPaused && (Classic == null || Classic.CanArrange);
        public bool CanOperateKitchen => !IsPaused && !TutorialBriefingOpen && (Classic == null || Classic.Phase == DayPhase.Service);

        /// <summary>Money the player currently holds.</summary>
        public int Money { get; private set; }

        /// <summary>Seconds since the level started, scaled by the current time scale.</summary>
        public float ElapsedTime { get; private set; }

        /// <summary>Level configuration in use. Never null once initialised.</summary>
        public LevelConfig Level { get; private set; }

        /// <summary>Content created in code when no level asset was available.</summary>
        public GameContentFactory.Content FallbackContent { get; private set; }

        public GridManager Grid => gridManager;

        public CookerManager Cookers => cookerManager;

        public StaffManager Staff => staffManager;

        public WorkManager Work => workManager;

        public InventoryManager Inventory => inventoryManager;

        public RecipeManager Recipes => recipeManager;

        public OrderManager Orders => orderManager;

        public PlateReturnManager PlateReturns => plateReturnManager;

        public Kitchen.Level.LevelManager Levels => levelManager;

        public InputManager Input => inputManager;

        public UIManager UI => uiManager;

        public Camera WorldCamera => worldCamera;

        public Transform IngredientRoot => ingredientRoot;

        public bool IsInitialized => initialized;

        /// <summary>Definition of a clean plate, resolved once from the level catalogue.</summary>
        public ItemDefinition CleanPlate { get; private set; }

        /// <summary>Definition of a dirty plate, resolved once from the level catalogue.</summary>
        public ItemDefinition DirtyPlate { get; private set; }

        /// <summary>Raised whenever the notification system receives a message.</summary>
        public event System.Action<string, MessageKind> MessageRaised;

        // ------------------------------------------------------------------ lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[GameRuntime] A second GameRuntime was found; the newer one is ignored.");
                enabled = false;
                return;
            }

            Instance = this;
            Boot();
        }

        private void Start()
        {
            StartLevel();
        }

        private void OnDestroy()
        {
            if (IsPaused) Time.timeScale = resumeTimeScale;
            ShutdownSystems();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Builds every system, injects dependencies and creates the level. Split out of Awake so
        /// automated tests can drive the same code path explicitly.
        /// </summary>
        public void Boot()
        {
            if (initialized)
            {
                return;
            }

            Time.timeScale = Mathf.Max(0.01f, initialTimeScale);

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            if (worldCamera == null)
            {
                Debug.LogError("[GameRuntime] No camera found. Clicking the world will not work.");
            }

            // 1. Input first: it needs nothing else, and every later system may want to read a mode.
            if (inputManager != null)
            {
                inputManager.Initialize(worldCamera, this);
            }

            // 2. Systems. Order matters: the grid exists before anything can be placed on it.
            gridManager?.Initialize(this);
            inventoryManager?.Initialize(this);
            recipeManager?.Initialize(this);
            cookerManager?.Initialize(this);
            staffManager?.Initialize(this);
            workManager?.Initialize(this);
            orderManager?.Initialize(this);
            plateReturnManager?.Initialize(this);

            if (cookerManager != null)
            {
                cookerManager.SetWorldRoots(cookerRoot);
            }

            if (gridManager != null)
            {
                gridManager.SetWorldRoots(gridRoot);
            }

            if (staffManager != null)
            {
                staffManager.SetWorldRoots(staffRoot);
                staffManager.SetMoveSpeed(staffMoveSpeed);
            }

            if (plateReturnManager != null)
            {
                plateReturnManager.Configure(plateReturnMinSeconds, plateReturnMaxSeconds);
            }

            // Materials: the asset backed library is preferred so prefabs keep real material
            // references. Without it Whitebox falls back to building materials in memory.
            WhiteboxMaterialLibrary.Load();

            // 3. Level content. The level manager decides which level the campaign is on.
            levelManager?.Initialize(this);
            Level = ResolveLevelConfig();
            if (Level == null)
            {
                Debug.LogError("[GameRuntime] Could not resolve a LevelConfig; the kitchen cannot start.");
                return;
            }

            if (Level.IsClassicMode)
            {
                if (ClassicProgress.Current == null) ClassicProgress.Begin();
                Classic = new ClassicDayController(this, ClassicProgress.Current, Level.ServiceDuration);
                classicLevelInstance = Classic.PrepareLevel(Level);
                Level = classicLevelInstance;
                staffManager?.SetMoveSpeed(staffMoveSpeed * Classic.Run.MoveMultiplier);
            }
            ApplyLevelContent(Level);
            gridManager?.BuildGrid(Level);

            // 4. World geometry.
            BuildWorld();

            if (gridManager != null)
            {
                gridManager.SetWorldRoots(gridRoot);
            }

            // 5. Grid, staff, cookers, stock.
            if (gridManager != null && gridManager.CellCount == 0)
            {
                Debug.LogError("[GameRuntime] The grid is empty; check the LevelConfig dimensions.");
            }

            inventoryManager?.ApplyStartingInventory(Level);

            cookerManager?.SpawnLevelCookers(Level);
            staffManager?.SpawnLevelStaff(Level);
            if (Classic != null && Classic.Run.HasSnapshot)
            {
                for (int index = 0; index < cookerManager.Cookers.Count && index < Classic.Run.PreferredRecipes.Count; index++)
                    cookerManager.Cookers[index].PreferredRecipeId = Classic.Run.PreferredRecipes[index];
            }

            // The grid was created empty, so every level placed object is registered on it once both
            // owners have finished creating their objects.
            gridManager?.RebuildOccupancyFromManagers();

            // 6. UI last, so everything it reads already exists.
            if (uiManager != null)
            {
                uiManager.Initialize(this);
                uiManager.BindInput(inputManager);
            }

            Money = Level.StartingMoney;

            if (inputManager != null)
            {
                inputManager.BuildRequested += OnBuildRequested;
                inputManager.CancelRequested += OnCancelRequested;
                inputManager.PauseRequested += OnPauseRequested;
                inputManager.MoveCookerRequested += OnMoveCookerRequested;
                inputManager.MoveStaffRequested += OnMoveStaffRequested;
                inputManager.CookerContextRequested += OnCookerContextRequested;
                inputManager.RotateCookerRequested += OnRotateCookerRequested;
            }

            initialized = true;
            Notify($"厨房就绪：{staffManager?.Staff.Count ?? 0} 名厨师当班。", MessageKind.Info);
        }

        /// <summary>Issues the first order. Called from Start so the scene needs no manual stepping.</summary>
        public void StartLevel()
        {
            if (!initialized)
            {
                Debug.LogWarning("[GameRuntime] StartLevel called before Boot completed.");
                return;
            }

            if (levelStarted) return;
            levelStarted = true;
            TutorialBriefingOpen = Classic == null && uiManager != null && Level.TutorialSteps.Count > 0;
            if (Classic == null && !TutorialBriefingOpen) orderManager?.StartFirstOrder();
            uiManager?.RefreshAll();
        }

        public bool BeginTutorialPractice()
        {
            if (IsPaused || !TutorialBriefingOpen) return false;
            TutorialBriefingOpen = false;
            orderManager?.StartFirstOrder();
            uiManager?.RefreshAll();
            return true;
        }

        public bool BeginClassicService()
        {
            bool started = !IsPaused && Classic != null && Classic.BeginService();
            if (started) uiManager?.RefreshAll();
            return started;
        }

        public bool ChooseClassicSkill(KitchenSkill skill)
        {
            if (IsPaused || Classic == null || !Classic.ChooseSkill(skill)) return false;
            UnityEngine.SceneManagement.SceneManager.LoadScene(Kitchen.Level.LevelManager.KitchenSceneName);
            return true;
        }

        public void ReturnToMainMenu()
        {
            SetPaused(false);
            ClassicProgress.Leave();
            UnityEngine.SceneManagement.SceneManager.LoadScene(Kitchen.Level.LevelManager.MenuSceneName);
        }

        public void QuitGame()
        {
            SetPaused(false);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void SetPaused(bool paused)
        {
            if (IsPaused == paused) return;
            if (paused) inputManager?.CancelCurrentInteraction();
            IsPaused = paused;
            if (paused)
            {
                resumeTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            else Time.timeScale = resumeTimeScale;
            inputManager?.SetPauseMenuOpen(paused);
            uiManager?.PauseMenu?.SetVisible(paused);
            uiManager?.RefreshAll();
        }

        private void OnPauseRequested()
        {
            if (uiManager != null && uiManager.PauseMenu != null) uiManager.PauseMenu.HandleEscape();
            else SetPaused(!IsPaused);
        }

        private LevelConfig ResolveLevelConfig()
        {
            if (ClassicProgress.Current != null)
            {
                LevelConfig classicLevel = Resources.Load<LevelConfig>("Kitchen/Level_Classic");
                if (classicLevel != null) return classicLevel;
                Debug.LogError("[GameRuntime] Classic mode configuration is missing.");
                ClassicProgress.Leave();
            }
            // The campaign position wins, so the menu can start any level. The inspector value is the
            // fallback for a scene that has no level manager at all.
            LevelConfig campaignLevel = levelManager != null ? levelManager.ResolveLevel() : null;
            if (campaignLevel != null)
            {
                levelConfig = campaignLevel;
                return campaignLevel;
            }

            if (levelConfig != null)
            {
                return levelConfig;
            }

            if (loadFromResourcesWhenEmpty && !string.IsNullOrEmpty(resourcesLevelPath))
            {
                string path = CampaignProgress.LevelResourcesPath(CampaignProgress.LevelNumber);
                LevelConfig loaded = Resources.Load<LevelConfig>(path);
                if (loaded != null)
                {
                    levelConfig = loaded;
                    return loaded;
                }
            }

            // No prepared asset: build the default content in code so the scene is always playable.
            Debug.Log("[GameRuntime] No LevelConfig asset assigned; using the built-in default content.");
            FallbackContent = GameContentFactory.CreateRuntimeContent();
            levelConfig = FallbackContent.Level;
            return levelConfig;
        }

        private void ApplyLevelContent(LevelConfig level)
        {
            recipeManager?.ApplyLevel(level);
            cookerManager?.ApplyLevel(level);
            orderManager?.ApplyLevel(level);

            CleanPlate = level.FindItem(GameContentFactory.Ids.CleanPlate);
            DirtyPlate = level.FindItem(GameContentFactory.Ids.DirtyPlate);

            if (CleanPlate == null || DirtyPlate == null)
            {
                Debug.LogWarning("[GameRuntime] The level catalogue has no plate items; deliveries will fail.");
            }
        }

        private void BuildWorld()
        {
            // Fall back to names inside World/ so the scene still assembles if a slot is unassigned.
            if (gridRoot == null || cookerRoot == null || staffRoot == null || ingredientRoot == null || floorRoot == null)
            {
                Transform world = transform.parent != null ? transform.parent.Find("World") : null;
                if (world != null)
                {
                    floorRoot ??= world.Find("Floor");
                    gridRoot ??= world.Find("GridRoot");
                    cookerRoot ??= world.Find("CookerRoot");
                    staffRoot ??= world.Find("StaffRoot");
                    ingredientRoot ??= world.Find("IngredientRoot");
                }
            }

            if (gridManager != null && gridRoot != null)
            {
                gridManager.BuildGridVisuals(Level, gridRoot);
            }
            else if (gridManager != null)
            {
                Debug.LogWarning("[GameRuntime] No GridRoot assigned; the cell highlights will not be created.");
            }
        }

        private void Update()
        {
            if (!initialized)
            {
                return;
            }

            AdvanceFrame(Time.unscaledDeltaTime, IsPaused ? 1f : Time.timeScale);
        }

        /// <summary>
        /// Advances every system exactly once, in the documented order, for a given amount of
        /// unscaled time.
        ///
        /// This is the single place that defines the update order, so nothing else in the project
        /// decides sequencing. It is public so an automated run can pump the simulation with a
        /// chosen step size instead of depending on the frame rate.
        /// </summary>
        /// <param name="unscaledDeltaTime">Seconds to advance.</param>
        /// <param name="timeScale">Multiplier applied to the step. 1 = real time.</param>
        public void AdvanceFrame(float unscaledDeltaTime, float timeScale = 1f)
        {
            if (!initialized)
            {
                return;
            }

            float deltaTime = unscaledDeltaTime * Mathf.Max(0f, timeScale);
            if (deltaTime <= 0f)
            {
                return;
            }

            bool wasPaused = IsPaused;
            inputManager?.Tick(deltaTime, !TutorialBriefingOpen && (Classic == null || Classic.CanArrange));
            if (wasPaused || IsPaused) return;

            if (TutorialBriefingOpen || (Classic != null && Classic.Phase != DayPhase.Service))
            {
                if (!TutorialBriefingOpen && Classic.CanArrange)
                {
                    staffManager?.Tick(deltaTime);
                    cookerManager?.Tick(0f);
                }
                uiManager?.Tick(deltaTime);
                return;
            }
            if (Classic != null) deltaTime = Mathf.Min(deltaTime, Classic.RemainingSeconds);
            ElapsedTime += deltaTime;

            // Fixed advancement order: cookers work, plates come back, orders age, characters walk,
            // then activation is refreshed from the grid before the UI reads the result.
            workManager?.Tick(deltaTime);
            plateReturnManager?.Tick(deltaTime);
            orderManager?.Tick(deltaTime);
            staffManager?.Tick(deltaTime);
            cookerManager?.Tick(deltaTime);

            Classic?.Tick(deltaTime);

            uiManager?.Tick(deltaTime);
        }

        private void ShutdownSystems()
        {
            if (inputManager != null)
            {
                inputManager.BuildRequested -= OnBuildRequested;
                inputManager.CancelRequested -= OnCancelRequested;
                inputManager.PauseRequested -= OnPauseRequested;
                inputManager.MoveCookerRequested -= OnMoveCookerRequested;
                inputManager.MoveStaffRequested -= OnMoveStaffRequested;
                inputManager.CookerContextRequested -= OnCookerContextRequested;
                inputManager.RotateCookerRequested -= OnRotateCookerRequested;
            }

            workManager?.Shutdown();
            orderManager?.Shutdown();
            plateReturnManager?.Shutdown();
            cookerManager?.Shutdown();
            staffManager?.Shutdown();
            gridManager?.Shutdown();
            recipeManager?.Shutdown();
            inventoryManager?.Shutdown();
            if (classicLevelInstance != null)
            {
                if (Application.isPlaying) Destroy(classicLevelInstance);
                else DestroyImmediate(classicLevelInstance);
            }
            initialized = false;
        }

        // ------------------------------------------------------------------ money

        public int GetCookerPrice(CookerDefinition definition)
        {
            if (definition == null) return 0;
            return Classic != null ? Classic.Run.CookerPrice(definition, Level.ClassicCookerPriceIncreasePercent) : definition.Price;
        }

        public bool CanAfford(int price)
        {
            return Money >= price;
        }

        public void AddMoney(int amount)
        {
            if (amount == 0)
            {
                return;
            }

            Money = Mathf.Max(0, Money + amount);
        }

        public bool SpendMoney(int amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            if (!CanAfford(amount))
            {
                return false;
            }

            Money -= amount;
            return true;
        }

        // ------------------------------------------------------------------ messaging

        /// <summary>Sends a message to the notification panel. Managers use this instead of touching UI.</summary>
        public void Notify(string message, MessageKind kind = MessageKind.Info)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            MessageRaised?.Invoke(message, kind);
        }

        // ------------------------------------------------------------------ intent handling

        /// <summary>Called by InputManager when a staff member was clicked in the world.</summary>
        public void HandleStaffClicked(StaffRuntime staff)
        {
            if (!CanArrangeKitchen || TutorialBriefingOpen) return;
            if (staff == null)
            {
                return;
            }

            staffManager?.Select(staff);
            cookerManager?.SelectCooker(null);
            Notify($"已选中 {staff.DisplayName}：WASD 移动，点击相邻设备指定任务。", MessageKind.Info);
        }

        /// <summary>Called by InputManager when a cooker was clicked in the world.</summary>
        public void HandleCookerClicked(CookerRuntime cooker)
        {
            if (!CanArrangeKitchen || TutorialBriefingOpen) return;
            if (cooker == null)
            {
                return;
            }

            cookerManager?.SelectCooker(cooker);
            if (CanOperateKitchen && staffManager != null && staffManager.Selected != null &&
                staffManager.TryAssignCooker(staffManager.Selected, cooker))
            {
                Notify($"{staffManager.Selected.DisplayName} 负责 {cooker.Definition.DisplayName}。", MessageKind.Info);
            }
        }

        /// <summary>Called by InputManager when the clicked cell holds nothing selectable.</summary>
        public void HandleEmptyCellClicked(Vector2Int cell)
        {
            if (inputManager == null || inputManager.Mode != InteractionMode.Normal)
            {
                return;
            }

            // In normal mode an empty click clears the selection, which is the least surprising
            // behaviour and keeps the panels from showing stale data.
            cookerManager?.SelectCooker(null);
            staffManager?.Select(null);
        }

        /// <summary>Player asked to buy and place a cooker. Returns true when it was placed.</summary>
        public bool TryBuildCooker(CookerDefinition definition, Vector2Int cell, CookerFacing facing = CookerFacing.North)
        {
            if (!CanArrangeKitchen || TutorialBriefingOpen) return false;
            if (definition == null)
            {
                return false;
            }

            CookerRuntime created = cookerManager != null ? cookerManager.TryBuildCooker(definition, cell, facing) : null;
            if (created != null)
            {
                inputManager?.ExitBuildMode();
                uiManager?.RefreshAll();
                return true;
            }

            return false;
        }

        /// <summary>Player asked to move the selected cooker one cell.</summary>
        public bool TryMoveCooker(CookerRuntime cooker, Vector2Int direction)
        {
            if (!CanArrangeKitchen || TutorialBriefingOpen) return false;
            if (cooker == null || direction == Vector2Int.zero)
            {
                return false;
            }

            return cookerManager != null && cookerManager.TryMoveCooker(cooker, cooker.GridPosition + direction);
        }

        /// <summary>Player asked to move the selected chef one cell.</summary>
        public bool TryMoveSelectedStaff(Vector2Int direction)
        {
            if (!CanArrangeKitchen || TutorialBriefingOpen) return false;
            return staffManager != null && staffManager.TryMoveSelected(direction);
        }

        public bool TryRotateCooker(CookerRuntime cooker)
        {
            if (!CanArrangeKitchen || TutorialBriefingOpen) return false;
            return cookerManager != null && cookerManager.TryRotateCooker(cooker);
        }

        /// <summary>Player asked to delete the selected cooker.</summary>
        public bool TrySellCooker(CookerRuntime cooker)
        {
            if (!CanArrangeKitchen || TutorialBriefingOpen) return false;
            if (cooker == null)
            {
                return false;
            }

            return cookerManager != null && cookerManager.TryRemoveCooker(cooker);
        }

        public bool TrySetCookerRecipe(CookerRuntime cooker, string recipeId)
        {
            if (!CanArrangeKitchen || TutorialBriefingOpen) return false;
            if (cooker == null || cooker.Owner != this || !cooker.IsPlaced || cooker.Definition == null || recipeManager == null)
                return false;
            string caption = "自动";
            if (!string.IsNullOrEmpty(recipeId))
            {
                RecipeDefinition recipe = recipeManager.RecipesFor(cooker.Definition.Kind).Find(option => option.Id == recipeId);
                if (recipe == null) return false;
                caption = recipe.DisplayName;
            }
            cooker.PreferredRecipeId = string.IsNullOrEmpty(recipeId) ? null : recipeId;
            Notify($"加工方式：{caption}" + (cooker.LockedRecipe != null ? "，当前批次完成后生效。" : "。"), MessageKind.Info);
            uiManager?.RefreshValues();
            return true;
        }

        /// <summary>Player asked to collect the produce sitting in a cooker.</summary>
        public bool TryCollectCookerOutput(CookerRuntime cooker)
        {
            if (!CanOperateKitchen) return false;
            if (cooker == null)
            {
                return false;
            }

            return cookerManager != null && cookerManager.TryCollectCookerOutput(cooker);
        }

        /// <summary>Player asked to serve the active order.</summary>
        public bool TrySubmitOrder()
        {
            return CanOperateKitchen && orderManager != null && orderManager.TrySubmitCurrentOrder();
        }

        public bool TrySubmitOrder(OrderManager.OrderStatus order)
        {
            return CanOperateKitchen && orderManager != null && orderManager.TrySubmitOrder(order);
        }

        // ------------------------------------------------------------------ input callbacks

        private void OnBuildRequested(CookerDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            int price = GetCookerPrice(definition);
            bool affordable = CanAfford(price);
            if (!affordable)
            {
                Notify($"{definition.DisplayName} 需要 {price}，当前只有 {Money}。", MessageKind.Warning);
                inputManager?.ExitBuildMode();
                return;
            }

            Notify($"正在放置 {definition.DisplayName}：R 旋转，左键放置，Esc 或右键取消。", MessageKind.Info);
        }

        private void OnCancelRequested()
        {
            Notify("已取消。", MessageKind.Info);
        }

        private void OnMoveCookerRequested(CookerRuntime cooker, Vector2Int direction)
        {
            if (cooker == null)
            {
                return;
            }

            TryMoveCooker(cooker, direction);
        }

        private void OnMoveStaffRequested(Vector2Int direction)
        {
            TryMoveSelectedStaff(direction);
        }

        private void OnCookerContextRequested(CookerRuntime cooker)
        {
            HandleCookerClicked(cooker);
        }

        private void OnRotateCookerRequested(CookerRuntime cooker)
        {
            TryRotateCooker(cooker);
        }

        // ------------------------------------------------------------------ test / tuning hooks

        /// <summary>
        /// Multiplies the time scale. Exposed so an automated run can cover timings that would
        /// otherwise take minutes.
        /// </summary>
        public void SetTimeScale(float scale)
        {
            if (IsPaused) resumeTimeScale = Mathf.Clamp(scale, 0.01f, 100f);
            else Time.timeScale = Mathf.Clamp(scale, 0.01f, 100f);
        }

        /// <summary>Real time scale used by <see cref="Update"/>.</summary>
        public float GetTimeScale()
        {
            return Time.timeScale;
        }

        /// <summary>Index of the selected cooker in the build catalogue, or -1 when none is selected.</summary>
        public int SelectedCookerCatalogueIndex
        {
            get
            {
                if (cookerManager == null || cookerManager.Selected == null || cookerManager.Selected.Definition == null)
                {
                    return -1;
                }

                IReadOnlyList<CookerDefinition> catalogue = cookerManager.Catalogue;
                for (int i = 0; i < catalogue.Count; i++)
                {
                    if (catalogue[i] == cookerManager.Selected.Definition)
                    {
                        return i;
                    }
                }

                return -1;
            }
        }

        /// <summary>Directly grants stock. Used by tests and future cheats; still goes through InventoryManager.</summary>
        public void GrantItem(ItemDefinition item, int count)
        {
            inventoryManager?.Add(item, count);
        }

        /// <summary>Resolves a catalogue item by id.</summary>
        public ItemDefinition FindItem(string id)
        {
            return Level != null ? Level.FindItem(id) : null;
        }

        /// <summary>Resolves a catalogue cooker by id.</summary>
        public CookerDefinition FindCookerDefinition(string id)
        {
            return Level != null ? Level.FindCooker(id) : null;
        }

        /// <summary>Everyone currently employed.</summary>
        public IReadOnlyList<StaffRuntime> AllStaff => staffManager != null ? staffManager.Staff : new List<StaffRuntime>();
    }
}
