using hatake;
using System.Collections;
using UnityEngine;

public class ChandelierController : MonoBehaviour
{
    [SerializeField] ChandelierImpactEffect m_effect;
    [SerializeField] ChandelierBreakable m_chandelierBreakable;
    [SerializeField] ShakeObject m_shakeObject;
    [SerializeField] Rigidbody m_rigidbody;

    private void Start()
    {
        m_chandelierBreakable.SetEffect(m_effect);
    }

    public void HitProcess(HitData data) 
    {
        if (data.GetHitObject.CompareTag("MagicBullet")) 
        {
           // StartCoroutine(FallRoutine());

            m_rigidbody.isKinematic = false;
            m_rigidbody.useGravity = true;

            m_chandelierBreakable.Drop();
        }
    }

    private IEnumerator FallRoutine() 
    {
        yield return m_shakeObject.ShakeCoroutine(0.5f, 1.0f);

        m_rigidbody.isKinematic = false;
        m_rigidbody.useGravity = true;

        m_chandelierBreakable.Drop();
    }
}
