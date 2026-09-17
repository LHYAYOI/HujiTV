//-----------------------------------------------
// RuneTraceEvaluator.cs
// 制作日：2026/09/17
// 制作者：安田晴人
// 概要：プレイヤーのなぞり入力とルーンを比較して評価するクラス
//-----------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public static class RuneTraceEvaluator
{
    public static RuneTraceResult Evaluate(RuneData runeData, RuneTraceSession session)
    {
        if (runeData == null || session == null)
        {
            return new RuneTraceResult(0f, 0f, 0, false);
        }

        RuneTraceRule rule = runeData.Rule;

        float coverage = CalculateCoverage(runeData.TraceData.Strokes, session.Strokes, rule.DistanceTolerance);

        float accuracy = CalculateAccuracy(runeData.TraceData.Strokes, session.Strokes, rule.DistanceTolerance);

        bool strokeCountValid = session.StrokeCount <= rule.MaxStrokeCount;

        bool success = strokeCountValid && coverage >= rule.RequiredCoverage && accuracy >= rule.RequiredAccuracy;

        return new RuneTraceResult(coverage, accuracy, session.StrokeCount, success);
    }


    private static float CalculateCoverage(IReadOnlyList<RuneStrokeData> referenceStrokes, IReadOnlyList<RuneStrokeData> inputStrokes, float tolerance)
    {
        int totalPointCount = 0;
        int coveredPointCount = 0;

        foreach (RuneStrokeData referenceStroke in referenceStrokes)
        {
            foreach (Vector2 referencePoint in referenceStroke.Points)
            {
                totalPointCount++;

                if (IsPointNearStrokes(referencePoint, inputStrokes, tolerance))
                {
                    coveredPointCount++;
                }
            }
        }

        if (totalPointCount == 0)
        {
            return 0f;
        }

        return (float)coveredPointCount / totalPointCount;
    }


    private static float CalculateAccuracy(IReadOnlyList<RuneStrokeData> referenceStrokes, IReadOnlyList<RuneStrokeData> inputStrokes, float tolerance)
    {
        int totalPointCount = 0;
        int accuratePointCount = 0;

        foreach (RuneStrokeData inputStroke in inputStrokes)
        {
            foreach (Vector2 inputPoint in inputStroke.Points)
            {
                totalPointCount++;

                if (IsPointNearStrokes(inputPoint, referenceStrokes, tolerance))
                {
                    accuratePointCount++;
                }
            }
        }

        if (totalPointCount == 0)
        {
            return 0f;
        }

        return (float)accuratePointCount / totalPointCount;
    }



    private static bool IsPointNearStrokes(Vector2 point, IReadOnlyList<RuneStrokeData> strokes, float tolerance)
    {
        foreach (RuneStrokeData stroke in strokes)
        {
            IReadOnlyList<Vector2> points = stroke.Points;

            for (int i = 0; i < points.Count - 1; i++)
            {
                float distance = DistancePointToSegment(point, points[i], points[i + 1]);

                if (distance <= tolerance)
                {
                    return true;
                }
            }
        }

        return false;
    }


    private static float DistancePointToSegment(Vector2 point, Vector2 segmentStart, Vector2 segmentEnd)
    {
        Vector2 segment = segmentEnd - segmentStart;

        float segmentLengthSquared = segment.sqrMagnitude;

        if (segmentLengthSquared <= Mathf.Epsilon)
        {
            return Vector2.Distance(point, segmentStart);
        }

        float t = Vector2.Dot(point - segmentStart, segment) / segmentLengthSquared;

        t = Mathf.Clamp01(t);

        Vector2 closestPoint = segmentStart + segment * t;

        return Vector2.Distance(point, closestPoint);
    }
}