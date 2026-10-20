using UnityEngine;

namespace Kitchen.UI
{
    public sealed class PaperMenuPageMotion : MonoBehaviour
    {
        [SerializeField] private RectTransform visual;
        [SerializeField] private CanvasGroup group;
        private float elapsed;

        public void Configure(RectTransform target)
        {
            visual = target;
            group = target.gameObject.AddComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            elapsed = 0f;
            Apply(PaperUISettings.MotionEnabled ? 0f : 1f);
        }

        private void Update()
        {
            if (elapsed >= 0.22f) return;
            elapsed += Time.unscaledDeltaTime;
            Apply(PaperUISettings.MotionEnabled ? Mathf.Clamp01(elapsed / 0.22f) : 1f);
        }

        private void Apply(float progress)
        {
            if (visual == null || group == null) return;
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            visual.localScale = Vector3.one * Mathf.Lerp(0.97f, 1f, eased);
            visual.anchoredPosition = Vector2.down * (8f * (1f - eased));
            group.alpha = Mathf.Clamp01(progress * 2f);
            group.interactable = group.blocksRaycasts = progress >= 0.6f;
        }

        private void OnDisable() { Apply(1f); }
    }
}
