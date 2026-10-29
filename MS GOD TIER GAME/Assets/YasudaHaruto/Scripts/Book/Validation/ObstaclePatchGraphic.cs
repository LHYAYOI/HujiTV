using UnityEngine;
using UnityEngine.UI;

namespace YasudaHaruto.BookValidation
{
public sealed class ObstaclePatchGraphic : MaskableGraphic
{
    private ObstacleInstance m_instance;
    private float m_progress = -1f;
    public override Texture mainTexture => m_instance?.Data.Texture != null ? m_instance.Data.Texture : Texture2D.whiteTexture;
    public void Bind(ObstacleInstance instance)
    {
        bool replaced = !ReferenceEquals(m_instance, instance);
        float progress = instance.Interaction.Progress;
        if (!replaced && m_progress == progress) return;
        m_progress = progress;
        if (replaced) { m_instance = instance; SetMaterialDirty(); }
        SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (m_instance == null) return;
        Rect rect = GetPixelAdjustedRect();
        int count = m_instance.Interaction is ScratchObstacleInteraction ? ScratchObstacleInteraction.RESOLUTION : 1;
        for (int y = 0; y < count; y++)
            for (int x = 0; x < count; x++)
            {
                float alpha = m_instance.Interaction.Coverage(x, y);
                if (alpha <= 0) continue;
                Color tint = m_instance.Data.Color;
                tint.a *= alpha;
                int start = vh.currentVertCount;
                float x0 = (float)x / count, x1 = (float)(x + 1) / count;
                float y0 = (float)y / count, y1 = (float)(y + 1) / count;
                vh.AddVert(new Vector3(rect.x + x0 * rect.width, rect.y + y0 * rect.height), tint, new Vector2(x0, y0));
                vh.AddVert(new Vector3(rect.x + x0 * rect.width, rect.y + y1 * rect.height), tint, new Vector2(x0, y1));
                vh.AddVert(new Vector3(rect.x + x1 * rect.width, rect.y + y1 * rect.height), tint, new Vector2(x1, y1));
                vh.AddVert(new Vector3(rect.x + x1 * rect.width, rect.y + y0 * rect.height), tint, new Vector2(x1, y0));
                vh.AddTriangle(start, start + 1, start + 2);
                vh.AddTriangle(start, start + 2, start + 3);
            }
    }
}
}
