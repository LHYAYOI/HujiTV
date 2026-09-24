//-----------------------------------------------
// IRuneTraceView.cs
// 制作日：2026/09/19
// 制作者：安田晴人
// 概要： ルーンのなぞりビューを定義するインターフェース
//-----------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public interface IRuneTraceView
{
    void BeginTrace(RuneData runeData);

    void UpdateCurrentStroke(IReadOnlyList<Vector2> points);

    void EndStroke();

    void PlaySuccess(RuneTraceResult result);

    void PlayFailure(RuneTraceResult result);

    void Clear();
}
