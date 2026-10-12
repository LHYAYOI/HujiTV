//-----------------------------------------------
// RunePageView.cs
// 制作日：2026/10/09
// 制作者：安田晴人
// 概要： ルーンページビューの表示
//-----------------------------------------------

using System.Collections.Generic;
using UnityEngine;

public class RunePageView : MonoBehaviour, IRuneTraceView
{
    [SerializeField] private RuneLineGraphic m_referenceGraphic;
    [SerializeField] private RuneLineGraphic m_playerGraphic;

    private readonly List<RuneStrokeData> m_completedStrokes = new();
    private readonly List<RuneStrokeData> m_displayStrokes = new();
    private List<Vector2> m_lastCurrentStroke = new();

    public void BeginTrace(RuneData runeData)
    {
        BringToFront();
        Clear();

        if (runeData == null) return;

        m_referenceGraphic.SetStrokes(runeData.TraceData.Strokes);
    }

    public void UpdateCurrentStroke(IReadOnlyList<Vector2> points)
    {
        m_lastCurrentStroke = new List<Vector2>(points);

        m_displayStrokes.Clear();
        m_displayStrokes.AddRange(m_completedStrokes);

        if (points.Count >= 2)
            m_displayStrokes.Add(new RuneStrokeData(points));

        m_playerGraphic.SetStrokes(m_displayStrokes);
    }

    public void EndStroke()
    {
        if (m_lastCurrentStroke.Count >= 2)
            m_completedStrokes.Add(new RuneStrokeData(m_lastCurrentStroke));

        m_lastCurrentStroke.Clear();
        m_playerGraphic.SetStrokes(m_completedStrokes);
    }

    public void BringToFront()
    {
        transform.SetAsLastSibling();
    }

    public void PlaySuccess(RuneTraceResult result)
    {
    }

    public void PlayFailure(RuneTraceResult result)
    {
    }

    public void Clear()
    {
        m_completedStrokes.Clear();
        m_displayStrokes.Clear();
        m_lastCurrentStroke.Clear();

        if (m_referenceGraphic != null)
        {
            m_referenceGraphic.Clear();
        }

        if (m_playerGraphic != null)
        {
            m_playerGraphic.Clear();
        }
    }
}
