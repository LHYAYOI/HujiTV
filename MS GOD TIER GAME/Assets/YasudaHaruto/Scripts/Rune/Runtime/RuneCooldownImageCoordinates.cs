using UnityEngine;
using UnityEngine.UI;

// Encode image-local height before uGUI moves vertices into the Canvas batching space.
public sealed class RuneCooldownImageCoordinates : BaseMeshEffect
{
    protected override void OnEnable()
    {
        base.OnEnable();
        Canvas owner = graphic != null ? graphic.canvas : null;
        if (owner != null) owner.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
    }

    public override void ModifyMesh(VertexHelper mesh)
    {
        if (!IsActive()) return;
        Rect rect = graphic.rectTransform.rect;
        UIVertex vertex = default;
        for (int i = 0; i < mesh.currentVertCount; i++)
        {
            mesh.PopulateUIVertex(ref vertex, i);
            vertex.uv1.x = Mathf.Clamp01((vertex.position.y - rect.yMin) / Mathf.Max(0.0001f, rect.height));
            mesh.SetUIVertex(vertex, i);
        }
    }
}
