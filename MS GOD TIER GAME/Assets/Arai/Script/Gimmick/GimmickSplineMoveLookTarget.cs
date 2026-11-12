using UnityEngine;
using UnityEngine.Splines;

public class GimmickSplineMoveLookTarget : MonoBehaviour
{

    // ―――――――――――――――――――――――――――――――――――――
    // Propaty
    // ―――――――――――――――――――――――――――――――――――――
    [SerializeField] private GameObject m_target;
    [SerializeField] private SplineContainer m_spline;
    [SerializeField] private float m_duration = 0.5f;

    [SerializeField] private GimmickSplineRange m_range;
    [SerializeField] private AnimationCurve m_curve;




    private void Awake()
    {
        // event register 
        m_range.OnEnterRange += OnEnterRange;
        m_range.OnInRange += OnInRange;


        // 
        if(m_target != null && m_spline != null)
        {
            m_target.transform.position = m_spline.EvaluatePosition(0.0f);
        }
    }

    private void OnDestroy()
    {
        // event remove
        m_range.OnEnterRange -= OnEnterRange;
        m_range.OnInRange -= OnInRange;
    }


    private void OnEnterRange()
    {
        if(m_target != null)
        {
            CameraManager.Instance.RegisterLookTarget(m_target);
            CameraManager.Instance.SetLookTarget(m_duration);
        }
    }

    private void OnInRange(CartController cart , float rate)
    {
        m_target.transform.position = m_spline.EvaluatePosition(m_curve.Evaluate(rate));
    }
}
