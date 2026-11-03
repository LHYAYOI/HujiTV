using UnityEngine;

public class MagicBulletHitSender: HitSenderBase
{
    [SerializeField] GameObject m_parentObject;
    [SerializeField] MagicBulletController m_magicBulletController;

    protected override HitData CreateHitData()
    {
        HitData data = new HitData();

        data.SetHitObject(m_parentObject);
        data.SetMagicData(m_magicBulletController.GetMagicData());

        return data;
    }
}
