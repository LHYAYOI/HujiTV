using UnityEngine;
using UnityEngine.UI;

namespace YasudaHaruto.BookValidation
{
public sealed class ObstaclePatchGraphic : MaskableGraphic
{
    private ObstacleInstance m_instance;
    private float m_progress = -1f;
    private Rect m_uv = new Rect(0, 0, 1, 1);
    private Texture2D m_texture;
    public Rect UVRect => m_uv;
    public override Texture mainTexture => m_texture != null ? m_texture : Texture2D.whiteTexture;
    public bool Bind(ObstacleInstance instance) => Bind(instance, new Rect(0, 0, 1, 1));
    public bool Bind(ObstacleInstance instance, Rect uv) => Bind(instance, uv, instance?.Texture);
    public bool Bind(ObstacleInstance instance, Rect uv, Texture2D texture)
    {
        bool replaced = !ReferenceEquals(m_instance, instance) || m_texture != texture;
        float progress = instance != null ? instance.Progress : 0f;
        if (!replaced && m_progress == progress && m_uv == uv) return false;
        m_uv = uv;
        m_progress = progress;
        if (replaced) { m_instance = instance; m_texture = texture; SetMaterialDirty(); }
        SetVerticesDirty();
        return true;
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (m_instance == null) return;
        Rect rect = GetPixelAdjustedRect();
        Color tint = m_instance.Data.Color;
        tint.a *= 1f - m_instance.Progress;
        vh.AddVert(new Vector3(rect.xMin, rect.yMin), tint, new Vector2(m_uv.xMin, m_uv.yMin));
        vh.AddVert(new Vector3(rect.xMin, rect.yMax), tint, new Vector2(m_uv.xMin, m_uv.yMax));
        vh.AddVert(new Vector3(rect.xMax, rect.yMax), tint, new Vector2(m_uv.xMax, m_uv.yMax));
        vh.AddVert(new Vector3(rect.xMax, rect.yMin), tint, new Vector2(m_uv.xMax, m_uv.yMin));
        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(0, 2, 3);
    }
}
}
