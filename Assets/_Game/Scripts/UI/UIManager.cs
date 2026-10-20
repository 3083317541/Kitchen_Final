using System.Collections.Generic;
using Kitchen.Cookers;
using Kitchen.Core;
using Kitchen.Data;
using Kitchen.Entities;
using Kitchen.Input;
using Kitchen.Orders;
using Kitchen.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kitchen.UI
{
    /// <summary>
    /// Shows the state of the kitchen and forwards player commands.
    ///
    /// It never writes to the inventory, the orders or a cooker. Every button asks
    /// <see cref="GameRuntime"/> to perform an action, which keeps one authoritative code path for
    /// each rule and makes the UI impossible to desync from the simulation.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIManager : MonoBehaviour
    {
        // ------------------------------------------------------------------ inspector wiring

        [Header("Top bar")]
        [SerializeField] private TMP_Text moneyText;
        [SerializeField] private TMP_Text modeText;
        [SerializeField] private TMP_Text timeText;
        [SerializeField] private TMP_Text orderCountText;
        [SerializeField] private TMP_Text inventoryText;

        [Header("Order panel")]
        [SerializeField] private GameObject orderPanel;
        [SerializeField] private TMP_Text orderTitleText;
        [SerializeField] private TMP_Text orderRequirementText;
        [SerializeField] private TMP_Text orderTimerText;
        [SerializeField] private TMP_Text orderStateText;
        [SerializeField] private Button submitOrderButton;
        [SerializeField] private TMP_Text submitOrderButtonLabel;

        [Header("Cooker shop")]
        [SerializeField] private GameObject cookerShopPanel;
        [SerializeField] private Button[] cookerButtons = new Button[0];
        [SerializeField] private TMP_Text[] cookerButtonLabels = new TMP_Text[0];
        [SerializeField] private TMP_Text buildHintText;

        [Header("Selection panel - staff")]
        [SerializeField] private GameObject selectionPanel;
        [SerializeField] private GameObject staffSection;
        [SerializeField] private TMP_Text staffNameText;
        [SerializeField] private TMP_Text staffPositionText;
        [SerializeField] private TMP_Text staffHintText;

        [Header("Selection panel - cooker")]
        [SerializeField] private GameObject cookerSection;
        [SerializeField] private TMP_Text cookerNameText;
        [SerializeField] private TMP_Text cookerStateText;
        [SerializeField] private TMP_Text cookerPositionText;
        [SerializeField] private TMP_Text cookerInputText;
        [SerializeField] private TMP_Text cookerOutputText;
        [SerializeField] private Button recipeButton;
        [SerializeField] private TMP_Text recipeButtonLabel;

        [Header("HUD scale")]
        [Tooltip("Component that owns the canvas scale. Lives on the canvas, not here.")]
        [SerializeField] private HudScaler hudScaler;
        [SerializeField] private Button uiScaleDownButton;
        [SerializeField] private Button uiScaleUpButton;
        [SerializeField] private TMP_Text uiScaleText;
        [SerializeField] private Button moveCookerButton;
        [SerializeField] private Button collectButton;
        [SerializeField] private Button sellCookerButton;

        [Header("Recipe progress")]
        [SerializeField] private GameObject progressPanel;
        [SerializeField] private TMP_Text progressTitleText;
        [SerializeField] private TMP_Text progressDetailText;
        [SerializeField] private Image progressFill;

        [Header("Level clear")]
        [SerializeField] private GameObject levelClearPanel;
        [SerializeField] private TMP_Text levelClearTitleText;
        [SerializeField] private TMP_Text levelClearRewardText;
        [SerializeField] private Button levelClearNextButton;
        [SerializeField] private TMP_Text levelClearNextButtonText;

        [Header("Notifications")]
        [SerializeField] private GameObject notificationPanel;
        [SerializeField] private TMP_Text notificationText;
        [SerializeField] private float notificationSeconds = 4f;

        [Header("Behaviour")]
        [Tooltip("Seconds between UI refreshes. Keeps the frame cost of string formatting low.")]
        [SerializeField] private float refreshInterval = 0.2f;

        // ------------------------------------------------------------------ runtime state

        private GameRuntime runtime;
        private InputManager input;
        private float refreshTimer;
        private float notificationTimer;
        private string pendingMessage;
        private MessageKind pendingKind = MessageKind.Info;
        private readonly List<Button> cookerButtonsBound = new List<Button>();

        public GameRuntime Runtime => runtime;

        /// <summary>Container that shows the end of level summary.</summary>
        public GameObject LevelClearPanel => levelClearPanel;

        public TMP_Text LevelClearTitleText => levelClearTitleText;

        public TMP_Text LevelClearRewardText => levelClearRewardText;

        public Button LevelClearNextButton => levelClearNextButton;

        public TMP_Text LevelClearNextButtonText => levelClearNextButtonText;

        // ------------------------------------------------------------------ lifecycle

        /// <summary>Injects the runtime and subscribes to its events. Called by GameRuntime during Boot.</summary>
        public void Initialize(GameRuntime gameRuntime)
        {
            runtime = gameRuntime;
            if (moneyText != null) moneyText.GetComponent<PaperMoneyFeedback>()?.ResetValue();

            if (runtime != null)
            {
                runtime.MessageRaised -= OnMessage;
                runtime.MessageRaised += OnMessage;
            }

            if (levelClearNextButton != null)
            {
                levelClearNextButton.onClick.RemoveListener(OnLevelClearNext);
                levelClearNextButton.onClick.AddListener(OnLevelClearNext);
            }

            SetLevelClearVisible(false);

            RefreshAll();
        }

        /// <summary>Injects the input manager and hooks up every button once.</summary>
        public void BindInput(InputManager inputManager)
        {
            input = inputManager;

            if (input != null)
            {
                input.ModeChanged -= OnModeChanged;
                input.ModeChanged += OnModeChanged;
                input.CookerPurchaseRequested -= RequestCookerPurchase;
                input.CookerPurchaseRequested += RequestCookerPurchase;
            }

            BindCookerShopButtons();

            if (submitOrderButton != null)
            {
                submitOrderButton.onClick.RemoveListener(OnSubmitOrderClicked);
                submitOrderButton.onClick.AddListener(OnSubmitOrderClicked);
            }

            if (moveCookerButton != null)
            {
                moveCookerButton.onClick.RemoveListener(OnMoveCookerClicked);
                moveCookerButton.onClick.AddListener(OnMoveCookerClicked);
            }

            if (collectButton != null)
            {
                collectButton.onClick.RemoveListener(OnCollectClicked);
                collectButton.onClick.AddListener(OnCollectClicked);
            }

            if (recipeButton != null)
            {
                recipeButton.onClick.RemoveListener(OnCycleRecipeClicked);
                recipeButton.onClick.AddListener(OnCycleRecipeClicked);
            }

            if (uiScaleDownButton != null)
            {
                uiScaleDownButton.onClick.RemoveListener(OnUiScaleDown);
                uiScaleDownButton.onClick.AddListener(OnUiScaleDown);
            }

            if (uiScaleUpButton != null)
            {
                uiScaleUpButton.onClick.RemoveListener(OnUiScaleUp);
                uiScaleUpButton.onClick.AddListener(OnUiScaleUp);
            }

            hudScaler?.LoadAndApply();
            RefreshUiScaleText();

            if (sellCookerButton != null)
            {
                sellCookerButton.onClick.RemoveListener(OnSellCookerClicked);
                sellCookerButton.onClick.AddListener(OnSellCookerClicked);
            }

            SyncCookerShopLabels();
            SetNotificationPanelVisible(false);
            RefreshAll();
        }

        private void OnDestroy()
        {
            if (runtime != null)
            {
                runtime.MessageRaised -= OnMessage;
            }

            if (input != null)
            {
                input.ModeChanged -= OnModeChanged;
                input.CookerPurchaseRequested -= RequestCookerPurchase;
            }
        }

        // ------------------------------------------------------------------ ticking

        /// <summary>Refreshes the readouts on a fixed interval and ages the notification.</summary>
        public void Tick(float deltaTime)
        {
            refreshTimer -= deltaTime;
            if (refreshTimer <= 0f)
            {
                refreshTimer = Mathf.Max(0.02f, refreshInterval);
                RefreshValues();
            }

            if (notificationTimer > 0f)
            {
                notificationTimer -= deltaTime;
                if (notificationTimer <= 0f)
                {
                    SetNotificationPanelVisible(false);
                }
            }
        }

        // ------------------------------------------------------------------ commands from UI

        private void BindCookerShopButtons()
        {
            cookerButtonsBound.Clear();
            if (cookerButtons == null)
            {
                return;
            }

            for (int i = 0; i < cookerButtons.Length; i++)
            {
                Button button = cookerButtons[i];
                if (button == null)
                {
                    continue;
                }

                int index = i;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnBuyCookerClicked(index));
                cookerButtonsBound.Add(button);
            }
        }

        private void SyncCookerShopLabels()
        {
            if (runtime == null || runtime.Cookers == null || cookerButtonLabels == null)
            {
                return;
            }

            IReadOnlyList<CookerDefinition> catalogue = runtime.Cookers.Catalogue;
            for (int i = 0; i < cookerButtonLabels.Length; i++)
            {
                TMP_Text label = cookerButtonLabels[i];
                if (label == null)
                {
                    continue;
                }

                if (i < catalogue.Count && catalogue[i] != null)
                {
                    CookerDefinition definition = catalogue[i];
                    label.text = $"{definition.DisplayName}　{definition.Price} 金币";
                    if (i < cookerButtons.Length && cookerButtons[i] != null) cookerButtons[i].gameObject.SetActive(true);
                }
                else
                {
                    label.text = "-";
                    if (i < cookerButtons.Length && cookerButtons[i] != null) cookerButtons[i].gameObject.SetActive(false);
                }
            }
        }

        private void OnBuyCookerClicked(int catalogueIndex)
        {
            RequestCookerPurchase(catalogueIndex);
        }

        /// <summary>
        /// Shared entry point for the shop buttons and the number keys. It only validates
        /// affordability and asks InputManager to start a build, so the actual charge happens when
        /// the cooker is placed.
        /// </summary>
        public void RequestCookerPurchase(int catalogueIndex)
        {
            if (runtime == null || runtime.Cookers == null || input == null)
            {
                return;
            }

            CookerDefinition definition = runtime.Cookers.GetCatalogueEntry(catalogueIndex);
            if (definition == null)
            {
                return;
            }

            if (!runtime.CanAfford(definition.Price))
            {
                ShowNotification($"金钱不足，无法购买 {definition.DisplayName}（{definition.Price}）。", MessageKind.Warning);
                return;
            }

            // The UI only requests the mode change; InputManager owns it and GameRuntime validates
            // and charges on placement, so a cancelled build can never cost money.
            input.RequestBuildCooker(definition);
            RefreshValues();
        }

        private void OnSubmitOrderClicked()
        {
            if (runtime == null)
            {
                return;
            }

            // GameRuntime re-validates everything, so this click cannot bypass the delivery rules.
            runtime.TrySubmitOrder();
            RefreshValues();
        }

        private void OnMoveCookerClicked()
        {
            if (runtime == null || input == null || runtime.Cookers == null)
            {
                return;
            }

            CookerRuntime cooker = runtime.Cookers.Selected;
            if (cooker == null)
            {
                ShowNotification("请先选中一个厨具。", MessageKind.Warning);
                return;
            }

            input.EnterMoveCookerMode(cooker);
            ShowNotification("移动厨具：WASD 或点击相邻格，R 旋转，Esc 取消。", MessageKind.Info);
            RefreshValues();
        }

        /// <summary>
        /// Steps the selected cooker through 自动 and each recipe it can run.
        ///
        /// A single cycling button is used rather than a list so the panel keeps its size: it is anchored
        /// near the bottom of the screen and has no room to grow.
        /// </summary>
        // ------------------------------------------------------------------ HUD scale

        private void OnUiScaleDown()
        {
            StepUiScale(-0.1f);
        }

        private void OnUiScaleUp()
        {
            StepUiScale(0.1f);
        }

        /// <summary>Steps the HUD scale and applies it immediately.</summary>
        public void StepUiScale(float delta)
        {
            if (hudScaler == null)
            {
                hudScaler = GetComponentInChildren<HudScaler>();
            }

            if (hudScaler == null)
            {
                ShowNotification("这个场景没有界面缩放组件。", MessageKind.Warning);
                return;
            }

            hudScaler.Step(delta);
            RefreshUiScaleText();

            ShowNotification($"界面缩放：{(hudScaler.Scale * 100f):0}%", MessageKind.Info);
        }

        private void RefreshUiScaleText()
        {
            if (uiScaleText == null)
            {
                return;
            }

            if (hudScaler == null)
            {
                hudScaler = GetComponentInChildren<HudScaler>();
            }

            uiScaleText.text = hudScaler != null ? $"{(hudScaler.Scale * 100f):0}%" : "-";
        }

        private void OnCycleRecipeClicked()
        {
            if (runtime == null || runtime.Cookers == null || runtime.Recipes == null)
            {
                return;
            }

            CookerRuntime cooker = runtime.Cookers.Selected;
            if (cooker == null || cooker.Definition == null)
            {
                ShowNotification("请先选中一个厨具。", MessageKind.Warning);
                return;
            }

            List<Kitchen.Data.RecipeDefinition> options =
                runtime.Recipes.RecipesFor(cooker.Definition.Kind);

            if (options.Count <= 1)
            {
                ShowNotification("这个厨具只有一种加工方式。", MessageKind.Info);
                RefreshValues();
                return;
            }

            // auto -> recipe 0 -> recipe 1 -> ... -> auto
            int current = -1;
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].Id == cooker.PreferredRecipeId)
                {
                    current = i;
                    break;
                }
            }

            current++;
            cooker.PreferredRecipeId = current < options.Count ? options[current].Id : null;

            ShowNotification(
                current < options.Count
                    ? $"加工方式：{options[current].DisplayName}"
                    : "加工方式：自动",
                MessageKind.Info);

            RefreshValues();
        }

        private void OnCollectClicked()
        {
            if (runtime == null || runtime.Cookers == null)
            {
                return;
            }

            CookerRuntime cooker = runtime.Cookers.Selected;
            if (cooker == null)
            {
                ShowNotification("请先选中一个厨具。", MessageKind.Warning);
                return;
            }

            runtime.TryCollectCookerOutput(cooker);
            RefreshValues();
        }

        private void OnSellCookerClicked()
        {
            if (runtime == null || runtime.Cookers == null)
            {
                return;
            }

            CookerRuntime cooker = runtime.Cookers.Selected;
            if (cooker == null)
            {
                ShowNotification("请先选中一个厨具。", MessageKind.Warning);
                return;
            }

            runtime.TrySellCooker(cooker);
            RefreshValues();
        }

        private void OnModeChanged(InteractionMode mode)
        {
            RefreshValues();
        }

        private void OnMessage(string message, MessageKind kind)
        {
            ShowNotification(message, kind);
        }

        // ------------------------------------------------------------------ notifications

        // ------------------------------------------------------------------ level clear

        /// <summary>
        /// Shows the end of level summary. The player advances with the button, so the reward is read
        /// at the player\'s pace instead of flashing past.
        /// </summary>
        public void ShowLevelClear(LevelConfig cleared, string reward)
        {
            if (levelClearPanel == null)
            {
                return;
            }

            if (levelClearTitleText != null)
            {
                levelClearTitleText.text = cleared != null
                    ? $"{cleared.LevelTitle} 完成！"
                    : "本关完成！";
            }

            if (levelClearRewardText != null)
            {
                levelClearRewardText.text = string.IsNullOrEmpty(reward)
                    ? "没有额外奖励。"
                    : $"奖励：{reward}";
            }

            if (levelClearNextButtonText != null)
            {
                levelClearNextButtonText.text = cleared != null && cleared.HasNextLevel
                    ? "进入下一关"
                    : "返回开始界面";
            }

            SetLevelClearVisible(true);
        }

        /// <summary>Hides the end of level summary.</summary>
        public void HideLevelClear()
        {
            SetLevelClearVisible(false);
        }

        private void SetLevelClearVisible(bool visible)
        {
            if (levelClearPanel != null)
            {
                levelClearPanel.SetActive(visible);
            }
        }

        private void OnLevelClearNext()
        {
            SetLevelClearVisible(false);

            Kitchen.Level.LevelManager levels = runtime != null ? runtime.Levels : null;
            if (levels != null)
            {
                levels.GoToNextLevel();
            }
        }

        /// <summary>Displays a message in the notification panel.</summary>
        public void ShowNotification(string message, MessageKind kind)
        {
            pendingMessage = message;
            pendingKind = kind;
            notificationTimer = notificationSeconds;

            if (notificationText != null)
            {
                notificationText.text = message;
                notificationText.color = ColorFor(kind);
            }

            SetNotificationPanelVisible(!string.IsNullOrEmpty(message));
        }

        private void SetNotificationPanelVisible(bool visible)
        {
            if (notificationPanel != null)
            {
                notificationPanel.SetActive(visible);
            }
        }

        private static Color ColorFor(MessageKind kind)
        {
            switch (kind)
            {
                case MessageKind.Success:
                    return new Color32(41, 113, 66, 255);
                case MessageKind.Warning:
                    return new Color32(132, 91, 19, 255);
                case MessageKind.Error:
                    return new Color32(176, 55, 48, 255);
                default:
                    return new Color32(27, 27, 27, 255);
            }
        }

        // ------------------------------------------------------------------ refresh

        /// <summary>Full refresh: structure plus values. Called after level setup and on demand.</summary>
        public void RefreshAll()
        {
            SyncCookerShopLabels();
            SetPanelActive(orderPanel, true);
            SetPanelActive(cookerShopPanel, true);
            SetPanelActive(selectionPanel, true);
            SetPanelActive(progressPanel, true);
            RefreshValues();
        }

        /// <summary>Refreshes every text readout from the current runtime state.</summary>
        public void RefreshValues()
        {
            if (runtime == null)
            {
                return;
            }

            RefreshTopBar();
            RefreshOrderPanel();
            RefreshSelectionPanel();
            RefreshProgressPanel();
        }

        private void RefreshTopBar()
        {
            if (moneyText != null)
            {
                if (!runtime.IsInitialized) return;
                PaperMoneyFeedback feedback = moneyText.GetComponent<PaperMoneyFeedback>();
                if (feedback != null) feedback.Display(runtime.Money);
                else moneyText.text = $"金钱 {runtime.Money}";
            }

            if (modeText != null)
            {
                // The mode label reads the single source of truth for the interaction mode.
                string hint = input != null && input.Mode != InteractionMode.Normal
                    ? "WASD 移动 / Esc 取消"
                    : "左键选厨师，或购买厨具";
                modeText.text = $"{DescribeMode(input != null ? input.Mode : InteractionMode.Normal)} | {hint}";
            }

            if (timeText != null)
            {
                int seconds = Mathf.FloorToInt(runtime.ElapsedTime);
                timeText.text = $"{seconds / 60:00}:{seconds % 60:00}";
            }

            if (orderCountText != null && runtime.Orders != null)
            {
                orderCountText.text = $"完成 {runtime.Orders.CompletedOrders} / 失败 {runtime.Orders.FailedOrders}";
            }

            if (inventoryText != null && runtime.Inventory != null)
            {
                inventoryText.text = runtime.Inventory.Describe();
            }

            if (buildHintText != null)
            {
                bool building = input != null && input.Mode == InteractionMode.Build && input.PendingCooker != null;
                buildHintText.text = building
                    ? $"放置 {input.PendingCooker.DisplayName}：R 旋转，左键放置，Esc 取消"
                    : "1-4 购买；选中厨具后按 R 旋转正面";
            }

            // Buttons that would fail are shaded so the player sees the state before clicking.
            if (submitOrderButton != null && runtime.Orders != null)
            {
                bool canSubmit = runtime.Orders.CanSubmit();
                submitOrderButton.interactable = canSubmit;

                if (submitOrderButtonLabel != null)
                {
                    submitOrderButtonLabel.text = canSubmit ? "上菜" : "上菜（材料不足）";
                }
            }

            if (collectButton != null)
            {
                CookerRuntime selected = runtime.Cookers != null ? runtime.Cookers.Selected : null;
                collectButton.interactable = selected != null && selected.HasAnyOutput &&
                    runtime.Staff != null && runtime.Staff.Selected != null;
            }
        }

        /// <summary>Renders a cooker work state for the readout panel.</summary>
        private static string DescribeState(CookerState state)
        {
            switch (state)
            {
                case CookerState.WaitingForInput:
                    return "等待材料";
                case CookerState.Working:
                    return "加工中";
                case CookerState.ReadyToCollect:
                    return "已完成待取";
                case CookerState.Blocked:
                    return "产出已满";
                case CookerState.Preparing:
                    return "装料启动";
                case CookerState.Collecting:
                    return "取出成品";
                case CookerState.Burnt:
                    return "已烧焦，需清理";
                case CookerState.Cleaning:
                    return "清理中";
                default:
                    return "空闲";
            }
        }

        private static string DescribeMode(InteractionMode mode)
        {
            switch (mode)
            {
                case InteractionMode.Build:
                    return "建造模式";
                case InteractionMode.MoveCooker:
                    return "移动厨具";
                case InteractionMode.SelectIngredient:
                    return "选择材料";
                default:
                    return "普通模式";
            }
        }

        private void RefreshOrderPanel()
        {
            if (orderTitleText == null || runtime.Orders == null)
            {
                return;
            }

            OrderManager.OrderStatus order = runtime.Orders.Current;
            if (order == null || order.Definition == null)
            {
                orderTitleText.text = "暂无订单";
                if (orderRequirementText != null)
                {
                    orderRequirementText.text = "-";
                }

                if (orderTimerText != null)
                {
                    orderTimerText.text = string.Empty;
                }

                if (orderStateText != null)
                {
                    orderStateText.text = string.Empty;
                }

                return;
            }

            orderTitleText.text = order.Definition.DisplayName;

            string dish = order.Dish != null ? order.Dish.DisplayName : "缺少菜品";
            if (orderRequirementText != null)
            {
                orderRequirementText.text =
                    $"需要 {order.DishRequired} 份 {dish}，{order.PlatesRequired} 个干净盘子";
            }

            if (orderTimerText != null)
            {
                if (order.IsActive)
                {
                    int remaining = Mathf.CeilToInt(Mathf.Max(0f, order.TimeRemaining));
                    orderTimerText.text = $"剩余 {remaining / 60:00}:{remaining % 60:00}　奖励 {order.Reward}";
                }
                else
                {
                    orderTimerText.text = $"奖励 {order.Reward}";
                }
            }

            if (orderStateText != null)
            {
                switch (order.State)
                {
                    case OrderState.Completed:
                        orderStateText.text = "已完成";
                        orderStateText.color = ColorFor(MessageKind.Success);
                        break;
                    case OrderState.Failed:
                        orderStateText.text = "已超时失败";
                        orderStateText.color = ColorFor(MessageKind.Error);
                        break;
                    default:
                        orderStateText.text = "进行中";
                        orderStateText.color = ColorFor(MessageKind.Info);
                        break;
                }
            }
        }

        private void RefreshSelectionPanel()
        {
            StaffRuntime staff = runtime.Staff != null ? runtime.Staff.Selected : null;
            CookerRuntime cooker = runtime.Cookers != null ? runtime.Cookers.Selected : null;

            SetPanelActive(staffSection, staff != null && cooker == null);
            SetPanelActive(cookerSection, cooker != null);

            if (staff != null)
            {
                if (staffNameText != null)
                {
                    staffNameText.text = $"{staff.DisplayName}（已选中）";
                }

                if (staffPositionText != null)
                {
                    CookerRuntime occupied = staff.AssignedCooker;
                    string occupation = occupied != null ? $"　负责 {occupied.Definition.DisplayName}" : "　未指定任务";
                    staffPositionText.text = $"格子 {staff.GridPosition.x},{staff.GridPosition.y}{occupation}";
                }

                if (staffHintText != null)
                {
                    staffHintText.text = "WASD 走到正面操作格，再点击设备。共用操作格一次只能站一人。";
                }
            }

            if (cooker != null)
            {
                if (cookerNameText != null)
                {
                    cookerNameText.text = cooker.Definition != null ? cooker.Definition.DisplayName : "<cooker>";
                }

                if (cookerStateText != null)
                {
                    cookerStateText.text = $"{DescribeState(cooker.State)} · {runtime.Cookers.DescribeWorkPosition(cooker)}";
                }

                if (cookerPositionText != null)
                {
                    cookerPositionText.text = $"朝{CookerFacingUtility.Label(cooker.Facing)} · 操作格 {cooker.WorkPosition.x},{cooker.WorkPosition.y} · R旋转";
                }

                if (cookerInputText != null)
                {
                    cookerInputText.text = $"投入：{(runtime.Cookers != null ? runtime.Cookers.DescribeSlots(cooker) : "-")}";
                }

                if (cookerOutputText != null)
                {
                    cookerOutputText.text = $"产出：{(runtime.Cookers != null ? runtime.Cookers.DescribeOutputs(cooker) : "-")}";
                }

                if (recipeButton != null)
                {
                    recipeButton.interactable = true;
                }

                if (recipeButtonLabel != null)
                {
                    string label = "自动";

                    if (!string.IsNullOrEmpty(cooker.PreferredRecipeId) && runtime.Recipes != null)
                    {
                        List<Kitchen.Data.RecipeDefinition> options =
                            runtime.Recipes.RecipesFor(cooker.Definition.Kind);

                        for (int i = 0; i < options.Count; i++)
                        {
                            if (options[i].Id == cooker.PreferredRecipeId)
                            {
                                label = options[i].DisplayName;
                                break;
                            }
                        }
                    }

                    recipeButtonLabel.text = $"加工：{label}";
                }
            }
        }

        private void RefreshProgressPanel()
        {
            CookerRuntime cooker = runtime.Cookers != null ? runtime.Cookers.Selected : null;

            if (cooker == null)
            {
                if (progressTitleText != null)
                {
                    progressTitleText.text = "未选中厨具";
                }

                if (progressDetailText != null)
                {
                    progressDetailText.text = "左键点击厨具查看加工进度";
                }

                if (progressFill != null)
                {
                    progressFill.fillAmount = 0f;
                }

                return;
            }

            if (progressTitleText != null)
            {
                progressTitleText.text = cooker.Definition != null
                    ? $"{cooker.Definition.DisplayName}  ({cooker.State})"
                    : "Cooker";
            }

            if (progressDetailText != null)
            {
                string progress = cooker.Definition != null && cooker.Definition.WorkType == WorkType.Chopping
                    ? $"已切 {cooker.WorkTicks}/{cooker.Definition.RequiredTicks} 次"
                    : $"进度 {cooker.WorkProgress * 100f:0}%";

                progressDetailText.text = $"{cooker.StatusText}　{progress}";
            }

            if (progressFill != null)
            {
                progressFill.fillAmount = Mathf.Clamp01(cooker.WorkProgress);
            }
        }

        private static void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null && panel.activeSelf != active)
            {
                panel.SetActive(active);
            }
        }
    }
}
