using UnityEngine;

public class RuneTraceTestBootstrap : MonoBehaviour
{
    [SerializeField]
    private PageData m_pageData;

    [SerializeField]
    private RuneData m_runeData;

    [SerializeField]
    private DebugRuneTraceView m_view;

    [SerializeField]
    private MouseBookPointerInput m_mouseInput;

    [SerializeField]
    private BookInputRouter m_inputRouter;


    private PageInstance m_pageInstance;

    private void Start()
    {
        BookInputController inputController = new BookInputController();

        RuneTraceController runeTraceController = new RuneTraceController(m_view);

        BookNetworkClient client = BookNetworkClient.Instance;

        if (client == null)
        {
            Debug.LogError("BookNetworkClientÇ™ë∂ç›ÇµÇ‹ÇπÇÒ");
            return;
        }

        BookNetworkService networkService = new BookNetworkService(client);

        PageFactory pageFactory = new PageFactory(runeTraceController, inputController, networkService);

        PageInstance page = pageFactory.Create(m_pageData);

        //bookModel.AddPage(page);

        if (page == null)
        {
            Debug.LogError($"PageInstanceê∂ê¨é∏îs : {m_pageData?.name}");
            return;
        }

        //m_inputRouter.Initialize(m_mouseInput, inputController, runeTraceController);

        m_pageInstance.BeginInteraction();

        m_inputRouter.PageNavigationRequested += OnPageNavigationRequested;
    }

    private void OnPageNavigationRequested(
    PAGE_NAVIGATION_DIRECTION direction)
    {
        Debug.Log(
            $"Page Navigation Requested : {direction}");
    }

    private void OnDestroy()
    {
        m_pageInstance?.EndInteraction();
    }
}