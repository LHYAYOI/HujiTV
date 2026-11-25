using UnityEngine;

public class QuizController : MonoBehaviour
{
    [SerializeField] QuizData m_quizData;
    [SerializeField] PoltergeistController m_poltergeistController;
    [SerializeField] TargetableObject m_target;

    public void SetQuizData(QuizData data) 
    {
        m_quizData = data;
    }

    public void HitProcess(HitData data)
    {
        if (m_quizData == null) 
        {
            return;
        }

        if (data.GetMagicData != m_quizData.GetCorrectData()) 
        {
            return;
        }

        m_target.SetTargetable(false);

        m_poltergeistController.CheckIfIsAllPictureFrameAttacked();

        m_target.gameObject.SetActive(false);
    }
}
