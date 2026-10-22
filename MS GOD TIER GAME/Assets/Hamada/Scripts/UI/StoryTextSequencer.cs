using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;

public class StoryTextSequencer : MonoBehaviour
{
    [SerializeField] List<Image> m_storyImageList;

    int m_index = 0;

    Image m_currentText;

    public void ProccedText() 
    {
        m_index++;

        if (m_index > m_storyImageList.Count - 1) 
        {
            return;
        }

        m_currentText = m_storyImageList[m_index];
    }

    private IEnumerator ShowTextCoroutine(float fadeInTime, float Duration, float FadeOutTime) 
    {
        //フェードイン
        float elapsedTime = 0;

        while (elapsedTime > fadeInTime) 
        {
            float rate = elapsedTime / fadeInTime;

            rate = Mathf.Clamp01(rate);

            elapsedTime += Time.deltaTime;

            yield return null;
        }
    }
}
