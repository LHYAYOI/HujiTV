//-----------------------------------------------
// BookController.cs
// 制作日：2026/09/11
// 制作者：安田晴人
// 概要：ページめくりの制御を行うスクリプト
//-----------------------------------------------
using System;
using UnityEngine;

public class BookController : MonoBehaviour
{
    [SerializeField, Min(1)]
    private int m_pageCount = 4;

    [SerializeField]
    private int m_initialPage = 0;

    [SerializeField]
    private bool m_loopPages = false;

    public int CurrentPage { get; private set; }

    public event Action<int> OnPageChanged;
    public event Action<int> OnCastRequested;

    private void Start()
    {
        CurrentPage = Mathf.Clamp(m_initialPage, 0, m_pageCount - 1);

        Debug.Log($"初期ページ : {CurrentPage}");
    }

    public void NextPage()
    {
        SetPage(CurrentPage + 1);
    }

    public void PreviousPage()
    {
        SetPage(CurrentPage - 1);
    }

    public void SetPage(int pageIndex)
    {
        int nextPage;

        if (m_loopPages)
        {
            nextPage = (pageIndex % m_pageCount + m_pageCount) % m_pageCount;
        }
        else
        {
            nextPage = Mathf.Clamp(pageIndex, 0, m_pageCount - 1);
        }

        if (nextPage == CurrentPage)
        {
            return;
        }

        CurrentPage = nextPage;

        Debug.Log($"ページ変更 : {CurrentPage}");

        OnPageChanged?.Invoke(CurrentPage);
    }

    public void RequestCast()
    {
        Debug.Log($"魔法発動要求 : Page {CurrentPage}");

        OnCastRequested?.Invoke(CurrentPage);
    }
}