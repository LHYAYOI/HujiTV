using UnityEngine;

[CreateAssetMenu(fileName = "QuizData", menuName = "Scriptable Objects/QuizData")]
public class QuizData : ScriptableObject
{
    [SerializeField] MagicData m_correctData;
    [SerializeField] Sprite m_sprite;

    public MagicData GetCorrectData() 
    {
        return m_correctData;
    }

    public Sprite GetSprite() 
    {
        return m_sprite;
    }
}
