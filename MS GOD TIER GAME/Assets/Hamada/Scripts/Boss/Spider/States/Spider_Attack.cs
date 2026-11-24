using UnityEngine;

[CreateAssetMenu(fileName = "Spider_Attack", menuName = "Scriptable Objects/Spider_Attack")]
public class Spider_Attack : StateBase
{
    SpiderController m_controller;

    public override void Awake()
    {

    }

    public override void Start()
    {
        m_controller = m_stateOwner.GetComponent<SpiderController>();
    }

    public override void EnterState()
    {
        m_controller.GetAnimator().SetTrigger("Attack");
    }

    public override void UpdateState()
    {
        //アニメーション終わったらアイドルに戻る
        if (m_controller.IsAnimationFinished("Attack")) 
        {
            Debug.Log("攻撃終わり");

            m_controller.ChangeState("Idle_State");
        }
    }

    public override void ExitState()
    {

    }
}
