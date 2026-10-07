//-----------------------------------------------
// BookModel.cs
// 制作日：2026/09/28
// 制作者：安田晴人
// 概要： 本のページめくりのモデルクラス
//-----------------------------------------------
using System.Collections.Generic;

public class BookModel
{
    private readonly List<PageInstance> m_pages = new();

    private int m_currentPageIndex = -1;
    private int m_maxPageCount;

    public IReadOnlyList<PageInstance> Pages => m_pages;

    public int CurrentPageIndex => m_currentPageIndex;
    public int PageCount => m_pages.Count;

    public PageInstance CurrentPage
    {
        get
        {
            if (m_currentPageIndex < 0 || m_currentPageIndex >= m_pages.Count)
            {
                return null;
            }

            return m_pages[m_currentPageIndex];
        }
    }

    public bool CanMoveNext => m_currentPageIndex >= 0 && m_currentPageIndex < m_pages.Count - 1;

    public bool CanMovePrevious => m_currentPageIndex > 0;

    public bool CanAddPage => m_pages.Count < m_maxPageCount;

    public BookModel(int maxPageCount)
    {
        m_maxPageCount = maxPageCount;
    }

    public bool MoveNext()
    {
        if (!CanMoveNext)
        {
            return false;
        }

        m_currentPageIndex++;
        return true;
    }

    public bool MovePrevious()
    {
        if (!CanMovePrevious)
        {
            return false;
        }

        m_currentPageIndex--;
        return true;
    }

    public bool AddPage(PageInstance page)
    {
        return InsertPage(m_pages.Count, page);
    }

    public bool InsertPage(int index, PageInstance page)
    {
        if (page == null)
        {
            return false;
        }

        if (!CanAddPage)
        {
            return false;
        }

        if (index < 0 || index > m_pages.Count)
        {
            return false;
        }

        // 現在見ているPageInstanceを維持するため、
        // Currentより前に追加された場合はIndexをずらす。
        if (m_currentPageIndex >= 0 && index <= m_currentPageIndex)
        {
            m_currentPageIndex++;
        }

        m_pages.Insert(index, page);

        // 最初の1ページ
        if (m_currentPageIndex < 0)
        {
            m_currentPageIndex = 0;
        }

        return true;
    }

    public bool RemovePage(PageInstance page)
    {
        if (page == null)
        {
            return false;
        }

        int index = m_pages.IndexOf(page);

        if (index < 0)
        {
            return false;
        }

        return RemovePageAt(index);
    }

    private bool RemovePageAt(int index)
    {
        if (index < 0 || index >= m_pages.Count)
        {
            return false;
        }

        bool removingCurrent = index == m_currentPageIndex;

        m_pages.RemoveAt(index);

        // 全ページがなくなった
        if (m_pages.Count == 0)
        {
            m_currentPageIndex = -1;
            return true;
        }

        // Currentより前が削除された
        // 同じPageInstanceを見続けるためIndexを詰める
        if (index < m_currentPageIndex)
        {
            m_currentPageIndex--;
            return true;
        }

        if (removingCurrent)
        {
            // Current自身を削除した場合は
            // 1つ前のページを優先する。

            m_currentPageIndex--;

            // 先頭ページを削除した場合は
            // 前が存在しないので次ページへ。
            if (m_currentPageIndex < 0)
            {
                m_currentPageIndex = 0;
            }
        }

        return true;
    }
}