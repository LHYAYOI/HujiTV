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

    public bool ContainsScreenPosition(Vector2 screenPosition)
    {
        if (m_runeArea == null)
        {
            return false;
        }

        Canvas canvas = m_runeArea.GetComponentInParent<Canvas>();

        Camera camera = null;

        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            camera = canvas.worldCamera;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(m_runeArea, screenPosition, camera);
    }

    public bool TryConvertToNormalized(Vector2 screenPosition, out Vector2 normalizedPosition)
    {
        normalizedPosition = default;

        if (m_runeArea == null)
        {
            return false;
        }

        Canvas canvas = m_runeArea.GetComponentInParent<Canvas>();

        Camera camera = null;

        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            camera = canvas.worldCamera;
        }

        if (!RectTransformUtility.RectangleContainsScreenPoint(m_runeArea, screenPosition, camera))
        {
            return false;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(m_runeArea, screenPosition, camera, out Vector2 localPosition))
        {
            return false;
        }

        Rect rect = m_runeArea.rect;

        float x = Mathf.InverseLerp(rect.xMin, rect.xMax, localPosition.x);

        float y = Mathf.InverseLerp(rect.yMin, rect.yMax, localPosition.y);

        normalizedPosition = new Vector2(x, y);

        return true;
    }
}