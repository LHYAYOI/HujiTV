using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class RuneLineGraphic : Graphic
{
    private IReadOnlyList<RuneStrokeData> m_strokes;
    private IReadOnlyList<IReadOnlyList<Color>> m_strokeColors;
    private IReadOnlyList<Vector2> m_currentStroke;
    private readonly List<Vector2> m_positions = new List<Vector2>();
    private readonly List<Color> m_colors = new List<Color>();

    [SerializeField] private float m_lineWidth = 4f;
    [SerializeField, Range(0.25f, 3f)] private float m_edgeSoftness = 1f;

    public float LineWidth
    {
        get => m_lineWidth;
        set { m_lineWidth = Mathf.Max(0.1f, value); SetVerticesDirty(); }
    }

    public void SetStrokes(IReadOnlyList<RuneStrokeData> strokes,
        IReadOnlyList<IReadOnlyList<Color>> strokeColors = null)
    {
        m_strokes = strokes;
        m_strokeColors = strokeColors;
        m_currentStroke = null;
        SetVerticesDirty();
    }

    public void SetCurrentStroke(IReadOnlyList<Vector2> points)
    {
        m_strokes = null;
        m_currentStroke = points;
        m_strokeColors = null;
        SetVerticesDirty();
    }

    public void Clear()
    {
        m_strokeColors = null;
        m_strokes = null;
        m_currentStroke = null;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = rectTransform.rect;
        if (m_strokes != null)
            for (int i = 0; i < m_strokes.Count; i++)
                DrawStroke(vh, m_strokes[i].Points, rect,
                    m_strokeColors != null && i < m_strokeColors.Count ? m_strokeColors[i] : null);
        if (m_currentStroke != null)
            DrawStroke(vh, m_currentStroke, rect);
    }

    private void DrawStroke(VertexHelper vh, IReadOnlyList<Vector2> points, Rect rect,
        IReadOnlyList<Color> pointColors = null)
    {
        if (points == null || points.Count < 2) return;
        m_positions.Clear();
        m_colors.Clear();
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 p = new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, points[i].x),
                Mathf.Lerp(rect.yMin, rect.yMax, points[i].y));
            if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsInfinity(p.x) || float.IsInfinity(p.y)) continue;
            if (m_positions.Count > 0 && (p - m_positions[m_positions.Count - 1]).sqrMagnitude < 0.000001f) continue;
            m_positions.Add(p);
            m_colors.Add(pointColors != null && i < pointColors.Count ? pointColors[i] : color);
        }
        if (m_positions.Count < 2) return;
        float half = Mathf.Max(0.1f, m_lineWidth) * 0.5f;
        // One output pixel of transparent fringe works on both screen and book capture canvases.
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 origin = RectTransformUtility.WorldToScreenPoint(camera, transform.position);
        float pixels = Mathf.Min(
            Vector2.Distance(origin, RectTransformUtility.WorldToScreenPoint(camera, transform.TransformPoint(Vector3.right))),
            Vector2.Distance(origin, RectTransformUtility.WorldToScreenPoint(camera, transform.TransformPoint(Vector3.up))));
        float edge = Mathf.Max(0.25f, m_edgeSoftness) / Mathf.Max(0.01f, pixels);
        int first = 0;
        for (int i = 1; i < m_positions.Count - 1; i++)
        {
            Vector2 before = (m_positions[i] - m_positions[i - 1]).normalized;
            Vector2 after = (m_positions[i + 1] - m_positions[i]).normalized;
            // Split a reversal rather than allowing an unbounded join spike.
            if (Vector2.Dot(before, after) < -0.95f)
            {
                DrawRun(vh, first, i, half, edge);
                first = i;
            }
        }
        DrawRun(vh, first, m_positions.Count - 1, half, edge);
    }

    private void DrawRun(VertexHelper vh, int first, int last, float half, float edge)
    {
        if (last <= first) return;
        // Stay below uGUI's 16-bit mesh limit, even with unexpectedly long input.
        if (vh.currentVertCount + (last - first + 1) * 4 + 160 >= 64000) return;
        int start = vh.currentVertCount;
        for (int i = first; i <= last; i++)
        {
            Vector2 previous = (m_positions[i] - m_positions[i == first ? i : i - 1]).normalized;
            Vector2 next = (m_positions[i == last ? i : i + 1] - m_positions[i]).normalized;
            if (i == first) previous = next;
            if (i == last) next = previous;
            Vector2 nextNormal = new Vector2(-next.y, next.x);
            Vector2 normal = (new Vector2(-previous.y, previous.x) + nextNormal).normalized;
            normal /= Mathf.Max(0.5f, Vector2.Dot(normal, nextNormal));
            Vector2 p = m_positions[i];
            Color c = m_colors[i];
            Color transparent = c;
            transparent.a = 0f;
            AddVertex(vh, p + normal * (half + edge), transparent);
            AddVertex(vh, p + normal * half, c);
            AddVertex(vh, p - normal * half, c);
            AddVertex(vh, p - normal * (half + edge), transparent);
            if (i == first) continue;
            int index = start + (i - first - 1) * 4;
            for (int band = 0; band < 3; band++)
            {
                vh.AddTriangle(index + band, index + band + 4, index + band + 5);
                vh.AddTriangle(index + band, index + band + 5, index + band + 1);
            }
        }
        DrawCap(vh, m_positions[first], (m_positions[first] - m_positions[first + 1]).normalized,
            half, edge, m_colors[first]);
        DrawCap(vh, m_positions[last], (m_positions[last] - m_positions[last - 1]).normalized,
            half, edge, m_colors[last]);
    }

    private static void DrawCap(VertexHelper vh, Vector2 center, Vector2 outward, float half, float edge, Color c)
    {
        const int segments = 16;
        int origin = vh.currentVertCount;
        AddVertex(vh, center, c);
        Color transparent = c;
        transparent.a = 0f;
        Vector2 normal = new Vector2(-outward.y, outward.x);
        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.PI * i / segments;
            Vector2 direction = normal * Mathf.Cos(angle) + outward * Mathf.Sin(angle);
            AddVertex(vh, center + direction * half, c);
            AddVertex(vh, center + direction * (half + edge), transparent);
            if (i == 0) continue;
            int index = origin + 1 + (i - 1) * 2;
            vh.AddTriangle(origin, index, index + 2);
            vh.AddTriangle(index, index + 1, index + 3);
            vh.AddTriangle(index, index + 3, index + 2);
        }
    }

    private static void AddVertex(VertexHelper vh, Vector2 position, Color color)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = color;
        vh.AddVert(vertex);
    }
}
