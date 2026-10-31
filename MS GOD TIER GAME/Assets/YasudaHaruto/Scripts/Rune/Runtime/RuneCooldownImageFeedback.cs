using UnityEngine;
using UnityEngine.UI;

// Uses the existing page Image; no second sprite configuration or shared material edits.
public sealed class RuneCooldownImageFeedback : MonoBehaviour
{
    private Image m_target;
    private Image m_echo;
    private RuneCooldownImageCoordinates m_coordinates;
    private Material m_originalMaterial;
    private Material m_material;
    private Material m_echoMaterial;
    private float m_elapsed;
    private float m_duration;
    private float m_rise;
    private float m_remaining;
    private bool m_animate;
    private Vector2 m_origin;

    public bool IsVisible => m_target != null && m_material != null;
    public float Recovery => 1f - m_remaining;

    public void Show(Image target, bool animate, float duration, float darkness, float rise)
    {
        Hide();
        if (target == null) return;
        Shader shader = Resources.Load<Shader>("RuneCooldownImage");
        if (shader == null) { Debug.LogError("Rune cooldown shader was not found.", this); return; }
        m_target = target;
        m_originalMaterial = target.material;
        m_material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        m_material.SetFloat("_Darkness", Mathf.Clamp01(darkness));
        m_coordinates = target.gameObject.AddComponent<RuneCooldownImageCoordinates>();
        target.material = m_material;
        m_duration = Mathf.Max(0.001f, duration);
        m_rise = rise;
        m_elapsed = 0f;
        m_remaining = 1f;
        m_animate = animate;
        if (animate)
        {
            m_echo = new GameObject("Rune Release Light", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            m_echo.gameObject.layer = target.gameObject.layer;
            RectTransform copy = m_echo.rectTransform, source = target.rectTransform;
            copy.SetParent(source.parent, false);
            copy.anchorMin = source.anchorMin; copy.anchorMax = source.anchorMax; copy.pivot = source.pivot;
            copy.sizeDelta = source.sizeDelta; copy.anchoredPosition3D = source.anchoredPosition3D;
            copy.localRotation = source.localRotation; copy.localScale = source.localScale;
            copy.SetSiblingIndex(source.GetSiblingIndex() + 1);
            m_origin = copy.anchoredPosition;
            m_echo.sprite = target.overrideSprite;
            m_echo.type = target.type;
            m_echo.preserveAspect = target.preserveAspect;
            m_echo.raycastTarget = false;
            m_echoMaterial = new Material(m_material) { hideFlags = HideFlags.HideAndDontSave };
            m_echoMaterial.SetFloat("_Drain", 0f);
            m_echoMaterial.SetFloat("_Recovery", 0f);
            m_echoMaterial.SetFloat("_Flash", 0.85f);
            m_echo.material = m_echoMaterial;
        }
        Apply();
    }

    public void SetRemaining(float remaining)
    {
        m_remaining = Mathf.Clamp01(remaining);
        Apply();
    }

    private void Update() => Advance(Time.deltaTime);

    public void Advance(float deltaTime)
    {
        if (!IsVisible) return;
        m_elapsed += Mathf.Max(0f, deltaTime);
        Apply();
    }

    private void Apply()
    {
        if (!IsVisible) return;
        float t = m_animate ? Mathf.Clamp01(m_elapsed / m_duration) : 1f;
        m_material.SetFloat("_Recovery", Recovery);
        m_material.SetFloat("_Drain", Mathf.SmoothStep(0f, 1f, t));
        m_material.SetFloat("_Flash", m_animate ? Mathf.Sin(t * Mathf.PI) * 0.65f : 0f);
        if (m_echo == null) return;
        m_echo.rectTransform.anchoredPosition = m_origin + Vector2.up * (m_target.rectTransform.rect.height * m_rise * t);
        Color c = m_target.color;
        c.a *= Mathf.Sin(t * Mathf.PI) * (1f - t) * 0.85f;
        m_echo.color = c;
        if (t >= 1f) m_echo.gameObject.SetActive(false);
    }

    public void Hide()
    {
        if (m_target != null && m_target.material == m_material) m_target.material = m_originalMaterial;
        if (m_echo != null) { m_echo.gameObject.SetActive(false); Release(m_echo.gameObject); }
        if (m_coordinates != null) { m_coordinates.enabled = false; Release(m_coordinates); }
        m_coordinates = null;
        Release(m_material); Release(m_echoMaterial);
        m_target = null; m_echo = null; m_originalMaterial = null; m_material = null; m_echoMaterial = null;
    }

    private static void Release(Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
    }

    private void OnDisable() => Hide();
    private void OnDestroy() => Hide();
}
