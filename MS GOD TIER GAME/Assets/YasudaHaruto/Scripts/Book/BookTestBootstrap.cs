using UnityEngine;

public class BookTestBootstrap : MonoBehaviour
{
    [SerializeField]
    private BookInputRouter m_inputRouter;

    [SerializeField]
    private MouseBookPointerInput m_mouseInput;

    private BookController m_bookController;

    private void Start()
    {
        BookInputController inputController =
            new BookInputController();

        BookModel bookModel =
            new BookModel(10);

        PageInstance pageA =
            new PageInstance(
                null,
                new DebugPageInteraction("Page A"));

        PageInstance pageB =
            new PageInstance(
                null,
                new DebugPageInteraction("Page B"));

        PageInstance pageC =
            new PageInstance(
                null,
                new DebugPageInteraction("Page C"));

        bookModel.AddPage(pageA);
        bookModel.AddPage(pageB);
        bookModel.AddPage(pageC);

        m_bookController =
            new BookController(
                bookModel,
                inputController);

        m_inputRouter.Initialize(
            m_mouseInput,
            inputController,
            null);

        m_inputRouter.PageNavigationRequested +=
            m_bookController.RequestNavigation;


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