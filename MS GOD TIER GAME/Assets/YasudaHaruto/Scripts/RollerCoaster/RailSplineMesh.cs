using UnityEngine;
using UnityEngine.Splines;

namespace YasudaHaruto.RollerCoaster
{
    /// <summary>Editorで生成した線路を保持する。再生中のメッシュ生成は行わない。</summary>
    [DisallowMultipleComponent]
    public sealed class RailSplineMesh : MonoBehaviour
    {
        [SerializeField, Tooltip("既存走行と同じContainer。先頭のSplineを使用します。")]
        private SplineContainer m_spline;
        [SerializeField, Tooltip("長手方向を+Zに合わせて使う線路メッシュ。原本は変更しません。")]
        private Mesh m_sourceMesh;
        [SerializeField] private Material[] m_materials = new Material[0];
        [SerializeField, Tooltip("素材の進行方向を+Z、上方向を+Yへ合わせる回転。")]
        private Vector3 m_sourceRotation;
        [SerializeField] private Vector3 m_sourceScale = Vector3.one;
        [SerializeField, Tooltip("素材断面の中心を横0、高さ0とした配置オフセット（m）。")]
        private Vector2 m_crossSectionOffset;
        [SerializeField, Min(0f), Tooltip("生成開始位置。曲線に沿った実距離（m）。既存ギミックの距離相当値とは別です。")]
        private float m_startDistance;
        [SerializeField, Min(0f), Tooltip("生成終了位置の実距離（m）。0は終点。")]
        private float m_endDistance;
        [SerializeField,Min(0f)] private float m_suppressSleeperMeters;
        public float SuppressSleeperMeters=>m_suppressSleeperMeters;
        [SerializeField, Range(0.02f, 2f), Tooltip("素材を長手方向に分割する最大間隔（m）。小さいほど滑らかになります。")]
        private float m_maxSegmentLength = 0.2f;
        [SerializeField, HideInInspector] private GameObject m_generatedRoot;

        public SplineContainer Spline => m_spline;
        public Mesh SourceMesh => m_sourceMesh;
        public Material[] Materials => m_materials;
        public Vector3 SourceRotation => m_sourceRotation;
        public Vector3 SourceScale => m_sourceScale;
        public Vector2 CrossSectionOffset => m_crossSectionOffset;
        public float StartDistance => m_startDistance;
        public float EndDistance => m_endDistance;
        public float MaxSegmentLength => m_maxSegmentLength;
        public GameObject GeneratedRoot => m_generatedRoot;

#if UNITY_EDITOR
        public void SetGeneratedRoot(GameObject root) { m_generatedRoot = root; }
        public void Configure(SplineContainer spline, Mesh source, Material[] materials)
        {
            m_spline = spline;
            m_sourceMesh = source;
            m_materials = materials;
        }
#endif
    }
}
