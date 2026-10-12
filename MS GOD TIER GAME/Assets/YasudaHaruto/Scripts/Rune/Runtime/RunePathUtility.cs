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


    public static float CalculatePathLength(IReadOnlyList<Vector2> points)
    {
        if (points == null || points.Count < 2) return 0f;

        float length = 0f;
        for (int i = 1; i < points.Count; i++)
            length += Vector2.Distance(points[i - 1], points[i]);

        return length;
    }

    // 指定した位置をパス上に射影し、パス上の距離とパスからの距離を返す
    public static bool TryProjectOnPath(IReadOnlyList<Vector2> points, Vector2 position, float currentDistance, float backwardAllowance, float forwardAllowance, out float projectedDistance, out float distanceFromPath)
    {
        // 初期化
        projectedDistance = 0f;
        distanceFromPath = float.PositiveInfinity;

        if (points == null || points.Count < 2)
        {// パスが無効な場合は失敗
            return false;
        }

        // 許容範囲を計算
        float minDistance = Mathf.Max(0f, currentDistance - backwardAllowance);
        float maxDistance = currentDistance + forwardAllowance;
        float accumulated = 0f;
        bool found = false;

        // パスの各線分に対して処理
        for (int i = 1; i < points.Count; i++)
        {
            // 線分の始点と終点を取得
            Vector2 start = points[i - 1];
            Vector2 end = points[i];

            // 線分のベクトルと長さを計算
            Vector2 segment = end - start;
            float length = segment.magnitude;

            if (length <= Mathf.Epsilon)
            {// 線分の長さがほぼゼロの場合はスキップ
                continue;
            }

            // 許容範囲内の線分のパラメータ範囲を計算
            float rangeStart = Mathf.Max(0f, (minDistance - accumulated) / length);
            float rangeEnd = Mathf.Min(1f, (maxDistance - accumulated) / length);

            if (rangeStart <= rangeEnd)
            {// 許容範囲内の線分が存在する場合、射影を計算

                // 点から線分への射影を計算
                float t = Vector2.Dot(position - start, segment) / (length * length);
                t = Mathf.Clamp(t, rangeStart, rangeEnd);

                // 射影点を計算
                Vector2 closest = start + segment * t;
                float distance = Vector2.Distance(position, closest);
                float pathDistance = accumulated + length * t;

                if (!found || distance < distanceFromPath - 0.000001f || (Mathf.Abs(distance - distanceFromPath) <= 0.000001f &&
                     Mathf.Abs(pathDistance - currentDistance) < Mathf.Abs(projectedDistance - currentDistance)))
                {// より近い射影点が見つかった場合、または距離が同じで現在の距離に近い場合、更新
                    distanceFromPath = distance;
                    projectedDistance = pathDistance;
                    found = true;
                }
            }

            // 線分の長さを累積距離に加算
            accumulated += length;
        }

        // 射影点が見つかったかどうかを返す
        return found;
    }

}