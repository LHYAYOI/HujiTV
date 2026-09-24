//-----------------------------------------------
// RuneStrokeData.cs
// 制作日：2026/09/17
// 制作者：安田晴人
// 概要：ルーンのストロークデータを表すクラス
//-----------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RuneStrokeData
{
    [SerializeField] private List<Vector2> m_points = new();

    public IReadOnlyList<Vector2> Points => m_points;

    public RuneStrokeData()
    {
    }

    public RuneStrokeData(IEnumerable<Vector2> points)
    {
        this.m_points = new List<Vector2>(points);
    }

    // ルーンのストロークデータを追加するメソッド
    public void AddPoint(Vector2 point)
    {
        m_points.Add(point);
    }

    // ルーンのストロークデータをクリアするメソッド
    public void Clear()
    {
        m_points.Clear();
    }
}