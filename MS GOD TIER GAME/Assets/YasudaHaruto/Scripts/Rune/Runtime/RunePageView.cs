//-----------------------------------------------
// RunePageView.cs
// 制作日：2026/10/09
// 制作者：安田晴人
// 概要： ルーンページビューの表示
//-----------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;

public class RunePageView : MonoBehaviour, IRuneTraceView, IRuneTraceFeedbackView, IRuneCooldownView, IRuneCooldownReleaseView
{
    [SerializeField] private RuneLineGraphic m_referenceGraphic;
    [SerializeField] private RuneLineGraphic m_playerGraphic;

    [Header("Trace Feedback")]
    [SerializeField] private Color m_accurateColor = new Color(0.1f, 1f, 0.2f, 1f);
    [SerializeField] private Color m_deviatedColor = new Color(1f, 0.08f, 0.08f, 1f);
    [SerializeField, Min(0.1f)] private float m_deviationColorPower = 1f;
    [Header("Failure Feedback")]
    [SerializeField, Min(0.05f)] private float m_failureDuration = 0.7f;
    [SerializeField, Min(0.1f)] private float m_failureCrossScale = 1.15f;
    [SerializeField] private Color m_failureShade = new Color(0f, 0f, 0f, 0.35f);
    [SerializeField] private Color m_failureCrossColor = new Color(1f, 0.05f, 0.05f, 1f);
    [SerializeField] private Sprite m_failureCrossSprite;

    [Header("Completion Feedback")]
    [SerializeField, Min(0.05f)] private float m_completionDuration = 0.65f;
    [SerializeField, Min(1f)] private float m_completionEndScale = 1.4f;
    private RuneCompletionOverlay m_completionOverlay;
    [Header("Cooldown Feedback")]
    [SerializeField, Range(0.05f, 0.8f)] private float m_cooldownBrightness = 0.28f;
    [SerializeField, Min(0.05f)] private float m_cooldownReleaseDuration = 0.4f;
    [SerializeField, Range(0f, 0.5f)] private float m_cooldownReleaseRise = 0.12f;
    private RuneCooldownImageFeedback m_cooldownFeedback;
    private Func<UnityEngine.UI.Image> m_cooldownImage;
    private bool m_releaseCooldown;
    private float m_releaseCooldownSeconds;

    public void ConfigureCooldownImage(Func<UnityEngine.UI.Image> image) => m_cooldownImage = image;
    public void PlayCooldownRelease(float cooldownSeconds = 1f)
    {
        m_releaseCooldown = true;
        m_releaseCooldownSeconds = Mathf.Max(0f, cooldownSeconds);
    }


    private readonly List<RuneStrokeData> m_completedStrokes = new();
    private readonly List<RuneStrokeData> m_displayStrokes = new();
    private readonly List<Vector2> m_lastCurrentStroke = new();
    private RuneData m_runeData;
    private Func<Rect?> m_screenRect;
    private Func<RectTransform, Rect?> m_graphicScreenRect;
    private RuneFailureOverlay m_failureOverlay;

    public void ConfigureScreenProjection(Func<Rect?> screenRect, Func<RectTransform, Rect?> graphicScreenRect = null)
    {
        m_screenRect = screenRect;
        m_graphicScreenRect = graphicScreenRect;
    }

    public void BeginTrace(RuneData runeData)
    {
        // Retry resets the drawing without erasing the failure animation just started.
        if (m_runeData != runeData) HideFailure();
        HideCooldown();
        m_runeData = runeData;
        HideCompletion();
        BringToFront();
        ClearGraphics();
        SetTraceDeviation(0f);
        if (runeData != null && m_referenceGraphic != null)
            m_referenceGraphic.SetStrokes(runeData.TraceData.Strokes);
    }

    public void SetTraceDeviation(float normalizedDeviation)
    {
        float amount = Mathf.Pow(Mathf.Clamp01(normalizedDeviation), Mathf.Max(0.1f, m_deviationColorPower));
        if (m_playerGraphic != null)
            m_playerGraphic.color = Color.Lerp(m_accurateColor, m_deviatedColor, amount);
    }

    public void UpdateCurrentStroke(IReadOnlyList<Vector2> points)
    {
        m_lastCurrentStroke.Clear();
        m_lastCurrentStroke.AddRange(points);
        m_displayStrokes.Clear();
        m_displayStrokes.AddRange(m_completedStrokes);
        if (points.Count >= 2)
        {
            m_displayStrokes.Add(new RuneStrokeData(points));
        }
        if (m_playerGraphic != null) m_playerGraphic.SetStrokes(m_displayStrokes);
    }

