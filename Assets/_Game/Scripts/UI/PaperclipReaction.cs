using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Kitchen.UI
{
    public sealed class PaperclipReaction : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private RectTransform visual;
        [SerializeField] private RectTransform eyes;
        [SerializeField] private TMP_Text bubble;
        private float elapsed = 10f;
        private int phrase;
        private static readonly string[] Phrases = { "慢慢来，也会很好吃！", "今天也要好好吃饭。", "开工啦，厨师长！" };

        public void Configure(RectTransform target, RectTransform face, TMP_Text speech)
        {
            visual = target;
            eyes = face;
            bubble = speech;
        }

        public void OnPointerClick(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            elapsed = 0f;
            bubble.text = Phrases[phrase++ % Phrases.Length];
            bubble.transform.parent.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (elapsed > 2.3f) return;
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / 0.6f);
            visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(progress * Mathf.PI * 4f) * (1f - progress) * 16f);
            visual.localScale = Vector3.one * (1f + Mathf.Sin(progress * Mathf.PI) * 0.12f);
            eyes.localScale = new Vector3(1f, elapsed > 0.13f && elapsed < 0.25f ? 0.15f : 1f, 1f);
            if (elapsed >= 2.3f) bubble.transform.parent.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            elapsed = 10f;
            if (visual != null) { visual.localRotation = Quaternion.identity; visual.localScale = Vector3.one; }
            if (eyes != null) eyes.localScale = Vector3.one;
            if (bubble != null) bubble.transform.parent.gameObject.SetActive(false);
        }
    }
}
