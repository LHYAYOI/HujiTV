using UnityEngine;
using UnityEngine.Splines;

[System.Serializable]
public class SplineSwitchPointData2
{
    // ―――――――――――――――――――――――――――――――――――――
    // Propaty
    // ―――――――――――――――――――――――――――――――――――――

    [Tooltip("移動変更できる範囲")]
    [SerializeField] private float m_switchChangeRange;

    [SerializeField] private float m_switchPoint;

    [SerializeField] private SplineController2 m_splineController;

    [SerializeField] private float m_switchOffset;

    // ―――――――――――――――――――――――――――――――――――――
    // Public Propaty
    // ―――――――――――――――――――――――――――――――――――――
    public float SwitchPoint { get { return m_switchPoint; } }
    public float SwitchChangeRange { get { return m_switchChangeRange; } }
    public SplineController2 SplineController {  get { return m_splineController; } }
    public SplineContainer Spline { get { return m_splineController?.Spline; } }

    public float SwitchOffset { get { return m_switchOffset; }}
}