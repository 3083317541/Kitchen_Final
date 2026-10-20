using UnityEngine;

namespace Kitchen.UI
{
    public sealed class PaperHudLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform[] panels;
        [SerializeField] private Vector2[] positions;
        [SerializeField] private RectTransform topBar;
        [SerializeField] private RectTransform progress;
        private Vector2 lastSize;

        public void Configure(RectTransform[] targets, RectTransform status, RectTransform work)
        {
            panels = targets;
            positions = new Vector2[targets.Length];
            for (int index = 0; index < targets.Length; index++) positions[index] = targets[index].anchoredPosition;
            topBar = status;
            progress = work;
        }

        private void LateUpdate() { RefreshLayout(); }

        public void RefreshLayout()
        {
            Vector2 size = ((RectTransform)transform).rect.size;
            if (size == lastSize || size.x <= 0f || size.y <= 0f) return;
            lastSize = size;
            float fit = Mathf.Min(1f, size.x / 1500f, size.y / 1080f);
            for (int index = 0; index < panels.Length; index++)
            {
                panels[index].localScale = Vector3.one * fit;
                panels[index].anchoredPosition = positions[index] * fit;
            }
            topBar.sizeDelta = new Vector2(size.x / fit - 332f, 140f);
            if (progress != null) progress.sizeDelta = new Vector2(size.x / fit - 64f, 112f);
        }
    }
}
