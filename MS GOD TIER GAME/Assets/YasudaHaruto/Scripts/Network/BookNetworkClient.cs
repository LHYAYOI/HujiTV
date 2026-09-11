//-----------------------------------------------
// BookNetworkClient.cs
// 制作日：2026/09/11
// 制作者：安田晴人
// 概要：本のページめくりをUnity Transportを使用してサーバーに送信するクライアント
//-----------------------------------------------
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using UnityEngine;

public class BookNetworkClient : MonoBehaviour
{
    [SerializeField]
    private BookController m_bookController;

    private const ushort Port = 7777;

    private NetworkDriver m_driver;
    private NetworkConnection m_connection;
    private NetworkPipeline m_reliablePipeline;
    private bool m_isConnected;

    private void Start()
    {
        m_driver = NetworkDriver.Create();

        m_reliablePipeline = m_driver.CreatePipeline(typeof(ReliableSequencedPipelineStage));

        NetworkEndpoint endpoint = NetworkEndpoint.LoopbackIpv4.WithPort(Port);

        m_connection = m_driver.Connect(endpoint);

        Debug.Log($"Book Serverへ接続開始 : 127.0.0.1:{Port}");

        if (m_bookController != null)
        {
            m_bookController.OnPageChanged += OnPageChanged;

            m_bookController.OnCastRequested += OnCastRequested;
        }
    }

    private void Update()
    {
        if (!m_driver.IsCreated)
        {
            return;
        }

        m_driver.ScheduleUpdate().Complete();

        if (!m_connection.IsCreated)
        {
            return;
        }

        NetworkEvent.Type eventType;

        while ((eventType = m_connection.PopEvent(m_driver, out DataStreamReader reader)) != NetworkEvent.Type.Empty)
        {
            switch (eventType)
            {
                case NetworkEvent.Type.Connect:
                    m_isConnected = true;
                    Debug.Log("Book Serverへ接続成功");

                    if (m_bookController != null)
                    {
                        // 接続した時点のページをPCへ同期
                        SendPageChanged(m_bookController.CurrentPage);
                    }
                    break;

                case NetworkEvent.Type.Data:
                    // PC → Book通信は後で実装
                    break;

                case NetworkEvent.Type.Disconnect:
                    m_isConnected = false;
                    m_connection = default;

                    Debug.Log("Book Serverから切断されました");
                    break;
            }
        }
    }

    private void OnPageChanged(int pageIndex)
    {
        SendPageChanged(pageIndex);
    }

    private void SendPageChanged(int pageIndex)
    {
        if (!m_isConnected || !m_connection.IsCreated)
        {
            Debug.LogWarning("未接続のためPageChangedを送信できません");

            return;
        }

        int beginResult = m_driver.BeginSend(m_reliablePipeline, m_connection, out DataStreamWriter writer);

        if (beginResult != 0)
        {
            Debug.LogError($"PageChanged BeginSend失敗 : {beginResult}");

            return;
        }

        writer.WriteByte((byte)BOOK_MESSAGE_TYPE.PAGE_CHANGED);

        writer.WriteInt(pageIndex);

        int endResult = m_driver.EndSend(writer);

        if (endResult < 0)
        {
            Debug.LogError($"PageChanged EndSend失敗 : {endResult}");

            return;
        }

        Debug.Log($"PageChanged送信 : {pageIndex}");
    }

    private void OnCastRequested(int pageIndex)
    {
        SendCastRequest(pageIndex);
    }

    private void SendCastRequest(int pageIndex)
    {
        if (!m_isConnected || !m_connection.IsCreated)
        {
            Debug.LogWarning("未接続のためCastRequestを送信できません");

            return;
        }

        int beginResult = m_driver.BeginSend(m_reliablePipeline, m_connection, out DataStreamWriter writer);

        if (beginResult != 0)
        {
            Debug.LogError($"CastRequest BeginSend失敗 : {beginResult}");

            return;
        }

        writer.WriteByte((byte)BOOK_MESSAGE_TYPE.CAST_REQUEST);

        writer.WriteInt(pageIndex);

        int endResult = m_driver.EndSend(writer);

        if (endResult < 0)
        {
            Debug.LogError($"CastRequest EndSend失敗 : {endResult}");

            return;
        }

        Debug.Log($"CastRequest送信 : Page {pageIndex}");
    }

    private void OnDestroy()
    {
        if (m_bookController != null)
        {
            m_bookController.OnPageChanged -= OnPageChanged;
            m_bookController.OnCastRequested -= OnCastRequested;
        }

        if (!m_driver.IsCreated)
        {
            return;
        }

        if (m_connection.IsCreated)
        {
            m_driver.Disconnect(m_connection);
            m_driver.ScheduleUpdate().Complete();
        }

        m_driver.Dispose();
    }
}