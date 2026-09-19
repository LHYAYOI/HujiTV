using System.Collections.Generic;
using UnityEngine;

public class DebugRuneTraceView : MonoBehaviour, IRuneTraceView
{
    public void BeginTrace(RuneData runeData)
    {
        Debug.Log(
            $"Rune Trace Start : {runeData.DisplayName}");
    }

    public void UpdateCurrentStroke(
        IReadOnlyList<Vector2> points)
    {
        // ç°ÇÕï\é¶Ç»Çµ
    }

    public void EndStroke()
    {
        Debug.Log("Stroke End");
    }

    public void PlaySuccess(
        RuneTraceResult result)
    {
        Debug.Log(
            $"Rune SUCCESS " +
            $"Coverage:{result.Coverage:P1} " +
            $"Accuracy:{result.Accuracy:P1}");
    }

    public void PlayFailure(
        RuneTraceResult result)
    {
        Debug.Log(
            $"Rune FAILED " +
            $"Coverage:{result.Coverage:P1} " +
            $"Accuracy:{result.Accuracy:P1}");
    }

    public void Clear()
    {
        Debug.Log("Rune Trace Clear");
    }
}