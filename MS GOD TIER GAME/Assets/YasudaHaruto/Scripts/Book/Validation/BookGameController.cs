//-----------------------------------------------
// BookGameController.cs
// 制作日：2026/10/21
// 制作者：安田晴人
// 概要：本の見開きページの管理
//-----------------------------------------------
using UnityEngine;
using System;
using System.Collections.Generic;

namespace YasudaHaruto.BookValidation
{
public class BookGameController : MonoBehaviour
{
    [Header("Book")]
    [SerializeField] private YasudaBookViewController m_bookController;

    [Header("Fixed Pages")]
    [SerializeField] private BookPageData m_firstFixedPage;
    [SerializeField] private BookPageData m_lastFixedPage;

    [Header("Game Pages")]
    [SerializeField] private PageData[] m_initialPages;

    [SerializeField] private BookInventorySettings m_inventorySettings;
    private List<PageInstance> m_pageInstances;
    private PageFactory m_pageFactory;
    private bool m_editing;
    private Func<bool> m_canEdit;
    private PageData m_pendingPage;
    private PageInstance m_insertionAnchor;
    private IReadOnlyList<PageInstance> m_readOnlyPages;
    private int m_maxPageCount;
    private int m_maxPendingRequests;
    public IReadOnlyList<PageInstance> Pages => m_readOnlyPages;
    public bool IsInputBlocked => m_editing || m_pendingPage != null;
    public bool IsEditing => m_editing;
    public PageData PendingPage => m_pendingPage;
    public int MaxPageCount => m_maxPageCount;
    public int MaxPendingRequests => m_maxPendingRequests;
    public event Action ReplacementRequested;
    public event Action ReplacementDismissed;
    public event Action<bool> InventoryEditFinished;
    private PageInstance m_currentPage;
    private bool m_initialized;

    public PageInstance CurrentPage => m_currentPage;

