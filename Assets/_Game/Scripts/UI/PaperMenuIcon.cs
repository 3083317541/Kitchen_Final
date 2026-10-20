using UnityEngine;
using UnityEngine.UI;

namespace Kitchen.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PaperMenuIcon : MaskableGraphic
    {
        public enum Shape { Book, Hat, Settings, Arrow }
        [SerializeField] private Shape shape;
        public void Configure(Shape value) { shape = value; SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            switch (shape)
            {
                case Shape.Book:
                    Path(mesh, new Vector2(0.5f, 0.18f), new Vector2(0.28f, 0.26f), new Vector2(0.12f, 0.26f), new Vector2(0.12f, 0.8f), new Vector2(0.3f, 0.8f), new Vector2(0.5f, 0.71f), new Vector2(0.7f, 0.8f), new Vector2(0.88f, 0.8f), new Vector2(0.88f, 0.26f), new Vector2(0.72f, 0.26f), new Vector2(0.5f, 0.18f), new Vector2(0.5f, 0.71f));
                    break;
                case Shape.Hat:
                    Path(mesh, new Vector2(0.26f, 0.35f), new Vector2(0.26f, 0.14f), new Vector2(0.74f, 0.14f), new Vector2(0.74f, 0.35f));
                    Path(mesh, new Vector2(0.26f, 0.27f), new Vector2(0.74f, 0.27f));
                    Arc(mesh, new Vector2(0.29f, 0.57f), 0.2f, 90f, 270f);
                    Arc(mesh, new Vector2(0.5f, 0.7f), 0.22f, 0f, 180f);
                    Arc(mesh, new Vector2(0.71f, 0.57f), 0.2f, -90f, 90f);
                    Path(mesh, new Vector2(0.29f, 0.37f), new Vector2(0.26f, 0.35f));
                    Path(mesh, new Vector2(0.71f, 0.37f), new Vector2(0.74f, 0.35f));
                    break;
                case Shape.Settings:
                    for (int index = 0; index < 3; index++)
                    {
                        float height = 0.23f + index * 0.27f;
                        float horizontal = index == 1 ? 0.66f : 0.35f;
                        Path(mesh, new Vector2(0.12f, height), new Vector2(horizontal - 0.09f, height));
                        Path(mesh, new Vector2(horizontal + 0.09f, height), new Vector2(0.88f, height));
                        Arc(mesh, new Vector2(horizontal, height), 0.09f, 0f, 360f);
                    }
                    break;
                case Shape.Arrow:
                    Path(mesh, new Vector2(0.2f, 0.23f), new Vector2(0.8f, 0.78f), new Vector2(0.34f, 0.78f));
                    Path(mesh, new Vector2(0.8f, 0.78f), new Vector2(0.8f, 0.32f));
                    break;
            }
        }

        private void Arc(VertexHelper mesh, Vector2 center, float radius, float from, float to)
        {
            int segments = Mathf.CeilToInt((to - from) / 12f);
            for (int index = 0; index < segments; index++)
            {
                float first = Mathf.Lerp(from, to, (float)index / segments) * Mathf.Deg2Rad;
                float second = Mathf.Lerp(from, to, (float)(index + 1) / segments) * Mathf.Deg2Rad;
                Path(mesh, center + new Vector2(Mathf.Cos(first), Mathf.Sin(first)) * radius,
                    center + new Vector2(Mathf.Cos(second), Mathf.Sin(second)) * radius);
            }
        }

        private void Path(VertexHelper mesh, params Vector2[] points)
        {
            Rect bounds = GetPixelAdjustedRect();
            float width = Mathf.Min(bounds.width, bounds.height);
            Vector2 origin = bounds.center - Vector2.one * width * 0.5f;
            for (int index = 1; index < points.Length; index++)
            {
                Vector2 first = origin + points[index - 1] * width;
                Vector2 second = origin + points[index] * width;
                Vector2 direction = (second - first).normalized;
                Vector2 normal = new Vector2(-direction.y, direction.x) * width * 0.032f;
                first -= direction * width * 0.025f;
                second += direction * width * 0.025f;
                UIVertex[] vertices = new UIVertex[4];
                Vector2[] positions = { first - normal, first + normal, second + normal, second - normal };
                for (int vertex = 0; vertex < 4; vertex++)
                {
                    vertices[vertex] = UIVertex.simpleVert;
                    vertices[vertex].position = positions[vertex];
                    vertices[vertex].color = color;
                }
                mesh.AddUIVertexQuad(vertices);
            }
        }
    }
}
