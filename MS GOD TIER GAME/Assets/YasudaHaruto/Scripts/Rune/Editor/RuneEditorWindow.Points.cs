using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public partial class RuneEditorWindow
{
    private bool m_placingPoints;
    private bool m_snapPoints;
    private int m_gridDivisions = 20;
    private int m_selectedPointIndex = -1;
    private int m_pointUndoGroup = -1;

    private void DrawPointToolbar()
    {
        m_snapPoints = EditorGUILayout.Toggle("Snap to Grid", m_snapPoints);
        m_gridDivisions = EditorGUILayout.IntSlider("Grid Divisions", m_gridDivisions, 2, 100);
        EditorGUILayout.HelpBox(m_placingPoints
            ? "Click to add corners. Enter / Finish Stroke saves one stroke. Backspace removes the last point; Escape cancels."
            : "Click empty space to start a stroke. Drag an existing point to move it. Shift+click a line to insert a point. Delete removes the selected point.", MessageType.Info);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("New Stroke"))
        {
            CancelCanvasInput();
            m_placingPoints = true;
            m_selectedStrokeIndex = -1;
        }
        using (new EditorGUI.DisabledScope(m_currentStroke.Count < 2))
            if (GUILayout.Button("Finish Stroke")) FinishPointStroke();
        using (new EditorGUI.DisabledScope(!m_placingPoints))
            if (GUILayout.Button("Cancel Stroke")) { CancelCanvasInput(); m_placingPoints = false; }
        EditorGUILayout.EndHorizontal();
    }

    private Vector2 PointPosition(Vector2 canvas)
    {
        Vector2 point = CanvasToNormalized(canvas);
        if (m_snapPoints)
        {
            point.x = Mathf.Round(point.x * m_gridDivisions) / m_gridDivisions;
            point.y = Mathf.Round(point.y * m_gridDivisions) / m_gridDivisions;
        }
        return new Vector2(Mathf.Clamp01(point.x), Mathf.Clamp01(point.y));
    }

    private void AddPlacedPoint(Vector2 canvas)
    {
        Vector2 point = PointPosition(canvas);
        if (m_currentStroke.Count > 0 && (m_currentStroke[m_currentStroke.Count - 1] - point).sqrMagnitude < 0.00000001f) return;
        m_placingPoints = m_isDrawing = true;
        m_currentStroke.Add(point);
        Repaint();
    }

    private void FinishPointStroke()
    {
        if (m_currentStroke.Count < 2) return;
        FinishStroke();
        m_placingPoints = false;
        m_selectedPointIndex = -1;
        ResetTest();
    }

    private bool FindPoint(Vector2 canvas, out int strokeIndex, out int pointIndex)
    {
        strokeIndex = pointIndex = -1;
        float nearest = 10f;
        var strokes = m_runeData.AuthoringData.Strokes;
        for (int s = 0; s < strokes.Count; s++)
            for (int p = 0; p < strokes[s].Points.Count; p++)
            {
                float distance = Vector2.Distance(canvas, NormalizedToCanvas(strokes[s].Points[p]));
                if (distance > nearest) continue;
                nearest = distance;
                strokeIndex = s;
                pointIndex = p;
            }
        return pointIndex >= 0;
    }

    private bool ReplacePointStroke(List<Vector2> points)
    {
        var strokes = new List<RuneStrokeData>(m_runeData.AuthoringData.Strokes);
        if (m_selectedStrokeIndex < 0 || m_selectedStrokeIndex >= strokes.Count) return false;
        if (points.Count < 2) strokes.RemoveAt(m_selectedStrokeIndex);
        else strokes[m_selectedStrokeIndex] = new RuneStrokeData(points, true);
        m_runeData.AuthoringData.SetStrokes(strokes);
        return true;
    }

    private void MoveSelectedPoint(Vector2 canvas)
    {
        var strokes = m_runeData.AuthoringData.Strokes;
        if (m_selectedStrokeIndex < 0 || m_selectedStrokeIndex >= strokes.Count) return;
        var points = new List<Vector2>(strokes[m_selectedStrokeIndex].Points);
        if (m_selectedPointIndex < 0 || m_selectedPointIndex >= points.Count) return;
        Vector2 position = PointPosition(canvas);
        if (points[m_selectedPointIndex] == position) return;
        points[m_selectedPointIndex] = position;
        EditStroke(() => ReplacePointStroke(points));
        ResetTest();
    }

    private void DeleteSelectedPoint()
    {
        var strokes = m_runeData.AuthoringData.Strokes;
        if (m_selectedStrokeIndex < 0 || m_selectedStrokeIndex >= strokes.Count) return;
        var points = new List<Vector2>(strokes[m_selectedStrokeIndex].Points);
        if (m_selectedPointIndex < 0 || m_selectedPointIndex >= points.Count) return;
        points.RemoveAt(m_selectedPointIndex);
        EditStroke(() => ReplacePointStroke(points));
        if (points.Count < 2) m_selectedStrokeIndex = -1;
        m_selectedPointIndex = -1;
        ResetTest();
    }

    private void InsertPoint(Vector2 canvas)
    {
        int strokeIndex = FindStroke(canvas);
        if (strokeIndex < 0) return;
        var points = new List<Vector2>(m_runeData.AuthoringData.Strokes[strokeIndex].Points);
        int index = -1;
        float nearest = float.PositiveInfinity;
        Vector2 inserted = Vector2.zero;
        for (int i = 1; i < points.Count; i++)
        {
            Vector2 a = NormalizedToCanvas(points[i - 1]), b = NormalizedToCanvas(points[i]);
            Vector2 segment = b - a;
            if (segment.sqrMagnitude < 0.0001f) continue;
            float t = Mathf.Clamp01(Vector2.Dot(canvas - a, segment) / segment.sqrMagnitude);
            Vector2 projected = a + segment * t;
            float distance = Vector2.Distance(canvas, projected);
            if (distance >= nearest || t <= 0.0001f || t >= 0.9999f) continue;
            nearest = distance;
            index = i;
            // Insertion preserves the existing line. Grid snapping applies when moving it.
            inserted = Vector2.Lerp(points[i - 1], points[i], t);
        }
        if (index < 0) return;
        points.Insert(index, inserted);
        m_selectedStrokeIndex = strokeIndex;
        m_selectedPointIndex = index;
        EditStroke(() => ReplacePointStroke(points));
        ResetTest();
    }

    private void EndPointDrag()
    {
        if (m_pointUndoGroup < 0) return;
        Undo.FlushUndoRecordObjects();
        Undo.CollapseUndoOperations(m_pointUndoGroup);
        m_pointUndoGroup = -1;
    }

    private void HandlePointInput(Event e)
    {
        if (e.type == EventType.KeyDown && GUIUtility.keyboardControl == m_canvasControlId)
        {
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { FinishPointStroke(); e.Use(); }
            else if (e.keyCode == KeyCode.Escape) { CancelCanvasInput(); m_placingPoints = false; e.Use(); }
            else if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
            {
                if (m_placingPoints && m_currentStroke.Count > 0) m_currentStroke.RemoveAt(m_currentStroke.Count - 1);
                else if (!m_placingPoints) DeleteSelectedPoint();
                Repaint(); e.Use();
            }
            return;
        }
        if (e.type == EventType.MouseDown && e.button == 0 && m_drawingRect.Contains(e.mousePosition))
        {
            GUIUtility.keyboardControl = m_canvasControlId;
            if (m_placingPoints) AddPlacedPoint(e.mousePosition);
            else if (FindPoint(e.mousePosition, out int stroke, out int point))
            {
                m_selectedStrokeIndex = stroke;
                m_selectedPointIndex = point;
                GUIUtility.hotControl = m_canvasControlId;
                Undo.IncrementCurrentGroup();
                m_pointUndoGroup = Undo.GetCurrentGroup();
            }
            else if (e.shift) InsertPoint(e.mousePosition);
            else
            {
                m_selectedPointIndex = -1;
                m_selectedStrokeIndex = FindStroke(e.mousePosition);
                if (m_selectedStrokeIndex < 0) AddPlacedPoint(e.mousePosition);
            }
            m_mergeTargetIndex = -1;
            Repaint(); e.Use();
        }
        else if (GUIUtility.hotControl == m_canvasControlId && m_pointUndoGroup >= 0)
        {
            if (e.type == EventType.MouseDrag && e.button == 0) { MoveSelectedPoint(e.mousePosition); e.Use(); }
            else if (e.type == EventType.MouseUp && e.button == 0)
            {
                MoveSelectedPoint(e.mousePosition);
                EndPointDrag(); GUIUtility.hotControl = 0; e.Use();
            }
        }
    }

    private void DrawPointGrid()
    {
        if (m_canvasTool != CANVAS_TOOL.POINTS || !m_snapPoints || Event.current.type != EventType.Repaint) return;
        Handles.BeginGUI();
        Color previous = Handles.color;
        Handles.color = new Color(1f, 1f, 1f, 0.12f);
        for (int i = 1; i < m_gridDivisions; i++)
        {
            float t = (float)i / m_gridDivisions;
            Handles.DrawLine(NormalizedToCanvas(new Vector2(t, 0)), NormalizedToCanvas(new Vector2(t, 1)));
            Handles.DrawLine(NormalizedToCanvas(new Vector2(0, t)), NormalizedToCanvas(new Vector2(1, t)));
        }
        Handles.color = previous;
        Handles.EndGUI();
    }

    private void DrawPointHandles()
    {
        if (m_canvasTool != CANVAS_TOOL.POINTS || Event.current.type != EventType.Repaint) return;
        Handles.BeginGUI();
        Color previous = Handles.color;
        var strokes = m_runeData.AuthoringData.Strokes;
        for (int s = 0; s < strokes.Count; s++)
            for (int p = 0; p < strokes[s].Points.Count; p++)
            {
                Vector2 position = NormalizedToCanvas(strokes[s].Points[p]);
                Handles.color = s == m_selectedStrokeIndex && p == m_selectedPointIndex ? Color.yellow : Color.cyan;
                Handles.DrawSolidDisc(position, Vector3.forward, 5f);
            }
        Handles.color = Color.yellow;
        for (int p = 0; p < m_currentStroke.Count; p++)
        {
            Vector2 position = NormalizedToCanvas(m_currentStroke[p]);
            Handles.DrawSolidDisc(position, Vector3.forward, 5f);
            Handles.Label(position + new Vector2(7, -18), (p + 1).ToString());
        }
        Handles.color = previous;
        Handles.EndGUI();
    }
}
