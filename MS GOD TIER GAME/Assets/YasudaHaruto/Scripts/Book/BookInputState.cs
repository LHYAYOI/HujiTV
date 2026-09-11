//-----------------------------------------------
// BookInputState.cs
// 制作日：2026/09/11
// 制作者：安田晴人
// 概要：本のページめくりの状態を保持するクラス
//-----------------------------------------------
using System;
using UnityEngine;

public class BookInputState : MonoBehaviour
{
    public int CurrentPage { get; private set; } = -1;

    public event Action<int> OnPageChanged;
    public event Action<int> OnCastRequested;

    public void SetCurrentPage(int pageIndex)
    {
        if (pageIndex < 0)
        {
            Debug.LogWarning($"不正なPageIndexです : {pageIndex}");

            return;
        }

        if (CurrentPage == pageIndex)
        {
            return;
        }

        CurrentPage = pageIndex;

        Debug.Log($"PC側 現在ページ : {CurrentPage}");

        OnPageChanged?.Invoke(CurrentPage);
    }

    public void RequestCast(int pageIndex)
    {
        if (pageIndex < 0)
        {
            Debug.LogWarning($"不正なCastRequestです : {pageIndex}");

            return;
        }

        // PC側が認識しているページと違ったら発動させない
        if (CurrentPage != pageIndex)
        {
            Debug.LogWarning($"CastRequestのページが一致しません。" + $" PC={CurrentPage}" + $" Book={pageIndex}");

            return;
        }

        Debug.Log($"PC側 魔法発動要求 : Page {pageIndex}");

        OnCastRequested?.Invoke(pageIndex);
    }
}