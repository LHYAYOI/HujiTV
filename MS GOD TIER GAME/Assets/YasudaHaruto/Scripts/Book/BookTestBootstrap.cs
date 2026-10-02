using UnityEngine;

public class BookTestBootstrap : MonoBehaviour
{
    [Header("Page")]
    [SerializeField]
    private PageData[] m_initialPages;

    [Header("Rune")]
    [SerializeField]
    private DebugRuneTraceView m_runeTraceView;

    [Header("Input")]
    [SerializeField]
    private BookInputRouter m_inputRouter;

    [SerializeField]
    private MouseBookPointerInput m_mouseInput;

    [SerializeField]
    private RuneInputArea m_runeInputArea;

    [Header("Book")]
    [SerializeField]
    private int m_maxPageCount = 10;

    private BookController m_bookController;

    private void Start()
    {
        // Input
        BookInputController inputController =
            new BookInputController();

        // Rune
        RuneTraceController runeTraceController =
            new RuneTraceController(m_runeTraceView);

        // Network
        BookNetworkClient networkClient =
            BookNetworkClient.Instance;

        if (networkClient == null)
        {
            Debug.LogError("BookNetworkClientが存在しません");
            //return;
        }

        BookNetworkService networkService =
            new BookNetworkService(networkClient);

        // Page
        PageFactory pageFactory =
            new PageFactory(
                runeTraceController,
                inputController,
                networkService);

        // Book
        BookModel bookModel =
            new BookModel(m_maxPageCount);

        foreach (PageData pageData in m_initialPages)
        {
            if (pageData == null)
            {
                continue;
            }

            PageInstance page =
                pageFactory.Create(pageData);

            if (page == null)
            {
                Debug.LogError(
                    $"PageInstance生成失敗 : {pageData.name}");
                continue;
            }

            bookModel.AddPage(page);
        }

        // Book Controller
        m_bookController =
            new BookController(
                bookModel,
                inputController);

        // Input Router
        m_inputRouter.Initialize(
            m_mouseInput,
            inputController,
            runeTraceController,
            m_runeInputArea);

        m_inputRouter.PageNavigationRequested +=
            m_bookController.RequestNavigation;

        // 最初のページ開始
        m_bookController.Begin();
    }

    private void OnDestroy()
    {
        if (m_inputRouter != null &&
            m_bookController != null)
        {
            m_inputRouter.PageNavigationRequested -=
                m_bookController.RequestNavigation;
        }

        m_bookController?.End();
    }
}