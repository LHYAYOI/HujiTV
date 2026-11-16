using TMPro;
using UnityEngine;
using System.Collections;

public class DeathTextController : MonoBehaviour
{
    [SerializeField] TMP_Text m_text;
    [SerializeField] TextEffectController m_textEffectController;

    [SerializeField] float m_disappearTime;

    bool m_isUsing = false;

    public void SetUsingFlag(bool flag) 
    {
        m_isUsing = flag;
    }

    public bool IsUsing() 
    {
        return m_isUsing;
    }

    public void Appear(DeathTextData data)
    {
        m_text.text = data.GetDeathText();

        gameObject.transform.position = data.GetSpawnPosition();

        Color color = m_text.color;
        m_text.color = new Color(color.r, color.g, color.b, 1);

        m_textEffectController.Play();
    }

    public void Disappear()
    {
        StartCoroutine(DisappearCoroutine());
    }

    private IEnumerator DisappearCoroutine()
    {
        float elapsedTime = 0;

        while (elapsedTime < m_disappearTime)
        {
            elapsedTime += Time.deltaTime;

            float rate = elapsedTime / m_disappearTime;

            Color color = m_text.color;

            m_text.color = new Color(color.r, color.g, color.b, 1 - rate);

            yield return null;
        }

        SetUsingFlag(false);
    }
}
