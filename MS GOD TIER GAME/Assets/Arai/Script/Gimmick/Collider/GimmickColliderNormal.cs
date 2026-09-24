
using UnityEngine;

public class GimmickColliderNormal : GimmickCollider
{
    private void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.layer != m_targetLayer)
        {
            return;
        }

        OnEnter?.Invoke(collision);
    }
    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.layer != m_targetLayer)
        {
            return;
        }

        OnExit?.Invoke(collision); 
    }
    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.layer != m_targetLayer)
        {
            return;
        }

        OnStay?.Invoke(collision);
    }
}
