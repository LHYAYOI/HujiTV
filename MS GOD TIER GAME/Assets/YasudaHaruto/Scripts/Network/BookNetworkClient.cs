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

    private float m_connectionTimer;

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
        // Unity TransportのNetworkDriverを作成
        m_driver = NetworkDriver.Create();

        // ReliableSequencedPipelineStageを使用して信頼性のあるパイプラインを作成
        m_reliablePipeline = m_driver.CreatePipeline(typeof(ReliableSequencedPipelineStage));

        SetConnectionState(BOOK_CONNECTION_STATE.DISCONNECTED);
    }

    private void Update()
    {
        if (!m_driver.IsCreated)
        {// ドライバーが作成されていない場合は更新しない
            return;
        }

        // ドライバーの更新をスケジュールして完了させる
        m_driver.ScheduleUpdate().Complete();

        if (!m_connection.IsCreated)
        {// 接続が作成されていない場合は更新しない
            return;
        }

        NetworkEvent.Type eventType;

        // 接続イベントを処理するループ
        while ((eventType = m_connection.PopEvent(m_driver, out DataStreamReader reader)) != NetworkEvent.Type.Empty)
        {
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

        // 接続タイマーを更新
        if (ConnectionState == BOOK_CONNECTION_STATE.CONNECTING)
        {
            m_connectionTimer += Time.deltaTime;
            if (m_connectionTimer < m_connectionTimeoutSeconds)
            {
                return;
            }

            Debug.LogWarning("Serverへの接続がタイムアウトしました");
            Disconnect();
        }
    }

    public void Connect(string ipAddress, PLAYER_SLOT playerSlot)
    {
        if (ConnectionState != BOOK_CONNECTION_STATE.DISCONNECTED)
        {
            Debug.LogWarning("すでに接続中、または接続済みです");
            return;
        }

        if (playerSlot == PLAYER_SLOT.NONE)
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

        m_playerSlot = playerSlot;

        Debug.Log($"Serverへ接続開始 : {ipAddress}:{Port}");

        // 接続を開始
        m_connection = m_driver.Connect(endpoint);

        // 接続タイマーをリセット
        m_connectionTimer = 0.0f;

        // 接続状態を更新
        SetConnectionState(BOOK_CONNECTION_STATE.CONNECTING);
    }

    public void Disconnect()
    {
        if (m_driver.IsCreated && m_connection.IsCreated)
        {
            m_driver.Disconnect(m_connection);
            m_driver.ScheduleUpdate().Complete();
        }

        m_connection = default;

        m_connectionTimer = 0.0f;

        SetConnectionState(BOOK_CONNECTION_STATE.DISCONNECTED);

        Debug.Log("Book Serverから切断しました");
    }

    private void OnConnected()
    {
        m_connectionTimer = 0.0f;

        Debug.Log($"Book Serverへ接続成功 : {m_playerSlot}");

        SetConnectionState(BOOK_CONNECTION_STATE.CONNECTED);

        SendRegisterController();
    }

    private void OnDisconnected()
    {
        Debug.Log("Book Serverから切断されました");

        m_connection = default;
        m_connectionTimer = 0.0f;

        // 接続状態を更新
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

    public bool SendCastSkill(byte skillId)
    {
        if (!CanSend())
        {
            Debug.LogWarning("未接続のためSkillを送信できません");
            return false;
        }

        if (skillId == 0)
        {
            Debug.LogWarning("SkillId 0は無効です");
            return false;
        }

        int beginResult = m_driver.BeginSend(m_reliablePipeline, m_connection, out DataStreamWriter writer);

        if (beginResult != 0)
        {
            Debug.LogError($"CastSkill BeginSend失敗 : {beginResult}");
            return false;
        }

        writer.WriteByte((byte)BOOK_MESSAGE_TYPE.CAST_MAGIC);

        writer.WriteByte(skillId);

        int endResult = m_driver.EndSend(writer);

        if (endResult < 0)
        {
            Debug.LogError($"CastSkill EndSend失敗 : {endResult}");
            return false;
        }

        Debug.Log($"CastSkill送信 : SkillId={skillId}");

        return true;
    }
    private bool CanSend()
    {
        // 接続状態がCONNECTEDであり、接続が作成されている場合に送信可能
        return ConnectionState == BOOK_CONNECTION_STATE.CONNECTED && m_connection.IsCreated;
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

    private void ReceiveTestPong(
        DataStreamReader reader)
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