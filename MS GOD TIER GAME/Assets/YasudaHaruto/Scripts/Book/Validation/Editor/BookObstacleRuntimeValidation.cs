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
    static BookObstacleRuntimeValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (!SessionState.GetBool(Running, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                s_step = 0; s_event = 0;
                s_next = EditorApplication.timeSinceStartup + 2;
                s_deadline = EditorApplication.timeSinceStartup + 90;
                EditorApplication.update += Tick;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            { SessionState.SetBool(Running, false); EditorApplication.update -= Tick; }
        };
        if (File.Exists("Temp/ObstacleValidation/runtime-request")) EditorApplication.delayCall += () =>
        { File.Delete("Temp/ObstacleValidation/runtime-request"); Run(); };
    }
    [MenuItem("Tools/YasudaHaruto/Verify Book Obstacles In Current Scene")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        Directory.CreateDirectory("Temp/ObstacleValidation");
        File.WriteAllText(Report, "Current scene test; no scene save or replacement.\n");
        SessionState.SetBool(Running, true);
        EditorApplication.EnterPlaymode();
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
            writer.WriteByte((byte)BOOK_MESSAGE_TYPE.APPLY_OBSTACLE);
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
                    s_obstacles = UnityEngine.Object.FindFirstObjectByType<BookObstacleController>();
                    s_input = UnityEngine.Object.FindFirstObjectByType<BookGameDragInput>();
                    var bootstrap = UnityEngine.Object.FindFirstObjectByType<BookGameBootstrap>();
                    Require(s_game != null && s_obstacles != null && s_input != null && bootstrap != null, "Runtime controllers initialized");
                    s_rune = (RuneTraceController)Get(bootstrap, "m_runeTraceController");
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
                    Packet(1); Packet(2);
                    Require(s_page.Obstacles[0] != null && s_page.Obstacles[1] != null && s_page.Obstacles[2] != null && !s_obstacles.BlocksActions, "Decoded packets fill first three slots");
                    s_saved = s_page.Obstacles[0];
                    s_obstacles.Receive(s_event, 1);
                    Require(s_page.Obstacles[3] == null, "Duplicate event ignored");
                    s_game.BookView.ShowNextSpread();
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
                    // Leave full state briefly so graphics are populated and can be inspected.
                    break;
                case 4:
                    var graphics = s_obstacles.GetComponentsInChildren<ObstaclePatchGraphic>();
                    Require(graphics.Length == 4, "Four visible patches built");
                    foreach (var graphic in graphics) Require(!graphic.raycastTarget && graphic.rectTransform.rect.width > 0, "Patch has geometry and leaves routing to shared input");
                    s_obstacles.SetConnected(false);
                    Require(s_obstacles.BlocksActions, "Connection change preserves obstacles");
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
