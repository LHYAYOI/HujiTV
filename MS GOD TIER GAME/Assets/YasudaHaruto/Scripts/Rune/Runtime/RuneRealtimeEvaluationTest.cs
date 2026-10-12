
using UnityEngine;

public class RuneRealtimeEvaluationTest : MonoBehaviour
{
    [SerializeField] private RuneData m_runeData;
    [SerializeField] private int m_strokeIndex;

    [Header("Test Settings")]
    [SerializeField] private float m_startTolerance = 0.04f;
    [SerializeField] private float m_endTolerance = 0.025f;
    [SerializeField] private float m_sampleInterval = 0.005f;

    [ContextMenu("Run Realtime Evaluation Test")]
    private void RunTest()
    {
        if (m_runeData == null || m_strokeIndex < 0 ||
            m_strokeIndex >= m_runeData.TraceData.Strokes.Count)
        {
            Debug.LogWarning("RuneDataÇ‹ÇΩÇÕStrokeIndexÇ™ê≥ÇµÇ≠Ç†ÇËÇ‹ÇπÇÒÅB");
            return;
        }

        var points = m_runeData.TraceData.Strokes[m_strokeIndex].Points;
        if (points.Count < 2) return;

        var evaluator = new RuneTraceRealtimeEvaluator(
            points, m_runeData.Rule.DistanceTolerance,
            m_startTolerance, m_endTolerance, m_sampleInterval);

        if (!evaluator.Begin(points[0]))
        {
            Debug.LogWarning("Begin Failed");
            return;
        }

        for (int i = 1; i < points.Count; i++)
        {
            RUNE_REALTIME_RESULT result = evaluator.EvaluateSegment(points[i - 1], points[i]);

            Debug.Log($"Point={i}, Progress={evaluator.Progress:P1}, Result={result}");

            if (result != RUNE_REALTIME_RESULT.IN_PROGRESS) break;
        }
    }

    [ContextMenu("Test Wrong Path")]
    private void TestWrongPath()
    {
        if (m_runeData == null || m_runeData.TraceData.Strokes.Count == 0) return;

        var points = m_runeData.TraceData.Strokes[m_strokeIndex].Points;
        if (points.Count < 2) return;

        var evaluator = new RuneTraceRealtimeEvaluator(
            points, m_runeData.Rule.DistanceTolerance,
            m_startTolerance, m_endTolerance, m_sampleInterval);

        evaluator.Begin(points[0]);

        Vector2 wrongPosition = points[0] + Vector2.up * 0.3f;
        RUNE_REALTIME_RESULT result = evaluator.EvaluateSegment(points[0], wrongPosition);

        Debug.Log($"Wrong Path Test : {result}, Progress={evaluator.Progress:P1}");
    }
    [ContextMenu("Test Shortcut")]
    private void TestShortcut()
    {
        if (m_runeData == null || m_strokeIndex < 0 ||
            m_strokeIndex >= m_runeData.TraceData.Strokes.Count) return;

        var points = m_runeData.TraceData.Strokes[m_strokeIndex].Points;
        if (points.Count < 2) return;

        var evaluator = new RuneTraceRealtimeEvaluator(
            points, m_runeData.Rule.DistanceTolerance,
            m_startTolerance, m_endTolerance, m_sampleInterval);

        if (!evaluator.Begin(points[0])) return;

        RUNE_REALTIME_RESULT result = evaluator.EvaluateSegment(points[0], points[points.Count - 1]);

        Debug.Log($"Shortcut Test : {result}, Progress={evaluator.Progress:P1}");
    }
    [ContextMenu("Test Backtracking")]
    private void TestBacktracking()
    {
        if (m_runeData == null || m_strokeIndex < 0 ||
            m_strokeIndex >= m_runeData.TraceData.Strokes.Count) return;

        var points = m_runeData.TraceData.Strokes[m_strokeIndex].Points;
        if (points.Count < 2) return;

        var evaluator = new RuneTraceRealtimeEvaluator(
            points, m_runeData.Rule.DistanceTolerance,
            m_startTolerance, m_endTolerance, m_sampleInterval);

        if (!evaluator.Begin(points[0])) return;

        Vector2 start = points[0];
        Vector2 end = points[points.Count - 1];

        Vector2 p50 = Vector2.Lerp(start, end, 0.5f);
        Vector2 p40 = Vector2.Lerp(start, end, 0.4f);

        evaluator.EvaluateSegment(start, p50);
        Debug.Log($"50% : Progress={evaluator.Progress:P1}");

        evaluator.EvaluateSegment(p50, p40);
        Debug.Log($"Back to 40% : Progress={evaluator.Progress:P1}");

        RUNE_REALTIME_RESULT result = evaluator.EvaluateSegment(p40, end);
        Debug.Log($"Final : {result}, Progress={evaluator.Progress:P1}");
    }
}
