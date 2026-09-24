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

        List<RuneStrokeData> evaluationStrokes = RunePathUtility.ResampleStrokes(session.Strokes, RuneTraceSettings.SAMPLE_INTERVAL);

        float coverage = CalculateCoverage(runeData.TraceData.Strokes, evaluationStrokes, rule.DistanceTolerance);

        float accuracy = CalculateAccuracy(runeData.TraceData.Strokes, evaluationStrokes, rule.DistanceTolerance);

        bool strokeCountValid = session.StrokeCount == rule.RequiredStrokeCount;

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

                if (RunePathUtility.IsPointNearStrokes(referencePoint, inputStrokes, tolerance))
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

                if (RunePathUtility.IsPointNearStrokes(inputPoint, referenceStrokes, tolerance))
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

}