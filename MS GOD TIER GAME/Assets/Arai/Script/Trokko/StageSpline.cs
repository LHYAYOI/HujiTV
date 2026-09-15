using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Splines;

public class StageSpline : MonoBehaviour
{
    [SerializeField] private SplineController2 m_startSplineController;
    [SerializeField] private float m_startOffset = 0.0f;

    [SerializeField] private CartController m_cart;


    // runtime
    private SplineController2 m_currentSplineController = null;
    private SplineSwitchPointData2 m_switchPoint = null;


    private void Start()
    {
        m_currentSplineController = m_startSplineController;

        m_cart.SetSpline(
            m_currentSplineController.Spline, 
            m_startOffset,
            m_currentSplineController.LoopFlag);
    }

    private void Update()
    {
        // 乗り換えポイントの切り替え
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if(m_switchPoint != null)
            {
                m_switchPoint = null;
            }
            else
            {
                float currentDistanceOffset = m_cart.SplineOffset;

                m_switchPoint = m_currentSplineController.GetSwitchPointByDistance(currentDistanceOffset);
            }
        }

        // 乗り換え
        if(m_switchPoint != null)
        {
            float currentOffset = m_cart.SplineOffset;

            if(currentOffset > m_switchPoint.SwitchPoint)
            {
                // 乗り換え実行
                m_currentSplineController = m_switchPoint.SplineController;

                m_cart.SetSpline(
                    m_currentSplineController.Spline, 
                    m_switchPoint.SwitchOffset,
                    m_currentSplineController.LoopFlag);

                m_switchPoint = null;
            }
        }
    }


    private void OnDrawGizmos()
    {
        if(m_currentSplineController == null)
        {
            return;
        }

        if(m_switchPoint != null)
        {
            SplineContainer spline = m_currentSplineController.Spline;
            float distance = m_switchPoint.SwitchPoint / spline.CalculateLength();

            Gizmos.color = Color.green;
            Gizmos.DrawSphere(
               (Vector3)spline.EvaluatePosition(distance)
               + Vector3.up * 3.0f,
               1.0f);
        }
    }
}
