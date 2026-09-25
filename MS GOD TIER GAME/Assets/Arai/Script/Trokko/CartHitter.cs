using UnityEngine;

public class CartHitter : MonoBehaviour
{
    private void OnCollisionEnter(Collision other)
    { 

        HitReceiverController hitReceiver = other.gameObject.GetComponent<HitReceiverController>();

        if (hitReceiver == null)
        {
            return;
        }

        HitData hitData = new HitData();

        hitData.SetHitObject(other.gameObject);

        hitReceiver.OnHit(hitData);
    }
}
