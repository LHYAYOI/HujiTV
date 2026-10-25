using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;

public class StoryTextSequencer : MonoBehaviour
{
    [SerializeField] List<StoryTextController> m_storyImageList;

    int m_index = 0;

    StoryTextController m_currentText;

    private void Start()
    {
        m_currentText = m_storyImageList[m_index];
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
        {
            AppearText();
        }

        if (Input.GetKeyDown(KeyCode.O))
        {
            DisappearText();
        }

        if (Input.GetKeyDown(KeyCode.U)) 
        {
            ProccedText();
        }
    }

    private void AppearText() 
    {
        m_currentText.Appear();
    }

    private void DisappearText()
    {
        m_currentText.Disappear();
    }

    public void ProccedText() 
    {
        m_index++;

        if (m_index > m_storyImageList.Count - 1) 
        {
            return;
        }

        m_currentText = m_storyImageList[m_index];
    }

    
}
