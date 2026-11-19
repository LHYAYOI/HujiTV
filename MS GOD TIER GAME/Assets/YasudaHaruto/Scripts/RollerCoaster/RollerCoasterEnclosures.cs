using UnityEngine;

namespace YasudaHaruto.RollerCoaster
{
    /// <summary>走行経路に沿った囲いをEditorで生成するための設定。</summary>
    [DisallowMultipleComponent]
    public sealed class RollerCoasterEnclosures : MonoBehaviour
    {
        [SerializeField] private RollerCoasterCourse m_course;
        [SerializeField] private Mesh m_sourceMesh;
        [SerializeField] private Material[] m_palette;
        [SerializeField, Min(3f)] private float m_width = 4.8f;
        [SerializeField, Min(3f)] private float m_height = 4.8f;
        [SerializeField] private float m_centerHeight = 1.8f;
        [SerializeField, Range(0.5f, 3f)] private float m_spacing = 2.2f;
        [SerializeField, Range(0f, 2f)] private float m_overlap = 1.4f;
        [SerializeField] private int m_colorSeed = 173;
        [SerializeField] private bool m_smooth = true;
        [SerializeField] private bool m_useSourceDuct;
        public bool UseSourceDuct => m_useSourceDuct;
        [SerializeField, Range(0f,.8f)] private float m_cornerRadius=.45f;
        public float CornerRadius => m_cornerRadius;
        [SerializeField, Range(0.1f, 0.5f)] private float m_meshStep = 0.25f;
        [SerializeField, Min(3f)] private float m_fallClearance = 6f;
        [SerializeField, HideInInspector] private GameObject m_generatedRoot;
        public RollerCoasterCourse Course => m_course;
        public Mesh SourceMesh => m_sourceMesh;
        public Material[] Palette => m_palette;
        public float Width => m_width;
        public float Height => m_height;
        public float CenterHeight => m_centerHeight;
        public float Spacing => m_spacing;
        public float Overlap => m_overlap;
        public int ColorSeed => m_colorSeed;
        public bool Smooth => m_smooth;
        public float MeshStep => m_meshStep;
        public float FallClearance => m_fallClearance;
        public GameObject GeneratedRoot => m_generatedRoot;
#if UNITY_EDITOR
        public void Configure(RollerCoasterCourse course, Mesh mesh, Material[] palette)
        { m_course = course; m_sourceMesh = mesh; m_palette = palette; }
        public void SetGeneratedRoot(GameObject root) { m_generatedRoot = root; }
#endif
    }
}
