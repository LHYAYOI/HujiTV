using UnityEngine;

public enum OBSTACLE_REMOVAL_TYPE { RUB, SCRATCH }

[CreateAssetMenu(menuName = "Book/Obstacle Data")]
public sealed class ObstacleData : ScriptableObject
{
    [SerializeField] private byte m_obstacleId = 1;
    [SerializeField, Range(1, 4)] private int m_slotCount = 1;
    [SerializeField] private OBSTACLE_REMOVAL_TYPE m_removalType;
    [SerializeField] private Texture2D m_texture;
    [SerializeField] private Color m_color = new Color(0.25f, 0.12f, 0.35f, 0.95f);
    [SerializeField, Min(1)] private int m_rubPasses = 8;
    [SerializeField, Range(0.02f, 0.5f)] private float m_rubDistance = 0.2f;
    [SerializeField, Range(0.02f, 0.3f)] private float m_brushRadius = 0.1f;
    [SerializeField, Range(0.1f, 1f)] private float m_clearRatio = 0.85f;
    public byte ObstacleId => m_obstacleId;
    public int SlotCount => Mathf.Clamp(m_slotCount, 1, 4);
    public OBSTACLE_REMOVAL_TYPE RemovalType => m_removalType;
    public Texture2D Texture => m_texture;
    public Color Color => m_color;
    public int RubPasses => Mathf.Max(1, m_rubPasses);
    public float RubDistance => Mathf.Max(0.02f, m_rubDistance);
    public float BrushRadius => Mathf.Clamp(m_brushRadius, 0.02f, 0.3f);
    public float ClearRatio => Mathf.Clamp(m_clearRatio, 0.1f, 1f);
}
