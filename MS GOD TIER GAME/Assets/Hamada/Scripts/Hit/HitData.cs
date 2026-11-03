using UnityEngine;

public class HitData
{
    float m_damage = 0;

    string m_tag = "None";

    MagicData m_magicData;

    GameObject m_hitObject = null;

    public void SetDamage(float damage)
    {
        m_damage = damage;
    }

    public void SetTag(string tag)
    {
        m_tag = tag;
    }

    public void SetMagicData(MagicData magicData)
    {
        m_magicData = magicData;
    }

    public void SetHitObject(GameObject hitObject)
    {
        m_hitObject = hitObject;
    }

    //ƒeƒ“ƒvƒŒ‚Åî•ñ‚ğ•Ô‚·
    public T GetHitObjectComponent<T>() where T : Component
    {
        if (m_hitObject == null) 
        {
            return null;
        }

        return m_hitObject.GetComponent<T>();
    }

    public float GetDamage => m_damage;

    public string GetTag => m_tag;

    public MagicData GetMagicData => m_magicData;

    public GameObject GetHitObject => m_hitObject;
}
