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
    private BookInventory m_inventory;
    private bool m_editing;
    private Func<bool> m_canEdit;
    private PageData m_pendingPage;
    private PageInstance m_insertionAnchor;
    private int m_maxPageCount;
    private int m_maxPendingRequests;
    public IReadOnlyList<PageInstance> Pages => m_inventory?.Pages;
    public bool IsInputBlocked => !m_initialized || m_editing || m_pendingPage != null;
    public bool IsInitialized => m_initialized;
    public bool CanRemoveObstacles => m_initialized && isActiveAndEnabled && !IsInputBlocked &&
        m_bookController != null && m_bookController.IsReady && !m_bookController.IsClosed;
    public bool CanNavigate => m_initialized && isActiveAndEnabled && !IsInputBlocked &&
        m_bookController != null && m_bookController.IsReady && !(m_currentPage?.Obstacles.BlocksActions ?? false);
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
    public YasudaBookViewController BookView => m_bookController;
    public UnityEngine.UI.Image GetCurrentRuneImage() => m_currentPage?.Data.Interaction == null ? null :
        m_bookController.GetCurrentRuneImage(m_currentPage.Data.Interaction.RuneImageIndex);

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

        try
        {
            m_inventory = new BookInventory(pageFactory, m_initialPages,
                m_firstFixedPage, m_lastFixedPage, MaxPageCount);
        }
        catch (ArgumentException exception)
        {
            Debug.LogError("BookGameController : " + exception.Message, this);
            return false;
        }
        m_canEdit = canEdit;

        m_bookController.SpreadShown += OnSpreadShown;
        m_bookController.CoverChanged += OnCoverChanged;
        m_initialized = true;
        if (!m_bookController.InitializeInventory(m_inventory.BuildVisualPages(Pages)))
        {
            Shutdown();
            Debug.LogError("BookGameController : 表示の初期化に失敗しました", this);
            return false;
        }

        // 初回のSpreadShownが購読前に発生していても現在位置を反映する
        if (!m_bookController.IsBusy)
        {
            ActivateSpread(m_bookController.CurrentSpread);
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

        if (IsInputBlocked || m_bookController.IsBusy) return;
        PageInstance nextPage = m_inventory.GetPageAtSpread(spreadIndex);

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
        m_insertionAnchor = null;
        m_editing = false;
        m_initialized = false;
    }



    private bool Ready => CanRemoveObstacles && !(m_currentPage?.Obstacles.BlocksActions ?? false) &&
        (m_canEdit == null || m_canEdit());

    private bool CanConfirmReplacement => m_initialized && isActiveAndEnabled && !m_editing &&
        m_bookController.IsReady && !m_bookController.IsClosed &&
        (m_canEdit == null || m_canEdit());

    public PAGE_ADD_RESULT RequestAdd(PageData data)
    {
        if (!BookInventory.CanAdd(data)) return PAGE_ADD_RESULT.INVALID;
        if (!Ready || m_pendingPage != null) return PAGE_ADD_RESULT.BUSY;
        if (Pages.Count >= MaxPageCount)
        {
            m_pendingPage = data;
            m_insertionAnchor = m_currentPage;
            EndCurrentInteraction();
            ReplacementRequested?.Invoke();
            return PAGE_ADD_RESULT.NEEDS_SELECTION;
        }
        var candidate = m_inventory.Add(data, m_currentPage, m_bookController.CurrentSpread == 0);
        return Apply(candidate, m_currentPage, null) ? PAGE_ADD_RESULT.ACCEPTED : PAGE_ADD_RESULT.BUSY;
    }

    public bool ReplacePending(PageInstance selected)
    {
        if (!CanConfirmReplacement || m_pendingPage == null || selected == null) return false;
        var candidate = m_inventory.Replace(m_pendingPage, selected, m_insertionAnchor,
            m_bookController.CurrentSpread == 0, out PageInstance visible);
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
        var candidate = m_inventory.Remove(selected, m_currentPage, out PageInstance visible);
        return Apply(candidate, visible, null);
    }

    private bool Apply(List<PageInstance> candidate, PageInstance visible, Action committed)
    {
        if (candidate == null || candidate.Count > MaxPageCount) return false;
        var visuals = m_inventory.BuildVisualPages(candidate);
        int spread = visible != null ? candidate.IndexOf(visible) + 1 :
            (candidate.Count == 0 || m_bookController.CurrentSpread == 0 ? 0 : candidate.Count + 1);
        m_editing = true;
        EndCurrentInteraction();
        bool accepted = m_bookController.TryApplyInventory(visuals, spread, success =>
        {
            if (success)
            {
                m_inventory.Commit(candidate);
                committed?.Invoke();
            }
            m_editing = false;
            if (isActiveAndEnabled && m_initialized && m_bookController.IsReady && !m_bookController.IsClosed)
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
