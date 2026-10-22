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

    // Inverse of the input mapping: capture area -> left-page crop -> book output -> screen.
    public bool TryGetScreenRect(RectTransform area, out Rect screenRect)
    {
        screenRect = default;
        if (area == null || m_flipView == null || m_displayImage == null || m_captureCamera == null)
            return false;
        Vector4 crop = m_flipView.LeftCrop;
        if (crop.z <= 0f || crop.w <= 0f) return false;
        Vector2 bookMin = m_flipView.UvToBook(Vector2.zero);
        Vector2 bookMax = m_flipView.UvToBook(Vector2.one);
        Vector2 extent = bookMax - bookMin;
        if (Mathf.Abs(extent.x) < 0.0001f || Mathf.Abs(extent.y) < 0.0001f) return false;
        Canvas canvas = m_displayImage.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        var corners = new Vector3[4];
        area.GetWorldCorners(corners);
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (Vector3 corner in corners)
        {
            Vector3 capture = m_captureCamera.WorldToViewportPoint(corner);
            if (capture.z <= 0f) return false;
            Vector2 page = new Vector2((capture.x - crop.x) / crop.z, (capture.y - crop.y) / crop.w);
            Vector2 book = new Vector2(page.x - 1f, page.y * m_flipView.PageAspect);
            Vector2 uv = new Vector2((book.x - bookMin.x) / extent.x, (book.y - bookMin.y) / extent.y);
            Rect rect = m_displayImage.rect;
            Vector3 world = m_displayImage.TransformPoint(new Vector3(
                Mathf.LerpUnclamped(rect.xMin, rect.xMax, uv.x),
                Mathf.LerpUnclamped(rect.yMin, rect.yMax, uv.y), 0f));
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCamera, world);
            min = Vector2.Min(min, screen);
            max = Vector2.Max(max, screen);
        }
        screenRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return screenRect.width > 0f && screenRect.height > 0f;
    }

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
