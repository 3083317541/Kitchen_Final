using Kitchen.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kitchen.UI
{
    public sealed class KitchenDayPanel : MonoBehaviour
    {
        private GameRuntime runtime;
        private RectTransform canvasRect;
        private GameObject overlay;
        private RectTransform cardLayout;
        private RectTransform cardVisual;
        private TMP_Text heading;
        private TMP_Text summary;
        private Button start;
        private Button arrange;
        private Button briefing;
        private readonly Button[] skills = new Button[3];
        private bool briefingDismissed;
        private DayPhase previousPhase;
        private float entranceSeconds;

        public void Initialize(GameRuntime owner, TMP_Text fontTemplate, Button buttonTemplate, Image surfaceTemplate)
        {
            runtime = owner;
            if (overlay != null || (runtime.Classic == null && runtime.Level.TutorialSteps.Count == 0)) return;
            canvasRect = (RectTransform)transform;
            overlay = Rect(transform, "DayFlowOverlay", Vector2.zero, Vector2.zero).gameObject;
            RectTransform overlayRect = (RectTransform)overlay.transform;
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;
            overlay.AddComponent<Image>().color = new Color(0.06f, 0.07f, 0.08f, 0.55f);
            cardLayout = Rect(overlay.transform, "CardLayout", new Vector2(1040f, 820f), Vector2.zero);
            cardLayout.anchorMin = cardLayout.anchorMax = cardLayout.pivot = new Vector2(0.5f, 0.5f);
            cardVisual = Rect(cardLayout, "Visual", new Vector2(1040f, 820f), Vector2.zero);
            cardVisual.anchorMin = cardVisual.anchorMax = cardVisual.pivot = new Vector2(0.5f, 0.5f);
            Surface(cardVisual, surfaceTemplate, Color.white);
            RectTransform title = Rect(cardVisual, "Title", new Vector2(380f, 64f), new Vector2(-8f, 20f));
            Surface(title, surfaceTemplate, new Color32(249, 213, 128, 255));
            Label(title, "Label", runtime.Classic != null ? "每日营业" : "入门教程", fontTemplate.font, 34f,
                new Vector2(348f, 60f), new Vector2(22f, 0f));
            heading = Label(cardVisual, "Heading", "", fontTemplate.font, 38f,
                new Vector2(928f, 60f), new Vector2(56f, -68f));
            summary = Label(cardVisual, "Summary", "", fontTemplate.font, 28f,
                new Vector2(928f, 232f), new Vector2(56f, -144f));
            summary.alignment = TextAlignmentOptions.TopLeft;
            for (int index = 0; index < skills.Length; index++)
            {
                int choiceIndex = index;
                skills[index] = Button(cardVisual, "SkillChoice_" + index, buttonTemplate,
                    new Vector2(928f, 78f), new Vector2(56f, -390f - index * 92f));
                skills[index].onClick.AddListener(() =>
                {
                    if (runtime.Classic != null && choiceIndex < runtime.Classic.Choices.Count)
                        runtime.ChooseClassicSkill(runtime.Classic.Choices[choiceIndex]);
                });
            }
            start = Button(cardVisual, "BeginService", buttonTemplate, new Vector2(444f, 72f), new Vector2(540f, -640f));
            PaperButtonStyle.Apply(start, PaperButtonStyle.Positive);
            start.onClick.AddListener(() =>
            {
                if (runtime.Classic == null) runtime.BeginTutorialPractice();
                else runtime.BeginClassicService();
                Refresh();
            });
            arrange = Button(cardVisual, "ArrangeKitchen", buttonTemplate, new Vector2(444f, 72f), new Vector2(56f, -640f));
            Caption(arrange, "先布置厨房（计时暂停）");
            arrange.onClick.AddListener(() => { briefingDismissed = true; Refresh(); });
            Button menu = Button(cardVisual, "ReturnToMenu", buttonTemplate, new Vector2(928f, 46f), new Vector2(56f, -738f));
            Caption(menu, "返回主菜单 · 结束本轮经营");
            PaperButtonStyle.Apply(menu, PaperButtonStyle.Danger);
            menu.onClick.AddListener(runtime.ReturnToMainMenu);
            briefing = Button(transform, "OpenDailyBriefing", buttonTemplate, new Vector2(440f, 62f), new Vector2(0f, -174f));
            RectTransform briefingRect = (RectTransform)briefing.transform;
            briefingRect.anchorMin = briefingRect.anchorMax = new Vector2(0.5f, 1f);
            briefingRect.pivot = new Vector2(0.5f, 1f);
            Caption(briefing, "备战中 · 查看简报 / 开门营业");
            PaperButtonStyle.Apply(briefing, PaperButtonStyle.Primary);
            briefing.onClick.AddListener(() => { briefingDismissed = false; Refresh(); });
            previousPhase = DayPhase.Briefing;
            Refresh();
        }

        public void Refresh()
        {
            if (overlay == null) return;
            bool classic = runtime.Classic != null;
            DayPhase phase = classic ? runtime.Classic.Phase : DayPhase.Briefing;
            if (phase != previousPhase) briefingDismissed = false;
            previousPhase = phase;
            bool visible = classic ? (phase == DayPhase.Briefing && !briefingDismissed) || phase == DayPhase.Results :
                runtime.TutorialBriefingOpen;
            if (overlay.activeSelf != visible)
            {
                overlay.SetActive(visible);
                entranceSeconds = 0f;
                if (visible) overlay.transform.SetAsLastSibling();
            }
            briefing.gameObject.SetActive(classic && phase == DayPhase.Briefing && briefingDismissed);
            if (!visible) return;
            overlay.transform.SetAsLastSibling();
            bool results = classic && phase == DayPhase.Results;
            start.gameObject.SetActive(!results);
            arrange.gameObject.SetActive(classic && !results);
            Caption(start, classic ? "开门营业 · 开始计时" : "开始教程练习");
            if (classic)
            {
                ClassicDayController day = runtime.Classic;
                heading.text = $"第 {day.Run.Day} 天 · {(results ? "今日结算" : "营业简报")}";
                summary.text = results ?
                    $"今日得分 {day.Score}  ·  参考目标 {day.Benchmark}\n上菜 {day.CompletedOrders} 单  ·  超时 {day.MissedOrders} 单  ·  打烊未上菜 {day.UnservedOrders} 单\n焦糊 {day.BurntBatches} 次  ·  结余 {runtime.Money} 金币\n累计 {day.Run.TotalScore} 分  ·  单日最高 {day.Run.BestDayScore} 分\n选择一项技能，进入下一天（技能可叠加）：" :
                    $"营业 {runtime.Level.ServiceDuration:0} 秒，订单持续到来，争取更高分！参考目标 {day.Benchmark} 分。\n两张订单并行：沙拉 / 肉面包；超时顾客离开并补新单。\n基础原料无限，今天 {runtime.Level.StaffCount} 名员工、{runtime.Inventory.TotalCount(runtime.CleanPlate)} 个盘子，盘子要回收清洗。\n先购买并布置设备，同类每台加价原价的 {runtime.Level.ClassicCookerPriceIncreasePercent}%；开门才计时。\n已学技能：{day.Run.DescribeSkills()}";
            }
            else
            {
                heading.text = runtime.Level.LevelTitle;
                summary.text = string.Join("\n", runtime.Level.TutorialSteps);
            }
            for (int index = 0; index < skills.Length; index++)
            {
                skills[index].gameObject.SetActive(results && index < runtime.Classic.Choices.Count);
                if (!skills[index].gameObject.activeSelf) continue;
                KitchenSkill skill = runtime.Classic.Choices[index];
                Caption(skills[index], $"{ClassicDayController.SkillName(skill)}  → {runtime.Classic.Run.Rank(skill) + 1}级\n{ClassicDayController.SkillDescription(skill)}");
            }
        }

        private void Update()
        {
            if (overlay == null || !overlay.activeSelf) return;
            float fit = Mathf.Min(1f, (canvasRect.rect.width - 48f) / 1040f, (canvasRect.rect.height - 48f) / 820f);
            cardLayout.localScale = Vector3.one * Mathf.Max(0.1f, fit);
            entranceSeconds += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(entranceSeconds / 0.2f);
            float spring = 1f - Mathf.Pow(1f - progress, 3f) * Mathf.Cos(progress * Mathf.PI * 2f);
            cardVisual.localScale = Vector3.one * Mathf.LerpUnclamped(0.96f, 1f, spring);
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

        private static void Surface(RectTransform target, Image template, Color color)
        {
            Image image = target.gameObject.AddComponent<Image>();
            image.sprite = template != null ? template.sprite : null;
            image.type = Image.Type.Sliced;
            image.color = color;
        }

        private static TMP_Text Label(Transform parent, string name, string text, TMP_FontAsset font,
            float fontSize, Vector2 size, Vector2 position)
        {
            TMP_Text label = Rect(parent, name, size, position).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = fontSize;
            label.fontSizeMax = fontSize;
            label.fontSizeMin = 20f;
            label.enableAutoSizing = true;
            label.color = new Color32(69, 53, 28, 255);
            label.text = text;
            label.raycastTarget = false;
            return label;
        }

        private static Button Button(Transform parent, string name, Button template, Vector2 size, Vector2 position)
        {
            Button button = Instantiate(template, parent);
            button.name = name;
            button.gameObject.SetActive(true);
            button.interactable = true;
            button.onClick = new Button.ButtonClickedEvent();
            RectTransform rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            PaperButtonStyle.Apply(button, name.StartsWith("SkillChoice_") ? PaperButtonStyle.Choice : PaperButtonStyle.Neutral);
            return button;
        }

        private static void Caption(Button button, string text)
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null) return;
            label.text = text;
            label.fontSize = 26f;
            label.fontSizeMin = 20f;
            label.fontSizeMax = 26f;
            label.enableAutoSizing = true;
        }
    }
}
