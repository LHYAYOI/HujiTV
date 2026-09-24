////-----------------------------------------------
//// BookNetworkServer.cs
//// 制作日：2026/09/11
//// 制作者：安田晴人
//// 概要：本のページめくりをUnity Transportを使用して受信するサーバー
////-----------------------------------------------
//using Unity.Collections;
//using System.Collections.Generic;
//using Unity.Networking.Transport;
//using Unity.Networking.Transport.Utilities;
//using UnityEngine;

//public class BookNetworkServer : MonoBehaviour
//{
//    [SerializeField] private BookInputState[] m_bookInputStates;

//    private const ushort Port = 7777;

//    private NetworkDriver m_driver;
//    private NativeList<NetworkConnection> m_connections;

//    private NetworkPipeline m_reliablePipeline;

//    private readonly Dictionary<NetworkConnection, PLAYER_SLOT> m_playerSlots = new();

//    private void Start()
//    {
//        m_driver = NetworkDriver.Create();

//        // Clientと同じPipelineを同じ順番で作る
//        m_reliablePipeline = m_driver.CreatePipeline(typeof(ReliableSequencedPipelineStage));

//        m_connections = new NativeList<NetworkConnection>(Allocator.Persistent);

//        NetworkEndpoint endpoint = NetworkEndpoint.AnyIpv4.WithPort(Port);

//        int bindResult = m_driver.Bind(endpoint);

//        if (bindResult != 0)
//        {
//            Debug.LogError($"Bind失敗 : {bindResult}");

//            return;
//        }

//        int listenResult = m_driver.Listen();

//        if (listenResult != 0)
//        {
//            Debug.LogError($"Listen失敗 : {listenResult}");

//            return;
//        }

//        Debug.Log($"Book Network Server開始 : Port {Port}");
//    }

//    private void Update()
//    {
//        if (!m_driver.IsCreated)
//        {
//            return;
//        }

//        m_driver.ScheduleUpdate().Complete();

//        RemoveDisconnectedClients();
//        AcceptClients();
//        ProcessEvents();
//    }

//    private void RemoveDisconnectedClients()
//    {
//        for (int i = 0; i < m_connections.Length; i++)
//        {
//            if (!m_connections[i].IsCreated)
//            {
//                m_connections.RemoveAtSwapBack(i);
//                i--;
//            }
//        }
//    }

//    private void AcceptClients()
//    {
//        NetworkConnection connection;

//        while ((connection = m_driver.Accept()) != default)
//        {
//            m_connections.Add(connection);

//            Debug.Log("Book Clientが接続しました");
//        }
//    }

//    private void ProcessEvents()
//    {
//        for (int i = 0; i < m_connections.Length; i++)
//        {
//            NetworkConnection connection = m_connections[i];
//            NetworkEvent.Type eventType;

//            while ((eventType = m_driver.PopEventForConnection(connection, out DataStreamReader reader)) != NetworkEvent.Type.Empty)
//            {
//                switch (eventType)
//                {
//                    case NetworkEvent.Type.Data:
//                        ReceiveData(connection, reader);
//                        break;

//                    case NetworkEvent.Type.Disconnect:

//                        if (m_playerSlots.TryGetValue(connection, out PLAYER_SLOT playerSlot))
//                        {
//                            Debug.Log($"{playerSlot}が切断しました");

//                            m_playerSlots.Remove(connection);
//                        }
//                        else
//                        {
//                            Debug.Log("未登録Clientが切断しました");
//                        }

//                        m_connections[i] = default;
//                        break;
//                }
//            }
//        }
//    }

//    private void ReceiveData(NetworkConnection connection, DataStreamReader reader)
//    {
//        if (reader.Length < 1)
//        {
//            return;
//        }

//        BOOK_MESSAGE_TYPE messageType = (BOOK_MESSAGE_TYPE)reader.ReadByte();

//        switch (messageType)
//        {
//            case BOOK_MESSAGE_TYPE.REGISTER_CONTROLLER:
//                ReceiveRegisterController(connection, reader);
//                break;

//            case BOOK_MESSAGE_TYPE.PAGE_CHANGED:
//                ReceivePageChanged(connection, reader);
//                break;

//            case BOOK_MESSAGE_TYPE.CAST_REQUEST:
//                ReceiveCastRequest(connection, reader);
//                break;


//            default:
//                Debug.LogWarning($"未対応Message : {messageType}");
//                break;
//        }
//    }

//    private void ReceivePageChanged(NetworkConnection connection, DataStreamReader reader)
//    {
//        int remainingBytes = reader.Length - reader.GetBytesRead();

//        if (remainingBytes < 4)
//        {
//            Debug.LogWarning("PageChangedのデータが不足しています");

//            return;
//        }

//        int pageIndex = reader.ReadInt();

//        BookInputState bookInputState = GetBookInputState(connection);

//        if (bookInputState == null)
//        {
//            Debug.LogError("BookInputStateが設定されていません");

//            return;
//        }

//        bookInputState.SetCurrentPage(pageIndex);
//    }

//    private void ReceiveCastRequest(NetworkConnection connection, DataStreamReader reader)
//    {
//        int remainingBytes = reader.Length - reader.GetBytesRead();

//        if (remainingBytes < 4)
//        {
//            Debug.LogWarning("CastRequestのデータが不足しています");

//            return;
//        }

//        int pageIndex = reader.ReadInt();

//        Debug.Log($"PC側 CastRequest受信 : Page {pageIndex}");

//        BookInputState bookInputState = GetBookInputState(connection);

//        if (bookInputState == null)
//        {
//            Debug.LogError("BookInputStateが設定されていません");
//            return;
//        }

//        bookInputState.RequestCast(pageIndex);
//    }

//    private void ReceiveRegisterController(NetworkConnection connection, DataStreamReader reader)
//    {
//        int remainingBytes = reader.Length - reader.GetBytesRead();

//        if (remainingBytes < 1)
//        {
//            Debug.LogWarning("RegisterControllerのデータが不足しています");
//            return;
//        }

//        PLAYER_SLOT playerSlot = (PLAYER_SLOT)reader.ReadByte();

//        if (playerSlot != PLAYER_SLOT.PLAYER1 && playerSlot != PLAYER_SLOT.PLAYER2)
//        {
//            Debug.LogWarning($"不正なPlayerSlotです : {playerSlot}");

//            return;
//        }

//        // 同じPlayerがすでに使われていないか確認
//        foreach (var pair in m_playerSlots)
//        {
//            if (pair.Value == playerSlot && !pair.Key.Equals(connection))
//            {
//                Debug.LogWarning($"{playerSlot}はすでに接続済みです");
                
//                m_driver.Disconnect(connection);
//                return;
//            }
//        }

//        m_playerSlots[connection] = playerSlot;

//        Debug.Log($"Controller登録成功 : {playerSlot}");
//    }

//    private BookInputState GetBookInputState(NetworkConnection connection)
//    {
//        if (!m_playerSlots.TryGetValue(connection, out PLAYER_SLOT playerSlot))
//        {
//            Debug.LogWarning("未登録Controllerからデータを受信しました");
//            return null;
//        }

//        foreach (BookInputState state in m_bookInputStates)
//        {
//            if (state != null && state.PlayerSlot == playerSlot)
//            {
//                return state;
//            }
//        }

//        Debug.LogError($"{playerSlot}用のBookInputStateがありません");

//        return null;
//    }
//    private void OnDestroy()
//    {
//        if (m_driver.IsCreated)
//        {
//            m_driver.Dispose();
//        }

//        if (m_connections.IsCreated)
//        {
//            m_connections.Dispose();
//        }
//    }
//}