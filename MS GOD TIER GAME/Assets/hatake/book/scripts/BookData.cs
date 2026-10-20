using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// BookDataは、1冊の本の情報を管理するScriptableObjectです
/// Contents（新）に BookPageData（1ページ）と BookSpreadData（見開きセット）を混ぜて並べられる。
/// Contents が空なら従来の Pages（旧）を使う。

[CreateAssetMenu(
    fileName = "Book_New",
    menuName = "Book/Book Data")]
public sealed class BookData : ScriptableObject
{
    [Header("本の情報")]

    [SerializeField]
    private string m_bookTitle = "";

    [Header("ページ順（旧）")]

    [Tooltip("先頭から、左・右・左・右の順に登録します。Contents が空のときに使います")]
    [SerializeField]
    private List<BookPageData> m_pages =
        new List<BookPageData>();

    [Header("ページ順（新）")]

    [Tooltip("BookPageData（1ページ）と BookSpreadData（見開きセット）を順に並べます。" +
             "見開きセットは必ず左ページから始まるよう、必要なら手前に白紙を入れます")]
    [SerializeField]
    private List<ScriptableObject> m_contents =
        new List<ScriptableObject>();

    private List<BookPageData> m_expandedPages;
    private List<BookSpreadData> m_expandedSets;

    public string BookTitle => m_bookTitle;
    public int PageCount { get { Expand(); return m_expandedPages.Count; } }

    [Tooltip("ページ数が奇数の場合、最後の右ページは空白")]
    public int SpreadCount => (PageCount + 1) / 2;

    /// <summary>
    /// 0始まりの番号でページを取得します
    /// 範囲外、または未設定の場合はnullを返す
    /// </summary>
    public BookPageData GetPage(int pageIndex)
    {
        Expand();
        if (pageIndex < 0 || pageIndex >= m_expandedPages.Count)
        {
            return null;
        }

        return m_expandedPages[pageIndex];
    }

    /// <summary>ページが見開きセットの一部ならそのセット。旧方式のページは null</summary>
    public BookSpreadData GetSpreadSet(int pageIndex)
    {
        Expand();
        if (pageIndex < 0 || pageIndex >= m_expandedSets.Count) return null;
        return m_expandedSets[pageIndex];
    }

    /// <summary>
    /// 0始まりの見開き番号から、左右ページを取得します
    /// 戻り値は見開き番号が有効かどうかを表す
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

    /// <summary>実行時のページ一覧へ書き出す（pages と sets は同じ長さ）</summary>
    public void CopyTo(List<BookPageData> pages, List<BookSpreadData> sets)
    {
        m_expandedPages = null;   // セット側のアセットが編集されていても最新を読む
        Expand();
        pages.AddRange(m_expandedPages);
        sets.AddRange(m_expandedSets);
    }

    private void Expand()
    {
        if (m_expandedPages != null) return;
        m_expandedPages = new List<BookPageData>();
        m_expandedSets = new List<BookSpreadData>();
        if (m_contents == null || m_contents.Count == 0)
        {
            foreach (BookPageData page in m_pages)
            {
                m_expandedPages.Add(page);
                m_expandedSets.Add(null);
            }
            return;
        }
        foreach (ScriptableObject entry in m_contents)
        {
            if (entry is BookSpreadData spread)
            {
                // 見開きセットは左ページから始める
                if (m_expandedPages.Count % 2 == 1)
                {
                    m_expandedPages.Add(null);
                    m_expandedSets.Add(null);
                }
                m_expandedPages.Add(spread.LeftPage);
                m_expandedSets.Add(spread);
                m_expandedPages.Add(spread.RightPage);
                m_expandedSets.Add(spread);
            }
            else if (entry is BookPageData page)
            {
                m_expandedPages.Add(page);
                m_expandedSets.Add(null);
            }
            else if (entry == null)
            {
                // 空欄は白紙ページ
                m_expandedPages.Add(null);
                m_expandedSets.Add(null);
            }
        }
    }

    private void OnEnable() => m_expandedPages = null;

    private void OnValidate()
    {
        m_expandedPages = null;
        for (int i = 0; i < m_contents.Count; i++)
        {
            ScriptableObject entry = m_contents[i];
            if (entry != null && !(entry is BookPageData) && !(entry is BookSpreadData))
            {
                Debug.LogWarning($"{name}: Contents[{i}] ({entry.name}) は BookPageData か BookSpreadData を指定してください", this);
            }
        }
    }

#if UNITY_EDITOR
    [ContextMenu("旧 Pages を Contents へ移す")]
    private void MovePagesToContents()
    {
        UnityEditor.Undo.RecordObject(this, "Move pages to contents");
        foreach (BookPageData page in m_pages) m_contents.Add(page);
        m_pages.Clear();
        m_expandedPages = null;
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
