//-----------------------------------------------
// RuneInputArea.cs
// 制作日：2026/09/30
// 制作者：安田晴人
// 概要： ルーン入力エリアの判定
//-----------------------------------------------
using UnityEngine;

public class RuneInputArea : MonoBehaviour
{
    [SerializeField] private RectTransform m_runeArea;
    [SerializeField] private BookRuneCoordinateMapper m_coordinateMapper;

    public bool ContainsScreenPosition(Vector2 screenPosition)
    {
        if (m_coordinateMapper != null)
        {
            return m_coordinateMapper.ContainsScreenPosition(screenPosition);
        }

        return TryConvertToNormalized(screenPosition, out _);
    }

    public bool TryConvertToNormalized(Vector2 screenPosition, out Vector2 normalizedPosition, bool allowOutside = false)
    {
        if (m_coordinateMapper != null)
        {
            return m_coordinateMapper.TryConvertToNormalized(screenPosition, out normalizedPosition, allowOutside);
        }

        normalizedPosition = default;

        if (m_runeArea == null)
        {
            return false;
        }

        Canvas canvas = m_runeArea.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(m_runeArea, screenPosition, camera, out Vector2 localPosition))
        {
            return false;
        }

        Rect rect = m_runeArea.rect;

        if (rect.width <= 0f || rect.height <= 0f)
        {
            return false;
        }

        normalizedPosition = new Vector2((localPosition.x - rect.xMin) / rect.width, (localPosition.y - rect.yMin) / rect.height);

        return allowOutside || (normalizedPosition.x >= 0f && normalizedPosition.x <= 1f && normalizedPosition.y >= 0f && normalizedPosition.y <= 1f);
    }

}