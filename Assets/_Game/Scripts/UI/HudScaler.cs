using UnityEngine;
using UnityEngine.UI;

namespace Kitchen.UI
{
    /// <summary>
    /// Scales the whole HUD at runtime.
    ///
    /// A smaller canvas reference resolution makes every anchored element bigger, so one number scales
    /// the entire interface without any panel knowing about it. This lives on the canvas rather than in
    /// the UI manager because it is presentation state, and because it can then be exercised without
    /// building a HUD.
    /// </summary>
    [DisallowMultipleComponent]
    public class HudScaler : MonoBehaviour
    {
        /// <summary>PlayerPrefs key the chosen scale is stored under.</summary>
        public const string PrefsKey = "kitchen.uiScale";

        /// <summary>Bounds chosen so the HUD can be made larger or smaller but never unusable.</summary>
        public const float MinScale = 0.7f;

        public const float MaxScale = 1.8f;

        [Tooltip("Canvas to scale. Defaults to the canvas on this object.")]
        [SerializeField] private Canvas targetCanvas;

        [Tooltip("Reference resolution the scale factor is applied to.")]
        [SerializeField] private Vector2 baseReferenceResolution = new Vector2(1920f, 1080f);

        /// <summary>Current scale. 1 means the authored layout.</summary>
        public float Scale { get; private set; } = 1f;

        /// <summary>Binds the canvas to scale and the reference resolution it is scaled from.</summary>
        public void Configure(Canvas canvas, Vector2 baseReference)
        {
            targetCanvas = canvas;
            baseReferenceResolution = baseReference;
        }

        /// <summary>Reads the stored scale and applies it.</summary>
        public void LoadAndApply()
        {
            SetScale(PlayerPrefs.GetFloat(PrefsKey, 1f), false);
        }

        /// <summary>Steps the scale by a delta and stores the result.</summary>
        public void Step(float delta)
        {
            SetScale(Scale + delta, true);
        }

        /// <summary>Sets, clamps, applies and optionally stores the scale.</summary>
        public void SetScale(float value, bool persist = true)
        {
            Scale = Mathf.Clamp(value, MinScale, MaxScale);
            Apply();

            if (persist)
            {
                PlayerPrefs.SetFloat(PrefsKey, Scale);
                PlayerPrefs.Save();
            }
        }

        private void Apply()
        {
            Canvas canvas = targetCanvas != null ? targetCanvas : GetComponent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                return;
            }

            // Dividing by the scale grows the HUD, because every element is sized in reference units.
            scaler.referenceResolution = new Vector2(
                Mathf.Max(320f, baseReferenceResolution.x / Scale),
                Mathf.Max(180f, baseReferenceResolution.y / Scale));
        }
    }
}
