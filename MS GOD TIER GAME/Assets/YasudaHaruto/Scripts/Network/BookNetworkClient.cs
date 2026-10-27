//-----------------------------------------------
// BookNetworkClient.cs
// 制作日：2026/09/11
// 制作者：安田晴人
// 概要：本のページめくりをUnity Transportを使用してサーバーに送信するクライアント
//-----------------------------------------------
using System;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using UnityEngine;

public class BookNetworkClient : MonoBehaviour
{
    private static BookNetworkClient s_instance;

    public static BookNetworkClient Instance => s_instance;

    [SerializeField] private float m_connectionTimeoutSeconds = 5.0f;

    private const ushort Port = 7777;

    private NetworkDriver m_driver;
    private NetworkConnection m_connection;
    private NetworkPipeline m_reliablePipeline;

    private PLAYER_SLOT m_playerSlot = PLAYER_SLOT.NONE;

    private float m_connectionDeadline;
    private NetworkEndpoint m_endpoint;
    private bool m_shouldReconnect;
    private float m_nextReconnectTime;
    private float m_reconnectDelay = 1f;

    public bool IsReconnecting => m_shouldReconnect && ConnectionState != BOOK_CONNECTION_STATE.CONNECTED;

    public BOOK_CONNECTION_STATE ConnectionState { get; private set; } = BOOK_CONNECTION_STATE.DISCONNECTED;

    public event Action<BOOK_CONNECTION_STATE> OnConnectionStateChanged;
    public event System.Action<byte> AddPageReceived;


    private void Awake()
    {
        if (s_instance != null && s_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        s_instance = this;

        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        InitializeDriver();
    }

    private void InitializeDriver()
    {
        if (m_driver.IsCreated) return;
        m_driver = NetworkDriver.Create();
        m_reliablePipeline = m_driver.CreatePipeline(typeof(ReliableSequencedPipelineStage));
    }

    private void Update()
    {
        if (!m_driver.IsCreated) return;
        m_driver.ScheduleUpdate().Complete();

        // Finish processing the old connection before a retry replaces its handle.
        while (m_connection.IsCreated)
        {
            NetworkEvent.Type eventType = m_connection.PopEvent(m_driver, out DataStreamReader reader);
            if (eventType == NetworkEvent.Type.Empty) break;
            switch (eventType)
            {
                case NetworkEvent.Type.Connect:
                    OnConnected();
                    break;
                case NetworkEvent.Type.Data:
                    ReceiveData(reader);
                    break;
                case NetworkEvent.Type.Disconnect:
                    OnDisconnected();
                    break;
            }
        }

        if (ConnectionState == BOOK_CONNECTION_STATE.CONNECTING && Time.unscaledTime >= m_connectionDeadline)
        {
            Debug.LogWarning("Book server connection timed out; retry scheduled.");
            CloseConnection();
            OnDisconnected();
        }
        else if (ConnectionState == BOOK_CONNECTION_STATE.DISCONNECTED && m_shouldReconnect &&
                 Time.unscaledTime >= m_nextReconnectTime)
        {
            BeginConnection();
        }
    }

    public void Connect(string ipAddress, PLAYER_SLOT playerSlot)
    {
        if (ConnectionState != BOOK_CONNECTION_STATE.DISCONNECTED)
        {
            Debug.LogWarning("すでに接続中、または接続済みです");
            return;
        }

        if (playerSlot != PLAYER_SLOT.PLAYER1 && playerSlot != PLAYER_SLOT.PLAYER2)
        {
            Debug.LogWarning("Playerが選択されていません");
            return;
        }

        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            Debug.LogWarning("IPアドレスを入力してください");
            return;
        }

        // 前後の空白を削除
        ipAddress = ipAddress.Trim();

        // ユーザー入力なのでParseではなくTryParseを使う
        if (!NetworkEndpoint.TryParse(ipAddress, Port, out NetworkEndpoint endpoint, NetworkFamily.Ipv4))
        {
            Debug.LogWarning($"IPアドレスが不正です : {ipAddress}");
            return;
        }

        InitializeDriver();
        m_playerSlot = playerSlot;
        m_endpoint = endpoint;
        m_shouldReconnect = true;
        m_reconnectDelay = 1f;
        BeginConnection();
    }

