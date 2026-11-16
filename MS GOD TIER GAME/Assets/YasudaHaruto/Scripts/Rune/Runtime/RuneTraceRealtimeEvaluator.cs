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

    private readonly float[] m_pathDistances;
    private int m_coverageSegment;
    private float m_coverageFraction;
    private float m_coveredDistance;
    private float m_currentDistance;
    private float m_maxDistance;
    private bool m_hasStarted;
    private bool m_isCompleted;
    private bool m_hasFailed;

    public float NormalizedDeviation { get; private set; }

    public float Progress => m_pathLength > 0f ? Mathf.Clamp01(m_maxDistance / m_pathLength) : 0f;
    public float CurrentProgress => m_pathLength > 0f ? Mathf.Clamp01(m_currentDistance / m_pathLength) : 0f;
    public bool IsCompleted => m_isCompleted;
    public bool HasFailed => m_hasFailed;

    public RuneTraceRealtimeEvaluator(IReadOnlyList<Vector2> referencePoints, float distanceTolerance, float startTolerance, float endTolerance, float sampleInterval)
    {
        m_referencePoints = referencePoints;
        m_pathDistances = new float[referencePoints != null ? referencePoints.Count : 0];
        for (int i = 1; i < m_pathDistances.Length; i++)
            m_pathDistances[i] = m_pathDistances[i - 1] + Vector2.Distance(referencePoints[i - 1], referencePoints[i]);
        m_pathLength = m_pathDistances.Length > 0 ? m_pathDistances[m_pathDistances.Length - 1] : 0f;
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

        // Starting within StartTolerance also covers the contiguous prefix near the pointer.
        AdvanceCoverage(position, position, m_startTolerance);
        RunePathUtility.TryProjectOnPath(m_referencePoints, position, 0f, 0f, m_coveredDistance,
            out m_currentDistance, out float distance);
        m_maxDistance = m_currentDistance;
        NormalizedDeviation = Mathf.Clamp01(distance / Mathf.Max(0.0001f, m_distanceTolerance));
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
        NormalizedDeviation = 0f;
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

            Vector2 samplePrevious = Vector2.Lerp(previous, current, (float)(i - 1) / sampleCount);
            AdvanceCoverage(samplePrevious, position, m_distanceTolerance);

            // Distance is measured against the continuously visited part of the route.
            // Coverage cannot jump over an unvisited loop at a crossing or nearby branch.
            float backwardAllowance = Mathf.Max(m_distanceTolerance, m_sampleInterval * 4f);
            float forwardAllowance = Mathf.Max(0f, m_coveredDistance - m_currentDistance);
            bool projected = RunePathUtility.TryProjectOnPath(m_referencePoints, position, m_currentDistance,
                backwardAllowance, forwardAllowance, out float pathDistance, out float distanceFromPath);

            NormalizedDeviation = Mathf.Max(NormalizedDeviation, projected
                ? Mathf.Clamp01(distanceFromPath / Mathf.Max(0.0001f, m_distanceTolerance)) : 1f);

            if (!projected || distanceFromPath > m_distanceTolerance)
            {// パス上に投影できない、または許容距離を超えた場合は失敗
                return Fail();
            }

            // 距離を更新
            m_currentDistance = pathDistance;
            m_maxDistance = Mathf.Max(m_maxDistance, pathDistance);
        }

        // ルーンのトレースが完了したかどうかを判定
        float remaining = m_pathLength - m_coveredDistance;
        float progressTolerance = m_endTolerance;

        if (remaining <= progressTolerance && Vector2.Distance(current, m_referencePoints[m_referencePoints.Count - 1]) <= m_endTolerance)
        {// トレースが完了した場合は完了扱い
            m_isCompleted = true;
            m_maxDistance = m_pathLength;
            return RUNE_REALTIME_RESULT.COMPLETED;
        }

        // まだトレースが完了していない場合は進行中扱い
        return RUNE_REALTIME_RESULT.IN_PROGRESS;
    }

    // 評価をリセットする
    public void Reset()
    {
        NormalizedDeviation = 0f;
        m_coverageSegment = 0;
        m_coverageFraction = 0f;
        m_coveredDistance = 0f;
        m_currentDistance = 0f;
        m_maxDistance = 0f;
        m_hasStarted = false;
        m_isCompleted = false;
        m_hasFailed = false;
    }

    // Advance only a contiguous route prefix inside the swept input capsule.
    // A capsule is convex, so a straight reference segment with both ends inside is covered.
    private void AdvanceCoverage(Vector2 previous, Vector2 current, float tolerance)
    {
        float radiusSquared = tolerance * tolerance + 0.0000000001f;
        while (m_coverageSegment + 1 < m_referencePoints.Count)
        {
            Vector2 start = m_referencePoints[m_coverageSegment];
            Vector2 end = m_referencePoints[m_coverageSegment + 1];
            Vector2 frontier = Vector2.Lerp(start, end, m_coverageFraction);
            if (SquaredDistanceToSegment(frontier, previous, current) > radiusSquared) return;
            if (SquaredDistanceToSegment(end, previous, current) <= radiusSquared)
            {
                m_coverageSegment++;
                m_coverageFraction = 0f;
                m_coveredDistance = m_pathDistances[m_coverageSegment];
                continue;
            }

            // Locate the capsule exit without allocating or resampling the reference path.
            float low = m_coverageFraction, high = 1f;
            for (int step = 0; step < 16; step++)
            {
                float middle = (low + high) * 0.5f;
                Vector2 point = Vector2.Lerp(start, end, middle);
                if (SquaredDistanceToSegment(point, previous, current) <= radiusSquared) low = middle;
                else high = middle;
            }
            m_coverageFraction = low;
            m_coveredDistance = Mathf.Lerp(m_pathDistances[m_coverageSegment],
                m_pathDistances[m_coverageSegment + 1], low);
            return;
        }
    }

    private static float SquaredDistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 direction = end - start;
        float lengthSquared = direction.sqrMagnitude;
        float t = lengthSquared > 0f ? Mathf.Clamp01(Vector2.Dot(point - start, direction) / lengthSquared) : 0f;
        return (point - (start + t * direction)).sqrMagnitude;
    }

    // 評価が失敗した場合の処理
    private RUNE_REALTIME_RESULT Fail()
    {
        NormalizedDeviation = 1f;
        m_hasFailed = true;
        return RUNE_REALTIME_RESULT.FAILED;
    }
}
