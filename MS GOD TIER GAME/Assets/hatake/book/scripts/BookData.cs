
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(
    fileName = "Book_New",
    menuName = "Book/Book Data")]
public sealed class BookData : ScriptableObject
{
    [Header("本の情報")]

    [SerializeField]
    private string m_bookTitle = "";

    [Header("ページ順")]

    [Tooltip("先頭から、左・右・左・右の順に登録します。")]
    [SerializeField]
    private List<BookPageData> m_pages = new List<BookPageData>();

    public string BookTitle => m_bookTitle;

    public int PageCount => m_pages.Count;

    // ページ数が奇数なら、最後の右ページは空白になります。
    public int SpreadCount => (PageCount + 1) / 2;

    /// <summary>
    /// 0始まりの番号でページを取得します。
    /// 範囲外、または未設定の場合はnullを返します。
    /// </summary>
    public BookPageData GetPage(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= PageCount)
        {
            return null;
        }

        return m_pages[pageIndex];
    }

    /// <summary>
    /// 指定した見開きの左右ページを取得します。
    /// 見開き番号は0始まりです。
    /// </summary>
    public bool TryGetSpread(
        int spreadIndex,
        out BookPageData leftPage,
        out BookPageData rightPage)
    {
        leftPage = null;
        rightPage = null;

        if (spreadIndex < 0 || spreadIndex >= SpreadCount)
        {
            return false;
        }

        int leftIndex = spreadIndex * 2;

        leftPage = GetPage(leftIndex);
        rightPage = GetPage(leftIndex + 1);

        return true;
    }
}