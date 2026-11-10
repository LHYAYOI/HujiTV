using UnityEngine;

public class ChestDrawerHit : HitSenderBase
{
    protected override HitData CreateHitData()
    {
        HitData hitData = new HitData();

        hitData.SetHitObject(this.gameObject);

        return hitData;
    }
}
