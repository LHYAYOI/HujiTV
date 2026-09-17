//-----------------------------------------------
// RuneAuthoringData.cs
// 制作日：2026/09/17
// 制作者：安田晴人
// 概要：ルーンの編集用データを表すクラス
//-----------------------------------------------
using System;
using System.Collections.Generic;
using Unity.VectorGraphics;
using UnityEngine;

[Serializable]
public class RuneAuthoringData
{
    [SerializeField] private List<RuneStrokeData> m_strokes = new();

    public IReadOnlyList<RuneStrokeData> Strokes => m_strokes;

    // ルーンのストロークデータを追加するメソッド
    public void AddStroke(RuneStrokeData stroke)
    {
        if (stroke == null)
        {
            return;
        }

        m_strokes.Add(stroke);
    }

    // ルーンの最後のストロークデータを削除するメソッド
    public void RemoveLastStroke()
    {
        if (m_strokes.Count == 0)
        {
            return;
        }

        m_strokes.RemoveAt(m_strokes.Count - 1);
    }

    // ルーンのストロークデータをクリアするメソッド
    public void Clear()
    {
        m_strokes.Clear();
    }
}