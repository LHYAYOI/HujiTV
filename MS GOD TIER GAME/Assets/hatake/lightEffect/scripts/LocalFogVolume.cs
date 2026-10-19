using System.Collections.Generic;
using UnityEngine;

namespace LightShaftLab
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class LocalFogVolume : MonoBehaviour
    {
        public enum SHAPE
        {
            BOX = 0,
            SPHERE = 1
        }

        [UnityEngine.Serialization.FormerlySerializedAs("shape")]

        public SHAPE m_shape;
        [Min(0), Tooltip("画面上の大きさ・密度と合わせた選別の優先倍率。0=対象外。")]
        public float m_priority = 1;
        [Range(0, 10), Tooltip("この領域の太陽散乱を追加強調。0=通常、2=最大3倍。空間God Rayと霧が必要。境界はEdge Fadeでぼかす。重なりは最大値。")]
        public float m_sunBoost;
        [Range(0, 0.5f)]
        [UnityEngine.Serialization.FormerlySerializedAs("density")]
        public float m_density = 0.1f;
        [UnityEngine.Serialization.FormerlySerializedAs("tint")]
        public Color m_tint = new Color(0.7f, 0.85f, 1);
        [Range(0.01f, 1)]
        [UnityEngine.Serialization.FormerlySerializedAs("edgeFade")]
        public float m_edgeFade = 0.3f;
        [Tooltip("領域内の密度を減らして霧を抜く。")]
        [UnityEngine.Serialization.FormerlySerializedAs("subtract")]
        public bool m_subtractFlag;
        [HideInInspector, Tooltip("互換性のため保持。動的Fogは現在停止中です。")]
        [UnityEngine.Serialization.FormerlySerializedAs("m_reactive")]
        public bool m_reactiveFlag;
        [HideInInspector] public FogSimulationDomain m_motionDomain;
        [HideInInspector, Range(0, 1)] public float m_motionStrength = 1;
        public Bounds RenderBounds
        {
            get
            {
                var matrix = transform.localToWorldMatrix;
                Vector3 extent = (Abs(matrix.MultiplyVector(Vector3.right)) + Abs(matrix.MultiplyVector(Vector3.up)) + Abs(matrix.MultiplyVector(Vector3.forward))) * .5f;
                bool movingFlag = FogSimulationManager.IsAvailable && (m_reactiveFlag || (FogSimulationManager.Instance && FogSimulationManager.Instance.HasMotionState(this)));
                float padding = movingFlag && m_motionDomain && !m_subtractFlag ? Mathf.Max(0, m_motionDomain.m_maxDisplacement) : 0;
                return new Bounds(transform.position, extent * 2 + Vector3.one * padding * 2);
            }
        }
        static Vector3 Abs(Vector3 vector) => new Vector3(Mathf.Abs(vector.x), Mathf.Abs(vector.y), Mathf.Abs(vector.z));
        public static readonly List<LocalFogVolume> g_active = new List<LocalFogVolume>();
        void OnEnable()
        {
            if (!g_active.Contains(this))
                g_active.Add(this);
        }

        void OnDisable() => g_active.Remove(this);
        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = m_subtractFlag ? Color.red : m_tint;
            if (m_shape == SHAPE.BOX)
                Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
            else
                Gizmos.DrawWireSphere(Vector3.zero, 0.5f);
        }
    }
}
