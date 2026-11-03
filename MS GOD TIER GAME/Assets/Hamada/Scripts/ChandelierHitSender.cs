using UnityEngine;

public class ChandelierHitSender : HitSenderBase
{
    [SerializeField] GameObject m_parentObject;

    protected override HitData CreateHitData()
    {
        HitData data = new HitData();

        data.SetHitObject(m_parentObject);

        return data;
    }
}