    private void BeginConnection()
    {
        m_connection = m_driver.Connect(m_endpoint);
        m_connectionDeadline = Time.unscaledTime + Mathf.Max(0.1f, m_connectionTimeoutSeconds);
        if (!m_connection.IsCreated)
        {
            OnDisconnected();
            return;
        }
        SetConnectionState(BOOK_CONNECTION_STATE.CONNECTING);
    }

    public void Disconnect()
    {
        // Explicit disconnect also cancels all future retries.
        m_shouldReconnect = false;
        CloseConnection();
        SetConnectionState(BOOK_CONNECTION_STATE.DISCONNECTED);
    }

    private void CloseConnection()
    {
        if (m_driver.IsCreated && m_connection.IsCreated)
        {
            m_driver.Disconnect(m_connection);
            m_driver.ScheduleUpdate().Complete();
        }
        m_connection = default;
    }

    private void OnConnected()
    {
        m_reconnectDelay = 1f;
        SetConnectionState(BOOK_CONNECTION_STATE.CONNECTED);
        SendRegisterController();
    }

    private void OnDisconnected()
    {
        m_connection = default;
        m_nextReconnectTime = Time.unscaledTime + m_reconnectDelay;
        m_reconnectDelay = Mathf.Min(8f, m_reconnectDelay * 2f);
        SetConnectionState(BOOK_CONNECTION_STATE.DISCONNECTED);
    }

    private void SetConnectionState(BOOK_CONNECTION_STATE state)
    {
        if (ConnectionState == state)
        {
            return;
        }

        // 接続状態を更新
        ConnectionState = state;

        // 接続状態変更イベントを通知
        OnConnectionStateChanged?.Invoke(state);
    }


    private void SendRegisterController()
    {
        if (!CanSend())
        {
            return;
        }

        int beginResult = m_driver.BeginSend(m_reliablePipeline, m_connection, out DataStreamWriter writer);

        if (beginResult != 0)
        {
            Debug.LogError($"RegisterController BeginSend失敗 : {beginResult}");

            return;
        }

        writer.WriteByte((byte)BOOK_MESSAGE_TYPE.REGISTER_CONTROLLER);

        writer.WriteByte((byte)m_playerSlot);

        int endResult = m_driver.EndSend(writer);

        if (endResult < 0)
        {
            Debug.LogError($"RegisterController EndSend失敗 : {endResult}");
            return;
        }

        Debug.Log($"Controller登録送信 : {m_playerSlot}");
    }

    public void SendTestPing(int value)
    {
        if (!CanSend())
        {
            Debug.LogWarning("未接続のためTestPingを送信できません");
            return;
        }

        int beginResult = m_driver.BeginSend(m_reliablePipeline, m_connection, out DataStreamWriter writer);

        if (beginResult != 0)
        {
            Debug.LogError($"TestPing BeginSend失敗 : {beginResult}");

            return;
        }

        writer.WriteByte((byte)BOOK_MESSAGE_TYPE.TEST_PING);

        writer.WriteInt(value);

        int endResult = m_driver.EndSend(writer);

        if (endResult < 0)
        {
            Debug.LogError($"TestPing EndSend失敗 : {endResult}");

            return;
        }

        Debug.Log($"TestPing送信 : {value}");
    }

