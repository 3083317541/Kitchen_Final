using UnityEngine;

namespace Kitchen.UI
{
    [ExecuteAlways]
    public sealed class PaperMenuLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform composition;
        [SerializeField] private RectTransform brand;
        [SerializeField] private RectTransform card;
        private Vector2 previousSize;

        public void Configure(RectTransform root, RectTransform logo, RectTransform menu)
        {
            composition = root;
            brand = logo;
            card = menu;
            RefreshLayout();
        }

        private void LateUpdate()
        {
            RectTransform canvas = transform as RectTransform;
            if (canvas != null && previousSize != canvas.rect.size) RefreshLayout();
        }

        public void RefreshLayout()
        {
            if (composition == null || brand == null || card == null) return;
            RectTransform canvas = transform as RectTransform;
            if (canvas == null) return;
            previousSize = canvas.rect.size;
            bool split = previousSize.x / Mathf.Max(1f, previousSize.y) >= 1.2f;
            Vector2 size = split ? new Vector2(1460f, 740f) : new Vector2(760f, 1240f);
            composition.sizeDelta = size;
            float scale = Mathf.Min((previousSize.x - 120f) / size.x, (previousSize.y - 150f) / size.y);
            composition.localScale = Vector3.one * Mathf.Clamp(scale, 0.1f, 1.25f);
            brand.anchoredPosition = split ? new Vector2(0f, -130f) : new Vector2(20f, -25f);
            card.anchoredPosition = split ? new Vector2(820f, -80f) : new Vector2(70f, -560f);
        }
    }
}
