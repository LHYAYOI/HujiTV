using UnityEngine;
using UnityEngine.Splines;

namespace YasudaHaruto.RollerCoaster
{
    /// <summary>仮コースの設計値。形の更新はEditorの明示操作で行う。</summary>
    [DisallowMultipleComponent]
    public sealed class RollerCoasterCourse : MonoBehaviour
    {
        [Header("入口の上りと直線下降（m）")]
        [SerializeField, Min(4f)] private float m_entryLength = 14f;
        [SerializeField, Min(0.2f)] private float m_entryRise = 2f;
        [SerializeField, Min(12f)] private float m_descentLength = 48f;
        [SerializeField, Min(2f)] private float m_descentDrop = 36f;
        [Header("縮小するらせん（m）")]
        [SerializeField, Range(1, 5)] private int m_turns = 2;
        [SerializeField, Min(5f)] private float m_startRadius = 11.5f;
        [SerializeField, Min(5f)] private float m_endRadius = 8.5f;
        [SerializeField, Min(5f)] private float m_spiralDrop = 28f;
        [SerializeField, Range(12, 48)] private int m_pointsPerTurn = 16;
        [Header("線路のない落下（m）")]
        [SerializeField, Min(4f)] private float m_fallApproachLength = 8f;
        [SerializeField, Min(2f)] private float m_fallForwardLength = 8f;
        [SerializeField, Min(2f)] private float m_fallDrop = 12f;
        // 旧コースの保存値は保持するが、画像準拠の帰路生成には使わない。
        [SerializeField, HideInInspector] private float m_flatLength = 12f;
        [SerializeField, HideInInspector] private float m_hillHalfLength = 14f;
        [Header("着地後の上り・下り（m）")]
        [SerializeField, Min(0.2f)] private float m_firstHillHeight = 7.5f;
        [SerializeField, Min(0.2f)] private float m_secondHillHeight = 5.5f;
        [SerializeField, Min(0.2f), Tooltip("最後の青区間が、着地点よりさらに下がる高さ。")]
        private float m_exitDrop = 7.5f;
        [SerializeField, HideInInspector] private float m_exitLength = 10f;
        [SerializeField, HideInInspector] private float m_returnRadius = 26f;
        [SerializeField, HideInInspector] private float m_returnAngle = 180f;
        [SerializeField, HideInInspector] private float m_returnSway = 1f;
        [Header("画像に沿った帰路の大きさ（上から見た幅・奥行き、m）")]
        [SerializeField, Min(50f)] private float m_returnWidth = 85f;
        [SerializeField, Min(40f)] private float m_returnDepth = 58f;
        [Header("生成済みの経路と線路")]
        [SerializeField] private SplineContainer m_spline;
        [SerializeField] private RailSplineMesh m_beforeFall;
        [SerializeField] private RailSplineMesh m_afterFall;
        [SerializeField, HideInInspector] private int m_crestKnot;
        [SerializeField, HideInInspector] private int m_spiralStartKnot;
        [SerializeField, HideInInspector] private int m_spiralEndKnot;
        [SerializeField, HideInInspector] private int m_fallStartKnot;
        [SerializeField, HideInInspector] private int m_fallEndKnot;
        [SerializeField, HideInInspector] private int m_hillStartKnot;
        [SerializeField, HideInInspector] private int[] m_returnSectionKnots = new int[0];

        public float EntryLength => m_entryLength;
        public float EntryRise => m_entryRise;
        public float DescentLength => m_descentLength;
        public float DescentDrop => m_descentDrop;
        public int Turns => m_turns;
        public float StartRadius => m_startRadius;
        public float EndRadius => m_endRadius;
        public float SpiralDrop => m_spiralDrop;
        public int PointsPerTurn => m_pointsPerTurn;
        public float FallApproachLength => m_fallApproachLength;
        public float FallForwardLength => m_fallForwardLength;
        public float FallDrop => m_fallDrop;
        public float FlatLength => m_flatLength;
        public float HillHalfLength => m_hillHalfLength;
        public float FirstHillHeight => m_firstHillHeight;
        public float SecondHillHeight => m_secondHillHeight;
        public float ExitLength => m_exitLength;
        public float ExitDrop => m_exitDrop;
        public float ReturnWidth => m_returnWidth;
        public float ReturnDepth => m_returnDepth;
        public float ReturnRadius => m_returnRadius;
        public float ReturnAngle => m_returnAngle;
        public float ReturnSway => m_returnSway;
        public SplineContainer Spline => m_spline;
        public RailSplineMesh BeforeFall => m_beforeFall;
        public RailSplineMesh AfterFall => m_afterFall;
        public int CrestKnot => m_crestKnot;
        public int SpiralStartKnot => m_spiralStartKnot;
        public int SpiralEndKnot => m_spiralEndKnot;
        public int FallStartKnot => m_fallStartKnot;
        public int FallEndKnot => m_fallEndKnot;
        public int HillStartKnot => m_hillStartKnot;
        public int[] ReturnSectionKnots => m_returnSectionKnots;

#if UNITY_EDITOR
        public void SetGeneratedReferences(SplineContainer spline, RailSplineMesh before, RailSplineMesh after,
            int crest, int spiralStart, int spiralEnd, int fallStart, int fallEnd, int hillStart = 0, int[] returnSections = null)
        {
            m_spline = spline; m_beforeFall = before; m_afterFall = after;
            m_crestKnot = crest; m_spiralStartKnot = spiralStart; m_spiralEndKnot = spiralEnd;
            m_fallStartKnot = fallStart; m_fallEndKnot = fallEnd;
            m_hillStartKnot = hillStart;
            if (returnSections != null) m_returnSectionKnots = returnSections;
        }
#endif
    }
}
