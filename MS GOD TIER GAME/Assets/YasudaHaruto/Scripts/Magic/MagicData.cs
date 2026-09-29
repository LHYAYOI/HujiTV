using UnityEngine;

[CreateAssetMenu(fileName = "MagicData", menuName = "Book/Magic Data")]
public class MagicData : ScriptableObject
{
    [SerializeField] private byte m_magicId;

    [SerializeField] private string m_displayName;

    public byte MagicId => m_magicId;
    public string DisplayName => m_displayName;
}