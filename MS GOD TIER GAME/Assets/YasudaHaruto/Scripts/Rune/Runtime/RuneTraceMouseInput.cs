using UnityEngine;

public class RuneTraceMouseInput : MonoBehaviour
{
    private RuneTraceController m_controller;

    private RectTransform m_traceArea;

    public void Initialize(RuneTraceController controller, RectTransform traceArea)
    {
        m_controller = controller;
        m_traceArea = traceArea;
    }

    private void Update()
    {
        if (m_controller == null || !m_controller.IsTracing)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (TryGetNormalizedPosition(out Vector2 position))
            {
                m_controller.BeginStroke(position);
            }
        }

        if (Input.GetMouseButton(0))
        {
            if (TryGetNormalizedPosition(out Vector2 position))
            {
                m_controller.AddPoint(position);
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            m_controller.EndStroke();
        }
    }

    private bool TryGetNormalizedPosition(out Vector2 normalizedPosition)
    {
        normalizedPosition = default;

        if (m_traceArea == null)
        {
            return false;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(m_traceArea, Input.mousePosition, null, out Vector2 localPoint))
        {
            return false;
        }

        Rect rect = m_traceArea.rect;

        float x = Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x);
        float y = Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y);

        if (x < 0f || x > 1f || y < 0f || y > 1f)
        {
            return false;
        }

        normalizedPosition = new Vector2(x, y);

        return true;
    }
}