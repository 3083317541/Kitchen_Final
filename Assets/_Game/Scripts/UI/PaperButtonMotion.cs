using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kitchen.UI
{
    public sealed class PaperButtonMotion : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler,
        IPointerUpHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, ISubmitHandler, ICancelHandler
    {
        [SerializeField] private RectTransform visual;
        [SerializeField] private bool solidSurface;
        [SerializeField] private Color solidColor;
        private Button button;
        private Image face;
        private Color faceColor;
        private Vector2 restPosition;
        private Quaternion restRotation;
        private Vector3 restScale;
        private Vector3 springVelocity;
        private float angleVelocity;
        private float offsetVelocity;
        private float angle;
        private float offset;
        private bool hovered;
        private bool focused;
        private bool pressed;
        private bool initialized;

        public void Configure(RectTransform target)
        {
            visual = target;
            if (Application.isPlaying) Initialize();
        }

        public void ConfigureSolid(Color color)
        {
            solidSurface = true;
            solidColor = color;
            faceColor = color;
            if (face != null) face.color = color;
        }

        private void Awake() { Initialize(); }

        private void Initialize()
        {
            button = GetComponent<Button>();
            if (visual == null) return;
            restPosition = visual.anchoredPosition;
            restRotation = visual.localRotation;
            restScale = visual.localScale;
            Transform surface = visual.Find("Face");
            if (surface != null) face = surface.GetComponent<Image>();
            if (face != null) faceColor = solidSurface ? solidColor : face.color;
            initialized = true;
        }

        public void Pulse()
        {
            if (!initialized || !PaperUISettings.MotionEnabled) return;
            visual.localScale = Vector3.Scale(restScale, new Vector3(1.05f, 0.95f, 1f));
            springVelocity = Vector3.zero;
        }

        private void Update()
        {
            if (!initialized) return;
            if (button == null || !button.IsInteractable())
            {
                ResetVisual();
                if (face != null && solidSurface)
                    face.color = Color.Lerp(faceColor, new Color32(202, 205, 203, 255), 0.8f);
                return;
            }
            bool active = hovered || focused;
            if (face != null)
            {
                Color target = solidSurface
                    ? pressed ? Color.Lerp(faceColor, Color.black, 0.15f)
                        : active ? Color.Lerp(faceColor, Color.white, 0.26f) : faceColor
                    : pressed ? Color.Lerp(faceColor, new Color(0.94f, 0.85f, 0.65f), 0.45f)
                        : active ? Color.Lerp(faceColor, new Color(1f, 0.96f, 0.83f), 0.65f) : faceColor;
                face.color = Color.Lerp(face.color, target, 1f - Mathf.Exp(-24f * Time.unscaledDeltaTime));
            }
            if (!PaperUISettings.MotionEnabled) { RestoreTransform(); return; }
            Vector3 targetScale = Vector3.Scale(restScale, pressed ? new Vector3(0.97f, 0.94f, 1f)
                : active ? new Vector3(1.025f, 1.025f, 1f) : Vector3.one);
            float targetAngle = active && !pressed ? 0.6f : 0f;
            float targetOffset = pressed ? -2f : active ? 4f : 0f;
            float remaining = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            while (remaining > 0f)
            {
                float step = Mathf.Min(remaining, 1f / 120f);
                springVelocity += ((targetScale - visual.localScale) * 600f - springVelocity * 34f) * step;
                visual.localScale += springVelocity * step;
                angleVelocity += ((targetAngle - angle) * 600f - angleVelocity * 34f) * step;
                angle += angleVelocity * step;
                offsetVelocity += ((targetOffset - offset) * 600f - offsetVelocity * 34f) * step;
                offset += offsetVelocity * step;
                remaining -= step;
            }
            visual.anchoredPosition = restPosition + Vector2.up * offset;
            visual.localRotation = restRotation * Quaternion.Euler(0f, 0f, angle);
        }

        private void RestoreTransform()
        {
            if (!initialized) return;
            visual.localScale = restScale;
            visual.anchoredPosition = restPosition;
            visual.localRotation = restRotation;
            springVelocity = Vector3.zero;
            angleVelocity = offsetVelocity = angle = offset = 0f;
        }

        private void ResetVisual()
        {
            hovered = focused = pressed = false;
            RestoreTransform();
            if (face != null) face.color = faceColor;
        }

        public void OnPointerEnter(PointerEventData data) { hovered = true; }
        public void OnPointerExit(PointerEventData data) { hovered = pressed = false; }

        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || button == null || !button.IsInteractable()) return;
            pressed = true;
            focused = false;
            if (face != null && solidSurface) face.color = Color.Lerp(faceColor, Color.black, 0.15f);
            if (visual != null && PaperUISettings.MotionEnabled)
                visual.localScale = Vector3.Scale(restScale, new Vector3(0.97f, 0.94f, 1f));
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            pressed = false;
        }

        public void OnSelect(BaseEventData data) { focused = !(data is PointerEventData); }
        public void OnDeselect(BaseEventData data) { focused = false; }
        public void OnSubmit(BaseEventData data) { Pulse(); }
        public void OnCancel(BaseEventData data) { ResetVisual(); }
        private void OnDisable() { ResetVisual(); }
        private void OnApplicationFocus(bool value) { if (!value) ResetVisual(); }
    }
}
