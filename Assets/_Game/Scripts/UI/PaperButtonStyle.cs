using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kitchen.UI
{
    public static class PaperButtonStyle
    {
        public static readonly Color Primary = new Color32(181, 202, 239, 255);
        public static readonly Color Positive = new Color32(163, 215, 186, 255);
        public static readonly Color Neutral = new Color32(225, 226, 220, 255);
        public static readonly Color Danger = new Color32(237, 171, 163, 255);
        public static readonly Color Choice = new Color32(206, 193, 231, 255);
        public static readonly Color Ink = new Color32(36, 48, 62, 255);

        public static void Apply(Button button, Color color)
        {
            RectTransform visual = button.transform.Find("Visual") as RectTransform;
            if (visual == null) return;
            Image tray = visual.GetComponent<Image>();
            if (tray != null) tray.color = Color.clear;
            RectTransform face = visual.Find("Face") as RectTransform;
            if (face == null) return;
            face.anchorMin = Vector2.zero;
            face.anchorMax = Vector2.one;
            face.offsetMin = face.offsetMax = Vector2.zero;
            Image surface = face.GetComponent<Image>();
            surface.color = color;
            surface.raycastTarget = false;
            foreach (TMP_Text label in visual.GetComponentsInChildren<TMP_Text>(true))
                label.color = Ink;
            button.targetGraphic = surface;
            button.transition = Selectable.Transition.None;
            PaperButtonMotion motion = button.GetComponent<PaperButtonMotion>();
            if (motion == null) motion = button.gameObject.AddComponent<PaperButtonMotion>();
            motion.Configure(visual);
            motion.ConfigureSolid(color);
        }
    }
}
