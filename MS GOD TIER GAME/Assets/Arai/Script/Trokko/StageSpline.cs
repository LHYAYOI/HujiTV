using UnityEngine;

public class StageSpline : MonoBehaviour
{
    [SerializeField] private SplineController2 m_startSplineController;
    [SerializeField] private float m_startOffset = 0.0f;

    [SerializeField] private CartController m_cart;



    private void Start()
    {
        m_cart.SetSpline(m_startSplineController.Spline, m_startOffset, m_startSplineController.LoopFlag);
    }
}
