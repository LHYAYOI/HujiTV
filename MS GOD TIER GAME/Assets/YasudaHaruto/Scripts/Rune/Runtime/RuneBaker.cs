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

            List<Vector2> resampledPoints = RunePathUtility.Resample(stroke.Points, sampleInterval);

            if (resampledPoints.Count >= 2)
            {
                bakedStrokes.Add(new RuneStrokeData(resampledPoints));
            }
        }

        return bakedStrokes;
    }


}