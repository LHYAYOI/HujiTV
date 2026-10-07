//-----------------------------------------------
// MouseBookPointerInput.cs
// 制作日：2026/09/20
// 制作者：安田晴人
// 概要： マウス入力を本のページめくりのポインタ入力として扱うクラス
//-----------------------------------------------
using UnityEngine;

public class MouseBookPointerInput : MonoBehaviour, IBookPointerInput
{
    [SerializeField] private RectTransform m_inputArea;

    public bool TryGetPointerDown(out BookPointerData data)
    {
        if (Input.GetMouseButtonDown(0) && TryGetNormalizedPosition(out Vector2 position))
        {
            data = new BookPointerData(position, Input.mousePosition);

            return true;
        }

        data = default;
        return false;
    }

    public bool TryGetPointer(out BookPointerData data)
    {
        if (Input.GetMouseButton(0) && TryGetNormalizedPosition(out Vector2 position))
        {
            data = new BookPointerData(position, Input.mousePosition);

            return true;
        }

        data = default;
        return false;
    }

    public bool TryGetPointerUp(out BookPointerData data)
    {
        if (Input.GetMouseButtonUp(0) && TryGetNormalizedPosition(out Vector2 position))
        {
            data = new BookPointerData(position, Input.mousePosition);

            return true;
        }

        data = default;
        return false;
    }

    private bool TryGetNormalizedPosition(out Vector2 normalizedPosition)
    {
        normalizedPosition = default;

        if (m_inputArea == null)
        {
            return false;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(m_inputArea, Input.mousePosition, null, out Vector2 localPosition))
        {
            return false;
        }

        Rect rect = m_inputArea.rect;

        float x = Mathf.InverseLerp(rect.xMin, rect.xMax, localPosition.x);

        float y = Mathf.InverseLerp(rect.yMin, rect.yMax, localPosition.y);

        // TraceArea外なら入力として扱わない
        if (x < 0f || x > 1f || y < 0f || y > 1f)
        {
            return false;
        }

        normalizedPosition = new Vector2(x, y);

        return true;
    }
}