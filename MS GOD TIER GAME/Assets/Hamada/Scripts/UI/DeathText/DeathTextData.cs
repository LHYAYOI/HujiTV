using UnityEngine;

[CreateAssetMenu(fileName = "DeathTextData", menuName = "Scriptable Objects/DeathTextData")]
public class DeathTextData : ScriptableObject
{
    [SerializeField] string m_Text;

    Vector3 m_SpawnPosition;

    Vector2 m_textScale;

    public void SetSpawnPosition(Vector3 position) 
    {
        m_SpawnPosition = position;
    }

    public void SetTextScale(Vector2 scale) 
    {
        m_textScale = scale;
    }

    public Vector2 GetTextScale() 
    {
        return m_textScale;
    }

    public Vector3 GetSpawnPosition() 
    {
        return m_SpawnPosition;
    }

    public string GetDeathText() 
    {
        return m_Text;
    }
}
