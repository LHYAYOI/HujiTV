using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class RuneTraceToleranceValidation
{
    private const string Report = "Temp/RuneTraceToleranceValidation/result.txt";

    static RuneTraceToleranceValidation()
    {
        const string request = "Temp/RuneTraceToleranceValidation/runtime-request";
        if (!File.Exists(request)) return;
        EditorApplication.delayCall += () =>
        {
            File.Delete(request);
            File.WriteAllText(Report, "Ordered trace tolerance regression\n");
            try
            {
                Verify();
                YasudaHaruto.BookValidation.Editor.RuneFeedbackValidation.Run();
                File.AppendAllText(Report, "PASS: Existing feedback/controller regressions\nRune Trace Tolerance COMPLETE\n");
            }
            catch (Exception exception)
            {
                File.AppendAllText(Report, "FAIL: " + exception + "\n");
                Debug.LogException(exception);
            }
        };
    }

    public static void Verify()
    {
        var line = new[] { new Vector2(0, 0), new Vector2(1, 0) };
        var trace = Create(line, 0.05f, 0.2f, 0.1f);
        Require(trace.Begin(new Vector2(0.19f, 0)), "Start tolerance accepts a start farther along the first segment");
        Require(trace.EvaluateSegment(new Vector2(0.19f, 0), new Vector2(0.3f, 0)) != RUNE_REALTIME_RESULT.FAILED,
            "Accepted start does not immediately fail because progress was zero");
        Require(trace.EvaluateSegment(new Vector2(0.3f, 0), new Vector2(0.94f, 0)) == RUNE_REALTIME_RESULT.COMPLETED,
            "End tolerance completes before the exact endpoint");

        trace = Create(line);
        Require(trace.Begin(line[0]), "Exact start is accepted");
        Require(trace.EvaluateSegment(line[0], new Vector2(0.5f, 0)) == RUNE_REALTIME_RESULT.IN_PROGRESS,
            "Middle of the path cannot complete");
        Require(trace.EvaluateSegment(new Vector2(0.5f, 0), new Vector2(0.48f, 0.01f)) != RUNE_REALTIME_RESULT.FAILED,
            "Small backward hand motion remains valid");
        Require(trace.EvaluateSegment(new Vector2(0.48f, 0.01f), new Vector2(0.7f, 0.08f)) == RUNE_REALTIME_RESULT.FAILED,
            "Leaving the actual distance tolerance still fails immediately");

        var corner = new[] { Vector2.zero, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f) };
        Require(Trace(corner, new[] { Vector2.zero, new Vector2(0.46f, 0), new Vector2(0.5f, 0.04f), corner[2] }) == RUNE_REALTIME_RESULT.COMPLETED,
            "A small inside-corner shortcut within tolerance completes");
        Require(Trace(corner, new[] { Vector2.zero, corner[2] }) == RUNE_REALTIME_RESULT.FAILED,
            "A coarse input segment cutting a large corner fails");

        // Both branches meet at the center, but the upper loop must still be visited.
        var crossing = new[] { new Vector2(-0.5f, 0), Vector2.zero, new Vector2(0.5f, 0.5f),
            new Vector2(-0.5f, 0.5f), Vector2.zero, new Vector2(0.5f, 0) };
        Require(Trace(crossing, new[] { crossing[0], Vector2.zero, crossing[5] }) == RUNE_REALTIME_RESULT.FAILED,
            "Crossing cannot jump directly to the later branch");
        Require(Trace(crossing, crossing) == RUNE_REALTIME_RESULT.COMPLETED, "Following the full crossing route still completes");

        var hairpin = new[] { Vector2.zero, new Vector2(0.8f, 0), new Vector2(0.8f, 0.04f),
            new Vector2(0, 0.04f) };
        trace = Create(hairpin);
        Require(trace.Begin(hairpin[0]), "Hairpin start is accepted");
        Require(trace.EvaluateSegment(hairpin[0], hairpin[3]) != RUNE_REALTIME_RESULT.COMPLETED,
            "A nearby endpoint cannot bypass the long hairpin");
        Require(Trace(hairpin, hairpin) == RUNE_REALTIME_RESULT.COMPLETED, "Full nearby parallel branches complete in order");

        trace = Create(line);
        trace.Begin(line[0]);
        for (int i = 0; i < 50; i++) trace.EvaluateSegment(line[0], line[0]);
        Require(trace.Progress < 0.001f && !trace.IsCompleted, "Repeated stationary samples do not advance through the route");
        trace.Reset();
        Require(!trace.IsCompleted && !trace.HasFailed && trace.Progress == 0f, "Reset clears coverage and completion");

        var duplicate = new[] { Vector2.zero, Vector2.zero, new Vector2(0.5f, 0), new Vector2(0.5f, 0) };
        Require(Trace(duplicate, new[] { Vector2.zero, new Vector2(0.5f, 0) }) == RUNE_REALTIME_RESULT.COMPLETED,
            "Duplicate reference vertices do not stall coverage");

        RuneData fire = AssetDatabase.LoadAssetAtPath<RuneData>("Assets/YasudaHaruto/Data/Runes/FireRune.asset");
        Require(fire != null && fire.TraceData.Strokes.Count > 0, "Current FireRune can be tested without modifying it");
        var reference = fire.TraceData.Strokes[0].Points;
        Require(Trace(reference, reference, fire.Rule.DistanceTolerance, fire.Rule.StartTolerance, fire.Rule.EndTolerance)
            == RUNE_REALTIME_RESULT.COMPLETED, "Current FireRune exact route completes");
        foreach (float trim in new[] { 0.04f, 0.08f, 0.12f })
        {
            var author = fire.AuthoringData.Strokes[0].Points;
            var input = new List<Vector2> { author[0] };
            for (int i = 1; i + 1 < author.Count; i++)
            {
                input.Add(Vector2.Lerp(author[i], author[i - 1], Mathf.Min(0.45f, trim / Vector2.Distance(author[i], author[i - 1]))));
                input.Add(Vector2.Lerp(author[i], author[i + 1], Mathf.Min(0.45f, trim / Vector2.Distance(author[i], author[i + 1]))));
            }
            input.Add(author[author.Count - 1]);
            Require(Trace(reference, input, fire.Rule.DistanceTolerance, fire.Rule.StartTolerance, fire.Rule.EndTolerance)
                == RUNE_REALTIME_RESULT.COMPLETED, "Current FireRune tolerates rounded corners, trim=" + trim);
        }
    }

    private static RuneTraceRealtimeEvaluator Create(IReadOnlyList<Vector2> reference,
        float distance = 0.05f, float start = 0.02f, float end = 0.025f)
        => new RuneTraceRealtimeEvaluator(reference, distance, start, end, 0.005f);

    private static RUNE_REALTIME_RESULT Trace(IReadOnlyList<Vector2> reference, IReadOnlyList<Vector2> input,
        float distance = 0.05f, float start = 0.02f, float end = 0.025f)
    {
        var trace = Create(reference, distance, start, end);
        if (!trace.Begin(input[0])) return RUNE_REALTIME_RESULT.FAILED;
        RUNE_REALTIME_RESULT result = RUNE_REALTIME_RESULT.IN_PROGRESS;
        for (int i = 1; i < input.Count; i++)
        {
            result = trace.EvaluateSegment(input[i - 1], input[i]);
            if (result != RUNE_REALTIME_RESULT.IN_PROGRESS) return result;
        }
        return result;
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        if (File.Exists(Report)) File.AppendAllText(Report, "PASS: " + message + "\n");
    }
}
