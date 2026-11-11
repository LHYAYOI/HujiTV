using UnityEngine;

[CreateAssetMenu(fileName = "Spider_Idle", menuName = "Scriptable Objects/Spider_Idle")]
public class Spider_Idle : StateBase
{
    [SerializeField] float m_doAttackTime = 10;

    float m_AttackTime;

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
        m_AttackTime = 0;
        m_controller.GetAnimator().SetTrigger("Idle");
    }

    public override void UpdateState()
    {
        m_AttackTime += Time.deltaTime;

        if (m_AttackTime >= m_doAttackTime) 
        {
            m_controller.ChangeState("Attack_State");
        }
    }

    public override void ExitState()
    {

    }
}
