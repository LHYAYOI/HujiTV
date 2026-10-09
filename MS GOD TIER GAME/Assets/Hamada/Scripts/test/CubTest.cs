using UnityEngine;

public class CubTest : MonoBehaviour
{
    [SerializeField] MagicData m_attackData;

    private void OnCollisionEnter(Collision other)
    {
        HitReceiverController hitReceiver = other.gameObject.GetComponent<HitReceiverController>();

        if (hitReceiver == null) 
        {
            return;
        }

        HitData hitData = new HitData();

        hitData.SetDamage(m_attackData.GetDamage);
        hitData.SetMagicType(m_attackData.MagicType);
        hitData.SetTag(gameObject.tag);

        hitReceiver.OnHit(hitData);
    }
}
