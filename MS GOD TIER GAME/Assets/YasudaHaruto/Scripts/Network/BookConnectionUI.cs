//-----------------------------------------------
// BookConnectionUI.cs
// 制作日：2026/09/15
// 制作者：安田晴人
// 概要： 本のページめくりをUnity Transportを使用してサーバーに送信するクライアントの接続UI
//-----------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

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

    [SerializeField]
    private string m_bookSceneName = "BookScene";

    private ServerDiscoveryClient m_discovery;
    private string m_initialIp;
    private bool m_ipEdited;

    private int m_testPingValue;
    private bool m_shouldLoadBookScene;

    private void Start()
    {
        if (m_networkClient != null)
        {
            m_networkClient.OnConnectionStateChanged += OnConnectionStateChanged;
            m_networkClient.GameStarted += OnGameStarted;
            m_shouldLoadBookScene = m_networkClient.HasGameStarted;

            UpdateStatus(m_networkClient.ConnectionState);
            m_discovery = gameObject.AddComponent<ServerDiscoveryClient>();
            m_discovery.ServerFound += OnServerFound;
            m_discovery.SearchFinished += OnSearchFinished;
            if (m_ipInputField != null)
            {
                m_initialIp = m_ipInputField.text;
                m_ipInputField.onValueChanged.AddListener(OnIpEdited);
            }
            OnSearchServerButtonPressed();
        }
    }

    // Optional button binding for retrying discovery without changing scenes.
    public void OnSearchServerButtonPressed()
    {
        if (m_discovery == null || m_networkClient == null ||
            m_networkClient.ConnectionState != BOOK_CONNECTION_STATE.DISCONNECTED || m_networkClient.IsReconnecting) return;
        m_ipEdited = false;
        if (m_ipInputField != null) m_initialIp = m_ipInputField.text;
        m_discovery.BeginSearch();
        if (m_discovery.IsSearching && m_statusText != null)
            m_statusText.text = "Searching server... (manual IP available)";
    }

    private void OnIpEdited(string value) { m_ipEdited = true; }

    private void OnServerFound(string address)
    {
        if (m_networkClient == null || m_networkClient.ConnectionState != BOOK_CONNECTION_STATE.DISCONNECTED) return;
        if (m_ipInputField != null && !m_ipEdited && m_ipInputField.text == m_initialIp)
        {
            m_ipInputField.SetTextWithoutNotify(address);
            if (m_statusText != null) m_statusText.text = "Server found: " + address + " - select Player and Connect";
        }
        else if (m_statusText != null)
            m_statusText.text = "Server found: " + address + " (manual IP preserved)";
    }

    private void OnSearchFinished(string message)
    {
        if (m_statusText != null && m_networkClient != null &&
            m_networkClient.ConnectionState == BOOK_CONNECTION_STATE.DISCONNECTED)
            m_statusText.text = message;
    }

    private void LateUpdate()
    {
        if (!m_shouldLoadBookScene || m_networkClient == null || !m_networkClient.HasGameStarted)
        {
            return;
        }

        m_shouldLoadBookScene = false;

        SceneManager.LoadScene(m_bookSceneName);
    }

    public void OnConnectButtonPressed()
    {
        if (m_networkClient == null || m_ipInputField == null || m_playerDropdown == null)
        {
            return;
        }

        m_discovery?.StopSearch();
        string ipAddress = m_ipInputField.text;

        PLAYER_SLOT playerSlot = (PLAYER_SLOT)(m_playerDropdown.value + 1);

        m_networkClient.Connect(ipAddress, playerSlot);
    }

    private void OnConnectionStateChanged(BOOK_CONNECTION_STATE state)
    {
        if (state != BOOK_CONNECTION_STATE.DISCONNECTED) m_discovery?.StopSearch();
        UpdateStatus(state);

        if (state != BOOK_CONNECTION_STATE.CONNECTED) m_shouldLoadBookScene = false;
    }

    private void OnGameStarted() { m_shouldLoadBookScene = true; }

    private void UpdateStatus(BOOK_CONNECTION_STATE state)
    {
        if (m_statusText == null)
        {
            return;
        }

        switch (state)
        {
            case BOOK_CONNECTION_STATE.DISCONNECTED:

                m_statusText.text = string.IsNullOrEmpty(m_networkClient.LastConnectionError) ? "Status : Disconnected" : m_networkClient.LastConnectionError;

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

                m_statusText.text = "Status : Connecting / Registering...";

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

                m_statusText.text = "Connected - waiting for PC to start the game";

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

    public void OnTestPingButtonPressed()
    {
        if (m_networkClient == null)
        {
            return;
        }

        m_testPingValue++;

        m_networkClient.SendTestPing(m_testPingValue);
    }

    private void OnDestroy()
    {
        if (m_ipInputField != null) m_ipInputField.onValueChanged.RemoveListener(OnIpEdited);
        if (m_discovery != null)
        {
            m_discovery.ServerFound -= OnServerFound;
            m_discovery.SearchFinished -= OnSearchFinished;
            m_discovery.StopSearch();
        }
        if (m_networkClient != null)
        {
            m_networkClient.OnConnectionStateChanged -= OnConnectionStateChanged;
            m_networkClient.GameStarted -= OnGameStarted;
        }
    }
}