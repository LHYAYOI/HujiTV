using UnityEngine;

public class testHitSender : HitSenderBase
{
    protected override HitData CreateHitData()
    {
        HitData data = new HitData();

        data.SetHitObject(gameObject);

        return data;
    }
}
