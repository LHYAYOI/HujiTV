//-----------------------------------------------
// BookGameBootstrap.cs
// 制作日：2026/10/21
// 制作者：安田晴人
// 概要： 本の初期化と管理
//-----------------------------------------------
using UnityEngine;
using System.Collections.Generic;

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

    private RuneTraceController m_runeTraceController;
    [SerializeField] private PageDatabase m_pageDatabase;
    private BookNetworkClient m_networkClient;
    private readonly Queue<PageData> m_requests = new Queue<PageData>();

    private void Start()
    {
        BookInputController inputController = new BookInputController();

        m_runeTraceController = new RuneTraceController(m_runeTraceView);
        m_runeTraceController.SetCooldown(m_runeCooldownSeconds);

        if (m_gameDragInput == null)
        {
            Debug.LogError("BookGameBootstrap : BookGameDragInputが未設定です", this);
            return;
        }

        m_runeTraceView.ConfigureCooldownImage(() => m_gameController != null ? m_gameController.GetCurrentRuneImage() : null);
        m_runeTraceView.ConfigureScreenProjection(m_gameDragInput.GetRuneScreenRect, m_gameDragInput.GetGraphicScreenRect);
        m_gameDragInput.Initialize(m_runeTraceController, inputController);
        m_gameDragInput.SetInventoryOwner(m_gameController);

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

        Debug.Log("BookGameBootstrap : 初期化完了");
        m_networkClient = networkClient;
        if (m_networkClient != null)
        {
            m_networkClient.AddPageReceived += OnAddPageReceived;
            m_networkClient.OnConnectionStateChanged += OnConnectionStateChanged;
            OnConnectionStateChanged(m_networkClient.ConnectionState);
            gameObject.AddComponent<BookConnectionStatusView>().Initialize(m_networkClient);
        }
    }

    private void Update()
    {
        m_runeTraceController?.Tick(Time.deltaTime);
        if (m_requests.Count == 0 || m_gameController == null || m_gameController.IsInputBlocked) return;
        var result = m_gameController.RequestAdd(m_requests.Peek());
        if (result != PAGE_ADD_RESULT.BUSY) m_requests.Dequeue();
        if (result == PAGE_ADD_RESULT.INVALID) Debug.LogWarning("Unsupported page definition; request rejected.", this);
    }

    private void OnConnectionStateChanged(BOOK_CONNECTION_STATE state)
    {
        m_gameDragInput.SetRuneInputAvailable(state == BOOK_CONNECTION_STATE.CONNECTED);
    }

    private void OnAddPageReceived(byte id)
    {
        if (m_pageDatabase == null) { Debug.LogWarning("Page database is not configured.", this); return; }
        PageData data = m_pageDatabase.GetPage(id);
        if (data == null) return;
        if (m_requests.Count >= m_gameController.MaxPendingRequests)
        { Debug.LogWarning("Page request queue is full; incoming request rejected.", this); return; }
        m_requests.Enqueue(data);
    }

    private void OnDestroy()
    {
        if (m_networkClient != null)
        {
            m_networkClient.AddPageReceived -= OnAddPageReceived;
            m_networkClient.OnConnectionStateChanged -= OnConnectionStateChanged;
        }
        m_requests.Clear();
        if (m_gameController != null)
        {
            m_gameController.Shutdown();
        }
    }
}

}
