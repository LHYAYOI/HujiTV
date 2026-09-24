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
    private GimmickContext m_gimmickContext = null;


    private void Start()
    {
        SwitchSpline(m_startSplineController, m_startOffset);

        m_gimmickContext = new GimmickContext(
            m_cart,
            SwitchSpline,
            SetCartSpeed);
    }

    private void Update()
    {
        if(m_currentSplineController == null)
        {
            return;
        }

        var gimmicks = m_currentSplineController.Gimmicks;
        
        foreach(var g in gimmicks)
        {
            if(g.CanExecute(m_gimmickContext))
            {
                g.Execute(m_gimmickContext);
            }
        }
    }

    private void SwitchSpline(SplineController2 controller,float offset)
    {
        m_cart.SetSpline(controller.Spline, offset, controller.LoopFlag);

        m_currentSplineController = controller;
    }

    private void SetCartSpeed(float speed , float time)
    {
        StartCoroutine(m_cart.SetSpeed(speed, time));
    }
}
