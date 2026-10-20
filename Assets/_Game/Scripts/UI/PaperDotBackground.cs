using UnityEngine;
using UnityEngine.UI;

namespace Kitchen.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PaperDotBackground : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Rect bounds = rectTransform.rect;
            const float spacing = 32f;
            for (float vertical = bounds.yMin + 16f; vertical < bounds.yMax; vertical += spacing)
            {
                for (float horizontal = bounds.xMin + 16f; horizontal < bounds.xMax; horizontal += spacing)
                {
                    int center = helper.currentVertCount;
                    helper.AddVert(new Vector3(horizontal, vertical), color, Vector2.zero);
                    for (int segment = 0; segment <= 8; segment++)
                    {
                        float angle = segment * Mathf.PI / 4f;
                        helper.AddVert(new Vector3(horizontal + Mathf.Cos(angle) * 1.4f,
                            vertical + Mathf.Sin(angle) * 1.4f), color, Vector2.zero);
                        if (segment > 0) helper.AddTriangle(center, center + segment, center + segment + 1);
                    }
                }
            }
        }
    }
}
