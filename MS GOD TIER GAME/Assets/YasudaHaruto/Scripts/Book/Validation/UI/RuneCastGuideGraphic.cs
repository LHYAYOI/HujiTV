using UnityEngine;
using UnityEngine.UI;

namespace YasudaHaruto.BookValidation
{
    // UI geometry keeps the spotlight transparent and the arrows crisp at any resolution.
    public sealed class RuneCastGuideGraphic : MaskableGraphic
    {
        private Vector2 m_center;
        private float m_radius, m_length, m_shade, m_sideAngle;
        public float ArrowWidth { get; set; } = 0.1f;
        public int SelectedDirection { get; set; } = -1;

        public void SetGuide(Vector2 center, float radius, float length, float shade, float sideAngle)
        {
            m_center = center;
            m_radius = Mathf.Max(1f, radius);
            m_length = length;
            m_shade = shade;
            m_sideAngle = sideAngle;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = rectTransform.rect;
            Color dark = new Color(0f, 0f, 0f, m_shade);
            float r = m_radius;
            Quad(vh, new Vector2(rect.xMin, rect.yMin), new Vector2(m_center.x - r, rect.yMin),
                new Vector2(m_center.x - r, rect.yMax), new Vector2(rect.xMin, rect.yMax), dark);
            Quad(vh, new Vector2(m_center.x + r, rect.yMin), new Vector2(rect.xMax, rect.yMin),
                new Vector2(rect.xMax, rect.yMax), new Vector2(m_center.x + r, rect.yMax), dark);
            Quad(vh, new Vector2(m_center.x - r, rect.yMin), new Vector2(m_center.x + r, rect.yMin),
                new Vector2(m_center.x + r, m_center.y - r), new Vector2(m_center.x - r, m_center.y - r), dark);
            Quad(vh, new Vector2(m_center.x - r, m_center.y + r), new Vector2(m_center.x + r, m_center.y + r),
                new Vector2(m_center.x + r, rect.yMax), new Vector2(m_center.x - r, rect.yMax), dark);
            for (int i = 0; i < 64; i++)
            {
                Vector2 a = Unit(i * Mathf.PI / 32f), b = Unit((i + 1) * Mathf.PI / 32f);
                Vector2 sa = a * (r / Mathf.Max(Mathf.Abs(a.x), Mathf.Abs(a.y)));
                Vector2 sb = b * (r / Mathf.Max(Mathf.Abs(b.x), Mathf.Abs(b.y)));
                Quad(vh, m_center + a * r, m_center + sa, m_center + sb, m_center + b * r, dark);
                Quad(vh, m_center + a * (r * 0.96f), m_center + a * r,
                    m_center + b * r, m_center + b * (r * 0.96f), new Color(1f, 0.85f, 0.35f, 0.95f));
                Quad(vh, m_center + a * (r * 0.87f), m_center + a * (r * 0.96f),
                    m_center + b * (r * 0.96f), m_center + b * (r * 0.87f), new Color(1f, 0.85f, 0.4f, 0.15f));
            }
        }

        private static Vector2 Unit(float angle) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

        private static void Line(VertexHelper vh, Vector2 start, Vector2 end, float width, Color color)
        {
            Vector2 delta = (end - start).normalized;
            Vector2 normal = new Vector2(-delta.y, delta.x) * width * 0.5f;
            Quad(vh, start - normal, start + normal, end + normal, end - normal, color);
        }

        private static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            int index = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero);
            vh.AddVert(b, color, Vector2.zero);
            vh.AddVert(c, color, Vector2.zero);
            vh.AddVert(d, color, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
