//-----------------------------------------------
// BookGameBootstrap.cs
// 制作日：2026/10/21
// 制作者：安田晴人
// 概要： 本の初期化と管理
//-----------------------------------------------
using UnityEngine;

namespace YasudaHaruto.BookValidation
{
public class BookGameBootstrap : MonoBehaviour
{
    [Header("Book")]
    [SerializeField] private BookGameController m_gameController;

    [Header("Rune")]
    [SerializeField] private RunePageView m_runeTraceView;
    [SerializeField, Min(0f)] private float m_runeCooldownSeconds = 1f;

    [Header("Input")]
    [SerializeField] private BookGameDragInput m_gameDragInput;

    [Header("Obstacles")]
    [SerializeField] private ObstacleData[] m_obstacles = new ObstacleData[0];
    [SerializeField] private Rect m_rightObstacleArea = new Rect(0.1f, 0.15f, 0.8f, 0.7f);
    private BookObstacleController m_obstacleController;
    private RuneTraceController m_runeTraceController;
    [SerializeField] private PageDatabase m_pageDatabase;
    private BookNetworkClient m_networkClient;
    private BookPageRequestQueue m_pageRequests;

    private void Start()
    {
        if (m_gameController == null || m_runeTraceView == null || m_gameDragInput == null)
        {
            Debug.LogError("BookGameBootstrap : 本・ルーン表示・入力の参照を設定してください", this);
            return;
        }
        BookInputController inputController = new BookInputController();

        m_runeTraceController = new RuneTraceController(m_runeTraceView);
        m_runeTraceController.SetCooldown(m_runeCooldownSeconds);

        m_runeTraceView.ConfigureCooldownImage(() => m_gameController != null ? m_gameController.GetCurrentRuneImage() : null);
        m_runeTraceView.ConfigureScreenProjection(m_gameDragInput.GetRuneScreenRect, m_gameDragInput.GetGraphicScreenRect);
        m_gameDragInput.SetInventoryOwner(m_gameController);
        m_gameDragInput.ConfigureSpellPresentation(m_runeTraceView);

        BookNetworkClient networkClient = BookNetworkClient.Instance;
        BookNetworkService networkService = networkClient != null ? new BookNetworkService(networkClient) : null;

        PageFactory pageFactory = new PageFactory(m_runeTraceController, inputController, networkService);

        if (m_gameController == null || !m_gameController.Initialize(pageFactory,
            () => !m_gameDragInput.IsGestureActive && inputController.State == BOOK_INPUT_STATE.NORMAL &&
                (m_runeTraceController.State == RUNE_TRACE_STATE.INACTIVE ||
                 m_runeTraceController.State == RUNE_TRACE_STATE.WAITING_STROKE)))
        {
            Debug.LogError("BookGameBootstrap : 初期化失敗", this);
            return;
        }

        m_gameDragInput.Initialize(m_runeTraceController, inputController);
        m_pageRequests = new BookPageRequestQueue(m_pageDatabase, m_gameController);
        Debug.Log("BookGameBootstrap : 初期化完了");
        m_obstacleController = gameObject.AddComponent<BookObstacleController>();
        m_obstacleController.Initialize(m_gameController, m_gameDragInput, m_obstacles, m_rightObstacleArea);
        m_networkClient = networkClient;
        if (m_networkClient != null)
        {
            m_networkClient.AddPageReceived += m_pageRequests.Receive;
            m_networkClient.ObstacleReceived += m_obstacleController.Receive;
            m_networkClient.OnConnectionStateChanged += OnConnectionStateChanged;
            OnConnectionStateChanged(m_networkClient.ConnectionState);
            gameObject.AddComponent<BookConnectionStatusView>().Initialize(m_networkClient);
        }
    }

    private void Update()
    {
        m_runeTraceController?.Tick(Time.deltaTime);
        m_pageRequests?.Tick();
    }

    private void OnConnectionStateChanged(BOOK_CONNECTION_STATE state)
    {
        m_obstacleController.SetConnected(state == BOOK_CONNECTION_STATE.CONNECTED);
    }

    private void OnDestroy()
    {
        if (m_networkClient != null)
        {
            if (m_pageRequests != null) m_networkClient.AddPageReceived -= m_pageRequests.Receive;
            if (m_obstacleController != null) m_networkClient.ObstacleReceived -= m_obstacleController.Receive;
            m_networkClient.OnConnectionStateChanged -= OnConnectionStateChanged;
        }
        m_pageRequests?.Clear();
        if (m_gameController != null)
        {
            m_gameController.Shutdown();
        }
    }
}

}
