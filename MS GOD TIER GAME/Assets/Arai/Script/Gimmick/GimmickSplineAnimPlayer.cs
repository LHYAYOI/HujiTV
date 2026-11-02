using UnityEngine;

public class GimmickSplineAnimPlayer : GimmickSpline
{

    // ―――――――――――――――――――――――――――――――――――――
    // Propaty
    // ―――――――――――――――――――――――――――――――――――――
    [SerializeField] private Animator m_animator;

    [SerializeField] private string m_triggerName;
    

    // ―――――――――――――――――――――――――――――――――――――
    // Public Event
    // ―――――――――――――――――――――――――――――――――――――  
    public void Play()
    {
        m_animator.SetTrigger(m_triggerName);
    }

    // ―――――――――――――――――――――――――――――――――――――
    // Gizmos
    // ―――――――――――――――――――――――――――――――――――――
    private void OnDrawGizmos()
    {

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(transform.position, 0.1f);
        
        if(m_animator != null)
        {
            Vector3 to = m_animator.transform.position;
            Gizmos.DrawLine(transform.position, to);

            Gizmos.DrawSphere(to, 0.08f);
        }

    }
}
