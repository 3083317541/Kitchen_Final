using UnityEngine;
using UnityEngine.UI;

namespace Kitchen.UI
{
    [RequireComponent(typeof(Image))]
    public sealed class PaperProgressFill : MonoBehaviour
    {
        private Image fill;

        private void Awake() { fill = GetComponent<Image>(); }

        private void LateUpdate()
        {
            float amount = Mathf.Clamp01(fill.fillAmount);
            fill.enabled = amount > 0f;
            fill.rectTransform.anchorMax = new Vector2(amount, 1f);
        }
    }
}
