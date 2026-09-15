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
    [SerializeField] private BookController m_bookController;

    private const ushort Port = 7777;

    private NetworkDriver m_driver;
    private NetworkConnection m_connection;
    private NetworkPipeline m_reliablePipeline;

    public BOOK_CONNECTION_STATE ConnectionState { get; private set; } = BOOK_CONNECTION_STATE.DISCONNECTED;

    public event Action<BOOK_CONNECTION_STATE> OnConnectionStateChanged;

    private void Start()
    {
        // Unity TransportのNetworkDriverを作成
        m_driver = NetworkDriver.Create();

        // ReliableSequencedPipelineStageを使用して信頼性のあるパイプラインを作成
        m_reliablePipeline = m_driver.CreatePipeline(typeof(ReliableSequencedPipelineStage));

        // BookControllerのイベントに登録
        if (m_bookController != null)
        {
            m_bookController.OnPageChanged += OnPageChanged;
            m_bookController.OnCastRequested += OnCastRequested;
        }

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
    }

    public void Connect(string ipAddress)
    {
        if (ConnectionState != BOOK_CONNECTION_STATE.DISCONNECTED)
        {
            Debug.LogWarning("すでに接続中、または接続済みです");
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

        Debug.Log($"Serverへ接続開始 : {ipAddress}:{Port}");

        // 接続を開始
        m_connection = m_driver.Connect(endpoint);

        // 接続状態を更新
        SetConnectionState(BOOK_CONNECTION_STATE.CONNECTING);
    }

    private void OnConnected()
    {
        Debug.Log("Book Serverへ接続成功");

        // 接続状態を更新
        SetConnectionState(BOOK_CONNECTION_STATE.CONNECTED);

        // 接続直後に現在ページを同期
        if (m_bookController != null)
        {
            SendPageChanged(m_bookController.CurrentPage);
        }
    }

    private void OnDisconnected()
    {
        Debug.Log("Book Serverから切断されました");

        m_connection = default;

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

    private void OnPageChanged(int pageIndex)
    {
        SendPageChanged(pageIndex);
    }

    private void OnCastRequested(int pageIndex)
    {
        SendCastRequest(pageIndex);
    }

    private void SendPageChanged(int pageIndex)
    {
        if (!CanSend())
        {
            return;
        }

        // BeginSendを呼び出して送信の準備を行う
        int beginResult = m_driver.BeginSend(m_reliablePipeline, m_connection, out DataStreamWriter writer);

        if (beginResult != 0)
        {
            Debug.LogError($"PageChanged BeginSend失敗 : {beginResult}");

            return;
        }

        // メッセージタイプを送信
        writer.WriteByte((byte)BOOK_MESSAGE_TYPE.PAGE_CHANGED);

        // ページ番号を送信
        writer.WriteInt(pageIndex);

        // EndSendを呼び出して送信を完了する
        int endResult = m_driver.EndSend(writer);

        if (endResult < 0)
        {
            Debug.LogError($"PageChanged EndSend失敗 : {endResult}");

            return;
        }

        Debug.Log($"PageChanged送信 : {pageIndex}");
    }

    private void SendCastRequest(int pageIndex)
    {
        if (!CanSend())
        {// 送信可能か確認
            return;
        }

        // BeginSendを呼び出して送信の準備を行う
        int beginResult = m_driver.BeginSend(m_reliablePipeline, m_connection, out DataStreamWriter writer);

        if (beginResult != 0)
        {
            Debug.LogError($"CastRequest BeginSend失敗 : {beginResult}");
            return;
        }

        // メッセージタイプを送信
        writer.WriteByte((byte)BOOK_MESSAGE_TYPE.CAST_REQUEST);

        // ページ番号を送信
        writer.WriteInt(pageIndex);

        int endResult = m_driver.EndSend(writer);

        if (endResult < 0)
        {
            Debug.LogError($"CastRequest EndSend失敗 : {endResult}");
            return;
        }

        Debug.Log($"CastRequest送信 : Page {pageIndex}");
    }

    private bool CanSend()
    {
        // 接続状態がCONNECTEDであり、接続が作成されている場合に送信可能
        return ConnectionState == BOOK_CONNECTION_STATE.CONNECTED && m_connection.IsCreated;
    }

    private void ReceiveData(DataStreamReader reader)
    {
        // PC → Book通信は今後ここで処理
    }

    private void OnDestroy()
    {
        if (m_bookController != null)
        {
            // BookControllerのイベントから登録解除
            m_bookController.OnPageChanged -= OnPageChanged;
            m_bookController.OnCastRequested -= OnCastRequested;
        }

        if (!m_driver.IsCreated)
        {
            return;
        }

        if (m_connection.IsCreated)
        {
            // 接続が作成されている場合は切断
            m_driver.Disconnect(m_connection);
            m_driver.ScheduleUpdate().Complete();
        }

        m_driver.Dispose();
    }
}