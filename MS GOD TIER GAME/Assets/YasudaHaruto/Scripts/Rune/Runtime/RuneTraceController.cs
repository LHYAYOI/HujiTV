//-----------------------------------------------
// RuneTraceController.cs
// 制作日：2026/09/19
// 制作者：安田晴人
// 概要： ルーンのなぞりコントローラー
//-----------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;


public enum RUNE_TRACE_STATE
{
    INACTIVE,
    WAITING_STROKE,
    DRAWING,
    WAITING_RELEASE,
    FAILED_WAITING_RELEASE,
    CHARGING,
    WAITING_RELEASE_AFTER_CAST,
    COOLDOWN
}


public class RuneTraceController
{
    private readonly IRuneTraceView m_view;
    private readonly List<Vector2> m_currentStroke = new();

    private RuneData m_runeData;
    private RuneTraceSession m_session;
    private RuneTraceRealtimeEvaluator m_evaluator;
    private RUNE_TRACE_STATE m_state = RUNE_TRACE_STATE.INACTIVE;
    private int m_strokeIndex;
    private MAGIC_AIM_TYPE m_aimType = MAGIC_AIM_TYPE.THREE_DIRECTIONS;
    private float m_cooldownSeconds = 1.0f;
    private float m_cooldownRemaining;
    private bool m_pointerReleased;

    private const float MIN_POINT_DISTANCE = 0.005f;
    private const float START_TOLERANCE = 0.04f;
    private const float END_TOLERANCE = 0.025f;
    private const float SAMPLE_INTERVAL = 0.005f;

    public bool IsTracing => m_state != RUNE_TRACE_STATE.INACTIVE;
    public RUNE_TRACE_STATE State => m_state;
    public int CurrentStrokeIndex => m_strokeIndex;
    public float Progress => m_evaluator?.Progress ?? 0f;

    public bool CanBeginStroke => m_state == RUNE_TRACE_STATE.WAITING_STROKE;
    public float CooldownRemaining => m_cooldownRemaining;
    public float CooldownProgress => m_cooldownSeconds > 0f
        ? 1f - Mathf.Clamp01(m_cooldownRemaining / m_cooldownSeconds) : 1f;

    public event Action<RuneTraceResult> TraceSucceeded;
    public event Action<RuneTraceResult> TraceFailed;
    public event Action<MAGIC_CAST_DIRECTION> CastConfirmed;

    public void SetCooldown(float seconds)
    {
        m_cooldownSeconds = Mathf.Max(0f, seconds);
    }

    public RuneTraceController(IRuneTraceView view)
    {
        m_view = view;
    }

    public void BeginTrace(RuneData runeData, MAGIC_AIM_TYPE aimType = MAGIC_AIM_TYPE.THREE_DIRECTIONS)
    {
        Cancel();
        if (runeData == null || runeData.TraceData.Strokes.Count == 0) return;

        m_runeData = runeData;
        m_aimType = aimType;
        m_session = new RuneTraceSession();
        ResetAttempt();
    }

    public void BeginStroke(Vector2 normalizedPosition)
    {
        if (m_state != RUNE_TRACE_STATE.WAITING_STROKE) return;

        IReadOnlyList<Vector2> points = m_runeData.TraceData.Strokes[m_strokeIndex].Points;
        RuneTraceRule rule = m_runeData.Rule;
        m_evaluator = new RuneTraceRealtimeEvaluator(points, rule.DistanceTolerance, rule.StartTolerance, rule.EndTolerance, SAMPLE_INTERVAL);

        m_currentStroke.Clear();

        if (!m_evaluator.Begin(normalizedPosition))
        {
            m_state = RUNE_TRACE_STATE.FAILED_WAITING_RELEASE;
            Debug.Log($"Rune : Invalid Start / Stroke={m_strokeIndex + 1}");
            return;
        }

        m_state = RUNE_TRACE_STATE.DRAWING;
        m_currentStroke.Add(normalizedPosition);
        m_view.UpdateCurrentStroke(m_currentStroke);
    }

    public void AddPoint(Vector2 normalizedPosition)
    {
        if (m_state != RUNE_TRACE_STATE.DRAWING) return;

        Vector2 previous = m_currentStroke[m_currentStroke.Count - 1];
        if (Vector2.Distance(previous, normalizedPosition) < MIN_POINT_DISTANCE) return;

        RUNE_REALTIME_RESULT result = m_evaluator.EvaluateSegment(previous, normalizedPosition);

        if (result == RUNE_REALTIME_RESULT.FAILED)
        {
            m_state = RUNE_TRACE_STATE.FAILED_WAITING_RELEASE;
            Debug.Log($"Rune : Failed / Stroke={m_strokeIndex + 1}, Progress={Progress:P1}");
            return;
        }

        m_currentStroke.Add(normalizedPosition);
        m_view.UpdateCurrentStroke(m_currentStroke);

        if (result != RUNE_REALTIME_RESULT.COMPLETED) return;

        CompleteCurrentStroke();
    }

