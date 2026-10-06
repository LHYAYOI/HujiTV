using UnityEngine;
using UnityEngine.Events;

public class GimmickTester : MonoBehaviour
{
    [SerializeField] private bool m_a;
    [SerializeField] private UnityEvent m_action;


    private void FixedUpdate()
    {
        if(m_a)
        {
            m_a = false;


            m_action?.Invoke();
        }
    }
}
