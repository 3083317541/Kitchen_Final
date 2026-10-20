using System.Collections.Generic;
using Kitchen.Orders;
using Kitchen.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kitchen.UI
{
    public sealed class ParallelOrderPanel : MonoBehaviour
    {
        private sealed class Ticket
        {
            public RectTransform Root;
            public TMP_Text Title;
            public TMP_Text Requirement;
            public TMP_Text Timer;
            public Button Submit;
            public TMP_Text SubmitLabel;
            public OrderManager.OrderStatus Order;
        }

        private readonly List<Ticket> tickets = new List<Ticket>();
        private GameRuntime runtime;
        private RectTransform content;
        private TMP_Text emptyLabel;

        public void Initialize(GameRuntime owner, TMP_Text titleTemplate, Button buttonTemplate)
        {
            runtime = owner;
            if (content != null) return;
            Transform previous = transform.Find("OrderContent");
            if (previous != null) previous.gameObject.SetActive(false);
            content = new GameObject("ParallelOrderContent", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(transform, false);
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.offsetMin = new Vector2(24f, 18f);
            content.offsetMax = new Vector2(-24f, -44f);
            Sprite rounded = GetComponent<Image>() != null ? GetComponent<Image>().sprite : null;
            TMP_FontAsset font = titleTemplate.font;

            for (int index = 0; index < 2; index++)
            {
                RectTransform root = Rect(content, "OrderTicket_" + index, new Vector2(0f, 142f), new Vector2(0f, -index * 150f));
                root.anchorMax = Vector2.one;
                root.anchorMin = new Vector2(0f, 1f);
                root.offsetMin = new Vector2(0f, -index * 150f - 142f);
                root.offsetMax = new Vector2(0f, -index * 150f);
                Image surface = root.gameObject.AddComponent<Image>();
                surface.sprite = rounded;
                surface.type = Image.Type.Sliced;
                surface.color = new Color32(250, 246, 235, 255);
                surface.raycastTarget = false;
                Ticket ticket = new Ticket { Root = root };
                ticket.Title = Label(root, "Dish", font, 24f, new Vector2(390f, 32f), new Vector2(12f, -8f));
                ticket.Requirement = Label(root, "Requirements", font, 20f, new Vector2(390f, 30f), new Vector2(12f, -43f));
                ticket.Timer = Label(root, "Timer", font, 20f, new Vector2(264f, 44f), new Vector2(12f, -81f));
                ticket.Submit = Instantiate(buttonTemplate, root);
                ticket.Submit.name = "SubmitTicket_" + index;
                ticket.Submit.gameObject.SetActive(true);
                RectTransform submitRect = ticket.Submit.GetComponent<RectTransform>();
                submitRect.anchorMin = submitRect.anchorMax = new Vector2(1f, 0f);
                submitRect.pivot = new Vector2(1f, 0f);
                submitRect.sizeDelta = new Vector2(110f, 46f);
                submitRect.anchoredPosition = new Vector2(-12f, 12f);
                ticket.SubmitLabel = ticket.Submit.GetComponentInChildren<TMP_Text>(true);
                ticket.Submit.onClick = new Button.ButtonClickedEvent();
                ticket.Submit.onClick.AddListener(() =>
                {
                    runtime.TrySubmitOrder(ticket.Order);
                    Refresh();
                });
                tickets.Add(ticket);
            }

            emptyLabel = Label(content, "NoOrders", font, 26f, new Vector2(390f, 60f), new Vector2(12f, -8f));
            Refresh();
        }

        public void Refresh()
        {
            if (runtime == null || runtime.Orders == null) return;
            IReadOnlyList<OrderManager.OrderStatus> orders = runtime.Orders.ActiveOrders;
            emptyLabel.gameObject.SetActive(orders.Count == 0);
            emptyLabel.text = runtime.Orders.IsLevelCleared ? "今日订单已完成" : "暂无订单";
            for (int index = 0; index < tickets.Count; index++)
            {
                Ticket ticket = tickets[index];
                ticket.Root.gameObject.SetActive(index < orders.Count);
                ticket.Order = index < orders.Count ? orders[index] : null;
                if (ticket.Order == null) continue;
                OrderManager.OrderStatus order = ticket.Order;
                ticket.Title.text = $"#{order.SequenceIndex + 1} {order.Definition.DisplayName}";
                ticket.Requirement.text = $"{order.Dish.DisplayName} ×{order.DishRequired}  ·  干净盘子 ×{order.PlatesRequired}";
                int remaining = Mathf.CeilToInt(order.TimeRemaining);
                ticket.Timer.text = $"{remaining / 60:00}:{remaining % 60:00}  ·  奖励 {order.Reward}";
                ticket.Timer.color = order.TimeRemaining <= order.TimeLimit * 0.25f ?
                    new Color32(205, 62, 48, 255) : new Color32(69, 53, 28, 255);
                bool ready = runtime.Orders.CanSubmit(order);
                ticket.Submit.interactable = ready;
                if (ticket.SubmitLabel != null) ticket.SubmitLabel.text = ready ? "上菜" : "备菜中";
            }
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 size, Vector2 position)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static TMP_Text Label(Transform parent, string name, TMP_FontAsset font, float size, Vector2 bounds, Vector2 position)
        {
            TextMeshProUGUI label = Rect(parent, name, bounds, position).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = size;
            label.enableAutoSizing = true;
            label.fontSizeMin = 15f;
            label.fontSizeMax = size;
            label.color = new Color32(69, 53, 28, 255);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;
            return label;
        }
    }
}
