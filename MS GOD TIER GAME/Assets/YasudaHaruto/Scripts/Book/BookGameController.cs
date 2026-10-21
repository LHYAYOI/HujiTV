//-----------------------------------------------
// BookGameController.cs
// 制作日：2026/10/21
// 制作者：安田晴人
// 概要：本の見開きページの管理
//-----------------------------------------------
using UnityEngine;

public class BookGameController : MonoBehaviour
{
    [Header("Book")]
    [SerializeField] private hatake.BookController m_bookController;

    [Header("Fixed Pages")]
    [SerializeField] private BookPageData m_firstFixedPage;
    [SerializeField] private BookPageData m_lastFixedPage;

    [Header("Game Pages")]
    [SerializeField] private PageData[] m_initialPages;

    private PageInstance[] m_pageInstances;
    private PageInstance m_currentPage;
    private bool m_initialized;

    public PageInstance CurrentPage => m_currentPage;

    public bool Initialize(PageFactory pageFactory)
    {
        if (m_initialized)
        {
            return true;
        }

        if (m_bookController == null || pageFactory == null || m_initialPages == null)
        {
            Debug.LogError("BookGameController : 初期化に必要な参照がありません", this);
            return false;
        }

        if (!ValidateBookPages())
        {
            return false;
        }

        m_pageInstances = new PageInstance[m_initialPages.Length];

        for (int i = 0; i < m_initialPages.Length; i++)
        {
            m_pageInstances[i] = pageFactory.Create(m_initialPages[i]);

            if (m_pageInstances[i] == null)
            {
                Debug.LogError($"PageInstance生成失敗 : {m_initialPages[i].name}", this);
                m_pageInstances = null;
                return false;
            }
        }

        m_bookController.SpreadShown += OnSpreadShown;
        m_bookController.CoverChanged += OnCoverChanged;
        m_initialized = true;

        // 初回のSpreadShownが購読前に発生していても現在位置を反映する
        if (!m_bookController.IsBusy)
        {
            ActivateSpread(m_bookController.CurrentSpread);
        }

        return true;
    }

    private bool ValidateBookPages()
    {
        // 表紙内側1 + 固定ページ2 + 通常見開き2枚ずつ
        int expectedPageCount = 3 + m_initialPages.Length * 2;

        if (!m_bookController.CoversEnabled || !m_bookController.IsCoverInside(0))
        {
            Debug.LogError("BookGameController : Use Covers と Cover Insides を有効にしてください", this);
            return false;
        }

        if (m_bookController.PageCount != expectedPageCount)
        {
            Debug.LogError($"ページ数が一致しません : Book={m_bookController.PageCount}, Expected={expectedPageCount}", this);
            return false;
        }

        if (m_bookController.GetPage(1) != m_firstFixedPage || m_bookController.GetPage(expectedPageCount - 1) != m_lastFixedPage)
        {
            Debug.LogError("BookGameController : 固定ページの設定が一致しません", this);
            return false;
        }

        for (int i = 0; i < m_initialPages.Length; i++)
        {
            PageData page = m_initialPages[i];
            if (page == null)
            {
                Debug.LogError($"PageDataが未設定です : Index={i}", this);
                return false;
            }

            int leftIndex = 2 + i * 2;

            if (m_bookController.GetPage(leftIndex) != page.LeftVisualData || m_bookController.GetPage(leftIndex + 1) != page.RightVisualData)
            {
                Debug.LogError($"見開きの表示データが一致しません : {page.name}", this);
                return false;
            }
        }

        return true;
    }

    private void OnSpreadShown(int spreadIndex)
    {
        ActivateSpread(spreadIndex);
    }

    private void OnCoverChanged(hatake.BookCoverState coverState)
    {
        if (coverState != hatake.BookCoverState.Open)
        {
            EndCurrentInteraction();
        }
    }

    private void ActivateSpread(int spreadIndex)
    {
        if (!m_initialized || m_bookController.IsClosed) return;

        // Spread 0 は最初の固定ページ。
        // Spread 1～N が通常の見開き。
        // Spread N+1 は最後の固定ページ。
        int gamePageIndex = spreadIndex - 1;

        PageInstance nextPage = gamePageIndex >= 0 && gamePageIndex < m_pageInstances.Length ? m_pageInstances[gamePageIndex] : null;

        if (ReferenceEquals(m_currentPage, nextPage))
        {
            return;
        }

        EndCurrentInteraction();
        m_currentPage = nextPage;
        m_currentPage?.BeginInteraction();

        Debug.Log($"BookGameController : Spread={spreadIndex}, Page={m_currentPage?.Data.DisplayName ?? "Fixed"}");
    }

    private void EndCurrentInteraction()
    {
        m_currentPage?.EndInteraction();
        m_currentPage = null;
    }

    public void Shutdown()
    {
        if (!m_initialized)
        {
            return;
        }

        m_bookController.SpreadShown -= OnSpreadShown;
        m_bookController.CoverChanged -= OnCoverChanged;

        EndCurrentInteraction();
        m_initialized = false;
    }

    private void OnDestroy()
    {
        Shutdown();
    }
}
