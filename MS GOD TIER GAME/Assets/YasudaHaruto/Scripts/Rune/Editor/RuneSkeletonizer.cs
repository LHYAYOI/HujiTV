//-----------------------------------------------
// RuneSkeletonizer.cs
// 制作日：2026/09/29
// 制作者：安田晴人
// 概要：二値化されたルーン画像を細線化するクラス
//-----------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public static class RuneSkeletonizer
{
    // 二値画像を細線化するメソッド
    public static bool[,] Skeletonize(bool[,] source)
    {
        if (source == null)
        {
            return null;
        }

        int width = source.GetLength(0);
        int height = source.GetLength(1);

        bool[,] result = (bool[,])source.Clone();

        bool changed;

        do
        {
            changed = false;

            List<Vector2Int> removePixels = new();

            // Step 1
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    if (!result[x, y])
                    {
                        continue;
                    }

                    if (ShouldRemoveStep1(result, x, y))
                    {
                        removePixels.Add(new Vector2Int(x, y));
                    }
                }
            }

            if (removePixels.Count > 0)
            {
                changed = true;

                foreach (Vector2Int pixel in removePixels)
                {
                    result[pixel.x, pixel.y] = false;
                }
            }

            removePixels.Clear();

            // Step 2
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    if (!result[x, y])
                    {
                        continue;
                    }

                    if (ShouldRemoveStep2(result, x, y))
                    {
                        removePixels.Add(new Vector2Int(x, y));
                    }
                }
            }

            if (removePixels.Count > 0)
            {
                changed = true;

                foreach (Vector2Int pixel in removePixels)
                {
                    result[pixel.x, pixel.y] = false;
                }
            }

        } while (changed);

        return result;
    }

    private static bool ShouldRemoveStep1(bool[,] image, int x, int y)
    {
        GetNeighbors(image, x, y, out bool p2, out bool p3, out bool p4,
            out bool p5, out bool p6, out bool p7, out bool p8, out bool p9);

        int neighborCount = CountNeighbors(
            p2, p3, p4, p5, p6, p7, p8, p9);

        if (neighborCount < 2 || neighborCount > 6)
        {
            return false;
        }

        int transitionCount = CountTransitions(
            p2, p3, p4, p5, p6, p7, p8, p9);

        if (transitionCount != 1)
        {
            return false;
        }

        if (p2 && p4 && p6)
        {
            return false;
        }

        if (p4 && p6 && p8)
        {
            return false;
        }

        return true;
    }

    private static bool ShouldRemoveStep2(bool[,] image, int x, int y)
    {
        GetNeighbors(image, x, y, out bool p2, out bool p3, out bool p4,
            out bool p5, out bool p6, out bool p7, out bool p8, out bool p9);

        int neighborCount = CountNeighbors(
            p2, p3, p4, p5, p6, p7, p8, p9);

        if (neighborCount < 2 || neighborCount > 6)
        {
            return false;
        }

        int transitionCount = CountTransitions(
            p2, p3, p4, p5, p6, p7, p8, p9);

        if (transitionCount != 1)
        {
            return false;
        }

        if (p2 && p4 && p8)
        {
            return false;
        }

        if (p2 && p6 && p8)
        {
            return false;
        }

        return true;
    }

    private static void GetNeighbors(
        bool[,] image,
        int x,
        int y,
        out bool p2,
        out bool p3,
        out bool p4,
        out bool p5,
        out bool p6,
        out bool p7,
        out bool p8,
        out bool p9)
    {
        // Zhang-Suen法で使用する周囲8ピクセル
        //
        // p9 p2 p3
        // p8 P  p4
        // p7 p6 p5

        p2 = image[x, y + 1];
        p3 = image[x + 1, y + 1];
        p4 = image[x + 1, y];
        p5 = image[x + 1, y - 1];
        p6 = image[x, y - 1];
        p7 = image[x - 1, y - 1];
        p8 = image[x - 1, y];
        p9 = image[x - 1, y + 1];
    }

    private static int CountNeighbors(
        bool p2,
        bool p3,
        bool p4,
        bool p5,
        bool p6,
        bool p7,
        bool p8,
        bool p9)
    {
        int count = 0;

        if (p2) count++;
        if (p3) count++;
        if (p4) count++;
        if (p5) count++;
        if (p6) count++;
        if (p7) count++;
        if (p8) count++;
        if (p9) count++;

        return count;
    }

    private static int CountTransitions(
        bool p2,
        bool p3,
        bool p4,
        bool p5,
        bool p6,
        bool p7,
        bool p8,
        bool p9)
    {
        bool[] neighbors =
        {
            p2,
            p3,
            p4,
            p5,
            p6,
            p7,
            p8,
            p9,
            p2
        };

        int transitions = 0;

        for (int i = 0; i < 8; i++)
        {
            if (!neighbors[i] && neighbors[i + 1])
            {
                transitions++;
            }
        }

        return transitions;
    }
}