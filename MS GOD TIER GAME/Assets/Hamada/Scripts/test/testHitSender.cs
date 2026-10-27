using UnityEngine;

public class testHitSender : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        SendHit(collision.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        SendHit(other.gameObject);
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

        hitData.SetHitObject(gameObject);
        hitData.SetTag(gameObject.tag);

        return hitData;
    }
}
