//-----------------------------------------------
// PageDatabase.cs
// 制作日：2026/10/05
// 制作者：安田晴人
// 概要： ページデータのデータベース
//-----------------------------------------------
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PageDatabase", menuName = "Book/Page Database")]
public class PageDatabase : ScriptableObject
{
    [SerializeField] private List<PageData> m_pages = new();

    public PageData GetPage(byte pageId)
    {
        if (pageId == 0)
        {
            Debug.LogWarning("PageId 0は無効です。");
            return null;
        }

        PageData foundPage = null;

        foreach (PageData page in m_pages)
        {
            if (page == null || page.PageId != pageId)
            {
                continue;
            }

            if (foundPage != null)
            {
                Debug.LogError($"PageIdが重複しています : {pageId}");
                return null;
            }

            foundPage = page;
        }

        if (foundPage == null)
        {
            Debug.LogWarning($"PageIdが見つかりません : {pageId}");
        }

        return foundPage;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        HashSet<byte> usedIds = new();

        foreach (PageData page in m_pages)
        {
            if (page == null)
            {
                continue;
            }

            if (page.PageId == 0)
            {
                Debug.LogWarning($"{page.name} のPageIdが0です。0は無効値として予約されています。", page);
                continue;
            }

            if (!usedIds.Add(page.PageId))
            {
                Debug.LogWarning($"PageIdが重複しています : {page.PageId} ({page.name})", page);
            }
        }
    }
#endif
}