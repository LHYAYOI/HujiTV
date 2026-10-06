using UnityEngine;

public class BookTestBootstrap : MonoBehaviour
{
    [Header("Page")]
    [SerializeField]
    private PageData[] m_initialPages;

    [SerializeField]
    private PageDatabase m_pageDatabase;

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

    [Header("Book Display")]
    [SerializeField] private BookDisplayController m_displayController;

    [Header("Book")]
    [SerializeField]
    private int m_maxPageCount = 10;

    private BookController m_bookController;
    private BookPageService m_pageService;
    private BookNetworkClient m_networkClient;

    private void Start()
    {
        // Input
        BookInputController inputController = new BookInputController();

        // Rune
        RuneTraceController runeTraceController = new RuneTraceController(m_runeTraceView);

        // Network
        BookNetworkService networkService = null;

        m_networkClient = BookNetworkClient.Instance;

        if (m_networkClient != null)
        {
            networkService = new BookNetworkService(m_networkClient);
            m_networkClient.AddPageReceived += OnAddPageReceived;
        }
        else
        {
            Debug.Log("BookNetworkClientなしでBookテストを開始します");
        }

        // Page
        PageFactory pageFactory = new PageFactory(runeTraceController, inputController, networkService);

        // Book
        BookModel bookModel = new BookModel(m_maxPageCount);

        m_pageService = new BookPageService(bookModel, m_pageDatabase, pageFactory);

        foreach (PageData pageData in m_initialPages)
        {
            if (pageData == null)
            {
                continue;
            }

            PageInstance page = pageFactory.Create(pageData);

            if (page == null)
            {
                Debug.LogError($"PageInstance生成失敗 : {pageData.name}");
                continue;
            }

            bookModel.AddPage(page);
        }

        // Book Controller
        m_bookController = new BookController(bookModel, inputController, m_displayController);

        // Input Router
        m_inputRouter.Initialize(m_mouseInput, inputController, runeTraceController, m_runeInputArea);
        m_inputRouter.PageNavigationRequested += m_bookController.RequestNavigation;

        // 最初の見開きを表示してからInteraction開始
        if (bookModel.CurrentPage != null)
        {
            m_displayController.ShowInitial(bookModel.CurrentPage.Data, m_bookController.Begin);
        }
    }

    private void OnDestroy()
    {
        if (m_inputRouter != null && m_bookController != null)
        {
            m_inputRouter.PageNavigationRequested -= m_bookController.RequestNavigation;
        }

        m_bookController?.End();

        if (m_networkClient != null)
        {
            m_networkClient.AddPageReceived -= OnAddPageReceived;
        }
    }

    private void OnAddPageReceived(byte pageId)
    {
        if (m_pageService == null)
        {
            return;
        }

        bool success = m_pageService.AddPage(pageId);

        Debug.Log($"Network AddPage : PageId={pageId} / Success={success}");
    }
}