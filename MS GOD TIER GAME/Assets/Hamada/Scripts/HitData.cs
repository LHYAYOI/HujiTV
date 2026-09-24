using UnityEngine;

public class HitData
{
    float m_damage = 0;

    string m_tag = "None";

    MAGIC_TYPE m_magicType = MAGIC_TYPE.FIRE;

    public void SetDamage(float damage)
    {
        m_damage = damage;
    }

    public void SetTag(string tag)
    {
        m_tag = tag;
    }

    public void SetMagicType(MAGIC_TYPE magicType)
    {
        m_magicType = magicType;
    }

    public float GetDamage => m_damage;

    public string GetTag => m_tag;

    public MAGIC_TYPE GetMagicType => m_magicType;

}
