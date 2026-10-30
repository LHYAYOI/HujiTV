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
            SPHERE = 1,
            // 床に溜まる霧。箱の下側に溜まり、上端がノイズで盛り上がる
            GROUND = 2
        }

        [UnityEngine.Serialization.FormerlySerializedAs("shape")]

        public SHAPE m_shape;
        [Min(0), Tooltip("画面上の大きさ・密度と合わせた選別の優先倍率。0=対象外。")]
        public float m_priority = 1;
        [Range(0, 10), Tooltip("この領域の太陽散乱を追加強調。0=通常、2=最大3倍。空間God Rayと霧が必要。境界はEdge Fadeでぼかす。重なりは最大値。")]
        public float m_sunBoost;
        [Range(0, 5), Tooltip("1mあたりの濃さ。全体の霧は0.01〜0.1、床を這う濃い煙は1〜3が目安。")]
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
        [Header("床を這う霧（Shape = GROUND のときだけ使用）")]
        [Range(0.05f, 1), Tooltip("箱の高さのうち霧が溜まる割合。")]
        public float m_groundFill = 0.45f;
        [Range(0.01f, 1), Tooltip("霧の上端のぼかし幅（箱の高さに対する割合）。")]
        public float m_groundSoftness = 0.25f;
        [Min(0.01f), Tooltip("もこもこの細かさ。1mあたりの山の数。")]
        public float m_groundNoiseScale = 1.4f;
        [Range(0, 1), Tooltip("上端の凸凹と内部のむらの強さ。")]
        public float m_groundNoiseAmount = 0.75f;
        [Tooltip("霧の模様が流れる向きと速さ（ワールド、m/s）。")]
        public Vector3 m_groundFlow = new Vector3(0, 0, 0.25f);
        [Tooltip("広がりの中心。未設定なら箱全体に霧を出す。")]
        public Transform m_spreadOrigin;
        [Min(0), Tooltip("広がりの中心からの半径（m）。DoorSmokeが実行中に書き換える。")]
        public float m_spreadRadius = 100;
        [Min(0.01f), Tooltip("広がりの先端のぼかし幅（m）。")]
        public float m_spreadEdge = 1.2f;
        // 描画に寄与するか。広がり半径が0の床霧は何も映らない
        public bool HasVisibleRegion => !(m_shape == SHAPE.GROUND && m_spreadOrigin && m_spreadRadius <= 0);
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
            if (m_shape == SHAPE.SPHERE)
                Gizmos.DrawWireSphere(Vector3.zero, 0.5f);
            else
                Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
            if (m_shape != SHAPE.GROUND)
                return;
            // 霧が溜まる高さの目安
            float level = m_groundFill - 0.5f;
            Gizmos.DrawWireCube(new Vector3(0, level, 0), new Vector3(1, 0, 1));
            if (!m_spreadOrigin)
                return;
            // 広がりの半径（ワールドの水平円）
            Gizmos.matrix = Matrix4x4.identity;
            Vector3 center = m_spreadOrigin.position;
            float radius = Mathf.Min(m_spreadRadius, 50);
            Vector3 previous = center + new Vector3(radius, 0, 0);
            for (int i = 1; i <= 32; i++)
            {
                float angle = i / 32f * Mathf.PI * 2;
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
