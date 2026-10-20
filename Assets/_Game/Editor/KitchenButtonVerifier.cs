using System;
using System.Collections.Generic;
using Kitchen.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kitchen.EditorTools
{
    public static class KitchenButtonVerifier
    {
        public static void Verify(Transform root, Action<string, bool> check)
        {
            int motionSetting = PlayerPrefs.GetInt(PaperUISettings.MotionKey, 1);
            PlayerPrefs.SetInt(PaperUISettings.MotionKey, 1);
            try
            {
                foreach (Button button in root.GetComponentsInChildren<Button>(true))
                {
                    if (button.transform.Find("Visual") == null) continue;
                    var hidden = new List<GameObject>();
                    for (Transform ancestor = button.transform; ancestor != null; ancestor = ancestor.parent)
                        if (!ancestor.gameObject.activeSelf) hidden.Add(ancestor.gameObject);
                    for (int index = hidden.Count - 1; index >= 0; index--) hidden[index].SetActive(true);
                    try { VerifyButton(button, check); }
                    finally { foreach (GameObject target in hidden) target.SetActive(false); }
                }
            }
            finally { PlayerPrefs.SetInt(PaperUISettings.MotionKey, motionSetting); }
        }

        private static void VerifyButton(Button button, Action<string, bool> check)
        {
            PaperButtonMotion motion = button.GetComponent<PaperButtonMotion>();
            RectTransform visual = button.transform.Find("Visual") as RectTransform;
            Image face = visual.Find("Face").GetComponent<Image>();
            string prefix = "button_" + button.name + "_";
            check(prefix + "solid_surface_and_feedback", motion != null &&
                visual.GetComponent<Image>().color.a == 0f && face.color.a == 1f &&
                face.rectTransform.offsetMin == Vector2.zero && face.rectTransform.offsetMax == Vector2.zero &&
                button.transition == Selectable.Transition.None);
            if (motion == null) return;
            bool interactable = button.interactable;
            button.interactable = true;
            motion.OnCancel(new BaseEventData(EventSystem.current));
            Settle(motion);
            Color normal = face.color;
            Vector3 restScale = visual.localScale;
            Vector2 restPosition = visual.anchoredPosition;
            RectTransform layout = (RectTransform)button.transform;
            Vector2 layoutPosition = layout.anchoredPosition;
            Vector3 layoutScale = layout.localScale;
            PointerEventData pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            try
            {
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                Settle(motion);
                Color hover = face.color;
                check(prefix + "hover", visual.localScale.x > restScale.x + 0.005f &&
                    visual.anchoredPosition.y > restPosition.y + 1f && ColorDistance(normal, hover) > 0.02f);
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                Settle(motion);
                check(prefix + "press", visual.localScale.x < restScale.x - 0.005f &&
                    visual.localScale.y < restScale.y - 0.02f && face.color.grayscale < normal.grayscale - 0.03f);
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                Settle(motion);
                check(prefix + "release_returns_to_hover", visual.localScale.x > restScale.x + 0.005f && ColorDistance(face.color, hover) < 0.01f);
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerExitHandler);
                Settle(motion);
                check(prefix + "exit_and_stable_hit_area", Vector3.Distance(visual.localScale, restScale) < 0.002f &&
                    Vector2.Distance(visual.anchoredPosition, restPosition) < 0.01f &&
                    ColorDistance(face.color, normal) < 0.01f && layout.anchoredPosition == layoutPosition && layout.localScale == layoutScale);
                motion.OnPointerEnter(pointer);
                motion.OnPointerDown(pointer);
                button.interactable = false;
                Settle(motion);
                check(prefix + "disabled_clears_press_and_dims", Vector3.Distance(visual.localScale, restScale) < 0.002f && ColorDistance(face.color, normal) > 0.02f);
                button.interactable = true;
                Settle(motion);
                check(prefix + "reenabled_restores_color", ColorDistance(face.color, normal) < 0.01f);
                PlayerPrefs.SetInt(PaperUISettings.MotionKey, 0);
                motion.OnPointerEnter(pointer);
                Settle(motion);
                bool hoverWithoutMotion = ColorDistance(face.color, normal) > 0.02f && Vector3.Distance(visual.localScale, restScale) < 0.002f;
                motion.OnPointerDown(pointer);
                Settle(motion);
                check(prefix + "colors_with_motion_disabled", hoverWithoutMotion &&
                    face.color.grayscale < normal.grayscale - 0.03f && Vector3.Distance(visual.localScale, restScale) < 0.002f);
            }
            finally
            {
                PlayerPrefs.SetInt(PaperUISettings.MotionKey, 1);
                motion.OnCancel(new BaseEventData(EventSystem.current));
                button.interactable = interactable;
                Settle(motion);
            }
        }

        private static void Settle(PaperButtonMotion motion)
        {
            float step = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            int frames = step > 0f ? Mathf.CeilToInt(1.2f / step) : 0;
            for (int frame = 0; frame < frames; frame++) motion.SendMessage("Update");
        }

        private static float ColorDistance(Color first, Color second)
        {
            return Mathf.Abs(first.r - second.r) + Mathf.Abs(first.g - second.g) + Mathf.Abs(first.b - second.b);
        }
    }
}
