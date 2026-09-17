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

            List<Vector2> resampledPoints = Resample(stroke.Points, sampleInterval);

            if (resampledPoints.Count >= 2)
            {
                bakedStrokes.Add(new RuneStrokeData(resampledPoints));
            }
        }

        return bakedStrokes;
    }


    // Polylineを一定距離ごとのPointへ再サンプリングする
    private static List<Vector2> Resample(IReadOnlyList<Vector2> points, float interval)
    {
        List<Vector2> result = new();

        if (points == null || points.Count == 0)
        {
            return result;
        }

        if (points.Count == 1 || interval <= 0f)
        {
            result.Add(points[0]);
            return result;
        }

        // 最初のPointは必ず追加する
        result.Add(points[0]);

        float remainingDistance = interval;

        Vector2 segmentStart = points[0];

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector2 segmentEnd = points[i + 1];

            float segmentLength = Vector2.Distance(segmentStart, segmentEnd);

            // ほぼ同じ位置なら無視する
            if (segmentLength <= Mathf.Epsilon)
            {
                segmentStart = segmentEnd;
                continue;
            }

            while (segmentLength >= remainingDistance)
            {
                float t = remainingDistance / segmentLength;

                Vector2 samplePoint = Vector2.Lerp(segmentStart, segmentEnd, t);

                result.Add(samplePoint);

                segmentStart = samplePoint;

                segmentLength = Vector2.Distance(segmentStart, segmentEnd);

                remainingDistance = interval;
            }

            remainingDistance -= segmentLength;

            segmentStart = segmentEnd;
        }

        // 最後のPointも残す
        Vector2 lastPoint = points[points.Count - 1];

        if (Vector2.Distance(result[result.Count - 1], lastPoint) > Mathf.Epsilon)
        {
            result.Add(lastPoint);
        }

        return result;
    }
}