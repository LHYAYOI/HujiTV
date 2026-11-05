using UnityEngine;

public class MiniGhostHitSender : HitSenderBase
{
    protected override HitData CreateHitData()
    {
        HitData data = new HitData();

        data.SetHitObject(gameObject);

        return data;
    }
}
