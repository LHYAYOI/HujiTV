//-----------------------------------------------
// BookRuneCoordinateMapper.cs
// 制作日：2026/10/09
// 制作者：安田晴人
// 概要： 本のルーン座標をスクリーン座標に変換するためのマッパークラス
//-----------------------------------------------

using UnityEngine;

public class BookRuneCoordinateMapper : MonoBehaviour
{
    [SerializeField] private RectTransform m_displayArea;

    [Header("Rune Corners (Display Normalized)")]
    [SerializeField] private Vector2 m_topLeft = new(0f, 1f);
    [SerializeField] private Vector2 m_topRight = new(1f, 1f);
    [SerializeField] private Vector2 m_bottomLeft = new(0f, 0f);
    [SerializeField] private Vector2 m_bottomRight = new(1f, 0f);

    private const float EPSILON = 0.000001f;

    private void Update()
    {
        if (!Input.GetMouseButtonDown(0) || m_displayArea == null) return;

        Canvas canvas = m_displayArea.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            m_displayArea, Input.mousePosition, camera, out Vector2 local)) return;

        Rect rect = m_displayArea.rect;
        Vector2 normalized = new(
            (local.x - rect.xMin) / rect.width,
            (local.y - rect.yMin) / rect.height);

    }

    public bool ContainsScreenPosition(Vector2 screenPosition)
    {
        return TryConvertToNormalized(screenPosition, out _);
    }

    public bool TryConvertToNormalized(Vector2 screenPosition, out Vector2 position, bool allowOutside = false)
    {
        position = default;

        if (m_displayArea == null) return false;

        Canvas canvas = m_displayArea.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            m_displayArea, screenPosition, camera, out Vector2 local)) return false;

        Rect rect = m_displayArea.rect;
        if (rect.width <= 0f || rect.height <= 0f) return false;

        Vector2 point = new(
            (local.x - rect.xMin) / rect.width,
            (local.y - rect.yMin) / rect.height);

        if (!TryInverseBilinear(point, out position)) return false;

        return allowOutside || (position.x >= 0f && position.x <= 1f &&
                                position.y >= 0f && position.y <= 1f);
    }

    private bool TryInverseBilinear(Vector2 point, out Vector2 uv)
    {
        uv = new Vector2(0.5f, 0.5f);

        Vector2 a = m_bottomLeft;
        Vector2 b = m_bottomRight - m_bottomLeft;
        Vector2 c = m_topLeft - m_bottomLeft;
        Vector2 d = m_bottomLeft - m_bottomRight - m_topLeft + m_topRight;

        for (int i = 0; i < 16; i++)
        {
            Vector2 predicted = a + b * uv.x + c * uv.y + d * (uv.x * uv.y);
            Vector2 error = predicted - point;

            if (error.sqrMagnitude < EPSILON * EPSILON) return true;

            Vector2 dx = b + d * uv.y;
            Vector2 dy = c + d * uv.x;

            float determinant = dx.x * dy.y - dx.y * dy.x;
            if (Mathf.Abs(determinant) < EPSILON) return false;

            float stepX = (error.x * dy.y - error.y * dy.x) / determinant;
            float stepY = (dx.x * error.y - dx.y * error.x) / determinant;

            uv -= new Vector2(stepX, stepY);
        }

        Vector2 finalPoint = a + b * uv.x + c * uv.y + d * (uv.x * uv.y);
        return (finalPoint - point).sqrMagnitude < 0.000001f;
    }
}
