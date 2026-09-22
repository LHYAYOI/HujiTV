using UnityEngine;

public class HitData
{
    float m_damage;

    MAGIC_TYPE m_magicType;

    public void SetDamage(float damage)
    {
        m_damage = damage;
    }

    public void SetMagicType(MAGIC_TYPE magicType)
    {
        m_magicType = magicType;
    }

    public float GetDamage => m_damage;

    public MAGIC_TYPE GetMagicType => m_magicType;

}