    public bool Initialize(PageFactory pageFactory, Func<bool> canEdit = null)
    {
        if (m_initialized)
        {
            return true;
        }

        m_maxPageCount = m_inventorySettings != null ? m_inventorySettings.MaxGamePageCount : 0;
        m_maxPendingRequests = m_inventorySettings != null ? m_inventorySettings.MaxPendingRequests : 1;

        if (m_bookController == null || pageFactory == null || m_initialPages == null ||
            m_inventorySettings == null || m_initialPages.Length > MaxPageCount)
        {
            Debug.LogError("BookGameController : 初期化に必要な参照がありません", this);
            return false;
        }

        if (!ValidateBookPages())
        {
            return false;
        }

        m_pageFactory = pageFactory;
        m_canEdit = canEdit;
        m_pageInstances = new List<PageInstance>(m_initialPages.Length);
        m_readOnlyPages = m_pageInstances.AsReadOnly();

        for (int i = 0; i < m_initialPages.Length; i++)
        {
            m_pageInstances.Add(pageFactory.Create(m_initialPages[i]));

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

        if (IsInputBlocked) return;
        PageInstance nextPage = gamePageIndex >= 0 && gamePageIndex < m_pageInstances.Count ? m_pageInstances[gamePageIndex] : null;

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
        m_pendingPage = null;
        m_initialized = false;
    }



    private bool Ready => m_initialized && !m_editing && isActiveAndEnabled &&
        m_bookController.isActiveAndEnabled && !m_bookController.IsBusy &&
        !m_bookController.IsClosed && !m_bookController.FlipView.IsTurning &&
        (m_canEdit == null || m_canEdit());

    private static bool Valid(PageData data) => data != null && data.LeftVisualData != null &&
        data.RightVisualData != null && (data.InsertType == PAGE_INSERT_TYPE.END ||
        data.InsertType == PAGE_INSERT_TYPE.AFTER_CURRENT);

    public PAGE_ADD_RESULT RequestAdd(PageData data)
    {
        if (!Valid(data)) return PAGE_ADD_RESULT.INVALID;
        if (!Ready || m_pendingPage != null) return PAGE_ADD_RESULT.BUSY;
        if (m_pageInstances.Count >= MaxPageCount)
        {
            m_pendingPage = data;
            m_insertionAnchor = m_currentPage;
            EndCurrentInteraction();
            ReplacementRequested?.Invoke();
            return PAGE_ADD_RESULT.NEEDS_SELECTION;
        }
        var candidate = new List<PageInstance>(m_pageInstances);
        int at = InsertionIndex(candidate, data, m_currentPage);
        candidate.Insert(at, m_pageFactory.Create(data));
        return Apply(candidate, m_currentPage, null) ? PAGE_ADD_RESULT.ACCEPTED : PAGE_ADD_RESULT.BUSY;
    }

    private int InsertionIndex(List<PageInstance> pages, PageData data, PageInstance anchor)
    {
        if (data.InsertType == PAGE_INSERT_TYPE.END) return pages.Count;
        if (anchor != null) return pages.IndexOf(anchor) + 1;
        return m_bookController.CurrentSpread == 0 ? 0 : pages.Count;
    }

    public bool ReplacePending(PageInstance selected)
    {
        if (!Ready || m_pendingPage == null || selected == null) return false;
        int removed = m_pageInstances.IndexOf(selected);
        if (removed < 0 || m_pageInstances.Count > MaxPageCount) return false;
        var candidate = new List<PageInstance>(m_pageInstances);
        candidate.RemoveAt(removed);
        int at = ReferenceEquals(selected, m_insertionAnchor) &&
            m_pendingPage.InsertType == PAGE_INSERT_TYPE.AFTER_CURRENT
            ? removed : InsertionIndex(candidate, m_pendingPage, m_insertionAnchor);
        var added = m_pageFactory.Create(m_pendingPage);
        candidate.Insert(at, added);
        PageInstance visible = ReferenceEquals(selected, m_insertionAnchor) ? added : m_insertionAnchor;
        return Apply(candidate, visible, () => { m_pendingPage = null; m_insertionAnchor = null; });
    }

    public bool DeclinePending()
    {
        if (m_editing || m_pendingPage == null) return false;
        m_pendingPage = null;
        m_insertionAnchor = null;
        ActivateSpread(m_bookController.CurrentSpread);
        ReplacementDismissed?.Invoke();
        return true;
    }

    public bool Remove(PageInstance selected)
    {
        if (!Ready || m_pendingPage != null || selected == null) return false;
        int at = m_pageInstances.IndexOf(selected);
        if (at < 0) return false;
        var candidate = new List<PageInstance>(m_pageInstances);
        candidate.RemoveAt(at);
        PageInstance visible = m_currentPage;
        if (ReferenceEquals(visible, selected))
            visible = candidate.Count == 0 ? null : candidate[Math.Min(at, candidate.Count - 1)];
        return Apply(candidate, visible, null);
    }

    private bool Apply(List<PageInstance> candidate, PageInstance visible, Action committed)
    {
        if (candidate.Count > MaxPageCount) return false;
        var visuals = new List<BookPageData> { null, m_firstFixedPage };
        foreach (var page in candidate)
        {
            if (page == null || page.Data.LeftVisualData == null || page.Data.RightVisualData == null) return false;
            visuals.Add(page.Data.LeftVisualData);
            visuals.Add(page.Data.RightVisualData);
        }
        visuals.Add(m_lastFixedPage);
        int spread = visible != null ? candidate.IndexOf(visible) + 1 :
            (candidate.Count == 0 || m_bookController.CurrentSpread == 0 ? 0 : candidate.Count + 1);
        m_editing = true;
        EndCurrentInteraction();
        bool accepted = m_bookController.TryApplyInventory(visuals, spread, success =>
        {
            if (success)
            {
                m_pageInstances = candidate;
                m_readOnlyPages = m_pageInstances.AsReadOnly();
                committed?.Invoke();
            }
            m_editing = false;
            if (isActiveAndEnabled && m_initialized && m_bookController.isActiveAndEnabled && !m_bookController.IsClosed)
                ActivateSpread(m_bookController.CurrentSpread);
            InventoryEditFinished?.Invoke(success);
        });
        if (!accepted)
        {
            m_editing = false;
            ActivateSpread(m_bookController.CurrentSpread);
        }
        return accepted;
    }

    private void OnDestroy()
    {
        Shutdown();
    }

    private void OnDisable() => EndCurrentInteraction();
    private void OnEnable()
    {
        if (m_initialized) ActivateSpread(m_bookController.CurrentSpread);
    }
}

}
