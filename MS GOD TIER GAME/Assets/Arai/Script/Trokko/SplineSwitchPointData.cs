using UnityEngine;
using UnityEngine.Splines;

[System.Serializable]
public class SplineSwitchPointData
{
    // ―――――――――――――――――――――――――――――――――――――
    // Propaty
    // ―――――――――――――――――――――――――――――――――――――

    [Tooltip("移動できる範囲")]
    [SerializeField] private Vector2 m_switchStartEnd;

    [Tooltip("移動先SplineのOffset")]
    [SerializeField] private float m_switchStartOffset;

    [Tooltip("移動先Spline")]
    [SerializeField] private SplineContainer m_spline;


    // ―――――――――――――――――――――――――――――――――――――
    // Public Propaty
    // ―――――――――――――――――――――――――――――――――――――
    public Vector2 SwitchStartEnd { get { return m_switchStartEnd; } }
    public float SwitchStartOffset { get { return m_switchStartOffset; }}
    public SplineContainer Spline { get { return m_spline; }}
}
