//-----------------------------------------------
// RunePathUtility.cs
// 制作日：2026/09/18
// 制作者：安田晴人
// 概要：ルーンのパスに関する共通処理を提供するクラス
//-----------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public static class RunePathUtility
{

    // 指定したPointが、いずれかのStrokeの近くに存在するか
    public static bool IsPointNearStrokes(Vector2 point, IReadOnlyList<RuneStrokeData> strokes, float tolerance)
    {
        if (strokes == null)
        {
            return false;
        }

        foreach (RuneStrokeData stroke in strokes)
        {
            IReadOnlyList<Vector2> points = stroke.Points;

            for (int i = 0; i < points.Count - 1; i++)
            {
                // 線分と点の距離を計算
                float distance = DistancePointToSegment(point, points[i], points[i + 1]);

                if (distance <= tolerance)
                {
                    return true;
                }
            }
        }

        return false;
    }


    
    // Pointと線分との最短距離を求める
    public static float DistancePointToSegment(Vector2 point, Vector2 segmentStart, Vector2 segmentEnd)
    {
        // 線分のベクトルを計算
        Vector2 segment = segmentEnd - segmentStart;

        // 線分の長さの二乗を計算
        float segmentLengthSquared = segment.sqrMagnitude;

        // 線分の長さがほぼゼロの場合、線分の始点との距離を返す
        if (segmentLengthSquared <= Mathf.Epsilon)
        {
            return Vector2.Distance(point,segmentStart);
        }

        // 点から線分への射影を計算
        float t = Vector2.Dot(point - segmentStart, segment) / segmentLengthSquared;

        // tを0から1の範囲に制限
        t = Mathf.Clamp01(t);

        // 射影点を計算
        Vector2 closestPoint = segmentStart + segment * t;

        // 射影点と点との距離を返す
        return Vector2.Distance(point, closestPoint);
    }


    // Polylineを一定距離ごとのPointへ再サンプリングする
    public static List<Vector2> Resample(IReadOnlyList<Vector2> points, float interval)
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

        // 最初のPointを追加
        result.Add(points[0]);

        // 再サンプリングの残り距離を初期化
        float remainingDistance = interval;

        // 線分の始点を初期化
        Vector2 segmentStart = points[0];

        // 線分ごとに処理
        for (int i = 1; i < points.Count; i++)
        {
            // 線分の終点を取得
            Vector2 segmentEnd = points[i];

            // 線分の長さを計算
            float segmentLength = Vector2.Distance(segmentStart, segmentEnd);

            // 線分の長さがほぼゼロの場合、次の線分へ
            if (segmentLength <= Mathf.Epsilon)
            {
                segmentStart = segmentEnd;
                continue;
            }

            // 線分の長さが残り距離以上の場合、再サンプリングを行う
            while (segmentLength >= remainingDistance)
            {
                // 線分上の再サンプリング点を計算
                float t = remainingDistance / segmentLength;

                // 線分上の再サンプリング点を線形補間で求める
                Vector2 samplePoint = Vector2.Lerp(segmentStart, segmentEnd, t);

                // 再サンプリング点を結果に追加
                result.Add(samplePoint);

                // 線分の始点を再サンプリング点に更新
                segmentStart = samplePoint;

                // 線分の長さを再計算
                segmentLength = Vector2.Distance(segmentStart, segmentEnd);

                // 残り距離をリセット
                remainingDistance = interval;
            }

            // 線分の長さが残り距離未満の場合、残り距離を減算して次の線分へ
            remainingDistance -= segmentLength;

            // 線分の始点を更新
            segmentStart = segmentEnd;
        }

        // 最後のPointを追加（重複を避けるため、最後のPointがすでに追加されていない場合のみ追加）
        Vector2 lastPoint = points[points.Count - 1];
        if (Vector2.Distance(result[result.Count - 1], lastPoint) > Mathf.Epsilon)
        {
            result.Add(lastPoint);
        }

        return result;
    }

    public static List<RuneStrokeData> ResampleStrokes(IReadOnlyList<RuneStrokeData> strokes, float interval)
    {
        List<RuneStrokeData> result = new();

        if (strokes == null)
        {
            return result;
        }

        foreach (RuneStrokeData stroke in strokes)
        {
            if (stroke.Points.Count < 2)
            {
                continue;
            }

            List<Vector2> resampledPoints = Resample(stroke.Points, interval);

            if (resampledPoints.Count < 2)
            {
                continue;
            }

            result.Add(new RuneStrokeData(resampledPoints));
        }

        return result;
    }
}