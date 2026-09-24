using UnityEngine;

[CreateAssetMenu(fileName = "AttackData", menuName = "ScriptableObject/AttackObjects/AttackData")]
public class AttackData : ScriptableObject
{
    [SerializeField] MAGIC_TYPE m_magicType;

    [SerializeField] float m_damage;

    [SerializeField] float m_attackRange;

    public MAGIC_TYPE GetMagicType => m_magicType;

    public float GetDamage => m_damage;

    public float GetAttackRange => m_attackRange;
}
