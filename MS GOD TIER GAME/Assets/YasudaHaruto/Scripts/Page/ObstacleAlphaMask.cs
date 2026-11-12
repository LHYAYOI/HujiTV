using System;
using UnityEngine;

// Small, editor-baked occupancy map. Source textures remain non-readable on devices.
[Serializable]
public sealed class ObstacleAlphaMask
{
    [SerializeField] private Texture2D m_texture;
    [SerializeField] private int m_width, m_height;
    [SerializeField] private byte[] m_bits;
    public Texture2D Texture => m_texture;

    public ObstacleAlphaMask(Texture2D texture, int width, int height, byte[] bits)
    { m_texture = texture; m_width = width; m_height = height; m_bits = bits; }

    public bool Contains(Vector2 uv)
    {
        if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1 || m_width < 1 || m_height < 1 || m_bits == null) return false;
        int x = Mathf.Min(m_width - 1, (int)(uv.x * m_width));
        int y = Mathf.Min(m_height - 1, (int)(uv.y * m_height));
        int index = y * m_width + x;
        return index / 8 < m_bits.Length && (m_bits[index / 8] & (1 << (index % 8))) != 0;
    }

    public bool Matches(ObstacleAlphaMask other)
    {
        if (other == null || m_texture != other.m_texture || m_width != other.m_width || m_height != other.m_height ||
            m_bits == null || other.m_bits == null || m_bits.Length != other.m_bits.Length) return false;
        for (int i = 0; i < m_bits.Length; i++) if (m_bits[i] != other.m_bits[i]) return false;
        return true;
    }
}

[Serializable]
public sealed class ObstacleImagePart
{
    [SerializeField] private Texture2D m_texture;
    [SerializeField] private Rect m_placement;
    public Texture2D Texture => m_texture;
    public Rect Placement => m_placement;
}
