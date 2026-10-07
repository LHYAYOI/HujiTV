//-----------------------------------------------
// RunePathSimplifier.cs
// 制作日：2026/10/02
// 制作者：安田晴人
// 概要：ルーンのパスを形状を保ちながら簡略化するクラス
//-----------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public static class RunePathSimplifier
{
    // RDP法を使用してパスを簡略化するメソッド
    public static List<Vector2> Simplify(
        IReadOnlyList<Vector2> points,
        float tolerance)
    {
        List<Vector2> result = new();

        if (points == null || points.Count == 0)
        {
            return result;
        }

        if (points.Count <= 2)
        {
            result.AddRange(points);
            return result;
        }

        SimplifyRecursive(
            points,
            0,
            points.Count - 1,
            tolerance,
            result);

        // 最後の点は再帰処理では追加されないため追加する
        result.Add(points[points.Count - 1]);

        return result;
    }

    private static void SimplifyRecursive(
        IReadOnlyList<Vector2> points,
        int startIndex,
        int endIndex,
        float tolerance,
        List<Vector2> result)
    {
        if (endIndex <= startIndex + 1)
        {
            result.Add(points[startIndex]);
            return;
        }

        Vector2 start = points[startIndex];
        Vector2 end = points[endIndex];

        float maxDistance = 0f;
        int farthestIndex = -1;

        for (int i = startIndex + 1; i < endIndex; i++)
        {
            float distance =
                DistanceToSegment(
                    points[i],
                    start,
                    end);

            if (distance > maxDistance)
            {
                maxDistance = distance;
                farthestIndex = i;
            }
        }

        // 許容誤差より大きく曲がっている点が存在する場合
        if (maxDistance > tolerance
            && farthestIndex >= 0)
        {
            SimplifyRecursive(
                points,
                startIndex,
                farthestIndex,
                tolerance,
                result);

            SimplifyRecursive(
                points,
                farthestIndex,
                endIndex,
                tolerance,
                result);
        }
        else
        {
            // 中間点は不要
            result.Add(start);
        }
    }

    // 点と線分の最短距離を求めるメソッド
    private static float DistanceToSegment(
        Vector2 point,
        Vector2 start,
        Vector2 end)
    {
        Vector2 segment = end - start;

        float lengthSquared =
            segment.sqrMagnitude;

        if (lengthSquared <= Mathf.Epsilon)
        {
            return Vector2.Distance(
                point,
                start);
        }

        float t =
            Vector2.Dot(
                point - start,
                segment)
            / lengthSquared;

        t = Mathf.Clamp01(t);

        Vector2 closestPoint =
            start + segment * t;

        return Vector2.Distance(
            point,
            closestPoint);
    }
}