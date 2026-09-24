//-----------------------------------------------
// RuneTraceSession.cs
// 制作日：2026/09/17
// 制作者：安田晴人
// 概要：1回のルーンなぞり挑戦の入力状態を管理するクラス
//-----------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public class RuneTraceSession
{
    private readonly List<RuneStrokeData> m_strokes = new();

    public IReadOnlyList<RuneStrokeData> Strokes => m_strokes;

    public int StrokeCount => m_strokes.Count;


    public void AddStroke(IEnumerable<Vector2> points)
    {
        if (points == null)
        {
            return;
        }

        RuneStrokeData stroke = new RuneStrokeData(points);

        if (stroke.Points.Count < 2)
        {
            return;
        }

        m_strokes.Add(stroke);
    }


    public void Clear()
    {
        m_strokes.Clear();
    }
}