//-----------------------------------------------
// RuneTraceResult.cs
// 制作日：2026/09/17
// 制作者：安田晴人
// 概要：ルーンのなぞり判定結果を表すクラス
//-----------------------------------------------

public class RuneTraceResult
{
    public float Coverage { get; }
    public float Accuracy { get; }
    public int StrokeCount { get; }
    public bool IsSuccess { get; }

    public RuneTraceResult(float coverage, float accuracy, int strokeCount, bool isSuccess)
    {
        Coverage = coverage;
        Accuracy = accuracy;
        StrokeCount = strokeCount;
        IsSuccess = isSuccess;
    }
}