    public void EndStroke()
    {
        if (m_lastCurrentStroke.Count >= 2)
        {
            m_completedStrokes.Add(new RuneStrokeData(m_lastCurrentStroke));
        }
        m_lastCurrentStroke.Clear();
        if (m_playerGraphic != null) m_playerGraphic.SetStrokes(m_completedStrokes);
    }

    public void BringToFront() => transform.SetAsLastSibling();

    public void PlaySuccess(RuneTraceResult result) => PlayCompletion();

    public void PlayCompletion()
    {
        if (!isActiveAndEnabled || m_playerGraphic == null || m_completedStrokes.Count == 0) return;
        Rect? area = GetCompletionScreenRect();
        if (!area.HasValue || m_playerGraphic.rectTransform.rect.width <= 0f) return;
        if (m_completionOverlay == null)
            m_completionOverlay = new GameObject("Rune Completion Overlay", typeof(RectTransform))
                .AddComponent<RuneCompletionOverlay>();
        float width = m_playerGraphic.LineWidth * area.Value.width / m_playerGraphic.rectTransform.rect.width;
        m_completionOverlay.Play(m_completedStrokes, GetCompletionScreenRect, m_playerGraphic.color, width,
            m_completionDuration, m_completionEndScale);
    }

    private Rect? GetCompletionScreenRect()
    {
        return m_graphicScreenRect != null ? m_graphicScreenRect(m_playerGraphic.rectTransform) : GetScreenRect();
    }

    private void HideCompletion()
    {
        if (m_completionOverlay != null) m_completionOverlay.Hide();
    }

    public void PlayFailure(RuneTraceResult result)
    {
        HideCompletion();
        if (!isActiveAndEnabled) return;
        if (m_failureOverlay == null)
            m_failureOverlay = new GameObject("Rune Failure Overlay", typeof(RectTransform))
                .AddComponent<RuneFailureOverlay>();
        m_failureOverlay.Show(GetScreenRect, m_failureDuration, m_failureCrossScale,
            m_failureShade, m_failureCrossColor, m_failureCrossSprite);
    }

    private Rect? GetScreenRect()
    {
        if (m_screenRect != null) return m_screenRect();
        // Regular UI can use its own Rect. Book render textures need the configured mapper.
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null || (canvas.worldCamera != null && canvas.worldCamera.targetTexture != null)) return null;
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var corners = new Vector3[4];
        ((RectTransform)transform).GetWorldCorners(corners);
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (Vector3 corner in corners)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corner);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    public void ShowCooldown(float remainingFraction)
    {
        HideCompletion();
        ClearGraphics();
        if (!isActiveAndEnabled || remainingFraction <= 0f) { HideCooldown(); return; }
        if (m_cooldownFeedback == null) m_cooldownFeedback = gameObject.AddComponent<RuneCooldownImageFeedback>();
        if (!m_cooldownFeedback.IsVisible)
            m_cooldownFeedback.Show(m_cooldownImage?.Invoke(), m_releaseCooldown,
                Mathf.Min(m_cooldownReleaseDuration, m_releaseCooldownSeconds * 0.1f), m_cooldownBrightness, m_cooldownReleaseRise);
        m_releaseCooldown = false;
        m_cooldownFeedback.SetRemaining(remainingFraction);
    }

    private void HideCooldown()
    {
        m_releaseCooldown = false;
        if (m_cooldownFeedback != null) m_cooldownFeedback.Hide();
    }

    private void ClearGraphics()
    {
        m_completedStrokes.Clear();
        m_displayStrokes.Clear();
        m_lastCurrentStroke.Clear();
        if (m_referenceGraphic != null) m_referenceGraphic.Clear();
        if (m_playerGraphic != null) m_playerGraphic.Clear();
    }

    public void Clear()
    {
        HideCooldown();
        m_runeData = null;
        HideCompletion();
        ClearGraphics();
        HideFailure();
    }

    private void HideFailure()
    {
        if (m_failureOverlay != null) m_failureOverlay.Hide();
    }

    private void OnDisable()
    {
        HideCooldown();
        HideFailure();
        HideCompletion();
    }

    private void OnDestroy()
    {
        if (m_failureOverlay != null) Destroy(m_failureOverlay.gameObject);
        if (m_completionOverlay != null) Destroy(m_completionOverlay.gameObject);
    }
}
