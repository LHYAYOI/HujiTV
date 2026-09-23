using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class RuneLineGraphic : Graphic
{
    private IReadOnlyList<RuneStrokeData> m_strokes;
    private IReadOnlyList<Vector2> m_currentStroke;

    [SerializeField]
    private float m_lineWidth = 4f;

    public void SetStrokes(
        IReadOnlyList<RuneStrokeData> strokes)
    {
        m_strokes = strokes;
        m_currentStroke = null;

        SetVerticesDirty();
    }

    public void SetCurrentStroke(
        IReadOnlyList<Vector2> points)
    {
        m_strokes = null;
        m_currentStroke = points;

        SetVerticesDirty();
    }

    public void Clear()
    {
        m_strokes = null;
        m_currentStroke = null;

        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = rectTransform.rect;

        if (m_strokes != null)
        {
            foreach (RuneStrokeData stroke in m_strokes)
            {
                DrawStroke(
                    vh,
                    stroke.Points,
                    rect);
            }
        }

        if (m_currentStroke != null)
        {
            DrawStroke(
                vh,
                m_currentStroke,
                rect);
        }
    }

    private void DrawStroke(
        VertexHelper vh,
        IReadOnlyList<Vector2> points,
        Rect rect)
    {
        if (points == null ||
            points.Count < 2)
        {
            return;
        }

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector2 start =
                NormalizedToLocal(points[i], rect);

            Vector2 end =
                NormalizedToLocal(points[i + 1], rect);

            AddLine(
                vh,
                start,
                end,
                m_lineWidth);
        }
    }

    private Vector2 NormalizedToLocal(
    Vector2 point,
    Rect rect)
    {
        return new Vector2(
            Mathf.Lerp(
                rect.xMin,
                rect.xMax,
                point.x),

            Mathf.Lerp(
                rect.yMin,
                rect.yMax,
                point.y));
    }

    private void AddLine(
        VertexHelper vh,
        Vector2 start,
        Vector2 end,
        float width)
    {
        Vector2 direction =
            (end - start).normalized;

        Vector2 normal =
            new Vector2(
                -direction.y,
                direction.x);

        normal *= width * 0.5f;

        int index = vh.currentVertCount;

        UIVertex vertex =
            UIVertex.simpleVert;

        vertex.color = color;

        vertex.position = start - normal;
        vh.AddVert(vertex);

        vertex.position = start + normal;
        vh.AddVert(vertex);

        vertex.position = end + normal;
        vh.AddVert(vertex);

        vertex.position = end - normal;
        vh.AddVert(vertex);

        vh.AddTriangle(
            index,
            index + 1,
            index + 2);

        vh.AddTriangle(
            index,
            index + 2,
            index + 3);
    }
}