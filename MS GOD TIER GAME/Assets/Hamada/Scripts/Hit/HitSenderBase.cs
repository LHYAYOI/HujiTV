using UnityEngine;

public abstract class HitSenderBase : MonoBehaviour
{
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

    public void SendHit(GameObject target) 
    {
        HitReceiverController hitReceiver = GetHitReceiverController(target);

        if (hitReceiver == null)
        {
            return;
        }

        HitData hitData = CreateHitData();

        hitReceiver.OnHit(hitData);
    }

    public void SendStay(GameObject target)
    {
        HitReceiverController hitReceiver = GetHitReceiverController(target);

        if (hitReceiver == null)
        {
            return;
        }

        HitData hitData = CreateHitData();

        hitReceiver.OnStay(hitData);
    }

    public void SendExit(GameObject target)
    {
        HitReceiverController hitReceiver = GetHitReceiverController(target);

        if (hitReceiver == null)
        {
            return;
        }

        HitData hitData = CreateHitData();

        hitReceiver.OnStay(hitData);
    }

    public HitReceiverController GetHitReceiverController(GameObject target) 
    {
        HitReceiverController hitReceiver = target.GetComponent<HitReceiverController>();

        if (hitReceiver == null)
        {
            return null;
        }

        return hitReceiver;
    }

    protected abstract HitData CreateHitData();
}
