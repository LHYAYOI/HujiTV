using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace YasudaHaruto.BookValidation.Editor
{
[InitializeOnLoad]
public static class BookObstacleValidation
{
    private const string Request = "Temp/ObstacleValidation/request";
    static BookObstacleValidation()
    {
        if (File.Exists(Request)) EditorApplication.delayCall += () => { File.Delete(Request); Run(); };
    }
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    private static void Set(object target, string field, object value) => target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    [MenuItem("Tools/YasudaHaruto/Verify Book Obstacles")]
    public static void Run()
    {
        var rub = ScriptableObject.CreateInstance<ObstacleData>();
        var ink = ScriptableObject.CreateInstance<ObstacleData>();
        var all = ScriptableObject.CreateInstance<ObstacleData>();
        var texture = new Texture2D(64, 64);
        try
        {
            Set(ink, "m_slotCount", 2);
            Set(ink, "m_removalType", OBSTACLE_REMOVAL_TYPE.SCRATCH);
            Set(all, "m_slotCount", 4);
            var state = new PageObstacleState();
            state.Apply(rub);
            var first = state[0];
            state.Apply(ink);
            Check(ReferenceEquals(first, state[0]) && state[1] != null && state[2] != null && state[3] == null, "1 + 2 slot allocation");
            Check(!ReferenceEquals(state[1].Interaction, state[2].Interaction), "Independent removal state");
            var third = state[2];
            Check(state.Clear(1, state[1]) && state[1] == null && ReferenceEquals(third, state[2]), "No compaction");
            state.Apply(ink);
            Check(ReferenceEquals(first, state[0]) && ReferenceEquals(third, state[2]) && state.BlocksActions, "Fill empty slots before overwrite");
            state.Apply(rub);
            Check(!ReferenceEquals(first, state[0]) && ReferenceEquals(third, state[2]), "Overwrite oldest only");
            Check(!state.Clear(0, first), "Stale removal cannot clear replacement");
            Check(state.Clear(0, state[0]) && state.BlocksActions, "Right slot removal does not unlock");
            Check(state.Clear(3, state[3]) && !state.BlocksActions && state[2] != null, "Fourth slot removal unlocks");
            state.Apply(all);
            for (int i = 0; i < 4; i++) Check(state[i].Data == all && state[i].Interaction.Progress == 0, "Replace all and reset");
            var captured = state[0];
            var otherPage = new PageObstacleState();
            otherPage.Apply(rub);
            Check(ReferenceEquals(captured, state[0]), "Spread states remain independent");

            var rubbing = new RubObstacleInteraction(rub);
            rubbing.Begin(new Vector2(0.2f, 0.5f));
            rubbing.Move(new Vector2(0.5f, 0.5f));
            float progress = rubbing.Progress;
            rubbing.Move(new Vector2(0.8f, 0.5f));
            rubbing.Move(new Vector2(0.79f, 0.5f));
            Check(rubbing.Progress == progress, "Same direction and jitter must not add passes");
            rubbing.Begin(new Vector2(0.2f, 0.5f));
            rubbing.Move(new Vector2(0.8f, 0.5f));
            Check(rubbing.Progress == progress, "Repeated same direction across strokes must not add passes");
            rubbing.Move(new Vector2(0.2f, 0.5f));
            Check(rubbing.Progress > progress, "Reverse pass advances");

            var scratch = new ScratchObstacleInteraction(ink);
            scratch.Begin(new Vector2(0.1f, 0.5f));
            scratch.Move(new Vector2(0.9f, 0.5f));
            float scratched = scratch.Progress;
            Check(scratched > 0 && scratch.Coverage(32, 32) == 0, "Fast scratch fills segment");
            scratch.Move(new Vector2(0.1f, 0.5f));
            Check(scratch.Progress == scratched, "Already erased cells do not count twice");
            scratch.Cancel();
            Check(scratch.Progress == scratched && scratch.Coverage(32, 32) == 0, "Cancel retains scratch shape");
            for (int y = 0; y <= 10; y++)
            { scratch.Begin(new Vector2(0, y / 10f)); scratch.Move(new Vector2(1, y / 10f)); }
            Check(scratch.Complete, "Full scratch completes");
            var colors = new Color[4096];
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.clear;
            colors[32 * 64 + 32] = Color.white;
            texture.SetPixels(colors); texture.Apply();
            Set(ink, "m_texture", texture);
            var sparse = new ScratchObstacleInteraction(ink);
            sparse.Begin(new Vector2(0.5f, 0.5f));
            Check(sparse.Complete, "Transparent padding excluded from required area");
            Check((byte)BOOK_MESSAGE_TYPE.APPLY_OBSTACLE == 6 && (byte)BOOK_MESSAGE_TYPE.CURSOR_POSITION == 5, "Existing wire values preserved");
            Directory.CreateDirectory("Temp/ObstacleValidation");
            File.WriteAllText("Temp/ObstacleValidation/result.txt", "BookObstacle COMPLETE: allocation, independent clearing, oldest replacement, stale removal, fourth-slot lock, spread isolation, rub reversal, scratch segment/dedup/persistence/alpha, protocol IDs.");
            Debug.Log("BookObstacle COMPLETE");
        }
        catch (Exception exception)
        {
            Directory.CreateDirectory("Temp/ObstacleValidation");
            File.WriteAllText("Temp/ObstacleValidation/result.txt", exception.ToString());
            Debug.LogException(exception);
        }
        finally
        { UnityEngine.Object.DestroyImmediate(rub); UnityEngine.Object.DestroyImmediate(ink); UnityEngine.Object.DestroyImmediate(all); UnityEngine.Object.DestroyImmediate(texture); }
    }

    [MenuItem("Tools/YasudaHaruto/Preview Obstacle/Rub (Play Mode)")]
    private static void PreviewRub() => Preview(1);
    [MenuItem("Tools/YasudaHaruto/Preview Obstacle/Ink (Play Mode)")]
    private static void PreviewInk() => Preview(2);
    [MenuItem("Tools/YasudaHaruto/Preview Obstacle/Fill Four Slots (Play Mode)")]
    private static void PreviewFull() => Preview(1, 4);
    private static void Preview(byte id, int repeat = 1)
    {
        if (!Application.isPlaying) { Debug.LogWarning("Enter Play Mode and open a normal spread first."); return; }
        var controller = UnityEngine.Object.FindFirstObjectByType<BookObstacleController>();
        var game = UnityEngine.Object.FindFirstObjectByType<BookGameController>();
        if (controller == null || game == null || game.BookView.IsBusy || game.BookView.FlipView.IsTurning) return;
        if (game.BookView.IsClosed) game.BookView.OpenBook(1);
        else if (game.CurrentPage == null) game.BookView.ShowSpread(1);
        double deadline = EditorApplication.timeSinceStartup + 10;
        void ApplyWhenReady()
        {
            if (!Application.isPlaying || controller == null || game == null || EditorApplication.timeSinceStartup > deadline)
            { EditorApplication.update -= ApplyWhenReady; return; }
            if (game.CurrentPage == null || game.BookView.IsBusy || game.BookView.FlipView.IsTurning) return;
            EditorApplication.update -= ApplyWhenReady;
            foreach (var data in Resources.LoadAll<ObstacleData>("BookObstacles"))
                if (data.ObstacleId == id)
                    for (int i = 0; i < repeat; i++) typeof(BookObstacleController).GetMethod("Apply", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(controller, new object[] { data });
        }
        EditorApplication.update += ApplyWhenReady;
    }
}
}
