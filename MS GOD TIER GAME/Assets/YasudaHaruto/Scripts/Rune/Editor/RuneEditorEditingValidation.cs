using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class RuneEditorEditingValidation
{
    private const string Report = "Temp/RuneEditorValidation/result.txt";
    static RuneEditorEditingValidation()
    {
        const string request = "Temp/RuneEditorValidation/runtime-request";
        if (!File.Exists(request)) return;
        EditorApplication.delayCall += () =>
        {
            File.Delete(request);
            File.WriteAllText(Report, "Rune editor regression checks\n");
            try { Verify(); File.AppendAllText(Report, "Rune Editing COMPLETE\n"); }
            catch (Exception exception) { File.AppendAllText(Report, "FAIL: " + exception); Debug.LogException(exception); }
        };
    }
    public static void Verify()
    {
        RuneData rune = ScriptableObject.CreateInstance<RuneData>();
        RuneEditorWindow window = ScriptableObject.CreateInstance<RuneEditorWindow>();
        Texture2D source = new Texture2D(2, 2);
        try
        {
            Set(window, "m_runeData", rune);
            Set(window, "m_sourceRuneImage", source);
            Set(window, "m_drawingRect", new Rect(10f, 20f, 400f, 400f));
            var first = new RuneStrokeData(new[] { new Vector2(0.1f, 0.2f), new Vector2(0.9f, 0.2f) });
            var second = new RuneStrokeData(new[] { new Vector2(0.1f, 0.8f), new Vector2(0.9f, 0.8f) });
            rune.AuthoringData.AddStroke(first);
            rune.AuthoringData.AddStroke(second);
            rune.TraceData.SetStrokes(new List<RuneStrokeData> { first, second });
            Require((int)Call(window, "FindStroke", new Vector2(210f, 340f)) == 0, "Canvas selects the nearest saved stroke");
            Require((int)Call(window, "FindStroke", new Vector2(210f, 100f)) == 1, "Canvas selects the other stroke");
            Require((int)Call(window, "FindStroke", new Vector2(210f, 220f)) == -1, "Empty canvas does not select a distant stroke");
            Set(window, "m_selectedStrokeIndex", 0);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Call(window, "DeleteSelectedStroke");
            Undo.FlushUndoRecordObjects();
            Require(rune.AuthoringData.Strokes.Count == 1 && rune.AuthoringData.Strokes[0].Points[0] == second.Points[0], "Delete only removes the selected stroke");
            Require(rune.TraceData.Strokes.Count == 0, "Delete invalidates baked geometry");
            Require((int)Get(window, "m_selectedStrokeIndex") == -1, "Delete clears selection");
            Undo.CollapseUndoOperations(group);
            Undo.PerformUndo();
            Require(rune.AuthoringData.Strokes.Count == 2 && rune.TraceData.Strokes.Count == 2, "Undo restores source and baked geometry");
            Undo.PerformRedo();
            Require(rune.AuthoringData.Strokes.Count == 1 && rune.TraceData.Strokes.Count == 0, "Redo reapplies deletion");
            Call(window, "DeleteSelectedStroke");
            Require(rune.AuthoringData.Strokes.Count == 1, "Invalid selection cannot remove another stroke");

            Call(window, "StartStroke", new Vector2(50f, 60f));
            Call(window, "AddPoint", new Vector2(350f, 380f));
            Call(window, "FinishStroke");
            Require(rune.AuthoringData.Strokes.Count == 2, "Freehand drawing adds one stroke");
            Require(rune.AuthoringData.Strokes[1].Points[0] == new Vector2(0.1f, 0.9f), "Freehand matches the image's normalized coordinates");
            Require(rune.TraceData.Strokes.Count == 0, "Freehand requires a new bake");
            Call(window, "StartStroke", new Vector2(60f, 70f));
            Call(window, "CancelCanvasInput");
            Call(window, "FinishStroke");
            Require(rune.AuthoringData.Strokes.Count == 2, "Cancelling does not save a partial stroke");
            Require((Texture2D)Get(window, "m_sourceRuneImage") == source, "Editing preserves the background image reference");
            VerifyPoints(window, rune);
            Debug.Log("Rune Editing Validation COMPLETE");
        }
        finally
        {
            Undo.ClearUndo(rune);
            UnityEngine.Object.DestroyImmediate(window);
            UnityEngine.Object.DestroyImmediate(rune);
            UnityEngine.Object.DestroyImmediate(source);
        }
    }

    private static void VerifyPoints(RuneEditorWindow window, RuneData rune)
    {
        rune.AuthoringData.Clear();
        Set(window, "m_snapPoints", true);
        Set(window, "m_gridDivisions", 10);
        Call(window, "AddPlacedPoint", new Vector2(53f, 377f));
        Call(window, "AddPlacedPoint", new Vector2(213f, 377f));
        Call(window, "AddPlacedPoint", new Vector2(213f, 177f));
        Call(window, "AddPlacedPoint", new Vector2(213f, 177f));
        Require(((List<Vector2>)Get(window, "m_currentStroke")).Count == 3, "Grid placement ignores duplicate consecutive clicks");
        Call(window, "FinishPointStroke");
        Require(rune.AuthoringData.Strokes.Count == 1 && rune.AuthoringData.Strokes[0].Points.Count == 3,
            "Point placement saves one connected three-corner stroke");
        Require(rune.AuthoringData.Strokes[0].PreserveCorners, "Point-authored strokes explicitly retain their corners");
        var points = rune.AuthoringData.Strokes[0].Points;
        Require(Vector2.Distance(points[0], new Vector2(0.1f, 0.1f)) < 0.00001f &&
            Vector2.Distance(points[1], new Vector2(0.5f, 0.1f)) < 0.00001f &&
            Vector2.Distance(points[2], new Vector2(0.5f, 0.6f)) < 0.00001f,
            "Point placement snaps and uses the same image coordinates as freehand");

        var baked = RuneBaker.Bake(rune.AuthoringData, 0.037f);
        Require(baked.Count == 1 && Contains(baked[0].Points, points[1]), "Bake retains the exact authored corner at a non-divisible sample interval");
        Require(Mathf.Abs(RunePathUtility.CalculatePathLength(baked[0].Points) - 0.9f) < 0.00001f,
            "Bake does not cut diagonally across the corner");
        var evaluator = new RuneTraceRealtimeEvaluator(baked[0].Points, 0.03f, 0.03f, 0.03f, 0.01f);
        Require(evaluator.Begin(points[0]), "Placed stroke starts in the existing runtime evaluator");
        RUNE_REALTIME_RESULT result = RUNE_REALTIME_RESULT.IN_PROGRESS;
        for (int i = 1; i < baked[0].Points.Count; i++) result = evaluator.EvaluateSegment(baked[0].Points[i - 1], baked[0].Points[i]);
        Require(result == RUNE_REALTIME_RESULT.COMPLETED, "Placed and baked stroke completes through the existing trace evaluator");

        var legacy = new RuneAuthoringData();
        legacy.AddStroke(new RuneStrokeData(points));
        var legacyBake = RuneBaker.Bake(legacy, 0.037f)[0].Points;
        var expectedLegacy = RunePathUtility.Resample(points, 0.037f);
        Require(legacyBake.Count == expectedLegacy.Count, "Existing freehand and imported strokes keep the original bake density");
        for (int i = 0; i < legacyBake.Count; i++)
            if (legacyBake[i] != expectedLegacy[i]) throw new InvalidOperationException("Legacy bake geometry changed");
        var split = new RuneAuthoringData();
        split.AddStroke(new RuneStrokeData(points, true));
        Require(split.SplitStroke(0, 1) && split.Strokes[0].PreserveCorners && split.Strokes[1].PreserveCorners,
            "Splitting point strokes retains corner metadata");
        Require(split.MergeStrokes(0, 1, 0.001f) && split.Strokes[0].PreserveCorners,
            "Merging point strokes retains corner metadata");

        rune.TraceData.SetStrokes(baked);
        Set(window, "m_selectedStrokeIndex", 0);
        Set(window, "m_selectedPointIndex", 1);
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Call(window, "MoveSelectedPoint", new Vector2(253f, 337f));
        Undo.FlushUndoRecordObjects();
        Require(Vector2.Distance(rune.AuthoringData.Strokes[0].Points[1], new Vector2(0.6f, 0.2f)) < 0.00001f,
            "Moving a selected corner applies grid snapping");
        Require(rune.TraceData.Strokes.Count == 0, "Moving a corner invalidates baked data");
        Undo.CollapseUndoOperations(group);
        Undo.PerformUndo();
        Require(Vector2.Distance(rune.AuthoringData.Strokes[0].Points[1], new Vector2(0.5f, 0.1f)) < 0.00001f &&
            rune.TraceData.Strokes.Count == 1, "Undo restores the corner and its previous baked data");
        Undo.PerformRedo();
        Require(Vector2.Distance(rune.AuthoringData.Strokes[0].Points[1], new Vector2(0.6f, 0.2f)) < 0.00001f,
            "Redo reapplies the corner move");

        Call(window, "InsertPoint", new Vector2(150f, 360f));
        Require(rune.AuthoringData.Strokes[0].Points.Count == 4, "Line insertion adds one editable vertex");
        var inserted = rune.AuthoringData.Strokes[0].Points;
        Require(RunePathUtility.DistancePointToSegment(inserted[1], inserted[0], inserted[2]) < 0.00001f,
            "Inserted vertex preserves the original segment geometry");
        Call(window, "DeleteSelectedPoint");
        Require(rune.AuthoringData.Strokes[0].Points.Count == 3, "Delete removes only the selected vertex");
        Set(window, "m_selectedPointIndex", 1);
        Call(window, "DeleteSelectedPoint");
        Require(rune.AuthoringData.Strokes[0].Points.Count == 2, "Two endpoints remain a valid straight stroke");
        Set(window, "m_selectedPointIndex", 0);
        Call(window, "DeleteSelectedPoint");
        Require(rune.AuthoringData.Strokes.Count == 0, "Deleting below two vertices removes the invalid stroke");

        Call(window, "AddPlacedPoint", new Vector2(50f, 60f));
        Call(window, "CancelCanvasInput");
        Call(window, "FinishPointStroke");
        Require(rune.AuthoringData.Strokes.Count == 0, "Cancelling a point draft cannot save it");
    }

    private static bool Contains(IReadOnlyList<Vector2> points, Vector2 expected)
    {
        foreach (Vector2 point in points) if ((point - expected).sqrMagnitude < 0.0000000001f) return true;
        return false;
    }

    private static object Get(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static object Call(object target, string name, params object[] arguments) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Debug.Log("Rune Editing PASS: " + message);
        if (File.Exists(Report)) File.AppendAllText(Report, "PASS: " + message + "\n");
    }
}
