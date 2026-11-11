using System;
using System.IO;
using System.Reflection;
using Unity.Collections;
using Unity.Networking.Transport;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace YasudaHaruto.BookValidation.Editor
{
[InitializeOnLoad]
public static class BookObstacleRuntimeValidation
{
    private const string Running = "Yasuda.ObstacleRuntimeValidation";
    private const string Batch = "Yasuda.ObstacleRuntimeValidation.Batch";
    private const string Report = "Temp/ObstacleValidation/runtime-result.txt";
    private static int s_step;
    private static double s_next, s_deadline;
    private static BookGameController s_game;
    private static BookObstacleController s_obstacles;
    private static BookGameDragInput s_input;
    private static RuneTraceController s_rune;
    private static PageInstance s_page;
    private static GameObject s_packetObject;
    private static BookNetworkClient s_packetClient;
    private static ObstacleInstance s_saved;
    private static uint s_event;
    private static bool s_debugChecked;
    private static int ActiveStains(BookPageView page) => page.GetComponentsInChildren<ObstaclePatchGraphic>().Length;
    private static void CheckCapturedInk(Camera camera, ObstaclePatchGraphic graphic, RenderTexture texture)
    {
        Canvas.ForceUpdateCanvases();
        camera.Render();
        Vector3 viewport = camera.WorldToViewportPoint(graphic.rectTransform.TransformPoint(graphic.rectTransform.rect.center));
        var pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            RenderTexture.active = texture;
            pixel.ReadPixels(new Rect(Mathf.Clamp((int)(viewport.x * texture.width), 0, texture.width - 1),
                Mathf.Clamp((int)(viewport.y * texture.height), 0, texture.height - 1), 1, 1), 0, 0);
            pixel.Apply();
            Color ink = pixel.GetPixel(0, 0);
            Require(ink.r < 0.2f && ink.g < 0.2f && ink.b < 0.2f, "Opaque ink center exists in captured RenderTexture");
        }
        finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(pixel); }
    }
    static BookObstacleRuntimeValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (!SessionState.GetBool(Running, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                s_step = 0; s_event = 0; s_debugChecked = false;
                s_next = EditorApplication.timeSinceStartup + 2;
                s_deadline = EditorApplication.timeSinceStartup + 90;
                EditorApplication.update += Tick;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Running, false); EditorApplication.update -= Tick;
                if (SessionState.GetBool(Batch, false))
                {
                    SessionState.SetBool(Batch, false);
                    EditorApplication.Exit(File.Exists(Report) && File.ReadAllText(Report).Contains("BookObstacleRuntime COMPLETE") ? 0 : 1);
                }
            }
        };
        if (File.Exists("Temp/ObstacleValidation/runtime-request")) EditorApplication.delayCall += () =>
        { File.Delete("Temp/ObstacleValidation/runtime-request"); Run(); };
    }
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        Directory.CreateDirectory("Temp/ObstacleValidation");
        File.WriteAllText(Report, "Current scene test; no scene save or replacement.\n");
        SessionState.SetBool(Running, true);
        EditorApplication.EnterPlaymode();
    }
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Batch mode required.");
        BookObstacleValidation.Run();
        if (!File.ReadAllText("Temp/ObstacleValidation/result.txt").Contains("BookObstacle COMPLETE"))
            throw new InvalidOperationException("Core dirt validation failed; see result.txt.");
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/YasudaHaruto/Scenes/BookScene.unity");
        SessionState.SetBool(Batch, true);
        Run();
    }
    private static object Get(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        File.AppendAllText(Report, "PASS: " + message + "\n");
    }
    private static void Packet(byte id)
    {
        using (var bytes = new NativeArray<byte>(6, Allocator.Temp))
        {
            var writer = new DataStreamWriter(bytes);
            writer.WriteByte((byte)BOOK_MESSAGE_TYPE.APPLY_DIRT);
            writer.WriteUInt(++s_event);
            writer.WriteByte(id);
            typeof(BookNetworkClient).GetMethod("ReceiveData", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(s_packetClient, new object[] { new DataStreamReader(writer.AsNativeArray()) });
        }
    }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < s_next) return;
        try
        {
            if (EditorApplication.timeSinceStartup > s_deadline) throw new TimeoutException("Obstacle runtime test timed out.");
            if (s_game != null && (s_game.BookView.IsBusy || s_game.BookView.FlipView.IsTurning)) return;
            switch (s_step++)
            {
                case 0:
                    s_game = UnityEngine.Object.FindFirstObjectByType<BookGameController>();
                    s_obstacles = UnityEngine.Object.FindFirstObjectByType<BookGameBootstrap>().GetComponent<BookObstacleController>();
                    s_input = UnityEngine.Object.FindFirstObjectByType<BookGameDragInput>();
                    var bootstrap = UnityEngine.Object.FindFirstObjectByType<BookGameBootstrap>();
                    Require(s_game != null && s_obstacles != null && s_input != null && bootstrap != null, "Runtime controllers initialized");
                    s_rune = (RuneTraceController)Get(bootstrap, "m_runeTraceController");
                    Require(ReferenceEquals(bootstrap.GameController, s_game) && ReferenceEquals(bootstrap.ObstacleController, s_obstacles),
                        "Debug target uses Bootstrap references across separate GameObjects");
                    s_obstacles.SetConnected(true);
                    s_packetObject = new GameObject("Obstacle packet decoder validation");
                    s_packetObject.SetActive(false);
                    s_packetClient = s_packetObject.AddComponent<BookNetworkClient>();
                    s_packetClient.ObstacleReceived += s_obstacles.Receive;
                    Packet(1);
                    Require(s_game.CurrentPage == null, "Closed cover does not acquire an attack");
                    Require(s_game.BookView.OpenBook(1), "Open first game spread");
                    break;
                case 1:
                    s_page = s_game.CurrentPage;
                    Require(s_page != null && s_rune.IsInputAvailable, "Normal spread ready");
                    if (!s_debugChecked)
                    {
                        s_debugChecked = true;
                        BookObstacleValidation.Preview(2);
                        s_step--;
                        break;
                    }
                    Require(s_page.Obstacles[0] != null && s_page.Obstacles[1] != null && s_page.Obstacles[2] == null,
                        "Actual debug-tool Preview adds two stains to normal spread");
                    s_page.Obstacles.Clear(0, s_page.Obstacles[0]);
                    s_page.Obstacles.Clear(1, s_page.Obstacles[1]);
                    Packet(1); Packet(2);
                    Require(s_page.Obstacles[0] != null && s_page.Obstacles[1] != null && s_page.Obstacles[2] != null && !s_obstacles.BlocksActions, "Decoded packets fill first three slots");
                    s_saved = s_page.Obstacles[0];
                    s_obstacles.Receive(s_event, 1);
                    Require(s_page.Obstacles[3] == null, "Duplicate event ignored");
                    var pageGraphics = (ObstaclePatchGraphic[])Get(s_obstacles, "m_graphics");
                    Require(ActiveStains((BookPageView)Get(s_game.BookView, "m_rightPageView")) == 3 &&
                        pageGraphics[0].GetComponentInParent<Canvas>().rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay,
                        "Stains are attached to the captured page rather than a screen overlay");
                    CheckCapturedInk((Camera)Get(s_game.BookView, "m_captureCamera"), pageGraphics[0], s_game.BookView.CurrentTexture);
                    s_game.BookView.ShowNextSpread();
                    var liveRight = (BookPageView)Get(s_game.BookView, "m_liveRightView");
                    Require(liveRight != null && ActiveStains(liveRight) == 3, "Turning source retains all three stains on live page");
                    Require(ActiveStains((BookPageView)Get(s_game.BookView, "m_rightPageView")) == 0, "Clean destination does not inherit source stains");
                    CheckCapturedInk((Camera)Get(s_game.BookView, "m_liveCamera"), liveRight.GetComponentsInChildren<ObstaclePatchGraphic>()[0], s_game.BookView.CurrentTexture);
                    break;
                case 2:
                    Require(ReferenceEquals(s_saved, s_page.Obstacles[0]), "Turning preserves original spread state");
                    s_game.BookView.ShowPreviousSpread();
                    Packet(1); Packet(2); // Both must be queued while turning, then apply to destination.
                    break;
                case 3:
                    Require(ReferenceEquals(s_game.CurrentPage, s_page) && s_obstacles.BlocksActions && !s_rune.IsInputAvailable, "Queued attacks applied in order after turn; fourth slot blocks rune");
                    int spread = s_game.BookView.CurrentSpread;
                    s_input.OnBeginDrag(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
                    Require(s_game.BookView.CurrentSpread == spread && !s_game.BookView.FlipView.IsTurning, "Fourth slot prevents drag turning");
                    for (int i = 0; i < 4; i++) s_page.Obstacles.Clear(i, s_page.Obstacles[i]);
                    typeof(BookObstacleController).GetMethod("SynchronizeInput", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s_obstacles, null);
                    Require(s_rune.IsInputAvailable, "Clearing fourth slot restores input");
                    Packet(1); Packet(2);
                    Set(s_rune, "m_state", RUNE_TRACE_STATE.DRAWING);
                    Packet(1);
                    Require(s_rune.State == RUNE_TRACE_STATE.WAITING_STROKE && !s_rune.IsInputAvailable, "Fourth-slot arrival cancels an in-progress trace");
                    s_page.Obstacles.Clear(3, s_page.Obstacles[3]);
                    typeof(BookObstacleController).GetMethod("SynchronizeInput", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s_obstacles, null);
                    Set(s_rune, "m_state", RUNE_TRACE_STATE.COOLDOWN);
                    Set(s_rune, "m_cooldownRemaining", 10f);
                    Packet(1);
                    Require(s_rune.State == RUNE_TRACE_STATE.COOLDOWN && s_rune.CooldownRemaining > 9f, "Already started cooldown retained");
                    // Retain a rendered frame before verifying capture layers and pointer coordinates.
                    break;
                case 4:
                    var graphics = (ObstaclePatchGraphic[])Get(s_obstacles, "m_graphics");
                    Require(graphics.Length == 4, "Four visible patches built");
                    foreach (var graphic in graphics) Require(!graphic.raycastTarget && graphic.rectTransform.rect.width > 0, "Patch has geometry and leaves routing to shared input");
                    var areas = (Rect[])Get(s_obstacles, "m_areas");
                    Rect fourth = areas[3];
                    var fourthStain = s_page.Obstacles[3];
                    var pointer = new PointerEventData(EventSystem.current)
                    { button = PointerEventData.InputButton.Left, pointerId = -1, position = fourth.center };
                    s_input.OnPointerDown(pointer);
                    float span = Mathf.Min(fourth.width, fourth.height) * 0.2f;
                    pointer.position = fourth.center + Vector2.up * span;
                    s_input.OnDrag(pointer);
                    Require(fourthStain.Progress > 0 && fourthStain.Progress < 1, "Vertical pointer drag fades fourth stain continuously");
                    float beforeExit = fourthStain.Progress;
                    pointer.position = fourth.center + Vector2.right * fourth.width;
                    s_input.OnDrag(pointer);
                    pointer.position += Vector2.up * fourth.height;
                    s_input.OnDrag(pointer);
                    Require(fourthStain.Progress == beforeExit, "Moving outside stain does not clean it");
                    pointer.position = fourth.center - Vector2.up * span;
                    s_input.OnDrag(pointer);
                    Require(fourthStain.Progress == beforeExit, "Reentry while held does not count the outside path");
                    pointer.position = fourth.center + Vector2.up * span;
                    s_input.OnDrag(pointer);
                    Require(fourthStain.Progress > beforeExit, "Rubbing resumes after reentry without another press");
                    for (int i = 0; i < 16; i++)
                    {
                        pointer.position = fourth.center + Vector2.up * (i % 2 == 0 ? -span : span);
                        s_input.OnDrag(pointer);
                    }
                    s_input.OnPointerUp(pointer);
                    Require(s_page.Obstacles[3] == null && !s_obstacles.BlocksActions && s_rune.IsInputAvailable && s_page.Obstacles[0] != null,
                        "Rubbing fourth stain clears it independently and restores normal input");
                    s_obstacles.SetConnected(false);
                    Require(s_page.Obstacles[0] != null, "Connection change preserves remaining obstacles");
                    File.AppendAllText(Report, "BookObstacleRuntime COMPLETE\n");
                    Debug.Log("BookObstacleRuntime COMPLETE");
                    UnityEngine.Object.DestroyImmediate(s_packetObject);
                    EditorApplication.update -= Tick;
                    EditorApplication.ExitPlaymode();
                    break;
            }
            s_next = EditorApplication.timeSinceStartup + 0.6;
        }
        catch (Exception exception)
        {
            File.AppendAllText(Report, exception + "\n");
            Debug.LogException(exception);
            if (s_packetObject != null) UnityEngine.Object.DestroyImmediate(s_packetObject);
            EditorApplication.update -= Tick;
            EditorApplication.ExitPlaymode();
        }
    }
}
}
