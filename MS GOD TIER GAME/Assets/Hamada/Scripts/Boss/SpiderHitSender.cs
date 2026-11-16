using UnityEngine;

public class SpiderHitSender : HitSenderBase
{
    protected override HitData CreateHitData()
    {
        HitData hitData = new HitData();

        hitData.SetHitObject(gameObject);

        return hitData;
    }
}
