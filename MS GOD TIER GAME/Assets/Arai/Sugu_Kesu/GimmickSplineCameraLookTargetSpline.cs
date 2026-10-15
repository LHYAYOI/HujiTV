using UnityEngine;


[RequireComponent(typeof(CameraLookTarget))]
public class GimmickSplineCameraLookTargetSpline : GimmickSpline
{
    // range 0 - 1 で 注視点を spline 0 - 1 で動かす

    [SerializeField] private SplineController2 m_splineController;
    [SerializeField] private GimmickSplineRange m_range;

    private CameraLookTarget m_cameraLookTarget;

    private void Awake()
    {
        if (m_range != null)
            m_range.OnInRange += LookAt;

        m_cameraLookTarget = GetComponent<CameraLookTarget>();
    }

    private void OnDestroy()
    {
        if (m_range != null)
            m_range.OnInRange -= LookAt;
    }


    public void LookAt(CartController cart, float rate)
    {
        if (m_splineController == null)
        {
            return;
        }


        Vector3 position = m_splineController.Spline.EvaluatePosition(rate);

        m_cameraLookTarget.LookAt(position, 0.0f);
    }
}
