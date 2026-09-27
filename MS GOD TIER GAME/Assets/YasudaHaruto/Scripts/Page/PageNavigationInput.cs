using UnityEngine;

public class PageNavigationInput
{
    private const float SWIPE_THRESHOLD = 0.15f;

    private bool m_isDragging;
    private Vector2 m_startPosition;

    public bool IsDragging => m_isDragging;

    public bool Begin(Vector2 position)
    {
        if (m_isDragging)
        {
            return false;
        }

        m_startPosition = position;
        m_isDragging = true;

        return true;
    }

    public bool End(Vector2 position, out PAGE_NAVIGATION_DIRECTION direction)
    {
        direction = default;

        if (!m_isDragging)
        {
            return false;
        }

        m_isDragging = false;

        Vector2 delta = position - m_startPosition;

        if (Mathf.Abs(delta.x) < SWIPE_THRESHOLD)
        {
            return false;
        }

        // c•ûŒü‚Ì“®‚«‚ª‰¡•ûŒü‚æ‚è‘å‚«‚¢ê‡‚Í
        // ƒy[ƒW‚ß‚­‚è‚Æ‚µ‚Äˆµ‚í‚È‚¢
        if (Mathf.Abs(delta.y) > Mathf.Abs(delta.x))
        {
            return false;
        }

        direction = delta.x < 0f ? PAGE_NAVIGATION_DIRECTION.NEXT : PAGE_NAVIGATION_DIRECTION.PREVIOUS;

        return true;
    }

    public void Cancel()
    {
        m_isDragging = false;
    }
}