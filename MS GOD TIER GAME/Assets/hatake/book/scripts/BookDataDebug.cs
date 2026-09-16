
using UnityEngine;

/// <summary>
/// BookDataDebugは、BookDataの内容をデバッグ表示するためのコンポーネント

public sealed class BookDataDebug : MonoBehaviour
{
    [SerializeField]
    private BookData m_bookData;

    private void Start()
    {
        if (m_bookData == null)
        {
            Debug.LogWarning(
                "Book Dataを設定して",
                this);
            return;
        }

        Debug.Log(
            $"{m_bookData.BookTitle}: " +
            $"{m_bookData.PageCount}ページ / " +
            $"{m_bookData.SpreadCount}見開き",
            this);

        bool getSpreadFlag = m_bookData.TryGetSpread(
            0,
            out BookPageData leftPage,
            out BookPageData rightPage);

        if (!getSpreadFlag)
        {
            return;
        }

        string leftTitle = leftPage != null
            ? leftPage.PageName
            : "空白";

        string rightTitle = rightPage != null
            ? rightPage.PageName
            : "空白";

        Debug.Log(
            $"最初の見開き: 左「{leftTitle}」 / 右「{rightTitle}」",
            this);
    }
}