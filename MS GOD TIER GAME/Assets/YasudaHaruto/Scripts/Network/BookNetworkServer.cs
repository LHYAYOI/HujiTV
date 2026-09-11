//-----------------------------------------------
// BookNetworkServer.cs
// 制作日：2026/09/11
// 制作者：安田晴人
// 概要：本のページめくりをUnity Transportを使用して受信するサーバー
//-----------------------------------------------
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using UnityEngine;

public class BookNetworkServer : MonoBehaviour
{
    [SerializeField]
    private BookInputState m_bookInputState;

    private const ushort Port = 7777;

    private NetworkDriver m_driver;
    private NativeList<NetworkConnection> m_connections;

    private NetworkPipeline m_reliablePipeline;

    private void Start()
    {
        m_driver =
            NetworkDriver.Create();

        // Clientと同じPipelineを同じ順番で作る
        m_reliablePipeline = m_driver.CreatePipeline(typeof(ReliableSequencedPipelineStage));

        m_connections = new NativeList<NetworkConnection>(Allocator.Persistent);

        NetworkEndpoint endpoint = NetworkEndpoint.AnyIpv4.WithPort(Port);

        int bindResult = m_driver.Bind(endpoint);

        if (bindResult != 0)
        {
            Debug.LogError($"Bind失敗 : {bindResult}");

            return;
        }

        int listenResult = m_driver.Listen();

        if (listenResult != 0)
        {
            Debug.LogError($"Listen失敗 : {listenResult}");

            return;
        }

        Debug.Log($"Book Network Server開始 : Port {Port}");
    }

    private void Update()
    {
        if (!m_driver.IsCreated)
        {
            return;
        }

        m_driver.ScheduleUpdate().Complete();

        RemoveDisconnectedClients();
        AcceptClients();
        ProcessEvents();
    }

    private void RemoveDisconnectedClients()
    {
        for (int i = 0; i < m_connections.Length; i++)
        {
            if (!m_connections[i].IsCreated)
            {
                m_connections.RemoveAtSwapBack(i);
                i--;
            }
        }
    }

    private void AcceptClients()
    {
        NetworkConnection connection;

        while ((connection = m_driver.Accept()) != default)
        {
            m_connections.Add(connection);

            Debug.Log("Book Clientが接続しました");
        }
    }

    private void ProcessEvents()
    {
        for (int i = 0; i < m_connections.Length; i++)
        {
            NetworkConnection connection = m_connections[i];
            NetworkEvent.Type eventType;

            while ((eventType = m_driver.PopEventForConnection(connection, out DataStreamReader reader)) != NetworkEvent.Type.Empty)
            {
                switch (eventType)
                {
                    case NetworkEvent.Type.Data:
                        ReceiveData(reader);
                        break;

                    case NetworkEvent.Type.Disconnect:

                        Debug.Log("Book Clientが切断しました");

                        m_connections[i] = default;
                        break;
                }
            }
        }
    }

    private void ReceiveData(DataStreamReader reader)
    {
        if (reader.Length < 1)
        {
            return;
        }

        BOOK_MESSAGE_TYPE messageType = (BOOK_MESSAGE_TYPE)reader.ReadByte();

        switch (messageType)
        {
            case BOOK_MESSAGE_TYPE.PAGE_CHANGED:
                ReceivePageChanged(reader);
                break;

            case BOOK_MESSAGE_TYPE.CAST_REQUEST:
                ReceiveCastRequest(reader);
                break;


            default:

                Debug.LogWarning($"未対応Message : {messageType}");

                break;
        }
    }

    private void ReceivePageChanged(DataStreamReader reader)
    {
        int remainingBytes = reader.Length - reader.GetBytesRead();

        if (remainingBytes < 4)
        {
            Debug.LogWarning("PageChangedのデータが不足しています");

            return;
        }

        int pageIndex = reader.ReadInt();

        if (m_bookInputState == null)
        {
            Debug.LogError("BookInputStateが設定されていません");

            return;
        }

        m_bookInputState.SetCurrentPage(pageIndex);
    }

    private void ReceiveCastRequest(DataStreamReader reader)
    {
        int remainingBytes = reader.Length - reader.GetBytesRead();

        if (remainingBytes < 4)
        {
            Debug.LogWarning("CastRequestのデータが不足しています");

            return;
        }

        int pageIndex = reader.ReadInt();

        Debug.Log($"PC側 CastRequest受信 : Page {pageIndex}");

        if (m_bookInputState != null && m_bookInputState.CurrentPage != pageIndex)
        {
            Debug.LogWarning($"ページ状態が不一致です。" + $" PC={m_bookInputState.CurrentPage}" + $" Book={pageIndex}");
        }

        if (m_bookInputState == null)
        {
            Debug.LogError("BookInputStateが設定されていません");

            return;
        }

        m_bookInputState.RequestCast(pageIndex);
    }

    private void OnDestroy()
    {
        if (m_driver.IsCreated)
        {
            m_driver.Dispose();
        }

        if (m_connections.IsCreated)
        {
            m_connections.Dispose();
        }
    }
}