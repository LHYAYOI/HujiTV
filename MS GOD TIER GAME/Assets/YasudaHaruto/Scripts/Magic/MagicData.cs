
using UnityEngine;

[CreateAssetMenu(fileName = "MagicData", menuName = "Book/Magic Data")]
public class MagicData : ScriptableObject
{
    [SerializeField] private byte m_magicId;

    [SerializeField] private string m_displayName;

    [SerializeField] private MAGIC_TYPE m_magicType;

    [SerializeField] private MAGIC_AIM_TYPE m_aimType = MAGIC_AIM_TYPE.THREE_DIRECTIONS;


    public byte MagicId => m_magicId;
    public string DisplayName => m_displayName;
    public MAGIC_TYPE MagicType => m_magicType;
    public MAGIC_AIM_TYPE AimType => m_aimType;
}