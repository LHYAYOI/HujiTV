using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// オバケなど、近づくと緊張させるもの。HeartbeatVignette がこの距離を見て、画面の縁を心拍のように脈打たせる。
/// オバケのプレハブに付けるだけでよい。
/// </summary>
[DisallowMultipleComponent, AddComponentMenu("hatake/Lighting/Ghost Threat")]
public sealed class GhostThreat : MonoBehaviour
{
    [Min(0.1f), Tooltip("この距離（m）より近づくと緊張が高まり始める。")]
    public float m_radius = 6;
    [Range(0, 1), Tooltip("このオバケの怖さ。1 で最大。")]
    public float m_weight = 1;

    private static readonly List<GhostThreat> s_active = new List<GhostThreat>();

    public static IReadOnlyList<GhostThreat> Active { get { return s_active; } }

    private void OnEnable()
    {
        if (!s_active.Contains(this))
        {
            s_active.Add(this);
        }
    }

    private void OnDisable()
    {
        s_active.Remove(this);
    }

    /// <summary>listener の位置での緊張の強さ（0〜1）。</summary>
    public float ThreatAt(Vector3 listener)
    {
        float distance = Vector3.Distance(transform.position, listener);
        float closeness = 1 - Mathf.Clamp01(distance / m_radius);
        return closeness * closeness * (3 - 2 * closeness) * m_weight;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.8f, 0.2f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, m_radius);
    }
}
