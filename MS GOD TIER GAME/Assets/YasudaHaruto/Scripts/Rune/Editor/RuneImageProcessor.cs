//-----------------------------------------------
// RuneImagePreprocessor.cs
// 制作日：2026/09/29
// 制作者：安田晴人
// 概要：ルーン画像を解析用の二値画像に変換するクラス
//-----------------------------------------------
using UnityEngine;

public static class RuneImagePreprocessor
{
    /// <summary>
    /// Texture2Dを二値画像に変換する
    /// true  = ルーン部分
    /// false = 背景部分
    /// </summary>
    public static bool[,] Binarize(Texture2D texture, float threshold)
    {
        if (texture == null)
        {
            return null;
        }

        int width = texture.width;
        int height = texture.height;

        bool[,] result = new bool[width, height];

        Color[] pixels = texture.GetPixels();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color color = pixels[y * width + x];

                // RGBから明るさを求める
                float brightness =
                    color.r * 0.299f +
                    color.g * 0.587f +
                    color.b * 0.114f;

                // 暗い部分をルーンとして扱う
                result[x, y] = brightness < threshold;
            }
        }

        return result;
    }
}