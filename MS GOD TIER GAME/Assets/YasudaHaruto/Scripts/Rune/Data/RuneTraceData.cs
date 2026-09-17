//-----------------------------------------------
// RuneTraceData.cs
// 制作日：2026/09/17
// 制作者：安田晴人
// 概要：ルーンのゲーム内判定のなぞりデータを表すクラス
//-----------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RuneTraceData
{
    [SerializeField] private List<RuneStrokeData> m_strokes = new();

    public IReadOnlyList<RuneStrokeData> Strokes => m_strokes;

    // ルーンのストロークデータを設定するメソッド
    public void SetStrokes(IEnumerable<RuneStrokeData> strokes)
    {
        m_strokes.Clear();
        if (strokes == null)
        {
            return;
        }

        m_strokes.AddRange(strokes);
    }

    // ルーンのストロークデータをクリアするメソッド
    public void Clear()
    {
        m_strokes.Clear();
    }
}