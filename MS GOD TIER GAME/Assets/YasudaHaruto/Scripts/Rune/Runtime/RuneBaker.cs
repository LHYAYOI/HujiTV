//-----------------------------------------------
// RuneBaker.cs
// 制作日：2026/09/17
// 制作者：安田晴人
// 概要：ルーンの編集用データを判定用データへ変換するクラス
//-----------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public static class RuneBaker
{
    // AuthoringDataから判定用のStrokeを生成する
    public static List<RuneStrokeData> Bake(RuneAuthoringData authoringData, float sampleInterval)
    {
        List<RuneStrokeData> bakedStrokes = new();

        if (authoringData == null)
        {
            return bakedStrokes;
        }

        foreach (RuneStrokeData stroke in authoringData.Strokes)
        {
            if (stroke.Points.Count < 2)
            {
                continue;
            }

            List<Vector2> resampledPoints = stroke.PreserveCorners
                ? ResampleWithCorners(stroke.Points, sampleInterval)
                : RunePathUtility.Resample(stroke.Points, sampleInterval);

            if (resampledPoints.Count >= 2)
            {
                bakedStrokes.Add(new RuneStrokeData(resampledPoints));
            }
        }

        return bakedStrokes;
    }


    // Retain authored corners; subdivide each straight segment to at most the sample interval.
    private static List<Vector2> ResampleWithCorners(IReadOnlyList<Vector2> points, float interval)
    {
        var result = new List<Vector2>();
        if (points == null || points.Count == 0) return result;
        result.Add(points[0]);
        if (interval <= 0f) return result;
        for (int i = 1; i < points.Count; i++)
        {
            Vector2 start = points[i - 1], end = points[i];
            float distance = Vector2.Distance(start, end);
            if (distance <= Mathf.Epsilon) continue;
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / interval));
            for (int step = 1; step <= steps; step++)
                result.Add(Vector2.Lerp(start, end, (float)step / steps));
        }
        return result;
    }

}