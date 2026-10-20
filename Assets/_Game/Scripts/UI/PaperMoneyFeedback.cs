using TMPro;
using UnityEngine;

namespace Kitchen.UI
{
    public sealed class PaperMoneyFeedback : MonoBehaviour
    {
        [SerializeField] private RectTransform visual;
        private bool initialized;
        private int previous;
        private float remaining;

        public void ResetValue()
        {
            initialized = false;
            remaining = 0f;
            if (visual != null) visual.localScale = Vector3.one;
        }

        public void Configure(RectTransform target) { visual = target; }

        public void Display(int value)
        {
            GetComponent<TMP_Text>().text = value.ToString();
            if (initialized && previous != value) remaining = 0.4f;
            previous = value;
            initialized = true;
        }

        private void Update()
        {
            if (visual == null || remaining <= 0f) return;
            remaining = Mathf.Max(0f, remaining - Time.unscaledDeltaTime);
            float progress = 1f - remaining / 0.4f;
            float bump = Mathf.Sin(progress * Mathf.PI * 2f) * Mathf.Exp(-4f * progress) * 0.18f;
            visual.localScale = Vector3.one * (1f + bump);
        }

        private void OnDisable()
        {
            initialized = false;
            remaining = 0f;
            if (visual != null) visual.localScale = Vector3.one;
        }
    }
}
