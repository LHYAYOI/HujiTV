using UnityEngine;

public class GimmickColliderSpline : GimmickCollider
{
    [SerializeField] private SplineController2 m_splineController;
    [SerializeField] private float m_startPos = 0.0f;
    [SerializeField] private float m_endPos = 1.0f;

    private Collider m_startCollider;
    private Collider m_endCollider;



#if UNITY_EDITOR
    private void OnValidate()
    {
        if(m_startCollider == null)
        {

        }
    }
#endif
    private void Awake()
    {
        
    }


    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.layer != m_targetLayer)
        {
            return;
        }

        OnEnter?.Invoke(collision);
    }
}
