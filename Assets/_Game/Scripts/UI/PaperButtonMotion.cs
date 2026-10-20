using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kitchen.UI
{
    public sealed class PaperButtonMotion : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IPointerExitHandler, ISelectHandler, IDeselectHandler, ISubmitHandler, ICancelHandler
    {
        [SerializeField] private RectTransform visual;
        private Button button;
        private float elapsed = 1f;
        private Vector3 start = Vector3.one;

        public void Configure(RectTransform target) { visual = target; }
        private void Awake() { button = GetComponent<Button>(); }

        public void Pulse()
        {
            if (visual == null) return;
            start = new Vector3(1.06f, 0.94f, 1f);
            visual.localScale = start;
            elapsed = 0f;
        }

        private void Update()
        {
            if (visual == null) return;
            if (button != null && !button.interactable) { ResetVisual(); return; }
            if (elapsed >= 0.32f) return;
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / 0.32f);
            float spring = 1f - Mathf.Exp(-7f * progress) * Mathf.Cos(11f * progress);
            visual.localScale = Vector3.LerpUnclamped(start, Vector3.one, spring);
            if (progress >= 1f) visual.localScale = Vector3.one;
        }

        private void Release()
        {
            if (visual == null) return;
            start = visual.localScale;
            elapsed = 0f;
        }

        private void ResetVisual()
        {
            elapsed = 1f;
            if (visual != null) visual.localScale = Vector3.one;
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || button == null || !button.IsInteractable()) return;
            elapsed = 1f;
            if (visual != null) visual.localScale = new Vector3(0.97f, 0.91f, 1f);
        }

        public void OnPointerUp(PointerEventData data) { Release(); }
        public void OnPointerExit(PointerEventData data) { Release(); }
        public void OnSelect(BaseEventData data) { Pulse(); }
        public void OnDeselect(BaseEventData data) { Release(); }
        public void OnSubmit(BaseEventData data) { Pulse(); }
        public void OnCancel(BaseEventData data) { ResetVisual(); }
        private void OnDisable() { ResetVisual(); }
        private void OnApplicationFocus(bool focused) { if (!focused) ResetVisual(); }
    }
}
