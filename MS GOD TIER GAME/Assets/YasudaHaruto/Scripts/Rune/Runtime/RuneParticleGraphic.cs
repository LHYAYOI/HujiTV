using UnityEngine;
using UnityEngine.UI;

// ParticleSystem positions are in this full-screen UI layer's local coordinates.
// Unity's mesh particle renderer cannot draw in Screen Space Overlay, so uGUI submits the quads.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class RuneParticleGraphic : MaskableGraphic
{
    private ParticleSystem m_particles;
    private ParticleSystem.Particle[] m_buffer;
    private Texture m_texture;
    public override Texture mainTexture => m_texture != null ? m_texture : Texture2D.whiteTexture;

    public void Configure(ParticleSystem particles, Texture texture, int capacity)
    {
        m_particles = particles;
        m_buffer = new ParticleSystem.Particle[capacity];
        m_texture = texture;
        raycastTarget = false;
        SetAllDirty();
    }

    public void Refresh() => SetVerticesDirty();

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (m_particles == null || m_buffer == null) return;
        int count = m_particles.GetParticles(m_buffer);
        for (int i = 0; i < count; i++)
        {
            var particle = m_buffer[i];
            Vector2 center = particle.position;
            float half = particle.startSize * 0.5f;
            Color32 tint = particle.startColor;
            int index = vh.currentVertCount;
            vh.AddVert(center + new Vector2(-half, -half), tint, new Vector2(0, 0));
            vh.AddVert(center + new Vector2(-half, half), tint, new Vector2(0, 1));
            vh.AddVert(center + new Vector2(half, half), tint, new Vector2(1, 1));
            vh.AddVert(center + new Vector2(half, -half), tint, new Vector2(1, 0));
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
