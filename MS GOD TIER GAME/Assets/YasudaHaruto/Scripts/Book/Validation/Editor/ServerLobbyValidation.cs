using System;
using System.Reflection;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;

namespace YasudaHaruto.BookValidation.Editor
{
    // Dedicated batch Play Mode integration test. Never saves existing scenes or build settings.
    [InitializeOnLoad]
    public static class ServerLobbyValidation
    {
        private const string Running = "Yasuda.ServerLobbyValidation";
        private const string Batch = "Yasuda.ServerLobbyValidation.Batch";
        private static int s_step;
        private static double s_deadline;
        private static BookNetworkClient s_client;
        private static BookNetworkServer s_server;
        private static NetworkDriver s_peer;
        private static NetworkConnection s_connection;
        private static NetworkPipeline s_pipeline;
        private static bool s_peerAccepted;
        private static bool s_peerRejected;
        private static bool s_peerStarted;
        private static bool s_castReceived;
        private static bool s_done;
        private static int s_connectedCount;

        static ServerLobbyValidation()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
                s_step = 0;
                s_done = false;
                s_connectedCount = 0;
                s_castReceived = false;
                s_deadline = EditorApplication.timeSinceStartup + 90;
                EditorApplication.update += Tick;
            };
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Dedicated batch editor only; close the main Unity editor first.");
            EditorSceneManager.OpenScene("Assets/YasudaHaruto/Scenes/BookServerLobby.unity");
            SessionState.SetBool(Batch, true);
            SessionState.SetBool(Running, true);
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("Tools/YasudaHaruto/Verify Server Lobby In Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path !=
                "Assets/YasudaHaruto/Scenes/BookServerLobby.unity")
                throw new InvalidOperationException("Open BookServerLobby and exit Play Mode first.");
            SessionState.SetBool(Batch, false);
            SessionState.SetBool(Running, true);
            EditorApplication.EnterPlaymode();
        }

        private static void Require(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
            Debug.Log("ServerLobby PASS: " + description);
        }

        private static void CreatePeer(PLAYER_SLOT slot)
        {
            if (s_peer.IsCreated) s_peer.Dispose();
            s_peerAccepted = s_peerRejected = s_peerStarted = false;
            s_peer = NetworkDriver.Create();
            s_pipeline = s_peer.CreatePipeline(typeof(ReliableSequencedPipelineStage));
            s_connection = s_peer.Connect(NetworkEndpoint.LoopbackIpv4.WithPort(7777));
            s_peerSlot = slot;
        }
        private static PLAYER_SLOT s_peerSlot;

        private static void PumpPeer()
        {
            if (!s_peer.IsCreated) return;
            s_peer.ScheduleUpdate().Complete();
            NetworkEvent.Type type;
            while ((type = s_connection.PopEvent(s_peer, out DataStreamReader reader)) != NetworkEvent.Type.Empty)
            {
                if (type == NetworkEvent.Type.Connect)
                {
                    Require(s_peer.BeginSend(s_pipeline, s_connection, out DataStreamWriter writer) == 0, "peer registration send");
                    writer.WriteByte((byte)BOOK_MESSAGE_TYPE.REGISTER_CONTROLLER);
                    writer.WriteByte((byte)s_peerSlot);
                    s_peer.EndSend(writer);
                }
                if (type != NetworkEvent.Type.Data) continue;
                BOOK_MESSAGE_TYPE message = (BOOK_MESSAGE_TYPE)reader.ReadByte();
                if (message == BOOK_MESSAGE_TYPE.REGISTRATION_ACCEPTED) s_peerAccepted = true;
                if (message == BOOK_MESSAGE_TYPE.REGISTRATION_REJECTED) s_peerRejected = true;
                if (message == BOOK_MESSAGE_TYPE.GAME_STARTED) s_peerStarted = true;
            }
        }

        private static void Tick()
        {
            if (s_done) return;
            try
            {
                if (EditorApplication.timeSinceStartup > s_deadline) throw new TimeoutException("Step " + s_step);
                PumpPeer();
                switch (s_step)
                {
                    case 0:
                        s_server = BookNetworkServer.Instance;
                        if (s_server == null || !s_server.IsListening) return;
                        var flow = s_server.GetComponent<BookServerGameFlow>();
                        Require(!flow.CanStart, "zero players cannot start");
                        flow.StartGame();
                        Require(!flow.IsStarting, "zero-player button cannot load scene");
                        s_client = new GameObject("Lobby Validation Tablet").AddComponent<BookNetworkClient>();
                        s_client.OnConnectionStateChanged += state => { if (state == BOOK_CONNECTION_STATE.CONNECTED) s_connectedCount++; };
                        s_server.CastMagicReceived += (slot, id, direction) => { s_castReceived = slot == PLAYER_SLOT.PLAYER1 && id == 1; };
                        s_client.Connect("127.0.0.1", PLAYER_SLOT.PLAYER1);
                        s_step++;
                        break;
                    case 1:
                        if (s_client.ConnectionState != BOOK_CONNECTION_STATE.CONNECTED) return;
                        Require(s_server.IsPlayerConnected(PLAYER_SLOT.PLAYER1) && !s_server.IsPlayerConnected(PLAYER_SLOT.PLAYER2), "registered Player1 only");
                        Require(!s_client.HasGameStarted && SceneManager.GetActiveScene().name == "BookServerLobby", "tablet waits before host start");
                        Require(s_server.GetComponent<BookServerGameFlow>().CanStart, "one player can start");
                        CreatePeer(PLAYER_SLOT.PLAYER1);
                        s_step++;
                        break;
                    case 2:
                        if (!s_peerRejected) return;
                        Require(!s_peerAccepted && !s_peerStarted, "duplicate slot rejected without game access");
                        s_peer.Dispose();
                        s_server.GetComponent<BookServerGameFlow>().StartGame();
                        s_step++;
                        break;
                    case 3:
                        if (!s_client.HasGameStarted || SceneManager.GetActiveScene().name != "BookServerGame") return;
                        Require(BookNetworkServer.Instance == s_server && s_connectedCount == 1, "same server and connection survive real game scene load");
                        int drivers = 0;
                        BookNetworkServer adapter = null;
                        foreach (var server in UnityEngine.Object.FindObjectsByType<BookNetworkServer>(FindObjectsSortMode.None))
                        {
                            var driver = (NetworkDriver)typeof(BookNetworkServer).GetField("m_driver", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(server);
                            if (driver.IsCreated) drivers++;
                            if (server != s_server) adapter = server;
                        }
                        Require(drivers == 1 && adapter != null, "game scene server adapter does not bind a second driver");
                        Require(adapter.IsPlayerConnected(PLAYER_SLOT.PLAYER1), "scene-local reference forwards player status");
                        s_client.SendCastSkill(1, MAGIC_CAST_DIRECTION.NONE);
                        CreatePeer(PLAYER_SLOT.PLAYER2);
                        s_step++;
                        break;
                    case 4:
                        if (!s_peerAccepted || !s_peerStarted || !s_castReceived) return;
                        Require(s_server.IsPlayerConnected(PLAYER_SLOT.PLAYER2), "late Player2 receives registration and game start");
                        Require(s_castReceived, "CastMagic arrives after scene transition");
                        s_client.Disconnect();
                        s_step++;
                        break;
                    case 5:
                        if (s_server.IsPlayerConnected(PLAYER_SLOT.PLAYER1)) return;
                        Require(!s_client.HasGameStarted, "disconnect clears tablet start state");
                        s_client.Connect("127.0.0.1", PLAYER_SLOT.PLAYER1);
                        s_step++;
                        break;
                    case 6:
                        if (!s_client.HasGameStarted) return;
                        Require(s_client.ConnectionState == BOOK_CONNECTION_STATE.CONNECTED && s_connectedCount == 2, "rejoining tablet receives running game state");
                        Finish(0, "ServerLobby COMPLETE");
                        break;
                }
            }
            catch (Exception e) { Finish(1, "ServerLobby FAILED: " + e); }
        }

        private static void Finish(int exitCode, string message)
        {
            s_done = true;
            SessionState.SetBool(Running, false);
            EditorApplication.update -= Tick;
            if (s_peer.IsCreated) s_peer.Dispose();
            System.IO.Directory.CreateDirectory("Temp/ServerLobbyValidation");
            System.IO.File.WriteAllText("Temp/ServerLobbyValidation/results.txt", message);
            Debug.Log(message);
            if (SessionState.GetBool(Batch, false)) EditorApplication.Exit(exitCode);
            else EditorApplication.ExitPlaymode();
        }
    }
}