    // CastSkillメッセージを送信する
    public bool SendCastSkill(byte skillId, MAGIC_CAST_DIRECTION direction)
    {
        if (!CanSend())
        {// 接続状態がCONNECTEDでない場合は送信できない
            Debug.LogWarning("未接続のためSkillを送信できません");
            return false;
        }

        if (skillId == 0)
        {// SkillIdが0の場合は無効な値として送信しない
            Debug.LogWarning("SkillId 0は無効です");
            return false;
        }

        // CastSkillメッセージの送信を開始
        int beginResult = m_driver.BeginSend(m_reliablePipeline, m_connection, out DataStreamWriter writer);

        if (beginResult != 0)
        {// 送信開始に失敗した場合はエラーログを出力してfalseを返す
            Debug.LogError($"CastSkill BeginSend失敗 : {beginResult}");
            return false;
        }

        // CastSkillメッセージの送信
        writer.WriteByte((byte)BOOK_MESSAGE_TYPE.CAST_MAGIC);
        writer.WriteByte(skillId);
        writer.WriteByte((byte)direction);

        // 送信を終了
        int endResult = m_driver.EndSend(writer);

        if (endResult < 0)
        {// 送信終了に失敗した場合はエラーログを出力してfalseを返す
            Debug.LogError($"CastSkill EndSend失敗 : {endResult}");
            return false;
        }

        Debug.Log($"CastSkill送信 : SkillId={skillId}");

        return true;
    }

    public void SendVector2(Vector2 value)
    {
        if (!CanSend())
        {
            return;
        }

        int beginResult = m_driver.BeginSend(m_connection, out DataStreamWriter writer);

        if (beginResult != 0)
        {
            Debug.LogError($"Vector2 BeginSend失敗 : {beginResult}");
            return;
        }

        writer.WriteByte((byte)BOOK_MESSAGE_TYPE.CURSOR_POSITION);
        writer.WriteFloat(value.x);
        writer.WriteFloat(value.y);

        int endResult = m_driver.EndSend(writer);

        if (endResult < 0)
        {
            Debug.LogError($"Vector2 EndSend失敗 : {endResult}");
            return;
        }

        Debug.Log($"Vector2送信 : {value}");
    }

    private bool CanSend()
    {
        // 接続状態がCONNECTEDであり、接続が作成されている場合に送信可能
        return m_driver.IsCreated && ConnectionState == BOOK_CONNECTION_STATE.CONNECTED && m_connection.IsCreated;
    }

    private void ReceiveData(DataStreamReader reader)
    {
        if (reader.Length < 1)
        {
            return;
        }

        BOOK_MESSAGE_TYPE messageType =
            (BOOK_MESSAGE_TYPE)reader.ReadByte();

        switch (messageType)
        {
            case BOOK_MESSAGE_TYPE.TEST_PONG:
                ReceiveTestPong(reader);
                break;

            case BOOK_MESSAGE_TYPE.ADD_PAGE:
                ReceiveAddPage(reader);
                break;

            default:
                Debug.LogWarning(
                    $"未対応Message : {messageType}");
                break;
        }
    }

    private void ReceiveTestPong(DataStreamReader reader)
    {
        int remainingBytes =
            reader.Length - reader.GetBytesRead();

        if (remainingBytes < 4)
        {
            Debug.LogWarning(
                "TestPongのデータが不足しています");

            return;
        }

        int value = reader.ReadInt();

        Debug.Log($"TestPong受信 : {value}");
    }

    private void ReceiveAddPage(DataStreamReader reader)
    {
        if (reader.GetBytesRead() + 1 > reader.Length)
        {
            Debug.LogWarning("ADD_PAGEのデータが不足しています");
            return;
        }

        byte pageId = reader.ReadByte();

        Debug.Log($"AddPage受信 : PageId={pageId}");

        AddPageReceived?.Invoke(pageId);
    }
    private void OnDestroy()
    {
        if (s_instance != this)
        {
            return;
        }

        m_shouldReconnect = false;
        SetConnectionState(BOOK_CONNECTION_STATE.DISCONNECTED);
        s_instance = null;

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