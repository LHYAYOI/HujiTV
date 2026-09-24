using System.Collections.Generic;
using UnityEngine;

public class DebugRuneTraceView : MonoBehaviour, IRuneTraceView
{
    [SerializeField] private RuneLineGraphic m_referenceGraphic;

    [SerializeField] private RuneLineGraphic m_playerGraphic;

    public void BeginTrace(RuneData runeData)
    {
        Debug.Log($"Rune Trace Start : {runeData.DisplayName}");

        m_referenceGraphic.SetStrokes(runeData.TraceData.Strokes);

        m_playerGraphic.Clear();
    }

    public void UpdateCurrentStroke(IReadOnlyList<Vector2> points)
    {
        m_playerGraphic.SetCurrentStroke(points);
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

        m_referenceGraphic.Clear();
    }
}