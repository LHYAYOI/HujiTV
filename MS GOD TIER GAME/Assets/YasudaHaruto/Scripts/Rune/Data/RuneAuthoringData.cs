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

    // ルーンの最後のストロークデータを削除するメソッド
    public void RemoveLastStroke()
    {
        if (m_strokes.Count == 0)
        {
            return;
        }

        m_strokes.RemoveAt(m_strokes.Count - 1);
    }

    // ルーンのストロークデータを指定したインデックスに移動するメソッド
    public bool MoveStroke(int index, int newIndex)
    {
        if (index < 0 || index >= m_strokes.Count || newIndex < 0 || newIndex >= m_strokes.Count || index == newIndex)
        {// インデックスが範囲外または同じ場合は移動できない
            return false;
        }

        RuneStrokeData stroke = m_strokes[index];
        m_strokes.RemoveAt(index);
        m_strokes.Insert(newIndex, stroke);
        return true;
    }

    // ルーンのストロークデータを逆順にするメソッド
    public bool ReverseStroke(int index)
    {
        if (index < 0 || index >= m_strokes.Count)
        {// インデックスが範囲外の場合は逆順にできない
            return false;
        }

        m_strokes[index].Reverse();
        return true;
    }


    public bool SplitStroke(int strokeIndex, int pointIndex)
    {
        if (strokeIndex < 0 || strokeIndex >= m_strokes.Count) return false;

        IReadOnlyList<Vector2> points = m_strokes[strokeIndex].Points;
        if (pointIndex <= 0 || pointIndex >= points.Count - 1) return false;

        List<Vector2> first = new();
        List<Vector2> second = new();

        for (int i = 0; i <= pointIndex; i++) first.Add(points[i]);
        for (int i = pointIndex; i < points.Count; i++) second.Add(points[i]);

        m_strokes[strokeIndex] = new RuneStrokeData(first);
        m_strokes.Insert(strokeIndex + 1, new RuneStrokeData(second));
        return true;
    }

    public bool MergeStrokes(int firstIndex, int secondIndex, float maxEndpointDistance)
    {
        if (firstIndex < 0 || firstIndex >= m_strokes.Count ||
            secondIndex < 0 || secondIndex >= m_strokes.Count ||
            firstIndex == secondIndex) return false;

        IReadOnlyList<Vector2> first = m_strokes[firstIndex].Points;
        IReadOnlyList<Vector2> second = m_strokes[secondIndex].Points;
        if (first.Count < 2 || second.Count < 2) return false;

        float bestDistance = float.PositiveInfinity;
        bool reverseFirst = false;
        bool reverseSecond = false;

        for (int a = 0; a < 2; a++)
        {
            for (int b = 0; b < 2; b++)
            {
                Vector2 endA = a == 0 ? first[first.Count - 1] : first[0];
                Vector2 startB = b == 0 ? second[0] : second[second.Count - 1];
                float distance = Vector2.Distance(endA, startB);

                if (distance >= bestDistance) continue;

                bestDistance = distance;
                reverseFirst = a == 1;
                reverseSecond = b == 1;
            }
        }

        if (bestDistance > Mathf.Max(0f, maxEndpointDistance)) return false;

        List<Vector2> merged = new(first);
        List<Vector2> next = new(second);

        if (reverseFirst) merged.Reverse();
        if (reverseSecond) next.Reverse();

        // 接続点の微小なずれを吸収する
        Vector2 joint = (merged[merged.Count - 1] + next[0]) * 0.5f;
        merged[merged.Count - 1] = joint;

        for (int i = 1; i < next.Count; i++) merged.Add(next[i]);

        m_strokes[firstIndex] = new RuneStrokeData(merged);
        m_strokes.RemoveAt(secondIndex);
        return true;
    }


    // ルーンのストロークデータをクリアするメソッド
    public void Clear()
    {
        m_strokes.Clear();
    }
}