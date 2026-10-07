//-----------------------------------------------
// BookPageService.cs
// 制作日：2026/10/05
// 制作者：安田晴人
// 概要： ページの追加処理を行うサービス
//-----------------------------------------------
using UnityEngine;

public class BookPageService
{
    private readonly BookModel m_bookModel;
    private readonly PageDatabase m_pageDatabase;
    private readonly PageFactory m_pageFactory;

    public BookPageService(BookModel bookModel, PageDatabase pageDatabase, PageFactory pageFactory)
    {
        m_bookModel = bookModel;
        m_pageDatabase = pageDatabase;
        m_pageFactory = pageFactory;
    }

    public bool AddPage(byte pageId)
    {
        if (!m_bookModel.CanAddPage)
        {
            Debug.LogWarning($"ページ上限のため追加できません : PageId={pageId}");
            return false;
        }

        PageData pageData = m_pageDatabase.GetPage(pageId);
        if (pageData == null)
        {
            return false;
        }

        PageInstance page = m_pageFactory.Create(pageData);
        if (page == null)
        {
            Debug.LogError($"PageInstance生成失敗 : PageId={pageId}");
            return false;
        }

        bool success = pageData.InsertType switch
        {
            PAGE_INSERT_TYPE.END => m_bookModel.AddPage(page),
            PAGE_INSERT_TYPE.AFTER_CURRENT => AddAfterCurrent(page),
            PAGE_INSERT_TYPE.RANDOM => AddRandom(page),
            _ => false
        };

        if (success)
        {
            LogPages();
        }

        return success;
    }

    private bool AddAfterCurrent(PageInstance page)
    {
        int index = m_bookModel.CurrentPageIndex + 1;
        return m_bookModel.InsertPage(index, page);
    }

    private bool AddRandom(PageInstance page)
    {
        int index = Random.Range(0, m_bookModel.Pages.Count + 1);
        return m_bookModel.InsertPage(index, page);
    }

    private void LogPages()
    {
        string result = "Pages : ";

        for (int i = 0; i < m_bookModel.Pages.Count; i++)
        {
            PageInstance page = m_bookModel.Pages[i];

            result += $"[{i}:{page.Data.DisplayName}] ";
        }

        Debug.Log(result);

        Debug.Log($"CurrentPageIndex : {m_bookModel.CurrentPageIndex}");
    }
}