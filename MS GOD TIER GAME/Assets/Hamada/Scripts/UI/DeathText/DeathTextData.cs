using UnityEngine;

[CreateAssetMenu(fileName = "DeathTextData", menuName = "Scriptable Objects/DeathTextData")]
public class DeathTextData : ScriptableObject
{
    [SerializeField] string m_Text;

    public string GetDeathText() 
    {
        return m_Text;
    }
}
