using UnityEngine;
using UnityEngine.UI;

// A ring whose leading edge disappears clockwise from twelve o'clock.
public sealed class RuneCooldownGraphic : MaskableGraphic
{
    private float m_remaining = 1f;
    private const int SEGMENTS = 120;

    public float RemainingFraction => m_remaining;

    public void SetRemaining(float remaining)
    {
        m_remaining = Mathf.Clamp01(remaining);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (m_remaining <= 0f) return;
        Rect area = rectTransform.rect;
        float radius = Mathf.Min(area.width, area.height) * 0.5f;
        if (radius <= 0f) return;
        float inner = radius * 0.76f;
        float start = (1f - m_remaining) * 360f;
        int segments = Mathf.Max(1, Mathf.CeilToInt(SEGMENTS * m_remaining));
        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Lerp(start, 360f, (float)i / segments) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
            mesh.AddVert(area.center + direction * inner, color, Vector2.zero);
            mesh.AddVert(area.center + direction * radius, color, Vector2.zero);
            if (i == 0) continue;
            int index = i * 2;
            mesh.AddTriangle(index - 2, index - 1, index);
            mesh.AddTriangle(index, index - 1, index + 1);
        }
    }
}
