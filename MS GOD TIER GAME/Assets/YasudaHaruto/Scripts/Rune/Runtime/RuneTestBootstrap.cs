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
        BookInputController inputController =
            new BookInputController();

        RuneTraceController runeController =
            new RuneTraceController(m_view);

        BookNetworkClient client = BookNetworkClient.Instance;

        BookNetworkService networkService = new BookNetworkService(client);

        IPageCommand castCommand = new CastMagicCommand(1, networkService);

        RuneTraceInteraction runeInteraction =
            new RuneTraceInteraction(
                m_runeData,
                runeController,
                inputController,
                castCommand);

        RuneTraceInteraction interaction =
            new RuneTraceInteraction(
                m_runeData,
                runeController,
                inputController,
                castCommand);

        m_pageInstance =
            new PageInstance(
                m_pageData,
                interaction);

        m_inputRouter.Initialize(
            m_mouseInput,
            inputController,
            runeController);

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