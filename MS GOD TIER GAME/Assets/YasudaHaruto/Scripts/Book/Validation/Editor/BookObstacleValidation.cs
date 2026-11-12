using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

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

    public static void Run()
    {
        var data = ScriptableObject.CreateInstance<ObstacleData>();
        GameObject graphicObject = null;
        Texture2D decoded = null;
        try
        {
            var state = new PageObstacleState();
            state.Apply(data);
            var first = state[0];
            state.Apply(data, 2);
            Check(ReferenceEquals(first, state[0]) && state[1] != null && state[2] != null && state[3] == null, "1 + 2 slot allocation");
            state[1].Begin(Vector2.zero); state[1].Move(new Vector2(0.25f, 0));
            Check(state[1].Progress > 0 && state[2].Progress == 0, "Independent progress");
            var third = state[2];
            Check(state.Clear(1, state[1]) && ReferenceEquals(third, state[2]), "No compaction");
            state.Apply(data, 2);
            Check(ReferenceEquals(first, state[0]) && ReferenceEquals(third, state[2]) && state.BlocksActions, "Empty slots first");
            state.Apply(data);
            Check(!ReferenceEquals(first, state[0]) && ReferenceEquals(third, state[2]), "Oldest overwrite");
            Check(!state.Clear(0, first), "Stale clear rejected");
            Check(state.Clear(0, state[0]) && state.BlocksActions, "Right slot does not unlock");
            Check(state.Clear(3, state[3]) && !state.BlocksActions && state[2] != null, "Fourth slot unlocks");
            state.Apply(data, 4);
            for (int i = 0; i < 4; i++) Check(state[i].Progress == 0, "Full replacement resets progress");
            bool rejected = false;
            try { state.Apply(data, 5); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "Invalid counts rejected");

            var horizontal = new ObstacleInstance(data, 1);
            var vertical = new ObstacleInstance(data, 2);
            var diagonal = new ObstacleInstance(data, 3);
            horizontal.Begin(Vector2.zero); horizontal.Move(new Vector2(0.5f, 0));
            vertical.Begin(Vector2.zero); vertical.Move(new Vector2(0, 0.5f));
            diagonal.Begin(Vector2.zero); diagonal.Move(new Vector2(0.3f, 0.4f));
            Check(Mathf.Abs(horizontal.Progress - 0.125f) < 0.0001f &&
                Mathf.Abs(vertical.Progress - horizontal.Progress) < 0.0001f &&
                Mathf.Abs(diagonal.Progress - horizontal.Progress) < 0.0001f, "Distance equal in all directions");
            var sampled = new ObstacleInstance(data, 4);
            sampled.Begin(Vector2.zero);
            for (int i = 1; i <= 10; i++) sampled.Move(new Vector2(i * 0.05f, 0));
            Check(Mathf.Abs(sampled.Progress - horizontal.Progress) < 0.0001f, "Straight movement independent of event count");
            var serialized = new SerializedObject(data);
            serialized.FindProperty("m_requiredDistance").floatValue = 2f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var faster = new ObstacleInstance(data, 6);
            faster.Begin(Vector2.zero); faster.Move(new Vector2(0.5f, 0));
            Check(Mathf.Abs(faster.Progress - 0.25f) < 0.0001f, "Halving configured distance doubles cleaning per movement");
            serialized.Update(); serialized.FindProperty("m_requiredDistance").floatValue = 4f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            float before = horizontal.Progress;
            horizontal.Cancel(); horizontal.Move(Vector2.one);
            Check(horizontal.Progress == before, "Cancelled stroke retains progress and ignores movement");
            horizontal.Begin(Vector2.one);
            Check(horizontal.Progress == before, "Repress does not count gap");
            horizontal.Move(new Vector2(1.01f, 1));
            Check(horizontal.Progress > before && horizontal.Progress < before + 0.01f, "Small movement fades continuously");
            var jitter = new ObstacleInstance(data, 5);
            jitter.Begin(Vector2.zero);
            for (int i = 0; i < 100; i++) jitter.Move(new Vector2(i % 2 == 0 ? 0.001f : -0.001f, 0));
            Check(jitter.Progress == 0, "Stationary jitter ignored");
            horizontal.Move(new Vector2(20, 1));
            Check(horizontal.Complete && horizontal.Progress == 1f, "Progress clamps at completion");

            graphicObject = new GameObject("Dirt mesh validation", typeof(RectTransform), typeof(CanvasRenderer), typeof(ObstaclePatchGraphic));
            var graphic = graphicObject.GetComponent<ObstaclePatchGraphic>();
            graphic.Bind(vertical);
            using (var mesh = new VertexHelper())
            {
                typeof(ObstaclePatchGraphic).GetMethod("OnPopulateMesh",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly).Invoke(graphic, new object[] { mesh });
                Check(mesh.currentVertCount == 4, "Single quad instead of scratch grid");
                var vertex = new UIVertex(); mesh.PopulateUIVertex(ref vertex, 0);
                Check(Mathf.Abs(vertex.color.a / 255f - (1 - vertical.Progress)) < 0.005f, "Alpha follows cleaning distance");
            }
            var settings = Resources.Load<ObstacleData>("BookObstacles/Rub");
            ObstacleMaskBaker.Bake(settings);
            Check(settings != null && settings.HasStageImages, "Production settings contain all four stage images");
            var appearanceState = new PageObstacleState(); appearanceState.Apply(settings, 4);
            for (int slot = 0; slot < 4; slot++)
            {
                Rect placement = settings.GetSpreadPlacement(slot);
                Check(placement.width > 0.4f && placement.height > 0.25f, "Serialized stage placement has valid dimensions");
                string imagePath = AssetDatabase.GetAssetPath(settings.GetTexture(slot));
                Check(imagePath.EndsWith("/Dirt/Ink" + (slot == 3 ? 6 : slot + 1) + ".png"), "Stage selects corresponding authored image");
                Check(appearanceState[slot].Slot == slot && appearanceState[slot].Texture == settings.GetTexture(slot), "Allocated slot retains its own appearance");
                var importer = (TextureImporter)AssetImporter.GetAtPath(imagePath);
                Check(importer != null && importer.alphaIsTransparency && !importer.isReadable, "Alpha enabled without CPU texture readback");
                decoded = new Texture2D(2, 2);
                Check(decoded.LoadImage(File.ReadAllBytes(imagePath)), "Stage PNG decodes");
                bool opaque = false;
                for (int y = 0; y < decoded.height; y += Mathf.Max(1, decoded.height / 32))
                    for (int x = 0; x < decoded.width; x += Mathf.Max(1, decoded.width / 32))
                        opaque |= decoded.GetPixel(x, y).a > 0.8f;
                Check(decoded.GetPixel(0, 0).a < 0.01f && opaque, "Stage image has transparency and visible paint");
                UnityEngine.Object.DestroyImmediate(decoded); decoded = null;
            }
            Check(settings.ExtraPartCount == 4, "Four split splashes belong to the fourth stage");
            for (int slot = 0; slot < 4; slot++)
                for (int part = 0; part <= (slot == 3 ? settings.ExtraPartCount : 0); part++)
                {
                    Texture2D texture = settings.GetPartTexture(slot, part);
                    bool ink = false, empty = false;
                    for (int y = 1; y < 32; y++) for (int x = 1; x < 32; x++)
                    {
                        bool hit = settings.ContainsInk(texture, new Vector2(x / 32f, y / 32f));
                        ink |= hit; empty |= !hit;
                    }
                    Check(ink && empty, "Each split image has opaque and transparent hit-test regions");
                }
            graphic.Bind(appearanceState[3], new Rect(0.5f, 0f, 0.5f, 1f));
            using (var mesh = new VertexHelper())
            {
                typeof(ObstaclePatchGraphic).GetMethod("OnPopulateMesh", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly).Invoke(graphic, new object[] { mesh });
                var vertex = new UIVertex(); mesh.PopulateUIVertex(ref vertex, 0);
                Check(Mathf.Abs(vertex.uv0.x - 0.5f) < 0.0001f, "Right-page fragment samples the right half of the spread image");
            }
            Check((byte)BOOK_MESSAGE_TYPE.APPLY_OBSTACLE == 6 && (byte)BOOK_MESSAGE_TYPE.APPLY_DIRT == 10, "Legacy wire ID retained; new count message distinct");
            Directory.CreateDirectory("Temp/ObstacleValidation");
            File.WriteAllText("Temp/ObstacleValidation/result.txt", "BookObstacle COMPLETE: four slots, independent progress, configurable cleaning distance, four stage textures, split image UVs, overwrite, arbitrary-direction distance, jitter, cancellation, continuous alpha, protocol IDs.");
            Debug.Log("BookObstacle COMPLETE");
        }
        catch (Exception exception)
        {
            Directory.CreateDirectory("Temp/ObstacleValidation");
            File.WriteAllText("Temp/ObstacleValidation/result.txt", exception.ToString());
            Debug.LogException(exception);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(data);
            if (graphicObject != null) UnityEngine.Object.DestroyImmediate(graphicObject);
            if (decoded != null) UnityEngine.Object.DestroyImmediate(decoded);
        }
    }

    public static void Preview(byte slotCount)
    {
        if (slotCount < 1 || slotCount > PageObstacleState.SLOT_COUNT) return;
        if (!Application.isPlaying) { Debug.LogWarning("Enter Play Mode first."); return; }
        var bootstrap = UnityEngine.Object.FindFirstObjectByType<BookGameBootstrap>();
        var controller = bootstrap != null ? bootstrap.ObstacleController : null;
        var game = bootstrap != null ? bootstrap.GameController : null;
        if (controller == null || game == null || !game.IsInitialized || game.BookView == null)
        { Debug.LogWarning("本の初期化またはGame Controller参照を確認してください。"); return; }
        if (game.BookView.IsBusy || game.BookView.FlipView.IsTurning)
        { Debug.LogWarning("本のめくりが完了してから追加してください。"); return; }
        if (game.BookView.IsClosed) game.BookView.OpenBook(1);
        else if (game.CurrentPage == null) game.BookView.ShowSpread(1);
        double deadline = EditorApplication.timeSinceStartup + 10;
        void ApplyWhenReady()
        {
            if (!Application.isPlaying || controller == null || game == null || EditorApplication.timeSinceStartup > deadline)
            { EditorApplication.update -= ApplyWhenReady; return; }
            if (game.CurrentPage == null || game.BookView.IsBusy || game.BookView.FlipView.IsTurning) return;
            EditorApplication.update -= ApplyWhenReady;
            typeof(BookObstacleController).GetMethod("Apply", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(controller, new object[] { slotCount });
        }
        EditorApplication.update += ApplyWhenReady;
    }
}
}
