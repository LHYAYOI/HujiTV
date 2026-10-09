using UnityEngine;

[CreateAssetMenu(fileName = "MagicData", menuName = "Book/Magic Data")]
public class MagicData : ScriptableObject
{
    [SerializeField] private byte m_magicId;

    [SerializeField] private string m_displayName;

    [SerializeField] private MAGIC_TYPE m_magicType;

    [SerializeField] float m_damage;

    [SerializeField] float m_attackRange;

    public float GetDamage => m_damage;

    public float GetAttackRange => m_attackRange;

    public byte MagicId => m_magicId;

    public string DisplayName => m_displayName;

    public MAGIC_TYPE MagicType => m_magicType;
}