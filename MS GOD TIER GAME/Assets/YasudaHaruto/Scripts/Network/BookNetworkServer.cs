//-----------------------------------------------
// BookNetworkServer.cs
// 制作日：2026/09/11
// 制作者：安田晴人
// 概要：本のページめくりをUnity Transportを使用して受信するサーバー
//-----------------------------------------------
using Unity.Collections;
using System.Collections.Generic;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using UnityEngine;

public class BookNetworkServer : MonoBehaviour
{
    private const ushort Port = 7777;

    private NetworkDriver m_driver;
    private NativeList<NetworkConnection> m_connections;

    private NetworkPipeline m_reliablePipeline;

    private readonly Dictionary<NetworkConnection, PLAYER_SLOT> m_playerSlots = new();

    [SerializeField] private MagicManager m_magicManager;

    private void Start()
    {
        m_driver = NetworkDriver.Create();

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

        //if (Input.GetKeyDown(KeyCode.Alpha1))
        //{
        //    SendAddPage(PLAYER_SLOT.PLAYER1, 1);
        //}

        //if (Input.GetKeyDown(KeyCode.Alpha0))
        //{
        //    SendAddPage(PLAYER_SLOT.PLAYER1, 10);
        //}
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
                        ReceiveData(connection, reader);
                        break;

                    case NetworkEvent.Type.Disconnect:

                        if (m_playerSlots.TryGetValue(connection, out PLAYER_SLOT playerSlot))
                        {
                            Debug.Log($"{playerSlot}が切断しました");

                            m_playerSlots.Remove(connection);
                        }
                        else
                        {
                            Debug.Log("未登録Clientが切断しました");
                        }

                        m_connections[i] = default;
                        break;
                }
            }
        }
    }

    public bool SendAddPage(PLAYER_SLOT playerSlot, byte pageId)
    {
        if (pageId == 0)
        {
            Debug.LogWarning("PageId 0は送信できません");
            return false;
        }

        foreach (var pair in m_playerSlots)
        {
            if (pair.Value != playerSlot)
            {
                continue;
            }

            NetworkConnection connection = pair.Key;

            if (!connection.IsCreated)
            {
                return false;
            }

            if (m_driver.BeginSend(m_reliablePipeline, connection, out DataStreamWriter writer) != 0)
            {
                return false;
            }

            writer.WriteByte((byte)BOOK_MESSAGE_TYPE.ADD_PAGE);
            writer.WriteByte(pageId);

            m_driver.EndSend(writer);

            Debug.Log($"AddPage送信 : {playerSlot} / PageId={pageId}");
            return true;
        }

        Debug.LogWarning($"送信対象が見つかりません : {playerSlot}");
        return false;
    }

    private void ReceiveData(NetworkConnection connection, DataStreamReader reader)
    {
        if (reader.Length < 1)
        {
            return;
        }

        BOOK_MESSAGE_TYPE messageType = (BOOK_MESSAGE_TYPE)reader.ReadByte();

        switch (messageType)
        {
            case BOOK_MESSAGE_TYPE.REGISTER_CONTROLLER:
                ReceiveRegisterController(connection, reader);
                break;

            case BOOK_MESSAGE_TYPE.TEST_PING:
                ReceiveTestPing(connection, reader);
                break;

            case BOOK_MESSAGE_TYPE.CAST_MAGIC:
                ReceiveCastMagic(connection, reader);
                break;

            case BOOK_MESSAGE_TYPE.CURSOR_POSITION:
                ReceiveVector2(connection, reader);
                break;

            default:
                Debug.LogWarning($"未対応Message : {messageType}");
                break;
        }
    }

    private void ReceiveTestPing(NetworkConnection connection, DataStreamReader reader)
    {
        int remainingBytes = reader.Length - reader.GetBytesRead();

        if (remainingBytes < 4)
        {
            Debug.LogWarning("TestPingのデータが不足しています");

            return;
        }

        int value = reader.ReadInt();

        if (!m_playerSlots.TryGetValue(connection, out PLAYER_SLOT playerSlot))
        {
            Debug.LogWarning("未登録ControllerからTestPingを受信しました");

            return;
        }

        Debug.Log($"TestPing受信 : {playerSlot} / {value}");

        SendTestPong(connection, value + 1);
    }

    private void ReceiveCastMagic(NetworkConnection connection, DataStreamReader reader)
    {
        int remainingBytes = reader.Length - reader.GetBytesRead();

        if (remainingBytes < 1)
        {
            Debug.LogWarning("CastSkillのデータが不足しています");
            return;
        }

        if (!m_playerSlots.TryGetValue(connection, out PLAYER_SLOT playerSlot))
        {
            Debug.LogWarning("未登録ControllerからCastSkillを受信しました");
            return;
        }

        byte skillId = reader.ReadByte();

        if (skillId == 0)
        {
            Debug.LogWarning($"{playerSlot}から無効なSkillIdを受信しました");
            return;
        }

        Debug.Log($"CastSkill受信 : {playerSlot} / SkillId={skillId}");

        m_magicManager.RequestCast(skillId);
    }

    private void ReceiveVector2(NetworkConnection connection, DataStreamReader reader)
    {
        int remainingBytes = reader.Length - reader.GetBytesRead();

        // float × 2 = 8byte
        if (remainingBytes < 8)
        {
            Debug.LogWarning("Vector2Dataのデータが不足しています");
            return;
        }

        float x = reader.ReadFloat();
        float y = reader.ReadFloat();

        Vector2 value = new Vector2(x, y);

        if (m_playerSlots.TryGetValue(connection, out PLAYER_SLOT playerSlot))
        {
            Debug.Log($"{playerSlot} Vector2受信 : {value}");
        }
        else
        {
            Debug.LogWarning($"未登録ClientからVector2を受信 : {value}");
        }
    }

    private void SendTestPong(NetworkConnection connection, int value)
    {
        if (!connection.IsCreated)
        {
            return;
        }

        int beginResult = m_driver.BeginSend(m_reliablePipeline, connection, out DataStreamWriter writer);

        if (beginResult != 0)
        {
            Debug.LogError($"TestPong BeginSend失敗 : {beginResult}");

            return;
        }

        writer.WriteByte((byte)BOOK_MESSAGE_TYPE.TEST_PONG);

        writer.WriteInt(value);

        int endResult = m_driver.EndSend(writer);

        if (endResult < 0)
        {
            Debug.LogError($"TestPong EndSend失敗 : {endResult}");

            return;
        }

        Debug.Log($"TestPong送信 : {value}");
    }

    private void ReceiveRegisterController(NetworkConnection connection, DataStreamReader reader)
    {
        int remainingBytes = reader.Length - reader.GetBytesRead();

        if (remainingBytes < 1)
        {
            Debug.LogWarning("RegisterControllerのデータが不足しています");
            return;
        }

        PLAYER_SLOT playerSlot = (PLAYER_SLOT)reader.ReadByte();

        if (playerSlot != PLAYER_SLOT.PLAYER1 && playerSlot != PLAYER_SLOT.PLAYER2)
        {
            Debug.LogWarning($"不正なPlayerSlotです : {playerSlot}");

            return;
        }

        // 同じPlayerがすでに使われていないか確認
        foreach (var pair in m_playerSlots)
        {
            if (pair.Value == playerSlot && !pair.Key.Equals(connection))
            {
                Debug.LogWarning($"{playerSlot}はすでに接続済みです");

                m_driver.Disconnect(connection);
                return;
            }
        }

        m_playerSlots[connection] = playerSlot;

        Debug.Log($"Controller登録成功 : {playerSlot}");
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