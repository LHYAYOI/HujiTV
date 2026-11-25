using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class GimmickTimerEvent : MonoBehaviour
{
    [SerializeField] private float m_time;

    [SerializeField] private UnityEvent m_actions;


    public void OnStart()
    {
        StartCoroutine(TimeCoroutine());
    }

    private IEnumerator TimeCoroutine()
    {
        yield return new WaitForSeconds(m_time);

        m_actions?.Invoke();

        yield break;
    }

}
