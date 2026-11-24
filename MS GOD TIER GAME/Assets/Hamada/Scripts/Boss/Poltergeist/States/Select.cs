using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "Select", menuName = "Scriptable Objects/Poltergeist/Select")]
public class Select : StateBase
{
    [SerializeField] float m_timeLimit;

    float m_timer = 0;

    PoltergeistController m_controller;

    public override void Awake()
    {
    }

    public override void Start()
    {
        m_controller = m_stateOwner.GetComponent<PoltergeistController>();
    }

    public override void EnterState()
    {
        m_timer = 0;
        m_controller.SelectStateCollidersActivateFlag(true);
    }

    public override void UpdateState()
    {
        m_timer += Time.deltaTime;

        if (m_timer >= m_timeLimit) 
        {
            m_controller.SelectStateCollidersActivateFlag(false);
            m_controller.ChangeState("Attack");
        }
    }

    public override void ExitState()
    {

    }
}
