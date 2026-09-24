using UnityEngine;
using UnityEngine.Splines;

public abstract class GimmickSpline : Gimmick
{
    // ―――――――――――――――――――――――――――――――――――――
    // Propaty
    // ―――――――――――――――――――――――――――――――――――――
    private SplineController2 m_splineController;

    [Header("Gimmick Spline")]
    [SerializeField] private float m_gimmickPoint;


    // ―――――――――――――――――――――――――――――――――――――
    // Public Propaty
    // ―――――――――――――――――――――――――――――――――――――
    public float GimmickPoint { get { return m_gimmickPoint; } }
    public SplineController2 SplineController { get { return m_splineController; } }
    public SplineContainer Spline { get { return m_splineController?.Spline; } }
    
    
    // ―――――――――――――――――――――――――――――――――――――
    // Unity Event
    // ―――――――――――――――――――――――――――――――――――――
    private void Awake()
    {
        FindSplineController();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        FindSplineController();
    }
#endif


    // ―――――――――――――――――――――――――――――――――――――
    // Private Event
    // ―――――――――――――――――――――――――――――――――――――
    private void FindSplineController()
    {
        if (m_splineController == null)
        {
            m_splineController = GetComponentInParent<SplineController2>();
        }



        // 自分のポジションをSpline上に移動
        if (m_splineController != null)
        {
            SplineContainer spline = Spline;
            float length = spline.CalculateLength();

            float value = m_gimmickPoint / length;
            Vector3 position = spline.EvaluatePosition(value);

            transform.position = position;
        }

    }

    // ―――――――――――――――――――――――――――――――――――――
    // Gizmos
    // ―――――――――――――――――――――――――――――――――――――
    protected void OnGizmosMyPosition()
    {
        if(m_splineController == null)
        {
            return;
        }


        SplineContainer spline = Spline;
        float length = spline.CalculateLength();

        float value = m_gimmickPoint / length;
        Vector3 position = spline.EvaluatePosition(value);

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(position, 0.5f);
    }
}
