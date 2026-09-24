//-----------------------------------------------
// RuneTraceController.cs
// 制作日：2026/09/19
// 制作者：安田晴人
// 概要： ルーンのなぞりコントローラー
//-----------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;

public class RuneTraceController
{
    private readonly IRuneTraceView m_view;

    private RuneData m_runeData;
    private RuneTraceSession m_session;

    private readonly List<Vector2>　m_currentStroke = new();

    private bool m_isTracing;

    private bool m_isDrawingStroke;

    public bool IsTracing => m_isTracing;

    public event Action<RuneTraceResult>　TraceSucceeded;

    public event Action<RuneTraceResult>　TraceFailed;

    private const float MIN_POINT_DISTANCE = 0.005f;

    public RuneTraceController(IRuneTraceView view)
    {
        m_view = view;
    }

    public void BeginTrace(RuneData runeData)
    {
        if (runeData == null)
        {
            return;
        }

        m_runeData = runeData;
        m_session = new RuneTraceSession();

        m_currentStroke.Clear();

        m_isTracing = true;

        m_view.BeginTrace(m_runeData);
    }

    public void BeginStroke(Vector2 normalizedPosition)
    {
        if (!m_isTracing || m_isDrawingStroke)
        {
            return;
        }

        m_currentStroke.Clear();

        m_isDrawingStroke = true;

        AddPoint(normalizedPosition);
    }

    public void AddPoint(Vector2 normalizedPosition)
    {
        if (!m_isTracing || !m_isDrawingStroke)
        {
            return;
        }

        if (m_currentStroke.Count > 0)
        {
            Vector2 lastPoint = m_currentStroke[m_currentStroke.Count - 1];

            if (Vector2.Distance(lastPoint, normalizedPosition) < MIN_POINT_DISTANCE)
            {
                return;
            }
        }

        m_currentStroke.Add(normalizedPosition);

        m_view.UpdateCurrentStroke(m_currentStroke);
    }

    public void EndStroke()
    {
        if (!m_isTracing || !m_isDrawingStroke)
        {
            return;
        }

        m_isDrawingStroke = false;

        if (m_currentStroke.Count < 2)
        {
            m_currentStroke.Clear();
            return;
        }

        m_session.AddStroke(m_currentStroke);

        m_currentStroke.Clear();

        m_view.EndStroke();

        if (m_session.StrokeCount < m_runeData.Rule.RequiredStrokeCount)
        {
            return;
        }

        Evaluate();
    }

    public void Cancel()
    {
        if (!m_isTracing)
        {
            return;
        }

        m_isTracing = false;

        m_isDrawingStroke = false;

        m_currentStroke.Clear();

        m_session?.Clear();

        m_view.Clear();
    }

    private void Evaluate()
    {
        RuneTraceResult result = RuneTraceEvaluator.Evaluate(m_runeData, m_session);

        if (result.IsSuccess)
        {
            m_isTracing = false;

            m_view.PlaySuccess(result);

            TraceSucceeded?.Invoke(result);

            return;
        }

        m_view.PlayFailure(result);

        TraceFailed?.Invoke(result);

        // 失敗した入力は破棄して、
        // すぐ次の挑戦を開始できるようにする
        m_session.Clear();
    }
}