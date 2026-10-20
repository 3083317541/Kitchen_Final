using UnityEngine;

namespace Kitchen.UI
{
    public sealed class PaperMenuEntrance : MonoBehaviour
    {
        [SerializeField] private RectTransform visual;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private float delay;
        private float elapsed;
        private Quaternion rotation;

        public void Configure(RectTransform target, float startDelay)
        {
            visual = target;
            group = target.gameObject.AddComponent<CanvasGroup>();
            delay = startDelay;
        }

        private void Awake()
        {
            rotation = visual.localRotation;
            if (!PaperUISettings.MotionEnabled) { Finish(); return; }
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            visual.localScale = Vector3.one * 0.94f;
        }

        private void Update()
        {
            if (!PaperUISettings.MotionEnabled) { Finish(); return; }
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01((elapsed - delay) / 0.48f);
            float spring = 1f - Mathf.Pow(1f - progress, 3f) * Mathf.Cos(progress * Mathf.PI * 2f);
            visual.localScale = Vector3.one * Mathf.LerpUnclamped(0.94f, 1f, spring);
            visual.localRotation = rotation * Quaternion.Euler(0f, 0f, -3f * (1f - spring));
            group.alpha = Mathf.Clamp01(progress * 3f);
            if (progress >= 1f) Finish();
        }

        private void Finish()
        {
            visual.localScale = Vector3.one;
            visual.localRotation = rotation;
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
            enabled = false;
        }
    }
}
