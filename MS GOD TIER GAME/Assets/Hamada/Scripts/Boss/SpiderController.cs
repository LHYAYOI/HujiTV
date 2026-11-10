using UnityEngine;

public class SpiderController : MonoBehaviour
{
    [SerializeField] StateMachine m_stateMachine;

    public void ChangeState(string stateName) 
    {
        m_stateMachine.ChangeState(stateName);
    }
}
