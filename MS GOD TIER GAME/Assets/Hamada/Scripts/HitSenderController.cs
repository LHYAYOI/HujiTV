using Unity.VisualScripting;
using UnityEngine;

public class BulletHitSenderController : MonoBehaviour
{
    [SerializeField] MagicBulletController m_magicBulletController;

    private void OnCollisionEnter(Collision collision)
    {
        SendHit(collision.gameObject);
    }

    private void OnCollisionStay(Collision collision)
    {
        SendStay(collision.gameObject);
    }

    private void OnCollisionExit(Collision collision)
    {
        SendExit(collision.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        SendHit(other.gameObject);
    }

    private void OnTriggerStay(Collider other)
    {
        SendStay(other.gameObject);
    }

    private void OnTriggerExit(Collider other)
    {
        SendExit(other.gameObject);
    }

    private void SendHit(GameObject target) 
    {
        HitReceiverController hitReceiver = GetHitReceiverController(target);

        if (hitReceiver == null)
        {
            return;
        }

        HitData hitData = CreateHitData();

        hitReceiver.OnHit(hitData);
    }

    private void SendStay(GameObject target)
    {
        HitReceiverController hitReceiver = GetHitReceiverController(target);

        if (hitReceiver == null)
        {
            return;
        }

        HitData hitData = CreateHitData();

        hitReceiver.OnStay(hitData);
    }

    private void SendExit(GameObject target)
    {
        HitReceiverController hitReceiver = GetHitReceiverController(target);

        if (hitReceiver == null)
        {
            return;
        }

        HitData hitData = CreateHitData();

        hitReceiver.OnStay(hitData);
    }

    private HitReceiverController GetHitReceiverController(GameObject target) 
    {
        HitReceiverController hitReceiver = target.GetComponent<HitReceiverController>();

        if (hitReceiver == null)
        {
            return null;
        }

        return hitReceiver;
    }

    private HitData CreateHitData() 
    {
        HitData hitData = new HitData();

        AttackData attackData = m_magicBulletController.GetAttackData();

        hitData.SetDamage(attackData.GetDamage);
        hitData.SetMagicType(attackData.GetMagicType);
        hitData.SetTag(gameObject.tag);

        return hitData;
    }
}
