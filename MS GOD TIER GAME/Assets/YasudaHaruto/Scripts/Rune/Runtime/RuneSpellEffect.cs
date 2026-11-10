using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class RuneSpellEffect : MonoBehaviour
{
    private enum PHASE { HIDDEN, GATHERING, READY, FLYING }

    private const float TransformDuration = 0.18f;
    private PHASE m_phase;
    private RuneSpellSettings m_settings;
    private Func<Rect?> m_bookRect;
    private Func<Vector2> m_origin;
    private RuneLineGraphic m_trace;
    private RuneBulletPreview m_preview;
    private Rect m_source;
    private Vector2 m_pivot;
    private float m_traceExtent;
    private float m_elapsed, m_sideAngle, m_transformProgress;
    private bool m_dragging;
    private Color m_traceColor;
    private Vector2 m_drag, m_launchStart, m_launchDirection, m_center;

    public bool IsReady => m_phase == PHASE.READY;
    public bool IsFlying => m_phase == PHASE.FLYING;
    public bool IsVisible => m_phase != PHASE.HIDDEN && gameObject.activeSelf;
    public Vector2 Center => m_center;
    public float GatherProgress => m_phase == PHASE.GATHERING ? Mathf.Clamp01(m_elapsed / GatherDuration) : 1f;
    private float GatherDuration => m_settings.FloatDuration + m_settings.GatherDuration;

    public void Play(IReadOnlyList<RuneStrokeData> strokes, Rect source, Func<Rect?> bookRect,
        Func<Vector2> origin, RuneSpellSettings settings, Color color, float width, float sideAngle, MagicData magic)
    {
        Hide();
        EnsureCanvas();
        m_settings = settings;
        m_source = source;
        m_bookRect = bookRect;
        m_origin = origin;
        m_sideAngle = sideAngle;
        m_elapsed = 0f;
        m_drag = Vector2.zero;

        var snapshot = new List<RuneStrokeData>(strokes.Count);
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
        Vector2 traceSize = Vector2.Scale(max - min, m_source.size);
        m_traceExtent = Mathf.Max(1f, Mathf.Max(traceSize.x, traceSize.y) + width);
        m_trace.rectTransform.pivot = m_pivot;
        m_trace.rectTransform.sizeDelta = m_source.size;
        m_traceColor = color;
        m_trace.LineWidth = width;
        m_trace.SetStrokes(snapshot);
        m_trace.enabled = true;

        var catalog = Resources.Load<RuneBulletPreviewCatalog>("RuneSpell/BulletPreviews");
        MagicBulletController bullet = catalog != null ? catalog.Find(magic) : null;
        if (bullet != null)
        {
            m_preview = new GameObject("Rune Bullet Preview").AddComponent<RuneBulletPreview>();
            if (m_preview.Show(bullet, (RectTransform)transform)) m_preview.Hide();
            else DisposePreview();
        }
        m_phase = PHASE.GATHERING;
        gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        Advance(0f);
    }

    public void SetDrag(Vector2 offset)
    {
        if (!IsReady) return;
        m_dragging = true;
        m_drag = offset;
    }

    public void CancelDrag()
    {
        if (!IsReady) return;
        m_dragging = false;
        m_transformProgress = 0f;
        m_drag = Vector2.zero;
        m_preview?.Hide();
        Advance(0f);
    }

    public void Launch(MAGIC_CAST_DIRECTION direction)
    {
        if (!IsReady || direction == MAGIC_CAST_DIRECTION.NONE) return;
        float angle = ((int)direction - 1) * m_sideAngle * Mathf.Deg2Rad;
        m_launchDirection = m_drag.sqrMagnitude > 0f ? m_drag.normalized : new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
        Advance(0f);
        if (!IsReady) return;
        m_launchStart = m_center + m_drag * (1f - Mathf.SmoothStep(0f, 1f, m_transformProgress));
        m_elapsed = 0f;
        m_dragging = true;
        m_phase = PHASE.FLYING;
    }

    private void LateUpdate() => Advance(Time.unscaledDeltaTime);

    public void Advance(float deltaTime)
    {
        if (m_phase == PHASE.HIDDEN) return;
        Rect? book = m_bookRect?.Invoke();
        if (!book.HasValue || book.Value.height <= 0f) { Hide(); return; }
        m_elapsed += Mathf.Max(0f, deltaTime);
        Vector2 origin = m_origin != null ? m_origin() : new Vector2(0.5f, 0.39f);
        Vector2 home = book.Value.min + Vector2.Scale(origin, book.Value.size);
        float height = book.Value.height;
        float progress = GatherProgress;

        if (m_phase == PHASE.GATHERING && progress >= 1f) m_phase = PHASE.READY;
        if (m_dragging && IsReady)
            m_transformProgress = Mathf.MoveTowards(m_transformProgress, 1f, deltaTime / TransformDuration);
        float morph = IsFlying ? 1f : Mathf.SmoothStep(0f, 1f, m_transformProgress);
        m_center = home + m_drag * morph;
        // The opaque part of the glow spans about 91% of its image height.
        float scale = height * RuneSpellSettings.GlowHeightRatio * 0.91f * 0.75f / m_traceExtent;
        if (m_phase == PHASE.GATHERING)
        {
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            Vector2 start = m_source.min + Vector2.Scale(m_pivot, m_source.size);
            m_center = Vector2.LerpUnclamped(start, home, eased);
            m_center.y += Mathf.Sin(progress * Mathf.PI) * height * m_settings.FloatHeight;
            scale = Mathf.Lerp(1f, scale, eased);
        }
        else if (IsFlying)
        {
            float t = m_elapsed / m_settings.FlightDuration;
            if (t >= 1f) { Hide(); return; }
            float distance = new Vector2(Screen.width, Screen.height).magnitude + height;
            m_center = m_launchStart + m_launchDirection * distance * t * t;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, m_center, null, out Vector2 local);
        bool hasBullet = m_preview != null;
        m_trace.enabled = !hasBullet || morph < 1f;
        m_trace.rectTransform.anchoredPosition = local;
        m_trace.rectTransform.localScale = Vector3.one * (hasBullet ? scale * Mathf.Lerp(1f, 0.32f, morph) : scale);
        Color traceTint = m_traceColor;
        if (hasBullet) traceTint.a *= 1f - morph;
        m_trace.color = traceTint;
        if (hasBullet && m_dragging)
            m_preview.Draw(local, height * m_settings.CoreSize * 18f * Mathf.Lerp(0.6f, 1f, morph), morph);
    }

    private void EnsureCanvas()
    {
        if (m_trace != null) return;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30500;
        m_trace = new GameObject("Moving Handwritten Rune", typeof(RectTransform)).AddComponent<RuneLineGraphic>();
        m_trace.transform.SetParent(transform, false);
        m_trace.raycastTarget = false;
    }

    public void Hide()
    {
        m_phase = PHASE.HIDDEN;
        m_dragging = false;
        m_transformProgress = 0f;
        m_drag = Vector2.zero;
        if (m_trace != null) m_trace.Clear();
        DisposePreview();
        gameObject.SetActive(false);
    }

    private void DisposePreview()
    {
        if (m_preview == null) return;
        m_preview.Hide();
        Destroy(m_preview.gameObject);
        m_preview = null;
    }

    private void OnDestroy() => DisposePreview();
}
