using System.Collections.Generic;
using UnityEngine;

namespace LightShaftLab
{
    [AddComponentMenu(""), ExecuteAlways, DisallowMultipleComponent]
    public sealed class FogSimulationDomain : MonoBehaviour
    {
        [Tooltip("世界座標で軸に平行な共有領域の大きさ。回転・Scaleには依存しません。")]
        public Vector3 m_size = new Vector3(8, 5, 8);
        [Range(32, 64)] public int m_resolution = 32;
        [Min(0)] public float m_priority = 1;
        [UnityEngine.Serialization.FormerlySerializedAs("m_pin")]
        public bool m_pinFlag;
        [Min(0)] public float m_maxDisplacement = .5f;
        [Min(.01f)] public float m_restoration = 1.5f;
        [Min(.01f)] public float m_velocityDamping = 2;
        public Vector3 m_wind;
        public static readonly List<FogSimulationDomain> g_active = new List<FogSimulationDomain>();
        public Bounds Bounds => new Bounds(transform.position, new Vector3(Mathf.Max(.1f, m_size.x), Mathf.Max(.1f, m_size.y), Mathf.Max(.1f, m_size.z)));
        public int Resolution => m_resolution < 48 ? 32 : 64;
        void OnEnable() { if (!g_active.Contains(this)) g_active.Add(this); }
        void OnDisable() => g_active.Remove(this);
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan; Gizmos.DrawWireCube(Bounds.center, Bounds.size);
        }
    }
}
