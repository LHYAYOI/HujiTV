using UnityEngine;


[RequireComponent(typeof(CameraLookTarget))]
public class GimmickSplineCameraLookTarget : GimmickSpline
{
    [SerializeField] private Transform m_target;
    [SerializeField] private float m_time;

    private CameraLookTarget m_cameraLookTarget;



    private void Awake()
    {
        m_cameraLookTarget =  GetComponent<CameraLookTarget>();
    }
    public void LookAt()
    {
        m_cameraLookTarget.LookAt(m_target.position,m_time);   
    }


    // ―――――――――――――――――――――――――――――――――――――
    // Gizmos
    // ―――――――――――――――――――――――――――――――――――――
    private void OnDrawGizmos()
    {
        if(m_target == null)
        {
            return;
        }


        Gizmos.color = Color.deepSkyBlue;
        Gizmos.DrawLine(transform.position,m_target.position);

        Gizmos.color = Color.skyBlue;
        Gizmos.DrawSphere(m_target.position, 0.5f);
    }
}
