using UnityEngine;

public sealed class BookController : MonoBehaviour
{
    [SerializeField]
    private BookData m_bookData;

    [SerializeField]
    private BookPageView m_leftPageView;

    [SerializeField]
    private BookPageView m_rightPageView;

    private int m_spreadIndex;

    private void Start()
    {
        ShowSpread(0);
    }

    public void ShowSpread(int spreadIndex)
    {
        if (m_bookData == null ||
            m_leftPageView == null ||
            m_rightPageView == null)
        {
            Debug.LogWarning("本と左右の表示先を設定してください。", this);
            return;
        }

        bool getSpreadFlag = m_bookData.TryGetSpread(
            spreadIndex,
            out BookPageData leftPage,
            out BookPageData rightPage);

        if (!getSpreadFlag)
        {
            return;
        }

        m_spreadIndex = spreadIndex;

        m_leftPageView.ShowPage(leftPage);
        m_rightPageView.ShowPage(rightPage);
    }

    public void ShowNextSpread()
    {
        ShowSpread(m_spreadIndex + 1);
    }

    public void ShowPreviousSpread()
    {
        ShowSpread(m_spreadIndex - 1);
    }
}