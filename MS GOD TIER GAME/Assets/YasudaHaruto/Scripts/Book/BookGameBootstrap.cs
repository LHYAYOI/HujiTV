//-----------------------------------------------
// BookGameBootstrap.cs
// 制作日：2026/10/21
// 制作者：安田晴人
// 概要： 本の初期化と管理
//-----------------------------------------------
using UnityEngine;

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

        m_gameDragInput.Initialize(m_runeTraceController, inputController);

        BookNetworkClient networkClient = BookNetworkClient.Instance;
        BookNetworkService networkService = networkClient != null ? new BookNetworkService(networkClient) : null;

        PageFactory pageFactory = new PageFactory(m_runeTraceController, inputController, networkService);

        if (m_gameController == null || !m_gameController.Initialize(pageFactory))
        {
            Debug.LogError("BookGameBootstrap : 初期化失敗", this);
            return;
        }

        Debug.Log("BookGameBootstrap : 初期化完了");
    }

    private void Update()
    {
        m_runeTraceController?.Tick(Time.deltaTime);
    }

    private void OnDestroy()
    {
        if (m_gameController != null)
        {
            m_gameController.Shutdown();
        }
    }
}
