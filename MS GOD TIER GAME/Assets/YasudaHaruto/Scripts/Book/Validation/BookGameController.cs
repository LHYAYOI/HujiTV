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
    private IReadOnlyList<PageInstance> m_captureCandidate;

    internal PageInstance GetPageForCapture(int spread, bool live)
    {
        var pages = !live && m_captureCandidate != null ? m_captureCandidate : Pages;
        return pages != null && spread > 0 && spread <= pages.Count ? pages[spread - 1] : null;
    }
    private bool m_editing;
    private Func<bool> m_canEdit;
    private int m_maxPageCount;
    private int m_maxPendingRequests;
    public IReadOnlyList<PageInstance> Pages => m_inventory?.Pages;
    public bool IsInputBlocked => !m_initialized || m_editing;
    public bool IsInitialized => m_initialized;
    public bool CanRemoveObstacles => m_initialized && isActiveAndEnabled && !IsInputBlocked &&
        m_bookController != null && m_bookController.IsReady && !m_bookController.IsClosed;
    public bool CanNavigate => m_initialized && isActiveAndEnabled && !IsInputBlocked &&
        m_bookController != null && m_bookController.IsReady && !(m_currentPage?.Obstacles.BlocksActions ?? false);
    public bool IsEditing => m_editing;
    public PageData PendingPage => null;
    public int MaxPageCount => m_maxPageCount;
    public int MaxPendingRequests => m_maxPendingRequests;
    public event Action ReplacementRequested { add { } remove { } }
    public event Action ReplacementDismissed { add { } remove { } }
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
        m_editing = false;
        m_captureCandidate = null;
        m_initialized = false;
    }



    public PAGE_ADD_RESULT RequestAdd(PageData data, bool present = true)
    {
        if (!BookInventory.CanAdd(data)) return PAGE_ADD_RESULT.INVALID;
        if (!m_initialized) return PAGE_ADD_RESULT.BUSY;
        if (m_inventory.Contains(data.PageId)) return PAGE_ADD_RESULT.INVALID;
        if (m_editing || !m_bookController.IsReady || (m_canEdit != null && !m_canEdit()))
            return PAGE_ADD_RESULT.BUSY;
        if (present && m_bookController.IsClosed)
        {
            m_bookController.OpenBook();
            return PAGE_ADD_RESULT.BUSY;
        }

        var candidate = m_inventory.Add(data, out PageInstance added);
        var visuals = m_inventory.BuildVisualPages(candidate);
        int currentSpread = m_bookController.CurrentSpread;
        int spread = present ? candidate.IndexOf(added) + 1 :
            m_currentPage != null ? candidate.IndexOf(m_currentPage) + 1 :
            currentSpread == 0 ? 0 : currentSpread + 1;
        m_editing = true;
        EndCurrentInteraction();
        m_captureCandidate = candidate;
        bool accepted = m_bookController.TryApplyInventory(visuals, spread, present, success =>
        {
            if (success)
            {
                m_inventory.Commit(candidate);
                if (present) m_bookController.PlayAcquisitionEffect();
            }
            m_captureCandidate = null;
            m_editing = false;
            if (isActiveAndEnabled && m_initialized && m_bookController.IsReady && !m_bookController.IsClosed)
                ActivateSpread(m_bookController.CurrentSpread);
            InventoryEditFinished?.Invoke(success);
        });
        if (!accepted)
        {
            m_captureCandidate = null;
            m_editing = false;
            ActivateSpread(m_bookController.CurrentSpread);
        }
        return accepted ? PAGE_ADD_RESULT.ACCEPTED : PAGE_ADD_RESULT.BUSY;
    }

    // Compatibility for the legacy validation scene and its panel.
    public bool ReplacePending(PageInstance selected) => false;
    public bool DeclinePending() => false;
    public bool Remove(PageInstance selected) => false;

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
