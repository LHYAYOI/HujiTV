//-----------------------------------------------
// RuneStrokeExtractor.cs
// 制作日：2026/10/02
// 制作者：安田晴人
// 概要：細線化されたルーン画像からストロークを抽出するクラス
//-----------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public static class RuneStrokeExtractor
{
    private static readonly Vector2Int[] NEIGHBOR_OFFSETS =
    {
        new Vector2Int(-1,  1),
        new Vector2Int( 0,  1),
        new Vector2Int( 1,  1),
        new Vector2Int(-1,  0),
        new Vector2Int( 1,  0),
        new Vector2Int(-1, -1),
        new Vector2Int( 0, -1),
        new Vector2Int( 1, -1)
    };

    // Skeletonからストロークを抽出するメソッド
    public static List<RuneStrokeData> Extract(bool[,] skeleton)
    {
        List<RuneStrokeData> strokes = new();

        if (skeleton == null)
        {
            return strokes;
        }

        int width = skeleton.GetLength(0);
        int height = skeleton.GetLength(1);

        // 端点・分岐点を取得
        HashSet<Vector2Int> nodes = FindNodes(skeleton);

        // 使用済みの接続を記録
        HashSet<Edge> visitedEdges = new();

        foreach (Vector2Int node in nodes)
        {
            List<Vector2Int> neighbors = GetNeighbors(skeleton, node);

            foreach (Vector2Int neighbor in neighbors)
            {
                Edge firstEdge = new Edge(node, neighbor);

                if (visitedEdges.Contains(firstEdge))
                {
                    continue;
                }

                List<Vector2Int> path = TracePath(skeleton, node, neighbor, nodes, visitedEdges);

                if (path.Count < 2)
                {
                    continue;
                }

                List<Vector2> normalizedPoints = ConvertToNormalized(path, width, height);

                strokes.Add(new RuneStrokeData(normalizedPoints));
            }
        }

        return strokes;
    }

    // 端点・分岐点を取得するメソッド
    // 端点・分岐点を取得するメソッド
    private static HashSet<Vector2Int> FindNodes(
        bool[,] skeleton)
    {
        HashSet<Vector2Int> nodes = new();

        int width = skeleton.GetLength(0);
        int height = skeleton.GetLength(1);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!skeleton[x, y])
                {
                    continue;
                }

                Vector2Int position =
                    new Vector2Int(x, y);

                List<Vector2Int> neighbors =
                    GetNeighbors(skeleton, position);

                int directionCount =
                    CountConnectionGroups(
                        neighbors);

                // 端点
                if (directionCount == 1)
                {
                    nodes.Add(position);
                }
                // 分岐点
                else if (directionCount >= 3)
                {
                    nodes.Add(position);
                }
            }
        }

        return nodes;
    }

    // 周囲のSkeletonピクセルが何方向に接続しているかを取得するメソッド
    private static int CountConnectionGroups(
        List<Vector2Int> neighbors)
    {
        if (neighbors == null || neighbors.Count == 0)
        {
            return 0;
        }

        HashSet<Vector2Int> unvisited =
            new HashSet<Vector2Int>(neighbors);

        int groupCount = 0;

        while (unvisited.Count > 0)
        {
            Vector2Int start = default;

            foreach (Vector2Int position in unvisited)
            {
                start = position;
                break;
            }

            groupCount++;

            Queue<Vector2Int> queue = new();

            queue.Enqueue(start);
            unvisited.Remove(start);

            while (queue.Count > 0)
            {
                Vector2Int current =
                    queue.Dequeue();

                List<Vector2Int> connected =
                    new();

                foreach (Vector2Int other in unvisited)
                {
                    if (AreNeighborPixels(
                        current,
                        other))
                    {
                        connected.Add(other);
                    }
                }

                foreach (Vector2Int position in connected)
                {
                    unvisited.Remove(position);
                    queue.Enqueue(position);
                }
            }
        }

        return groupCount;
    }

    private static bool AreNeighborPixels(
    Vector2Int a,
    Vector2Int b)
    {
        int deltaX =
            Mathf.Abs(a.x - b.x);

        int deltaY =
            Mathf.Abs(a.y - b.y);

        return deltaX <= 1
            && deltaY <= 1
            && (deltaX != 0 || deltaY != 0);
    }

    // Nodeから次のNodeまで経路を追跡するメソッド
    private static List<Vector2Int> TracePath(bool[,] skeleton, Vector2Int start, Vector2Int first, HashSet<Vector2Int> nodes, HashSet<Edge> visitedEdges)
    {
        List<Vector2Int> path = new()
        {
            start
        };

        Vector2Int previous = start;
        Vector2Int current = first;

        visitedEdges.Add(new Edge(previous, current));

        while (true)
        {
            path.Add(current);

            // 開始地点以外のNodeに到達
            if (nodes.Contains(current)
                && current != start)
            {
                break;
            }

            List<Vector2Int> neighbors = GetNeighbors(skeleton, current);

            Vector2Int next = default;
            bool foundNext = false;

            foreach (Vector2Int neighbor in neighbors)
            {
                if (neighbor == previous)
                {
                    continue;
                }

                Edge edge = new Edge(current, neighbor);

                if (visitedEdges.Contains(edge))
                {
                    continue;
                }

                next = neighbor;
                foundNext = true;

                break;
            }

            if (!foundNext)
            {
                break;
            }

            visitedEdges.Add(new Edge(current, next));

            previous = current;
            current = next;
        }

        return path;
    }

    // 周囲8方向のSkeletonピクセルを取得するメソッド
    private static List<Vector2Int> GetNeighbors(bool[,] skeleton,
        Vector2Int position)
    {
        List<Vector2Int> neighbors = new();

        int width = skeleton.GetLength(0);
        int height = skeleton.GetLength(1);

        foreach (Vector2Int offset in NEIGHBOR_OFFSETS)
        {
            Vector2Int target = position + offset;

            if (target.x < 0 || target.x >= width || target.y < 0 || target.y >= height)
            {
                continue;
            }

            if (skeleton[target.x, target.y])
            {
                neighbors.Add(target);
            }
        }

        return neighbors;
    }

    // Pixel座標をRuneData用の0～1座標へ変換するメソッド
    private static List<Vector2> ConvertToNormalized(List<Vector2Int> path, int width, int height)
    {
        List<Vector2> points = new();

        foreach (Vector2Int pixel in path)
        {
            float x = width > 1 ? (float)pixel.x / (width - 1) : 0f;

            float y = height > 1 ? (float)pixel.y / (height - 1) : 0f;

            points.Add(new Vector2(x, y));
        }

        return points;
    }

    // Skeleton上の接続を表す構造体
    private readonly struct Edge
    {
        private readonly Vector2Int m_a;
        private readonly Vector2Int m_b;

        public Edge(Vector2Int a, Vector2Int b)
        {
            // A-BとB-Aを同じEdgeとして扱う
            if (a.x < b.x || (a.x == b.x && a.y <= b.y))
            {
                m_a = a;
                m_b = b;
            }
            else
            {
                m_a = b;
                m_b = a;
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is not Edge other)
            {
                return false;
            }

            return m_a == other.m_a && m_b == other.m_b;
        }

        public override int GetHashCode()
        {
            return System.HashCode.Combine(m_a, m_b);
        }
    }
}