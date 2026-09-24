using UnityEngine;
using UnityEngine.Events;

public class HitReceiverController : MonoBehaviour,IHitable
{
    [SerializeField] private UnityEvent<HitData> m_onHit;
    [SerializeField] private UnityEvent<HitData> m_onStay;
    [SerializeField] private UnityEvent<HitData> m_onExit;

    public void OnHit(HitData hitData)
    {
        m_onHit?.Invoke(hitData);
    }

    public void OnStay(HitData hitData)
    {
        m_onStay?.Invoke(hitData);
    }

    public void OnExit(HitData hitData)
    {
        m_onExit?.Invoke(hitData);
    }
}
