using System;
using UnityEngine;
using UnityEngine.UI;

// Lives outside the book capture Canvas so the shade covers the entire screen.
public sealed class RuneFailureOverlay : MonoBehaviour
{
    private CanvasGroup m_group;
    private RectTransform m_cross;
    private Func<Rect?> m_screenRect;
    private float m_remaining;
    private float m_duration;
    private float m_scale;

    public bool IsVisible => m_remaining > 0f;

    public void Show(Func<Rect?> screenRect, float duration, float scale, Color shade,
        Color crossColor, Sprite crossSprite)
    {
        m_screenRect = screenRect;
        m_duration = Mathf.Max(0.05f, duration);
        m_remaining = m_duration;
        m_scale = Mathf.Max(0.1f, scale);
        if (m_group == null) Build(shade, crossColor, crossSprite);
        gameObject.SetActive(true);
        m_group.alpha = 1f;
        UpdatePosition();
    }

    private void Build(Color shade, Color crossColor, Sprite sprite)
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 31000;
        gameObject.AddComponent<GraphicRaycaster>();
        m_group = gameObject.AddComponent<CanvasGroup>();

        Image curtain = CreateImage("Screen Shade", transform, shade);
        curtain.rectTransform.anchorMin = Vector2.zero;
        curtain.rectTransform.anchorMax = Vector2.one;
        curtain.rectTransform.offsetMin = Vector2.zero;
        curtain.rectTransform.offsetMax = Vector2.zero;
        curtain.raycastTarget = true;

        m_cross = new GameObject("Failure Cross", typeof(RectTransform)).GetComponent<RectTransform>();
        m_cross.SetParent(transform, false);
        m_cross.anchorMin = m_cross.anchorMax = Vector2.zero;
        if (sprite != null)
        {
            Image image = m_cross.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = crossColor;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }
        else
        {
            // Vector fallback: two uGUI Images make a sharp cross at any resolution.
            for (int i = 0; i < 2; i++)
            {
                Image bar = CreateImage("Cross Bar " + i, m_cross, crossColor);
                bar.raycastTarget = false;
                bar.rectTransform.anchorMin = new Vector2(-0.15f, 0.46f);
                bar.rectTransform.anchorMax = new Vector2(1.15f, 0.54f);
                bar.rectTransform.offsetMin = bar.rectTransform.offsetMax = Vector2.zero;
                bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 45f : -45f);
            }
        }
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        Image image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.color = color;
        return image;
    }

    private void UpdatePosition()
    {
        Rect? screenRect = m_screenRect?.Invoke();
        m_cross.gameObject.SetActive(screenRect.HasValue);
        if (!screenRect.HasValue) return;
        Rect rect = screenRect.Value;
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,
            rect.center, null, out Vector2 local);
        m_cross.position = transform.TransformPoint(local);
        float size = Mathf.Min(rect.width, rect.height) * m_scale;
        m_cross.sizeDelta = new Vector2(size, size);
    }

    private void LateUpdate()
    {
        m_remaining = Mathf.Max(0f, m_remaining - Time.unscaledDeltaTime);
        if (m_remaining <= 0f) { Hide(); return; }
        m_group.alpha = Mathf.Clamp01(m_remaining / Mathf.Min(0.15f, m_duration));
        UpdatePosition();
    }

    public void Hide()
    {
        m_remaining = 0f;
        gameObject.SetActive(false);
    }
}
