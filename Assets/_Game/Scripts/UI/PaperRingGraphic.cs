using UnityEngine;
using UnityEngine.UI;

namespace Kitchen.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PaperRingGraphic : MaskableGraphic
    {
        private float progress = 1f;

        public float Progress
        {
            get => progress;
            set
            {
                float next = Mathf.Clamp01(value);
                if (Mathf.Approximately(next, progress)) return;
                progress = next;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (progress <= 0f) return;
            Rect bounds = rectTransform.rect;
            float radius = Mathf.Min(bounds.width, bounds.height) * 0.5f - 5f;
            const float thickness = 8f;
            int segments = Mathf.Max(1, Mathf.CeilToInt(progress * 100f));
            for (int index = 0; index <= segments; index++)
            {
                float angle = Mathf.PI * 0.5f - progress * Mathf.PI * 2f * index / segments;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                mesh.AddVert(bounds.center + direction * (radius - thickness * 0.5f), color, Vector2.zero);
                mesh.AddVert(bounds.center + direction * (radius + thickness * 0.5f), color, Vector2.zero);
                if (index == 0) continue;
                int vertex = index * 2;
                mesh.AddTriangle(vertex - 2, vertex - 1, vertex);
                mesh.AddTriangle(vertex, vertex - 1, vertex + 1);
            }
            if (progress >= 0.999f) return;
            AddCap(mesh, bounds.center + Vector2.up * radius, thickness * 0.5f);
            float end = Mathf.PI * 0.5f - progress * Mathf.PI * 2f;
            AddCap(mesh, bounds.center + new Vector2(Mathf.Cos(end), Mathf.Sin(end)) * radius, thickness * 0.5f);
        }

        private void AddCap(VertexHelper mesh, Vector2 center, float radius)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(center, color, Vector2.zero);
            for (int index = 0; index <= 16; index++)
            {
                float angle = index * Mathf.PI * 2f / 16f;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
                if (index > 0) mesh.AddTriangle(start, start + index, start + index + 1);
            }
        }
    }
}
