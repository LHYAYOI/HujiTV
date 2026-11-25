using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "QuizMasterData", menuName = "Scriptable Objects/QuizMasterData")]
public class QuizMasterData : ScriptableObject
{
    [SerializeField] List<QuizData> m_quizList;

    public QuizData GetRandomQuizData()
    {
        int randomIndex = Random.Range(0, m_quizList.Count);

        return m_quizList[randomIndex];
    }
}
