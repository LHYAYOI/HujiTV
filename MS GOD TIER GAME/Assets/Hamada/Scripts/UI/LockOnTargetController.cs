using UnityEngine;
using UnityEngine.UI;

public class LockOnTargetController : MonoBehaviour
{
    [SerializeField] Image m_targetImage;

    [SerializeField] private StateMachine m_stateMachine;

    GameObject m_target;

    //入りのプロパティ

    //ロック中のプロパティ

    //解除のプロパティ

    private void Update()
    {
        //ターゲットを追従
    }

    public void ChangeState(string stateName)
    {
        if (m_stateMachine == null)
        {
            return;
        }

        m_stateMachine.ChangeState(stateName);
    }

    public void SetTarget(GameObject target)
    {
        m_target = target;
    }


}
