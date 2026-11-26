using UnityEngine;

public class SimpleHitSender : HitSenderBase
{
    protected override HitData CreateHitData()
    {
        HitData data = new HitData();

        data.SetHitObject(gameObject);

        return data;
    }
}
