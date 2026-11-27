using UnityEngine;

public class GimmickStoryText : MonoBehaviour
{
    [SerializeField] StoryTextController m_textController;

    public void OnEnter()
    {
        m_textController.Appear();
    }

    public void OnExit()
    {
        m_textController.Disappear();
    }
}
