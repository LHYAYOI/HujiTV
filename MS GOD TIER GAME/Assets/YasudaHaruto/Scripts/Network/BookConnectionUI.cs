//-----------------------------------------------
// BookConnectionUI.cs
// 制作日：2026/09/15
// 制作者：安田晴人
// 概要： 本のページめくりをUnity Transportを使用してサーバーに送信するクライアントの接続UI
//-----------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BookConnectionUI : MonoBehaviour
{
    [SerializeField]
    private BookNetworkClient m_networkClient;

    [SerializeField]
    private TMP_InputField m_ipInputField;

    [SerializeField]
    private TMP_Text m_statusText;

    [SerializeField]
    private Button m_connectButton;

    [SerializeField]
    private TMP_Dropdown m_playerDropdown;

    private void Start()
    {
        if (m_networkClient != null)
        {
            m_networkClient.OnConnectionStateChanged += OnConnectionStateChanged;

            UpdateStatus(m_networkClient.ConnectionState);
        }
    }

    public void OnConnectButtonPressed()
    {
        if (m_networkClient == null || m_ipInputField == null || m_playerDropdown == null)
        {
            return;
        }

        string ipAddress = m_ipInputField.text;

        PLAYER_SLOT playerSlot = (PLAYER_SLOT)(m_playerDropdown.value + 1);

        m_networkClient.Connect(ipAddress, playerSlot);
    }

    private void OnConnectionStateChanged(BOOK_CONNECTION_STATE state)
    {
        UpdateStatus(state);
    }

    private void UpdateStatus(BOOK_CONNECTION_STATE state)
    {
        if (m_statusText == null)
        {
            return;
        }

        switch (state)
        {
            case BOOK_CONNECTION_STATE.DISCONNECTED:

                m_statusText.text = "Status : Disconnected";

                if (m_connectButton != null)
                {
                    m_connectButton.interactable = true;
                }

                if (m_playerDropdown != null)
                {
                    m_playerDropdown.interactable = true;
                }

                break;

            case BOOK_CONNECTION_STATE.CONNECTING:

                m_statusText.text = "Status : Connecting...";

                if (m_connectButton != null)
                {
                    m_connectButton.interactable = false;
                }

                if (m_playerDropdown != null)
                {
                    m_playerDropdown.interactable = false;
                }

                break;

            case BOOK_CONNECTION_STATE.CONNECTED:

                m_statusText.text = "Status : Connected";

                if (m_connectButton != null)
                {
                    m_connectButton.interactable = false;
                }

                if (m_playerDropdown != null)
                {
                    m_playerDropdown.interactable = false;
                }

                break;
        }
    }

    private void OnDestroy()
    {
        if (m_networkClient != null)
        {
            m_networkClient.OnConnectionStateChanged -= OnConnectionStateChanged;
        }
    }
}