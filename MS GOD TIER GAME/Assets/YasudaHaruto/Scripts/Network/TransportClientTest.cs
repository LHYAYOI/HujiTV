//-----------------------------------------------
// TransportClientTest.cs
// 制作日：2026/09/10
// 制作者：安田晴人
// 概要：Unity Transportを使用したクライアントのテストスクリプト
//-----------------------------------------------
using Unity.Collections;
using Unity.Networking.Transport;
using UnityEngine;
using Unity.Networking.Transport.Utilities;

public class TransportClientTest : MonoBehaviour
{
    private const ushort Port = 7777;

    private NetworkDriver m_driver;
    private NetworkConnection m_connection;
    private NetworkPipeline m_reliablePipeline;

    private bool m_connectionFinished;
    private bool m_isConnected;

    [SerializeField]
    private float m_mouseSensitivity = 0.15f;

    [SerializeField]
    private float m_orientationSendRate = 30.0f;

    private float m_yaw;
    private float m_pitch;

    private float m_orientationSendTimer;

    private Quaternion m_currentOrientation = Quaternion.identity;

    private void Start()
    {
        m_driver = NetworkDriver.Create();

        m_reliablePipeline = m_driver.CreatePipeline(typeof(ReliableSequencedPipelineStage));

        // 127.0.0.1 = このPC自身
        NetworkEndpoint endpoint = NetworkEndpoint.LoopbackIpv4.WithPort(Port);

        m_connection = m_driver.Connect(endpoint);

        Debug.Log($"Serverへ接続開始 : 127.0.0.1:{Port}");
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
            if (!m_connectionFinished)
            {
                Debug.LogError("Serverへ接続できませんでした");
                m_connectionFinished = true;
            }

            return;
        }

        NetworkEvent.Type eventType;

        while ((eventType = m_connection.PopEvent(m_driver, out DataStreamReader reader)) != NetworkEvent.Type.Empty)
        {
            switch (eventType)
            {
                case NetworkEvent.Type.Connect:
                    OnConnected();
                    break;

                case NetworkEvent.Type.Data:
                    OnReceiveData(reader);
                    break;

                case NetworkEvent.Type.Disconnect:
                    OnDisconnected();
                    break;
            }
        }

        if (m_isConnected)
        {
            UpdateMockOrientation();
            UpdateOrientationSend();
        }
    }

    private void UpdateMockOrientation()
    {
        float mouseX =
            Input.GetAxis("Mouse X");

        float mouseY =
            Input.GetAxis("Mouse Y");

        m_yaw +=
            mouseX * m_mouseSensitivity;

        m_pitch -=
            mouseY * m_mouseSensitivity;
        m_pitch =
            Mathf.Clamp(
                m_pitch,
                -80.0f,
                80.0f);

        m_currentOrientation =
            Quaternion.Euler(
                m_pitch,
                m_yaw,
                0.0f);
    }

    private void UpdateOrientationSend()
    {
        m_orientationSendTimer += Time.deltaTime;

        float sendInterval = 1.0f / m_orientationSendRate;
        if (m_orientationSendTimer < sendInterval)
        {
            return;
        }

        m_orientationSendTimer -= sendInterval;

        SendOrientation(m_currentOrientation);
    }

    // Serverへの接続が成功したときに呼ばれる
    private void OnConnected()
    {
        Debug.Log("Serverへの接続成功");

        m_isConnected = true;
        m_connectionFinished = true;
    }

    // Unity UIのButtonから呼ぶ
    public void SendButtonPressed(int buttonId)
    {
        if (!m_connection.IsCreated)
        {
            Debug.LogWarning("Serverに接続されていません");
            return;
        }

        // 今回ButtonIDは1byteで送るので0～255まで
        if (buttonId < byte.MinValue || buttonId > byte.MaxValue)
        {
            Debug.LogError($"不正なButtonIDです : {buttonId}");
            return;
        }

        int beginResult = m_driver.BeginSend(m_reliablePipeline, m_connection, out DataStreamWriter writer);

        if (beginResult != 0)
        {
            Debug.LogError($"BeginSend失敗 : {beginResult}");
            return;
        }

        // 最初に「これは何のデータか」を書く
        writer.WriteByte((byte)BOOK_MESSAGE_TYPE.BUTTON_PRESSED);

        // 次にButtonPressed固有のデータを書く
        writer.WriteByte((byte)buttonId);

        int endResult = m_driver.EndSend(writer);

        if (endResult < 0)
        {
            Debug.LogError($"EndSend失敗 : {endResult}");
            return;
        }

        Debug.Log($"Button送信 : ID = {buttonId}");
    }

    private void SendOrientation(
    Quaternion rotation)
    {
        if (!m_isConnected ||
            !m_connection.IsCreated)
        {
            return;
        }

        // Pipelineを指定しない
        // = 通常のUnreliable通信
        int beginResult =
            m_driver.BeginSend(
                m_connection,
                out DataStreamWriter writer);

        if (beginResult != 0)
        {
            Debug.LogError(
                $"Orientation BeginSend失敗 : {beginResult}");
            return;
        }

        writer.WriteByte(
            (byte)BOOK_MESSAGE_TYPE.ORIENTATION);

        writer.WriteFloat(rotation.x);
        writer.WriteFloat(rotation.y);
        writer.WriteFloat(rotation.z);
        writer.WriteFloat(rotation.w);

        int endResult =
            m_driver.EndSend(writer);

        if (endResult < 0)
        {
            Debug.LogError(
                $"Orientation EndSend失敗 : {endResult}");
        }
    }

    private void OnReceiveData(DataStreamReader reader)
    {
        Debug.Log($"Serverからデータ受信 : {reader.Length} byte");
    }

    // Serverから切断されたときに呼ばれる
    private void OnDisconnected()
    {
        Debug.Log("Serverから切断されました");

        m_connection = default;
        m_connectionFinished = true;
        m_isConnected = false;
    }

    private void OnDestroy()
    {
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