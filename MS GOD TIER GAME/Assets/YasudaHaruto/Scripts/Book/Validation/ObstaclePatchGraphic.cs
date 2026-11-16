using UnityEngine;
using UnityEngine.UI;

namespace YasudaHaruto.BookValidation
{
public sealed class ObstaclePatchGraphic : MaskableGraphic
{
    private ObstacleInstance m_instance;
    private float m_progress = -1f;
    public override Texture mainTexture => m_instance?.Data.Texture != null ? m_instance.Data.Texture : Texture2D.whiteTexture;
    public bool Bind(ObstacleInstance instance)
    {
        bool replaced = !ReferenceEquals(m_instance, instance);
        float progress = instance != null ? instance.Progress : 0f;
        if (!replaced && m_progress == progress) return false;
        m_progress = progress;
        if (replaced) { m_instance = instance; SetMaterialDirty(); }
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
        vh.AddVert(new Vector3(rect.xMin, rect.yMin), tint, Vector2.zero);
        vh.AddVert(new Vector3(rect.xMin, rect.yMax), tint, Vector2.up);
        vh.AddVert(new Vector3(rect.xMax, rect.yMax), tint, Vector2.one);
        vh.AddVert(new Vector3(rect.xMax, rect.yMin), tint, Vector2.right);
        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(0, 2, 3);
    }
}
}