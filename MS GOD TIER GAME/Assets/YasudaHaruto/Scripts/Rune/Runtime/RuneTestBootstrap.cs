using UnityEngine;

public class RuneTraceTestBootstrap : MonoBehaviour
{
    [Header("Page")]
    [SerializeField]
    private PageData m_pageData;

    [Header("Rune")]
    [SerializeField]
    private RuneData m_runeData;

    [Header("View")]
    [SerializeField]
    private DebugRuneTraceView m_view;

    [Header("Input")]
    [SerializeField]
    private MouseBookPointerInput m_mouseInput;
    
    [SerializeField]
    private BookInputRouter m_inputRouter;

    [SerializeField]
    private RectTransform m_traceArea;

    private PageInstance m_pageInstance;

    private void Start()
    {
        BookInputController inputController =
            new BookInputController();

        RuneTraceController runeController =
            new RuneTraceController(m_view);

        RuneTraceInteraction interaction =
            new RuneTraceInteraction(
                m_runeData,
                runeController,
                inputController);

        m_pageInstance =
            new PageInstance(
                m_pageData,
                interaction);

        m_inputRouter.Initialize(
            m_mouseInput,
            inputController,
            runeController);

        m_pageInstance.BeginInteraction();
    }

    private void OnDestroy()
    {
        m_pageInstance?.EndInteraction();
    }
}