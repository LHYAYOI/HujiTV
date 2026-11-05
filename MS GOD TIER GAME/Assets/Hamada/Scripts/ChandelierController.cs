using hatake;
using System.Collections;
using UnityEngine;

public class ChandelierController : TargetableObject
{
    [SerializeField] ChandelierImpactEffect m_effect;
    [SerializeField] ChandelierBreakable m_chandelierBreakable;
    [SerializeField] ShakeObject m_shakeObject;
    [SerializeField] Rigidbody m_rigidbody;

    [SerializeField] float m_shakeIntensity = 1.0f;
    [SerializeField] float m_shakeDuration = 1.0f;

    private void Start()
    {
        m_chandelierBreakable.SetEffect(m_effect);
    }

    public void HitProcess(HitData data) 
    {
        if (data.GetHitObject.CompareTag("MagicBullet")) 
        {
            StartCoroutine(FallRoutine());
        }
    }

    private IEnumerator FallRoutine() 
    {
        yield return m_shakeObject.ShakeCoroutine(m_shakeDuration, m_shakeIntensity);

        m_rigidbody.isKinematic = false;
        m_rigidbody.useGravity = true;

        m_chandelierBreakable.Drop();
    }
}
