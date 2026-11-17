using UnityEngine;

[CreateAssetMenu(fileName = "Spider_Damageable", menuName = "Scriptable Objects/Spider_Damageable")]
public class Spider_Damageable : StateBase
{
    [SerializeField] float m_damageableDuration = 10;
    SpiderController m_controller;

    float m_damageableTime;

    public override void Awake()
    {
    }

    public override void Start()
    {
        m_controller = m_stateOwner.GetComponent<SpiderController>();
    }

    public override void EnterState()
    {
        m_damageableTime = 0;
        m_controller.GetAnimator().SetTrigger("Idle");
        m_controller.SetCollidersTargetableFlag(true);
    }

    public override void UpdateState()
    {
        m_damageableTime += Time.deltaTime;

        if (m_damageableTime >= m_damageableDuration) 
        {
            m_controller.SetCollidersTargetableFlag(false);

            m_controller.ChangeState("Idle");
        }
    }


    public override void ExitState()
    {
    }
}
