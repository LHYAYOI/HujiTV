using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BookControllerは、BookDataの見開きを撮影し、表示するためのコンポーネント

public sealed class BookController : MonoBehaviour
{
    [Header("ページデータ")]
    [SerializeField] private BookData m_bookData;
    [SerializeField] private BookPageView m_leftPageView;
    [SerializeField] private BookPageView m_rightPageView;
    [Header("撮影")]
    [SerializeField] private Camera m_captureCamera;
    [SerializeField] private RenderTexture m_textureA;
    [SerializeField] private RenderTexture m_textureB;
    [Header("画面表示")]
    [SerializeField] private RawImage m_displayImage;
    [Header("3D表示（未設定なら従来の2D表示）")]
    [SerializeField] private BookModelView m_modelView;

    private int m_spreadIndex;
    private bool m_captureFlag;
    private RenderTexture m_currentTexture;
    private RenderTexture m_previousTexture;

    public RenderTexture CurrentTexture => m_currentTexture;
    public RenderTexture PreviousTexture => m_previousTexture;

    private void OnEnable()
    {
        if (m_displayImage != null) m_displayImage.enabled = false;
        ShowSpread(m_spreadIndex);
    }

    public void ShowSpread(int spreadIndex)
    {
        if (!isActiveAndEnabled || m_captureFlag) return;
        if (m_bookData == null || m_leftPageView == null ||
            m_rightPageView == null || m_captureCamera == null ||
            m_displayImage == null || m_textureA == null || m_textureB == null)
        {
            Debug.LogWarning("BookControllerの参照をすべて設定", this);
            return;
        }
        if (m_textureA == m_textureB)
        {
            Debug.LogError("Texture_A/Bには別々のRenderTextureが必要", this);
            return;
        }
        if (!m_captureCamera.gameObject.activeInHierarchy)
        {
            Debug.LogError("撮影CameraのGameObjectを有効に", this);
            return;
        }
        bool getSpreadFlag = m_bookData.TryGetSpread(
            spreadIndex, out BookPageData leftPage, out BookPageData rightPage);
        if (!getSpreadFlag) return;
        if (m_modelView != null && !m_modelView.Initialize()) return;
        StartCoroutine(CaptureSpread(spreadIndex, leftPage, rightPage));
    }

    public void ShowNextSpread() => ShowSpread(m_spreadIndex + 1);
    public void ShowPreviousSpread() => ShowSpread(m_spreadIndex - 1);

    private IEnumerator CaptureSpread(
        int spreadIndex, BookPageData leftPage, BookPageData rightPage)
    {
        m_captureFlag = true;
        bool advanceFlag = spreadIndex > m_spreadIndex;
        bool turnFlag = m_currentTexture != null && spreadIndex != m_spreadIndex;
        RenderTexture targetTexture = m_currentTexture == m_textureA ? m_textureB : m_textureA;

        m_captureCamera.targetTexture = targetTexture;
        m_leftPageView.ShowPage(leftPage);
        m_rightPageView.ShowPage(rightPage);

        Canvas.ForceUpdateCanvases();

        m_captureCamera.enabled = true;
        yield return new WaitForEndOfFrame();

        // m_captureCamera.enabled = false;

        if (m_modelView != null)
        {
            m_displayImage.texture = m_modelView.OutputTexture;
            if (turnFlag)
            {
                // 入力ロックは撮影だけでなく、めくり終了まで維持
                yield return m_modelView.PlayTurn(m_currentTexture, targetTexture, advanceFlag);
            }
            else
            {
                m_modelView.ShowSpread(targetTexture);
            }
            // 撮影終了はフレーム末,
            // 次フレームで3DCameraを描画
            yield return null;
            yield return new WaitForEndOfFrame();
        }
        else
        {
            m_displayImage.texture = targetTexture;
        }
        m_previousTexture = m_currentTexture;
        m_currentTexture = targetTexture;
        m_spreadIndex = spreadIndex;
        m_displayImage.enabled = true;
        m_captureFlag = false;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        m_captureFlag = false;
        if (m_captureCamera != null) m_captureCamera.enabled = false;
        if (m_modelView != null) m_modelView.StopDisplay();
        if (m_displayImage != null)
        {
            m_displayImage.enabled = false;
            m_displayImage.texture = null;
        }
        m_currentTexture = null;
        m_previousTexture = null;
    }
}
