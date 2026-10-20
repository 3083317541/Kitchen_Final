using UnityEngine;
using UnityEngine.UI;

namespace Kitchen.UI
{
    public sealed class PaperShopLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform content;

        public void Configure(RectTransform target) { content = target; }

        private void LateUpdate()
        {
            if (content == null) return;
            float height = LayoutUtility.GetPreferredHeight(content) + 72f;
            RectTransform panel = (RectTransform)transform;
            if (Mathf.Abs(panel.sizeDelta.y - height) > 0.5f)
                panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }
    }
}
