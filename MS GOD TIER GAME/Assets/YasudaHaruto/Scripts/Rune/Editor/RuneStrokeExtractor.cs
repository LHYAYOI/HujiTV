//-----------------------------------------------
// RuneStrokeExtractor.cs
// 制作日：2026/10/02
// 制作者：安田晴人
// 概要：細線化されたルーン画像からストロークを抽出するクラス
//-----------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;

public static class RuneStrokeExtractor
{
    // A pixel graph with no diagonal shortcut across an orthogonal connection.
    public static List<RuneStrokeData> Extract(bool[,] skeleton)
    {
        var result = new List<RuneStrokeData>();
        if (skeleton == null) return result;
        int width = skeleton.GetLength(0), height = skeleton.GetLength(1);
        var graph = new Dictionary<int, List<int>>();
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            if (!skeleton[x, y]) continue;
            var neighbors = new List<int>();
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if ((dx == 0 && dy == 0) || nx < 0 || ny < 0 || nx >= width || ny >= height || !skeleton[nx, ny]) continue;
                if (dx != 0 && dy != 0 && (skeleton[nx, y] || skeleton[x, ny])) continue;
                neighbors.Add(ny * width + nx);
            }
            graph.Add(y * width + x, neighbors);
        }
        var visited = new HashSet<ulong>();
        var paths = new List<List<int>>();
        // Open paths first. A second pass also captures components made only of loops.
        for (int pass = 0; pass < 2; pass++)
        foreach (var entry in graph)
        {
            if (pass == 0 && entry.Value.Count == 2) continue;
            foreach (int neighbor in entry.Value)
            {
                if (visited.Contains(Edge(entry.Key, neighbor))) continue;
                var path = new List<int> { entry.Key };
                int previous = entry.Key, current = neighbor;
                visited.Add(Edge(previous, current));
                while (true)
                {
                    path.Add(current);
                    if (current == path[0] || graph[current].Count != 2) break;
                    int next = graph[current][0] == previous ? graph[current][1] : graph[current][0];
                    if (!visited.Add(Edge(current, next))) break;
                    previous = current;
                    current = next;
                }
                paths.Add(path);
            }
        }
        // Pair the straightest branches at each junction using a short tangent,
        // rather than a one-pixel direction which is unstable on stair-step lines.
        while (true)
        {
            float best = -0.85f;
            int first = -1, second = -1;
            bool reverseFirst = false, reverseSecond = false;
            for (int a = 0; a < paths.Count; a++)
            for (int b = a + 1; b < paths.Count; b++)
            for (int ea = 0; ea < 2; ea++)
            for (int eb = 0; eb < 2; eb++)
            {
                var pa = paths[a]; var pb = paths[b];
                if (pa[0] == pa[pa.Count - 1] || pb[0] == pb[pb.Count - 1]) continue;
                int ja = ea == 0 ? 0 : pa.Count - 1;
                int jb = eb == 0 ? 0 : pb.Count - 1;
                if (pa[ja] != pb[jb]) continue;
                Vector2 da = Tangent(pa, ea == 0, width);
                Vector2 db = Tangent(pb, eb == 0, width);
                float dot = Vector2.Dot(da, db);
                if (dot >= best) continue;
                best = dot; first = a; second = b;
                reverseFirst = ea == 0; reverseSecond = eb == 1;
            }
            if (first < 0) break;
            if (reverseFirst) paths[first].Reverse();
            if (reverseSecond) paths[second].Reverse();
            paths[first].AddRange(paths[second].GetRange(1, paths[second].Count - 1));
            paths.RemoveAt(second);
        }
        foreach (var path in paths)
        {
            var points = new List<Vector2>();
            foreach (int id in path)
                points.Add(new Vector2(width > 1 ? (float)(id % width) / (width - 1) : 0f,
                    height > 1 ? (float)(id / width) / (height - 1) : 0f));
            result.Add(new RuneStrokeData(points));
        }
        return result;
    }

    private static Vector2 Tangent(List<int> path, bool start, int width)
    {
        int origin = start ? path[0] : path[path.Count - 1];
        int offset = Math.Min(6, path.Count - 1);
        int sample = start ? path[offset] : path[path.Count - 1 - offset];
        return new Vector2(sample % width - origin % width, sample / width - origin / width).normalized;
    }

    private static ulong Edge(int a, int b)
    {
        return ((ulong)(uint)Math.Min(a, b) << 32) | (uint)Math.Max(a, b);
    }
}
