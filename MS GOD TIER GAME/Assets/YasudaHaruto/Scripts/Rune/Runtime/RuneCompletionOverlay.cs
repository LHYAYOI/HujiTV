using System;
using System.Collections.Generic;
using UnityEngine;

// A snapshot of the player's completed strokes. It never takes pointer input.
public sealed class RuneCompletionOverlay : MonoBehaviour
{
    private RuneLineGraphic m_graphic;
    private Func<Rect?> m_screenRect;
    private float m_elapsed, m_duration, m_endScale;
    private Color m_color;
    private Vector2 m_pivot;

    public bool IsVisible => gameObject.activeSelf && m_graphic != null;

    public void Play(IReadOnlyList<RuneStrokeData> strokes, Func<Rect?> screenRect, Color color,
        float lineWidth, float duration, float endScale)
    {
        if (m_graphic == null)
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30500;
            m_graphic = new GameObject("Completed Rune", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(RuneLineGraphic)).GetComponent<RuneLineGraphic>();
            m_graphic.transform.SetParent(transform, false);
            m_graphic.raycastTarget = false;
        }
        var snapshot = new List<RuneStrokeData>();
        Vector2 min = Vector2.one, max = Vector2.zero;
        foreach (RuneStrokeData stroke in strokes)
        {
            snapshot.Add(new RuneStrokeData(stroke.Points));
            foreach (Vector2 point in stroke.Points)
            {
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
        }
        m_pivot = (min + max) * 0.5f;
        m_screenRect = screenRect;
        m_color = color;
        m_elapsed = 0f;
        m_duration = Mathf.Max(0.05f, duration);
        m_endScale = Mathf.Max(1f, endScale);
        m_graphic.LineWidth = lineWidth;
        m_graphic.SetStrokes(snapshot);
        gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        ApplyProgress(0f);
    }

    private void ApplyProgress(float progress)
    {
        Rect? area = m_screenRect?.Invoke();
        if (!area.HasValue) { Hide(); return; }
        Rect rect = area.Value;
        RectTransform target = m_graphic.rectTransform;
        target.pivot = m_pivot;
        target.sizeDelta = rect.size;
        Vector2 screen = rect.min + Vector2.Scale(m_pivot, rect.size);
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, screen, null, out Vector2 local);
        target.position = transform.TransformPoint(local);
        float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));
        target.localScale = Vector3.one * Mathf.Lerp(1f, m_endScale, eased);
        Color tint = m_color;
        tint.a *= 1f - eased;
        m_graphic.color = tint;
    }

    private void LateUpdate()
    {
        m_elapsed += Time.unscaledDeltaTime;
        if (m_elapsed >= m_duration) { Hide(); return; }
        ApplyProgress(m_elapsed / m_duration);
    }

    public void Hide()
    {
        if (m_graphic != null) m_graphic.Clear();
        gameObject.SetActive(false);
    }
}
