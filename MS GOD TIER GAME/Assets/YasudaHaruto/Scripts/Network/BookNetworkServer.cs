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
    [SerializeField] private MagicManager m_magicManager;

    private static BookNetworkServer s_instance;
    public static BookNetworkServer Instance => s_instance;
    private BookNetworkServer Owner => s_instance != null ? s_instance : this;
    private bool m_isListening;
    private bool m_isGameStarted;
    public bool IsListening => Owner.m_isListening;
    public bool IsGameStarted => Owner.m_isGameStarted;
    private readonly Dictionary<NetworkConnection, BOOK_MESSAGE_TYPE> m_registrationReplies = new();
    private readonly List<byte> m_acquiredPages = new();
    private readonly Dictionary<NetworkConnection, (int Next, int ReplayCount)> m_pageDelivery = new();

    public bool AcquirePageForBoth(byte pageId)
    {
        if (Owner != this) return Owner.AcquirePageForBoth(pageId);
        if (!m_isGameStarted || pageId == 0 || m_acquiredPages.Contains(pageId)) return false;
        m_acquiredPages.Add(pageId);
        return true;
    }

    private readonly HashSet<NetworkConnection> m_startNotified = new();
    private readonly List<NetworkConnection> m_replyBuffer = new();
    private System.Action<PLAYER_SLOT, byte, MAGIC_CAST_DIRECTION> m_castMagicReceived;

    // Scene-local servers remain as reference adapters for existing Inspector links.
    private void Awake()
    {
        if (s_instance != null && s_instance != this) return;
        s_instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public bool IsPlayerConnected(PLAYER_SLOT slot)
    {
        if (Owner != this) return Owner.IsPlayerConnected(slot);
        foreach (var pair in m_playerSlots)
            if (pair.Value == slot && IsConnectionAlive(pair.Key)) return true;
        return false;
    }

    private bool IsConnectionAlive(NetworkConnection connection) => m_driver.IsCreated &&
        connection.IsCreated && m_driver.GetConnectionState(connection) == NetworkConnection.State.Connected;

    // Called by the game flow only after the PC game scene has loaded.
    public void BeginGame()
    {
        if (Owner != this) { Owner.BeginGame(); return; }
        if (!m_isListening || m_isGameStarted) return;
        m_isGameStarted = true;
    }

    private const ushort Port = ServerDiscoveryProtocol.GamePort;
    private ServerDiscoveryResponder m_discovery;

    private NetworkDriver m_driver;
    private NativeList<NetworkConnection> m_connections;

    private NetworkPipeline m_reliablePipeline;

    private readonly Dictionary<NetworkConnection, PLAYER_SLOT> m_playerSlots = new();

    private Vector2 m_cursorPosition;

    public event System.Action<PLAYER_SLOT, byte, MAGIC_CAST_DIRECTION> CastMagicReceived
    {
        add { Owner.m_castMagicReceived += value; }
        remove { Owner.m_castMagicReceived -= value; }
    }

    private void Start()
    {
        if (Owner != this) return;
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

        m_isListening = true;
        m_discovery = gameObject.AddComponent<ServerDiscoveryResponder>();
        m_discovery.BeginResponding();

        Debug.Log($"Book Network Server開始 : Port {Port}");
    }

    private void Update()
    {
        if (Owner != this || !m_driver.IsCreated)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            SendDirt(PLAYER_SLOT.PLAYER1, 1);
        }

        m_driver.ScheduleUpdate().Complete();

        RemoveDisconnectedClients();
        AcceptClients();
        ProcessEvents();
        SendSessionMessages();
        SendAcquiredPages();
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

                        m_registrationReplies.Remove(connection);
                        m_startNotified.Remove(connection);
                        m_pageDelivery.Remove(connection);
                        m_connections[i] = default;
                        break;
                }
            }
        }
    }

    private uint m_obstacleEvent;
    // Compatibility for previous callers: old Rub=1, old Ink=2.
    public bool SendObstacle(PLAYER_SLOT playerSlot, byte obstacleId) =>
        obstacleId == 1 || obstacleId == 2 ? SendDirt(playerSlot, obstacleId) : false;

    public bool SendDirt(PLAYER_SLOT playerSlot, byte slotCount = 1)
    {
        if (Owner != this) return Owner.SendDirt(playerSlot, slotCount);
        if (slotCount < 1 || slotCount > 4 || !m_driver.IsCreated) return false;
        foreach (var pair in m_playerSlots)
        {
            if (pair.Value != playerSlot || !pair.Key.IsCreated) continue;
            if (m_driver.BeginSend(m_reliablePipeline, pair.Key, out DataStreamWriter writer) != 0) return false;
            writer.WriteByte((byte)BOOK_MESSAGE_TYPE.APPLY_DIRT);
            writer.WriteUInt(++m_obstacleEvent);
            writer.WriteByte(slotCount);
            return m_driver.EndSend(writer) >= 0;
        }
        return false;
    }
    public bool SendAddPage(PLAYER_SLOT playerSlot, byte pageId)
    {
        if (Owner != this) return Owner.SendAddPage(playerSlot, pageId);
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


    // 受信したCastMagicのデータを処理する
    private void ReceiveCastMagic(NetworkConnection connection, DataStreamReader reader)
    {
        if (!m_isGameStarted) return;
        // CastMagicのデータはSkillId(1byte) + CastDirection(1byte) = 2byte
        int remainingBytes = reader.Length - reader.GetBytesRead();

        if (remainingBytes != 2)
        {// 2byte以外は不正データとして無視する
            Debug.LogWarning($"CastMagicのデータ長が不正です : {remainingBytes}");
            return;
        }

        if (!m_playerSlots.TryGetValue(connection, out PLAYER_SLOT playerSlot))
        {// 未登録ControllerからのCastMagicは無視する
            Debug.LogWarning("未登録ControllerからCastMagicを受信しました");
            return;
        }

        // SkillIdとCastDirectionを読み取る
        byte skillId = reader.ReadByte();
        MAGIC_CAST_DIRECTION direction = (MAGIC_CAST_DIRECTION)reader.ReadByte();

        if (skillId == 0)
        {// SkillId 0は無効な値として無視する
            Debug.LogWarning("無効なSkillIdを受信しました");
            return;
        }

        if (direction != MAGIC_CAST_DIRECTION.NONE && direction != MAGIC_CAST_DIRECTION.LEFT && direction != MAGIC_CAST_DIRECTION.CENTER && direction != MAGIC_CAST_DIRECTION.RIGHT)
        {// CastDirectionが不正な値の場合は無視する
            Debug.LogWarning($"不正なCastDirection : {direction}");
            return;
        }

        // CastMagicの受信を通知する
        Debug.Log($"CastMagic受信 : {playerSlot} / SkillId={skillId} / Direction={direction}");
        m_castMagicReceived?.Invoke(playerSlot, skillId, direction);
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

        m_cursorPosition = value;

        if (m_playerSlots.TryGetValue(connection, out PLAYER_SLOT playerSlot))
        {
            Debug.Log($"{playerSlot} Vector2受信 : {value}");
        }
        else
        {
            Debug.LogWarning($"未登録ClientからVector2を受信 : {value}");
        }
    }

    public Vector2 GetCursorPosition()
    {
        return Owner.m_cursorPosition;
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

                m_registrationReplies[connection] = BOOK_MESSAGE_TYPE.REGISTRATION_REJECTED;
                return;
            }
        }

        m_playerSlots[connection] = playerSlot;
        m_registrationReplies[connection] = BOOK_MESSAGE_TYPE.REGISTRATION_ACCEPTED;

        Debug.Log($"Controller登録成功 : {playerSlot}");
    }

    private bool SendSessionMessage(NetworkConnection connection, BOOK_MESSAGE_TYPE type)
    {
        if (!IsConnectionAlive(connection) ||
            m_driver.BeginSend(m_reliablePipeline, connection, out DataStreamWriter writer) != 0) return false;
        writer.WriteByte((byte)type);
        return m_driver.EndSend(writer) >= 0;
    }

    private void SendSessionMessages()
    {
        // Retain messages if the reliable queue is temporarily full.
        m_replyBuffer.Clear();
        foreach (var pair in m_registrationReplies)
            if (SendSessionMessage(pair.Key, pair.Value)) m_replyBuffer.Add(pair.Key);
        foreach (var connection in m_replyBuffer) m_registrationReplies.Remove(connection);
        if (!m_isGameStarted) return;
        foreach (var pair in m_playerSlots)
        {
            if (m_registrationReplies.ContainsKey(pair.Key) || m_startNotified.Contains(pair.Key)) continue;
            if (SendSessionMessage(pair.Key, BOOK_MESSAGE_TYPE.GAME_STARTED))
            {
                m_startNotified.Add(pair.Key);
                m_pageDelivery[pair.Key] = (0, m_acquiredPages.Count);
            }
        }
    }

    private void SendAcquiredPages()
    {
        foreach (var pair in m_playerSlots)
        {
            NetworkConnection connection = pair.Key;
            if (!m_pageDelivery.TryGetValue(connection, out var delivery)) continue;
            while (delivery.Next < m_acquiredPages.Count)
            {
                if (!IsConnectionAlive(connection) ||
                    m_driver.BeginSend(m_reliablePipeline, connection, out DataStreamWriter writer) != 0) break;
                writer.WriteByte((byte)BOOK_MESSAGE_TYPE.ADD_PAGE);
                writer.WriteByte(m_acquiredPages[delivery.Next]);
                writer.WriteByte(delivery.Next < delivery.ReplayCount ? (byte)0 : (byte)1);
                if (m_driver.EndSend(writer) < 0) break;
                delivery.Next++;
            }
            m_pageDelivery[connection] = delivery;
        }
    }

    private void OnDestroy()
    {
        if (s_instance != this) return;
        s_instance = null;
        m_isListening = false;
        if (m_discovery != null) m_discovery.StopResponding();
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