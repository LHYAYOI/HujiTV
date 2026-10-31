using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class RuneEditorEditingValidation
{
    [MenuItem("Tools/YasudaHaruto/Verify Rune Editing")]
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

    private static object Get(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static object Call(object target, string name, params object[] arguments) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Debug.Log("Rune Editing PASS: " + message);
    }
}
