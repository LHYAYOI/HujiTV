using UnityEngine;
using UnityEngine.UI;

public sealed class RuneBulletPreview : MonoBehaviour
{
    private const int PreviewLayer = 31;
    private Camera m_camera;
    private RenderTexture m_target;
    private RawImage m_image;
    private Material m_material;

    public bool Show(MagicBulletController prefab, RectTransform canvas)
    {
        Transform visual = prefab != null ? prefab.transform.Find("Visual") : null;
        if (visual == null || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            return false;

        // Only the visual subtree is instantiated. The projectile controller and hit collider
        // are siblings in the actual bullet and stay on the gameplay side.
        gameObject.SetActive(false);
        transform.position = new Vector3(0f, -2000f, 0f);
        Transform instance = Instantiate(visual, transform, false);
        foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = PreviewLayer;

        m_target = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32)
        {
            name = "Rune Bullet Preview",
            useMipMap = false
        };
        m_camera = new GameObject("Rune Bullet Camera").AddComponent<Camera>();
        m_camera.transform.SetParent(transform, false);
        m_camera.transform.localPosition = new Vector3(0f, 0f, -3f);
        m_camera.clearFlags = CameraClearFlags.SolidColor;
        m_camera.backgroundColor = Color.clear;
        m_camera.cullingMask = 1 << PreviewLayer;
        m_camera.orthographic = true;
        m_camera.orthographicSize = 0.65f;
        m_camera.nearClipPlane = 0.1f;
        m_camera.farClipPlane = 6f;
        m_camera.allowHDR = false;
        m_camera.allowMSAA = false;
        m_camera.useOcclusionCulling = false;
        m_camera.targetTexture = m_target;

        m_image = new GameObject("Actual Bullet Visual", typeof(RectTransform)).AddComponent<RawImage>();
        m_image.transform.SetParent(canvas, false);
        m_image.texture = m_target;
        m_image.raycastTarget = false;
        Shader shader = Resources.Load<Shader>("RuneSpell/RuneCastComposite");
        if (shader != null)
        {
            m_material = new Material(shader);
            m_material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m_image.material = m_material;
        }

        return true;
    }

    public void Draw(Vector2 localPoint, float size, float opacity)
    {
        gameObject.SetActive(true);
        m_image.gameObject.SetActive(true);
        m_image.rectTransform.anchoredPosition = localPoint;
        m_image.rectTransform.sizeDelta = Vector2.one * size;
        m_image.color = new Color(1f, 1f, 1f, opacity);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        if (m_image != null) m_image.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (m_image != null) Destroy(m_image.gameObject);
        if (m_material != null) Destroy(m_material);
        if (m_target != null)
        {
            if (m_camera != null) m_camera.targetTexture = null;
            m_target.Release();
            Destroy(m_target);
        }
    }
}
