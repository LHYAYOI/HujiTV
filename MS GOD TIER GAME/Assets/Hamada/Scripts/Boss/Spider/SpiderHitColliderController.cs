using UnityEngine;

public class SpiderHitColliderController : MonoBehaviour
{
    [SerializeField] TargetableObject m_targetObject;

    SpiderController m_spiderController;

    public void SetSpiderController(SpiderController controller) 
    {
        m_spiderController = controller;
    }

    public void DisableHitCollider() 
    {
        SetTargetable(false);

        gameObject.SetActive(false);

        m_spiderController.CheckIfDead();
    }

    public void SetTargetable(bool flag) 
    {
        m_targetObject.SetTargetable(flag);
    }
}
