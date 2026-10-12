//-----------------------------------------------
// RuneTraceRealtimeEvaluator.cs
// 制作日：2026/10/10
// 制作者：安田晴人
// 概要：ルーンのリアルタイムトレース評価を行うクラス
//-----------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public enum RUNE_REALTIME_RESULT
{
    IN_PROGRESS,
    COMPLETED,
    FAILED
}

public class RuneTraceRealtimeEvaluator
{
    private readonly IReadOnlyList<Vector2> m_referencePoints;
    private readonly float m_pathLength;
    private readonly float m_distanceTolerance;
    private readonly float m_startTolerance;
    private readonly float m_endTolerance;
    private readonly float m_sampleInterval;

    private float m_currentDistance;
    private float m_maxDistance;
    private bool m_hasStarted;
    private bool m_isCompleted;
    private bool m_hasFailed;

    public float Progress => m_pathLength > 0f ? Mathf.Clamp01(m_maxDistance / m_pathLength) : 0f;
    public float CurrentProgress => m_pathLength > 0f ? Mathf.Clamp01(m_currentDistance / m_pathLength) : 0f;
    public bool IsCompleted => m_isCompleted;
    public bool HasFailed => m_hasFailed;

    public RuneTraceRealtimeEvaluator(IReadOnlyList<Vector2> referencePoints, float distanceTolerance, float startTolerance, float endTolerance, float sampleInterval)
    {
        m_referencePoints = referencePoints;
        m_pathLength = RunePathUtility.CalculatePathLength(referencePoints);
        m_distanceTolerance = Mathf.Max(0f, distanceTolerance);
        m_startTolerance = Mathf.Max(0f, startTolerance);
        m_endTolerance = Mathf.Max(0f, endTolerance);
        m_sampleInterval = Mathf.Max(0.0001f, sampleInterval);
    }

    // 開始位置を設定し、評価を開始する
    public bool Begin(Vector2 position)
    {
        Reset();

        if (m_referencePoints == null || m_referencePoints.Count < 2 || m_pathLength <= Mathf.Epsilon)
        {// パスが無効な場合は失敗
            return false;
        }

        if (Vector2.Distance(position, m_referencePoints[0]) > m_startTolerance)
        {// 開始位置が許容範囲外の場合は失敗
            return false;
        }

        m_hasStarted = true;
        return true;
    }

    // ルーンのトレースを評価する
    public RUNE_REALTIME_RESULT EvaluateSegment(Vector2 previous, Vector2 current)
    {
        if (!m_hasStarted || m_hasFailed)
        {// 評価が開始されていない、または失敗している場合は失敗扱い
            return RUNE_REALTIME_RESULT.FAILED;
        }

        if (m_isCompleted)
        {// すでに完了している場合は完了扱い
            return RUNE_REALTIME_RESULT.COMPLETED;
        }

        // 入力距離をサンプリング間隔で分割して評価する
        float inputDistance = Vector2.Distance(previous, current);
        int sampleCount = Mathf.Max(1, Mathf.CeilToInt(inputDistance / m_sampleInterval));

        // 異常に長い移動を計算量制限で成功扱いしない
        if (sampleCount > 4096)
        {
            return Fail();
        }

        // サンプリングしてパス上に投影し、距離を更新する
        for (int i = 1; i <= sampleCount; i++)
        {
            // サンプリング位置を計算
            Vector2 position = Vector2.Lerp(previous, current, (float)i / sampleCount);

            // パス上に投影する際の許容距離を設定
            float backwardAllowance = Mathf.Max(m_distanceTolerance, m_sampleInterval * 4f);
            float forwardAllowance = m_sampleInterval * 2f;

            // パス上に投影し、距離を取得
            bool projected = RunePathUtility.TryProjectOnPath(m_referencePoints, position, m_currentDistance, backwardAllowance, forwardAllowance, out float pathDistance, out float distanceFromPath);

            if (!projected || distanceFromPath > m_distanceTolerance)
            {// パス上に投影できない、または許容距離を超えた場合は失敗
                return Fail();
            }

            // 距離を更新
            m_currentDistance = pathDistance;
            m_maxDistance = Mathf.Max(m_maxDistance, pathDistance);
        }

        // ルーンのトレースが完了したかどうかを判定
        float remaining = m_pathLength - m_maxDistance;
        float progressTolerance = Mathf.Min(m_sampleInterval * 2f, m_endTolerance);

        if (remaining <= progressTolerance && Vector2.Distance(current, m_referencePoints[m_referencePoints.Count - 1]) <= m_endTolerance)
        {// トレースが完了した場合は完了扱い
            m_isCompleted = true;
            return RUNE_REALTIME_RESULT.COMPLETED;
        }

        // まだトレースが完了していない場合は進行中扱い
        return RUNE_REALTIME_RESULT.IN_PROGRESS;
    }

    // 評価をリセットする
    public void Reset()
    {
        m_currentDistance = 0f;
        m_maxDistance = 0f;
        m_hasStarted = false;
        m_isCompleted = false;
        m_hasFailed = false;
    }

    // 評価が失敗した場合の処理
    private RUNE_REALTIME_RESULT Fail()
    {
        m_hasFailed = true;
        return RUNE_REALTIME_RESULT.FAILED;
    }
}
