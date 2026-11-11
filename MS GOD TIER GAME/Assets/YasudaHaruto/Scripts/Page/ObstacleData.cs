using UnityEngine;

[CreateAssetMenu(menuName = "Book/Obstacle Data")]
public sealed class ObstacleData : ScriptableObject
{
    [SerializeField] private Texture2D m_texture;
    [SerializeField] private Color m_color = Color.white;
    [Tooltip("Total rub distance, in units of the patch's shorter side.")]
    [SerializeField, Min(0.1f)] private float m_requiredDistance = 4f;
    [Tooltip("Movements smaller than this distance from the last anchor are ignored.")]
    [SerializeField, Range(0.001f, 0.02f)] private float m_minimumMovement = 0.003f;
    public Texture2D Texture => m_texture;
    public Color Color => m_color;
    public float RequiredDistance => Mathf.Max(0.1f, m_requiredDistance);
    public float MinimumMovement => Mathf.Clamp(m_minimumMovement, 0.001f, 0.02f);
}