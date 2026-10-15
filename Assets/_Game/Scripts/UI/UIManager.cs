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
        [SerializeField] private Button moveCookerButton;
        [SerializeField] private Button collectButton;
        [SerializeField] private Button sellCookerButton;

        [Header("Recipe progress")]
        [SerializeField] private GameObject progressPanel;
        [SerializeField] private TMP_Text progressTitleText;
        [SerializeField] private TMP_Text progressDetailText;
        [SerializeField] private Image progressFill;

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

        // ------------------------------------------------------------------ lifecycle

        /// <summary>Injects the runtime and subscribes to its events. Called by GameRuntime during Boot.</summary>
        public void Initialize(GameRuntime gameRuntime)
        {
            runtime = gameRuntime;

            if (runtime != null)
            {
                runtime.MessageRaised -= OnMessage;
                runtime.MessageRaised += OnMessage;
            }

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
                    label.text = $"Buy {definition.DisplayName}\n{definition.Price}";
                }
                else
                {
                    label.text = "-";
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
            ShowNotification("移动厨具：WASD 或点击相邻格子，Esc 取消。", MessageKind.Info);
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
                    return new Color(0.55f, 0.95f, 0.62f);
                case MessageKind.Warning:
                    return new Color(1f, 0.84f, 0.45f);
                case MessageKind.Error:
                    return new Color(1f, 0.52f, 0.48f);
                default:
                    return new Color(0.92f, 0.94f, 0.98f);
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
                moneyText.text = $"金钱 {runtime.Money}";
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
                    ? $"放置 {input.PendingCooker.DisplayName}：左键点空地，Esc 取消"
                    : "在商店购买厨具（或按 1-4），再点空地放置";
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
                collectButton.interactable = selected != null && selected.HasAnyOutput;
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

            SetPanelActive(staffSection, staff != null);
            SetPanelActive(cookerSection, cooker != null);

            if (staff != null)
            {
                if (staffNameText != null)
                {
                    staffNameText.text = $"{staff.DisplayName}（已选中）";
                }

                if (staffPositionText != null)
                {
                    CookerRuntime occupied = staff.OccupiedCooker;
                    string occupation = occupied != null ? $"　正在激活 {occupied.Definition.DisplayName}" : string.Empty;
                    staffPositionText.text = $"格子 {staff.GridPosition.x},{staff.GridPosition.y}{occupation}";
                }

                if (staffHintText != null)
                {
                    CookerRuntime occupied = staff.OccupiedCooker;
                    staffHintText.text = occupied != null
                        ? "站在厨具上即可持续加工，WASD 走到相邻格子"
                        : "WASD 走到相邻格子，站到厨具上即可激活";
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
                    string active = cooker.IsActive ? "加工中" : "未激活";
                    cookerStateText.text = $"{active} / {DescribeState(cooker.State)}";
                }

                if (cookerPositionText != null)
                {
                    cookerPositionText.text = $"格子 {cooker.GridPosition.x},{cooker.GridPosition.y}";
                }

                if (cookerInputText != null)
                {
                    cookerInputText.text = $"投入：{(runtime.Cookers != null ? runtime.Cookers.DescribeSlots(cooker) : "-")}";
                }

                if (cookerOutputText != null)
                {
                    cookerOutputText.text = $"产出：{(runtime.Cookers != null ? runtime.Cookers.DescribeOutputs(cooker) : "-")}";
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