    public void EndStroke()
    {
        switch (m_state)
        {
            case RUNE_TRACE_STATE.WAITING_RELEASE:
                m_strokeIndex++;
                m_evaluator = null;
                m_currentStroke.Clear();
                m_state = RUNE_TRACE_STATE.WAITING_STROKE;
                Debug.Log($"Rune : Next Stroke={m_strokeIndex + 1}");
                break;

            case RUNE_TRACE_STATE.DRAWING:
            case RUNE_TRACE_STATE.FAILED_WAITING_RELEASE:
                Debug.Log("Rune : Attempt Failed");
                m_view.PlayFailure(new RuneTraceResult(0f, 0f, m_session.StrokeCount, false));
                TraceFailed?.Invoke(new RuneTraceResult(0f, 0f, m_session.StrokeCount, false));
                ResetAttempt();
                break;

            case RUNE_TRACE_STATE.CHARGING:
                Debug.Log("Rune : Invalid Cast");
                m_view.PlayFailure(new RuneTraceResult(0f, 0f, m_session.StrokeCount, false));
                ResetAttempt();
                break;

            case RUNE_TRACE_STATE.WAITING_RELEASE_AFTER_CAST:
                ResetAttempt();
                break;

            case RUNE_TRACE_STATE.COOLDOWN:
                m_pointerReleased = true;
                break;
        }
    }

    // 現在のストロークを完了する
    private void CompleteCurrentStroke()
    {
        // ストロークをセッションに追加する
        m_session.AddStroke(m_currentStroke);

        // ビューにストローク完了を通知する
        m_view.EndStroke();

        if (m_strokeIndex == m_runeData.TraceData.Strokes.Count - 1)
        {// すべてのストロークが完了した場合、詠唱を確定する
            if (m_aimType == MAGIC_AIM_TYPE.NONE)
            {// 方向指定がない場合は詠唱を確定する
                StartCooldown(false);
                CastConfirmed?.Invoke(MAGIC_CAST_DIRECTION.NONE);
            }
            else
            {// 方向指定がある場合は詠唱を開始する
                m_state = RUNE_TRACE_STATE.CHARGING;
                Debug.Log("Rune : CHARGING");
            }
        }
        else
        {// 次のストロークを待機する
            m_state = RUNE_TRACE_STATE.WAITING_RELEASE;
        }
    }

    // ルーンの詠唱を確定する
    public void ConfirmCast(MAGIC_CAST_DIRECTION direction)
    {
        if (m_state != RUNE_TRACE_STATE.CHARGING)
        {
            return;
        }

        Debug.Log($"Rune : Cast Confirmed / Direction={direction}");
        StartCooldown(true);
        CastConfirmed?.Invoke(direction);
    }


    private void StartCooldown(bool pointerReleased)
    {
        m_state = RUNE_TRACE_STATE.COOLDOWN;
        m_cooldownRemaining = m_cooldownSeconds;
        m_pointerReleased = pointerReleased;
        Debug.Log($"Rune : Cooldown Started / {m_cooldownSeconds:F1}s");
    }

    public void Tick(float deltaTime)
    {
        if (m_state != RUNE_TRACE_STATE.COOLDOWN || !m_pointerReleased)
        {// ポインタが離されていない場合はクールダウンを進めない
            return;
        }

        // クールダウンを進める
        m_cooldownRemaining = Mathf.Max(0f, m_cooldownRemaining - deltaTime);

        if (m_cooldownRemaining > 0f)
        {// クールダウン中
            return;
        }

        // クールダウンが終了した場合、ルーンのなぞりをリセットする
        Debug.Log("Rune : Cooldown Finished");
        ResetAttempt();
    }

    public void Cancel()
    {
        m_state = RUNE_TRACE_STATE.INACTIVE;
        m_strokeIndex = 0;
        m_evaluator = null;
        m_runeData = null;
        m_currentStroke.Clear();
        m_session?.Clear();
        m_session = null;
        m_view.Clear();
        m_cooldownRemaining = 0f;
        m_pointerReleased = true;
    }

    private void ResetAttempt()
    {
        m_session ??= new RuneTraceSession();
        m_session.Clear();
        m_currentStroke.Clear();
        m_strokeIndex = 0;
        m_evaluator = null;
        m_state = RUNE_TRACE_STATE.WAITING_STROKE;
        m_view.BeginTrace(m_runeData);
        m_cooldownRemaining = 0f;
        m_pointerReleased = true;
    }
}
