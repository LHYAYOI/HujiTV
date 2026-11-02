using UnityEngine;

public class GimmickSplineSetLookTarget : GimmickSpline
{

    // ―――――――――――――――――――――――――――――――――――――
    // Propaty
    // ―――――――――――――――――――――――――――――――――――――
    [SerializeField] private GameObject m_target;
    [SerializeField] private float m_duration = 0.5f;

    // ―――――――――――――――――――――――――――――――――――――
    // Public Event
    // ―――――――――――――――――――――――――――――――――――――  
    public void SetLookTarget()
    {
        CameraManager.Instance.SetLookTarget(m_target,m_duration);
    }


    // ―――――――――――――――――――――――――――――――――――――
    // Gizmos
    // ―――――――――――――――――――――――――――――――――――――
    private void OnDrawGizmos()
    {
        OnGizmosMyPosition();

        if (m_target == null)
        {
            return;
        }

        Gizmos.color = Color.skyBlue;
        Gizmos.DrawLine(transform.position, m_target.transform.position);

    }
}
