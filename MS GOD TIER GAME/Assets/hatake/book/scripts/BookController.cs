using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class BookController : MonoBehaviour
{
    [Header("ページデータ")]

    [SerializeField]
    private BookData m_bookData;

    [SerializeField]
    private BookPageView m_leftPageView;

    [SerializeField]
    private BookPageView m_rightPageView;

    [Header("撮影")]

    [SerializeField]
    private Camera m_captureCamera;

    [SerializeField]
    private RenderTexture m_textureA;

    [SerializeField]
    private RenderTexture m_textureB;

    [Header("画面表示")]

    [SerializeField]
    private RawImage m_displayImage;

    private int m_spreadIndex;
    private bool m_captureFlag;

    private RenderTexture m_currentTexture;
    private RenderTexture m_previousTexture;

    // 後で3Dモデルへ渡すための参照
    public RenderTexture CurrentTexture => m_currentTexture;
    public RenderTexture PreviousTexture => m_previousTexture;

    private void OnEnable()
    {
        if (m_displayImage != null)
        {
            m_displayImage.enabled = false;
        }

        ShowSpread(m_spreadIndex);
    }

    public void ShowSpread(int spreadIndex)
    {
        if (!isActiveAndEnabled || m_captureFlag)
        {
            return;
        }

        if (m_bookData == null ||
            m_leftPageView == null ||
            m_rightPageView == null ||
            m_captureCamera == null ||
            m_displayImage == null ||
            m_textureA == null ||
            m_textureB == null)
        {
            Debug.LogWarning(
                "BookControllerの参照をすべて設定してください。",
                this);
            return;
        }

        if (m_textureA == m_textureB)
        {
            Debug.LogWarning(
                "Texture AとBには別々のRender Textureを設定してください。",
                this);
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

        StartCoroutine(CaptureSpread(
            spreadIndex,
            leftPage,
            rightPage));
    }

    public void ShowNextSpread()
    {
        ShowSpread(m_spreadIndex + 1);
    }

    public void ShowPreviousSpread()
    {
        ShowSpread(m_spreadIndex - 1);
    }

    private IEnumerator CaptureSpread(
        int spreadIndex,
        BookPageData leftPage,
        BookPageData rightPage)
    {
        m_captureFlag = true;

        // 現在表示していないTextureへ描画
        RenderTexture targetTexture =
            m_currentTexture == m_textureA
                ? m_textureB
                : m_textureA;

        m_captureCamera.targetTexture = targetTexture;

        m_leftPageView.ShowPage(leftPage);
        m_rightPageView.ShowPage(rightPage);

        Canvas.ForceUpdateCanvases();

        m_captureCamera.enabled = true;

        yield return new WaitForEndOfFrame();

        // 撮影時点の状態を保持
        m_captureCamera.enabled = false;

        m_previousTexture = m_currentTexture;
        m_currentTexture = targetTexture;
        m_spreadIndex = spreadIndex;

        m_displayImage.texture = m_currentTexture;
        m_displayImage.enabled = true;

        m_captureFlag = false;
    }

    private void OnDisable()
    {
        StopAllCoroutines();

        m_captureFlag = false;

        if (m_captureCamera != null)
        {
            m_captureCamera.enabled = false;
        }

        if (m_displayImage != null)
        {
            m_displayImage.enabled = false;
            m_displayImage.texture = null;
        }

        m_currentTexture = null;
        m_previousTexture = null;
    }
}