using System.Collections;
using TMPro;
using UnityEngine;

public class StoryTextController : MonoBehaviour
{
    [SerializeField] TMP_Text m_text;
    [SerializeField] TextEffectController m_textEffectController;
    [SerializeField] float disappearTime = 0.5f;

    public void Appear() 
    {
        m_textEffectController.Play();
    }

    public void Disappear() 
    {
        StartCoroutine(DisappearCoroutine());
    }

    private IEnumerator DisappearCoroutine() 
    {
        float elapsedTime = 0;

        while (elapsedTime < disappearTime) 
        {
            elapsedTime += Time.deltaTime;

            float rate = elapsedTime / disappearTime;

            Color color = m_text.color;

            m_text.color = new Color(color.r, color.g, color.b, 1 - rate);

            yield return null;
        }
    }
}
