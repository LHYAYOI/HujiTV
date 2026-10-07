//-----------------------------------------------
// BookDisplayController.cs
// 制作日：2026/10/06
// 制作者：安田晴人
// 概要： 本の見開き表示を管理するコンポーネント
//-----------------------------------------------
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class BookDisplayController : MonoBehaviour
{
    [Header("Page View")]
    [SerializeField] private BookPageView m_leftPageView;
    [SerializeField] private BookPageView m_rightPageView;

    [Header("Capture")]
    [SerializeField] private Camera m_captureCamera;
    [SerializeField] private RenderTexture m_textureA;
    [SerializeField] private RenderTexture m_textureB;

    [Header("Display")]
    [SerializeField] private RawImage m_displayImage;
    [SerializeField] private BookModelView m_modelView;

    private RenderTexture m_currentTexture;
    private bool m_isUpdating;

    public bool IsUpdating => m_isUpdating;

    public void ShowInitial(PageData pageData, Action onCompleted = null)
    {
        if (pageData == null || m_isUpdating)
        {
            return;
        }

        StartCoroutine(CaptureInitial(pageData, onCompleted));
    }

    public void PlayTurn(PageData nextPage, bool forward, Action onCompleted = null)
    {
        if (nextPage == null || m_isUpdating || m_currentTexture == null)
        {
            return;
        }

        StartCoroutine(CaptureAndTurn(nextPage, forward, onCompleted));
    }

    private IEnumerator CaptureInitial(PageData pageData, Action onCompleted)
    {
        m_isUpdating = true;

        if (!m_modelView.Initialize())
        {
            m_isUpdating = false;
            yield break;
        }

        RenderTexture targetTexture = m_textureA;

        SetPageViews(pageData);

        m_captureCamera.targetTexture = targetTexture;
        m_captureCamera.enabled = true;

        Canvas.ForceUpdateCanvases();
        yield return new WaitForEndOfFrame();

        m_modelView.ShowSpread(targetTexture);
        m_displayImage.texture = m_modelView.OutputTexture;

        m_currentTexture = targetTexture;
        m_displayImage.enabled = true;
        m_isUpdating = false;

        onCompleted?.Invoke();
    }

    private IEnumerator CaptureAndTurn(PageData nextPage, bool forward, Action onCompleted)
    {
        m_isUpdating = true;

        RenderTexture targetTexture = m_currentTexture == m_textureA ? m_textureB : m_textureA;

        SetPageViews(nextPage);

        m_captureCamera.targetTexture = targetTexture;

        Canvas.ForceUpdateCanvases();
        yield return new WaitForEndOfFrame();

        yield return m_modelView.PlayTurn(m_currentTexture, targetTexture, forward);

        m_currentTexture = targetTexture;
        m_isUpdating = false;

        onCompleted?.Invoke();
    }

    private void SetPageViews(PageData pageData)
    {
        m_leftPageView.ShowPage(pageData.LeftVisualData);
        m_rightPageView.ShowPage(pageData.RightVisualData);
    }
}