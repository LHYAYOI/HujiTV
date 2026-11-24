using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "Stun", menuName = "Scriptable Objects/Poltergeist/Stun")]
public class Stun : StateBase
{
    [SerializeField] float m_stunDuration;

    float m_timer;

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
        Debug.Log("スタン");
    }

    public override void UpdateState()
    {
        m_timer += Time.deltaTime;

        if (m_timer >= m_stunDuration) 
        {
            m_controller.ChangeState("PreSelect");
        }
    }

    public override void ExitState()
    {

    }

    IEnumerator BringBackBodyPartsRoutine()
    {
        yield return m_controller.ResetBodyPartsPosition(2);
    }
}
