//-----------------------------------------------
// TransportServerTest.cs
// 制作日：2026/09/10
// 制作者：安田晴人
// 概要：Unity Transportを使用したサーバーのテストスクリプト
//-----------------------------------------------
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using UnityEngine;

public class TransportServerTest : MonoBehaviour
{
    private const ushort Port = 7777;

    private NetworkDriver m_driver;
    private NativeList<NetworkConnection> m_connections;
    private NetworkPipeline m_reliablePipeline;

    [SerializeField] Transform m_orientationTarget;

    private void Start()
    {
        // 通信を担当するNetworkDriverを作成
        m_driver = NetworkDriver.Create();
        m_reliablePipeline = m_driver.CreatePipeline(typeof(ReliableSequencedPipelineStage));

        // 接続してきたClientを保存するリスト
        m_connections = new NativeList<NetworkConnection>(Allocator.Persistent);

        // すべてのIPv4アドレスからPort 7777への接続を受け付ける
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

        Debug.Log($"Server開始 : Port {Port}");
    }

    private void Update()
    {
        if (!m_driver.IsCreated)
        {
            return;
        }

        // NetworkDriver内部の送受信処理を更新
        m_driver.ScheduleUpdate().Complete();

        RemoveDisconnectedClients();
        AcceptNewClients();
        ProcessClientEvents();
    }


    // 接続が切断されたClientをリストから削除する
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

    // 新しいClientの接続を受け付ける
    private void AcceptNewClients()
    {
        NetworkConnection connection;

        while ((connection = m_driver.Accept()) != default)
        {
            m_connections.Add(connection);

            Debug.Log("Clientが接続しました");
        }
    }

    // Clientからのイベントを処理する
    private void ProcessClientEvents()
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
                        Debug.Log("Clientが切断しました");

                        m_connections[i] = default;
                        break;
                }
            }
        }
    }

    // Clientから受信したデータを処理する
    private void ReceiveData(DataStreamReader reader)
    {
        if (reader.Length < 1)
        {
            Debug.LogWarning("空のデータを受信しました");
            return;
        }

        BOOK_MESSAGE_TYPE messageType =
            (BOOK_MESSAGE_TYPE)reader.ReadByte();

        switch (messageType)
        {
            case BOOK_MESSAGE_TYPE.BUTTON_PRESSED:
                ReceiveButtonPressed(reader);
                break;

            case BOOK_MESSAGE_TYPE.ORIENTATION:
                ReceiveOrientation(reader);
                break;

            default:
                Debug.LogWarning(
                    $"未対応のMessageTypeです : {messageType}");
                break;
        }
    }

    private void ReceiveButtonPressed(DataStreamReader reader)
    {
        // MessageTypeを1byte読んだ後なので、
        // ButtonIDが存在するか確認
        if (reader.GetBytesRead() >= reader.Length)
        {
            Debug.LogWarning(
                "ButtonPressedにButtonIDがありません");
            return;
        }

        byte buttonId = reader.ReadByte();

        Debug.Log($"Button Pressed : ID = {buttonId}");
    }

    private void ReceiveOrientation(
    DataStreamReader reader)
    {
        // Quaternion = float × 4 = 16byte
        int remainingBytes =
            reader.Length - reader.GetBytesRead();

        if (remainingBytes < 16)
        {
            Debug.LogWarning(
                $"Orientationのデータが不足しています : {remainingBytes} byte");
            return;
        }

        float x = reader.ReadFloat();
        float y = reader.ReadFloat();
        float z = reader.ReadFloat();
        float w = reader.ReadFloat();

        Quaternion rotation =
            new Quaternion(x, y, z, w);

        if (m_orientationTarget != null)
        {
            m_orientationTarget.rotation = rotation;
        }
    }

    // Clientにデータを送信する
    private void SendData(NetworkConnection connection, uint value)
    {
        int beginResult = m_driver.BeginSend(connection, out DataStreamWriter writer);

        if (beginResult != 0)
        {
            Debug.LogError($"BeginSend失敗 : {beginResult}");
            return;
        }

        writer.WriteUInt(value);

        int endResult = m_driver.EndSend(writer);

        if (endResult < 0)
        {
            Debug.LogError($"EndSend失敗 : {endResult}");
            return;
        }

        Debug.Log($"Server送信 : {value}");
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