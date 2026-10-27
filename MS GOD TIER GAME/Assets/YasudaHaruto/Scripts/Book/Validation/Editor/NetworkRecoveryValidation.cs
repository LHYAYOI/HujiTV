using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;

namespace YasudaHaruto.BookValidation.Editor
{
    [InitializeOnLoad]
    public static class NetworkRecoveryValidation
    {
        private const string Running = "YasudaNetworkRecovery.Batch";
        private const string Batch = "YasudaNetworkRecovery.ExitEditor";
        private static BookNetworkClient s_client;
        private static BookGameDragInput s_input;
        private static RuneTraceController s_rune;
        private static RuneData s_data;
        private static NetworkDriver s_server;
        private static NetworkConnection s_connection;
        private static int s_step;
        private static int s_registrations;
        private static double s_deadline;
        private static float s_mark;
        private static int s_scene;

        private sealed class View : IRuneTraceView
        {
            public void BeginTrace(RuneData data) { }
            public void UpdateCurrentStroke(IReadOnlyList<Vector2> points) { }
            public void EndStroke() { }
            public void PlaySuccess(RuneTraceResult result) { }
            public void PlayFailure(RuneTraceResult result) { }
            public void Clear() { }
        }

        static NetworkRecoveryValidation()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
                s_step = 0;
                s_registrations = 0;
                s_deadline = EditorApplication.timeSinceStartup + 45;
                EditorApplication.update += Tick;
            };
        }

        // Run only in a dedicated batch editor. Does not save or modify source scenes.
        public static void RunBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use a dedicated batch editor with the main editor closed.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Batch, true);
            SessionState.SetBool(Running, true);
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("Tools/YasudaHaruto/Verify Network Recovery")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");
            if (UnityEngine.Object.FindFirstObjectByType<BookNetworkClient>() != null ||
                UnityEngine.Object.FindFirstObjectByType<BookConnectionUI>() != null ||
                UnityEngine.Object.FindFirstObjectByType<BookNetworkServer>() != null)
                throw new InvalidOperationException("Use the book scene without an active network client, connection UI, or server.");
            SessionState.SetBool(Batch, false);
            SessionState.SetBool(Running, true);
            EditorApplication.EnterPlaymode();
        }

        private static object Get(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            Debug.Log("NetworkRecovery PASS: " + message);
        }

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > s_deadline) throw new TimeoutException("Network recovery validation timed out at step " + s_step);
                if (s_server.IsCreated) PumpServer();
                switch (s_step)
                {
                    case 0:
                        RuneFeedbackValidation.Run();
                        CheckRuneRecovery();
                        Time.timeScale = 0f;
                        s_scene = SceneManager.GetActiveScene().handle;
                        s_client = new GameObject("Recovery Validation Client").AddComponent<BookNetworkClient>();
                        Set(s_client, "m_connectionTimeoutSeconds", 0.2f);
                        s_client.OnConnectionStateChanged += state => s_input.SetRuneInputAvailable(state == BOOK_CONNECTION_STATE.CONNECTED);
                        s_client.Connect("invalid", PLAYER_SLOT.PLAYER1);
                        Require(!s_client.IsReconnecting, "Invalid address never starts automatic retries");
                        s_client.Connect("127.0.0.1", (PLAYER_SLOT)99);
                        Require(!s_client.IsReconnecting, "Invalid player slot never starts automatic retries");
                        s_client.Connect("127.0.0.1", PLAYER_SLOT.PLAYER2);
                        s_input.gameObject.AddComponent<BookConnectionStatusView>().Initialize(s_client);
                        s_step++;
                        break;
                    case 1:
                        if (s_client.ConnectionState != BOOK_CONNECTION_STATE.DISCONNECTED) break;
                        Require(s_client.IsReconnecting && (float)Get(s_client, "m_reconnectDelay") == 2f,
                            "Initial failure schedules retry with timeScale zero");
                        Require(s_input.GetComponentInChildren<Text>().text.Contains("再接続中"),
                            "Reconnect banner displays while the server is unavailable");
                        foreach (var graphic in s_input.GetComponentsInChildren<Graphic>(true))
                            Require(!graphic.raycastTarget, "Connection banner never captures page input");
                        Require(s_input.GetComponentInChildren<GraphicRaycaster>(true) == null,
                            "Connection status canvas has no input raycaster");
                        s_mark = Time.unscaledTime;
                        s_step++;
                        break;
                    case 2:
                        if (s_client.ConnectionState != BOOK_CONNECTION_STATE.CONNECTING) break;
                        Require(Time.unscaledTime - s_mark >= 0.9f, "Retry waits its one second interval using unscaled time");
                        s_server = NetworkDriver.Create();
                        s_server.CreatePipeline(typeof(ReliableSequencedPipelineStage));
                        Require(s_server.Bind(NetworkEndpoint.AnyIpv4.WithPort(7777)) == 0 && s_server.Listen() == 0,
                            "Late-started local server binds successfully");
                        s_step++;
                        break;
                    case 3:
                        if (s_registrations < 1 || s_client.ConnectionState != BOOK_CONNECTION_STATE.CONNECTED) break;
                        Require(s_rune.CanBeginStroke && !s_client.IsReconnecting, "Connection restores rune input");
                        Require(s_input.GetComponentInChildren<Text>(true).gameObject.activeInHierarchy == false,
                            "Connection banner disappears after successful connection");
                        s_rune.BeginStroke(new Vector2(0.1f, 0.5f));
                        s_rune.AddPoint(new Vector2(0.9f, 0.5f));
                        s_rune.EndStroke();
                        Require(s_rune.IsAwaitingCast, "Cast is pending before connection loss");
                        s_server.Disconnect(s_connection);
                        s_connection = default;
                        s_step++;
                        break;
                    case 4:
                        if (s_client.ConnectionState == BOOK_CONNECTION_STATE.CONNECTED) break;
                        Require(!s_rune.CanBeginStroke && !s_rune.IsAwaitingCast && !s_input.IsGestureActive,
                            "Real transport disconnect cancels pending cast and blocks rune input");
                        s_step++;
                        break;
                    case 5:
                        if (s_registrations < 2 || s_client.ConnectionState != BOOK_CONNECTION_STATE.CONNECTED) break;
                        Require(s_rune.CanBeginStroke && SceneManager.GetActiveScene().handle == s_scene,
                            "Reconnect registers the same slot and retains the current scene");
                        Require((float)Get(s_client, "m_reconnectDelay") == 1f, "Successful connection resets retry delay");
                        s_client.Disconnect();
                        s_mark = Time.unscaledTime;
                        s_step++;
                        break;
                    case 6:
                        if (Time.unscaledTime - s_mark < 1.2f) break;
                        Require(!s_client.IsReconnecting && s_client.ConnectionState == BOOK_CONNECTION_STATE.DISCONNECTED,
                            "Explicit disconnect stops automatic retries");
                        var disconnected = typeof(BookNetworkClient).GetMethod("OnDisconnected", BindingFlags.Instance | BindingFlags.NonPublic);
                        Set(s_client, "m_shouldReconnect", true);
                        foreach (float delay in new[] { 1f, 2f, 4f, 8f, 8f })
                        {
                            disconnected.Invoke(s_client, null);
                            Require(Mathf.Abs((float)Get(s_client, "m_nextReconnectTime") - Time.unscaledTime - delay) < 0.01f,
                                "Retry wait is " + delay + " seconds and remains capped");
                        }
                        s_client.Disconnect();
                        Finish(0);
                        break;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Finish(1);
            }
        }

        private static void PumpServer()
        {
            s_server.ScheduleUpdate().Complete();
            NetworkConnection accepted;
            while ((accepted = s_server.Accept()) != default) s_connection = accepted;
            if (!s_connection.IsCreated) return;
            NetworkEvent.Type type;
            while ((type = s_server.PopEventForConnection(s_connection, out var reader)) != NetworkEvent.Type.Empty)
            {
                if (type == NetworkEvent.Type.Disconnect) { s_connection = default; break; }
                if (type != NetworkEvent.Type.Data) continue;
                Require(reader.Length == 2 && reader.ReadByte() == (byte)BOOK_MESSAGE_TYPE.REGISTER_CONTROLLER &&
                    reader.ReadByte() == (byte)PLAYER_SLOT.PLAYER2, "Each connection sends the original player registration");
                s_registrations++;
            }
        }

        private static void CheckRuneRecovery()
        {
            s_data = ScriptableObject.CreateInstance<RuneData>();
            s_data.TraceData.SetStrokes(new List<RuneStrokeData> { new RuneStrokeData(new List<Vector2>
                { new Vector2(0.1f, 0.5f), new Vector2(0.9f, 0.5f) }) });
            s_rune = new RuneTraceController(new View());
            s_input = new GameObject("Recovery Validation Input", typeof(RectTransform)).AddComponent<BookGameDragInput>();
            var input = new BookInputController();
            s_input.Initialize(s_rune, input);
            s_rune.BeginTrace(s_data);
            input.BeginRuneTrace();
            s_rune.BeginStroke(new Vector2(0.1f, 0.5f));
            s_input.SetRuneInputAvailable(false);
            s_rune.AddPoint(new Vector2(0.9f, 0.5f));
            Require(!s_rune.CanBeginStroke && s_rune.State == RUNE_TRACE_STATE.WAITING_STROKE && input.CanNavigatePage,
                "Disconnect clears partial tracing and releases page navigation");
            s_rune.BeginTrace(s_data);
            s_rune.BeginStroke(new Vector2(0.1f, 0.5f));
            Require(s_rune.State == RUNE_TRACE_STATE.WAITING_STROKE, "Turning to another page cannot re-enable offline drawing");
            s_input.SetRuneInputAvailable(true);
            s_rune.ResumeCooldown(0.8f);
            s_input.SetRuneInputAvailable(false);
            s_rune.Tick(0.2f);
            Require(Mathf.Abs(s_rune.CooldownRemaining - 0.6f) < 0.001f, "Disconnect retains and advances an already started cooldown");
            s_rune.Tick(1f);
            Require(!s_rune.CanBeginStroke && !s_rune.TryBeginCast(), "Cooldown expiry cannot unlock offline casting");
            s_input.SetRuneInputAvailable(true);
            var interaction = new RuneTraceInteraction(s_data, s_rune, input, null, MAGIC_AIM_TYPE.THREE_DIRECTIONS);
            interaction.Begin();
            s_rune.ResumeCooldown(0.7f);
            interaction.End();
            interaction.Begin();
            Require(s_rune.State == RUNE_TRACE_STATE.COOLDOWN && s_rune.CooldownRemaining > 0.6f,
                "Page interaction retains cooldown across leaving and returning");
            interaction.End();
            s_rune.BeginTrace(s_data);
        }

        private static void Finish(int code)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(Running, false);
            Time.timeScale = 1f;
            if (s_client != null) UnityEngine.Object.DestroyImmediate(s_client.gameObject);
            if (s_input != null) UnityEngine.Object.DestroyImmediate(s_input.gameObject);
            if (s_data != null) UnityEngine.Object.DestroyImmediate(s_data);
            if (s_server.IsCreated) s_server.Dispose();
            if (code == 0) Debug.Log("NetworkRecovery COMPLETE");
            if (SessionState.GetBool(Batch, false)) EditorApplication.Exit(code);
            else EditorApplication.ExitPlaymode();
        }
    }
}
