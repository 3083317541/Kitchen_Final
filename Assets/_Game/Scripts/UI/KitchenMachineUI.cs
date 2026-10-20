using System;
using System.Collections.Generic;
using Kitchen.Cookers;
using Kitchen.Core;
using Kitchen.Data;
using Kitchen.Input;
using Kitchen.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kitchen.UI
{
    public sealed class KitchenMachineUI : MonoBehaviour
    {
        [SerializeField] private Sprite rounded;
        [SerializeField] private TMP_FontAsset font;
        private static readonly Color Yellow = new Color32(246, 207, 124, 255);
        private static readonly Color Ink = new Color32(69, 53, 28, 255);
        private readonly Dictionary<CookerRuntime, ProgressBadge> badges = new Dictionary<CookerRuntime, ProgressBadge>();
        private readonly List<CookerRuntime> removed = new List<CookerRuntime>();
        private readonly HashSet<CookerRuntime> placed = new HashSet<CookerRuntime>();
        private GameRuntime runtime;
        private InputManager input;
        private RectTransform canvasRect;
        private RectTransform progressLayer;
        private RectTransform modal;
        private RectTransform wheel;
        private RectTransform wheelVisual;
        private CookerRuntime target;
        private float openElapsed;
        private int page;
        public bool IsOpen => modal != null && modal.gameObject.activeSelf;

        private sealed class ProgressBadge
        {
            public RectTransform Root;
            public PaperRingGraphic Ring;
            public PaperRingGraphic WarningRing;
            public TMP_Text Percent;
            public TMP_Text Status;
        }

        public void Configure(Sprite surface, TMP_FontAsset typeface)
        {
            rounded = surface;
            font = typeface;
        }

        public void Initialize(GameRuntime owner, InputManager controls)
        {
            CloseRecipes();
            Unbind();
            runtime = owner;
            input = controls;
            canvasRect = (RectTransform)transform;
            if (progressLayer == null)
            {
                progressLayer = Rect(transform, "MachineProgress", Vector2.zero, Vector2.zero);
                progressLayer.anchorMin = Vector2.zero;
                progressLayer.anchorMax = Vector2.one;
                progressLayer.offsetMin = progressLayer.offsetMax = Vector2.zero;
                progressLayer.SetSiblingIndex(0);
            }
            if (input != null)
            {
                input.CookerRecipeRequested += OpenRecipes;
                input.CancelRequested += CloseRecipes;
                input.ModeChanged += OnModeChanged;
            }
        }

        private void Unbind()
        {
            if (input == null) return;
            input.CookerRecipeRequested -= OpenRecipes;
            input.CancelRequested -= CloseRecipes;
            input.ModeChanged -= OnModeChanged;
        }

        private void OnModeChanged(InteractionMode mode) { CloseRecipes(); }

        public void OpenRecipes(CookerRuntime cooker)
        {
            if (runtime == null || cooker == null || cooker.Owner != runtime || !cooker.IsPlaced || cooker.Definition == null) return;
            CloseRecipes();
            target = cooker;
            page = 0;
            if (modal == null)
            {
                modal = Rect(transform, "RecipeRadialMenu", Vector2.zero, Vector2.zero);
                modal.anchorMin = Vector2.zero;
                modal.anchorMax = Vector2.one;
                modal.offsetMin = modal.offsetMax = Vector2.zero;
                modal.gameObject.AddComponent<Image>().color = new Color(0.2f, 0.16f, 0.08f, 0.12f);
                modal.gameObject.AddComponent<Button>().onClick.AddListener(CloseRecipes);
                wheel = Rect(modal, "Wheel", new Vector2(640f, 680f), Vector2.zero);
            }
            modal.gameObject.SetActive(true);
            modal.SetAsLastSibling();
            input?.SetRecipeMenuOpen(true);
            BuildWheel();
        }

        public void CloseRecipes()
        {
            if (modal != null) modal.gameObject.SetActive(false);
            input?.SetRecipeMenuOpen(false);
            target = null;
        }

        private void BuildWheel()
        {
            if (wheelVisual != null)
            {
                wheelVisual.gameObject.SetActive(false);
                Destroy(wheelVisual.gameObject);
            }
            wheelVisual = Rect(wheel, "Visual", new Vector2(640f, 680f), Vector2.zero);
            Surface(wheelVisual, Color.white);
            RectTransform title = Rect(wheelVisual, "TitleTab", new Vector2(276f, 58f), new Vector2(-145f, 322f));
            Surface(title, Yellow);
            Label(title, "选择加工", new Vector2(260f, 52f), Vector2.zero, 29f);
            Label(wheelVisual, target.Definition.DisplayName, new Vector2(480f, 36f), new Vector2(0f, 254f), 23f);
            List<RecipeDefinition> options = runtime.Recipes.RecipesFor(target.Definition.Kind);
            int start = page * 6;
            int count = Mathf.Min(6, options.Count - start);
            for (int index = 0; index < count; index++)
            {
                RecipeDefinition recipe = options[start + index];
                float angle = (90f - index * 360f / Mathf.Max(3, count)) * Mathf.Deg2Rad;
                Vector2 position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 192f;
                bool selected = target.PreferredRecipeId == recipe.Id;
                Button option = Choice(wheelVisual, recipe.Id, new Vector2(174f, 94f), position,
                    (selected ? "已选 · " : "") + recipe.DisplayName, () => ChooseRecipe(recipe.Id), selected);
                Label(option.transform.Find("Visual"), Ingredients(recipe), new Vector2(156f, 24f), new Vector2(0f, -26f), 16f);
            }
            Choice(wheelVisual, "Automatic", new Vector2(148f, 76f), new Vector2(0f, -12f),
                string.IsNullOrEmpty(target.PreferredRecipeId) ? "已选 · 自动" : "自动选择", () => ChooseRecipe(null), string.IsNullOrEmpty(target.PreferredRecipeId));
            Label(wheelVisual, target.LockedRecipe != null ? "当前批次继续完成，新选择下批生效" : "缺料也可预选，备齐后开始加工",
                new Vector2(580f, 30f), new Vector2(0f, -265f), 20f);
            Label(wheelVisual, "点击空白 / 右键 / Esc 关闭", new Vector2(430f, 28f), new Vector2(0f, -306f), 18f);
            if (options.Count > 6)
            {
                Choice(wheelVisual, "Previous", new Vector2(68f, 40f), new Vector2(-260f, -310f), "上一页",
                    () => { page = (page + (options.Count - 1) / 6) % ((options.Count + 5) / 6); BuildWheel(); }, false);
                Choice(wheelVisual, "Next", new Vector2(68f, 40f), new Vector2(260f, -310f), "下一页",
                    () => { page = (page + 1) % ((options.Count + 5) / 6); BuildWheel(); }, false);
            }
            openElapsed = 0f;
        }

        public void ChooseRecipe(string recipeId)
        {
            if (runtime != null && runtime.TrySetCookerRecipe(target, recipeId)) CloseRecipes();
        }

        private static string Ingredients(RecipeDefinition recipe)
        {
            List<string> parts = new List<string>();
            foreach (RecipeInput ingredient in recipe.Inputs)
                if (ingredient.IsValid) parts.Add(ingredient.Item.DisplayName + " ×" + ingredient.Count);
            return string.Join(" + ", parts);
        }

        private void LateUpdate()
        {
            if (runtime == null || !runtime.IsInitialized || runtime.WorldCamera == null || progressLayer == null) return;
            bool cleared = runtime.UI != null && runtime.UI.LevelClearPanel != null && runtime.UI.LevelClearPanel.activeSelf;
            progressLayer.gameObject.SetActive(!cleared);
            if (IsOpen)
            {
                if (cleared || target == null || !target.IsPlaced || target.Owner != runtime) CloseRecipes();
                else
                {
                    float fit = Mathf.Min(1f, (canvasRect.rect.width - 24f) / 640f, (canvasRect.rect.height - 24f) / 700f);
                    wheel.localScale = Vector3.one * fit;
                    Vector2 half = new Vector2(320f, 350f) * fit;
                    Vector2 point = ScreenPoint(target.WorldPosition + Vector3.up);
                    wheel.anchoredPosition = new Vector2(Mathf.Clamp(point.x, canvasRect.rect.xMin + half.x, canvasRect.rect.xMax - half.x),
                        Mathf.Clamp(point.y, canvasRect.rect.yMin + half.y, canvasRect.rect.yMax - half.y));
                    openElapsed += Time.unscaledDeltaTime;
                    float phase = Mathf.Clamp01(openElapsed / 0.26f);
                    wheelVisual.localScale = Vector3.one * (1f - 0.12f * Mathf.Exp(-6f * phase) * Mathf.Cos(10f * phase));
                }
            }
            removed.Clear();
            placed.Clear();
            foreach (CookerRuntime cooker in runtime.Cookers.Cookers) placed.Add(cooker);
            foreach (KeyValuePair<CookerRuntime, ProgressBadge> pair in badges)
                if (!pair.Key.IsPlaced || pair.Key.Owner != runtime || !placed.Contains(pair.Key)) removed.Add(pair.Key);
            foreach (CookerRuntime cooker in removed)
            {
                badges[cooker].Root.gameObject.SetActive(false);
                Destroy(badges[cooker].Root.gameObject);
                badges.Remove(cooker);
            }
            foreach (CookerRuntime cooker in runtime.Cookers.Cookers)
            {
                if (!cooker.IsPlaced) continue;
                if (!badges.TryGetValue(cooker, out ProgressBadge badge))
                {
                    badge = CreateBadge(cooker);
                    badges.Add(cooker, badge);
                }
                Vector3 world = (cooker.View != null ? cooker.View.transform.position : cooker.WorldPosition) + Vector3.up * 2.15f;
                Vector3 viewport = runtime.WorldCamera.WorldToViewportPoint(world);
                bool visible = viewport.z > 0f && viewport.x > 0f && viewport.x < 1f && viewport.y > 0f && viewport.y < 1f &&
                    (cooker == runtime.Cookers.Selected || cooker.LockedRecipe != null || cooker.HasAnyOutput || cooker.State != CookerState.Idle);
                badge.Root.gameObject.SetActive(visible);
                if (!visible) continue;
                badge.Root.anchoredPosition = ScreenPoint(world);
                badge.Root.localScale = Vector3.one * Mathf.Min(1f, canvasRect.rect.height / 900f);
                bool ready = cooker.State == CookerState.ReadyToCollect || cooker.State == CookerState.Blocked;
                badge.Ring.Progress = ready || cooker.IsOvercooking ? 1f : cooker.WorkProgress;
                badge.Ring.color = cooker.HasAnyOutput && cooker.HeatStopped ? new Color32(98, 173, 118, 255) : Yellow;
                badge.WarningRing.Progress = cooker.BurnProgress;
                badge.Percent.text = cooker.IsOvercooking ? $"{cooker.BurnProgress * 100f:0}%" :
                    ready ? "完成" : cooker.State == CookerState.Burnt ? "焦了" : $"{cooker.WorkProgress * 100f:0}%";
                badge.Status.text = Status(cooker);
            }
        }

        private Vector2 ScreenPoint(Vector3 world)
        {
            Canvas canvas = GetComponent<Canvas>();
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, runtime.WorldCamera.WorldToScreenPoint(world), uiCamera, out Vector2 local);
            return local;
        }

        private ProgressBadge CreateBadge(CookerRuntime cooker)
        {
            RectTransform root = Rect(progressLayer, "CookerProgress_" + cooker.Id, new Vector2(154f, 118f), Vector2.zero);
            Surface(root, Color.white).raycastTarget = false;
            RectTransform ringRect = Rect(root, "Track", new Vector2(76f, 76f), new Vector2(0f, 14f));
            PaperRingGraphic track = ringRect.gameObject.AddComponent<PaperRingGraphic>();
            track.color = new Color32(247, 240, 221, 255);
            track.raycastTarget = false;
            RectTransform fill = Rect(root, "Progress", new Vector2(76f, 76f), new Vector2(0f, 14f));
            PaperRingGraphic ring = fill.gameObject.AddComponent<PaperRingGraphic>();
            ring.color = Yellow;
            ring.raycastTarget = false;
            RectTransform warning = Rect(root, "OvercookProgress", new Vector2(76f, 76f), new Vector2(0f, 14f));
            PaperRingGraphic warningRing = warning.gameObject.AddComponent<PaperRingGraphic>();
            warningRing.color = new Color32(222, 65, 55, 255);
            warningRing.Progress = 0f;
            warningRing.raycastTarget = false;
            return new ProgressBadge { Root = root, Ring = ring, WarningRing = warningRing,
                Percent = Label(root, "0%", new Vector2(64f, 30f), new Vector2(0f, 14f), 21f),
                Status = Label(root, "等待员工", new Vector2(146f, 26f), new Vector2(0f, -40f), 19f) };
        }

        private static string Status(CookerRuntime cooker)
        {
            if (cooker.IsOvercooking) return "过火 · 满圈烤糊";
            if (cooker.HasAnyOutput && cooker.HeatStopped && cooker.State != CookerState.Collecting) return "已关火 · 安全待取";
            switch (cooker.State)
            {
                case CookerState.Preparing: return "装料中";
                case CookerState.Collecting: return cooker.IsActive ? "收取中" : "已关火 · 安全待取";
                case CookerState.Cleaning: return "清理中";
                case CookerState.Burnt: return "需要清理";
                case CookerState.ReadyToCollect: return "等待收取";
                case CookerState.Blocked: return "成品待取";
                case CookerState.WaitingForInput: return "等待食材";
                default: return cooker.IsHeating ? "加热中" : cooker.LockedRecipe != null && !cooker.IsActive ? "暂停 · 等待员工" :
                    cooker.LockedRecipe != null ? cooker.LockedRecipe.DisplayName : "右键选择加工";
            }
        }

        private Button Choice(Transform parent, string name, Vector2 size, Vector2 position, string caption, Action clicked, bool selected)
        {
            RectTransform root = Rect(parent, name, size, position);
            Image hit = root.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            Button button = root.gameObject.AddComponent<Button>();
            RectTransform visual = Rect(root, "Visual", size, Vector2.zero);
            Image tray = Surface(visual, Yellow);
            tray.raycastTarget = false;
            Image face = Surface(Rect(visual, "Face", size - new Vector2(8f, 10f), new Vector2(0f, 3f)), selected ? new Color32(255, 244, 209, 255) : Color.white);
            face.raycastTarget = false;
            Label(visual, caption, size - new Vector2(14f, 16f), size.y > 80f ? new Vector2(0f, 12f) : Vector2.zero, 23f);
            button.targetGraphic = tray;
            button.onClick.AddListener(() => clicked());
            root.gameObject.AddComponent<PaperButtonMotion>().Configure(visual);
            PaperButtonStyle.Apply(button, selected ? PaperButtonStyle.Positive : PaperButtonStyle.Primary);
            return button;
        }

        private Image Surface(RectTransform rect, Color color)
        {
            Image surface = rect.gameObject.AddComponent<Image>();
            surface.sprite = rounded;
            surface.type = Image.Type.Sliced;
            surface.color = color;
            return surface;
        }

        private TMP_Text Label(Transform parent, string caption, Vector2 size, Vector2 position, float fontSize)
        {
            TextMeshProUGUI label = Rect(parent, "Label", size, position).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = caption;
            label.fontSize = fontSize;
            label.enableAutoSizing = true;
            label.fontSizeMin = 13f;
            label.fontSizeMax = fontSize;
            label.color = Ink;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 size, Vector2 position)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private void OnDisable() { CloseRecipes(); }
        private void OnDestroy() { CloseRecipes(); Unbind(); }
    }
}
