using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class TextTest : MonoBehaviour
{

    [SerializeField] GameObject m_lookObject;
    [SerializeField] TMP_Text m_text;

    [SerializeField] float radius = 1.5f;

    float m_time;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        transform.LookAt(m_lookObject.transform);

        m_time += Time.deltaTime;

        if (m_time > 0.05f)
        {
            ModifyMesh();
        }
    }

    private void ModifyMesh()
    {
        m_text.ForceMeshUpdate();

        TMP_TextInfo info = m_text.textInfo;

        TextMove(info);

        m_text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
    }

    private void TextMove(TMP_TextInfo textInfo) 
    {
        for (int c = 0; c < textInfo.characterCount; c++) 
        {
            TMP_CharacterInfo charInfo = textInfo.characterInfo[c];

            if (!charInfo.isVisible) 
            {
                continue;
            }

            int materialIndex = charInfo.materialReferenceIndex;

            int vertexIndex = charInfo.vertexIndex;

            //’¸“_À•W
            Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;

            float rad = Random.Range(0, 360) * Mathf.Deg2Rad;

            Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * radius;

            for (int i = 0; i < 4; i++) 
            {
                vertices[vertexIndex + i] += dir;
            }

        }
    }
}
