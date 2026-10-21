//-----------------------------------------------
// BookFlipCoordinateMapper.cs
// 制作日：2026/10/21
// 制作者：安田晴人
// 概要： 本のページめくり座標変換
//-----------------------------------------------
using UnityEngine;

namespace YasudaHaruto.BookValidation
{
public class BookFlipCoordinateMapper : MonoBehaviour
{
    [Header("Book")]
    [SerializeField] private YasudaBookViewController m_bookController;
    [SerializeField] private hatake.BookFlipView m_flipView;

    [Header("Display")]
    [SerializeField] private RectTransform m_displayImage;
    [SerializeField] private Camera m_captureCamera;

    public bool TryGetCapturePosition(Vector2 screenPosition, out BOOK_PAGE_SIDE side, out Ray captureRay)
    {
        side = BOOK_PAGE_SIDE.NONE;
        captureRay = default;

        if (m_bookController == null || m_flipView == null || m_displayImage == null || m_captureCamera == null)
        {
            return false;
        }

        if (m_bookController.IsClosed || m_bookController.IsBusy || m_flipView.IsTurning)
        {
            return false;
        }

        Canvas canvas = m_displayImage.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        if (!RectTransformUtility.RectangleContainsScreenPoint(m_displayImage, screenPosition, uiCamera))
        {// UIの表示範囲外
            return false;
        }

        if (!m_flipView.ScreenToBook(m_displayImage, screenPosition, uiCamera, out Vector2 bookPoint))
        {// UIの表示範囲内でも本のページ範囲外
            return false;
        }

        if (!m_flipView.ContainsPage(bookPoint))
        {// 本のページ範囲外
            return false;
        }

        // ページの左右判定
        side = bookPoint.x < 0f ? BOOK_PAGE_SIDE.LEFT : BOOK_PAGE_SIDE.RIGHT;

        // ページUV座標に変換
        Vector2 pageUV = new Vector2(side == BOOK_PAGE_SIDE.LEFT ? bookPoint.x + 1f : bookPoint.x, bookPoint.y / m_flipView.PageAspect);

        // ページUV座標をキャプチャカメラのViewport座標に変換
        Vector4 crop = side == BOOK_PAGE_SIDE.LEFT ? m_flipView.LeftCrop : m_flipView.RightCrop;
        if (crop.z <= 0f || crop.w <= 0f)
        {// キャプチャ範囲が無効
            side = BOOK_PAGE_SIDE.NONE;
            return false;
        }

        // ページUV座標をキャプチャカメラのViewport座標に変換
        Vector2 captureUV = new Vector2(crop.x + pageUV.x * crop.z, crop.y + pageUV.y * crop.w);

        // キャプチャカメラのViewport座標からRayを取得
        captureRay = m_captureCamera.ViewportPointToRay(captureUV);
        return true;
    }
}

}
